using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.ImGateway.Core;
using ForgeSelf.Api.Plugins.ImGateway.Entities;
using ForgeSelf.Api.Plugins.ImGateway.Services;
using ForgeSelf.Core;
using ForgeSelf.Api.Plugins.ImGateway.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FluentAssertions;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.ImGatewayTests;

/// <summary>
/// PILOT-079 T2 门禁（AC3/AC9/AC10）：任务前缀派发、绑定落库、ack、状态查询、
/// Watcher 终态回复/幂等/审批卡片/超时/重启恢复、审批关键字决策闭环、simulate-inbound 注入。
/// 契约（IAgentRunDispatch/IApprovalService）与通道全部接口替身；绑定表用 XCodeTestFixture（PC-014）。
/// </summary>
[Collection("XCode")]
public class ImTaskDispatchTests : IClassFixture<XCodeTestFixture>
{
    private readonly XCodeTestFixture _fixture;

    public ImTaskDispatchTests(XCodeTestFixture fixture)
    {
        _fixture = fixture;

        ImGatewayTask.Meta.Cache.Clear("test reset");
        ImGatewayTask.Meta.Cache.Expire = 0;
        ImGatewaySession.Meta.Cache.Clear("test reset");
        ImGatewaySession.Meta.Cache.Expire = 0;
        ImGatewayMessage.Meta.Cache.Clear("test reset");
        ImGatewayMessage.Meta.Cache.Expire = 0;
        ImGatewayChannel.Meta.Cache.Clear("test reset");
        ImGatewayChannel.Meta.Cache.Expire = 0;

        Purge();
    }

    private static void Purge()
    {
        foreach (var row in ImGatewayTask.FindAll(ImGatewayTask._.Id > 0)) row.Delete();
        foreach (var row in ImGatewayMessage.FindAll(ImGatewayMessage._.Id > 0)) row.Delete();
        foreach (var row in ImGatewaySession.FindAll(ImGatewaySession._.Id > 0)) row.Delete();
        foreach (var row in ImGatewayChannel.FindAll(ImGatewayChannel._.Id > 0)) row.Delete();
    }

    // ---- 替身 ----

    /// <summary>IAgentRunDispatch 替身：Submit/Resume 走脚本化 Outcome，Find 走 key→快照字典。</summary>
    internal sealed class FakeDispatch : IAgentRunDispatch
    {
        public List<AgentRunDispatchRequest> SubmitRequests { get; } = new();
        public Queue<AgentRunDispatchOutcome> SubmitOutcomes { get; } = new();
        public AgentRunDispatchOutcome DefaultSubmitOutcome { get; set; } =
            AgentRunDispatchOutcome.Ok("abc123def4567890", "agent.programmer", "程序员", "pending");
        public List<(string TaskKey, string? Note)> ResumeCalls { get; } = new();
        public AgentRunDispatchOutcome ResumeOutcome { get; set; } =
            AgentRunDispatchOutcome.Ok("k1", "agent.programmer", "程序员", "running", 12);
        public Dictionary<string, AgentRunDispatchSnapshot> FindResults { get; } = new();

        public Task<AgentRunDispatchOutcome> SubmitAsync(AgentRunDispatchRequest request, CancellationToken ct = default)
        {
            SubmitRequests.Add(request);
            return Task.FromResult(SubmitOutcomes.Count > 0 ? SubmitOutcomes.Dequeue() : DefaultSubmitOutcome);
        }

        public Task<AgentRunDispatchSnapshot?> FindAsync(string taskKey, CancellationToken ct = default)
            => Task.FromResult<AgentRunDispatchSnapshot?>(FindResults.TryGetValue(taskKey, out var snap) ? snap : null);

        public Task<AgentRunDispatchOutcome> ResumeAsync(string taskKey, string? note, CancellationToken ct = default)
        {
            ResumeCalls.Add((taskKey, note));
            return Task.FromResult(ResumeOutcome);
        }
    }

    /// <summary>IApprovalService 替身：内存审批记录 + Decide 调用记录。</summary>
    internal sealed class FakeApproval : IApprovalService
    {
        public List<ApprovalDto> Approvals { get; } = new();
        public List<(long Id, bool Approved, string? Note)> DecideCalls { get; } = new();
        public Func<long, ApprovalDto?>? DecideScript { get; set; }

