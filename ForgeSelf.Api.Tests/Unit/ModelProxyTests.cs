using ForgeSelf.Abstractions;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Services.Net;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// ModelProxy 实时代理决策测试（2026-10-16-settings-network-proxy AC-1）：
/// 关→直连；开+外网→代理 uri；回环→永不走代理；无效配置→兜底直连；认证与解密失败降级。
/// 本类改写 ForgeSetting 进程级全局配置，归入 SharedGlobalState 串行执行并还原现场。
/// </summary>
[Collection("SharedGlobalState")]
public class ModelProxyTests : IDisposable
{
    private static readonly Uri ExternalTarget = new("https://api.openai.com/v1/chat/completions");

    private readonly bool _origEnabled;
    private readonly string _origHost;
    private readonly int _origPort;
    private readonly string _origUser;
    private readonly string _origCipher;

    public ModelProxyTests()
    {
        var s = ForgeSetting.Current;
        _origEnabled = s.ProxyEnabled;
        _origHost = s.ProxyHost;
        _origPort = s.ProxyPort;
        _origUser = s.ProxyUsername;
        _origCipher = s.ProxyPasswordCipher;
    }

    public void Dispose()
    {
        var s = ForgeSetting.Current;
        s.ProxyEnabled = _origEnabled;
        s.ProxyHost = _origHost;
        s.ProxyPort = _origPort;
        s.ProxyUsername = _origUser;
        s.ProxyPasswordCipher = _origCipher;
        s.Save();
    }

    private static ModelProxy CreateProxy(ISecretEncryptionService? encryption = null) =>
        new(encryption ?? new PassThroughEncryption());

    #region GetProxy 决策

    [Fact]
    public void 关闭代理_返回null直连_忽略系统代理()
    {
        var s = ForgeSetting.Current;
        s.ProxyEnabled = false;
        s.ProxyHost = "proxy.example.com";
        s.ProxyPort = 7890;

        CreateProxy().GetProxy(ExternalTarget).Should().BeNull("关闭=显式直连（BR-1）");
    }

    [Fact]
    public void 开启且地址合法_返回代理uri_带http前缀()
    {
        var s = ForgeSetting.Current;
        s.ProxyEnabled = true;
        s.ProxyHost = "proxy.example.com";
        s.ProxyPort = 7890;

        var uri = CreateProxy().GetProxy(ExternalTarget);
        uri.Should().NotBeNull();
        uri!.ToString().Should().Be("http://proxy.example.com:7890/");
    }

    [Theory]
    [InlineData("http://127.0.0.1:7102/v1/models")]
    [InlineData("http://localhost:7002/settings")]
    [InlineData("http://[::1]:7102/v1")]
    public void 回环目标_永不走代理(string target)
    {
        var s = ForgeSetting.Current;
        s.ProxyEnabled = true;
        s.ProxyHost = "proxy.example.com";
        s.ProxyPort = 7890;

        var proxy = CreateProxy();
        proxy.GetProxy(new Uri(target)).Should().BeNull($"BR-2：{target}");
        proxy.IsBypassed(new Uri(target)).Should().BeTrue();
    }

    [Fact]
    public void 开启但host为空_兜底直连不留异常()
    {
        var s = ForgeSetting.Current;
        s.ProxyEnabled = true;
        s.ProxyHost = "";
        s.ProxyPort = 7890;

        CreateProxy().GetProxy(ExternalTarget).Should().BeNull("BR-5 兜底：无效配置直连 + Warn");
    }

    [Fact]
    public void 开启但端口非法_兜底直连()
    {
        var s = ForgeSetting.Current;
        s.ProxyEnabled = true;
        s.ProxyHost = "proxy.example.com";
        s.ProxyPort = 0;

        CreateProxy().GetProxy(ExternalTarget).Should().BeNull();
    }

    [Fact]
    public void host粘贴了scheme_解析失败_兜底直连()
    {
        var s = ForgeSetting.Current;
        s.ProxyEnabled = true;
        s.ProxyHost = "http://proxy.example.com";
        s.ProxyPort = 7890;

        CreateProxy().GetProxy(ExternalTarget).Should().BeNull("uri 拼接失败不得抛异常打断请求");
    }

