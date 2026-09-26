using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;
using NewLife.Log;
using ForgeSelf.Api.Models;

namespace ForgeSelf.Api.Services;

/// <summary>
/// 分阶段更新状态快照（供 API/前端轮询）。
/// Status 取值：idle | checking | checked | downloading | verifying | extracting | ready | applying | failed。
/// </summary>
public class UpdateStageInfo
{
    public string Status { get; set; } = "idle";
    public int Progress { get; set; }
    public string? Message { get; set; }
    public string? Tag { get; set; }
    public string? StagedDir { get; set; }
    public UpdateCheckResult? Check { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 暂存式更新编排服务（spec 036）。
/// 流程：检查 → 下载+校验+解压到 %LOCALAPPDATA%\ForgeSelf\Updates\&lt;tag&gt;\（staged）→
/// 用户点「重启并更新」→ 拉起 update-agent.ps1（等待本进程退出后备份/覆盖/重启）→ 宿主自停。
/// 与 008 的 <see cref="UpdateService"/>（Windows 服务模式）并存，不改变其行为。
/// </summary>
public class StagedUpdateService
{
    private readonly UpdateChecker _checker;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly object _gate = new();
    private UpdateStageInfo _state = new();

    public StagedUpdateService(UpdateChecker checker, IHostApplicationLifetime lifetime)
    {
        _checker = checker ?? throw new ArgumentNullException(nameof(checker));
        _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
    }

    /// <summary>更新暂存根目录 %LOCALAPPDATA%\ForgeSelf\Updates。</summary>
    public static string UpdatesRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ForgeSelf", "Updates");

    /// <summary>当前状态快照（引用不可变：状态变更时整体替换）。</summary>
    public UpdateStageInfo GetState()
    {
        lock (_gate) return _state;
    }

    private void Set(string status, int progress, string? message,
        UpdateCheckResult? check = null, string? tag = null, string? stagedDir = null)
    {
        lock (_gate)
        {
            _state = new UpdateStageInfo
            {
                Status = status,
                Progress = progress,
                Message = message,
                Check = check ?? _state.Check,
                Tag = tag ?? _state.Tag,
                StagedDir = stagedDir ?? _state.StagedDir,
            };
        }
    }

    // ======================================================================
    // 检查
    // ======================================================================

    /// <summary>检查更新（同步等待结果），并更新状态机。</summary>
    public async Task<UpdateCheckResult> CheckAsync()
    {
        // 首写也要守卫：下载/暂存进行中时不得把 downloading 覆盖成 checking
        // （checking 不在前端终止态里，但结论回写前的中间态漂移会让轮询协议失真）。
        lock (_gate)
        {
            if (_state.Status is not ("downloading" or "verifying" or "extracting" or "ready" or "applying"))
                Set("checking", 0, "检查更新中...");
        }
        UpdateCheckResult result;
        try
        {
            result = await _checker.CheckForUpdateAsync();
        }
        catch (Exception ex)
        {
            Set("failed", 0, $"检查失败: {ex.Message}");
            throw;
        }

        // 下载/暂存进行中或已就绪时，检查结论只返回不改状态机——否则会把 downloading
        // 覆盖成 checked，前端把 checked 当终止态永久停轮询，「重启并更新」按钮消失
        // （spec 036 实机根因：UI 重复触发 check 时掐掉了进行中的下载状态）。
        // 读状态与写入必须同锁（lock 可重入，Set 内部同锁）。
        lock (_gate)
        {
            var inFlightOrReady = _state.Status is "downloading" or "verifying" or "extracting" or "ready" or "applying";
            if (!inFlightOrReady)
            {
                if (!result.IsSuccess)
                {
                    Set("failed", 0, result.ErrorMessage ?? "检查失败", result);
                }
                else if (result.HasUpdate)
                {
                    Set("checked", 100, $"发现新版本 {result.LatestVersionTag ?? result.LatestVersion?.ToString()}", result,
                        tag: result.LatestVersionTag);
                }
                else
                {
                    Set("idle", 100, "已是最新版本", result, tag: null, stagedDir: null);
                }
            }
        }

        return result;
    }

    // ======================================================================
    // 下载 + 校验 + 解压（staged）
    // ======================================================================

    /// <summary>启动后台下载任务；已有下载/解压任务在跑时返回 false（幂等防重复）。</summary>
    public bool StartDownload()
    {
        lock (_gate)
        {
            if (_state.Status is "downloading" or "verifying" or "extracting")
                return false;
            if (_state.Status == "ready")
                return false; // 已就绪，无需重复下载
            if (_state.Check == null || !_state.Check.HasUpdate)
                return false;

            // 必须在返回前同步置 downloading：若等后台任务再置，POST 响应/首轮轮询
            // 可能读到 checked，被前端当作终止态停止轮询（spec 036 实机竞态）。
            Set("downloading", 0, "下载更新包...", _state.Check, _state.Tag, _state.StagedDir);
            _ = Task.Run(DownloadAndStageAsync);
            return true;
        }
    }

    /// <summary>下载更新包 → SHA256 校验 → 解压到 staged 目录，状态推进到 ready。</summary>
    public async Task DownloadAndStageAsync()
    {
        UpdateCheckResult? check;
        lock (_gate) check = _state.Check;

        if (check == null || !check.HasUpdate || check.LatestVersion == null)
        {
            Set("failed", 0, "没有可下载的更新，请先检查更新");
            return;
        }

        var tag = check.LatestVersionTag ?? check.LatestVersion.ToString();
        var downloadVersion = check.LatestVersionTag ?? tag.TrimStart('v', 'V');

        try
        {
            var stageDir = Path.Combine(UpdatesRoot, Sanitize(tag));
            Directory.CreateDirectory(stageDir);
            var zipPath = Path.Combine(stageDir, "update.zip");

            Set("downloading", 10, "下载更新包...", stagedDir: stageDir, tag: tag);
            await _checker.DownloadPackageAsync(downloadVersion, zipPath);

            Set("verifying", 60, "校验更新包...", stagedDir: stageDir);
            if (!string.IsNullOrEmpty(check.PackageHash))
            {
                if (!_checker.VerifyPackage(zipPath, check.PackageHash))
                    throw new InvalidDataException("更新包 SHA256 校验失败，文件可能已损坏或被篡改。");
            }
            else
            {
                XTrace.Log.Warn("StagedUpdate: 更新包无哈希信息，跳过校验。");
            }

            Set("extracting", 75, "解压更新包...", stagedDir: stageDir);
            var extractDir = Path.Combine(stageDir, "extracted");
            if (Directory.Exists(extractDir))
                Directory.Delete(extractDir, recursive: true);
            ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

            // 简单完整性检查：staged 里必须有当前 exe 同名文件
            var exeName = GetHostExeName();
            if (!File.Exists(Path.Combine(extractDir, exeName)))
                throw new InvalidDataException($"更新包内容异常：缺少 {exeName}。");

            Set("ready", 100, "更新已就绪，点击「重启并更新」完成升级",
                stagedDir: stageDir, tag: tag);
            XTrace.Log.Info("StagedUpdate: 更新已暂存 {0} → {1}", tag, extractDir);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("StagedUpdate: 下载/暂存失败: {0}", ex.Message);
            Set("failed", 0, $"下载更新失败: {ex.Message}", tag: tag);
        }
    }

    // ======================================================================
    // 应用（重启并更新）
    // ======================================================================

    /// <summary>
    /// 应用已暂存的更新：拉起 update-agent.ps1（等本进程退出 → 备份 → 覆盖 → 重启），
    /// 然后宿主自行停止。仅在 ready 状态下允许。
    /// </summary>
    /// <returns>代理已成功拉起返回 true；否则 false（状态置 failed）。</returns>
    public bool ApplyStaged()
    {
        UpdateStageInfo current;
        lock (_gate) current = _state;

        if (current.Status != "ready" || current.StagedDir == null)
        {
            Set("failed", 0, "更新尚未就绪（请先下载完成）");
            return false;
        }

        var installDir = Path.GetDirectoryName(Environment.ProcessPath)
            ?? throw new InvalidOperationException("无法确定应用程序目录。");
        var extractDir = Path.Combine(current.StagedDir, "extracted");

        // 代理脚本优先用 staged 新版（随包更新），回退到安装目录
        var agentPath = new[]
            {
                Path.Combine(extractDir, "update-agent.ps1"),
                Path.Combine(installDir, "update-agent.ps1"),
            }
            .FirstOrDefault(File.Exists);

        if (agentPath == null)
        {
            Set("failed", 0, "找不到 update-agent.ps1，无法自动更新");
            return false;
        }

        try
        {
            var exeName = GetHostExeName();
            var hostArgs = Environment.GetCommandLineArgs().Skip(1);
            var argList = string.Join(";", hostArgs);

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-ExecutionPolicy");
            psi.ArgumentList.Add("Bypass");
            psi.ArgumentList.Add("-File");
            psi.ArgumentList.Add(agentPath);
            psi.ArgumentList.Add("-HostPid");
            psi.ArgumentList.Add(Process.GetCurrentProcess().Id.ToString());
            psi.ArgumentList.Add("-InstallDir");
            psi.ArgumentList.Add(installDir);
            psi.ArgumentList.Add("-StagedDir");
            psi.ArgumentList.Add(extractDir);
            psi.ArgumentList.Add("-ExeName");
            psi.ArgumentList.Add(exeName);
            psi.ArgumentList.Add("-ArgList");
            psi.ArgumentList.Add(argList);

            var proc = Process.Start(psi);
            XTrace.Log.Info("StagedUpdate: 更新代理已启动 PID={0}，宿主即将退出完成换文件",
                proc?.Id ?? -1);

            Set("applying", 100, "正在重启并更新，请稍候...");

            // 留出响应回写窗口后自停（agent 在等待本 PID 退出）
            _ = Task.Run(async () =>
            {
                await Task.Delay(1500);
                _lifetime.StopApplication();
            });
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("StagedUpdate: 启动更新代理失败: {0}", ex.Message);
            Set("failed", 0, $"启动更新代理失败: {ex.Message}");
            return false;
        }
    }

    // ======================================================================
    // 辅助
    // ======================================================================

    private static string GetHostExeName()
    {
        var name = Path.GetFileName(Environment.ProcessPath);
        return string.IsNullOrEmpty(name) ? "ForgeSelf.exe" : name;
    }

    private static string Sanitize(string value)
    {
        return Regex.Replace(value, "[^A-Za-z0-9._-]", "_");
    }
}
