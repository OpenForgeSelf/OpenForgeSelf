using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.ImGateway.Core;
using ForgeSelf.Api.Plugins.ImGateway.Entities;
using ForgeSelf.Api.Plugins.ImGateway.Services;
using ForgeSelf.Core;
using FluentAssertions;
using Moq;
using Xunit;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Tests.Plugins.ImGatewayTests;

/// <summary>
/// ImGatewayRouter 落库链路（PILOT-078 AC-B2/AC-B3）单测：
/// - 成功链路：scripted IChatCompletion 成功 → in + out(ok) 各 1 条，Content/SessionId/PlatformMsgId 正确，回发照常；
/// - 失败链路：AI 失败 → in + out(error, ErrorText=错误摘要)，不回发，不抛；
/// - 主链不受落库影响：消息表整表不可用时（DB 层失败），ProcessInboundAsync 照常完成、回发照常（网关可用性优先）。
/// AI 用 ScriptedProvider 模式桩接（参考 ReactLoopAgentTests）；通道用内存 Fake，零真实网络。
/// 库管理：XCodeTestFixture（PC-014 唯一入口）+ [Collection("XCode")] 全进程串行。
/// </summary>
[Collection("XCode")]
public class ImGatewayRouterRecordTests : IClassFixture<XCodeTestFixture>
{
    private readonly XCodeTestFixture _fixture;

    public ImGatewayRouterRecordTests(XCodeTestFixture fixture)
    {
        _fixture = fixture;

        ImGatewayMessage.Meta.Cache.Clear("test reset");
        ImGatewayMessage.Meta.Cache.Expire = 0;
        ImGatewaySession.Meta.Cache.Clear("test reset");
        ImGatewaySession.Meta.Cache.Expire = 0;

        Purge();
    }

    private static void Purge()
    {
        foreach (var row in ImGatewayMessage.FindAll(ImGatewayMessage._.Id > 0)) row.Delete();
        foreach (var row in ImGatewaySession.FindAll(ImGatewaySession._.Id > 0)) row.Delete();
        foreach (var row in ImGatewayProcessedMsg.FindAll(ImGatewayProcessedMsg._.Id > 0)) row.Delete();
    }

    /// <summary>脚本化 IChatCompletion：按脚本返回结果，记录收到的请求（ScriptedProvider 模式）。</summary>
    private sealed class ScriptedChat : IChatCompletion
    {
        private readonly Func<ChatCompletionRequest, ChatCompletionResult> _script;

        public ScriptedChat(Func<ChatCompletionRequest, ChatCompletionResult> script) => _script = script;

        public List<ChatCompletionRequest> Requests { get; } = new();

