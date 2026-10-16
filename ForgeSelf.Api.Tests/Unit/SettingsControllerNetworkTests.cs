using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Controllers;
using ForgeSelf.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// 设置 API 网络字段契约测试（2026-10-16-settings-network-proxy AC-1/AC-4）：
/// 读写、null=不修改、开启校验 400、密码密文落盘且永不回传。
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

    private static void SetNetwork(bool enabled, string host, int port, string user = "", string cipher = "")
    {
        var s = ForgeSetting.Current;
        s.ProxyEnabled = enabled;
        s.ProxyHost = host;
        s.ProxyPort = port;
        s.ProxyUsername = user;
        s.ProxyPasswordCipher = cipher;
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
        SetNetwork(true, "proxy.example.com", 7890, "alice", "v2:secret");

        var ok = _sut.GetSettings().Result.Should().BeOfType<OkObjectResult>().Subject;
        var data = DataOf(ok);

        data.GetProperty("proxyEnabled").GetBoolean().Should().BeTrue();
        data.GetProperty("proxyHost").GetString().Should().Be("proxy.example.com");
        data.GetProperty("proxyPort").GetInt32().Should().Be(7890);
        data.GetProperty("proxyUsername").GetString().Should().Be("alice");
        data.GetProperty("proxyPasswordSet").GetBoolean().Should().BeTrue();
        data.ToString().Should().NotContain("secret", "任何形态的密码都不得出现在响应里");
    }

    #endregion

    #region POST 写入与不修改语义

    [Fact]
    public void POST_更新网络字段_密码加密落盘_响应仅回存在性()
    {
        SetNetwork(false, "", 0);

        var result = _sut.UpdateSettings(new UpdateSettingsRequest
        {
            ProxyEnabled = true,
            ProxyHost = " 10.0.0.9 ",
            ProxyPort = 8080,
            ProxyUsername = "bob",
            ProxyPassword = "pass123"
        }).Result.Should().BeOfType<OkObjectResult>().Subject;

        var s = ForgeSetting.Current;
        s.ProxyEnabled.Should().BeTrue();
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
        SetNetwork(true, "keep.me", 3128, "u", "v2:p");
        ForgeSetting.Current.DefaultModel = "OpenAI:gpt-4o";

        // 只改用户名，其余（含 DefaultModel 与密码）都不动
        _sut.UpdateSettings(new UpdateSettingsRequest { ProxyUsername = "newu" })
            .Result.Should().BeOfType<OkObjectResult>();

        var s = ForgeSetting.Current;
        s.ProxyEnabled.Should().BeTrue();
        s.ProxyHost.Should().Be("keep.me");
        s.ProxyPort.Should().Be(3128);
        s.ProxyUsername.Should().Be("newu");
        s.ProxyPasswordCipher.Should().Be("v2:p");
        s.DefaultModel.Should().Be("OpenAI:gpt-4o", "网络面板保存不得误清默认模型（2026-10-16 语义调整）");
    }

    [Fact]
    public void POST_显式空串清空密码_非空串覆盖()
    {
        SetNetwork(true, "proxy.example.com", 7890, "alice", "v2:old");

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

    [Theory]
    [InlineData("", 7890)]          // 开启但无地址
    [InlineData("http://pxy.local", 7890)] // 带 scheme
    [InlineData("pxy.local:7890", 0)]      // 地址里混端口
    public void POST_开启时地址非法_400且不落盘(string host, int port)
    {
        SetNetwork(false, "old.host", 1111);

        var bad = _sut.UpdateSettings(new UpdateSettingsRequest
        {
            ProxyEnabled = true,
            ProxyHost = host,
            ProxyPort = port
        }).Result.Should().BeOfType<BadRequestObjectResult>().Subject;

        MessageOf(bad).GetProperty("message").GetString().Should().Contain("代理地址无效");
        var s = ForgeSetting.Current;
        s.ProxyEnabled.Should().BeFalse("400 拒绝落盘");
        s.ProxyHost.Should().Be("old.host");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(70000)]
    public void POST_端口越界_400(int port)
    {
        SetNetwork(false, "old.host", 1111);

        var bad = _sut.UpdateSettings(new UpdateSettingsRequest
        {
            ProxyEnabled = true,
            ProxyHost = "proxy.example.com",
            ProxyPort = port
        }).Result.Should().BeOfType<BadRequestObjectResult>().Subject;

        MessageOf(bad).GetProperty("message").GetString().Should().Contain("端口");
        ForgeSetting.Current.ProxyPort.Should().Be(1111);
    }

    [Fact]
    public void POST_关闭态允许保留非法残留而不受校验阻拦()
    {
        SetNetwork(true, "proxy.example.com", 7890);

        // 关闭开关时清空地址（行业惯例：关=直连，残留配置允许为空）
        _sut.UpdateSettings(new UpdateSettingsRequest
        {
            ProxyEnabled = false,
            ProxyHost = "",
            ProxyPort = 0
        }).Result.Should().BeOfType<OkObjectResult>();

        ForgeSetting.Current.ProxyEnabled.Should().BeFalse();
    }

    #endregion

    #region 加密子系统故障

    [Fact]
    public void POST_加密失败_500且不落盘()
    {
        var sut = new SettingsController(new ThrowingEncryption());
        SetNetwork(false, "old.host", 1111);

        var result = sut.UpdateSettings(new UpdateSettingsRequest
        {
            ProxyEnabled = true,
            ProxyHost = "proxy.example.com",
            ProxyPort = 7890,
            ProxyPassword = "boom"
        }).Result.Should().BeOfType<ObjectResult>().Subject;

        result.StatusCode.Should().Be(500);
        ForgeSetting.Current.ProxyEnabled.Should().BeFalse("加密失败整体不落盘，不留下半套配置");
    }

    #endregion

    private sealed class ForgeSnapshot
    {
        public bool Enabled; public string Host = ""; public int Port; public string User = ""; public string Cipher = ""; public string DefaultModel = "";

        public static ForgeSnapshot Capture()
        {
            var s = ForgeSetting.Current;
            return new ForgeSnapshot
            {
                Enabled = s.ProxyEnabled,
                Host = s.ProxyHost,
                Port = s.ProxyPort,
                User = s.ProxyUsername,
                Cipher = s.ProxyPasswordCipher,
                DefaultModel = s.DefaultModel
            };
        }

        public void Restore()
        {
            var s = ForgeSetting.Current;
            s.ProxyEnabled = Enabled;
            s.ProxyHost = Host;
            s.ProxyPort = Port;
            s.ProxyUsername = User;
            s.ProxyPasswordCipher = Cipher;
            s.DefaultModel = DefaultModel;
            // 本机在跑宿主/并发集合会争抢 ForgeSetting.config 句柄（File.Replace 瞬时 IOException，
            // 已知家族「config/*.tmp 句柄争抢」）：内存态必须先复位，落盘重试几次
            for (var i = 1; ; i++)
            {
                try { s.Save(); return; }
                catch (IOException) when (i < 5) { Thread.Sleep(200); }
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