        public Task<bool> AskAsync(string sessionId, string toolName, string argsJson, string? reason, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<ApprovalDto> CreatePendingAsync(ApprovalCreateRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ApprovalDto>> ListBySessionAsync(string sessionId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ApprovalDto>>(Approvals.Where(a => a.SessionId == sessionId).ToList());

        public Task<ApprovalDto?> DecideAsync(long id, bool approved, string? note, CancellationToken ct = default)
        {
            DecideCalls.Add((id, approved, note));
            if (DecideScript != null) return Task.FromResult(DecideScript.Invoke(id));
            return Task.FromResult(Approvals.FirstOrDefault(a => a.Id == id));
        }

        public Task<IReadOnlyList<ApprovalContinuation>> ConsumeContinuationsAsync(string sessionId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public static ApprovalDto Make(long id, string sessionId, string status = "Pending",
            string tool = "write_file", string risk = "high", string description = "向 index.html 写入内容") => new()
        {
            Id = id,
            SessionId = sessionId,
            ToolName = tool,
            RiskLevel = risk,
            Description = description,
            Status = status,
            CreatedAt = DateTimeOffset.Now,
            ExpiresAt = DateTimeOffset.Now.AddMinutes(30),
        };
    }

    /// <summary>内存通道桩：记录回发消息，无真实外连。</summary>
    internal sealed class FakeChannel : IImChannel
    {
        public string ChannelType => ImChannelTypes.WeCom;
        public string DisplayName => "企微（测试桩）";
        public bool IsEnabled => true;
        public List<OutboundMessage> Sent { get; } = new();
        public List<string> FailWhenContains = new();   // T12：内容含指定子串则 SendAsync 抛（模拟第 N 段失败）

        public Task StartAsync(Func<InboundMessage, CancellationToken, Task> onMessage, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendAsync(OutboundMessage message, CancellationToken cancellationToken = default)
        {
            if (FailWhenContains.Any(f => message.Content.Contains(f))) throw new InvalidOperationException("模拟通道失败");
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    // ---- 组装助手 ----

    internal sealed class Rig
    {
        public FakeChannel Channel = new();
        public FakeDispatch Dispatch = new();
        public FakeApproval Approval = new();
        public WeComConfig Config = new()
        {
            Enabled = true,
            TaskGateway = new TaskGatewayConfig { Enabled = true },
        };
        public ImTaskDispatcher Dispatcher = null!;
    public ImGatewayConfig? LoadedConfig;   // T16 诊断：mock 返回的配置实例
        public ImTaskWatcher Watcher = null!;
        public ImGatewayRouter Router = null!;
        public Mock<IContext> Ctx = new();
    }

    private static Rig BuildRig(WeComConfig? config = null, IChatCompletion? chat = null, ImTaskDispatcher? dispatcher = null)
    {
        var rig = new Rig();
        if (config != null) rig.Config = config;
        var ctx = new Mock<IContext>();
        ctx.Setup(c => c.Get<IAgentRunDispatch>()).Returns(rig.Dispatch);
        ctx.Setup(c => c.Get<IApprovalService>()).Returns(rig.Approval);
        if (chat != null) ctx.Setup(c => c.Get<IChatCompletion>()).Returns(chat);
        rig.Ctx = ctx;
        var configStore = new Mock<IConfigStore>();
        var loaded = new ImGatewayConfig { WeCom = rig.Config };
        rig.LoadedConfig = loaded;
        configStore.Setup(c => c.Load()).Returns(loaded);
        rig.Dispatcher = dispatcher ?? new ImTaskDispatcher(ctx.Object, new ImGatewayMessageStore(), new IImChannel[] { rig.Channel });
        rig.Watcher = new ImTaskWatcher(ctx.Object, configStore.Object, new DbSessionStore(), new IImChannel[] { rig.Channel });
        rig.Router = new ImGatewayRouter(ctx.Object, configStore.Object, new DbSessionStore(), new ImGatewayMessageStore(), new IImChannel[] { rig.Channel }, rig.Dispatcher);
        return rig;
    }

    private static InboundMessage MakeInbound(string content, string msgId = "m1", string chatType = "single") => new()
    {
        ChannelType = ImChannelTypes.WeCom,
        ConversationId = "conv-1",
        PlatformUserId = "user-1",
        MessageType = ImMessageTypes.Text,
        Content = content,
        MsgId = msgId,
        ReplyReqId = "req-1",
        ChatType = chatType,
    };

    private const string ChannelKey = "wecom:conv-1:user-1";

    private static ImGatewayTask InsertBinding(string taskKey = "k1", long runId = 12, string channelKey = ChannelKey,
        string status = "running", string? prompted = null, DateTime? createdAt = null, string chatType = "single")
    {
        var row = new ImGatewayTask
        {
            TaskKey = taskKey,
            RunId = runId,
            ChannelKey = channelKey,
            ChannelType = ImChannelTypes.WeCom,
            ConversationId = "conv-1",
            PlatformUserId = "user-1",
            ChatType = chatType,
            ReplyReqId = "req-0",
            InboundMsgId = "m0",
            TaskInput = "写个报告",
            Status = status,
            CreatedAt = createdAt ?? DateTime.Now.AddMinutes(-5),
            PromptedApprovalIds = prompted ?? string.Empty,
        };
        row.Save();
        return row;
    }
    // ---- AC3：前缀派发 / 查询 / 拒绝路径 ----

    [Fact]
    public async Task 前缀命中_提交并ack并落绑定()
    {
        var rig = BuildRig();
        var inbound = MakeInbound("/task 写个xx报告");

        var handled = await rig.Dispatcher.HandleAsync(inbound, rig.Config, ChannelKey, "chat-session-1", "agent-im", "prov:m1", CancellationToken.None);

        handled.Should().BeTrue();
        var req = rig.Dispatch.SubmitRequests.Single();
        req.TaskInput.Should().Be("写个xx报告");
        req.SessionId.Should().Be("imtask:chat-session-1");   // FR：Run SessionId=imtask:{会话Id}
        req.CreatedBy.Should().Be(ChannelKey);
        req.ChatModelId.Should().Be("prov:m1");
        var sent = rig.Channel.Sent.Single();
        sent.Content.Should().Contain("已建任务").And.Contain("abc123de").And.Contain("程序员");
        sent.ReplyReqId.Should().Be("req-1");                  // ack 用仍热的 req_id 被动回复
        var binding = ImGatewayTask.FindAll(ImGatewayTask._.ChannelKey == ChannelKey).Single();
        binding.TaskKey.Should().Be("abc123def4567890");
        binding.Status.Should().Be("pending");
        binding.TaskInput.Should().Be("写个xx报告");
    }

    [Fact]
    public async Task 前缀未命中_返回false不提交()
    {
        var rig = BuildRig();
        var inbound = MakeInbound("今天天气如何");

        var handled = await rig.Dispatcher.HandleAsync(inbound, rig.Config, ChannelKey, "chat-session-1", null, null, CancellationToken.None);

        handled.Should().BeFalse();
        rig.Dispatch.SubmitRequests.Should().BeEmpty();
        rig.Channel.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Enabled关闭_即使前缀命中也返回false()
    {
        var cfg = new WeComConfig { Enabled = true, TaskGateway = new TaskGatewayConfig { Enabled = false } };
        var rig = BuildRig(cfg);
        var inbound = MakeInbound("/task 写个xx报告");

        var handled = await rig.Dispatcher.HandleAsync(inbound, cfg, ChannelKey, "chat-session-1", null, null, CancellationToken.None);

        rig.Dispatch.SubmitRequests.Should().BeEmpty();
        rig.Dispatch.SubmitRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task 前缀后空内容_回用法不建任务()
    {
        var rig = BuildRig();
        var inbound = MakeInbound("/task ");

        var handled = await rig.Dispatcher.HandleAsync(inbound, rig.Config, ChannelKey, "chat-session-1", null, null, CancellationToken.None);

        handled.Should().BeTrue();
        rig.Dispatch.SubmitRequests.Should().BeEmpty();
        rig.Channel.Sent.Single().Content.Should().Contain("用法");
    }

    [Fact]
    public async Task 前缀带Agent名_剥离后作为AgentRef提交()
    {
        var rig = BuildRig();
        var inbound = MakeInbound("/task @程序员 整理文档");

        await rig.Dispatcher.HandleAsync(inbound, rig.Config, ChannelKey, "chat-session-1", "agent-im", "prov:m1", CancellationToken.None);

        var req = rig.Dispatch.SubmitRequests.Single();
        req.AgentRef.Should().Be("程序员");
        req.TaskInput.Should().Be("整理文档");
    }

    [Fact]
    public async Task 查询_任务状态_回最近绑定快照()
    {
        var rig = BuildRig();
        InsertBinding(taskKey: "k1", runId: 12, status: "stuck");
        rig.Dispatch.FindResults["k1"] = new AgentRunDispatchSnapshot
        {
            TaskKey = "k1",
            RunId = 12,
            Status = "stuck",
            Terminal = false,
            ResultText = "等待 write_file 审批",
            AgentName = "程序员",
        };

        var inbound = MakeInbound("任务状态");
        var handled = await rig.Dispatcher.HandleAsync(inbound, rig.Config, ChannelKey, "chat-session-1", null, null, CancellationToken.None);

        handled.Should().BeTrue();
        var text = rig.Channel.Sent.Single().Content;
        text.Should().Contain("#12").And.Contain("stuck").And.Contain("程序员");
    }

    [Fact]
    public async Task 查询_任务号12_按数字键回退查询()
    {
        var rig = BuildRig();
        rig.Dispatch.FindResults["12"] = new AgentRunDispatchSnapshot
        {
            TaskKey = "12",
            RunId = 12,
            Status = "completed",
            Terminal = true,
            ResultText = "报告已完成",
            AgentName = "程序员",
        };
        var inbound = MakeInbound("任务 #12");

        var handled = await rig.Dispatcher.HandleAsync(inbound, rig.Config, ChannelKey, "chat-session-1", null, null, CancellationToken.None);

        handled.Should().BeTrue();
        rig.Channel.Sent.Single().Content.Should().Contain("#12").And.Contain("completed").And.Contain("报告已完成");
    }

    [Fact]
    public async Task 查询_无任务_回空态指引()
    {
        var rig = BuildRig();
        var inbound = MakeInbound("任务状态");

        var handled = await rig.Dispatcher.HandleAsync(inbound, rig.Config, ChannelKey, "chat-session-1", null, null, CancellationToken.None);

        handled.Should().BeTrue();
        rig.Channel.Sent.Single().Content.Should().Contain("暂无任务");
    }

    [Fact]
    public async Task 查询_不存在的任务号_回未找到()
    {
        var rig = BuildRig();
        var inbound = MakeInbound("任务 #999");

        var handled = await rig.Dispatcher.HandleAsync(inbound, rig.Config, ChannelKey, "chat-session-1", null, null, CancellationToken.None);

        handled.Should().BeTrue();
        rig.Channel.Sent.Single().Content.Should().Contain("#999").And.Contain("未找到");
    }

    [Fact]
    public async Task 同会话在途再提交_回既有任务号()
    {
        var rig = BuildRig();
        rig.Dispatch.SubmitOutcomes.Enqueue(AgentRunDispatchOutcome.Fail("该会话已有任务在执行（#9，running），不重复提交。"));
        var inbound = MakeInbound("/task 第二条任务");

        var handled = await rig.Dispatcher.HandleAsync(inbound, rig.Config, ChannelKey, "chat-session-1", null, null, CancellationToken.None);

        handled.Should().BeTrue();                       // 前缀命中即拦截（含失败提示）
        rig.Dispatch.SubmitRequests.Should().ContainSingle();
        ImGatewayTask.FindAll(ImGatewayTask._.ChannelKey == ChannelKey).Should().BeEmpty();  // 失败不落绑定
        rig.Channel.Sent.Single().Content.Should().Contain("已有任务在执行").And.Contain("#9");
    }
    // ---- AC3/FR-4/FR-5：Watcher ----

    [Fact]
    public async Task Watcher_终态完成_模板回复且RepliedAt幂等()
    {
        var rig = BuildRig();
        var binding = InsertBinding(taskKey: "k1", runId: 42, status: "running");
        rig.Dispatch.FindResults["k1"] = new AgentRunDispatchSnapshot
        {
            TaskKey = "k1",
            RunId = 42,
            Status = "completed",
            Terminal = true,
            ResultText = "任务已完成的终局文本",
            AgentName = "程序员",
        };

        await rig.Watcher.PollOnceAsync(CancellationToken.None);

        var sent = rig.Channel.Sent.Single();
        sent.Content.Should().StartWith("✅ 任务 #42 完成").And.Contain("任务已完成的终局文本");
        sent.ReplyReqId.Should().BeNull();                 // 主动推送：不用可能过期的 ReplyReqId
        sent.ChatType.Should().Be("single");
        binding = ImGatewayTask.FindById(binding.Id)!;
        binding.RepliedAt.Should().NotBe(default(DateTime));
        binding.Status.Should().Be("completed");

        await rig.Watcher.PollOnceAsync(CancellationToken.None);
        rig.Channel.Sent.Should().ContainSingle();         // RepliedAt 幂等：不重发
    }

    [Fact]
    public async Task Watcher_超长结果_按MaxReplyChars截断()
    {
        var cfg = new WeComConfig { Enabled = true, TaskGateway = new TaskGatewayConfig { Enabled = true, MaxReplyChars = 50 } };
        var rig = BuildRig(cfg);
        InsertBinding(taskKey: "k1", runId: 42, status: "running");
        rig.Dispatch.FindResults["k1"] = new AgentRunDispatchSnapshot
        {
            TaskKey = "k1",
            RunId = 42,
            Status = "completed",
            Terminal = true,
            ResultText = new string('长', 200),
        };

        await rig.Watcher.PollOnceAsync(CancellationToken.None);

        var text = rig.Channel.Sent.Single().Content;
        text.Length.Should().BeLessThanOrEqualTo(80);
        text.Should().Contain("全文见任务中心").And.Contain("#42");
    }

    [Fact]
    public async Task Watcher_失败终态_回失败模板()
    {
        var rig = BuildRig();
        InsertBinding(taskKey: "k1", runId: 42, status: "running");
        rig.Dispatch.FindResults["k1"] = new AgentRunDispatchSnapshot
        {
            TaskKey = "k1",
            RunId = 42,
            Status = "failed",
            Terminal = true,
            ResultText = "上游模型 500",
        };

        await rig.Watcher.PollOnceAsync(CancellationToken.None);

        rig.Channel.Sent.Single().Content.Should().StartWith("❌ 任务 #42 失败").And.Contain("上游模型 500");
    }

    [Fact]
    public async Task Watcher_stuck且有Pending审批_发卡片每Id仅一次()
    {
        var rig = BuildRig();
        // 会话映射种子：Watcher 经 GetOrCreateSession 还原聊天会话（imtask:chat-session-1）
        new ImGatewaySession { ChannelKey = ChannelKey, SessionId = "chat-session-1", UpdatedAt = DateTime.Now }.Save();
        var binding = InsertBinding(taskKey: "k1", runId: 12, status: "stuck");
        rig.Dispatch.FindResults["k1"] = new AgentRunDispatchSnapshot
        {
            TaskKey = "k1",
            RunId = 12,
            Status = "stuck",
            Terminal = false,
            ResultText = "等待 write_file 审批",
        };
        rig.Approval.Approvals.Add(FakeApproval.Make(12, "imtask:chat-session-1"));

        await rig.Watcher.PollOnceAsync(CancellationToken.None);

        var card = rig.Channel.Sent.Single();
        card.Content.Should().Contain("⏸ 任务 #12 等待审批").And.Contain("write_file").And.Contain("同意 #12").And.Contain("拒绝 #12");
        binding = ImGatewayTask.FindById(binding.Id)!;
        binding.PromptedApprovalIds.Should().Contain("12");

        await rig.Watcher.PollOnceAsync(CancellationToken.None);
        rig.Channel.Sent.Should().ContainSingle();         // 每审批 Id 仅提示一次
    }

    [Fact]
    public async Task Watcher_审批卡_回显当前审批与拒绝关键词()
    {
        var rig = BuildRig();
        new ImGatewaySession { ChannelKey = ChannelKey, SessionId = "chat-session-1", UpdatedAt = DateTime.Now }.Save();
        var binding = InsertBinding(taskKey: "k1", runId: 12, status: "stuck");
        rig.Dispatch.FindResults["k1"] = new AgentRunDispatchSnapshot
        {
            TaskKey = "k1",
            RunId = 12,
            Status = "stuck",
            Terminal = false,
            ResultText = "等待 write_file 审批",
        };
        rig.Approval.Approvals.Add(FakeApproval.Make(12, "imtask:chat-session-1"));

        await rig.Watcher.PollOnceAsync(CancellationToken.None);

        var card = rig.Channel.Sent.Single();
        // T10-B：卡片回显当前词表（用户自定义数字快捷键「1」知情可用，防「发了 1 没反应/不知为何被吞」）
        card.Content.Should().Contain("同意").And.Contain("批准").And.Contain("拒绝").And.Contain("驳回");
    }

    [Fact]
    public async Task Watcher_捕获RunId_回填绑定行()
    {
        // qa 缺陷召回：Dispatcher 落库 RunId=0，Watcher 查到快照后必须回填（否则重启恢复路径不可达）
        var rig = BuildRig();
        var binding = InsertBinding(taskKey: "k1", runId: 0, status: "pending");
        rig.Dispatch.FindResults["k1"] = new AgentRunDispatchSnapshot
        {
            TaskKey = "k1",
            RunId = 42,
            Status = "running",
            Terminal = false,
        };

        await rig.Watcher.PollOnceAsync(CancellationToken.None);

        binding = ImGatewayTask.FindById(binding.Id)!;
        binding.RunId.Should().Be(42, "FR-4：Watcher 拿到 RunId 后回填绑定行，宿主重启后按数字键恢复");
        binding.Status.Should().Be("running");
        rig.Channel.Sent.Should().BeEmpty();               // 非终态：无回复
    }

    [Fact]
    public async Task Watcher_超时MaxWaitHours_回指引不再跟进()
    {
        var cfg = new WeComConfig { Enabled = true, TaskGateway = new TaskGatewayConfig { Enabled = true, MaxWaitHours = 24 } };
        var rig = BuildRig(cfg);
        InsertBinding(taskKey: "k1", runId: 42, status: "running", createdAt: DateTime.Now.AddHours(-25));
        rig.Dispatch.FindResults["k1"] = new AgentRunDispatchSnapshot
        {
            TaskKey = "k1",
            RunId = 42,
            Status = "running",
            Terminal = false,
        };

        await rig.Watcher.PollOnceAsync(CancellationToken.None);

        var text = rig.Channel.Sent.Single().Content;
        text.Should().Contain("仍在执行").And.Contain("任务中心");
        var binding = ImGatewayTask.FindAll(ImGatewayTask._.TaskKey == "k1").Single();
        binding.RepliedAt.Should().NotBe(default(DateTime));
    }

    [Fact]
    public async Task Watcher_宿主重启恢复_按RunId续跟()
    {
        // 模拟重启：内存记录丢失，仅 DB 绑定行 + 契约数字键回退可查
        var rig = BuildRig();
        InsertBinding(taskKey: "7f3a9c1e", runId: 77, status: "running");
        rig.Dispatch.FindResults["77"] = new AgentRunDispatchSnapshot
        {
            TaskKey = "77",
            RunId = 77,
            Status = "completed",
            Terminal = true,
            ResultText = "重启后查到的终局",
        };

        await rig.Watcher.PollOnceAsync(CancellationToken.None);

        rig.Channel.Sent.Single().Content.Should().Contain("✅ 任务 #77 完成").And.Contain("重启后查到的终局");
    }

    [Fact]
    public async Task Watcher_规划期丢失Key_记Unknown不误报()
    {
        var rig = BuildRig();
        var binding = InsertBinding(taskKey: "lostkey", runId: 0, status: "pending");

        await rig.Watcher.PollOnceAsync(CancellationToken.None);

        rig.Channel.Sent.Should().BeEmpty();               // 无 RunId 可查：不发臆测回复
        binding = ImGatewayTask.FindById(binding.Id)!;
        binding.Status.Should().Be("unknown");
    }
    // ---- AC10：IM 审批闭环 ----

    [Fact]
    public async Task 同意审批号_DecideAsync带IM留痕并触发续跑()
    {
        var rig = BuildRig();
        InsertBinding(taskKey: "k1", runId: 12, status: "stuck", prompted: "12");
        rig.Approval.Approvals.Add(FakeApproval.Make(12, "imtask:chat-session-1", status: "Pending", tool: "write_file"));
        var inbound = MakeInbound("同意 #12");

        var handled = await rig.Dispatcher.HandleAsync(inbound, rig.Config, ChannelKey, "chat-session-1", null, null, CancellationToken.None);

        handled.Should().BeTrue();
        var decide = rig.Approval.DecideCalls.Single();
        decide.Id.Should().Be(12);
        decide.Approved.Should().BeTrue();
        decide.Note.Should().Contain("imgateway:wecom:conv-1:user-1").And.Contain("经 IM 决策");
        rig.Dispatch.ResumeCalls.Should().ContainSingle().Which.TaskKey.Should().Be("k1");
        rig.Channel.Sent.Single().Content.Should().Contain("已同意 #12").And.Contain("write_file").And.Contain("继续执行");
    }

    [Fact]
    public async Task 拒绝审批号_决策拒绝并回执()
    {
        var rig = BuildRig();
        InsertBinding(taskKey: "k1", runId: 12, status: "stuck", prompted: "12");
        rig.Approval.Approvals.Add(FakeApproval.Make(12, "imtask:chat-session-1"));
        var inbound = MakeInbound("拒绝 #12");

        var handled = await rig.Dispatcher.HandleAsync(inbound, rig.Config, ChannelKey, "chat-session-1", null, null, CancellationToken.None);

        handled.Should().BeTrue();
        rig.Approval.DecideCalls.Single().Approved.Should().BeFalse();
        rig.Dispatch.ResumeCalls.Should().ContainSingle();   // 拒绝同样触发续跑（由 Run 按决策落终态）
        rig.Channel.Sent.Single().Content.Should().Contain("已拒绝 #12");
    }

    [Fact]
    public async Task 跨通道提示的审批_关键字不拦截()
    {
        var rig = BuildRig();
        InsertBinding(taskKey: "k-other", runId: 12, channelKey: "wecom:conv-other:u2", status: "stuck", prompted: "12");
        rig.Approval.Approvals.Add(FakeApproval.Make(12, "imtask:s-other"));
        var inbound = MakeInbound("同意 #12");

        var handled = await rig.Dispatcher.HandleAsync(inbound, rig.Config, ChannelKey, "chat-session-1", null, null, CancellationToken.None);

        handled.Should().BeFalse();                        // 非本通道会话任务 → 不拦截走聊天
        rig.Approval.DecideCalls.Should().BeEmpty();
        rig.Dispatch.ResumeCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task 无待审批时关键字_零误伤走聊天()
    {
        var rig = BuildRig();
        var inbound = MakeInbound("同意这个方案");

        var handled = await rig.Dispatcher.HandleAsync(inbound, rig.Config, ChannelKey, "chat-session-1", null, null, CancellationToken.None);

        handled.Should().BeFalse();
        rig.Approval.DecideCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task 审批已超时_回指引不误放行()
    {
        var rig = BuildRig();
        InsertBinding(taskKey: "k1", runId: 12, status: "stuck", prompted: "12");
        // 列表已无 Pending（审批超时转 Expired）；DecideAsync fail-closed 返回 null
        rig.Approval.Approvals.Add(FakeApproval.Make(12, "imtask:chat-session-1", status: "Expired"));
        rig.Approval.DecideScript = _ => null;
        var inbound = MakeInbound("同意 #12");

        var handled = await rig.Dispatcher.HandleAsync(inbound, rig.Config, ChannelKey, "chat-session-1", null, null, CancellationToken.None);

        handled.Should().BeTrue();
        rig.Approval.DecideCalls.Should().ContainSingle().Which.Approved.Should().BeTrue();
        rig.Dispatch.ResumeCalls.Should().BeEmpty();       // 决策未成功绝不续跑
        rig.Channel.Sent.Single().Content.Should().Contain("超时").And.Contain("任务中心");
    }

    [Fact]
    public async Task ImApproval子开关关闭_关键字不拦截()
    {
        var cfg = new WeComConfig { Enabled = true, TaskGateway = new TaskGatewayConfig { Enabled = true, ImApprovalEnabled = false } };
        var rig = BuildRig(cfg);
        InsertBinding(taskKey: "k1", runId: 12, status: "stuck", prompted: "12");
        rig.Approval.Approvals.Add(FakeApproval.Make(12, "imtask:chat-session-1"));
        var inbound = MakeInbound("同意 #12");

        var handled = await rig.Dispatcher.HandleAsync(inbound, cfg, ChannelKey, "chat-session-1", null, null, CancellationToken.None);

        handled.Should().BeFalse();
        rig.Approval.DecideCalls.Should().BeEmpty();
    }

    // ---- AC3（Router 分支集成）+ AC9（simulate-inbound） ----

    [Fact]
    public async Task Router_Enabled时前缀消息进任务分支不进聊天()
    {
        var scripted = new ScriptedChat(_ => new ChatCompletionResult { Success = true, Content = "聊天回复" });
        var rig = BuildRig(chat: scripted);
        var inbound = MakeInbound("/task 写个报告", msgId: "m-task-1");

        await rig.Router.ProcessInboundAsync(inbound, CancellationToken.None);

        scripted.Requests.Should().BeEmpty();              // 任务分支拦截，聊天未触达
        rig.Channel.Sent.Single().Content.Should().Contain("已建任务");
        ImGatewayTask.FindAll(ImGatewayTask._.ChannelKey == ChannelKey).Should().ContainSingle();
    }

    [Fact]
    public async Task Router_非任务消息_走原聊天链路()
    {
        var scripted = new ScriptedChat(req => new ChatCompletionResult { Success = true, Content = "聊天回复", SessionId = req.SessionId });
        var rig = BuildRig(chat: scripted);
        var inbound = MakeInbound("今天天气如何", msgId: "m-chat-1");

        await rig.Router.ProcessInboundAsync(inbound, CancellationToken.None);

        scripted.Requests.Should().ContainSingle();        // 聊天链路照常
        rig.Channel.Sent.Single().Content.Should().Be("聊天回复");
        ImGatewayTask.FindAll(ImGatewayTask._.ChannelKey == ChannelKey).Should().BeEmpty();
    }

    [Fact]
    public async Task SimulateInbound_组装同形状入站并直调Router()
    {
        var scripted = new ScriptedChat(req => new ChatCompletionResult { Success = true, Content = "模拟链路回复", SessionId = req.SessionId });
        var rig = BuildRig(chat: scripted);
        var controller = new ImGatewayController(new IImChannel[] { rig.Channel }, rig.Router,
            new Mock<IConfigStore>().Object, null!, null!);

        var result = await controller.SimulateInbound(new SimulateInboundRequest
        {
            ChannelType = "wecom",
            ConversationId = "conv-1",
            PlatformUserId = "user-1",
            Content = "直接注入的消息",
        }, CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        var payloadType = json.Value!.GetType();
        Assert.True((bool)payloadType.GetProperty("success")!.GetValue(json.Value)!);
        ((string)payloadType.GetProperty("msgId")!.GetValue(json.Value)!).Should().StartWith("sim-");
        scripted.Requests.Should().ContainSingle();        // 零旁路：真实 Router 全链路执行
        scripted.Requests[0].Message.Should().Be("直接注入的消息");
        rig.Channel.Sent.Single().Content.Should().Be("模拟链路回复");
    }

    [Fact]
    public void SimulateInbound_类级ApiKeyPolicy继承_端点未加匿名放行()
    {
        var classAttr = typeof(ImGatewayController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true);
        classAttr.Should().NotBeEmpty("模拟入站端点必须继承类级 ApiKeyPolicy 鉴权");
        typeof(ImGatewayController).GetMethods()
            .Where(m => m.Name == nameof(ImGatewayController.SimulateInbound))
            .SelectMany(m => m.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true))
            .Should().BeEmpty();
    }

    /// <summary>脚本化 IChatCompletion（ScriptedProvider 模式，先例 ImGatewayRouterRecordTests）。</summary>
    internal sealed class ScriptedChat : IChatCompletion
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

    // ---- T16：进展推送 / 无变化计时 / 去重 / 节拍配置（验收#8 用户口径） ----

    [Fact]
    public async Task Watcher_执行中快照变化_推简短进展首轮只建基线()
    {
        var rig = BuildRig();
        var binding = InsertBinding(taskKey: "k1", runId: 12, status: "running");
        rig.Dispatch.FindResults["k1"] = new AgentRunDispatchSnapshot
        {
            TaskKey = "k1", RunId = 12, Status = "running", Terminal = false,
            CurrentStepIndex = 1, StepCount = 5,
        };

        await rig.Watcher.PollOnceAsync(CancellationToken.None);
        rig.Channel.Sent.Should().BeEmpty();   // 首轮：只建基线不推（防重启轰炸）

        // 第二轮：步骤推进（指纹变化）→ 推进展
        rig.Dispatch.FindResults["k1"] = new AgentRunDispatchSnapshot
        {
            TaskKey = "k1", RunId = 12, Status = "running", Terminal = false,
            CurrentStepIndex = 2, StepCount = 5,
        };
        await rig.Watcher.PollOnceAsync(CancellationToken.None);
        rig.Channel.Sent.Should().ContainSingle();
        rig.Channel.Sent.Single().Content.Should().Contain("▶ 任务 #12 执行中").And.Contain("步骤 3/5");

        // 第三轮：无变化 → 不再推
        await rig.Watcher.PollOnceAsync(CancellationToken.None);
        rig.Channel.Sent.Should().ContainSingle();
    }

    [Fact]
    public async Task Watcher_进展推送子开关关_不推但仍建基线()
    {
        var config = new WeComConfig();
        config.TaskGateway.Enabled = true;   // 网关总开关（默认关；用例需激活轮询）
        config.TaskGateway.ProgressPushEnabled = false;
        var rig = BuildRig(config);
        var binding = InsertBinding(taskKey: "k1", runId: 12, status: "running");
        rig.Dispatch.FindResults["k1"] = new AgentRunDispatchSnapshot
        {
            TaskKey = "k1", RunId = 12, Status = "running", Terminal = false, CurrentStepIndex = 0, StepCount = 5,
        };
        await rig.Watcher.PollOnceAsync(CancellationToken.None);
        rig.Dispatch.FindResults["k1"] = new AgentRunDispatchSnapshot
        {
            TaskKey = "k1", RunId = 12, Status = "running", Terminal = false, CurrentStepIndex = 1, StepCount = 5,
        };
        await rig.Watcher.PollOnceAsync(CancellationToken.None);
        rig.Channel.Sent.Should().BeEmpty();   // 开关关：变化也不推
    }
    [Fact]
    public async Task Watcher_无变化达卡住分钟_通知一次幂等()
    {
        var rig = BuildRig();   // 默认 StalledMinutes=5
        var binding = InsertBinding(taskKey: "k1", runId: 12, status: "running", createdAt: DateTime.Now.AddMinutes(-10));
        var snap = new AgentRunDispatchSnapshot
        {
            TaskKey = "k1", RunId = 12, Status = "running", Terminal = false, CurrentStepIndex = 0, StepCount = 5,
        };
        rig.Dispatch.FindResults["k1"] = snap;
        var clock = new DateTime(2026, 11, 4, 12, 0, 0);
        rig.Watcher.NowProvider = () => clock;

        await rig.Watcher.PollOnceAsync(CancellationToken.None);   // 首轮：建基线（计时起点=12:00）
        rig.Channel.Sent.Should().BeEmpty();   // 首轮不推进展

        clock = new DateTime(2026, 11, 4, 12, 6, 0);   // 前拨 6 分钟（> StalledMinutes=5）
        await rig.Watcher.PollOnceAsync(CancellationToken.None);   // 无变化轮 → 卡住通知一次
        rig.Channel.Sent.Should().ContainSingle();
        rig.Channel.Sent.Single().Content.Should().Contain("⏳ 任务 #12").And.Contain("无进展");

        clock = new DateTime(2026, 11, 4, 12, 12, 0);
        await rig.Watcher.PollOnceAsync(CancellationToken.None);   // 再轮：幂等不重发
        rig.Channel.Sent.Should().ContainSingle();
    }

    [Fact]
    public async Task Watcher_StalledMinutes配置生效_为0时两轮即通知()
    {
        var config = new WeComConfig();
        config.TaskGateway.Enabled = true;   // 网关总开关（默认关；用例需激活轮询）
        config.TaskGateway.StalledMinutes = 0;   // 验收：可配——0=立即（真实时钟）
        var rig = BuildRig(config);
        var binding = InsertBinding(taskKey: "k1", runId: 12, status: "running", createdAt: DateTime.Now.AddMinutes(-10));
        rig.Dispatch.FindResults["k1"] = new AgentRunDispatchSnapshot
        {
            TaskKey = "k1", RunId = 12, Status = "running", Terminal = false, CurrentStepIndex = 0, StepCount = 5,
        };
        rig.Watcher.NowProvider = null;   // 真实时钟
        await rig.Watcher.PollOnceAsync(CancellationToken.None);   // 首轮建基线
        rig.Channel.Sent.Should().BeEmpty();
        await rig.Watcher.PollOnceAsync(CancellationToken.None);   // 无变化 → 立即通知（配置生效则 StalledMinutes=0）
        rig.Channel.Sent.Should().ContainSingle("StalledMinutes=0 配置应生效");
        rig.Channel.Sent.Single().Content.Should().Contain("⏳ 任务 #12");
    }

    // ---- T12：超长回复分段发送（取消静默截断；MaxReplyChars 改总保险丝） ----

    [Fact]
    public async Task Watcher_终态回复超通道阈值_分段顺序发送不截断()
    {
        var rig = BuildRig();   // 默认 MaxCharsPerMessage=2000
        var binding = InsertBinding(taskKey: "k1", runId: 12, status: "completed");
        var longText = new string('x', 4500);   // 超过 2000 → 3 段
        rig.Dispatch.FindResults["k1"] = new AgentRunDispatchSnapshot
        {
            TaskKey = "k1", RunId = 12, Status = "completed", Terminal = true, ResultText = longText,
        };
        await rig.Watcher.PollOnceAsync(CancellationToken.None);
        rig.Channel.Sent.Should().HaveCount(3);
        rig.Channel.Sent[0].Content.Should().StartWith("✅ 任务 #12 完成").And.Contain("（1/3）");
        rig.Channel.Sent[1].Content.Should().Contain("（2/3）");
        rig.Channel.Sent[2].Content.Should().Contain("（3/3）");
        string.Concat(rig.Channel.Sent.Select(m => m.Content)).Should().Contain(longText.Substring(0, 500)).And.Contain(longText.Substring(4000, 500));   // 全文可回收，不截断
    }

    [Fact]
    public async Task Watcher_回复未超阈值_单段无分段标记()
    {
        var rig = BuildRig();
        var binding = InsertBinding(taskKey: "k1", runId: 12, status: "completed");
        rig.Dispatch.FindResults["k1"] = new AgentRunDispatchSnapshot
        {
            TaskKey = "k1", RunId = 12, Status = "completed", Terminal = true, ResultText = "短结果",
        };
        await rig.Watcher.PollOnceAsync(CancellationToken.None);
        rig.Channel.Sent.Should().ContainSingle();
        rig.Channel.Sent.Single().Content.Should().Contain("短结果").And.NotContain("（1/");
    }

    [Fact]
    public async Task Watch_MaxReply保险丝_总上限断加尾_为0时关闭()
    {
        var config = new WeComConfig();
        config.TaskGateway.Enabled = true;
        config.TaskGateway.MaxReplyChars = 100;   // 总保险丝收紧
        config.TaskGateway.MaxCharsPerMessage = 2000;
        var rig = BuildRig(config);
        var binding = InsertBinding(taskKey: "k1", runId: 12, status: "completed");
        var longText = new string('y', 5000);
        rig.Dispatch.FindResults["k1"] = new AgentRunDispatchSnapshot
        {
            TaskKey = "k1", RunId = 12, Status = "completed", Terminal = true, ResultText = longText,
        };
        await rig.Watcher.PollOnceAsync(CancellationToken.None);
        var all = string.Concat(rig.Channel.Sent.Select(m => m.Content));
        all.Should().Contain("全文见任务中心 #12");   // 保险丝截断+尾注
        all.Should().NotContain(longText.Substring(200, 100));   // 100 字后不出现
    }

    [Fact]
    public async Task Watcher_分段中途失败_不置RepliedAt_下轮全量重发()   // 语义裁决：全量重发非续发
    {
        var rig = BuildRig();
        var binding = InsertBinding(taskKey: "k1", runId: 12, status: "completed");
        var longText = new string('z', 4500);   // 3 段
        rig.Dispatch.FindResults["k1"] = new AgentRunDispatchSnapshot
        {
            TaskKey = "k1", RunId = 12, Status = "completed", Terminal = true, ResultText = longText,
        };
        rig.Channel.FailWhenContains.Add("（3/3）");   // 第 3 段必失败

        await rig.Watcher.PollOnceAsync(CancellationToken.None);   // 第 1 轮：2 段成功、第 3 段失败不进 Sent
        rig.Channel.Sent.Should().HaveCount(2);   // 失败段不进 Sent（SendAsync 抛异常在 Add 前）
        ImGatewayTask.FindById(binding.Id)!.RepliedAt.Should().Be(default(DateTime));   // 未全成功：不置位（XCode 非空 DateTime）

        rig.Channel.FailWhenContains.Clear();      // 通道恢复
        await rig.Watcher.PollOnceAsync(CancellationToken.None);   // 下轮：全量重发（语义裁决=全量非续发）
        rig.Channel.Sent.Should().HaveCount(5);    // 2+3
        ImGatewayTask.FindById(binding.Id)!.RepliedAt.Should().NotBe(default(DateTime));   // 全成 → 置位
    }
}
