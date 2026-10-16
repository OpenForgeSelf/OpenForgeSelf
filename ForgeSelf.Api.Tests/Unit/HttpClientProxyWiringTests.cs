using System.Net.Http;
using System.Reflection;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Services.Net;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// 接线测试（2026-10-16-settings-network-proxy AC-2）：验证 AppBuilder.ConfigureModelProxyHttpClient
/// 生产的 IHttpClientFactory 默认 primary handler 挂了 ModelProxy——模型请求出口 M1~M5
/// （typed AIService / OpenAICompatibleProvider / AIModelService / AIProviderService / AIProviderController）
/// 全部经该默认管线，一处接线全覆盖。
/// </summary>
public class HttpClientProxyWiringTests
{
    private sealed class StubEncryption : ISecretEncryptionService
    {
        public string Encrypt(string plainText) => plainText;
        public string Decrypt(string cipherText) => cipherText;
        public bool TryDecrypt(string cipherText, out string plainText) { plainText = cipherText; return true; }
        public string Mask(string plainText) => "****";
    }

    [Fact]
    public void 默认client的primary_handler使用ModelProxy()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient();
        services.AddSingleton<ISecretEncryptionService, StubEncryption>();
        AppBuilder.ConfigureModelProxyHttpClient(services);

        using var sp = services.BuildServiceProvider();
        var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient();

        // 不依赖 BCL 内部字段名：沿继承链（handler 实际存在基类 HttpMessageInvoker 的私有字段）
        // 遍历实例字段找到消息管线 handler，再沿 InnerHandler 链往下找 SocketsHttpHandler
        var pipeline = EnumerateHandlerFields(client)
            .Select(FindSocketsHandler)
            .FirstOrDefault(static h => h != null);

        pipeline.Should().NotBeNull("未能从 HttpClient 管线中找到 SocketsHttpHandler（BCL 内部结构变更时同步更新本测试，防止接线验证静默失效）");
        pipeline!.Proxy.Should().BeOfType<ModelProxy>("所有 factory client 默认走实时代理决策");
    }

    /// <summary>含基类地遍历实例字段，取所有 HttpMessageHandler 类型的字段值（HttpClient 自身不持有 handler，存在 HttpMessageInvoker._handler）</summary>
    private static IEnumerable<HttpMessageHandler> EnumerateHandlerFields(object client)
    {
        for (var t = client.GetType(); t is not null && t != typeof(object); t = t.BaseType)
        {
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (f.GetValue(client) is HttpMessageHandler h)
                {
                    yield return h;
                }
            }
        }
    }

    /// <summary>沿 InnerHandler 链向下查找终级 SocketsHttpHandler（反射读属性，避开仓内 NewLife 同名类型干扰）</summary>
    private static SocketsHttpHandler? FindSocketsHandler(HttpMessageHandler? handler)
    {
        while (handler != null)
        {
            if (handler is SocketsHttpHandler s) return s;
            handler = (handler as DelegatingHandler)?.InnerHandler
                ?? handler.GetType().GetProperty("InnerHandler")?.GetValue(handler) as HttpMessageHandler;
        }
        return null;
    }

    [Fact]
    public void ModelProxy注册为单例且可从容器解析()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient();
        services.AddSingleton<ISecretEncryptionService, StubEncryption>();
        AppBuilder.ConfigureModelProxyHttpClient(services);

        using var sp = services.BuildServiceProvider();
        var a = sp.GetRequiredService<ModelProxy>();
        var b = sp.GetRequiredService<ModelProxy>();
        a.Should().BeSameAs(b, "单例：handler 缓存与实时配置读取解耦");
    }
}
