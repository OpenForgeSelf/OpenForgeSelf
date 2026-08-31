using System.IO;
using ForgeSelf.Api.Plugins.AIAgent.Models;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

/// <summary>
/// 项目技能自动识别：扫描项目根目录下两类约定目录，把其中的技能定义解析为「可用技能」。
/// - <c>.agents/skills/&lt;name&gt;/SKILL.md</c>：frontmatter <c>name/description</c>，来源 <c>agents</c>
/// - <c>.codebuddy/commands/&lt;name&gt;.md</c>：文件名即名称，首标题/正文首段为描述，来源 <c>commands</c>
/// </summary>
public interface IProjectSkillScannerService
{
    /// <summary>扫描项目根目录，返回识别的技能列表（未选目录/目录不存在返回空数组）。</summary>
    List<ProjectSkillItem> Scan(string? projectRoot);
}

public class ProjectSkillScannerService : IProjectSkillScannerService
{
    public List<ProjectSkillItem> Scan(string? projectRoot)
    {
        var result = new List<ProjectSkillItem>();
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return result;
        }

        if (!Directory.Exists(projectRoot))
        {
            return result;
        }

        try
        {
            ScanAgentsSkills(projectRoot, result);
            ScanCodebuddyCommands(projectRoot, result);
        }
        catch (Exception)
        {
            // 扫描异常不阻断面板：任一卷取失败静默返回已识别部分
        }

        result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    private static void ScanAgentsSkills(string root, List<ProjectSkillItem> result)
    {
        var baseDir = Path.Combine(root, ".agents", "skills");
        if (!Directory.Exists(baseDir))
        {
            return;
        }

        foreach (var skillDir in Directory.GetDirectories(baseDir))
        {
            var skillFile = Path.Combine(skillDir, "SKILL.md");
            if (!File.Exists(skillFile))
            {
                continue;
            }

            var rel = ToRel(root, skillFile);
            var (name, description) = ParseFrontMatter(skillFile);
            result.Add(new ProjectSkillItem
            {
                Id = $"agents:{rel}",
                Name = string.IsNullOrWhiteSpace(name)
                    ? Path.GetFileName(skillDir)
                    : name,
                Description = description,
                Source = "agents",
                Path = rel,
                IsDirectory = true
            });
        }
    }

    private static void ScanCodebuddyCommands(string root, List<ProjectSkillItem> result)
    {
        var baseDir = Path.Combine(root, ".codebuddy", "commands");
        if (!Directory.Exists(baseDir))
        {
            return;
        }

        foreach (var mdFile in Directory.GetFiles(baseDir, "*.md"))
        {
            var rel = ToRel(root, mdFile);
            var name = Path.GetFileNameWithoutExtension(mdFile);
            result.Add(new ProjectSkillItem
            {
                Id = $"commands:{rel}",
                Name = name,
                Description = PreviewFirstLines(mdFile),
                Source = "commands",
                Path = rel,
                IsDirectory = false
            });
        }
    }

    private static string ToRel(string root, string fullPath)
    {
        var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var rel = fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
            ? fullPath[rootPrefix.Length..]
            : Path.GetFileName(fullPath);
        return rel.Replace('\\', '/');
    }

    /// <summary>解析 SKILL.md 的 YAML frontmatter 首块（--- 开头）中的 name / description。</summary>
    private static (string Name, string Description) ParseFrontMatter(string file)
    {
        var lines = SafeReadLines(file, 40);
        if (lines.Count == 0)
        {
            return (string.Empty, string.Empty);
        }

        var name = string.Empty;
        var desc = string.Empty;
        // 仅当首行是 --- 才按 frontmatter 解析；否则当普通 .md
        if (lines[0].Trim() != "---")
        {
            name = Path.GetFileNameWithoutExtension(file);
            desc = PreviewFirstLines(lines);
            return (name, desc);
        }

        for (var i = 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Trim() == "---")
            {
                break;
            }
            if (line.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
            {
                name = line["name:".Length..].Trim();
            }
            else if (line.StartsWith("description:", StringComparison.OrdinalIgnoreCase))
            {
                desc = line["description:".Length..].Trim();
            }
        }
        return (name, desc);
    }

    private static string PreviewFirstLines(string file)
        => PreviewFirstLines(SafeReadLines(file, 20));

    private static string PreviewFirstLines(List<string> lines)
    {
        // 跳过 frontmatter，取正文首个非空段落节选
        var inFront = lines.Count > 0 && lines[0].Trim() == "---";
        foreach (var l in lines)
        {
            var t = l.Trim();
            if (inFront)
            {
                if (t == "---")
                {
                    inFront = false;
                }
                continue;
            }
            if (t.Length == 0 || t.StartsWith("#"))
            {
                continue;
            }
            return t.Length > 120 ? t[..120] + "…" : t;
        }
        return string.Empty;
    }

    private static List<string> SafeReadLines(string file, int max)
    {
        var list = new List<string>();
        try
        {
            using var reader = new StreamReader(file);
            for (var i = 0; i < max && !reader.EndOfStream; i++)
            {
                list.Add(reader.ReadLine() ?? string.Empty);
            }
        }
        catch (Exception)
        {
            // 读文件失败（占用/编码）静默返回已读部分
        }
        return list;
    }
}