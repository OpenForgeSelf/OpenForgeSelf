using System.Text.Json;
using ForgeSelf.Abstractions;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent.Services.ToolFunctions;

/// <summary>
/// 计划驱动运行流特殊工具集（design.md §4 / research.md R4）：
/// <c>submit_plan</c>（规划出口）、<c>complete_step</c>（步骤完成出口）、<c>request_help</c>（卡住出口）。
/// 三工具注册进 ToolRegistry（可被 <c>ExecuteToolWithTimeoutAsync</c> 执行），但被
/// <see cref="AIAgentService.ResolveOwnToolDefinitions"/> 按名排除，**不进 FreeLoop**；
/// 仅 PlanDriven 步骤循环经 <see cref="IAIAgentService.RunAgentLoopAsync"/> 的
/// <c>extraTools</c> 参数显式挂载，由 PlanGeneratorService / StepRunLoopService 消费
/// <c>tool_call</c> 事件驱动阶段出口。
/// </summary>
public class RunFlowToolSet
{
    public SubmitPlanToolFunction SubmitPlan { get; }
    public CompleteStepToolFunction CompleteStep { get; }
    public RequestHelpToolFunction RequestHelp { get; }

    public List<IToolFunctionExtension> All { get; }

    public RunFlowToolSet(string pluginId)
    {
        SubmitPlan = new SubmitPlanToolFunction(pluginId);
        CompleteStep = new CompleteStepToolFunction(pluginId);
        RequestHelp = new RequestHelpToolFunction(pluginId);
        All = new List<IToolFunctionExtension> { SubmitPlan, CompleteStep, RequestHelp };
    }

    /// <summary>规划阶段只挂 submit_plan（避免过早出现步骤出口工具干扰规划）。</summary>
    public List<IToolFunctionExtension> Planning() => new() { SubmitPlan };

    /// <summary>步骤循环挂 complete_step + request_help（步骤出口）。</summary>
    public List<IToolFunctionExtension> Stepping() => new() { CompleteStep, RequestHelp };
}

/// <summary>
/// 规划阶段工具：LLM 用结构化 JSON 提交执行计划（Plan DSL）。
/// 返回值仅供记录；真正的计划由消费方从 <c>tool_call</c> 事件的 Arguments 解析。
/// </summary>
public class SubmitPlanToolFunction : IToolFunctionExtension
{
    public string Id => "aiagent.submit_plan";
    public string Name => "submit_plan";
    public string PluginId { get; }
    public string Description => "规划阶段提交执行计划：goal（整体目标一句话）+ steps[]（每个步骤含 id/name/objective/expectedOutput/allowedTools/mandatory）。提交后进入逐步执行阶段。";

    public string ParametersJsonSchema => """
{
  "type": "object",
  "properties": {
    "plan": {
      "type": "object",
      "description": "执行计划对象",
      "properties": {
        "goal": { "type": "string", "description": "整体目标一句话" },
        "steps": {
          "type": "array",
          "description": "步骤列表（顺序执行，2-6 步为宜）",
          "items": {
            "type": "object",
            "properties": {
              "id": { "type": "string", "description": "步骤 id，如 s1" },
              "name": { "type": "string", "description": "步骤名" },
              "objective": { "type": "string", "description": "本步要完成什么（给 LLM 的执行指令）" },
              "expectedOutput": { "type": "string", "description": "期望产出物" },
              "allowedTools": { "type": "array", "items": { "type": "string" }, "description": "本步允许的工具白名单，空=全部工具" },
              "mandatory": { "type": "boolean", "description": "是否必须步骤" }
            },
            "required": ["id", "name", "objective", "expectedOutput"]
          }
        }
      },
      "required": ["goal", "steps"]
    }
  },
  "required": ["plan"]
}
""";

    public SubmitPlanToolFunction(string pluginId)
    {
        PluginId = pluginId;
    }

    public Task<string> ExecuteAsync(string parameters)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(parameters))
                return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = "plan 参数为空" }));

            using var doc = JsonDocument.Parse(parameters);
            if (!doc.RootElement.TryGetProperty("plan", out var plan) || plan.ValueKind != JsonValueKind.Object)
                return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = "缺少 plan 对象" }));

            return Task.FromResult(JsonSerializer.Serialize(new { success = true, message = "计划已提交，进入逐步执行阶段" }));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] submit_plan 执行失败: {0}", ex.Message);
            return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
        }
    }
}

/// <summary>
/// 步骤循环出口工具：LLM 声明本步完成并附产出。
/// 消费方从 <c>tool_call</c> 事件的 Arguments.output 捕获产出。
/// </summary>
public class CompleteStepToolFunction : IToolFunctionExtension
{
    public string Id => "aiagent.complete_step";
    public string Name => "complete_step";
    public string PluginId { get; }
    public string Description => "声明当前步骤已完成并提交产出。调用后本步结束，进入下一步。";

    public string ParametersJsonSchema => """
{
  "type": "object",
  "properties": {
    "output": { "type": "string", "description": "本步产出的完整文本（作为步骤留痕与下一步上下文）" }
  },
  "required": ["output"]
}
""";

    public CompleteStepToolFunction(string pluginId)
    {
        PluginId = pluginId;
    }

    public Task<string> ExecuteAsync(string parameters)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(parameters))
                return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = "output 参数为空" }));

            using var doc = JsonDocument.Parse(parameters);
            if (!doc.RootElement.TryGetProperty("output", out var output) || string.IsNullOrWhiteSpace(output.GetString()))
                return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = "缺少 output" }));

            return Task.FromResult(JsonSerializer.Serialize(new { success = true, message = "步骤完成" }));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] complete_step 执行失败: {0}", ex.Message);
            return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
        }
    }
}

/// <summary>
/// 步骤循环出口工具：LLM 卡住求助（Run → Stuck，等待人工介入）。
/// 消费方从 <c>tool_call</c> 事件的 Arguments.reason 捕获卡住原因。
/// </summary>
public class RequestHelpToolFunction : IToolFunctionExtension
{
    public string Id => "aiagent.request_help";
    public string Name => "request_help";
    public string PluginId { get; }
    public string Description => "当前步骤无法推进时求助：说明卡住原因与需要的人工协助。调用后 Run 进入卡住状态等待人工介入。";

    public string ParametersJsonSchema => """
{
  "type": "object",
  "properties": {
    "reason": { "type": "string", "description": "卡住原因（为什么无法完成本步）" },
    "question": { "type": "string", "description": "需要人工回答的具体问题（可选）" }
  },
  "required": ["reason"]
}
""";

    public RequestHelpToolFunction(string pluginId)
    {
        PluginId = pluginId;
    }

    public Task<string> ExecuteAsync(string parameters)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(parameters))
                return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = "reason 参数为空" }));

            using var doc = JsonDocument.Parse(parameters);
            if (!doc.RootElement.TryGetProperty("reason", out var reason) || string.IsNullOrWhiteSpace(reason.GetString()))
                return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = "缺少 reason" }));

            return Task.FromResult(JsonSerializer.Serialize(new { success = true, message = "已求助，等待人工介入" }));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] request_help 执行失败: {0}", ex.Message);
            return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
        }
    }
}
