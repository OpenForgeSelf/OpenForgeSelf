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
/// 规则与 mcp-center 公共工具的（2026-10-28 归属迁移前在 AIAgent）
/// <c>Plugins/McpCenter/Services/PublicTools/TerminalCommandGuard.cs</c> **逐条同值**：
/// 引号感知分段（PILOT-075，2026-10-29 用户拍板，与 mcp-center 同步）：引号外 | ; &amp;&amp; || 换行 = 分隔符，
/// 逐段过同一套门；重定向（&lt; &gt; 反引号）与单独 &amp;、空段、引号不配对整串拒 → 可执行名必须裸名 →
/// 白名单（dotnet/pnpm/node/git/ssh/pwsh，拒绝模式）→ 破坏性 token 精确匹配 + 内联代码词边界扫描 +
/// -EncodedCommand base64（UTF-16LE）解码后扫描，不可验证即拒。
///
/// 为什么不直接复用那份：它带 <c>XTrace.Log</c> 且命名空间在 mcp-center 插件内（迁移前在 AIAgent），而
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

        // PILOT-075（2026-10-29 用户拍板，与 mcp-center 逐行同构）：换行/管道/链式是分隔符而非拒绝理由；
        // 整串级检查收敛进分段器，重定向、反引号、单独 &、空段、引号不配对仍整串拒绝。
        if (!TrySplitSegments(command, out var segments, out var splitError))
        {
            return GuardDecision.Reject(splitError!);
        }

        // 逐段同一校验：每段独立过与单命令完全相同的门；段数 > 1 加 segment 前缀，= 1 原因逐字节不变。
        for (var i = 0; i < segments.Count; i++)
        {
            var single = CheckSingleCommand(segments[i]);
            if (!single.Allowed)
            {
                return GuardDecision.Reject(
                    segments.Count == 1 ? single.Reason! : $"Error: segment {i + 1}/{segments.Count}: {single.Reason}");
            }
        }

        return GuardDecision.Allow();
    }

    /// <summary>
    /// 引号感知分段（PILOT-075 FR-1，与 mcp-center <c>TerminalCommandGuard.TrySplitSegments</c> 逐行同构）：
    /// 仅在引号外把 <c>&amp;&amp;</c> <c>||</c> <c>|</c> <c>;</c> 换行当分隔符；段内原文保留（含引号）。
    /// 返回 false 时 <paramref name="error"/> 为整串级拒绝原因，<paramref name="segments"/> 为空。
    /// </summary>
    public static bool TrySplitSegments(string? command, out IReadOnlyList<string> segments, out string? error)
    {
        segments = Array.Empty<string>();
        error = null;
        if (string.IsNullOrWhiteSpace(command))
        {
            error = "Error: command is empty";
            return false;
        }

        var result = new List<string>();
        var sb = new System.Text.StringBuilder();
        var inDouble = false;
        var inSingle = false;

        string? FlushSegment()
        {
            var seg = sb.ToString().Trim();
            sb.Clear();
            if (seg.Length == 0)
            {
                return "Error: chained/piped commands are not allowed (empty segment)";
            }
            result.Add(seg);
            return null;
        }

        var text = command!;
        var i = 0;
        string? flushError;
        while (i < text.Length)
        {
            var ch = text[i];
            if (inDouble || inSingle)
            {
                // 引号内：只有配对的关闭引号才退出引号态。
                if ((inDouble && ch == '"') || (inSingle && ch == '\''))
                {
                    inDouble = false;
                    inSingle = false;
                }
                sb.Append(ch);
                i++;
                continue;
            }

            switch (ch)
            {
                case '"':
                    inDouble = true;
                    sb.Append(ch);
                    i++;
                    break;
                case '\'':
                    inSingle = true;
                    sb.Append(ch);
                    i++;
                    break;
                case '<' or '>' or '`':
                    error = $"Error: chained/piped commands are not allowed (contains '{ch}')";
                    return false;
                case '&':
                    if (i + 1 < text.Length && text[i + 1] == '&')
                    {
                        if ((flushError = FlushSegment()) != null)
                        {
                            error = flushError;
                            return false;
                        }
                        i += 2;
                    }
                    else
                    {
                        error = "Error: chained/piped commands are not allowed (contains '&')";
                        return false;
                    }
                    break;
                case '|':
                    if ((flushError = FlushSegment()) != null)
                    {
                        error = flushError;
                        return false;
                    }
                    i += i + 1 < text.Length && text[i + 1] == '|' ? 2 : 1;
                    break;
                case ';':
                    if ((flushError = FlushSegment()) != null)
                    {
                        error = flushError;
                        return false;
                    }
                    i++;
                    break;
                case '\n':
                    if ((flushError = FlushSegment()) != null)
                    {
                        error = flushError;
                        return false;
                    }
                    i++;
                    break;
                case '\r':
                    if ((flushError = FlushSegment()) != null)
                    {
                        error = flushError;
                        return false;
                    }
                    i += i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
                    break;
                default:
                    sb.Append(ch);
                    i++;
                    break;
            }
        }

        if (inDouble || inSingle)
        {
            error = "Error: unbalanced quotes in command";
            return false;
        }

        if ((flushError = FlushSegment()) != null)
        {
            error = flushError;
            return false;
        }

        segments = result;
        return true;
    }

    /// <summary>
    /// 单段命令校验（= 2026-10-29 之前 CheckCore 去掉换行/分隔符整串检查后的原样逻辑，判定逐条不变）。
    /// </summary>
    private static GuardDecision CheckSingleCommand(string command)
    {
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
