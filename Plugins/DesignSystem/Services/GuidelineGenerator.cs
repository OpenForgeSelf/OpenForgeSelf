using NewLife;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>一条规则（level 取 <see cref="GuidelineCategories.Levels"/>；文本里只允许反引号令牌路径，不允许数字单位）</summary>
public sealed record GuidelineRule(String Id, String Level, String Text);

/// <summary>一条默认规范（生成器产物；落库前后形状一致）。<c>Code</c> 为项目内唯一标识（kebab，契约不可改）。</summary>
public sealed record GuidelineDraft(
    String Code,
    String Category,
    String Title,
    String Summary,
    String Body,
    IReadOnlyList<GuidelineRule> Rules,
    IReadOnlyList<String> TokenRefs,
    IReadOnlyList<String> AppliesTo);

/// <summary>
/// 默认 UX 规范生成器（M3 §G2/§G3）：确定性的纯函数，输入 <c>kind/industry/density + 项目里真存在的令牌路径</c>
/// → 恰 14 条规范。
///
/// 三条纪律，每条都有用例钉：
/// 1. **只写令牌路径、不写数字**（决策 D5）。数字一律由渲染端（<see cref="GuidelineRenderer"/>）现查当前令牌值。
///    写进文本的数字就是第二份真相 —— 令牌改了值，规范还留着旧数字，比没规范更坏。
/// 2. **引用的令牌必须真存在**。传进来的 <c>tokenPaths</c> 里没有的引用会被剔掉（规则文本不依赖数字，故保留）；
///    渲染端遇到后来被删的引用会标「令牌已不存在」，不静默。
/// 3. **同输入必同输出**：不读时钟、不用随机、不依赖字典枚举序（差异全部来自显式参数）。
///
/// 文案内引用概念一律用直角引号「」，且不得出现数字 + 单位（机器守卫正则核）。
/// </summary>
public static class GuidelineGenerator
{
    /// <summary>生成器版本（进 <c>GeneratorVersion</c> 列；文本口径变了要升它，同版本产物变了必须升版）</summary>
    public const String Version = "1";

    /// <summary>§G2 的 14 个 code（契约：集合恰好等于这份清单，增删要过闸门）</summary>
    public static readonly String[] Codes =
    [
        "layout-grid", "elevation", "density", "responsive",
        "page-patterns", "navigation",
        "buttons", "forms",
        "feedback", "states",
        "data-display",
        "content", "a11y", "motion",
    ];

    /// <summary>用途（DesignProject.Kind）分组：控制台类 / 营销品牌类 / 其余算产品类</summary>
    static readonly String[] ConsoleKinds = ["console", "system"];
    static readonly String[] MarketingKinds = ["marketing", "brand"];

    /// <summary>生成种子：只由三个输入决定，可复现可 diff（同 M1 的种子纪律）</summary>
    public static String SeedFor(String kind, String industry, String density) =>
        $"kind={kind};industry={industry};density={density}";

    public static IReadOnlyList<GuidelineDraft> Generate(String? kind, String? industry, String? density, IEnumerable<String>? tokenPaths)
    {
        var k = NormalizeKind(kind);
        var ind = DesignGenerator.NormalizeIndustry(industry);
        var den = NormalizeDensity(density);
        var paths = (tokenPaths ?? []).ToHashSet(StringComparer.Ordinal);

        // 密度决定「引用哪个间距档」，不写死像素（§G3）
        var pageMargin = den == "compact" ? "space.5" : den == "comfortable" ? "space.8" : "space.6";
        var formGap = den == "compact" ? "space.2" : den == "comfortable" ? "space.4" : "space.3";
        var buttonSize = den == "comfortable" ? "lg" : den == "compact" ? "sm" : "md";

        var drafts = new List<GuidelineDraft>
        {
            LayoutGrid(k, pageMargin),
            Elevation(),
            Density(den),
            Responsive(k),
            PagePatterns(k),
            Navigation(k),
            Buttons(buttonSize),
            Forms(formGap),
            Feedback(ind),
            States(),
            DataDisplay(ind),
            Content(k, ind),
            Accessibility(ind),
            Motion(),
        };

        return drafts.Select(d => d with { TokenRefs = KeepExisting(d.TokenRefs, paths) }).ToList();
    }

