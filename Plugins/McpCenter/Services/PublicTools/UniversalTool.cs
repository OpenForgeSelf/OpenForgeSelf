using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.McpCenter.Services;

/// <summary>
/// 万能工具聚合网关（031；2026-10-28 工具归属迁移自 AIAgent → mcp-center，裸名为平台保留名）：
/// 解析 {tool, parameters}，经宿主 IToolRegistry.ExecuteAsync（B9 迁六闸门执行面）分发，结果原样透传。
/// 零新增分发逻辑：宿主分发核自带 校验 → tools/pre-execute 三态 → 单调守卫 → tools/execute/post-execute → finalize → 使用统计，全部免费复用。
/// 模型侧可见性由 AIAgentService 白名单控制（"77 工具全挂爆 prompt" 历史教训——只挂精简集 + 本工具）。
/// </summary>
/// <remarks>
/// 与本目录其他公共工具同范式：构造期注入 IContext，运行时软依赖 ctx.Get&lt;T&gt;()（宿主 seed 晚于插件 Apply）。
/// 使用统计经注册表分发自带（post-execute），工具内不重复上报（避免双重计数）。
/// </remarks>
public class UniversalTool : IToolFunctionExtension
{
    private readonly IContext _ctx;

    public string Id => "mcp-center.universal_tool";
    public string Name => "universal_tool";
    public string PluginId => "mcp-center";

