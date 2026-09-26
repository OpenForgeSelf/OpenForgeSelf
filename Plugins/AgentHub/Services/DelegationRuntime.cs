using System.Collections.Concurrent;
using System.Text.Json;
using ForgeSelf.Api.Plugins.AgentHub.Entities;
using ForgeSelf.Api.Plugins.AgentHub.Models;
using ForgeSelf.Api.Plugins.AgentHub.Profiles;
using NewLife;
using NewLife.Log;
using XCode;

namespace ForgeSelf.Api.Plugins.AgentHub.Services;

/// <summary>任务状态常量（与 DelegationTask.Status 落库值一致，改动须双端同步）。</summary>
public static class TaskStatus
{
    /// <summary>已入队，等待调度</summary>
    public const String Queued = "Queued";

    /// <summary>执行中</summary>
    public const String Running = "Running";

    /// <summary>等待人工审批（G2：此态不计入执行超时预算）</summary>
    public const String AwaitingPermission = "AwaitingPermission";

    /// <summary>成功</summary>
    public const String Succeeded = "Succeeded";

    /// <summary>失败</summary>
    public const String Failed = "Failed";

    /// <summary>已取消</summary>
    public const String Cancelled = "Cancelled";

    /// <summary>执行超时</summary>
    public const String Timeout = "Timeout";

    /// <summary>被中断（宿主重启时仍在运行的任务，G5）</summary>
    public const String Interrupted = "Interrupted";

    /// <summary>终态集合</summary>
    public static readonly String[] Terminal = [Succeeded, Failed, Cancelled, Timeout, Interrupted];

    /// <summary>是否终态</summary>
    /// <param name="status">状态</param>
    /// <returns>是否终态</returns>
    public static Boolean IsTerminal(String? status)
        => status != null && Terminal.Contains(status);
}

/// <summary>委派任务视图（供 API / 工具消费）。</summary>
public class DelegationTaskDto
{
    /// <summary>主键</summary>
    public Int32 Id { get; set; }

    /// <summary>对外任务标识</summary>
    public String TaskKey { get; set; } = String.Empty;

    /// <summary>目标 agent</summary>
    public Int32 AgentId { get; set; }

    /// <summary>目标 agent 名（冗余展示）</summary>
    public String? AgentName { get; set; }

    /// <summary>交互口</summary>
    public Int32 AccessPointId { get; set; }

    /// <summary>提示词</summary>
    public String Prompt { get; set; } = String.Empty;

    /// <summary>工作目录</summary>
    public String? Cwd { get; set; }

    /// <summary>状态</summary>
    public String Status { get; set; } = TaskStatus.Queued;

    /// <summary>外部会话 id</summary>
    public String? SessionRef { get; set; }

    /// <summary>权限模式</summary>
    public String PermissionMode { get; set; } = "read-only";

    /// <summary>退出码</summary>
    public Int32 ExitCode { get; set; }

    /// <summary>错误分类</summary>
    public String? ErrorCode { get; set; }

    /// <summary>结果文本</summary>
    public String? ResultText { get; set; }

    /// <summary>产物 JSON</summary>
    public String? ArtifactsJson { get; set; }

    /// <summary>用量 JSON</summary>
    public String? UsageJson { get; set; }

    /// <summary>发起方</summary>
    public String? CreatedBy { get; set; }

    /// <summary>开始时间</summary>
    public DateTime StartTime { get; set; }

    /// <summary>结束时间</summary>
    public DateTime EndTime { get; set; }

    /// <summary>执行耗时（毫秒，不含审批等待）</summary>
    public Int32 ElapsedMs { get; set; }

    /// <summary>事件水位（SSE 断线续读用）</summary>
    public Int32 LastSeq { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>错误信息（用于 tool 返回可读描述）</summary>
    public String? Message { get; set; }
}

/// <summary>创建委派任务的请求。</summary>
public class DelegationRequest
{
    /// <summary>提示词（必填）</summary>
    public String Prompt { get; set; } = String.Empty;

    /// <summary>目标 agent（为空则按 facet/tag 选路）</summary>
    public Int32? AgentId { get; set; }

    /// <summary>指定交互口（为空取默认）</summary>
    public Int32? AccessPointId { get; set; }

    /// <summary>工作目录（为空取 agent 默认）</summary>
    public String? Cwd { get; set; }

    /// <summary>权限模式（为空取 agent 默认）</summary>
    public String? PermissionMode { get; set; }

