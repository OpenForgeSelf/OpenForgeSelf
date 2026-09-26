using NewLife;
using NewLife.Data;
using NewLife.Log;
using XCode;
using ScheduledTaskEntity = ForgeSelf.Api.Plugins.Scheduler.Entities.ScheduledTask;
using ScheduledTaskLogEntity = ForgeSelf.Api.Plugins.Scheduler.Entities.ScheduledTaskLog;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.Scheduler.Models;
using ForgeSelf.Core;
// 注意：Abstractions 与 Scheduler.Models 均有 PagedResult<T>（结构一致）。
// 为保持 ISchedulerService 公开签名不变，本文件对 PagedResult<T> 使用全限定名引用 Scheduler.Models 版本。

namespace ForgeSelf.Api.Plugins.Scheduler.Services;

public interface ISchedulerService
{
    Task<ScheduledTaskDto> CreateTaskAsync(CreateScheduledTaskRequest request);
    Task<ScheduledTaskDto?> UpdateTaskAsync(long id, UpdateScheduledTaskRequest request);
    Task<bool> DeleteTaskAsync(long id);
    Task<ScheduledTaskDto?> GetTaskAsync(long id);
    Task<ForgeSelf.Api.Plugins.Scheduler.Models.PagedResult<ScheduledTaskDto>> ListTasksAsync(string? keyword = null, ScheduledTaskStatus? status = null, int page = 1, int pageSize = 20);
    Task<ForgeSelf.Api.Plugins.Scheduler.Models.PagedResult<ScheduledTaskLogDto>> GetTaskLogsAsync(long taskId, int page = 1, int pageSize = 20);
    Task<ScheduledTaskDto?> ToggleTaskStatusAsync(long id, bool enabled);
    Task<bool> RunNowAsync(long id);
    CronParseResult ParseCron(string cronExpression, int count = 5, string? timeZone = null);
}

public class SchedulerService : ISchedulerService
{
    private readonly ICronParser _cronParser;
    private readonly ITaskScheduler _taskScheduler;

    public SchedulerService(IContext ctx, ITaskScheduler taskScheduler)
    {
        // 宿主契约（ICronParser）经 Cordis 上下文在运行期获取；插件自有服务（任务调度器）保持构造注入。
        _cronParser = ctx.Get<ICronParser>() ?? throw new InvalidOperationException("宿主未提供 ICronParser 契约，无法初始化定时任务服务");
        _taskScheduler = taskScheduler;
    }

    public Task<ScheduledTaskDto> CreateTaskAsync(CreateScheduledTaskRequest request)
    {
        try
        {
            XTrace.Log.Info("[SchedulerService] 创建定时任务: {0}, 类型: {1}", request.Name, request.TaskType);

            var now = DateTime.UtcNow;
            var entity = new ScheduledTaskEntity
            {
                Name = request.Name,
                Description = request.Description,
                TaskType = (int)request.TaskType,
                TargetId = request.TargetId,
                ScheduleType = (int)request.ScheduleType,
                CronExpression = request.CronExpression,
                IntervalMinutes = request.IntervalMinutes ?? 0,
                RunAt = request.RunAt ?? DateTime.MinValue,
                WeekDays = request.WeekDays,
                DayOfMonth = request.DayOfMonth ?? 0,
                TimeOfDay = request.TimeOfDay.HasValue ? request.TimeOfDay.Value.ToString() : null,
                TimeZone = request.TimeZone ?? "Asia/Shanghai",
                Status = (int)ScheduledTaskStatus.Enabled,
                InputParameters = request.InputParameters,
                RunCount = 0,
                FailureCount = 0,
                CreatedAt = now,
                UpdatedAt = now
            };

            var task = MapToModel(entity);
            entity.NextRunTime = _taskScheduler.GetNextRunTime(task) ?? DateTime.MinValue;

            entity.Insert();

            XTrace.Log.Info("[SchedulerService] 定时任务创建成功，Id={0}", entity.Id);

            return Task.FromResult(MapToDto(entity));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[SchedulerService] 创建定时任务失败: {0}", ex.Message);
            throw;
        }
    }

