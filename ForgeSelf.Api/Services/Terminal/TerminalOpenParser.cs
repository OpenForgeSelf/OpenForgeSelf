using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Services.Terminal;

public sealed class ParsedTerminalOpen
{
    public required string Query { get; init; }
    /// <summary>从链接取出的 SSH 口令。不得写入日志、收件箱 query 或地址栏。</summary>
    public string? Password { get; init; }
    public bool SecretStripped { get; init; }
}

/// <summary>
/// 把宿主启动参数收成与页面深链相同的 query。
/// 接受 <c>forgeself://terminal?type=ssh&amp;host=&amp;port=&amp;user=</c>
/// 和 <c>--open-terminal --type ssh --host h --user u</c>。口令键会被丢掉。
/// </summary>
public static class TerminalOpenParser
{
    private static readonly string[] ValueFlags =
    [
        "--type", "--host", "--port", "--user", "--profile", "--shell", "--title",
        "--cwd", "--auth", "--identityFile", "--step", "--session", "--password"
    ];

    public static ParsedTerminalOpen? TryParse(IReadOnlyList<string>? args)
    {
        if (args == null || args.Count == 0) return null;

        foreach (var arg in args)
        {
            if (arg.Contains("forgeself:", StringComparison.OrdinalIgnoreCase))
                return FromProtocol(arg);
        }

        for (var i = 0; i < args.Count; i++)
        {
            if (!args[i].Equals("--open-terminal", StringComparison.OrdinalIgnoreCase)) continue;
            if (i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                var next = args[i + 1];
                if (next.Contains("forgeself:", StringComparison.OrdinalIgnoreCase))
                    return FromProtocol(next);
                if (next.Contains('='))
                    return FromQuery(next.TrimStart('?'));
            }
            return FromFlags(args, i + 1);
        }

        return null;
    }

    /// <summary>从参数里拿掉协议 URL 和 <c>--open-terminal</c> 及其键，其余原样留下。</summary>
    public static string[] Strip(string[] args)
    {
        var kept = new List<string>();
        var skipping = false;
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Contains("forgeself:", StringComparison.OrdinalIgnoreCase))
            {
                skipping = false;
                continue;
            }

            if (arg.Equals("--open-terminal", StringComparison.OrdinalIgnoreCase))
            {
                skipping = true;
                continue;
            }

            if (skipping)
            {
                if (IsValueFlag(arg))
                {
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                        i++;
                    continue;
                }

                if (!arg.StartsWith("--", StringComparison.Ordinal))
                    continue;

                skipping = false;
            }

            kept.Add(arg);
        }

        return kept.ToArray();
    }

    private static ParsedTerminalOpen FromProtocol(string raw)
    {
        var idx = raw.IndexOf("forgeself:", StringComparison.OrdinalIgnoreCase);
        var url = raw[idx..].Trim().Trim('"');
        var query = "";
        var qm = url.IndexOf('?');
        if (qm >= 0)
        {
            query = url[(qm + 1)..];
            var hash = query.IndexOf('#');
            if (hash >= 0) query = query[..hash];
        }
        return FromQuery(query);
    }

    private static ParsedTerminalOpen FromFlags(IReadOnlyList<string> args, int start)
    {
        var pairs = new List<(string Key, string Value)>();
        for (var i = start; i < args.Count; i++)
        {
            if (!IsValueFlag(args[i])) break;
            if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal)) continue;
            var key = args[i][2..];
            pairs.Add((key, args[++i]));
        }

        var query = string.Join("&", pairs.Select(p =>
            $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
        return FromQuery(query);
    }

    public static ParsedTerminalOpen FromQuery(string? query)
    {
        var (clean, password, droppedOther) = TerminalOpenSecrets.Split(query);
        var kept = string.IsNullOrEmpty(clean)
            ? new List<string>()
            : clean.Split('&', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (!kept.Any(s => s.StartsWith("st=", StringComparison.OrdinalIgnoreCase)))
            kept.Insert(0, "st=1");

        return new ParsedTerminalOpen
        {
            Query = string.Join("&", kept),
            Password = password,
            SecretStripped = droppedOther
        };
    }

    private static bool IsValueFlag(string arg)
        => ValueFlags.Any(f => f.Equals(arg, StringComparison.OrdinalIgnoreCase));
}
