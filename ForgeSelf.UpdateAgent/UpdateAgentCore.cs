namespace ForgeSelf.UpdateAgent;

/// <summary>
/// 版本目录名排序核心（PC-041 canonical fix，2026-11-21 输入15）。
/// 发行串第 4 段是 10 位时间码（如 2611211942 > int.MaxValue），任何经
/// System.Version / [version] 的分量解析都会抛 OverflowException，把已落位
/// 成功的更新误报为失败（实机 agent-20261121203854.log 实录）。canonical fix =
/// 各段 PadLeft(12,'0') 补零定宽后做 ordinal 字符串比较：任意段长无溢出，
/// 数字段按数值序、段数不一致时短串在前。与 scripts/update-agent.ps1 的
/// 版本保留排序同源（该脚本以注释锚定本类型，勿两处漂移）。
/// </summary>
public static class UpdateAgentCore
{
    /// <summary>把版本串映射为补零定宽键：每段 PadLeft(12,'0')，段间以 '|' 分隔。</summary>
    public static string ToSortKey(string version)
    {
        var parts = (version ?? string.Empty).Split('.', StringSplitOptions.RemoveEmptyEntries);
        return string.Join("|", parts.Select(p => p.PadLeft(12, '0')));
    }

    /// <summary>补零定宽 + ordinal 比较：&lt;0 = a 更旧，0 = 相同，&gt;0 = a 更新。null/空串视为最旧。</summary>
    public static int CompareVersionNames(string? a, string? b)
    {
        return string.CompareOrdinal(ToSortKey(a ?? string.Empty), ToSortKey(b ?? string.Empty));
    }

    /// <summary>按补零定宽序从新到旧排序（PC-041：禁止 Sort-Object/OrderBy 带 [version] 解析）。</summary>
    public static IEnumerable<string> SortNewestFirst(IEnumerable<string> versions)
    {
        return versions.OrderByDescending(v => v, Comparer<string>.Create((x, y) => CompareVersionNames(x, y)));
    }
}

/// <summary>命令行参数契约（Program 解析结果）。</summary>
public sealed record UpdateAgentOptions(
    int HostPid,
    string InstallDir,
    string StagedDir,
    string ExeName,
    string ArgList)
{
    public static UpdateAgentOptions Parse(string[] args)
    {
        string? hostPid = null, installDir = null, stagedDir = null, exeName = null, argList = null;
        for (var i = 0; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "--host-pid": hostPid = args[++i]; break;
                case "--install-dir": installDir = args[++i]; break;
                case "--staged-dir": stagedDir = args[++i]; break;
                case "--exe-name": exeName = args[++i]; break;
                case "--arg-list": argList = args[++i]; break;
            }
        }

        if (!int.TryParse(hostPid, out var pid) || pid <= 0)
            throw new ArgumentException($"--host-pid 缺失或非法: {hostPid}");
        if (string.IsNullOrWhiteSpace(installDir) || !Directory.Exists(installDir))
            throw new ArgumentException($"--install-dir 缺失或不存在: {installDir}");
        if (string.IsNullOrWhiteSpace(stagedDir) || !Directory.Exists(stagedDir))
            throw new ArgumentException($"--staged-dir 缺失或不存在: {stagedDir}");
        exeName = string.IsNullOrWhiteSpace(exeName) ? "ForgeSelf.exe" : exeName;

        return new UpdateAgentOptions(pid, installDir, stagedDir, exeName, argList ?? string.Empty);
    }
}

/// <summary>
/// Agent 日志：写 %LOCALAPPDATA%\ForgeSelf\Logs\update-agent-<yyyyMMddHHmmss>.log。
/// 三重降级（2026-11-21 输入15 定稿）：首选专目录 → 回退 Updates 根 → 再回退纯控制台；
/// 任何写失败只降级不抛——日志上抛会把 Run 的 catch/finally 一路炸穿，连尽力重启都执行不到。
/// </summary>
public static class AgentLog
{
    private static readonly object Gate = new();
    private static string? _path;

    public static string? LogPath => _path;

    public static void Write(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        Console.WriteLine(line);
        lock (Gate)
        {
            try
            {
                if (_path == null)
                {
                    var dir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "ForgeSelf", "Logs");
                    _path = Path.Combine(dir, $"update-agent-{DateTime.Now:yyyyMMddHHmmss}.log");
                    Directory.CreateDirectory(dir);
                }
                File.AppendAllText(_path, line + Environment.NewLine, System.Text.Encoding.UTF8);
            }
            catch
            {
                // 降级 1：专目录不可写（权限/沙箱），回退 Updates 根
                try
                {
                    var fallback = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "ForgeSelf", "Updates");
                    Directory.CreateDirectory(fallback);
                    _path = Path.Combine(fallback, $"update-agent-{DateTime.Now:yyyyMMddHHmmss}.log");
                    File.AppendAllText(_path, line + Environment.NewLine, System.Text.Encoding.UTF8);
                }
                catch
                {
                    // 降级 2（终态）：纯控制台，绝不抛
                    _path = null;
                }
            }
        }
    }
}
