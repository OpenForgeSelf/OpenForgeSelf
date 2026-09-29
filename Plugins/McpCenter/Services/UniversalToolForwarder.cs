using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.McpCenter.Services;

/// <summary>转发结果：text 为放入 MCP 文本内容块的原文；IsError 标记结果语义（MCP tools/call 的 isError）。</summary>
public sealed class ForwardResult
{
    public required string Text { get; init; }
    public bool IsError { get; init; }
}

/// <summary>
/// 万能工具转发器（MCP 中心对外唯一工具）：解析 {tool, parameters}。
/// 路由（v2.1.0）：
///   tool 以 "mcp." 开头（mcp.&lt;服务器id&gt;.&lt;工具名&gt;）→ 转发外部 MCP 服务器会话（v2.1.0 外部 MCP 客户端接入）；
///   其余 → 经宿主 IToolRegistry 分发（v2.0.0 行为不变）。
/// 结果原样透传。语义对齐 031 UniversalTool（防自引用 / 未知工具预检带已注册数量 / 失败负载原样透传 / 软依赖 ctx.Get&lt;IToolRegistry&gt;），
/// 但**不注册**进宿主注册表（避免与 AIAgent 的 universal_tool 同名冲突；本类只被 MCP tools/call 处理器消费）。
/// </summary>
public sealed class UniversalToolForwarder
{
    /// <summary>对外暴露的工具名（tools/list 唯一条目）。</summary>
    public const string ToolName = "universal_tool";

    private readonly IContext _ctx;
    private readonly McpClientManager _clientManager;

    public UniversalToolForwarder(IContext ctx, McpClientManager clientManager)
    {
        _ctx = ctx;
        _clientManager = clientManager;
    }

    /// <summary>对外工具的定义（MCP tools/list 契约）。</summary>
    public static string ToolDefinitionJson => """
        {
          "name": "universal_tool",
          "description": "万能工具网关：对外唯一入口，按名转发调用本项目已注册的全部工具与已接入的外部 MCP 服务器工具。入参 {tool: 工具名, parameters: 该工具的参数对象}，返回目标工具结果的原样 JSON，失败返回 {success:false, error:...}。不可调用自身。发现工具：先调 {tool: 'list_tools'} 可列出全部已注册工具（名称/说明/参数格式，可按关键字过滤），再按名调用。常规能力举例：读写文件（list_files / read_file / write_file / file_stats / file_rename 等）、执行命令与脚本（run_terminal_command / run_script / execute_code）、计算与文本（calculate / format_json / encode_base64 / text_stats）、搜索（search_memories / search_links）、系统监控（get_cpu_usage / get_memory_usage / get_process_list）、工作流（plan_workflow / execute_workflow）等。外部 MCP 服务器工具：tool 传 mcp.<服务器id>.<工具名>（如 mcp.deepwiki.search），需先在 MCP 中心界面连接该服务器。",
          "inputSchema": {
            "type": "object",
            "properties": {
              "tool": {
                "type": "string",
                "description": "目标工具名：裸名（如 read_file / write_file / run_terminal_command / list_tools）走本项目工具注册表；mcp.<服务器id>.<工具名>（如 mcp.deepwiki.search）走外部 MCP 服务器。不确定有哪些工具时，先传 list_tools 枚举。"
              },
              "parameters": {
                "type": "object",
                "description": "目标工具的参数（按目标工具 schema 传对象；无参工具可省略或传 {}）"
              }
            },
            "required": ["tool"]
          }
        }
        """;

