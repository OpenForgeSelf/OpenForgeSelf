using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ForgeSelf.Api.Controllers.UnifiedAI;

namespace ForgeSelf.Api.Controllers.UnifiedAI;

/// <summary>
/// 将 Microsoft Agent Framework 的 <see cref="AgentResponseUpdate"/> 翻译为 OpenAI 流式 chunk。
/// 关键点：工具调用（<see cref="FunctionCallContent"/>）与工具结果（<see cref="FunctionResultContent"/>）
/// 都会被保留并输出，避免像手写代理那样把 tool_calls 丢掉（本项目原 bug）。
/// </summary>
public static class AgentStreamTranslator
{
    /// <summary>
    /// 把一个流式更新翻译为一个或多个 OpenAI chunk。
    /// <paramref name="roleEmitted"/> 用于保证 assistant 角色只在第一块出现。
    /// </summary>
    public static IEnumerable<OpenAIStreamChunk> Translate(
        AgentResponseUpdate update,
        string id,
        string model,
        long created,
        ref bool roleEmitted)
    {
        var chunks = new List<OpenAIStreamChunk>();

        // 优先使用聚合文本属性
        if (!string.IsNullOrEmpty(update.Text))
        {
            chunks.Add(MakeContentChunk(id, model, created, ref roleEmitted, update.Text!));
        }

        // 结构化内容：工具调用 / 工具结果 / 思考
        if (update.Contents != null)
        {
            var toolIndex = 0;
            foreach (var content in update.Contents)
            {
                switch (content)
                {
                    case FunctionCallContent fcc:
                        chunks.Add(MakeToolCallChunk(id, model, created, ref roleEmitted, fcc, toolIndex));
                        toolIndex++;
                        break;
                    case FunctionResultContent frc:
                        // 非标准但可读：把服务端工具执行结果作为 tool 角色消息透出，便于在测试接口观察工具确实被调用
                        chunks.Add(MakeToolResultChunk(id, model, created, frc));
                        break;
                    case TextContent tc:
                        // 极少数情况下 Text 聚合属性未覆盖，再补一份
                        if (string.IsNullOrEmpty(update.Text) && !string.IsNullOrEmpty(tc.Text))
                            chunks.Add(MakeContentChunk(id, model, created, ref roleEmitted, tc.Text!));
                        break;
                }
            }
        }

        // 结束原因（tool_calls 或 stop）
        if (update.FinishReason != null)
        {
            var reason = update.FinishReason.Value == ChatFinishReason.ToolCalls ? "tool_calls" : "stop";
            chunks.Add(MakeFinishChunk(id, model, created, reason));
        }

        return chunks;
    }

    private static OpenAIStreamChunk MakeContentChunk(string id, string model, long created, ref bool roleEmitted, string content)
    {
        var emitRole = !roleEmitted;
        roleEmitted = true;
        return new OpenAIStreamChunk
        {
            Id = id,
            Object = "chat.completion.chunk",
            Created = created,
            Model = model,
            Choices = new List<OpenAIStreamChoice>
            {
                new()
                {
                    Index = 0,
                    Delta = new OpenAIStreamDelta
                    {
                        Role = emitRole ? "assistant" : null,
                        Content = content
                    }
                }
            }
        };
    }

    private static OpenAIStreamChunk MakeToolCallChunk(string id, string model, long created, ref bool roleEmitted, FunctionCallContent fcc, int index)
    {
        var emitRole = !roleEmitted;
        roleEmitted = true;
        var argsJson = fcc.Arguments == null || fcc.Arguments.Count == 0
            ? "{}"
            : JsonSerializer.Serialize(fcc.Arguments);
        return new OpenAIStreamChunk
        {
            Id = id,
            Object = "chat.completion.chunk",
            Created = created,
            Model = model,
            Choices = new List<OpenAIStreamChoice>
            {
                new()
                {
                    Index = 0,
                    Delta = new OpenAIStreamDelta
                    {
                        Role = emitRole ? "assistant" : null,
                        ToolCalls = new List<OpenAIToolCall>
                        {
                            new()
                            {
                                Index = index,
                                Id = fcc.CallId,
                                Type = "function",
                                Function = new OpenAIFunctionCall
                                {
                                    Name = fcc.Name ?? string.Empty,
                                    Arguments = argsJson
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    private static OpenAIStreamChunk MakeToolResultChunk(string id, string model, long created, FunctionResultContent frc)
    {
        return new OpenAIStreamChunk
        {
            Id = id,
            Object = "chat.completion.chunk",
            Created = created,
            Model = model,
            Choices = new List<OpenAIStreamChoice>
            {
                new()
                {
                    Index = 0,
                    Delta = new OpenAIStreamDelta
                    {
                        Role = "tool",
                        Content = frc.Result?.ToString()
                    }
                }
            }
        };
    }

    private static OpenAIStreamChunk MakeFinishChunk(string id, string model, long created, string finishReason)
    {
        return new OpenAIStreamChunk
        {
            Id = id,
            Object = "chat.completion.chunk",
            Created = created,
            Model = model,
            Choices = new List<OpenAIStreamChoice>
            {
                new()
                {
                    Index = 0,
                    Delta = new OpenAIStreamDelta(),
                    FinishReason = finishReason
                }
            }
        };
    }
}
