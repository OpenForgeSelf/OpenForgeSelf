using System.Text.Json;
using FluentAssertions;
using ForgeSelf.Api.Plugins.ImGateway.Core;
using ForgeSelf.Api.Plugins.ImGateway.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.ImGateway;

/// <summary>
/// 企业微信长连接协议层单测：锁定「收帧解析」与「发帧构造」两侧的官方契约。
/// 全部使用占位符，禁止出现真实 botId/secret（PII 红线）。
/// 为什么必须锁：官方文档输出格式可能变化，且长连接回复有个反直觉约定——
/// 普通文本回复也必须用 msgtype=stream（不是 text），写错了消息能发出去但客户端不显示。
/// </summary>
public class WeComFrameParserTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void 解析群聊文本消息_剥离机器人at前缀并透传reqId()
    {
        // 群聊里 @机器人 时官方正文带 "@机器人名 " 前缀，必须剥离后再交给 AI
        const string frame = """
        {
          "cmd": "aibot_msg_callback",
          "headers": { "req_id": "req_placeholder_001" },
          "body": {
            "msgid": "msg_placeholder_001",
            "aibotid": "aibot_placeholder",
            "chatid": "chat_placeholder_group",
            "chattype": "group",
            "from": { "userid": "user_placeholder_001" },
            "msgtype": "text",
            "text": { "content": "@机器人助手 今天天气如何" }
          }
        }
        """;

        var msg = WeComFrameParser.ParseMessage(Parse(frame), frame);

        msg.Should().NotBeNull();
        msg!.Content.Should().Be("今天天气如何", "群聊 @前缀必须剥离，否则 AI 每轮都看到无意义 @");
        msg.ReplyReqId.Should().Be("req_placeholder_001", "req_id 必须透传，回复才能关联到这次提问");
        msg.ChatType.Should().Be("group");
        msg.ConversationId.Should().Be("chat_placeholder_group", "群聊会话标识取 chatid");
        msg.PlatformUserId.Should().Be("user_placeholder_001");
        msg.MsgId.Should().Be("msg_placeholder_001");
        msg.ChannelType.Should().Be(ImChannelTypes.WeCom);
    }

    [Fact]
    public void 解析单聊消息_无chatid时会话标识回落到用户标识()
    {
        const string frame = """
        {
          "cmd": "aibot_msg_callback",
          "headers": { "req_id": "req_placeholder_002" },
          "body": {
            "msgid": "msg_placeholder_002",
            "chattype": "single",
            "from": { "userid": "user_placeholder_002" },
            "msgtype": "text",
            "text": { "content": "你好" }
          }
        }
        """;

        var msg = WeComFrameParser.ParseMessage(Parse(frame), frame);

        msg.Should().NotBeNull();
        msg!.ConversationId.Should().Be("user_placeholder_002", "单聊无 chatid，会话标识应回落到 userid");
        msg.ChatType.Should().Be("single");
        msg.Content.Should().Be("你好", "单聊正文不得被 @剥离逻辑误伤");
    }

    [Fact]
    public void 解析非文本消息_降级为占位说明而不臆造内容()
    {
        const string frame = """
        {
          "cmd": "aibot_msg_callback",
          "headers": { "req_id": "req_placeholder_003" },
          "body": {
            "msgid": "msg_placeholder_003",
            "chattype": "single",
            "from": { "userid": "user_placeholder_003" },
            "msgtype": "image",
            "image": { "url": "https://example.com/x.png", "aeskey": "placeholder_aeskey" }
          }
        }
        """;

        var msg = WeComFrameParser.ParseMessage(Parse(frame), frame);

        msg.Should().NotBeNull();
        msg!.MessageType.Should().Be("image");
        msg.Content.Should().StartWith("[image", "未支持类型应给出可读占位，不得塞空串冒充已处理");
    }

    [Fact]
    public void 解析事件帧_识别被踢下线事件()
    {
        // disconnected_event 表示本连接被同机器人的新实例抢占，必须停止重连避免互踢
        const string frame = """
        {
          "cmd": "aibot_event_callback",
          "headers": { "req_id": "req_placeholder_004" },
          "body": {
            "msgid": "msg_placeholder_004",
            "msgtype": "event",
            "event": { "eventtype": "disconnected_event" }
          }
        }
        """;

        WeComFrameParser.ParseEventType(Parse(frame)).Should().Be("disconnected_event");
    }

    [Fact]
    public void 构造回复帧_长连接必须用stream类型且透传reqId()
    {
        var json = WeComFrameParser.BuildRespondFrame("req_placeholder_005", "收到：你好");
        var root = Parse(json);

        root.GetProperty("cmd").GetString().Should().Be("aibot_respond_msg");
        root.GetProperty("headers").GetProperty("req_id").GetString()
            .Should().Be("req_placeholder_005", "回复必须透传回调帧的 req_id");

        var body = root.GetProperty("body");
        // 关键契约：长连接下普通文本回复也必须是 msgtype=stream，用 text 客户端不显示
        body.GetProperty("msgtype").GetString().Should().Be("stream");
        var stream = body.GetProperty("stream");
        stream.GetProperty("finish").GetBoolean().Should().BeTrue("一次性完整回复应置 finish=true 结束流式");
        stream.GetProperty("content").GetString().Should().Be("收到：你好");
        stream.GetProperty("id").GetString().Should().NotBeNullOrEmpty("流式消息必须有 stream id");
    }

    [Fact]
    public void 构造订阅帧_字段名与官方一致()
    {
        var json = WeComFrameParser.BuildSubscribeFrame("bot_placeholder", "secret_placeholder");
        var root = Parse(json);

        root.GetProperty("cmd").GetString().Should().Be("aibot_subscribe");
        root.GetProperty("body").GetProperty("bot_id").GetString().Should().Be("bot_placeholder");
        root.GetProperty("body").GetProperty("secret").GetString().Should().Be("secret_placeholder");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void 构造推送帧_chatType按单聊群聊区分(int chatType)
    {
        // chatType 决定服务端如何解析 chatid：1=单聊传 userid，2=群聊传 chatid，传错单聊发不出去
        var json = WeComFrameParser.BuildSendFrame("target_placeholder", chatType, "定时提醒");
        var root = Parse(json);

        root.GetProperty("cmd").GetString().Should().Be("aibot_send_msg");
        root.GetProperty("body").GetProperty("chattype").GetInt32().Should().Be(chatType);
        root.GetProperty("body").GetProperty("chatid").GetString().Should().Be("target_placeholder");
    }

    [Fact]
    public void 构造心跳帧_命令为ping()
    {
        var root = Parse(WeComFrameParser.BuildPingFrame());
        root.GetProperty("cmd").GetString().Should().Be("ping", "官方心跳为 ping 命令帧");
    }
}
