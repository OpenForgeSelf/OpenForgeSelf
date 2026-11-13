using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Controllers.UnifiedAI;

namespace ForgeSelf.Api.Services.AI.Proxy;

internal sealed class WireDelta
{
    public string? Id { get; init; }
    public string? Model { get; init; }
    public string? Text { get; init; }
    public int? ToolIndex { get; init; }
    public string? ToolId { get; init; }
    public string? ToolName { get; init; }
    public string? ToolArgs { get; init; }
    public string? Finish { get; init; }
    public int? InputTokens { get; init; }
    public int? OutputTokens { get; init; }
    public int? TotalTokens { get; init; }
    public bool Done { get; init; }
}

/// <summary>跨协议时把上游 SSE 收成入站协议的 SSE。同协议不走这里。</summary>
internal static class WireStreamTranslator
{
    public static async Task CopyAsync(
        Stream upstream,
        Stream downstream,
        LlmWireProtocol outbound,
        LlmWireProtocol inbound,
        CancellationToken cancellationToken)
    {
        var deltas = outbound == LlmWireProtocol.Responses
            ? ReadResponses(upstream, cancellationToken)
            : ReadChat(upstream, cancellationToken);

        if (inbound == LlmWireProtocol.Responses)
            await WriteResponses(downstream, deltas, cancellationToken);
        else if (inbound == LlmWireProtocol.AnthropicMessages)
            await WriteAnthropic(downstream, deltas, cancellationToken);
        else
            await WriteChat(downstream, deltas, cancellationToken);
    }

