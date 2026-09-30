using System.Text.Json;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>内置图标定义（代码内建，不引第三方图标包/字体）</summary>
/// <param name="Code">稳定标识（界面与令牌引用它，改名即破坏兼容）</param>
/// <param name="Tags">逗号分隔检索词（中英文都放，用户只会搜自己熟悉的词）</param>
/// <param name="Usage">用途约束（写"什么时候用它"，避免界面里图标乱配）</param>
public sealed record BuiltinIcon(String Code, String Name, String SvgBody, String Tags, String Usage = "");

/// <summary>
/// 内置图标库 <c>forge</c>：插件自带的一套零许可证负担图标。
///
/// 为什么自带而不是引 lucide/tabler/Font Awesome：
/// 1. 第三方图标包都有许可证与归属要求（MIT 也要保留版权声明、部分字体图标另有 OFL/商标条款），
///    "内置即用"若把外部许可带进产物，用户在商业系统里就得替我们做一次合规判断 —— 这不该由设计系统插件转嫁；
/// 2. 本套图形是本项目原创绘制（作者：ForgeSelf Team，2026-09），License 记 <c>Owned</c>，无归属义务；
/// 3. 用户仍可导入自己的图标集（<c>DesignIcon</c> 支持任意 Collection），内置集只是"开箱就有一套能用且好看的"。
///
/// 绘制规范（全套一致才谈得上"设计系统"）：
/// - 24×24 网格，viewBox <c>0 0 24 24</c>；描边 1.5px、<c>round</c> 端点与拐角、不填充；
/// - 视觉留白 2px（图形基本落在 3~21 区间），主体重心居中；
/// - 需要实心点（如提示类图标的感叹号下点）时在该元素上显式写 fill="currentColor"；
/// - svgBody 只存 <c>&lt;svg&gt;</c> 的内部内容（一个 &lt;g&gt; 包裹），渲染方负责 viewBox 与尺寸。
/// </summary>
public static class BuiltinIcons
{
    /// <summary>描边包装：所有图标共用同一组绘制属性，改一处即全套一致</summary>
    const String G = """<g fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">""";

    static String I(String body) => G + body + "</g>";

    /// <summary>实心小圆点（感叹号 / 状态指示用）</summary>
    static String Dot(Double x, Double y, Double r = 0.9) =>
        $"""<circle cx="{Num(x)}" cy="{Num(y)}" r="{Num(r)}" fill="currentColor" stroke="none"/>""";

