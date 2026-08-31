using System.Text.Json;
using ForgeSelf.Abstractions;
using Xunit;

namespace ForgeSelf.Abstractions.Tests;

/// <summary>
/// sems 共享项目契约（<see cref="ProjectRecord"/>）的序列化回归保护。
/// 背景（2026-08-31）：PathExists/IsGitRepo 曾标 [JsonIgnore]，导致 sems 首页
/// /api/projects 响应缺失可达性字段 → 前端把可达项目误渲染为「不可达」。
/// 本测试确保这两个展示字段能随 API 序列化，避免回归。
/// </summary>
public class SemsSharedContractTests
{
    // 与 ASP.NET API 序列化一致（camelCase），确保断言反映真实 /api/projects 响应契约。
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ProjectRecord_Serializes_PathExists_And_IsGitRepo()
    {
        var record = new ProjectRecord
        {
            Root = @"D:\src\proj\demo",
            Name = "demo",
            PathExists = true,
            IsGitRepo = true
        };

        var json = JsonSerializer.Serialize(record, JsonOpts);

        Assert.Contains("\"pathExists\":true", json);
        Assert.Contains("\"isGitRepo\":true", json);
    }

    [Fact]
    public void ProjectRecord_Deserializes_And_DefaultCompaction()
    {
        var json = """{"root":"D:\\src\\proj\\demo","name":"demo","source":"ai-agent"}""";
        var parsed = JsonSerializer.Deserialize<ProjectRecord>(json, JsonOpts);

        Assert.NotNull(parsed);
        Assert.Equal("ai-agent", parsed!.Source);
        // 未落盘的展示字段默认 false，由读取端（sems）计算覆盖
        Assert.False(parsed.PathExists);
    }

    [Theory]
    [InlineData(@"C:\data", @"C:\data\Shared\sems-projects.json")]
    [InlineData(@"C:\data\", @"C:\data\Shared\sems-projects.json")]
    public void GetProjectsFilePath_DerivesSharedPath(string hostDataRoot, string expected)
    {
        Assert.Equal(expected.Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar),
            SemsShared.GetProjectsFilePath(hostDataRoot));
    }
}