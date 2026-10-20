using System;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeSelf.Abstractions;

/// <summary>
/// 插件侧解析宿主 DI 容器的<b>唯一公共入口</b>（2026-10-17 用户拍板「写个公共方法统一这一个动作，杜绝问题」）。
///
/// 背景（复现批实证，docs/ai/pilot/2026-10-17-gateway-list-todos-scope-repro/）：
/// 插件 <c>Apply(IContext)</c> 在 <c>builder.Build()</c> <b>之前</b>执行，而宿主根
/// <see cref="IServiceProvider"/> 要到 Build 之后的 <c>PluginManager.ProvideHostServices</c>
/// 才 seed 进根 Context——在构造期解析恒失败，回落后把插件 Context 本体存进字段，
/// 请求期 <c>CreateScope()</c> 抛出费解的「No service for type 'IServiceScopeFactory' has been registered.」
/// （TodoTracker / sems / AgentHub 已三次踩中同一模式）。
///
/// 规则（架构守卫 <c>ForgeSelf.Api.Tests/Architecture/PluginToolConstructorDiGuardTests</c> 机械执行）：
/// <list type="number">
/// <item>构造函数只存原始上下文（ctx/provider 本体），<b>禁止</b>构造期解析服务；</item>
/// <item>需要宿主服务时在 <c>ExecuteAsync</c>/请求处理内调本类——每次调用即时解析，seed 完成后自然可用；</item>
/// <item>未就绪时本类抛指名道姓的 <see cref="InvalidOperationException"/>，不再让 MS DI 冒费解文案。</item>
/// </list>
/// 只依赖 <c>ForgeSelf.Core</c>（经由调用方传入的 provider 形态）与 DI.Abstractions；禁止引入 NewLife/XTrace。
/// </summary>
public static class PluginContextDiExtensions
{
    /// <summary>未 seed 场景的统一错误文案（三要素：现状 / seed 时机 / 正确用法）。测试逐字锚定，改动须同步守卫与用例。</summary>
    public const string NotSeededError =
        "宿主根 IServiceProvider 尚未注册进插件上下文（ProvideHostServices 在 builder.Build() 之后才 seed）。" +
        "本方法只能在执行期（ExecuteAsync/请求处理）调用，禁止在插件 Apply 构造期解析服务——" +
        "构造时只保存原始上下文，用时再调 RequireHostProvider/CreateHostScope。";

    /// <summary>
    /// 解析可建作用域的宿主容器：优先取宿主 seed 进 Context 的根 <see cref="IServiceProvider"/>；
    /// 直传真实 MS 容器（宿主代码/测试组装）时原样可用；两者皆非则抛 <see cref="NotSeededError"/>。
    /// </summary>
    public static IServiceProvider RequireHostProvider(this IServiceProvider? services)
    {
        if (services == null)
            throw new InvalidOperationException("服务提供者为 null。" + NotSeededError);

        // ① 宿主 seed 姿势（PluginManager.ProvideHostServices 契约清单含 typeof(IServiceProvider)）；
        //    真实 MS 容器上此调用返回其根 scope（同样可建作用域），一并覆盖。
        if (services.GetService(typeof(IServiceProvider)) is IServiceProvider seeded)
            return seeded;

        // ② 能力探测：有 IServiceScopeFactory 说明拿到的是真容器（直传场景），可直接建作用域。
        if (services.GetService(typeof(IServiceScopeFactory)) is not null)
            return services;

        // ③ 既非 seed 命中也非真容器（典型：Apply 期传入、宿主尚未 seed 的 Context 本体）——明确报错。
        throw new InvalidOperationException(NotSeededError);
    }

    /// <summary>
    /// 执行期创建一个宿主容器作用域（= <see cref="RequireHostProvider"/> 后 <c>CreateScope()</c>）。
    /// 每次调用即时解析、不缓存结果——这是对 Apply/seed 时序免疫的根。调用方负责 dispose。
    /// </summary>
    public static IServiceScope CreateHostScope(this IServiceProvider? services)
        => services.RequireHostProvider().CreateScope();
}
