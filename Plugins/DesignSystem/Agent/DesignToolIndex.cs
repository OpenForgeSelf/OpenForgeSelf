namespace ForgeSelf.Api.Plugins.DesignSystem.Agent;

/// <summary>design_* 工具索引：名字/读-写归类/摘要/何时用 + JSON Schema（单一真源，design_guide、meta.agentTools、GET agent/tools 同源）。</summary>
public static class DesignToolIndex
{
    public const String Guide = "design_guide";
    public const String Context = "design_context";
    public const String Lookup = "design_lookup";
    public const String Review = "design_review";
    public const String Audit = "design_audit";
    public const String Presets = "design_presets";
    public const String Create = "design_create";
    public const String Edit = "design_edit";

    public sealed record Entry(String Name, String Kind, String Summary, String When, String Schema);

    public static readonly IReadOnlyList<Entry> All = Build();

    public static Entry Of(String name) => All.First(e => e.Name == name);

    public static Boolean IsDesignTool(String name) => All.Any(e => e.Name == name);

    static List<Entry> Build() =>
    [
        new(Guide, "read", "设计系统使用指南：版本、工具清单、工作流、可选项目与预设，以及外部客户端如何发现工具。",
            "任何设计系统相关工作的第一步；先看 tools 与 workflows 再选工具。",
            "{\"type\":\"object\",\"properties\":{}}"),
        new(Context, "read", "设计说明书（唯一真源）：项目身份、使用规则、颜色/排版/尺度/组件/品牌/交付清单，可 md 或 json。",
            "写 UI 前先读本文；用 sections/maxChars 控制篇幅，format=json 取结构化数据。",
            "{\"type\":\"object\",\"properties\":{\"project\":{\"type\":\"string\",\"description\":\"项目 code 或 id；缺省=唯一非归档项目\"},\"theme\":{\"type\":\"string\",\"description\":\"色向主题 code，缺省=项目默认主题\"},\"sections\":{\"type\":\"array\",\"items\":{\"type\":\"string\"},\"description\":\"identity,rules,colors,typography,scales,components,brand,checklist；identity 恒含\"},\"format\":{\"type\":\"string\",\"enum\":[\"markdown\",\"json\"],\"default\":\"markdown\"},\"maxChars\":{\"type\":\"integer\",\"default\":16000,\"minimum\":2000,\"maximum\":60000}},\"required\":[]}"),
        new(Lookup, "read", "查设计令牌：token 按路径/前缀/tier/type 分页；component 列表或详情；nearest 按值反查最近令牌；export 取导出工件文本；icon 查图标库。",
            "写代码时不知道令牌名 → kind=nearest；需要交接/贴 CSS → kind=export format=css。",
            "{\"type\":\"object\",\"properties\":{\"kind\":{\"type\":\"string\",\"enum\":[\"token\",\"component\",\"nearest\",\"export\",\"icon\"],\"description\":\"查询类别\"},\"project\":{\"type\":\"string\",\"description\":\"项目 code 或 id；icon 可省\"},\"theme\":{\"type\":\"string\"},\"prefix\":{\"type\":\"string\"},\"q\":{\"type\":\"string\"},\"tier\":{\"type\":\"string\",\"enum\":[\"primitive\",\"semantic\",\"component\"]},\"type\":{\"type\":\"string\"},\"limit\":{\"type\":\"integer\",\"default\":50,\"minimum\":1,\"maximum\":200},\"offset\":{\"type\":\"integer\",\"default\":0,\"minimum\":0},\"code\":{\"type\":\"string\"},\"value\":{\"type\":\"string\"},\"property\":{\"type\":\"string\"},\"format\":{\"type\":\"string\"},\"maxChars\":{\"type\":\"integer\",\"default\":16000,\"minimum\":2000,\"maximum\":60000},\"collection\":{\"type\":\"string\"},\"includeSvg\":{\"type\":\"boolean\",\"default\":false}},\"required\":[\"kind\"]}"),
        new(Review, "read", "审查代码与设计系统的一致性：给代码/文件返回硬编码与未知令牌引用；checklist 给出交付前清单。",
            "写完代码后必跑：code 模式给 files 或 code+language；strict=true 把 warning 计为 error。",
            "{\"type\":\"object\",\"properties\":{\"mode\":{\"type\":\"string\",\"enum\":[\"code\",\"checklist\"],\"default\":\"code\"},\"project\":{\"type\":\"string\"},\"theme\":{\"type\":\"string\"},\"files\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"properties\":{\"path\":{\"type\":\"string\"},\"content\":{\"type\":\"string\"}},\"required\":[\"path\",\"content\"]},\"description\":\"≤200 个文件、总内容 ≤200KB\"},\"code\":{\"type\":\"string\"},\"language\":{\"type\":\"string\",\"enum\":[\"css\",\"scss\",\"less\",\"vue\",\"html\",\"tsx\",\"jsx\",\"ts\",\"js\"]},\"strict\":{\"type\":\"boolean\",\"default\":false},\"maxFindings\":{\"type\":\"integer\",\"default\":100,\"minimum\":1,\"maximum\":500},\"page\":{\"type\":\"string\",\"enum\":[\"dashboard\",\"list\",\"form\",\"detail\",\"login\",\"settings\",\"workbench\",\"landing\",\"mobile\",\"any\"],\"default\":\"any\"}},\"required\":[]}"),
        new(Audit, "read", "读可达性审计结论；run=true 重跑并落库（写动作）。",
            "发布前必看 summary.blocking；critical 未清时 design_edit publish 会被拒。",
            "{\"type\":\"object\",\"properties\":{\"project\":{\"type\":\"string\"},\"run\":{\"type\":\"boolean\",\"default\":false,\"description\":\"true=重跑并落库（写动作）\"},\"kind\":{\"type\":\"string\"},\"onlyFailed\":{\"type\":\"boolean\",\"default\":true},\"limit\":{\"type\":\"integer\",\"default\":50,\"minimum\":1,\"maximum\":200}},\"required\":[]}"),
        new(Presets, "read", "风格预设目录：list 给 8 个预设全字段；recommend 按 brief/industry/kind/tone/density 打分推荐。",
            "为新项目建设计系统前先用 recommend 选预设，再 design_create 落地。",
            "{\"type\":\"object\",\"properties\":{\"action\":{\"type\":\"string\",\"enum\":[\"list\",\"recommend\"],\"default\":\"list\"},\"brief\":{\"type\":\"string\"},\"industry\":{\"type\":\"string\"},\"kind\":{\"type\":\"string\",\"enum\":[\"product\",\"console\",\"brand\",\"marketing\",\"system\"]},\"tone\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}},\"brandColor\":{\"type\":\"string\"},\"density\":{\"type\":\"string\",\"enum\":[\"default\",\"compact\",\"comfortable\"]},\"limit\":{\"type\":\"integer\",\"default\":3,\"minimum\":1,\"maximum\":8}},\"required\":[]}"),
        new(Create, "write", "快速创建设计系统项目：从预设 + 显式参数生成令牌/组件/审计；apply=false 干跑不写库。",
            "新项目第一步；先 design_presets recommend 选预设，再 apply=true 落地。",
            "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\",\"description\":\"显示名（必填，中文可）\"},\"code\":{\"type\":\"string\",\"pattern\":\"^[a-z0-9][a-z0-9-]{0,39}$\",\"description\":\"自动分配可不填\"},\"kind\":{\"type\":\"string\",\"enum\":[\"product\",\"console\",\"brand\",\"marketing\",\"system\"],\"default\":\"product\"},\"description\":{\"type\":\"string\"},\"preset\":{\"type\":\"string\",\"description\":\"§D 预设 id，未知→错误并列出全部\"},\"brief\":{\"type\":\"string\"},\"seedColor\":{\"type\":\"string\",\"description\":\"hex 或 oklch(...)；覆盖预设种子\"},\"hue\":{\"type\":\"number\"},\"chroma\":{\"type\":\"number\"},\"density\":{\"type\":\"string\"},\"typeRatio\":{\"type\":\"number\"},\"typeBasePx\":{\"type\":\"number\"},\"radiusBase\":{\"type\":\"number\"},\"motionScale\":{\"type\":\"number\"},\"brandName\":{\"type\":\"string\"},\"industry\":{\"type\":\"string\"},\"themes\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}},\"apply\":{\"type\":\"boolean\",\"default\":false,\"description\":\"false=干跑不写库\"}},\"required\":[\"name\"]}"),
        new(Edit, "write", "改设计系统：set_token 写单个令牌、regenerate 重新生成、publish 发布版本（critical 未清会被拒）。",
            "缺令牌/改令牌 → set_token；整体重做 → regenerate；交付 → publish。",
            "{\"type\":\"object\",\"properties\":{\"project\":{\"type\":\"string\"},\"action\":{\"type\":\"string\",\"enum\":[\"set_token\",\"regenerate\",\"publish\"],\"description\":\"必填\"},\"path\":{\"type\":\"string\",\"description\":\"set_token 必填：令牌路径（点分 kebab）\"},\"value\":{\"type\":\"string\",\"description\":\"set_token：字面值（与 alias 二选一）\"},\"alias\":{\"type\":\"string\",\"description\":\"set_token：别名目标路径（不含花括号）\"},\"theme\":{\"type\":\"string\"},\"tier\":{\"type\":\"string\",\"enum\":[\"primitive\",\"semantic\",\"component\"]},\"type\":{\"type\":\"string\"},\"description\":{\"type\":\"string\"},\"overwrite\":{\"type\":\"boolean\",\"default\":false},\"apply\":{\"type\":\"boolean\",\"default\":false},\"version\":{\"type\":\"string\",\"description\":\"publish 必填\"},\"notes\":{\"type\":\"string\"}},\"required\":[\"action\"]}"),
    ];
}
