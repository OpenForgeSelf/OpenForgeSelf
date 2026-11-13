using System.Collections.Concurrent;

namespace ForgeSelf.Abstractions;

/// <summary>
/// 一次「打开智能终端标签」的通知。Query 与页面深链同一套参数，不含密码。
/// SessionId 有值时表示会话已由 Agent 建好，界面只附着该标签，不再新建。
/// </summary>
public sealed class TerminalOpenNotice
{
    public string Id { get; init; } = string.Empty;
    public string? Query { get; init; }
    public string? SessionId { get; init; }
    public bool SecretStripped { get; init; }
    public DateTimeOffset At { get; init; }
}

/// <summary>
/// 进程内收件箱。冷启动由宿主在拉起 Web 之前写入；已运行实例由
/// <c>POST /api/smart-terminal/inbound</c> 写入。界面轮询取走。
/// </summary>
public static class TerminalOpenInbox
{
    private static readonly ConcurrentQueue<TerminalOpenNotice> Queue = new();

    public static TerminalOpenNotice Enqueue(string? query, string? sessionId, bool secretStripped = false)
    {
        var notice = new TerminalOpenNotice
        {
            Id = Guid.NewGuid().ToString("n"),
            Query = string.IsNullOrWhiteSpace(query) ? null : query.Trim(),
            SessionId = string.IsNullOrWhiteSpace(sessionId) ? null : sessionId.Trim(),
            SecretStripped = secretStripped,
            At = DateTimeOffset.UtcNow
        };
        Queue.Enqueue(notice);
        return notice;
    }

    public static IReadOnlyList<TerminalOpenNotice> Drain()
    {
        var list = new List<TerminalOpenNotice>();
        while (Queue.TryDequeue(out var notice))
            list.Add(notice);
        return list;
    }

    public static void Clear()
    {
        while (Queue.TryDequeue(out _)) { }
    }
}

/// <summary>深链里禁止出现的口令键。宿主解析与插件入站共用。</summary>
public static class TerminalOpenSecrets
{
    private static readonly HashSet<string> Keys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "pwd", "passwd", "pass", "passphrase", "secret", "token",
        "ssh_password", "sshpass", "ssh_passwd",
        "st_password", "st_pwd", "st_pass"
    };

    public static bool IsPasswordKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        var name = key.Trim();
        if (name.Equals("token", StringComparison.OrdinalIgnoreCase)
            || name.Equals("secret", StringComparison.OrdinalIgnoreCase))
            return false;
        if (name.Equals("password", StringComparison.OrdinalIgnoreCase)
            || name.Equals("pwd", StringComparison.OrdinalIgnoreCase)
            || name.Equals("passwd", StringComparison.OrdinalIgnoreCase)
            || name.Equals("pass", StringComparison.OrdinalIgnoreCase)
            || name.Equals("passphrase", StringComparison.OrdinalIgnoreCase)
            || name.Equals("ssh_password", StringComparison.OrdinalIgnoreCase)
            || name.Equals("sshpass", StringComparison.OrdinalIgnoreCase)
            || name.Equals("ssh_passwd", StringComparison.OrdinalIgnoreCase)
            || name.Equals("st_password", StringComparison.OrdinalIgnoreCase)
            || name.Equals("st_pwd", StringComparison.OrdinalIgnoreCase)
            || name.Equals("st_pass", StringComparison.OrdinalIgnoreCase))
            return true;
        return name.Contains("password", StringComparison.OrdinalIgnoreCase)
               || name.Contains("passwd", StringComparison.OrdinalIgnoreCase)
               || name.Contains("sshpass", StringComparison.OrdinalIgnoreCase)
               || name.Contains("passphrase", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSecretKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        var name = key.Trim();
        if (Keys.Contains(name)) return true;
        return IsPasswordKey(name);
    }

    /// <summary>
    /// 从 query 取出第一个 SSH 口令，并从结果里删掉口令键和其他敏感键。
    /// 返回的 Clean 不含口令；Password 只供当次建连，调用方用完即丢，不得写入日志。
    /// </summary>
    public static (string Clean, string? Password, bool DroppedOtherSecret) Split(string? query)
    {
        var kept = new List<string>();
        string? password = null;
        var droppedOther = false;
        if (!string.IsNullOrEmpty(query))
        {
            foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = part.IndexOf('=');
                var rawKey = eq < 0 ? part : part[..eq];
                var rawValue = eq < 0 ? "" : part[(eq + 1)..];
                var key = Uri.UnescapeDataString(rawKey.Replace("+", " ", StringComparison.Ordinal));
                var value = Uri.UnescapeDataString(rawValue.Replace("+", " ", StringComparison.Ordinal));
                if (IsPasswordKey(key))
                {
                    if (string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(value))
                        password = value;
                    continue;
                }
                if (IsSecretKey(key))
                {
                    droppedOther = true;
                    continue;
                }
                kept.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
            }
        }
        return (string.Join("&", kept), password, droppedOther);
    }

    /// <summary>query 中是否仍带口令键（值不返回）。</summary>
    public static bool ContainsSecret(string? query)
    {
        if (string.IsNullOrEmpty(query)) return false;
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            var key = eq < 0 ? part : part[..eq];
            if (IsSecretKey(Uri.UnescapeDataString(key))) return true;
        }
        return false;
    }
}
