using System.Text.Json;
using System.Text.Json.Serialization;

namespace ForgeSelf.Api.Plugins.AgentHub.Models;

/// <summary>
/// Agent 策略（落 AgentDefinition.PolicyJson）。
/// 权限默认 read-only，禁止把 yolo/danger-full-access 设为默认（见 design §6.1）。
/// </summary>
public class AgentPolicy
{
    /// <summary>默认权限模式（read-only|workspace-write|accept-edits）</summary>
    public String PermissionMode { get; set; } = "read-only";

    /// <summary>执行超时（秒，默认 600）</summary>
    public Int32 TimeoutSeconds { get; set; } = 600;

    /// <summary>审批超时（秒，默认 120，超时按拒绝）</summary>
    public Int32 ApprovalTimeoutSeconds { get; set; } = 120;

    /// <summary>该 agent 的并发上限（默认 1，避免多进程抢同一工作区）</summary>
    public Int32 MaxConcurrency { get; set; } = 1;

    /// <summary>允许的工作目录白名单（为空表示仅允许 AgentDefinition.DefaultCwd）</summary>
    public List<String> AllowedCwds { get; set; } = [];

    /// <summary>允许写操作的目录白名单（G7：危险目录默认禁写）</summary>
    public List<String> WritableCwds { get; set; } = [];

    /// <summary>是否允许重试（默认否——委派有副作用）</summary>
    public Boolean RetryOnTransient { get; set; }

    /// <summary>序列化为 JSON（落库）</summary>
    /// <returns>JSON 字符串</returns>
    public String ToJson() => JsonSerializer.Serialize(this, JsonOpts);

    /// <summary>从 JSON 反序列化（空/非法返回默认策略）</summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>策略</returns>
    public static AgentPolicy FromJson(String? json)
    {
        if (String.IsNullOrWhiteSpace(json)) return new AgentPolicy();

        try
        {
            return JsonSerializer.Deserialize<AgentPolicy>(json, JsonOpts) ?? new AgentPolicy();
        }
        catch
        {
            // 坏数据回落默认（安全侧）：读保持只读，不让脏配置把权限放开
            return new AgentPolicy();
        }
    }

    /// <summary>危险权限模式集合（任何情况下都不得成为默认值）</summary>
    public static readonly String[] ForbiddenModes = ["yolo", "danger-full-access", "dangerously-skip-permissions"];

    /// <summary>判断给定权限模式是否属于禁止设为默认的危险模式</summary>
    /// <param name="mode">权限模式</param>
    /// <returns>是否危险</returns>
    public static Boolean IsForbiddenMode(String? mode)
        => !String.IsNullOrWhiteSpace(mode)
           && ForbiddenModes.Any(m => String.Equals(m, mode.Trim(), StringComparison.OrdinalIgnoreCase));

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
