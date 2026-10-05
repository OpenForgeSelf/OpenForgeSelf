using System.Text.Json;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using ForgeSelf.Api.Plugins.DesignSystem.Services;
using ForgeSelf.Core;
using NewLife;

namespace ForgeSelf.Api.Plugins.DesignSystem.Agent;

/// <summary>8 个 design_* 工具（§B1–B8）。全部经 <see cref="DesignToolBase"/> 封套；写动作先过写开关。</summary>
public sealed class DesignGuideTool(DesignToolKit kit) : DesignToolBase(kit)
{
    public override String Id => "design-system.tool.design_guide";
    public override String Name => DesignToolIndex.Guide;
    public override String PluginId => DesignSystemConstants.PluginId;
    public override String Description => DesignToolIndex.Of(Name).Summary;
    public override String ParametersJsonSchema => DesignToolIndex.Of(Name).Schema;
    public override Boolean IsWrite(JsonElement args) => false;

    protected override Task<Object?> Handle(JsonElement args)
    {
        var projects = Kit.Projects.List(null, null).Where(p => p.Status != ProjectStatus.Archived).OrderByDescending(p => p.UpdatedAt).Take(20).ToList();
        var (allow, _, _, _) = Kit.AgentAccess.Get();
        var next = projects.Count == 0
            ? new[] { "design_presets action=recommend", "design_create" }
            : new[] { "design_context" };
        return Task.FromResult<Object?>(new
        {
            version = DesignSystemConstants.ModelVersion,
            purpose = "设计系统是前端 UI/UX 唯一真源：颜色/排版/尺度/组件/品牌/审计统一从这里取，禁止另起一套。",
            tools = DesignToolIndex.All.Select(t => new { t.Name, t.Kind, t.Summary, t.When }).ToArray(),
            workflows = new object[]
            {
                new { id = "consume", title = "开发前读真源 → 写完审查", steps = new[] { "design_context sections=guidelines（先读本项目 UX 规范）", "design_lookup kind=token", "design_review" } },
                new { id = "create", title = "为新项目建设计系统", steps = new[] { "design_presets action=recommend", "design_create" } },
                new { id = "maintain", title = "调整并发布", steps = new[] { "design_edit set_token|regenerate", "design_edit guideline（改交互要求）", "design_audit run=true", "design_edit publish" } },
                new { id = "guideline", title = "查/改某条 UX 要求", steps = new[] { "design_lookup kind=guideline [code|q]", "design_edit action=guideline apply=false（干跑）", "design_edit action=guideline apply=true", "design_review mode=checklist（派生条目 id=g:<code>:<ruleId>）" } },
            },
            projects = projects.Select(p => new
            {
                code = p.Code, name = p.Name, kind = p.Kind, version = p.Version, status = p.Status,
                tokenCount = Kit.Projects.CountTokens(p.Id), componentCount = Kit.Projects.CountComponents(p.Id),
            }).ToArray(),
            projectsTotal = Kit.Projects.List(null, null).Count(p => p.Status != ProjectStatus.Archived),
            presets = StylePresets.All.Select(p => new { p.Id, p.Name, p.Tagline }).ToArray(),
            access = new { allowWrite = allow },
            discovery = "外部 MCP 客户端请先 `universal_tool {\"tool\":\"list_tools\",\"parameters\":{\"keyword\":\"design\"}}` 枚举，再按名调用 design_* 工具。",
            next,
        });
    }
}

public sealed class DesignContextTool(DesignToolKit kit) : DesignToolBase(kit)
{
    public override String Id => "design-system.tool.design_context";
    public override String Name => DesignToolIndex.Context;
    public override String PluginId => DesignSystemConstants.PluginId;
    public override String Description => DesignToolIndex.Of(Name).Summary;
    public override String ParametersJsonSchema => DesignToolIndex.Of(Name).Schema;
    public override Boolean IsWrite(JsonElement args) => false;

