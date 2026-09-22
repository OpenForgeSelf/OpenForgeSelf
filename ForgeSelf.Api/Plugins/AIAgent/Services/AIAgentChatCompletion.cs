using System.Diagnostics.CodeAnalysis;
using System.Text;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

/// <summary>
/// <see cref="IChatCompletion"/> 的 AIAgent 落地实现（跨插件聊天补全契约的提供方）。
/// 设计要点：
/// 1. 自身是单例，持有 Apply 阶段构建的根 IServiceProvider（与 IWorkflowAIAdvisor 同款模式）；
/// 2. 每次补全在独立 scope 内解析 IAIAgentService + IPluginMessageService（均为 scoped），
///    复用 AIAgent 既有的「历史加载 → 跑 Agent 工具循环 → 落库」全链路，只把流式事件收敛成最终文本；
/// 3. 不重复造聊天逻辑——IM 网关等消费方经 ctx.Get&lt;IChatCompletion&gt;() 拿到本实例即可，与底层模型/工具解耦。
/// </summary>
public class AIAgentChatCompletion : IChatCompletion
{
    private readonly IServiceProvider _rootProvider;

    public AIAgentChatCompletion(IServiceProvider rootProvider)
    {
        _rootProvider = rootProvider ?? throw new ArgumentNullException(nameof(rootProvider));
    }

    public async Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return new ChatCompletionResult { Success = false, Error = "消息不能为空" };
        }

        // 每个请求独立 scope：IAIAgentService/IPluginMessageService 均为 scoped，且依赖 IContext（宿主在 scope 内注入）。
        await using var scope = _rootProvider.CreateAsyncScope();
        var agent = scope.ServiceProvider.GetRequiredService<IAIAgentService>();
        var messageService = scope.ServiceProvider.GetRequiredService<IPluginMessageService>();

        var sessionId = string.IsNullOrWhiteSpace(request.SessionId)
            ? Guid.NewGuid().ToString("N")
            : request.SessionId!;

        try
        {
            // 1) 落库用户消息（与 AIChatController 同口径，保证历史连续）。
            await messageService.SaveMessageAsync(sessionId, "user", request.Message);

            // 2) 加载历史，构造统一消息列表。
            var history = await messageService.GetHistoryAsync(sessionId);
            var aiMessages = history.Select(m => new Abstractions.AIChatMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList();

            // 3) 跑 Agent 工具循环，收敛流式事件为最终文本。
            var finalContent = new StringBuilder();
            var toolCalls = new List<string>();
            var sawContentChunk = false;
            string? error = null;

            await foreach (var ev in agent.RunAgentLoopAsync(
                aiMessages,
                request.ChatModelId,
                request.AgentId,
                request.EnabledToolNames,
                null,
                true,
                cancellationToken))
            {
                switch (ev.Type)
                {
                    case "content":
                        if (!string.IsNullOrEmpty(ev.Content))
                        {
                            finalContent.Append(ev.Content);
                            sawContentChunk = true;
                        }
                        break;
                    case "tool_call":
                        if (!string.IsNullOrEmpty(ev.Name) && !toolCalls.Contains(ev.Name!))
                            toolCalls.Add(ev.Name!);
                        break;
                    case "done":
                        // done 携带完整文本；仅当没有增量 content 事件时才作为兜底，避免重复。
                        if (!sawContentChunk && !string.IsNullOrEmpty(ev.Content))
                            finalContent.Append(ev.Content);
                        break;
                    case "error":
                        error = ev.Content;
                        break;
                }
            }

            if (error != null)
            {
                XTrace.Log.Error("[AIAgentChatCompletion] 补全失败 SessionId={0}: {1}", sessionId, error);
                return new ChatCompletionResult
                {
                    SessionId = sessionId,
                    Success = false,
                    Error = error
                };
            }

            var content = finalContent.ToString();

            // 4) 落库助手消息（带工具轨迹留痕，便于复盘）。
            var toolCallsJson = System.Text.Json.JsonSerializer.Serialize(toolCalls);
            await messageService.SaveMessageAsync(sessionId, "assistant", content, toolCallsJson);

            XTrace.Log.Info("[AIAgentChatCompletion] 补全完成 SessionId={0}, 长度={1}, 工具={2}",
                sessionId, content.Length, toolCalls.Count);

            return new ChatCompletionResult
            {
                SessionId = sessionId,
                Content = content,
                ToolCalls = toolCalls,
                Success = true
            };
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentChatCompletion] 补全异常 SessionId={0}: {1}", sessionId, ex.Message);
            return new ChatCompletionResult
            {
                SessionId = sessionId,
                Success = false,
                Error = ex.Message
            };
        }
    }
}

/// <summary>
/// 构建根 IServiceProvider 的扩展（Apply 阶段调用一次）。
/// 与 IWorkflowAIAdvisor 落地同款：从插件 IServiceCollection 构建 provider，
/// 该集合即宿主 DI 容器（含 IContext 等宿主契约），故 scope 内可解析 IAIAgentService。
/// </summary>
[ExcludeFromCodeCoverage]
internal static class ChatCompletionRegistration
{
    public static void RegisterChatCompletion(this IServiceCollection services, IContext ctx)
    {
        // 复用插件既有的宿主 DI 集合构建 provider（含宿主契约注册）。
        var rootProvider = services.BuildServiceProvider();
        var completion = new AIAgentChatCompletion(rootProvider);

        // 双注册：services（宿主 DI 可见）+ ctx 共享服务表（跨插件 ctx.Get 可见），同一实例。
        services.AddSingleton<IChatCompletion>(completion);
        ctx.Register<IChatCompletion>(completion);

        XTrace.Log.Info("[AIAgentPlugin] 已注册 IChatCompletion（跨插件聊天补全契约）");
    }
}
