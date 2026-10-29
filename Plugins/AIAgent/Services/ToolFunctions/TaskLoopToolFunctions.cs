using System.Text.Json;
using ForgeSelf.Abstractions;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent.Services.ToolFunctions;

/// <summary>
/// 任务循环（PILOT-051）完成出口工具：<c>task_complete</c>。
/// 行业标准「finish/complete 工具」模式——模型必须显式调用它宣告任务全部完成（附终局摘要）；
/// 未调用之前，回合不会因「模型只回文本」而结束（ReactLoopAgent 续跑 / MafTaskLoopAgent 续跑）。
/// 与 RunFlowToolSet 出口工具同型：声明即出口、不真实执行，仅经 <see cref="AgentOptions.ExtraTools"/>
/// 挂载并进 <see cref="AgentOptions.ExitToolNames"/>（<see cref="ReactLoopAgent"/> / <see cref="MafTaskLoopAgent"/>
/// 依出口语义收束回合）。**不注册进 ToolRegistry、不进 FreeLoop 工具列表**（由解析层按名排除）。
/// </summary>
public class TaskCompleteToolFunction : IToolFunctionExtension
{
    public string Id => "aiagent.task_complete";
    public string Name => AgentOptions.TaskCompleteToolName;
    public string PluginId { get; }

    public string Description =>
        "宣告任务已全部完成并提交终局摘要。任务未完成前禁止调用本工具：先用工具推进任务并持续汇报进度，"
        + "直到目标全部达成（文件已写、命令已跑、验证已过）才调用 task_complete，summary 用一段话给出最终结果与关键产出。"
        + "注意：只输出文字而不调用本工具不会结束任务循环，系统会要求你继续执行。";

    public string ParametersJsonSchema => """
{
  "type": "object",
  "properties": {
    "summary": {
      "type": "string",
      "description": "终局摘要：任务结果、关键产出（文件/路径/数据）、未尽事项（如有）"
    }
  },
  "required": ["summary"]
}
""";

    public TaskCompleteToolFunction(string pluginId)
    {
        PluginId = pluginId;
    }

    /// <summary>声明式出口工具：不应被真实执行（声明即出口）。防御性返回成功说明。</summary>
    public Task<string> ExecuteAsync(string parameters)
    {
        XTrace.Log.Warn("[AIAgentPlugin] task_complete 被直接执行（声明即出口，正常不应走到）：{0}",
            parameters is { Length: > 200 } ? parameters[..200] : parameters);
        return Task.FromResult(JsonSerializer.Serialize(new { success = true, message = "任务已完成" }));
    }
}

/// <summary>任务循环工具集：统一构造入口（同 RunFlowToolSet 模式）。</summary>
public class TaskLoopToolSet
{
    public TaskCompleteToolFunction TaskComplete { get; }

    public List<IToolFunctionExtension> All { get; }

    public TaskLoopToolSet(string pluginId)
    {
        TaskComplete = new TaskCompleteToolFunction(pluginId);
        All = new List<IToolFunctionExtension> { TaskComplete };
    }
}
