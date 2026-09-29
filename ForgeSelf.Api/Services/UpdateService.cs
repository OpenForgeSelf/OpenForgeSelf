using System.Diagnostics;
using System.IO.Compression;
using System.ServiceProcess;
using System.Text;
using NewLife.Log;
using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Models;

namespace ForgeSelf.Api.Services;

/// <summary>
/// 更新流程编排服务（008 旧链路，2026-09-28 输入34 冻结）。
/// 【冻结声明】本类不再维护：旧 Windows 服务更新链路（停止→备份→替换→启动服务）已由
/// spec 036 <see cref="StagedUpdateService"/>（暂存式 + update-agent 版本化应用）全面接管。
/// 唯一存量残留 = 托盘「检查更新/启动时检查」提示（Program.cs 已改走 036 CheckAsync）。
/// 全流程方法（CheckForUpdatesAsync 之外的下载/备份/服务启停/回滚）为历史保留，禁止新代码调用；
/// DI 注册保留仅防 WindowsService 模式引用。规则真源：docs/04-standards/packaging-upgrade-backup.md。
/// </summary>
public class UpdateService
{
    private readonly UpdateChecker _updateChecker;
    private readonly IServiceManager _serviceManager;
    private readonly UpdateConfig _updateConfig;
    private readonly ServiceConfig _serviceConfig;
    private readonly string _appName;
    private readonly string _appVersion;
    private readonly string _appDir;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// 初始化更新流程编排服务。
    /// </summary>
    /// <param name="updateChecker">更新检查器，负责版本检查和包下载</param>
    /// <param name="serviceManager">服务管理器，负责服务启停</param>
    /// <param name="updateConfig">更新配置</param>
    /// <param name="serviceConfig">服务配置</param>
    /// <param name="httpClient">HTTP 客户端，用于启动后的健康检查</param>
    /// <param name="appName">应用名称，默认为 "ForgeSelf"</param>
    /// <param name="appVersion">当前版本号，默认从 <see cref="UpdateChecker.GetCurrentVersion"/> 获取</param>
    /// <param name="appDir">应用安装目录，默认取当前进程所在目录</param>
    /// <exception cref="ArgumentNullException">必要依赖为 null</exception>
    /// <exception cref="InvalidOperationException">无法确定应用程序目录</exception>
    public UpdateService(
        UpdateChecker updateChecker,
        IServiceManager serviceManager,
        UpdateConfig updateConfig,
        ServiceConfig serviceConfig,
        HttpClient httpClient,
        string? appName = null,
        string? appVersion = null,
        string? appDir = null)
    {
        _updateChecker = updateChecker ?? throw new ArgumentNullException(nameof(updateChecker));
        _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
        _updateConfig = updateConfig ?? throw new ArgumentNullException(nameof(updateConfig));
        _serviceConfig = serviceConfig ?? throw new ArgumentNullException(nameof(serviceConfig));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _appName = appName ?? "ForgeSelf";
        _appVersion = appVersion ?? UpdateChecker.GetCurrentVersion();
        _appDir = appDir ?? Path.GetDirectoryName(Environment.ProcessPath)
            ?? throw new InvalidOperationException("无法确定应用程序目录。");
    }

    /// <summary>
    /// 检查版本更新。
    /// 委托给 <see cref="UpdateChecker.CheckForUpdateAsync"/>，返回检查结果供调用方决定是否显示通知。
    /// </summary>
    /// <returns>版本检查结果</returns>
    public async Task<UpdateCheckResult> CheckForUpdatesAsync()
    {
        return await _updateChecker.CheckForUpdateAsync();
    }