    protected override Task<Object?> Handle(JsonElement args)
    {
        var project = ResolveProject(GetStr(args, "project"), requireWrite: false);
        var (theme, note) = ResolveTheme(project, GetStr(args, "theme"));
        var sections = GetStrArray(args, "sections");
        var format = GetStr(args, "format", DesignBriefBuilder.Markdown);
        if (format != DesignBriefBuilder.Markdown && format != DesignBriefBuilder.Json)
            throw new ArgumentException($"format 只能是 {DesignBriefBuilder.Markdown}/{DesignBriefBuilder.Json}");
        var maxChars = GetInt(args, "maxChars", 16000, 2000, 60000);
        var outcome = Kit.Brief.Build(project.Id, theme, sections, maxChars, format);
        var notes = outcome.Notes.ToList();
        if (note != null) notes.Add(note);
        return Task.FromResult<Object?>(new
        {
            outcome.Project, outcome.Theme, outcome.ContentHash, outcome.Sections, outcome.Omitted,
            truncated = outcome.Truncated, outcome.Markdown, json = outcome.Json, notes,
        });
    }
}

public sealed class DesignLookupTool(DesignToolKit kit) : DesignToolBase(kit)
{
    public override String Id => "design-system.tool.design_lookup";
    public override String Name => DesignToolIndex.Lookup;
    public override String PluginId => DesignSystemConstants.PluginId;
    public override String Description => DesignToolIndex.Of(Name).Summary;
    public override String ParametersJsonSchema => DesignToolIndex.Of(Name).Schema;
    public override Boolean IsWrite(JsonElement args) => false;

    protected override Task<Object?> Handle(JsonElement args)
    {
        var kind = GetStr(args, "kind", "");
        var themeArg = GetStr(args, "theme");
        return Task.FromResult<Object?>(kind switch
        {
            "token" => LookupTokens(args, themeArg),
            "component" => LookupComponents(args),
            "nearest" => LookupNearest(args, themeArg),
            "export" => LookupExport(args),
            "icon" => LookupIcons(args),
            "guideline" => LookupGuidelines(args, themeArg),
            _ => throw new ArgumentException($"kind 只能是 token/component/nearest/export/icon/guideline，收到 {kind}"),
        });
    }

    Object LookupTokens(JsonElement args, String? themeArg)
    {
        var project = ResolveProject(GetStr(args, "project"), requireWrite: false);
        var (theme, _) = ResolveTheme(project, themeArg);
        var index = Kit.Review.LoadIndex(project.Id, theme, out _, out _);
        var prefix = GetStr(args, "prefix");
        var q = GetStr(args, "q");
        var tier = GetStr(args, "tier");
        var type = GetStr(args, "type");
        var limit = GetInt(args, "limit", 50, 1, 200);
        var offset = GetInt(args, "offset", 0, 0, Int32.MaxValue);

        var all = index.Tokens
            .Where(t => prefix.IsNullOrEmpty() || t.Path.StartsWith(prefix!, StringComparison.OrdinalIgnoreCase))
            .Where(t => q.IsNullOrEmpty() || t.Path.Contains(q!, StringComparison.OrdinalIgnoreCase))
            .Where(t => tier.IsNullOrEmpty() || t.Tier == tier)
            .Where(t => type.IsNullOrEmpty() || t.Type == type)
            .ToList();
        var items = all.Skip(offset).Take(limit).Select(t => new
        {
            t.Path, t.Tier, t.Type, value = t.Value, hex = t.ColorHex,
            cssVar = ExportService.CssVarName(t.Path), alias = t.AliasPath,
            lifecycle = index.LifecycleByPath.TryGetValue(t.Path, out var lc) ? lc : "active",
        }).ToArray();
        return new { theme, total = all.Count, offset, limit, items };
    }

    Object LookupComponents(JsonElement args)
    {
        var project = ResolveProject(GetStr(args, "project"), requireWrite: false);
        var code = GetStr(args, "code");
        if (code.IsNullOrEmpty())
        {
            var list = Kit.Catalog.ListComponents(project.Id, null).Select(c => new
            {
                c.Code, c.Name, c.Category, c.Interactive, c.Status,
                tokenCount = CountRefs(c.TokenRefsJson), variantCount = Kit.Catalog.ListVariants(c.Id).Count,
            }).ToArray();
            return new { components = list };
        }

        var c = Kit.Catalog.FindComponent(project.Id, code!);
        if (c == null)
            return new { code = code, error = $"组件 {code} 不存在", candidates = Kit.Catalog.ListComponents(project.Id, null).Select(x => x.Code).Take(20).ToArray() };
        var cells = Kit.Catalog.ListVariants(c.Id);
        var axes = ExportService.AxisSummary(cells.ToList());
        var states = VariantAxes.Sort(VariantAxes.State, cells.Select(v => v.State).Where(s => !s.IsNullOrEmpty()));
        var tokens = cells.SelectMany(v => ParseRefs(v.TokenRefsJson)).Distinct(StringComparer.Ordinal).ToArray();
        var anatomy = ParseAnatomy(ExportService.AnatomyJson(c.GuidanceJson));
        return new
        {
            c.Code, c.Name, c.Category,
            anatomy, states, axes = AxisDict(axes), tokens, a11yNotes = c.A11yNotes, variantCount = cells.Count,
        };
    }

