using System;
using System.IO;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Moq;
using ForgeSelf.Api;
using Xunit;

namespace ForgeSelf.Api.Tests;

/// <summary>
/// B9-4 方案 A（lead 批复）：`FORGESELF_DATA_ROOT` 环境变量在两个解析重载中<b>最前置</b>生效，
/// 把宿主数据根重定向到指定目录。
/// 根因：WAF 测试宿主默认 Testing 环境（非 Development），实例版解析走
/// `SpecialFolder.UserProfile`（读 Windows Known Folder/注册表，不读 USERPROFILE 环境变量）
/// → 与 dev 实例 `~/.forgeself` 撞句柄，~50 条沙箱必红族。
/// 先红后绿：两个重载各测一次（静态版 = ForgeConfig 静态构造路径；实例版 = WAF 注入路径）。
/// 与 <see cref="DataLocationServiceTests"/> 同挂禁并行集合（两组类都改进程级环境变量，防互相污染）。
/// </summary>
[Collection("EnvVarIsolation")]
public class DataLocationOverrideTests
{
    /// <summary>构造一个测试专用数据根（每用例独立 GUID，不与任何真实实例重叠）。</summary>
    private static string NewRoot() => Path.Combine(
        Path.GetTempPath(), $"forgeself-data-root-{Guid.NewGuid():N}");

    [Fact]
    public void ResolveHostDataDirectory_静态版_设置DataRoot_重定向到该根()
    {
        var root = NewRoot();
        var originalRoot = Environment.GetEnvironmentVariable("FORGESELF_DATA_ROOT");
        var originalEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("FORGESELF_DATA_ROOT", root);
            // 故意用 Production：证明重载优先于 Development 判定（最前置）
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");

            DataLocationService.ResolveHostDataDirectory().Should().Be(root);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FORGESELF_DATA_ROOT", originalRoot);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnv);
        }
    }

    [Fact]
    public void ResolveHostDataDirectory_实例版_设置DataRoot_重定向到该根()
    {
        var root = NewRoot();
        var originalRoot = Environment.GetEnvironmentVariable("FORGESELF_DATA_ROOT");
        var originalEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("FORGESELF_DATA_ROOT", root);
            var env = new Mock<IWebHostEnvironment>();
            env.Setup(e => e.EnvironmentName).Returns("Production");

            var svc = new DataLocationService(env.Object);
            svc.GetHostDataDirectory().Should().Be(root);
            // 插件数据目录随之整体重定向（派生路径一致性）
            svc.GetPluginDataDirectory("demo")
                .Should().Be(Path.Combine(root, "plugins", "demo"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("FORGESELF_DATA_ROOT", originalRoot);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnv);
        }
    }

    [Fact]
    public void ResolveHostDataDirectory_DataRoot为空白_视为未设置_保持原语义()
    {
        var originalRoot = Environment.GetEnvironmentVariable("FORGESELF_DATA_ROOT");
        var originalEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("FORGESELF_DATA_ROOT", "   ");
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");

            DataLocationService.ResolveHostDataDirectory()
                .Should().Be(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".forgeself"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("FORGESELF_DATA_ROOT", originalRoot);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnv);
        }
    }
}
