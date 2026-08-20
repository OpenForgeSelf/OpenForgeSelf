using System.Reflection;
using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Backend.Plugins.AIAgent.Services;
using OpenForgeSelf.Backend.Plugins.WorkflowEngine.Services;
using OpenForgeSelf.Core;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace OpenForgeSelf.Backend.Tests.Integration;

/// <summary>
/// 内核「插件间服务互通」dispose 门禁测试（对标 deepseek-harness，调研 §5.6 裁决 A/D/E/F）。
/// 场景：AIAgent（提供方）经 ctx.Register 把 IWorkflowAIAdvisor 写入 root 共享服务表，
/// 兄弟插件 WorkflowEngine（消费方）经 ctx.Get 解析。
/// 覆盖：提供可解析 → 卸载（Fiber.Dispose）自动摘除 → 消费方降级默认重试 → 重挂载恢复；
/// 宿主 seed 契约不受插件卸载影响；框架私有本地值（PluginMetadata/IServiceCollection）不泄漏给兄弟插件。
/// </summary>
public class KernelServiceInteropTests
{
    /// <summary>测试 1：AIAgent 提供后，兄弟 WorkflowEngine 经 ctx.Get 可解析。</summary>
    [Fact]
    public void AIAgentProvidesAdvisor_SiblingWorkflowEngineCanResolve()
    {
        var root = new Context();
        var aiAgent = new Fiber(root);        // 模拟 AIAgent 插件（提供方）
        var workflowEngine = new Fiber(root); // 模拟 WorkflowEngine 插件（兄弟 Fiber，消费方）
        var advisor = CreateAdvisor();

        aiAgent.Mount(ctx => ctx.Register<IWorkflowAIAdvisor>(advisor));

        Assert.Same(advisor, workflowEngine.Context.Get<IWorkflowAIAdvisor>());
        Assert.Same(advisor, root.Get<IWorkflowAIAdvisor>());
    }

    /// <summary>测试 2：卸载 AIAgent 后解析为 null，且 WorkflowExecutor 走默认重试（降级语义保留）。</summary>
    [Fact]
    public async Task UnloadAIAgent_AdvisorNull_ExecutorFallsBackToDefaultRetry()
    {
        var root = new Context();
        var aiAgent = new Fiber(root);
        var workflowEngine = new Fiber(root);
        aiAgent.Mount(ctx => ctx.Register<IWorkflowAIAdvisor>(CreateAdvisor()));

        var executor = new WorkflowExecutor(ctx: workflowEngine.Context);
        var step = new WorkflowStep { Name = "s1", Type = WorkflowStepType.ToolCall, ToolName = "t" };
        var execution = new WorkflowExecution { Variables = new Dictionary<string, object?>() };

        // AIAgent 在线：GetAIAdviceAsync 返回 AI 建议（非 null）。
        var before = await InvokeGetAIAdviceAsync(executor, step, "boom", 1, execution);
        Assert.NotNull(before);

        aiAgent.Dispose(); // 卸载 AIAgent → 共享表条目经 effect 自动摘除

        Assert.Null(workflowEngine.Context.Get<IWorkflowAIAdvisor>());
        // 降级语义保留：GetAIAdviceAsync 返回 null → 调用方走默认重试。
        var after = await InvokeGetAIAdviceAsync(executor, step, "boom", 1, execution);
        Assert.Null(after);
    }

    /// <summary>测试 3：重挂载 AIAgent 后恢复可解析。</summary>
    [Fact]
    public void RemountAIAgent_AdvisorResolvableAgain()
    {
        var root = new Context();
        var workflowEngine = new Fiber(root);

        var first = new Fiber(root);
        first.Mount(ctx => ctx.Register<IWorkflowAIAdvisor>(CreateAdvisor()));
        first.Dispose();
        Assert.Null(workflowEngine.Context.Get<IWorkflowAIAdvisor>());

        var second = new Fiber(root); // 重挂载 AIAgent
        var advisor2 = CreateAdvisor();
        second.Mount(ctx => ctx.Register<IWorkflowAIAdvisor>(advisor2));

        Assert.Same(advisor2, workflowEngine.Context.Get<IWorkflowAIAdvisor>());
    }

