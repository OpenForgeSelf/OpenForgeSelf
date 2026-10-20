using System;
using FluentAssertions;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ForgeSelf.Api.Tests;

/// <summary>
/// <see cref="PluginContextDiExtensions"/> 公共助手回归（PILOT-062，2026-10-17「全部都做」批）。
/// 取代原 <c>TodoToolProviderTests</c>（TodoToolProvider 已删，语义迁移至此并按新规则强化：
/// 解析发生在调用时刻而非构造时刻，seed 时序无关；未 seed 必须报明确错误而非 MS DI 费解文案）。
/// </summary>
public class PluginContextDiExtensionsTests
{
    private sealed record Marker(string Origin);

    [Fact]
    public void RequireHostProvider_SeededContext_Resolves_Host_Root_Provider()
    {
        using var root = new Context();
        var pluginCtx = root.Derive();
        using var host = new ServiceCollection().AddSingleton(new Marker("host")).BuildServiceProvider();
        // 宿主 seed 姿势（PluginManager.ProvideHostServices）：根 provider 注册进 root Context
        root.Register(typeof(IServiceProvider), host);

        pluginCtx.RequireHostProvider().Should().BeSameAs(host);

        using var scope = ((IServiceProvider)pluginCtx).CreateHostScope();
        scope.ServiceProvider.GetService<Marker>().Should().NotBeNull();
    }

    [Fact]
    public void RequireHostProvider_PlainHostContainer_Still_Resolves_Services()
    {
        using var plain = new ServiceCollection().AddSingleton(new Marker("plain")).BuildServiceProvider();

        // 真容器的 GetService(typeof(IServiceProvider)) 返回根 scope（非自身实例），
        // 判据必须是「仍能建作用域解析服务」，不是引用相等（沿用旧 TodoToolProviderTests 的实测纠正）。
        using var scope = ((IServiceProvider)plain).CreateHostScope();
        scope.ServiceProvider.GetService<Marker>().Should().NotBeNull();
    }

    [Fact]
    public void RequireHostProvider_UnseededContext_Throws_Explicit_Seed_Timing_Error()
    {
        using var ctx = new Context();

        var ex = ctx.Invoking(c => c.RequireHostProvider()).Should().Throw<InvalidOperationException>().Which;

        // 杜绝「No service for type 'IServiceScopeFactory' has been registered.」这类离真因极远的文案
        ex.Message.Should().Contain(PluginContextDiExtensions.NotSeededError)
            .And.Contain("ProvideHostServices", "须指明 seed 时机")
            .And.Contain("ExecuteAsync", "须指明正确调用时刻");
    }

    [Fact]
    public void RequireHostProvider_Null_Throws_Explicit_Error()
    {
        // FluentAssertions 的 Invoking 拒收 null subject，故用 Action 包装直接调扩展方法
        Action act = () => ((IServiceProvider?)null!).RequireHostProvider();
        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain(PluginContextDiExtensions.NotSeededError);
    }

    [Fact]
    public void CreateHostScope_IsEvaluated_Per_Call_Not_Cached_So_Later_Seed_Takes_Effect()
    {
        using var root = new Context();
        using var host = new ServiceCollection().AddSingleton(new Marker("late")).BuildServiceProvider();

        // 第一次调用（未 seed）失败——正是插件 Apply 期的世界
        root.Invoking(c => c.CreateHostScope()).Should().Throw<InvalidOperationException>();

        // seed 之后**同一个**上下文实例再调用即成功：证明解析发生在调用时刻、无构造期缓存
        root.Register(typeof(IServiceProvider), host);
        using var scope = ((IServiceProvider)root).CreateHostScope();
        scope.ServiceProvider.GetService<Marker>().Should().NotBeNull();
    }
}
