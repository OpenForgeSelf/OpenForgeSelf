using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NewLife;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>一个导出工件</summary>
public sealed record ExportedFile(String Name, String ContentType, Byte[] Bytes)
{
    public String Text => Encoding.UTF8.GetString(Bytes);
}

/// <summary>导出格式清单（`GET meta` 与前端下拉的唯一真源）</summary>
public static class ExportFormats
{
    public const String Dtcg = "dtcg";
    public const String Css = "css";
    public const String Tailwind = "tailwind";
    public const String Scss = "scss";
    public const String Less = "less";
    public const String TypeScript = "ts";
    public const String TokensStudio = "tokens-studio";
    public const String DesignMd = "design-md";
    public const String ElementPlus = "element-plus";
    public const String Registry = "registry";
    public const String StardustJson = "stardust-json";
    public const String StardustSql = "stardust-sql";
    public const String Brief = "brief";
    public const String AgentRules = "agent-rules";
    public const String Bundle = "bundle";

    public static readonly String[] All =
    [
        Dtcg, Css, Tailwind, Scss, Less, TypeScript, TokensStudio, DesignMd, ElementPlus, Registry, StardustJson, StardustSql, Brief, AgentRules, Bundle
    ];

    /// <summary>尚未实现的目标一律不声明（能力面诚实）</summary>
    public static Boolean IsKnown(String? format) => format != null && All.Contains(format, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// 导出投影层：库里是按行存的令牌（tier/mode/别名），对外一律走 **DTCG 树**这一中间表示。
///
/// 硬约束（design D18/G18）：物理表形状不得泄漏到任何工件里；所有目标格式都只读 <see cref="Snapshot"/>。
/// </summary>
public sealed class ExportService
{
    readonly TokenRepository _tokens;
    readonly DesignProjectService _projects;
    readonly CatalogRepository _catalog;

    public ExportService(TokenRepository tokens, DesignProjectService projects, CatalogRepository catalog)
    {
        _tokens = tokens;
        _projects = projects;
        _catalog = catalog;
    }

    /// <summary>
    /// brief / agent-rules 两种导出由 DesignBriefBuilder 提供（§E 同源：hex==Load 的 ColorHex、变量名==CssVarName）。
    /// 由插件 Apply 装配时注入（避免构造循环：DesignBriefBuilder 依赖 ExportService 解析快照）。
    /// </summary>
    public DesignBriefBuilder? BriefBuilder { get; set; }

    /// <summary>
    /// 某主题的导出快照：共享层 + 该主题覆盖层，别名解析后带有效值。
    /// 品牌三表（字体/资产/页面）也进快照 —— 投影不许只给令牌：
    /// 库里登记了字体却不出 `@font-face`、有 logo 却不进 bundle，就是"库里有了、交付物里没有"的半套交付。
    /// 组件目录与变体矩阵同样进来（M10）：`a11yNotes` / `anatomy` / 每格令牌清单是规格的本体，
    /// 只把 `component.*` 令牌丢进产物等于把"这条规格要求什么"留在了库里。
    /// </summary>
    public sealed record Snapshot(DesignProject Project, String? ThemeCode, IReadOnlyList<Snap> Tokens, IReadOnlyList<DesignTheme> Themes,
        IReadOnlyList<DesignFontFace> Fonts, IReadOnlyList<DesignAsset> Assets, IReadOnlyList<DesignScreen> Screens,
        IReadOnlyList<DesignComponent> Components, IReadOnlyList<DesignComponentVariant> Variants, IReadOnlyList<DesignIcon> Icons)
    {
        public IEnumerable<Snap> InTier(String tier) => Tokens.Where(t => t.Tier == tier);
    }

    /// <summary>投影用的单个令牌（已解析有效值）</summary>
    public sealed record Snap(String Path, String Tier, String Type, String Value, String? ValueJson, String? AliasPath, String? AliasRendered,
        String? Group, String? Description, String? ColorHex, Double ContrastRatio, String? WcagLevel, String? Extensions);

    public Snapshot Load(Int64 projectId, String? themeCode)
    {
        var project = _projects.Find(projectId) ?? throw new KeyNotFoundException($"项目 {projectId} 不存在");
        var themeId = themeCode.IsNullOrEmpty() ? DesignSystemConstants.SharedThemeId : _projects.ResolveThemeId(projectId, themeCode);
        var graph = _tokens.LoadGraph(projectId, themeId > 0 ? themeId : null, themeCode);

        var components = _catalog.ListComponents(projectId, null).ToList();
        // 一次批量取变体：以前逐组件查（N+1），导出页并行预览多个格式时把上百次查询压到同一个
        // SQLite 文件上，实测 database is locked（v2.6.8 e2e 抓到的 500）
        var variants = _catalog.VariantsByComponent(components.Select(c => c.Id).ToList());
        return new Snapshot(project, themeCode, BuildSnaps(graph), _projects.ListThemes(projectId).ToList(),
            _catalog.ListFonts(projectId).ToList(),
            _catalog.ListAssets(projectId, null).ToList(),
            _catalog.ListScreens(projectId).ToList(),
            components,
            components.SelectMany(c => variants[c.Id]).ToList(),
            _catalog.ListIcons(projectId, null, null).ToList());
    }

    /// <summary>
    /// 内存令牌图 → 投影令牌行（层级/路径排序 + 有效值解析 + 颜色补 hex）。
    ///
    /// 抽成公开静态方法的唯一理由：`preview-css`（内存预览）与落库导出必须**共用这一份构造逻辑**。
    /// 若内存预览自己再写一遍排序/解析，两份就会漂 —— AC2「展厅看到的 == 交付拿到的」随之失去守卫。
    /// 排序不可省：它决定 CSS 里变量行的**出现次序**，同源判据要求逐字相同。
    /// </summary>
    public static IReadOnlyList<Snap> BuildSnaps(TokenGraph graph)
    {
        var list = new List<Snap>();
        foreach (var node in graph.All().OrderBy(n => TokenTiers.Rank(n.Tier)).ThenBy(n => n.Path, StringComparer.Ordinal))
        {
            var r = graph.Resolve(node.Path);
            var aliasRendered = node.AliasPath == null ? null : RenderAlias(node.AliasPath);
            list.Add(new Snap(node.Path, node.Tier, node.Type, r.IsOk ? r.Value : (node.Value ?? ""), node.ValueJson, node.AliasPath, aliasRendered,
                node.Path.Split('.')[0], null, null, -1, null, node.Extensions));
        }

        // 颜色有效值补 hex/对比度（供 DESIGN.md 与 CSS 注释用）
        return list.Select(s =>
        {
            if (s.Type != TokenTypes.Color) return s;
            var c = graph.ResolveColor(s.Path);
            if (c == null) return s;
            var hex = Oklch.ToRgb8(c.Value).ToHex();
            return s with { ColorHex = hex };
        }).ToList();
    }

    /// <summary>
    /// 用一张内存令牌图装配投影快照（`preview-css` 专用）。
    /// 品牌/组件/页面等目录一律空：内存预览只承诺"令牌层 CSS 与落库导出一致"，
    /// 不假装自己带了资产与页面清单（能力面诚实——空目录就是空目录，不编造条目）。
    /// </summary>
    public Snapshot SnapshotFromGraph(TokenGraph graph, DesignProject project, String? themeCode) =>
        new(project, themeCode, BuildSnaps(graph), [], [], [], [], [], [], []);

    static String RenderAlias(String path) => "{" + path + "}";

    /// <summary>按格式产出一个工件；bundle 返回 zip</summary>
    public ExportedFile Produce(Int64 projectId, String format, String? themeCode)
    {
        var snap = Load(projectId, themeCode);
        var suffix = themeCode.IsNullOrEmpty() ? "" : $"-{themeCode}";

        return format.ToLowerInvariant() switch
        {
            ExportFormats.Dtcg => Json($"{snap.Project.Code}{suffix}.tokens.json", ToDtcg(snap)),
            ExportFormats.Css => Text($"tokens{suffix}.css", "text/css", ToCss(snap)),
            ExportFormats.Tailwind => Text($"theme{suffix}.css", "text/css", ToTailwind(snap)),
            ExportFormats.Scss => Text($"_tokens{suffix}.scss", "text/x-scss", ToScss(snap)),
            ExportFormats.Less => Text($"tokens{suffix}.less", "text/x-less", ToLess(snap)),
            ExportFormats.TypeScript => Text($"tokens{suffix}.ts", "text/plain", ToTypeScript(snap)),
            ExportFormats.TokensStudio => Json($"{snap.Project.Code}{suffix}.tokens-studio.json", ToTokensStudio(snap)),
            ExportFormats.DesignMd => Text($"DESIGN{suffix}.md", "text/markdown", ToDesignMd(snap)),
            ExportFormats.ElementPlus => Text($"element-plus{suffix}.css", "text/css", ToElementPlus(snap)),
            ExportFormats.Registry => Json("registry.json", ToRegistry(snap)),
            ExportFormats.StardustJson => Json("design-system.api.json", ToStardustIndex(snap)),
            ExportFormats.StardustSql => Text("design-system.data.sql", "application/sql", ToStardustSql(snap)),
            ExportFormats.Brief => Brief(snap, projectId, themeCode, suffix),
            ExportFormats.AgentRules => AgentRules(snap, projectId, themeCode),
            ExportFormats.Bundle => Bundle(projectId, themeCode),
            _ => throw new ArgumentException($"未知导出格式 {format}，可用：{String.Join(",", ExportFormats.All)}", nameof(format)),
        };
    }

    /// <summary>设计说明书（BRIEF.md）：同源要求见 <see cref="DesignBriefBuilder"/>（密度主题下无语义色则省略 colors 节，不抛错）。</summary>
    ExportedFile Brief(Snapshot snap, Int64 projectId, String? themeCode, String suffix)
    {
        var builder = BriefBuilder ?? throw new ArgumentException("brief 导出未装配（DesignBriefBuilder 未注入），请用 REST 或工具取说明书");
        var outcome = builder.Build(projectId, themeCode, null, 60000, DesignBriefBuilder.Markdown);
        return Text($"BRIEF{suffix}.md", "text/markdown", outcome.Markdown ?? "");
    }

    /// <summary>接入规则（agent-rules.md，与主题无关）。</summary>
    ExportedFile AgentRules(Snapshot snap, Int64 projectId, String? themeCode)
    {
        var builder = BriefBuilder ?? throw new ArgumentException("agent-rules 导出未装配（DesignBriefBuilder 未注入）");
        return Text("agent-rules.md", "text/markdown", builder.BuildAgentRules(projectId, themeCode));
    }

    /// <summary>全套工件打包成 zip（一次交付给下游工程）</summary>
    public ExportedFile Bundle(Int64 projectId, String? themeCode)
    {
        var snap = Load(projectId, themeCode);
        var files = new List<(String Name, Byte[] Bytes)>
        {
            ("README.md", Encoding.UTF8.GetBytes(Manifest(snap))),
            ($"{snap.Project.Code}.tokens.json", Encoding.UTF8.GetBytes(ToDtcg(snap))),
            ($"{snap.Project.Code}.tokens-studio.json", Encoding.UTF8.GetBytes(ToTokensStudio(snap))),
            ("registry.json", Encoding.UTF8.GetBytes(ToRegistry(snap))),
            ("stardust/api/index.json", Encoding.UTF8.GetBytes(ToStardustIndex(snap))),
            ("stardust/design-system.data.sql", Encoding.UTF8.GetBytes(ToStardustSql(snap))),
            ("stardust/design-system.xml", Encoding.UTF8.GetBytes(ToXcodeModel(snap))),
            ("brand/fonts.json", JsonBytes(BrandFonts(snap))),
            ("brand/screens.json", JsonBytes(BrandScreens(snap))),
        };

        // 接入规则与设计说明书进包（§E5/§I：agent-rules 与主题无关；brief 按非密度主题逐份）
        if (BriefBuilder != null)
        {
            files.Add(("agent-rules.md", Encoding.UTF8.GetBytes(BriefBuilder.BuildAgentRules(projectId, themeCode))));
            foreach (var theme in snap.Themes.Where(t => t.ModeKind != ThemeModeKinds.Density))
            {
                var brief = BriefBuilder.Build(projectId, theme.Code, null, 60000, DesignBriefBuilder.Markdown);
                files.Add(($"brief/BRIEF.{theme.Code}.md", Encoding.UTF8.GetBytes(brief.Markdown ?? "")));
            }
        }

        // 图形资产以独立 .svg 落进包里：下游要的是能直接引用的文件，不是一段藏在 JSON 里的字符串
        foreach (var a in snap.Assets.Where(a => !a.SvgBody.IsNullOrEmpty()))
            files.Add(($"{BrandDir}/{a.Code}.svg", Encoding.UTF8.GetBytes(SvgDocument(a))));

        foreach (var theme in snap.Themes.Where(t => t.ModeKind != ThemeModeKinds.Density))
        {
            var t = Load(projectId, theme.Code);
            files.Add(($"css/tokens.{theme.Code}.css", Encoding.UTF8.GetBytes(ToCss(t))));
            files.Add(($"tailwind/theme.{theme.Code}.css", Encoding.UTF8.GetBytes(ToTailwind(t))));
            files.Add(($"scss/_tokens.{theme.Code}.scss", Encoding.UTF8.GetBytes(ToScss(t))));
            files.Add(($"less/tokens.{theme.Code}.less", Encoding.UTF8.GetBytes(ToLess(t))));
            files.Add(($"ts/tokens.{theme.Code}.ts", Encoding.UTF8.GetBytes(ToTypeScript(t))));
            files.Add(($"element-plus/theme.{theme.Code}.css", Encoding.UTF8.GetBytes(ToElementPlus(t))));
            files.Add(($"design-md/DESIGN.{theme.Code}.md", Encoding.UTF8.GetBytes(ToDesignMd(t))));
        }

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            foreach (var (name, bytes) in files)
            {
                var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
                using var s = entry.Open();
                s.Write(bytes);
            }
        }
        return new ExportedFile($"{snap.Project.Code}-export.zip", "application/zip", ms.ToArray());
    }

    /// <summary>品牌工件在 bundle 里的目录名</summary>
    const String BrandDir = "brand";

    /// <summary>
    /// 品牌图形统一按 **24×24 网格**授权（与内置图标库同一套网格规矩）：
    /// 库里只存图形本体（`SvgBody`，不含 `&lt;svg&gt;` 外层），落文件时补这层外壳。
    /// 颜色一律 `currentColor`，所以这里**不写任何色值** —— 写死就等于把换肤关掉。
    /// </summary>
    static String SvgDocument(DesignAsset a) =>
        $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" width=\"24\" height=\"24\" " +
        $"role=\"img\" aria-label=\"{EscapeXml(a.Name ?? a.Code)}\">{a.SvgBody}</svg>\n";

    static String EscapeXml(String s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    /// <summary>
    /// 品牌工件的 JSON：缩进 + **不转义非 ASCII**。
    /// 这两个文件是给人和 CI 读合规信息的（"这套字体能不能带"），默认转义会把中文许可证变成一串 `\uXXXX`，等于把答案锁在编码里。
    /// </summary>
    static readonly JsonSerializerOptions BrandJson = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    static Byte[] JsonBytes(String json) => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(JsonNode.Parse(json), BrandJson));

    /// <summary>字体登记表（含许可证）：交付方最常被合规问的一句是"这套字体能不能带"</summary>
    static String BrandFonts(Snapshot snap)
    {
        var faces = new JsonArray();
        foreach (var f in snap.Fonts)
        {
            faces.Add((JsonNode)new JsonObject
            {
                ["family"] = f.Family,
                ["weight"] = f.Weight,
                ["style"] = f.Style,
                ["role"] = f.Role,
                ["fileName"] = f.FileName,
                ["fileRef"] = f.FileRef,
                ["license"] = f.License ?? "未标注",
                ["sourceUrl"] = f.SourceUrl,
            });
        }
        return JsonSerializer.Serialize(new JsonObject
        {
            ["note"] = "FileName/FileRef 非空的行会在 CSS 投影里生成 @font-face；其余是字体栈成员（系统提供，不随产物分发）",
            ["faces"] = faces,
        });
    }

    /// <summary>页面清单：这个设计系统服务哪些屏（生成器给的是起手建议，不是产品事实）</summary>
    static String BrandScreens(Snapshot snap)
    {
        var screens = new JsonArray();
        foreach (var s in snap.Screens)
        {
            screens.Add((JsonNode)new JsonObject
            {
                ["code"] = s.Code,
                ["title"] = s.Title,
                ["route"] = s.Route,
                ["icon"] = s.IconCode,
            });
        }
        return JsonSerializer.Serialize(new JsonObject
        {
            ["note"] = "起手屏建议（可改可删），不是对产品功能的断言",
            ["screens"] = screens,
        });
    }

    #region DTCG

    /// <summary>行存 → DTCG 树：合成点分路径、$type 继承、别名保持 {path} 字符串</summary>
    public String ToDtcg(Snapshot snap)
    {
        var root = new JsonObject();
        foreach (var t in snap.Tokens)
        {
            var leaf = BuildToken(t);
            Insert(root, t.Path.Split('.'), leaf);
        }
        ApplyTypeInheritance(root);

        var doc = new JsonObject
        {
            ["$schema"] = "https://designtokens.org/schemas/2025.10/format.json",
            ["$extensions"] = new JsonObject
            {
                ["forgeself"] = new JsonObject
                {
                    ["project"] = snap.Project.Code,
                    ["version"] = snap.Project.Version,
                    ["theme"] = snap.ThemeCode,
                    ["generatorVersion"] = DesignSystemConstants.GeneratorVersion,
                    ["projectionVersion"] = DesignSystemConstants.ProjectionVersion,
                    ["modelVersion"] = DesignSystemConstants.ModelVersion,
                },
            },
        };
        foreach (var kv in root.ToList())
        {
            root.Remove(kv.Key);        // JsonNode 同一节点不能同时挂在两个父节点下，必须先摘再挂
            doc[kv.Key] = kv.Value;
        }
        return doc.ToJsonString(JsonOpts);
    }

    static JsonObject BuildToken(Snap t)
    {
        var o = new JsonObject();
        JsonNode value = t.Type switch
        {
            TokenTypes.Shadow or TokenTypes.Typography or TokenTypes.Transition or TokenTypes.Border or TokenTypes.Gradient
                when !t.ValueJson.IsNullOrEmpty() => JsonNode.Parse(t.ValueJson!) ?? JsonValue.Create(t.Value ?? "")!,
            _ => t.AliasPath.IsNullOrEmpty()
                ? JsonValue.Create(t.Value ?? "")!
                : JsonValue.Create("{" + t.AliasPath + "}")!,
        };
        o["$value"] = value;
        o["$type"] = t.Type;
        if (!t.Description.IsNullOrEmpty()) o["$description"] = t.Description;
        if (!t.Extensions.IsNullOrEmpty())
        {
            try { o["$extensions"] = JsonNode.Parse(t.Extensions!); }
            catch (JsonException) { /* 非 JSON 的元数据袋不阻塞导出 */ }
        }
        return o;
    }

    static void Insert(JsonObject root, String[] segments, JsonObject leaf)
    {
        var current = root;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (current[segments[i]] is not JsonObject next)
            {
                next = new JsonObject();
                current[segments[i]] = next;
            }
            current = next;
        }

        var last = segments[^1];
        // 同名既是组又是令牌时（如 duration.base 与 duration.base-reduced 不冲突，但 a 与 a.b 会），
        // 保留已有子组并把叶子挂到 a.a 上（DTCG 不允许同名混用）
        if (current[last] is JsonObject existing && existing.ContainsKey("$value") == false)
            current[segments[^1] + "." + segments[^1]] = leaf;
        else
            current[last] = leaf;
    }

