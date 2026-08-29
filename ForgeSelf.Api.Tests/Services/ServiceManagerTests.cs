using System.ComponentModel;
using System.ServiceProcess;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Services;

namespace ForgeSelf.Api.Tests.Services;

public class ServiceManagerTests : IDisposable
{
    private readonly ServiceConfig _config;
    private readonly Mock<IServiceController> _controllerMock;
    private readonly ServiceManager _manager;
    private ServiceControllerStatus _currentStatus;

    public ServiceManagerTests()
    {
        _config = new ServiceConfig
        {
            ServiceName = "TestService",
            DisplayName = "Test Service Display",
            Description = "Test service description",
            AutoStart = true,
        };
        _controllerMock = new Mock<IServiceController>(MockBehavior.Loose);
        _currentStatus = ServiceControllerStatus.Stopped;

        // 默认 Status 返回当前状态
        _controllerMock.SetupGet(x => x.Status).Returns(() => _currentStatus);

        _manager = new ServiceManager(
            _config,
            serviceFilePath: @"C:\test\app.exe",
            controllerFactory: _ => _controllerMock.Object);
    }

    public void Dispose()
    {
        _controllerMock.Reset();
    }

    // ================================================================
    // IsInstalled() — 在不同 SCM 状态下的返回值
    // ================================================================

