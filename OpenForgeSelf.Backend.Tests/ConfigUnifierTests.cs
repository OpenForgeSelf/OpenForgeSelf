using System;
using System.IO;
using FluentAssertions;
using NewLife;
using NewLife.Configuration;
using OpenForgeSelf.Backend.Models;
using XCode;
using Xunit;

namespace OpenForgeSelf.Backend.Tests;

/// <summary>
/// 验证 ConfigUnifier 把框架与项目所有 Config<T> 配置文件统一收敛到指定目录。
/// 仅设置 FileName（字符串），不触发 .Current/.Save，不会向磁盘写入真实配置文件。
/// </summary>
public class ConfigUnifierTests
{
    [Fact]
    public void UnifyAllConfigFiles_框架与项目配置均重定向到统一目录()
    {
        var configDir = Path.Combine(Path.GetTempPath(), "ofs_configuni_" + Guid.NewGuid().ToString("N"));
        try
        {
            ConfigUnifier.UnifyAllConfigFiles(configDir);

            // 框架 XCode 配置 → XCode.config
            var xcodeFcp = XCodeSetting.Provider as FileConfigProvider;
            xcodeFcp.Should().NotBeNull("XCodeSetting 基于文件配置提供器");
            xcodeFcp!.FileName.Should().Be(Path.Combine(configDir, "XCode.config"));

            // 框架 Core 配置 → Core.config
            var coreFcp = NewLife.Setting.Provider as FileConfigProvider;
            coreFcp.Should().NotBeNull("NewLife.Setting 基于文件配置提供器");
            coreFcp!.FileName.Should().Be(Path.Combine(configDir, "Core.config"));

            // 项目自有 ForgeSetting → ForgeSetting.config（继承 ForgeConfig<T>，同样被统一覆盖）
            var forgeFcp = ForgeSetting.Provider as FileConfigProvider;
            forgeFcp.Should().NotBeNull("ForgeSetting 基于文件配置提供器");
            forgeFcp!.FileName.Should().Be(Path.Combine(configDir, "ForgeSetting.config"));
        }
        finally
        {
            try { if (Directory.Exists(configDir)) Directory.Delete(configDir, true); }
            catch { /* 临时目录清理失败不影响测试结论 */ }
        }
    }
}