    /// <summary>引用只保留项目里真存在的路径（共享层或任一主题层出现过即算存在）</summary>
    static IReadOnlyList<String> KeepExisting(IReadOnlyList<String> refs, HashSet<String> paths) =>
        refs.Where(paths.Contains).Distinct(StringComparer.Ordinal).ToList();

    static String NormalizeKind(String? kind) => kind switch
    {
        { Length: > 0 } k when ConsoleKinds.Contains(k, StringComparer.OrdinalIgnoreCase) => "console",
        { Length: > 0 } k when MarketingKinds.Contains(k, StringComparer.OrdinalIgnoreCase) => "marketing",
        _ => "product",
    };

    static String NormalizeDensity(String? density) => density switch
    {
        { Length: > 0 } d when d.Equals("compact", StringComparison.OrdinalIgnoreCase) => "compact",
        { Length: > 0 } d when d.Equals("comfortable", StringComparison.OrdinalIgnoreCase) => "comfortable",
        _ => "default",
    };

    // ---- §G2 的 14 条正文 ----

    static GuidelineDraft LayoutGrid(String kind, String margin)
    {
        var rules = new List<GuidelineRule>
        {
            new("page-margin", "MUST", $"页面水平边距取 `space.*` 档之一，本项目默认 `{margin}`（随密度变化，具体值由令牌给）。"),
            new("grid-twelve", "SHOULD", "栅格按十二列划分，列间距取 `space.4`，不自己算百分比。"),
            new("breakpoints", "MUST", "断点只用 `breakpoint.2` 到 `breakpoint.5` 这档已有的四档，不新增自定义断点值。"),
            new("max-width", "MAY", kind == "marketing"
                ? "内容型长文页的最大宽度取 `breakpoint.4`，超宽屏靠两侧留白而不是拉宽行。"
                : "控制台类以填满工作区为常态，不额外约束内容最大宽。"),
        };
        if (kind != "console")
            rules.Add(new("narrow-first", "SHOULD", "窄屏优先：先排最小断点下的顺序，再逐级放宽。"));

        return new("layout-grid", "layout", "栅格与页面边距",
            "页面水平边距、列间距、断点全部取自已有的间距与断点令牌，密度改了它们就跟着改。",
            "布局层只允许引用尺度令牌。任何一处把边距写成具体尺寸，都会在下一个密度档上失效。",
            rules,
            ["space.4", margin, "space.5", "space.6", "space.8", "breakpoint.2", "breakpoint.3", "breakpoint.4", "breakpoint.5"],
            Kinds(kind));
    }

    static GuidelineDraft Elevation() => new("elevation", "layout", "层级与阴影",
        "卡片、浮层、对话框分别取固定的阴影层级，层叠顺序只用 z-index 令牌。",
        "层级是「谁盖住谁」的约定，不是装饰：同一页出现两种卡片阴影，说明有人在自造层级。",
        new[]
        {
            new GuidelineRule("card-elevation", "MUST", "卡片底用 `shadow.elevation-2`，不在组件里自写阴影值。"),
            new GuidelineRule("menu-elevation", "MUST", "菜单与浮层用 `shadow.elevation-3`，对话框用 `shadow.elevation-5`。"),
            new GuidelineRule("z-index", "MUST", "层叠顺序只取 `z-index.*`，禁止写具体数值。"),
            new GuidelineRule("one-shadow-per-role", "SHOULD", "同一种容器在整站只用一个阴影层级；要改就改令牌。"),
        },
        ["shadow.elevation-2", "shadow.elevation-3", "shadow.elevation-5",
         "z-index.1", "z-index.2", "z-index.3", "z-index.4", "z-index.5", "z-index.6"],
        Kinds(null));

