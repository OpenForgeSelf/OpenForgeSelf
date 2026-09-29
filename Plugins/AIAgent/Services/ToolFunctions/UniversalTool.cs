using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent;

/// <summary>
/// 万能工具聚合网关（031）：解析 {tool, parameters}，经宿主 IToolRegistry.ExecuteAsync（B9 迁六闸门执行面）分发，结果原样透传。
/// 零新增分发逻辑：宿主分发核自带 校验 → tools/pre-execute 三态 → 单调守卫 → tools/execute/post-execute → finalize → 使用统计，全部免费复用。
/// 模型侧可见性由 AIAgentService 白名单控制（"77 工具全挂爆 prompt" 历史教训——只挂精简集 + 本工具）。
/// </summary>
/// <remarks>
/// 与 ProjectFileToolFunctions 同范式：构造期注入 IContext，运行时软依赖 ctx.Get&lt;T&gt;()（宿主 seed 晚于插件 Apply）。
/// 使用统计经注册表分发自带（post-execute），工具内不重复上报（避免双重计数），与 ProjectFileToolFunctions 范式一致。
/// </remarks>
public class UniversalTool : IToolFunctionExtension
{
    private readonly IContext _ctx;

    public string Id => "aiagent.universal_tool";
    public string Name => "universal_tool";
    public string PluginId { get; }

    public string Description =>
        "万能工具网关：按名调用任意已注册工具（如 read_file / write_file / list_files / calculate / 记忆与工作流工具，" +
        "以及宿主与其他插件注册进 ToolRegistry 的工具）。入参 {tool: 工具名, parameters: 该工具的参数}，" +
        "返回目标工具结果的原样 JSON，失败返回 {success:false, error:...}。" +
        "不可调用自身；未注册的工具名返回错误并附已注册数量。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""tool"": {
            ""type"": ""string"",
            ""description"": ""目标工具名（裸名，如 read_file / write_file / list_files / calculate）""
        },
        ""parameters"": {
            ""type"": ""object"",
            ""description"": ""目标工具的参数（按目标工具 schema 传对象；无参工具可省略或传 {}）""
        }
    },
    ""required"": [""tool""]
}";

    public UniversalTool(string pluginId, IContext ctx)
    {
        PluginId = pluginId;
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
                XTrace.Log.Warn("[AIAgentPlugin] universal_tool 拒绝自引用");
                return JsonSerializer.Serialize(new { success = false, error = "universal_tool cannot invoke itself" });
            }

            // 3. 软依赖注册表，按名分发（预检 unknown 带已注册数量）
            var registry = _ctx.Get<IToolRegistry>()
                ?? throw new InvalidOperationException("IToolRegistry 未注册到插件上下文");

            if (registry.GetTool(toolName) == null)
            {
                var n = registry.GetAllTools().Count();
                XTrace.Log.Info("[AIAgentPlugin] universal_tool 未知工具：{0}（已注册 {1} 个）", toolName, n);
                return JsonSerializer.Serialize(new { success = false, error = $"unknown tool '{toolName}'（已注册 {n} 个，可用工具名见各工具说明）" });
            }

            // 4. 经宿主六闸门分发核执行（pre-execute 三态/守卫/事件链/统计在宿主侧生效；B9 迁 ExecuteAsync）
            var result = await registry.ExecuteAsync(new ToolExecution
            {
                CallId = string.Empty,
                ToolName = toolName,
                ArgsJson = paramJson,
                SessionId = string.Empty
            });
            if (result.Success)
            {
                XTrace.Log.Info("[AIAgentPlugin] universal_tool 透传 {0} 成功（{1}ms）", toolName, Environment.TickCount64 - start);
                return result.Result;
            }

            // 失败：保留宿主错误语义——宿主失败时 Result 可能已含错误 JSON，原样透传；为空则统一错误对象
            if (!string.IsNullOrEmpty(result.Result))
            {
                return result.Result;
            }
            XTrace.Log.Warn("[AIAgentPlugin] universal_tool 转发 {0} 失败：{1}", toolName, result.ErrorMessage);
            return JsonSerializer.Serialize(new { success = false, error = result.ErrorMessage ?? "EXECUTION_FAILED" });
        }
        catch (JsonException jex)
        {
            XTrace.Log.Error("[AIAgentPlugin] universal_tool 入参解析失败: {0}", jex.Message);
            return JsonSerializer.Serialize(new { success = false, error = "入参必须是 JSON：{tool: 工具名, parameters: 参数对象}，" + jex.Message });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] universal_tool 执行失败: {0}", ex.Message);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }
}
