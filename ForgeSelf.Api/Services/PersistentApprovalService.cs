using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Entities;
using NewLife.Log;

namespace ForgeSelf.Api.Services;

/// <summary>
/// 可持久化审批服务（替换 <see cref="NoopApprovalService"/>）：XCode 实体 + 内存等待闸门。
/// Ask 路径阻塞等待决策；MAF 路径 CreatePending 后挂起回合，Decide 后以续跑条目喂回模型。
/// </summary>
public sealed class PersistentApprovalService : IApprovalService
{
    private readonly ConcurrentDictionary<long, TaskCompletionSource<bool>> _waiters = new();
    private readonly object _writeGate = new();
    private int _initialized;
    private readonly object _initializeGate = new();

    /// <inheritdoc />
    public async Task<bool> AskAsync(string sessionId, string toolName, string argsJson, string? reason, CancellationToken ct)
    {
        var dto = await CreatePendingAsync(new ApprovalCreateRequest
        {
            SessionId = sessionId ?? string.Empty,
            ToolName = toolName ?? string.Empty,
            ArgsJson = string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson,
            Description = reason ?? $"工具 {toolName} 需要审批",
            RiskLevel = HighRiskToolPolicy.RiskHigh,
            Timeout = HighRiskToolPolicy.DefaultTimeout,
        }, ct).ConfigureAwait(false);

        var tcs = _waiters.GetOrAdd(dto.Id, _ => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));

        var current = FindRow(dto.Id);
        if (current != null && !IsPending(current.Status))
        {
            _waiters.TryRemove(dto.Id, out _);
            return string.Equals(current.Status, nameof(ApprovalStatus.Approved), StringComparison.OrdinalIgnoreCase);
        }

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var remain = dto.ExpiresAt - DateTimeOffset.UtcNow;
            if (remain <= TimeSpan.Zero)
            {
                Expire(dto.Id);
                return false;
            }