    /// <summary>DTCG 的 $type 沿树继承：组上标一次即可，子令牌不重复写</summary>
    static void ApplyTypeInheritance(JsonNode root)
    {
        Walk(root, null);

        static void Walk(JsonNode node, String? inherited)
        {
            if (node is not JsonObject o) return;
            if (o.ContainsKey("$value"))
            {
                if (inherited != null && o["$type"]?.ToString() == inherited) o.Remove("$type");
                return;
            }
            var childType = o["$type"]?.ToString() ?? inherited;
            foreach (var kv in o.ToList())
                if (kv.Value is JsonObject) Walk(kv.Value, childType);
        }
    }

    #endregion

    #region CSS / Tailwind / SCSS / LESS / TS

    /// <summary>令牌路径 → CSS 变量名（`--ds-` + 路径 `.`→`-`）；供 brief/reviewer/lookup 同源复用（不改行为）</summary>
    public static String CssVarName(String path) => "--ds-" + path.Replace('.', '-');

    public String ToCss(Snapshot snap)
    {
        var sb = new StringBuilder();
        Header(sb, "CSS Custom Properties（DTCG 投影）");
        sb.AppendLine(":root {");
        sb.AppendLine($"  /* forgeself-design-system: {snap.Project.Code} v{snap.Project.Version} theme={snap.ThemeCode ?? "shared"} projection={DesignSystemConstants.ProjectionVersion} */");
        foreach (var t in snap.Tokens)
        {
            if (ShouldSkipCss(t)) continue;
            sb.Append("  ").Append(CssVarName(t.Path)).Append(": ").Append(CssValue(t));
            if (t.Type == TokenTypes.Color && !t.ColorHex.IsNullOrEmpty() && t.AliasPath.IsNullOrEmpty())
                sb.Append("; /* ").Append(t.ColorHex).Append(" */");
            else sb.Append(';');
            sb.AppendLine();
        }
        sb.AppendLine("}");

        AppendReducedMotion(sb, snap);
        AppendFocusVisible(sb, snap);
        AppendFontFaces(sb, snap);
        return sb.ToString();
    }