    /// <summary>续接的外部会话 id</summary>
    public String? SessionRef { get; set; }

    /// <summary>要求的能力面（F1_Driving 等，用于选路）</summary>
    public String? Facet { get; set; }

    /// <summary>要求的擅长标签（用于选路）</summary>
    public String? Tag { get; set; }

    /// <summary>发起方（ui|agent:xxx|workflow）</summary>
    public String? CreatedBy { get; set; }

    /// <summary>是否同步等待完成（工具调用场景为 true，UI 场景通常 false）</summary>
    public Boolean Wait { get; set; }
}

/// <summary>
/// 委派运行时（design §5）：状态机、并发调度、超时、取消、事件落库。
///
/// 关键约束：
/// - 状态机 `Queued → Running ⇄ AwaitingPermission → Succeeded/Failed/Cancelled/Timeout/Interrupted`；
/// - 并发按 agent 的 MaxConcurrency（默认 1）；全局串行调度避免抢工作区；
/// - G6：同一 cwd 互斥（同目录同时只跑一个任务）；
/// - G7：cwd 必须落在白名单，非白名单直接拒绝，不进进程；
/// - G5：宿主启动时把遗留的非终态任务标 Interrupted（进程已随宿主消失）。
/// </summary>
public class DelegationRuntime
{
    private readonly IAgentRegistry _registry;
    private readonly ProfileLoader _profiles;
    private readonly PermissionBroker _permissions;
    private readonly IAgentTransport _transport;

    /// <summary>正在执行的任务（taskId → 会话句柄）</summary>
    private static readonly ConcurrentDictionary<Int32, AgentSession> _running = new();

    /// <summary>正在占用的 cwd（G6 互斥）</summary>
    private static readonly ConcurrentDictionary<String, Int32> _busyCwds = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public DelegationRuntime(
        IAgentRegistry registry,
        ProfileLoader profiles,
        PermissionBroker permissions,
        IAgentTransport transport)
    {
        _registry = registry;
        _profiles = profiles;
        _permissions = permissions;
        _transport = transport;
    }

    /// <summary>
    /// 启动时收尾：把库里遗留的非终态任务标为 Interrupted（G5）。
    /// 理由：宿主重启后子进程已不存在，任务不可能还在跑；不标记会永远卡在 Running。
    /// </summary>
    /// <returns>被标记的任务数</returns>
    public Int32 RecoverOrphans()
    {
        var orphans = DelegationTask.FindAllUnfinished();
        if (orphans.Count == 0) return 0;

        foreach (var task in orphans)
        {
            task.Status = TaskStatus.Interrupted;
            task.ErrorCode = "host_restart";
            task.EndTime = DateTime.Now;
            task.Update();
        }

        XTrace.Log.Warn("[AgentHub] 宿主重启收尾：{0} 个遗留任务已标记为 Interrupted", orphans.Count);
        return orphans.Count;
    }

    /// <summary>
    /// 创建并执行一次委派（同步等待完成）。
    /// </summary>
    /// <param name="req">委派请求</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>任务 DTO</returns>
    public async Task<DelegationTaskDto> DelegateAsync(DelegationRequest req, CancellationToken ct = default)
    {
        var task = await CreateAsync(req);
        if (task.Status == TaskStatus.Failed) return task;   // 前置校验失败（选路/目录/权限）

        return await RunAsync(task.Id, ct);
    }