    static GuidelineDraft Density(String density) => new("density", "layout", "密度",
        "同一页面不混用多种密度；数据密集型页面取紧凑，阅读型取宽松。",
        $"本项目的默认密度档是{DensityLabel(density)}。间距只取 `space.*` 档位，不写绝对值。",
        new[]
        {
            new GuidelineRule("single-density", "MUST", "同一页面只用一种密度，不在局部把间距改回更松或更紧。"),
            new GuidelineRule("spacing-only", "MUST", "所有间距取 `space.*` 档位；不接受组件内自报的尺寸值。"),
            new GuidelineRule("data-dense", "SHOULD", density == "compact"
                ? "本项目默认紧凑：表格、日志、列表等数据密集页沿用默认档即可。"
                : "数据密集页可整体切到紧凑主题，但必须整页切，不能只切一块。"),
            new GuidelineRule("reading-roomy", "SHOULD", density == "comfortable"
                ? "本项目默认宽松：长文与卡片沿用默认档。"
                : "阅读型页面切到宽松档时，标题与正文的层级关系保持不变。"),
        },
        ["space.1", "space.2", "space.3", "space.4", "space.5", "space.6", "space.8"],
        Kinds(null));

    static GuidelineDraft Responsive(String kind) => new("responsive", "layout", "响应式",
        kind == "marketing" ? "营销与品牌页移动优先；控制台桌面优先但必须能在窄屏看完关键信息。"
            : kind == "console" ? "控制台以桌面为主，窄屏下不得出现横向滚动。"
            : "产品页移动优先；任何断点都不出现横向滚动。",
        "响应式的判据是「能不能看完」，不是「有没有断点」。",
        new[]
        {
            new GuidelineRule("no-h-scroll", "MUST", "任一断点下页面不出现横向滚动条。"),
            new GuidelineRule("breakpoints", "SHOULD", "断点取 `breakpoint.*`，与栅格条用同一组档。"),
            new GuidelineRule("touch-target", "MUST", "可点区域不小于 `component.button.sm.min-height`，触屏场景取 `component.button.lg.min-height`。"),
            new GuidelineRule("priority", "MUST", kind == "console"
                ? "控制台桌面优先：窄屏下次要信息可折叠，主任务必须仍可完成。"
                : "移动优先：先排窄屏顺序，再逐级放宽。"),
        },
        ["breakpoint.2", "breakpoint.3", "breakpoint.4", "breakpoint.5",
         "component.button.sm.min-height", "component.button.lg.min-height"],
        Kinds(kind));

