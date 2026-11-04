using System.Runtime.InteropServices;
using System.Text.Json;
using ForgeSelf.Api.Plugins.SmartTerminal.Models;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.SmartTerminal.Services;

/// <summary>
/// 跨平台外壳探测：Windows（cmd / Windows PowerShell / pwsh + WT profiles）与
/// Linux/macOS（bash / zsh / sh，以及 fish/dash）均为一等公民；不可用项保留在列表中并标记 Available=false。
/// </summary>
public interface IShellProfileDetector
{
    IReadOnlyList<ShellProfileDto> ListProfiles();
    ShellProfileDto? Find(string shellId);
    /// <summary>按平台与 $SHELL 给出默认首选壳 Id（仅在 Available 时返回）。</summary>
    string? PreferDefaultShellId();
}

public sealed class ShellProfileDetector : IShellProfileDetector
{
    private readonly object _lock = new();
    private IReadOnlyList<ShellProfileDto>? _cache;

    public IReadOnlyList<ShellProfileDto> ListProfiles()
    {
        lock (_lock)
        {
            return _cache ??= Detect();
        }
    }

    public ShellProfileDto? Find(string shellId)
        => ListProfiles().FirstOrDefault(p => string.Equals(p.Id, shellId, StringComparison.OrdinalIgnoreCase));

    public string? PreferDefaultShellId()
    {
        var shells = ListProfiles();
        var env = Environment.GetEnvironmentVariable("SHELL");
        if (!string.IsNullOrWhiteSpace(env))
        {
            var baseName = Path.GetFileName(env.Trim()).ToLowerInvariant();
            // strip .exe on Windows paths pointing at Git Bash etc.
            if (baseName.EndsWith(".exe", StringComparison.Ordinal))
                baseName = baseName[..^4];
            var fromEnv = shells.FirstOrDefault(s => s.Available && string.Equals(s.Id, baseName, StringComparison.OrdinalIgnoreCase));
            if (fromEnv != null) return fromEnv.Id;
        }

        // Linux/macOS：POSIX 优先；Windows：PowerShell 家族优先，Git Bash 次之
        string[] order = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ["pwsh", "powershell", "cmd", "bash", "zsh", "sh", "fish", "dash"]
            : ["bash", "zsh", "sh", "pwsh", "fish", "dash", "powershell", "cmd"];

        foreach (var id in order)
        {
            var hit = shells.FirstOrDefault(s => s.Available && s.Id == id);
            if (hit != null) return hit.Id;
        }

        return shells.FirstOrDefault(s => s.Available)?.Id;
    }

    private static List<ShellProfileDto> Detect()
    {
        var list = new List<ShellProfileDto>();
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        // —— Windows 壳（Linux 上仍列出，标记不可用，便于 UI 多壳选择器对照）——
        list.Add(Make(
            "cmd", "Command Prompt", "cmd.exe", "/d /c {command}", "cmd",
            isWindows ? FindOnPath("cmd.exe") : null,
            available: isWindows ? null : false,
            note: isWindows ? null : "仅 Windows 可用"));
        list.Add(Make(
            "powershell", "Windows PowerShell", "powershell.exe",
            "-NoProfile -NonInteractive -Command {command}", "powershell",
            isWindows ? FindOnPath("powershell.exe") : null,
            available: isWindows ? null : false,
            note: isWindows ? null : "仅 Windows 可用"));
        list.Add(Make(
            "pwsh", "PowerShell 7+", isWindows ? "pwsh.exe" : "pwsh",
            "-NoProfile -NonInteractive -Command {command}", "pwsh",
            FindOnPath(isWindows ? "pwsh.exe" : "pwsh") ?? FindOnPath("pwsh")));

        if (isWindows)
            TryAddWindowsTerminalProfiles(list);

        // —— POSIX 壳：Linux/macOS 一等公民；Windows 上探测 Git Bash / 其它 PATH 条目 ——
        list.Add(Make("bash", "Bash", "bash", "-lc {command}", "bash", FindPosixShell("bash")));
        list.Add(Make("zsh", "Zsh", "zsh", "-lc {command}", "zsh", FindPosixShell("zsh")));
        list.Add(Make("sh", "POSIX sh", "sh", "-c {command}", "sh", FindPosixShell("sh")));
        list.Add(Make("fish", "Fish", "fish", "-c {command}", "fish", FindPosixShell("fish")));
        list.Add(Make("dash", "Dash", "dash", "-c {command}", "sh", FindPosixShell("dash")));

        // 标注 $SHELL 首选
        var shellEnv = Environment.GetEnvironmentVariable("SHELL");
        if (!string.IsNullOrWhiteSpace(shellEnv))
        {
            var baseName = Path.GetFileName(shellEnv.Trim()).ToLowerInvariant();
            if (baseName.EndsWith(".exe", StringComparison.Ordinal))
                baseName = baseName[..^4];
            for (var i = 0; i < list.Count; i++)
            {
                var cur = list[i];
                if (!string.Equals(cur.Id, baseName, StringComparison.OrdinalIgnoreCase)) continue;
                if (!cur.Available) continue;
                var note = string.IsNullOrWhiteSpace(cur.Note)
                    ? $"$SHELL 默认（{shellEnv}）"
                    : cur.Note + $" · $SHELL 默认（{shellEnv}）";
                list[i] = new ShellProfileDto
                {
                    Id = cur.Id,
                    DisplayName = cur.DisplayName,
                    Executable = cur.Executable,
                    ArgumentsTemplate = cur.ArgumentsTemplate,
                    Kind = cur.Kind,
                    Available = cur.Available,
                    Source = cur.Source,
                    Note = note
                };
            }
        }

        return list;
    }

