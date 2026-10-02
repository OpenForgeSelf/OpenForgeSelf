using System;
using System.IO;
using FluentAssertions;
using ForgeSelf.Api;
using NewLife;
using NewLife.Configuration;
using NewLife.Log;
using XCode;
using Xunit;

namespace ForgeSelf.Api.Tests;

/// <summary>
/// 数据根隔离守卫（2026-10-02 输入4）：钉死「测试进程的数据根绝不指向真实宿主根 <c>~/.forgeself</c>」，
/// 防止 <c>dotnet test</c> 污染宿主数据、日志、配置。
/// 本测试<b>不改</b>环境变量，只读当前解析结果，因此挂 <c>EnvVarIsolation</c> 集合只为与
/// 会临时改写/清除该变量的测试类（<see cref="DataLocationServiceTests"/> / <see cref="DataLocationOverrideTests"/>）
/// 串行，避免读到它们的窗口期值。
/// </summary>
[Collection("EnvVarIsolation")]
public class TestDataRootIsolationGuardTests
{
    /// <summary>真实宿主数据根（生产回落值）。</summary>
    private static string RealHostRoot() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".forgeself");

    [Fact]
    public void 测试进程数据根_不得等于真实宿主根()
    {
        var resolved = DataLocationService.ResolveHostDataDirectory();

        resolved.Should().NotBe(RealHostRoot(),
            "dotnet test 必须与真实宿主数据根物理隔离，否则会污染宿主数据/日志/配置");
    }

    [Fact]
    public void 测试进程数据根_自动隔离时落在仓库内隔离目录()
    {
        // 外部显式设置（e2e / CI / 手工前缀）→ 本机制短路，不做位置断言（尊重外部选择）。
        if (!TestDataRootIsolation.Applied)
        {
            return;
        }

        var normalized = DataLocationService.ResolveHostDataDirectory().Replace('\\', '/');

        normalized.Should().Contain("/.temp/dotnet-test/",
            "无外部前缀时，数据根应自动落在仓库内 .temp/dotnet-test/（.temp/ 已 gitignore）");
    }

    [Fact]
    public void 测试进程日志路径_不得落在真实宿主根()
    {
        // 2026-10-02 输入4 实证：即便数据根已隔离，若程序目录残留旧 Core.config（内含宿主 LogPath），
        // 测试进程仍会尝试写 ~/.forgeself/log。此处钉死日志路径同样不得指向真实宿主根。
        var hostRoot = RealHostRoot();

        XTrace.LogPath.Should().NotStartWith(hostRoot,
            "dotnet test 不得写宿主日志目录（XTrace.LogPath 必须落在隔离根）");
        NewLife.Setting.Current.LogPath.Should().NotStartWith(hostRoot,
            "dotnet test 不得写宿主日志目录（Setting.LogPath 必须落在隔离根）");
    }

    [Fact]
    public void 测试进程配置文件_不得落在真实宿主根()
    {
        // 配置文件含数据库连接串等敏感信息；落在宿主根会让测试进程连宿主库 / 覆盖宿主配置。
        var hostRoot = RealHostRoot();

        (XCodeSetting.Provider as FileConfigProvider)?.FileName.Should().NotStartWith(hostRoot,
            "XCode 配置不得读写宿主配置目录（连接串风险）");
        (NewLife.Setting.Provider as FileConfigProvider)?.FileName.Should().NotStartWith(hostRoot,
            "NewLife 配置不得读写宿主配置目录");
    }
}