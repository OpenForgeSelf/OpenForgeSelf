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

    /// <summary>
    /// 判定进程归属的项目。无命中返回 null。
    /// 步骤：
    /// 1. 候选路径 = { exePath } ∪ CommandLine 中提取的绝对路径（归一化）；
    /// 2. 任一候选路径以某项目 Root（归一化）为前缀且**带目录分隔符边界** → 命中该项目；
    /// 3. 类型二次确认：Type 为空直接采信；否则要求进程名/路径特征与 Type 相容。
    /// </summary>
    public static ProjectInfo? MatchProject(string? exePath, string? commandLine, IReadOnlyList<ProjectInfo> projects)
    {
        if (projects == null || projects.Count == 0) return null;

        var candidates = CollectCandidatePaths(exePath, commandLine);
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

    /// <summary>候选路径集合（归一化、去重、非空）。</summary>
    private static List<string> CollectCandidatePaths(string? exePath, string? commandLine)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(exePath))
        {
            var n = Normalize(exePath);
            if (!string.IsNullOrEmpty(n)) set.Add(n);
        }
        if (!string.IsNullOrWhiteSpace(commandLine))
        {
            foreach (Match m in AbsolutePathRegex.Matches(commandLine))
            {
                var n = Normalize(m.Groups[1].Value);
                if (!string.IsNullOrEmpty(n)) set.Add(n);
            }
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
}
