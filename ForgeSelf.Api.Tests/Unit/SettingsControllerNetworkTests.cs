using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Controllers;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Services.Net;
using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// 设置 API 网络字段契约测试（2026-10-16-settings-proxy-mode-bypass AC-4；前身 settings-network-proxy AC-1/AC-4）：
/// proxyMode/proxyBypass 读写、null=不修改、非法模式/旁路违规 400 带行号、密码密文落盘且永不回传。
/// 改写 ForgeSetting 全局配置，归入 SharedGlobalState 串行并还原现场。
/// </summary>
[Collection("SharedGlobalState")]
public class SettingsControllerNetworkTests : IDisposable
{
    private readonly SettingsController _sut;
    private readonly StubEncryption _encryption = new();
    private readonly ForgeSnapshot _before;

    public SettingsControllerNetworkTests()
    {
        _sut = new SettingsController(_encryption);
        _before = ForgeSnapshot.Capture();
    }

    public void Dispose() => _before.Restore();

    private static void SetNetwork(string mode, string host, int port, string user = "", string cipher = "", string bypass = "")
    {
        var s = ForgeSetting.Current;
        s.ProxyMode = mode;
        s.ProxyHost = host;
        s.ProxyPort = port;
        s.ProxyUsername = user;
        s.ProxyPasswordCipher = cipher;
        s.ProxyBypass = bypass;
    }

    private static JsonElement DataOf(OkObjectResult ok) =>
        JsonSerializer.SerializeToElement(ok.Value!, WebOptions).GetProperty("data");

    private static JsonElement MessageOf(BadRequestObjectResult bad) =>
        JsonSerializer.SerializeToElement(bad.Value!, WebOptions);

    /// <summary>模拟 ASP.NET Core 真实传输命名（Web 默认 camelCase）</summary>
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    #region GET

    [Fact]
    public void GET_返回网络字段且不含密码()
    {
        SetNetwork(ModelProxy.ModeManual, "proxy.example.com", 7890, "alice", "v2:secret", "*.internal.dev\n10.*");

        var ok = _sut.GetSettings().Result.Should().BeOfType<OkObjectResult>().Subject;
        var data = DataOf(ok);

        data.GetProperty("proxyMode").GetString().Should().Be("manual");
        data.GetProperty("proxyBypass").EnumerateArray().Select(e => e.GetString()).Should().BeEquivalentTo(["*.internal.dev", "10.*"]);
        data.GetProperty("proxyHost").GetString().Should().Be("proxy.example.com");
        data.GetProperty("proxyPort").GetInt32().Should().Be(7890);
        data.GetProperty("proxyUsername").GetString().Should().Be("alice");
        data.GetProperty("proxyPasswordSet").GetBoolean().Should().BeTrue();
        data.TryGetProperty("proxyEnabled", out _).Should().BeFalse("旧二值字段已从契约移除");
        data.ToString().Should().NotContain("secret", "任何形态的密码都不得出现在响应里");
    }

    #endregion

    #region POST 写入与不修改语义

    [Fact]
    public void POST_更新网络字段_密码加密落盘_响应仅回存在性()
    {
        SetNetwork(ModelProxy.ModeDirect, "", 0);

        var result = _sut.UpdateSettings(new UpdateSettingsRequest
        {
            ProxyMode = "MANUAL", // 大小写不敏感（BR：Trim+ToLower 后落盘）
            ProxyHost = " 10.0.0.9 ",
            ProxyPort = 8080,
            ProxyUsername = "bob",
            ProxyPassword = "pass123"
        }).Result.Should().BeOfType<OkObjectResult>().Subject;

        var s = ForgeSetting.Current;
        s.ProxyMode.Should().Be("manual");
        s.ProxyHost.Should().Be("10.0.0.9", "BC-4：Trim 后存储");
        s.ProxyPort.Should().Be(8080);
        s.ProxyPasswordCipher.Should().Be("stub:pass123", "密文落盘（此处为桩加密前缀）");
        s.ProxyPasswordCipher.Should().NotBe("pass123");

        var data = DataOf(result);
        data.GetProperty("proxyPasswordSet").GetBoolean().Should().BeTrue();
        data.ToString().Should().NotContain("pass123");
        _encryption.WasEncrypted.Should().BeTrue();
    }

