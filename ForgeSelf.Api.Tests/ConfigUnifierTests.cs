using System;
using System.IO;
using FluentAssertions;
using NewLife;
using NewLife.Configuration;
using ForgeSelf.Api.Models;
using XCode;
using Xunit;

namespace ForgeSelf.Api.Tests;

/// <summary>
/// 验证 ConfigUnifier 把框架与项目所有 Config<T> 配置文件统一收敛到指定目录。
/// 仅设置 FileName（字符串），不触发 .Current/.Save，不会向磁盘写入真实配置文件。
/// （改写 NewLife Config&lt;T&gt;.Provider.FileName 这一进程级全局状态，故归入 SharedGlobalState 集合串行执行。）
/// </summary>
[Collection("SharedGlobalState")]
public class ConfigUnifierTests
{
    [Fact]
    public void UnifyAllConfigFiles_框架与项目配置均重定向到统一目录()
    {
        var configDir = Path.Combine(Path.GetTempPath(), "ofs_configuni_" + Guid.NewGuid().ToString("N"));
        // 进程级全局状态铁律：先保存 KnownConfigs 全部 FileName，finally 全量还原，
        // 否则泄漏会污染 SharedGlobalState 集合中的 ForgeConfigTests 等默认路径断言（B9 收官全量实证）。
        var savedFileNames = new (IConfigProvider Provider, string? FileName)[]
        {
            (XCodeSetting.Provider, (XCodeSetting.Provider as FileConfigProvider)?.FileName),
            (NewLife.Setting.Provider, (NewLife.Setting.Provider as FileConfigProvider)?.FileName),
            (NewLife.Agent.Setting.Provider, (NewLife.Agent.Setting.Provider as FileConfigProvider)?.FileName),
            (ForgeSetting.Provider, (ForgeSetting.Provider as FileConfigProvider)?.FileName),
        };
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
            // 还原全部 FileName（全局状态 save+restore 铁律）
            foreach (var (provider, fileName) in savedFileNames)
            {
                if (provider is FileConfigProvider fcp && fileName != null)
                    fcp.FileName = fileName;
            }
            // 数据安全铁律：测试自建配置目录只创建、不自动删除
        }
    }
}
