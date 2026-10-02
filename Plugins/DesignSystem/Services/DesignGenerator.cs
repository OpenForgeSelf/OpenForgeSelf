using System.Globalization;
using System.Text.Json;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using NewLife;
using XCode;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>生成请求（全部为参数，可复现）。</summary>
public sealed class GenerationRequest
{
    /// <summary>需求描述文本。可空——为空即纯参数模式。只用于**确定性**推导种子色相与行业倾向，不宣称色彩符号学</summary>
    public String? Brief { get; set; }

    /// <summary>显式种子色（hex 或 oklch）。给了它就以它为准，忽略 brief 推到的色相</summary>
    public String? SeedColor { get; set; }

    /// <summary>直接给色相（0~360），优先级低于 SeedColor</summary>
    public Double? Hue { get; set; }

    /// <summary>强调色相偏移（度），默认 168（近似互补，参考物用 +282 但那个值是为紫色定制的）</summary>
    public Double AccentHueOffset { get; set; } = 168;

    /// <summary>以下可空项 = 用户显式覆盖；不给则用行业倾向推导值</summary>
    public Double? Chroma { get; set; }
    public String? Density { get; set; }
    public Double? TypeRatio { get; set; }
    public Double? TypeBasePx { get; set; }
    public Double? RadiusBase { get; set; }
    public Double? MotionScale { get; set; }
    public String? BrandName { get; set; }

    /// <summary>要生成语义层的主题（色向）。空则 light + dark</summary>
    public List<String> Themes { get; set; } = ["light", "dark"];

    /// <summary>行业倾向覆盖：null=由 brief 推断</summary>
    public String? Industry { get; set; }
}

/// <summary>生成产物</summary>
public sealed class GenerationResult
{
    public String Seed { get; set; } = "";
    public String Industry { get; set; } = "general";
    public Double Hue { get; set; }
    public List<TokenPatch> Shared { get; } = new();
    /// <summary>主题码 → 该主题的语义覆盖层令牌</summary>
    public Dictionary<String, List<TokenPatch>> Themed { get; } = new(StringComparer.Ordinal);
    /// <summary>主题码 → shadow 复合令牌（含展开层）</summary>
    public Dictionary<String, IReadOnlyList<ScaleGenerators.ShadowToken>> Shadows { get; } = new(StringComparer.Ordinal);
    /// <summary>提示（如 brief 未给种子色时按哈希定色相），不算错误但要可见</summary>
    public List<String> Notes { get; } = new();
    /// <summary>因人工修改保护而未覆盖的行数：跳过必须可见，否则"生成成功但库里没变"就是一次静默失败（AC18）</summary>
    public Int32 SkippedProtected { get; set; }
    /// <summary>同路径冲突提示（界面原样列出，不翻译不美化）</summary>
    public List<String> Conflicts { get; } = new();
    public Int32 Total => Shared.Count + Themed.Values.Sum(t => t.Count);
}

/// <summary>组件目录落地结果：两个数字都要回给界面，"生成了但目录没变"必须是可见的</summary>
public sealed record ComponentSeed(Int32 Components, Int32 Variants);

/// <summary>品牌层登记结果（字体/页面/资产三张表各落了几条）</summary>
public sealed record BrandSeed(Int32 Fonts, Int32 Screens, Int32 Assets);

/// <summary>
/// 确定性设计系统生成器：种子色 + 参数 → 三层令牌 + 多主题语义层 + 组件层引用。
///
/// 与 v1 的关键差别（v1 见 generate.ts:90-218 / generate.ts:23-61）：
/// - 非色彩尺度按参数计算，不同参数得不同值，不再是全局常量表；
/// - 语义角色由对比度反查 tone（<see cref="SemanticResolver"/>），不是固定 8 键色表；
/// - 同输入必同输出（种子参数写进每行 GeneratorSeed，可复现可 diff）。
/// </summary>
public static class DesignGenerator
{
    /// <summary>行业倾向 → 参数包（影响的是尺度/比例/彩度这些可解释的设计判断，不是"某个行业该用什么颜色"）</summary>
    static readonly Dictionary<String, (Double Ratio, Double Chroma, Double Radius, String Density, Double Motion)> Industries = new(StringComparer.Ordinal)
    {
        ["devtools"] = (1.2, 0.17, 6, "compact", 0.85),
        ["finance"] = (1.15, 0.13, 4, "default", 0.8),
        ["healthcare"] = (1.25, 0.15, 10, "default", 1),
        ["education"] = (1.3, 0.22, 12, "comfortable", 1.15),
        ["commerce"] = (1.25, 0.2, 8, "default", 1),
        ["media"] = (1.4, 0.24, 4, "default", 1.2),
        ["general"] = (1.25, 0.19, 6, "default", 1),
    };

    static readonly (String Keyword, String Industry)[] IndustryClues =
    [
        ("控制台", "devtools"), ("运维", "devtools"), ("devops", "devtools"), ("监控", "devtools"), ("集群", "devtools"), ("api", "devtools"),
        ("银行", "finance"), ("金融", "finance"), ("支付", "finance"), ("账单", "finance"), ("风控", "finance"),
        ("医院", "healthcare"), ("医疗", "healthcare"), ("患者", "healthcare"), ("护理", "healthcare"),
        ("课堂", "education"), ("学生", "education"), ("课程", "education"), ("儿童", "education"),
        ("商城", "commerce"), ("电商", "commerce"), ("商品", "commerce"), ("购物车", "commerce"), ("订单", "commerce"),
        ("视频", "media"), ("媒体", "media"), ("资讯", "media"), ("直播", "media"), ("内容", "media"),
    ];