    /// <summary>
    /// 字体进 CSS 的规矩：**只有真登记了文件**（FileName/FileRef）的行才出 `@font-face`。
    /// 系统字体栈成员没有可分发文件，硬编一个 `src` 就是假声明（浏览器 404、离线环境直接掉字）；
    /// 但它们也不能隐身——许可证与"这份清单从哪来"要以注释写清，消费方才知道该不该自己补文件。
    /// </summary>
    void AppendFontFaces(StringBuilder sb, Snapshot snap)
    {
        var withFile = snap.Fonts.Where(f => !f.FileName.IsNullOrEmpty() || !f.FileRef.IsNullOrEmpty()).ToList();
        var systemStack = snap.Fonts.Except(withFile).ToList();
        if (withFile.Count == 0 && systemStack.Count == 0) return;

        sb.AppendLine();
        foreach (var f in withFile)
        {
            var src = f.FileRef.IsNullOrEmpty() ? f.FileName : f.FileRef;
            sb.AppendLine("@font-face {");
            sb.AppendLine($"  font-family: \"{f.Family}\";");
            sb.AppendLine($"  font-style: {(f.Style.IsNullOrEmpty() ? "normal" : f.Style)};");
            sb.AppendLine($"  font-weight: {f.Weight};");
            sb.AppendLine($"  font-display: {(f.Display.IsNullOrEmpty() ? "swap" : f.Display)};");
            sb.AppendLine($"  src: url(\"{src}\");");
            sb.AppendLine("}");
        }

        if (systemStack.Count > 0)
        {
            sb.AppendLine("/* 以下字族是生成器字体栈的成员（由系统/浏览器提供），不随本产物分发，故不出 @font-face；");
            sb.AppendLine(" * 要自托管就先把字体文件登记进来（带真实许可证），重新导出即生成上面的 @font-face：");
            foreach (var g in systemStack.GroupBy(f => f.Family, StringComparer.Ordinal))
                sb.AppendLine($" *   {g.Key}（字重 {String.Join("/", g.Select(f => f.Weight).Distinct().Order())}）· {g.First().License ?? "许可证未标注"}");
            sb.AppendLine(" */");
        }
    }

    public String ToTailwind(Snapshot snap)
    {
        var sb = new StringBuilder();
        Header(sb, "Tailwind v4 CSS-first 主题（@theme）");
        sb.AppendLine("@theme {");
        foreach (var t in snap.InTier(TokenTiers.Primitive).Concat(snap.InTier(TokenTiers.Semantic)))
        {
            if (ShouldSkipCss(t)) continue;
            var name = TwName(t);
            if (name == null) continue;
            sb.Append("  ").Append(name).Append(": ").Append(CssValue(t)).AppendLine(";");
        }
        sb.AppendLine("}");
        AppendReducedMotion(sb, snap);
        return sb.ToString();
    }

    public String ToScss(Snapshot snap) => Preprocessor(snap, "$", "scss");

    public String ToLess(Snapshot snap) => Preprocessor(snap, "@", "less");