    public Task<ScheduledTaskDto?> UpdateTaskAsync(long id, UpdateScheduledTaskRequest request)
    {
        try
        {
            XTrace.Log.Info("[SchedulerService] 更新定时任务，id={0}", id);

            var entity = ScheduledTaskEntity.FindById(id);
            if (entity == null)
                return Task.FromResult<ScheduledTaskDto?>(null);

            entity.Name = request.Name;
            entity.Description = request.Description;
            entity.TaskType = (int)request.TaskType;
            entity.TargetId = request.TargetId;
            entity.ScheduleType = (int)request.ScheduleType;
            entity.CronExpression = request.CronExpression;
            entity.IntervalMinutes = request.IntervalMinutes ?? 0;
            entity.RunAt = request.RunAt ?? DateTime.MinValue;
            entity.WeekDays = request.WeekDays;
            entity.DayOfMonth = request.DayOfMonth ?? 0;
            entity.TimeOfDay = request.TimeOfDay.HasValue ? request.TimeOfDay.Value.ToString() : null;
            entity.TimeZone = request.TimeZone ?? "Asia/Shanghai";
            entity.InputParameters = request.InputParameters;
            entity.UpdatedAt = DateTime.UtcNow;

            if (entity.Status == (int)ScheduledTaskStatus.Enabled)
            {
                var task = MapToModel(entity);
                entity.NextRunTime = _taskScheduler.GetNextRunTime(task) ?? DateTime.MinValue;
            }

            entity.Update();

            XTrace.Log.Info("[SchedulerService] 定时任务更新成功，Id={0}", id);
            return Task.FromResult<ScheduledTaskDto?>(MapToDto(entity));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[SchedulerService] 更新定时任务失败 [{0}]: {1}", id, ex.Message);
            throw;
        }
    }

    public Task<bool> DeleteTaskAsync(long id)
    {
        try
        {
            XTrace.Log.Info("[SchedulerService] 删除定时任务，id={0}", id);

            var entity = ScheduledTaskEntity.FindById(id);
            if (entity == null)
                return Task.FromResult(false);

            var logs = ScheduledTaskLogEntity.FindAll(ScheduledTaskLogEntity._.TaskId == id);
            foreach (var log in logs)
            {
                log.Delete();
            }

            entity.Delete();

            XTrace.Log.Info("[SchedulerService] 定时任务删除成功，Id={0}", id);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[SchedulerService] 删除定时任务失败 [{0}]: {1}", id, ex.Message);
            throw;
        }
    }

    public Task<ScheduledTaskDto?> GetTaskAsync(long id)
    {
        try
        {
            XTrace.Log.Debug("[SchedulerService] 获取定时任务详情，id={0}", id);

            var entity = ScheduledTaskEntity.FindById(id);
            if (entity == null)
                return Task.FromResult<ScheduledTaskDto?>(null);

            return Task.FromResult<ScheduledTaskDto?>(MapToDto(entity));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[SchedulerService] 获取定时任务详情失败 [{0}]: {1}", id, ex.Message);
            throw;
        }
    }

