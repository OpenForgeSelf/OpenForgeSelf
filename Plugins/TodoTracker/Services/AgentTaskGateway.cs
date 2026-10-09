using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.TodoTracker.Services;

/// <summary>
/// 委派接缝消费侧实现（PILOT-054 · FR-6/BR-6）。
///
/// 接缝<b>每次现取</b>（<c>_ctx.Get&lt;IAgentDelegation&gt;()</c>）：提供方热重载后共享表条目会摘除，
/// 缓存成字段就是悬空引用（architecture-design 铁律 4）。缺席时一律走 <see cref="Missing"/> 分支，
/// 由控制器映射 503 + 原文 —— 绝不静默当成功，也绝不去直连别的插件的 HTTP 端口。
/// </summary>
public class AgentTaskGateway : IAgentTaskGateway
{
    /// <summary>接缝缺席时给用户的固定文案（控制器与界面共用，避免两套说法）。</summary>
    public const string SeamNotAvailableMessage =
        "未检测到 agent 委派能力（agent-hub 插件未安装或未启用），任务仍可手工下发给 agent 执行";

    private readonly IContext _ctx;

    public AgentTaskGateway(IContext ctx) => _ctx = ctx;

    private IAgentDelegation? Delegation => _ctx.Get<IAgentDelegation>();

    /// <inheritdoc />
    public bool IsAvailable => Delegation != null;

    /// <inheritdoc />
    public IReadOnlyList<AgentDelegationAgent> AvailableAgents()
    {
        var delegation = Delegation;
        if (delegation == null) return [];

        try
        {
            return delegation.ListAvailableAgents();
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[todo-tracker] 读取可用 agent 失败：{0}", ex.Message);
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<GatewayResult<AgentDelegationOutcome>> SubmitAsync(AgentDelegationRequest request)
    {
        var delegation = Delegation;
        if (delegation == null) return GatewayResult<AgentDelegationOutcome>.Missing(SeamNotAvailableMessage);
        if (request == null) return GatewayResult<AgentDelegationOutcome>.Failed("委派请求不能为空");

        try
        {
            var outcome = await delegation.SubmitAsync(request);
            if (outcome == null) return GatewayResult<AgentDelegationOutcome>.Failed("委派接缝返回空结果");

            return outcome.Success
                ? GatewayResult<AgentDelegationOutcome>.Ok(outcome)
                : GatewayResult<AgentDelegationOutcome>.Failed(outcome.Error ?? "委派失败");
        }
        catch (Exception ex)
        {
            // 提供方抛出的异常（DB 故障等）如实冒泡为可读失败，不伪装成"参数错"；细节进日志
            XTrace.Log.Error("[todo-tracker] 提交委派异常：{0}", ex.Message);
            return GatewayResult<AgentDelegationOutcome>.Failed($"提交委派失败：{ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<GatewayResult<AgentDelegationSnapshot>> Query(string taskKey)
    {
        var delegation = Delegation;
        if (delegation == null) return GatewayResult<AgentDelegationSnapshot>.Missing(SeamNotAvailableMessage);

        try
        {
            var snapshot = await delegation.FindAsync(taskKey);
            return snapshot == null
                ? GatewayResult<AgentDelegationSnapshot>.Failed($"委派任务不存在：{taskKey}")
                : GatewayResult<AgentDelegationSnapshot>.Ok(snapshot);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[todo-tracker] 读回委派状态异常：{0}", ex.Message);
            return GatewayResult<AgentDelegationSnapshot>.Failed($"读回委派状态失败：{ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<GatewayResult<bool>> MarkCompleted(string taskKey)
    {
        var delegation = Delegation;
        if (delegation == null) return GatewayResult<bool>.Missing(SeamNotAvailableMessage);

        try
        {
            var marked = await delegation.MarkCompletedAsync(taskKey);
            return GatewayResult<bool>.Ok(marked);
        }
        catch (Exception ex)
        {
            // 标记失败不阻断回报主链路：记录在案即可（下次轮询仍可见真实状态）
            XTrace.Log.Warn("[todo-tracker] 标记委派完成异常（taskKey={0}）：{1}", taskKey, ex.Message);
            return GatewayResult<bool>.Failed($"标记委派完成失败：{ex.Message}");
        }
    }
}
