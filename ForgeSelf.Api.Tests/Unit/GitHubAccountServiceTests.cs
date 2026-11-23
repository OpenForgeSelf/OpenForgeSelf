using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ForgeSelf.Api.Services;
using ForgeSelf.Abstractions;
using FluentAssertions;
using Xunit;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>GitHub 设备流服务单测（PILOT-083）：fake HttpHandler 零外联；预填/失败透传/协议语义不抛错。</summary>
public class GitHubAccountServiceTests
{
    private const string ClientId = "Ov23likdtDN2AlQvAO33";

    #region 基建

    private sealed class FakeEncryption : ISecretEncryptionService
    {
        public string Encrypt(string plainText) => "v2:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
        public string Decrypt(string cipherText)
        {
            if (!cipherText.StartsWith("v2:")) throw new System.Security.Cryptography.CryptographicException("bad cipher");
            return Encoding.UTF8.GetString(Convert.FromBase64String(cipherText[3..]));
        }
        public bool TryDecrypt(string cipherText, out string plainText)
        {
            try { plainText = Decrypt(cipherText); return true; }
            catch { plainText = ""; return false; }
        }
        public string Mask(string plainText) => "****";
    }

    /// <summary>按路径→响应体路由的 fake handler；记录全部请求。</summary>
    private sealed class FakeHandler : HttpMessageHandler
    {
        public List<(string Url, string? Authorization)> Requests { get; } = new();
        private readonly Dictionary<string, (int Status, string Body)> _routes;

        public FakeHandler(Dictionary<string, (int, string)> routes) => _routes = routes;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            Requests.Add((url, request.Headers.Authorization?.ToString()));
            var (status, body) = _routes.TryGetValue(url, out var r) ? r : (404, "{\"error\":\"not_found\"}");
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static GitHubAccountService BuildService(FakeHandler handler, string clientId = ClientId)
        => new(
            new GitHubOAuthConfig { ClientId = clientId },
            new GitHubBindingStore(new FakeEncryption()),
            new HttpClient(handler));

    private static readonly string DeviceUrl = "https://github.com/login/device/code";
    private static readonly string TokenUrl = "https://github.com/login/oauth/access_token";

    private const string DeviceBody = """
        {"device_code":"DEV123","user_code":"ABCD-1234","verification_uri":"https://github.com/login/device","expires_in":900,"interval":5}
        """;

    #endregion

    #region 申请设备码

    [Fact]
    public async Task StartAsync_Success_ParsesDeviceCodeFields()
    {
        var svc = BuildService(new FakeHandler(new() { [DeviceUrl] = (200, DeviceBody) }));
        var start = await svc.StartAsync();
        start.DeviceCode.Should().Be("DEV123");
        start.UserCode.Should().Be("ABCD-1234");
        start.ExpiresIn.Should().Be(900);
        start.Interval.Should().Be(5);
    }

    [Fact]
    public async Task StartAsync_Prefill_UsesQuestionMark_OnCleanUri()
    {
        var svc = BuildService(new FakeHandler(new() { [DeviceUrl] = (200, DeviceBody) }));
        var start = await svc.StartAsync();
        start.VerificationUriPrefilled.Should().Be("https://github.com/login/device?user_code=ABCD-1234");
    }

    [Fact]
    public void BuildVerificationUri_UsesAmpersand_WhenUriAlreadyHasQuery()
    {
        GitHubAccountService.BuildVerificationUri("https://github.com/login/device?a=1", "XYZ").Should()
            .Be("https://github.com/login/device?a=1&user_code=XYZ");
    }

    [Fact]
    public void BuildVerificationUri_EscapesReservedCharacters()
    {
        GitHubAccountService.BuildVerificationUri("https://github.com/login/device", "A B&C").Should()
            .Be("https://github.com/login/device?user_code=A%20B%26C");
    }

    [Fact]
    public void BuildVerificationUri_EmptyUserCode_ReturnsBaseUnchanged()
    {
        GitHubAccountService.BuildVerificationUri("https://github.com/login/device", "").Should()
            .Be("https://github.com/login/device");
    }

    [Fact]
    public async Task StartAsync_NotConfigured_ThrowsWithClearReason()
    {
        var svc = new GitHubAccountService(new GitHubOAuthConfig { ClientId = "" },
            new GitHubBindingStore(new FakeEncryption()), new HttpClient(new FakeHandler(new())));
        var act = () => svc.StartAsync();
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ClientId*");
    }

    #endregion

    #region 失败原因透传（DescribeStartFailure）

    [Fact]
    public async Task StartAsync_UpstreamError_SurfacesErrorDescription_NotGenericMessage()
    {
        var svc = BuildService(new FakeHandler(new()
        {
            [DeviceUrl] = (401, """{"error":"incorrect_client_credentials","error_description":"The client_id is not valid."}"""),
        }));
        var act = () => svc.StartAsync();
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*client_id is not valid*");
    }

    [Fact]
    public void DescribeStartFailure_MergesErrorAndDescription()
    {
        var msg = GitHubAccountService.DescribeStartFailure(
            """{"error":"bad_param","error_description":"参数不合法"}""");
        msg.Should().Be("参数不合法 · bad_param");
    }

