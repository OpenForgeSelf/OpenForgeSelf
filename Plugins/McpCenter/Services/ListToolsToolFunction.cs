using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;

namespace ForgeSelf.Api.Plugins.McpCenter.Services;

/// <summary>
/// 工具清单枚举工具（v2.1.0 网关能力）：列出宿主工具注册表中全部已注册工具（名称/说明/参数 schema），
/// 作为万能工具（universal_tool）的「发现工具」入口——外部客户端先调
/// <c>universal_tool {tool:"list_tools"}</c> 看有哪些工具可用，再按名调用。
/// 本工具只枚举、不执行业务；结果来自宿主 IToolRegistry.GetAllTools() 实时快照。
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
        "列出本项目全部已注册工具（名称 + 说明 + 参数格式），用于发现可用工具后，再经万能工具按名调用；支持按关键字过滤";

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
                ["pluginId"] = t.PluginId
            };
            if (includeSchema)
            {
                try
                {
                    entry["parametersSchema"] = string.IsNullOrWhiteSpace(t.ParametersJsonSchema)
                        ? new Dictionary<string, object?>()
                        : JsonSerializer.Deserialize<object>(t.ParametersJsonSchema);
                }
                catch (JsonException)
                {
                    entry["parametersSchema"] = null;
                }
            }
            list.Add(entry);
        }

        var result = JsonSerializer.Serialize(new
        {
            total = list.Count,
            keyword = kw,
            tools = list
        });
        return Task.FromResult(result);
    }
}
