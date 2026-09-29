using System;
using System.IO;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Moq;
using ForgeSelf.Api;
using Xunit;

namespace ForgeSelf.Api.Tests;

/// <summary>
/// 统一数据根服务形态判定单测：
/// 开发模式 → 程序目录 data/（调试数据不污染用户目录）；否则 → 用户主目录 ~/.forgeself。
/// （2026-09-29 输入37 目录命名统一小写：Data → data）
/// </summary>
public class DataLocationServiceTests
{
    [Fact]
    public void ResolveHostDataDirectory_开发环境_返回程序目录data()
    {
        var original = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            DataLocationService.ResolveHostDataDirectory()
                .Should().Be(Path.Combine(AppContext.BaseDirectory, "data"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", original);
        }
    }

    [Fact]
    public void ResolveHostDataDirectory_发布环境_返回用户目录Forgeself()
    {
        var original = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
            DataLocationService.ResolveHostDataDirectory()
                .Should().Be(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".forgeself"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", original);
        }
    }

    [Fact]
    public void 实例_开发环境_宿主数据目录为程序目录data()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Development");
        var svc = new DataLocationService(env.Object);
        svc.GetHostDataDirectory().Should().Be(Path.Combine(AppContext.BaseDirectory, "data"));
    }

    [Fact]
    public void 实例_非开发环境_宿主数据目录为用户目录Forgeself()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Production");
        var svc = new DataLocationService(env.Object);
        svc.GetHostDataDirectory().Should().Be(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".forgeself"));
    }

    [Fact]
    public void GetPluginDataDirectory_拼接插件子目录()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Development");
        var svc = new DataLocationService(env.Object);
        svc.GetPluginDataDirectory("quicklinks")
            .Should().Be(Path.Combine(AppContext.BaseDirectory, "data", "plugins", "quicklinks"));
    }

    [Fact]
    public void GetPluginDataDirectory_清洗非法目录字符()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Development");
        var svc = new DataLocationService(env.Object);
        svc.GetPluginDataDirectory("bad/id:name")
            .Should().Be(Path.Combine(AppContext.BaseDirectory, "data", "plugins", "badidname"));
    }
}