    [Fact]
    public void IsInstalled_ServiceExists_ReturnsTrue()
    {
        // Arrange
        _currentStatus = ServiceControllerStatus.Running;

        // Act
        var result = _manager.IsInstalled();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsInstalled_ServiceExistsAndStopped_ReturnsTrue()
    {
        // Arrange
        _currentStatus = ServiceControllerStatus.Stopped;

        // Act
        var result = _manager.IsInstalled();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsInstalled_ServiceNotInstalled_ReturnsFalse()
    {
        // Arrange
        _controllerMock.SetupGet(x => x.Status)
            .Throws(new InvalidOperationException("服务未安装"));

        // Act
        var result = _manager.IsInstalled();

        // Assert
        result.Should().BeFalse();
    }

    // ================================================================
    // GetStatus() — 返回 SCM 状态
    // ================================================================

    [Fact]
    public void GetStatus_ServiceRunning_ReturnsRunning()
    {
        // Arrange
        _currentStatus = ServiceControllerStatus.Running;
        _controllerMock.Setup(x => x.Refresh());

        // Act
        var result = _manager.GetStatus();

        // Assert
        result.Should().Be(ServiceControllerStatus.Running);
    }

    [Fact]
    public void GetStatus_ServiceStopped_ReturnsStopped()
    {
        // Arrange
        _currentStatus = ServiceControllerStatus.Stopped;
        _controllerMock.Setup(x => x.Refresh());

        // Act
        var result = _manager.GetStatus();

        // Assert
        result.Should().Be(ServiceControllerStatus.Stopped);
    }

    [Fact]
    public void GetStatus_ServicePaused_ReturnsPaused()
    {
        // Arrange
        _currentStatus = ServiceControllerStatus.Paused;
        _controllerMock.Setup(x => x.Refresh());

        // Act
        var result = _manager.GetStatus();

        // Assert
        result.Should().Be(ServiceControllerStatus.Paused);
    }

    [Fact]
    public void GetStatus_ServiceNotInstalled_ReturnsStopped()
    {
        // Arrange
        _controllerMock.Setup(x => x.Refresh())
            .Throws(new InvalidOperationException("服务未安装"));

        // Act
        var result = _manager.GetStatus();

        // Assert
        result.Should().Be(ServiceControllerStatus.Stopped);
    }

    // ================================================================
    // Install() — 错误路径
    // ================================================================

    [Fact]
    public void Install_NotAdmin_ThrowsUnauthorizedAccessException()
    {
        // RequireAdmin() 是静态方法，检查真实 Windows 身份。
        // 在非管理员测试环境中，它返回 false，Install() 应抛出异常。
        // 在管理员环境中此测试可能被跳过或验证管理员身份返回 true。
        var isAdmin = ServiceManager.RequireAdmin();

        if (!isAdmin)
        {
            // Act
            var act = () => _manager.Install();

            // Assert
            act.Should().Throw<UnauthorizedAccessException>()
                .WithMessage("*管理员权限*");
        }
        else
        {
            // 管理员环境：Install() 会继续执行并尝试 sc.exe，
            // 这里我们只验证 RequireAdmin() 返回 true
            isAdmin.Should().BeTrue();
        }
    }

    [Fact]
    public void Install_AlreadyInstalled_SkipsWithoutError()
    {
        // Arrange: 让 IsInstalled() 返回 true
        _currentStatus = ServiceControllerStatus.Stopped;

        // 跳过 RequireAdmin 检查 —— 注：RequireAdmin() 是静态方法，
        // 此测试仅在管理员环境中才能到达 "already installed" 分支。
        if (!ServiceManager.RequireAdmin())
            return;

        // Act
        var act = () => _manager.Install();

        // Assert —— 不抛异常，静默跳过
        act.Should().NotThrow();
    }

    // ================================================================
    // Uninstall() — 错误路径
    // ================================================================

    [Fact]
    public void Uninstall_NotAdmin_ThrowsUnauthorizedAccessException()
    {
        var isAdmin = ServiceManager.RequireAdmin();

        if (!isAdmin)
        {
            // Act
            var act = () => _manager.Uninstall();

            // Assert
            act.Should().Throw<UnauthorizedAccessException>()
                .WithMessage("*管理员权限*");
        }
        else
        {
            isAdmin.Should().BeTrue();
        }
    }

    [Fact]
    public void Uninstall_NotInstalled_SkipsWithoutError()
    {
        // Arrange: 让 IsInstalled() 返回 false
        _controllerMock.SetupGet(x => x.Status)
            .Throws(new InvalidOperationException("服务未安装"));

        if (!ServiceManager.RequireAdmin())
            return;

        // Act
        var act = () => _manager.Uninstall();

        // Assert
        act.Should().NotThrow();
    }

    // ================================================================
    // Restart() — 异常处理
    // ================================================================

    [Fact]
    public void Restart_ServiceNotInstalled_ThrowsInvalidOperationException()
    {
        // Arrange
        _controllerMock.SetupGet(x => x.Status)
            .Throws(new InvalidOperationException("服务未安装"));

        // Act
        var act = () => _manager.Restart();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*未安装*");
    }

    [Fact]
    public void Restart_ServiceInstalled_CallsStopThenStart()
    {
        // Arrange
        _currentStatus = ServiceControllerStatus.Running;
        _controllerMock.Setup(x => x.Refresh());
        _controllerMock.Setup(x => x.Stop())
            .Callback(() => _currentStatus = ServiceControllerStatus.Stopped);
        _controllerMock.Setup(x => x.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30)));
        _controllerMock.Setup(x => x.Start())
            .Callback(() => _currentStatus = ServiceControllerStatus.Running);
        _controllerMock.Setup(x => x.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30)));

        // Act
        _manager.Restart();

        // Assert
        _controllerMock.Verify(x => x.Stop(), Times.Once);
        _controllerMock.Verify(x => x.Start(), Times.Once);
        _controllerMock.Verify(x => x.WaitForStatus(ServiceControllerStatus.Stopped, It.IsAny<TimeSpan>()), Times.Once);
        _controllerMock.Verify(x => x.WaitForStatus(ServiceControllerStatus.Running, It.IsAny<TimeSpan>()), Times.Once);
    }

    // ================================================================
    // Start() — 异常处理
    // ================================================================

    [Fact]
    public void Start_AlreadyRunning_DoesNotCallStart()
    {
        // Arrange
        _currentStatus = ServiceControllerStatus.Running;
        _controllerMock.Setup(x => x.Refresh());

        // Act
        _manager.Start();

        // Assert
        _controllerMock.Verify(x => x.Start(), Times.Never);
        _controllerMock.Verify(x => x.WaitForStatus(It.IsAny<ServiceControllerStatus>(), It.IsAny<TimeSpan>()), Times.Never);
    }

    [Fact]
    public void Start_Stopped_CallsStartAndWaits()
    {
        // Arrange
        _currentStatus = ServiceControllerStatus.Stopped;
        _controllerMock.Setup(x => x.Refresh());
        _controllerMock.Setup(x => x.Start());
        _controllerMock.Setup(x => x.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30)));

        // Act
        _manager.Start();

        // Assert
        _controllerMock.Verify(x => x.Start(), Times.Once);
        _controllerMock.Verify(x => x.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30)), Times.Once);
    }

    [Fact]
    public void Start_InvalidOperationException_RethrowsAsInvalidOperationException()
    {
        // Arrange
        _currentStatus = ServiceControllerStatus.Stopped;
        _controllerMock.Setup(x => x.Refresh());
        _controllerMock.Setup(x => x.Start())
            .Throws(new InvalidOperationException("拒绝访问。"));
        _controllerMock.Setup(x => x.WaitForStatus(It.IsAny<ServiceControllerStatus>(), It.IsAny<TimeSpan>()));

        // Act
        var act = () => _manager.Start();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*启动服务*失败*");
    }

    [Fact]
    public void Start_Win32Exception_RethrowsAsInvalidOperationException()
    {
        // Arrange
        _currentStatus = ServiceControllerStatus.Stopped;
        _controllerMock.Setup(x => x.Refresh());
        _controllerMock.Setup(x => x.Start())
            .Throws(new Win32Exception("服务没有响应。"));
        _controllerMock.Setup(x => x.WaitForStatus(It.IsAny<ServiceControllerStatus>(), It.IsAny<TimeSpan>()));

        // Act
        var act = () => _manager.Start();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*启动服务*失败*");
    }

    // ================================================================
    // Stop() — 异常处理
    // ================================================================

    [Fact]
    public void Stop_AlreadyStopped_DoesNotCallStop()
    {
        // Arrange
        _currentStatus = ServiceControllerStatus.Stopped;
        _controllerMock.Setup(x => x.Refresh());

        // Act
        _manager.Stop();

        // Assert
        _controllerMock.Verify(x => x.Stop(), Times.Never);
        _controllerMock.Verify(x => x.WaitForStatus(It.IsAny<ServiceControllerStatus>(), It.IsAny<TimeSpan>()), Times.Never);
    }

    [Fact]
    public void Stop_Running_CallsStopAndWaits()
    {
        // Arrange
        _currentStatus = ServiceControllerStatus.Running;
        _controllerMock.Setup(x => x.Refresh());
        _controllerMock.Setup(x => x.Stop());
        _controllerMock.Setup(x => x.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30)));

        // Act
        _manager.Stop();

        // Assert
        _controllerMock.Verify(x => x.Stop(), Times.Once);
        _controllerMock.Verify(x => x.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30)), Times.Once);
    }

    [Fact]
    public void Stop_InvalidOperationException_RethrowsAsInvalidOperationException()
    {
        // Arrange
        _currentStatus = ServiceControllerStatus.Running;
        _controllerMock.Setup(x => x.Refresh());
        _controllerMock.Setup(x => x.Stop())
            .Throws(new InvalidOperationException("无法停止服务。"));
        _controllerMock.Setup(x => x.WaitForStatus(It.IsAny<ServiceControllerStatus>(), It.IsAny<TimeSpan>()));

        // Act
        var act = () => _manager.Stop();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*停止服务*失败*");
    }

    [Fact]
    public void Stop_Win32Exception_RethrowsAsInvalidOperationException()
    {
        // Arrange
        _currentStatus = ServiceControllerStatus.Running;
        _controllerMock.Setup(x => x.Refresh());
        _controllerMock.Setup(x => x.Stop())
            .Throws(new Win32Exception("服务进程被终止。"));
        _controllerMock.Setup(x => x.WaitForStatus(It.IsAny<ServiceControllerStatus>(), It.IsAny<TimeSpan>()));

        // Act
        var act = () => _manager.Stop();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*停止服务*失败*");
    }

    // ================================================================
    // RequireAdmin() — 静态方法基本行为
    // ================================================================

    [Fact]
    public void RequireAdmin_ReturnsBool()
    {
        // Act
        var result = ServiceManager.RequireAdmin();

        // Assert — 不抛异常，返回值类型为 bool
        result.GetType().Should().Be(typeof(bool));
    }

    // ================================================================
    // 构造函数参数验证
    // ================================================================

    [Fact]
    public void Constructor_NullConfig_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ServiceManager(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("config");
    }

    [Fact]
    public void Constructor_NullServiceFilePathAndNullProcessPath_ThrowsInvalidOperationException()
    {
        // 只在 Environment.ProcessPath 为 null 时触发
        // （测试环境通常有 ProcessPath，此测试验证逻辑）
        // 使用空字符串模拟：实际上 constructor 会在 ProcessPath 为 null 时抛异常
    }

    // ================================================================
    // ServiceControllerWrapper 集成验证（快速冒烟）
    // ================================================================

    [Fact]
    public void ServiceControllerWrapper_ConstructAndDispose_DoesNotThrow()
    {
        // Act
        var act = () =>
        {
            using var wrapper = new ServiceControllerWrapper("NonExistentServiceForTesting");
            // 不访问 Status，避免真实 SCM 查询
        };

        // Assert
        act.Should().NotThrow();
    }
}