    /// <summary>
    /// 执行一次万能工具转发。入参为 MCP tools/call 的 arguments（JSON 对象原文，缺失传空串）。
    /// 返回结果文本（目标工具结果 JSON 原文 / 错误 JSON）与是否错误标记。
    /// </summary>
    public async Task<ForwardResult> ForwardAsync(string argumentsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
            var root = doc.RootElement;
            var toolName = (root.TryGetProperty("tool", out var t) ? t.GetString() : null) ?? string.Empty;
            var paramJson = "{}";
            if (root.TryGetProperty("parameters", out var p))
            {
                // 对象按原文透传（GetRawText 免再序列化）；兼容模型误传字符串形态
                paramJson = p.ValueKind == JsonValueKind.String
                    ? (p.GetString() ?? "{}")
                    : p.ValueKind == JsonValueKind.Object ? p.GetRawText() : "{}";
            }

            if (string.IsNullOrWhiteSpace(toolName))
            {
                return Err("工具名为空：入参需 {tool: 工具名, parameters: 参数对象}");
            }

            // 防自引用（防递归耗 tokens）
            if (toolName.Trim().Equals(ToolName, StringComparison.OrdinalIgnoreCase))
            {
                XTrace.Log.Warn("[McpCenter] universal_tool 拒绝自引用");
                return Err($"{ToolName} cannot invoke itself");
            }

            // v2.1.0 外部 MCP 客户端路由：mcp.<服务器id>.<工具名> → 外部会话转发（不依赖宿主注册表）
            if (toolName.StartsWith(McpClientManager.ExternalPrefix, StringComparison.Ordinal))
            {
                return await ForwardExternalAsync(toolName, paramJson);
            }

            // 软依赖注册表，按名分发（预检 unknown 带已注册数量）
            var registry = _ctx.Get<IToolRegistry>();
            if (registry == null)
            {
                return Err("IToolRegistry 未注册到插件上下文");
            }

            if (registry.GetTool(toolName) == null)
            {
                var n = registry.GetAllTools().Count();
                XTrace.Log.Info("[McpCenter] universal_tool 未知工具：{0}（已注册 {1} 个）", toolName, n);
                return Err($"unknown tool '{toolName}'（已注册 {n} 个，可用工具名见各工具说明）");
            }

            // 经宿主六闸门分发核执行（pre-execute 三态/守卫/事件链/统计在宿主侧生效；B9 迁 ExecuteAsync）
            var result = await registry.ExecuteAsync(new ToolExecution
            {
                CallId = string.Empty,
                ToolName = toolName,
                ArgsJson = paramJson,
                SessionId = string.Empty
            });
            if (result.Success)
            {
                return new ForwardResult { Text = result.Result, IsError = false };
            }

            // 失败：保留宿主错误语义——宿主失败时 Result 可能已含错误 JSON，原样透传；为空则统一错误对象
            if (!string.IsNullOrEmpty(result.Result))
            {
                return new ForwardResult { Text = result.Result, IsError = true };
            }
            XTrace.Log.Warn("[McpCenter] universal_tool 转发 {0} 失败：{1}", toolName, result.ErrorMessage);
            return Err(result.ErrorMessage ?? "EXECUTION_FAILED");
        }
        catch (JsonException jex)
        {
            XTrace.Log.Error("[McpCenter] universal_tool 入参解析失败: {0}", jex.Message);
            return Err("入参必须是 JSON：{tool: 工具名, parameters: 参数对象}，" + jex.Message);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[McpCenter] universal_tool 执行失败: {0}", ex.Message);
            return Err(ex.Message);
        }
    }

    private static ForwardResult Err(string message) =>
        new() { Text = JsonSerializer.Serialize(new { success = false, error = message }), IsError = true };

    /// <summary>
    /// 外部 MCP 工具转发：tool 形如 mcp.&lt;服务器id&gt;.&lt;工具名&gt;（命名空间内不含点，工具名按首段点拆分）。
    /// 未连接/目标工具不存在/传输失败 → isError + 明确中文错误。
    /// </summary>
    private async Task<ForwardResult> ForwardExternalAsync(string toolName, string paramJson)
    {
        var rest = toolName[McpClientManager.ExternalPrefix.Length..];
        var dotIdx = rest.IndexOf('.');
        if (dotIdx <= 0 || dotIdx == rest.Length - 1)
        {
            return Err($"外部工具名格式应为 mcp.<服务器id>.<工具名>（当前: {toolName}；例如 mcp.deepwiki.search）");
        }
        var serverId = rest[..dotIdx];
        var externalTool = rest[(dotIdx + 1)..];

        try
        {
            var text = await _clientManager.CallExternalAsync(serverId, externalTool, paramJson, CancellationToken.None);
            return new ForwardResult { Text = text, IsError = false };
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[McpCenter] 外部 MCP 工具调用失败 {0}: {1}", toolName, ex.Message);
            return Err(ex.Message);
        }
    }
}
