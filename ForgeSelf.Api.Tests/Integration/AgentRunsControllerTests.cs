using System.Text;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Controllers;
using ForgeSelf.Api.Plugins.AIAgent.Entities;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using XCode;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Tests.Integration;

/// <summary>
/// AgentRunsController API 测试（tasks.md T017-T018，contracts/agent-runs-api.md）：
/// SSE 端点（POST /runs、resume）用 mock IRunOrchestratorService + 内存响应流断言事件序列；
/// 列表/详情/restart/cancel/intervene 走真实 XCode 双表（[Collection("XCode")] 独立临时库）。
/// </summary>
[Collection("XCode")]
public class AgentRunsControllerTests
{
    private readonly string _dbDir;

    public AgentRunsControllerTests()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfRunsCtl_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dbDir);

        DAL.AddConnStr("AIAgent", $"Data Source={Path.Combine(_dbDir, "AIAgent.db")}", null, "SQLite");
        EntityFactory.InitConnection("AIAgent");

        AgentRun.Meta.Cache.Clear("test reset");
        AgentStepRun.Meta.Cache.Clear("test reset");
        AgentRun.Meta.Cache.Expire = 0;
        AgentStepRun.Meta.Cache.Expire = 0;
    }

    private static RunOrchestratorService NewRealOrchestrator() => new(null!, null!);

    /// <summary>构造控制器并挂内存响应流，返回可读出的 SSE 文本。</summary>
    private static (AgentRunsController controller, MemoryStream body) NewSseController(
        IRunOrchestratorService orchestrator)
    {
        var controller = new AgentRunsController(orchestrator);
        var ctx = new DefaultHttpContext();
        var body = new MemoryStream();
        ctx.Response.Body = body;
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };
        return (controller, body);
    }

    private static async IAsyncEnumerable<AgentLoopEvent> StreamEvents(params AgentLoopEvent[] events)
    {
        foreach (var e in events) yield return e;
        await Task.CompletedTask;
    }

    private static Mock<IRunOrchestratorService> MockOrchestrator(params AgentLoopEvent[] runEvents)
    {
        var mock = new Mock<IRunOrchestratorService>();
        if (runEvents.Length > 0)
        {
            mock.Setup(m => m.RunAsync(It.IsAny<RunRequest>(), It.IsAny<CancellationToken>()))
                .Returns(StreamEvents(runEvents));
            mock.Setup(m => m.ResumeAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .Returns(StreamEvents(runEvents));
        }
        return mock;
    }

    private static AgentRun NewRun(AgentRunStatus status = AgentRunStatus.Running, string? planJson = null)
    {
        var run = new AgentRun
        {
            AgentId = "agent.generalist",
            AgentName = "通用",
            TaskInput = "统计项目文件行数",
            Status = (int)status,
            StepCount = 1,
            CurrentStepIndex = 0,
        };
        if (planJson != null) run.PlanJson = planJson;
        run.Insert();
        return run;
    }

    private static AgentStepRun NewStep(AgentRun run, AgentStepStatus status, int index = 0)
    {
        var step = new AgentStepRun
        {
            RunId = run.Id,
            StepIndex = index,
            StepId = "s1",
            Name = "步骤一",
            Objective = "完成第一件事",
            Status = (int)status,
        };
        step.Insert();
        return step;
    }

    private static string ReadSse(MemoryStream body)
    {
        body.Position = 0;
        using var reader = new StreamReader(body, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    #region T017：POST /runs（SSE）

    [Fact]
    public async Task CreateRun_EmptyTaskInput_WritesErrorEvent()
    {
        var (controller, body) = NewSseController(MockOrchestrator().Object);

        await controller.CreateRun(new RunRequest { AgentId = "agent.x", TaskInput = "   " }, CancellationToken.None);

        var sse = ReadSse(body);
        sse.Should().Contain("taskInput");
        sse.Should().Contain("error");
    }

    [Fact]
    public async Task CreateRun_WithTaskInput_StreamsPlanAndStepEvents()
    {
        var planJson = JsonSerializer.Serialize(new
        {
            goal = "统计行数",
            steps = new[] { new { id = "s1", name = "统计", objective = "统计行数", expectedOutput = "数字" } }
        });
        var (controller, body) = NewSseController(MockOrchestrator(
            new AgentLoopEvent { Type = "plan_created", Content = $"{{ \"plan\": {{}}, \"planJson\": {planJson} }}" },
            new AgentLoopEvent { Type = "step_started", Content = "{\"runId\":1,\"stepIndex\":0}" },
            new AgentLoopEvent { Type = "step_completed", Content = "{\"runId\":1,\"stepIndex\":0,\"output\":\"123\"}" },
            new AgentLoopEvent { Type = "done", Content = "# 统计行数" }).Object);

        await controller.CreateRun(new RunRequest { AgentId = "agent.x", TaskInput = "统计行数" }, CancellationToken.None);

        var sse = ReadSse(body);
        sse.Should().Contain("plan_created");
        sse.Should().Contain("step_started");
        sse.Should().Contain("step_completed");
        sse.Should().Contain("done");
    }

    #endregion

    #region T017：GET 列表（分页 + 状态过滤）

    [Fact]
    public async Task ListRuns_ReturnsPagedResultWithCorrectTotal()
    {
        var controller = new AgentRunsController(NewRealOrchestrator());
        NewRun(AgentRunStatus.Running);
        NewRun(AgentRunStatus.Completed);

        var result = await controller.ListRuns(page: 1, pageSize: 1);

        result.Result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result.Result!;
        var resp = (AgentRunListResponse)ok.Value!;
        resp.Success.Should().BeTrue();
        resp.Total.Should().Be(2);
        resp.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task ListRuns_WithStatusFilter_ReturnsOnlyMatching()
    {
        var controller = new AgentRunsController(NewRealOrchestrator());
        NewRun(AgentRunStatus.Running);
        NewRun(AgentRunStatus.Completed);

        var result = await controller.ListRuns(status: "completed");

        var ok = (OkObjectResult)result.Result!;
        var resp = (AgentRunListResponse)ok.Value!;
        resp.Total.Should().Be(1);
        resp.Items.Should().ContainSingle().Which.Status.Should().Be(AgentRunStatus.Completed);
    }

    [Fact]
    public async Task ListRuns_InvalidStatus_BadRequest()
    {
        var controller = new AgentRunsController(NewRealOrchestrator());

        var result = await controller.ListRuns(status: "bogus");

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region T018：详情 / resume / restart / cancel / intervene

    [Fact]
    public async Task GetRunDetail_Existing_ReturnsRunWithSteps()
    {
        var controller = new AgentRunsController(NewRealOrchestrator());
        var run = NewRun(AgentRunStatus.Stuck);
        NewStep(run, AgentStepStatus.Stuck);

        var result = await controller.GetRunDetail(run.Id);

        result.Result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result.Result!;
        var resp = (AgentRunDetailResponse)ok.Value!;
        resp.Run.Id.Should().Be(run.Id);
        resp.Steps.Should().ContainSingle().Which.StepIndex.Should().Be(0);
    }

    [Fact]
    public async Task GetRunDetail_NonExistent_NotFound()
    {
        var controller = new AgentRunsController(NewRealOrchestrator());

        var result = await controller.GetRunDetail(999999);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ResumeRun_NonStuckStatus_WritesErrorEvent()
    {
        var controller = new AgentRunsController(NewRealOrchestrator());
        var run = NewRun(AgentRunStatus.Running);

        var (ctl, body) = NewSseController(NewRealOrchestrator());
        await ctl.ResumeRun(run.Id, CancellationToken.None);

        var sse = ReadSse(body);
        sse.Should().Contain("Running");
        // SSE 序列化对非 ASCII 转义为 \uXXXX，解析 content 字段断言中文。
        using var doc = JsonDocument.Parse(sse.Trim().Replace("data: ", "").Trim());
        doc.RootElement.GetProperty("content").GetString().Should().Contain("不可恢复");
    }

    [Fact]
    public async Task ResumeRun_StuckRun_StreamsEvents()
    {
        var planJson = JsonSerializer.Serialize(new AgentPlan { Goal = "g", Steps = { new AgentPlanStep { Id = "s1", Name = "n", Objective = "o", ExpectedOutput = "e" } } });
        var run = NewRun(AgentRunStatus.Stuck, planJson);
        NewStep(run, AgentStepStatus.Stuck);

        var mock = MockOrchestrator(
            new AgentLoopEvent { Type = "step_started", Content = "{\"runId\":1,\"stepIndex\":0}" },
            new AgentLoopEvent { Type = "step_completed", Content = "{\"runId\":1,\"stepIndex\":0,\"output\":\"123\"}" },
            new AgentLoopEvent { Type = "done", Content = "# g" });
        mock.Setup(m => m.GetRunAsync(run.Id)).ReturnsAsync(new AgentRunDto
        {
            Id = run.Id,
            AgentId = run.AgentId,
            Status = AgentRunStatus.Stuck,
            CurrentStepIndex = 0,
        });
        var (controller, body) = NewSseController(mock.Object);

        await controller.ResumeRun(run.Id, CancellationToken.None);

        var sse = ReadSse(body);
        sse.Should().Contain("step_started");
        sse.Should().Contain("done");
    }

    [Fact]
    public async Task Restart_ExistingRun_ReturnsNewRunId()
    {
        var controller = new AgentRunsController(NewRealOrchestrator());
        var run = NewRun(AgentRunStatus.Completed, """{"goal":"g","steps":[{"id":"s1","name":"n","objective":"o","expectedOutput":"e"}]}""");

        var result = await controller.RestartRun(run.Id);

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var payload = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(ok.Value));
        payload.GetProperty("success").GetBoolean().Should().BeTrue();
        payload.GetProperty("newRunId").GetInt64().Should().NotBe(run.Id);
    }

    [Fact]
    public async Task Restart_NonExistent_NotFound()
    {
        var controller = new AgentRunsController(NewRealOrchestrator());

        var result = await controller.RestartRun(999999);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Cancel_StuckRun_Ok()
    {
        var controller = new AgentRunsController(NewRealOrchestrator());
        var run = NewRun(AgentRunStatus.Stuck);

        var result = await controller.CancelRun(run.Id);

        result.Should().BeOfType<OkObjectResult>();
        ((AgentRunStatus)AgentRun.FindById(run.Id)!.Status).Should().Be(AgentRunStatus.Cancelled);
    }

    [Fact]
    public async Task Cancel_CompletedRun_BadRequest()
    {
        var controller = new AgentRunsController(NewRealOrchestrator());
        var run = NewRun(AgentRunStatus.Completed);

        var result = await controller.CancelRun(run.Id);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Intervene_IllegalAction_BadRequest()
    {
        var controller = new AgentRunsController(NewRealOrchestrator());
        var run = NewRun(AgentRunStatus.Stuck);
        NewStep(run, AgentStepStatus.Stuck);

        var result = await controller.Intervene(run.Id, 0, new InterveneRequest { Action = "bogus" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Intervene_Skip_OkAndAdvances()
    {
        var controller = new AgentRunsController(NewRealOrchestrator());
        var run = NewRun(AgentRunStatus.Stuck);
        NewStep(run, AgentStepStatus.Stuck);

        var result = await controller.Intervene(run.Id, 0, new InterveneRequest { Action = "skip", Note = "可跳过" });

        result.Should().BeOfType<OkObjectResult>();
        var step = AgentStepRun.FindAllByRunIdAndStepIndex(run.Id, 0).First();
        ((AgentStepStatus)step.Status).Should().Be(AgentStepStatus.Skipped);
        step.HumanNote.Should().Be("可跳过");
    }

    [Fact]
    public async Task Intervene_Override_OkAndCompletes()
    {
        var controller = new AgentRunsController(NewRealOrchestrator());
        var run = NewRun(AgentRunStatus.Stuck);
        NewStep(run, AgentStepStatus.Stuck);

        var result = await controller.Intervene(run.Id, 0, new InterveneRequest { Action = "override", Output = "人工产出" });

        result.Should().BeOfType<OkObjectResult>();
        var step = AgentStepRun.FindAllByRunIdAndStepIndex(run.Id, 0).First();
        ((AgentStepStatus)step.Status).Should().Be(AgentStepStatus.Completed);
        step.OutputJson.Should().Be("人工产出");
    }

    #endregion
}
