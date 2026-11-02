using ForgeSelf.Api.Plugins.ImGateway.Entities;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.ImGateway.Services;

/// <summary>
/// 消息记录存储（PILOT-078）：把网关收到/回复的消息尽力落库，供「已绑定 IM 会话列表」查询。
/// 铁律：落库为<b>尽力而为</b>——任何异常仅 XTrace 记录，绝不向网关主链抛出（网关可用性优先）。
/// 事件类消息与空内容消息不入表（与 Router 不进 AI 的边界一致，由调用点保证）。
/// </summary>
public class ImGatewayMessageStore
{
    /// <summary>记入站消息（direction=in, status=ok）。</summary>
    public void RecordInbound(string channelKey, string channelType, string content, string? platformMsgId, string? sessionId)
    {
        Record(new ImGatewayMessage
        {
            ChannelKey = channelKey,
            ChannelType = channelType,
            Direction = ImGatewayMessage.DirectionIn,
            Content = content ?? string.Empty,
            PlatformMsgId = platformMsgId,
            SessionId = sessionId,
            Status = ImGatewayMessage.StatusOk,
            CreatedAt = DateTime.Now,
        });
    }

    /// <summary>记回复消息（direction=out, status=ok），回发成功后调用。</summary>
    public void RecordReply(string channelKey, string channelType, string content, string? sessionId)
    {
        Record(new ImGatewayMessage
        {
            ChannelKey = channelKey,
            ChannelType = channelType,
            Direction = ImGatewayMessage.DirectionOut,
            Content = content ?? string.Empty,
            SessionId = sessionId,
            Status = ImGatewayMessage.StatusOk,
            CreatedAt = DateTime.Now,
        });
    }

    /// <summary>记回复失败（direction=out, status=error, ErrorText=错误摘要截断 1000），AI 补全失败后调用。</summary>
    public void RecordError(string channelKey, string channelType, string? sessionId, string? errorText)
    {
        Record(new ImGatewayMessage
        {
            ChannelKey = channelKey,
            ChannelType = channelType,
            Direction = ImGatewayMessage.DirectionOut,
            Content = string.Empty,
            SessionId = sessionId,
            Status = ImGatewayMessage.StatusError,
            ErrorText = Truncate(errorText, 1000),
            CreatedAt = DateTime.Now,
        });
    }

    /// <summary>统一落库入口：全 try/catch，失败仅记 XTrace（含异常全文），绝不抛。</summary>
    private void Record(ImGatewayMessage msg)
    {
        try
        {
            msg.Save();
            XTrace.Log.Debug("[IM网关] 消息已落库 Channel={0} Direction={1} Status={2} Id={3}",
                msg.ChannelKey, msg.Direction, msg.Status, msg.Id);
        }
        catch (Exception ex)
        {
            // 网关主链不受影响：记录失败只影响回放展示，不影响收发
            XTrace.Log.Error("[IM网关] 消息落库失败（不影响网关主链） Channel={0} Direction={1}: {2}",
                msg.ChannelKey, msg.Direction, ex.Message);
        }
    }

    /// <summary>截断到指定长度（ErrorText 列上限 1000），null 安全。</summary>
    private static string Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length <= maxLength ? text : text[..maxLength];
    }
}
