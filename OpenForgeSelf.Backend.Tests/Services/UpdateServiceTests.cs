using System.IO.Compression;
using System.Net;
using System.ServiceProcess;
using FluentAssertions;
using Moq;
using Moq.Protected;
using OpenForgeSelf.Backend.Models;
using OpenForgeSelf.Backend.Services;

namespace OpenForgeSelf.Backend.Tests.Services;

/// <summary>
/// UpdateService 单元测试。
/// 测试更新流程编排、文件替换失败回滚、新版本启动失败回滚、健康检查失败回滚。
/// Mock <see cref="IServiceManager"/>，使用 test double 重写 <see cref="UpdateChecker"/>
/// 的 virtual 方法，文件系统操作使用真实临时目录。
/// 
/// 注意：<see cref="UpdateService"/> 的 <c>RecordUpdateTrace</c> 为 private static 方法，
/// 直接使用 XCode <c>UpdateTrace</c> 实体调用 <c>Insert()</c>，无法在单元测试中 mock。
/// 因此 UpdateTrace 记录正确性通过间接验证：正确返回值和步骤调用顺序。
/// </summary>
public class UpdateServiceTests : IDisposable
{
    private readonly TestUpdateChecker _updateChecker;
    private readonly Mock<IServiceManager> _mockServiceManager;
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly HttpClient _httpClient;
    private readonly UpdateConfig _updateConfig;
    private readonly ServiceConfig _serviceConfig;
    private readonly string _testAppName;
    private readonly string _testAppVersion;
    private readonly string _appDir;
    private readonly string _backupDir;
    private readonly UpdateCheckResult _availableUpdate;

    // 用于 file-replace 测试的文件锁
    private FileStream? _lockedFile;

    public UpdateServiceTests()
    {
        _testAppName = "UpdateServiceTestApp";
        _testAppVersion = "1.0.0.0";

        // 创建临时应用目录
        _appDir = Path.Combine(Path.GetTempPath(), $"{_testAppName}_AppDir_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_appDir);

        // 创建初始文件
        File.WriteAllText(Path.Combine(_appDir, "app.dll"), "original dll content");
        File.WriteAllText(Path.Combine(_appDir, "config.json"), """{"version":"1.0.0"}""");
        Directory.CreateDirectory(Path.Combine(_appDir, "wwwroot"));
        File.WriteAllText(Path.Combine(_appDir, "wwwroot", "index.html"), "<html>original</html>");

        _backupDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            _testAppName,
            "Backups");

        _updateConfig = new UpdateConfig
        {
            ServerUrl = "http://test-update-server.com",
            Channel = "stable",
            CheckTimeoutSeconds = 5,
            DownloadTimeoutSeconds = 300,
        };

        _serviceConfig = new ServiceConfig
        {
            ServiceName = "TestService",
            DisplayName = "Test Service",
            Description = "Test service for unit tests",
            AutoStart = true,
        };

        _mockHttpHandler = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        _httpClient = new HttpClient(_mockHttpHandler.Object);

        // 使用 test double（继承自 UpdateChecker，重写 virtual 方法）
        _updateChecker = new TestUpdateChecker(_updateConfig, _httpClient, _testAppName, _testAppVersion);

        _mockServiceManager = new Mock<IServiceManager>(MockBehavior.Strict);

        _availableUpdate = new UpdateCheckResult
        {
            HasUpdate = true,
            CurrentVersion = new Version("1.0.0.0"),
            LatestVersion = new Version("2.0.0.0"),
            DownloadUrl = "http://test-update-server.com/pkg/2.0.0.0.zip",
            PackageHash = "sha256:abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890",
            PackageSize = 1024,
            CheckTime = DateTime.UtcNow,
            IsSuccess = true,
        };
    }

    public void Dispose()
    {
        _lockedFile?.Dispose();
        _httpClient.Dispose();
        _mockHttpHandler.Reset();

        // 清理临时目录
        try
        {
            if (Directory.Exists(_appDir))
                Directory.Delete(_appDir, recursive: true);
        }
        catch { /* 忽略清理失败 */ }

        try
        {
            if (Directory.Exists(_backupDir))
                Directory.Delete(_backupDir, recursive: true);
        }
        catch { /* 忽略清理失败 */ }
    }

