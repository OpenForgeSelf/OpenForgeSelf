using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using ForgeSelf.Api.Plugins.Scheduler.Models;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.Scheduler.Services;

public class WorkflowTaskHandler
{
    private readonly IContext _ctx;

    public WorkflowTaskHandler(IContext ctx)
    {
        // PC-034：构造注入 IContext（Cordis 模式）。DI 子容器与工具手动路径（传 ctx）行为一致——
        // IWorkflowExecutor 由 WorkflowEngine ctx.Register 进共享表，ctx.GetRequiredService 两路都可解析；
        // 旧 IServiceProvider 注入在 DI 子容器路径会因缺 IWorkflowExecutor 抛 InvalidOperationException。
        _ctx = ctx;
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

            // 通过 ctx（IContext）解析 IWorkflowExecutor（接口已迁 Abstractions，实现由 WorkflowEngine 插件
            // ctx.Register 进共享表）。取代旧的 Type.GetType("...WorkflowExecutor, ForgeSelf.Api") 反射 +
            // 硬编码程序集名；旧反射 Activator.CreateInstance(type, dbPath, ctx) 与当前构造函数
            // WorkflowExecutor(IContext?) 签名不匹配，运行时会抛 MissingMethodException（陈旧代码）。
            var executor = _ctx.GetRequiredService<IWorkflowExecutor>();
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
