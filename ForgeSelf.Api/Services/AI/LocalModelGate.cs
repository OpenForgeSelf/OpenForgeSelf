using System.Net;
using System.Text.Json;
using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Services.AI;

/// <summary>
/// 本地模式开关与本机地址判断。开启后，模型请求发往非回环地址会被拒绝，请求不会出网。
/// 开关落在程序目录 local-model-mode.json，重启后仍生效。
/// </summary>
public static class LocalModelGate
{
    private static readonly object Sync = new();
    private static int _enabled;
    private static bool _loaded;

    public static bool Enabled
    {
        get
        {
            EnsureLoaded();
            return Volatile.Read(ref _enabled) == 1;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        EnsureLoaded();
        Volatile.Write(ref _enabled, enabled ? 1 : 0);
        Persist();
    }

    /// <summary>本地模式开启且目标不是本机时抛出，调用方不得继续发请求。</summary>
    public static void EnsureAllowed(string? requestUrl)
    {
        if (!Enabled) return;
        if (IsLoopback(requestUrl)) return;

        var host = TryHost(requestUrl) ?? (string.IsNullOrWhiteSpace(requestUrl) ? "(空地址)" : requestUrl);
        throw new InvalidOperationException($"本地模式已开启，拒绝向非本机地址发送模型请求：{host}");
    }

    public static bool IsLoopback(string? endpoint)
    {
        var host = TryHost(endpoint);
        if (string.IsNullOrWhiteSpace(host)) return false;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
        if (host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)) return true;
        if (host.Equals("::1", StringComparison.OrdinalIgnoreCase)) return true;
        if (host.Equals("[::1]", StringComparison.OrdinalIgnoreCase)) return true;
        return IPAddress.TryParse(host.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip);
    }

    /// <summary>回环地址，或尚未填写地址的 Ollama / LM Studio，标为本地；其余标为远程。</summary>
    public static AIProviderDeployment ResolveDeployment(AIProviderType type, string? endpoint)
    {
        if (IsLoopback(endpoint)) return AIProviderDeployment.Local;
        if (string.IsNullOrWhiteSpace(endpoint) && type is AIProviderType.Ollama or AIProviderType.LMStudio)
            return AIProviderDeployment.Local;
        return AIProviderDeployment.Remote;
    }

    private static string? TryHost(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) return null;
        var raw = endpoint.Trim();
        if (!raw.Contains("://", StringComparison.Ordinal))
            raw = "http://" + raw;
        return Uri.TryCreate(raw, UriKind.Absolute, out var uri) ? uri.Host : null;
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        lock (Sync)
        {
            if (_loaded) return;
            try
            {
                var path = FilePath();
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var doc = JsonSerializer.Deserialize<LocalModeFile>(json);
                    Volatile.Write(ref _enabled, doc?.Enabled == true ? 1 : 0);
                }
            }
            catch
            {
                Volatile.Write(ref _enabled, 0);
            }
            _loaded = true;
        }
    }

    private static void Persist()
    {
        lock (Sync)
        {
            try
            {
                var path = FilePath();
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(new LocalModeFile { Enabled = Volatile.Read(ref _enabled) == 1 }, ForgeJson.Default);
                File.WriteAllText(path, json);
            }
            catch
            {
                // 落盘失败时本次进程内开关仍然有效
            }
        }
    }

    private static string FilePath() => Path.Combine(AppContext.BaseDirectory, "local-model-mode.json");

    private sealed class LocalModeFile
    {
        public bool Enabled { get; set; }
    }
}
