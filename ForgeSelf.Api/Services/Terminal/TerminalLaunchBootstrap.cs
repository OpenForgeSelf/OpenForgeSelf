using System.Diagnostics;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Security;
using Microsoft.Extensions.Configuration;
using NewLife.Log;

namespace ForgeSelf.Api.Services.Terminal;

/// <summary>
/// 主实例收起启动参数并登记 <c>forgeself:</c> 协议；从实例把参数转交后退出。
/// </summary>
public static class TerminalLaunchBootstrap
{
    private static bool _openUi;

    public static string[] PreparePrimary(string[] args)
    {
        ForgeselfProtocol.TryRegisterCurrentProcess();
        var parsed = TerminalOpenParser.TryParse(args);
        if (parsed == null) return args;

        TerminalOpenInbox.Enqueue(parsed.Query, null, parsed.SecretStripped);
        if (Environment.UserInteractive
            && Environment.GetEnvironmentVariable("FORGESELF_NO_TRAY") != "1")
        {
            _openUi = true;
        }

        if (parsed.SecretStripped)
            XTrace.Log.Warn("[terminal-open] 启动参数里的口令已丢弃，不会写入收件箱。");
        return TerminalOpenParser.Strip(args);
    }

    /// <returns>0 已送达；2 有打开请求但没送达；-1 这不是打开请求。</returns>
    public static int TryForwardToRunningInstance(string[] args)
    {
        var parsed = TerminalOpenParser.TryParse(args);
        if (parsed == null) return -1;
        if (parsed.SecretStripped)
            XTrace.Log.Warn("[terminal-open] 转交前已丢弃启动参数中的口令。");
        return TerminalInstanceForwarder.TryForward(parsed) ? 0 : 2;
    }

    public static void OpenUiIfRequested()
    {
        if (!_openUi) return;
        _openUi = false;
        TerminalUiOpener.OpenSmartTerminalWhenReady();
    }
}

public static class ForgeselfProtocol
{
    public const string Scheme = "forgeself";

    public static string CommandValue(string exePath)
        => $"\"{exePath}\" \"%1\"";

    /// <summary>
    /// 版本目录里的业务层 exe 登记安装根的启动器，这样升级后协议仍指向根启动器。
    /// 根启动器会把 URL 原样转给当前版本。
    /// </summary>
    public static string ResolveRegistrationExe(string processPath, Func<string, bool> exists)
    {
        var dir = Path.GetDirectoryName(processPath);
        var versions = string.IsNullOrEmpty(dir) ? null : Directory.GetParent(dir);
        var root = versions?.Parent?.FullName;
        if (versions != null
            && versions.Name.Equals("versions", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(root))
        {
            var launcher = Path.Combine(root, "ForgeSelf.exe");
            if (exists(launcher)) return launcher;
        }
        return processPath;
    }

    public static void TryRegisterCurrentProcess()
    {
        if (!OperatingSystem.IsWindows()) return;
        if (!Environment.UserInteractive) return;
        if (Environment.GetEnvironmentVariable("FORGESELF_NO_TRAY") == "1") return;
        if (Environment.GetEnvironmentVariable("FORGESELF_NO_PROTOCOL") == "1") return;
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FORGESelf_INSTANCE_ID"))) return;

        var processPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(processPath)) return;
        var name = Path.GetFileName(processPath);
        if (name.Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            || name.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            var exe = ResolveRegistrationExe(processPath, File.Exists);
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Classes\forgeself");
            key?.SetValue("", "URL:ForgeSelf Protocol");
            key?.SetValue("URL Protocol", "");
            using var command = key?.CreateSubKey(@"shell\open\command");
            command?.SetValue("", CommandValue(exe));
            XTrace.Log.Info("[terminal-open] 已登记 forgeself: 协议 → {0}", exe);
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[terminal-open] 协议登记失败: {0}", ex.Message);
        }
    }
}

public static class TerminalUiOpener
{
    public static void OpenSmartTerminalWhenReady()
    {
        var port = ForgeSetting.Current.PortNumber;
        if (port <= 0) return;
        _ = Task.Run(() => WaitAndOpen(port));
    }

    private static void WaitAndOpen(int port)
    {
        using var handler = new HttpClientHandler { UseProxy = false };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(1) };
        for (var i = 0; i < 40; i++)
        {
            try
            {
                using var res = http.GetAsync($"http://127.0.0.1:{port}/api/health").GetAwaiter().GetResult();
                if (res.IsSuccessStatusCode) break;
            }
            catch
            {
                // 端口还没听
            }
            Thread.Sleep(250);
        }

        var url = $"http://127.0.0.1:{port}/smart-terminal";
        var token = ReadTokenWithoutRotate();
        if (!string.IsNullOrEmpty(token))
            url += "#token=" + Uri.EscapeDataString(token);

        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            XTrace.Log.Info("[terminal-open] 已打开智能终端页面。");
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[terminal-open] 打开浏览器失败: {0}", ex.Message);
        }
    }

    private static string? ReadTokenWithoutRotate()
    {
        try
        {
            var cipher = ForgeSetting.Current.ApiToken;
            if (string.IsNullOrEmpty(cipher)) return null;
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();
            var encryption = new AesSecretEncryptionService(config, new MachineKeyProvider());
            return encryption.TryDecrypt(cipher, out var plain) ? plain : null;
        }
        catch
        {
            return null;
        }
    }
}