    /// <summary>
    /// 创建任务（入队），不做执行。UI 场景用这个 + SSE 观察。
    /// </summary>
    /// <param name="req">委派请求</param>
    /// <returns>任务 DTO（前置校验失败时 Status=Failed + Message）</returns>
    public Task<DelegationTaskDto> CreateAsync(DelegationRequest req)
    {
        if (req.Prompt.IsNullOrWhiteSpace())
        {
            return Task.FromResult(Fail("提示词不能为空"));
        }

        // 选路
        AgentDto? agent = null;
        if (req.AgentId is > 0)
        {
            agent = _registry.Get(req.AgentId.Value);
            if (agent == null) return Task.FromResult(Fail($"agent #{req.AgentId} 不存在"));
        }
        else
        {
            var candidates = _registry.SelectCandidates(req.Facet, req.Tag);
            agent = candidates.FirstOrDefault();
            if (agent == null) return Task.FromResult(Fail("没有可用的 agent 候选（检查启用状态与交互口）"));
        }

        if (!agent.Enabled) return Task.FromResult(Fail($"agent「{agent.Name}」已停用"));

        // 交互口
        var ap = req.AccessPointId is > 0
            ? agent.AccessPoints.FirstOrDefault(p => p.Id == req.AccessPointId.Value)
            : agent.AccessPoints.FirstOrDefault(p => p.IsDefault) ?? agent.AccessPoints.FirstOrDefault();

        if (ap == null) return Task.FromResult(Fail($"agent「{agent.Name}」没有可用交互口"));

        // 工作目录白名单校验（G7 前置）
        var cwd = !req.Cwd.IsNullOrEmpty() ? req.Cwd : agent.DefaultCwd;
        var cwdCheck = ValidateCwd(agent, cwd);
        if (cwdCheck != null) return Task.FromResult(Fail(cwdCheck));

        // 权限模式
        var mode = !req.PermissionMode.IsNullOrEmpty() ? req.PermissionMode : agent.Policy.PermissionMode;
        if (AgentPolicy.IsForbiddenMode(mode))
            return Task.FromResult(Fail($"权限模式「{mode}」被禁止（不得使用无限权限模式）"));

        var entity = new DelegationTask
        {
            AgentId = agent.Id,
            AccessPointId = ap.Id,
            Prompt = req.Prompt,
            Cwd = cwd,
            Status = TaskStatus.Queued,
            PermissionMode = mode,
            SessionRef = req.SessionRef,
            CreatedBy = req.CreatedBy ?? "ui"
        };

        entity.Insert();

        // 落队列事件（第一个 Seq）
        AppendEvent(entity, AgentEventTypes.Meta, JsonSerializer.Serialize(new
        {
            stage = "queued",
            agent = agent.Name,
            agentId = agent.Id,
            accessPointId = ap.Id,
            cwd,
            permissionMode = mode
        }));

        return Task.FromResult(ToDto(entity, agent.Name));
    }