    public static GenerationResult Generate(GenerationRequest req)
    {
        req ??= new GenerationRequest();
        var brief = (req.Brief ?? "").Trim();
        var industry = ResolveIndustry(req, brief);
        var (indRatio, indChroma, indRadius, indDensity, indMotion) = Industries[industry];

        // 参数优先级：显式请求 > 行业倾向。v1 只有"行业关键词"一层，这里参数才是主角、行业只是缺省值
        var ratio = req.TypeRatio ?? indRatio;
        var chroma = req.Chroma ?? indChroma;
        var radius = req.RadiusBase ?? indRadius;
        var density = req.Density.IsNullOrEmpty() ? indDensity : req.Density!;
        var motion = req.MotionScale ?? indMotion;
        var basePx = req.TypeBasePx is > 0 ? req.TypeBasePx.Value : 16.0;

        // 种子色相优先级：显式色 > 显式色相 > brief 的稳定哈希（明确告知：哈希不带语义判断）
        Double hue;
        String seedSource;
        Oklch.Color? seedColor;
        var parsed = ColorRampGenerator.SeedColorOrNull(req.SeedColor);
        if (parsed != null) { seedColor = parsed; hue = Oklch.NormalizeHue(parsed.Value.H); if (parsed.Value.C > 0.01) chroma = parsed.Value.C; seedSource = "seed-color"; }
        else if (req.Hue != null) { seedColor = null; hue = Oklch.NormalizeHue(req.Hue.Value); seedSource = "hue"; }
        else { seedColor = null; hue = StableHue(brief); seedSource = "brief-hash"; }
        var anchorStep = seedColor == null ? null : ColorRampGenerator.NearestStep(seedColor.Value.L);

        var result = new GenerationResult
        {
            Industry = industry,
            Hue = Math.Round(hue, 2),
            Seed = BuildSeed(req, industry, hue, chroma, ratio, radius, density, motion, basePx),
        };
        if (seedSource == "brief-hash" && brief.Length > 0)
            result.Notes.Add($"未给种子色，色相按需求文本稳定哈希取 {Math.Round(hue, 1)}°（同文本必同色，不含色彩心理学判断）");

        var opts = new ScaleOptions(density, radius, 1, motion);
        var typeOpts = new TypeOptions(basePx, ratio, 320, 1280, 0.35);

        // 1) primitive 色阶：族序 = ColorFamilies.All（同一张表经 /meta 供给界面排色阶条带）。
        //    逐族产出按表遍历（不靠 Dictionary 的枚举序），产出的语义层也从同一份 ramps 选 tone。
        var ramps = ColorFamilies.All.ToDictionary(f => f,
            f => RampFor(f, hue, chroma, req.AccentHueOffset, seedColor, anchorStep), StringComparer.Ordinal);
        foreach (var family in ColorFamilies.All)
            foreach (var s in ramps[family])
                result.Shared.Add(new TokenPatch
                {
                    Path = $"color.{family}.{s.Step}",
                    Tier = TokenTiers.Primitive,
                    Type = TokenTypes.Color,
                    Value = s.Hex,
                    ValueJson = JsonSerializer.Serialize(new { colorSpace = "oklch", components = new[] { Num(s.Oklch.L), Num(s.Oklch.C), Num(Oklch.NormalizeHue(s.Oklch.H)) }, alpha = 1 }),
                    AliasPath = null,
                    Group = family,
                    Name = $"{family}-{s.Step}",
                    Description = s.Clamped ? $"该阶在 sRGB 内彩度受限（maxC≈{Num(s.MaxChroma)}）" : null,
                    Generator = TokenGenerators.Ramp,
                    GeneratorSeed = result.Seed,
                    Extensions = JsonSerializer.Serialize(new { forgeself = new { hue = Num(hue), chroma = Num(chroma), maxChroma = Num(s.MaxChroma), clamped = s.Clamped } }),
                });

        // 2) 非色 primitive：尺度/排版/动效/断点（全部随参数变）
        result.Shared.AddRange(ScaleGenerators.Spacing(opts));
        result.Shared.AddRange(ScaleGenerators.Radius(opts));
        result.Shared.AddRange(ScaleGenerators.Border(opts));
        result.Shared.AddRange(ScaleGenerators.Motion(opts));
        result.Shared.AddRange(ScaleGenerators.Breakpoints(opts));
        result.Shared.AddRange(ChartSeriesPalette());
        var type = TypographyGenerator.Generate(typeOpts);
        // 排版的 primitive（字族/字重/比例）进共享层，type.* 角色是语义层，随主题一致故也放共享
        result.Shared.AddRange(type);

        // 3) 每个主题一层覆盖：色向主题铺语义别名（对比度定向选 tone），密度主题铺尺度覆盖
        foreach (var theme in req.Themes.Distinct(StringComparer.Ordinal).DefaultIfEmpty("light"))
        {
            var themeDensity = DensityOfTheme(theme);
            if (themeDensity != null)
            {
                // 密度是 mode 轴：切到 compact 必须真的改变 space./radius./duration. 的有效值，
                // 否则"主题"只是装饰（用户要的是能用，不是看起来多）。
                var dopts = opts with { Density = themeDensity };
                var scales = new List<TokenPatch>();
                scales.AddRange(ScaleGenerators.Spacing(dopts));
                scales.AddRange(ScaleGenerators.Radius(dopts));
                scales.AddRange(ScaleGenerators.Border(dopts));
                scales.AddRange(ScaleGenerators.Motion(dopts));
                result.Themed[theme] = scales;
                result.Notes.Add($"主题 {theme} 按密度轴生成（density={themeDensity}，铺 {scales.Count} 条尺度覆盖，不铺语义色）");
                continue;
            }

            var choices = SemanticResolver.Resolve(ramps, theme);
            var list = new List<TokenPatch>();
            foreach (var c in choices)
            {
                list.Add(new TokenPatch
                {
                    Path = $"semantic.{c.Role}",
                    Tier = TokenTiers.Semantic,
                    Type = TokenTypes.Color,
                    Value = null,               // 纯别名：值由 primitive 解析而来，不双写
                    AliasPath = c.TokenPath,
                    Group = "semantic",
                    Name = c.Role,
                    Generator = TokenGenerators.Derived,
                    GeneratorSeed = result.Seed,
                    Extensions = JsonSerializer.Serialize(new { forgeself = new { against = c.AgainstPath, ratio = Num(c.Ratio), required = Num(c.Required), satisfied = c.Satisfied } }),
                });
            }

            var shadows = ScaleGenerators.Shadows(opts, theme);
            result.Shadows[theme] = shadows;
            foreach (var s in shadows)
                list.Add(new TokenPatch
                {
                    Path = $"shadow.{s.Name}",
                    Tier = TokenTiers.Semantic,
                    Type = TokenTypes.Shadow,
                    Value = ShadowCss(s),
                    ValueJson = JsonSerializer.Serialize(s.Layers.Select(l => new { offsetX = Num(l.OffsetX), offsetY = Num(l.OffsetY), blur = Num(l.Blur), spread = Num(l.Spread), color = l.ColorValue, alpha = Num(l.Alpha), inset = l.IsInset })),
                    Group = "shadow",
                    Name = s.Name,
                    Description = s.Note,
                    Generator = TokenGenerators.Derived,
                    GeneratorSeed = result.Seed,
                });

            result.Themed[theme] = list;
        }

        // 4) component 层：只引用 semantic（绝不直连 primitive，违规会被审计抓）
        result.Shared.AddRange(ComponentTokens());
        return result;
    }