    public string Description =>
        "万能工具网关：按名调用任意已注册工具（如 read_file / write_file / list_files / run_terminal_command / calculate / 记忆与工作流工具，" +
        "以及宿主与其他插件注册进 ToolRegistry 的工具），也可直调已连接的外部 MCP 工具" +
        "（tool 传 mcp.<服务器id>.<工具名>，如 mcp.deepwiki.ask_wiki_question）。" +
        "入参 {tool: 工具名, parameters: 该工具的参数}，" +
        "返回目标工具结果的原样 JSON，失败返回 {success:false, error:...}。" +
        "不可调用自身；未注册的工具名返回错误并附已注册数量与 top 建议。" +
        "找工具先搜索、后枚举：不确定工具名时先用 search_tools {\"keyword\":\"能力词或名词根\",\"limit\":可选} 检索（按名称与说明打分，返回最匹配 top-N 及参数形态），" +
        "检索无果再用 list_tools 全量枚举兜底（含外部工具）；不要每次都直接 list_tools。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""tool"": {
            ""type"": ""string"",
            ""description"": ""目标工具名（裸名，如 read_file / write_file / list_files / calculate；已连接的外部 MCP 工具用全名 mcp.<服务器id>.<工具名>；不确定工具名先调 search_tools 检索，而非直接 list_tools 全量枚举）""
        },
        ""parameters"": {
            ""type"": ""object"",
            ""description"": ""目标工具的参数（按目标工具 schema 传对象；无参工具可省略或传 {}）""
        }
    },
    ""required"": [""tool""]
}";

    public UniversalTool(IContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var start = Environment.TickCount64;
        try
        {
            // 1. 解析入参 {tool, parameters}
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(parameters) ? "{}" : parameters);
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
                return JsonSerializer.Serialize(new { success = false, error = "工具名为空：入参需 {tool: 工具名, parameters: 参数对象}" });
            }

            // 2. 防自引用（防递归耗 tokens）
            if (toolName.Trim().Equals(Name, StringComparison.OrdinalIgnoreCase))
            {
                XTrace.Log.Warn("[McpCenter] universal_tool 拒绝自引用");
                return JsonSerializer.Serialize(new { success = false, error = "universal_tool cannot invoke itself" });
            }

            // 3. v1.9.0 外部 MCP 工具直调：mcp.<服务器id>.<工具名> 经本插件提供的跨插件契约转发
            //    （外部工具不注册进宿主 IToolRegistry——决策 D10，故必在注册表分发之前分流）
            if (toolName.StartsWith(McpExternalToolNames.Prefix, StringComparison.Ordinal))
            {
                return await ForwardExternalAsync(toolName, paramJson);
            }

            // 4. 软依赖注册表，按名分发（预检 unknown 带已注册数量）
            var registry = _ctx.Get<IToolRegistry>()
                ?? throw new InvalidOperationException("IToolRegistry 未注册到插件上下文");

            if (registry.GetTool(toolName) == null)
            {
                var n = registry.GetAllTools().Count();
                // 输入2·2026-10-13：未知工具封套附 top 搜索建议（复用 search_tools 打分），避免模型退化为全量枚举
                var hint = ToolSearchHints(registry.GetAllTools, toolName);
                XTrace.Log.Info("[McpCenter] universal_tool 未知工具：{0}（已注册 {1} 个）", toolName, n);
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"unknown tool '{toolName}'（已注册 {n} 个，可用工具名见各工具说明）",
                    hint
                });
            }

            // 5. 经宿主六闸门分发核执行（pre-execute 三态/守卫/事件链/统计在宿主侧生效；B9 迁 ExecuteAsync）
            var result = await registry.ExecuteAsync(new ToolExecution
            {
                CallId = string.Empty,
                ToolName = toolName,
                ArgsJson = paramJson,
                SessionId = string.Empty
            });
            if (result.Success)
            {
                XTrace.Log.Info("[McpCenter] universal_tool 透传 {0} 成功（{1}ms）", toolName, Environment.TickCount64 - start);
                return result.Result;
            }

            // 失败：保留宿主错误语义——宿主失败时 Result 可能已含错误 JSON，原样透传；为空则统一错误对象
            if (!string.IsNullOrEmpty(result.Result))
            {
                return result.Result;
            }
            XTrace.Log.Warn("[McpCenter] universal_tool 转发 {0} 失败：{1}", toolName, result.ErrorMessage);
            return JsonSerializer.Serialize(new { success = false, error = result.ErrorMessage ?? "EXECUTION_FAILED" });
        }
        catch (JsonException jex)
        {
            XTrace.Log.Error("[McpCenter] universal_tool 入参解析失败: {0}", jex.Message);
            return JsonSerializer.Serialize(new { success = false, error = "入参必须是 JSON：{tool: 工具名, parameters: 参数对象}，" + jex.Message });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[McpCenter] universal_tool 执行失败: {0}", ex.Message);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 外部 MCP 工具转发（v1.9.0）：与网关侧同语义（同一 <see cref="IMcpExternalGateway"/> 契约、
    /// 同一全名解析真源 <see cref="McpExternalToolNames"/>），区别只在错误封套形态——本工具统一用
    /// <c>{success:false, error:...}</c>（与宿主工具失败同形）。
    /// 契约运行期每次 Get（禁缓存为字段：提供方热重载/卸载后条目随 Fiber 摘除，缓存会悬空）。
    /// </summary>
    private async Task<string> ForwardExternalAsync(string toolName, string paramJson)
    {
        var gateway = _ctx.Get<IMcpExternalGateway>();
        if (gateway == null)
        {
            XTrace.Log.Warn("[McpCenter] universal_tool 外部工具请求被拒：IMcpExternalGateway 未提供（MCP 中心未启用）");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "外部 MCP 能力不可用：请确认 MCP 中心插件已启用，并在「网关配置」里连接目标外部服务器"
            });
        }

        try
        {
            var text = await gateway.InvokeAsync(toolName, paramJson, CancellationToken.None);
            return text;
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[McpCenter] universal_tool 外部工具 {0} 调用失败: {1}", toolName, ex.Message);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 未知工具封套的 hint：用 search_tools 同款打分给出 top≤3 建议 + 引导语（输入2·2026-10-13）。
    /// getAll 用 Func 传入，与分发路径解耦；无建议时只给引导语（先 search_tools，再 list_tools 兜底）。
    /// </summary>
    internal static string ToolSearchHints(Func<IEnumerable<IToolFunctionExtension>> getAll, string toolName)
    {
        try
        {
            var top = ToolSearchScorer.Search(getAll(), toolName, 3);
            var suggestion = top.Count > 0
                ? $"did you mean: {string.Join(" / ", top.Select(m => m.Tool.Name))}？"
                : string.Empty;
            return $"先 search_tools {{\"keyword\":\"…\"}} 检索工具，检索无果再用 list_tools 全量枚举。{suggestion}";
        }
        catch (Exception ex)
        {
            // 建议生成失败不能吞掉原本的 unknown 错误封套
            XTrace.Log.Warn("[McpCenter] universal_tool 工具建议生成失败: {0}", ex.Message);
            return "先 search_tools 检索工具，检索无果再用 list_tools 全量枚举。";
        }
    }
}
