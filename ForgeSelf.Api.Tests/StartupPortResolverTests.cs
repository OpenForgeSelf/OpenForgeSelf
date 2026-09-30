using System;
using Xunit;

namespace ForgeSelf.Api.Tests;

/// <summary>
/// <see cref="StartupPortResolver"/> 单元测试（纯解析 + 无覆盖分支）。
/// 通过注入 env 查找函数测试，**不改进程级环境变量**——xUnit 跨集合并行，
/// 进程 env 窗口期会外泄给并行建宿主的 WebApplicationFactory 用例（2026-09-30 实证教训）。
/// 落盘（Save）分支不在此单测覆盖：其依赖 ForgeSetting 静态构造已固化的配置文件路径，
/// 进程内改写 FORGESELF_DATA_ROOT 无法重定向；该分支由 e2e（宿主进程启动期 env 已设、
/// 静态构造即指向隔离数据根）真实验证（Verified）。
/// </summary>
public class StartupPortResolverTests
{
    /// <summary>恒空查找：模拟「未设置任何环境变量」。</summary>
    private static string? NoEnv(string _) => null;

    [Fact]
    public void ResolveFromSources_EnvWinsOverCli()
    {
        string? lookup(string name) => name == StartupPortResolver.EnvPort ? "7105" : null;
        var result = StartupPortResolver.ResolveFromSources(new[] { "--server-port", "7200" }, lookup);
        Assert.Equal(7105, result);
    }

    [Fact]
    public void ResolveFromSources_CliEqualsForm()
    {
        var result = StartupPortResolver.ResolveFromSources(new[] { "--server-port=7200" }, NoEnv);
        Assert.Equal(7200, result);
    }

    [Fact]
    public void ResolveFromSources_CliSpaceForm()
    {
        var result = StartupPortResolver.ResolveFromSources(new[] { "--server-port", "7300" }, NoEnv);
        Assert.Equal(7300, result);
    }

    [Fact]
    public void ResolveFromSources_InvalidEnvIgnored_FallsThroughToCli()
    {
        string? lookup(string name) => name == StartupPortResolver.EnvPort ? "not-a-port" : null;
        var result = StartupPortResolver.ResolveFromSources(new[] { "--server-port=7400" }, lookup);
        Assert.Equal(7400, result);
    }

    [Fact]
    public void ResolveFromSources_OutOfRange_ReturnsNull()
    {
        Assert.Null(StartupPortResolver.ResolveFromSources(new[] { "--server-port=700" }, NoEnv));   // 低于 1024
        Assert.Null(StartupPortResolver.ResolveFromSources(new[] { "--server-port=70000" }, NoEnv)); // 高于 65535
    }

    [Fact]
    public void ResolveFromSources_BlankOrMissing_ReturnsNull()
    {
        Assert.Null(StartupPortResolver.ResolveFromSources(Array.Empty<string>(), NoEnv));
        Assert.Null(StartupPortResolver.ResolveFromSources(new[] { "--other", "x" }, NoEnv));
        string? blankEnv(string name) => name == StartupPortResolver.EnvPort ? "   " : null;
        Assert.Null(StartupPortResolver.ResolveFromSources(Array.Empty<string>(), blankEnv));
    }

    [Fact]
    public void ResolveAndApply_NoOverride_ReturnsCurrentWithoutSideEffect()
    {
        var current = ForgeSelf.Api.Models.ForgeSetting.Current.PortNumber;
        var result = StartupPortResolver.ResolveAndApply(new[] { "--other" }, NoEnv);
        Assert.Equal(current, result);
    }
}
