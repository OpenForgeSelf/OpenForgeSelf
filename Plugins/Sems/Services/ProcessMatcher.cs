using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Plugins.Sems.Services;

/// <summary>
/// 本机进程归属判定纯函数（design §5.3）。
/// 给定一条进程的 ExecutablePath / CommandLine，按「路径前缀命中 + 类型二次确认」规则
/// 判定其归属的已登记项目。抽为纯函数便于表驱动单测。
/// </summary>
public static class ProcessMatcher
{
    // 从 CommandLine 提取绝对路径（含引号/空格分隔的绝对路径；Windows 盘符形如 C:\ 或 C:/）
    private static readonly Regex AbsolutePathRegex =
        new(@"[""']?([A-Za-z]:[\\/][^""'\s]+)[""']?", RegexOptions.Compiled);

    // Unix 绝对路径。至少两段，避免把 `cmd /c` 这类开关当成路径。
    private static readonly Regex UnixAbsolutePathRegex =
        new(@"(?<![A-Za-z0-9])(/(?:[^/\s""']+/)+[^/\s""']*)", RegexOptions.Compiled);

    /// <summary>
    /// 判定进程归属的项目。无命中返回 null。
    /// 步骤：
    /// 1. 候选路径 = { exePath, cwd } ∪ CommandLine 中提取的绝对路径（归一化）；
    /// 2. 任一候选路径以某项目 Root（归一化）为前缀且**带目录分隔符边界** → 命中该项目；
    /// 3. 类型二次确认：Type 为空直接采信；否则要求进程名/路径特征与 Type 相容。
    /// </summary>
    public static ProjectInfo? MatchProject(
        string? exePath,
        string? commandLine,
        IReadOnlyList<ProjectInfo> projects,
        string? cwd = null)
    {
        if (projects == null || projects.Count == 0) return null;

        var candidates = CollectCandidatePaths(exePath, commandLine, cwd);
        if (candidates.Count == 0) return null;

        foreach (var project in projects)
        {
            if (string.IsNullOrWhiteSpace(project.Root)) continue;
            var rootNorm = Normalize(project.Root);
            if (candidates.Any(c => IsUnderRoot(c, rootNorm)))
            {
                // 类型二次确认；通过则命中，否则继续看下一个项目（宁漏勿误）
                if (TypeCompatible(project.Type, exePath, commandLine))
                    return project;
            }
        }
        return null;
    }

    /// <summary>
    /// 进程是否落在任一已登记项目目录下，不管类型是否相容。
    /// 类型对不上的进程仍要出现在运行实况（projectId 为空），但不把项目目录之外的进程算进来。
    /// </summary>
    public static bool LocatedUnderRegisteredRoot(
        string? exePath,
        string? commandLine,
        IReadOnlyList<ProjectInfo> projects,
        string? cwd = null)
    {
        if (projects == null || projects.Count == 0) return false;
        var candidates = CollectCandidatePaths(exePath, commandLine, cwd);
        if (candidates.Count == 0) return false;
        foreach (var project in projects)
        {
            if (string.IsNullOrWhiteSpace(project.Root)) continue;
            var rootNorm = Normalize(project.Root);
            if (candidates.Any(c => IsUnderRoot(c, rootNorm))) return true;
        }
        return false;
    }

