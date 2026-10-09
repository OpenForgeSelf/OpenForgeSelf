using FluentAssertions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AgentHub.Models;
using ForgeSelf.Api.Plugins.AgentHub.Profiles;
using ForgeSelf.Api.Plugins.AgentHub.Services;
using ForgeSelf.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
// Abstractions 里另有同名 IAgentRegistry（Agent 运行时注册表契约），本文件用的是本插件的注册中心，显式别名消歧。
using IAgentRegistry = ForgeSelf.Api.Plugins.AgentHub.Services.IAgentRegistry;

namespace ForgeSelf.Api.Tests.Plugins.AgentHub;

/// <summary>
/// AgentDelegationProvider 宿主 DI 解析回归（PILOT-055 走查实证修复）。
///
/// 缺陷现场：接缝在（<c>ctx.Get&lt;IAgentDelegation&gt;()</c> 能取到），但 <c>ListAvailableAgents()</c> 恒空、
/// <c>SubmitAsync</c> 恒回「AgentHub 运行时不可用（插件未正确加载）」——因为 provider 直接用插件 ctx 当容器
/// <c>GetService</c> 取 <see cref="DelegationRuntime"/>/<see cref="IAgentRegistry"/>，而 <c>ForgeSelf.Core.Context</c>
/// 只解析「本地值 + 全局共享表」，不含宿主 MS DI；这两个服务实际注册在宿主容器。
///
/// 本测试模拟宿主 <c>PluginManager.ProvideHostServices</c> 的 seed 姿势（<c>ctx.Register(typeof(IServiceProvider), host)</c>），
/// 断言：① provider 能经宿主 provider 解析到与控制器同一份注册表单例（登记后立即可见）；② 未 seed 宿主 provider 的裸
/// 上下文按旧路径解析不到（Mutation 探针：没有修复时本用例必红）。
/// </summary>
[Collection("XCode")]
public class AgentDelegationProviderResolutionTests : IClassFixture<XCodeTestFixture>
{
    private readonly XCodeTestFixture _fixture;

    public AgentDelegationProviderResolutionTests(XCodeTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task 接缝_经宿主provider解析_候选与控制器同源()
    {
        // 模拟宿主容器：插件 Apply 里 AddSingleton 的那批服务（与 AgentHubPlugin.RegisterServices 同形状）
        var services = new ServiceCollection();
        services.AddSingleton<ProfileLoader>();
        services.AddSingleton<IAgentRegistry, AgentRegistry>();
        services.AddSingleton<DelegationRuntime>();
        var host = services.BuildServiceProvider();

        // 宿主 ProvideHostServices 特判：把根 IServiceProvider seed 进 root Context
        var ctx = new Context();
        ctx.Register(typeof(IServiceProvider), host);

        var provider = new AgentDelegationProvider(ctx);
        var registry = host.GetService<IAgentRegistry>()
            ?? throw new InvalidOperationException("测试夹具自身错误：宿主 provider 应能解析 IAgentRegistry");

        // 经宿主注册表登记（与控制器同一条路），断言接缝立即可见
        var name = $"res-{Guid.NewGuid():N}"[..20];
        registry.Create(new AgentSaveRequest { Name = name, Vendor = "opencode" });

        try
        {
            var agents = provider.ListAvailableAgents();
            agents.Should().Contain(a => a.Name == name,
                "接缝必须经宿主 provider 解析到与控制器同一份注册表单例（旧实现直接 ctx.GetService 恒空）");
            agents.Single(a => a.Name == name).Vendor.Should().Be("opencode");
        }
        finally
        {
            var dto = registry.GetByName(name);
            if (dto != null) registry.Delete(dto.Id);
        }
    }

    [Fact]
    public async Task 裸上下文_未seed宿主provider_按旧路径解析不到()
    {
        // Mutation 探针：ctx 里没有 IServiceProvider 时（旧实现 / 未 seed 的环境），必须走「运行时不可用」失败分支，
        // 不得抛异常、不得误当作成功——同时证明 ResolveHost 的回落不会凭空造出服务。
        var provider = new AgentDelegationProvider(new Context());

        provider.ListAvailableAgents().Should().BeEmpty("宿主 provider 缺失时候选必须为空，让消费方走「无候选」分支");

        var outcome = await provider.SubmitAsync(new AgentDelegationRequest { Prompt = "x" });
        outcome.Success.Should().BeFalse();
        outcome.Error.Should().Contain("AgentHub 运行时不可用");
    }
}