    [Fact]
    public void POST_缺省字段不修改_null不等于清空()
    {
        SetNetwork(ModelProxy.ModeManual, "keep.me", 3128, "u", "v2:p", "*.keep.bypass");
        ForgeSetting.Current.DefaultModel = "OpenAI:gpt-4o";

        // 只改用户名，其余（含模式/旁路/DefaultModel/密码）都不动
        _sut.UpdateSettings(new UpdateSettingsRequest { ProxyUsername = "newu" })
            .Result.Should().BeOfType<OkObjectResult>();

        var s = ForgeSetting.Current;
        s.ProxyMode.Should().Be(ModelProxy.ModeManual);
        s.ProxyHost.Should().Be("keep.me");
        s.ProxyPort.Should().Be(3128);
        s.ProxyBypass.Should().Be("*.keep.bypass", "proxyBypass null=不修改");
        s.ProxyUsername.Should().Be("newu");
        s.ProxyPasswordCipher.Should().Be("v2:p");
        s.DefaultModel.Should().Be("OpenAI:gpt-4o", "网络面板保存不得误清默认模型（2026-10-16 语义调整）");
    }

    [Fact]
    public void POST_显式空串清空密码_非空串覆盖()
    {
        SetNetwork(ModelProxy.ModeManual, "proxy.example.com", 7890, "alice", "v2:old");

        _sut.UpdateSettings(new UpdateSettingsRequest { ProxyPassword = "" });
        ForgeSetting.Current.ProxyPasswordCipher.Should().BeEmpty("空串=显式清空（前端「清除密码」链接）");

        _sut.UpdateSettings(new UpdateSettingsRequest { ProxyPassword = "fresh" });
        ForgeSetting.Current.ProxyPasswordCipher.Should().Be("stub:fresh");
    }

    [Fact]
    public void POST_DefaultModel空串仍表示清空()
    {
        ForgeSetting.Current.DefaultModel = "OpenAI:gpt-4o";

        _sut.UpdateSettings(new UpdateSettingsRequest { DefaultModel = "" });
        ForgeSetting.Current.DefaultModel.Should().BeEmpty("保持 GeneralPanel 既有清空语义");
    }

    #endregion

    #region POST 校验（BR-5）

    [Fact]
    public void POST_非法模式_400且不落盘()
    {
        SetNetwork(ModelProxy.ModeSystem, "old.host", 1111);

        var bad = _sut.UpdateSettings(new UpdateSettingsRequest { ProxyMode = "socks5" })
            .Result.Should().BeOfType<BadRequestObjectResult>().Subject;

        MessageOf(bad).GetProperty("message").GetString().Should().Contain("代理模式无效");
        ForgeSetting.Current.ProxyMode.Should().Be(ModelProxy.ModeSystem, "400 拒绝落盘");
    }

