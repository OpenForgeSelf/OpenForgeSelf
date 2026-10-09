namespace ForgeSelf.Api.Plugins.AgentHub.Services;

/// <summary>
/// 插件侧解析宿主 DI 服务的统一入口（PILOT-055 走查实证修复）。
///
/// 背景：<see cref="ForgeSelf.Core.IContext"/>（<c>ForgeSelf.Core.Context.GetService</c>）只解析
/// 「本地值 + 全局共享表」，<b>不包含宿主 Microsoft DI 容器</b>。AgentDelegationProvider 与工具基类
/// 此前直接用插件 ctx 当容器 <c>GetService</c> 取 <see cref="DelegationRuntime"/>/<see cref="IAgentRegistry"/>
/// —— 这两个服务是插件在 Apply 里 <c>services.AddSingleton</c> 进宿主容器的，ctx 里永远解析不到，
/// 于是「接缝发起」路径恒回 <c>AgentHub 运行时不可用（插件未正确加载）</c>、预览恒给空候选，
/// 而控制器（走 MVC 宿主 DI）一切正常——同一功能的两种入口表现不一致。
///
/// 正确姿势：宿主已在 <c>PluginManager.ProvideHostServices</c> 把<b>根 <c>IServiceProvider</c></b>
/// seed 进 root Context（<c>ctx.Get&lt;IServiceProvider&gt;()</c> 是官方回落姿势，MemorySystem/SamplePlugin 同款）。
/// 解析必须先经它取宿主 provider，再 <c>GetService</c>，才能拿到与控制器<b>同一份单例</b>
/// （若在 Apply 里另 <c>BuildServiceProvider</c> 会造出第二份 PermissionBroker/AgentRegistry，
/// G2「接缝发起的任务」与「控制器审批面板」的待审批队列就不是同一份，人在回路直接失灵）。
/// </summary>
internal static class AgentHubDi
{
    /// <summary>从插件上下文解析宿主 DI 服务；取不到宿主 provider 时回落原容器（兼容直传宿主 provider 的用法）。</summary>
    public static T? ResolveHost<T>(IServiceProvider? services) where T : class
        => (services?.GetService(typeof(IServiceProvider)) as IServiceProvider ?? services)?.GetService(typeof(T)) as T;
}
