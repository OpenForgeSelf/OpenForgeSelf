using System.Text;
using System.Text.RegularExpressions;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

/// <summary>命令执行安全门判定结果（纯数据，供单测断言）。</summary>
public sealed class TerminalGuardDecision
{
    public bool Allowed { get; init; }

    /// <summary>拒绝原因（Allowed=true 时为 null）；拒绝路径不得启动任何子进程。</summary>
    public string? Reason { get; init; }

    public static TerminalGuardDecision Allow() => new() { Allowed = true };
    public static TerminalGuardDecision Reject(string reason) => new() { Allowed = false, Reason = reason };
}

/// <summary>
/// 命令执行安全策略（031 design.md §4，纯逻辑零副作用、可单测）：
/// 第 1 门 可执行名白名单（首个 token 命中 dotnet/pnpm/node/git/ssh/pwsh）；
/// 第 2 门 管道/链式/重定向拒绝（| ; &amp; &lt; &gt; 换行/反引号——防 git status &amp;&amp; rm -rf 式拆包）；
/// 第 3 门 CWD 越界由 <see cref="IProjectWorkspaceService.ResolveSafePath"/> 承担（工具侧调用，越界即拒）；
/// 破坏性红线：精确 token 扫描 + 内联代码词边界扫描（防 pwsh -Command Remove-Item 绕过）。
/// 白名单/红线 V1 写死常量 + 预留配置扩展位（design.md 4.1 注）。
/// 2026-09-18 修复（e2e 首轮实测发现）：内联代码扫描由裸子串 Contains 改为词边界正则——
/// 裸子串把良性内容误杀（实测案例：node -e "console.log('031-terminal-alive')"，
/// 'terminal' 内含子串 'rm' 被拒）。词边界 \b 保证独立出现才命中；
/// 同步补墙：-EncodedCommand 的 base64 载荷（UTF-16LE）解码后扫描，无法解码或解码会
/// 损毁（奇数字节）即拒——否则词边界扫描读不懂密文，白名单 exe 壳下仍有任意代码通道。
/// </summary>
public static class TerminalCommandGuard
{
    /// <summary>单段输出截断上限（bytes）：50KB，防 prompt 爆炸。</summary>
    public const int MaxOutputBytes = 50 * 1024;

    /// <summary>超时上限（秒），与 FreeLoop ExecuteToolWithTimeoutAsync 的 30s 对齐。</summary>
    public const int MaxTimeoutSeconds = 30;

    /// <summary>可执行名白名单（默认 allowlist 模式；首个 token 必须命中，裸名或裸名.exe）。</summary>
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

    /// <summary>
    /// 内联代码破坏词扫描（词边界版）：\b(?:...)\b，对已归一化（小写）内容匹配。
    /// 长词优先排序（'remove-item' 先于 're...'），compiled + 1s 超时防灾难回溯。
    /// 2026-09-18：替代裸 Contains——'terminal' 中的 'rm'、'padding' 中的 'dd' 等良性子串不再误杀；
    /// 真实内联调用（exec('rm -rf /')、-c "format c:"）仍被词边界独立词命中。
    /// </summary>
    private static readonly Regex InlineDestructiveRegex = BuildInlineRegex();

