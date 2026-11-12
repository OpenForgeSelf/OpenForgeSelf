using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.ImGateway.Core;
using ForgeSelf.Api.Plugins.ImGateway.Entities;
using ForgeSelf.Core;
using Microsoft.Extensions.Hosting;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.ImGateway.Services;

/// <summary>
/// IM 任务跟进 Watcher（PILOT-079 FR-4/FR-5/FR-9）：轮询未终态绑定 → FindAsync → 状态回写 →
/// 终态主动回复（FR-5 模板，超通道阈值分段发送、MaxReplyChars 仅总保险丝）→ stuck 审批卡片（每 Id 仅一次，PromptedApprovalIds 上限 10）→
/// MaxWaitHours 超时收尾。终态回复幂等（RepliedAt 已置不再跟进）；发送失败不自动重发。
/// 先例 ImGatewayConnectionHostedService：纯 IHostedService，不注入 IHostApplicationLifetime。
/// 全程吞异常：轮询/单行处理异常只记日志，绝不终止后台循环、绝不阻塞网关。
/// </summary>
public class ImTaskWatcher : IHostedService
{
    private readonly IContext _ctx;
    private readonly IConfigStore _configStore;
    private readonly DbSessionStore _sessionStore;
    private readonly ImGatewayMessageStore _messageStore = new();
    private readonly Dictionary<string, IImChannel> _channels;
    private readonly HashSet<long> _sendAttempted = new();     // 发送失败行：本进程内不自动重发
    private readonly Dictionary<long, string> _lastFingerprints = new();   // T16：绑定行上次进展指纹（status|stepIdx|stepCount）
    private readonly Dictionary<long, DateTime> _lastChangeAt = new();     // T16：上次变化时刻（无进展计时起点）
    private readonly HashSet<long> _stalledNotified = new();               // T16：已通知过卡住的行（幂等去重）
    /// <summary>测试缝：假时钟（默认 null → DateTime.Now）。T16 无变化计时用。</summary>
    internal Func<DateTime>? NowProvider;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public ImTaskWatcher(IContext ctx, IConfigStore configStore, DbSessionStore sessionStore,
        IEnumerable<IImChannel> channels)
    {
        _ctx = ctx;
        _configStore = configStore;
        _sessionStore = sessionStore;
        _channels = channels.ToDictionary(c => c.ChannelType, c => c);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = new CancellationTokenSource();
        XTrace.Log.Info("[ImTaskWatcher] 轮询已启动（间隔 {0}s，单轮扫描绑定行）", ReadPollInterval());
        _loop = Task.Run(() => RunLoopAsync(_cts.Token));
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try { _cts?.Cancel(); } catch { /* ignore */ }
        if (_loop != null)
        {
            try { await _loop; } catch { /* ignore */ }
        }
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(ct);
            }
            catch (Exception ex)
            {
                XTrace.Log.Error("[ImTaskWatcher] 轮询异常（不终止）: {0}", ex.Message);
            }
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(ReadPollInterval()), ct);
            }
            catch (TaskCanceledException) { break; }
        }
    }

    private int ReadPollInterval()
    {
        try
        {
            var tg = _configStore.Load().WeCom.TaskGateway;
                return Math.Clamp(tg?.PollIntervalSeconds ?? 60, 3, 3600);   // T16：默认 1 分钟节拍
        }
        catch
        {
                return 60;   // T16：配置读取异常时同样回默认 1 分钟
        }
    }

    /// <summary>单轮轮询：Enabled 关 → 零行为；扫 RepliedAt 为空（未回执）的绑定逐行处理。</summary>
    public async Task PollOnceAsync(CancellationToken ct)
    {
        var tg = _configStore.Load().WeCom.TaskGateway ?? new TaskGatewayConfig();
        if (!tg.Enabled) return;

        var rows = ImGatewayTask.FindAll(ImGatewayTask._.Id > 0)
            .Where(r => r.RepliedAt == null || r.RepliedAt == DateTime.MinValue)   // 未回执：MinValue/NULL 双形态兼容
            .ToList();
        XTrace.Log.Debug("[ImTaskWatcher] 本轮扫描未回执绑定 {0} 行", rows.Count);
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            if (_sendAttempted.Contains(row.Id)) continue;       // 发送失败：本进程不自动重发
            try
            {
                await ProcessRowAsync(row, tg, ct);
            }
            catch (Exception ex)
            {
                XTrace.Log.Error("[ImTaskWatcher] 处理绑定 {0} 异常: {1}", row.TaskKey, ex.Message);
            }
        }
    }

    private async Task ProcessRowAsync(ImGatewayTask row, TaskGatewayConfig tg, CancellationToken ct)
    {
        if (!_channels.TryGetValue(row.ChannelType, out var channel)) return;
        var dispatch = _ctx.Get<IAgentRunDispatch>();
        if (dispatch == null) return;

        // 查快照：优先 RunId（宿主重启后 taskKey 只存于已丢失的内存记录，数字键走 DB 投影），否则 taskKey
        var snapshot = row.RunId > 0
            ? await dispatch.FindAsync(row.RunId.ToString(), ct)
            : await dispatch.FindAsync(row.TaskKey, ct);
        snapshot ??= row.RunId > 0 ? await dispatch.FindAsync(row.TaskKey, ct) : null;

        if (snapshot == null)
        {
            // 规划期丢失窗口（既无 RunId 又查不到 taskKey）：记 unknown，不发臆测回复
            if (row.Status != "unknown")
            {
                row.Status = "unknown";
                row.Save();
                XTrace.Log.Warn("[ImTaskWatcher] taskKey={0} 无快照可查，标记 unknown", row.TaskKey);
            }
            return;
        }

        if (row.Status != snapshot.Status) row.Status = snapshot.Status;   // 状态回写（词表与任务中心一致）
        // RunId 回填（FR-4）：Dispatcher 落库时 RunId=0，此处捕获后补齐——宿主重启后按数字键投影恢复的前提
        if (snapshot.RunId > 0 && row.RunId != snapshot.RunId) row.RunId = snapshot.RunId.Value;

        // MaxWaitHours 超时收尾：非终态且超窗 → 回指引后不再跟进
        if (!snapshot.Terminal && (DateTime.Now - row.CreatedAt).TotalHours > tg.MaxWaitHours)
        {
            var label = snapshot.RunId > 0 ? snapshot.RunId.ToString() : ShortCode(row.TaskKey);
            if (await TrySendAsync(channel, row, $"任务 #{label} 仍在执行，请去任务中心查看"))
                row.RepliedAt = DateTime.Now;
            row.Save();
            return;
        }
        // T16②③：执行中 → 快照变化即推简短进展（可关）；无变化连续 StalledMinutes 分钟 → 卡住通知一次
        if (!snapshot.Terminal && snapshot.Status == "running")
        {
            await TrackProgressAsync(row, snapshot, channel, tg, ct);
        }


        // stuck → 审批卡片（每 Id 仅一次）
        if (snapshot.Status == "stuck")
            await PromptApprovalsAsync(row, snapshot, channel, tg, ct);

        // 终态 → FR-5 模板回复（幂等）
        if (snapshot.Terminal)
        {
            if (await TrySendInSegmentsAsync(channel, row, ComposeTerminalText(snapshot, tg), tg))
                row.RepliedAt = DateTime.Now;
        }

        row.Save();
    }
    // ---- 审批卡片（FR-9：每 Id 一次，上限 10） ----
    // ---- T16②③：执行进展跟踪（指纹变化即推 / 无变化计时判卡住） ----

    private async Task TrackProgressAsync(ImGatewayTask row, AgentRunDispatchSnapshot snapshot,
        IImChannel channel, TaskGatewayConfig tg, CancellationToken ct)
    {
        var fingerprint = $"{snapshot.Status}|{snapshot.CurrentStepIndex?.ToString() ?? "-"}|{snapshot.StepCount?.ToString() ?? "-"}";
        var had = _lastFingerprints.TryGetValue(row.Id, out var last);
        var changed = !had || last != fingerprint;
        if (changed)
        {
            _lastFingerprints[row.Id] = fingerprint;
            _lastChangeAt[row.Id] = Now();
            _stalledNotified.Remove(row.Id);               // 恢复有进展：清卡住通知标记，重开计时
            if (tg.ProgressPushEnabled && had)             // 首轮只建基线（防宿主重启轰炸）；开关关不推
            {
                var label0 = snapshot.RunId > 0 ? snapshot.RunId.ToString() : ShortCode(row.TaskKey);
                await TrySendAsync(channel, row, ComposeProgressText(snapshot, label0));
            }
            return;
        }
        // 无变化：连续 StalledMinutes 分钟 → 卡住通知（每行一次幂等）
        var started = _lastChangeAt.TryGetValue(row.Id, out var t0) ? t0 : row.CreatedAt;
        if ((Now() - started).TotalMinutes >= tg.StalledMinutes && _stalledNotified.Add(row.Id))
        {
            var label = row.RunId > 0 ? row.RunId.ToString() : ShortCode(row.TaskKey);
            await TrySendAsync(channel, row, ComposeStalledText(label, tg.StalledMinutes));
        }
    }

    /// <summary>进展简报（FR-5 模板家族：表情 + 任务 #label + 状态短语；简洁不刷屏）。</summary>

    private DateTime Now() => NowProvider?.Invoke() ?? DateTime.Now;
    internal static string ComposeProgressText(AgentRunDispatchSnapshot snapshot, string label)
    {
        var step = snapshot.CurrentStepIndex is { } idx ? $"（步骤 {idx + 1}{(snapshot.StepCount is { } count ? $"/{count}" : "")}）" : "";
        return $"▶ 任务 #{label} 执行中{step}";
    }

    /// <summary>无进展卡住通知（每行一次；与 T14 的 300s 无进展阈值对齐）。</summary>
    internal static string ComposeStalledText(string label, int stalledMinutes)
        => $"⏳ 任务 #{label} 已 {stalledMinutes} 分钟无进展，仍在执行中——可到任务中心查看或终止";



    private async Task PromptApprovalsAsync(ImGatewayTask row, AgentRunDispatchSnapshot snapshot,
        IImChannel channel, TaskGatewayConfig tg, CancellationToken ct)
    {
        var approvalService = _ctx.Get<IApprovalService>();
        if (approvalService == null) return;

        // Run 的审批会话 = imtask:{聊天会话Id}；聊天会话按通道键映射取（不改既有绑定）
        string imtaskSession;
        try
        {
            var session = _sessionStore.GetOrCreateSession(row.ChannelType, row.ConversationId, row.PlatformUserId, null, null);
            imtaskSession = $"imtask:{session.SessionId}";
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ImTaskWatcher] 会话映射失败 ChannelKey={0}: {1}", row.ChannelKey, ex.Message);
            return;
        }

        IReadOnlyList<ApprovalDto> approvals;
        try
        {
            approvals = await approvalService.ListBySessionAsync(imtaskSession, ct);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ImTaskWatcher] 审批列表查询失败 {0}: {1}", imtaskSession, ex.Message);
            return;
        }

        var prompted = (row.PromptedApprovalIds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet();
        var label = snapshot.RunId > 0 ? snapshot.RunId.ToString() : ShortCode(row.TaskKey);

        foreach (var a in approvals.Where(x => x.Status == nameof(ApprovalStatus.Pending) && !prompted.Contains(x.Id.ToString())).ToList())
        {
        var approveWords = string.Join("/", (tg.ApproveKeywords ?? []).Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()));
        var rejectWords = string.Join("/", (tg.RejectKeywords ?? []).Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()));
        var card = $"⏸ 任务 #{label} 等待审批\n工具：{a.ToolName}（风险 {a.RiskLevel}）\n{Truncate(a.Description, 200)}\n回复『同意 #{a.Id}』放行，『拒绝 #{a.Id}』拒绝\n（当前词表——同意：{approveWords}；拒绝：{rejectWords}，可回复编号如「同意 #{a.Id}」）";
            if (!await TrySendAsync(channel, row, card)) continue;   // 发送失败不记提示：下轮再试
            prompted.Add(a.Id.ToString());
            row.PromptedApprovalIds = string.Join(",", prompted.Take(10));   // 防膨胀上限 10
        }
    }

    // ---- FR-5 终态模板 ----

    private static string ComposeTerminalText(AgentRunDispatchSnapshot s, TaskGatewayConfig tg)
    {
        var label = s.RunId > 0 ? s.RunId.ToString() : ShortCode(s.TaskKey);
        var head = s.Status switch
        {
            "completed" => $"✅ 任务 #{label} 完成",
            "failed" => $"❌ 任务 #{label} 失败",
            "cancelled" => $"⛔ 任务 #{label} 已取消",
            _ => $"任务 #{label} {s.Status}",
        };
        if (string.IsNullOrWhiteSpace(s.ResultText)) return head;
        var text = s.ResultText!;
        return $"{head}\n{text}";
    }

    // ---- 工具 ----

    private static string ShortCode(string key) => key.Length >= 8 ? key[..8] : key;

    private static string Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    /// <summary>主动推送（不用可能过期的 ReplyReqId，ChatType 必填）。失败 → RecordError，返回 false 不自动重发。</summary>
    /// <summary>T12：超阈值分段顺序发送（每段独立发送、失败即停不置 RepliedAt；下轮全量重发）。</summary>
    private async Task<bool> TrySendInSegmentsAsync(IImChannel channel, ImGatewayTask row, string content, TaskGatewayConfig tg)
    {
        // T12：MaxReplyChars=总上限保险丝（0=关）——超限截断+尾注，语义从「截断点」变为「兜底」
        if (tg.MaxReplyChars > 0 && content.Length > tg.MaxReplyChars)
        {
            var label = row.RunId > 0 ? row.RunId.ToString() : ShortCode(row.TaskKey);
            content = content[..tg.MaxReplyChars] + $"…全文见任务中心 #{label}";
        }
        // 超过单条阈值 → 分段顺序发送（不并发，段间顺序保证）；段尾标 (i/n)
        if (content.Length <= tg.MaxCharsPerMessage)
        {
            return await TrySendAsync(channel, row, content);
        }
        var total = (int)Math.Ceiling(content.Length / (double)tg.MaxCharsPerMessage);
        for (var i = 0; i < total; i++)
        {
            var start = i * tg.MaxCharsPerMessage;
            var seg = content.Substring(start, Math.Min(tg.MaxCharsPerMessage, content.Length - start));
            if (!await TrySendAsync(channel, row, $"{seg}（{i + 1}/{total}）"))
            {
                return false;   // 失败即停：不置 RepliedAt，下轮全量重发（语义裁决=全量非续发，见 03-plan）
            }
        }
        return true;
    }


    private async Task<bool> TrySendAsync(IImChannel channel, ImGatewayTask row, string content)
    {
        try
        {
            await channel.SendAsync(new OutboundMessage
            {
                ChannelType = row.ChannelType,
                ConversationId = row.ConversationId,
                PlatformUserId = row.PlatformUserId,
                Content = content,
                ReplyReqId = null,                                // 主动推送语义
                ChatType = string.IsNullOrWhiteSpace(row.ChatType) ? "single" : row.ChatType,
            });
            XTrace.Log.Info("[ImTaskWatcher] 已推送任务回复 taskKey={0} RunId={1} 状态={2}", row.TaskKey, row.RunId, row.Status);
            _messageStore.RecordReply(row.ChannelKey, row.ChannelType, content, null);
            return true;
        }
        catch (Exception ex)
        {
            _messageStore.RecordError(row.ChannelKey, row.ChannelType, null, $"任务回复发送失败：{ex.Message}");
            XTrace.Log.Error("[ImTaskWatcher] 发送失败 taskKey={0}: {1}", row.TaskKey, ex.Message);
            return false;
        }
    }
}