    /// <summary>
    /// 执行已入队的任务（阻塞至终态）。
    /// </summary>
    /// <param name="taskId">任务主键</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>任务 DTO</returns>
    public async Task<DelegationTaskDto> RunAsync(Int32 taskId, CancellationToken ct = default)
    {
        var entity = DelegationTask.FindById(taskId);
        if (entity == null) return Fail($"任务 #{taskId} 不存在");

        if (TaskStatus.IsTerminal(entity.Status))
            return ToDto(entity, _registry.Get(entity.AgentId)?.Name);

        var agent = _registry.Get(entity.AgentId);
        if (agent == null)
        {
            Finish(entity, TaskStatus.Failed, "agent_missing", "目标 agent 已被删除");
            return ToDto(entity, null);
        }

        var ap = agent.AccessPoints.FirstOrDefault(p => p.Id == entity.AccessPointId)
                 ?? _registry.GetDefaultAccessPoint(entity.AgentId);

        if (ap == null)
        {
            Finish(entity, TaskStatus.Failed, "access_point_missing", "交互口已不存在");
            return ToDto(entity, agent.Name);
        }

        // G6：同 cwd 互斥
        var cwdKey = (entity.Cwd ?? String.Empty).TrimEnd('\\', '/');
        if (!cwdKey.IsNullOrEmpty() && !_busyCwds.TryAdd(cwdKey, taskId))
        {
            Finish(entity, TaskStatus.Failed, "cwd_busy",
                $"工作目录「{entity.Cwd}」已有任务在执行（避免多进程抢同一工作区）");
            return ToDto(entity, agent.Name);
        }

        var session = new AgentSession
        {
            TaskId = entity.Id,
            AccessPointId = ap.Id,
            Kind = _transport.Kind,
            SessionRef = entity.SessionRef,
            CancelSupported = ap.CancelSupported
        };

        _running[entity.Id] = session;

        // 进入 Running
        entity.Status = TaskStatus.Running;
        entity.StartTime = DateTime.Now;
        entity.Update();
        AppendEvent(entity, AgentEventTypes.Meta, """{"stage":"running"}""");

        var runReq = new AgentRunRequest
        {
            Prompt = entity.Prompt,
            Cwd = entity.Cwd,
            PermissionMode = entity.PermissionMode,
            SessionRef = entity.SessionRef,
            TimeoutMs = Math.Max(1, agent.Policy.TimeoutSeconds) * 1000
        };

        var resultText = new System.Text.StringBuilder();
        var artifacts = new List<Object>();
        var usage = new Dictionary<String, Object>();
        var finalStatus = TaskStatus.Succeeded;
        var errorCode = (String?)null;
        var errorMessage = (String?)null;
        var exitCode = 0;
        var approvalWaitMs = 0L;

        try
        {
            using var streamCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var stream = _transport.StreamAsync(ap, runReq, session, streamCts.Token);

            await foreach (var evt in stream.WithCancellation(streamCts.Token))
            {
                // 退出事件单独处理（不改状态，等循环自然结束）
                if (evt.Type == AgentEventTypes.Exit)
                {
                    exitCode = evt.ExitCode ?? 0;
                    if (!evt.Text.IsNullOrEmpty()) AppendEvent(entity, evt.Type, null, evt.Text);
                    continue;
                }

                // 结果文本累积（Text 事件）
                if (evt.Type == AgentEventTypes.Text && !evt.Text.IsNullOrEmpty())
                {
                    if (resultText.Length < 8000) resultText.AppendLine(evt.Text);
                }

                // 产物收集（文件变更 / diff）
                if (evt.Type is AgentEventTypes.FileChange or AgentEventTypes.Diff)
                {
                    artifacts.Add(new { type = evt.Type, tool = evt.Tool, text = evt.Text });
                }

                // 会话 id 回填（供续接）
                if (session.SessionRef != null && session.SessionRef != entity.SessionRef)
                {
                    entity.SessionRef = session.SessionRef;
                }

                // 错误事件：记录但不立即终止（进程可能自己恢复）
                if (evt.Type == AgentEventTypes.Error)
                {
                    errorMessage = evt.Text;
                }

                AppendEvent(entity, evt.Type, evt.Payload?.ToJsonString(), evt.Text, evt.Truncated, evt.ExitCode);
            }

            // 判终态：取消 > 超时 > 退出码
            if (ct.IsCancellationRequested)
            {
                finalStatus = TaskStatus.Cancelled;
                errorCode = "cancelled";
            }
            else if (exitCode != 0)
            {
                // 上游错误分类（见 design §5：timeout / upstream_error / auth_expired / spawn_failed）
                finalStatus = TaskStatus.Failed;
                errorCode = ClassifyError(errorMessage, exitCode);
            }
        }
        catch (OperationCanceledException)
        {
            finalStatus = TaskStatus.Cancelled;
            errorCode = "cancelled";
        }
        catch (Exception ex)
        {
            finalStatus = TaskStatus.Failed;
            errorCode = "spawn_failed";
            errorMessage = $"{ex.GetType().Name}: {ex.Message}";
            XTrace.Log.Error("[AgentHub] 任务 {0} 执行异常: {1}", entity.Id, ex.Message);
        }
        finally
        {
            _running.TryRemove(entity.Id, out _);
            if (!cwdKey.IsNullOrEmpty()) _busyCwds.TryRemove(cwdKey, out _);
            _permissions.CancelAll(entity.Id);

            // G2：执行耗时只算 Running 态，审批等待另记
            var elapsed = entity.StartTime.Year > 1
                ? (Int32)Math.Max(0, (DateTime.Now - entity.StartTime).TotalMilliseconds - approvalWaitMs)
                : 0;

            entity.ElapsedMs = elapsed;
            entity.ExitCode = exitCode;
            entity.ResultText = resultText.Length > 0 ? resultText.ToString()[..Math.Min(8000, resultText.Length)] : null;
            if (artifacts.Count > 0) entity.ArtifactsJson = JsonSerializer.Serialize(artifacts, JsonOpts)[..Math.Min(4000, JsonSerializer.Serialize(artifacts, JsonOpts).Length)];
            if (usage.Count > 0) entity.UsageJson = JsonSerializer.Serialize(usage, JsonOpts);
            entity.SessionRef = session.SessionRef ?? entity.SessionRef;

            Finish(entity, finalStatus, errorCode, errorMessage);
        }

        return ToDto(entity, agent.Name);
    }

    /// <summary>
    /// 取消任务：杀进程树（OneShot 的唯一手段）。
    /// </summary>
    /// <param name="taskId">任务主键</param>
    /// <returns>是否命中运行中的任务</returns>
    public async Task<Boolean> CancelAsync(Int32 taskId)
    {
        var entity = DelegationTask.FindById(taskId);
        if (entity == null) return false;

        if (TaskStatus.IsTerminal(entity.Status))
            return false;

        if (_running.TryGetValue(taskId, out var session))
        {
            await _transport.CancelAsync(session);
            _permissions.CancelAll(taskId);
            AppendEvent(entity, AgentEventTypes.Meta, """{"stage":"cancelling"}""", "用户请求取消，已终止进程树");
            return true;
        }

        // 尚未启动（Queued）：直接落终态
        Finish(entity, TaskStatus.Cancelled, "cancelled", "任务在出队前被取消");
        return true;
    }

