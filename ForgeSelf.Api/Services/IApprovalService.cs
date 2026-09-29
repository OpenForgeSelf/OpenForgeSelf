namespace ForgeSelf.Api.Services;

/// <summary>
/// 工具审批服务（B8/A1 的 Ask 落点）：pre-execute 返回 <c>PreToolDecision.Ask</c> 时，
/// 管线调用本服务征询人工批准。
/// </summary>
/// <remarks>
/// 红线 4：实现<b>必须</b>携带 <paramref name="ct"/> 并自带超时，超时/取消/无响应一律返回 false
/// （fail-closed），绝不挂死 agent loop。宿主默认注册 <see cref="NoopApprovalService"/>（恒 false）。
/// </remarks>
public interface IApprovalService
{
    /// <summary>
    /// 征询审批：返回 true = 放行；false = 拒绝（含超时/取消/服务不可用，fail-closed）。
    /// </summary>
    /// <param name="sessionId">发起会话 ID（可为空串：旧单工具入口）。</param>
    /// <param name="toolName">工具名。</param>
    /// <param name="argsJson">工具参数 JSON。</param>
    /// <param name="reason">pre-execute 监听器给出的审批原因（可空）。</param>
    /// <param name="ct">取消令牌（超时 fail-closed 的兜底）。</param>
    Task<bool> AskAsync(string sessionId, string toolName, string argsJson, string? reason, CancellationToken ct);
}
