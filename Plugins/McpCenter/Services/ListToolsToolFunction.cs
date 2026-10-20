using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.McpCenter.Services;

/// <summary>
/// 工具清单枚举工具（v2.1.0 网关能力）：列出本项目全部可用工具（名称/说明/参数 schema），
/// 作为万能工具（universal_tool）的「发现工具」入口——调用方先调
/// <c>universal_tool {tool:"list_tools"}</c> 看有哪些工具可用，再按名调用。
/// 数据源（v2.6.0 合并）：① 宿主 <c>IToolRegistry.GetAllTools()</c> 实时快照（条目 <c>source="registry"</c>）
/// + ② 已连接外部 MCP 服务器的工具（<see cref="IMcpExternalGateway.ListConnectedTools"/>，条目名即
/// <c>mcp.&lt;服务器id&gt;.&lt;工具名&gt;</c> 全名，<c>source="mcp-external"</c>、<c>pluginId</c>=服务器 id）。
/// 外部工具仍**不注册进宿主注册表**（决策 D10 隔离保持），合并不发生在注册表而在本枚举出口——
/// 因此网关与内置 AIAgent 两个消费侧看到的 list_tools 是同一份合并结果。
/// 本工具只枚举、不执行业务。契约解析为 null（McpCenter 未启用/已卸载）时自动回退为仅列宿主工具。
/// 注册方式（与 AIAgent 同模式）：McpCenterPlugin.ToolExtensions 属性暴露 → 宿主
/// ExtensionPointManager.DiscoverExtensionsFromPlugin 自动注册进 ToolRegistry，
/// 热重载时 RemovePluginExtensions 自动注销（勿在 Apply 里手动 RegisterTool——
/// 插件装配期 ctx.Get&lt;IToolRegistry&gt;() 为 null，运行期才可解析，见 AIChatController 注释）。
/// </summary>
public sealed class ListToolsToolFunction : IToolFunctionExtension
{
    public const string ToolName = "list_tools";
    public const string ToolId = "mcp-center-list-tools";

    private readonly IContext? _ctx;

    public ListToolsToolFunction(IContext? ctx)
    {
        _ctx = ctx;
    }

    public string Id => ToolId;
    public string Name => ToolName;
    public string PluginId => "mcp-center";

    public string Description =>
        "列出本项目全部可用工具（宿主注册工具 + 已连接外部 MCP 工具，名称 + 说明 + 参数格式），用于发现可用工具后"
        + "再经万能工具按名调用（外部工具名形如 mcp.<服务器id>.<工具名>）；支持按关键字过滤";

    public string ParametersJsonSchema => """
        {
          "type": "object",
          "properties": {
            "keyword": {
              "type": "string",
              "description": "可选：按工具名或说明包含该关键字过滤（忽略大小写）"
            },
            "includeSchema": {
              "type": "boolean",
              "description": "可选：是否在结果中包含每个工具的完整参数 schema（默认 false，仅返回名称与说明）"
            }
          },
          "additionalProperties": false
        }
        """;

    public Task<string> ExecuteAsync(string parameters)
    {
        var keyword = "";
        var includeSchema = false;
        if (!string.IsNullOrWhiteSpace(parameters))
        {
            try
            {
                using var doc = JsonDocument.Parse(parameters);
                var root = doc.RootElement;
                if (root.TryGetProperty("keyword", out var k) && k.ValueKind == JsonValueKind.String)
                    keyword = k.GetString() ?? "";
                if (root.TryGetProperty("includeSchema", out var s) && s.ValueKind == JsonValueKind.True)
                    includeSchema = true;
            }
            catch (JsonException)
            {
                // 入参解析失败按空条件处理（宽松语义：枚举全部）
            }
        }

        // 运行期解析宿主注册表（插件装配期 Get 不到，见类注释）
        var registry = _ctx?.GetService(typeof(IToolRegistry)) as IToolRegistry;

        var kw = keyword.Trim();
        var tools = (registry?.GetAllTools() ?? Enumerable.Empty<IToolFunctionExtension>())
            .Where(t => !string.Equals(t.Name, ToolName, StringComparison.OrdinalIgnoreCase))
            .Where(t => string.IsNullOrEmpty(kw)
                || (t.Name?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false)
                || (t.Description?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var list = new List<Dictionary<string, object?>>(tools.Count);
        foreach (var t in tools)
        {
            var entry = new Dictionary<string, object?>
            {
                ["name"] = t.Name,
                ["description"] = t.Description,
                ["pluginId"] = t.PluginId,
                ["source"] = "registry"
            };
            if (includeSchema)
            {
                entry["parametersSchema"] = ParseSchema(t.ParametersJsonSchema);
            }
            list.Add(entry);
        }

        // v2.6.0 外部 MCP 工具合并：运行期每次 Get（禁缓存字段——提供方卸载后条目随 Fiber 摘除，缓存会悬空）；
        // 拿到即列（仅已连接服务器）、拿不到即回退到仅宿主工具；本段整体异常不得影响宿主工具枚举（Spec BC-4）。
        try
        {
            var gateway = _ctx?.Get<IMcpExternalGateway>();
            if (gateway != null)
            {
                foreach (var ext in gateway.ListConnectedTools()
                             .Where(x => string.IsNullOrEmpty(kw)
                                 || x.FullName.Contains(kw, StringComparison.OrdinalIgnoreCase)
                                 || x.Description.Contains(kw, StringComparison.OrdinalIgnoreCase))
                             .OrderBy(x => x.FullName, StringComparer.OrdinalIgnoreCase))
                {
                    var entry = new Dictionary<string, object?>
                    {
                        ["name"] = ext.FullName,
                        ["description"] = ext.Description,
                        ["pluginId"] = ext.ServerId,
                        ["source"] = "mcp-external"
                    };
                    if (includeSchema)
                    {
                        entry["parametersSchema"] = ParseSchema(ext.InputSchemaJson);
                    }
                    list.Add(entry);
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[McpCenter] list_tools 合并外部 MCP 工具失败（仅返回宿主工具）: {0}", ex.Message);
        }

        var result = JsonSerializer.Serialize(new
        {
            total = list.Count,
            keyword = kw,
            tools = list
        });
        return Task.FromResult(result);
    }

    /// <summary>schema 原文 → JSON 结构（宿主工具与外部工具同规则：空→空对象，非法 JSON→null，宽松语义）。</summary>
    private static object? ParseSchema(string? schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
            return new Dictionary<string, object?>();
        try
        {
            return JsonSerializer.Deserialize<object>(schemaJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
