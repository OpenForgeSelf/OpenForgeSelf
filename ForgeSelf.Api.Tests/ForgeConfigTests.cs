using System;
using System.IO;
using FluentAssertions;
using NewLife.Configuration;
using ForgeSelf.Api.Models;
using Xunit;

namespace ForgeSelf.Api.Tests;

/// <summary>
/// ForgeConfig 基类防漏回归：派生配置类的配置文件必须归一到数据根 config/ 下，
/// 任何继承 ForgeConfig 的配置类都自动生效，无需各自写路径。
/// （断言依赖 NewLife Config&lt;T&gt;.Provider.FileName 这一进程级全局状态，故归入 SharedGlobalState 集合串行执行。
///  2026-09-29 输入37 目录命名统一小写：Config → config）
/// </summary>
[Collection("SharedGlobalState")]
public class ForgeConfigTests
{
    [Fact]
    public void ForgeSetting_配置文件归一到数据根config目录()
    {
        var original = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            // 开发环境：配置应落在程序目录 data/config（不污染用户目录）
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            // 首次访问 Current 会 new ForgeSetting()，触发 ForgeConfig<ForgeSetting> 静态构造（覆盖 FileName）
            _ = ForgeSetting.Current;
            var provider = ForgeSetting.Provider;
            provider.Should().BeAssignableTo<FileConfigProvider>("ForgeConfig 基于文件配置提供器（XmlConfigProvider 继承之）");
            var fcp = (FileConfigProvider)provider;
            // 关键不变量：配置文件必须归一到「绝对数据根/config/{Name}.config」，
            // 不再落默认相对路径 Config\{Name}（程序目录）。与开发/发布形态无关。
            fcp.FileName.Should().NotBeNullOrEmpty();
            Path.IsPathRooted(fcp.FileName).Should().BeTrue("配置文件路径必须是绝对路径，已归一到数据根");
            fcp.FileName.Should().EndWith(
                Path.Combine("config", "ForgeSetting.config"),
                "配置文件名必须收敛为 config/ForgeSetting.config");
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", original);
        }
    }
}
