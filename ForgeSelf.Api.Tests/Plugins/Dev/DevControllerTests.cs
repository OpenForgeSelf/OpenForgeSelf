using ForgeSelf.Api.Controllers;
using ForgeSelf.Api.Plugins;
using ForgeSelf.Api.Plugins.Abstractions;
using ForgeSelf.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ForgeSelf.Api.Tests.Plugins.Dev;

/// <summary>
/// DevController 行为测试：dev-off 404 语义（AC-3 的单测面）、reload 404、诊断形状。
/// PluginManager 用空插件目录构造（真实实例，无 mock manager）。
/// </summary>
public class DevControllerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PluginManager _manager;

    public DevControllerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dev-controller-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        var services = new ServiceCollection();
        _manager = new PluginManager(
            services.BuildServiceProvider(),
            new Mock<IPermissionChecker>().Object);
        _manager.SetPluginsDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    private DevController CreateController(Func<bool> gate) =>
        new(_manager, gate)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    [Fact]
    public void Diagnostics_DevOff_Returns404()
    {
        var controller = CreateController(() => false);

        var result = controller.Diagnostics();

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void Reload_DevOff_Returns404()
    {
        var controller = CreateController(() => false);

        var result = controller.Reload("any");

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void ReloadAll_DevOff_Returns404()
    {
        var controller = CreateController(() => false);

        var result = controller.ReloadAll();

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void Reload_UnknownPlugin_Returns404WithMessage()
    {
        var controller = CreateController(() => true);

        var result = controller.Reload("no-such-plugin");

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void Diagnostics_DevOn_EmptyManager_ReturnsWellFormedShape()
    {
        var controller = CreateController(() => true);

        var result = controller.Diagnostics();
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = (ApiResponse<DevDiagnosticsDto>)ok.Value!;

        payload.Data.Should().NotBeNull();
        // 不断言 DevMode.Enabled 的值：DevMode 是进程级静态，DevModeTests 的 env 矩阵用例
        // 会并发翻转它（xUnit 跨类并行），断言其值会引入竞态；gate 行为已由 404 用例覆盖。
        payload.Data!.Plugins.Should().BeEmpty();
        payload.Data!.Shadow.Should().NotBeNull();
        payload.Data!.LogTail.Should().NotBeNull();
    }

    [Fact]
    public void Diagnostics_DevOn_ReportsFlagsAndPluginsDirectory()
    {
        var controller = CreateController(() => true);

        var result = controller.Diagnostics();
        var ok = (OkObjectResult)result.Result!;
        var payload = (ApiResponse<DevDiagnosticsDto>)ok.Value!;

        payload.Data!.DevMode.PluginsDirectory.Should().Be(_tempDir);
    }
}
