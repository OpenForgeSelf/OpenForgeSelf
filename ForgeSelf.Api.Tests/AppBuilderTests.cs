using System.IO;
using FluentAssertions;
using ForgeSelf.Api;
using Xunit;

namespace ForgeSelf.Api.Tests;

/// <summary>
/// AppBuilder 基础设施回归测试。
/// 重点保护 SPA Fallback 中间件对 <c>WebRootPath</c> 为 null 的兜底逻辑
/// （服务启动时 wwwroot 不存在会导致 WebRootPath=null，曾引发 Path.Combine 抛 ArgumentNullException）。
/// </summary>
public class AppBuilderTests
{
    [Theory]
    [InlineData(null, "/app")]
    [InlineData("", "/app")]
    [InlineData("   ", "/app")]
    public void ResolveSpaWebRootPath_空WebRoot回退到ContentRoot下的wwwroot(string? webRoot, string contentRoot)
    {
        // 空（含 null/空白）时回退到 {ContentRoot}/wwwroot，期望值随平台目录分隔符变化。
        var expected = Path.Combine(contentRoot, "wwwroot");
        AppBuilder.ResolveSpaWebRootPath(webRoot, contentRoot).Should().Be(expected);
    }

    [Theory]
    [InlineData("/app/wwwroot", "/app")]
    [InlineData("C:/custom/web", "/app")]
    public void ResolveSpaWebRootPath_非空WebRoot原样返回(string webRoot, string contentRoot)
    {
        AppBuilder.ResolveSpaWebRootPath(webRoot, contentRoot).Should().Be(webRoot);
    }
}

/// <summary>
/// 验证 ResolveWebRootPath：必须以 exe 目录为基准，兼容开发期项目目录，且兜底非空。
/// 用临时目录模拟不同启动位置，避免从其他文件夹拉起 exe 时找不到 wwwroot。
/// </summary>
public class AppBuilderWebRootTests
{
    [Fact]
    public void ResolveWebRootPath_优先取exe目录下的wwwroot()
    {
        var baseDir = CreateTempDirWithWwwroot();
        var contentDir = CreateTempDir();
        try
        {
            AppBuilder.ResolveWebRootPath(baseDir, contentDir)
                .Should().Be(Path.Combine(baseDir, "wwwroot"));
        }
        finally
        {
            // 数据安全铁律：测试自建临时目录只创建、不自动删除
        }
    }

    [Fact]
    public void ResolveWebRootPath_exe目录无wwwroot时回退项目目录()
    {
        var baseDir = CreateTempDir();
        var contentDir = CreateTempDirWithWwwroot();
        try
        {
            AppBuilder.ResolveWebRootPath(baseDir, contentDir)
                .Should().Be(Path.Combine(contentDir, "wwwroot"));
        }
        finally
        {
            // 数据安全铁律：测试自建临时目录只创建、不自动删除
        }
    }

    [Fact]
    public void ResolveWebRootPath_均无index时回退exe目录且非空()
    {
        var baseDir = CreateTempDir();
        var contentDir = CreateTempDir();
        try
        {
            var result = AppBuilder.ResolveWebRootPath(baseDir, contentDir);
            result.Should().Be(Path.Combine(baseDir, "wwwroot"));
            result.Should().NotBeNullOrEmpty();
        }
        finally
        {
            // 数据安全铁律：测试自建临时目录只创建、不自动删除
        }
    }

    private static string CreateTempDir()
        => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ofs_webroot_test_" + Guid.NewGuid().ToString("N"))).FullName;

    private static string CreateTempDirWithWwwroot()
    {
        var dir = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(dir, "wwwroot"));
        File.WriteAllText(Path.Combine(dir, "wwwroot", "index.html"), "<!doctype html>");
        return dir;
    }
}