    // ================================================================
    // 辅助方法
    // ================================================================

    /// <summary>
    /// 创建 UpdateService 实例，所有可选参数使用测试值。
    /// </summary>
    private UpdateService CreateService()
    {
        return new UpdateService(
            _updateChecker,
            _mockServiceManager.Object,
            _updateConfig,
            _serviceConfig,
            _httpClient,
            _testAppName,
            _testAppVersion,
            _appDir);
    }

    /// <summary>
    /// 设置 Mock ServiceManager 为已安装+运行中状态。
    /// </summary>
    private void SetupMockServiceInstalledAndRunning()
    {
        _mockServiceManager
            .Setup(x => x.IsInstalled())
            .Returns(true);
        _mockServiceManager
            .Setup(x => x.GetStatus())
            .Returns(ServiceControllerStatus.Running);
    }

    /// <summary>
    /// 设置 Mock ServiceManager 为已安装+停止状态。
    /// </summary>
    private void SetupMockServiceInstalledAndStopped()
    {
        _mockServiceManager
            .Setup(x => x.IsInstalled())
            .Returns(true);
        _mockServiceManager
            .Setup(x => x.GetStatus())
            .Returns(ServiceControllerStatus.Stopped);
    }

    /// <summary>
    /// 设置 Mock ServiceManager 为未安装状态。
    /// </summary>
    private void SetupMockServiceNotInstalled()
    {
        _mockServiceManager
            .Setup(x => x.IsInstalled())
            .Returns(false);
    }

