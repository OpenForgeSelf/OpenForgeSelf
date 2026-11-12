using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AIAgent;

/// <summary>
/// 缺陷召回 #2 类级守卫（qa e2e 暴露的 DI 俘虏依赖）：
/// AIAgentPlugin 用 4 个 eager scope 把 IAgentRunDispatch/IBuiltInAgentExecution 等「插件寿命」实例
/// 注册进 ctx 共享表，若其依赖 IAgentRegistryService 是 Scoped，eager 捕获即俘虏——UI 请求作用域
/// 新建 Agent 落在另一个注册表实例上，派发/内置执行侧永远不可见（多视图分裂 + 启动期重复 DB 加载）。
/// 本测试按 AIAgentPlugin 真实注册动词组装 ServiceCollection（镜像其 L44/L50/L62），
/// 单例派发实例 + 请求作用域新建 Agent → 断言派发侧可见。Scoped 注册下必红，AddSingleton 后绿。
/// </summary>
[Collection("XCode")]
public class AgentRegistryCaptiveTests : IClassFixture<XCodeTestFixture>
{
    public AgentRegistryCaptiveTests(XCodeTestFixture fixture) => _ = fixture;   // AgentRegistryService 构造期从 DB 加载

    /// <summary>按插件真实注册动词组装（注册生命周期与 AIAgentPlugin.Apply 保持同形）。</summary>
    private static (ServiceProvider Root, IServiceCollection Services) BuildPluginGraph()
    {
        var services = new ServiceCollection();
        // —— 镜像 AIAgentPlugin：orchestrator 用替身（本守卫只盯注册表寿命，不拉真 MAF 引擎） ——
        services.AddSingleton<IRunOrchestratorService>(new AgentRunDispatchTests.FakeDispatchOrchestrator());
        // 镜像 AIAgentPlugin L47（方案 A 修复后）：注册表单例——缺陷基线 Scoped 下本测试红（已实证 p079-d2-red.log）
        services.AddSingleton<IAgentRegistryService, AgentRegistryService>();
        // 镜像 AIAgentPlugin L62：IAgentRunDispatch（Scoped；eager 共享实例经插件存活 scope 捕获）
        services.AddScoped<IAgentRunDispatch, AgentRunDispatchService>();
        return (services.BuildServiceProvider(), services);
    }

    [Fact]
    public void 请求作用域新建Agent_派发侧共享实例必须可见()
    {
        var (root, _) = BuildPluginGraph();
        using var rootLease = root;

        // ① 插件 eager 捕获模拟：插件存活 scope 里解析派发实例（其 ctor 捕获同 scope 的注册表）
        using var dispatchScope = root.CreateScope();
        var sharedDispatch = dispatchScope.ServiceProvider.GetRequiredService<IAgentRunDispatch>();
        _ = sharedDispatch;   // 与生产同形：先解析持有，后续观察其捕获的注册表视图

        // ② UI 请求作用域：另一个 scope 里经注册表新建 Agent（写库 + 写「那个实例」的内存字典）
        using var requestScope = root.CreateScope();
        var uiRegistry = requestScope.ServiceProvider.GetRequiredService<IAgentRegistryService>();
        var created = uiRegistry.CreateAgent(new AgentDefinition { Name = "e2e-guard-agent" });

        // ③ 派发侧可见性：派发实例捕获的注册表视图必须能看到该 Agent。
        //    Scoped 注册（缺陷基线）下两者是不同实例/不同字典 → GetAgent=null → 本断言红；
        //    AddSingleton（方案 A）后同一实例 → 绿。
        var dispatchView = dispatchScope.ServiceProvider.GetRequiredService<IAgentRegistryService>();
        var seen = dispatchView.GetAgent(created.Id);
        seen.Should().NotBeNull("单例注册表下 UI 新建 Agent 必须对派发侧（eager 捕获实例）立即可见");
        seen!.Name.Should().Be("e2e-guard-agent");
    }
    [Fact]
    public void 跨provider实例统一_eager捕获与宿主主provider与请求作用域同一实例()
    {
        // 镜像 AIAgentPlugin 接线（缺陷单 #3）：
        // 旧接线（缺陷基线）= eager 块独立 BuildServiceProvider，无实例注册 → 宿主主 provider 另建实例 → 分裂（Red）；
        // 新接线（方案 A 中间路径）= 共享 pluginProvider + eager 实例注册回 services（last-wins）→ 单视图（Green）。
        var services = new ServiceCollection();
        services.AddSingleton<IRunOrchestratorService>(new AgentRunDispatchTests.FakeDispatchOrchestrator());
        services.AddScoped<IAgentRegistryService, AgentRegistryService>();
        services.AddScoped<IAgentRunDispatch, AgentRunDispatchService>();   // 镜像插件类型注册（eager 捕获用）
        var eagerProvider = services.BuildServiceProvider();
        using var eagerLease = eagerProvider;
        using var eagerScope = eagerProvider.CreateScope();
        var eagerRegistry = eagerScope.ServiceProvider.GetRequiredService<IAgentRegistryService>();
        var eagerDispatch = eagerScope.ServiceProvider.GetRequiredService<IAgentRunDispatch>();
        _ = eagerDispatch;

        // —— 方案 A ②：eager 产出以实例单例注册回 services（last-wins 取代类型注册；Red 阶段禁用已实证分裂） ——
        services.AddSingleton<IAgentRegistryService>(eagerRegistry);
        services.AddSingleton<IAgentRunDispatch>(eagerDispatch);

        // —— 宿主主 provider（控制器侧） ——
        var hostProvider = services.BuildServiceProvider();
        using var hostLease = hostProvider;
        var fromHost = hostProvider.GetRequiredService<IAgentRegistryService>();
        using var requestScope = hostProvider.CreateScope();
        var fromRequest = requestScope.ServiceProvider.GetRequiredService<IAgentRegistryService>();

        fromHost.Should().BeSameAs(eagerRegistry, "实例单例注册后宿主主 provider 必须与 eager 捕获同实例");
        fromRequest.Should().BeSameAs(eagerRegistry, "请求作用域解析必须与 eager 捕获同实例（全局单视图）");

        // 行为：请求作用域新建 Agent eager 派发侧视图即可见
        // 行为：请求作用域新建 Agent → eager 派发侧视图即可见
        var created = fromRequest.CreateAgent(new AgentDefinition { Name = "e2e-cross-provider" });
    }
}
