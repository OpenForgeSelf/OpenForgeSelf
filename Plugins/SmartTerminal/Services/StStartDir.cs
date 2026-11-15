using ForgeSelf.Api.Plugins.SmartTerminal.Models;

namespace ForgeSelf.Api.Plugins.SmartTerminal.Services;

/// <summary>
/// 会话起始目录解析（单一真源：V1 创建 + V2 ResolveLocalCwd 共用，PC-036）。
/// 不变量：返回值必须是已存在目录的绝对路径；报错信息只回显用户可理解的输入，
/// 不拼接宿主进程工作目录（2026-11-13 BUG#1 泄露面的类级修复）。
/// </summary>
public static class StStartDir
{
    public static string Resolve(string? cwd)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var hasHome = !string.IsNullOrEmpty(home) && Directory.Exists(home);
        if (string.IsNullOrWhiteSpace(cwd))
            return hasHome ? home : Environment.CurrentDirectory;

        var raw = cwd.Trim();
        string path;
        if (raw is "~" or "~/" or "~\\")
        {
            if (!hasHome)
                throw new SmartTerminalException("invalid_cwd", "工作目录不存在: ~", 400);
            path = home;
        }
        else if (raw.StartsWith("~/") || raw.StartsWith("~\\"))
        {
            if (!hasHome)
                throw new SmartTerminalException("invalid_cwd", "工作目录不存在: " + raw, 400);
            path = Path.Combine(home, raw[2..]);
        }
        else
        {
            path = Environment.ExpandEnvironmentVariables(raw);
        }

        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception)
        {
            throw new SmartTerminalException("invalid_cwd", "工作目录不存在: " + Display(raw, path), 400);
        }
        if (!Directory.Exists(full))
            throw new SmartTerminalException("invalid_cwd", "工作目录不存在: " + Display(raw, full), 400);
        return full;
    }

    /// <summary>报错回显：根路径与 `~` 形态展示解析后的完整路径（便于用户自查）；
    /// 相对路径只回显用户输入原文，不暴露宿主进程工作目录。</summary>
    private static string Display(string raw, string full)
        => raw.StartsWith("~") || Path.IsPathRooted(raw) ? full : raw;
}
