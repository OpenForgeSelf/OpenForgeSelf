using System.Reflection;
using System.Text.Json;
using ForgeSelf.Api.Plugins.FileTools;
using ForgeSelf.Api.Plugins.FileTools.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// 批次C folders 链路的「鉴权 + AI 工具表面」测试（**不碰数据库**）。
/// 鉴权断言复刻 <c>Plugins/McpCenterTests/McpAdminAuthTests.cs</c> 的反射写法。
/// </summary>
public class FileToolsFoldersAuthAndToolTests
{
    private const string ApiKeyPolicy = "ApiKeyPolicy";

    #region 鉴权（类级 Authorize）

    [Fact]
    public void FileToolsController_Must_Have_ApiKeyPolicy_Authorize_AtClassLevel()
    {
        var attr = typeof(FileToolsController).GetCustomAttribute<AuthorizeAttribute>();

        attr.Should().NotBeNull(
            $"folders 端点按请求传入任意绝对路径读写宿主文件系统（管理面），类级必须带 [Authorize(\"{ApiKeyPolicy}\")]");
        attr!.Policy.Should().Be(ApiKeyPolicy);
    }

    #endregion

    #region 工具表面（Id / Name / Schema）

    [Fact]
    public void FolderStatsToolFunction_Identity_IsAsSpecified()
    {
        var tool = new FolderStatsToolFunction("file-tools", null);

        tool.Id.Should().Be("filetools.folder_stats");
        tool.Name.Should().Be("folder_stats");
        tool.PluginId.Should().Be("file-tools");
    }

    [Fact]
    public void FolderStatsToolFunction_ParametersSchema_IsValidJson_AndRequiresDirectory()
    {
        var tool = new FolderStatsToolFunction("file-tools", null);

        using var doc = JsonDocument.Parse(tool.ParametersJsonSchema);
        var root = doc.RootElement;

        root.TryGetProperty("required", out var required).Should().BeTrue("schema 应含 required 数组");
        required.EnumerateArray().Select(e => e.GetString()).Should().Contain("directory");

        root.TryGetProperty("properties", out var props).Should().BeTrue();
        props.TryGetProperty("directory", out _).Should().BeTrue();
    }

    #endregion

    #region 工具执行（真实临时目录 / 非法路径）

    [Fact]
    public async Task FolderStatsToolFunction_Execute_OnRealTempDir_ReturnsSuccessWithRanking()
    {
        // Arrange —— root 下建一个有文件的子目录，保证排行非空
        var root = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "FTTool_" + Guid.NewGuid().ToString("N"))).FullName;
        Directory.CreateDirectory(Path.Combine(root, "sub"));
        File.WriteAllBytes(Path.Combine(root, "sub", "f.bin"), new byte[4096]);

        var tool = new FolderStatsToolFunction("file-tools", null);
        var parameters = JsonSerializer.Serialize(new { directory = root, top = 20 });

        // Act
        var json = await tool.ExecuteAsync(parameters);

        // Assert
        using var doc = JsonDocument.Parse(json);
        var root2 = doc.RootElement;
        root2.GetProperty("success").GetBoolean().Should().BeTrue(json);
        root2.GetProperty("rootTotalBytes").GetInt64().Should().Be(4096);

        var ranking = root2.GetProperty("ranking");
        ranking.ValueKind.Should().Be(JsonValueKind.Array);
        ranking.GetArrayLength().Should().BeGreaterThan(0, "排行应至少含一个子目录");
        ranking[0].GetProperty("relativePath").GetString().Should().Be("sub");
    }

    [Fact]
    public async Task FolderStatsToolFunction_Execute_OnBogusPath_ReturnsSuccessFalse_AndDoesNotThrow()
    {
        var tool = new FolderStatsToolFunction("file-tools", null);
        var ghost = Path.Combine(Path.GetTempPath(), "FTTool_missing_" + Guid.NewGuid().ToString("N"));
        var parameters = JsonSerializer.Serialize(new { directory = ghost });

        // Act —— 非法路径不得抛出，须以 success=false 返回
        var act = async () => await tool.ExecuteAsync(parameters);
        await act.Should().NotThrowAsync();

        var json = await tool.ExecuteAsync(parameters);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse(json);
        doc.RootElement.GetProperty("error").GetString().Should().NotBeNullOrWhiteSpace();
    }

    #endregion
}
