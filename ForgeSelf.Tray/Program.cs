using System.Diagnostics;
using System.IO.Pipes;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using NewLife.Log;
using ForgeSelf.Api;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Security;
using ForgeSelf.Api.Services;

var options = ParseArgs(args);
var dataRoot = HostDataRoot.ResolveFromEnvironment();
Directory.CreateDirectory(Path.Combine(dataRoot, "log"));
NewLife.Setting.Current.LogPath = Path.Combine(dataRoot, "log");
XTrace.LogPath = Path.Combine(dataRoot, "log");

var port = options.Port;
if (port == 0)
    port = ForgeSetting.Current.PortNumber;

XTrace.Log.Info("Tray: 启动 role={0} port={1} pipe={2}", options.Role, port, options.PipeName);

var apiDir = string.IsNullOrEmpty(options.ApiExecutable)
    ? AppContext.BaseDirectory
    : Path.GetDirectoryName(options.ApiExecutable) ?? AppContext.BaseDirectory;
var config = new ConfigurationBuilder()
    .SetBasePath(apiDir)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .Build();
var encryption = new AesSecretEncryptionService(config, new MachineKeyProvider());
var keys = new ApiServerKeyService(encryption);
var serviceManager = new ServiceManager(
    new ServiceConfig(),
    string.IsNullOrWhiteSpace(options.ApiExecutable) ? null : options.ApiExecutable);

var version = "0";
if (!string.IsNullOrEmpty(options.ApiExecutable) && File.Exists(options.ApiExecutable))
    version = FileVersionInfo.GetVersionInfo(options.ApiExecutable).FileVersion ?? version;

using var tray = new TrayIconManager(
    serviceManager,
    port,
    appVersion: version,
    mainPageTokenResolver: () => keys.TryReadActiveKeyPlain());

using var exit = new ManualResetEventSlim(false);
NamedPipeClientStream? pipe = null;

tray.Configure(
    onCheckUpdate: () =>
    {
        var token = keys.TryReadActiveKeyPlain();
        _ = CheckUpdateAsync(port, token, tray);
    },
    onExit: () =>
    {
        if (options.Role.Equals("interactive", StringComparison.OrdinalIgnoreCase))
            WriteLine(pipe, "shutdown");
        exit.Set();
    });

tray.Show();

if (!string.IsNullOrEmpty(options.PipeName))
{
    _ = Task.Run(async () =>
    {
        try
        {
            pipe = new NamedPipeClientStream(".", options.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(10000);
            WriteLine(pipe, "hello");
            while (true)
            {
                var message = await ReadLineAsync(pipe);
                if (message == null) break;
                if (message.Equals("shutdown", StringComparison.OrdinalIgnoreCase))
                {
                    XTrace.Log.Info("Tray: 收到宿主 shutdown");
                    exit.Set();
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("Tray: 管道连接失败: {0}", ex.Message);
        }
    });
}

_ = Task.Run(async () =>
{
    try
    {
        await Task.Delay(3000);
        await CheckUpdateAsync(port, keys.TryReadActiveKeyPlain(), tray);
    }
    catch (Exception ex)
    {
        XTrace.Log.Error("Tray: 启动时检查更新失败: {0}", ex.Message);
    }
});

exit.Wait();
tray.Hide();
pipe?.Dispose();
return 0;

static TrayOptions ParseArgs(string[] args)
{
    var result = new TrayOptions();
    for (var i = 0; i < args.Length; i++)
    {
        var key = args[i];
        if (!key.StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
            continue;
        var value = args[++i];
        if (key.Equals("--pipe-name", StringComparison.OrdinalIgnoreCase)) result.PipeName = value;
        else if (key.Equals("--port", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var port)) result.Port = port;
        else if (key.Equals("--role", StringComparison.OrdinalIgnoreCase)) result.Role = value;
        else if (key.Equals("--api-exe", StringComparison.OrdinalIgnoreCase)) result.ApiExecutable = value;
    }
    if (string.IsNullOrEmpty(result.Role)) result.Role = "service";
    return result;
}

static void WriteLine(Stream? stream, string line)
{
    if (stream == null || !stream.CanWrite) return;
    var bytes = Encoding.UTF8.GetBytes(line + "\n");
    try
    {
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }
    catch (Exception ex)
    {
        XTrace.Log.Warn("Tray: 写管道失败: {0}", ex.Message);
    }
}

static async Task<string?> ReadLineAsync(Stream stream)
{
    var buffer = new byte[1];
    var text = new StringBuilder();
    while (true)
    {
        var read = await stream.ReadAsync(buffer);
        if (read == 0)
            return text.Length == 0 ? null : text.ToString();
        if (buffer[0] == '\n')
            return text.ToString().TrimEnd('\r');
        text.Append((char)buffer[0]);
    }
}

static async Task CheckUpdateAsync(int port, string? token, TrayIconManager tray)
{
    if (string.IsNullOrEmpty(token))
    {
        tray.ShowBalloonTip("检查更新", "没有读到本机令牌，无法向 API 鉴权。", ToolTipIcon.Warning);
        return;
    }

    try
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.PostAsync(
            $"http://127.0.0.1:{port}/api/update/check",
            new StringContent(string.Empty));
        var body = await response.Content.ReadAsStringAsync();
        var hasUpdate = false;
        var latest = "";
        if (!string.IsNullOrWhiteSpace(body))
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("data", out var data))
            {
                if (data.TryGetProperty("hasUpdate", out var flag) && flag.ValueKind == JsonValueKind.True)
                    hasUpdate = true;
                if (data.TryGetProperty("latestVersion", out var versionNode))
                    latest = versionNode.ToString();
            }
        }

        if (!response.IsSuccessStatusCode)
        {
            tray.ShowBalloonTip("检查更新失败", $"HTTP {(int)response.StatusCode}", ToolTipIcon.Error);
            return;
        }

        if (hasUpdate)
            tray.ShowBalloonTip("有新版本可用", string.IsNullOrEmpty(latest) ? "有新版本可用。" : $"版本 {latest} 已可用。", ToolTipIcon.Info);
        else
            tray.ShowBalloonTip("已是最新版本", "当前已是最新版本。", ToolTipIcon.Info);
    }
    catch (Exception ex)
    {
        XTrace.Log.Error("Tray: 检查更新失败: {0}", ex.Message);
        tray.ShowBalloonTip("检查更新失败", ex.Message, ToolTipIcon.Error);
    }
}

sealed class TrayOptions
{
    public string PipeName { get; set; } = "";
    public int Port { get; set; }
    public string Role { get; set; } = "service";
    public string? ApiExecutable { get; set; }
}
