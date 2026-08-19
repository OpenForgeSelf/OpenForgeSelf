using NewLife.Log;
using XCode;
using ScheduledTaskEntity = OpenForgeSelf.Backend.Plugins.Scheduler.Entities.ScheduledTask;
using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Backend.Plugins.Scheduler.Models;
using OpenForgeSelf.Core;

namespace OpenForgeSelf.Backend.Plugins.Scheduler.Services;

public interface ITaskScheduler
{
    Task StartAsync();
    Task StopAsync();
    Task ScheduleTaskAsync(ScheduledTask task);
    Task UnscheduleTaskAsync(long taskId);
    Task PauseTaskAsync(long taskId);
    Task ResumeTaskAsync(long taskId);
    Task RunNowAsync(long taskId);
    DateTime? GetNextRunTime(ScheduledTask task);
}

public class TaskScheduler : ITaskScheduler, ISchedulerHost
{
    private readonly ITaskExecutor _taskExecutor;
    private readonly ICronParser _cronParser;
    private CancellationTokenSource? _cts;
    private Task? _schedulerTask;
    private bool _isRunning;
    private readonly object _lock = new();
    private readonly HashSet<long> _runningTasks = new();

    public TaskScheduler(ITaskExecutor taskExecutor, IContext ctx)
    {
        _taskExecutor = taskExecutor;
        // 宿主契约（ICronParser）经 Cordis 上下文在运行期获取；插件自有服务（任务执行器）保持构造注入。
        _cronParser = ctx.Get<ICronParser>() ?? throw new InvalidOperationException("宿主未提供 ICronParser 契约，无法初始化任务调度器");
    }

    public async Task StartAsync()
    {
        if (_isRunning) return;

        lock (_lock)
        {
            if (_isRunning) return;
            _isRunning = true;
        }

        _cts = new CancellationTokenSource();
        _schedulerTask = Task.Run(() => RunSchedulerLoop(_cts.Token));

        XTrace.Log.Info("[Scheduler] 任务调度器已启动");

        await Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (!_isRunning) return;

        _cts?.Cancel();

        try
        {
            if (_schedulerTask != null)
                await _schedulerTask.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException)
        {
            XTrace.Log.Warn("[Scheduler] 任务调度器停止超时");
        }
        catch (OperationCanceledException)
        {
        }

        lock (_lock)
        {
            _isRunning = false;
        }

        XTrace.Log.Info("[Scheduler] 任务调度器已停止");
    }

