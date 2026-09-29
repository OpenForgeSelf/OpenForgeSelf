using NewLife.Log;

namespace ForgeSelf.Api.Services;

/// <summary>
/// 默认审批服务：无审批 UI/人工通道时的占位实现——<b>恒拒绝</b>（fail-closed，A1）。
/// </summary>
/// <remarks>
/// 宿主默认注册本实现：任何 <c>PreToolDecision.Ask</c> 在没有真实审批服务替换它之前一律拒绝，
/// 保证「需要审批」语义在无审批能力时不会静默放行。真实审批服务就绪后经 DI 替换本注册即可。
/// </remarks>
public sealed class NoopApprovalService : IApprovalService
{
    /// <inheritdoc />
    public Task<bool> AskAsync(string sessionId, string toolName, string argsJson, string? reason, CancellationToken ct)
    {
        // fail-closed：占位实现永不放行。ct 仅为契约完整性保留（本实现同步返回，无需等待）。
        _ = ct;
        XTrace.Log.Info("[NoopApprovalService] Ask 审批被占位实现拒绝（fail-closed）: tool={0}, session={1}, reason={2}",
            toolName, sessionId, reason ?? "(无)");
        return Task.FromResult(false);
    }
}
