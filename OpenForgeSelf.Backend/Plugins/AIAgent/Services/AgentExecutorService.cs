using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Backend.Plugins.AIAgent.Models;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.AIAgent.Services;

public interface IAgentExecutorService
{
    Task<AgentExecutionResult> ExecuteTaskAsync(AgentTask task, CancellationToken cancellationToken = default);
    AgentInstance? GetAgentInstance(string instanceId);
    List<AgentInstance> GetActiveInstances();
}

public class AgentExecutorService : IAgentExecutorService
{
    private readonly IAgentRegistryService _agentRegistry;
    private readonly IAIAgentService _aiAgentService;
    private readonly ConcurrentDictionary<string, AgentInstance> _activeInstances = new();

    public AgentExecutorService(IAgentRegistryService agentRegistry, IAIAgentService aiAgentService)
    {
        _agentRegistry = agentRegistry;
        _aiAgentService = aiAgentService;
    }

    public async Task<AgentExecutionResult> ExecuteTaskAsync(AgentTask task, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[AgentExecutor] 开始执行任务: {0}, Agent类型: {1}",
                task.TaskId, task.AssignedAgentType);

            var agentDef = ResolveAgent(task);
            if (agentDef == null)
            {
                XTrace.Log.Warn("[AgentExecutor] 未找到合适的 Agent，使用通用 Agent");
                agentDef = _agentRegistry.GetAgent("agent.generalist")
                    ?? throw new InvalidOperationException("没有可用的 Agent");
            }

            var instance = CreateAgentInstance(agentDef, task);
            _activeInstances[instance.InstanceId] = instance;