    private static async IAsyncEnumerable<WireDelta> ReadChat(
        Stream upstream,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(upstream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
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
                yield return new WireDelta { Done = true };
                yield break;
            }

            JsonObject? obj;
            try { obj = JsonNode.Parse(payload) as JsonObject; }
            catch { continue; }
            if (obj is null)
                continue;

            var usage = obj["usage"] as JsonObject;
            var choice = obj["choices"] is JsonArray choices && choices.Count > 0 ? choices[0] as JsonObject : null;
            var delta = choice?["delta"] as JsonObject;
            var tool = delta?["tool_calls"] is JsonArray tools && tools.Count > 0 ? tools[0] as JsonObject : null;
            var fn = tool?["function"] as JsonObject;
            yield return new WireDelta
            {
                Id = LlmJson.Str(obj["id"]),
                Model = LlmJson.Str(obj["model"]),
                Text = LlmJson.Str(delta?["content"]),
                ToolIndex = LlmJson.Int(tool?["index"]),
                ToolId = LlmJson.Str(tool?["id"]),
                ToolName = LlmJson.Str(fn?["name"]),
                ToolArgs = LlmJson.Str(fn?["arguments"]),
                Finish = LlmJson.Str(choice?["finish_reason"]),
                InputTokens = LlmJson.Int(usage?["prompt_tokens"]),
                OutputTokens = LlmJson.Int(usage?["completion_tokens"]),
                TotalTokens = LlmJson.Int(usage?["total_tokens"])
            };
        }
    }

    private static async IAsyncEnumerable<WireDelta> ReadResponses(
        Stream upstream,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(upstream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
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
                yield return new WireDelta { Done = true };
                yield break;
            }

            JsonObject? obj;
            try { obj = JsonNode.Parse(payload) as JsonObject; }
            catch { continue; }
            if (obj is null)
                continue;

            var type = LlmJson.Str(obj["type"]);
            if (type == "response.output_text.delta")
            {
                yield return new WireDelta { Text = LlmJson.Str(obj["delta"]), Id = LlmJson.Str(obj["item_id"]) };
            }
            else if (type == "response.output_item.added" && obj["item"] is JsonObject item && LlmJson.Str(item["type"]) == "function_call")
            {
                yield return new WireDelta
                {
                    ToolId = LlmJson.Str(item["call_id"]) ?? LlmJson.Str(item["id"]),
                    ToolName = LlmJson.Str(item["name"]),
                    ToolArgs = string.Empty,
                    ToolIndex = LlmJson.Int(obj["output_index"])
                };
            }
            else if (type == "response.function_call_arguments.delta")
            {
                yield return new WireDelta
                {
                    ToolId = LlmJson.Str(obj["item_id"]),
                    ToolArgs = LlmJson.Str(obj["delta"]) ?? string.Empty,
                    ToolIndex = LlmJson.Int(obj["output_index"])
                };
            }
            else if (type == "response.completed" && obj["response"] is JsonObject response)
            {
                var usage = response["usage"] as JsonObject;
                yield return new WireDelta
                {
                    Id = LlmJson.Str(response["id"]),
                    Model = LlmJson.Str(response["model"]),
                    Finish = "stop",
                    InputTokens = LlmJson.Int(usage?["input_tokens"]),
                    OutputTokens = LlmJson.Int(usage?["output_tokens"]),
                    TotalTokens = LlmJson.Int(usage?["total_tokens"])
                };
            }
        }
    }

    private static async Task WriteChat(Stream downstream, IAsyncEnumerable<WireDelta> deltas, CancellationToken cancellationToken)
    {
        var id = $"chatcmpl-{Guid.NewGuid():N}";
        string? model = null;
        var sawDone = false;
        await foreach (var delta in deltas.WithCancellation(cancellationToken))
        {
            if (!string.IsNullOrEmpty(delta.Id))
                id = delta.Id!;
            if (!string.IsNullOrEmpty(delta.Model))
                model = delta.Model;
            if (delta.Done)
            {
                sawDone = true;
                break;
            }

            if (!string.IsNullOrEmpty(delta.Text) || delta.ToolName != null || delta.ToolArgs != null || delta.ToolId != null)
            {
                var choiceDelta = new JsonObject();
                if (!string.IsNullOrEmpty(delta.Text))
                    choiceDelta["content"] = delta.Text;
                if (delta.ToolId != null || delta.ToolName != null || delta.ToolArgs != null)
                {
                    var fn = new JsonObject();
                    if (delta.ToolName != null)
                        fn["name"] = delta.ToolName;
                    if (delta.ToolArgs != null)
                        fn["arguments"] = delta.ToolArgs;
                    var call = new JsonObject
                    {
                        ["index"] = delta.ToolIndex ?? 0,
                        ["type"] = "function",
                        ["function"] = fn
                    };
                    if (!string.IsNullOrEmpty(delta.ToolId))
                        call["id"] = delta.ToolId;
                    choiceDelta["tool_calls"] = new JsonArray { call };
                }

                await WriteData(downstream, new JsonObject
                {
                    ["id"] = id,
                    ["object"] = "chat.completion.chunk",
                    ["model"] = model ?? string.Empty,
                    ["choices"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["index"] = 0,
                            ["delta"] = choiceDelta,
                            ["finish_reason"] = delta.Finish
                        }
                    }
                }, cancellationToken);
            }

            if (delta.InputTokens != null || delta.OutputTokens != null || delta.TotalTokens != null)
            {
                await WriteData(downstream, new JsonObject
                {
                    ["id"] = id,
                    ["object"] = "chat.completion.chunk",
                    ["model"] = model ?? string.Empty,
                    ["choices"] = new JsonArray(),
                    ["usage"] = new JsonObject
                    {
                        ["prompt_tokens"] = delta.InputTokens ?? 0,
                        ["completion_tokens"] = delta.OutputTokens ?? 0,
                        ["total_tokens"] = delta.TotalTokens ?? 0
                    }
                }, cancellationToken);
            }
        }

        if (sawDone)
            await WriteRaw(downstream, "data: [DONE]\n\n", cancellationToken);
    }

    private static async Task WriteResponses(Stream downstream, IAsyncEnumerable<WireDelta> deltas, CancellationToken cancellationToken)
    {
        var acc = new OpenAIResponsesStreamEvents.Accumulator();
        var responseId = $"resp_{Guid.NewGuid():N}";
        string model = string.Empty;
        var openedTools = new HashSet<int>();
        var sawDone = false;
        await foreach (var delta in deltas.WithCancellation(cancellationToken))
        {
            if (!string.IsNullOrEmpty(delta.Id) && delta.Id!.StartsWith("resp", StringComparison.Ordinal))
                responseId = delta.Id;
            if (!string.IsNullOrEmpty(delta.Model))
                model = delta.Model!;
            if (delta.InputTokens != null || delta.OutputTokens != null)
            {
                acc.Observe(new UnifiedStreamChunk
                {
                    Usage = new UnifiedUsage
                    {
                        PromptTokens = delta.InputTokens ?? 0,
                        CompletionTokens = delta.OutputTokens ?? 0,
                        TotalTokens = delta.TotalTokens ?? (delta.InputTokens ?? 0) + (delta.OutputTokens ?? 0)
                    }
                });
            }

            if (!string.IsNullOrEmpty(delta.Text))
            {
                foreach (var ev in OpenAIResponsesStreamEvents.TextDeltaEvents(acc, delta.Text))
                    await WriteData(downstream, ev, cancellationToken);
            }

            if (delta.ToolName != null || delta.ToolArgs != null || delta.ToolId != null)
            {
                var index = delta.ToolIndex ?? openedTools.Count;
                if (openedTools.Add(index) && delta.ToolName != null)
                {
                    await WriteData(downstream, new
                    {
                        type = "response.output_item.added",
                        output_index = index,
                        item = new
                        {
                            type = "function_call",
                            status = "in_progress",
                            call_id = delta.ToolId ?? string.Empty,
                            name = delta.ToolName,
                            arguments = string.Empty
                        }
                    }, cancellationToken);
                }

                if (!string.IsNullOrEmpty(delta.ToolArgs))
                {
                    await WriteData(downstream, new
                    {
                        type = "response.function_call_arguments.delta",
                        output_index = index,
                        delta = delta.ToolArgs
                    }, cancellationToken);
                }
            }

            if (delta.Done)
            {
                sawDone = true;
                break;
            }
        }

        foreach (var ev in OpenAIResponsesStreamEvents.FinishEvents(acc, responseId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), model))
            await WriteData(downstream, ev, cancellationToken);
        if (sawDone)
            await WriteRaw(downstream, "data: [DONE]\n\n", cancellationToken);
    }

    private static async Task WriteAnthropic(Stream downstream, IAsyncEnumerable<WireDelta> deltas, CancellationToken cancellationToken)
    {
        var id = $"msg_{Guid.NewGuid():N}";
        string model = string.Empty;
        var started = false;
        var blockOpen = false;
        var outputTokens = 0;
        await foreach (var delta in deltas.WithCancellation(cancellationToken))
        {
            if (!string.IsNullOrEmpty(delta.Model))
                model = delta.Model!;
            if (delta.OutputTokens != null)
                outputTokens = delta.OutputTokens.Value;
            if (!started)
            {
                started = true;
                await WriteEvent(downstream, "message_start", new JsonObject
                {
                    ["type"] = "message_start",
                    ["message"] = new JsonObject
                    {
                        ["id"] = id,
                        ["type"] = "message",
                        ["role"] = "assistant",
                        ["content"] = new JsonArray(),
                        ["model"] = model,
                        ["stop_reason"] = null,
                        ["usage"] = new JsonObject { ["input_tokens"] = delta.InputTokens ?? 0, ["output_tokens"] = 0 }
                    }
                }, cancellationToken);
            }

            if (!string.IsNullOrEmpty(delta.Text))
            {
                if (!blockOpen)
                {
                    blockOpen = true;
                    await WriteEvent(downstream, "content_block_start", new JsonObject
                    {
                        ["type"] = "content_block_start",
                        ["index"] = 0,
                        ["content_block"] = new JsonObject { ["type"] = "text", ["text"] = string.Empty }
                    }, cancellationToken);
                }

                await WriteEvent(downstream, "content_block_delta", new JsonObject
                {
                    ["type"] = "content_block_delta",
                    ["index"] = 0,
                    ["delta"] = new JsonObject { ["type"] = "text_delta", ["text"] = delta.Text }
                }, cancellationToken);
            }

            if (delta.Done)
                break;
        }

        if (blockOpen)
        {
            await WriteEvent(downstream, "content_block_stop", new JsonObject
            {
                ["type"] = "content_block_stop",
                ["index"] = 0
            }, cancellationToken);
        }

        await WriteEvent(downstream, "message_delta", new JsonObject
        {
            ["type"] = "message_delta",
            ["delta"] = new JsonObject { ["stop_reason"] = "end_turn" },
            ["usage"] = new JsonObject { ["output_tokens"] = outputTokens }
        }, cancellationToken);
        await WriteEvent(downstream, "message_stop", new JsonObject { ["type"] = "message_stop" }, cancellationToken);
    }

    private static async Task WriteData(Stream downstream, object payload, CancellationToken cancellationToken)
    {
        var json = payload is JsonNode node
            ? LlmJson.Write(node)
            : JsonSerializer.Serialize(payload, OpenAIResponsesStreamEvents.SerializerOptions);
        await WriteRaw(downstream, "data: " + json + "\n\n", cancellationToken);
    }

    private static async Task WriteEvent(Stream downstream, string name, JsonObject payload, CancellationToken cancellationToken)
    {
        await WriteRaw(downstream, $"event: {name}\ndata: {LlmJson.Write(payload)}\n\n", cancellationToken);
    }

    private static async Task WriteRaw(Stream downstream, string text, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await downstream.WriteAsync(bytes, cancellationToken);
        await downstream.FlushAsync(cancellationToken);
    }
}