    /// <summary>
    /// 把生成结果写进库。**写入顺序按层**：primitive/scale → 各主题 semantic → component。
    /// 顺序不能颠倒：component 全部靠别名指向 semantic，若先写 component 再写 semantic，
    /// 第一批的别名校验就会因"目标还不存在"而整批回滚（实测踩过：生成后库里 0 行）。
    /// 任一批校验失败即抛出，把诊断带给调用方，绝不静默吞。
    /// </summary>
    public static GenerationResult ApplyToProject(TokenRepository tokens, DesignProjectService projects, Int64 projectId, GenerationRequest req, Boolean overwrite)
    {
        var result = Generate(req);

        // 第一批：共享层里"自给自足"的令牌（primitive 全部 + 带字面值的 semantic，如 type.*）
        Write(tokens, projectId, DesignSystemConstants.SharedThemeId,
            result.Shared.Where(p => !NeedsThemedTarget(p)).ToList(), overwrite, result);

        foreach (var (theme, patches) in result.Themed)
        {
            var themeId = projects.ResolveThemeId(projectId, theme);
            if (themeId == DesignSystemConstants.SharedThemeId && !theme.Equals("shared", StringComparison.OrdinalIgnoreCase))
                throw new KeyNotFoundException($"主题 {theme} 不存在，先生成主题");

            // 密度/品牌轴不是配色轴：它们的覆盖层只装尺度，绝不铺语义色（否则 mode 轴退化成"另一套配色"）。
            // Generate() 已按轴分派内容，这里只是防御旧调用方直接塞进来的错层补丁。
            var themeEntity = projects.FindTheme(themeId);
            var layer = patches;
            if (themeEntity != null && !themeEntity.ModeKind.Equals(ThemeModeKinds.Color, StringComparison.OrdinalIgnoreCase))
            {
                layer = patches.FindAll(p => !(p.Tier == TokenTiers.Semantic && p.Type == TokenTypes.Color));
                if (layer.Count != patches.Count)
                    result.Notes.Add($"主题 {theme} 是{themeEntity.ModeKind}轴，已剔除 {patches.Count - layer.Count} 条语义色补丁");
                if (layer.Count == 0) continue;
            }

            foreach (var p in layer) p.ThemeId = themeId;
            Write(tokens, projectId, themeId, layer, overwrite, result);

            // 复合 shadow 的展开层必须与 ValueJson 成对落库，否则查询投影与真源脱节（design G6）
            if (!result.Shadows.TryGetValue(theme, out var shadowTokens)) continue;
            foreach (var s in shadowTokens)
            {
                var entity = tokens.Find(projectId, themeId, $"shadow.{s.Name}");
                if (entity != null) tokens.ReplaceShadowLayers(entity, s.Layers);
            }
        }

        // 最后一批：所有要靠 semantic 中转的令牌（component 层 + chart 系列色等共享 semantic 别名）
        Write(tokens, projectId, DesignSystemConstants.SharedThemeId,
            result.Shared.Where(NeedsThemedTarget).ToList(), overwrite, result);

        return result;
    }

    /// <summary>
    /// 组件目录蓝本 —— 只声明"怎么从 component.* 令牌推出目录"，令牌清单一律取本次真实生成的路径。
    /// 写死清单会让目录与库脱节（目录里出现库里没有的令牌，等于又造一个样子货）。
    /// </summary>
    static readonly (String Code, String Name, String Category, Boolean Interactive, String[] Anatomy, String A11y)[] ComponentBlueprints =
    {
        ("button", "按钮", "action", true,
            new[] { "容器", "图标位", "文本" },
            "前景与底色对比按审计判级须达 4.5:1；焦点环取 component.focus.*；禁用态不得只靠调透明度"),
        ("card", "卡片", "surface", false,
            new[] { "容器", "标题", "内容", "操作区" },
            "卡内文字层级用 semantic.text-1/2/3，正文对卡底须达 4.5:1"),
        ("input", "输入框", "form", true,
            new[] { "容器", "占位文本", "标签", "错误提示" },
            "聚焦态除边框换色外还须有非颜色线索（焦点环）；占位符对比不低于 3:1"),
        ("badge", "徽标", "display", false,
            new[] { "容器", "文本" },
            "tint 底色上的前景要按合成后的实际颜色重新判级，不能沿用原色对比"),
        ("nav", "导航", "navigation", true,
            new[] { "容器", "项", "指示条" },
            "当前项不得只靠颜色区分，需指示条或字重差"),
        ("table", "数据表", "data", true,
            new[] { "表头", "行", "单元格" },
            "表头/行底色差用于分组，行 hover 不得盖掉选中态"),
        ("dialog", "对话框", "overlay", true,
            new[] { "遮罩", "容器", "标题", "内容", "操作区" },
            "模态必须锁焦点且 Esc 可关（WCAG 2.1.2 不得有键盘陷阱）；关闭不能只靠遮罩点击"),
        ("tooltip", "提示浮层", "display", false,
            new[] { "容器", "文本" },
            "必须可被键盘聚焦触发并可关闭（WCAG 1.4.13），不得只挂在 hover 上"),
        ("tabs", "选项卡", "navigation", true,
            new[] { "容器", "标签", "指示条" },
            "当前标签除颜色外需指示条或字重差；方向键可在标签间移动"),
        ("select", "选择器", "form", true,
            new[] { "触发器", "菜单", "选项" },
            "菜单里高亮项必须与键盘焦点同步；选项文字对菜单底不低于 4.5:1"),
    };

