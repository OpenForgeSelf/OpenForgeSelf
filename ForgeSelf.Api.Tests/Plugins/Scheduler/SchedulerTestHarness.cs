using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.Scheduler.Models;
using ForgeSelf.Api.Plugins.Scheduler.Services;
using ForgeSelf.Core;
using Xunit;
using ScheduledTaskEntity = ForgeSelf.Api.Plugins.Scheduler.Entities.ScheduledTask;

namespace ForgeSelf.Api.Tests.Plugins.Scheduler;

/// <summary>最小 IContext：字典注册表（铁律 12c 同构——宿主契约运行期解析）。</summary>
public sealed class SchedulerFakeContext : IContext
{
    private readonly Dictionary<Type, object> _services = new();

    public SchedulerFakeContext With<TService>(TService instance) where TService : class
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

/// <summary>可编程 ITaskExecutor 替身：返回预设 outcome 并记录派发次数。</summary>
public sealed class StubTaskExecutor : ITaskExecutor
{
    private readonly TaskExecutionOutcome _outcome;

    public StubTaskExecutor(TaskExecutionOutcome outcome) => _outcome = outcome;

    public int Calls;

    public Task<TaskExecutionOutcome> ExecuteTaskAsync(ScheduledTask task)
    {
        Interlocked.Increment(ref Calls);
        return Task.FromResult(_outcome);
    }
}

/// <summary>恒返回 Ok 的触发替身。</summary>
public sealed class OkAgentRunTrigger : IScheduledAgentRunTrigger
{
    public Task<ScheduledAgentRunResult> TriggerAsync(ScheduledAgentRunRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(ScheduledAgentRunResult.Ok(1, "Completed", "已创建 Run"));
}

/// <summary>恒返回 Skip 的触发替身（模拟同一 Agent 会话已有未结束 Run）。</summary>
public sealed class SkipAgentRunTrigger : IScheduledAgentRunTrigger
{
    public Task<ScheduledAgentRunResult> TriggerAsync(ScheduledAgentRunRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(ScheduledAgentRunResult.Skip(null, "Running", "该 Agent 已有未结束的 Run，本次触发跳过"));
}

/// <summary>B5 调度器测试公共件。</summary>
public static class SchedulerTestHarness
{
    /// <summary>直接插一条 Once+Enabled 的任务（绕过 service 校验，专测调度器消费行为）。</summary>
    public static long SeedOnceTask(DateTime runAtUtc, string? name = null)
    {
        var e = new ScheduledTaskEntity
        {
            Name = name ?? $"t-{Guid.NewGuid():N}",
            TaskType = (int)ScheduledTaskType.AgentRun,
            TargetId = "agent.test",
            ScheduleType = (int)ScheduleType.Once,
            RunAt = runAtUtc,
            NextRunTime = runAtUtc,
            Status = (int)ScheduledTaskStatus.Enabled,
            InputParameters = """{"agentId":"agent.test","prompt":"p"}""",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        e.Insert();
        return e.Id;
    }

    public static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, string because)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(50);
        }

        Assert.True(condition(), $"等待超时（{because}）");
    }
}
