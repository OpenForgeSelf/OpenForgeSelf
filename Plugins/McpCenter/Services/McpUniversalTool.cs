using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ForgeSelf.Api.Plugins.McpCenter.Services;

/// <summary>
/// 官方 MCP SDK 形态的「万能工具」外壳：对外唯一工具 universal_tool。
/// 只做「入参组装 → 转调 UniversalToolForwarder → 原样回传结果」，
/// 转发语义（宿主 IToolRegistry / mcp.&lt;服务器id&gt;.&lt;工具名&gt; 外部服务器、防自引用、未知工具预检）
/// 全部由 UniversalToolForwarder 承担，本类不复制任何一份。
/// </summary>
/// <remarks>
/// 非静态类：官方 SDK 的 WithTools&lt;T&gt;() 不接受静态类（CS0718），且需由 DI 提供 UniversalToolForwarder 实例。
/// </remarks>
[McpServerToolType]
public sealed class McpUniversalTool
{
    private readonly UniversalToolForwarder _forwarder;

    public McpUniversalTool(UniversalToolForwarder forwarder) => _forwarder = forwarder;

    [McpServerTool(Name = UniversalToolForwarder.ToolName)]
    [Description("万能工具网关：对外唯一入口，按名转发调用本项目已注册的全部工具与已接入的外部 MCP 服务器工具。" +
                 "入参 {tool: 工具名, parameters: 该工具的参数对象}，返回目标工具结果的原样 JSON，失败返回 {success:false, error:...}。不可调用自身。" +
                 "找工具先搜索、搜不到再枚举：不确定工具名时先传 tool='search_tools'（parameters 传 {keyword: '能力词或名词根', limit: 可选，默认 8}），" +
                 "按工具名与说明打分检索，返回最匹配的 top-N 及参数形态，命中即可按名调用；" +
                 "检索无果（或提示无此工具、search_tools 不可用）时再传 tool='list_tools' 全量枚举兜底（名称/说明/参数格式，可按关键字过滤），" +
                 "然后按名调用。不要一上来就全量枚举。" +
                 "外部 MCP 服务器工具：tool 传 mcp.<服务器id>.<工具名>（如 mcp.deepwiki.search），需先在 MCP 中心界面连接该服务器。")]
    public async Task<CallToolResult> UniversalToolAsync(
        [Description("目标工具名：裸名（如 read_file / write_file / run_terminal_command）走本项目工具注册表；" +
                     "mcp.<服务器id>.<工具名> 走外部 MCP 服务器。不确定工具名时先调 search_tools 检索，检索无果再调 list_tools 枚举。")]
        string tool,
        [Description("目标工具的参数（按目标工具 schema 传对象；无参工具可省略或传 {}）")]
        JsonElement? parameters = null)
    {
        // 组装成转发器约定的 {tool, parameters} 原文；parameters 缺省补 {}（与转发器默认值一致）
        var args = new JsonObject { ["tool"] = tool };
        args["parameters"] = parameters.HasValue && parameters.Value.ValueKind != JsonValueKind.Undefined
            ? JsonNode.Parse(parameters.Value.GetRawText())
            : new JsonObject();

        var result = await _forwarder.ForwardAsync(args.ToJsonString());

        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = result.Text }],
            IsError = result.IsError
        };
    }
}