    /// <summary>取某任务的事件列表（供详情页回放）。</summary>
    /// <param name="taskId">任务主键</param>
    /// <param name="fromSeq">起始序号（含；0 表示全部）</param>
    /// <returns>事件列表</returns>
    public static IReadOnlyList<DelegationEvent> ListEvents(Int32 taskId, Int32 fromSeq = 0)
        => fromSeq > 0
            ? DelegationEvent.FindAfterSeq(taskId, fromSeq).ToList()
            : DelegationEvent.FindAllByTaskId(taskId).ToList();

    /// <summary>按任务标识（TaskKey）取任务。</summary>
    /// <param name="taskKey">任务标识</param>
    /// <returns>DTO；不存在返回 null</returns>
    public DelegationTaskDto? GetByKey(String taskKey)
    {
        if (taskKey.IsNullOrEmpty()) return null;

        var entity = DelegationTask.FindByTaskKey(taskKey);
        return entity == null ? null : ToDto(entity, _registry.Get(entity.AgentId)?.Name);
    }

    /// <summary>按主键取任务。</summary>
    /// <param name="id">主键</param>
    /// <returns>DTO；不存在返回 null</returns>
    public DelegationTaskDto? Get(Int32 id)
    {
        var entity = DelegationTask.FindById(id);
        return entity == null ? null : ToDto(entity, _registry.Get(entity.AgentId)?.Name);
    }

    /// <summary>任务列表（可按状态过滤）。</summary>
    /// <param name="status">状态（空表示全部）</param>
    /// <param name="limit">条数上限</param>
    /// <returns>DTO 列表</returns>
    public IReadOnlyList<DelegationTaskDto> List(String? status, Int32 limit = 50)
    {
        var max = Math.Max(1, limit);
        var list = status.IsNullOrEmpty()
            ? DelegationTask.FindAll(null, DelegationTask._.Id.Desc(), null, 0, max)
            : DelegationTask.FindAll(DelegationTask._.Status == status, DelegationTask._.Id.Desc(), null, 0, max);

        var result = new List<DelegationTaskDto>(list.Count);
        foreach (var entity in list)
        {
            result.Add(ToDto(entity, _registry.Get(entity.AgentId)?.Name));
        }

        return result;
    }

    /// <summary>统计（供 UI 概览）。</summary>
    /// <returns>状态 → 数量</returns>
    public static Dictionary<String, Int32> Stats()
    {
        var stats = new Dictionary<String, Int32>();
        foreach (var status in new[] { TaskStatus.Queued, TaskStatus.Running, TaskStatus.AwaitingPermission })
        {
            stats[status] = (Int32)DelegationTask.FindCount(DelegationTask._.Status == status);
        }

        stats["Total"] = (Int32)DelegationTask.FindCount();
        return stats;
    }

    /// <summary>写入一条事件（自动累加 Seq 并更新任务水位）。</summary>
    /// <param name="task">任务实体</param>
    /// <param name="type">事件类型</param>
    /// <param name="payloadJson">载荷 JSON</param>
    /// <param name="text">文本（无 payload 时用）</param>
    /// <param name="truncated">是否截断</param>
    /// <param name="exitCode">退出码（Exit 事件用）</param>
    /// <returns>事件实体</returns>
    public static DelegationEvent AppendEvent(
        DelegationTask task,
        String type,
        String? payloadJson = null,
        String? text = null,
        Boolean truncated = false,
        Int32? exitCode = null)
    {
        var seq = DelegationEvent.GetMaxSeq(task.Id) + 1;

        var evt = new DelegationEvent
        {
            TaskId = task.Id,
            Seq = seq,
            Type = type,
            PayloadJson = payloadJson ?? (text != null ? JsonSerializer.Serialize(new { text }, JsonOpts) : null),
            Truncated = truncated
        };

        if (exitCode != null)
        {
            evt.PayloadJson = JsonSerializer.Serialize(new { text, exitCode }, JsonOpts);
        }

        evt.Insert();

        task.LastSeq = seq;
        task.Update();

        return evt;
    }