    /// <summary>
    /// 把生成出来的组件层令牌落成组件目录（DesignComponent）+ 变体矩阵（DesignComponentVariant）。
    /// 生成器过去只写 component.* 令牌，组件目录与变体都留空 —— 于是"组件库"页面对刚生成的项目永远是 0 条。
    /// 这次没生成某组件的令牌就不建它的目录/格子（宁缺不造空壳）。
    /// </summary>
    public static ComponentSeed SeedComponentCatalog(CatalogRepository catalog, Int64 projectId, GenerationResult result)
    {
        var paths = result.Shared
            .Where(p => p.Path.StartsWith("component.", StringComparison.Ordinal))
            .Select(p => p.Path).ToList();

        var components = 0;
        var cells = 0;
        // 目录 + 矩阵整批一个事务：几十次独立提交只是把撞锁窗口拉长（同 AuditRepository.Record 的理由）
        using var et = new EntityTransaction<DesignComponent>();
        foreach (var b in ComponentBlueprints)
        {
            var prefix = $"component.{b.Code}.";
            var refs = paths.Where(p => p.StartsWith(prefix, StringComparison.Ordinal)).ToList();
            if (refs.Count == 0) continue;

            var variants = refs
                .Select(p => p[prefix.Length..].Split('.')[0])
                .Where(x => !x.IsNullOrEmpty())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            var component = catalog.SaveComponent(projectId, new ComponentInput
            {
                Code = b.Code,
                Name = b.Name,
                Category = b.Category,
                Interactive = b.Interactive,
                Description = $"生成器落地的{b.Name}规格，引用令牌 {refs.Count} 条（种子 {result.Seed}）",
                A11yNotes = b.A11y,
                TokenRefsJson = JsonSerializer.Serialize(refs),
                GuidanceJson = JsonSerializer.Serialize(new { anatomy = b.Anatomy, variants }),
                Status = "generated",
                SortOrder = components,
            });
            components++;

            foreach (var (variant, state, tokens) in DeriveVariantCells(prefix, refs))
            {
                catalog.SaveVariant(projectId, component, new VariantInput
                {
                    Code = $"{b.Code}-{variant}-{state}",
                    Name = $"{b.Name}·{variant}·{state}",
                    VariantJson = JsonSerializer.Serialize(AxisOf(variant)),
                    State = state,
                    ThemeId = DesignSystemConstants.SharedThemeId,
                    TokenRefsJson = JsonSerializer.Serialize(tokens),
                    Description = $"由 {tokens.Count} 条 {prefix}{variant}* 令牌推导（不是手写清单，凑不出令牌的组合不落格）",
                    SortOrder = cells,
                });
                cells++;
            }
        }
        et.Commit();
        return new ComponentSeed(components, cells);
    }

    /// <summary>
    /// 叶子上的已知交互后缀 → 状态名。不在表里的后缀一律算 default（不发明状态）。
    /// 后缀表与"状态档位序"是**同一张表**（<see cref="VariantAxes.State"/>）：这里去掉 `default` 再加回 `-`，
    /// 免得建矩阵用的顺序和产物里输出的顺序长成两回事。
    /// </summary>
    static readonly String[] StateSuffixes = [.. VariantAxes.OrderOf(VariantAxes.State).Skip(1).Select(s => $"-{s}")];

    /// <summary>没有命名空间段的组件级令牌（radius/type/padding…）归到 base 变体</summary>
    const String BaseVariant = "base";

    /// <summary>
    /// 命名空间段属于哪条轴：尺寸 / 角色 / 其它。认不出的一律记 `variant`（不猜没证据的轴），
    /// base（组件级令牌）不给轴键 —— 它就是该组件的默认形态。
    /// </summary>
    static Dictionary<String, String> AxisOf(String variant) => variant switch
    {
        BaseVariant => [],
        _ when VariantAxes.Has(VariantAxes.Size, variant) => new Dictionary<String, String> { [VariantAxes.Size] = variant },
        _ when VariantAxes.Has(VariantAxes.Role, variant) => new Dictionary<String, String> { [VariantAxes.Role] = variant },
        _ => new Dictionary<String, String> { ["variant"] = variant },
    };

    /// <summary>
    /// 从真实令牌路径推导 (变体, 状态) 格子：
    /// 变体 = 路径第一段命名空间（primary / secondary / item / header …），没有命名空间就是 base；
    /// 状态 = 叶子上的已知交互后缀（-hover/-active/-focus/-disabled/-pressed），没有就是 default。
    /// 只输出真有令牌支撑的格子 —— 矩阵因此是令牌的投影，不是另一份要人维护的清单。
    /// </summary>
    static IEnumerable<(String Variant, String State, List<String> Tokens)> DeriveVariantCells(String prefix, List<String> refs)
    {
        var map = new Dictionary<(String, String), List<String>>();
        foreach (var path in refs)
        {
            var segs = path[prefix.Length..].Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (segs.Length == 0) continue;

            var leaf = segs[^1];
            var state = "default";
            foreach (var s in StateSuffixes)
            {
                if (!leaf.EndsWith(s, StringComparison.Ordinal)) continue;
                state = s[1..];
                break;
            }

            var variant = segs.Length > 1 ? segs[0] : BaseVariant;
            if (map.TryGetValue((variant, state), out var list)) list.Add(path);
            else map[(variant, state)] = [path];
        }

        // 变体按字母序，状态按**交互档位序**（`VariantAxes.State`）：矩阵的 SortOrder 与产物里的状态序必须同出一张表，
        // 否则界面按 sortOrder 排、导出按字母序排，同一份库会给出两种"顺序"。
        foreach (var kv in map.OrderBy(k => k.Key.Item1, StringComparer.Ordinal)
                     .ThenBy(k => VariantAxes.Rank(VariantAxes.State, k.Key.Item2))
                     .ThenBy(k => k.Key.Item2, StringComparer.Ordinal))
            yield return (kv.Key.Item1, kv.Key.Item2, kv.Value);
    }

