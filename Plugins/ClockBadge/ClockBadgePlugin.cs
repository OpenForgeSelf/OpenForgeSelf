using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.ClockBadge;

/// <summary>
/// 时钟徽标插件（演示用最小插件）：
/// - 注册一个控制器 GET /api/clock-badge/now（返回 { time, timestamp }）；
/// - 自带 web/ 界面（plugin.json frontend.entry = web/dist/index.js）；
/// - 无实体无库（无 Data/），故不涉及 XCodeConfig.PluginDbs 登记（铁律 12a/12b）。
/// 结构参照 SamplePlugin（可运行最简骨架）+ QuickLinks（带 web/ 先例）。
/// </summary>
public class ClockBadgePlugin : IPlugin
{
    public List<IMenuExtension> MenuExtensions { get; } = new();
    public List<IToolFunctionExtension> ToolExtensions { get; } = new();

    public void Apply(IContext ctx)
    {
        // ① 自身元数据（Id 来自 plugin.json，不硬编码——铁律：Id 单一真源是 plugin.json）
        var pluginId = ctx.Get<PluginMetadata>()?.Id ?? "clock-badge";
        XTrace.Log.Info("[ClockBadge] 初始化时钟徽标插件 (Id={0})", pluginId);

        // ② 本插件无状态，不需要额外的 DI 服务（控制器由宿主 MapControllers 自动装配）。

        // ③ 菜单/路由入口只在 plugin.json frontend 单处声明（铁律 19①），
        //    带界面插件不再重复贡献 IMenuExtension（参照 Home/QuickLinks 均 >= 见其 frontend + menu 扩展各有取舍，
        //    最小插件这里保持零扩展点，菜单由 manifest 机 械派生）。

        XTrace.Log.Info("[ClockBadge] 时钟徽标插件初始化完成");
    }
}
