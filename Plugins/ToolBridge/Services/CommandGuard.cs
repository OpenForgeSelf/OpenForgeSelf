using System.Text.RegularExpressions;

namespace ForgeSelf.Api.Plugins.ToolBridge.Services;

/// <summary>命令守卫判定结果（纯数据，供单测与对账断言）。</summary>
public sealed class GuardDecision
{
    public bool Allowed { get; init; }

    /// <summary>拒绝原因（Allowed=true 时为 null）；拒绝路径不得启动任何子进程。</summary>
    public string? Reason { get; init; }

    public static GuardDecision Allow() => new() { Allowed = true };

    public static GuardDecision Reject(string reason) => new() { Allowed = false, Reason = reason };
}

/// <summary>
/// 命令执行安全门（PILOT-053 03-plan 决策 D1）。
///
/// 规则与内置 agent 的 <c>Plugins/AIAgent/Services/TerminalCommandGuard.cs</c> **逐条同值**：
/// 换行即拒 → 管道/链式/重定向字符（| ; &amp; &lt; &gt; 反引号）即拒 → 可执行名必须裸名 →
/// 白名单（dotnet/pnpm/node/git/ssh/pwsh，拒绝模式）→ 破坏性 token 精确匹配 + 内联代码词边界扫描 +
/// -EncodedCommand base64（UTF-16LE）解码后扫描，不可验证即拒。
///
/// 为什么不直接复用那份：它带 <c>XTrace.Log</c> 且命名空间在 AIAgent 插件内，而
/// <c>ForgeSelf.Core</c> / <c>ForgeSelf.Abstractions</c> 两个共享层**均未引用 NewLife.Core**
/// （实读两份 csproj），上移即等于给内核层加依赖 = 依赖结构变更（规范 §1.3 高风险，须另立批次出 ADR）。
/// 漂移风险由常驻判据 <c>ForgeSelf.Api.Tests/Plugins/ToolBridgeTests/ToolBridgeGuardParityTests.cs</c>
/// 用同一张命令金样表对账钉死：两份实现判定或原因不同即红。
///
/// ⚠ 本任务**不放宽任何一条**（02-spec BR-4）；白名单不可配置是既有事实（AIAgent 侧同样写死常量）。
/// </summary>
public static class CommandGuard
{
    /// <summary>单段输出截断上限（bytes）：50KB，防 prompt 爆炸。</summary>
    public const int MaxOutputBytes = 50 * 1024;

    /// <summary>超时上限（秒），与内置 agent 对齐。</summary>
    public const int MaxTimeoutSeconds = 30;

    /// <summary>可执行名白名单（默认 allowlist 拒绝模式；首个 token 必须命中，裸名或裸名.exe）。</summary>
    public static readonly string[] DefaultAllowlist = { "dotnet", "pnpm", "node", "git", "ssh", "pwsh" };

    /// <summary>破坏性命令字（删除/格式化/下载/提权/系统操作；全命令 token 精确匹配，防 pwsh -Command 内嵌绕过）。</summary>
    private static readonly HashSet<string> DestructiveTokens = new(StringComparer.Ordinal)
    {
        // 删除类
        "rm", "rmdir", "rd", "del", "erase", "deltree", "remove-item", "remove-itemproperty",
        "clear-content", "shred", "unlink",
        // 格式化/磁盘类
        "format", "format-volume", "diskpart", "fdisk", "mkfs", "dd",
        // 下载/远程内容类
        "curl", "wget", "iwr", "irm", "invoke-webrequest", "invoke-restmethod", "certutil", "bitsadmin",
        // 任意代码执行类
        "invoke-expression", "iex", "invoke-command", "start-process", "start-job", "invoke-item",
        "mshta", "rundll32", "wscript", "cscript",
        // 系统/提权/破坏操作类
        "shutdown", "restart-computer", "stop-computer", "taskkill", "stop-process", "kill",
        "stop-service", "set-service", "netsh", "reg", "regedit", "regsvr32", "sc",
        "set-executionpolicy", "set-acl", "icacls", "takeown", "cacls",
        "bcdedit", "cipher", "wevtutil", "vssadmin", "schtasks"
    };

    /// <summary>内联代码破坏词扫描（词边界版，长词优先，compiled + 1s 超时防灾难回溯）。</summary>
    private static readonly Regex InlineDestructiveRegex = BuildInlineRegex();

