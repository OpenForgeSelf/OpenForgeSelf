using System.Runtime.InteropServices;
using System.Text.Json;
using ForgeSelf.Api.Plugins.SmartTerminal.Models;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.SmartTerminal.Services;

/// <summary>
/// Windows 多壳探测：cmd / Windows PowerShell / pwsh，并尽量读取 Windows Terminal settings.json profiles。
/// 非 Windows 宿主：返回可用 POSIX 壳并标记 Windows 壳 unavailable（不解构建）。
/// </summary>
public interface IShellProfileDetector
{
    IReadOnlyList<ShellProfileDto> ListProfiles();
    ShellProfileDto? Find(string shellId);
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

    private static List<ShellProfileDto> Detect()
    {
        var list = new List<ShellProfileDto>();
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        if (isWindows)
        {
            list.Add(Make("cmd", "Command Prompt", "cmd.exe", "/d /c {command}", "cmd", FindOnPath("cmd.exe")));
            list.Add(Make("powershell", "Windows PowerShell", "powershell.exe", "-NoProfile -NonInteractive -Command {command}", "powershell", FindOnPath("powershell.exe")));
            list.Add(Make("pwsh", "PowerShell 7+", "pwsh.exe", "-NoProfile -NonInteractive -Command {command}", "pwsh", FindOnPath("pwsh.exe") ?? FindOnPath("pwsh")));
            TryAddWindowsTerminalProfiles(list);
        }
        else
        {
            list.Add(Make("cmd", "Command Prompt", "cmd.exe", "/d /c {command}", "cmd", null, available: false, note: "仅 Windows 可用"));
            list.Add(Make("powershell", "Windows PowerShell", "powershell.exe", "-NoProfile -NonInteractive -Command {command}", "powershell", null, available: false, note: "仅 Windows 可用"));
            list.Add(Make("pwsh", "PowerShell 7+", "pwsh", "-NoProfile -NonInteractive -Command {command}", "pwsh", FindOnPath("pwsh")));
            list.Add(Make("bash", "Bash", "bash", "-lc {command}", "bash", FindOnPath("bash")));
            list.Add(Make("sh", "POSIX sh", "sh", "-c {command}", "other", FindOnPath("sh")));
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
                // unpackaged / scoop / portable
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