    String Preprocessor(Snapshot snap, String sigil, String flavor)
    {
        var sb = new StringBuilder();
        Header(sb, $"{flavor.ToUpperInvariant()} 变量投影（值已解析，别名以引用形式保留在注释里）");
        foreach (var t in snap.Tokens)
        {
            if (ShouldSkipCss(t)) continue;
            var value = t.AliasPath.IsNullOrEmpty() ? RawValue(t) : RawValue(t);
            if (flavor == "scss" && value.StartsWith('#')) value = Unquote(value);
            sb.Append(sigil).Append(t.Path.Replace('.', '-')).Append(": ").Append(value);
            sb.Append(flavor == "scss" ? ';' : ';');
            if (!t.AliasPath.IsNullOrEmpty()) sb.Append(" // ← {").Append(t.AliasPath).Append('}');
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public String ToTypeScript(Snapshot snap)
    {
        var sb = new StringBuilder();
        Header(sb, "TypeScript 常量对象（含联合类型键名）");
        sb.AppendLine("export const tokens = {");
        foreach (var t in snap.Tokens)
        {
            if (ShouldSkipCss(t)) continue;
            sb.Append("  ").Append(Quote(t.Path)).Append(": ").Append(Quote(RawValue(t))).AppendLine(",");
        }
        sb.AppendLine("} as const;");
        sb.AppendLine();
        sb.AppendLine("export type TokenPath = keyof typeof tokens;");
        sb.AppendLine();
        sb.AppendLine("/** 颜色令牌 → 解析后 hex（供运行时 canvas / 测试断言用） */");
        sb.AppendLine("export const colorHex = {");
        foreach (var t in snap.Tokens.Where(t => t.Type == TokenTypes.Color && !t.ColorHex.IsNullOrEmpty()))
            sb.Append("  ").Append(Quote(t.Path)).Append(": ").Append(Quote(t.ColorHex!)).AppendLine(",");
        sb.AppendLine("} as const;");
        return sb.ToString();
    }

    /// <summary>CSS 投影跳过的行：复合类型（Typography/Transition/Shadow）由专用渲染器出值，CubicBezier 空值跳过</summary>
    public static Boolean ShouldSkipCss(Snap t) => t.Type is TokenTypes.Typography or TokenTypes.Transition or TokenTypes.Shadow
        ? false : t.Type == TokenTypes.CubicBezier && t.Value.IsNullOrEmpty();

    static String CssValue(Snap t)
    {
        if (!t.AliasPath.IsNullOrEmpty())
        {
            var tint = ReadTint(t.Extensions);
            var target = "var(" + CssVarName(t.AliasPath!) + ")";
            if (tint != null) return $"color-mix(in oklab, {target} {Num(tint.Value * 100)}%, transparent)";
            return target;
        }
        return RawValue(t);
    }

    static String RawValue(Snap t) => t.Type switch
    {
        TokenTypes.CubicBezier when !t.ValueJson.IsNullOrEmpty() => BezierCss(t.ValueJson!),
        TokenTypes.Typography when !t.ValueJson.IsNullOrEmpty() => TypographyCss(t),
        // 复合类型的值只存在 ValueJson 里；不展开就会导出 `--ds-shadow-xxx: ;`（空值），
        // 前端换肤与交付方拿到的都是失效声明 —— 2026-09-29 实测发现并修。
        TokenTypes.Shadow when !t.ValueJson.IsNullOrEmpty() => ShadowCss(t.ValueJson!) is { Length: > 0 } s ? s : (t.Value ?? ""),
        TokenTypes.Transition when !t.ValueJson.IsNullOrEmpty() => TransitionCss(t.ValueJson!),
        _ => t.Value ?? "",
    };

    /// <summary>引用型值（{path}）→ var(--ds-...)，与标量别名同一规则</summary>
    static String Ref(JsonNode? node)
    {
        var s = node?.ToString() ?? "";
        return s.StartsWith('{') && s.EndsWith('}') ? "var(" + CssVarName(s[1..^1]) + ")" : s;
    }

    /// <summary>层里的长度/透明度可能是 JSON 数字，也可能是 Num() 写出的字符串，两种都要能吃下</summary>
    static Double Dbl(JsonNode? node)
    {
        if (node == null) return 0;
        try
        {
            return node.GetValueKind() switch
            {
                System.Text.Json.JsonValueKind.Number => node.GetValue<Double>(),
                System.Text.Json.JsonValueKind.String =>
                    Double.TryParse(node.GetValue<String>(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0,
                _ => 0,
            };
        }
        catch (Exception) { return 0; }
    }

    /// <summary>shadow 的 ValueJson 是层数组，逐层拼成合法 box-shadow（alpha&lt;1 用 color-mix 表达）</summary>
    static String ShadowCss(String json)
    {
        try
        {
            var arr = JsonNode.Parse(json)?.AsArray();
            if (arr == null || arr.Count == 0) return "";
            var layers = new List<String>();
            foreach (var item in arr)
            {
                if (item?.AsObject() is not JsonObject o) continue;
                var color = Ref(o["color"]);
                if (color.IsNullOrEmpty()) color = Ref(o["colorAliasPath"]);
                if (color.IsNullOrEmpty()) color = "currentcolor";
                var alpha = Dbl(o["alpha"]);
                var colorCss = alpha > 0 && alpha < 1 ? $"color-mix(in oklab, {color} {Num(alpha * 100)}%, transparent)" : color;
                var inset = o["inset"]?.GetValueKind() == System.Text.Json.JsonValueKind.True ? "inset " : "";
                layers.Add($"{inset}{Num(Dbl(o["offsetX"]))}px {Num(Dbl(o["offsetY"]))}px {Num(Dbl(o["blur"]))}px {Num(Dbl(o["spread"]))}px {colorCss}".Trim());
            }
            return string.Join(", ", layers);
        }
        catch (Exception) { return ""; }
    }

    static String TransitionCss(String json)
    {
        try
        {
            var o = JsonNode.Parse(json)?.AsObject();
            if (o == null) return "";
            var parts = new[] { Ref(o["duration"]), Ref(o["timing"]), Ref(o["delay"]) }.Where(s => !s.IsNullOrEmpty());
            return string.Join(" ", parts);
        }
        catch (Exception) { return ""; }
    }

    static String BezierCss(String json)
    {
        try
        {
            var arr = JsonNode.Parse(json)?.AsArray();
            if (arr == null) return "cubic-bezier(0.2, 0, 0, 1)";
            var nums = arr.Select(x => x?.GetValue<Double>() ?? 0).ToArray();
            return nums.Length == 4 ? $"cubic-bezier({Num(nums[0])}, {Num(nums[1])}, {Num(nums[2])}, {Num(nums[3])})" : "cubic-bezier(0.2, 0, 0, 1)";
        }
        catch (Exception) { return "cubic-bezier(0.2, 0, 0, 1)"; }
    }

    static String TypographyCss(Snap t)
    {
        try
        {
            var o = JsonNode.Parse(t.ValueJson!)?.AsObject();
            var size = o?["fontSize"]?.ToString();
            var lh = o?["lineHeight"]?.ToString();
            var fam = o?["fontFamily"]?.ToString();
            var w = o?["fontWeight"]?.ToString();
            var ls = o?["letterSpacing"]?.ToString();
            var resolve = new Func<String?, String>(s =>
            {
                if (s == null) return "";
                if (!s.StartsWith('{') || !s.EndsWith('}')) return s;
                return "var(" + CssVarName(s[1..^1]) + ")";
            });
            return string.Join(" / ", new[]
            {
                w,
                resolve(size),
                resolve(lh),
            }.Where(x => !x.IsNullOrEmpty())) + (ls.IsNullOrEmpty() ? "" : " " + resolve(ls));
        }
        catch (Exception) { return t.Value ?? ""; }
    }

    static String? TwName(Snap t) => t.Type switch
    {
        TokenTypes.Color when t.Path.StartsWith("color.", StringComparison.Ordinal) => "--color-" + t.Path["color.".Length..].Replace('.', '-'),
        TokenTypes.Color when t.Path.StartsWith("semantic.", StringComparison.Ordinal) => "--color-" + t.Path["semantic.".Length..].Replace('.', '-'),
        TokenTypes.Dimension when t.Path.StartsWith("space.", StringComparison.Ordinal) => "--spacing-" + t.Path["space.".Length..],
        TokenTypes.Dimension when t.Path.StartsWith("radius.", StringComparison.Ordinal) => "--radius-" + t.Path["radius.".Length..],
        TokenTypes.FontFamily => "--font-" + t.Path["font.".Length..],
        TokenTypes.FontWeight when t.Path.StartsWith("weight.") => "--font-weight-" + t.Path["weight.".Length..],
        TokenTypes.Dimension when t.Path.StartsWith("size.") => "--text-" + t.Path["size.".Length..],
        TokenTypes.Number when t.Path.StartsWith("leading.") => "--leading-" + t.Path["leading.".Length..],
        TokenTypes.Dimension when t.Path.StartsWith("tracking.") => "--tracking-" + t.Path["tracking.".Length..],
        TokenTypes.Duration when t.Path.StartsWith("duration.") => "--transition-duration-" + t.Path["duration.".Length..],
        _ => null,
    };

    static void AppendReducedMotion(StringBuilder sb, Snapshot snap)
    {
        var reduced = snap.Tokens.Where(t => t.Type == TokenTypes.Duration && t.Path.EndsWith("-reduced", StringComparison.Ordinal)).ToList();
        if (reduced.Count == 0) return;
        sb.AppendLine();
        sb.AppendLine("/* 可达性：动效可关闭（生成产物，不靠文档提醒） */");
        sb.AppendLine("@media (prefers-reduced-motion: reduce) {");
        sb.AppendLine("  :root {");
        foreach (var r in reduced)
            sb.Append("    ").Append(CssVarName(r.Path[..^"-reduced".Length])).Append(": ").AppendLine($"{r.Value};");
        sb.AppendLine("    --ds-transition-duration: 0.01ms;");
        sb.AppendLine("  }");
        sb.AppendLine("}");
    }

    static void AppendFocusVisible(StringBuilder sb, Snapshot snap)
    {
        if (!snap.Tokens.Any(t => t.Path == "component.focus.outline-width")) return;
        sb.AppendLine();
        sb.AppendLine("/* 焦点可见（WCAG 2.4.7 / 2.4.11）：统一用令牌，避免各组件自写 */");
        sb.AppendLine(":where(a, button, input, select, textarea, [tabindex]):focus-visible {");
        sb.AppendLine("  outline: var(--ds-component-focus-outline-width, 2px) solid var(--ds-component-focus-outline-color, currentColor);");
        sb.AppendLine("  outline-offset: var(--ds-component-focus-outline-offset, 2px);");
        sb.AppendLine("}");
    }

    #endregion

    #region Element Plus 接缝（--ds-* → --el-*，spec U3）

    /// <summary>
    /// EP 主色族 → 我们的语义角色。`error` 与 `danger` 同源：EP 内部两套键都在用，
    /// 只映射一边会让另一半组件停在默认红上。
    /// </summary>
    static readonly (String El, String Token)[] ElColorBase =
    [
        ("primary", "semantic.brand"),
        ("success", "semantic.success"),
        ("warning", "semantic.warning"),
        ("danger", "semantic.danger"),
        ("error", "semantic.danger"),
        ("info", "semantic.info"),
    ];

    /// <summary>
    /// EP 的 `light-N` 定义是"与白混合"、`dark-2` 是"与黑混合"（比例即 N 成反）。
    /// 这里**照它的比例**，但把混合目标换成"本页底色 / 正文墨色"：
    /// 写死 white/black 在深色主题下会把悬停态洗成灰白、把按下态压成死黑 —— 换肤就不跟主题走了。
    /// </summary>
    static readonly (String Suffix, Int32 Keep)[] ElLightSteps = [("light-3", 70), ("light-5", 50), ("light-7", 30), ("light-8", 20), ("light-9", 10)];

    const Int32 ElDarkKeep = 80;

    /// <summary>直接引用型接缝（左：EP 变量；右：我们的令牌路径）。档位按"级别对应级别"选，不按当前值凑数。</summary>
    static readonly (String El, String Token)[] ElDirect =
    [
        ("--el-bg-color", "semantic.surface-1"),
        ("--el-bg-color-page", "semantic.surface-bg"),
        ("--el-bg-color-overlay", "semantic.surface-2"),
        ("--el-fill-color-blank", "semantic.surface-1"),
        ("--el-fill-color-lighter", "semantic.surface-1"),
        ("--el-fill-color-light", "semantic.surface-2"),
        ("--el-fill-color", "semantic.surface-2"),
        ("--el-fill-color-dark", "semantic.surface-3"),
        ("--el-fill-color-darker", "semantic.surface-3"),
        ("--el-text-color-primary", "semantic.text-1"),
        ("--el-text-color-regular", "semantic.text-2"),
        ("--el-text-color-secondary", "semantic.text-3"),
        ("--el-text-color-placeholder", "semantic.text-3"),
        ("--el-text-color-disabled", "semantic.text-3"),
        ("--el-border-color", "semantic.border-1"),
        ("--el-border-color-light", "semantic.border-1"),
        ("--el-border-color-lighter", "semantic.border-1"),
        ("--el-border-color-extra-light", "semantic.border-1"),
        ("--el-border-color-hover", "semantic.border-strong"),
        ("--el-border-color-dark", "semantic.border-strong"),
        ("--el-border-color-darker", "semantic.border-strong"),
        ("--el-disabled-text-color", "semantic.text-3"),
        ("--el-disabled-bg-color", "semantic.surface-2"),
        ("--el-disabled-border-color", "semantic.border-1"),
        ("--el-font-family", "font.sans"),
        ("--el-font-weight-primary", "weight.regular"),
        ("--el-font-line-height-primary", "leading.body"),
        ("--el-font-size-extra-small", "size.caption"),
        ("--el-font-size-small", "size.small"),
        ("--el-font-size-base", "size.body"),
        ("--el-font-size-medium", "size.h5"),
        ("--el-font-size-large", "size.h4"),
        ("--el-font-size-extra-large", "size.h3"),
        ("--el-border-radius-small", "radius.sm"),
        ("--el-border-radius-base", "radius.md"),
        ("--el-border-radius-round", "radius.pill"),
        ("--el-border-radius-circle", "radius.full"),
        ("--el-border-width", "border.hairline"),
        ("--el-box-shadow-lighter", "shadow.elevation-1"),
        ("--el-box-shadow-light", "shadow.elevation-2"),
        ("--el-box-shadow", "shadow.elevation-3"),
        ("--el-box-shadow-dark", "shadow.elevation-4"),
        ("--el-transition-duration", "duration.base"),
        ("--el-transition-duration-fast", "duration.micro"),
        ("--el-transition-function-ease-in-out-bezier", "ease.standard"),
        ("--el-transition-function-fast-bezier", "ease.decelerate"),
        // 组件尺寸：EP 的 default/small/large 高度直接取我们按钮的 min-height（已过 WCAG 2.5.8 下限），不再写死 32/24/40
        ("--el-component-size-small", "component.button.sm.min-height"),
        ("--el-component-size", "component.button.md.min-height"),
        ("--el-component-size-large", "component.button.lg.min-height"),
    ];

    /// <summary>遮罩与叠加色：EP 用带 alpha 的黑/白，这里按同一语义用 `transparent` 混合表达</summary>
    static readonly (String El, String Token, Int32 Keep)[] ElAlphaMix =
    [
        ("--el-mask-color", "semantic.surface-bg", 90),
        ("--el-mask-color-extra-light", "semantic.surface-bg", 30),
        ("--el-overlay-color", "semantic.text-1", 80),
        ("--el-overlay-color-light", "semantic.text-1", 70),
        ("--el-overlay-color-lighter", "semantic.text-1", 50),
    ];

    /// <summary>
    /// 产出 Element Plus 换肤接缝：**右侧只允许引用 `tokens.css` 里真存在的变量**。
    /// 库里没有的档位不发明值，改在文末如实列出（与 M10 的 `unresolvedTokenRefs` 同一条纪律）。
    /// </summary>
    public String ToElementPlus(Snapshot snap)
    {
        var defined = DefinedCssVars(snap);
        var missing = new List<String>();
        var sb = new StringBuilder();
        Header(sb, "Element Plus 换肤接缝（--ds-* → --el-*）");
        sb.AppendLine("/* 用法：先引入 `tokens.<theme>.css`（定义 --ds-*），再引入本文件，EP 组件即按本设计系统上色。");
        sb.AppendLine(" * 本文件不含任何字面色值：右侧只有对 `--ds-*` 令牌的引用（含其 color-mix 派生）。 */");
        sb.AppendLine(":root {");

        void Ref(String el, String token)
        {
            var v = CssVarName(token);
            if (!defined.Contains(v)) { missing.Add($"{el} ← {token}"); return; }
            sb.AppendLine($"  {el}: var({v});");
        }

        void Mix(String el, String token, Int32 keep, String toward)
        {
            var v = CssVarName(token);
            var towardVar = toward == TransparentToward ? null : CssVarName(toward);
            if (!defined.Contains(v) || (towardVar != null && !defined.Contains(towardVar!)))
            {
                missing.Add($"{el} ← color-mix({token} {keep}%, {(towardVar == null ? "transparent" : toward)})");
                return;
            }
            // 混合目标必须是**颜色值**：写成裸的 `--ds-…` 会让整条声明在 computed-value 阶段失效（EP 组件直接掉回默认色）
            var mix = towardVar == null ? "transparent" : $"var({towardVar})";
            sb.AppendLine($"  {el}: color-mix(in oklab, var({v}) {keep}%, {mix});");
        }

        foreach (var (el, token) in ElColorBase)
        {
            Ref($"--el-color-{el}", token);
            foreach (var (suffix, keep) in ElLightSteps) Mix($"--el-color-{el}-{suffix}", token, keep, "semantic.surface-bg");
            Mix($"--el-color-{el}-dark-2", token, ElDarkKeep, "semantic.text-1");

            // EP 用 --el-color-*-rgb 拼 rgba()：值取自投影里已解析的 hex，不另算一套色
            var hex = snap.Tokens.FirstOrDefault(t => t.Path == token)?.ColorHex;
            if (hex != null && TryRgbTriplet(hex, out var rgb)) sb.AppendLine($"  --el-color-{el}-rgb: {rgb};");
            else missing.Add($"--el-color-{el}-rgb ← {token}（无可解析的 hex 有效值）");
        }

        foreach (var (el, token) in ElDirect) Ref(el, token);
        foreach (var (el, token, keep) in ElAlphaMix) Mix(el, token, keep, TransparentToward);
        sb.AppendLine("}");

        if (missing.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"/* 未映射 {missing.Count} 项（本主题投影里没有对应令牌，宁可让 EP 用自己的默认值，也不凭空造一个设计值）：");
            foreach (var m in missing.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
                sb.AppendLine($" *   {m}");
            sb.AppendLine(" * 要补齐就在插件里把这条令牌写进库，重新导出即出现。 */");
        }

        sb.AppendLine();
        sb.AppendLine("/* 有意**不映射**的 EP 变量：");
        sb.AppendLine(" *   --el-color-white / --el-color-black —— EP 把它们当固定前景用（按钮文字等），");
        sb.AppendLine(" *     映射到表面色会把文字压成与背景同色；我们的前景对比度由 component.*.foreground 令牌与审计门禁负责。");
        sb.AppendLine(" *   --el-index-* —— 层级不是设计值；--el-svg-monochrome-grey —— EP 图标自带灰，我们没有对应角色。");
        sb.AppendLine(" *   --el-transition-all/-color/-border/-box-shadow/-fade* —— EP 由 duration + function 组合出来的整串，已给原料。 */");
        return sb.ToString();
    }

    const String TransparentToward = "transparent";

    /// <summary>`tokens.css` 里真定义了的变量名集合（接缝只允许引用这里面的名字）</summary>
    HashSet<String> DefinedCssVars(Snapshot snap)
    {
        var set = new HashSet<String>(StringComparer.Ordinal);
        foreach (var raw in ToCss(snap).Split('\n'))
        {
            var line = raw.TrimStart();
            if (!line.StartsWith("--ds-", StringComparison.Ordinal)) continue;
            var colon = line.IndexOf(':');
            if (colon > 0) set.Add(line[..colon].Trim());
        }
        return set;
    }

    static Boolean TryRgbTriplet(String hex, out String triplet)
    {
        triplet = "";
        var s = hex.TrimStart('#');
        if (s.Length == 3) s = new String([s[0], s[0], s[1], s[1], s[2], s[2]]);
        if (s.Length < 6) return false;
        var r = Convert.ToInt32(s[..2], 16);
        var g = Convert.ToInt32(s[2..4], 16);
        var b = Convert.ToInt32(s[4..6], 16);
        triplet = $"{r}, {g}, {b}";
        return true;
    }

    #endregion


    #region Tokens Studio / DESIGN.md / registry

    /// <summary>Tokens Studio 兼容：$themes + 每个 set 一份 DTCG 文档</summary>
    public String ToTokensStudio(Snapshot snap)
    {
        // 只广播"真的有令牌"的主题：项目可能建了 high-contrast 但还没生成它的语义层，
        // 列进 $themes 等于给下游一个空主题（审计已把它记为 theme-empty 警告）
        var colorThemes = snap.Themes
            .Where(t => t.ModeKind != ThemeModeKinds.Density && _tokens.FindThemed(snap.Project.Id, t.Id).Count > 0)
            .Select(t => t.Code).ToList();
        var doc = new JsonObject
        {
            ["name"] = snap.Project.Name,
            ["disabled"] = false,
            ["lastUpdated"] = DateTime.UtcNow.ToString("O"),
            ["tokens"] = new JsonObject
            {
                [$"$global"] = JsonNode.Parse(ToDtcg(Load(snap.Project.Id, null)))!,
            },
            ["$themes"] = new JsonArray(colorThemes.Select(c => (JsonNode)new JsonObject
            {
                ["id"] = c,
                ["name"] = c,
                ["selectedTokenSets"] = new JsonObject { [$"themes.{c}"] = "enabled" },
                ["group"] = "forgeself",
            }).ToArray()),
            ["sets"] = new JsonObject(),
        };

        foreach (var c in colorThemes)
            doc["sets"]![$"themes.{c}"] = JsonNode.Parse(ToDtcg(Load(snap.Project.Id, c)))!;

        return doc.ToJsonString(JsonOpts);
    }

    /// <summary>Google DESIGN.md 形态：YAML 前置令牌 + 规范正文（可被 npx @google/design.md lint 检查）</summary>
    public String ToDesignMd(Snapshot snap)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"name: {snap.Project.Name}");
        sb.AppendLine($"description: {Esc(snap.Project.Description ?? snap.Project.Name)}");
        sb.AppendLine($"version: {snap.Project.Version}");
        sb.AppendLine("colors:");
        foreach (var c in snap.Tokens.Where(t => t.Type == TokenTypes.Color && !c2(t) && t.AliasPath.IsNullOrEmpty()))
            sb.Append("  ").Append(Safe(c.Path)).Append(": ").AppendLine(c.ColorHex ?? c.Value);
        sb.AppendLine("typography:");
        foreach (var t in snap.Tokens.Where(t => t.Type == TokenTypes.Dimension && t.Path.StartsWith("size.", StringComparison.Ordinal)))
            sb.Append("  ").Append(Safe(t.Path["size.".Length..])).Append(": { fontSize: \"").Append(t.Value).AppendLine("\" }");
        sb.AppendLine("rounded:");
        foreach (var t in snap.Tokens.Where(t => t.Path.StartsWith("radius.", StringComparison.Ordinal)))
            sb.Append("  ").Append(Safe(t.Path["radius.".Length..])).AppendLine($": {{ rounded: \"{t.Value}\" }}");
        sb.AppendLine("spacing:");
        foreach (var t in snap.Tokens.Where(t => t.Path.StartsWith("space.", StringComparison.Ordinal)))
            sb.Append("  ").Append(Safe(t.Path["space.".Length..])).AppendLine($": {t.Value}");
        sb.AppendLine("components:");
        foreach (var group in snap.Tokens.Where(t => t.Tier == TokenTiers.Component && t.Type == TokenTypes.Color)
                     .GroupBy(t => t.Path.Split('.')[1], StringComparer.Ordinal))
        {
            sb.Append("  ").Append(group.Key).AppendLine(":");
            foreach (var t in group)
                sb.Append("    ").Append(Safe(t.Path.Split('.')[2..].Last())).Append(": ").AppendLine(t.ColorHex ?? t.Value);
        }
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"# {snap.Project.Name} 设计契约");
        sb.AppendLine();
        sb.AppendLine($"> 由 OpenForgeSelf 设计系统插件生成；投影 {DesignSystemConstants.ProjectionVersion}；主题 `{snap.ThemeCode ?? "shared"}`。");
        sb.AppendLine();
        sb.AppendLine("## 颜色使用规则");
        sb.AppendLine("- 界面只允许消费上面的语义/组件色；十六进制字面量不得出现在业务样式里。");
        sb.AppendLine("- 正文与背景对比度必须 ≥4.5:1（大字与非文本 ≥3:1，WCAG 2.2 1.4.3/1.4.11）。");
        sb.AppendLine("- 换主题只换 `data-ds-theme`，不改组件样式。");
        sb.AppendLine();
        sb.AppendLine("## 排版与尺度");
        sb.AppendLine("- 字号走模块化比例，跨视口用 fluid `clamp()`，不要写死 px 阶梯。");
        sb.AppendLine("- 间距/圆角/阴影/动效一律取上面表格里的档位，禁止插入中间值。");
        sb.AppendLine();
        sb.AppendLine("## 组件");
        sb.AppendLine("- 组件层令牌是唯一来源；新增状态先加令牌（`component.<名>.<属性>-<状态>`），再写样式。");
        sb.AppendLine("- 所有可交互元素必须有 `:focus-visible` 表现。");
        AppendComponentSpecs(sb, snap);
        AppendBrandSections(sb, snap);
        sb.AppendLine();
        sb.AppendLine("## Do / Don't");
        sb.AppendLine("- Do：用语义角色（`semantic.text-1`）表达意图。");
        sb.AppendLine("- Don't：把 `color.brand.500` 直接写进按钮样式——那样换肤不跟随。");
        return sb.ToString();

        static Boolean c2(Snap t) => t.Path.StartsWith("chart.", StringComparison.Ordinal);
    }

    /// <summary>
    /// 组件规格节：把库里的蓝本（解剖 / 状态 / 变体轴 / 可达性要求）逐条列进 DESIGN.md。
    /// 过去这一节只有"组件层令牌是唯一来源"两条通用提醒 —— 那等于把规格留在了库里，
    /// 下游拿到的是一份没有解剖、没有状态、没有 a11y 约束的"组件"标题（M10 收的就是这一口）。
    /// 目录为空不开子标题：空规格比没规格更像假交付。
    /// </summary>
    static void AppendComponentSpecs(StringBuilder sb, Snapshot snap)
    {
        if (snap.Components.Count == 0) return;

        sb.AppendLine();
        sb.AppendLine($"### 组件规格（{snap.Components.Count} 条蓝本 · {snap.Variants.Count} 个变体格子，由库生成，不是模板文案）");
        foreach (var c in snap.Components)
        {
            var cells = snap.Variants.Where(v => v.ComponentId == c.Id).ToList();
            var states = VariantAxes.Sort(VariantAxes.State, cells.Select(v => v.State));
            var axes = AxisSummary(cells);
            var anatomy = ParseStringArray(AnatomyJson(c.GuidanceJson));
            var refs = ParseStringArray(c.TokenRefsJson);

            sb.AppendLine($"- `{c.Code}`（{c.Name}，{c.Category}{(c.Interactive ? "，可交互" : "")}）：");
            if (anatomy.Count > 0) sb.AppendLine($"  - 解剖：{String.Join(" / ", anatomy)}");
            sb.AppendLine($"  - 状态：{String.Join("、", states.Count > 0 ? states : ["default"])}");
            if (axes.Count > 0) sb.AppendLine($"  - 变体轴：{String.Join("；", axes)}");
            sb.AppendLine($"  - 令牌：{refs.Count} 条（逐条可在 registry.json 的 registryDependencies 里核对）");
            if (!c.A11yNotes.IsNullOrEmpty()) sb.AppendLine($"  - 可达性要求：{c.A11yNotes}");
        }
    }

    /// <summary>
    /// 变体格子上的轴 → `size = sm / md / lg`。只列真有格子的轴值，不为凑齐档位编造状态。
    /// 轴内按 <see cref="VariantAxes"/> 的档位序（字母序会把 `sm / md / lg` 排成 `lg / md / sm`，读起来像随机），
    /// 轴间按已知轴在前、其余字母序；表外的轴/值一律退回字母序。
    /// </summary>
    /// <summary>变体格子上的轴摘要（供 DESIGN.md / brief 复用；不改行为）</summary>
    public static List<String> AxisSummary(IReadOnlyList<DesignComponentVariant> cells)
    {
        var byKey = new Dictionary<String, List<String>>(StringComparer.Ordinal);
        foreach (var cell in cells)
            foreach (var (k, v) in ParseAxes(cell.VariantJson))
            {
                var value = v?.ToString() ?? "";
                if (value.Length == 0) continue;
                if (!byKey.TryGetValue(k, out var list)) byKey[k] = list = [];
                if (!list.Contains(value, StringComparer.Ordinal)) list.Add(value);
            }

        return byKey
            .OrderBy(kv => AxisRank(kv.Key)).ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key} = {String.Join(" / ", VariantAxes.Sort(kv.Key, kv.Value))}")
            .ToList();
    }

