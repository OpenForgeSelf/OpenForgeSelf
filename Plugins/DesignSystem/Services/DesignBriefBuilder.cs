using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using NewLife;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>设计说明书（§E）：章节渲染 + 预算 + contentHash + agent-rules。所有数值/变量名一律复用 ExportService 解析结果（E4 同源）。</summary>
public sealed class DesignBriefBuilder
{
    public const String Markdown = "markdown";
    public const String Json = "json";

    /// <summary>章节声明序（identity 恒含；components/brand 空则不开节）</summary>
    public static readonly String[] SectionOrder =
        ["identity", "rules", "colors", "typography", "scales", "components", "brand", "checklist"];

    public sealed record BriefOutcome(
        Object Project, String Theme, String ContentHash, IReadOnlyList<String> Sections,
        IReadOnlyList<String> Omitted, Boolean Truncated, String? Markdown, Object? Json, IReadOnlyList<String> Notes);

    readonly ExportService _export;
    readonly DesignProjectService _projects;
    readonly CatalogRepository _catalog;
    readonly DesignReviewService _review;

    public DesignBriefBuilder(ExportService export, DesignProjectService projects, CatalogRepository catalog, DesignReviewService review)
    {
        _export = export;
        _projects = projects;
        _catalog = catalog;
        _review = review;
    }

    /// <summary>构建说明书；无令牌项目只含 identity + 引导 notes（不抛错）。</summary>
    public BriefOutcome Build(Int64 projectId, String? themeCode, IReadOnlyList<String>? sections, Int32 maxChars, String format)
    {
        var snap = _export.Load(projectId, themeCode);
        var project = snap.Project;
        var theme = ResolveThemeCode(snap, projectId);
        var notes = new List<String>();

        var want = new HashSet<String>(sections is { Count: > 0 }
            ? sections.Where(s => SectionOrder.Contains(s, StringComparer.Ordinal))
            : SectionOrder, StringComparer.Ordinal);
        want.Add("identity");

        var hasTokens = snap.Tokens.Count > 0;
        if (!hasTokens) notes.Add("该项目尚未生成令牌，仅提供项目身份与引导；请用 design_create 或 design_edit action=regenerate 生成");
        if (themeCode.IsNullOrEmpty() == false && themeCode != snap.ThemeCode) notes.Add($"主题 {themeCode} 不存在，已回落默认主题 {theme}");

        var contentHash = ContentHash(snap, theme);
        var sectionsOut = new List<String>();
        var omitted = new List<String>();
        var bodies = new Dictionary<String, String>(StringComparer.Ordinal);
        var jsonSections = new Dictionary<String, Object>(StringComparer.Ordinal);
        var budget = Math.Clamp(maxChars, 2000, 60000);
        var used = 0;

        String? identityBody = null;
        if (want.Contains("identity"))
            identityBody = RenderIdentity(snap, theme, contentHash);

        // 按章节顺序装配：identity 恒先入，其余按预算尝试，放不下记 omitted 并继续尝试更小章节
        foreach (var section in SectionOrder)
        {
            if (!want.Contains(section)) continue;
            String? body = section switch
            {
                "identity" => identityBody,
                "rules" => RenderRules(),
                "colors" => hasTokens ? RenderColors(snap) : null,
                "typography" => hasTokens ? RenderTypography(snap) : null,
                "scales" => hasTokens ? RenderScales(snap) : null,
                "components" => snap.Components.Count > 0 ? RenderComponents(snap) : null,
                "brand" => (snap.Fonts.Count + snap.Assets.Count + snap.Screens.Count) > 0 ? RenderBrand(snap) : null,
                "checklist" => hasTokens ? RenderChecklist(projectId) : null,
                _ => null,
            };
            if (body == null) continue;   // 空则不开该节（components/brand 同规则）

            var text = format == Json ? JsonSection(section, snap, body) : body;
            if (section == "identity" || used + text.Length <= budget)
            {
                if (section == "identity") { sectionsOut.Insert(0, section); used = text.Length; }
                else { sectionsOut.Add(section); used += text.Length; }
                bodies[section] = body;
                jsonSections[section] = body;
            }
            else
            {
                omitted.Add(section);
            }
        }

        var markdown = format == Markdown ? String.Join("\n\n", sectionsOut.Where(bodies.ContainsKey).Select(b => bodies[b])) : null;
        var json = format == Json ? BuildJsonObject(snap, theme, contentHash, sectionsOut, jsonSections) : null;

        return new BriefOutcome(
            new { code = project.Code, name = project.Name, kind = project.Kind, version = project.Version, status = project.Status },
            theme, contentHash, sectionsOut, omitted, omitted.Count > 0, markdown, json, notes);
    }

