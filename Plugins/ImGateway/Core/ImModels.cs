using System.Text.Json.Serialization;

namespace ForgeSelf.Api.Plugins.ImGateway.Core;

/// <summary>
/// 统一入站消息（企微长连接推送 → 网关内部标准格式）。
/// 平台差异（JSON 帧结构、字段名）在通道适配器内被消化，网关内核只认这个。
/// </summary>
public class InboundMessage
{
    /// <summary>来源通道类型（当前仅 wecom）。</summary>
    public string ChannelType { get; set; } = string.Empty;

    /// <summary>平台用户标识（企业微信 userId / 群聊中发送者 userid）。</summary>
    public string PlatformUserId { get; set; } = string.Empty;

    /// <summary>会话标识（单聊=用户标识；群聊=chatId）。回发时据此定位目标。</summary>
    public string ConversationId { get; set; } = string.Empty;

    /// <summary>消息类型（text / image / event / voice ...）。</summary>
    public string MessageType { get; set; } = "text";

    /// <summary>文本正文；非文本消息为可读性说明（如「[图片]」）。</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>平台侧消息唯一 id（用于去重，防止长连接重复推送触发重复处理）。</summary>
    public string? MsgId { get; set; }

    /// <summary>原始平台载荷（调试/透传用）。</summary>
    public string? RawPayload { get; set; }

    /// <summary>
    /// 回复凭证：长连接通道下平台回调帧携带的 req_id，回复时必须原样透传，
    /// 服务端据此把回复关联到对应回调。
    /// </summary>
    public string? ReplyReqId { get; set; }

    /// <summary>会话形态：single=单聊 / group=群聊。长连接主动推送时决定 chatid 如何解析。</summary>
    public string? ChatType { get; set; }
}

/// <summary>
/// 统一出站消息（网关内部 → 平台发送）。
/// </summary>
public class OutboundMessage
{
    public string ChannelType { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string PlatformUserId { get; set; } = string.Empty;
    public string MessageType { get; set; } = "text";
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 回复凭证：透传入站消息的 req_id。非空=被动回复（aibot_respond_msg）；
    /// 为空=主动推送（aibot_send_msg）。长连接通道据此选择发送命令。
    /// </summary>
    public string? ReplyReqId { get; set; }

    /// <summary>会话形态：single=单聊 / group=群聊。主动推送必填，否则服务端默认按群聊解析导致单聊发不出去。</summary>
    public string? ChatType { get; set; }
}

/// <summary>
/// 通道传输模式。v2.0.0 起仅 WebSocket（长连接，客户端主动外连，无需公网 IP）。
/// </summary>
public enum ImTransportMode
{
    WebSocket = 0,
}

/// <summary>长连接通道的连接状态（供配置页展示，不做过度细分）。</summary>
public enum ImConnectionState
{
    /// <summary>未连接（未启用 / 凭据不全 / 已停止）。</summary>
    Disconnected = 0,

    /// <summary>正在建连或订阅中。</summary>
    Connecting = 1,

    /// <summary>已订阅成功，可收发。</summary>
    Connected = 2,

    /// <summary>连接失败（含凭据错误），将按退避重试。</summary>
    Failed = 3,

    /// <summary>被同机器人的新连接踢下线（disconnected_event），已停止重连避免互踢。</summary>
    Kicked = 4,
}

/// <summary>长连接通道对外暴露的状态快照。</summary>
public class ImConnectionStatus
{
    public ImConnectionState State { get; set; } = ImConnectionState.Disconnected;

    /// <summary>状态补充说明（失败原因、踢线提示等），可空。</summary>
    public string? Message { get; set; }

    /// <summary>最近一次成功订阅的时间（本地），未成功过则为空。</summary>
    public DateTime? ConnectedAt { get; set; }
}

/// <summary>消息类型常量。</summary>
public static class ImMessageTypes
{
    public const string Text = "text";
    public const string Image = "image";
    public const string Event = "event";
    public const string Voice = "voice";
}

/// <summary>各通道类型常量（v2.0.0 起仅企微长连接一种）。</summary>
public static class ImChannelTypes
{
    public const string WeCom = "wecom";
}
