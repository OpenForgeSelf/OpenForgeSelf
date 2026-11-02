using System.Text;

namespace ForgeSelf.Api.Services.Skills;

/// <summary>
/// 个人技能的文档形态：标准 SKILL.md（YAML frontmatter 的 name/description + 正文即系统提示）。
/// 不另造技能包格式。库表上的分类、工具、设置、启用与用量是技能库元数据，不写进正文。
/// </summary>
internal static class SkillMarkdown
{
    public static string Compose(string? name, string? description, string? systemPrompt)
    {
        var sb = new StringBuilder();
        sb.Append("---\n");
        sb.Append("name: ").Append(Quote(name)).Append('\n');
        sb.Append("description: ").Append(Quote(description)).Append('\n');
        sb.Append("---\n");
        if (!string.IsNullOrEmpty(systemPrompt))
            sb.Append(systemPrompt);
        if (string.IsNullOrEmpty(systemPrompt) || systemPrompt[^1] != '\n')
            sb.Append('\n');
        return sb.ToString();
    }

    private static string Quote(string? value)
    {
        var v = value ?? string.Empty;
        var plain = v.Length > 0
            && v.IndexOfAny(['\n', '\r', ':', '#', '"', '\'', '{', '}', '[', ']']) < 0
            && !char.IsWhiteSpace(v[0])
            && !char.IsWhiteSpace(v[^1]);
        if (plain)
            return v;

        return "\"" + v
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r\n", "\\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\n", StringComparison.Ordinal) + "\"";
    }
}