    [Fact]
    public void host前后空格_Trim后生效()
    {
        var s = ForgeSetting.Current;
        s.ProxyEnabled = true;
        s.ProxyHost = "  10.0.0.9  ";
        s.ProxyPort = 8080;

        CreateProxy().GetProxy(ExternalTarget)!.ToString().Should().Be("http://10.0.0.9:8080/");
    }

    [Theory, InlineData("-"), InlineData("."), InlineData("_"), InlineData("..--..")]
    public void 极端host通过Regex_GetProxy永不抛异常(string host)
    {
        // Bugbot 审计 2026-10-16：Regex 放行的边界串可能在 new Uri 构造处抛 UriFormatException，
        // 兜底必须覆盖构造：不得打断请求，要么直连(null)要么可用 Uri
        var s = ForgeSetting.Current;
        s.ProxyEnabled = true;
        s.ProxyHost = host;
        s.ProxyPort = 7890;

        var act = () => CreateProxy().GetProxy(ExternalTarget);
        act.Should().NotThrow($"任何落盘 host（{host}）都不得在运行期打断模型请求");
        var uri = act();
        if (uri != null) uri.ToString().Should().StartWith("http://");
    }

    #endregion

    #region 认证凭据

    [Fact]
    public void 配置用户名密码_GetCredential返回解密后明文()
    {
        var s = ForgeSetting.Current;
        s.ProxyEnabled = true;
        s.ProxyHost = "proxy.example.com";
        s.ProxyPort = 7890;
        s.ProxyUsername = "alice";
        s.ProxyPasswordCipher = "v2:hunter2";

        var cred = CreateProxy(new PassThroughEncryption()).Credentials!
            .GetCredential(new Uri("http://proxy.example.com:7890"), "Basic");

        cred!.UserName.Should().Be("alice");
        cred.Password.Should().Be("hunter2");
    }

    [Fact]
    public void 未配置用户名_GetCredential返回null()
    {
        var s = ForgeSetting.Current;
        s.ProxyEnabled = true;
        s.ProxyHost = "proxy.example.com";
        s.ProxyPort = 7890;
        s.ProxyUsername = "";

        CreateProxy().Credentials!.GetCredential(new Uri("http://proxy.example.com:7890"), "Basic")
            .Should().BeNull("匿名代理不发认证");
    }

    [Fact]
    public void 密码解密失败_降级为无密码但不中断认证()
    {
        var s = ForgeSetting.Current;
        s.ProxyEnabled = true;
        s.ProxyHost = "proxy.example.com";
        s.ProxyPort = 7890;
        s.ProxyUsername = "bob";
        s.ProxyPasswordCipher = "not-a-valid-cipher";

        var cred = CreateProxy(new ThrowingEncryption()).Credentials!
            .GetCredential(new Uri("http://proxy.example.com:7890"), "Basic");

        cred!.UserName.Should().Be("bob");
        cred.Password.Should().BeEmpty("BC-2：解密失败按无密码处理，不抛异常");
    }

    #endregion

    /// <summary>透传桩：v2: 前缀即剥掉，其余原样返回</summary>
    private sealed class PassThroughEncryption : ISecretEncryptionService
    {
        public string Encrypt(string plainText) => "v2:" + plainText;
        public string Decrypt(string cipherText) =>
            cipherText.StartsWith("v2:") ? cipherText[3..] : cipherText;
        public bool TryDecrypt(string cipherText, out string plainText)
        {
            plainText = Decrypt(cipherText);
            return true;
        }
        public string Mask(string plainText) => "****";
    }

    /// <summary>解密必炸桩：模拟密钥轮换/配置被手改（BC-2）</summary>
    private sealed class ThrowingEncryption : ISecretEncryptionService
    {
        public string Encrypt(string plainText) => "x:" + plainText;
        public string Decrypt(string cipherText) =>
            throw new System.Security.Cryptography.CryptographicException("桩：密钥不匹配");
        public bool TryDecrypt(string cipherText, out string plainText)
        {
            plainText = string.Empty;
            return false;
        }
        public string Mask(string plainText) => "****";
    }
}
