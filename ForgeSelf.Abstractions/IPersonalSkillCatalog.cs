namespace ForgeSelf.Abstractions;

/// <summary>
/// 宿主个人技能库的只读目录（实现：<c>SkillsService</c>，XCode/SQLite）。
/// 智能体用它发现已启用的个人技能并注入上下文。
/// 项目 <c>.agents/skills</c> 仍由项目扫描负责，不走本契约。
/// 内容以 SKILL.md 文本为中心（frontmatter <c>name</c>/<c>description</c> + 正文）。
/// </summary>
public interface IPersonalSkillCatalog
{
    /// <summary>已启用的个人技能。Id 与 <c>/api/skills</c> 的 Id 相同。</summary>
    IReadOnlyList<PersonalSkillSnapshot> ListEnabled();
}

/// <summary>注入上下文用的个人技能快照。正文是完整 SKILL.md，不是另一种技能格式。</summary>
public sealed class PersonalSkillSnapshot
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    /// <summary>完整 SKILL.md 文本。</summary>
    public string SkillMarkdown { get; init; } = string.Empty;
}
