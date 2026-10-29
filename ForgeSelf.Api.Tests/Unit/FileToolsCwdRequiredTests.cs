using System.Text.Json;
using FluentAssertions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.FileTools;
using ForgeSelf.Core;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// 文件三件套 cwd 必填化（2026-10-29 用户拍板）：read_file / write_file / list_files 必须显式传
/// cwd（绝对路径、目录存在），彻底移除「未选择项目目录/FORGESELF_DEFAULT_PROJECT_ROOT」隐式回退。
/// </summary>
public class FileToolsCwdRequiredTests
{
    private static IContext Ctx() => Mock.Of<IContext>();

    private static string? RequireFailure(string json)
    {
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse("cwd 类拒绝路径应回 success=false：" + json);
        return doc.RootElement.GetProperty("error").GetString();
    }

    [Theory]
    [InlineData("read_file")]
    [InlineData("write_file")]
    [InlineData("list_files")]
    public async Task MissingCwd_ReturnsRequiredError(string toolName)
    {
        var json = toolName switch
        {
            "read_file" => await new ReadFileToolFunction(Ctx()).ExecuteAsync(
                JsonSerializer.Serialize(new { path = "D:\\nope\\a.txt", start_line = 1 })),
            "write_file" => await new WriteFileToolFunction(Ctx()).ExecuteAsync(
                JsonSerializer.Serialize(new { path = "D:\\nope\\a.txt", content = "x", start_line = 1, end_line = 1 })),
            _ => await new ListFilesToolFunction(Ctx()).ExecuteAsync(
                JsonSerializer.Serialize(new { })),
        };

        var error = RequireFailure(json);
        error.Should().Contain("缺少必填参数 cwd");
        error.Should().Contain("cwd 必传");
    }

    [Fact]
    public async Task ListFiles_RelativeCwd_ReturnsAbsolutePathError()
    {
        var json = await new ListFilesToolFunction(Ctx()).ExecuteAsync(
            JsonSerializer.Serialize(new { cwd = "relative/dir" }));

        RequireFailure(json).Should().Contain("cwd 必须为绝对路径");
    }

    [Fact]
    public async Task ReadFile_NonexistentCwd_ReturnsDirNotExistsError()
    {
        var missing = Path.Combine(Path.GetTempPath(), "forgeself-cwd-missing-" + Guid.NewGuid().ToString("N"));
        var json = await new ReadFileToolFunction(Ctx()).ExecuteAsync(
            JsonSerializer.Serialize(new { cwd = missing, path = "D:\\nope\\a.txt", start_line = 1 }));

        RequireFailure(json).Should().Contain("cwd 目录不存在");
    }

    [Fact]
    public async Task HappyPath_WithExplicitCwd_AllThreeToolsSucceed_AndEchoCwd()
    {
        // 仓库内临时目录（AGENTS.md 临时目录约定；本机对测试进程新建文件有静默拦截，勿用 Path.GetTempPath()）
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var dir = Directory.CreateDirectory(
            Path.Combine(repoRoot, ".temp", "cwd-tests", Guid.NewGuid().ToString("N"))).FullName;
        var file = Path.Combine(dir, "a.txt");
        try
        {
                        // 诊断：绕过工具直写，判定 testhost 写入能力
            System.IO.File.WriteAllText(file, "第一行\n第二行");
            System.Console.WriteLine($"[diag] direct-write exists={System.IO.File.Exists(file)} files={System.IO.Directory.GetFiles(dir).Length}");
            var writeJson = await new WriteFileToolFunction(Ctx()).ExecuteAsync(JsonSerializer.Serialize(new
            {
                cwd = dir,
                path = file,
                content = "第一行\n第二行",
                start_line = 1,
                end_line = 1,
            }));
            using (var doc = JsonDocument.Parse(writeJson))
            {
                doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue(writeJson);
                doc.RootElement.GetProperty("cwd").GetString().Should().Be(dir);
                doc.RootElement.GetProperty("path").GetString().Should().Be(file);
            }
            File.Exists(file).Should().BeTrue("write 返回 success 后文件应立即可见；dir 现有：" + string.Join("|", Directory.GetFiles(dir)));
            var readJson = await new ReadFileToolFunction(Ctx()).ExecuteAsync(JsonSerializer.Serialize(new
            {
                cwd = dir,
                path = file,
                start_line = 1,
            }));
            using (var doc = JsonDocument.Parse(readJson))
            {
                doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue(readJson);
                doc.RootElement.GetProperty("cwd").GetString().Should().Be(dir);
                doc.RootElement.GetProperty("content").GetString().Should().Contain("第一行");
            }

            var listJson = await new ListFilesToolFunction(Ctx()).ExecuteAsync(
                JsonSerializer.Serialize(new { cwd = dir }));
            using (var doc = JsonDocument.Parse(listJson))
            {
                doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue(listJson);
                doc.RootElement.GetProperty("root").GetString().Should().Be(dir);
                doc.RootElement.GetProperty("files").EnumerateArray()
                    .Any(f => f.GetProperty("name").GetString() == "a.txt").Should().BeTrue();
            }
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
