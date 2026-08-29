using System.Diagnostics;
using System.Text.Json;
using NewLife.Log;
using XCode;
using ScheduledTaskEntity = ForgeSelf.Api.Plugins.Scheduler.Entities.ScheduledTask;
using ScheduledTaskLogEntity = ForgeSelf.Api.Plugins.Scheduler.Entities.ScheduledTaskLog;
using ForgeSelf.Api.Plugins.Scheduler.Models;

namespace ForgeSelf.Api.Plugins.Scheduler.Services;

public interface ITaskExecutor
{
    Task ExecuteTaskAsync(ScheduledTask task);
}

public class TaskExecutor : ITaskExecutor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly WorkflowTaskHandler _workflowHandler;
    private readonly HttpWebhookHandler _httpHandler;

    public TaskExecutor(IServiceProvider serviceProvider,
        WorkflowTaskHandler workflowHandler, HttpWebhookHandler httpHandler)
    {
        _serviceProvider = serviceProvider;
        _workflowHandler = workflowHandler;
        _httpHandler = httpHandler;
    }

    public async Task ExecuteTaskAsync(ScheduledTask task)
    {
        var stopwatch = Stopwatch.StartNew();
        long logId = 0;

        try
        {
            XTrace.Log.Info("[TaskExecutor] 开始执行任务 [{0}] {1}", task.Id, task.Name);

            logId = CreateTaskLog(task.Id);

            string resultMessage;

            switch (task.TaskType)
            {
                case ScheduledTaskType.Workflow:
                    resultMessage = await _workflowHandler.ExecuteAsync(task);
                    break;

                case ScheduledTaskType.HttpWebhook:
                    resultMessage = await _httpHandler.ExecuteAsync(task);
                    break;

                case ScheduledTaskType.SystemCommand:
                    resultMessage = ExecuteSystemCommand(task);
                    break;

                default:
                    throw new NotSupportedException($"不支持的任务类型: {task.TaskType}");
            }

            stopwatch.Stop();
            UpdateTaskLog(logId, ScheduledTaskLogStatus.Success, resultMessage, null, stopwatch.Elapsed.TotalMilliseconds);

            XTrace.Log.Info("[TaskExecutor] 任务 [{0}] {1} 执行成功，耗时 {2}ms", task.Id, task.Name, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            XTrace.Log.Error("[TaskExecutor] 任务 [{0}] {1} 执行失败: {2}", task.Id, task.Name, ex.Message);

            UpdateTaskLog(logId, ScheduledTaskLogStatus.Failed, null, ex.Message, stopwatch.Elapsed.TotalMilliseconds);

            try
            {
                var entity = ScheduledTaskEntity.FindById(task.Id);
                if (entity != null)
                {
                    entity.FailureCount++;
                    entity.UpdatedAt = DateTime.UtcNow;
                    entity.Update();
                }
            }
            catch
            {
            }

            throw;
        }
    }

    private static long CreateTaskLog(long taskId)
    {
        var log = new ScheduledTaskLogEntity
            {
            TaskId = taskId,
            StartTime = DateTime.UtcNow,
            Status = (int)ScheduledTaskLogStatus.Running
        };

        log.Insert();

        return log.Id;
    }

    private static void UpdateTaskLog(long logId, ScheduledTaskLogStatus status, string? resultMessage, string? errorMessage, double durationMs)
    {
        var log = ScheduledTaskLogEntity.FindById(logId);
        if (log == null) return;

        log.EndTime = DateTime.UtcNow;
        log.Status = (int)status;
        log.ResultMessage = resultMessage;
        log.ErrorMessage = errorMessage;
        log.DurationMs = durationMs;

        log.Update();
    }

    private static string ExecuteSystemCommand(ScheduledTask task)
    {
        try
        {
            var parameters = string.IsNullOrWhiteSpace(task.InputParameters)
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(task.InputParameters) ?? new Dictionary<string, string>();

            var command = task.TargetId;
            var arguments = parameters.TryGetValue("arguments", out var args) ? args : string.Empty;

            XTrace.Log.Info("[TaskExecutor] 执行系统命令: {0} {1}", command, arguments);

            var processInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
                return "进程启动失败";

            process.WaitForExit(300000);

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            if (process.ExitCode != 0)
                throw new Exception($"命令执行失败，退出码: {process.ExitCode}\n{error}");

            return string.IsNullOrEmpty(output) ? "命令执行成功" : output;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[TaskExecutor] 系统命令执行失败: {0}", ex.Message);
            throw;
        }
    }
}
