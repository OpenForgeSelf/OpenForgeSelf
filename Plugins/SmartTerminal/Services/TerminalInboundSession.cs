using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.SmartTerminal.Models;

namespace ForgeSelf.Api.Plugins.SmartTerminal.Services;

/// <summary>
/// 把进站 query 收成创建会话请求。口令只放在 <see cref="CreateSessionRequest.Password"/>，
/// 描述串不得包含口令原文。
/// </summary>
public static class TerminalInboundSession
{
    public sealed class Built
    {
        public CreateSessionRequest Request { get; init; } = new();
        public IReadOnlyList<string> Steps { get; init; } = Array.Empty<string>();
    }

    public static Built Build(string? query, string? password)
    {
        var (clean, fromQuery, _) = TerminalOpenSecrets.Split(query);
        var secret = string.IsNullOrEmpty(password) ? fromQuery : password;
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in clean.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            var key = Uri.UnescapeDataString(eq < 0 ? part : part[..eq]);
            var value = Uri.UnescapeDataString(eq < 0 ? "" : part[(eq + 1)..]);
            if (!map.TryGetValue(key, out var list))
            {
                list = new List<string>();
                map[key] = list;
            }
            list.Add(value);
        }

        string? One(params string[] keys)
        {
            foreach (var key in keys)
            {
                if (map.TryGetValue(key, out var list) && list.Count > 0 && !string.IsNullOrWhiteSpace(list[0]))
                    return list[0];
            }
            return null;
        }

        var type = (One("type", "st_type", "profile", "st_profile") ?? "local").Trim().ToLowerInvariant();
        var kind = type is "ssh" ? "ssh" : "local";
        var auth = One("auth", "st_auth");
        if (kind == "ssh" && !string.Equals(auth, "key", StringComparison.OrdinalIgnoreCase))
            auth = "interactive";

        int? port = null;
        var portText = One("port", "st_port");
        if (int.TryParse(portText, out var parsedPort)) port = parsedPort;

        var steps = new List<string>();
        foreach (var key in new[] { "step", "st_step", "cmd", "st_cmd" })
        {
            if (!map.TryGetValue(key, out var list)) continue;
            steps.AddRange(list.Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        return new Built
        {
            Request = new CreateSessionRequest
            {
                Kind = kind,
                Title = One("title", "st_title"),
                ShellId = One("shell", "st_shell"),
                Cwd = One("cwd", "st_cwd"),
                Host = One("host", "st_host"),
                Port = port,
                User = One("user", "st_user"),
                Auth = kind == "ssh" ? auth : null,
                IdentityFile = One("identityFile", "identity", "key"),
                Password = kind == "ssh" ? secret : null
            },
            Steps = steps
        };
    }

    /// <summary>日志只用这一行。口令是否存在用布尔表示，原文不出现。</summary>
    public static string Describe(CreateSessionRequest request)
        => $"kind={request.Kind};host={request.Host};user={request.User};passwordProvided={!string.IsNullOrEmpty(request.Password)}";
}
