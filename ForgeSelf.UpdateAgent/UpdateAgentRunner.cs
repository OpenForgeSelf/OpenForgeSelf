using System.Diagnostics;
using System.Text;

namespace ForgeSelf.UpdateAgent;

/// <summary>
/// 更新应用主流程（2026-11-21 输入15；与 scripts/update-agent.ps1 步骤一一对应）：
///   0 解析/校验参数并写启动日志
///   1 等业务层宿主进程（--host-pid）退出
///   2 定位 staged 布局（解压根公共层 + versions/&lt;newVer&gt;/）并校验（拒绝安装根在 versions 内逐代嵌套）
///   3 业务层落 versions/&lt;newVer&gt;/（全新目录，无文件锁；同版本重装走 robocopy 覆盖）
///   4 公共层合并 robocopy（/XD versions：根启动器/运行时/框架/plugins/update-agent 自身 覆盖安装根；
///     versions/ 整树排除，旧版本目录不动）
///   5 写 versions/current（tmp + move 原子切换）
///   6 扁平残留清理（保留名单制；失败降级日志告警不中断）
///   6.5 目录名规范化（Plugins→plugins 等；失败降级不中断）
///   7 旧版本清理（按 UpdateAgentCore 补零定宽序保留 current+上一版；失败降级不中断）
///   8 清理 staged tag 与退役 Backups 存量（失败降级不中断）
///   9 重启根启动器（公共层；WindowStyle=Hidden，DOTNET_ROOT 指回安装根公共运行时）
/// 版本比较一律 UpdateAgentCore.ToSortKey 补零定宽 + ordinal（PC-041：10 位时间码第 4 段
/// 超 Int32.MaxValue，禁 System.Version/[version] 分量解析）。
/// </summary>
public static class UpdateAgentRunner
{
    private static readonly string[] RobocopyExclusions = { "/R:2", "/W:2", "/NFL", "/NDL", "/NP" };