    [Fact]
    public void DescribeStartFailure_TruncatesAt200Chars()
    {
        var longBody = new string('x', 500);
        GitHubAccountService.DescribeStartFailure(longBody).Length.Should().Be(200);
    }

    [Fact]
    public void DescribeStartFailure_NonJsonBody_PassesThroughTrimmed()
    {
        GitHubAccountService.DescribeStartFailure("  plain text failure  ").Should().Be("plain text failure");
    }

    [Fact]
    public void DescribeStartFailure_EmptyBody_ReturnsFallback()
    {
        GitHubAccountService.DescribeStartFailure("").Should().NotBeNullOrEmpty();
        GitHubAccountService.DescribeStartFailure(null).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void DescribeStartFailure_InvalidJson_FallsBackToBody()
    {
        GitHubAccountService.DescribeStartFailure("{not json").Should().Be("{not json");
    }

    #endregion

    #region 轮询（协议语义不抛错）

    [Fact]
    public async Task PollAsync_AuthorizationPending_ReturnsPending_NeverThrows()
    {
        var svc = BuildService(new FakeHandler(new()
        {
            [TokenUrl] = (400, """{"error":"authorization_pending","error_description":"等待用户授权"}"""),
        }));
        var outcome = await svc.PollAsync(new DeviceCodeStartResult { DeviceCode = "DEV123" });
        outcome.Status.Should().Be(PollStatus.Pending);
        outcome.Error.Should().BeNull();
    }

    [Fact]
    public async Task PollAsync_SlowDown_ReturnsSlowDown()
    {
        var svc = BuildService(new FakeHandler(new()
        {
            [TokenUrl] = (400, """{"error":"slow_down"}"""),
        }));
        var outcome = await svc.PollAsync(new DeviceCodeStartResult { DeviceCode = "DEV123" });
        outcome.Status.Should().Be(PollStatus.SlowDown);
    }

    [Fact]
    public async Task PollAsync_Expired_ReturnsTerminalExpiredWithReason()
    {
        var svc = BuildService(new FakeHandler(new()
        {
            [TokenUrl] = (400, """{"error":"expired_token"}"""),
        }));
        var outcome = await svc.PollAsync(new DeviceCodeStartResult { DeviceCode = "DEV123" });
        outcome.Status.Should().Be(PollStatus.Expired);
        outcome.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PollAsync_AccessDenied_ReturnsTerminalDenied()
    {
        var svc = BuildService(new FakeHandler(new()
        {
            [TokenUrl] = (403, """{"error":"access_denied"}"""),
        }));
        var outcome = await svc.PollAsync(new DeviceCodeStartResult { DeviceCode = "DEV123" });
        outcome.Status.Should().Be(PollStatus.Denied);
    }

    [Fact]
    public async Task PollAsync_Success_BindsAccountAndStoresEncryptedToken()
    {
        var store = new GitHubBindingStore(new FakeEncryption());
        var svc = new GitHubAccountService(
            new GitHubOAuthConfig { ClientId = ClientId },
            store,
            new HttpClient(new FakeHandler(new()
            {
                [TokenUrl] = (200, """{"access_token":"gho_SECRET","scope":"repo,workflow"}"""),
                ["https://api.github.com/user"] = (200, """{"login":"octocat"}"""),
            })));
        var outcome = await svc.PollAsync(new DeviceCodeStartResult { DeviceCode = "DEV123" });
        outcome.Status.Should().Be(PollStatus.Bound);
        outcome.AccountLogin.Should().Be("octocat");

        var dto = store.Get();
        dto.Bound.Should().BeTrue();
        dto.AccountLogin.Should().Be("octocat");
    }

    [Fact]
    public async Task PollAsync_UnknownError_ReturnsFailedWithUpstreamReason()
    {
        var svc = BuildService(new FakeHandler(new()
        {
            [TokenUrl] = (400, """{"error":"bad_verification_code","error_description":"设备码无效"}"""),
        }));
        var outcome = await svc.PollAsync(new DeviceCodeStartResult { DeviceCode = "DEV123" });
        outcome.Status.Should().Be(PollStatus.Failed);
        outcome.Error.Should().Contain("设备码无效");
    }

    [Fact]
    public async Task GetBearerToken_Gating_BoundAutoOnReturnsToken_OffReturnsNull()
    {
        var store = new GitHubBindingStore(new FakeEncryption());
        store.Unbind(); // ForgeSetting 为进程级全局，先清其他用例的绑定残留，保证门控从零开始
        var svc = new GitHubAccountService(new GitHubOAuthConfig { ClientId = ClientId }, store,
            new HttpClient(new FakeHandler(new()
            {
                [TokenUrl] = (200, """{"access_token":"gho_TOK","scope":"repo"}"""),
                ["https://api.github.com/user"] = (200, """{"login":"octocat"}"""),
            })));
        svc.GetBearerToken().Should().BeNull();

        await svc.PollAsync(new DeviceCodeStartResult { DeviceCode = "DEV" });
        svc.GetBearerToken().Should().Be("gho_TOK");

        svc.SetAutoAttach(false);
        svc.GetBearerToken().Should().BeNull();
    }

    #endregion
}
