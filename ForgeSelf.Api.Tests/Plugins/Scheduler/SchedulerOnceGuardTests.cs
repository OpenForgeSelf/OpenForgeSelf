using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.Scheduler.Models;
using ForgeSelf.Api.Plugins.Scheduler.Services;
using ForgeSelf.Api.Services;
using ForgeSelf.Api.Plugins.Scheduler;
using ForgeSelf.Core;
using XCode;
using Xunit;
using TaskScheduler = ForgeSelf.Api.Plugins.Scheduler.Services.TaskScheduler;
using ScheduledTaskDto = ForgeSelf.Api.Plugins.Scheduler.Models.ScheduledTask;
using ScheduledTaskEntity = ForgeSelf.Api.Plugins.Scheduler.Entities.ScheduledTask;

namespace ForgeSelf.Api.Tests.Plugins.Scheduler;

/// <summary>
/// B5 P2③：Once 一次性任务的四缺陷防护。
/// ① create/update 不校验 runAt → NextRunTime=MinValue 僵尸行（永不触发）
/// ② runAt 无 UTC 归一（带本地时区偏移时晚 8 小时触发）
/// ③ 触发被跳过（会话已有未结束 Run）仍无条件推进 → Once 被空耗（Status=Disabled + RunCount+1，但 Run 从未发生）
/// ④ 无删除工具（scheduler.tool.delete_scheduled_task）
/// </summary>
[Collection("XCode")]
public class SchedulerOnceValidationTests : IClassFixture<XCodeTestFixture>
{
    public SchedulerOnceValidationTests(XCodeTestFixture fixture) => _ = fixture;

    private static (SchedulerFakeContext Ctx, SchedulerService Service, TaskScheduler Scheduler) NewService()
    {
        var ctx = new SchedulerFakeContext().With<ICronParser>(new CronParser());
        var scheduler = new TaskScheduler(new StubTaskExecutor(TaskExecutionOutcome.Executed), ctx);
        return (ctx, new SchedulerService(ctx, scheduler), scheduler);
    }

    private static CreateScheduledTaskRequest OnceRequest(DateTime? runAt)
        => new()
        {
            Name = $"t-{Guid.NewGuid():N}",
            TaskType = ScheduledTaskType.AgentRun,
            TargetId = "agent.test",
            ScheduleType = ScheduleType.Once,
            RunAt = runAt
        };

    [Fact]
    public async Task Once_WithoutRunAt_Rejected_NoZombieRow()
    {
        var (_, service, _) = NewService();

        var request = OnceRequest(null);
        var ex = await Assert.ThrowsAnyAsync<ArgumentException>(
            () => service.CreateTaskAsync(request));

        Assert.Contains("runAt", ex.Message);
        Assert.Equal(0, ScheduledTaskEntity.FindCount(ScheduledTaskEntity._.Name == request.Name));
    }

    [Fact]
    public async Task Once_PastRunAt_Rejected_NoZombieRow()
    {
        var (_, service, _) = NewService();

        var request = OnceRequest(DateTime.UtcNow.AddHours(-1));
        var ex = await Assert.ThrowsAnyAsync<ArgumentException>(
            () => service.CreateTaskAsync(request));

        Assert.Contains("runAt", ex.Message);
        Assert.Contains("晚于", ex.Message);
        Assert.Equal(0, ScheduledTaskEntity.FindCount(ScheduledTaskEntity._.Name == request.Name));
    }

    [Fact]
    public async Task Once_RunAt_IsNormalizedToUtc()
    {
        var (_, service, _) = NewService();
        var wall = DateTime.Now.AddHours(3); // 本地未来时刻，ToUniversalTime 必须按偏移折算

        var dto = await service.CreateTaskAsync(OnceRequest(wall));
        var entity = ScheduledTaskEntity.FindById(dto.Id);

        Assert.NotNull(entity);
        var expected = wall.ToUniversalTime();
        Assert.Equal(expected, entity.RunAt);
        Assert.Equal(expected, entity.NextRunTime);
    }

    [Fact]
    public async Task Once_PastRunAt_Updated_KeepsNextRunTime()
    {
        var future = DateTime.UtcNow.AddHours(2);
        var id = SchedulerTestHarness.SeedOnceTask(future, name: $"u-{Guid.NewGuid():N}");
        var (_, service, _) = NewService();

        var request = new UpdateScheduledTaskRequest
        {
            Name = ScheduledTaskEntity.FindById(id)!.Name,
            TaskType = ScheduledTaskType.AgentRun,
            TargetId = "agent.test",
            ScheduleType = ScheduleType.Once,
            RunAt = null
        };

        await Assert.ThrowsAnyAsync<ArgumentException>(() => service.UpdateTaskAsync(id, request));

        var entity = ScheduledTaskEntity.FindById(id);
        Assert.NotNull(entity);
        Assert.Equal((int)ScheduledTaskStatus.Enabled, entity.Status);
        Assert.NotEqual(DateTime.MinValue, entity.NextRunTime);
        Assert.True(Math.Abs((entity.NextRunTime - future).TotalSeconds) < 60,
            $"NextRunTime 应保持到期值 {future:O}，实际 {entity.NextRunTime:O}");
    }

    [Fact]
    public async Task Once_Update_WithFutureRunAt_NormalizesAndRecalculates()
    {
        var id = SchedulerTestHarness.SeedOnceTask(DateTime.UtcNow.AddHours(2));
        var (_, service, _) = NewService();
        var wall = DateTime.Now.AddHours(4);

        var request = new UpdateScheduledTaskRequest
        {
            Name = ScheduledTaskEntity.FindById(id)!.Name,
            TaskType = ScheduledTaskType.AgentRun,
            TargetId = "agent.test",
            ScheduleType = ScheduleType.Once,
            RunAt = wall
        };

        var dto = await service.UpdateTaskAsync(id, request);

        Assert.NotNull(dto);
        var entity = ScheduledTaskEntity.FindById(id);
        Assert.NotNull(entity);
        var expected = wall.ToUniversalTime();
        Assert.Equal(expected, entity.RunAt);
        Assert.Equal(expected, entity.NextRunTime);
    }
}