    public Task<ForgeSelf.Api.Plugins.Scheduler.Models.PagedResult<ScheduledTaskDto>> ListTasksAsync(string? keyword = null, ScheduledTaskStatus? status = null, int page = 1, int pageSize = 20)
    {
        try
        {
            XTrace.Log.Debug("[SchedulerService] 获取定时任务列表，keyword={0}, status={1}, page={2}, pageSize={3}",
                keyword, status, page, pageSize);

            var exp = new WhereExpression();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim();
                exp &= (ScheduledTaskEntity._.Name.Contains(kw) | ScheduledTaskEntity._.Description.Contains(kw));
            }

            if (status.HasValue)
            {
                exp &= ScheduledTaskEntity._.Status == (int)status.Value;
            }

            var pageParam = new PageParameter
            {
                PageIndex = page - 1,
                PageSize = pageSize,
                Sort = "CreatedAt",
                Desc = true,
                RetrieveTotalCount = true
            };

            var tasks = ScheduledTaskEntity.FindAll(exp, pageParam);
            var total = (int)pageParam.TotalCount;

            var items = tasks.Select(MapToDto).ToList();

            XTrace.Log.Info("[SchedulerService] 获取定时任务列表成功，总数: {0}, 当前页数量: {1}", total, items.Count);

            return Task.FromResult(new ForgeSelf.Api.Plugins.Scheduler.Models.PagedResult<ScheduledTaskDto>
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize
            });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[SchedulerService] 获取定时任务列表失败: {0}", ex.Message);
            throw;
        }
    }

    public Task<ForgeSelf.Api.Plugins.Scheduler.Models.PagedResult<ScheduledTaskLogDto>> GetTaskLogsAsync(long taskId, int page = 1, int pageSize = 20)
    {
        try
        {
            XTrace.Log.Debug("[SchedulerService] 获取任务执行日志，taskId={0}, page={1}, pageSize={2}", taskId, page, pageSize);

            var exp = ScheduledTaskLogEntity._.TaskId == taskId;

            var pageParam = new PageParameter
            {
                PageIndex = page - 1,
                PageSize = pageSize,
                Sort = "StartTime",
                Desc = true,
                RetrieveTotalCount = true
            };

            var logs = ScheduledTaskLogEntity.FindAll(exp, pageParam);
            var total = (int)pageParam.TotalCount;

            var items = logs.Select(MapLogToDto).ToList();

            XTrace.Log.Debug("[SchedulerService] 获取任务执行日志成功，总数: {0}", total);

            return Task.FromResult(new ForgeSelf.Api.Plugins.Scheduler.Models.PagedResult<ScheduledTaskLogDto>
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize
            });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[SchedulerService] 获取任务执行日志失败 [{0}]: {1}", taskId, ex.Message);
            throw;
        }
    }

    public Task<ScheduledTaskDto?> ToggleTaskStatusAsync(long id, bool enabled)
    {
        try
        {
            XTrace.Log.Info("[SchedulerService] 切换任务状态，id={0}, enabled={1}", id, enabled);

            var entity = ScheduledTaskEntity.FindById(id);
            if (entity == null)
                return Task.FromResult<ScheduledTaskDto?>(null);

            if (enabled)
            {
                _taskScheduler.ResumeTaskAsync(id).Wait();
                entity.Status = (int)ScheduledTaskStatus.Enabled;
                var task = MapToModel(entity);
                entity.NextRunTime = _taskScheduler.GetNextRunTime(task) ?? DateTime.MinValue;
            }
            else
            {
                _taskScheduler.PauseTaskAsync(id).Wait();
                entity.Status = (int)ScheduledTaskStatus.Disabled;
                entity.NextRunTime = DateTime.MinValue;
            }

            entity.UpdatedAt = DateTime.UtcNow;
            entity.Update();

            return Task.FromResult<ScheduledTaskDto?>(MapToDto(entity));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[SchedulerService] 切换任务状态失败 [{0}]: {1}", id, ex.Message);
            throw;
        }
    }

    public Task<bool> RunNowAsync(long id)
    {
        try
        {
            XTrace.Log.Info("[SchedulerService] 立即执行任务，id={0}", id);

            var entity = ScheduledTaskEntity.FindById(id);
            if (entity == null)
                return Task.FromResult(false);

            _taskScheduler.RunNowAsync(id).Wait();
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[SchedulerService] 立即执行任务失败 [{0}]: {1}", id, ex.Message);
            throw;
        }
    }

    public CronParseResult ParseCron(string cronExpression, int count = 5, string? timeZone = null)
    {
        try
        {
            XTrace.Log.Debug("[SchedulerService] 解析Cron表达式: {0}", cronExpression);

            var result = new CronParseResult
            {
                Valid = _cronParser.IsValid(cronExpression)
            };

            if (!result.Valid)
            {
                result.ErrorMessage = "无效的Cron表达式";
                return result;
            }

            TimeZoneInfo? tz = null;
            if (!string.IsNullOrWhiteSpace(timeZone))
            {
                try
                {
                    tz = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
                }
                catch
                {
                    tz = TimeZoneInfo.Local;
                }
            }

            result.NextRunTimes = _cronParser.GetNextRunTimes(
                cronExpression,
                DateTime.UtcNow,
                Math.Clamp(count, 1, 50),
                tz);

            return result;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[SchedulerService] 解析Cron表达式失败: {0}", ex.Message);
            return new CronParseResult
            {
                Valid = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private static ScheduledTask MapToModel(ScheduledTaskEntity entity)
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

    private static ScheduledTaskDto MapToDto(ScheduledTaskEntity entity)
    {
        return new ScheduledTaskDto
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

    private static ScheduledTaskLogDto MapLogToDto(ScheduledTaskLogEntity entity)
    {
        return new ScheduledTaskLogDto
        {
            Id = entity.Id,
            TaskId = entity.TaskId,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime > DateTime.MinValue ? entity.EndTime : null,
            Status = (ScheduledTaskLogStatus)entity.Status,
            ResultMessage = entity.ResultMessage,
            ErrorMessage = entity.ErrorMessage,
            DurationMs = entity.DurationMs
        };
    }
}
