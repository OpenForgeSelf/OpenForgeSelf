using System.Text.Json;
using System.Text.RegularExpressions;
using ForgeSelf.Api.Plugins.SmartTerminal.Models;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.SmartTerminal.Services;

/// <summary>
/// 命令风险分类器 + 权限模式决策。
/// 行业对齐：
/// - VS Code Agent Approvals / autoApprove 规则：按模式决定哪些可自动、哪些需批；
/// - Open Interpreter：sandbox 与 approvals 分离——本类只做审批决策，执行另层；
/// - mcp-center TerminalCommandGuard：破坏性 token / 管道到 shell / EncodedCommand 思路。
/// </summary>
public interface IPermissionClassifier
{
    PermissionMode CurrentMode { get; }
    void SetMode(PermissionMode mode);
    PermissionClassifyResult Classify(string command, PermissionMode? modeOverride = null);
    void ReloadRules();
}

public sealed class PermissionClassifier : IPermissionClassifier
{
    private readonly object _lock = new();
    private readonly string _rulesPath;
    private RuleSet _rules;
    private PermissionMode _mode = PermissionMode.AskWrite;

    public PermissionMode CurrentMode
    {
        get { lock (_lock) return _mode; }
    }

    public PermissionClassifier(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _rulesPath = Path.Combine(dataDirectory, "permission-rules.json");
        EnsureRulesFile();
        _rules = LoadRules(_rulesPath);
    }

    public void SetMode(PermissionMode mode)
    {
        lock (_lock) _mode = mode;
    }

    public void ReloadRules()
    {
        lock (_lock) _rules = LoadRules(_rulesPath);
    }

    public PermissionClassifyResult Classify(string command, PermissionMode? modeOverride = null)
    {
        var mode = modeOverride ?? CurrentMode;
        var text = command?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return new PermissionClassifyResult
            {
                Risk = CommandRiskClass.Deny,
                Decision = ApprovalDecision.Deny,
                MatchedRule = "empty",
                Reason = "命令为空"
            };
        }

        RuleSet rules;
        lock (_lock) rules = _rules;

        var tokens = Tokenize(text).Select(NormalizeToken).Where(t => t.Length > 0).ToList();
        var first = tokens.FirstOrDefault() ?? string.Empty;

        // 1) Hard deny（任何模式都拒绝，对齐「Auto 除硬 denylist 外自动」）
        if (HitAny(tokens, rules.HardDeny) || IsHardDenyPattern(text))
        {
            var hit = FirstHit(tokens, rules.HardDeny) ?? "hard-deny-pattern";
            return Decide(CommandRiskClass.Deny, mode, $"hardDeny:{hit}", "命中硬拒绝名单");
        }

        // 2) High-risk patterns (curl|sh, eval, EncodedCommand, sudo…)
        var patternHit = FirstPatternHit(text, rules.HighRiskCompiled);
        if (patternHit != null)
        {
            return Decide(CommandRiskClass.HighRisk, mode, $"highRiskPattern:{patternHit}", "命中高危正则");
        }

        if (HitAny(tokens, rules.HighRisk))
        {
            var hit = FirstHit(tokens, rules.HighRisk)!;
            return Decide(CommandRiskClass.HighRisk, mode, $"highRisk:{hit}", "命中高危命令字");
        }

        // 3) Write / mutating
        var writePattern = FirstPatternHit(text, rules.WriteCompiled);
        if (writePattern != null)
        {
            return Decide(CommandRiskClass.Write, mode, $"writePattern:{writePattern}", "命中写/副作用模式");
        }

        // git/npm 等：首 token 在 writeMutating，但子命令可能只读（status/log/list）
        if (IsMutatingInvocation(tokens, rules))
        {
            var hit = first;
            return Decide(CommandRiskClass.Write, mode, $"writeMutating:{hit}", "命中写/变更类调用");
        }

        // 4) Read / unknown → 未知按 Write（fail-safe 偏 AskWrite）
        if (HitAny(tokens, rules.ReadAllow) || IsLikelyRead(tokens))
        {
            return Decide(CommandRiskClass.Read, mode, $"readAllow:{first}", "判定为只读/探测类");
        }

