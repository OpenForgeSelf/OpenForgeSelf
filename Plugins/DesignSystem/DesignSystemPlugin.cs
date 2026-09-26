using ForgeSelf.Abstractions;
using ForgeSelf.Core;

namespace ForgeSelf.Api.Plugins.DesignSystem;

/// <summary>
/// 设计系统插件（Stardust Design System）。
///
/// 职责：仅贡献「自带界面」——在宿主侧边栏注册「设计系统」入口（route /design-system），
/// 由 <see cref="plugin.json"/> 的 frontend 块声明，运行时经 /plugins/design-system/web/dist/index.js
/// 远程加载。后端无独立服务/工具/端点，故 Apply 为空实现（契约要求 IPlugin.Apply）。
/// </summary>
public class DesignSystemPlugin : IPlugin
{
    public void Apply(IContext ctx)
    {
        // 前端贡献完全由 plugin.json 的 frontend 块驱动（菜单/路由/视图），
        // 宿主 PluginController 读取清单后动态注册路由与侧栏项，无需后端代码干预。
    }
}
