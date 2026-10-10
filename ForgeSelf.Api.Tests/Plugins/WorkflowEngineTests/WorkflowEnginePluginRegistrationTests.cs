using ForgeSelf.Api.Plugins.WorkflowEngine;
using ForgeSelf.Api.Plugins.WorkflowEngine.Services;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.WorkflowEngineTests;

/// <summary>
/// 工作流引擎插件契约注册单测（2026-10-10 缺陷修复回归）：
/// 此前 WorkflowEnginePlugin 只把 IWorkflowService/IWorkflowExecutor/IWorkflowScheduler
/// AddScoped 进 IServiceCollection，从未 ctx.Register 进插件上下文共享表 → 兄弟插件（AIAgent）
/// 的工作流工具经 IContext 解析恒为 null → 抛「No service for type IWorkflowService」
/// （界面表现为「服务没激活」类报错）。修复后 Apply 内三者必须经 ctx.Register 可解析。
/// </summary>
public class WorkflowEnginePluginRegistrationTests
{
    /// <summary>记录 Register 调用的最小 IContext 桩（GetService 按注册表解析，模拟共享表语义）。</summary>
    private sealed class RecordingContext : IContext
    {
        private readonly Dictionary<Type, object> _shared = new();
        private readonly Dictionary<Type, object> _local = new();

        public void Register<TService>(TService instance) where TService : class
            => _shared[typeof(TService)] = instance;

        public void Register<TService, TImpl>() where TService : class where TImpl : class, TService, new()
            => Register<TService>(new TImpl());

        public void RegisterLocal<TService>(TService instance) where TService : class
            => _local[typeof(TService)] = instance;

        public TService? Get<TService>() where TService : class
            => GetService(typeof(TService)) as TService;

        public object? GetService(Type serviceType)
        {
            if (_local.TryGetValue(serviceType, out var local)) return local;
            return _shared.TryGetValue(serviceType, out var shared) ? shared : null;
        }

        public IDisposable Effect(Func<IDisposable> sideEffect) => sideEffect();
        public IEventBus Events => throw new NotSupportedException();
        public IContext Derive() => throw new NotSupportedException();
    }

    [Fact]
    public void Apply_后_三契约经上下文可解析()
    {
        var ctx = new RecordingContext();
        ctx.RegisterLocal(new PluginMetadata { Id = "workflow-engine" });
        ctx.RegisterLocal<IServiceCollection>(new ServiceCollection());

        new WorkflowEnginePlugin().Apply(ctx);

        // 缺陷回归断言：修复前这三项解析恒为 null（只进了 IServiceCollection，不在共享表）
        Assert.NotNull(ctx.Get<IWorkflowService>());
        Assert.NotNull(ctx.Get<IWorkflowExecutor>());
        Assert.NotNull(ctx.Get<IWorkflowScheduler>());
        // 端到端解析断言：AIAgent 工作流工具运行时就是走这条路解析（WorkflowServiceResolver.Resolve(ctx)），
        // 模拟「两插件共存」的真实组合——修复前此断言必红（工具层抛 No service for type IWorkflowService）
        Assert.NotNull(ForgeSelf.Api.Plugins.AIAgent.WorkflowServiceResolver.Resolve(ctx));
    }
}