        return Decide(CommandRiskClass.Write, mode, "unknown:fail-safe-write", "未识别命令按写/副作用处理（fail-safe）");
    }

    private static PermissionClassifyResult Decide(CommandRiskClass risk, PermissionMode mode, string rule, string reason)
    {
        var decision = risk switch
        {
            CommandRiskClass.Deny => ApprovalDecision.Deny,
            CommandRiskClass.HighRisk => mode switch
            {
                PermissionMode.Auto => ApprovalDecision.Allow, // Auto 仍放过高危，但 hard deny 已在上层拦
                _ => ApprovalDecision.Ask
            },
            CommandRiskClass.Write => mode switch
            {
                PermissionMode.AskWrite => ApprovalDecision.Ask,
                PermissionMode.Auto => ApprovalDecision.Allow,
                PermissionMode.AskHighRisk => ApprovalDecision.Allow,
                _ => ApprovalDecision.Ask
            },
            CommandRiskClass.Read => ApprovalDecision.Allow,
            _ => ApprovalDecision.Ask
        };

        // Auto 模式对 HighRisk：用户要求「自动执行，除硬 denylist」→ Allow
        if (mode == PermissionMode.Auto && risk == CommandRiskClass.HighRisk)
        {
            decision = ApprovalDecision.Allow;
        }

        return new PermissionClassifyResult
        {
            Risk = risk,
            Decision = decision,
            MatchedRule = rule,
            Reason = $"{reason}；模式={mode} → {decision}"
        };
    }

    private static bool IsMutatingInvocation(List<string> tokens, RuleSet rules)
    {
        if (tokens.Count == 0) return false;
        var exe = tokens[0];
        if (!rules.WriteMutating.Contains(exe)) return false;

        // 常见只读子命令
        var readSubs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "status", "log", "show", "diff", "branch", "tag", "remote", "stash", "version", "-v", "--version",
            "list", "ls", "info", "help", "-h", "--help", "search", "outdated", "why", "view",
            "test", "build", "restore", "run", "exec", "tool"
        };

        if (tokens.Count == 1) return false; // 裸 git/dotnet 多半打印帮助，当读
        var sub = tokens[1];
        if (readSubs.Contains(sub)) return false;

        // echo/printf/set 有赋值或重定向才算写——此处分词粗判：带 = 或后续路径
        if (exe is "echo" or "printf" or "set" or "cat")
        {
            return tokens.Any(t => t.Contains('=', StringComparison.Ordinal) || t is ">" or ">>");
        }

        return true;
    }

    private static bool IsLikelyRead(List<string> tokens)
    {
        if (tokens.Count == 0) return false;
        var exe = tokens[0];
        return exe is "cd" or "ls" or "dir" or "pwd" or "type" or "cat" or "echo" or "whoami" or "hostname";
    }

    private static bool IsHardDenyPattern(string text) =>
        Regex.IsMatch(text, @"(?i)\b(format|diskpart|bcdedit)\b", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private void EnsureRulesFile()
    {
        if (File.Exists(_rulesPath)) return;
        var bundled = Path.Combine(AppContext.BaseDirectory, "Data", "permission-rules.default.json");
        if (!File.Exists(bundled))
        {
            // 插件 ALC 下可能相对插件目录
            bundled = Path.Combine(Path.GetDirectoryName(typeof(PermissionClassifier).Assembly.Location) ?? ".", "Data", "permission-rules.default.json");
        }
        if (File.Exists(bundled))
        {
            File.Copy(bundled, _rulesPath, overwrite: false);
            return;
        }
        File.WriteAllText(_rulesPath, """{"version":1,"hardDeny":["format","diskpart"],"highRisk":["rm","del","curl","wget","kill","sudo"],"writeMutating":["git","npm","pnpm","set-content"],"readAllow":["ls","dir","pwd","git"],"highRiskPatterns":["(?i)\\beval\\b"],"writePatterns":["(?i)>\\s*\\S+"]}""");
        XTrace.Log.Warn("[SmartTerminal] 未找到 bundled 默认规则，已写入最小 permission-rules.json");
    }

    private static RuleSet LoadRules(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            var dto = JsonSerializer.Deserialize<RuleSetDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                      ?? new RuleSetDto();
            return RuleSet.FromDto(dto);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[SmartTerminal] 加载权限规则失败，回落内置最小集: {0}", ex.Message);
            return RuleSet.Fallback();
        }
    }

    private static bool HitAny(IEnumerable<string> tokens, HashSet<string> set) => tokens.Any(set.Contains);
    private static string? FirstHit(IEnumerable<string> tokens, HashSet<string> set) => tokens.FirstOrDefault(set.Contains);
    private static bool HitPatterns(string text, IReadOnlyList<Regex> patterns) => FirstPatternHit(text, patterns) != null;

    private static string? FirstPatternHit(string text, IReadOnlyList<Regex> patterns)
    {
        foreach (var rx in patterns)
        {
            try
            {
                var m = rx.Match(text);
                if (m.Success) return rx.ToString();
            }
            catch (RegexMatchTimeoutException)
            {
                // ignore
            }
        }
        return null;
    }

    private static string NormalizeToken(string token)
    {
        var t = token.Trim().Trim('"', '\'').ToLowerInvariant();
        if (t.EndsWith(".exe", StringComparison.Ordinal)) t = t[..^4];
        return t;
    }

    private static IEnumerable<string> Tokenize(string command)
    {
        var sb = new System.Text.StringBuilder();
        var inQuote = false;
        foreach (var ch in command)
        {
            if (ch is '"' or '\'')
            {
                inQuote = !inQuote;
                continue;
            }
            if (!inQuote && char.IsWhiteSpace(ch))
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
        if (sb.Length > 0) yield return sb.ToString();
    }

    private sealed class RuleSetDto
    {
        public List<string> HardDeny { get; set; } = new();
        public List<string> HighRisk { get; set; } = new();
        public List<string> WriteMutating { get; set; } = new();
        public List<string> ReadAllow { get; set; } = new();
        public List<string> HighRiskPatterns { get; set; } = new();
        public List<string> WritePatterns { get; set; } = new();
    }

    private sealed class RuleSet
    {
        public HashSet<string> HardDeny { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> HighRisk { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> WriteMutating { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ReadAllow { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyList<Regex> HighRiskCompiled { get; init; } = Array.Empty<Regex>();
        public IReadOnlyList<Regex> WriteCompiled { get; init; } = Array.Empty<Regex>();

        public static RuleSet FromDto(RuleSetDto dto) => new()
        {
            HardDeny = new HashSet<string>(dto.HardDeny ?? [], StringComparer.OrdinalIgnoreCase),
            HighRisk = new HashSet<string>(dto.HighRisk ?? [], StringComparer.OrdinalIgnoreCase),
            WriteMutating = new HashSet<string>(dto.WriteMutating ?? [], StringComparer.OrdinalIgnoreCase),
            ReadAllow = new HashSet<string>(dto.ReadAllow ?? [], StringComparer.OrdinalIgnoreCase),
            HighRiskCompiled = CompileAll(dto.HighRiskPatterns),
            WriteCompiled = CompileAll(dto.WritePatterns)
        };

        public static RuleSet Fallback() => FromDto(new RuleSetDto
        {
            HardDeny = ["format", "diskpart", "bcdedit"],
            HighRisk = ["rm", "del", "curl", "wget", "kill", "sudo", "iex"],
            WriteMutating = ["git", "npm", "pnpm", "set-content", "out-file"],
            ReadAllow = ["ls", "dir", "pwd", "git", "type", "cat"],
            HighRiskPatterns = [@"(?i)\beval\b", @"(?i)(curl|wget).*\|\s*(sh|bash|pwsh)"],
            WritePatterns = [@"(?i)>\s*\S+"]
        });

        private static List<Regex> CompileAll(List<string>? patterns)
        {
            var list = new List<Regex>();
            foreach (var p in patterns ?? [])
            {
                try
                {
                    list.Add(new Regex(p, RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)));
                }
                catch (Exception ex)
                {
                    XTrace.Log.Warn("[SmartTerminal] 忽略非法正则 {0}: {1}", p, ex.Message);
                }
            }
            return list;
        }
    }
}