    [Theory]
    [InlineData("", 7890)]          // manual 但无地址
    [InlineData("http://pxy.local", 7890)] // 带 scheme
    [InlineData("pxy.local:7890", 8080)]   // 地址里混端口
    public void POST_manual模式地址非法_400且不落盘(string host, int port)
    {
        SetNetwork(ModelProxy.ModeSystem, "old.host", 1111);

        var bad = _sut.UpdateSettings(new UpdateSettingsRequest
        {
            ProxyMode = ModelProxy.ModeManual,
            ProxyHost = host,
            ProxyPort = port
        }).Result.Should().BeOfType<BadRequestObjectResult>().Subject;

        MessageOf(bad).GetProperty("message").GetString().Should().Contain("代理地址无效");
        var s = ForgeSetting.Current;
        s.ProxyMode.Should().Be(ModelProxy.ModeSystem, "400 拒绝落盘");
        s.ProxyHost.Should().Be("old.host");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(70000)]
    public void POST_端口越界_400(int port)
    {
        SetNetwork(ModelProxy.ModeSystem, "old.host", 1111);

        var bad = _sut.UpdateSettings(new UpdateSettingsRequest
        {
            ProxyMode = ModelProxy.ModeManual,
            ProxyHost = "proxy.example.com",
            ProxyPort = port
        }).Result.Should().BeOfType<BadRequestObjectResult>().Subject;

        MessageOf(bad).GetProperty("message").GetString().Should().Contain("端口");
        ForgeSetting.Current.ProxyPort.Should().Be(1111);
    }

    [Fact]
    public void POST_direct模式允许保留非法残留地址而不受校验阻拦()
    {
        SetNetwork(ModelProxy.ModeManual, "proxy.example.com", 7890);

        // 切直连时清空地址（行业惯例：direct=全部直连，残留配置允许为空）
        _sut.UpdateSettings(new UpdateSettingsRequest
        {
            ProxyMode = ModelProxy.ModeDirect,
            ProxyHost = "",
            ProxyPort = 0
        }).Result.Should().BeOfType<OkObjectResult>();

        ForgeSetting.Current.ProxyMode.Should().Be(ModelProxy.ModeDirect);
    }

    #endregion

    #region POST 旁路条目校验（BR-5 / AC-4）

    [Fact]
    public void POST_旁路条目合法_逐条trim后落盘为换行分隔()
    {
        SetNetwork(ModelProxy.ModeManual, "proxy.example.com", 7890);

        _sut.UpdateSettings(new UpdateSettingsRequest
        {
            ProxyBypass = [" *.internal.dev ", "", "10.*", "192.168.1.50", "gpustack.luxvisions-inno.com"]
        }).Result.Should().BeOfType<OkObjectResult>();

        ForgeSetting.Current.ProxyBypass.Should().Be("*.internal.dev\n10.*\n192.168.1.50\ngpustack.luxvisions-inno.com", "空行忽略、trim、\\n join");
    }

    [Fact]
    public void POST_旁路空数组表示清空()
    {
        SetNetwork(ModelProxy.ModeManual, "proxy.example.com", 7890, bypass: "*.a.com");

        _sut.UpdateSettings(new UpdateSettingsRequest { ProxyBypass = [] }).Result.Should().BeOfType<OkObjectResult>();

        ForgeSetting.Current.ProxyBypass.Should().BeEmpty();
    }

    [Theory]
    [InlineData("http://bad.entry")]   // 带 scheme（含 /）
    [InlineData("bad entry with space")] // 空格以外的非法字符集外内容（含空格）
    [InlineData("日本.example.com")]     // 非 ASCII
    public void POST_旁路条目非法字符_400带行号(string entry)
    {
        SetNetwork(ModelProxy.ModeManual, "proxy.example.com", 7890);

        var bad = _sut.UpdateSettings(new UpdateSettingsRequest
        {
            ProxyBypass = ["good.entry", entry]
        }).Result.Should().BeOfType<BadRequestObjectResult>().Subject;

        MessageOf(bad).GetProperty("message").GetString().Should().Contain("第 2 行");
        ForgeSetting.Current.ProxyBypass.Should().BeEmpty("400 拒绝落盘");
    }

    [Fact]
    public void POST_旁路条目超50条_400()
    {
        SetNetwork(ModelProxy.ModeManual, "proxy.example.com", 7890);

        var entries = Enumerable.Range(1, 51).Select(i => $"h{i}.example.com").ToArray();
        var bad = _sut.UpdateSettings(new UpdateSettingsRequest { ProxyBypass = entries })
            .Result.Should().BeOfType<BadRequestObjectResult>().Subject;

        MessageOf(bad).GetProperty("message").GetString().Should().Contain("50");
    }

