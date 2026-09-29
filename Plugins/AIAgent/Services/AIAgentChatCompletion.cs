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
/// 2. 每次补全在独立 scope 内解析 IAIAgentService + AIAgentProjectionService（均为 scoped），
///    复用 AIAgent 既有的「历史派生 → 跑 Agent 工具循环 → 落日志」全链路，只把流式事件收敛成最终文本；
/// 3. 不重复造聊天逻辑——IM 网关等消费方经 ctx.Get&lt;IChatCompletion&gt;() 拿到本实例即可，与底层模型/工具解耦。
/// </summary>
/// <remarks>
/// B5（041）写路径唯一化（修复 QA 定性的旁路）：
/// 本实现<b>不再直写</b>插件 <c>AIChatMessage</c> 投影表（旧实现经 <c>IPluginMessageService.SaveMessageAsync</c>
/// 直写、并经 <c>GetHistoryAsync</c> 读表构造模型输入），改为宿主会话事件日志单一真相源：
/// <list type="number">
/// <item>用户消息先落 <see cref="UserMessageEvent"/>（模型可见 = 已记录）；</item>
/// <item>模型输入只从 <see cref="ISessionStore.DeriveMessages"/> 派生（请求是日志的纯函数）；</item>
/// <item>助手回复与工具轨迹由会话版 <c>IAIAgentService.RunAgentLoopAsync(sessionId, …)</c> 内部的
/// turn/step 状态机逐条落 <c>assistant/message</c>、<c>tool/call</c>、<c>tool/result</c> 事件；</item>
/// <item>返回前经 <see cref="AIAgentProjectionService.SyncAsync"/> 幂等同步只读投影。</item>
/// </list>
/// 旧路径的危害：IM 网关会话违反两条不变量（表有日志无 / 日志有不进模型），
/// 且对该会话跑一次 SyncAsync 会按「日志为空」前缀对齐把网关写的行<b>全删</b>——真实数据丢失路径。
/// </remarks>
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

        // 每个请求独立 scope：IAIAgentService/AIAgentProjectionService 均为 scoped，且依赖 IContext（宿主在 scope 内注入）。
        await using var scope = _rootProvider.CreateAsyncScope();
        var agent = scope.ServiceProvider.GetRequiredService<IAIAgentService>();
        var ctx = scope.ServiceProvider.GetRequiredService<IContext>();
        var projection = scope.ServiceProvider.GetService<AIAgentProjectionService>();

        // 宿主会话事件日志（唯一写路径）：经 Cordis 上下文运行期获取（宿主契约不在插件子容器内）。
        var store = ctx.Get<ISessionStore>()
            ?? throw new InvalidOperationException("宿主未提供 ISessionStore 契约，IM 补全无法以会话日志为真相源");

        var sessionId = string.IsNullOrWhiteSpace(request.SessionId)
            ? Guid.NewGuid().ToString("N")
            : request.SessionId!;

        try
        {
            // 1) 唯一写路径：用户消息先落日志（模型可见 = 已记录）
            store.Append(sessionId, new UserMessageEvent(
                0, sessionId, DateTimeOffset.Now, request.Message, MessageSource.Plugin));

            // 2) 唯一读路径：模型输入只从日志派生（会话版循环内部派发；此处取一次用于观测留痕）
            var derivedCount = store.DeriveMessages(sessionId).Count;
            XTrace.Log.Debug("[AIAgentChatCompletion] 会话日志派生模型输入 {0} 条，SessionId={1}", derivedCount, sessionId);

            // 3) 跑 Agent 工具循环（会话版：工具调用/结果逐步落日志，助手回复落 assistant/message）
            var finalContent = new StringBuilder();
            var toolCalls = new List<string>();
            var sawContentChunk = false;
            string? error = null;

            await foreach (var ev in agent.RunAgentLoopAsync(
                sessionId,
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

            // 4) 返回前把只读投影同步到 AIChatMessage 表（失败不影响 IM 回复，与控制器侧 SyncProjectionSafe 同策略）
            await SyncProjectionSafeAsync(projection, sessionId);

            XTrace.Log.Info("[AIAgentChatCompletion] 补全完成 SessionId={0}, 长度={1}, 工具={2}",
                sessionId, finalContent.Length, toolCalls.Count);

            return new ChatCompletionResult
            {
                SessionId = sessionId,
                Content = finalContent.ToString(),
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

    /// <summary>
    /// 投影同步兜底：AIChatMessage 只读视图与日志对齐；失败仅记日志，不中断 IM 回复
    /// （投影库不可用时仍能返回文本，避免整条对话失败）。
    /// </summary>
    private static async Task SyncProjectionSafeAsync(AIAgentProjectionService? projection, string sessionId)
    {
        if (projection == null)
        {
            return;
        }

        try
        {
            await projection.SyncAsync(sessionId);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentChatCompletion] 会话投影同步失败（不影响 IM 回复）: SessionId={0}, {1}", sessionId, ex.Message);
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
