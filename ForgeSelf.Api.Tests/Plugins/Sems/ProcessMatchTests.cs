using System.Collections.Generic;
using System.IO;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.Sems.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.Sems;

/// <summary>
/// 进程归属判定纯函数 <see cref="ProcessMatcher.MatchProject"/> 表驱动测试：
/// 路径前缀命中 / 分隔符边界不误匹配 / 类型二次确认 / 无 Type 直接采信。
/// </summary>
public class ProcessMatchTests
{
    private static ProjectInfo MakeProject(int id, string root, string type) => new()
    {
        Id = id,
        Root = root,
        Name = $"P{id}",
        Type = type
    };

    public static IEnumerable<object[]> PrefixHitCases()
    {
        // 命中：可执行文件路径在项目 Root 下
        yield return new object[] { @"D:\proj\frontend\node.exe", null,
            new[] { MakeProject(1, @"D:\proj\frontend", "frontend") }, 1 };
        // （CommandLine 路径命中但 node↔backend 类型不兼容 → 不命中，见 MatchProject_CommandLinePathHit_ButTypeMismatch_NotHit）
        // 命中：大小写与 / \ 归一
        yield return new object[] { @"d:/proj/full/dotnet.exe", null,
            new[] { MakeProject(3, @"D:\proj\full", "fullstack") }, 3 };
    }

    [Theory]
    [MemberData(nameof(PrefixHitCases))]
    public void MatchProject_PrefixHit_ReturnsProject(
        string exePath, string? commandLine, ProjectInfo[] projects, int expectedId)
    {
        var result = ProcessMatcher.MatchProject(exePath, commandLine, projects);
        Assert.NotNull(result);
        Assert.Equal(expectedId, result!.Id);
    }

    [Fact]
    public void MatchProject_CommandLinePathHit_ButTypeMismatch_NotHit()
    {
        // CommandLine 中提取的绝对路径命中 backend 目录，但进程技术(node)与 backend 类型不兼容
        // → 类型二次确认拒绝（宁漏勿误），不命中。
        var projects = new[] { MakeProject(2, @"D:\proj\backend", "backend") };
        var hit = ProcessMatcher.MatchProject(
            @"C:\Windows\System32\cmd.exe",
            @"cmd /c node D:\proj\backend\app.js",
            projects);
        Assert.Null(hit);
    }

    [Fact]
    public void MatchProject_SeparatorBoundary_NotMisMatch()
    {
        // 项目根 D:\foo 不应命中 D:\foobar 下的进程（分隔符边界）
        var projects = new[] { MakeProject(1, @"D:\foo", "frontend") };
        var hit = ProcessMatcher.MatchProject(@"D:\foobar\app\node.exe", null, projects);
        Assert.Null(hit);
    }

    [Fact]
    public void MatchProject_DriveBoundary_NotMisMatch()
    {
        // D:\foo 不应命中 E:\foo 下的进程（盘符不同）
        var projects = new[] { MakeProject(1, @"D:\foo", "frontend") };
        var hit = ProcessMatcher.MatchProject(@"E:\foo\node.exe", null, projects);
        Assert.Null(hit);
    }

    public static IEnumerable<object[]> TypeConfirmCases()
    {
        // 类型二次确认通过：node 进程 ↔ frontend
        yield return new object[] { @"D:\proj\frontend\node.exe", "frontend", true };
        // 类型二次确认通过：dotnet 进程 ↔ backend
        yield return new object[] { @"D:\proj\backend\dotnet.exe", "backend", true };
        // 类型二次确认通过：node 进程 ↔ fullstack
        yield return new object[] { @"D:\proj\full\node.exe", "fullstack", true };
        // 类型二次确认失败：node 进程 ↔ backend（node 不兼容 backend）
        yield return new object[] { @"D:\proj\backend\node.exe", "backend", false };
        // 类型二次确认失败：dotnet 进程 ↔ frontend
        yield return new object[] { @"D:\proj\frontend\dotnet.exe", "frontend", false };
    }

    [Theory]
    [MemberData(nameof(TypeConfirmCases))]
    public void MatchProject_TypeConfirmation(
        string exePath, string type, bool shouldHit)
    {
        var projects = new[] { MakeProject(1, Path.GetDirectoryName(exePath)!, type) };
        var hit = ProcessMatcher.MatchProject(exePath, null, projects);
        if (shouldHit) Assert.NotNull(hit);
        else Assert.Null(hit);
    }

    [Fact]
    public void MatchProject_EmptyType_AcceptPathHit()
    {
        // Type 为空 → 直接采信路径命中，跳过类型二次确认
        var projects = new[] { MakeProject(1, @"D:\proj\whatever", "") };
        var hit = ProcessMatcher.MatchProject(@"D:\proj\whatever\node.exe", null, projects);
        Assert.NotNull(hit);
        Assert.Equal(1, hit!.Id);
    }

    [Fact]
    public void MatchProject_NoCandidatePath_ReturnsNull()
    {
        var projects = new[] { MakeProject(1, @"D:\proj\frontend", "frontend") };
        var hit = ProcessMatcher.MatchProject(@"C:\Windows\System32\conhost.exe", null, projects);
        Assert.Null(hit);
    }

    [Fact]
    public void MatchProject_CommandLineExtractsAbsolutePath()
    {
        // CommandLine 中带引号绝对路径也应被提取命中
        var projects = new[] { MakeProject(1, @"D:\proj\web", "frontend") };
        var hit = ProcessMatcher.MatchProject(
            @"C:\Windows\System32\cmd.exe",
            @"""D:\proj\web\node.exe"" --watch",
            projects);
        Assert.NotNull(hit);
        Assert.Equal(1, hit!.Id);
    }
}