    static Int32 AxisRank(String key) => key switch
    {
        VariantAxes.Size => 0,
        VariantAxes.Role => 1,
        VariantAxes.State => 2,
        _ => 3,
    };

    /// <summary>
    /// DESIGN.md 的品牌段：资产清单 / 字体与许可证 / 起手屏。
    /// 三张表**空着就不开这一节** —— 空标题比没有标题更像假交付。
    /// </summary>
    static void AppendBrandSections(StringBuilder sb, Snapshot snap)
    {
        if (snap.Assets.Count == 0 && snap.Fonts.Count == 0 && snap.Screens.Count == 0) return;

        sb.AppendLine();
        sb.AppendLine("## 品牌与资产");
        if (snap.Assets.Count > 0)
        {
            sb.AppendLine($"- 图形资产 {snap.Assets.Count} 条（24×24 网格、颜色一律 `currentColor`，随主题换色而不改文件）：" +
                          String.Join("、", snap.Assets.Select(a => $"`{a.Code}`（{a.Kind}，{a.License ?? "许可证未标注"}）")));
        }
        if (snap.Fonts.Count > 0)
        {
            sb.AppendLine("- 字体与许可证（合规要能一眼答上）：");
            foreach (var g in snap.Fonts.GroupBy(f => f.Family, StringComparer.Ordinal))
            {
                var weights = String.Join("/", g.Select(f => f.Weight).Distinct().Order());
                var selfHost = g.Any(f => !f.FileName.IsNullOrEmpty() || !f.FileRef.IsNullOrEmpty());
                sb.AppendLine($"  - `{g.Key}`（字重 {weights}）：{g.First().License ?? "许可证未标注"}" +
                              (selfHost ? " — 已登记文件，CSS 投影里有 `@font-face`" : " — 字体栈成员，系统提供，不随产物分发"));
            }
        }
        if (snap.Screens.Count > 0)
        {
            sb.AppendLine($"- 起手屏 {snap.Screens.Count} 个（生成器按行业倾向给的建议，不是产品事实）：" +
                          String.Join("、", snap.Screens.Select(s => $"`{s.Route}` {s.Title}")));
        }
    }