    /// <summary>theme 取值：请求主题→默认主题→light 回落（与 B2 一致）。</summary>
    String ResolveThemeCode(ExportService.Snapshot snap, Int64 projectId)
    {
        if (!snap.ThemeCode.IsNullOrEmpty()) return snap.ThemeCode!;
        var def = _projects.ListThemes(projectId).FirstOrDefault(t => t.IsDefault);
        return def?.Code ?? "light";
    }

    /// <summary>E3 contentHash：SHA-256 前 16 位小写 hex；输入为规范文本逐行拼接，不含时间戳。</summary>
    public static String ContentHash(ExportService.Snapshot snap, String theme)
    {
        var sb = new StringBuilder();
        sb.Append(snap.Project.Code).Append('\n');
        sb.Append(snap.Project.Version).Append('\n');
        sb.Append(theme).Append('\n');
        sb.Append(DesignSystemConstants.ProjectionVersion).Append('\n');
        foreach (var t in snap.Tokens)
            sb.Append($"{t.Path}|{t.Tier}|{t.Type}|{t.Value}|{t.AliasPath ?? ""}\n");
        foreach (var c in snap.Components.OrderBy(c => c.Code, StringComparer.Ordinal))
            sb.Append($"{c.Code}|{c.Status}|{c.TokenRefsJson}\n");
        foreach (var f in snap.Fonts)
            sb.Append($"{f.Family}|{f.Role}|{f.Weight}|{f.License}\n");
        foreach (var a in snap.Assets)
            sb.Append($"{a.Code}|{a.Kind}|{a.SvgBody}\n");
        foreach (var s in snap.Screens)
            sb.Append($"{s.Code}|{s.Route}\n");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
    }

    String RenderIdentity(ExportService.Snapshot snap, String theme, String hash)
    {
        var p = snap.Project;
        var themes = String.Join(", ", snap.Themes.Select(t => t.Code));
        var seed = p.SeedText ?? "";
        if (seed.Length > 120) seed = seed[..120];
        var sb = new StringBuilder();
        sb.AppendLine($"# {p.Name}（{p.Code}）设计说明书 · v{p.Version}");
        sb.AppendLine();
        sb.AppendLine($"> 唯一真源：设计系统「{p.Code}」v{p.Version}（theme={theme}，hash={hash}）。写 UI 前先读本文，写完用 `design_review` 对照");
        sb.AppendLine();
        sb.AppendLine("- 用途类型：" + (p.Kind.IsNullOrEmpty() ? "product" : p.Kind));
        sb.AppendLine("- 状态：" + p.Status);
        sb.AppendLine("- 主题：" + themes);
        if (!seed.IsNullOrEmpty()) sb.AppendLine("- 种子：" + seed);
        return sb.ToString().TrimEnd();
    }

    String RenderRules()
    {
        return """
        ## 使用规则（MUST / MUST NOT）

        1. MUST 颜色一律 `var(--ds-semantic-*)` 或 `var(--ds-component-*)`，禁字面色值，禁直接引用原语色阶 `--ds-color-*`；
        2. MUST 间距/圆角/描边/字号/阴影/时长一律取 `--ds-space-* / --ds-radius-* / --ds-border-* / --ds-size-* / --ds-shadow-elevation-* / --ds-duration-*`；
        3. MUST 保持焦点可见（`--ds-component-focus-*`），不得 `outline:none` 而无替代；
        4. MUST 动效尊重 `prefers-reduced-motion`；
        5. MUST NOT 为单页新增令牌或自定义色值，缺令牌用 `design_edit set_token` 或提给设计维护者；
        6. 不知道令牌名时用 `design_lookup kind=nearest`，不凭感觉写值。
        """.Trim();
    }