    static String Num(Double v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    public static readonly IReadOnlyList<BuiltinIcon> All =
    [
        new("home", "首页", I("""<path d="M3 10.5 12 3l9 7.5"/><path d="M5.5 9.5V20h13V9.5"/><path d="M10 20v-5h4v5"/>"""), "首页,概览,home,仪表盘入口", "系统主入口；不放二级页面"),
        new("search", "搜索", I("""<circle cx="11" cy="11" r="6.5"/><path d="m16 16 4.6 4.6"/>"""), "搜索,查找,search,过滤入口", "检索类动作的通用图标"),
        new("sliders", "设置", I("""<path d="M4 7h9"/><path d="M18 7h2"/><circle cx="16" cy="7" r="2"/><path d="M4 12h4"/><path d="M12 12h8"/><circle cx="10" cy="12" r="2"/><path d="M4 17h9"/><path d="M18 17h2"/><circle cx="16" cy="17" r="2"/>"""), "设置,偏好,调节,sliders,settings", "参数/偏好设置；不要用齿轮表示「系统级配置」"),
        new("user", "用户", I("""<circle cx="12" cy="8" r="3.5"/><path d="M5 20c1.6-3.4 4-5 7-5s5.4 1.6 7 5"/>"""), "用户,个人,账户,user,头像", "单人主体；多人用 users"),
        new("users", "群组", I("""<circle cx="9" cy="8" r="3.2"/><path d="M3 20c1.4-3 3.4-4.5 6-4.5s4.6 1.5 6 4.5"/><path d="M16 5.2a3.2 3.2 0 0 1 0 5.6"/><path d="M18 15.6c1.9.6 3.3 2 4.3 4.4"/>"""), "群组,团队,成员,users,协作", "多人/角色/共享对象"),
        new("bell", "通知", I("""<path d="M6 16V11a6 6 0 0 1 12 0v5l1.6 2.2H4.4L6 16Z"/><path d="M10 20.6a2 2 0 0 0 4 0"/>"""), "通知,提醒,bell,消息", "未读提醒配红点组件，不用本图标表达数量"),
        new("folder", "目录", I("""<path d="M3 7.5A1.5 1.5 0 0 1 4.5 6h4l2 2.6h7A1.5 1.5 0 0 1 19 10.1v7.4A1.5 1.5 0 0 1 17.5 19h-13A1.5 1.5 0 0 1 3 17.5V7.5Z"/>"""), "目录,文件夹,folder,分组", "层级容器；单篇文档用 file"),
        new("file", "文档", I("""<path d="M7 3h6l4 4v14H7V3Z"/><path d="M13 3v4h4"/>"""), "文档,文件,file,详情页", "任意单文档；类型细分靠角标"),
        new("calendar", "日期", I("""<rect x="3" y="5" width="18" height="16" rx="2"/><path d="M3 10h18"/><path d="M8 3v4"/><path d="M16 3v4"/>"""), "日期,日历,calendar,排期,时间", "日期选择与时间范围"),
        new("chart-bar", "柱状图", I("""<path d="M3 21h18"/><path d="M7 21v-6"/><path d="M12 21V9"/><path d="M17 21v-9"/>"""), "柱状图,统计,chart-bar,指标", "对比类目数量；趋势用 chart-line"),
        new("chart-line", "折线图", I("""<path d="M3 21h18"/><path d="m5 16 4-5 3 3 5-7"/>"""), "折线图,趋势,chart-line,监控", "时间序列趋势"),
        new("layers", "分层", I("""<path d="m12 3 9 5-9 5-9-5 9-5Z"/><path d="M3 13.2 12 18l9-4.8"/>"""), "分层,图层,layers,架构,堆叠", "层级结构/组合关系"),
        new("database", "数据库", I("""<ellipse cx="12" cy="6" rx="8" ry="3"/><path d="M4 6v12c0 1.7 3.6 3 8 3s8-1.3 8-3V6"/><path d="M4 12c0 1.7 3.6 3 8 3s8-1.3 8-3"/>"""), "数据库,存储,仓库,database,数据集", "持久化与数据源"),
        new("shield", "安全", I("""<path d="M12 3l7 3v6c0 4-3 6.6-7 9-4-2.4-7-5-7-9V6l7-3Z"/>"""), "安全,防护,shield,权限,合规", "安全/风控/授权状态"),
        new("lock", "锁定", I("""<rect x="5" y="11" width="14" height="9" rx="2"/><path d="M8 11V8a4 4 0 0 1 8 0v3"/>"""), "锁定,私有,lock,加密", "只读/受限；解锁态用 unlock"),
        new("unlock", "解锁", I("""<rect x="5" y="11" width="14" height="9" rx="2"/><path d="M8 11V8a4 4 0 0 1 7.6-1.7"/>"""), "解锁,开放,unlock,可编辑", "可编辑/已授权"),
        new("mail", "邮件", I("""<rect x="3" y="6" width="18" height="12" rx="2"/><path d="m4 8 8 5.4L20 8"/>"""), "邮件,联系,mail,收件", "邮箱与联系渠道"),
        new("monitor", "终端屏", I("""<rect x="3" y="5" width="18" height="11" rx="2"/><path d="M9 20h6"/><path d="M12 16v4"/>"""), "屏幕,终端,monitor,运行环境", "运行环境/设备视图"),
        new("cloud", "云", I("""<path d="M7 18a4.5 4.5 0 0 1-.6-8.9A6 6 0 0 1 18 10.6 3.8 3.8 0 0 1 17.4 18H7Z"/>"""), "云,同步,cloud,托管", "云端与同步状态"),
        new("download", "下载", I("""<path d="M12 3v11"/><path d="m7 10 5 5 5-5"/><path d="M4 20h16"/>"""), "下载,导出,download,保存", "取回本地；交付动作可用 export"),
        new("upload", "上传", I("""<path d="M12 20V9"/><path d="m7 12 5-5 5 5"/><path d="M4 4h16"/>"""), "上传,导入,upload", "提交到远端/导入"),
        new("refresh", "刷新", I("""<path d="M20.5 12a8.5 8.5 0 1 1-2.5-6"/><path d="M20.5 4.5V9H16"/>"""), "刷新,重试,refresh,重新加载", "重新取数；不要用播放键代劳"),
        new("check", "成功", I("""<path d="m5 13 4.2 4.2L19 6.5"/>"""), "完成,成功,check,通过,对勾", "已达成/校验通过"),
        new("close", "关闭", I("""<path d="m6 6 12 12"/><path d="M18 6 6 18"/>"""), "关闭,删除,close,取消,叉", "关闭与否定；破坏性动作要再加 shield/警示文案"),
        new("plus", "新增", I("""<path d="M12 5v14"/><path d="M5 12h14"/>"""), "新增,添加,plus,创建", "创建一条记录"),
        new("minus", "移除", I("""<path d="M5 12h14"/>"""), "减少,移除,minus,收起", "收起/减量；删除数据别用它"),
        new("alert-triangle", "警告", I($"""<path d="M12 4 21 19H3L12 4Z"/><path d="M12 10.2v3.8"/>{Dot(12, 16.6)}"""), "警告,风险,alert,异常,注意", "需人处理但未失败；失败用 close/ danger 文案"),
        new("info", "说明", I($"""<circle cx="12" cy="12" r="8.5"/><path d="M12 11v5"/>{Dot(12, 7.9)}"""), "说明,提示,info,帮助", "解释性信息，不带情绪"),
        new("star", "收藏", I("""<path d="m12 4 2.5 5.1 5.6.8-4 4 .9 5.6L12 17.2 7 19.5l.9-5.6-4-4 5.6-.8L12 4Z"/>"""), "收藏,重要,star,标记", "用户主动标记"),
        new("heart", "喜欢", I("""<path d="M12 20s-7-4.3-7-9a4 4 0 0 1 7-2.6A4 4 0 0 1 19 11c0 4.7-7 9-7 9Z"/>"""), "喜欢,点赞,heart", "正向反馈"),
        new("tag", "标签", I($"""<path d="M4 4h7l9 9-7 7-9-9V4Z"/>{Dot(8, 8, 0.8)}"""), "标签,分类,tag,归档", "分类与检索维度"),
        new("link", "链接", I("""<path d="m9.6 14.4 4.8-4.8"/><path d="M11.2 7.2 12.8 5.6a4 4 0 0 1 5.6 5.6l-1.6 1.6"/><path d="M12.8 16.8l-1.6 1.6a4 4 0 0 1-5.6-5.6l1.6-1.6"/>"""), "链接,关联,link,引用", "跨对象跳转/引用关系"),
        new("eye", "查看", I("""<path d="M2.5 12S6 6.5 12 6.5 21.5 12 21.5 12 18 17.5 12 17.5 2.5 12 2.5 12Z"/><circle cx="12" cy="12" r="3"/>"""), "查看,可见,预览,eye", "预览与可见性开关"),
        new("filter", "筛选", I("""<path d="M4 6h16"/><path d="M7 12h10"/><path d="M10 18h4"/>"""), "筛选,过滤,filter,收窄", "列表条件收窄；不要与 search 混用"),
        new("grid", "网格视图", I("""<rect x="4" y="4" width="7" height="7" rx="1.2"/><rect x="13" y="4" width="7" height="7" rx="1.2"/><rect x="4" y="13" width="7" height="7" rx="1.2"/><rect x="13" y="13" width="7" height="7" rx="1.2"/>"""), "网格,卡片视图,grid,总览", "多对象并列浏览"),
        new("terminal", "命令行", I("""<rect x="3" y="5" width="18" height="14" rx="2"/><path d="m7 10 3 2-3 2"/><path d="M13 15h4"/>"""), "命令,终端,脚本,terminal,控制台", "命令与脚本类功能"),
        new("play", "执行", I("""<path d="m8 5 11 7-11 7V5Z"/>"""), "执行,运行,播放,play", "启动一次执行；停止用 close/pause 语义"),
        new("palette", "设计", I($"""<path d="M12 3a9 9 0 1 0 0 18c1.4 0 1.9-1 1.2-1.9-.7-.9.1-2.1 1.3-2.1H17a4 4 0 0 0 4-4c0-4.4-4-10-9-10Z"/>{Dot(8.5, 10.5)}{Dot(12, 7.8)}{Dot(15.6, 9.6)}"""), "设计,主题,调色板,palette,品牌", "设计系统/主题相关入口"),
        new("arrow-right", "前进", I("""<path d="M4 12h15"/><path d="m14 7 5 5-5 5"/>"""), "前进,下一步,箭头,arrow", "方向推进/跳转"),
        new("book", "手册", I("""<path d="M4 5.5A1.5 1.5 0 0 1 5.5 4H11v16H5.5A1.5 1.5 0 0 1 4 18.5v-13Z"/><path d="M20 5.5A1.5 1.5 0 0 0 18.5 4H13v16h5.5a1.5 1.5 0 0 0 1.5-1.5v-13Z"/>"""), "文档,手册,规范,book,指南", "规范与说明类页面"),
    ];

    /// <summary>
    /// 首植内置图标库（幂等：已存在同 code 就只更新绘制，不新增行）。
    /// 返回本次写入条数，供启动日志与测试断言。
    /// </summary>
    public static Int32 Seed(CatalogRepository catalog)
    {
        var count = 0;
        foreach (var icon in All)
        {
            catalog.SeedBuiltinIcon(new IconInput
            {
                Code = icon.Code,
                Name = icon.Name,
                SvgBody = icon.SvgBody,
                Tags = icon.Tags,
                Usage = icon.Usage,
                StrokeWidth = 1.5,
                GridPx = 24,
                ViewBox = "0 0 24 24",
                Sizes = "16,20,24,32",
                License = "Owned",
            });
            count++;
        }
        return count;
    }

    /// <summary>已有内置图标数（直查库，绕开 XCode 实体级缓存，铁律 11）</summary>
    public static Int32 ExistingCount() =>
        Entities.DesignIcon.FindAll(Entities.DesignIcon._.ProjectId == DesignSystemConstants.BuiltinProjectId).Count;

    /// <summary>缺才补、有则跳过：插件每次启动都可能被调用，不能每次重写全套</summary>
    public static Int32 SeedIfMissing(CatalogRepository catalog) =>
        ExistingCount() >= All.Count ? 0 : Seed(catalog);

    /// <summary>导出成 JSON（给界面/测试核对绘制规范用；不进交付物）</summary>
    public static String ToJson() => JsonSerializer.Serialize(All.Select(a => new { a.Code, a.Name, a.Tags, a.Usage }),
        new JsonSerializerOptions { WriteIndented = true });
}
