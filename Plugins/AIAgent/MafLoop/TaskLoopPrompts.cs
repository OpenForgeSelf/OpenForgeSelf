using System.Text.Json;
using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Plugins.AIAgent.MafLoop;

/// <summary>
/// 任务循环协议文案与出口工具 schema（PILOT-051 FR-1）：两引擎（MafTaskLoopAgent / ReactLoopAgent）
/// 共用单一词汇源，防止双实现提示词漂移。
/// </summary>
public static class TaskLoopPrompts
{
    /// <summary>task_complete 工具参数 schema（含必填 summary）。</summary>
    public const string TaskCompleteSchemaJson =
        "{\"type\":\"object\",\"properties\":{\"summary\":{\"type\":\"string\",\"description\":\"任务完成情况总结\"}},\"required\":[\"summary\"]}";

    /// <summary>任务循环协议块：注入回合系统提示（告诉模型持续工作 + 出口工具契约）。</summary>
    public static string Protocol(IReadOnlyList<string> exitToolNames)
    {
        var exitList = string.Join("、", exitToolNames);
        return $"""
## 任务循环协议
你处于持续执行模式：接到任务后要**持续使用工具推进**（读文件、写代码、执行命令、验证结果），不要只给文字建议。
- 每轮做完一段工作后继续下一步，直到任务全部完成；
- 任务**全部完成**时，调用出口工具（{exitList}）提交终局摘要；
- 只输出文字而不调用出口工具不会结束循环，系统会要求你继续执行；
- 遇到确实无法自行解决的阻塞，说明原因并停止调用工具等待人工介入。
""";
    }

    /// <summary>续跑指令：注入续跑步的系统提示（配合 ToolChoice=required 强制行动）。</summary>
    public static string Continuation(IReadOnlyList<string> exitToolNames)
    {
        var exitList = string.Join("、", exitToolNames);
        return $"""
[任务循环] 你上一条回复没有调用任何工具，任务尚未宣告完成。请继续使用工具推进任务；
全部完成时调用出口工具（{exitList}）提交终局摘要。不要只用文本汇报进度。
""";
    }

    /// <summary>task_complete 出口工具的统一工具定义（声明即出口，不进六闸门执行器）。</summary>
    public static UnifiedToolDefinition TaskCompleteDefinition()
        => new()
        {
            Name = AgentOptions.TaskCompleteToolName,
            Description = "任务全部完成时调用本工具提交终局摘要，宣告任务结束",
            Parameters = JsonSerializer.Deserialize<JsonElement>(TaskCompleteSchemaJson)
        };
}