    [Fact]
    public void POST_旁路条目超长_400带行号()
    {
        SetNetwork(ModelProxy.ModeManual, "proxy.example.com", 7890);

        var longEntry = new string('a', 129);
        var bad = _sut.UpdateSettings(new UpdateSettingsRequest { ProxyBypass = [longEntry] })
            .Result.Should().BeOfType<BadRequestObjectResult>().Subject;

        MessageOf(bad).GetProperty("message").GetString().Should().Contain("第 1 行");
    }

    #endregion

    #region 加密子系统故障

    [Fact]
    public void POST_加密失败_500且不落盘()
    {
        var sut = new SettingsController(new ThrowingEncryption());
        SetNetwork(ModelProxy.ModeSystem, "old.host", 1111);

        var result = sut.UpdateSettings(new UpdateSettingsRequest
        {
            ProxyMode = ModelProxy.ModeManual,
            ProxyHost = "proxy.example.com",
            ProxyPort = 7890,
            ProxyPassword = "boom"
        }).Result.Should().BeOfType<ObjectResult>().Subject;

        result.StatusCode.Should().Be(500);
        ForgeSetting.Current.ProxyMode.Should().Be(ModelProxy.ModeSystem, "加密失败整体不落盘，不留下半套配置");
    }

    #endregion

    private sealed class ForgeSnapshot
    {
        public string Mode = ""; public string Host = ""; public int Port; public string User = ""; public string Cipher = ""; public string Bypass = ""; public string DefaultModel = "";

        public static ForgeSnapshot Capture()
        {
            var s = ForgeSetting.Current;
            return new ForgeSnapshot
            {
                Mode = s.ProxyMode,
                Host = s.ProxyHost,
                Port = s.ProxyPort,
                User = s.ProxyUsername,
                Cipher = s.ProxyPasswordCipher,
                Bypass = s.ProxyBypass,
                DefaultModel = s.DefaultModel
            };
        }

        public void Restore()
        {
            var s = ForgeSetting.Current;
            s.ProxyMode = Mode;
            s.ProxyHost = Host;
            s.ProxyPort = Port;
            s.ProxyUsername = User;
            s.ProxyPasswordCipher = Cipher;
            s.ProxyBypass = Bypass;
            s.DefaultModel = DefaultModel;
            // 本机在跑宿主/并发集合会争抢 ForgeSetting.config 句柄（File.Replace 瞬时 IOException，
            // 已知家族「config/*.tmp 句柄争抢」）：内存态必须先复位，落盘按退避重试（最长约 6s，
            // 全量 dotnet test 并发高时 1s 窗口实测不够 —— 2026-10-18 门禁实证）
            for (var i = 1; ; i++)
            {
                try { s.Save(); return; }
                catch (IOException) when (i < 8) { Thread.Sleep(i * 300); }
            }
        }
    }

    private sealed class StubEncryption : ISecretEncryptionService
    {
        public bool WasEncrypted { get; private set; }
        public string Encrypt(string plainText) { WasEncrypted = true; return "stub:" + plainText; }
        public string Decrypt(string cipherText) => cipherText.StartsWith("stub:") ? cipherText[5..] : cipherText;
        public bool TryDecrypt(string cipherText, out string plainText)
        {
            plainText = Decrypt(cipherText);
            return true;
        }
        public string Mask(string plainText) => "****";
    }

    private sealed class ThrowingEncryption : ISecretEncryptionService
    {
        public string Encrypt(string plainText) => throw new InvalidOperationException("模拟加密子系统不可用");
        public string Decrypt(string cipherText) => cipherText;
        public bool TryDecrypt(string cipherText, out string plainText) { plainText = cipherText; return true; }
        public string Mask(string plainText) => "****";
    }
}
