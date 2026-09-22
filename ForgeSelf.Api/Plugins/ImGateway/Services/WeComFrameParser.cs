using System.Text.Json;
using ForgeSelf.Api.Plugins.ImGateway.Core;

namespace ForgeSelf.Api.Plugins.ImGateway.Services;

/// <summary>
/// 企业微信长连接帧解析层（纯函数，无 IO，便于单测锁定协议契约）。
/// 单独成类的原因：官方文档的输出格式可能变化（PRD 已列为中风险），
/// 把解析集中在一处并对关键字段写单测，变化时能在单测里立刻暴露，而不是等到线上收不到消息。
/// </summary>
public static class WeComFrameParser
{
    /// <summary>发送帧序列化约定：camelCase，与官方文档示例字段名一致（bot_id/chatid/chattype/msgtype...）。</summary>
    private static readonly JsonSerializerOptions FrameOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// 构造被动回复帧 aibot_respond_msg。
    /// 关键契约（易错点）：长连接下普通文本回复也必须用 msgtype=stream（官方 Node SDK 对齐），
    /// 不是 msgtype=text；req_id 必须透传回调帧的值；finish=true 表示流式结束（一次性回复直接置 true）。
    /// </summary>
    public static string BuildRespondFrame(string reqId, string content, string? streamId = null, bool finish = true)
    {
        var frame = new
        {
            cmd = "aibot_respond_msg",
            headers = new { req_id = reqId },
            body = new
            {
                msgtype = "stream",
                stream = new
                {
                    id = streamId ?? WeComAiBotClient.NewReqId("stream"),
                    finish,
                    content,
                },
            },
        };
        return JsonSerializer.Serialize(frame, FrameOpts);
    }

    /// <summary>
    /// 构造主动推送帧 aibot_send_msg（无回调触发时使用）。
    /// chatType：1=单聊（chatId 传 userid），2=群聊（chatId 传 chatid）；
    /// 缺省服务端按群聊解析，单聊会发不出去，故此处为必填形参。
    /// </summary>
    public static string BuildSendFrame(string chatId, int chatType, string content, string? streamId = null)
    {
        var frame = new
        {
            cmd = "aibot_send_msg",
            headers = new { req_id = WeComAiBotClient.NewReqId("send") },
            body = new
            {
                chatid = chatId,
                chattype = chatType,
                msgtype = "stream",
                stream = new
                {
                    id = streamId ?? WeComAiBotClient.NewReqId("stream"),
                    finish = true,
                    content,
                },
            },
        };
        return JsonSerializer.Serialize(frame, FrameOpts);
    }

    /// <summary>构造心跳帧 ping（官方：需定期发送 ping 保活，建议 30s）。</summary>
    public static string BuildPingFrame()
    {
        var frame = new { cmd = "ping", headers = new { req_id = WeComAiBotClient.NewReqId("ping") } };
        return JsonSerializer.Serialize(frame, FrameOpts);
    }

    /// <summary>构造订阅帧 aibot_subscribe（连接建立后的身份校验）。</summary>
    public static string BuildSubscribeFrame(string botId, string secret)
    {
        var frame = new
        {
            cmd = "aibot_subscribe",
            headers = new { req_id = WeComAiBotClient.NewReqId("sub") },
            body = new { bot_id = botId, secret },
        };
        return JsonSerializer.Serialize(frame, FrameOpts);
    }
    /// <summary>
    /// 解析 aibot_msg_callback 帧为统一入站消息。非消息帧/结构异常返回 null。
    /// </summary>
    public static InboundMessage? ParseMessage(JsonElement root, string rawText)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;
        if (!root.TryGetProperty("body", out var body) || body.ValueKind != JsonValueKind.Object) return null;

        // req_id 必须原样透传给回复帧，服务端据此把回复关联到这次提问
        string? reqId = null;
        if (root.TryGetProperty("headers", out var headers) && headers.ValueKind == JsonValueKind.Object
            && headers.TryGetProperty("req_id", out var rid))
        {
            reqId = rid.GetString();
        }

        var msgType = body.TryGetProperty("msgtype", out var mt) ? mt.GetString() ?? "text" : "text";
        var chatType = body.TryGetProperty("chattype", out var ctp) ? ctp.GetString() : null;
        var chatId = body.TryGetProperty("chatid", out var cid) ? cid.GetString() : null;
        var msgId = body.TryGetProperty("msgid", out var mid) ? mid.GetString() : null;
        var userId = body.TryGetProperty("from", out var f) && f.ValueKind == JsonValueKind.Object
            && f.TryGetProperty("userid", out var u) ? u.GetString() ?? string.Empty : string.Empty;

        var content = ExtractContent(body, msgType);

        // 群聊里 @机器人 时正文带 "@机器人名 " 前缀：必须剥离后再交给 AI，
        // 否则每轮提示词都带一个无意义 @ 前缀，影响语义。仅群聊剥离，单聊原样保留。
        if (string.Equals(chatType, "group", StringComparison.OrdinalIgnoreCase) && content.StartsWith('@'))
        {
            var sp = content.IndexOf(' ');
            if (sp > 0) content = content[(sp + 1)..].TrimStart();
        }

        return new InboundMessage
        {
            ChannelType = ImChannelTypes.WeCom,
            PlatformUserId = userId,
            ConversationId = !string.IsNullOrEmpty(chatId) ? chatId! : userId,
            MessageType = msgType == "event" ? ImMessageTypes.Event : msgType,
            Content = content,
            MsgId = msgId,
            RawPayload = rawText,
            ReplyReqId = reqId,
            ChatType = chatType,
        };
    }

    /// <summary>提取正文：仅文本/语音（已转文本）可直取；其它类型降级为占位说明，不臆造解析。</summary>
    private static string ExtractContent(JsonElement body, string msgType)
    {
        switch (msgType)
        {
            case "text":
                return body.TryGetProperty("text", out var t) && t.TryGetProperty("content", out var tc)
                    ? tc.GetString() ?? string.Empty : string.Empty;

            case "voice":
                // 语音消息平台已转为文本
                return body.TryGetProperty("voice", out var v) && v.TryGetProperty("content", out var vc)
                    ? vc.GetString() ?? string.Empty : string.Empty;

            default:
                return $"[{msgType} 消息，当前版本暂不处理]";
        }
    }

    /// <summary>取事件类型（enter_chat / template_card_event / feedback_event / disconnected_event）。</summary>
    public static string? ParseEventType(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;
        if (!root.TryGetProperty("body", out var body) || body.ValueKind != JsonValueKind.Object) return null;
        if (!body.TryGetProperty("event", out var ev) || ev.ValueKind != JsonValueKind.Object) return null;
        return ev.TryGetProperty("eventtype", out var et) ? et.GetString() : null;
    }
}
