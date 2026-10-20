using System.Net;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Services.Net;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// ModelProxy 实时代理决策测试（2026-10-16-settings-proxy-mode-bypass AC-1/2/3）：
/// 三模式（system/manual/direct）决策顺序、旁路条目五形态、系统代理委托与兜底、认证与解密失败降级。
/// 本类改写 ForgeSetting 进程级全局配置，归入 SharedGlobalState 串行执行并还原现场。
/// </summary>
[Collection("SharedGlobalState")]
public class ModelProxyTests : IDisposable
{
    private static readonly Uri ExternalTarget = new("https://api.openai.com/v1/chat/completions");

    private readonly string _origMode;
    private readonly string _origBypass;
    private readonly string _origHost;
    private readonly int _origPort;
    private readonly string _origUser;
    private readonly string _origCipher;

    public ModelProxyTests()
    {
        var s = ForgeSetting.Current;
        _origMode = s.ProxyMode;
        _origBypass = s.ProxyBypass;
        _origHost = s.ProxyHost;
        _origPort = s.ProxyPort;
        _origUser = s.ProxyUsername;
        _origCipher = s.ProxyPasswordCipher;
    }

    public void Dispose()
    {
        var s = ForgeSetting.Current;
        s.ProxyMode = _origMode;
        s.ProxyBypass = _origBypass;
        s.ProxyHost = _origHost;
        s.ProxyPort = _origPort;
        s.ProxyUsername = _origUser;
        s.ProxyPasswordCipher = _origCipher;
        s.Save();
    }

    private static ModelProxy CreateProxy(ISecretEncryptionService? encryption = null, Func<IWebProxy>? systemProvider = null) =>
        new(encryption ?? new PassThroughEncryption(), systemProvider);

    private void SetManual()
    {
        var s = ForgeSetting.Current;
        s.ProxyMode = ModelProxy.ModeManual;
        s.ProxyBypass = "";
        s.ProxyHost = "proxy.example.com";
        s.ProxyPort = 7890;
    }

    #region 三模式决策（BR-1）

    [Fact]
    public void 直连模式_返回null_不查系统代理()
    {
        var s = ForgeSetting.Current;
        s.ProxyMode = ModelProxy.ModeDirect;
        s.ProxyHost = "proxy.example.com";
        s.ProxyPort = 7890;
        var queried = false;

        CreateProxy(systemProvider: () => { queried = true; return new FakeSystemProxy(); })
            .GetProxy(ExternalTarget).Should().BeNull("direct=显式直连");
        queried.Should().BeFalse("direct 模式不得委托系统代理");
    }

    [Fact]
    public void 手动模式_地址合法_返回代理uri_带http前缀()
    {
        SetManual();

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
        SetManual();

        var proxy = CreateProxy();
        proxy.GetProxy(new Uri(target)).Should().BeNull($"BR-2：{target}");
        proxy.IsBypassed(new Uri(target)).Should().BeTrue();
    }

    [Fact]
    public void 手动但host为空_兜底直连不留异常()
    {
        SetManual();
        ForgeSetting.Current.ProxyHost = "";

        CreateProxy().GetProxy(ExternalTarget).Should().BeNull("BR-5 兜底：无效配置直连 + Warn");
    }

    [Fact]
    public void 手动但端口非法_兜底直连()
    {
        SetManual();
        ForgeSetting.Current.ProxyPort = 0;

        CreateProxy().GetProxy(ExternalTarget).Should().BeNull();
    }

    [Fact]
    public void host粘贴了scheme_解析失败_兜底直连()
    {
        SetManual();
        ForgeSetting.Current.ProxyHost = "http://proxy.example.com";

        CreateProxy().GetProxy(ExternalTarget).Should().BeNull("uri 拼接失败不得抛异常打断请求");
    }

    [Fact]
    public void host前后空格_Trim后生效()
    {
        SetManual();
        ForgeSetting.Current.ProxyHost = "  10.0.0.9  ";
        ForgeSetting.Current.ProxyPort = 8080;

        CreateProxy().GetProxy(ExternalTarget)!.ToString().Should().Be("http://10.0.0.9:8080/");
    }

    [Theory, InlineData("-"), InlineData("."), InlineData("_"), InlineData("..--..")]
    public void 极端host通过Regex_GetProxy永不抛异常(string host)
    {
        // Bugbot 审计 2026-10-16：Regex 放行的边界串可能在 new Uri 构造处抛 UriFormatException，
        // 兜底必须覆盖构造：不得打断请求，要么直连(null)要么可用 Uri
        SetManual();
        ForgeSetting.Current.ProxyHost = host;

        var act = () => CreateProxy().GetProxy(ExternalTarget);
        act.Should().NotThrow($"任何落盘 host（{host}）都不得在运行期打断模型请求");
        var uri = act();
        if (uri != null) uri.ToString().Should().StartWith("http://");
    }