    private static Regex BuildInlineRegex()
    {
        // token 为受控常量集合（小写 + 连字符），按长度倒序：长词先试（remove-itemproperty 先于 remove-item）。
        var ordered = DestructiveTokens
            .OrderByDescending(t => t.Length)
            .Select(Regex.Escape);
        var pattern = @"\b(?:" + string.Join("|", ordered) + @")\b";
        return new Regex(pattern,
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// 前两道门 + 破坏性红线（第 3 道 CWD 越界门由 RunTerminalCommandTool 经
    /// IProjectWorkspaceService.ResolveSafePath 实现——复用既有越界防护，本类不重复路径逻辑）。
    /// </summary>
    /// <param name="command">完整命令串，如 "git status"、"pwsh -File scripts/publish.ps1"。</param>
    public static TerminalGuardDecision Check(string? command)
    {
        var decision = CheckCore(command);
        if (!decision.Allowed)
        {
            XTrace.Log.Warn("[AIAgentPlugin] TerminalCommandGuard 拒绝：{0}（command={1}）", decision.Reason, command);
        }
        return decision;
    }

    private static TerminalGuardDecision CheckCore(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return TerminalGuardDecision.Reject("Error: command is empty");
        }

        // 换行续行：多行 = 隐式链式（第二道门最严格形态）。
        if (command.Contains('\n') || command.Contains('\r'))
        {
            return TerminalGuardDecision.Reject("Error: chained/piped commands are not allowed (newline)");
        }

        // 管道/链式/重定向拒绝：| ; & < > ` ——不管怎么组合，先拒再说（防 git status && rm -rf）。
        foreach (var ch in command)
        {
            if (ch is '|' or ';' or '&' or '<' or '>' or '`')
            {
                return TerminalGuardDecision.Reject(
                    $"Error: chained/piped commands are not allowed (contains '{ch}')");
            }
        }

        var (exe, _) = SplitExecutable(command);
        if (string.IsNullOrEmpty(exe))
        {
            return TerminalGuardDecision.Reject("Error: cannot parse executable name from command");
        }
        if (exe.Contains('/') || exe.Contains('\\'))
        {
            return TerminalGuardDecision.Reject(
                $"Error: executable must be a bare name resolved via PATH (got '{exe}')");
        }

        var normalized = NormalizeToken(exe);
        if (!DefaultAllowlist.Contains(normalized))
        {
            var allowlist = string.Join("/", DefaultAllowlist);
            return TerminalGuardDecision.Reject(
                $"Error: command not allowed by terminal allowlist: {normalized}. " +
                $"（默认白名单拒绝模式，仅放行 {allowlist}；如需扩展请经 Agent 配置调整）");
        }

        // 破坏性红线（V2 扫描范围）：
        // ① 内联代码 flag（-e/-c/-command/-encodedcommand//c//k）的内容做词边界扫描
        //    （拦 node -e "...rm -rf..."、pwsh -Command Remove-Item 这类内嵌绕过）；
        //    -encodedcommand 额外做 base64 解码扫描（pwsh 语义 UTF-16LE）——密文不解码=读不懂=有通道；
        //    解码失败或奇数字节（解码必损毁，可能恰好抹掉破坏词）均视为不可验证 → 拒
        //    （失效 base64 本就跑不起来，拒之无害）。
        // ② 位置参数（非 flag、非 flag 值）做精确匹配（拦 pwsh Remove-Item、dotnet format）；
        // ③ 普通 flag 的值不扫描（避免误报 dotnet test --filter Format 这类良性参数值）。
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
                    return TerminalGuardDecision.Reject(
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
                    return TerminalGuardDecision.Reject(
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
                return TerminalGuardDecision.Reject(
                    $"Error: destructive command not allowed by safety policy: {raw} " +
                    "（删除/格式化/下载/任意执行类命令被拒绝）");
            }
        }

        return TerminalGuardDecision.Allow();
    }

    /// <summary>
    /// 解码 pwsh -EncodedCommand 的 base64 载荷并做词边界扫描；返回命中词或 null。
    /// pwsh 的 EncodedCommand 语义是 UTF-16LE；解码失败或长度非偶数字节（解码必损毁）
    /// 均返回不可验证标记（调用方拒绝）——损毁解码可能恰好抹掉破坏词，不可作为放行依据。
    /// </summary>
    private static string? CheckEncodedPayload(string base64)
    {
        try
        {
            var bytes = Convert.FromBase64String(base64.Trim());
            if (bytes.Length == 0 || bytes.Length % 2 != 0)
            {
                return "unverifiable-encoded-payload";
            }
            var decoded = Encoding.Unicode.GetString(bytes);
            var match = InlineDestructiveRegex.Match(decoded);
            return match.Success ? match.Value : null;
        }
        catch (FormatException)
        {
            // 无法验证的载荷 = 无法确认无害：宁可误拒（失效 base64 进程也跑不起来）。
            return "unverifiable-encoded-payload";
        }
    }

    /// <summary>
    /// 解析命令首 token（引号感知）：返回（裸可执行名，剩余原始串）。
    /// 剩余串原样透传给 Process.Arguments——引号语义由目标可执行文件自行处理（V1 已知简化，见 design.md）。
    /// </summary>
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

        // 快路径：未超限。
        if (Encoding.UTF8.GetByteCount(text) <= MaxOutputBytes)
        {
            return (text, false);
        }

        var sb = new System.Text.StringBuilder();
        var bytes = 0;
        foreach (var ch in text)
        {
            var size = Encoding.UTF8.GetByteCount([ch]);
            if (bytes + size > MaxOutputBytes)
            {
                break;
            }
            sb.Append(ch);
            bytes += size;
        }
        return (sb.ToString(), true);
    }

    /// <summary>归一化：小写 + 剥引号 + 剥 .exe 后缀 + 剥路径前缀（token 内带分隔符的已在首 token 门拒绝）。</summary>
    private static string NormalizeToken(string token)
    {
        var t = token.Trim().Trim('"', '\'').ToLowerInvariant();
        if (t.EndsWith(".exe", StringComparison.Ordinal))
        {
            t = t[..^4];
        }
        return t;
    }

    /// <summary>空格/制表符分词（双引号内的空白保留为 token 一部分；不解析转义语义，V1 简化）。</summary>
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

    /// <summary>首 token 结束位置（供 SplitExecutable 切剩余串；与 Tokenize 的引号语义一致）。</summary>
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