    /// <summary>
    /// 把生成结果落成"品牌层可登记资产"：字体登记（从真实字体栈解析，许可证如实写"不随产物分发"）、
    /// 页面清单（按行业倾向的起手屏，标 generated 可改）、资产（logo 母题 SVG，用 currentColor，不把颜色烤进图形）。
    /// 动机：capabilities 声明了 fonts/screens/assets，但过去既没有写入口也没人种数据 —— 界面只能显示"无…"，
    /// 那是"声明了但拿不到"，与假能力同罪。
    /// </summary>
    public static BrandSeed SeedBrandCatalog(CatalogRepository catalog, Int64 projectId, GenerationResult result)
    {
        String Value(String path) => result.Shared.FirstOrDefault(p => p.Path == path)?.Value ?? "";

        // 整批一个事务（同 AuditRepository.Record 的理由：几十次独立提交只是拉长撞锁窗口）
        using var et = new EntityTransaction<DesignFontFace>();

        // 只补空、不覆盖：这三张表用户会直接改（换 logo、登记可分发字体、删掉不需要的起手屏），
        // 重跑生成把它们写回去等于删用户的数据。令牌侧早有 Generator=manual 保护，这里按自然键跳过同名行。
        var knownFonts = new HashSet<String>(catalog.ListFonts(projectId)
            .Where(f => f.ProjectId == projectId)
            .Select(f => $"{f.Family}|{f.Weight}|{f.Style}"), StringComparer.Ordinal);
        var knownScreens = new HashSet<String>(catalog.ListScreens(projectId).Select(s => s.Code), StringComparer.Ordinal);
        var knownAssets = new HashSet<String>(catalog.ListAssets(projectId, null).Select(a => a.Code), StringComparer.Ordinal);

        var fonts = knownFonts.Count;
        foreach (var (role, stack) in new[] { ("sans", Value("font.sans")), ("mono", Value("font.mono")) })
        {
            if (stack.IsNullOrEmpty()) continue;
            foreach (var raw in stack.Split(','))
            {
                var family = raw.Trim().Trim('\'', '"');
                if (family.IsNullOrEmpty()) continue;
                foreach (var weight in new[] { 400, 700 })
                {
                    if (knownFonts.Contains($"{family}|{weight}|normal")) continue;   // 用户登记过就不动它
                    catalog.SaveFont(projectId, family, weight, "normal", null, null, "swap", role, null,
                        "字体栈成员（系统/浏览器提供），不随设计产物分发；要改用可分发字体必须在资产库登记真实许可证",
                        null);
                    knownFonts.Add($"{family}|{weight}|normal");
                    fonts++;
                }
            }
        }

        var screens = knownScreens.Count;
        foreach (var (code, title, icon) in ScreenSetFor(result.Industry))
        {
            if (knownScreens.Contains(code)) continue;
            catalog.SaveScreen(projectId, code, title, icon, $"/{code}", null,
                $"按行业倾向 {result.Industry} 给的起手屏（生成器建议，不是产品事实，可改可删）", null,
                DesignSystemConstants.SharedThemeId, screens);
            knownScreens.Add(code);
            screens++;
        }

        var assets = knownAssets.Count;
        foreach (var (code, name, kind, svg, refs, desc) in new[]
                 {
                     ("logo", "品牌标识", "logo", LogoSvg(),
                      new[] { "semantic.brand", "semantic.text-1" },
                      "由生成器给的原创母题：同心弧 + 中心方点，颜色一律 currentColor，换肤靠令牌"),
                     ("motif-grid", "背景母题", "motif", MotifSvg(),
                      new[] { "semantic.brand", "semantic.border-1" }, "可平铺的装饰母题；不含任何烤死的色值"),
                 })
        {
            if (knownAssets.Contains(code)) continue;
            catalog.SaveAsset(projectId, code, name, kind, svg,
                null, JsonSerializer.Serialize(refs), desc, "Owned");
            knownAssets.Add(code);
            assets++;
        }

        et.Commit();
        return new BrandSeed(fonts, screens, assets);
    }

    /// <summary>行业倾向 → 起手屏清单。这是"从哪几屏开始画"的建议，不是断言用户产品长这样（描述里已写明）。</summary>
    static (String Code, String Title, String Icon)[] ScreenSetFor(String industry) => industry switch
    {
        "devtools" => [("overview", "概览", "dashboard"), ("services", "服务列表", "box"), ("deployments", "部署记录", "download"), ("config", "配置", "settings"), ("audit", "审计", "shield")],
        "finance" => [("overview", "总览", "chart"), ("accounts", "账户", "credit-card"), ("transactions", "流水", "list"), ("reports", "报表", "file"), ("settings", "设置", "settings")],
        "healthcare" => [("overview", "概览", "heart"), ("patients", "患者", "user"), ("schedule", "排程", "calendar"), ("records", "病历", "file"), ("settings", "设置", "settings")],
        "commerce" => [("home", "首页", "home"), ("catalog", "商品", "box"), ("cart", "购物车", "cart"), ("orders", "订单", "list"), ("account", "账户", "user")],
        "media" => [("feed", "信息流", "layers"), ("article", "详情", "file"), ("search", "搜索", "search"), ("library", "收藏", "bookmark"), ("settings", "设置", "settings")],
        "education" => [("dashboard", "学习总览", "dashboard"), ("courses", "课程", "book"), ("lessons", "课时", "play"), ("practice", "练习", "edit"), ("settings", "设置", "settings")],
        _ => [("overview", "概览", "dashboard"), ("list", "列表", "list"), ("detail", "详情", "file"), ("settings", "设置", "settings")],
    };

