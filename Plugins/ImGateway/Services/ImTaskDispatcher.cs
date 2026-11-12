using System.Text.RegularExpressions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.ImGateway.Core;
using ForgeSelf.Api.Plugins.ImGateway.Entities;
using ForgeSelf.Core;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.ImGateway.Services;

/// <summary>
/// IM 任务派发器（PILOT-079 FR-3/FR-9）：任务前缀派发 / 任务状态查询 / 审批关键字决策。
/// 由 ImGatewayRouter 在消息落库后调用；返回 false = 非任务消息，Router 继续原聊天链路。
/// 分支优先级：任务前缀 > 审批关键字 > 任务状态 > 聊天。
/// 依赖只收接缝：契约每次经 ctx.Get 解析（不缓存实例），通道按 ChannelType 解析。
/// 契约失败一律 Success=false + Error 中文原文；本类全程吞异常，绝不网关。
/// </summary>
public class ImTaskDispatcher
{
    private readonly IContext _ctx;
    private readonly ImGatewayMessageStore _messageStore;
    private readonly Dictionary<string, IImChannel> _channels;

    public ImTaskDispatcher(IContext ctx, ImGatewayMessageStore messageStore, IEnumerable<IImChannel> channels)
    {
        _ctx = ctx;
        _messageStore = messageStore;
        _channels = channels.ToDictionary(c => c.ChannelType, c => c);
    }