            linked.CancelAfter(remain);
            return await tcs.Task.WaitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Expire(dto.Id);
            XTrace.Log.Warn("[PersistentApprovalService] Ask 超时/取消 fail-closed: id={0} tool={1}", dto.Id, toolName);
            return false;
        }
        finally
        {
            _waiters.TryRemove(dto.Id, out _);
        }
    }

    /// <inheritdoc />
    public Task<ApprovalDto> CreatePendingAsync(ApprovalCreateRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();
        EnsureTable();

        var now = DateTime.UtcNow;
        var timeout = request.Timeout ?? HighRiskToolPolicy.DefaultTimeout;
        var argsJson = NormalizeArgsJson(request.ArgsJson);
        var row = new ToolApproval
        {
            SessionId = request.SessionId ?? string.Empty,
            ToolName = request.ToolName ?? string.Empty,
            Arguments = argsJson,
            ArgumentsHash = HashArgs(argsJson),
            RiskLevel = string.IsNullOrWhiteSpace(request.RiskLevel) ? HighRiskToolPolicy.RiskHigh : request.RiskLevel,
            Description = request.Description ?? string.Empty,
            Status = nameof(ApprovalStatus.Pending),
            CreatedAt = now,
            ExpiresAt = now.Add(timeout),
            MeaiRequestId = request.MeaiRequestId,
            CallId = request.CallId,
            ContinuationConsumed = false,
        };

        lock (_writeGate)
        {
            row.Insert();
        }

        XTrace.Log.Info("[PersistentApprovalService] 创建 Pending 审批 id={0} session={1} tool={2}",
            row.Id, row.SessionId, row.ToolName);
        return Task.FromResult(ToDto(row));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ApprovalDto>> ListBySessionAsync(string sessionId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(sessionId))
            return Task.FromResult<IReadOnlyList<ApprovalDto>>(Array.Empty<ApprovalDto>());

        EnsureTable();
        ExpireOverdueForSession(sessionId);
        var rows = ToolApproval.FindAllBySessionIdOrdered(sessionId);
        return Task.FromResult<IReadOnlyList<ApprovalDto>>(rows.Select(ToDto).ToList());
    }

    /// <inheritdoc />
    public Task<ApprovalDto?> DecideAsync(long id, bool approved, string? note, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        EnsureTable();

        ToolApproval? row;
        lock (_writeGate)
        {
            row = ToolApproval.FindById(id);
            if (row == null) return Task.FromResult<ApprovalDto?>(null);

            ExpireIfNeeded(row);
            if (!IsPending(row.Status))
            {
                return Task.FromResult<ApprovalDto?>(ToDto(row));
            }

            row.Status = approved ? nameof(ApprovalStatus.Approved) : nameof(ApprovalStatus.Denied);
            row.Note = note;
            row.Update();
        }

        if (_waiters.TryGetValue(id, out var tcs))
        {
            tcs.TrySetResult(approved);
        }

        XTrace.Log.Info("[PersistentApprovalService] 决策 id={0} approved={1}", id, approved);
        return Task.FromResult<ApprovalDto?>(ToDto(row));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ApprovalContinuation>> ConsumeContinuationsAsync(string sessionId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(sessionId))
            return Task.FromResult<IReadOnlyList<ApprovalContinuation>>(Array.Empty<ApprovalContinuation>());

        EnsureTable();
        var result = new List<ApprovalContinuation>();
        lock (_writeGate)
        {
            var rows = ToolApproval.FindAllBySessionIdOrdered(sessionId)
                .Where(r => !r.ContinuationConsumed
                    && (string.Equals(r.Status, nameof(ApprovalStatus.Approved), StringComparison.OrdinalIgnoreCase)
                        || string.Equals(r.Status, nameof(ApprovalStatus.Denied), StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var row in rows)
            {
                row.ContinuationConsumed = true;
                row.Update();
                result.Add(new ApprovalContinuation
                {
                    Id = row.Id,
                    SessionId = row.SessionId,
                    ToolName = row.ToolName,
                    ArgsJson = row.Arguments,
                    Approved = string.Equals(row.Status, nameof(ApprovalStatus.Approved), StringComparison.OrdinalIgnoreCase),
                    Note = row.Note,
                    MeaiRequestId = row.MeaiRequestId ?? $"approval-{row.Id}",
                    CallId = row.CallId ?? $"call-{row.Id}",
                });
            }
        }

        return Task.FromResult<IReadOnlyList<ApprovalContinuation>>(result);
    }

    private void EnsureTable()
    {
        if (Volatile.Read(ref _initialized) == 1) return;
        lock (_initializeGate)
        {
            if (Volatile.Read(ref _initialized) == 1) return;
            for (var attempt = 0; attempt < 6; attempt++)
            {
                if (ToolApproval.EnsureCreated())
                {
                    Volatile.Write(ref _initialized, 1);
                    return;
                }
                if (attempt < 5) Thread.Sleep(100 * (attempt + 1));
            }
            throw new InvalidOperationException("ToolApproval 表初始化失败");
        }
    }

    private static ToolApproval? FindRow(long id)
    {
        try { return ToolApproval.FindById(id); }
        catch { return null; }
    }

    private void Expire(long id)
    {
        lock (_writeGate)
        {
            var row = ToolApproval.FindById(id);
            if (row == null || !IsPending(row.Status)) return;
            row.Status = nameof(ApprovalStatus.Expired);
            row.Update();
        }
        if (_waiters.TryGetValue(id, out var tcs))
            tcs.TrySetResult(false);
    }

    private void ExpireOverdueForSession(string sessionId)
    {
        var now = DateTime.UtcNow;
        lock (_writeGate)
        {
            foreach (var row in ToolApproval.FindAllBySessionIdOrdered(sessionId))
            {
                if (IsPending(row.Status) && row.ExpiresAt <= now)
                {
                    row.Status = nameof(ApprovalStatus.Expired);
                    row.Update();
                    if (_waiters.TryGetValue(row.Id, out var tcs))
                        tcs.TrySetResult(false);
                }
            }
        }
    }

    private static void ExpireIfNeeded(ToolApproval row)
    {
        if (IsPending(row.Status) && row.ExpiresAt <= DateTime.UtcNow)
        {
            row.Status = nameof(ApprovalStatus.Expired);
            row.Update();
        }
    }

    private static bool IsPending(string? status)
        => string.Equals(status, nameof(ApprovalStatus.Pending), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeArgsJson(string? argsJson)
    {
        if (string.IsNullOrWhiteSpace(argsJson)) return "{}";
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            return JsonSerializer.Serialize(doc.RootElement, ForgeJson.Default);
        }
        catch
        {
            return argsJson;
        }
    }

    private static string HashArgs(string argsJson)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(argsJson ?? "{}"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static ApprovalDto ToDto(ToolApproval row) => new()
    {
        Id = row.Id,
        SessionId = row.SessionId,
        ToolName = row.ToolName,
        ArgsJson = row.Arguments,
        ArgsHash = row.ArgumentsHash,
        RiskLevel = row.RiskLevel,
        Description = row.Description ?? string.Empty,
        Status = row.Status,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc)),
        ExpiresAt = new DateTimeOffset(DateTime.SpecifyKind(row.ExpiresAt, DateTimeKind.Utc)),
        Note = row.Note,
        MeaiRequestId = row.MeaiRequestId,
        CallId = row.CallId,
    };
}