    /// <summary>候选路径集合（归一化、去重、非空）。</summary>
    private static List<string> CollectCandidatePaths(string? exePath, string? commandLine, string? cwd)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            var n = Normalize(raw);
            if (!string.IsNullOrEmpty(n)) set.Add(n);
        }

        Add(exePath);
        Add(cwd);
        if (!string.IsNullOrWhiteSpace(commandLine))
        {
            foreach (Match m in AbsolutePathRegex.Matches(commandLine))
                Add(m.Groups[1].Value);
            foreach (Match m in UnixAbsolutePathRegex.Matches(commandLine))
                Add(m.Groups[1].Value);
        }
        return set.ToList();
    }

    /// <summary>
    /// 归一化：目录分隔符统一为 \；去除尾随分隔符（比较前缀时 Root 不带尾分隔符，候选也不带）。
    /// 不转小写——比较在 IsUnderRoot 内统一转小写。
    /// </summary>
    private static string Normalize(string path)
    {
        var p = path.Trim().Replace('/', '\\').TrimEnd('\\');
        return p;
    }

    /// <summary>
    /// 候选路径 candidate 是否位于 root（均不含尾分隔符）之下：
    /// 忽略大小写；要求 candidate == root 或 candidate 以 root + 目录分隔符为前缀（边界保护，
    /// 避免 D:\foo 误匹配 D:\foobar）。
    /// </summary>
    private static bool IsUnderRoot(string candidate, string root)
    {
        if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(root)) return false;
        if (candidate.Length < root.Length) return false;

        if (candidate.Length == root.Length)
            return candidate.Equals(root, StringComparison.OrdinalIgnoreCase);

        // candidate 比 root 长：要求 root 之后紧跟一个目录分隔符
        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return false;
        return candidate[root.Length] == '\\';
    }

    /// <summary>
    /// 进程名/路径特征是否与项目 Type 相容。
    /// Type 为空或无法识别 → 直接采信路径命中（宁漏勿误，未知类型不误杀）。
    /// </summary>
    private static bool TypeCompatible(string type, string? exePath, string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(type)) return true;

        var procName = ExtractProcessName(exePath);
        var all = $"{exePath} {commandLine}".ToLowerInvariant();

        return type.Trim().ToLowerInvariant() switch
        {
            "frontend" => MatchesAny(procName, all, new[] { "node", "npm", "yarn", "pnpm", "vite", "ng", "bun" }),
            "backend" => MatchesAny(procName, all, new[] { "dotnet", "java", "python", "py", "uvicorn", "gunicorn", "go" }),
            "fullstack" => MatchesAny(procName, all, new[] { "node", "npm", "yarn", "pnpm", "vite", "ng", "bun", "dotnet", "java", "python", "py", "uvicorn", "gunicorn", "go" }),
            // 其他类型（library/tool/other/自定义）无法识别特征 → 采信路径命中
            _ => true
        };
    }

    private static bool MatchesAny(string procName, string allLower, string[] keywords)
    {
        if (keywords.Any(k => procName.StartsWith(k, StringComparison.OrdinalIgnoreCase)))
            return true;
        return keywords.Any(k => allLower.Contains(k));
    }

    private static string ExtractProcessName(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath)) return string.Empty;
        try
        {
            var name = Path.GetFileNameWithoutExtension(exePath);
            return name ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static readonly Regex TokenRegex =
        new(@"[""']([^""']+)[""']|(\S+)", RegexOptions.Compiled);

    /// <summary>
    /// 按已登记命令的脚本匹配本机进程。对得上则返回该命令（多条命中时取更具体的脚本），对不上返回 null。
    /// 不写入命令档案，只做会话回填。
    /// </summary>
    public static RunCommandInfo? MatchCommand(
        string? exePath,
        string? commandLine,
        IReadOnlyList<RunCommandInfo> commands)
    {
        if (commands == null || commands.Count == 0) return null;

        var haystack = $"{exePath} {commandLine}".Trim();
        if (string.IsNullOrWhiteSpace(haystack)) return null;
        var haystackNorm = haystack.Replace('\\', '/');
        var hayTokens = Tokenize(haystackNorm);
        if (hayTokens.Count == 0) return null;

        RunCommandInfo? best = null;
        var bestScore = 0;
        foreach (var command in commands)
        {
            var script = command.Script?.Trim();
            if (string.IsNullOrWhiteSpace(script)) continue;
            var score = ScoreScript(haystackNorm, hayTokens, script);
            if (score > bestScore)
            {
                bestScore = score;
                best = command;
            }
        }

        return best;
    }

    private static int ScoreScript(string haystackNorm, IReadOnlyList<string> hayTokens, string script)
    {
        var scriptNorm = script.Trim().Replace('\\', '/');
        if (scriptNorm.Length == 0) return 0;

        if (haystackNorm.IndexOf(scriptNorm, StringComparison.OrdinalIgnoreCase) >= 0)
            return 1000 + scriptNorm.Length;

        var scriptTokens = Tokenize(scriptNorm);
        if (scriptTokens.Count == 0) return 0;
        if (scriptTokens.Any(t => !TokenPresent(t, hayTokens))) return 0;
        return scriptTokens.Sum(t => t.Length);
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        foreach (Match m in TokenRegex.Matches(text))
        {
            var raw = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            if (!string.IsNullOrWhiteSpace(raw))
                tokens.Add(raw.Trim());
        }
        return tokens;
    }

    private static bool TokenPresent(string token, IReadOnlyList<string> hayTokens)
    {
        foreach (var hay in hayTokens)
        {
            if (hay.Equals(token, StringComparison.OrdinalIgnoreCase)) return true;

            string file;
            string stem;
            try
            {
                var unified = hay.Replace('\\', '/');
                file = Path.GetFileName(unified);
                stem = Path.GetFileNameWithoutExtension(file);
            }
            catch
            {
                continue;
            }

            if (file.Equals(token, StringComparison.OrdinalIgnoreCase)
                || stem.Equals(token, StringComparison.OrdinalIgnoreCase)
                || token.Equals(file, StringComparison.OrdinalIgnoreCase)
                || token.Equals(stem, StringComparison.OrdinalIgnoreCase)
                || stem.StartsWith(token, StringComparison.OrdinalIgnoreCase) && token.Length >= 3)
                return true;

            // npm-cli.js 路径中的 npm 段等
            foreach (var segment in hay.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (segment.Equals(token, StringComparison.OrdinalIgnoreCase)
                    || Path.GetFileNameWithoutExtension(segment)
                        .Equals(token, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // python / python3 / python3.12 视为同一解释器
            if (IsPythonToken(token) && IsPythonToken(stem)) return true;
        }

        // 脚本里的路径只比文件名
        try
        {
            var tokenFile = Path.GetFileName(token.Replace('\\', '/'));
            var tokenStem = Path.GetFileNameWithoutExtension(tokenFile);
            if (!string.IsNullOrEmpty(tokenFile) && tokenFile != token)
            {
                foreach (var hay in hayTokens)
                {
                    var unified = hay.Replace('\\', '/');
                    var file = Path.GetFileName(unified);
                    if (file.Equals(tokenFile, StringComparison.OrdinalIgnoreCase)
                        || Path.GetFileNameWithoutExtension(file)
                            .Equals(tokenStem, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
        }
        catch
        {
            // 非路径 token，忽略
        }

        return false;
    }

    private static bool IsPythonToken(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return name.StartsWith("python", StringComparison.OrdinalIgnoreCase)
            || name.Equals("py", StringComparison.OrdinalIgnoreCase);
    }
}