    static GuidelineDraft PagePatterns(String kind)
    {
        String title, body;
        GuidelineRule[] rules;
        if (kind == "console")
        {
            title = "列表 / 详情 / 表单 / 仪表盘四类页面的结构固定";
            body = "控制台页面按四类模式套结构，区块顺序固定，换页面不等于换布局习惯。";
            rules =
            [
                new("four-patterns", "MUST", "新页面必须归入列表、详情、表单、仪表盘四类之一，说不归类的先别画。"),
                new("list-order", "MUST", "列表页顺序：标题区 → 筛选区 → 表格 → 批量操作，不自造新顺序。"),
                new("detail-order", "SHOULD", "详情页顺序：标题与主操作 → 摘要信息 → 明细区块 → 关联记录。"),
                new("form-sections", "SHOULD", "表单按区块分组，区块标题用 `type.h3`，字段间距见表单条。"),
                new("surface-card", "MAY", "区块容器用 `component.card.*`，不自己拼底色与圆角。"),
            ];
        }
        else if (kind == "marketing")
        {
            title = "首屏 / 特性 / 定价 / 页脚四段式";
            body = "品牌与营销页按阅读节奏分段，每段只承担一个主张。";
            rules =
            [
                new("hero-first", "MUST", "首屏只放一个主张与一个主行动按钮。"),
                new("section-order", "SHOULD", "顺序：首屏 → 特性 → 佐证 → 定价 → 页脚，缺段的按空段跳过而不是硬凑。"),
                new("footer-links", "MUST", "页脚必须有可达的联系方式与法务入口，不放装饰性链接。"),
                new("width", "SHOULD", "正文段落宽度受 `breakpoint.4` 约束，超宽屏用两侧留白。"),
                new("content-page", "MAY", "内容型站点另设文章与详情页模板，不复用落地页结构。"),
            ];
        }
        else
        {
            title = "标题区 → 主体 → 操作区";
            body = "产品页按三段式组织：先说清这是什么，再给内容，最后给动作。";
            rules =
            [
                new("three-blocks", "MUST", "页面自上而下是标题区、主体、操作区，操作区不藏进滚动深处。"),
                new("one-primary", "MUST", "每屏只有一个主行动，其余降为次要按钮（按钮层级条已约束）。"),
                new("empty-first", "SHOULD", "新用户的空态就是第一屏，按状态条给行动而不是留白。"),
                new("mobile-flow", "MAY", "移动优先时主操作落到底部可达区域。"),
            ];
        }

        return new("page-patterns", "page", "页面模式", title, body, rules,
            ["component.card.background", "component.card.radius", "component.card.padding",
             "component.table.header", "type.h3"],
            Kinds(kind));
    }

    static GuidelineDraft Navigation(String kind) => new("navigation", "navigation", "导航",
        "一级入口限量、当前项必须有非颜色指示、层级不超过三层。",
        "导航解决的是「用户在不在」的问题：找不到入口的页面等于没有。",
        new[]
        {
            new GuidelineRule("entry-limit", "MUST", "一级入口数量设上限，超出就归类而不是继续加项。"),
            new GuidelineRule("current-indicator", "MUST", "当前项必须有指示条或字重差（`component.nav.indicator`），不得只靠颜色区分。"),
            new GuidelineRule("depth-limit", "MUST", "层级不超过三层；到第三层还在加深时，改成一个列表页加详情页。"),
            new GuidelineRule("breadcrumb", "SHOULD", kind == "console"
                ? "控制台深页面给面包屑，用户能从当前位置退回上一级。"
                : "站点层级较深时给面包屑或分类入口。"),
            new GuidelineRule("mobile-nav", "SHOULD", kind == "marketing"
                ? "移动端顶栏收纳为抽屉或底部标签，标签数不超过五个。"
                : "移动端底部标签栏数量设上限，超出的收进「更多」。"),
        },
        ["component.nav.indicator", "component.nav.item.foreground",
         "component.nav.item.foreground-active", "component.nav.item.background-active"],
        Kinds(kind));

    static GuidelineDraft Buttons(String size) => new("buttons", "form", "按钮层级",
        "每屏一个主按钮；危险操作二次确认；禁用态不得只靠透明度。",
        "按钮层级是页面的语法：主按钮一多，用户就不知道该点哪个。",
        new[]
        {
            new GuidelineRule("one-primary", "MUST", "每屏至多一个主按钮（`component.button.primary.*`），其余用次要或文字按钮。"),
            new GuidelineRule("danger-confirm", "MUST", "不可逆操作只用危险按钮（`component.button.danger.*`），且必须二次确认并在确认框里写清后果。"),
            new GuidelineRule("size-choice", "SHOULD", $"默认尺寸取 {size} 档；触屏为主的场景升一档，数据密集表格内降一档。"),
            new GuidelineRule("disabled-visible", "MUST", "禁用态用 `component.button.primary.background-disabled` 与 `component.button.primary.foreground-disabled` 两条真令牌，不得只调透明度。"),
            new GuidelineRule("label-verb", "SHOULD", "按钮文字用「动词 + 宾语」，不用「确定/提交」这类脱离上下文的词。"),
        },
        ["component.button.primary.background", "component.button.danger.background",
         "component.button.sm.min-height", "component.button.md.min-height", "component.button.lg.min-height",
         "component.button.primary.background-disabled", "component.button.primary.foreground-disabled"],
        Kinds(null));