    String RenderColors(ExportService.Snapshot snap)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 颜色");
        sb.AppendLine();
        sb.AppendLine("| 令牌 | CSS 变量 | 值(hex) | 对 surface-bg 对比度 | 用途 |");
        sb.AppendLine("| --- | --- | --- | --- | --- |");
        var surface = snap.InTier(TokenTiers.Semantic).FirstOrDefault(t => t.Path == "semantic.surface-bg");
        foreach (var t in snap.InTier(TokenTiers.Semantic).Where(t => t.Type == TokenTypes.Color))
        {
            var hex = t.ColorHex ?? "";
            var ratio = (surface?.ColorHex != null && hex.Length == 7)
                ? ContrastMath.Ratio(surface.ColorHex, hex).ToString("0.00") : "—";
            sb.AppendLine($"| `{t.Path}` | `{ExportService.CssVarName(t.Path)}` | `{hex}` | {ratio} | {t.Description ?? ""} |");
        }
        var componentColors = snap.Tokens.Count(t => t.Tier == TokenTiers.Component && t.Type == TokenTypes.Color);
        sb.AppendLine();
        sb.AppendLine($"组件层颜色令牌 {componentColors} 条；原语色阶 {ColorFamilies.All.Length} 族，不直接引用。");
        return sb.ToString().TrimEnd();
    }

    String RenderTypography(ExportService.Snapshot snap)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 排版");
        var sans = snap.Tokens.FirstOrDefault(t => t.Path == "font.sans");
        var mono = snap.Tokens.FirstOrDefault(t => t.Path == "font.mono");
        sb.AppendLine();
        sb.AppendLine($"- `font.sans`：`{sans?.Value ?? ""}`");
        sb.AppendLine($"- `font.mono`：`{mono?.Value ?? ""}`");
        sb.AppendLine();
        sb.AppendLine("| 角色 | 字号 | 行高 | 字重 | 字距 |");
        sb.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var t in snap.Tokens.Where(t => t.Type == TokenTypes.Typography))
        {
            var detail = TypeDetail(t);
            sb.AppendLine($"| `{t.Path}` | {detail} |");
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>type.* 复合令牌的 ValueJson 摘要（fontSize/fontWeight/lineHeight/letterSpacing）</summary>
    static String TypeDetail(ExportService.Snap t)
    {
        if (t.ValueJson.IsNullOrEmpty()) return "—";
        try
        {
            using var doc = JsonDocument.Parse(t.ValueJson);
            var parts = new List<String>();
            foreach (var key in new[] { "fontSize", "fontWeight", "lineHeight", "letterSpacing" })
                if (doc.RootElement.TryGetProperty(key, out var v))
                    parts.Add($"{key}={v.ValueKind switch { JsonValueKind.String => v.GetString(), JsonValueKind.Number => v.GetRawText(), _ => v.GetRawText() }}");
            return parts.Count > 0 ? String.Join(" / ", parts) : "—";
        }
        catch (JsonException)
        {
            return "—";
        }
    }

    String RenderScales(ExportService.Snapshot snap)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 尺度");
        sb.AppendLine();
        sb.AppendLine("| 档位 | 值 |");
        sb.AppendLine("| --- | --- |");
        foreach (var prefix in new[] { "space.", "radius.", "border." })
            foreach (var t in snap.Tokens.Where(t => t.Path.StartsWith(prefix, StringComparison.Ordinal)).OrderBy(t => t.Path, StringComparer.Ordinal))
                sb.AppendLine($"| `{t.Path}` | `{t.Value}` |");
        foreach (var t in snap.Tokens.Where(t => t.Path.StartsWith("shadow.elevation-", StringComparison.Ordinal)).OrderBy(t => t.Path, StringComparer.Ordinal))
        {
            var first = FirstShadowLayer(t);
            sb.AppendLine($"| `{t.Path}` | 首层：{first} |");
        }
        foreach (var t in snap.Tokens.Where(t => t.Path.StartsWith("duration.", StringComparison.Ordinal) || t.Path.StartsWith("ease.", StringComparison.Ordinal)).OrderBy(t => t.Path, StringComparer.Ordinal))
            sb.AppendLine($"| `{t.Path}` | `{t.Value}` |");
        foreach (var t in snap.Tokens.Where(t => t.Path.StartsWith("breakpoint.", StringComparison.Ordinal)).OrderBy(t => t.Path, StringComparer.Ordinal))
            sb.AppendLine($"| `{t.Path}` | `{t.Value}` |");
        foreach (var t in snap.Tokens.Where(t => t.Path.StartsWith("z-index.", StringComparison.Ordinal)).OrderBy(t => t.Path, StringComparer.Ordinal))
            sb.AppendLine($"| `{t.Path}` | `{t.Value}` |");
        return sb.ToString().TrimEnd();
    }

    static String FirstShadowLayer(ExportService.Snap t)
    {
        if (t.ValueJson.IsNullOrEmpty()) return t.Value;
        try
        {
            using var doc = JsonDocument.Parse(t.ValueJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("layers", out var layers)
                && layers.ValueKind == JsonValueKind.Array && layers.GetArrayLength() > 0)
            {
                var l = layers[0];
                var parts = new List<String>();
                foreach (var key in new[] { "inset", "offsetX", "offsetY", "blur", "spread", "color" })
                    if (l.TryGetProperty(key, out var v)) parts.Add($"{key}={v.GetRawText()}");
                return String.Join(" ", parts);
            }
        }
        catch (JsonException) { }
        return t.Value;
    }

    String RenderComponents(ExportService.Snapshot snap)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 组件");
        sb.AppendLine();
        var variantMap = snap.Variants.GroupBy(v => v.ComponentId).ToDictionary(g => g.Key, g => (IReadOnlyList<DesignComponentVariant>)g.ToList());
        foreach (var c in snap.Components.OrderBy(c => c.Code, StringComparer.Ordinal))
        {
            var cells = variantMap.TryGetValue(c.Id, out var list) ? list : [];
            var axes = ExportService.AxisSummary(cells);
            var states = String.Join("、", VariantAxes.Sort(VariantAxes.State, cells.Select(v => v.State).Where(s => !s.IsNullOrEmpty())));
            sb.AppendLine($"- **{c.Name}**（`{c.Code}`）：分类 {c.Category}，状态序 {states}，变体轴 {String.Join("/", axes)}，令牌 {CountRefs(c.TokenRefsJson)} 个");
            if (!c.A11yNotes.IsNullOrEmpty()) sb.AppendLine($"  - 可达性：{c.A11yNotes}");
        }
        return sb.ToString().TrimEnd();
    }

    static Int32 CountRefs(String tokenRefsJson)
    {
        if (tokenRefsJson.IsNullOrEmpty()) return 0;
        try
        {
            using var doc = JsonDocument.Parse(tokenRefsJson);
            return doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.GetArrayLength() : 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    String RenderBrand(ExportService.Snapshot snap)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 品牌");
        if (snap.Fonts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("| 字体 | 角色 | 字重 | 许可 |");
            sb.AppendLine("| --- | --- | --- | --- |");
            foreach (var f in snap.Fonts)
                sb.AppendLine($"| `{f.Family}` | {f.Role} | {f.Weight} | {f.License} |");
        }
        if (snap.Assets.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("| 资产 | 类型 |");
            sb.AppendLine("| --- | --- |");
            foreach (var a in snap.Assets)
                sb.AppendLine($"| `{a.Code}` | {a.Kind} |");
        }
        if (snap.Screens.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("| 页面 | 路由 |");
            sb.AppendLine("| --- | --- |");
            foreach (var s in snap.Screens)
                sb.AppendLine($"| `{s.Code}` | `{s.Route}` |");
        }
        return sb.ToString().TrimEnd();
    }

    String RenderChecklist(Int64 projectId)
    {
        var items = _review.Checklist(projectId, null, "any");
        var sb = new StringBuilder();
        sb.AppendLine("## 交付前清单");
        sb.AppendLine();
        foreach (var item in items)
            sb.AppendLine($"- [{item.Severity}] {item.Check}（{String.Join("、", item.Tokens) }）");
        return sb.ToString().TrimEnd();
    }

    static String JsonSection(String section, ExportService.Snapshot snap, String markdown) =>
        $"```json\n{{\"{section}\":\"{markdown.Replace("\"", "\\\"").Replace("\n", "\\n")}\"}}\n```";

    Object BuildJsonObject(ExportService.Snapshot snap, String theme, String hash,
        IReadOnlyList<String> sections, Dictionary<String, Object> jsonSections) => new
    {
        project = new { code = snap.Project.Code, name = snap.Project.Name, kind = snap.Project.Kind, version = snap.Project.Version, status = snap.Project.Status },
        theme,
        contentHash = hash,
        sections,
        sectionsContent = jsonSections,
    };

    /// <summary>E5 agent-rules：可粘贴进目标项目 AGENTS.md / CLAUDE.md / .cursorrules 的 Markdown。</summary>
    public String BuildAgentRules(Int64 projectId, String? themeCode)
    {
        var snap = _export.Load(projectId, themeCode);
        var theme = ResolveThemeCode(snap, projectId);
        var hash = ContentHash(snap, theme);
        var p = snap.Project;

        var sb = new StringBuilder();
        sb.AppendLine($"# 设计系统接入规则（{p.Name} · {p.Code} v{p.Version}）");
        sb.AppendLine();
        sb.AppendLine($"> 唯一真源：设计系统「{p.Code}」v{p.Version}（theme={theme}，hash={hash}）。所有前端 UI/UX 决策以它为基准。");
        sb.AppendLine();
        sb.AppendLine("## 开工前");
        sb.AppendLine("- 有 MCP：`universal_tool {\"tool\":\"design_context\",\"parameters\":{\"project\":\"<code>\"}}`（先 `list_tools` 枚举可用工具）");
        sb.AppendLine("- 无 MCP：读随包 `DESIGN.md` 与 `tokens.css`");
        sb.AppendLine();
        sb.AppendLine("## 写代码时");
        sb.AppendLine("- 只用 `var(--ds-*)` 取样式，禁字面色值/随意像素");
        sb.AppendLine("- 不知令牌名用 `design_lookup {\"kind\":\"nearest\",\"value\":\"…\"}`");
        sb.AppendLine();
        sb.AppendLine("## 写完后");
        sb.AppendLine("- `design_review {\"files\":[…],\"strict\":true}`，`summary.passed` 必须为 true、`tokenCoverage` 不得低于改动前");
        sb.AppendLine("- 无 MCP 时人工核对 DESIGN.md「使用规则」");
        sb.AppendLine();
        sb.AppendLine("## MUST / MUST NOT");
        sb.AppendLine("- MUST 颜色用 `--ds-semantic-*` / `--ds-component-*`；尺度用 `--ds-space-* / --ds-radius-* / --ds-border-* / --ds-size-* / --ds-shadow-elevation-* / --ds-duration-*`");
        sb.AppendLine("- MUST 焦点可见（`--ds-component-focus-*`）；MUST NOT `outline:none` 而无替代");
        sb.AppendLine("- MUST NOT 为单页新增令牌或自定义色值");
        sb.AppendLine();
        sb.AppendLine("## 令牌速查");
        foreach (var t in snap.InTier(TokenTiers.Semantic).Where(t => t.Type == TokenTypes.Color).Take(12))
            sb.AppendLine($"- `{ExportService.CssVarName(t.Path)}`：`{t.ColorHex ?? t.Value}`");
        return sb.ToString().TrimEnd();
    }
}
