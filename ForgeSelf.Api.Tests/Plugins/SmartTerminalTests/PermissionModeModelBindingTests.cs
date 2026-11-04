using System.Text.Json;
using FluentAssertions;
using ForgeSelf.Api.Plugins.SmartTerminal.Models;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.SmartTerminalTests;

/// <summary>
/// PC 守卫（2026-11-05 51888 实测缺陷）：PermissionMode 在 HTTP 边界按字符串名交换
/// （前端契约 ''Auto''|''AskWrite''|''AskHighRisk''）。STJ 默认只收数字，曾致建会话 400。
/// 枚举已挂 JsonStringEnumConverter：字符串名必须可反序列化（数字保持兼容）。
/// </summary>
public class PermissionModeModelBindingTests
{
    private static T? Bind<T>(string json) where T : class =>
        JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    [Theory]
    [InlineData("\"AskWrite\"", PermissionMode.AskWrite)]
    [InlineData("\"Auto\"", PermissionMode.Auto)]
    [InlineData("\"AskHighRisk\"", PermissionMode.AskHighRisk)]
    public void PermissionMode_字符串名可反序列化(string raw, PermissionMode expected)
    {
        var dto = Bind<CreateSessionRequest>("{" + $"\"permissionMode\":{raw}" + "}");
        dto!.PermissionMode.Should().Be(expected);
    }

    [Fact]
    public void PermissionMode_数字仍兼容()
    {
        var dto = Bind<CreateSessionRequest>("{\"permissionMode\":1}");
        dto!.PermissionMode.Should().Be(PermissionMode.AskWrite);
    }

    [Fact]
    public void PermissionMode_序列化为字符串名()
    {
        var json = JsonSerializer.Serialize(new CreateSessionRequest { PermissionMode = PermissionMode.AskWrite });
        json.Should().Contain("\"AskWrite\"");
    }
}