    static GuidelineDraft Forms(String gap) => new("forms", "form", "表单",
        "标签在输入上方，失焦即校验，错误文案写「原因 + 怎么改」。",
        "表单是流失率最高的地方：校验与错误文案的质量直接决定用户能不能提交成功。",
        new[]
        {
            new GuidelineRule("label-above", "MUST", "标签置于输入框上方，不靠占位符当标签（输入后占位符消失，用户就忘了这一栏填什么）。"),
            new GuidelineRule("required-mark", "MUST", "必填项有非颜色标记（星号或文字），不只靠标签颜色区分。"),
            new GuidelineRule("validate-blur", "MUST", "失焦即校验单字段；提交时汇总全部错误并把焦点移到第一个错误字段。"),
            new GuidelineRule("error-text", "MUST", "错误文案含「问题 + 怎么改」；聚焦态用 `component.input.border-focus`，不只靠变色。"),
            new GuidelineRule("field-gap", "SHOULD", $"字段垂直间距取 `{gap}`（随密度），同一表单内保持一致。"),
        },
        ["component.input.border", "component.input.border-focus", "component.input.border-active",
         "component.input.placeholder", "component.input.background", gap, "semantic.danger"],
        Kinds(null));

    static GuidelineDraft Feedback(String industry)
    {
        var rules = new List<GuidelineRule>
        {
            new("semantic-colors", "MUST", "成功/警告/错误/信息各用对应语义色令牌，且必须同时给图标与文字。"),
            new("transient-vs-sticky", "MUST", "瞬时提示可自动消失；错误与需要用户处置的提示必须手动关闭。"),
            new("page-level-result", "SHOULD", "关键结果用页内反馈（就地显示在相关区块），不要只用一条浮层提示打发。"),
            new("no-silent-fail", "MUST", "任何失败都必须可见；请求失败却没有提示等于把错误吞掉。"),
        };
        if (industry == "healthcare")
            rules.Add(new("critical-ack", "MUST", "临床关键告警必须显式确认，确认后仍保留可回溯记录。"));
        if (industry == "commerce")
            rules.Add(new("purchase-success", "MUST", "下单或支付成功后给可核对的凭据（编号与下一步），不只说成功。"));
        if (industry == "education")
            rules.Add(new("encouraging-tone", "SHOULD", "失败反馈先说下一步怎么做，语气鼓励而非指责。"));

        return new("feedback", "feedback", "反馈与提示",
            "四类提示各有语义色，且一律配图标与文字；错误不自动消失。",
            "反馈的判据是「用户看完知道下一步做什么」，不是「有没有弹提示」。",
            rules,
            ["semantic.success", "semantic.warning", "semantic.danger", "semantic.info",
             "component.badge.foreground", "component.badge.tint"],
            Kinds(null));
    }

    static GuidelineDraft States() => new("states", "state", "空 / 加载 / 错误态",
        "加载较慢给骨架，空态给下一步行动，错误态给原因与重试；禁止空白页。",
        "这三种状态最容易被漏掉：漏掉就是把「还没数据」显示成「坏了」。",
        new[]
        {
            new GuidelineRule("loading-skeleton", "MUST", "预计较慢时用骨架屏（取 `semantic.surface-2` 打底），不用全屏遮罩转圈。"),
            new GuidelineRule("empty-action", "MUST", "空态必须给出下一步行动入口；新用户的第一次进入就是空态。"),
            new GuidelineRule("error-retry", "MUST", "错误态含原因与重试入口，并区分「没数据」与「取失败」。"),
            new GuidelineRule("no-blank", "MUST", "任何路径都不得留下无提示的空白页。"),
            new GuidelineRule("partial-visible", "SHOULD", "部分成功要显示部分结果与失败清单，不假装全量成功。"),
        },
        ["semantic.text-3", "semantic.surface-2", "semantic.surface-1", "component.card.background"],
        Kinds(null));

