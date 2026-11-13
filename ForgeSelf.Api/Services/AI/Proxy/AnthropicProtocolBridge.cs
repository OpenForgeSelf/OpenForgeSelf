using System.Text.Json.Nodes;
using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Services.AI.Proxy;

/// <summary>
/// Anthropic Messages 与 Chat Completions 之间只搬已支持的字段：
/// model、文本、system、max_tokens、temperature、stream、tools、图片。不识图、不调视觉模型。
/// </summary>
internal static class AnthropicProtocolBridge
{
    public static JsonObject ToChat(JsonObject anthropic)
    {
        var chat = new JsonObject();
        Copy(anthropic, chat, "model");
        Copy(anthropic, chat, "temperature");
        Copy(anthropic, chat, "stream");
        var max = LlmJson.Int(anthropic["max_tokens"]);
        if (max != null)
            chat["max_tokens"] = max.Value;

        var messages = new JsonArray();
        var system = SystemText(anthropic["system"]);
        if (!string.IsNullOrEmpty(system))
            messages.Add(new JsonObject { ["role"] = "system", ["content"] = system });

        if (anthropic["messages"] is JsonArray source)
        {
            foreach (var node in source)
            {
                if (node is JsonObject message)
                    messages.Add(ToChatMessage(message));
            }
        }
        chat["messages"] = messages;

        if (anthropic["tools"] is JsonArray tools)
        {
            var mapped = new JsonArray();
            foreach (var node in tools)
            {
                if (node is not JsonObject tool)
                    continue;
                var fn = new JsonObject { ["name"] = LlmJson.Str(tool["name"]) ?? string.Empty };
                if (tool["description"] != null)
                    fn["description"] = tool["description"]!.DeepClone();
                if (tool["input_schema"] != null)
                    fn["parameters"] = tool["input_schema"]!.DeepClone();
                mapped.Add(new JsonObject { ["type"] = "function", ["function"] = fn });
            }
            chat["tools"] = mapped;
        }

        return chat;
    }

    public static JsonObject FromChatResponse(JsonObject chat)
    {
        var choice = chat["choices"] is JsonArray choices && choices.Count > 0
            ? choices[0] as JsonObject
            : null;
        var message = choice?["message"] as JsonObject;
        var content = new JsonArray();
        var text = ChatResponsesBridge.ExtractText(message?["content"]);
        if (!string.IsNullOrEmpty(text))
            content.Add(new JsonObject { ["type"] = "text", ["text"] = text });

        if (message?["tool_calls"] is JsonArray calls)
        {
            foreach (var node in calls)
            {
                if (node is not JsonObject call)
                    continue;
                var fn = call["function"] as JsonObject;
                JsonNode input = new JsonObject();
                var args = LlmJson.Str(fn?["arguments"]);
                if (!string.IsNullOrWhiteSpace(args))
                {
                    try { input = JsonNode.Parse(args) ?? input; }
                    catch { input = new JsonObject(); }
                }
                content.Add(new JsonObject
                {
                    ["type"] = "tool_use",
                    ["id"] = LlmJson.Str(call["id"]) ?? string.Empty,
                    ["name"] = LlmJson.Str(fn?["name"]) ?? string.Empty,
                    ["input"] = input
                });
            }
        }

        var finish = LlmJson.Str(choice?["finish_reason"]);
        var stop = finish switch
        {
            "tool_calls" => "tool_use",
            "length" => "max_tokens",
            _ => "end_turn"
        };
        var usage = chat["usage"] as JsonObject;
        return new JsonObject
        {
            ["id"] = LlmJson.Str(chat["id"]) ?? $"msg_{Guid.NewGuid():N}",
            ["type"] = "message",
            ["role"] = "assistant",
            ["content"] = content,
            ["model"] = LlmJson.Str(chat["model"]) ?? string.Empty,
            ["stop_reason"] = stop,
            ["usage"] = new JsonObject
            {
                ["input_tokens"] = LlmJson.Int(usage?["prompt_tokens"]) ?? 0,
                ["output_tokens"] = LlmJson.Int(usage?["completion_tokens"]) ?? 0
            }
        };
    }

    private static JsonObject ToChatMessage(JsonObject message)
    {
        var role = LlmJson.Str(message["role"]) ?? "user";
        var content = message["content"];
        if (LlmJson.Str(content) is string text)
            return new JsonObject { ["role"] = role, ["content"] = text };

        if (content is not JsonArray blocks)
            return new JsonObject { ["role"] = role, ["content"] = string.Empty };

        var parts = new JsonArray();
        var toolCalls = new JsonArray();
        foreach (var node in blocks)
        {
            if (node is not JsonObject block)
                continue;
            var type = LlmJson.Str(block["type"]);
            if (type == "text")
            {
                parts.Add(new JsonObject { ["type"] = "text", ["text"] = LlmJson.Str(block["text"]) ?? string.Empty });
            }
            else if (type == "image")
            {
                var url = ImageSourceToUrl(block["source"] as JsonObject);
                if (!string.IsNullOrEmpty(url))
                    parts.Add(new JsonObject { ["type"] = "image_url", ["image_url"] = new JsonObject { ["url"] = url } });
            }
            else if (type == "tool_use")
            {
                toolCalls.Add(new JsonObject
                {
                    ["id"] = LlmJson.Str(block["id"]) ?? string.Empty,
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = LlmJson.Str(block["name"]) ?? string.Empty,
                        ["arguments"] = block["input"]?.ToJsonString(ForgeJson.Default) ?? "{}"
                    }
                });
            }
            else if (type == "tool_result")
            {
                return new JsonObject
                {
                    ["role"] = "tool",
                    ["tool_call_id"] = LlmJson.Str(block["tool_use_id"]) ?? string.Empty,
                    ["content"] = ChatResponsesBridge.ExtractText(block["content"]) ?? LlmJson.Str(block["content"]) ?? string.Empty
                };
            }
        }

        var chat = new JsonObject { ["role"] = role };
        if (parts.Count == 1 && parts[0] is JsonObject only && LlmJson.Str(only["type"]) == "text")
            chat["content"] = LlmJson.Str(only["text"]) ?? string.Empty;
        else
            chat["content"] = parts;
        if (toolCalls.Count > 0)
            chat["tool_calls"] = toolCalls;
        return chat;
    }

    private static string? ImageSourceToUrl(JsonObject? source)
    {
        if (source is null)
            return null;
        var type = LlmJson.Str(source["type"]);
        if (type == "url")
            return LlmJson.Str(source["url"]);
        if (type == "base64")
        {
            var media = LlmJson.Str(source["media_type"]) ?? "image/png";
            var data = LlmJson.Str(source["data"]) ?? string.Empty;
            return $"data:{media};base64,{data}";
        }
        return null;
    }

    private static string? SystemText(JsonNode? system)
    {
        if (LlmJson.Str(system) is string text)
            return text;
        return ChatResponsesBridge.ExtractText(system);
    }

    private static void Copy(JsonObject from, JsonObject to, string name)
    {
        if (from[name] != null)
            to[name] = from[name]!.DeepClone();
    }
}