    /// <summary>
    /// 设置健康检查 HTTP mock 返回指定状态码。
    /// </summary>
    private void SetupHealthCheckResponse(HttpStatusCode statusCode)
    {
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri != null && r.RequestUri.AbsolutePath == "/health"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage { StatusCode = statusCode });
    }

    /// <summary>
    /// 设置健康检查 HTTP mock 抛出异常。
    /// </summary>
    private void SetupHealthCheckException(Exception exception)
    {
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri != null && r.RequestUri.AbsolutePath == "/health"),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(exception);
    }

    // ================================================================
    // CheckForUpdatesAsync — 委托给 UpdateChecker
    // ================================================================

    [Fact]
    public async Task CheckForUpdatesAsync_DelegatesToUpdateChecker()
    {
        // Arrange
        _updateChecker.CheckForUpdateResult = new UpdateCheckResult
        {
            HasUpdate = true,
            LatestVersion = new Version("2.0.0.0"),
            IsSuccess = true,
        };

        var service = CreateService();

        // Act
        var result = await service.CheckForUpdatesAsync();

        // Assert
        result.HasUpdate.Should().BeTrue();
        result.LatestVersion.Should().Be(new Version("2.0.0.0"));
        _updateChecker.CheckForUpdateCallCount.Should().Be(1);
    }

    // ================================================================
    // ApplyUpdateAsync — 参数验证
    // ================================================================

    [Fact]
    public async Task ApplyUpdateAsync_NullCheckResult_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateService();

        // Act
        var act = () => service.ApplyUpdateAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ApplyUpdateAsync_NoUpdate_ReturnsFalse()
    {
        // Arrange
        var noUpdate = new UpdateCheckResult
        {
            HasUpdate = false,
            CurrentVersion = new Version("1.0.0.0"),
            LatestVersion = new Version("1.0.0.0"),
            IsSuccess = true,
        };
        var service = CreateService();

        // Act
        var result = await service.ApplyUpdateAsync(noUpdate);

        // Assert
        result.Should().BeFalse();
        _updateChecker.DownloadPackageCallCount.Should().Be(0);
    }

    [Fact]
    public async Task ApplyUpdateAsync_NullLatestVersion_ThrowsInvalidOperationException()
    {
        // Arrange
        var invalid = new UpdateCheckResult
        {
            HasUpdate = true,
            LatestVersion = null,
            IsSuccess = true,
        };
        var service = CreateService();

        // Act
        var act = () => service.ApplyUpdateAsync(invalid);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*缺少最新版本号*");
    }

    // ================================================================
    // 完整更新流程编排 — 成功路径
    // ================================================================

    [Fact]
    public async Task ApplyUpdateAsync_FullFlow_Success_WithServiceInstalled()
    {
        // Arrange
        SetupMockServiceInstalledAndRunning();
        _mockServiceManager.Setup(x => x.Stop());
        _mockServiceManager.Setup(x => x.Start());
        SetupHealthCheckResponse(HttpStatusCode.OK);

        // 设置 test double：下载时创建 ZIP 包，校验通过
        _updateChecker.DownloadPackageCallback = CreateZipWithNewVersion;
        _updateChecker.VerifyPackageResult = true;

        var service = CreateService();

        // Act
        var result = await service.ApplyUpdateAsync(_availableUpdate);

        // Assert
        result.Should().BeTrue();

        // 验证 UpdateChecker 调用
        _updateChecker.DownloadPackageCallCount.Should().Be(1);
        _updateChecker.VerifyPackageCallCount.Should().Be(1);

        // 验证 ServiceManager 调用链：先停止，后启动
        _mockServiceManager.Verify(x => x.Stop(), Times.Once);
        _mockServiceManager.Verify(x => x.Start(), Times.Once);

        // 验证文件已被替换（新版本文件存在）
        var appDllContent = File.ReadAllText(Path.Combine(_appDir, "app.dll"));
        appDllContent.Should().Be("new version dll content");

        var configContent = File.ReadAllText(Path.Combine(_appDir, "config.json"));
        configContent.Should().Contain("2.0.0");

        var indexContent = File.ReadAllText(Path.Combine(_appDir, "wwwroot", "index.html"));
        indexContent.Should().Be("<html>updated</html>");

        // 验证新文件被添加
        File.Exists(Path.Combine(_appDir, "new_feature.dll")).Should().BeTrue();
    }

    [Fact]
    public async Task ApplyUpdateAsync_FullFlow_Success_WithServiceNotInstalled()
    {
        // Arrange
        SetupMockServiceNotInstalled();
        _updateChecker.DownloadPackageCallback = CreateZipWithNewVersion;
        _updateChecker.VerifyPackageResult = true;

        var service = CreateService();

        // Act
        var result = await service.ApplyUpdateAsync(_availableUpdate);

        // Assert
        result.Should().BeTrue();

        // 服务未安装时，不调用 Stop/Start
        _mockServiceManager.Verify(x => x.Stop(), Times.Never);
        _mockServiceManager.Verify(x => x.Start(), Times.Never);

        // 但文件仍被替换
        var appDllContent = File.ReadAllText(Path.Combine(_appDir, "app.dll"));
        appDllContent.Should().Be("new version dll content");
    }

    [Fact]
    public async Task ApplyUpdateAsync_FullFlow_Success_WithServiceAlreadyStopped()
    {
        // Arrange
        SetupMockServiceInstalledAndStopped();
        _mockServiceManager.Setup(x => x.Start());
        SetupHealthCheckResponse(HttpStatusCode.OK);
        _updateChecker.DownloadPackageCallback = CreateZipWithNewVersion;
        _updateChecker.VerifyPackageResult = true;

        var service = CreateService();

        // Act
        var result = await service.ApplyUpdateAsync(_availableUpdate);

        // Assert
        result.Should().BeTrue();

        // 服务已停止，不调用 Stop，但调用 Start
        _mockServiceManager.Verify(x => x.Stop(), Times.Never);
        _mockServiceManager.Verify(x => x.Start(), Times.Once);
    }

    // ================================================================
    // 文件替换失败回滚
    // ================================================================

    [Fact]
    public async Task ApplyUpdateAsync_FileReplaceFailure_RollbacksToBackup()
    {
        // Arrange
        SetupMockServiceInstalledAndRunning();
        _mockServiceManager.Setup(x => x.Stop());
        _updateChecker.DownloadPackageCallback = CreateZipWithNewVersion;
        _updateChecker.VerifyPackageResult = true;

        // 锁定 app.dll 使其无法被覆盖（独占锁定）
        var lockedFilePath = Path.Combine(_appDir, "app.dll");
        _lockedFile = new FileStream(lockedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        // 健康检查 mock（回滚后不应走到健康检查）
        SetupHealthCheckResponse(HttpStatusCode.OK);

        var service = CreateService();

        // Act
        var result = await service.ApplyUpdateAsync(_availableUpdate);

        // Assert
        result.Should().BeFalse();

        // 释放锁，以便验证文件内容
        _lockedFile.Dispose();
        _lockedFile = null;

        // 验证文件内容仍然是原始内容（已回滚）
        var appDllContent = File.ReadAllText(Path.Combine(_appDir, "app.dll"));
        appDllContent.Should().Be("original dll content");

        var configContent = File.ReadAllText(Path.Combine(_appDir, "config.json"));
        configContent.Should().Contain("1.0.0");

        // 验证服务被停止（回滚前已执行停止）
        _mockServiceManager.Verify(x => x.Stop(), Times.Once);
        // 启动不应被调用（回滚时替换失败，未走到启动步骤）
        _mockServiceManager.Verify(x => x.Start(), Times.Never);
    }

    // ================================================================
    // 新版本启动失败回滚
    // ================================================================

    [Fact]
    public async Task ApplyUpdateAsync_NewVersionStartupFailure_RollbacksToOldVersion()
    {
        // Arrange
        SetupMockServiceInstalledAndRunning();
        _mockServiceManager.Setup(x => x.Stop());
        _updateChecker.DownloadPackageCallback = CreateZipWithNewVersion;
        _updateChecker.VerifyPackageResult = true;

        // 第一次 Start() 被调用时抛出异常（新版本启动失败）
        // 第二次 Start() 用于回滚后重启旧版本
        var startCallCount = 0;
        _mockServiceManager
            .Setup(x => x.Start())
            .Callback(() =>
            {
                startCallCount++;
                if (startCallCount == 1)
                    throw new InvalidOperationException("新版本服务启动超时 (30s)");
                // 第二次调用（回滚重启旧版本）正常通过
            });

        var service = CreateService();

        // Act
        var result = await service.ApplyUpdateAsync(_availableUpdate);

        // Assert
        result.Should().BeFalse();
        startCallCount.Should().Be(2);

        // 验证文件已被回滚到原始内容
        var appDllContent = File.ReadAllText(Path.Combine(_appDir, "app.dll"));
        appDllContent.Should().Be("original dll content");

        var configContent = File.ReadAllText(Path.Combine(_appDir, "config.json"));
        configContent.Should().Contain("1.0.0");

        // 新文件不应存在
        File.Exists(Path.Combine(_appDir, "new_feature.dll")).Should().BeFalse();
    }

    // ================================================================
    // 健康检查失败回滚
    // ================================================================

    [Fact]
    public async Task ApplyUpdateAsync_HealthCheckTimeout_RollbacksToOldVersion()
    {
        // Arrange
        SetupMockServiceInstalledAndRunning();
        _mockServiceManager.Setup(x => x.Stop());
        _updateChecker.DownloadPackageCallback = CreateZipWithNewVersion;
        _updateChecker.VerifyPackageResult = true;

        // 健康检查返回 503（触发回滚）
        SetupHealthCheckResponse(HttpStatusCode.ServiceUnavailable);

        // 第一次 Start() 成功（新版本启动），第二次 Start() 用于回滚后重启旧版本
        _mockServiceManager.Setup(x => x.Start());

        var service = CreateService();

        // Act
        var result = await service.ApplyUpdateAsync(_availableUpdate);

        // Assert
        result.Should().BeFalse();

        // 验证 Start 被调用了两次（新版本启动成功，但健康检查失败后回滚并重启旧版本）
        _mockServiceManager.Verify(x => x.Start(), Times.Exactly(2));

        // 验证文件已被回滚
        var appDllContent = File.ReadAllText(Path.Combine(_appDir, "app.dll"));
        appDllContent.Should().Be("original dll content");
    }

    [Fact]
    public async Task ApplyUpdateAsync_HealthCheckHttpException_RollbacksToOldVersion()
    {
        // Arrange
        SetupMockServiceInstalledAndRunning();
        _mockServiceManager.Setup(x => x.Stop());
        _mockServiceManager.Setup(x => x.Start());
        _updateChecker.DownloadPackageCallback = CreateZipWithNewVersion;
        _updateChecker.VerifyPackageResult = true;

        // 健康检查连接拒绝
        SetupHealthCheckException(new HttpRequestException("连接被拒绝"));

        var service = CreateService();

        // Act
        var result = await service.ApplyUpdateAsync(_availableUpdate);

        // Assert
        result.Should().BeFalse();

        // 验证回滚后文件被恢复
        var appDllContent = File.ReadAllText(Path.Combine(_appDir, "app.dll"));
        appDllContent.Should().Be("original dll content");
    }

    // ================================================================
    // SHA256 校验失败
    // ================================================================

    [Fact]
    public async Task ApplyUpdateAsync_PackageHashMismatch_ThrowsAndReturnsFalse()
    {
        // Arrange
        SetupMockServiceInstalledAndRunning();
        _updateChecker.DownloadPackageCallback = CreateZipWithNewVersion;
        // VerifyPackage 返回 false
        _updateChecker.VerifyPackageResult = false;

        var service = CreateService();

        // Act
        var result = await service.ApplyUpdateAsync(_availableUpdate);

        // Assert
        result.Should().BeFalse();

        // 校验失败后，不应执行停止/启动
        _mockServiceManager.Verify(x => x.Stop(), Times.Never);
        _mockServiceManager.Verify(x => x.Start(), Times.Never);

        // 文件不应被修改
        var appDllContent = File.ReadAllText(Path.Combine(_appDir, "app.dll"));
        appDllContent.Should().Be("original dll content");
    }

    // ================================================================
    // 下载失败
    // ================================================================

    [Fact]
    public async Task ApplyUpdateAsync_DownloadFailure_ReturnsFalseAndPreservesOriginalFiles()
    {
        // Arrange
        _updateChecker.DownloadPackageException = new HttpRequestException("网络不可达");

        var service = CreateService();

        // Act
        var result = await service.ApplyUpdateAsync(_availableUpdate);

        // Assert
        result.Should().BeFalse();

        // 文件不应被修改
        var appDllContent = File.ReadAllText(Path.Combine(_appDir, "app.dll"));
        appDllContent.Should().Be("original dll content");
    }

    // ================================================================
    // UpdateTrace 记录正确性（间接验证）
    // ================================================================

    /// <summary>
    /// 验证成功更新流程中 UpdateTrace 被正确记录（通过不抛出异常和正确的返回值判断）。
    /// RecordUpdateTrace 为 private static 方法，直接调用 XCode UpdateTrace.Insert()，
    /// 无法在单元测试中 mock。但该方法内部有 try-catch 保护，不会因 DB 问题影响流程。
    /// 本测试验证整体流程按预期完成，无异常抛出。
    /// </summary>
    [Fact]
    public async Task ApplyUpdateAsync_SuccessFlow_CompletesWithoutException()
    {
        // Arrange
        SetupMockServiceInstalledAndRunning();
        _mockServiceManager.Setup(x => x.Stop());
        _mockServiceManager.Setup(x => x.Start());
        SetupHealthCheckResponse(HttpStatusCode.OK);
        _updateChecker.DownloadPackageCallback = CreateZipWithNewVersion;
        _updateChecker.VerifyPackageResult = true;

        var service = CreateService();

        // Act
        Func<Task<bool>> act = () => service.ApplyUpdateAsync(_availableUpdate);

        // Assert — 整体流程不抛出异常
        var result = await act.Should().NotThrowAsync();
        result.Subject.Should().BeTrue();
    }

    /// <summary>
    /// 验证回滚流程中 UpdateTrace 记录不被异常阻断。
    /// 回滚场景下 RecordUpdateTrace 被调用，且不抛出异常。
    /// </summary>
    [Fact]
    public async Task ApplyUpdateAsync_RollbackFlow_CompletesWithoutException()
    {
        // Arrange
        SetupMockServiceInstalledAndRunning();
        _mockServiceManager.Setup(x => x.Stop());
        _updateChecker.DownloadPackageCallback = CreateZipWithNewVersion;
        _updateChecker.VerifyPackageResult = true;

        // 启动失败触发回滚
        _mockServiceManager
            .Setup(x => x.Start())
            .Throws(new InvalidOperationException("启动失败"));

        var service = CreateService();

        // Act
        Func<Task<bool>> act = () => service.ApplyUpdateAsync(_availableUpdate);

        // Assert — 回滚流程不抛出异常，返回 false
        var result = await act.Should().NotThrowAsync();
        result.Subject.Should().BeFalse();
    }

    // ================================================================
    // 边界场景：服务状态检查
    // ================================================================

    [Fact]
    public async Task ApplyUpdateAsync_ServiceIsPaused_StopsBeforeUpdate()
    {
        // Arrange
        _mockServiceManager
            .Setup(x => x.IsInstalled())
            .Returns(true);
        _mockServiceManager
            .Setup(x => x.GetStatus())
            .Returns(ServiceControllerStatus.Paused);
        _mockServiceManager.Setup(x => x.Stop());
        _mockServiceManager.Setup(x => x.Start());
        SetupHealthCheckResponse(HttpStatusCode.OK);
        _updateChecker.DownloadPackageCallback = CreateZipWithNewVersion;
        _updateChecker.VerifyPackageResult = true;

        var service = CreateService();

        // Act
        var result = await service.ApplyUpdateAsync(_availableUpdate);

        // Assert
        result.Should().BeTrue();
        _mockServiceManager.Verify(x => x.Stop(), Times.Once);
        _mockServiceManager.Verify(x => x.Start(), Times.Once);
    }

    [Fact]
    public async Task ApplyUpdateAsync_ServiceIsStartPending_StopsBeforeUpdate()
    {
        // Arrange
        _mockServiceManager
            .Setup(x => x.IsInstalled())
            .Returns(true);
        _mockServiceManager
            .Setup(x => x.GetStatus())
            .Returns(ServiceControllerStatus.StartPending);
        _mockServiceManager.Setup(x => x.Stop());
        _mockServiceManager.Setup(x => x.Start());
        SetupHealthCheckResponse(HttpStatusCode.OK);
        _updateChecker.DownloadPackageCallback = CreateZipWithNewVersion;
        _updateChecker.VerifyPackageResult = true;

        var service = CreateService();

        // Act
        var result = await service.ApplyUpdateAsync(_availableUpdate);

        // Assert
        result.Should().BeTrue();
        _mockServiceManager.Verify(x => x.Stop(), Times.Once);
    }

    // ================================================================
    // 构造函数参数验证
    // ================================================================

    [Fact]
    public void Constructor_NullUpdateChecker_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new UpdateService(
            null!,
            _mockServiceManager.Object,
            _updateConfig,
            _serviceConfig,
            _httpClient,
            _testAppName,
            _testAppVersion,
            _appDir);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("updateChecker");
    }

    [Fact]
    public void Constructor_NullServiceManager_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new UpdateService(
            _updateChecker,
            null!,
            _updateConfig,
            _serviceConfig,
            _httpClient,
            _testAppName,
            _testAppVersion,
            _appDir);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("serviceManager");
    }

    [Fact]
    public void Constructor_NullUpdateConfig_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new UpdateService(
            _updateChecker,
            _mockServiceManager.Object,
            null!,
            _serviceConfig,
            _httpClient,
            _testAppName,
            _testAppVersion,
            _appDir);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("updateConfig");
    }

    [Fact]
    public void Constructor_NullServiceConfig_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new UpdateService(
            _updateChecker,
            _mockServiceManager.Object,
            _updateConfig,
            null!,
            _httpClient,
            _testAppName,
            _testAppVersion,
            _appDir);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("serviceConfig");
    }

    [Fact]
    public void Constructor_NullHttpClient_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new UpdateService(
            _updateChecker,
            _mockServiceManager.Object,
            _updateConfig,
            _serviceConfig,
            null!,
            _testAppName,
            _testAppVersion,
            _appDir);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("httpClient");
    }

    // ================================================================
    // 辅助方法：创建 ZIP 包
    // ================================================================

    /// <summary>
    /// 在指定的 destPath 创建一个包含新版本文件的 ZIP 包。
    /// </summary>
    private static void CreateZipWithNewVersion(string version, string destPath)
    {
        var dir = Path.GetDirectoryName(destPath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        using var zipStream = new FileStream(destPath, FileMode.Create);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

        var dllEntry = archive.CreateEntry("app.dll");
        using (var writer = new StreamWriter(dllEntry.Open()))
            writer.Write("new version dll content");

        var configEntry = archive.CreateEntry("config.json");
        using (var writer = new StreamWriter(configEntry.Open()))
            writer.Write("""{"version":"2.0.0"}""");

        var wwwrootEntry = archive.CreateEntry("wwwroot/index.html");
        using (var writer = new StreamWriter(wwwrootEntry.Open()))
            writer.Write("<html>updated</html>");

        var newFileEntry = archive.CreateEntry("new_feature.dll");
        using (var writer = new StreamWriter(newFileEntry.Open()))
            writer.Write("new feature dll");
    }
}

