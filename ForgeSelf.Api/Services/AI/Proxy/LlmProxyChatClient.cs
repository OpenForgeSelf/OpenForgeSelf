using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using ForgeSelf.Abstractions;
using Microsoft.Extensions.AI;

namespace ForgeSelf.Api.Services.AI.Proxy;

/// <summary>
/// 实验性 Agent 入站：把 M.E.AI 消息收成 Chat Completions JSON，交给 <see cref="ILlmProxyPipeline"/>。
/// 别名、协议和出站都不在这里处理。
/// </summary>
public sealed class LlmProxyChatClient : IChatClient
{
    private static readonly JsonSerializerOptions Json = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly ILlmProxyPipeline _pipeline;
    private readonly string _modelId;

    public LlmProxyChatClient(ILlmProxyPipeline pipeline, string modelId)
    {
        _pipeline = pipeline;
        _modelId = modelId;
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in GetStreamingResponseAsync(messages, options, cancellationToken))
            updates.Add(update);
        return updates.ToChatResponse();
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var model = string.IsNullOrWhiteSpace(options?.ModelId) ? _modelId : options!.ModelId!;
        var json = BuildJson(model, messages, options);
        var result = await _pipeline.ExecuteAsync(
            new LlmProxyRequest { Protocol = LlmWireProtocol.ChatCompletions, BodyJson = json }, cancellationToken);
        await using (result)
        {
            if (result.StatusCode is < 200 or >= 300)
            {
                var error = result.Body == null ? string.Empty : await new StreamReader(result.Body).ReadToEndAsync(cancellationToken);
                throw new InvalidOperationException($"上游返回 {result.StatusCode}: {error}");
            }

            if (result.Body == null)
                yield break;

            var pending = new List<(string Id, string Name, string Args)>();
            string? responseId = null;
            await foreach (var chunk in ReadChunks(result.Body, cancellationToken))
            {
                if (!string.IsNullOrEmpty(chunk.Id))
                    responseId = chunk.Id;
                if (!string.IsNullOrEmpty(chunk.Text))
                {
                    var update = new ChatResponseUpdate(ChatRole.Assistant, [new TextContent(chunk.Text)]);
                    if (!string.IsNullOrEmpty(responseId))
                        update.ResponseId = responseId;
                    yield return update;
                }

                if (chunk.ToolId != null || chunk.ToolName != null || chunk.ToolArgs != null)
                    Accumulate(pending, chunk.ToolId, chunk.ToolName, chunk.ToolArgs);
            }

            foreach (var call in pending)
            {
                if (string.IsNullOrWhiteSpace(call.Name))
                    continue;
                var update = new ChatResponseUpdate(ChatRole.Assistant,
                [
                    new FunctionCallContent(
                        string.IsNullOrEmpty(call.Id) ? $"call-{Guid.NewGuid():N}" : call.Id,
                        call.Name,
                        ParseArguments(call.Args))
                ]);
                if (!string.IsNullOrEmpty(responseId))
                    update.ResponseId = responseId;
                yield return update;
            }
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }

