using NewLife;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>
/// §D 预设目录（8 个）。id / 名称 / 性格词 / 适用范围 / 关键词为契约不可改；
/// 数值（hue/chroma/density/typeRatio/typeBasePx/radiusBase/motionScale/themes/request.industry）
/// 可按下述规则微调：生成→审计出 critical 时先把 chroma ±0.03 内调整；仍不过再去掉 high-contrast
/// 并在 05-evidence 登记。不得改 id、名称、性格词、适用范围、关键词。
/// Request 是 GenerationRequest 的深拷贝，调用方改它不得影响目录。
/// </summary>
public sealed class StylePreset
{
    public required String Id { get; init; }
    public required String Name { get; init; }
    public required String Tagline { get; init; }
    public required IReadOnlyList<String> Tones { get; init; }
    public required IReadOnlyList<String> Kinds { get; init; }
    public required IReadOnlyList<String> Industries { get; init; }
    public required IReadOnlyList<String> Keywords { get; init; }
    public required GenerationRequest Request { get; init; }
}

public static class StylePresets
{
    public static readonly IReadOnlyList<StylePreset> All = Build();

    public static StylePreset? Find(String? id)
    {
        if (id.IsNullOrEmpty()) return null;
        var p = All.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (p == null) return null;
        // 深拷贝：调用方改写 Request 不得影响目录（§D 契约）
        return new StylePreset
        {
            Id = p.Id,
            Name = p.Name,
            Tagline = p.Tagline,
            Tones = p.Tones,
            Kinds = p.Kinds,
            Industries = p.Industries,
            Keywords = p.Keywords,
            Request = PresetRecommender.Copy(p.Request),
        };
    }

    static GenerationRequest Req(Double? hue, Double? chroma, String density, Double? typeRatio,
        Double? typeBasePx, Double? radiusBase, Double? motionScale, String industry, params String[] themes) =>
        new()
        {
            Hue = hue,
            Chroma = chroma,
            Density = density,
            TypeRatio = typeRatio,
            TypeBasePx = typeBasePx,
            RadiusBase = radiusBase,
            MotionScale = motionScale,
            Industry = industry,
            Themes = [.. themes],
        };

    static IReadOnlyList<StylePreset> Build()
    {
        const String defaultThemes = "light,dark,high-contrast,compact";
        return
        [
            new StylePreset
            {
                Id = "admin-calm",
                Name = "后台·沉稳",
                Tagline = "后台/中台管理系统的沉稳蓝，信息密度适中",
                Tones = ["calm", "professional", "reliable"],
                Kinds = ["console", "system"],
                Industries = ["general", "devtools"],
                Keywords = ["后台", "管理", "中台", "运营", "报表", "admin", "dashboard"],
                Request = Req(255, 0.15, "default", 1.2, 14, 6, 0.9, "general", "light", "dark", "high-contrast", "compact"),
            },
            new StylePreset
            {
                Id = "workbench-focus",
                Name = "工作台·专注",
                Tagline = "工具/工作台的低彩度紧凑风格，让内容而非界面说话",
                Tones = ["focused", "minimal", "efficient"],
                Kinds = ["console", "product"],
                Industries = ["devtools", "general"],
                Keywords = ["工具", "工作台", "编辑器", "ide", "开发", "效率", "控制台"],
                Request = Req(275, 0.09, "compact", 1.15, 14, 4, 0.8, "devtools", "light", "dark", "high-contrast", "compact"),
            },
            new StylePreset
            {
                Id = "finance-trust",
                Name = "金融·稳健",
                Tagline = "金融/支付/风控场景的稳健蓝，小圆角、克制动效",
                Tones = ["trustworthy", "serious", "precise"],
                Kinds = ["console", "product"],
                Industries = ["finance"],
                Keywords = ["金融", "银行", "支付", "账单", "风控", "保险", "证券"],
                Request = Req(240, 0.12, "default", 1.15, 14, 4, 0.8, "finance", "light", "dark", "high-contrast", "compact"),
            },
            new StylePreset
            {
                Id = "healthcare-gentle",
                Name = "医疗·柔和",
                Tagline = "医疗健康的青绿柔和风格，大圆角更亲切",
                Tones = ["gentle", "clean", "reassuring"],
                Kinds = ["product", "console"],
                Industries = ["healthcare"],
                Keywords = ["医疗", "医院", "患者", "健康", "护理"],
                Request = Req(190, 0.11, "default", 1.25, 16, 10, 1.0, "healthcare", "light", "dark", "high-contrast", "compact"),
            },
            new StylePreset
            {
                Id = "commerce-vivid",
                Name = "电商·活力",
                Tagline = "电商/促销的暖橙活力风格",
                Tones = ["energetic", "friendly", "bold"],
                Kinds = ["marketing", "product"],
                Industries = ["commerce"],
                Keywords = ["电商", "商城", "商品", "购物", "促销", "订单"],
                Request = Req(35, 0.19, "default", 1.25, 16, 8, 1.0, "commerce", "light", "dark", "high-contrast", "compact"),
            },
            new StylePreset
            {
                Id = "media-bold",
                Name = "内容·大胆",
                Tagline = "官网/媒体/品牌站的大胆玫红与强字阶",
                Tones = ["bold", "editorial", "expressive"],
                Kinds = ["brand", "marketing"],
                Industries = ["media"],
                Keywords = ["媒体", "资讯", "视频", "直播", "内容", "官网", "品牌"],
                Request = Req(350, 0.22, "default", 1.4, 16, 4, 1.2, "media", "light", "dark", "high-contrast", "compact"),
            },
            new StylePreset
            {
                Id = "education-friendly",
                Name = "教育·亲和",
                Tagline = "教育/学习产品的亲和绿，宽松密度与大圆角",
                Tones = ["friendly", "playful", "approachable"],
                Kinds = ["product", "marketing"],
                Industries = ["education"],
                Keywords = ["教育", "学习", "课程", "学生", "儿童", "课堂"],
                Request = Req(150, 0.17, "comfortable", 1.3, 16, 12, 1.15, "education", "light", "dark", "high-contrast", "compact"),
            },
            new StylePreset
            {
                Id = "mobile-fresh",
                Name = "移动·清新",
                Tagline = "移动端/H5 的清新天蓝，宽松触控密度",
                Tones = ["fresh", "light", "friendly"],
                Kinds = ["product", "marketing"],
                Industries = ["general"],
                Keywords = ["移动", "H5", "小程序", "app", "手机"],
                Request = Req(205, 0.14, "comfortable", 1.25, 16, 12, 1.0, "general", "light", "dark", "high-contrast", "compact"),
            },
        ];
    }
}