    static GuidelineDraft DataDisplay(String industry)
    {
        var rules = new List<GuidelineRule>
        {
            new("table-tokens", "MUST", "表头与行 hover 底色取 `component.table.*`，不自选灰色。"),
            new("numbers-right", "MUST", "数字列右对齐，位数不齐时用等宽字体（`font.mono`）。"),
            new("series-order", "MUST", "图表系列色按 `chart.series-1` 起的顺序取，且不只靠颜色区分系列（配图标或文字标注）。"),
            new("no-raw-percent", "SHOULD", "百分比与金额由数据层格式化，视图层不自己算占比。"),
        };
        if (industry == "finance")
        {
            rules.Add(new("money-grouping", "MUST", "金额做千分位分组，负数以明确符号标注而不是只靠颜色。"));
            rules.Add(new("decimal-places", "MUST", "同类金额小数位一致，位数由业务口径给，不在页面临时改。"));
        }
        if (industry == "devtools")
            rules.Add(new("mono-ids", "MUST", "代码、日志、标识符用 `font.mono`，并保持可复制。"));

        return new("data-display", "data", "表格与图表",
            "表格用组件令牌，数字右对齐；图表系列色按序取且不只靠颜色区分。",
            "数据展示的目标是能被核对：对齐、单位、来源三样都要看得见。",
            rules,
            ["component.table.header", "component.table.row", "component.table.border", "font.mono",
             "chart.series-1", "chart.series-2", "chart.series-3", "chart.series-4",
             "chart.series-5", "chart.series-6", "chart.series-7", "chart.series-8"],
            Kinds(null));
    }

    static GuidelineDraft Content(String kind, String industry)
    {
        var rules = new List<GuidelineRule>
        {
            new("tone-by-kind", "MUST", kind == "console"
                ? "语气平实、面向操作：先给结论，再给细节；不用营销词。"
                : kind == "marketing" ? "语气有感染力但不夸大；一个段落只说一件事。"
                : "语气面向使用者：说清能做什么，不堆术语。"),
            new("button-label", "MUST", "按钮与链接用「动词 + 宾语」，同一动作全站同一个词。"),
            new("term-consistent", "MUST", "同一概念全站用同一个词（术语在库里，改名要一起改）。"),
            new("no-placeholder", "MUST", "不得留「待补充/TBD」这类占位文案上线。"),
        };
        if (industry == "finance")
            rules.Add(new("currency-clear", "MUST", "金额写明币种与小数口径，同一页不混用两种记法。"));
        if (industry == "devtools")
            rules.Add(new("copyable", "MUST", "标识符、命令、错误码可复制，文案简洁不修饰。"));
        if (industry == "media")
        {
            rules.Add(new("reading-width", "SHOULD", "正文行长受 `breakpoint.4` 约束，行高取排版令牌，不手动加空行。"));
            rules.Add(new("heading-hierarchy", "MUST", "标题层级按 `type.h1` 到 `type.h4` 顺序使用，不跳级当大字用。"));
        }
        if (industry == "commerce")
            rules.Add(new("price-label", "MUST", "价格与促销标注并列出现（原价、到手价、优惠条件），不藏条件。"));
        if (industry == "education")
            rules.Add(new("plain-language", "SHOULD", "用易懂语言，必要术语首次出现时给一句解释。"));

        return new("content", "content", "文案与术语",
            "语气随用途；按钮用动词短语；术语全站一致；不留占位文案。",
            "文案是界面的一部分，不是最后贴上去的皮：写不清通常说明功能还没想清。",
            rules, ["type.h1", "type.h2", "type.h3", "type.h4", "type.body", "breakpoint.4"], Kinds(kind));
    }

