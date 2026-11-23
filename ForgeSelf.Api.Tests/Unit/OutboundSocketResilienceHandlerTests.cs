using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using ForgeSelf.Api.Services.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// 出站 10013 自愈层（OutboundSocketResilienceHandler）韧性/wiring 测试（2026-11-21 输入15）。
/// 根因场景：全机动态端口段撞 WinNAT 保留段 → connect 抛 SocketException(AccessDenied)=10013。
/// </summary>
public class OutboundSocketResilienceHandlerTests
{
    private const string DiagKeyword = "net stop winnat";

    // ────────────── 接线（wiring）：ConfigureModelProxyHttpClient 装的是自愈层 ──────────────

    [Fact]
    public void 默认client管线_首层为自愈层且终级handler仍挂ModelProxy()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient();
        services.AddSingleton<ForgeSelf.Abstractions.ISecretEncryptionService, ProxyWiringStubEncryption>();
        AppBuilder.ConfigureModelProxyHttpClient(services);

        using var sp = services.BuildServiceProvider();
        var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient();

        // 沿管线找终级 SocketsHttpHandler（同 HttpClientProxyWiringTests 手法，不依赖 BCL 内部字段名）
        var chain = EnumerateHandlerFields(client).SelectMany(WalkChain).ToList();
        var sockets = chain.OfType<SocketsHttpHandler>().FirstOrDefault();
        sockets.Should().NotBeNull("未能在管线中找到 SocketsHttpHandler（BCL 内部结构变更时同步更新本测试）");
        sockets!.Proxy.Should().BeOfType<ModelProxy>("自愈层不得改变代理语义：终级 handler 仍持有 ModelProxy");

        // 管线首层（primary）= OutboundSocketResilienceHandler
        chain.OfType<ForgeSelf.Api.Services.Net.OutboundSocketResilienceHandler>().Should().NotBeEmpty(
            "ConfigureHttpClientDefaults 的 primary handler 应为 10013 自愈层（wiring 断言防接线被静默移除）");
    }

    /// <summary>沿 InnerHandler 链展开完整管线（factory 会在 primary 外再包日志/作用域 handler）。</summary>
    private static IEnumerable<System.Net.Http.HttpMessageHandler> WalkChain(System.Net.Http.HttpMessageHandler? handler)
    {
        while (handler != null)
        {
            yield return handler;
            handler = (handler as DelegatingHandler)?.InnerHandler
                ?? handler.GetType().GetProperty("InnerHandler")?.GetValue(handler) as System.Net.Http.HttpMessageHandler;
        }
    }

    private sealed class ProxyWiringStubEncryption : ForgeSelf.Abstractions.ISecretEncryptionService
    {
        public string Encrypt(string plainText) => plainText;
        public string Decrypt(string cipherText) => cipherText;
        public bool TryDecrypt(string cipherText, out string plainText) { plainText = cipherText; return true; }
        public string Mask(string plainText) => "****";
    }

    private static IEnumerable<System.Net.Http.HttpMessageHandler> EnumerateHandlerFields(object client)
    {
        // 含基类地遍历实例字段，取所有 HttpMessageHandler 类型字段值（HttpClient 自身不持有 handler，
        // 存在基类 HttpMessageInvoker 的私有字段——与 HttpClientProxyWiringTests 同手法）
        for (var t = client.GetType(); t is not null && t != typeof(object); t = t.BaseType)
        {
            foreach (var f in t.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
            {
                if (f.GetValue(client) is System.Net.Http.HttpMessageHandler h)
                {
                    yield return h;
                }
            }
        }
    }

    // ────────────── AccessDenied 有界重试 ──────────────

    private static HttpRequestException AccessDenied() => new(
        "socket error",
        new SocketException((int)SocketError.AccessDenied)); // WSAEACCES = 10013

    private sealed class FlakyHandler : HttpMessageHandler
    {
        private readonly Func<int, HttpRequestException?> _failFor;
        public int Attempts { get; private set; }

        public FlakyHandler(Func<int, HttpRequestException?> failFor) => _failFor = failFor;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Attempts++;
            var fail = _failFor(Attempts);
            if (fail != null) throw fail;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    [Fact]
    public async Task AccessDenied_前几次失败_有界重试后成功()
    {
        var flaky = new FlakyHandler(attempt => attempt < 3 ? AccessDenied() : null);
        var sut = new ForgeSelf.Api.Services.Net.OutboundSocketResilienceHandler(NullLogger<ForgeSelf.Api.Services.Net.OutboundSocketResilienceHandler>.Instance)
        {
            InnerHandler = flaky,
        };
        using var client = new HttpClient(sut);

        var resp = await client.GetAsync("http://gpustack.example/v1/models");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        flaky.Attempts.Should().Be(3, "第 3 次成功，前 2 次 AccessDenied 均被有界重试吸收");
    }

    [Fact]
    public async Task AccessDenied_持续失败_耗尽后抛带处置指引的中文诊断()
    {
        var flaky = new FlakyHandler(_ => AccessDenied());
        var sut = new ForgeSelf.Api.Services.Net.OutboundSocketResilienceHandler(NullLogger<ForgeSelf.Api.Services.Net.OutboundSocketResilienceHandler>.Instance)
        {
            InnerHandler = flaky,
        };
        using var client = new HttpClient(sut);

        var act = async () => await client.GetAsync("http://gpustack.example/v1/models");

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*10013*")
            .WithMessage($"*{DiagKeyword}*");
        flaky.Attempts.Should().Be(6, "MaxAttempts=6 耗尽后不再重试");
    }

    [Fact]
    public async Task 非AccessDenied的Socket异常_立即透传不重试()
    {
        var flaky = new FlakyHandler(_ => new HttpRequestException(
            "timeout", new SocketException((int)SocketError.TimedOut)));
        var sut = new ForgeSelf.Api.Services.Net.OutboundSocketResilienceHandler(NullLogger<ForgeSelf.Api.Services.Net.OutboundSocketResilienceHandler>.Instance)
        {
            InnerHandler = flaky,
        };
        using var client = new HttpClient(sut);

        var act = async () => await client.GetAsync("http://gpustack.example/v1/models");

        await act.Should().ThrowAsync<HttpRequestException>();
        flaky.Attempts.Should().Be(1, "只有 AccessDenied（10013）才进重试，其他异常立即透传");
    }

    [Fact]
    public async Task IsAccessDenied_嵌套链深层命中_也能识别()
    {
        var flaky = new FlakyHandler(_ => new HttpRequestException(
            "outer",
            new InvalidOperationException("mid", new SocketException((int)SocketError.AccessDenied))));
        var sut = new ForgeSelf.Api.Services.Net.OutboundSocketResilienceHandler(NullLogger<ForgeSelf.Api.Services.Net.OutboundSocketResilienceHandler>.Instance)
        {
            InnerHandler = flaky,
        };
        using var client = new HttpClient(sut);

        var act = async () => await client.GetAsync("http://gpustack.example/v1/models");

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage($"*{DiagKeyword}*");
        flaky.Attempts.Should().Be(6, "异常链深层的 AccessDenied 同样识别并走满重试");
    }
}
