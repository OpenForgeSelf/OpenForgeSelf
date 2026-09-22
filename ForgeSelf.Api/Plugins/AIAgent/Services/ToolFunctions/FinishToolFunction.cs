using System.Text.Json;
using ForgeSelf.Abstractions;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent.Services.ToolFunctions;

/// <summary>
/// 自主循环的**完成出口工具**（自治循环契约）：
/// 模型调用它即声明「任务已完成」，<see cref="AIAgentService.RunAgentLoopAsync"/> 收到后结束循环并发 done 事件。
/// </summary>
/// <remarks>
/// 为什么需要它：循环原先把「本轮没有工具调用」等同于「任务完成」，而模型经常只是输出一段说明（陈述/计划）就停手，
/// 导致自主任务每轮都在第一次纯文本输出处熔断，只能靠人工连发「继续」。改为显式完成工具后，
/// 只有模型自己宣布完成才结束，否则自动进入下一轮（直到轮次上限）。
/// 与 submit_plan / complete_step / request_help 的区别：那三个是计划驱动运行流的**阶段出口**，
/// 不进 FreeLoop；本工具恰恰相反——只进 FreeLoop（自治聊天循环），由控制器显式开启。
/// </remarks>
public class FinishToolFunction : IToolFunctionExtension
{
    /// <summary>工具名（循环判定与白名单强制挂载均按此名匹配）。</summary>
    public const string ToolName = "finish";

    public string Id => "aiagent.finish";
    public string Name => ToolName;
    public string PluginId { get; }

    public string Description =>
        "宣布任务已完成并结束本轮自主循环。必须在任务真正全部完成后调用；调用后不再发起新一轮。参数 summary 写清完成了什么、关键改动与结果。未完成就调用会让任务提前终止。";

    public string ParametersJsonSchema => """
{
  "type": "object",
  "properties": {
    "summary": { "type": "string", "description": "完成总结：做了什么、改了哪些文件/产出、结果如何" }
  },
  "required": ["summary"]
}
""";

    public FinishToolFunction(string pluginId)
    {
        PluginId = pluginId;
    }

    public Task<string> ExecuteAsync(string parameters)
    {
        try
        {
            var summary = TryExtractSummary(parameters);
            if (string.IsNullOrWhiteSpace(summary))
                return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = "缺少 summary" }));

            XTrace.Log.Info("[AIAgentPlugin] 模型声明任务完成（finish），总结长度: {0}", summary!.Length);
            return Task.FromResult(JsonSerializer.Serialize(new { success = true, message = "任务已声明完成", summary }));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] finish 执行失败: {0}", ex.Message);
            return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
        }
    }

    /// <summary>从工具参数 JSON 中提取完成总结；缺失或解析失败返回 null。</summary>
    public static string? TryExtractSummary(string? argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("summary", out var summary))
            {
                var text = summary.GetString();
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
        }
        catch
        {
            // 参数非 JSON（弱模型常见）时静默降级：调用方会退回用本轮 assistant 文本作为总结。
        }
        return null;
    }

    /// <summary>
    /// 构造工具定义，供自治模式在 UI 工具白名单过滤掉 finish 时强制补挂
    /// （finish 是循环控制工具，被过滤 = 模型永远无法声明完成 → 必然跑满轮次上限）。
    /// </summary>
    public static AIToolDefinition CreateDefinition()
    {
        return new AIToolDefinition
        {
            Function = new AIFunctionDefinition
            {
                Name = ToolName,
                Description = new FinishToolFunction(string.Empty).Description,
                Parameters = JsonDocument.Parse(new FinishToolFunction(string.Empty).ParametersJsonSchema).RootElement.Clone()
            }
        };
    }
}