    /// <summary>处理一条入站消息的任务分支。true=已拦截（不再进聊天）；false=走原聊天链路。</summary>
    public async Task<bool> HandleAsync(InboundMessage inbound, WeComConfig config, string channelKey,
        string sessionId, string? agentId, string? modelId, CancellationToken ct)
    {
        var tg = config.TaskGateway ?? new TaskGatewayConfig();
        if (!tg.Enabled) return false;                       // 总开关关：零行为（v2.1.1 语义）

        try
        {
        var content = inbound.Content ?? string.Empty;   // 原文："/task " 前缀含尾空格，TrimEnd 会破坏前缀匹配
            if (content.Length == 0) return false;

            // 优先级 1：任务前缀
            var prefix = (tg.CommandPrefixes ?? [])
                .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && content.StartsWith(p, StringComparison.Ordinal));
            if (prefix != null)
            {
                await HandleTaskSubmitAsync(inbound, tg, prefix, channelKey, sessionId, agentId, modelId, ct);
                return true;                                  // 前缀命中即拦截（含失败/用法提示）
            }

            // 优先级 2：审批关键字决策（子开关）
            if (tg.ImApprovalEnabled && await TryHandleApprovalAsync(inbound, tg, channelKey, sessionId, ct))
                return true;

            // 优先级 3：任务状态查询
            if (await TryHandleStatusQueryAsync(inbound, channelKey, sessionId, ct))
                return true;

            return false;                                     // 非任务消息 → 原聊天链路（零误伤）
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ImTask] 任务分支处理异常 Channel={0}: {1}", channelKey, ex.Message);
            return false;                                     // 分支自身异常交回聊天链路兜底，不阻塞网关
        }
    }

    // ---- 前缀派发 ----

    private async Task HandleTaskSubmitAsync(InboundMessage inbound, TaskGatewayConfig tg, string prefix,
        string channelKey, string sessionId, string? agentId, string? modelId, CancellationToken ct)
    {
        if (!_channels.TryGetValue(inbound.ChannelType, out var channel)) return;   // 无通道可回：仅记日志

        var rest = inbound.Content![prefix.Length..].Trim();
        if (rest.Length == 0)
        {
            // 边界：前缀命中但内容为空 → 用法提示，不建任务
            await SendAckAsync(channel, inbound, channelKey, sessionId,
                "任务用法：/task <任务原文>（或 任务：<任务原文>），可 @Agent名 指派。例：/task @程序员 整理文档");
            return;
        }

        // @agent 剥离：AgentRef 优先级 @名字 > 会话绑定（agentId 已在映射里）> 通道 BoundAgentId > 默认链。
        // DefaultAgentId（design D2）仅在无 @ 且会话未绑定 Agent 时作为 AgentRef 落点。
        string? agentRef = null;
        var m = Regex.Match(rest, @"^@(\S+)\s*");
        if (m.Success)
        {
            agentRef = m.Groups[1].Value;
            rest = rest[m.Length..].Trim();
        }
        else if (string.IsNullOrWhiteSpace(agentId) && !string.IsNullOrWhiteSpace(tg.DefaultAgentId))
        {
            agentRef = tg.DefaultAgentId.Trim();
        }

        var dispatch = _ctx.Get<IAgentRunDispatch>();
        if (dispatch == null)
        {
            SendAckAsync(channel, inbound, channelKey, sessionId,
                "任务派发不可用：IAgentRunDispatch 契约未注册（AIAgent 插件是否已加载？）");
            return;
        }

        var outcome = await dispatch.SubmitAsync(new AgentRunDispatchRequest
        {
            TaskInput = rest,
            AgentRef = agentRef,                              // null = orchestrator 既有默认链
            SessionId = $"imtask:{sessionId}",                // FR：Run SessionId=imtask:{会话Id}
            ChatModelId = modelId,
            CreatedBy = channelKey,                           // 派发来源留痕
        }, ct);

        if (!outcome.Success || string.IsNullOrWhiteSpace(outcome.TaskKey))
        {
            await SendAckAsync(channel, inbound, channelKey, sessionId, $"任务派发失败：{outcome.Error}");
            return;                                           // 失败不落绑定（可重发）
        }

        var row = new ImGatewayTask
        {
            TaskKey = outcome.TaskKey!,
            RunId = 0,                                        // RunId 由 Watcher 捕获快照后回填
            ChannelKey = channelKey,
            ChannelType = inbound.ChannelType,
            ConversationId = inbound.ConversationId,
            PlatformUserId = inbound.PlatformUserId,
            ChatType = string.IsNullOrWhiteSpace(inbound.ChatType) ? "single" : inbound.ChatType,
            ReplyReqId = inbound.ReplyReqId,
            InboundMsgId = inbound.MsgId,
            TaskInput = Truncate(rest, 200),                  // 全文进 Run，绑定只存 200 字摘要
            Status = outcome.Status ?? "pending",
            CreatedAt = DateTime.Now,
            PromptedApprovalIds = string.Empty,
        };
        row.Save();

        var code = outcome.TaskKey!.Length >= 8 ? outcome.TaskKey![..8] : outcome.TaskKey!;
        var agentName = string.IsNullOrWhiteSpace(outcome.AgentName) ? "默认 Agent" : outcome.AgentName!;
        await SendAckAsync(channel, inbound, channelKey, sessionId,
            $"已建任务（编号 {code}）已指派 {agentName}，完成后自动回复");
        XTrace.Log.Info("[ImTask] 派发成功 Channel={0} taskKey={1} Agent={2}", channelKey, outcome.TaskKey, outcome.AgentId ?? "(默认)");
    }
    // ---- 审批关键字决策（FR-9） ----

    private async Task<bool> TryHandleApprovalAsync(InboundMessage inbound, TaskGatewayConfig tg,
        string channelKey, string sessionId, CancellationToken ct)
    {
        var content = (inbound.Content ?? string.Empty).Trim();
        var approved = (tg.ApproveKeywords ?? []).Any(k => !string.IsNullOrWhiteSpace(k) && content.StartsWith(k.Trim(), StringComparison.Ordinal));
        var denied = !approved && (tg.RejectKeywords ?? []).Any(k => !string.IsNullOrWhiteSpace(k) && content.StartsWith(k.Trim(), StringComparison.Ordinal));
        if (!approved && !denied) return false;

        if (!_channels.TryGetValue(inbound.ChannelType, out var channel)) return false;
        var dispatch = _ctx.Get<IAgentRunDispatch>();
        var approvalService = _ctx.Get<IApprovalService>();
        if (dispatch == null || approvalService == null) return false;

        var imtaskSession = $"imtask:{sessionId}";
        // 本通道已提示过的审批 Id 并集（决策与拦截都以「已提示」为界，零误伤）
        var bindings = ImGatewayTask.FindAll(ImGatewayTask._.ChannelKey == channelKey);
        var promptedIds = new HashSet<long>();
        foreach (var b in bindings)
            foreach (var piece in (b.PromptedApprovalIds ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (long.TryParse(piece, out var pid)) promptedIds.Add(pid);

        var approvals = await approvalService.ListBySessionAsync(imtaskSession, ct);
        var match = Regex.Match(content, @"#(\d+)");
        ApprovalDto? target;
        if (match.Success && long.TryParse(match.Groups[1].Value, out var explicitId))
        {
            // 指定编号：只拦本通道已提示的审批（跨会话/未提示 → 不拦截走聊天）
            if (!promptedIds.Contains(explicitId)) return false;
            target = approvals.FirstOrDefault(a => a.Id == explicitId);   // 可能 null（已转 Expired 出列）→ Decide fail-closed 兜底
            if (target == null) target = new ApprovalDto { Id = explicitId, SessionId = imtaskSession, ToolName = "?", Status = "Expired" };
        }
        else
        {
            var pending = approvals
                .Where(a => a.Status == nameof(ApprovalStatus.Pending) && promptedIds.Contains(a.Id))
                .ToList();
            if (pending.Count == 1) target = pending[0];
            else if (pending.Count > 1)
            {
                await SendAckAsync(channel, inbound, channelKey, sessionId, $"有 {pending.Count} 个待审批，请指定编号，如：同意 #12");
                return true;
            }
            else return false;                                            // 无待审批 → 走聊天（零误伤）
        }

        var note = $"imgateway:{channelKey} 经 IM 决策 @{DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        var decided = await approvalService.DecideAsync(target.Id, approved, note, ct);
        if (decided == null)
        {
            // fail-closed：超时/已决策/不存在 → 指引任务中心，绝不续跑
            await SendAckAsync(channel, inbound, channelKey, sessionId,
                $"审批 #{target.Id} 已超时或已处理，请在任务中心重新发起/续跑");
            return true;
        }

        // 续跑（同意/拒绝都续）：MAF 下一回合消费决策（批准→继续；拒绝→按决策落终态）
        var binding = bindings
            .Where(b => (b.PromptedApprovalIds ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(decided.Id.ToString()))
            .OrderByDescending(b => b.Id)
            .FirstOrDefault() ?? bindings.OrderByDescending(b => b.Id).FirstOrDefault();
        if (binding != null)
        {
            var resume = await dispatch.ResumeAsync(binding.TaskKey, note, ct);
            XTrace.Log.Info("[ImTask] IM 审批决策 #{0} approved={1} 续跑 taskKey={2} success={3}",
                decided.Id, approved, binding.TaskKey, resume.Success);
        }

        var tool = string.IsNullOrWhiteSpace(decided.ToolName) ? "?" : decided.ToolName;
        await SendAckAsync(channel, inbound, channelKey, sessionId,
            approved ? $"已同意 #{decided.Id}（{tool}），任务继续执行" : $"已拒绝 #{decided.Id}");
        return true;
    }

    // ---- 任务状态查询（FR-4） ----

    private async Task<bool> TryHandleStatusQueryAsync(InboundMessage inbound, string channelKey,
        string sessionId, CancellationToken ct)
    {
        var content = (inbound.Content ?? string.Empty).Trim();
        ImGatewayTask? binding = null;
        string? numericKey = null;

        if (content == "任务状态")
        {
            binding = ImGatewayTask.FindAll(ImGatewayTask._.ChannelKey == channelKey)
                .OrderByDescending(b => b.Id).FirstOrDefault();
            if (binding == null)
            {
                if (!_channels.TryGetValue(inbound.ChannelType, out var ch0)) return false;
                await SendAckAsync(ch0, inbound, channelKey, sessionId, "暂无任务，发 /task 创建");
                return true;
            }
        }
        else
        {
            var m = Regex.Match(content, @"^任务\s*#(\d+)$");
            if (!m.Success) return false;
            numericKey = m.Groups[1].Value;
            binding = ImGatewayTask.FindAll(ImGatewayTask._.ChannelKey == channelKey)
                .FirstOrDefault(b => b.RunId.ToString() == numericKey || b.TaskKey == numericKey);
        }

        if (!_channels.TryGetValue(inbound.ChannelType, out var channel)) return false;
        var dispatch = _ctx.Get<IAgentRunDispatch>();
        if (dispatch == null) return false;

        if (binding == null)
        {
            // 指定任务号且本通道无绑定：纯数字键直查（契约含 RunId 投影回退）
            var snap = await dispatch.FindAsync(numericKey!, ct);
            if (snap == null)
            {
                await SendAckAsync(channel, inbound, channelKey, sessionId, $"任务 #{numericKey} 未找到");
                return true;
            }
            await SendAckAsync(channel, inbound, channelKey, sessionId, FormatSnapshot(snap));
            return true;
        }

        var snapshot = await dispatch.FindAsync(binding.TaskKey, ct)
                       ?? (binding.RunId > 0 ? await dispatch.FindAsync(binding.RunId.ToString(), ct) : null);
        if (snapshot == null)
        {
            var label = binding.RunId > 0 ? binding.RunId.ToString() : ShortCode(binding.TaskKey);
            await SendAckAsync(channel, inbound, channelKey, sessionId, $"任务 #{label} 暂无快照，稍后再查");
            return true;
        }
        await SendAckAsync(channel, inbound, channelKey, sessionId, FormatSnapshot(snapshot));
        return true;
    }

    private static string FormatSnapshot(AgentRunDispatchSnapshot s)
    {
        var label = s.RunId > 0 ? s.RunId.ToString() : ShortCode(s.TaskKey);
        var text = $"任务 #{label}｜状态：{s.Status}｜指派：{s.AgentName ?? "默认"}";
        if (!string.IsNullOrWhiteSpace(s.ResultText)) text += "\n" + Truncate(s.ResultText, 200);
        return text;
    }

    private static string ShortCode(string key) => key.Length >= 8 ? key[..8] : key;

    private static string Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    // ---- 回发（尽力而为，绝不抛） ----

    private async Task SendAckAsync(IImChannel channel, InboundMessage inbound, string channelKey,
        string sessionId, string content)
    {
        try
        {
            await channel.SendAsync(new OutboundMessage
            {
                ChannelType = inbound.ChannelType,
                ConversationId = inbound.ConversationId,
                PlatformUserId = inbound.PlatformUserId,
                Content = content,
                ReplyReqId = inbound.ReplyReqId,                  // ack：req_id 仍热，被动回复
                ChatType = inbound.ChatType,
            });
            _messageStore.RecordReply(channelKey, inbound.ChannelType, content, sessionId);
        }
        catch (Exception ex)
        {
            _messageStore.RecordError(channelKey, inbound.ChannelType, sessionId, ex.Message);
            XTrace.Log.Error("[ImTask] ack 回发失败 Channel={0}: {1}", channelKey, ex.Message);
        }
    }
}