    /// <summary>
    /// UX 规范查询（M3）：不带 <c>code</c> 给列表（可 <c>q</c> 过滤），带 <c>code</c> 给详情。
    /// 详情里的正文/规则一律带当前值括注，并同文给出原文 —— agent 需要知道"这条要求怎么说"，
    /// 也需要知道"现在这一档到底是多少"，两者必须来自同一次读取（<see cref="DesignToolBase.GuidelineValues"/>）。
    /// </summary>
    Object LookupGuidelines(JsonElement args, String? themeArg)
    {
        var project = ResolveProject(GetStr(args, "project"), requireWrite: false);
        var (theme, _) = ResolveTheme(project, themeArg);
        var valueOf = GuidelineValues(project, theme);
        var q = (GetStr(args, "q") ?? "").Trim();
        var limit = GetInt(args, "limit", 50, 1, 200);

        var code = GetStr(args, "code");
        if (!code.IsNullOrEmpty())
        {
            var one = Kit.Guidelines.Find(project.Id, code!);
            if (one == null)
                return new { code, error = $"规范 {code} 不存在", candidates = Kit.Guidelines.List(project.Id).Select(x => x.Code).Take(20).ToArray() };
            return new { guideline = GuidelineDto(one, valueOf), theme };
        }

        var rows = Kit.Guidelines.List(project.Id).ToList();
        var visible = rows.Count;
        if (q.Length > 0)
            rows = rows.Where(g => Matches(g, q)).ToList();

        return new
        {
            theme,
            total = rows.Count,
            // 归档行不进默认清单（与界面/导出口径一致），但总数要说清库里还有几条被归档
            archived = Kit.Guidelines.List(project.Id, "archived").Count,
            returned = Math.Min(rows.Count, limit),
            truncated = rows.Count > limit,
            omittedByQuery = visible - rows.Count,
            guidelines = rows.Take(limit).Select(g => GuidelineDto(g, valueOf)).ToList(),
        };

        static Boolean Matches(DesignGuideline g, String q) =>
            g.Code.Contains(q, StringComparison.OrdinalIgnoreCase)
            || (g.Title ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
            || (g.Summary ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
            || GuidelineRepository.ReadRules(g.RulesJson).Any(r => r.Text.Contains(q, StringComparison.OrdinalIgnoreCase));
    }

    Object LookupNearest(JsonElement args, String? themeArg)
    {
        var project = ResolveProject(GetStr(args, "project"), requireWrite: false);
        var (theme, _) = ResolveTheme(project, themeArg);
        var value = GetStr(args, "value");
        if (value.IsNullOrEmpty()) throw new ArgumentException("kind=nearest 时 value 必填（颜色/长度/时长/阴影/字体族/字重）");
        var limit = GetInt(args, "limit", 3, 1, 5);
        var index = Kit.Review.LoadIndex(project.Id, theme, out _, out _);
        var result = NearestTokenFinder.Find(index, value, GetStr(args, "property"), limit);
        return new { theme, error = result.Error, matches = result.Matches, note = result.Note };
    }

    Object LookupExport(JsonElement args)
    {
        var project = ResolveProject(GetStr(args, "project"), requireWrite: false);
        var format = GetStr(args, "format", "");
        if (format.IsNullOrEmpty()) throw new ArgumentException("kind=export 时 format 必填");
        var (theme, _) = ResolveTheme(project, GetStr(args, "theme"));   // §B3：theme 缺省=项目默认主题
        var maxChars = GetInt(args, "maxChars", 16000, 2000, 60000);
        var file = Kit.Export.Produce(project.Id, format, theme);
        var restUrl = $"api/design-system/projects/{project.Id}/export?format={Uri.EscapeDataString(format)}&theme={theme ?? ""}";
        if (file.ContentType == "application/zip" || format.Equals("bundle", StringComparison.OrdinalIgnoreCase))
            return new { format, fileName = file.Name, contentType = "application/zip", restUrl, note = "二进制，请经 REST 下载", length = file.Bytes.Length, truncated = false };
        var text = file.Text;
        var truncated = text.Length > maxChars;
        return new { format, theme, fileName = file.Name, contentType = file.ContentType, length = text.Length, truncated, content = truncated ? text[..maxChars] : text, restUrl };
    }

    Object LookupIcons(JsonElement args)
    {
        var projectId = 0L;
        var code = GetStr(args, "project");
        if (!code.IsNullOrEmpty()) projectId = ResolveProject(code, requireWrite: false).Id;
        var collection = GetStr(args, "collection");
        var includeSvg = GetBool(args, "includeSvg", false);
        var limit = GetInt(args, "limit", 30, 1, 100);
        var items = Kit.Catalog.ListIcons(projectId, collection, null).Take(limit).Select(i => new
        {
            i.Code, i.Name, i.Collection, i.License,
            tags = ExportService.ParseStringArray(i.Tags),
            svg = includeSvg ? i.SvgBody : null,
        }).ToArray();
        return new { total = items.Length, items };
    }

    static Int32 CountRefs(String tokenRefsJson) => ParseRefs(tokenRefsJson).Count;

    static List<String> ParseRefs(String tokenRefsJson)
    {
        if (tokenRefsJson.IsNullOrEmpty()) return [];
        try
        {
            using var doc = JsonDocument.Parse(tokenRefsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];
            var list = new List<String>();
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                var s = e.ValueKind == JsonValueKind.String ? e.GetString() : e.TryGetProperty("path", out var p) ? p.GetString() : null;
                if (!s.IsNullOrEmpty()) list.Add(s!);
            }
            return list;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    static Dictionary<String, String[]> AxisDict(IReadOnlyList<String> axes) =>
        axes.Count == 0 ? [] : axes.ToDictionary(a => a, a => Array.Empty<String>());

    static List<String> ParseAnatomy(String? json)
    {
        if (json.IsNullOrEmpty()) return [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];
            return doc.RootElement.EnumerateArray()
                .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : e.GetRawText())
                .Where(s => !s.IsNullOrEmpty())
                .Select(s => s!).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

public sealed class DesignReviewTool(DesignToolKit kit) : DesignToolBase(kit)
{
    public override String Id => "design-system.tool.design_review";
    public override String Name => DesignToolIndex.Review;
    public override String PluginId => DesignSystemConstants.PluginId;
    public override String Description => DesignToolIndex.Of(Name).Summary;
    public override String ParametersJsonSchema => DesignToolIndex.Of(Name).Schema;
    public override Boolean IsWrite(JsonElement args) => false;

    protected override Task<Object?> Handle(JsonElement args)
    {
        var project = ResolveProject(GetStr(args, "project"), requireWrite: false);
        var (theme, _) = ResolveTheme(project, GetStr(args, "theme"));   // §B4：theme 缺省=项目默认主题（否则走 shared 快照，语义层为空）
        var mode = GetStr(args, "mode", "code");
        if (mode == "checklist")
        {
            var page = GetStr(args, "page", "any");
            var items = Kit.Review.Checklist(project.Id, theme, page);
            return Task.FromResult<Object?>(new { page, items });
        }
        if (mode != "code") throw new ArgumentException($"mode 只能是 code/checklist，收到 {mode}");

        var code = GetStr(args, "code");
        var files = GetObjArray(args, "files");
        if (code.IsNullOrEmpty() && files.Count == 0) throw new ArgumentException("code 与 files 二选一（code 模式必填其一）");
        var inputs = files.Count > 0
            ? files.Select(f =>
            {
                var path = f.TryGetProperty("path", out var p) ? p.GetString() : "";
                var content = f.TryGetProperty("content", out var c) ? c.GetString() : "";
                return new ReviewInput(path ?? "file", content ?? "", null);
            }).ToList()
            : [new ReviewInput("snippet." + (GetStr(args, "language") ?? "css"), code ?? "", GetStr(args, "language"))];

        var strict = GetBool(args, "strict", false);
        var maxFindings = GetInt(args, "maxFindings", 100, 1, 500);
        var outcome = Kit.Review.Review(project.Id, theme, inputs, strict, maxFindings);
        if (outcome.Error != null) throw new ArgumentException(outcome.Error);
        return Task.FromResult<Object?>(new
        {
            project = new { code = project.Code, name = project.Name, kind = project.Kind, version = project.Version, status = project.Status },
            theme,
            summary = outcome.Summary,
            findings = outcome.Findings,
            truncated = outcome.Truncated,
            notes = outcome.Notes,
            skipped = outcome.Skipped,
        });
    }
}

public sealed class DesignAuditTool(DesignToolKit kit) : DesignToolBase(kit)
{
    public override String Id => "design-system.tool.design_audit";
    public override String Name => DesignToolIndex.Audit;
    public override String PluginId => DesignSystemConstants.PluginId;
    public override String Description => DesignToolIndex.Of(Name).Summary;
    public override String ParametersJsonSchema => DesignToolIndex.Of(Name).Schema;
    public override Boolean IsWrite(JsonElement args) => GetBool(args, "run", false);

    protected override Task<Object?> Handle(JsonElement args)
    {
        var project = ResolveProject(GetStr(args, "project"), requireWrite: IsWrite(args));
        var ran = false;
        if (GetBool(args, "run", false)) { Kit.AuditEngine.Run(project.Id); ran = true; }
        var kind = GetStr(args, "kind");
        var onlyFailed = GetBool(args, "onlyFailed", true);
        var limit = GetInt(args, "limit", 50, 1, 200);
        var summary = Kit.Audits.Summarize(project.Id);
        var rows = Kit.Audits.List(project.Id, 0, kind, onlyFailed ? false : null)
            .OrderByDescending(a => SevRank(a.Severity)).ThenBy(a => a.TargetPath, StringComparer.Ordinal)
            .Take(limit)
            .Select(a => new
            {
                a.Kind, a.Rule, a.Severity, target = a.TargetPath, a.Passed, expected = a.Expected, actual = a.Actual, a.Message, a.Suggestion,
            }).ToArray();
        return Task.FromResult<Object?>(new { ran, summary, blocking = summary.Blocking, items = rows });
    }

    static Int32 SevRank(String s) => s == "critical" ? 3 : s == "warning" ? 2 : 1;
}

public sealed class DesignPresetsTool(DesignToolKit kit) : DesignToolBase(kit)
{
    public override String Id => "design-system.tool.design_presets";
    public override String Name => DesignToolIndex.Presets;
    public override String PluginId => DesignSystemConstants.PluginId;
    public override String Description => DesignToolIndex.Of(Name).Summary;
    public override String ParametersJsonSchema => DesignToolIndex.Of(Name).Schema;
    public override Boolean IsWrite(JsonElement args) => false;

    protected override Task<Object?> Handle(JsonElement args)
    {
        var action = GetStr(args, "action", "list");
        if (action == "list")
            return Task.FromResult<Object?>(new { presets = StylePresets.All.Select(PresetDto).ToArray() });
        if (action != "recommend") throw new ArgumentException($"action 只能是 list/recommend，收到 {action}");

        var brief = GetStr(args, "brief");
        var kind = GetStr(args, "kind");
        var industry = GetStr(args, "industry");
        var tones = GetStrArray(args, "tone");
        var density = GetStr(args, "density");
        var brandColor = GetStr(args, "brandColor");
        var limit = GetInt(args, "limit", 3, 1, 8);
        var matches = PresetRecommender.Recommend(brief, kind, industry, tones, density, brandColor, limit);
        return Task.FromResult<Object?>(new
        {
            inferredIndustry = DesignGenerator.InferIndustry(brief),
            matches = matches.Select(m => new { m.Id, m.Name, m.Tagline, m.Score, m.Reasons, request = RequestDto(m.Request) }).ToArray(),
        });
    }

    static Object PresetDto(StylePreset p) => new
    {
        p.Id, p.Name, p.Tagline, p.Tones, p.Kinds, p.Industries, p.Keywords,
        request = RequestDto(p.Request),
    };

    static Object RequestDto(GenerationRequest r) => new
    {
        r.Brief, seedColor = r.SeedColor, r.Hue, r.Chroma, r.Density, r.TypeRatio, r.TypeBasePx, r.RadiusBase,
        r.MotionScale, r.BrandName, r.Industry, themes = r.Themes,
        // M3：`design_presets` 出参里的 request 必须带出轴取值，否则衣柜里看见的预设与实际生成的参数对不上
        shadowStyle = r.ShadowStyle, shadowStrength = r.ShadowStrength, borderStrength = r.BorderStrength,
        neutralTemp = r.NeutralTemp, fontPairing = r.FontPairing, radiusStyle = r.RadiusStyle,
        accentStrategy = r.AccentStrategy,
    };
}

public sealed class DesignCreateTool(DesignToolKit kit) : DesignToolBase(kit)
{
    public override String Id => "design-system.tool.design_create";
    public override String Name => DesignToolIndex.Create;
    public override String PluginId => DesignSystemConstants.PluginId;
    public override String Description => DesignToolIndex.Of(Name).Summary;
    public override String ParametersJsonSchema => DesignToolIndex.Of(Name).Schema;
    public override Boolean IsWrite(JsonElement args) => GetBool(args, "apply", false);

    protected override Task<Object?> Handle(JsonElement args)
    {
        var apply = GetBool(args, "apply", false);
        var overrides = new GenerationRequest
        {
            Brief = GetStr(args, "brief"),
            SeedColor = GetStr(args, "seedColor"),
            Hue = GetDouble(args, "hue"),
            Chroma = GetDouble(args, "chroma"),
            Density = GetStr(args, "density"),
            TypeRatio = GetDouble(args, "typeRatio"),
            TypeBasePx = GetDouble(args, "typeBasePx"),
            RadiusBase = GetDouble(args, "radiusBase"),
            MotionScale = GetDouble(args, "motionScale"),
            BrandName = GetStr(args, "brandName"),
            Industry = GetStr(args, "industry"),
        };
        var themes = GetStrArray(args, "themes");
        if (themes.Count > 0) overrides.Themes = [.. themes];

        var preview = Kit.QuickCreate.Create(
            GetStr(args, "name", ""), GetStr(args, "code"), GetStr(args, "kind"), GetStr(args, "description"),
            GetStr(args, "preset"), overrides, apply,
            out var uiRoute, out var applied, out var error);
        if (error != null) throw new ArgumentException(error);
        if (!apply)
            return Task.FromResult<Object?>(new
            {
                applied = false,
                preview = new { preview!.Code, preview.CodeAvailable, preview.Name, preview.Seed, preview.Industry, preview.Hue, preview.Tokens, preview.Themes, preview.Notes },
                request = RequestDto(overrides),
                uiRoute,
            });

        var gen = applied!.Generation;
        var outbound = GenerationDto(gen);
        return Task.FromResult<Object?>(new
        {
            applied = true,
            project = new { applied.Project.Id, applied.Project.Code, applied.Project.Name, applied.Project.Version, applied.Project.Status },
            generation = outbound,
            audit = new { applied.Audit.Total, applied.Audit.Passed, applied.Audit.Critical, applied.Audit.Warning, applied.Audit.Info, applied.Audit.Blocking },
            warnings = applied.Warnings,
            uiRoute,
        });
    }

    static Object GenerationDto(GenerationOutcome gen) => new
    {
        seed = gen.Result.Seed, industry = gen.Result.Industry, hue = gen.Result.Hue,
        tokens = gen.Result.Total, components = gen.Components.Components, variants = gen.Components.Variants,
        fonts = gen.Brand.Fonts, screens = gen.Brand.Screens, assets = gen.Brand.Assets,
        themes = gen.Result.Themed.Keys.ToArray(), notes = gen.Result.Notes, skippedProtected = gen.Result.SkippedProtected,
    };

    static Object RequestDto(GenerationRequest r) => new
    {
        r.Brief, seedColor = r.SeedColor, r.Hue, r.Chroma, r.Density, r.TypeRatio, r.TypeBasePx, r.RadiusBase,
        r.MotionScale, r.BrandName, r.Industry, themes = r.Themes,
        // M3：出参必须把轴带回去，否则 agent 读到的 request 与实际生成的不是同一份
        shadowStyle = r.ShadowStyle, shadowStrength = r.ShadowStrength, borderStrength = r.BorderStrength,
        neutralTemp = r.NeutralTemp, fontPairing = r.FontPairing, radiusStyle = r.RadiusStyle,
        accentStrategy = r.AccentStrategy,
    };

}

public sealed class DesignEditTool(DesignToolKit kit) : DesignToolBase(kit)
{
    public override String Id => "design-system.tool.design_edit";
    public override String Name => DesignToolIndex.Edit;
    public override String PluginId => DesignSystemConstants.PluginId;
    public override String Description => DesignToolIndex.Of(Name).Summary;
    public override String ParametersJsonSchema => DesignToolIndex.Of(Name).Schema;

    public override Boolean IsWrite(JsonElement args)
    {
        var action = GetStr(args, "action", "");
        var apply = GetBool(args, "apply", false);
        return action switch
        {
            "set_token" => true,
            "publish" => true,
            "regenerate" => apply,
            "guideline" => apply,
            _ => false,
        };
    }

    protected override Task<Object?> Handle(JsonElement args)
    {
        var action = GetStr(args, "action", "");
        var apply = GetBool(args, "apply", false);
        var project = ResolveProject(GetStr(args, "project"), requireWrite: IsWrite(args));
        return Task.FromResult<Object?>(action switch
        {
            "set_token" => SetToken(project, args),
            "regenerate" => Regenerate(project, args, apply),
            "publish" => Publish(project, args),
            "guideline" => GuidelineEdit(project, args, apply),
            _ => throw new ArgumentException($"action 只能是 set_token/regenerate/publish/guideline，收到 {action}"),
        });
    }

    /// <summary>
    /// 新增/更新一条 UX 规范（upsert，M3）。<c>apply=false</c> 只干跑并回读现状，不写库、也不算写动作（写开关不受扰）。
    /// <c>tokens</c>/<c>rules</c> **缺省与空数组语义不同**：解析时空数组一律当"这次不改"（null），
    /// 否则 agent 只想改标题就会把引用清单清空 —— 这类"顺手清库"的口子必须堵死。
    /// </summary>
    Object GuidelineEdit(DesignProject project, JsonElement args, Boolean apply)
    {
        var code = GetStr(args, "code");
        if (code.IsNullOrEmpty()) throw new ArgumentException("action=guideline 时 code 必填（小写 kebab，项目内唯一，如 layout-grid）");

        var valueOf = GuidelineValues(project, GetStr(args, "theme"));
        var existing = Kit.Guidelines.Find(project.Id, code!);
        if (!apply)
            return new
            {
                applied = false,
                code = code!,
                wouldCreate = existing == null,
                before = existing == null ? null : GuidelineDto(existing, valueOf),
                note = "apply=false 干跑：未写入。确认 before/字段后带 apply=true 重发",
            };

        var tokens = GetStrArray(args, "tokens");
        var ruleEls = GetObjArray(args, "rules");
        var patch = new GuidelinePatch
        {
            Title = GetStr(args, "title"),
            Summary = GetStr(args, "summary"),
            Body = GetStr(args, "body"),
            Category = GetStr(args, "category"),
            Status = GetStr(args, "status"),
            TokenRefs = tokens.Count > 0 ? tokens.ToList() : null,
            Rules = ruleEls.Count > 0
                ? ruleEls.Select(e => new GuidelineRuleInput(
                    Read(e, "id"), Read(e, "level"), Read(e, "text"))).ToList()
                : null,
        };

        var saved = Kit.Guidelines.Save(project.Id, code!, patch);
        return new
        {
            applied = true,
            created = existing == null,
            guideline = GuidelineDto(saved, GuidelineValues(project, GetStr(args, "theme"))),
        };

        static String? Read(JsonElement e, String key) =>
            e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    Object SetToken(DesignProject project, JsonElement args)
    {
        var path = GetStr(args, "path");
        if (path.IsNullOrEmpty()) throw new ArgumentException("set_token 时 path 必填（点分 kebab，如 semantic.brand.primary）");
        var value = GetStr(args, "value");
        var alias = GetStr(args, "alias");
        if (value.IsNullOrEmpty() && alias.IsNullOrEmpty()) throw new ArgumentException("set_token 时 value 与 alias 二选一");
        if (!value.IsNullOrEmpty() && !alias.IsNullOrEmpty()) throw new ArgumentException("set_token 时 value 与 alias 只能给一个");

        var tier = GetStr(args, "tier");
        var type = GetStr(args, "type");
        var themeCode = GetStr(args, "theme");
        var themeId = DesignSystemConstants.SharedThemeId;
        var existing = Kit.Tokens.Find(project.Id, DesignSystemConstants.SharedThemeId, path!);
        if (!themeCode.IsNullOrEmpty())
            themeId = Kit.Projects.ResolveThemeId(project.Id, themeCode);
        else if (existing == null && tier == TokenTiers.Semantic)
            throw new ArgumentException($"新建 semantic 令牌必须给 theme（它按主题落层）；primitive/component 写共享层可省");

        var patch = new TokenPatch { Path = path!, ThemeId = themeId, Value = value, AliasPath = alias };
        if (!tier.IsNullOrEmpty()) patch.Tier = tier;
        if (!type.IsNullOrEmpty()) patch.Type = type;
        patch.Description = GetStr(args, "description");
        patch.Generator = "manual";

        var result = Kit.Tokens.UpsertBatch(project.Id, [patch], overwrite: true);
        if (!result.Succeeded)
            return new { path, theme = themeCode ?? "shared", created = false, diagnostics = result.Diagnostics };

        var (afterValue, afterHex) = ReadBack(project.Id, themeId, path!);
        var created = existing == null;
        return new
        {
            path = path!, theme = themeCode ?? "shared", created,
            before = existing == null ? null : new { value = existing.Value, alias = existing.AliasPath },
            after = new { value = afterValue, hex = afterHex },
            generator = "manual",
        };
    }

    (String, String?) ReadBack(Int64 projectId, Int64 themeId, String path)
    {
        var t = Kit.Tokens.Find(projectId, themeId, path);
        if (t == null) return ("", null);
        var snap = Kit.Export.Load(projectId, themeId > 0 ? Kit.Tokens.FindTheme(themeId)?.Code : null);
        var s = snap.Tokens.FirstOrDefault(x => x.Path == path);
        return (s?.Value ?? t.Value, s?.ColorHex);
    }

    Object Regenerate(DesignProject project, JsonElement args, Boolean apply)
    {
        var overwrite = GetBool(args, "overwrite", false);
        var request = new GenerationRequest
        {
            Brief = GetStr(args, "brief"),
            SeedColor = GetStr(args, "seedColor"),
            Density = GetStr(args, "density"),
            Industry = GetStr(args, "industry"),
            // M3 风格轴：regenerate 与 create 同面，缺一条就是"agent 传了但重新生成时没生效"
            ShadowStyle = GetStr(args, "shadowStyle"),
            ShadowStrength = GetDouble(args, "shadowStrength"),
            BorderStrength = GetStr(args, "borderStrength"),
            NeutralTemp = GetStr(args, "neutralTemp"),
            FontPairing = GetStr(args, "fontPairing"),
            RadiusStyle = GetStr(args, "radiusStyle"),
            AccentStrategy = GetStr(args, "accentStrategy"),
        };
        var themes = GetStrArray(args, "themes");
        if (themes.Count > 0) request.Themes = [.. themes];

        if (!apply)
        {
            var gen = DesignGenerator.Generate(request);
            return new
            {
                applied = false,
                preview = new { gen.Seed, gen.Industry, gen.Hue, tokens = gen.Total, themes = gen.Themed.Keys.ToArray(), notes = gen.Notes },
            };
        }

        var outcome = Kit.Generation.Run(project.Id, request, overwrite);
        return new
        {
            applied = true,
            generation = new
            {
                seed = outcome.Result.Seed, industry = outcome.Result.Industry, hue = outcome.Result.Hue,
                tokens = outcome.Result.Total, components = outcome.Components.Components, variants = outcome.Components.Variants,
                fonts = outcome.Brand.Fonts, screens = outcome.Brand.Screens, assets = outcome.Brand.Assets,
                themes = outcome.Result.Themed.Keys.ToArray(), notes = outcome.Result.Notes,
            },
            skippedProtected = outcome.Result.SkippedProtected,
            conflicts = outcome.Result.Conflicts,
            audit = new { outcome.Audit.Total, outcome.Audit.Passed, outcome.Audit.Critical, outcome.Audit.Warning, outcome.Audit.Info, outcome.Audit.Blocking },
        };
    }

    Object Publish(DesignProject project, JsonElement args)
    {
        var version = GetStr(args, "version");
        if (version.IsNullOrEmpty()) throw new ArgumentException("publish 时 version 必填");
        var release = Kit.Releases.Create(project.Id, version!, GetStr(args, "notes"));
        return new
        {
            release = new
            {
                release.Id, release.Version, release.TokensHash, release.TokenCount, release.AuditPassed,
                snapshotAvailable = !release.SnapshotFile.IsNullOrEmpty() && File.Exists(release.SnapshotFile),
            },
        };
    }
}
