using System.Text.Json;
using System.Text.Json.Serialization;

namespace ForgeSelf.Api.Plugins.AgentHub.Models;

/// <summary>
/// 能力面（Facet）标识常量。见 design §15：能力矩阵六格，每格要么支持要么显式不支持。
/// </summary>
public static class AgentFacets
{
    /// <summary>F1 委派执行（one-shot / 会话）</summary>
    public const String Driving = "F1_Driving";

    /// <summary>F2 会话可枚举、可导出、可续接</summary>
    public const String Sessions = "F2_Sessions";

    /// <summary>F3 可读取并归一化历史（含 token/工具统计）</summary>
    public const String Transcripts = "F3_Transcripts";

    /// <summary>F4 安装、更新、登录、MCP/skills/hooks 管理</summary>
    public const String Configure = "F4_Configure";

    /// <summary>F5 版本、登录态、额度、体积、模型清单</summary>
    public const String HealthUsage = "F5_HealthUsage";

    /// <summary>F6 权限模式、沙箱、并发、预算</summary>
    public const String Policy = "F6_Policy";

    /// <summary>六个能力面的固定顺序（UI 按此渲染，保证列序稳定）</summary>
    public static readonly String[] All =
    [
        Driving, Sessions, Transcripts, Configure, HealthUsage, Policy
    ];

    /// <summary>取能力面的中文短名（用于 UI 与错误提示）</summary>
    /// <param name="facet">能力面标识</param>
    /// <returns>中文短名</returns>
    public static String DisplayName(String facet) => facet switch
    {
        Driving => "委派执行",
        Sessions => "会话管理",
        Transcripts => "历史读取",
        Configure => "配置管理",
        HealthUsage => "健康用量",
        Policy => "权限策略",
        _ => facet
    };
}

/// <summary>
/// 能力矩阵：六个能力面的显式声明。
/// 设计要旨：**每格要么 ✅ 要么显式 ❌**——不支持必须显式声明，不静默假装可用。
/// 缺省的格子按「不支持」处理（安全默认）。
/// </summary>
public class AgentCapabilityMatrix
{
    /// <summary>逐格支持标记（键为 <see cref="AgentFacets"/> 中的标识）</summary>
    public Dictionary<String, Boolean> Facets { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>该能力面拿不到证据时的备注（如「待验证」）</summary>
    public Dictionary<String, String> Notes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>是否支持指定能力面（未声明视为不支持）</summary>
    /// <param name="facet">能力面标识</param>
    /// <returns>是否支持</returns>
    public Boolean Supports(String facet)
        => Facets.TryGetValue(facet, out var ok) && ok;

    /// <summary>取指定能力面的备注</summary>
    /// <param name="facet">能力面标识</param>
    /// <returns>备注；无则返回 null</returns>
    public String? NoteOf(String facet) => Notes.TryGetValue(facet, out var note) ? note : null;

    /// <summary>序列化为 JSON（落库用，驼峰命名）</summary>
    /// <returns>JSON 字符串</returns>
    public String ToJson() => JsonSerializer.Serialize(this, JsonOpts);

    /// <summary>从 JSON 反序列化（空/非法返回全不支持的空矩阵，不抛）</summary>
    /// <param name="json">JSON 字符串</param>
    /// <returns>能力矩阵</returns>
    public static AgentCapabilityMatrix FromJson(String? json)
    {
        if (String.IsNullOrWhiteSpace(json)) return new AgentCapabilityMatrix();

        try
        {
            return JsonSerializer.Deserialize<AgentCapabilityMatrix>(json, JsonOpts) ?? new AgentCapabilityMatrix();
        }
        catch
        {
            // 坏数据不炸流程：返回空矩阵（全不支持 = 安全默认），调用方可经 UI 重设
            return new AgentCapabilityMatrix();
        }
    }

    /// <summary>由简单标记字典构造（用于从 profile 转换）</summary>
    /// <param name="facets">标记字典（键为 profile 里的能力面名）</param>
    /// <returns>能力矩阵</returns>
    public static AgentCapabilityMatrix FromFlags(IDictionary<String, Boolean>? facets)
    {
        var matrix = new AgentCapabilityMatrix();
        if (facets == null) return matrix;

        foreach (var (key, value) in facets)
        {
            matrix.Facets[key] = value;
        }
        return matrix;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
