using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using ForgeSelf.Api.Plugins.ImGateway.Core;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.ImGateway.Services;

/// <summary>
/// IM 网关路由核心（Gateway）：把「统一入站消息」接通「AI Agent」并回发。
/// 职责：
/// 1. 幂等去重（WeChat 超时重试会重复推同一条）。
/// 2. 会话映射：把「通道:会话:用户」稳定映射到 AIAgent 的 SessionId（上下文连续）。
/// 3. 经 IChatCompletion 契约调 AI（与平台解耦）。
/// 4. 把 AI 回复经对应通道适配器回发。
/// 本身不关心任何平台协议——那都是 IImChannel 的职责。
/// </summary>
public class ImGatewayRouter
{
    private readonly IContext _ctx;
    private readonly IConfigStore _configStore;
    private readonly DbSessionStore _sessionStore;
    private readonly Dictionary<string, IImChannel> _channels;

    public ImGatewayRouter(IContext ctx, IConfigStore configStore, DbSessionStore sessionStore, IEnumerable<IImChannel> channels)
    {
        _ctx = ctx;
        _configStore = configStore;
        _sessionStore = sessionStore;
        _channels = channels.ToDictionary(c => c.ChannelType, c => c);
    }

    /// <summary>处理一条入站消息：去重 → 映射会话 → 调 AI → 回发。全程吞异常，绝不阻塞网关。</summary>
    public async Task ProcessInboundAsync(InboundMessage inbound, CancellationToken cancellationToken = default)
    {
        try
        {
            // 1) 幂等去重
            if (!string.IsNullOrEmpty(inbound.MsgId) && _sessionStore.IsProcessed(inbound.ChannelType, inbound.MsgId))
            {
                XTrace.Log.Info("[IM网关] 重复消息已忽略 Channel={0} MsgId={1}", inbound.ChannelType, inbound.MsgId);
                return;
            }
            if (!string.IsNullOrEmpty(inbound.MsgId)) _sessionStore.MarkProcessed(inbound.ChannelType, inbound.MsgId);

            // 2) 事件类消息（subscribe/enter_agent 等）暂不进 AI；后续可扩展为自动欢迎语
            if (inbound.MessageType == ImMessageTypes.Event)
            {
                XTrace.Log.Info("[IM网关] 收到事件消息 Channel={0} Content={1}（暂不进 AI）", inbound.ChannelType, inbound.Content);
                return;
            }

            if (string.IsNullOrWhiteSpace(inbound.Content))
            {
                XTrace.Log.Info("[IM网关] 空内容消息跳过 Channel={0}", inbound.ChannelType);
                return;
            }

            // 3) 取通道绑定的 Agent/模型（v2.0.0 起仅企微一通道）
            var wecomCfg = _configStore.Load().WeCom;
            var boundAgent = wecomCfg.BoundAgentId;
            var boundModel = wecomCfg.BoundChatModelId;

            // 4) 会话映射
            var (sessionId, agentId, modelId) = _sessionStore.GetOrCreateSession(
                inbound.ChannelType, inbound.ConversationId, inbound.PlatformUserId, boundAgent, boundModel);

            // 5) 调 AI（跨插件契约，AIAgent 提供实现）
            var chat = _ctx.Get<IChatCompletion>();
            if (chat == null)
            {
                XTrace.Log.Error("[IM网关] 未找到 IChatCompletion 契约（AIAgent 插件是否已加载？）");
                return;
            }
            var result = await chat.CompleteAsync(new ChatCompletionRequest
            {
                SessionId = sessionId,
                Message = inbound.Content,
                ChatModelId = modelId,
                AgentId = agentId
            }, cancellationToken);

            if (!result.Success || string.IsNullOrWhiteSpace(result.Content))
            {
                XTrace.Log.Error("[IM网关] AI 补全失败 Channel={0} Session={1} Err={2}", inbound.ChannelType, sessionId, result.Error);
                return;
            }

            // 6) 经通道适配器回发
            if (_channels.TryGetValue(inbound.ChannelType, out var channel))
            {
                await channel.SendAsync(new OutboundMessage
                {
                    ChannelType = inbound.ChannelType,
                    ConversationId = inbound.ConversationId,
                    PlatformUserId = inbound.PlatformUserId,
                    Content = result.Content,
                    // 长连接通道靠 ReplyReqId 把回复关联回那次提问（透传入站帧的 req_id）；
                    // ChatType 决定主动推送时 chatid 按单聊还是群聊解析。回调型通道两字段为空，无副作用。
                    ReplyReqId = inbound.ReplyReqId,
                    ChatType = inbound.ChatType,
                }, cancellationToken);
                XTrace.Log.Info("[IM网关] 已回发 Channel={0} 会话={1}", inbound.ChannelType, inbound.ConversationId);
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[IM网关] 处理入站消息异常 Channel={0}: {1}", inbound.ChannelType, ex.Message);
        }
    }
}
