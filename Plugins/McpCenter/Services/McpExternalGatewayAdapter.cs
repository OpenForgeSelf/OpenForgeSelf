using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.McpCenter.Services.McpClient;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.McpCenter.Services;

/// <summary>
/// <see cref="IMcpExternalGateway"/> 的 McpCenter 实现（v2.6.0）：薄适配，只把
/// <see cref="McpClientManager"/> 已有的连接会话快照与转发能力包成跨插件契约，不新增任何连接/调用逻辑。
/// eager 构造于 <c>McpCenterPlugin.Apply</c>（仅依赖同为 eager 单例的 Manager，不持宿主 Scoped 服务——防 captive dependency），
/// 经 <c>ctx.Register&lt;IMcpExternalGateway&gt;()</c> 提供到 root 共享表，随插件 Fiber 回滚自动摘除（调研 §5.6 裁决 A/F）。
/// internal：只由本插件注册，消费方一律经契约访问（测试经 InternalsVisibleTo 触达）。
/// </summary>
internal sealed class McpExternalGatewayAdapter : IMcpExternalGateway
{
    private readonly McpClientManager _clientManager;

    public McpExternalGatewayAdapter(McpClientManager clientManager)
    {
        _clientManager = clientManager;
    }

    /// <summary>
    /// 已连接服务器的工具清单：直接遍历 <see cref="McpClientManager.ConnectedSessions"/> 内存会话快照
    /// （字典本身只含已连会话，零 IO、不读盘、不脱敏——契约不变量，CodeReview PILOT-072 Issue 1），
    /// 逐会话取 tools/list 缓存映射为契约项。单服务器异常跳过并记 Warn——发现入口宁缺勿崩（Spec BC-4）。
    /// </summary>
    public IReadOnlyList<McpExternalToolInfo> ListConnectedTools()
    {
        var result = new List<McpExternalToolInfo>();
        foreach (var session in _clientManager.ConnectedSessions())
        {
            var serverId = session.ServerId;
            try
            {
                foreach (var tool in session.GetTools())
                {
                    result.Add(new McpExternalToolInfo(
                        tool.FullName, tool.ServerId, tool.Name, tool.Description, tool.InputSchemaJson));
                }
            }
            catch (Exception ex)
            {
                XTrace.Log.Warn("[McpCenter] 枚举外部服务器 '{0}' 工具清单失败（跳过该服务器）: {1}", serverId, ex.Message);
            }
        }
        return result;
    }

    /// <summary>按全名直调外部工具（全名解析与网关转发器同源，错误消息沿用中文语义并以异常抛给消费方包装）。</summary>
    public async Task<string> InvokeAsync(string fullName, string argumentsJson, CancellationToken ct = default)
    {
        if (!McpExternalToolNames.TrySplit(fullName, out var serverId, out var toolName))
        {
            throw new McpClientException(
                $"外部工具名格式应为 mcp.<服务器id>.<工具名>（当前: {fullName}；例如 mcp.deepwiki.search）");
        }
        return await _clientManager.CallExternalAsync(serverId, toolName, argumentsJson, ct);
    }
}