    /// <summary>
    /// shadcn 风格 registry：机器可消费的条目清单。
    ///
    /// 条目 = 组件目录（`DesignComponent`）∪ 有 `component.*` 令牌的分组：
    /// 只有令牌没有目录、或目录引用了库里查不到的令牌，都**如实标出来**（`unresolvedTokenRefs`），
    /// 因为"规格在库里、产物里拿不到 a11y 约束和状态清单"正是 M10 要收掉的缺口。
    /// </summary>
    public String ToRegistry(Snapshot snap)
    {
        var tokenPaths = snap.Tokens.Select(t => t.Path).ToHashSet(StringComparer.Ordinal);
        var tokensByCode = new Dictionary<String, List<Snap>>(StringComparer.Ordinal);
        foreach (var t in snap.Tokens.Where(t => t.Tier == TokenTiers.Component))
        {
            var code = t.Path.Split('.')[1];
            if (!tokensByCode.TryGetValue(code, out var list)) tokensByCode[code] = list = new List<Snap>();
            list.Add(t);
        }

        var codes = tokensByCode.Keys.Union(snap.Components.Select(c => c.Code), StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal).ToList();
        var items = new JsonArray();
        foreach (var code in codes)
        {
            var entry = snap.Components.FirstOrDefault(c => c.Code == code);
            var tokenPathsOfCode = tokensByCode.TryGetValue(code, out var l)
                ? l.Select(t => t.Path).ToList() : new List<String>();
            var declared = ParseStringArray(entry?.TokenRefsJson);
            var unresolved = declared.Where(p => !tokenPaths.Contains(p)).OrderBy(x => x, StringComparer.Ordinal).ToList();
            var dependencies = tokenPathsOfCode.Union(declared.Except(unresolved)).Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal).ToList();

            var variants = new JsonArray();
            foreach (var v in snap.Variants.Where(v => entry != null && v.ComponentId == entry.Id)
                         .OrderBy(v => v.SortOrder).ThenBy(v => v.Code, StringComparer.Ordinal))
                variants.Add((JsonNode)new JsonObject
                {
                    ["code"] = v.Code,
                    ["name"] = v.Name,
                    ["state"] = v.State,
                    ["axes"] = ParseAxes(v.VariantJson),
                    ["tokens"] = new JsonArray([.. ParseStringArray(v.TokenRefsJson)
                        .Where(p => tokenPaths.Contains(p)).Select(p => (JsonNode)JsonValue.Create(p)!)]),
                });

            items.Add((JsonNode)new JsonObject
            {
                ["name"] = code,
                ["type"] = "registry:block",
                ["title"] = entry?.Name ?? code,
                ["description"] = entry?.Description.IsNullOrEmpty() == false
                    ? entry!.Description : $"{snap.Project.Name} 的 {code} 组件令牌",
                ["registryDependencies"] = new JsonArray([.. dependencies.Select(p => (JsonNode)JsonValue.Create(p)!)])
                ,
                ["meta"] = new JsonObject
                {
                    ["category"] = entry?.Category,
                    ["interactive"] = entry?.Interactive ?? false,
                    ["inCatalog"] = entry != null,
                    ["a11yNotes"] = entry?.A11yNotes,
                    ["anatomy"] = new JsonArray([.. ParseStringArray(AnatomyJson(entry?.GuidanceJson))
                        .Select(a => (JsonNode)JsonValue.Create(a)!)]),
                    ["tokens"] = new JsonArray([.. tokenPathsOfCode.Select(p => (JsonNode)JsonValue.Create(p)!)]),
                    ["states"] = new JsonArray([.. VariantAxes.Sort(VariantAxes.State, variants.OfType<JsonObject>()
                            .Select(v => v["state"]?.GetValue<String>() ?? ""))
                        .Where(s => s.Length > 0)
                        .Select(s => (JsonNode)JsonValue.Create(s)!)]),
                    ["variants"] = variants,
                    ["unresolvedTokenRefs"] = new JsonArray([.. unresolved.Select(p => (JsonNode)JsonValue.Create(p)!)]),
                },
            });
        }