    /// <summary>
    /// 执行完整更新流程：
    /// 下载 → 校验 → 解压 → 停止服务 → 备份 → 替换文件 → 启动服务 → 健康检查 → 记录 UpdateTrace。
    /// 任一关键步骤失败时自动回滚。
    /// </summary>
    /// <param name="checkResult">版本检查结果，包含下载地址、版本号、哈希等信息</param>
    /// <param name="progress">进度报告，0–100</param>
    /// <returns>更新成功返回 true，失败或回滚返回 false</returns>
    /// <exception cref="ArgumentNullException">checkResult 为 null</exception>
    /// <exception cref="InvalidOperationException">当前没有任何更新可用，或版本号无效</exception>
    public async Task<bool> ApplyUpdateAsync(UpdateCheckResult checkResult, IProgress<int>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(checkResult);

        if (!checkResult.HasUpdate)
        {
            XTrace.Log.Info("UpdateService: 没有可用更新，跳过。");
            return false;
        }

        if (checkResult.LatestVersion == null)
        {
            throw new InvalidOperationException("检查结果中缺少最新版本号，无法执行更新。");
        }

        var stopwatch = Stopwatch.StartNew();
        var preVersion = _appVersion;
        string? postVersion = checkResult.LatestVersion.ToString();
        string? errorMessage = null;
        string? rollbackVersion = null;
        int resultCode = 1; // 默认失败
        string? packagePath = null;
        string? extractDir = null;
        string? backupDir = null;
        var tempRoot = Path.Combine(Path.GetTempPath(), $"{_appName}_Update_{Guid.NewGuid():N}");

        try
        {
            // ── 1. 下载更新包 ──
            XTrace.Log.Info("UpdateService: 开始更新 {0} → {1}", preVersion, postVersion);
            progress?.Report(10);

            packagePath = Path.Combine(tempRoot, "update.zip");
            Directory.CreateDirectory(Path.GetDirectoryName(packagePath)!);

            await _updateChecker.DownloadPackageAsync(postVersion, packagePath);

            // ── 2. 校验更新包 SHA256 ──
            progress?.Report(20);
            if (!string.IsNullOrEmpty(checkResult.PackageHash))
            {
                XTrace.Log.Info("UpdateService: 校验更新包哈希...");
                if (!_updateChecker.VerifyPackage(packagePath, checkResult.PackageHash))
                {
                    throw new InvalidDataException("更新包 SHA256 校验失败，文件可能已损坏或被篡改。");
                }
                XTrace.Log.Info("UpdateService: 哈希校验通过");
            }

            // ── 3. 解压更新包到临时目录 ──
            progress?.Report(30);
            extractDir = Path.Combine(tempRoot, "extracted");
            Directory.CreateDirectory(extractDir);
            XTrace.Log.Info("UpdateService: 解压更新包到 {0}", extractDir);
            ZipFile.ExtractToDirectory(packagePath, extractDir, overwriteFiles: true);
            XTrace.Log.Info("UpdateService: 解压完成");

            // ── 4. 停止当前服务 ──
            progress?.Report(45);
            if (_serviceManager.IsInstalled())
            {
                var status = _serviceManager.GetStatus();
                if (status == ServiceControllerStatus.Running ||
                    status == ServiceControllerStatus.Paused ||
                    status == ServiceControllerStatus.StartPending)
                {
                    XTrace.Log.Info("UpdateService: 停止服务 '{0}'...", _serviceConfig.ServiceName);
                    _serviceManager.Stop();
                    XTrace.Log.Info("UpdateService: 服务已停止");
                }
                else
                {
                    XTrace.Log.Info("UpdateService: 服务当前状态为 {0}，无需停止", status);
                }
            }
            else
            {
                XTrace.Log.Warn("UpdateService: 服务未安装，跳过停止步骤（仅替换文件）");
            }

            // ── 5. 备份旧版本文件 ──
            progress?.Report(60);
            backupDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                _appName,
                "Backups",
                DateTime.Now.ToString("yyyyMMddHHmmss"));
            Directory.CreateDirectory(backupDir);
            XTrace.Log.Info("UpdateService: 备份当前版本到 {0}", backupDir);
            BackupDirectory(_appDir, backupDir);
            XTrace.Log.Info("UpdateService: 备份完成 ({0} 个文件)", Directory.GetFiles(backupDir, "*", SearchOption.AllDirectories).Length);

            // ── 6. 替换文件（原子操作：先复制新文件到安装目录，失败时回滚） ──
            progress?.Report(75);
            try
            {
                XTrace.Log.Info("UpdateService: 替换文件到 {0}", _appDir);
                ReplaceDirectory(extractDir, _appDir);
                XTrace.Log.Info("UpdateService: 文件替换完成");
            }
            catch (Exception ex)
            {
                // 文件替换失败 → 从备份恢复
                errorMessage = $"文件替换失败: {ex.Message}";
                XTrace.Log.Error("UpdateService: {0}", errorMessage);

                progress?.Report(80);
                XTrace.Log.Info("UpdateService: 正在从备份恢复...");
                RestoreDirectory(backupDir, _appDir);
                XTrace.Log.Info("UpdateService: 备份恢复完成");

                resultCode = 2; // 回滚
                rollbackVersion = preVersion;
                RecordUpdateTrace(preVersion, postVersion, checkResult, resultCode, errorMessage, rollbackVersion, stopwatch.ElapsedMilliseconds);
                return false;
            }

            // ── 7. 启动新版本服务 ──
            progress?.Report(85);
            if (_serviceManager.IsInstalled())
            {
                try
                {
                    XTrace.Log.Info("UpdateService: 启动服务 '{0}'...", _serviceConfig.ServiceName);
                    _serviceManager.Start();
                    XTrace.Log.Info("UpdateService: 服务启动成功");
                }
                catch (Exception ex)
                {
                    // 新版本启动失败（超时 30 秒）→ 恢复旧版本并重启
                    errorMessage = $"新版本启动失败: {ex.Message}";
                    XTrace.Log.Error("UpdateService: {0}", errorMessage);

                    progress?.Report(90);
                    XTrace.Log.Info("UpdateService: 正在回滚到旧版本...");
                    RestoreDirectory(backupDir, _appDir);

                    // 重启旧版本服务
                    XTrace.Log.Info("UpdateService: 重启旧版本服务...");
                    try
                    {
                        _serviceManager.Start();
                        XTrace.Log.Info("UpdateService: 旧版本服务已启动");
                    }
                    catch (Exception restartEx)
                    {
                        XTrace.Log.Error("UpdateService: 旧版本重启也失败: {0}", restartEx.Message);
                    }

                    resultCode = 2; // 回滚
                    rollbackVersion = preVersion;
                    RecordUpdateTrace(preVersion, postVersion, checkResult, resultCode, errorMessage, rollbackVersion, stopwatch.ElapsedMilliseconds);
                    return false;
                }

                // ── 8. 健康检查 ──
                progress?.Report(92);
                try
                {
                    XTrace.Log.Info("UpdateService: 执行健康检查...");
                    var healthCheckOk = await HealthCheckAsync(TimeSpan.FromSeconds(30));
                    if (!healthCheckOk)
                    {
                        errorMessage = "健康检查失败：新版本服务未能在30秒内正常响应";
                        XTrace.Log.Error("UpdateService: {0}", errorMessage);

                        progress?.Report(95);
                        XTrace.Log.Info("UpdateService: 正在回滚到旧版本...");
                        RestoreDirectory(backupDir, _appDir);

                        XTrace.Log.Info("UpdateService: 重启旧版本服务...");
                        try
                        {
                            _serviceManager.Start();
                        }
                        catch (Exception restartEx)
                        {
                            XTrace.Log.Error("UpdateService: 旧版本重启失败: {0}", restartEx.Message);
                        }

                        resultCode = 2; // 回滚
                        rollbackVersion = preVersion;
                        RecordUpdateTrace(preVersion, postVersion, checkResult, resultCode, errorMessage, rollbackVersion, stopwatch.ElapsedMilliseconds);
                        return false;
                    }
                    XTrace.Log.Info("UpdateService: 健康检查通过");
                }
                catch (Exception ex)
                {
                    errorMessage = $"健康检查异常: {ex.Message}";
                    XTrace.Log.Error("UpdateService: {0}", errorMessage);

                    XTrace.Log.Info("UpdateService: 正在回滚到旧版本...");
                    RestoreDirectory(backupDir, _appDir);

                    try { _serviceManager.Start(); } catch { /* 忽略 */ }

                    resultCode = 2;
                    rollbackVersion = preVersion;
                    RecordUpdateTrace(preVersion, postVersion, checkResult, resultCode, errorMessage, rollbackVersion, stopwatch.ElapsedMilliseconds);
                    return false;
                }
            }
            else
            {
                XTrace.Log.Warn("UpdateService: 服务未安装，跳过启动步骤（文件已替换，需手动重启进程）");
            }

            // ── 9. 更新完成 ──
            stopwatch.Stop();
            resultCode = 0; // 成功
            progress?.Report(100);

            RecordUpdateTrace(preVersion, postVersion, checkResult, resultCode, null, null, stopwatch.ElapsedMilliseconds);

            XTrace.Log.Info("UpdateService: 更新完成 {0} → {1} (耗时 {2}ms)",
                preVersion, postVersion, stopwatch.ElapsedMilliseconds);
            return true;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            if (errorMessage == null)
                errorMessage = ex.Message;

            // 如果回滚尚未发生且备份目录存在，尝试回滚
            if (resultCode != 2 && backupDir != null && Directory.Exists(backupDir))
            {
                try
                {
                    XTrace.Log.Info("UpdateService: 发生异常，尝试回滚...");
                    RestoreDirectory(backupDir, _appDir);
                    if (_serviceManager.IsInstalled())
                        _serviceManager.Start();
                    rollbackVersion = preVersion;
                    resultCode = 2;
                }
                catch (Exception rollbackEx)
                {
                    XTrace.Log.Error("UpdateService: 回滚也失败: {0}", rollbackEx.Message);
                }
            }

            RecordUpdateTrace(preVersion, postVersion, checkResult, resultCode, errorMessage, rollbackVersion, stopwatch.ElapsedMilliseconds);

            XTrace.Log.Error("UpdateService: 更新失败: {0}", errorMessage);
            return false;
        }
        finally
        {
            // 清理临时文件
            try
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
            catch (Exception ex)
            {
                XTrace.Log.Warn("UpdateService: 清理临时文件失败: {0}", ex.Message);
            }
        }
    }

    /// <summary>
    /// 获取备份目录路径。
    /// </summary>
    public static string GetBackupRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ForgeSelf",
            "Backups");
    }

    /// <summary>
    /// 获取所有备份列表，按时间降序排列。
    /// </summary>
    public static IReadOnlyList<string> GetBackups()
    {
        var backupRoot = GetBackupRoot();
        if (!Directory.Exists(backupRoot))
            return Array.Empty<string>();

        return Directory.GetDirectories(backupRoot)
            .OrderByDescending(d => d)
            .ToList()
            .AsReadOnly();
    }

    // ======================================================================
    // 文件操作（内部）
    // ======================================================================

    /// <summary>
    /// 递归备份源目录到目标目录。
    /// 保留目录结构，仅复制文件。
    /// </summary>
    private static void BackupDirectory(string sourceDir, string destDir)
    {
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, file);
            var destFile = Path.Combine(destDir, relativePath);
            var destFileDir = Path.GetDirectoryName(destFile);
            if (!string.IsNullOrEmpty(destFileDir) && !Directory.Exists(destFileDir))
                Directory.CreateDirectory(destFileDir);
            File.Copy(file, destFile, overwrite: true);
        }
    }

    /// <summary>
    /// 递归替换目标目录中的文件为源目录中的文件。
    /// 保留源目录的目录结构，覆盖目标目录中的同名文件。
    /// 步骤：
    ///   1. 遍历源目录所有文件
    ///   2. 对每个文件确保目标子目录存在
    ///   3. 使用 File.Copy(overwrite: true) 覆盖目标文件
    /// 失败时抛出异常，由调用方决定是否回滚。
    /// </summary>
    private static void ReplaceDirectory(string sourceDir, string targetDir)
    {
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, file);
            var targetFile = Path.Combine(targetDir, relativePath);
            var targetFileDir = Path.GetDirectoryName(targetFile);
            if (!string.IsNullOrEmpty(targetFileDir) && !Directory.Exists(targetFileDir))
                Directory.CreateDirectory(targetFileDir);

            // 使用 File.Copy(overwrite: true) 覆盖目标文件
            // 对于正在运行的可执行文件，服务已停止所以文件不被锁定
            File.Copy(file, targetFile, overwrite: true);
        }
    }

    /// <summary>
    /// 从备份目录恢复到目标目录。
    /// 先清空目标目录（保留目录本身），再复制备份文件。
    /// 对单个文件的删除/复制失败做容错处理（跳过被锁定的文件），确保尽可能多地恢复。
    /// </summary>
    private static void RestoreDirectory(string backupDir, string targetDir)
    {
        // 清空目标目录中的所有文件和子目录
        foreach (var file in Directory.GetFiles(targetDir, "*", SearchOption.AllDirectories))
        {
            try { File.Delete(file); } catch { /* 忽略个别文件删除失败（如被锁定） */ }
        }

        foreach (var dir in Directory.GetDirectories(targetDir, "*", SearchOption.AllDirectories))
        {
            try
            {
                Directory.Delete(dir, recursive: false);
            }
            catch { /* 忽略非空目录或权限问题 */ }
        }

        // 从备份复制文件回目标目录（逐文件容错，跳过被锁定的文件）
        foreach (var file in Directory.GetFiles(backupDir, "*", SearchOption.AllDirectories))
        {
            try
            {
                var relativePath = Path.GetRelativePath(backupDir, file);
                var destFile = Path.Combine(targetDir, relativePath);
                var destFileDir = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(destFileDir) && !Directory.Exists(destFileDir))
                    Directory.CreateDirectory(destFileDir);
                File.Copy(file, destFile, overwrite: true);
            }
            catch (Exception ex)
            {
                XTrace.Log.Warn("RestoreDirectory: 恢复文件失败（跳过）: {0}, {1}", file, ex.Message);
            }
        }
    }

    /// <summary>
    /// 判断目录是否为空（不含任何文件或子目录）。
    /// </summary>
    private static bool IsDirectoryEmpty(string path)
    {
        return !Directory.EnumerateFileSystemEntries(path).Any();
    }

    // ======================================================================
    // 健康检查
    // ======================================================================

    /// <summary>
    /// 对新版本服务进行健康检查。
    /// 尝试访问 HTTP 健康检查端点，超时时间内返回 200 即认为健康。
    /// </summary>
    /// <param name="timeout">健康检查超时时间，默认 30 秒</param>
    /// <returns>健康检查通过返回 true</returns>
    private async Task<bool> HealthCheckAsync(TimeSpan timeout)
    {
        // 从 appsettings 中的 URLs 配置获取端口，或默认 7102
        var port = 7102; // 默认端口
        var healthUrl = $"http://localhost:{port}/health";

        using var cts = new CancellationTokenSource(timeout);
        var retryInterval = TimeSpan.FromSeconds(2);
        var maxRetries = (int)(timeout.TotalSeconds / retryInterval.TotalSeconds);

        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                cts.Token.ThrowIfCancellationRequested();

                var response = await _httpClient.GetAsync(healthUrl, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    XTrace.Log.Info("UpdateService: 健康检查通过 (attempt {0})", i + 1);
                    return true;
                }

                XTrace.Log.Info("UpdateService: 健康检查暂未通过 (attempt {0}, status={1})",
                    i + 1, response.StatusCode);
            }
            catch (OperationCanceledException)
            {
                XTrace.Log.Error("UpdateService: 健康检查超时 ({0}s)", timeout.TotalSeconds);
                return false;
            }
            catch (HttpRequestException ex)
            {
                XTrace.Log.Info("UpdateService: 健康检查连接失败 (attempt {0}): {1}",
                    i + 1, ex.Message);
            }

            // 等待后重试
            if (i < maxRetries - 1)
                await Task.Delay(retryInterval, cts.Token);
        }

        return false;
    }

    // ======================================================================
    // UpdateTrace 记录
    // ======================================================================

    /// <summary>
    /// 记录更新操作到 SQLite（通过 XCode <see cref="UpdateTrace"/> 实体）。
    /// </summary>
    private static void RecordUpdateTrace(
        string preVersion,
        string? postVersion,
        UpdateCheckResult checkResult,
        int result,
        string? errorMessage,
        string? rollbackVersion,
        long durationMs)
    {
        try
        {
            var trace = new UpdateTrace
            {
                PreVersion = preVersion,
                PostVersion = postVersion,
                UpdateTime = DateTime.UtcNow,
                Result = result,
                ErrorMessage = errorMessage != null && errorMessage.Length > 500
                    ? errorMessage[..500]
                    : errorMessage,
                RollbackVersion = rollbackVersion,
                DownloadUrl = checkResult.DownloadUrl,
                PackageSize = checkResult.PackageSize,
                DurationMs = durationMs,
            };

            trace.Insert();

            XTrace.Log.Info("UpdateService: UpdateTrace 已记录 (Id={0}, Result={1})",
                trace.Id, result);
        }
        catch (Exception ex)
        {
            // 记录 UpdateTrace 失败不应影响更新流程本身
            XTrace.Log.Error("UpdateService: 记录 UpdateTrace 失败: {0}", ex.Message);
        }
    }
}