    /// <summary>原创 logo 母题：同心弧 + 中心点，全部 currentColor（不烤颜色，换肤交给令牌）</summary>
    static String LogoSvg() =>
        "<circle cx=\"12\" cy=\"12\" r=\"9.2\" stroke=\"currentColor\" stroke-width=\"1.5\" fill=\"none\" stroke-linecap=\"round\" stroke-dasharray=\"20 8\"/>" +
        "<path d=\"M12 6.4 A5.6 5.6 0 0 1 17.6 12\" stroke=\"currentColor\" stroke-width=\"1.5\" fill=\"none\" stroke-linecap=\"round\"/>" +
        "<circle cx=\"12\" cy=\"12\" r=\"1.9\" fill=\"currentColor\"/>";

    /// <summary>
    /// 可平铺装饰母题：细网格 + 对角点，同样不含色值。
    /// 画在 **24×24 网格**上（与 logo、内置图标同一套规矩）——导出成 `.svg` 文件时补的外壳就是 `viewBox="0 0 24 24"`，
    /// 换个网格数图形会被裁掉，所以这里不允许多套尺寸各自漂移。
    /// </summary>
    static String MotifSvg() =>
        "<path d=\"M0 6h24M6 0v24M12 0v24M18 0v24M0 12h24M0 18h24\" stroke=\"currentColor\" stroke-width=\"0.6\" opacity=\".35\"/>" +
        "<circle cx=\"6\" cy=\"6\" r=\"1.1\" fill=\"currentColor\"/><circle cx=\"18\" cy=\"18\" r=\"1.1\" fill=\"currentColor\"/>";

    /// <summary>
    /// 一个色族的色阶。色相/彩度的取向是设计判断（品牌跟随种子色、状态色用固定色相环位置），
    /// 但**族名必须来自 <see cref="ColorFamilies.All"/>**：表里多一族而这里没规则 = 当场抛，
    /// 而不是安静地少一族（少一族时界面的色阶条带只是少一条，很难被发现）。
    /// </summary>
    static IReadOnlyList<RampStep> RampFor(String family, Double hue, Double chroma, Double accentOffset, Oklch.Color? seedColor, String? anchorStep) => family switch
    {
        ColorFamilies.Brand => ColorRampGenerator.Generate(new RampOptions(hue, chroma, AnchorStep: anchorStep, AnchorColor: seedColor)),
        ColorFamilies.Accent => ColorRampGenerator.Generate(new RampOptions(Oklch.NormalizeHue(hue + accentOffset), chroma * 0.92)),
        ColorFamilies.Neutral => ColorRampGenerator.GenerateNeutral(hue),
        ColorFamilies.Success => ColorRampGenerator.Generate(new RampOptions(152, chroma * 0.78)),
        ColorFamilies.Warning => ColorRampGenerator.Generate(new RampOptions(85, chroma * 0.86)),
        ColorFamilies.Danger => ColorRampGenerator.Generate(new RampOptions(27, chroma * 0.9)),
        ColorFamilies.Info => ColorRampGenerator.Generate(new RampOptions(Oklch.NormalizeHue(hue + accentOffset), chroma * 0.7)),
        _ => throw new InvalidOperationException($"色族 {family} 在 ColorFamilies.All 里但没有产出规则"),
    };

    /// <summary>
    /// 主题码 → 密度档（mode 轴里的密度档）。认紧凑/舒适两组常见写法，其余返回 null（=按色向主题处理）。
    /// </summary>
    static String? DensityOfTheme(String themeCode) => themeCode.ToLowerInvariant() switch
    {
        "compact" or "dense" or "condensed" or "tight" or "density-compact" => "compact",
        "comfortable" or "roomy" or "cozy" or "loose" or "density-comfortable" => "comfortable",
        "default" or "regular" or "density-default" => "default",
        _ => null,
    };

    /// <summary>
    /// 该令牌的别名目标住在各主题的 semantic 覆盖层，必须等主题层写完才存在（否则别名校验整批拒绝）。
    /// </summary>
    static Boolean NeedsThemedTarget(TokenPatch p) =>
        p.Tier == TokenTiers.Component ||
        (p.Tier == TokenTiers.Semantic && !string.IsNullOrEmpty(p.AliasPath) && p.AliasPath!.StartsWith("semantic.", StringComparison.Ordinal));

    static void Write(TokenRepository tokens, Int64 projectId, Int64 themeId, IReadOnlyList<TokenPatch> patches, Boolean overwrite, GenerationResult? result = null)
    {
        if (patches.Count == 0) return;
        var r = tokens.UpsertBatch(projectId, patches, overwrite);
        // 手改保护跳过的条数必须让调用方看见：否则"生成成功但库里没变"又是一次静默
        if (result != null)
        {
            result.SkippedProtected += r.SkippedProtected;
            result.Conflicts.AddRange(r.Conflicts);
        }
        if (r.Succeeded) return;

        var detail = string.Join("; ", r.Diagnostics.Take(8).Select(d => $"{d.Path}: {d.Message}"));
        throw new InvalidOperationException($"生成落库被拒（主题 {themeId}，共 {r.Diagnostics.Count} 项）：{detail}");
    }

    static String ShadowCss(ScaleGenerators.ShadowToken s) =>
        string.Join(", ", s.Layers.Select(l =>
            $"{(l.IsInset ? "inset " : "")}{Num(l.OffsetX)}px {Num(l.OffsetY)}px {Num(l.Blur)}px {Num(l.Spread)}px {AlphaColor(l.ColorValue, l.Alpha)}"));

    static String AlphaColor(String? hex, Double alpha)
    {
        var rgb = Oklch.TryParseRgb8(hex);
        return rgb == null ? hex ?? "transparent"
            : $"rgba({rgb.Value.R}, {rgb.Value.G}, {rgb.Value.B}, {Num(alpha)})";
    }

    static IEnumerable<TokenPatch> ChartSeriesPalette()
    {
        // 图表系列色是行业里最常缺的一类（参考物有图表组件却无系列色令牌）
        for (var i = 1; i <= 8; i++)
            yield return new TokenPatch
            {
                Path = $"chart.series-{i}",
                Tier = TokenTiers.Semantic,
                Type = TokenTypes.Color,
                AliasPath = i switch
                {
                    1 => "semantic.brand",
                    2 => "semantic.info",
                    3 => "semantic.success",
                    4 => "semantic.warning",
                    5 => "semantic.danger",
                    6 => "semantic.link",
                    7 => "semantic.brand-strong",
                    _ => "semantic.text-3",
                },
                Group = "chart",
                Name = $"series-{i}",
                Generator = TokenGenerators.Derived,
                SortOrder = i,
            };
    }

