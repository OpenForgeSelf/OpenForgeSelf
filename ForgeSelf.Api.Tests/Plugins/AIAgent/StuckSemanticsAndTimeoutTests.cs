using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using Xunit;
namespace ForgeSelf.Api.Tests.Plugins.AIAgent;
/// <summary>
/// T14 stuck 语义校正 + 无进展阈值 45s→300s（验收#7+#8 后端）：
/// ① 阈值默认 300s、AIAgent_StepTimeoutSeconds 环境变量可配、显式传参优先；
/// ② 挂起原因三分类：等待审批 / 无进展超时 / 未声明完成（等待审批≠无进展超时）。
/// </summary>
public class StuckSemanticsAndTimeoutTests
{
    private static RunOrchestratorService NewOrchestrator(TimeSpan? stepTimeout = null)
    {
        var planner = new Moq.Mock<IPlanGeneratorService>();
        var ai = new Moq.Mock<IAIAgentService>();
        return new RunOrchestratorService(planner.Object, ai.Object, stepTimeout: stepTimeout);
    }

    [Theory]
    [InlineData(null, 300)]          // 默认 300s（用户裁定 45s→300s）
    [InlineData("600", 600)]         // 环境变量配置化
    [InlineData("abc", 300)]         // 非法环境变量 → 回退默认
    [InlineData("5", 300)]           // 低于下限 10s → 回退默认（防误配 0）
    [InlineData("8000", 300)]        // 高于上限 7200s → 回退默认
    public void 无进展阈值_默认300秒_环境变量可配_非法回退(string? envValue, int expectedSeconds)
    {
        Environment.SetEnvironmentVariable("AIAgent_StepTimeoutSeconds", envValue);
        try
        {
            var orchestrator = NewOrchestrator();
            Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), orchestrator.ResolveStepTimeout());
        }
        finally
        {
            Environment.SetEnvironmentVariable("AIAgent_StepTimeoutSeconds", null);
        }
    }

    [Fact]
    public void 无进展阈值_显式传参优先于环境变量()
    {
        Environment.SetEnvironmentVariable("AIAgent_StepTimeoutSeconds", "600");
        try
        {
            var orchestrator = NewOrchestrator(TimeSpan.FromMilliseconds(400));
            Assert.Equal(TimeSpan.FromMilliseconds(400), orchestrator.ResolveStepTimeout());
        }
        finally
        {
            Environment.SetEnvironmentVariable("AIAgent_StepTimeoutSeconds", null);
        }
    }

    [Fact]
    public void 挂起原因_存在Pending审批_等待审批而非无进展超时()
    {
        var reason = RunOrchestratorService.ComposeSuspendReason(1, 300, suspended: true);
        Assert.Contains("等待审批", reason);
        Assert.DoesNotContain("无进展超时", reason);
    }

    [Fact]
    public void 挂起原因_无审批_保留无进展超时文案()
    {
        var reason = RunOrchestratorService.ComposeSuspendReason(0, 300, suspended: true);
        Assert.Contains("无进展超时", reason);
        Assert.Contains("300s", reason);
    }

    [Fact]
    public void 挂起原因_回合未声明完成_非挂起文案()
    {
        var reason = RunOrchestratorService.ComposeSuspendReason(0, 300, suspended: false);
        Assert.Contains("complete_step", reason);
        Assert.DoesNotContain("无进展超时", reason);
    }
}