    [Fact]
    public void 未知模式值_按system处理_非法值不炸()
    {
        var s = ForgeSetting.Current;
        s.ProxyMode = "socks5"; // 手改配置文件的非法值
        var called = false;
        CreateProxy(systemProvider: () => { called = true; return new FakeSystemProxy { Bypass = true }; })
            .GetProxy(ExternalTarget).Should().BeNull();
        called.Should().BeTrue("非法值归一化为 system 后走委托");
        ModelProxy.NormalizeMode("  MANUAL ").Should().Be(ModelProxy.ModeManual);
        ModelProxy.NormalizeMode("").Should().Be(ModelProxy.ModeSystem);
    }

    #endregion

    #region 旁路条目（BR-2 五形态）

    [Theory]
    [InlineData("*.example.com", "example.com")]        // 后缀匹配含自身
    [InlineData("*.example.com", "a.b.example.com")]     // 多级子域
    [InlineData("example.com", "sub.example.com")]       // 裸域名=后缀（NO_PROXY 惯例）
    [InlineData("EXAMPLE.COM", "example.com")]           // 大小写不敏感
    [InlineData("10.*", "10.188.253.75")]                // 段前缀
    [InlineData("192.168.*", "192.168.1.5")]
    [InlineData("10.188.253.75", "10.188.253.75")]      // IPv4 精确
    [InlineData("gpustack.luxvisions-inno.com", "gpustack.luxvisions-inno.com")] // 输入6 场景
    public void 旁路命中_手动模式下直连(string entry, string host)
    {
        SetManual();
        ForgeSetting.Current.ProxyBypass = entry;

        CreateProxy().GetProxy(new Uri($"http://{host}/v1")).Should().BeNull($"条目 {entry} 应命中 {host}");
    }

    [Fact]
    public void 旁路不误伤_相邻后缀与不同IP段仍走代理()
    {
        SetManual();
        ForgeSetting.Current.ProxyBypass = "*.example.com\n10.*";

        // evilnotexample.com 不得被 .example.com 后缀命中；110.0.0.1 不得被 10.* 前缀命中
        CreateProxy().GetProxy(new Uri("https://evilnotexample.com/v1")).Should().NotBeNull();
        CreateProxy().GetProxy(new Uri("https://110.0.0.1/v1")).Should().NotBeNull();
    }

    [Fact]
    public void 旁路在跟随系统模式下同样优先生效()
    {
        var s = ForgeSetting.Current;
        s.ProxyMode = ModelProxy.ModeSystem;
        s.ProxyBypass = "*.internal.dev";
        var queried = false;

        var proxy = CreateProxy(systemProvider: () => { queried = true; return new FakeSystemProxy { Ret = new Uri("http://sys.proxy:8080") }; });
        proxy.GetProxy(new Uri("http://app.internal.dev/api")).Should().BeNull();
        queried.Should().BeFalse("BR-1①：旁路先于系统委托");
    }

    [Fact]
    public void 多行条目_空行忽略_命中任一直连()
    {
        SetManual();
        ForgeSetting.Current.ProxyBypass = "\n  *.a.com \n\n10.*\n";

        CreateProxy().GetProxy(new Uri("http://x.a.com/v1")).Should().BeNull();
        CreateProxy().GetProxy(new Uri("http://10.1.1.1/v1")).Should().BeNull();
        CreateProxy().GetProxy(ExternalTarget).Should().NotBeNull();
        BypassMatcher.Parse(ForgeSetting.Current.ProxyBypass).Should().BeEquivalentTo(["*.a.com", "10.*"]);
    }

    #endregion

    #region 跟随系统委托（BR-1④）

    [Fact]
    public void 系统模式_委托返回代理_透传()
    {
        ForgeSetting.Current.ProxyMode = ModelProxy.ModeSystem;
        ForgeSetting.Current.ProxyBypass = "";
        var sysUri = new Uri("http://sys.proxy:8080");

        CreateProxy(systemProvider: () => new FakeSystemProxy { Ret = sysUri })
            .GetProxy(ExternalTarget).Should().Be(sysUri);
    }