/// <summary>
/// UpdateChecker 的 test double（手动 Mock）。
/// 继承自 <see cref="UpdateChecker"/>，重写 virtual 方法以控制测试行为。
/// </summary>
internal class TestUpdateChecker : UpdateChecker
{
    /// <summary>CheckForUpdateAsync 的返回值</summary>
    public UpdateCheckResult? CheckForUpdateResult { get; set; }

    /// <summary>CheckForUpdateAsync 被调用的次数</summary>
    public int CheckForUpdateCallCount { get; private set; }

    /// <summary>DownloadPackageAsync 的回调，用于创建 ZIP 包</summary>
    public Action<string, string>? DownloadPackageCallback { get; set; }

    /// <summary>DownloadPackageAsync 被调用的次数</summary>
    public int DownloadPackageCallCount { get; private set; }

    /// <summary>DownloadPackageAsync 抛出的异常（不为 null 时抛出）</summary>
    public Exception? DownloadPackageException { get; set; }

    /// <summary>VerifyPackage 的返回值</summary>
    public bool VerifyPackageResult { get; set; } = true;

    /// <summary>VerifyPackage 被调用的次数</summary>
    public int VerifyPackageCallCount { get; private set; }

    public TestUpdateChecker(UpdateConfig config, HttpClient httpClient, string appName, string currentVersion)
        : base(config, httpClient, appName, currentVersion) { }

    public override async Task<UpdateCheckResult> CheckForUpdateAsync()
    {
        CheckForUpdateCallCount++;
        await Task.CompletedTask;
        return CheckForUpdateResult ?? new UpdateCheckResult { IsSuccess = false, ErrorMessage = "未设置 Mock 返回值" };
    }

    public override async Task DownloadPackageAsync(string version, string destPath)
    {
        DownloadPackageCallCount++;
        if (DownloadPackageException != null)
            throw DownloadPackageException;

        if (DownloadPackageCallback != null)
        {
            DownloadPackageCallback(version, destPath);
            await Task.CompletedTask;
            return;
        }

        await Task.CompletedTask;
    }

    public override bool VerifyPackage(string packagePath, string expectedHash)
    {
        VerifyPackageCallCount++;
        return VerifyPackageResult;
    }
}