            try
            {
                instance.Status = AgentStatus.Thinking;
                task.StartedAt = DateTime.Now;
                task.Status = AgentTaskStatus.InProgress;

                var messages = BuildAgentMessages(agentDef, task);
                instance.Context = messages;

                instance.Status = AgentStatus.Working;

                var result = await ExecuteAgentWorkAsync(agentDef, instance, task, cancellationToken);

                instance.Status = AgentStatus.Completed;
                task.Status = AgentTaskStatus.Completed;
                task.CompletedAt = DateTime.Now;

                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;

                XTrace.Log.Info("[AgentExecutor] 任务执行成功: {0}, 耗时: {1}ms",
                    task.TaskId, stopwatch.ElapsedMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                instance.Status = AgentStatus.Failed;
                task.Status = AgentTaskStatus.Failed;
                task.ErrorMessage = ex.Message;

                stopwatch.Stop();

                XTrace.Log.Error("[AgentExecutor] 任务执行失败: {0}, 错误: {1}",
                    task.TaskId, ex.Message);

                return new AgentExecutionResult
                {
                    Success = false,
                    AgentId = agentDef.Id,
                    AgentName = agentDef.Name,
                    TaskId = task.TaskId,
                    ErrorMessage = ex.Message,
                    Duration = stopwatch.Elapsed
                };
            }
            finally
            {
                instance.LastActiveAt = DateTime.Now;
                var instanceId = instance.InstanceId;
                _ = Task.Delay(TimeSpan.FromMinutes(30))
                    .ContinueWith(t =>
                    {
                        _activeInstances.TryRemove(instanceId, out _);
                    });
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            XTrace.Log.Error("[AgentExecutor] 执行任务异常: {0}", ex.Message);
            return new AgentExecutionResult
            {
                Success = false,
                AgentId = "unknown",
                AgentName = "未知 Agent",
                TaskId = task.TaskId,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    public AgentInstance? GetAgentInstance(string instanceId)
    {
        _activeInstances.TryGetValue(instanceId, out var instance);
        return instance;
    }

    public List<AgentInstance> GetActiveInstances()
    {
        return _activeInstances.Values.ToList();
    }

    private AgentDefinition? ResolveAgent(AgentTask task)
    {
        if (!string.IsNullOrEmpty(task.AssignedAgentId))
        {
            var agent = _agentRegistry.GetAgent(task.AssignedAgentId);
            if (agent != null) return agent;
        }

        var agents = _agentRegistry.GetAgentsByType(task.AssignedAgentType);
        if (agents.Count > 0)
            return agents[0];

        return _agentRegistry.GetAgent("agent.generalist");
    }

    private static AgentInstance CreateAgentInstance(AgentDefinition agentDef, AgentTask task)
    {
        return new AgentInstance
        {
            InstanceId = $"inst-{Guid.NewGuid():N}",
            AgentId = agentDef.Id,
            AgentName = agentDef.Name,
            AgentType = agentDef.Type,
            Status = AgentStatus.Idle,
            SessionId = task.TaskId,
            CurrentTaskId = task.TaskId,
            CreatedAt = DateTime.Now,
            LastActiveAt = DateTime.Now
        };
    }

    private static List<AIChatMessage> BuildAgentMessages(AgentDefinition agentDef, AgentTask task)
    {
        var messages = new List<AIChatMessage>();

        var systemPrompt = BuildFullSystemPrompt(agentDef, task);
        messages.Add(new AIChatMessage { Role = "system", Content = systemPrompt });

        messages.Add(new AIChatMessage { Role = "user", Content = task.Input });

        return messages;
    }

    private static string BuildFullSystemPrompt(AgentDefinition agentDef, AgentTask task)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# 角色设定：{agentDef.Name}");
        sb.AppendLine();
        sb.AppendLine(agentDef.Description);
        sb.AppendLine();

        sb.AppendLine("## 人格特征");
        sb.AppendLine($"- 创造力: {GetLevelText(agentDef.Personality.Creativity)}");
        sb.AppendLine($"- 分析力: {GetLevelText(agentDef.Personality.Analytical)}");
        sb.AppendLine($"- 同理心: {GetLevelText(agentDef.Personality.Empathy)}");
        sb.AppendLine($"- 自信心: {GetLevelText(agentDef.Personality.Confidence)}");
        sb.AppendLine($"- 正式度: {GetLevelText(agentDef.Personality.Formality)}");
        sb.AppendLine();

        sb.AppendLine($"## 语言风格");
        sb.AppendLine($"- 语气风格: {agentDef.Personality.ToneStyle}");
        sb.AppendLine($"- 沟通方式: {agentDef.Personality.CommunicationStyle}");
        sb.AppendLine();

        if (agentDef.Personality.Strengths.Count > 0)
        {
            sb.AppendLine("## 擅长领域");
            foreach (var strength in agentDef.Personality.Strengths)
            {
                sb.AppendLine($"- {strength}");
            }
            sb.AppendLine();
        }

        if (agentDef.Personality.Limitations.Count > 0)
        {
            sb.AppendLine("## 局限性");
            foreach (var limitation in agentDef.Personality.Limitations)
            {
                sb.AppendLine($"- {limitation}");
            }
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(agentDef.SystemPrompt))
        {
            sb.AppendLine("## 核心指令");
            sb.AppendLine(agentDef.SystemPrompt);
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(agentDef.Personality.SystemPromptAddon))
        {
            sb.AppendLine("## 附加指令");
            sb.AppendLine(agentDef.Personality.SystemPromptAddon);
            sb.AppendLine();
        }

        if (agentDef.Capabilities.Count > 0)
        {
            sb.AppendLine("## 能力清单");
            foreach (var cap in agentDef.Capabilities)
            {
                sb.AppendLine($"- {cap}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## 当前任务");
        sb.AppendLine(task.Description);
        sb.AppendLine();
        sb.AppendLine($"任务优先级: {task.Priority}");
        sb.AppendLine();

        sb.AppendLine("请以上述角色和风格完成任务，提供高质量的输出。");

        return sb.ToString();
    }

    private static string GetLevelText(double value)
    {
        return value switch
        {
            < 0.25 => "很低",
            < 0.4 => "较低",
            < 0.6 => "中等",
            < 0.75 => "较高",
            _ => "很高"
        };
    }

    private async Task<AgentExecutionResult> ExecuteAgentWorkAsync(
        AgentDefinition agentDef,
        AgentInstance instance,
        AgentTask task,
        CancellationToken cancellationToken)
    {
        try
        {
            XTrace.Log.Debug("[AgentExecutor] Agent {0} 开始工作", agentDef.Name);

            var useTools = agentDef.Tools != null && agentDef.Tools.Count > 0;
            var result = await _aiAgentService.ChatAsync(instance.Context, useTools);

            task.Output = result;
            instance.Context.Add(new AIChatMessage { Role = "assistant", Content = result });

            var toolsUsed = new List<string>();
            var iterations = 1;

            return new AgentExecutionResult
            {
                Success = true,
                AgentId = agentDef.Id,
                AgentName = agentDef.Name,
                TaskId = task.TaskId,
                Output = result,
                Iterations = iterations,
                ToolsUsed = toolsUsed
            };
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AgentExecutor] Agent 执行异常: {0}", ex.Message);
            throw;
        }
    }
}
