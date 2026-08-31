namespace ForgeSelf.Api.Plugins.AIAgent.Models;

/// <summary>
/// 从项目目录自动识别出的一条「技能」。
/// 目前识别两类约定目录：
/// - <c>.agents/skills/&lt;name&gt;/SKILL.md</c>（frontmatter 提供 name/description，来源 <c>agents</c>）
/// - <c>.codebuddy/commands/&lt;name&gt;.md</c>（文件名即名称，来源 <c>commands</c>）
/// </summary>
public class ProjectSkillItem
{
    /// <summary>唯一 id（source:相对路径）。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>技能名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>技能描述。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>来源分类：agents / commands。</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>相对项目根的 SKILL.md / .md 路径（正斜杠）。</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>是否为目录型技能（.agents/skills，含可执行引用）；assistant 仅用于展示区分。</summary>
    public bool IsDirectory { get; set; }
}