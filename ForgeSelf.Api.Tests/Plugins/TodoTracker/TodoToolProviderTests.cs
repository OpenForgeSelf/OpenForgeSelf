using System;
using FluentAssertions;
using ForgeSelf.Api.Plugins.TodoTracker;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.TodoTracker;

/// <summary>
/// TodoTracker 工具函数的服务解析回归（与 sems 同类缺陷同源）：
/// 插件 Apply 传进来的是 IContext（自有服务表，无 IServiceScopeFactory），
/// 必须经宿主 seed 进 Context 的根 IServiceProvider 回落，才能 CreateScope 解析插件注册的服务。
/// </summary>
public class TodoToolProviderTests
{
    private sealed class ContextLikeProvider : IServiceProvider
    {
        private readonly IServiceProvider? _inner;
        public ContextLikeProvider(IServiceProvider? inner) => _inner = inner;
        public object? GetService(Type serviceType)
            => serviceType == typeof(IServiceProvider) ? _inner : null;
    }

    [Fact]
    public void Resolve_Context_Wrapped_Provider_Returns_Host_Container()
    {
        var sc = new ServiceCollection();
        sc.AddSingleton(new Marker("host"));
        var host = sc.BuildServiceProvider();

        var resolved = TodoToolProvider.Resolve(new ContextLikeProvider(host));

        resolved.Should().BeSameAs(host);
        resolved!.GetService(typeof(Marker)).Should().NotBeNull();
    }

    [Fact]
    public void Resolve_Plain_Provider_Still_Resolves_Services()
    {
        var sc = new ServiceCollection();
        sc.AddSingleton(new Marker("plain"));
        var plain = sc.BuildServiceProvider();

        // 真容器的 GetService(typeof(IServiceProvider)) 返回根 ServiceProviderEngineScope（非自身实例），
        // 判据必须是「仍能解析服务」，不是引用相等（2026-09-30 实测纠正）。
        var resolved = TodoToolProvider.Resolve(plain);

        resolved.Should().NotBeNull();
        resolved!.GetService(typeof(Marker)).Should().NotBeNull();
        using var scope = resolved.CreateScope();
        scope.ServiceProvider.GetService(typeof(Marker)).Should().NotBeNull();
    }

    [Fact]
    public void Resolve_Null_Returns_Null()
    {
        TodoToolProvider.Resolve(null).Should().BeNull();
    }

    [Fact]
    public void Resolve_Context_Without_Seeded_Provider_Falls_Back_To_Context()
    {
        var ctx = new ContextLikeProvider(null);

        TodoToolProvider.Resolve(ctx).Should().BeSameAs(ctx);
    }

    private sealed record Marker(string Origin);
}