    public static async Task<int> RunAsync(UpdateAgentOptions o)
    {
        AgentLog.Write($"更新代理启动: HostPid={o.HostPid} InstallDir={o.InstallDir} StagedDir={o.StagedDir} ExeName={o.ExeName}");

        // ── 1. 等宿主业务层进程退出（上限 5 分钟，防僵尸挂死更新链） ──
        await WaitForHostExitAsync(o.HostPid);
        AgentLog.Write("宿主业务层进程已退出");

        // ── 2. 定位 staged 布局并校验 ──
        var stagedVersionsDir = Path.Combine(o.StagedDir, "versions");
        if (!Directory.Exists(stagedVersionsDir))
            throw new InvalidOperationException($"staged 解压缺 versions/ 目录: {stagedVersionsDir}");
        var stagedVersionDirs = Directory.GetDirectories(stagedVersionsDir);
        if (stagedVersionDirs.Length == 0)
            throw new InvalidOperationException($"staged versions/ 为空: {stagedVersionsDir}");
        // 多目录时取补零定宽序最新（PC-041 安全比较）
        var newVerName = UpdateAgentCore.SortNewestFirst(stagedVersionDirs.Select(Path.GetFileName)!).First();
        var newVer = Path.GetFileName(newVerName);
        var stagedVersionDir = Path.Combine(stagedVersionsDir, newVer);
        AgentLog.Write($"新版本目录 = {newVer}（staged 业务层 {stagedVersionDir}）");

        // 拒绝安装根在版本目录内（防逐代嵌套：宿主把 versions/<ver>/ 业务层当安装根传进来时，
        // 其父目录名即 versions——新版本会落 versions/<ver>/versions/<new>/，2026-10-04 输入19 实证）
        var resolvedInstall = Path.TrimEndingDirectorySeparator(Path.GetFullPath(o.InstallDir));
        var parentName = Path.GetFileName(Path.GetDirectoryName(resolvedInstall));
        if (string.Equals(parentName, "versions", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"安装根位于版本目录内（父目录 = versions）：{o.InstallDir}，拒绝执行以防逐代嵌套；应以上层公共层为安装根重跑。");
        var versionsRoot = Path.Combine(resolvedInstall, "versions");
        Directory.CreateDirectory(versionsRoot);

        var installVersionDir = Path.Combine(versionsRoot, newVer);

        // ── 3. 业务层落 versions/<newVer>/（全新目录无锁；同版本重装 robocopy 覆盖） ──
        Robocopy(stagedVersionDir, installVersionDir, extra: Array.Empty<string>());
        AgentLog.Write($"业务层已落位: {installVersionDir}");

        // ── 4. 公共层合并（覆盖安装根版本目录之外的公共件；排除 versions/ 免整树覆盖旧版本） ──
        Robocopy(o.StagedDir, o.InstallDir, extra: new[] { "/XD", "versions" });
        AgentLog.Write("公共层合并完成（根启动器/运行时/框架/plugins/update-agent 自身）");

        // ── 5. 写 versions/current（tmp + move 原子切换） ──
        var currentFile = Path.Combine(versionsRoot, "current");
        var tmpFile = currentFile + ".tmp";
        File.WriteAllText(tmpFile, newVer, new UTF8Encoding(false));
        File.Move(tmpFile, currentFile, overwrite: true);
        AgentLog.Write($"current 指针已切换: {newVer}");

        // ── 6. 扁平存量迁移清理（保留名单制；失败降级不中断） ──
        CleanupFlatLeftovers(o.InstallDir, installVersionDir);

        // ── 6.5 目录名规范化（大小写统一小写；失败降级不中断） ──
        NormalizeDirectoryCasing(o.InstallDir);

        // ── 7. 旧版本清理（保留 current + 补零定宽序上一版；失败降级不中断） ──
        CleanupOldVersions(versionsRoot, newVer);

        // ── 8. 清理 staged tag 与退役 Backups 存量（失败降级不中断） ──
        CleanupStagedAndBackups(o.InstallDir, o.StagedDir);

        // ── 9. 重启根启动器（公共层） ──
        RestartLauncher(o);

        AgentLog.Write("更新流程完成（exit 0）");
        return 0;
    }

    // ────────────────────────── 步骤实现 ──────────────────────────

    private static async Task WaitForHostExitAsync(int hostPid)
    {
        try
        {
            var host = Process.GetProcessById(hostPid);
            AgentLog.Write($"等待宿主进程退出 PID={hostPid} ...");
            await host.WaitForExitAsync(new CancellationTokenSource(TimeSpan.FromMinutes(5)).Token)
                .ConfigureAwait(false);
            if (!host.HasExited)
                AgentLog.Write("警告: 等待宿主退出达 5 分钟上限，按已退出继续（进程句柄释放交由 OS）");
        }
        catch (ArgumentException)
        {
            AgentLog.Write($"宿主进程 PID={hostPid} 已不存在（视为已退出）");
        }
        catch (Exception ex)
        {
            AgentLog.Write($"警告: 等待宿主退出异常（{ex.Message}），按已退出继续");
        }
    }

    /// <summary>robocopy 退出码 0-7 均为成功（位标志），≥8 视为失败抛出。</summary>
    private static void Robocopy(string source, string dest, string[] extra)
    {
        if (!Directory.Exists(source))
            throw new InvalidOperationException($"robocopy 源不存在: {source}");
        Directory.CreateDirectory(dest);
        var args = new List<string> { source, dest, "/E" };
        args.AddRange(extra);
        args.AddRange(RobocopyExclusions);
        AgentLog.Write($"robocopy {string.Join(' ', args)}");

        var psi = new ProcessStartInfo("robocopy")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("robocopy 进程创建失败");
        proc.WaitForExit();
        var code = proc.ExitCode;
        if (code > 7)
            throw new InvalidOperationException($"robocopy 失败，退出码 {code}（{source} → {dest}，详见日志）");
        AgentLog.Write($"robocopy 完成，退出码 {code}");
    }

    /// <summary>扁平存量迁移清理：QQNT 版本化后安装根不再承载业务层程序集。
    /// 保留名单制（根启动器/代理/公共运行时/插件/数据目录/配置一律不碰），
    /// 只删「已被版本层接管」的扁平程序集件；失败降级日志告警不中断。</summary>
    private static void CleanupFlatLeftovers(string installDir, string installVersionDir)
    {
        try
        {
            var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "forgeSelf.exe", "update-agent.exe", "update-agent.ps1",
                "versions", "plugins", "data", "log", "logs", "config", "backups",
                "host", "shared", "appsettings.json", "appsettings.production.json",
                "forgeself.updateagent", "agent-run",
            };
            var removed = 0;
            foreach (var file in Directory.EnumerateFiles(installDir))
            {
                var name = Path.GetFileName(file);
                if (keep.Contains(name)) continue;
                var ext = Path.GetExtension(name).ToLowerInvariant();
                var isVersionLayerOwned =
                    ext is ".dll" or ".deps.json" or ".runtimeconfig.json"
                    || name.StartsWith("ForgeSelf.", StringComparison.OrdinalIgnoreCase)
                       && ext is ".dll" or ".pdb";
                if (!isVersionLayerOwned) continue;
                // 仅当版本层已有同名接管件时才清扁平残留（保守：找不到接管件就不删）
                var takeover = Path.Combine(installVersionDir, name);
                var takeoverAny = File.Exists(takeover)
                    || Directory.Exists(Path.Combine(installVersionDir, Path.GetFileNameWithoutExtension(name)));
                if (!takeoverAny) continue;
                TryDelete(file);
                removed++;
            }
            if (removed > 0) AgentLog.Write($"扁平残留清理: 删除 {removed} 个已被版本层接管的文件");
        }
        catch (Exception ex)
        {
            AgentLog.Write($"警告: 扁平残留清理失败（降级继续）: {ex.Message}");
        }
    }