    private async Task RunSchedulerLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ScanAndExecuteDueTasks(cancellationToken);
            }
            catch (Exception ex)
            {
                XTrace.Log.Error("[Scheduler] 调度循环异常: {0}", ex.Message);
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private Task ScanAndExecuteDueTasks(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var exp = new WhereExpression();
        exp &= ScheduledTaskEntity._.Status == (int)ScheduledTaskStatus.Enabled;
        exp &= ScheduledTaskEntity._.NextRunTime > DateTime.MinValue;
        exp &= ScheduledTaskEntity._.NextRunTime <= now;

        var dueTasks = ScheduledTaskEntity.FindAll(exp);

        if (dueTasks.Count == 0)
            return Task.CompletedTask;

        XTrace.Log.Debug("[Scheduler] 发现 {0} 个待执行任务", dueTasks.Count);

        foreach (var taskEntity in dueTasks)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            lock (_runningTasks)
            {
                if (_runningTasks.Contains(taskEntity.Id))
                    continue;

                _runningTasks.Add(taskEntity.Id);
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var task = MapToDto(taskEntity);
                    await _taskExecutor.ExecuteTaskAsync(task);
                }
                catch (Exception ex)
                {
                    XTrace.Log.Error("[Scheduler] 执行任务 [{0}] 失败: {1}", taskEntity.Id, ex.Message);
                }
                finally
                {
                    lock (_runningTasks)
                    {
                        _runningTasks.Remove(taskEntity.Id);
                    }
                }
            }, cancellationToken);

            try
            {
                taskEntity.LastRunTime = now;
                taskEntity.RunCount++;

                var task = MapToDto(taskEntity);
                var nextRunTime = GetNextRunTime(task);
                taskEntity.NextRunTime = nextRunTime ?? DateTime.MinValue;
                taskEntity.UpdatedAt = DateTime.UtcNow;

                if (taskEntity.ScheduleType == (int)ScheduleType.Once)
                {
                    taskEntity.Status = (int)ScheduledTaskStatus.Disabled;
                }

                taskEntity.Update();
            }
            catch (Exception ex)
            {
                XTrace.Log.Error("[Scheduler] 更新任务 [{0}] 状态失败: {1}", taskEntity.Id, ex.Message);
            }
        }

        return Task.CompletedTask;
    }

    public Task ScheduleTaskAsync(ScheduledTask task)
    {
        var entity = ScheduledTaskEntity.FindById(task.Id);
        if (entity == null)
            return Task.CompletedTask;

        entity.Status = (int)ScheduledTaskStatus.Enabled;
        entity.NextRunTime = GetNextRunTime(task) ?? DateTime.MinValue;
        entity.UpdatedAt = DateTime.UtcNow;

        entity.Update();

        XTrace.Log.Info("[Scheduler] 任务 [{0}] 已调度，下次执行时间: {1}", task.Id, entity.NextRunTime);

        return Task.CompletedTask;
    }

    public Task UnscheduleTaskAsync(long taskId)
    {
        var entity = ScheduledTaskEntity.FindById(taskId);
        if (entity == null)
            return Task.CompletedTask;

        entity.Status = (int)ScheduledTaskStatus.Disabled;
        entity.NextRunTime = DateTime.MinValue;
        entity.UpdatedAt = DateTime.UtcNow;

        entity.Update();

        XTrace.Log.Info("[Scheduler] 任务 [{0}] 已取消调度", taskId);

        return Task.CompletedTask;
    }

    public Task PauseTaskAsync(long taskId)
    {
        var entity = ScheduledTaskEntity.FindById(taskId);
        if (entity == null)
            return Task.CompletedTask;

        entity.Status = (int)ScheduledTaskStatus.Paused;
        entity.UpdatedAt = DateTime.UtcNow;

        entity.Update();

        XTrace.Log.Info("[Scheduler] 任务 [{0}] 已暂停", taskId);

        return Task.CompletedTask;
    }

    public Task ResumeTaskAsync(long taskId)
    {
        var entity = ScheduledTaskEntity.FindById(taskId);
        if (entity == null)
            return Task.CompletedTask;

        var task = MapToDto(entity);
        entity.Status = (int)ScheduledTaskStatus.Enabled;
        entity.NextRunTime = GetNextRunTime(task) ?? DateTime.MinValue;
        entity.UpdatedAt = DateTime.UtcNow;

        entity.Update();

        XTrace.Log.Info("[Scheduler] 任务 [{0}] 已恢复，下次执行时间: {1}", taskId, entity.NextRunTime);

        return Task.CompletedTask;
    }

    public Task RunNowAsync(long taskId)
    {
        var entity = ScheduledTaskEntity.FindById(taskId);
        if (entity == null)
            return Task.CompletedTask;

        var task = MapToDto(entity);

        XTrace.Log.Info("[Scheduler] 立即执行任务 [{0}]", taskId);

        _ = Task.Run(async () =>
        {
            try
            {
                await _taskExecutor.ExecuteTaskAsync(task);
            }
            catch (Exception ex)
            {
                XTrace.Log.Error("[Scheduler] 立即执行任务 [{0}] 失败: {1}", taskId, ex.Message);
            }
        });

        return Task.CompletedTask;
    }

    public DateTime? GetNextRunTime(ScheduledTask task)
    {
        try
        {
            TimeZoneInfo? timeZone = null;
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(task.TimeZone);
            }
            catch
            {
                timeZone = TimeZoneInfo.Local;
            }

            var now = DateTime.UtcNow;

            return task.ScheduleType switch
            {
                ScheduleType.Cron => GetNextCronRunTime(task, now, timeZone),
                ScheduleType.Interval => GetNextIntervalRunTime(task, now),
                ScheduleType.Once => task.RunAt,
                ScheduleType.Daily => GetNextDailyRunTime(task, now, timeZone),
                ScheduleType.Weekly => GetNextWeeklyRunTime(task, now, timeZone),
                ScheduleType.Monthly => GetNextMonthlyRunTime(task, now, timeZone),
                _ => null
            };
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[Scheduler] 计算下次执行时间失败 [{0}]: {1}", task.Id, ex.Message);
            return null;
        }
    }

    private DateTime? GetNextCronRunTime(ScheduledTask task, DateTime now, TimeZoneInfo timeZone)
    {
        if (string.IsNullOrWhiteSpace(task.CronExpression))
            return null;

        return _cronParser.GetNextRunTime(task.CronExpression, now, timeZone);
    }

    private DateTime? GetNextIntervalRunTime(ScheduledTask task, DateTime now)
    {
        if (!task.IntervalMinutes.HasValue || task.IntervalMinutes.Value <= 0)
            return null;

        var lastRun = task.LastRunTime ?? now;
        var next = lastRun.AddMinutes(task.IntervalMinutes.Value);

        if (next <= now)
            next = now.AddMinutes(task.IntervalMinutes.Value);

        return next;
    }

    private DateTime? GetNextDailyRunTime(ScheduledTask task, DateTime now, TimeZoneInfo timeZone)
    {
        if (!task.TimeOfDay.HasValue)
            return null;

        var tzNow = TimeZoneInfo.ConvertTimeFromUtc(now, timeZone);
        var today = tzNow.Date.Add(task.TimeOfDay.Value);

        var next = today <= tzNow ? today.AddDays(1) : today;
        return TimeZoneInfo.ConvertTimeToUtc(next, timeZone);
    }

    private DateTime? GetNextWeeklyRunTime(ScheduledTask task, DateTime now, TimeZoneInfo timeZone)
    {
        if (!task.TimeOfDay.HasValue || string.IsNullOrWhiteSpace(task.WeekDays))
            return null;

        var weekDays = task.WeekDays.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s.Trim(), out var d) ? d : -1)
            .Where(d => d >= 0 && d <= 6)
            .ToList();

        if (weekDays.Count == 0)
            return null;

        var tzNow = TimeZoneInfo.ConvertTimeFromUtc(now, timeZone);

        for (int i = 0; i < 7; i++)
        {
            var candidate = tzNow.Date.AddDays(i).Add(task.TimeOfDay.Value);
            if (candidate <= tzNow)
                continue;

            if (weekDays.Contains((int)candidate.DayOfWeek))
                return TimeZoneInfo.ConvertTimeToUtc(candidate, timeZone);
        }

        return null;
    }

    private DateTime? GetNextMonthlyRunTime(ScheduledTask task, DateTime now, TimeZoneInfo timeZone)
    {
        if (!task.TimeOfDay.HasValue || !task.DayOfMonth.HasValue)
            return null;

        var tzNow = TimeZoneInfo.ConvertTimeFromUtc(now, timeZone);
        var day = Math.Clamp(task.DayOfMonth.Value, 1, 31);

        for (int i = 0; i < 60; i++)
        {
            var candidateMonth = tzNow.Date.AddMonths(i);
            var daysInMonth = DateTime.DaysInMonth(candidateMonth.Year, candidateMonth.Month);
            var actualDay = Math.Min(day, daysInMonth);
            var candidate = new DateTime(candidateMonth.Year, candidateMonth.Month, actualDay).Add(task.TimeOfDay.Value);

            if (candidate > tzNow)
                return TimeZoneInfo.ConvertTimeToUtc(candidate, timeZone);
        }

        return null;
    }

    private static ScheduledTask MapToDto(ScheduledTaskEntity entity)
    {
        return new ScheduledTask
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            TaskType = (ScheduledTaskType)entity.TaskType,
            TargetId = entity.TargetId,
            ScheduleType = (ScheduleType)entity.ScheduleType,
            CronExpression = entity.CronExpression,
            IntervalMinutes = entity.IntervalMinutes > 0 ? entity.IntervalMinutes : null,
            RunAt = entity.RunAt > DateTime.MinValue ? entity.RunAt : null,
            WeekDays = entity.WeekDays,
            DayOfMonth = entity.DayOfMonth > 0 ? entity.DayOfMonth : null,
            TimeOfDay = !string.IsNullOrWhiteSpace(entity.TimeOfDay) && TimeSpan.TryParse(entity.TimeOfDay, out var ts) ? ts : null,
            TimeZone = entity.TimeZone,
            Status = (ScheduledTaskStatus)entity.Status,
            LastRunTime = entity.LastRunTime > DateTime.MinValue ? entity.LastRunTime : null,
            NextRunTime = entity.NextRunTime > DateTime.MinValue ? entity.NextRunTime : null,
            RunCount = entity.RunCount,
            FailureCount = entity.FailureCount,
            InputParameters = entity.InputParameters,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