    private static string BuildJson(string model, IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        var root = new JsonObject
        {
            ["model"] = model,
            ["stream"] = true,
            ["messages"] = new JsonArray()
        };
        var list = (JsonArray)root["messages"]!;
        if (!string.IsNullOrWhiteSpace(options?.Instructions))
            list.Add(new JsonObject { ["role"] = "system", ["content"] = options!.Instructions });

        foreach (var message in messages)
        {
            if (message.Role == ChatRole.System)
            {
                var text = TextOf(message);
                if (!string.IsNullOrEmpty(text))
                    list.Add(new JsonObject { ["role"] = "system", ["content"] = text });
                continue;
            }

            var item = new JsonObject
            {
                ["role"] = message.Role == ChatRole.Tool ? "tool" : message.Role == ChatRole.Assistant ? "assistant" : "user",
                ["content"] = TextOf(message) ?? string.Empty
            };
            foreach (var content in message.Contents)
            {
                if (content is FunctionCallContent call && !string.IsNullOrWhiteSpace(call.Name))
                {
                    var calls = item["tool_calls"] as JsonArray ?? new JsonArray();
                    item["tool_calls"] = calls;
                    calls.Add(new JsonObject
                    {
                        ["id"] = call.CallId,
                        ["type"] = "function",
                        ["function"] = new JsonObject
                        {
                            ["name"] = call.Name,
                            ["arguments"] = JsonSerializer.Serialize(call.Arguments ?? new Dictionary<string, object?>(), Json)
                        }
                    });
                }
                else if (content is FunctionResultContent result)
                {
                    item["role"] = "tool";
                    item["tool_call_id"] = result.CallId;
                    if (string.IsNullOrEmpty(LlmJson.Str(item["content"])))
                        item["content"] = result.Result?.ToString() ?? string.Empty;
                }
            }
            list.Add(item);
        }

        if (options?.Tools is { Count: > 0 })
        {
            var tools = new JsonArray();
            foreach (var tool in options.Tools)
            {
                if (tool is not AIFunction function)
                    continue;
                var fn = new JsonObject
                {
                    ["name"] = function.Name,
                    ["description"] = function.Description ?? string.Empty,
                    ["parameters"] = JsonNode.Parse(function.JsonSchema.GetRawText())
                };
                tools.Add(new JsonObject { ["type"] = "function", ["function"] = fn });
            }
            if (tools.Count > 0)
                root["tools"] = tools;
        }

        if (options?.ToolMode is RequiredChatToolMode && options.Tools is { Count: > 0 })
            root["tool_choice"] = "required";
        if (options?.Temperature is { } temperature)
            root["temperature"] = temperature;
        if (options?.TopP is { } topP)
            root["top_p"] = topP;
        if (options?.MaxOutputTokens is { } max)
            root["max_completion_tokens"] = max;
        return root.ToJsonString(Json);
    }

    private static async IAsyncEnumerable<SseChunk> ReadChunks(
        Stream body,
        [EnumeratorCancellation] CancellationToken cancellationToken)
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
                yield break;
            JsonObject? obj;
            try { obj = JsonNode.Parse(payload) as JsonObject; }
            catch { continue; }
            if (obj is null)
                continue;
            var choice = obj["choices"] is JsonArray choices && choices.Count > 0 ? choices[0] as JsonObject : null;
            var delta = choice?["delta"] as JsonObject;
            var tool = delta?["tool_calls"] is JsonArray tools && tools.Count > 0 ? tools[0] as JsonObject : null;
            var fn = tool?["function"] as JsonObject;
            yield return new SseChunk(
                LlmJson.Str(obj["id"]),
                LlmJson.Str(delta?["content"]),
                LlmJson.Str(tool?["id"]),
                LlmJson.Str(fn?["name"]),
                LlmJson.Str(fn?["arguments"]));
        }
    }

    private static void Accumulate(List<(string Id, string Name, string Args)> pending, string? id, string? name, string? args)
    {
        if (!string.IsNullOrEmpty(id))
        {
            pending.Add((id, name ?? string.Empty, args ?? string.Empty));
            return;
        }
        if (pending.Count == 0)
            return;
        var last = pending[^1];
        pending[^1] = (last.Id, last.Name + (name ?? string.Empty), last.Args + (args ?? string.Empty));
    }

    private static string? TextOf(ChatMessage message)
    {
        var sb = new StringBuilder();
        foreach (var content in message.Contents)
        {
            if (content is TextContent { Text: { Length: > 0 } text })
            {
                if (sb.Length > 0)
                    sb.Append('\n');
                sb.Append(text);
            }
        }
        return sb.Length == 0 ? null : sb.ToString();
    }

    private static Dictionary<string, object?> ParseArguments(string? json)
    {
        var dict = new Dictionary<string, object?>();
        if (string.IsNullOrWhiteSpace(json))
            return dict;
        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var prop in doc.RootElement.EnumerateObject())
                dict[prop.Name] = prop.Value.Clone();
        }
        catch
        {
            // 参数不是对象时交给工具侧报错
        }
        return dict;
    }

    private sealed record SseChunk(string? Id, string? Text, string? ToolId, string? ToolName, string? ToolArgs);
}