    /// <summary>测试 4：宿主 seed 契约（IToolRegistry）不受插件卸载影响（root 常驻，裁决 B/C）。</summary>
    [Fact]
    public void HostSeedContract_SurvivesPluginUnload()
    {
        var root = new Context();
        var toolRegistry = new Mock<IToolRegistry>();
        root.Register<IToolRegistry>(toolRegistry.Object); // 宿主 seed 契约（对标 ProvideHostServices）

        var aiAgent = new Fiber(root);
        aiAgent.Mount(ctx => ctx.Register<IWorkflowAIAdvisor>(CreateAdvisor()));
        aiAgent.Dispose();

        Assert.Same(toolRegistry.Object, root.Get<IToolRegistry>());
        Assert.Same(toolRegistry.Object, new Fiber(root).Context.Get<IToolRegistry>());
    }

    /// <summary>测试 5a：PluginMetadata 本地值不被兄弟插件 Get 到（裁决 A 防泄漏回归）。</summary>
    [Fact]
    public void RegisterLocal_PluginMetadata_NotVisibleToSiblingFiber()
    {
        var root = new Context();
        var aiAgent = new Fiber(root);
        var workflowEngine = new Fiber(root);
        var metadata = new PluginMetadata { Id = "aiagent" };

        aiAgent.Mount(ctx => ctx.RegisterLocal(metadata));

        Assert.Same(metadata, aiAgent.Context.Get<PluginMetadata>());
        Assert.Null(workflowEngine.Context.Get<PluginMetadata>());
        Assert.Null(root.Get<PluginMetadata>());
    }

    /// <summary>测试 5b：IServiceCollection 本地值不被兄弟插件 Get 到（裁决 A 防泄漏回归）。</summary>
    [Fact]
    public void RegisterLocal_ServiceCollection_NotVisibleToSiblingFiber()
    {
        var root = new Context();
        var aiAgent = new Fiber(root);
        var workflowEngine = new Fiber(root);
        var services = new ServiceCollection();

        aiAgent.Mount(ctx => ctx.RegisterLocal(services));

        Assert.Same(services, aiAgent.Context.Get<ServiceCollection>());
        Assert.Null(workflowEngine.Context.Get<ServiceCollection>());
    }

    /// <summary>真实 AIWorkflowAdvisor 链路：可经子容器依赖构造并经 ctx.Register 提供给兄弟插件。</summary>
    [Fact]
    public void RealAIWorkflowAdvisor_Registered_ResolvableBySibling()
    {
        var root = new Context();
        var aiAgent = new Fiber(root);
        var workflowEngine = new Fiber(root);

        var assistant = new Mock<IAIWorkflowAssistant>();
        var advisor = new AIWorkflowAdvisor(assistant.Object);
        aiAgent.Mount(ctx => ctx.Register<IWorkflowAIAdvisor>(advisor));

        Assert.Same(advisor, workflowEngine.Context.Get<IWorkflowAIAdvisor>());
    }

    /// <summary>构造一个行为可预期的 advisor（默认返回「应重试」建议）。</summary>
    private static IWorkflowAIAdvisor CreateAdvisor()
    {
        var mock = new Mock<IWorkflowAIAdvisor>();
        mock.Setup(a => a.GetRetryAdviceAsync(
                It.IsAny<WorkflowStep>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Dictionary<string, object?>?>()))
            .ReturnsAsync(new RetryAdvice { ShouldRetry = true });
        return mock.Object;
    }

    /// <summary>反射调用 WorkflowExecutor 私有 GetAIAdviceAsync，验证「AIAgent 在线返回建议 / 卸载后走默认重试」。</summary>
    private static async Task<RetryAdvice?> InvokeGetAIAdviceAsync(WorkflowExecutor executor, WorkflowStep step, string errorMessage, int retryCount, WorkflowExecution execution)
    {
        var method = typeof(WorkflowExecutor).GetMethod("GetAIAdviceAsync", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("WorkflowExecutor.GetAIAdviceAsync 不存在");
        var task = (Task<RetryAdvice?>)method.Invoke(executor, new object[] { step, errorMessage, retryCount, execution })!;
        return await task;
    }
}
