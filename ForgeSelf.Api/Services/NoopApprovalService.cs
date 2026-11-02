using ForgeSelf.Abstractions;
using NewLife.Log;

namespace ForgeSelf.Api.Services;

/// <summary>
/// 占位审批服务：无持久化通道时的 fail-closed 实现——Ask 恒拒绝。
/// 生产默认已改为 <see cref="PersistentApprovalService"/>；本类保留给测试/降级。
/// </summary>
public sealed class NoopApprovalService : IApprovalService
{
    /// <inheritdoc />
    public Task<bool> AskAsync(string sessionId, string toolName, string argsJson, string? reason, CancellationToken ct)
    {
        _ = ct;
        XTrace.Log.Info("[NoopApprovalService] Ask 审批被占位实现拒绝（fail-closed）: tool={0}, session={1}, reason={2}",
            toolName, sessionId, reason ?? "(无)");
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<ApprovalDto> CreatePendingAsync(ApprovalCreateRequest request, CancellationToken ct = default)
    {
        _ = ct;
        var now = DateTimeOffset.UtcNow;
        return Task.FromResult(new ApprovalDto
        {
            Id = 0,
            SessionId = request.SessionId,
            ToolName = request.ToolName,
            ArgsJson = request.ArgsJson,
            ArgsHash = string.Empty,
            RiskLevel = request.RiskLevel,
            Description = request.Description ?? string.Empty,
            Status = nameof(ApprovalStatus.Denied),
            CreatedAt = now,
            ExpiresAt = now,
            MeaiRequestId = request.MeaiRequestId,
            CallId = request.CallId,
        });
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ApprovalDto>> ListBySessionAsync(string sessionId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ApprovalDto>>(Array.Empty<ApprovalDto>());

    /// <inheritdoc />
    public Task<ApprovalDto?> DecideAsync(long id, bool approved, string? note, CancellationToken ct = default)
        => Task.FromResult<ApprovalDto?>(null);

    /// <inheritdoc />
    public Task<IReadOnlyList<ApprovalContinuation>> ConsumeContinuationsAsync(string sessionId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ApprovalContinuation>>(Array.Empty<ApprovalContinuation>());
}
