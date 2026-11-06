using System.Diagnostics;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins;
using ForgeSelf.Core;

namespace ForgeSelf.Api.Plugins.Services;

/// <summary>在插件 <c>src/</c> 里构建、测试、打包。已是 git 仓库时用独立 worktree。</summary>
public sealed class PluginBuildService
{
    private readonly IDataLocationService _data;
    private readonly PluginJobStore _jobs;

    public PluginBuildService(IDataLocationService data, PluginJobStore jobs)
    {
        _data = data;
        _jobs = jobs;
    }

    public async Task<(bool Ok, string JobId, string Message)> RunAsync(
        string pluginId, string verb, CancellationToken cancellationToken)
    {
        var pluginDir = _data.GetPluginDataDirectory(pluginId);
        PluginVersionLayout.EnsureLayout(pluginDir);
        var src = PluginVersionLayout.SourceDirectory(pluginDir);
        if (!Directory.Exists(src))
        {
            var id = _jobs.Write(pluginDir, verb, false, null, "src 不存在");
            return (false, id, "src 不存在");
        }

        var work = src;
        string? worktree = null;
        if (IsGitCheckout(src))
        {
            worktree = Path.Combine(Path.GetTempPath(), "ofs-plugin-wt-" + Guid.NewGuid().ToString("N"));
            var added = await RunProcessAsync("git", $"-C \"{src}\" worktree add --detach \"{worktree}\" HEAD", cancellationToken);
            if (added.ExitCode == 0)
                work = worktree;
            else
                worktree = null;
        }

        try
        {
            var (file, args) = verb switch
            {
                "build" => ("dotnet", $"build \"{work}\" --nologo"),
                "test" => ("dotnet", $"test \"{work}\" --nologo"),
                "package" => BuildPackageCommand(work),
                _ => throw new InvalidOperationException($"未知动词 {verb}")
            };
            if (file == null)
            {
                var missing = _jobs.Write(pluginDir, verb, false, null, "找不到 scripts/package-plugin.ps1");
                return (false, missing, "找不到 scripts/package-plugin.ps1");
            }

            var result = await RunProcessAsync(file, args, cancellationToken);
            var ok = result.ExitCode == 0;
            var message = ok ? "完成" : Trim(result.Output);
            var jobId = _jobs.Write(pluginDir, verb, ok, null, message);
            return (ok, jobId, message);
        }
        finally
        {
            if (worktree != null)
            {
                await RunProcessAsync("git", $"-C \"{src}\" worktree remove --force \"{worktree}\"", CancellationToken.None);
                if (Directory.Exists(worktree))
                    Directory.Delete(worktree, true);
            }
        }
    }

    private static (string? File, string Args) BuildPackageCommand(string work)
    {
        var script = FindRepoFile(Path.Combine("scripts", "package-plugin.ps1"));
        if (script == null)
            return (null, "");
        return ("pwsh", $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -Source \"{work}\"");
    }

    private static bool IsGitCheckout(string src) =>
        Directory.Exists(Path.Combine(src, ".git")) || File.Exists(Path.Combine(src, ".git"));

    internal static string? FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    private static async Task<(int ExitCode, string Output)> RunProcessAsync(
        string fileName, string arguments, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"无法启动 {fileName}");
        var stdoutTask = ProcessOutputText.CaptureAsync(process.StandardOutput.BaseStream, cancellationToken);
        var stderrTask = ProcessOutputText.CaptureAsync(process.StandardError.BaseStream, cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stdout = ProcessOutputText.Decode(await stdoutTask);
        var stderr = ProcessOutputText.Decode(await stderrTask);
        return (process.ExitCode, Trim(stdout.Text + stderr.Text));
    }

    private static string Trim(string text)
    {
        var t = text.Trim();
        return t.Length <= 500 ? t : t[^500..];
    }
}