    private static void TryAddWindowsTerminalProfiles(List<ShellProfileDto> list)
    {
        try
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var settings = Path.Combine(local, "Packages", "Microsoft.WindowsTerminal_8wekyb3d8bbwe", "LocalState", "settings.json");
            if (!File.Exists(settings))
            {
                var alt = Path.Combine(local, "Microsoft", "Windows Terminal", "settings.json");
                if (File.Exists(alt)) settings = alt;
                else return;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(settings));
            if (!doc.RootElement.TryGetProperty("profiles", out var profiles)) return;
            JsonElement listEl;
            if (profiles.ValueKind == JsonValueKind.Object && profiles.TryGetProperty("list", out var nested))
                listEl = nested;
            else if (profiles.ValueKind == JsonValueKind.Array)
                listEl = profiles;
            else return;

            var i = 0;
            foreach (var p in listEl.EnumerateArray())
            {
                i++;
                var name = p.TryGetProperty("name", out var n) ? n.GetString() : null;
                var guid = p.TryGetProperty("guid", out var g) ? g.GetString() : null;
                var commandline = p.TryGetProperty("commandline", out var c) ? c.GetString() : null;
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(commandline)) continue;

                var exe = commandline.Trim().Trim('"');
                var firstSpace = exe.IndexOf(' ');
                var file = firstSpace > 0 ? exe[..firstSpace].Trim('"') : exe;
                var id = "wt:" + (guid ?? $"profile-{i}");
                if (list.Any(x => x.Id == id)) continue;

                list.Add(new ShellProfileDto
                {
                    Id = id,
                    DisplayName = $"WT · {name}",
                    Executable = file,
                    ArgumentsTemplate = firstSpace > 0 ? exe[(firstSpace + 1)..] + " {command}" : "{command}",
                    Kind = "wt-profile",
                    Available = File.Exists(file) || FindOnPath(Path.GetFileName(file)) != null,
                    Source = settings,
                    Note = "来自 Windows Terminal settings.json"
                });
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Debug("[SmartTerminal] 读取 Windows Terminal profiles 失败: {0}", ex.Message);
        }
    }

    private static ShellProfileDto Make(string id, string display, string exe, string args, string kind, string? resolved, bool? available = null, string? note = null)
        => new()
        {
            Id = id,
            DisplayName = display,
            Executable = resolved ?? exe,
            ArgumentsTemplate = args,
            Kind = kind,
            Available = available ?? !string.IsNullOrEmpty(resolved),
            Source = resolved,
            Note = note
        };

    /// <summary>POSIX 壳探测：PATH + 常见绝对路径 +（Windows）Git Bash。</summary>
    private static string? FindPosixShell(string name)
    {
        var hit = FindOnPath(name) ?? FindOnPath(name + ".exe");
        if (hit != null) return hit;

        string[] wellKnown =
        [
            $"/bin/{name}",
            $"/usr/bin/{name}",
            $"/usr/local/bin/{name}",
            $"/opt/homebrew/bin/{name}",
            // Git for Windows
            $@"C:\Program Files\Git\bin\{name}.exe",
            $@"C:\Program Files\Git\usr\bin\{name}.exe",
            $@"C:\Program Files (x86)\Git\bin\{name}.exe",
        ];
        foreach (var p in wellKnown)
        {
            try
            {
                if (File.Exists(p)) return p;
            }
            catch
            {
                // ignore
            }
        }

        return null;
    }

    private static string? FindOnPath(string fileName)
    {
        try
        {
            if (File.Exists(fileName)) return Path.GetFullPath(fileName);
            var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    var candidate = Path.Combine(dir.Trim('"'), fileName);
                    if (File.Exists(candidate)) return candidate;
                }
                catch
                {
                    // ignore bad PATH segments
                }
            }
        }
        catch
        {
            // ignore
        }
        return null;
    }
}
