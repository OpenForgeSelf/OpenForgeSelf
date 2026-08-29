using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.Scheduler.Models;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.Scheduler.Services;

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

            // 通过 DI 解析 IWorkflowExecutor（接口已迁 Abstractions，实现由 WorkflowEngine 插件注册）。
            // 取代旧的 Type.GetType("...WorkflowExecutor, ForgeSelf.Api") 反射 + 硬编码程序集名；
            // 旧反射 Activator.CreateInstance(type, dbPath, _serviceProvider) 与当前构造函数
            // WorkflowExecutor(IServiceProvider?) 签名不匹配，运行时会抛 MissingMethodException（陈旧代码）。
            var executor = _serviceProvider.GetRequiredService<IWorkflowExecutor>();
            var execution = await executor.ExecuteAsync(workflowId, inputVariables, "scheduler");

            if (execution == null)
            {
                return "工作流已启动";
            }

            return $"工作流执行已启动，执行ID: {execution.Id}，状态: {execution.Status}";
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[WorkflowTaskHandler] 执行工作流任务失败: {0}", ex.Message);
            throw;
        }
    }
}
