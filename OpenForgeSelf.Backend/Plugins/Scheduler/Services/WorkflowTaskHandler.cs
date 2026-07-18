using System.Text.Json;
using OpenForgeSelf.Backend.Plugins.Scheduler.Models;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.Scheduler.Services;

public class WorkflowTaskHandler
{
    private readonly IServiceProvider _serviceProvider;

    public WorkflowTaskHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(ScheduledTask task)
    {
        try
        {
            XTrace.Log.Info("[WorkflowTaskHandler] 执行工作流任务: {0} (WorkflowId: {1})", task.Name, task.TargetId);

            if (!long.TryParse(task.TargetId, out var workflowId))
            {
                throw new ArgumentException($"无效的工作流ID: {task.TargetId}");
            }

            Dictionary<string, object?>? inputVariables = null;
            if (!string.IsNullOrWhiteSpace(task.InputParameters))
            {
                try
                {
                    inputVariables = JsonSerializer.Deserialize<Dictionary<string, object?>>(task.InputParameters);
                }
                catch (JsonException ex)
                {
                    XTrace.Log.Warn("[WorkflowTaskHandler] 解析输入参数失败: {0}", ex.Message);
                }
            }

            var dbPath = Path.Combine(AppContext.BaseDirectory, "workflow.db");
            var workflowExecutorType = Type.GetType(
                "OpenForgeSelf.Backend.Plugins.WorkflowEngine.Services.WorkflowExecutor, OpenForgeSelf.Backend");

            if (workflowExecutorType == null)
            {
                throw new InvalidOperationException("工作流执行器不可用，请确保工作流引擎插件已启用");
            }

            var executor = Activator.CreateInstance(workflowExecutorType, dbPath, _serviceProvider);
            if (executor == null)
            {
                throw new InvalidOperationException("无法创建工作流执行器实例");
            }

            var executeMethod = workflowExecutorType.GetMethod("ExecuteAsync");
            if (executeMethod == null)
            {
                throw new InvalidOperationException("工作流执行器缺少 ExecuteAsync 方法");
            }

            var taskResult = (Task?)executeMethod.Invoke(executor, new object?[] { workflowId, inputVariables, "scheduler" });
            if (taskResult == null)
            {
                throw new InvalidOperationException("工作流执行方法返回null");
            }

            await taskResult;

            var resultProperty = taskResult.GetType().GetProperty("Result");
            var execution = resultProperty?.GetValue(taskResult);

            if (execution == null)
            {
                return "工作流已启动";
            }

            var idProperty = execution.GetType().GetProperty("Id");
            var statusProperty = execution.GetType().GetProperty("Status");

            var executionId = idProperty?.GetValue(execution)?.ToString() ?? "unknown";
            var status = statusProperty?.GetValue(execution)?.ToString() ?? "unknown";

            return $"工作流执行已启动，执行ID: {executionId}，状态: {status}";
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[WorkflowTaskHandler] 执行工作流任务失败: {0}", ex.Message);
            throw;
        }
    }
}