    private static Regex BuildInlineRegex()
    {
        var ordered = DestructiveTokens
            .OrderByDescending(t => t.Length)
            .Select(Regex.Escape);
        var pattern = @"\b(?:" + string.Join("|", ordered) + @")\b";
        return new Regex(pattern,
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
    }

    public static GuardDecision Check(string? command)
    {
        var decision = CheckCore(command);
        if (!decision.Allowed)
        {
            NewLife.Log.XTrace.Log.Warn("[tool-bridge] CommandGuard 拒绝：{0}（command={1}）", decision.Reason, command);
        }
        return decision;
    }

    private static GuardDecision CheckCore(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return GuardDecision.Reject("Error: command is empty");
        }

        // 换行即拒：多行 = 隐式链式。
        if (command.Contains('\n') || command.Contains('\r'))
        {
            return GuardDecision.Reject("Error: chained/piped commands are not allowed (newline)");
        }

        // 管道/链式/重定向拒绝。
        foreach (var ch in command)
        {
            if (ch is '|' or ';' or '&' or '<' or '>' or '`')
            {
                return GuardDecision.Reject(
                    $"Error: chained/piped commands are not allowed (contains '{ch}')");
            }
        }

        var (exe, _) = SplitExecutable(command);
        if (string.IsNullOrEmpty(exe))
        {
            return GuardDecision.Reject("Error: cannot parse executable name from command");
        }
        if (exe.Contains('/') || exe.Contains('\\'))
        {
            return GuardDecision.Reject(
                $"Error: executable must be a bare name resolved via PATH (got '{exe}')");
        }

        var normalized = NormalizeToken(exe);
        if (!DefaultAllowlist.Contains(normalized))
        {
            var allowlist = string.Join("/", DefaultAllowlist);
            return GuardDecision.Reject(
                $"Error: command not allowed by terminal allowlist: {normalized}. " +
                $"（默认白名单拒绝模式，仅放行 {allowlist}；如需扩展请经 Agent 配置调整）");
        }

        var tokens = Tokenize(command).ToList();
        var prevInlineCode = false;
        var prevEncodedCommand = false;
        var prevValueFlag = false;
        for (var i = 1; i < tokens.Count; i++)
        {
            var raw = tokens[i];
            var norm = NormalizeToken(raw);

            if (prevEncodedCommand)
            {
                prevEncodedCommand = false;
                var encodedHit = CheckEncodedPayload(raw);
                if (encodedHit != null)
                {
                    return GuardDecision.Reject(
                        $"Error: destructive command not allowed by safety policy: {encodedHit} " +
                        "（删除/格式化/下载/任意执行类命令被拒绝；-EncodedCommand 载荷解码扫描命中红线）");
                }
                continue;
            }

            if (prevInlineCode)
            {
                prevInlineCode = false;
                var match = InlineDestructiveRegex.Match(norm);
                if (match.Success)
                {
                    return GuardDecision.Reject(
                        $"Error: destructive command not allowed by safety policy: {match.Value} " +
                        "（删除/格式化/下载/任意执行类命令被拒绝）");
                }
                continue;
            }

            if (prevValueFlag)
            {
                prevValueFlag = false;
                continue;
            }

            if (raw.StartsWith('-'))
            {
                if (norm is "-e" or "-c" or "-command" or "-encodedcommand" or "/c" or "/k")
                {
                    prevEncodedCommand = norm is "-encodedcommand";
                    prevInlineCode = !prevEncodedCommand;
                }
                else
                {
                    prevValueFlag = true;
                }
                continue;
            }

            if (DestructiveTokens.Contains(norm))
            {
                return GuardDecision.Reject(
                    $"Error: destructive command not allowed by safety policy: {raw} " +
                    "（删除/格式化/下载/任意执行类命令被拒绝）");
            }
        }

        return GuardDecision.Allow();
    }

    /// <summary>解码 pwsh -EncodedCommand 的 base64 载荷并做词边界扫描；返回命中词或 null。</summary>
    private static string? CheckEncodedPayload(string base64)
    {
        try
        {
            var bytes = Convert.FromBase64String(base64.Trim());
            if (bytes.Length == 0 || bytes.Length % 2 != 0)
            {
                return "unverifiable-encoded-payload";
            }
            var decoded = System.Text.Encoding.Unicode.GetString(bytes);
            var match = InlineDestructiveRegex.Match(decoded);
            return match.Success ? match.Value : null;
        }
        catch (FormatException)
        {
            return "unverifiable-encoded-payload";
        }
    }

    /// <summary>解析命令首 token（引号感知）：返回（裸可执行名，剩余原始串）。</summary>
    public static (string exe, string rest) SplitExecutable(string command)
    {
        var tokens = Tokenize(command ?? string.Empty).ToList();
        if (tokens.Count == 0)
        {
            return (string.Empty, string.Empty);
        }

        var exe = tokens[0].Trim().Trim('"', '\'').Trim();
        var firstIndex = IndexOfFirstTokenEnd(command!);
        var rest = firstIndex < command!.Length ? command[firstIndex..].TrimStart() : string.Empty;
        return (exe, rest);
    }

    /// <summary>输出截断（UTF-8 字节按 MaxOutputBytes 保护；多字节字符不截半）。</summary>
    public static (string text, bool truncated) Truncate(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return (string.Empty, false);
        }

        if (System.Text.Encoding.UTF8.GetByteCount(text) <= MaxOutputBytes)
        {
            return (text, false);
        }

        var sb = new System.Text.StringBuilder();
        var bytes = 0;
        foreach (var ch in text)
        {
            var size = System.Text.Encoding.UTF8.GetByteCount([ch]);
            if (bytes + size > MaxOutputBytes)
            {
                break;
            }
            sb.Append(ch);
            bytes += size;
        }
        return (sb.ToString(), true);
    }

    private static string NormalizeToken(string token)
    {
        var t = token.Trim().Trim('"', '\'').ToLowerInvariant();
        if (t.EndsWith(".exe", StringComparison.Ordinal))
        {
            t = t[..^4];
        }
        return t;
    }

    private static IEnumerable<string> Tokenize(string command)
    {
        var sb = new System.Text.StringBuilder();
        var inQuote = false;
        foreach (var ch in command)
        {
            if (ch == '"')
            {
                inQuote = !inQuote;
                continue;
            }
            if (!inQuote && (ch == ' ' || ch == '\t'))
            {
                if (sb.Length > 0)
                {
                    yield return sb.ToString();
                    sb.Clear();
                }
                continue;
            }
            sb.Append(ch);
        }
        if (sb.Length > 0)
        {
            yield return sb.ToString();
        }
    }

    private static int IndexOfFirstTokenEnd(string command)
    {
        var inQuote = false;
        for (var i = 0; i < command.Length; i++)
        {
            var ch = command[i];
            if (ch == '"')
            {
                inQuote = !inQuote;
                continue;
            }
            if (!inQuote && char.IsWhiteSpace(ch))
            {
                return i;
            }
        }
        return command.Length;
    }
}