        public Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(_script(request));
        }
    }

    /// <summary>内存通道桩：记录回发消息，不产生任何真实外连。</summary>
    private sealed class FakeChannel : IImChannel
    {
        public string ChannelType => ImChannelTypes.WeCom;

        public string DisplayName => "企微（测试桩）";

        public bool IsEnabled => true;

        public List<OutboundMessage> Sent { get; } = new();

        public Task StartAsync(Func<InboundMessage, CancellationToken, Task> onMessage, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendAsync(OutboundMessage message, CancellationToken cancellationToken = default)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private static ImGatewayRouter BuildRouter(IChatCompletion chat, out FakeChannel channel,
        string? boundAgent = "agent-im", string? boundModel = "prov-x:model-y")
    {
        channel = new FakeChannel();
        var ctx = new Mock<IContext>();
        ctx.Setup(c => c.Get<IChatCompletion>()).Returns(chat);
        var config = new Mock<IConfigStore>();
        config.Setup(c => c.Load()).Returns(new ImGatewayConfig
        {
            WeCom = new WeComConfig { Enabled = true, BoundAgentId = boundAgent, BoundChatModelId = boundModel },
        });
        return new ImGatewayRouter(ctx.Object, config.Object, new DbSessionStore(), new ImGatewayMessageStore(), new IImChannel[] { channel });
    }

    private static InboundMessage MakeInbound(string conversation, string user, string content, string msgId) => new()
    {
        ChannelType = ImChannelTypes.WeCom,
        ConversationId = conversation,
        PlatformUserId = user,
        MessageType = ImMessageTypes.Text,
        Content = content,
        MsgId = msgId,
        ReplyReqId = "req-e2e-9",
        ChatType = "single",
    };

    [Fact]
    public async Task 成功链路_in与out_ok各一条_字段与回发正确()
    {
        Purge();
        var scripted = new ScriptedChat(req => new ChatCompletionResult
        {
            SessionId = req.SessionId,
            Content = "这是AI的回复",
            Success = true,
        });
        var router = BuildRouter(scripted, out var channel);
        var key = ImChannelKeys.Build("wecom", "conv-ok", "user-ok");
        var inbound = MakeInbound("conv-ok", "user-ok", "用户提问", $"msg-ok-{Guid.NewGuid():N}");

        await router.ProcessInboundAsync(inbound);

        // 主链：AI 收到的请求与会话映射一致
        var req = scripted.Requests.Single();
        req.Message.Should().Be("用户提问");
        req.AgentId.Should().Be("agent-im", "绑定 AgentId 透传给 AI");
        req.ChatModelId.Should().Be("prov-x:model-y", "绑定模型透传给 AI");
        req.SessionId.Should().NotBeNullOrEmpty();

        var session = ImGatewaySession.Find(ImGatewaySession._.ChannelKey == key);
        session.Should().NotBeNull("会话映射应已建立");
        req.SessionId.Should().Be(session!.SessionId, "AI 请求会话 = 会话映射返回值");

        // 回发主链：通道桩收到回复，ReplyReqId 透传
        var sent = channel.Sent.Single();
        sent.Content.Should().Be("这是AI的回复");
        sent.ReplyReqId.Should().Be("req-e2e-9");

        // 落库（AC-B2）：in + out(ok) 各 1 条
        var rows = ImGatewayMessage.FindAll(ImGatewayMessage._.ChannelKey == key).ToList();
        rows.Should().HaveCount(2, "成功链路应各落一条收与回");
        var inRow = rows.Single(r => r.Direction == ImGatewayMessage.DirectionIn);
        inRow.Content.Should().Be("用户提问");
        inRow.PlatformMsgId.Should().Be(inbound.MsgId);
        inRow.Status.Should().Be(ImGatewayMessage.StatusOk);
        inRow.SessionId.Should().Be(session.SessionId);
        inRow.ChannelType.Should().Be("wecom");

        var outRow = rows.Single(r => r.Direction == ImGatewayMessage.DirectionOut);
        outRow.Content.Should().Be("这是AI的回复");
        outRow.Status.Should().Be(ImGatewayMessage.StatusOk);
        outRow.ErrorText.Should().BeNull();
        outRow.SessionId.Should().Be(session.SessionId);
    }

    [Fact]
    public async Task 失败链路_in加out_error_ErrorText为摘要_不回发()
    {
        Purge();
        var scripted = new ScriptedChat(_ => new ChatCompletionResult
        {
            SessionId = "ignored",
            Content = string.Empty,
            Success = false,
            Error = "Service request failed. Status: 404 (Not Found)",
        });
        var router = BuildRouter(scripted, out var channel);
        var key = ImChannelKeys.Build("wecom", "conv-err", "user-err");
        var inbound = MakeInbound("conv-err", "user-err", "会失败的消息", $"msg-err-{Guid.NewGuid():N}");

        Func<Task> act = async () => await router.ProcessInboundAsync(inbound);
        await act.Should().NotThrowAsync("AI 失败不得把异常抛回通道回调（主链吞异常）");

        channel.Sent.Should().BeEmpty("AI 失败不得回发");

        // 落库（AC-B3）：in + out(error, ErrorText=摘要)
        var rows = ImGatewayMessage.FindAll(ImGatewayMessage._.ChannelKey == key).ToList();
        rows.Should().HaveCount(2, "失败链路应落一条收与一条失败回复");
        rows.Should().Contain(r => r.Direction == ImGatewayMessage.DirectionIn
                                   && r.Status == ImGatewayMessage.StatusOk
                                   && r.Content == "会失败的消息");
        var errRow = rows.Single(r => r.Direction == ImGatewayMessage.DirectionOut);
        errRow.Status.Should().Be(ImGatewayMessage.StatusError);
        errRow.ErrorText.Should().Be("Service request failed. Status: 404 (Not Found)", "ErrorText = AI 失败摘要");
        errRow.Content.Should().BeEmpty();
        errRow.SessionId.Should().NotBeNullOrEmpty("失败记录同样携带会话映射");
    }

    [Fact]
    public async Task 落库失败不影响主链_回发照常完成()
    {
        Purge();
        var scripted = new ScriptedChat(req => new ChatCompletionResult
        {
            SessionId = req.SessionId,
            Content = "落库失败时的回复",
            Success = true,
        });
        var router = BuildRouter(scripted, out var channel);
        var key = ImChannelKeys.Build("wecom", "conv-drop", "user-drop");
        var inbound = MakeInbound("conv-drop", "user-drop", "用户提问", $"msg-drop-{Guid.NewGuid():N}");

        // 制造消息表整表不可用（DB 层失败，不碰连接串——PC-014）
        var dal = DAL.Create("ImGateway");
        if (dal.Tables.Any(t => string.Equals(t.Name, "ImGatewayMessage", StringComparison.OrdinalIgnoreCase)))
        {
            dal.Execute("DROP TABLE ImGatewayMessage");
        }

        try
        {
            Func<Task> act = async () => await router.ProcessInboundAsync(inbound);
            await act.Should().NotThrowAsync("落库失败绝不影响网关主链");

            var sent = channel.Sent.Single();
            sent.Content.Should().Be("落库失败时的回复", "回发主链照常");
            sent.ReplyReqId.Should().Be("req-e2e-9");
        }
        finally
        {
            // 恢复：SetTables 幂等重建缺失表（XCodeTestFixture 首绑同款入口）
            XCodeTestFixture.EnsureTablesCreated();
        }

        ImGatewayMessage.FindAll(ImGatewayMessage._.ChannelKey == key).Should().BeEmpty("落库全程静默失败，不得有半截数据");
    }

    [Fact]
    public async Task 事件与空内容消息不入消息表()
    {
        Purge();
        var scripted = new ScriptedChat(_ => new ChatCompletionResult { SessionId = "x", Content = "不应被调用", Success = true });
        var router = BuildRouter(scripted, out var channel);

        var eventInbound = MakeInbound("conv-br2", "user-br2", "subscribe", $"msg-ev-{Guid.NewGuid():N}");
        eventInbound.MessageType = ImMessageTypes.Event;
        await router.ProcessInboundAsync(eventInbound);

        var emptyInbound = MakeInbound("conv-br2", "user-br2", "   ", $"msg-empty-{Guid.NewGuid():N}");
        await router.ProcessInboundAsync(emptyInbound);

        scripted.Requests.Should().BeEmpty("事件/空内容消息不进 AI");
        channel.Sent.Should().BeEmpty();
        var key = ImChannelKeys.Build("wecom", "conv-br2", "user-br2");
        ImGatewayMessage.FindAll(ImGatewayMessage._.ChannelKey == key).Should().BeEmpty("业务规则 2：事件与空内容不入消息表");
    }
}
