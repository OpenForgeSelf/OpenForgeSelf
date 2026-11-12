using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Controllers;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Core;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ForgeSelf.Api.Tests.Integration;

/// <summary>
/// TasksOverviewController API 测试（战役 B3，Mock 服务层）：
/// 开放任务桶 = Mock IPluginMessageService + Mock ISessionStore（经 FakeContext 注入，铁律 12c 同构）；
/// 计划任务桶 = Mock IRunOrchestratorService。覆盖 02-spec §3.3 投影规则表与统一 DTO 契约。
/// 不触 XCode 实体，无需 [Collection("XCode")]。
/// </summary>
public class TasksOverviewControllerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 2, 20, 0, 0, TimeSpan.FromHours(8));

    #region 装配

    /// <summary>最小 IContext：字典注册表（铁律 12c 同构——宿主契约运行期解析）。</summary>
    private sealed class FakeContext : IContext
    {
        private readonly Dictionary<Type, object> _services = new();

        public FakeContext With<TService>(TService instance) where TService : class
        {
            _services[typeof(TService)] = instance;
            return this;
        }

        public T? Get<T>() where T : class => _services.TryGetValue(typeof(T), out var v) ? (T)v : null;
        public object? GetService(Type serviceType) => _services.TryGetValue(serviceType, out var v) ? v : null;
        public void Register<TService>(TService instance) where TService : class => _services[typeof(TService)] = instance;
        public void Register<TService, TImpl>() where TService : class where TImpl : class, TService, new() => _services[typeof(TService)] = new TImpl();
        public void RegisterLocal<TService>(TService instance) where TService : class { }
        public IDisposable Effect(Func<IDisposable> sideEffect) => sideEffect();
        public IEventBus Events => throw new NotSupportedException();
        public IContext Derive() => throw new NotSupportedException();
    }

    private static (TasksOverviewController controller, Mock<IPluginMessageService> messages, Mock<IRunOrchestratorService> runs)
        NewController(IReadOnlyList<SessionEvent>? events = null, bool withStore = true)
    {
        var messages = new Mock<IPluginMessageService>();
        messages
            .Setup(m => m.GetSessionsAsync(It.IsAny<SessionArchivedFilter>()))
            .ReturnsAsync(new List<SessionSummaryModel>
            {
                new() { SessionId = "s-1", Title = "统计项目文件行数", MessageCount = 2, LastTime = T0.LocalDateTime },
            });

        var store = new Mock<ISessionStore>();
        store.Setup(s => s.Replay(It.IsAny<string>())).Returns(events ?? Array.Empty<SessionEvent>());

        var runs = new Mock<IRunOrchestratorService>();
        runs
            .Setup(m => m.ListRunsAsync(It.IsAny<string?>(), It.IsAny<AgentRunStatus?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new AgentRunListResponse { Success = true, Total = 0, Items = new List<AgentRunDto>() });

        var ctx = new FakeContext();
        if (withStore) ctx.With<ISessionStore>(store.Object);

        return (new TasksOverviewController(messages.Object, runs.Object, ctx), messages, runs);
    }

    private static TasksOverviewResponse ReadOk(ActionResult<TasksOverviewResponse> result)
    {
        var ok = result.Result as OkObjectResult;
        ok.Should().NotBeNull("应返回 200");
        return (ok!.Value as TasksOverviewResponse)!;
    }

    #endregion

    #region 汇聚与契约

    [Fact]
    public async Task GetOverview_NoSessionsAndNoRuns_EmptyItemsBothOk()
    {
        var messages = new Mock<IPluginMessageService>();
        messages.Setup(m => m.GetSessionsAsync(It.IsAny<SessionArchivedFilter>())).ReturnsAsync(new List<SessionSummaryModel>());
        var runs = new Mock<IRunOrchestratorService>();
        runs.Setup(m => m.ListRunsAsync(It.IsAny<string?>(), It.IsAny<AgentRunStatus?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new AgentRunListResponse());
        var controller = new TasksOverviewController(messages.Object, runs.Object,
            new FakeContext().With<ISessionStore>(EmptyStore()));

        var response = ReadOk(await controller.GetOverview());

        response.Success.Should().BeTrue();
        response.Sources.Should().HaveCount(2);
        response.Sources.Should().OnlyContain(s => s.Ok);
        response.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOverview_SessionWithoutTurnEvents_ProducesUnknownRow()
    {
        // 会话存在但无任何 turn 事件（旧数据）：不假判，得 1 条 unknown 行（非空列表）
        var (controller, _, _) = NewController(events: Array.Empty<SessionEvent>());

        var response = ReadOk(await controller.GetOverview());
        var item = response.Items.Should().ContainSingle().Subject;

        item.Id.Should().Be("open:s-1");
        item.Status.Should().Be("unknown");
        response.Sources.Should().Contain(s => s.Source == "open" && s.Ok && s.Count == 1);
    }

    [Fact]
    public async Task GetOverview_SessionStoreMissing_OpenBucketFailsWithReason()
    {
        var (controller, _, _) = NewController(withStore: false);

        var response = ReadOk(await controller.GetOverview());

        response.Sources.Should().ContainSingle(s => s.Source == "open" && !s.Ok && s.Error!.Contains("ISessionStore"));
        response.Sources.Should().ContainSingle(s => s.Source == "planned" && s.Ok);
    }

    [Fact]
    public async Task GetOverview_InvalidSource_Returns400()
    {
        var (controller, _, _) = NewController();

        var result = await controller.GetOverview(source: "bogus");
        (result.Result as BadRequestObjectResult).Should().NotBeNull();
    }

    [Fact]
    public async Task GetOverview_SourceFilterPlanned_OnlyCallsPlannedBucket()
    {
        var (controller, messages, _) = NewController();

        var response = ReadOk(await controller.GetOverview(source: "planned"));

        response.Sources.Should().ContainSingle().Which.Source.Should().Be("planned");
        messages.Verify(m => m.GetSessionsAsync(It.IsAny<SessionArchivedFilter>()), Times.Never);
    }

    [Fact]
    public async Task GetOverview_LimitClampedAndApplied()
    {
        var messages = new Mock<IPluginMessageService>();
        messages.Setup(m => m.GetSessionsAsync(It.IsAny<SessionArchivedFilter>())).ReturnsAsync(
            Enumerable.Range(1, 5).Select(i => new SessionSummaryModel
            { SessionId = $"s-{i}", Title = $"会话{i}", LastTime = T0.AddMinutes(-i).DateTime }).ToList());
        var runs = new Mock<IRunOrchestratorService>();
        runs.Setup(m => m.ListRunsAsync(It.IsAny<string?>(), It.IsAny<AgentRunStatus?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new AgentRunListResponse());
        var controller = new TasksOverviewController(messages.Object, runs.Object, new FakeContext().With<ISessionStore>(EmptyStore()));

        var response = ReadOk(await controller.GetOverview(limit: 500, source: "open"));

        // limit=500 → 钳为 100；会话仅 5 条 → 全取 5 条（钳位不放大、不丢失）
        response.Items.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetOverview_LimitApplied_TakesTopNByLastTime()
    {
        var messages = new Mock<IPluginMessageService>();
        messages.Setup(m => m.GetSessionsAsync(It.IsAny<SessionArchivedFilter>())).ReturnsAsync(
            Enumerable.Range(1, 5).Select(i => new SessionSummaryModel
            { SessionId = $"s-{i}", Title = $"会话{i}", LastTime = T0.AddMinutes(-i).DateTime }).ToList());
        var runs = new Mock<IRunOrchestratorService>();
        runs.Setup(m => m.ListRunsAsync(It.IsAny<string?>(), It.IsAny<AgentRunStatus?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new AgentRunListResponse());
        var controller = new TasksOverviewController(messages.Object, runs.Object, new FakeContext().With<ISessionStore>(EmptyStore()));

        var response = ReadOk(await controller.GetOverview(limit: 2, source: "open"));

        response.Items.Should().HaveCount(2);
        response.Items.Should().OnlyContain(i => i.Source == "open");
    }

    #endregion

    #region 计划任务桶投影

    [Fact]
    public async Task PlannedRun_Failed_MapsFailureReasonAndRetry()
    {
        var (_, _, runs) = NewController();
        runs.Setup(m => m.ListRunsAsync(It.IsAny<string?>(), It.IsAny<AgentRunStatus?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new AgentRunListResponse
            {
                Items = new List<AgentRunDto>
                {
                    new()
                    {
                        Id = 7, AgentId = "agent.x", TaskInput = "修复构建\n第二行忽略",
                        Status = AgentRunStatus.Failed, StuckReason = "boom: 编译失败",
                        CreateTime = T0.DateTime, UpdateTime = T0.AddMinutes(5).DateTime,
                    },
                },
            });

        var response = ReadOk(await (NewControllerWith(runs)).GetOverview());
        var item = response.Items.Should().ContainSingle().Subject;

        item.Id.Should().Be("planned:7");
        item.Source.Should().Be("planned");
        item.Status.Should().Be("failed");
        item.FailureReason.Should().Be("boom: 编译失败");
        item.Retryable.Should().BeTrue();
        item.RetryUrl.Should().Be("/api/ai-agent/runs/7/resume");
        item.EndedAt.Should().NotBeNull();
        item.Title.Should().Be("修复构建");
    }

    [Fact]
    public async Task PlannedRun_Completed_NotRetryable()
    {
        var runs = new Mock<IRunOrchestratorService>();
        runs.Setup(m => m.ListRunsAsync(It.IsAny<string?>(), It.IsAny<AgentRunStatus?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new AgentRunListResponse
            {
                Items = new List<AgentRunDto>
                {
                    new() { Id = 3, TaskInput = "任务", Status = AgentRunStatus.Completed, CreateTime = T0.DateTime, UpdateTime = T0.DateTime },
                },
            });

        var response = ReadOk(await NewControllerWith(runs).GetOverview());
        var item = response.Items.Should().ContainSingle().Subject;

        item.Status.Should().Be("completed");
        item.Retryable.Should().BeFalse();
        item.RetryUrl.Should().BeNull();
        item.EndedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task PlannedRun_Stuck_MapsStuckAndRetry()
    {
        var runs = new Mock<IRunOrchestratorService>();
        runs.Setup(m => m.ListRunsAsync(It.IsAny<string?>(), It.IsAny<AgentRunStatus?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new AgentRunListResponse
            {
                Items = new List<AgentRunDto>
                {
                    new() { Id = 9, TaskInput = "t", Status = AgentRunStatus.Stuck, StuckReason = "等待用户确认", CreateTime = T0.DateTime, UpdateTime = T0.DateTime },
                },
            });

        var response = ReadOk(await NewControllerWith(runs).GetOverview());
        var item = response.Items.Should().ContainSingle().Subject;

        item.Status.Should().Be("stuck");
        item.FailureReason.Should().Be("等待用户确认");
        item.Retryable.Should().BeTrue();
        item.CancelUrl.Should().Be("/api/ai-agent/runs/9/cancel");   // T14⑥：stuck 行下发取消地址

    }
    [Fact]
    public async Task PlannedRun_Completed_NoCancelUrl_T14()
    {
        var runs = new Mock<IRunOrchestratorService>();
        runs.Setup(m => m.ListRunsAsync(It.IsAny<string?>(), It.IsAny<AgentRunStatus?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new AgentRunListResponse
            {
                Items = new List<AgentRunDto>
                {
                    new() { Id = 8, TaskInput = "t", Status = AgentRunStatus.Completed, CreateTime = T0.LocalDateTime, UpdateTime = T0.LocalDateTime },
                },
            });
        var response = ReadOk(await NewControllerWith(runs).GetOverview());
        var item = response.Items.Should().ContainSingle().Subject;
        item.CancelUrl.Should().BeNull();   // T14⑥：终态不可取消
    }

    #endregion

    #region 开放任务桶投影（02-spec §3.3 表逐行）

    [Fact]
    public async Task OpenSession_TurnInFlight_Running()
    {
        var (controller, _, _) = NewController(new SessionEvent[]
        {
            new UserMessageEvent(1, "s-1", T0, "做事", MessageSource.WebUi),
            new TurnStartEvent(2, "s-1", T0.AddSeconds(1), "t1"),
        });

        var response = ReadOk(await controller.GetOverview());
        var item = response.Items.Should().ContainSingle().Subject;

        item.Id.Should().Be("open:s-1");
        item.Status.Should().Be("running");
        item.Retryable.Should().BeFalse();
        item.EndedAt.Should().BeNull();
        item.StartedAt.Should().Be(T0.AddSeconds(1));
    }

    [Fact]
    public async Task OpenSession_CompletedTurn_CompletedWithEndTime()
    {
        var (controller, _, _) = NewController(new SessionEvent[]
        {
            new TurnStartEvent(1, "s-1", T0, "t1"),
            new AssistantMessageEvent(2, "s-1", T0.AddSeconds(2), "done", Array.Empty<ToolCallRef>(), null, "stop"),
            new TurnEndEvent(3, "s-1", T0.AddSeconds(3), "t1", TurnEndReason.Completed),
        });

        var response = ReadOk(await controller.GetOverview());
        var item = response.Items.Should().ContainSingle().Subject;

        item.Status.Should().Be("completed");
        item.EndedAt.Should().Be(T0.AddSeconds(3));
    }

    [Fact]
    public async Task OpenSession_SuspendedTurn_StuckRetryable()
    {
        var (controller, _, _) = NewController(new SessionEvent[]
        {
            new TurnStartEvent(1, "s-1", T0, "t1"),
            new TurnEndEvent(2, "s-1", T0.AddSeconds(1), "t1", TurnEndReason.Suspended),
        });

        var response = ReadOk(await controller.GetOverview());
        var item = response.Items.Should().ContainSingle().Subject;

        item.Status.Should().Be("stuck");
        item.Retryable.Should().BeTrue();
    }

    [Fact]
    public async Task OpenSession_AbortedWithAttemptError_Failed()
    {
        var (controller, _, _) = NewController(new SessionEvent[]
        {
            new TurnStartEvent(1, "s-1", T0, "t1"),
            new AssistantAttemptEvent(2, "s-1", T0.AddSeconds(1), string.Empty, "request-timeout", "m1"),
            new TurnEndEvent(3, "s-1", T0.AddSeconds(2), "t1", TurnEndReason.Aborted),
        });

        var response = ReadOk(await controller.GetOverview());
        var item = response.Items.Should().ContainSingle().Subject;

        item.Status.Should().Be("failed");
        item.FailureReason.Should().Contain("request-timeout");
        item.Retryable.Should().BeTrue();
    }

    [Fact]
    public async Task OpenSession_AbortedUserCancel_Cancelled()
    {
        var (controller, _, _) = NewController(new SessionEvent[]
        {
            new TurnStartEvent(1, "s-1", T0, "t1"),
            new AssistantAttemptEvent(2, "s-1", T0.AddSeconds(1), string.Empty, "aborted", "m1"),
            new TurnEndEvent(3, "s-1", T0.AddSeconds(2), "t1", TurnEndReason.Aborted),
        });

        var response = ReadOk(await controller.GetOverview());
        var item = response.Items.Should().ContainSingle().Subject;

        item.Status.Should().Be("cancelled");
        item.Retryable.Should().BeFalse();
    }

    [Fact]
    public async Task OpenSession_UnresolvedApproval_StuckWithToolName()
    {
        var (controller, _, _) = NewController(new SessionEvent[]
        {
            new TurnStartEvent(1, "s-1", T0, "t1"),
            new ToolCallEvent(2, "s-1", T0.AddSeconds(1), "c1", "run_terminal_command", "{}"),
            new ToolApprovalRequestEvent(3, "s-1", T0.AddSeconds(2), 11, "meai-1", "c1", "run_terminal_command", "{}", "high"),
        });

        var response = ReadOk(await controller.GetOverview());
        var item = response.Items.Should().ContainSingle().Subject;

        item.Status.Should().Be("stuck");
        item.FailureReason.Should().Contain("run_terminal_command");
        item.Retryable.Should().BeTrue();
    }

    [Fact]
    public async Task OpenSession_ClosedApproval_NotStuck()
    {
        var (controller, _, _) = NewController(new SessionEvent[]
        {
            new TurnStartEvent(1, "s-1", T0, "t1"),
            new ToolApprovalRequestEvent(2, "s-1", T0.AddSeconds(1), 11, "meai-1", "c1", "run_terminal_command", "{}", "high"),
            new ToolResultEvent(3, "s-1", T0.AddSeconds(3), "c1", "run_terminal_command", "{}", ToolOutcome.Ok, 10),
            new TurnEndEvent(4, "s-1", T0.AddSeconds(4), "t1", TurnEndReason.Completed),
        });

        var response = ReadOk(await controller.GetOverview());
        var item = response.Items.Should().ContainSingle().Subject;

        item.Status.Should().Be("completed");
    }

    [Fact]
    public async Task OpenSession_NoEvents_Unknown()
    {
        var (controller, _, _) = NewController(new SessionEvent[]
        {
            new UserMessageEvent(1, "s-1", T0, "旧数据会话", MessageSource.WebUi),
        });

        var response = ReadOk(await controller.GetOverview());
        var item = response.Items.Should().ContainSingle().Subject;

        item.Status.Should().Be("unknown");
        item.Retryable.Should().BeFalse();
    }

    [Fact]
    public async Task OpenSession_ReplayThrows_RowDegradesNotBucket()
    {
        var messages = new Mock<IPluginMessageService>();
        messages.Setup(m => m.GetSessionsAsync(It.IsAny<SessionArchivedFilter>())).ReturnsAsync(
            new List<SessionSummaryModel> { new() { SessionId = "s-1", Title = "t", LastTime = T0.DateTime } });
        var store = new Mock<ISessionStore>();
        store.Setup(s => s.Replay(It.IsAny<string>())).Throws(new InvalidOperationException("store broken"));
        var runs = new Mock<IRunOrchestratorService>();
        var controller = new TasksOverviewController(messages.Object, runs.Object, new FakeContext().With<ISessionStore>(store.Object));

        var response = ReadOk(await controller.GetOverview(source: "open"));

        response.Sources.Should().ContainSingle().Which.Ok.Should().BeTrue("单会话读失败只降级该行");
        response.Items.Should().ContainSingle().Which.Status.Should().Be("unknown");
    }

    #endregion

    /// <summary>计划任务桶装配捷径：真实消息服务 mock + 指定 runs mock。</summary>
    private static TasksOverviewController NewControllerWith(Mock<IRunOrchestratorService> runs)
    {
        var messages = new Mock<IPluginMessageService>();
        messages.Setup(m => m.GetSessionsAsync(It.IsAny<SessionArchivedFilter>())).ReturnsAsync(new List<SessionSummaryModel>());
        return new TasksOverviewController(messages.Object, runs.Object, new FakeContext().With<ISessionStore>(EmptyStore()));
    }

    /// <summary>空事件日志 store（Moq.Of 无 Setup 会返回 null 引发投影 NRE，必须显式回空数组）。</summary>
    private static ISessionStore EmptyStore()
    {
        var store = new Mock<ISessionStore>();
        store.Setup(s => s.Replay(It.IsAny<string>())).Returns(Array.Empty<SessionEvent>());
        return store.Object;
    }
}