/// <summary>
/// B5 ③：触发被跳过（会话已有未结束 Run）时，调度器不得推进调度状态——
/// Once 任务保持 Enabled、RunCount 不增、NextRunTime 不被抹，下轮到点重试。
/// </summary>
[Collection("XCode")]
public class SchedulerSkipConsumptionTests : IClassFixture<XCodeTestFixture>
{
    public SchedulerSkipConsumptionTests(XCodeTestFixture fixture) => _ = fixture;

    [Fact]
    public async Task Skip_DoesNotConsumeOnce()
    {
        var ctx = new SchedulerFakeContext().With<ICronParser>(new CronParser());
        var stub = new StubTaskExecutor(TaskExecutionOutcome.Skipped);
        var scheduler = new TaskScheduler(stub, ctx);
        var seedRunAt = DateTime.UtcNow.AddSeconds(-2);
        var id = SchedulerTestHarness.SeedOnceTask(seedRunAt);

        try
        {
            await scheduler.StartAsync();

            await SchedulerTestHarness.WaitUntilAsync(
                () => Volatile.Read(ref stub.Calls) >= 1,
                TimeSpan.FromSeconds(8), "任务应被派发执行一次");
            await Task.Delay(500); // 稳定期：让派发后的推进块（若存在）完成

            var entity = ScheduledTaskEntity.FindById(id);
            Assert.NotNull(entity);
            Assert.Equal((int)ScheduledTaskStatus.Enabled, entity.Status);
            Assert.Equal(0, entity.RunCount);
            Assert.Equal(DateTime.MinValue, entity.LastRunTime);
            Assert.Equal(seedRunAt, entity.NextRunTime);
        }
        finally
        {
            await scheduler.StopAsync();
        }
    }

    /// <summary>阳性对照：真执行时 Once 必须被正常消费（防替身/轮询假绿）。</summary>
    [Fact]
    public async Task Executed_OnceTask_Consumed()
    {
        var ctx = new SchedulerFakeContext().With<ICronParser>(new CronParser());
        var stub = new StubTaskExecutor(TaskExecutionOutcome.Executed);
        var scheduler = new TaskScheduler(stub, ctx);
        var id = SchedulerTestHarness.SeedOnceTask(DateTime.UtcNow.AddSeconds(-2));

        try
        {
            await scheduler.StartAsync();

            await SchedulerTestHarness.WaitUntilAsync(
                () =>
                {
                    var e = ScheduledTaskEntity.FindById(id);
                    return e != null && e.Status == (int)ScheduledTaskStatus.Disabled && e.RunCount >= 1;
                },
                TimeSpan.FromSeconds(8), "真执行后 Once 应被消费（Disabled + RunCount>=1）");
        }
        finally
        {
            await scheduler.StopAsync();
        }
    }

    /// <summary>AgentRun 触发返回 Skipped 时，TaskExecutor 必须把它传播为 Skipped outcome（写成功日志）。</summary>
    [Fact]
    public async Task AgentRunTrigger_Skipped_PropagatesThroughExecutor()
    {
        var ctx = new SchedulerFakeContext().With<ICronParser>(new CronParser())
            .With<IScheduledAgentRunTrigger>(new SkipAgentRunTrigger());
        var executor = new TaskExecutor(ctx, new WorkflowTaskHandler(ctx), new HttpWebhookHandler());

        var dto = new ScheduledTaskDto
        {
            Id = 1,
            Name = "skip-propagation",
            TaskType = ScheduledTaskType.AgentRun,
            TargetId = "agent.test",
            InputParameters = """{"agentId":"agent.test","prompt":"p"}"""
        };

        var outcome = await executor.ExecuteTaskAsync(dto);

        Assert.Equal(TaskExecutionOutcome.Skipped, outcome);
    }
}

/// <summary>B5 ④：删除工具（scheduler.tool.delete_scheduled_task / schedule_delete）。</summary>
[Collection("XCode")]
public class SchedulerDeleteToolTests : IClassFixture<XCodeTestFixture>
{
    public SchedulerDeleteToolTests(XCodeTestFixture fixture) => _ = fixture;
    [Fact]
    public async Task DeleteTool_RoundTrip()
    {
        var id = SchedulerTestHarness.SeedOnceTask(DateTime.UtcNow.AddHours(1));
        var ctx = new SchedulerFakeContext().With<ICronParser>(new CronParser());
        var tool = new DeleteScheduledTaskToolFunction(ctx);

        var json = await tool.ExecuteAsync($"{{\"taskId\":{id}}}");
        var root = JsonDocument.Parse(json).RootElement;

        Assert.True(root.GetProperty("success").GetBoolean(), json);
        Assert.Null(ScheduledTaskEntity.FindById(id));
    }

    [Fact]
    public async Task DeleteTool_MissingTask_Fails()
    {
        var ctx = new SchedulerFakeContext().With<ICronParser>(new CronParser());
        var tool = new DeleteScheduledTaskToolFunction(ctx);

        var json = await tool.ExecuteAsync("{\"taskId\":999999}");
        var root = JsonDocument.Parse(json).RootElement;

        Assert.False(root.GetProperty("success").GetBoolean(), json);
        Assert.Contains("不存在", root.GetProperty("error").GetString());
    }
}