    /// <summary>组件层令牌：一律 alias 到 semantic，附带 tint/alpha 的 $extension 供 CSS 用 color-mix 派生</summary>
    static IEnumerable<TokenPatch> ComponentTokens()
    {
        yield return Comp("button.primary.background", "semantic.brand");
        yield return Comp("button.primary.background-hover", "semantic.brand-hover");
        yield return Comp("button.primary.background-active", "semantic.brand-strong", tint: null);
        yield return Comp("button.primary.foreground", "semantic.surface-1");
        yield return Comp("button.secondary.background", "semantic.surface-2");
        yield return Comp("button.secondary.border", "semantic.border-1");
        yield return Comp("button.secondary.foreground", "semantic.text-1");
        yield return Comp("button.danger.background", "semantic.danger");
        yield return Comp("button.danger.foreground", "semantic.surface-1");
        yield return Comp("button.radius", "radius.md");
        yield return Comp("button.padding-block", "space.2");
        yield return Comp("button.padding-inline", "space.4");
        yield return Comp("button.type", "type.body");

        // 尺寸轴：sm/md/lg 的内外边距与圆角一律别名到既有 space/radius 令牌（不新造数字）；
        // min-height 是唯一字面值，取 WCAG 2.5.8「目标不小于 24px」之上的可用档位。
        foreach (var (size, padBlock, padInline, radius, minH) in new[]
        {
            ("sm", "space.1", "space.3", "radius.sm", "28px"),
            ("md", "space.2", "space.4", "radius.md", "34px"),
            ("lg", "space.3", "space.5", "radius.lg", "42px"),
        })
        {
            yield return Comp($"button.{size}.padding-block", padBlock);
            yield return Comp($"button.{size}.padding-inline", padInline);
            yield return Comp($"button.{size}.radius", radius);
            yield return MinHeight($"button.{size}.min-height", minH, size);
            yield return Comp($"input.{size}.padding-block", padBlock);
            yield return Comp($"input.{size}.radius", radius);
            yield return MinHeight($"input.{size}.min-height", minH, size);
        }

        // 交互态补齐：hover/active/focus/disabled 都要有真令牌，
        // 否则组件矩阵里那一格只能空着（矩阵是令牌的投影，不靠人补写清单）。
        yield return Comp("button.secondary.background-hover", "semantic.surface-3");
        yield return Comp("input.border-focus", "semantic.brand");
        foreach (var role in new[] { "primary", "secondary", "danger" })
        {
            // disabled 不许只调透明度：底色与前景各给一条真令牌，判级才有对象
            yield return Comp($"button.{role}.background-disabled", "semantic.surface-2");
            yield return Comp($"button.{role}.foreground-disabled", "semantic.text-3");
        }
        yield return Comp("nav.item.background-active", "semantic.surface-3");
        yield return Comp("table.row.background-active", "semantic.surface-3");

        yield return Comp("badge.tint", "semantic.success", tint: 0.15);
        yield return Comp("badge.foreground", "semantic.success");
        yield return Comp("badge.border", "semantic.success", tint: 0.3);
        yield return Comp("badge.radius", "radius.pill");
        yield return Comp("badge.type", "type.caption");

        yield return Comp("input.background", "semantic.surface-1");
        yield return Comp("input.foreground", "semantic.text-1");
        yield return Comp("input.placeholder", "semantic.text-3");
        yield return Comp("input.border", "semantic.border-1");
        yield return Comp("input.border-active", "semantic.brand");
        yield return Comp("input.radius", "radius.md");

        yield return Comp("card.background", "semantic.surface-1");
        yield return Comp("card.foreground", "semantic.text-1");
        yield return Comp("card.border", "semantic.border-1");
        yield return Comp("card.shadow", "shadow.elevation-2");
        yield return new TokenPatch { Path = "component.card.padding", Tier = TokenTiers.Component, Type = TokenTypes.Dimension, Value = null, AliasPath = "space.5", Group = "card", Generator = TokenGenerators.Derived };
        yield return new TokenPatch { Path = "component.card.radius", Tier = TokenTiers.Component, Type = TokenTypes.Dimension, Value = null, AliasPath = "radius.lg", Group = "card", Generator = TokenGenerators.Derived };

        yield return Comp("nav.item.background-hover", "semantic.surface-2");
        yield return Comp("nav.item.foreground", "semantic.text-2");
        yield return Comp("nav.item.foreground-active", "semantic.text-1");
        yield return Comp("nav.indicator", "semantic.brand");

        yield return Comp("table.header.background", "semantic.surface-2");
        yield return Comp("table.row.background-hover", "semantic.surface-2");
        yield return Comp("table.border", "semantic.border-1");
        yield return Comp("table.type", "type.small");

        // 浮起来的一类组件参考物有而我们没有：对话框 / 提示浮层 / 选项卡 / 选择器。
        // 一律只引用既有语义与尺度令牌（不新造数字），这样换主题/换密度时它们跟着整套系统走。
        yield return Comp("dialog.background", "semantic.surface-1");
        yield return Comp("dialog.foreground", "semantic.text-1");
        yield return Comp("dialog.border", "semantic.border-1");
        yield return Comp("dialog.scrim", "semantic.overlay", tint: 0.55);
        yield return Comp("dialog.shadow", "shadow.elevation-5");
        yield return Comp("dialog.radius", "radius.lg");
        yield return Comp("dialog.padding", "space.6");

        yield return Comp("tooltip.background", "semantic.text-1");
        yield return Comp("tooltip.foreground", "semantic.surface-1");
        yield return Comp("tooltip.shadow", "shadow.elevation-2");
        yield return Comp("tooltip.radius", "radius.sm");
        yield return Comp("tooltip.padding-block", "space.1");
        yield return Comp("tooltip.padding-inline", "space.2");
        yield return Comp("tooltip.type", "type.caption");

        yield return Comp("tabs.tab.foreground", "semantic.text-2");
        yield return Comp("tabs.tab.foreground-active", "semantic.text-1");
        yield return Comp("tabs.tab.background-hover", "semantic.surface-2");
        yield return Comp("tabs.indicator", "semantic.brand");
        yield return Comp("tabs.border", "semantic.border-1");
        yield return Comp("tabs.type", "type.small");

        yield return Comp("select.background", "semantic.surface-1");
        yield return Comp("select.foreground", "semantic.text-1");
        yield return Comp("select.border", "semantic.border-1");
        yield return Comp("select.border-focus", "semantic.brand");
        yield return Comp("select.option.background-active", "semantic.surface-2");
        yield return Comp("select.option.foreground-active", "semantic.text-1");
        yield return Comp("select.menu.shadow", "shadow.elevation-3");
        yield return Comp("select.radius", "radius.md");

        yield return Comp("overlay.scrim", "semantic.overlay", tint: 0.55);
        yield return new TokenPatch
        {
            Path = "component.focus.outline-width", Tier = TokenTiers.Component, Type = TokenTypes.Dimension, Value = "2px",
            Group = "focus", Generator = TokenGenerators.Derived, Description = ":focus-visible 外框宽（WCAG 2.4.7 / 2.4.11）",
        };
        yield return new TokenPatch
        {
            Path = "component.focus.outline-offset", Tier = TokenTiers.Component, Type = TokenTypes.Dimension, Value = "2px",
            Group = "focus", Generator = TokenGenerators.Derived,
        };
        yield return Comp("focus.outline-color", "semantic.brand");
        yield return new TokenPatch
        {
            Path = "component.motion.reduced.duration", Tier = TokenTiers.Component, Type = TokenTypes.Duration, Value = "0.01ms",
            Group = "motion", Generator = TokenGenerators.Derived, Description = "prefers-reduced-motion 全局替代时长",
        };
    }

