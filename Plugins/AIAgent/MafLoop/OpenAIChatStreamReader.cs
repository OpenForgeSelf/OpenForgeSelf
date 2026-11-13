using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Plugins.AIAgent.MafLoop;

/// <summary>把管道返回的 Chat Completions SSE 收成 <see cref="UnifiedStreamChunk"/>。HTTP 入站不使用本读取器。</summary>
public static class OpenAIChatStreamReader
{
    public static async IAsyncEnumerable<UnifiedStreamChunk> Read(
        Stream body,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null)
                yield break;
            if (!line.StartsWith("data:", StringComparison.Ordinal))
                continue;
            var payload = line["data:".Length..].Trim();
            if (payload == "[DONE]")
            {
                yield return new UnifiedStreamChunk { IsDone = true };
                yield break;
            }

            JsonObject? obj;
            try { obj = JsonNode.Parse(payload) as JsonObject; }
            catch { continue; }
            if (obj is null)
                continue;

            var choice = obj["choices"] is JsonArray choices && choices.Count > 0 ? choices[0] as JsonObject : null;
            var delta = choice?["delta"] as JsonObject;
            var tool = delta?["tool_calls"] is JsonArray tools && tools.Count > 0 ? tools[0] as JsonObject : null;
            var fn = tool?["function"] as JsonObject;
            var usage = obj["usage"] as JsonObject;
            UnifiedToolCall? deltaCall = null;
            if (tool != null)
            {
                deltaCall = new UnifiedToolCall
                {
                    Id = Str(tool["id"]) ?? string.Empty,
                    Name = Str(fn?["name"]) ?? string.Empty,
                    Arguments = Str(fn?["arguments"]) ?? string.Empty
                };
            }

            yield return new UnifiedStreamChunk
            {
                Id = Str(obj["id"]) ?? string.Empty,
                Model = Str(obj["model"]) ?? string.Empty,
                DeltaContent = Str(delta?["content"]) ?? string.Empty,
                DeltaRole = Str(delta?["role"]),
                FinishReason = Str(choice?["finish_reason"]),
                DeltaToolCall = deltaCall,
                Usage = usage == null ? null : new UnifiedUsage
                {
                    PromptTokens = Int(usage["prompt_tokens"]),
                    CompletionTokens = Int(usage["completion_tokens"]),
                    TotalTokens = Int(usage["total_tokens"])
                }
            };
        }
    }

    private static string? Str(JsonNode? node)
        => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static int Int(JsonNode? node)
    {
        if (node is not JsonValue value)
            return 0;
        if (value.TryGetValue<int>(out var i))
            return i;
        if (value.TryGetValue<long>(out var l))
            return (int)l;
        return 0;
    }
}

/// <summary>把已经映射好的统一请求收成 Chat Completions JSON。只作为入站报文，不改模型别名。</summary>
public static class OpenAIChatJson
{
    private static readonly JsonSerializerOptions Json = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string FromUnified(UnifiedChatRequest request)
    {
        var root = new JsonObject
        {
            ["model"] = request.Model,
            ["stream"] = true,
            ["messages"] = new JsonArray()
        };
        if (request.StreamOptions?.IncludeUsage == true)
            root["stream_options"] = new JsonObject { ["include_usage"] = true };

        var messages = (JsonArray)root["messages"]!;
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            messages.Add(new JsonObject { ["role"] = "system", ["content"] = request.SystemPrompt });

        foreach (var message in request.Messages)
        {
            var item = new JsonObject
            {
                ["role"] = message.Role,
                ["content"] = message.Content ?? string.Empty
            };
            if (!string.IsNullOrEmpty(message.ToolCallId))
                item["tool_call_id"] = message.ToolCallId;
            if (message.ToolCalls is { Count: > 0 })
            {
                var calls = new JsonArray();
                foreach (var call in message.ToolCalls)
                {
                    calls.Add(new JsonObject
                    {
                        ["id"] = call.Id,
                        ["type"] = "function",
                        ["function"] = new JsonObject
                        {
                            ["name"] = call.Name,
                            ["arguments"] = string.IsNullOrEmpty(call.Arguments) ? "{}" : call.Arguments
                        }
                    });
                }
                item["tool_calls"] = calls;
            }
            messages.Add(item);
        }

        if (request.Tools is { Count: > 0 })
        {
            var tools = new JsonArray();
            foreach (var tool in request.Tools)
            {
                var fn = new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description ?? string.Empty
                };
                if (tool.Parameters != null)
                    fn["parameters"] = JsonNode.Parse(JsonSerializer.Serialize(tool.Parameters, Json));
                tools.Add(new JsonObject { ["type"] = "function", ["function"] = fn });
            }
            root["tools"] = tools;
        }

        if (!string.IsNullOrWhiteSpace(request.ToolChoice) && request.Tools is { Count: > 0 })
            root["tool_choice"] = request.ToolChoice;
        if (request.MaxTokens is int max)
            root["max_completion_tokens"] = max;
        root["temperature"] = request.Temperature;
        root["top_p"] = request.TopP;
        return root.ToJsonString(Json);
    }
}
