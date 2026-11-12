using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.Scheduler.Models;
using ForgeSelf.Core;

namespace ForgeSelf.Api.Plugins.Scheduler.Services;

/// <summary>
/// 到点触发本机智能体 Run。配置在 <see cref="ScheduledTask.InputParameters"/>（JSON），
/// Bot/AgentId 也可放在 <see cref="ScheduledTask.TargetId"/>。
/// 不执行系统命令；写操作由 AIAgent 现有审批处理。
/// </summary>
public static class AgentRunTaskHandler
{
    public static async Task<(bool Skipped, string Message)> ExecuteAsync(ScheduledTask task, IServiceProvider services)
    {
        var config = Parse(task);
        var ctx = ResolveContext(services);
        var trigger = ctx?.Get<IScheduledAgentRunTrigger>();
        if (trigger == null)
            throw new InvalidOperationException("本机 Agent Run 触发契约未注册（AIAgent 插件未启用），已拒绝执行，不会改走系统命令。");

        var result = await trigger.TriggerAsync(new ScheduledAgentRunRequest
        {
            ScheduledTaskId = task.Id,
            AgentId = config.AgentId,
            Workspace = config.Workspace,
            Prompt = config.Prompt,
            ApprovalPolicy = config.ApprovalPolicy
        });

        if (!result.Success)
            throw new InvalidOperationException(result.Message);

        return (result.Skipped, result.Message);
    }

    private static AgentRunConfig Parse(ScheduledTask task)
    {
        var config = new AgentRunConfig();
        if (!string.IsNullOrWhiteSpace(task.InputParameters))
        {
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(task.InputParameters);
            }
            catch (JsonException ex)
            {
                throw new ArgumentException($"AgentRun 的 InputParameters 必须是 JSON 对象：{ex.Message}");
            }

            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    throw new ArgumentException("AgentRun 的 InputParameters 必须是 JSON 对象");

                config.AgentId = FirstString(root, "agentId", "botId") ?? string.Empty;
                config.Workspace = FirstString(root, "workspace", "cwd");
                config.Prompt = FirstString(root, "prompt", "taskInput") ?? string.Empty;
                config.ApprovalPolicy = FirstString(root, "approvalPolicy");
            }
        }

        if (string.IsNullOrWhiteSpace(config.AgentId))
            config.AgentId = task.TargetId?.Trim() ?? string.Empty;

        return config;
    }

    private static string? FirstString(JsonElement root, params string[] names)
    {
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.String)
                continue;
            foreach (var name in names)
            {
                if (!prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    continue;
                var text = prop.Value.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    return text.Trim();
            }
        }

        return null;
    }

    private static IContext? ResolveContext(IServiceProvider services)
    {
        if (services is IContext direct)
            return direct;
        return services.GetService(typeof(IContext)) as IContext;
    }

    private sealed class AgentRunConfig
    {
        public string AgentId { get; set; } = string.Empty;
        public string? Workspace { get; set; }
        public string Prompt { get; set; } = string.Empty;
        public string? ApprovalPolicy { get; set; }
    }
}
