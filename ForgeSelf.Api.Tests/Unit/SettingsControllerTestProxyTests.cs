using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Controllers;
using ForgeSelf.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// test-proxy 端点单元测试（2026-10-16-settings-proxy-mode-bypass AC-5 单元层）：
/// 参数校验 400、不可达代理 reachable=false+错误原文、零配置副作用、响应不回传明文密码。
/// 真实代理成功路径（10808）属现场验证（AC-5 现场层 / S7），不在单测内依赖外网。
/// 归入 SharedGlobalState：零副作用断言需排除并发集合改配置造成的假失败。
/// </summary>
[Collection("SharedGlobalState")]
public class SettingsControllerTestProxyTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private static JsonElement DataOf(OkObjectResult ok) =>
        JsonSerializer.SerializeToElement(ok.Value!, WebOptions).GetProperty("data");

    private static JsonElement MessageOf(BadRequestObjectResult bad) =>
        JsonSerializer.SerializeToElement(bad.Value!, WebOptions);

    [Theory]
    [InlineData("", 7890)]                 // 空地址
    [InlineData("http://pxy.local", 7890)] // 带 scheme
    [InlineData("pxy local", 7890)]        // 非法字符
    public void 地址非法_400(string host, int port)
    {
        var bad = _sut.TestProxy(new TestProxyRequest { ProxyHost = host, ProxyPort = port })
            .Result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;

        MessageOf(bad).GetProperty("message").GetString().Should().Contain("代理地址无效");
    }

    private static readonly SettingsController _sut = new(new NullEncryption());

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(70000)]
    public void 端口非法_400(int port)
    {
        var bad = _sut.TestProxy(new TestProxyRequest { ProxyHost = "127.0.0.1", ProxyPort = port })
            .Result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;

        MessageOf(bad).GetProperty("message").GetString().Should().Contain("端口");
    }

    [Fact]
    public void 探测地址非http绝对地址_400()
    {
        var bad = _sut.TestProxy(new TestProxyRequest
        {
            ProxyHost = "127.0.0.1",
            ProxyPort = 10808,
            ProxyTestUrl = "socks5://evil"
        }).Result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;

        MessageOf(bad).GetProperty("message").GetString().Should().Contain("探测地址无效");
    }

    [Fact]
    public async Task 代理不可达_reachable为false且返回错误原文()
    {
        // 127.0.0.1:1 无监听 → 立即连接拒绝（不依赖外网）
        var ok = ((await _sut.TestProxy(new TestProxyRequest
        {
            ProxyHost = "127.0.0.1",
            ProxyPort = 1
        })).Result).Should().BeOfType<OkObjectResult>().Subject;

        var data = DataOf(ok);
        data.GetProperty("reachable").GetBoolean().Should().BeFalse();
        data.GetProperty("statusCode").ValueKind.Should().Be(JsonValueKind.Null);
        data.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace("错误原文供 UI 直显");
        data.GetProperty("latencyMs").GetInt64().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task 测试端点零配置副作用()
    {
        var s = ForgeSetting.Current;
        var before = (s.ProxyMode, s.ProxyHost, s.ProxyPort, s.ProxyBypass, s.ProxyPasswordCipher);

        await _sut.TestProxy(new TestProxyRequest
        {
            ProxyHost = "127.0.0.1",
            ProxyPort = 1,
            ProxyUsername = "u",
            ProxyPassword = "p",
            ProxyTestUrl = "https://www.baidu.com"
        });

        var after = (s.ProxyMode, s.ProxyHost, s.ProxyPort, s.ProxyBypass, s.ProxyPasswordCipher);
        after.Should().Be(before, "BR-4：test-proxy 收未保存表单值，绝不落盘");
    }

    [Fact]
    public async Task 响应不回传明文密码()
    {
        var ok = ((await _sut.TestProxy(new TestProxyRequest
        {
            ProxyHost = "127.0.0.1",
            ProxyPort = 1,
            ProxyUsername = "user1",
            ProxyPassword = "topsecret"
        })).Result).Should().BeOfType<OkObjectResult>().Subject;

        JsonSerializer.SerializeToElement(ok.Value!, WebOptions).ToString()
            .Should().NotContain("topsecret", "密码仅存内存用于 407，不得出现在任何响应/日志形态");
    }

    /// <summary>test-proxy 不触碰加密；仅满足控制器构造签名</summary>
    private sealed class NullEncryption : ISecretEncryptionService
    {
        public string Encrypt(string plainText) => throw new NotSupportedException();
        public string Decrypt(string cipherText) => throw new NotSupportedException();
        public bool TryDecrypt(string cipherText, out string plainText) { plainText = ""; return false; }
        public string Mask(string plainText) => "";
    }
}