    private static void NormalizeDirectoryCasing(string installDir)
    {
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(installDir))
            {
                var name = Path.GetFileName(dir);
                var lower = name.ToLowerInvariant();
                if (name != lower && !Directory.Exists(Path.Combine(installDir, lower)))
                    TryRename(dir, Path.Combine(installDir, lower));
            }
        }
        catch (Exception ex)
        {
            AgentLog.Write($"警告: 目录名规范化失败（降级继续）: {ex.Message}");
        }
    }

    /// <summary>版本保留策略：current + 补零定宽序上一版，其余删除（PC-041 安全比较）。</summary>
    private static void CleanupOldVersions(string versionsRoot, string newVer)
    {
        try
        {
            var dirs = Directory.GetDirectories(versionsRoot)
                .Select(Path.GetFileName)!
                .Where(n => !string.Equals(n, "current", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (dirs.Count <= 2) return;

            var ordered = UpdateAgentCore.SortNewestFirst(dirs).ToList();
            var keepSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { newVer };
            if (ordered.Count > 1) keepSet.Add(ordered[1]); // 上一版（回滚能力）
            var removed = 0;
            foreach (var dir in Directory.GetDirectories(versionsRoot))
            {
                var name = Path.GetFileName(dir);
                if (keepSet.Contains(name)) continue;
                TryDelete(dir);
                removed++;
            }
            AgentLog.Write($"旧版本清理: 删除 {removed} 个（保留 {newVer} + 上一版 {ordered[1]}）");
        }
        catch (Exception ex)
        {
            AgentLog.Write($"警告: 旧版本清理失败（降级继续）: {ex.Message}");
        }
    }

    private static void CleanupStagedAndBackups(string installDir, string stagedDir)
    {
        try
        {
            // staged tag 目录 = staged-dir（Updates/<tag>/extracted）的父目录 Updates/<tag>/，
            // 删 tag 目录连带 zip；仅当祖父目录名为 Updates 时执行（真实暂存布局校验）
            var tagDir = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(stagedDir)));
            var updatesRoot = tagDir == null ? null : Path.GetDirectoryName(tagDir);
            if (tagDir != null && updatesRoot != null &&
                string.Equals(Path.GetFileName(updatesRoot), "Updates", StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(tagDir);
            }
            // 退役 Backups 存量（QQNT 版本化后无整目录备份；空/陈旧 Backups 清掉）
            var backups = Path.Combine(installDir, "Backups");
            if (Directory.Exists(backups)) TryDelete(backups);
        }
        catch (Exception ex)
        {
            AgentLog.Write($"警告: staged/Backups 清理失败（降级继续）: {ex.Message}");
        }
    }

    private static void RestartLauncher(UpdateAgentOptions o)
    {
        var launcher = Path.Combine(o.InstallDir, o.ExeName);
        if (!File.Exists(launcher))
            throw new InvalidOperationException($"根启动器不存在: {launcher}（公共层合并后应有）");

        var psi = new ProcessStartInfo
        {
            FileName = launcher,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden, // 有意偏差②：静默重启不出黑窗
            WorkingDirectory = o.InstallDir,
        };
        // 宿主原命令行参数原样回传（ArgList 以分号拼接）
        if (!string.IsNullOrEmpty(o.ArgList))
        {
            foreach (var a in o.ArgList.Split(';', StringSplitOptions.RemoveEmptyEntries))
                psi.ArgumentList.Add(a);
        }
        // 根启动器（公共层 FDD）运行时指向安装根自带副本——agent 自己的 DOTNET_ROOT
        // 指向 staged 解压根（马上要被清理），必须改指安装根再拉起
        psi.EnvironmentVariables["DOTNET_ROOT"] = o.InstallDir;

        var proc = Process.Start(psi) ?? throw new InvalidOperationException("根启动器进程创建失败");
        AgentLog.Write($"根启动器已重启 PID={proc.Id}: {launcher}");
    }

    // ────────────────────────── 工具 ──────────────────────────

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            else if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            AgentLog.Write($"警告: 删除失败（跳过）: {path} — {ex.Message}");
        }
    }

    private static void TryRename(string from, string to)
    {
        try
        {
            Directory.Move(from, to);
            AgentLog.Write($"目录名规范化: {Path.GetFileName(from)} → {Path.GetFileName(to)}");
        }
        catch (Exception ex)
        {
            AgentLog.Write($"警告: 目录改名失败（跳过）: {from} — {ex.Message}");
        }
    }
}