        return new JsonObject
        {
            ["name"] = snap.Project.Code,
            ["homepage"] = "/design-system",
            ["items"] = items,
        }.ToJsonString(JsonOpts);
    }

    /// <summary>`guidanceJson` 里的 anatomy 段（生成器写的是 `{ anatomy: [...], variants: [...] }`）；供 brief/reviewer 复用</summary>
    public static String? AnatomyJson(String? guidanceJson)
    {
        if (guidanceJson.IsNullOrEmpty()) return null;
        try
        {
            return guidanceJson is not null && JsonNode.Parse(guidanceJson)?["anatomy"] is JsonNode anatomy ? anatomy.ToJsonString() : null;
        }
        catch (JsonException) { return null; }   // 手改坏的 JSON 不许把整个导出带崩，按"没有"处理
    }

    /// <summary>解析 JSON 字符串数组（供 brief/reviewer 复用；不改行为）</summary>
    public static List<String> ParseStringArray(String? json)
    {
        if (json.IsNullOrEmpty()) return [];
        try
        {
            // 用 ToString() 不用 GetValue<String>()：调用方塞进来 `[123]`（数字 id）是合法的，
            // 抛 InvalidOperationException 会把整个导出带崩 —— 元数据袋按"能读成什么就是什么"处理。
            return JsonNode.Parse(json!) is JsonArray arr
                ? arr.Select(n => n?.ToString() ?? "").Where(s => s.Length > 0).ToList()
                : [];
        }
        catch (JsonException) { return []; }
    }

    static JsonObject ParseAxes(String? json)
    {
        var o = new JsonObject();
        if (json.IsNullOrEmpty()) return o;
        try
        {
            if (JsonNode.Parse(json!) is JsonObject src)
                foreach (var kv in src) o[kv.Key] = kv.Value?.DeepClone();
        }
        catch (JsonException) { /* 同上：坏 JSON 按空轴处理 */ }
        return o;
    }

    #endregion

    #region Stardust 兼容投影

    /// <summary>
    /// 参考物的十类逻辑实体（清单端点与明细端点**共用这一份**，两边不可能各说一套）。
    /// </summary>
    public static readonly String[] StardustEntities =
    [
        "design-color", "design-shadow", "design-motion", "design-type-role", "design-spacing", "design-radius",
        "design-icon", "design-component", "design-screen", "design-font-face"
    ];

    /// <summary>尚未实现供给的实体一律不声明（能力面诚实）</summary>
    public static Boolean IsKnownEntity(String? entity) =>
        entity != null && StardustEntities.Contains(entity, StringComparer.OrdinalIgnoreCase);

    /// <summary>与参考物 api/index.json 同形：清单 + 每类实体的真实明细 url（spec FR13）</summary>
    public String ToStardustIndex(Snapshot snap)
    {
        var generated = DateTime.UtcNow.ToString("O");
        var list = StardustEntities.Select(e => new
        {
            entity = e,
            source = "forgeself-design-system",
            generated,
            url = $"/api/design-system/{snap.Project.Id}/{e}.json",
            total = EntityRows(snap, e).Count,
        }).ToArray();

        var payload = new
        {
            project = snap.Project.Code,
            version = snap.Project.Version,
            projectionVersion = DesignSystemConstants.ProjectionVersion,
            note = "本清单由设计系统插件从库中实时生成；字段名与参考物逻辑实体一致，不暴露物理表。",
            entities = list,
        };
        return JsonSerializer.Serialize(payload, JsonOpts);
    }

    /// <summary>
    /// 实体明细（FR13 的 <c>data[]</c> 端点）。清单里写了几行，这里就必须能拿到几行 ——
    /// 两者同源 <see cref="EntityRows"/>，所以"清单 12 行、明细空数组"在结构上不再可能。
    /// </summary>
    public String ToStardustEntity(Snapshot snap, String entity)
    {
        if (!IsKnownEntity(entity))
            throw new ArgumentException($"未知逻辑实体 {entity}，可用：{String.Join(",", StardustEntities)}", nameof(entity));

        var name = StardustEntities.First(e => String.Equals(e, entity, StringComparison.OrdinalIgnoreCase));
        var rows = EntityRows(snap, name);
        return JsonSerializer.Serialize(new
        {
            entity = name,
            source = "forgeself-design-system",
            project = snap.Project.Code,
            version = snap.Project.Version,
            projectionVersion = DesignSystemConstants.ProjectionVersion,
            theme = snap.ThemeCode,
            generated = DateTime.UtcNow.ToString("O"),
            total = rows.Count,
            data = rows,
        }, JsonOpts);
    }

    /// <summary>
    /// 逻辑实体 → 行集合。M10 之前的 <c>CountFor</c> 是另一套计数：
    /// <c>design-component</c> 数的是 component 层**令牌**有几条（不是目录里有几个组件），
    /// <c>design-icon / design-screen / design-font-face</c> 直接写死 0 —— 库里明明有行。
    /// 现在总行数由数据本身决定，声明与供给不可能分叉。
    /// </summary>
    public JsonArray EntityRows(Snapshot snap, String entity) => entity switch
    {
        "design-color" => FromTokens(snap, t => t.Type == TokenTypes.Color, t =>
        {
            var parts = t.Path.Split('.');
            return new JsonObject
            {
                ["code"] = t.Path,
                ["family"] = parts.Length > 1 ? parts[1] : "custom",
                ["step"] = parts.Length > 2 ? parts[^1] : parts[0],
                ["mode"] = snap.ThemeCode ?? "both",
                ["tier"] = t.Tier,
                ["value"] = t.ColorHex ?? t.Value,
                ["colorSpace"] = "oklch",
                ["alias"] = t.AliasPath is null ? null : JsonValue.Create(RenderAlias(t.AliasPath)),
                ["usage"] = t.Description,
            };
        }),
        "design-shadow" => FromTokens(snap, t => t.Type == TokenTypes.Shadow, t => new JsonObject
        {
            ["code"] = t.Path,
            ["mode"] = snap.ThemeCode ?? "both",
            ["layers"] = LayerCount(t.ValueJson),
            ["css"] = t.ValueJson.IsNullOrEmpty() ? t.Value : ShadowCss(t.ValueJson!),
        }),
        "design-motion" => FromTokens(snap, t => t.Type is TokenTypes.Duration or TokenTypes.CubicBezier, t => new JsonObject
        {
            ["code"] = t.Path,
            ["mode"] = snap.ThemeCode ?? "both",
            ["kind"] = t.Type,
            ["value"] = RawValue(t),
        }),
        "design-type-role" => FromTokens(snap, t => t.Type == TokenTypes.Typography, t => new JsonObject
        {
            ["code"] = t.Path,
            ["role"] = t.Path.Split('.')[^1],
            ["mode"] = snap.ThemeCode ?? "both",
            ["css"] = t.ValueJson.IsNullOrEmpty() ? t.Value : TypographyCss(t),
        }),
        "design-spacing" => FromTokens(snap, t => t.Path.StartsWith("space.", StringComparison.Ordinal), t => new JsonObject
        {
            ["code"] = t.Path,
            ["mode"] = snap.ThemeCode ?? "both",
            ["value"] = t.Value,
        }),
        "design-radius" => FromTokens(snap, t => t.Path.StartsWith("radius.", StringComparison.Ordinal), t => new JsonObject
        {
            ["code"] = t.Path,
            ["mode"] = snap.ThemeCode ?? "both",
            ["value"] = t.Value,
        }),
        "design-icon" => FromRows(snap.Icons, i => new JsonObject
        {
            ["code"] = i.Code,
            ["name"] = i.Name,
            ["collection"] = i.Collection,
            ["grid"] = i.GridPx,
            ["viewBox"] = i.ViewBox,
            ["sizes"] = i.Sizes,
            ["strokeWidth"] = i.StrokeWidth,
            ["tags"] = i.Tags,
            ["usage"] = i.Usage,
            ["license"] = i.License,
            ["svg"] = i.SvgBody,
        }),
        "design-component" => FromRows(snap.Components, c =>
        {
            var cells = snap.Variants.Where(v => v.ComponentId == c.Id).ToList();
            var o = new JsonObject
            {
                ["code"] = c.Code,
                ["name"] = c.Name,
                ["category"] = c.Category,
                ["interactive"] = c.Interactive,
                ["description"] = c.Description,
                ["a11yNotes"] = c.A11yNotes,
                ["anatomy"] = StringArray(ParseStringArray(AnatomyJson(c.GuidanceJson))),
                ["tokens"] = StringArray(ParseStringArray(c.TokenRefsJson)),
                ["states"] = StringArray(VariantAxes.Sort(VariantAxes.State, cells.Select(v => v.State))),
                ["axes"] = AxesObject(cells),
                ["variants"] = new JsonArray([.. cells.Select(v => (JsonNode)new JsonObject
                {
                    ["code"] = v.Code,
                    ["name"] = v.Name,
                    ["state"] = v.State,
                    ["axes"] = ParseAxes(v.VariantJson),
                    ["tokens"] = StringArray(ParseStringArray(v.TokenRefsJson)),
                })]),
            };
            return o;
        }),
        "design-screen" => FromRows(snap.Screens, s => new JsonObject
        {
            ["code"] = s.Code,
            ["title"] = s.Title,
            ["route"] = s.Route,
            ["icon"] = s.IconCode,
            ["components"] = StringArray(ParseStringArray(s.ComponentIdsJson)),
            ["description"] = s.Description,
            ["notes"] = s.Notes,
        }),
        "design-font-face" => FromRows(snap.Fonts, f => new JsonObject
        {
            ["family"] = f.Family,
            ["weight"] = f.Weight,
            ["style"] = f.Style,
            ["role"] = f.Role,
            ["display"] = f.Display,
            ["fileName"] = f.FileName,
            ["fileRef"] = f.FileRef,
            ["sourceUrl"] = f.SourceUrl,
            ["license"] = f.License ?? "未标注",
            ["selfHosted"] = !f.FileName.IsNullOrEmpty() || !f.FileRef.IsNullOrEmpty(),
        }),
        _ => throw new ArgumentException($"未知逻辑实体 {entity}", nameof(entity)),
    };

    static JsonArray FromTokens(Snapshot snap, Func<Snap, Boolean> where, Func<Snap, JsonObject> map) =>
        FromRows(snap.Tokens.Where(where).ToList(), map);

    static JsonArray FromRows<T>(IReadOnlyList<T> rows, Func<T, JsonObject> map)
    {
        var a = new JsonArray();
        foreach (var r in rows) a.Add(map(r));
        return a;
    }

    static JsonArray StringArray(IReadOnlyList<String> values) =>
        new JsonArray([.. values.Select(v => (JsonNode)JsonValue.Create(v)!)]);

    /// <summary>轴 → 该轴上真实存在的档位（只汇总真有格子的值，不补没证据的档位）</summary>
    static JsonObject AxesObject(IReadOnlyList<DesignComponentVariant> cells)
    {
        var o = new JsonObject();
        foreach (var kv in AxisSummary(cells))
        {
            var i = kv.IndexOf(" = ", StringComparison.Ordinal);
            if (i <= 0) continue;
            o[kv[..i]] = StringArray(kv[(i + 3)..].Split(" / "));
        }
        return o;
    }

    static Int32 LayerCount(String? json)
    {
        if (json.IsNullOrEmpty()) return 0;
        try { return JsonNode.Parse(json!)?.AsArray().Count ?? 0; }
        catch (JsonException) { return 0; }
    }


    /// <summary>
    /// 参考物同款 SQL 种子。**逐表独立 DELETE**——参考物那份 `design-system.data.sql:5` 从第 2 张表起是非法 SQL，
    /// 我们生成的必须能真跑，不照抄缺陷。
    /// </summary>
    public String ToStardustSql(Snapshot snap)
    {
        var sb = new StringBuilder();
        sb.AppendLine("-- 由 OpenForgeSelf 设计系统插件生成（projection " + DesignSystemConstants.ProjectionVersion + "）");
        sb.AppendLine($"DELETE FROM DesignColor;");
        sb.AppendLine($"DELETE FROM DesignShadow;");
        sb.AppendLine("BEGIN TRANSACTION;");

        var ordinal = 0;
        foreach (var t in snap.Tokens.Where(t => t.Type == TokenTypes.Color && t.AliasPath.IsNullOrEmpty()))
        {
            ordinal++;
            var parts = t.Path.Split('.');
            var family = parts.Length >= 2 ? parts[1] : "custom";
            var step = parts.Length >= 3 ? parts[2] : parts[^1];
            sb.Append("INSERT INTO DesignColor (Ordinal, Code, Family, Step, Mode, Value, ColorSpace, OklchL, OklchC, OklchH, Alpha, AliasOf, IsPrimary, Usage) VALUES (")
              .Append(ordinal).Append(", ")
              .Append(Sql(t.Path)).Append(", ").Append(Sql(family)).Append(", ").Append(Sql(step)).Append(", ")
              .Append(Sql(snap.ThemeCode ?? "both")).Append(", ").Append(Sql(t.ColorHex ?? t.Value)).Append(", 'oklch', ")
              .Append(Num(t.ColorHex == null ? 0 : Oklch.ParseHex(t.ColorHex)!.Value.L)).Append(", ")
              .Append(Num(t.ColorHex == null ? 0 : Oklch.ParseHex(t.ColorHex)!.Value.C)).Append(", ")
              .Append(Num(t.ColorHex == null ? 0 : Oklch.ParseHex(t.ColorHex)!.Value.H)).Append(", 1.0, NULL, ")
              .Append(family == "brand" && step == "500" ? 1 : 0).Append(", ").Append(Sql(t.Description ?? "")).AppendLine(");");
        }

        foreach (var t in snap.Tokens.Where(t => t.Type == TokenTypes.Shadow))
        {
            ordinal++;
            sb.Append("INSERT INTO DesignShadow (Ordinal, Code, Mode, Layer, IsInset, OffsetX, OffsetY, Blur, Spread, Color, Alpha, Usage) VALUES (")
              .Append(ordinal).Append(", ").Append(Sql(t.Path)).Append(", ").Append(Sql(snap.ThemeCode ?? "both"))
              .Append(", 0, 0, 0, ").Append(Sql(t.Value)).AppendLine(", '', 1.0, '复合值见 Value 列');");
        }

        sb.AppendLine("COMMIT;");
        return sb.ToString();
    }

    /// <summary>反向产出 XCode EntityModel XML（参考物的 design-system.xml 是手写的，我们让它由库生成，防漂移）</summary>
    public String ToXcodeModel(Snapshot snap)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<EntityModel xmlns:xs=\"http://www.w3.org/2001/XMLSchema\" xmlns=\"https://newlifex.com/Model202509.xsd\" ModelVersion=\"1.0\">");
        sb.AppendLine("  <Option>");
        sb.AppendLine($"    <Namespace>{Esc(snap.Project.Code)}.Design</Namespace>");
        sb.AppendLine($"    <ConnName>{Esc(snap.Project.Code)}Design</ConnName>");
        sb.AppendLine("    <Output>Entities</Output>");
        sb.AppendLine("  </Option>");
        sb.AppendLine("  <Tables>");
        sb.AppendLine("""    <Table Name="DesignColor" Description="设计颜色">""");
        sb.AppendLine("      <Columns>");
        sb.AppendLine("        <Column Name=\"Id\" DataType=\"Int64\" Identity=\"True\" PrimaryKey=\"True\" Description=\"ID\" />");
        foreach (var (name, type) in new[]
                 {
                     ("Code", "String"), ("Family", "String"), ("Step", "String"), ("Mode", "String"), ("Value", "String"),
                     ("ColorSpace", "String"), ("OklchL", "Double"), ("OklchC", "Double"), ("OklchH", "Double"), ("Alpha", "Double"),
                     ("AliasOf", "String"), ("IsPrimary", "Boolean"), ("Usage", "String"),
                 })
            sb.AppendLine($"        <Column Name=\"{name}\" DataType=\"{type}\" Length=\"200\" Description=\"{name}\" />");
        sb.AppendLine("      </Columns>");
        sb.AppendLine("    </Table>");
        sb.AppendLine("  </Tables>");
        sb.AppendLine("</EntityModel>");
        return sb.ToString();
    }

    #endregion

    #region 工具

    static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    String Manifest(Snapshot snap)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {snap.Project.Name} 设计系统导出包");
        sb.AppendLine();
        sb.AppendLine($"- 项目：`{snap.Project.Code}` 版本 `{snap.Project.Version}`");
        sb.AppendLine($"- 生成器 {DesignSystemConstants.GeneratorVersion} / 模型 {DesignSystemConstants.ModelVersion} / 投影 {DesignSystemConstants.ProjectionVersion}");
        sb.AppendLine($"- 令牌数：{snap.Tokens.Count}（primitive {snap.InTier(TokenTiers.Primitive).Count()}、semantic {snap.InTier(TokenTiers.Semantic).Count()}、component {snap.InTier(TokenTiers.Component).Count()}）");
        sb.AppendLine($"- 主题：{String.Join(", ", snap.Themes.Select(t => t.Code))}");
        sb.AppendLine();
        sb.AppendLine("## 目录");
        sb.AppendLine("| 路径 | 用途 |");
        sb.AppendLine("|---|---|");
        sb.AppendLine("| `<project>.tokens.json` | W3C DTCG 2025.10 交换格式（Style Dictionary v4+ 直读） |");
        sb.AppendLine("| `css/tokens.<theme>.css` | CSS 自定义属性 + reduced-motion + focus-visible |");
        sb.AppendLine("| `tailwind/theme.<theme>.css` | Tailwind v4 `@theme` |");
        sb.AppendLine("| `scss/`, `less/`, `ts/` | 预处理器与 TS 常量 |");
        sb.AppendLine("| `element-plus/theme.<theme>.css` | Element Plus 换肤接缝（`--el-*` 只引用 `--ds-*`，不含字面色值） |");
        sb.AppendLine("| `design-md/DESIGN.<theme>.md` | 可 lint 的设计契约文档 |");
        sb.AppendLine("| `<project>.tokens-studio.json` | Tokens Studio `$themes`/sets（Figma Variables 同构） |");
        sb.AppendLine("| `registry.json` | shadcn 风格条目清单 |");
        sb.AppendLine("| `stardust/` | 参考物兼容投影：index.json / data.sql / design-system.xml |");
        sb.AppendLine("| `brand/` | 品牌工件：图形资产逐文件 `.svg` + `fonts.json`（含许可证）+ `screens.json` |");
        sb.AppendLine("| `agent-rules.md` | 可粘贴进目标项目 AGENTS.md / CLAUDE.md / .cursorrules 的接入规则 |");
        sb.AppendLine("| `brief/BRIEF.<theme>.md` | 设计说明书（按非密度主题逐份；唯一真源声明 + 使用规则 + 令牌速查） |");
        sb.AppendLine();
        sb.AppendLine($"## 品牌工件计数（来自库里登记，不是模板占位）");
        sb.AppendLine();
        sb.AppendLine($"- 图形资产 {snap.Assets.Count} 条 → `brand/*.svg`；字体 {snap.Fonts.Count} 行（其中登记了文件、会出 `@font-face` 的 " +
                      $"{snap.Fonts.Count(f => !f.FileName.IsNullOrEmpty() || !f.FileRef.IsNullOrEmpty())} 行）；起手屏 {snap.Screens.Count} 个");
        sb.AppendLine();
        sb.AppendLine("## 注意");
        sb.AppendLine("- 别名在 CSS 里保持为 `var(...)` 引用，改一处即可全站跟随；tint 类令牌编译成 `color-mix(in oklab, …)`。");
        sb.AppendLine("- 包内不含任何数据库表形状：库结构变了这些工件也不必变。");
        return sb.ToString();
    }

    static ExportedFile Json(String name, String body) => new(name, "application/json", Encoding.UTF8.GetBytes(body));

    static ExportedFile Text(String name, String contentType, String body) => new(name, contentType, Encoding.UTF8.GetBytes(body));

    static void Header(StringBuilder sb, String what)
    {
        sb.AppendLine($"/* {what} —— 由 OpenForgeSelf 设计系统插件生成");
        sb.AppendLine($" * projection {DesignSystemConstants.ProjectionVersion}｜generator {DesignSystemConstants.GeneratorVersion}｜请勿手改：回插件里改令牌 */");
    }

    static String Unquote(String v) => v;

    static Double? ReadTint(String? extensions)
    {
        if (extensions.IsNullOrEmpty()) return null;
        try
        {
            using var doc = JsonDocument.Parse(extensions!);
            if (doc.RootElement.TryGetProperty("forgeself", out var f) && f.TryGetProperty("tint", out var t) && t.TryGetDouble(out var v)) return v;
        }
        catch (JsonException) { }
        return null;
    }

    static String Sql(String? v) => v == null ? "NULL" : "'" + v.Replace("'", "''") + "'";

    static String Quote(String v) => JsonSerializer.Serialize(v);

    static String Safe(String v) => new(v.Where(ch => Char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.').ToArray());

    static String Esc(String v) => v.Replace("\r", " ").Replace("\n", " ").Replace(':', ' ');

    static String Num(Double v) => Math.Round(v, 4).ToString("0.####", CultureInfo.InvariantCulture);

    #endregion
}