    /// <summary>尺寸轴的点击目标高度：组件层唯一允许的字面维度值（其余尺寸一律别名到 space/radius）</summary>
    static TokenPatch MinHeight(String path, String value, String size) => new()
    {
        Path = "component." + path,
        Tier = TokenTiers.Component,
        Type = TokenTypes.Dimension,
        Value = value,
        Group = path.Split('.')[0],
        Name = path,
        Generator = TokenGenerators.Derived,
        Description = $"{size} 档点击目标高度下限（WCAG 2.5.8 要求不小于 24px）",
    };

    static TokenPatch Comp(String path, String alias, Double? tint = null)
    {
        var full = path.StartsWith("component.", StringComparison.Ordinal) ? path : "component." + path;
        return new TokenPatch
        {
            Path = full,
            Tier = TokenTiers.Component,
            Type = TypeOfAlias(alias),
            AliasPath = alias,
            Group = full.Split('.')[1],
            Name = full,
            Generator = TokenGenerators.Derived,
            Extensions = tint == null ? null : JsonSerializer.Serialize(new { forgeself = new { tint = tint.Value, css = $"color-mix(in oklab, var(--{alias.Replace('.', '-')}) {Num(tint.Value * 100)}%, transparent)" } }),
        };
    }

    /// <summary>组件令牌的值类型由被引用的语义/基础令牌推断（不能全标成 color，否则导出与审计都错类）</summary>
    static String TypeOfAlias(String alias) => alias switch
    {
        var a when a.StartsWith("radius.", StringComparison.Ordinal) || a.StartsWith("space.", StringComparison.Ordinal) => TokenTypes.Dimension,
        var a when a.StartsWith("shadow.", StringComparison.Ordinal) => TokenTypes.Shadow,
        var a when a.StartsWith("type.", StringComparison.Ordinal) => TokenTypes.Typography,
        var a when a.StartsWith("duration.", StringComparison.Ordinal) => TokenTypes.Duration,
        _ => TokenTypes.Color,
    };

    static String ResolveIndustry(GenerationRequest req, String brief)
    {
        if (!req.Industry.IsNullOrEmpty() && Industries.ContainsKey(req.Industry!)) return req.Industry!;
        return InferIndustry(brief) ?? "general";
    }

    /// <summary>行业清单（取 Industries 字典键，单一来源；供预设推荐/工具枚举用，不另抄）</summary>
    public static IReadOnlyList<String> KnownIndustries => Industries.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();

    /// <summary>从 brief 关键词推断行业；无命中返回 null（调用方回落 general）</summary>
    public static String? InferIndustry(String? brief)
    {
        if (brief.IsNullOrEmpty()) return null;
        foreach (var (kw, ind) in IndustryClues)
            if (brief.Contains(kw, StringComparison.OrdinalIgnoreCase)) return ind;
        return null;
    }

    /// <summary>把传入行业规范到 KnownIndustries；未知 → general</summary>
    public static String NormalizeIndustry(String? industry)
    {
        if (!industry.IsNullOrEmpty() && Industries.ContainsKey(industry!)) return industry!;
        return "general";
    }

    /// <summary>稳定哈希取色相：与 String.GetHashCode 不同，跨进程/跨运行必须一致，否则"可复现"是假的</summary>
    static Double StableHue(String text)
    {
        if (text.Length == 0) return 262;
        var h = 2166136261u;
        foreach (var ch in text.ToLowerInvariant())
        {
            h ^= ch;
            h = (h * 16777619u) & 0xFFFFFFFF;
        }
        return h % 360;
    }

    static String BuildSeed(GenerationRequest req, String industry, Double hue, Double chroma, Double ratio, Double radius, String density, Double motion, Double basePx)
    {
        var raw = string.Join('|',
            industry, Num(hue), Num(chroma), Num(req.AccentHueOffset), density, Num(ratio), Num(basePx), Num(radius), Num(motion),
            String.Join(",", req.Themes), req.BrandName ?? "", (req.Brief ?? "").ToLowerInvariant());
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    static String Num(Double v) => Math.Round(v, 4).ToString("0.####", CultureInfo.InvariantCulture);
}