    static GuidelineDraft Accessibility(String industry)
    {
        var rules = new List<GuidelineRule>
        {
            new("contrast-gate", "MUST", "正文对比度不低于 4.5:1、大字与非文本不低于 3:1，由可达性审计强制，不靠人眼判断。"),
            new("focus-visible", "MUST", "键盘焦点必须可见，焦点环取 `component.focus.*`（宽度与偏移都来自令牌）。"),
            new("target-size", "MUST", "交互目标不小于 `component.button.sm.min-height`。"),
            new("keyboard-path", "MUST", "全部核心路径键盘可达，模态不得锁死键盘（不得有键盘陷阱）。"),
            new("not-color-only", "MUST", "状态区分不得只依赖颜色：另有图标、文字或形状。"),
        };
        if (industry == "healthcare")
        {
            rules.Add(new("clinical-not-color", "MUST", "临床状态不得只用颜色区分，必须同时给文字标签。"));
            rules.Add(new("min-text-size", "MUST", "正文字号不小于正文档（`type.body`），不许为省空间压小字。"));
        }

        return new("a11y", "a11y", "可达性",
            "对比度、焦点可见、目标尺寸、键盘可达、不只靠颜色——五条都由审计与界面共同保证。",
            "可达性不是加分项。WCAG 2.2 是这些规则的出处，本插件的审计门禁负责把它变成数据。",
            rules,
            ["component.focus.outline-width", "component.focus.outline-offset", "component.focus.outline-color",
             "component.button.sm.min-height", "type.body", "semantic.text-1", "semantic.surface-1"],
            Kinds(null));
    }

    static GuidelineDraft Motion() => new("motion", "motion", "动效",
        "时长只用 duration 档、缓动只用 ease 档；减少动效偏好下用 reduced 档；禁无限循环抢注意力。",
        "动效的判据是「能不能帮用户理解变化」，帮不上的动效就是延迟。",
        new[]
        {
            new GuidelineRule("duration-tokens", "MUST", "时长只取 `duration.micro`、`duration.base`、`duration.macro`、`duration.emphasized` 四档，不写毫秒字面量。"),
            new GuidelineRule("easing-tokens", "MUST", "缓动只取 `ease.*` 令牌；进入用 `ease.decelerate`，退出用 `ease.accelerate`。"),
            new GuidelineRule("reduced-motion", "MUST", "尊重系统偏好：减少动效时用 `duration.base-reduced` 等 reduced 档替代。"),
            new GuidelineRule("no-distracting-loop", "SHOULD", "无限循环动画不得抢占注意力；提示性动效播完即停。"),
            new GuidelineRule("motion-carries-meaning", "MAY", "动效要表达方向或因果（从哪来、到哪去），纯装饰性动效优先删掉。"),
        },
        ["duration.micro", "duration.base", "duration.macro", "duration.emphasized",
         "duration.micro-reduced", "duration.base-reduced", "duration.macro-reduced", "duration.emphasized-reduced",
         "ease.standard", "ease.decelerate", "ease.accelerate", "ease.emphasized"],
        Kinds(null));

    /// <summary>适用用途：全类通用还是明确几类（界面与工具按它筛「这条对我这个项目管不管」）</summary>
    static IReadOnlyList<String> Kinds(String? kind) => kind switch
    {
        null => ["product", "console", "brand", "marketing", "system"],
        "console" => ["console", "system", "product"],
        "marketing" => ["marketing", "brand"],
        _ => ["product", "console", "system"],
    };

    static String DensityLabel(String density) => density switch
    {
        "compact" => "紧凑",
        "comfortable" => "宽松",
        _ => "适中",
    };
}
