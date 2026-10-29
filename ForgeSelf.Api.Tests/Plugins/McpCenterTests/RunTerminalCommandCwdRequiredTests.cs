using System.Text.Json;
using FluentAssertions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.McpCenter.Services;
using ForgeSelf.Core;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.McpCenterTests;

/// <summary>
/// run_terminal_command cwd 必填化（2026-10-29 用户拍板）：彻底移除「未选择项目目录且未设置
/// FORGESELF_DEFAULT_PROJECT_ROOT」隐式回退——cwd 必须显式传（绝对路径、目录存在），否则必传错误。
/// 拒绝路径零子进程：错误用例不会启动任何进程。
/// </summary>
public class RunTerminalCommandCwdRequiredTests
{
    private static RunTerminalCommandTool CreateTool() =>
        new(Mock.Of<IContext>());

    private static string? RequireFailure(JsonElement root)
    {
        root.GetProperty("success").GetBoolean().Should().BeFalse("cwd 类拒绝路径应回 success=false");
        return root.GetProperty("error").GetString();
    }

    private static async Task<JsonElement> ExecuteAsync(string parameters)
    {
        var json = await CreateTool().ExecuteAsync(parameters);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    [Fact]
    public async Task MissingCwd_ReturnsRequiredError_WithoutProjectRootFallback()
    {
        var root = await ExecuteAsync(JsonSerializer.Serialize(new { command = "git --version" }));

        var error = RequireFailure(root);
        error.Should().Contain("缺少必填参数 cwd");
        error.Should().Contain("cwd 必传");
    }

    [Fact]
    public async Task EmptyCwd_ReturnsRequiredError()
    {
        var root = await ExecuteAsync(JsonSerializer.Serialize(new { command = "git --version", cwd = "" }));

        RequireFailure(root).Should().Contain("缺少必填参数");
    }

    [Fact]
    public async Task RelativeCwd_ReturnsAbsolutePathError()
    {
        var root = await ExecuteAsync(JsonSerializer.Serialize(new { command = "git --version", cwd = "sub/dir" }));

        RequireFailure(root).Should().Contain("cwd 必须为绝对路径");
    }

    [Fact]
    public async Task NonexistentCwd_ReturnsDirNotExistsError()
    {
        var missing = Path.Combine(Path.GetTempPath(), "forgeself-cwd-missing-" + Guid.NewGuid().ToString("N"));

        var root = await ExecuteAsync(JsonSerializer.Serialize(new { command = "git --version", cwd = missing }));

        RequireFailure(root).Should().Contain("cwd 目录不存在");
    }

    [Fact]
    public async Task ExplicitCwd_RunsProcessThere_AndEchoesCwd()
    {
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "forgeself-cwd-ok-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var root = await ExecuteAsync(JsonSerializer.Serialize(new
            {
                command = "pwsh -NoProfile -NonInteractive -Command \"(Get-Location).Path\"",
                cwd = dir,
                timeoutSeconds = 30,
            }));

            root.GetProperty("success").GetBoolean().Should().BeTrue();
            root.GetProperty("cwd").GetString().Should().Be(dir);
            (root.GetProperty("stdout").GetString() ?? string.Empty).Should().Contain(dir);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
