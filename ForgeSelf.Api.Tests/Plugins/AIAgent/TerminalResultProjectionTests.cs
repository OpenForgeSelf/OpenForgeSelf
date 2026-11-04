using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Entities;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using NewLife.Serialization;
using Xunit;
namespace ForgeSelf.Api.Tests.Plugins.AIAgent;
/// <summary>
/// T10-A 终态结果投影缺陷（resume-then-complete 场景）：completed 快照的 ResultText
/// 装的是陈旧 stuck 原因（用户收到「✅ 任务 #12 完成\n步骤执行超过 45s 挂起…」坏内容）。
/// 三层修复面：① ProjectResultText completed 优先收据 summary；② FreezeReceipt stuck 期
/// 收据可被 Completed 终局覆盖（终局→终局仍 write-once）；③ FinalizeRunAsync 完成清 StuckReason。
/// </summary>
[Collection("XCode")]
public class TerminalResultProjectionTests
{
    [Fact]
    public void ProjectResultText_Completed态_优先收据Summary而非StuckReason()
    {
        var dto = new AgentRunDto
        {
            Id = 12,
            Status = AgentRunStatus.Completed,
            StuckReason = "步骤执行超过 45s 挂起（请求无进展超时），已保留现场等待人工介入",
            ResultReceipt = """{"runId":12,"status":"Completed","summary":"已创建 test.txt 并写入内容"}""",
        };
        var text = AgentRunDispatchService.ProjectResultText(dto);
        Assert.Equal("已创建 test.txt 并写入内容", text);
    }

    [Fact]
    public void ProjectResultText_Stuck态_仍投影StuckReason()
    {
        var dto = new AgentRunDto
        {
            Id = 12,
            Status = AgentRunStatus.Stuck,
            StuckReason = "等待审批中：本步骤含需审批的写操作（1 条待批），批准后自动继续",
            ResultReceipt = """{"runId":12,"status":"Stuck","summary":"半截产出"}""",
        };
        var text = AgentRunDispatchService.ProjectResultText(dto);
        Assert.Contains("等待审批", text);
    }

    [Fact]
    public void ProjectResultText_Completed但收据脏_宁可空也不回退StuckReason()
    {
        var dto = new AgentRunDto
        {
            Id = 12,
            Status = AgentRunStatus.Completed,
            StuckReason = "旧挂起原因（历史脏数据）",
            ResultReceipt = "not-json",
        };
        var text = AgentRunDispatchService.ProjectResultText(dto);
        Assert.True(string.IsNullOrWhiteSpace(text));
    }

    [Fact]
    public void FreezeReceipt_Stuck期收据_可被Completed终局覆盖()
    {
        var run = new AgentRun { Id = 990001, Status = (int)AgentRunStatus.Stuck };
        RunOrchestratorService.FreezeReceipt(run, "stuck 期半截产出", null, "Stuck");
        var before = run.ResultReceipt;
        Assert.Contains("stuck 期半截产出", before);

        RunOrchestratorService.FreezeReceipt(run, "终局总结文本", null, "Completed");

        Assert.Contains("终局总结文本", run.ResultReceipt);
        Assert.Contains("Completed", run.ResultReceipt);
        Assert.DoesNotContain("stuck 期半截产出", run.ResultReceipt);
    }

    [Fact]
    public void FreezeReceipt_已是Completed终局收据_重放不覆盖()
    {
        var run = new AgentRun { Id = 990002, Status = (int)AgentRunStatus.Completed };
        RunOrchestratorService.FreezeReceipt(run, "首次终局总结", null, "Completed");

        RunOrchestratorService.FreezeReceipt(run, "重放不应覆盖", null, "Completed");

        Assert.Contains("首次终局总结", run.ResultReceipt);
        Assert.DoesNotContain("重放不应覆盖", run.ResultReceipt);
    }
}