    /// <summary>写终态并落 Exit 事件。</summary>
    private static void Finish(DelegationTask entity, String status, String? errorCode, String? message)
    {
        entity.Status = status;
        entity.ErrorCode = errorCode;
        entity.EndTime = DateTime.Now;
        if (entity.StartTime.Year > 1 && entity.ElapsedMs == 0)
        {
            entity.ElapsedMs = (Int32)Math.Max(0, (entity.EndTime - entity.StartTime).TotalMilliseconds);
        }
        entity.Update();

        AppendEvent(entity, AgentEventTypes.Exit, JsonSerializer.Serialize(new
        {
            status,
            errorCode,
            message,
            exitCode = entity.ExitCode
        }), message);
    }

    /// <summary>工作目录白名单校验（G7：危险目录直接拒绝，不进进程）。</summary>
    /// <param name="agent">agent</param>
    /// <param name="cwd">目标目录</param>
    /// <returns>错误信息；通过返回 null</returns>
    private static String? ValidateCwd(AgentDto agent, String? cwd)
    {
        if (cwd.IsNullOrEmpty())
        {
            // 无 cwd 用进程当前目录，此时不给写权限，仅允许只读
            return null;
        }

        if (!Directory.Exists(cwd)) return $"工作目录不存在：{cwd}";

        var policy = agent.Policy;
        var allowed = policy.AllowedCwds.Count > 0
            ? policy.AllowedCwds
            : (!agent.DefaultCwd.IsNullOrEmpty() ? [agent.DefaultCwd] : []);

        if (allowed.Count == 0) return null;   // 未配置白名单 = 不限制（首版宽松，可后续收紧）

        var normalized = Path.GetFullPath(cwd).TrimEnd('\\', '/');
        foreach (var dir in allowed)
        {
            if (dir.IsNullOrEmpty()) continue;
            var root = Path.GetFullPath(dir).TrimEnd('\\', '/');
            if (normalized.Equals(root, StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        return $"工作目录「{cwd}」不在 agent「{agent.Name}」的白名单内";
    }

    /// <summary>上游错误分类（用于 UI 给出可操作提示）。</summary>
    private static String ClassifyError(String? message, Int32 exitCode)
    {
        if (message.IsNullOrEmpty()) return $"exit_{exitCode}";

        var m = message.ToLowerInvariant();
        if (m.Contains("not logged in") || m.Contains("unauthorized") || m.Contains("401") || m.Contains("api key"))
            return "auth_expired";
        if (m.Contains("timeout") || m.Contains("timed out")) return "timeout";
        if (m.Contains("no such file") || m.Contains("not found") || m.Contains("enoent")) return "spawn_failed";
        if (m.Contains("rate limit") || m.Contains("429") || m.Contains("overloaded")) return "upstream_busy";

        return "upstream_error";
    }

    /// <summary>实体 → DTO。</summary>
    private static DelegationTaskDto ToDto(DelegationTask e, String? agentName) => new()
    {
        Id = e.Id,
        TaskKey = e.TaskKey,
        AgentId = e.AgentId,
        AgentName = agentName,
        AccessPointId = e.AccessPointId,
        Prompt = e.Prompt,
        Cwd = e.Cwd,
        Status = e.Status,
        SessionRef = e.SessionRef,
        PermissionMode = e.PermissionMode,
        ExitCode = e.ExitCode,
        ErrorCode = e.ErrorCode,
        ResultText = e.ResultText,
        ArtifactsJson = e.ArtifactsJson,
        UsageJson = e.UsageJson,
        CreatedBy = e.CreatedBy,
        StartTime = e.StartTime,
        EndTime = e.EndTime,
        ElapsedMs = e.ElapsedMs,
        LastSeq = e.LastSeq,
        CreateTime = e.CreateTime,
        Message = e.ErrorCode.IsNullOrEmpty() ? null : $"[{e.ErrorCode}] {e.ErrorCode switch
        {
            "auth_expired" => "外部 agent 登录态失效，请先在终端重新登录",
            "spawn_failed" => "进程启动失败，检查可执行文件路径",
            "cwd_busy" => "该工作目录已有任务在执行",
            "timeout" => "执行超时",
            "cancelled" => "任务已取消",
            "host_restart" => "宿主重启导致中断",
            _ => "任务失败"
        }}"
    };

    /// <summary>构造一个「前置失败」的 DTO（不落库）。</summary>
    private static DelegationTaskDto Fail(String message) => new()
    {
        Status = TaskStatus.Failed,
        Message = message,
        CreateTime = DateTime.Now
    };
}