    [Fact]
    public void 系统模式_系统判定旁路_直连()
    {
        ForgeSetting.Current.ProxyMode = ModelProxy.ModeSystem;
        ForgeSetting.Current.ProxyBypass = "";

        CreateProxy(systemProvider: () => new FakeSystemProxy { Bypass = true, Ret = new Uri("http://sys.proxy:8080") })
            .GetProxy(ExternalTarget).Should().BeNull();
    }

    [Fact]
    public void 系统模式_系统未配置代理_原样返回视为直连()
    {
        ForgeSetting.Current.ProxyMode = ModelProxy.ModeSystem;
        ForgeSetting.Current.ProxyBypass = "";

        // 系统代理对象无配置时通常原样返回 destination（.NET 语义），不得当「有代理」透传
        CreateProxy(systemProvider: () => new FakeSystemProxy { Ret = ExternalTarget })
            .GetProxy(ExternalTarget).Should().BeNull();
    }

    [Fact]
    public void 系统模式_获取系统代理抛异常_兜底直连不炸()
    {
        ForgeSetting.Current.ProxyMode = ModelProxy.ModeSystem;
        ForgeSetting.Current.ProxyBypass = "";

        var act = () => CreateProxy(systemProvider: () => throw new PlatformNotSupportedException("桩")).GetProxy(ExternalTarget);
        act.Should().NotThrow().Subject.Should().BeNull();
    }

    [Fact]
    public void 系统模式_真实系统代理provider_不抛异常()
    {
        // Unknown① 探针：真实 GetSystemWebProxy 在本环境可调用且决策不抛（不断言具体值——环境相关）
        ForgeSetting.Current.ProxyMode = ModelProxy.ModeSystem;
        ForgeSetting.Current.ProxyBypass = "";

        var act = () => CreateProxy().GetProxy(ExternalTarget);
        act.Should().NotThrow();
    }

    #endregion

    #region 认证凭据

    [Fact]
    public void 手动模式_配置用户名密码_GetCredential返回解密后明文()
    {
        SetManual();
        var s = ForgeSetting.Current;
        s.ProxyUsername = "alice";
        s.ProxyPasswordCipher = "v2:hunter2";

        var cred = CreateProxy(new PassThroughEncryption()).Credentials!
            .GetCredential(new Uri("http://proxy.example.com:7890"), "Basic");

        cred!.UserName.Should().Be("alice");
        cred.Password.Should().Be("hunter2");
    }

    [Fact]
    public void 非手动模式_凭据不参与()
    {
        var s = ForgeSetting.Current;
        s.ProxyMode = ModelProxy.ModeSystem;
        s.ProxyUsername = "alice";
        s.ProxyPasswordCipher = "v2:hunter2";

        CreateProxy().Credentials!.GetCredential(new Uri("http://proxy.example.com:7890"), "Basic")
            .Should().BeNull("system/direct 模式不发放手动凭据");
    }

    [Fact]
    public void 手动模式_未配置用户名_GetCredential返回null()
    {
        SetManual();
        ForgeSetting.Current.ProxyUsername = "";

        CreateProxy().Credentials!.GetCredential(new Uri("http://proxy.example.com:7890"), "Basic")
            .Should().BeNull("匿名代理不发认证");
    }

    [Fact]
    public void 密码解密失败_降级为无密码但不中断认证()
    {
        SetManual();
        var s = ForgeSetting.Current;
        s.ProxyUsername = "bob";
        s.ProxyPasswordCipher = "not-a-valid-cipher";

        var cred = CreateProxy(new ThrowingEncryption()).Credentials!
            .GetCredential(new Uri("http://proxy.example.com:7890"), "Basic");

        cred!.UserName.Should().Be("bob");
        cred.Password.Should().BeEmpty("BC-2：解密失败按无密码处理，不抛异常");
    }

    #endregion

    #region BypassMatcher 单元

    [Theory]
    [InlineData("*.example.com", true)]
    [InlineData("example.com", true)]
    [InlineData("10.*", true)]
    [InlineData("192.168.1.5", true)]
    [InlineData("host-name_x:8080", true)]
    [InlineData("bad entry with space", false)]
    [InlineData("中文域名", false)]
    [InlineData("", true)] // 空行合法（保存前过滤）
    public void IsValidEntry字符集白名单(string entry, bool valid) =>
        BypassMatcher.IsValidEntry(entry).Should().Be(valid);

    #endregion

    /// <summary>系统代理桩</summary>
    private sealed class FakeSystemProxy : IWebProxy
    {
        public Uri? Ret;
        public bool Bypass;
        public ICredentials? Credentials { get; set; }
        public bool IsBypassed(Uri destination) => Bypass;
        public Uri? GetProxy(Uri destination) => Ret;
    }

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
