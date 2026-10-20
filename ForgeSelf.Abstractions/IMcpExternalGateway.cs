namespace ForgeSelf.Abstractions;

/// <summary>
/// 外部 MCP 服务器能力契约（L1 契约下沉，插件→插件接缝）：
/// 提供方（McpCenter）在 <c>Apply</c> 内 eager 构造并 <c>IContext.Register&lt;IMcpExternalGateway&gt;()</c> 写入 root 共享表；
/// 消费方（AIAgent 的 universal_tool、McpCenter 自身的 list_tools 枚举出口）每次经 <c>IContext.Get&lt;IMcpExternalGateway&gt;()</c>
/// 解析，<b>禁止缓存为字段</b>（提供方热重载/卸载时条目随 Fiber 摘除，缓存会悬空）；解析为 null 即走降级分支。
/// 用途：让已连接的外部 MCP 工具「可发现（进 list_tools 枚举）+ 可调用（按全名直调）」，
/// 但外部工具<b>不注册进宿主 IToolRegistry</b>（决策 D10 的注册表隔离保持，合并只发生在枚举出口）。
/// </summary>
public interface IMcpExternalGateway
{
    /// <summary>
    /// 已连接外部服务器的全部工具（同步、零 IO——取连接会话的 tools/list 缓存快照）。
    /// 未连接/禁用中的服务器不出现（避免「列出来却调不了」）；单项异常由提供方内部吞掉并记日志，
    /// 发现入口保持宽松语义（宁缺勿崩）。
    /// </summary>
    IReadOnlyList<McpExternalToolInfo> ListConnectedTools();

    /// <summary>
    /// 按完整转发名调用外部工具，返回目标工具结果文本原文。
    /// </summary>
    /// <param name="fullName">形如 <c>mcp.&lt;服务器id&gt;.&lt;工具名&gt;</c> 的全名（含 <c>mcp.</c> 前缀）。</param>
    /// <param name="argumentsJson">目标工具参数 JSON 原文（按目标工具 schema；无参传 <c>{}</c>）。</param>
    /// <param name="ct">调用方取消令牌。</param>
    /// <exception cref="Exception">全名格式非法 / 服务器未连接 / 传输失败——
    /// 异常消息为面向用户的中文说明，由消费方按各自错误封套呈现（不在此层包装）。
    /// 注：目标工具名不在清单**不抛异常**——透传远端，结果以远端错误负载原样返回（与直调远端语义一致）。</exception>
    Task<string> InvokeAsync(string fullName, string argumentsJson, CancellationToken ct = default);
}

/// <summary>
/// 外部 MCP 工具清单项（跨插件传递用，与 McpCenter 内部 DTO 解耦）。
/// <paramref name="FullName"/> 即可直接作为 universal_tool 的 <c>tool</c> 参数；
/// <paramref name="ServerId"/> 供枚举侧回填 pluginId；<paramref name="InputSchemaJson"/> 为输入 schema 原文（无则空串）。
/// </summary>
public sealed record McpExternalToolInfo(
    string FullName,
    string ServerId,
    string Name,
    string Description,
    string InputSchemaJson);

/// <summary>
/// 外部 MCP 工具全名命名规则（跨插件单一真源）：提供方拼名、网关与消费方拆名均走本类，
/// 避免两个消费侧（McpCenter 网关转发器 / AIAgent universal_tool）各自实现拆分而逐步分叉。
/// 形状：<c>mcp.&lt;服务器id&gt;.&lt;工具名&gt;</c>（命名空间段内不含点，故前缀后的首个点即分隔点）。
/// </summary>
public static class McpExternalToolNames
{
    /// <summary>外部工具命名空间前缀。</summary>
    public const string Prefix = "mcp.";

    /// <summary>拼装全名（服务器 id 为 kebab-case，非显示名）。</summary>
    public static string BuildFullName(string serverId, string toolName) => $"{Prefix}{serverId}.{toolName}";

    /// <summary>拆解全名；前缀缺失 / 缺段 / 空段均判非法（返回 false，输出置空）。</summary>
    public static bool TrySplit(string? fullName, out string serverId, out string toolName)
    {
        serverId = string.Empty;
        toolName = string.Empty;
        if (string.IsNullOrWhiteSpace(fullName) || !fullName.StartsWith(Prefix, StringComparison.Ordinal))
            return false;
        var rest = fullName[Prefix.Length..];
        var dotIdx = rest.IndexOf('.');
        if (dotIdx <= 0 || dotIdx == rest.Length - 1)
            return false;
        serverId = rest[..dotIdx];
        toolName = rest[(dotIdx + 1)..];
        return true;
    }
}
