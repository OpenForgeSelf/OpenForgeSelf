using System.Text.Json.Nodes;
using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Services.AI.Proxy;

/// <summary>
/// Chat Completions 与 OpenAI Responses 的标准字段映射。没有标准对应物的字段丢掉，名单见 <see cref="ChatToResponsesDropped"/>。
/// </summary>
internal static class ChatResponsesBridge
{
    /// <summary>Chat → Responses 时明确丢掉的字段（没有 Responses 标准对应物）。</summary>
    public static readonly string[] ChatToResponsesDropped =
    [
        "n", "stop", "presence_penalty", "frequency_penalty", "logit_bias",
        "stream_options", "logprobs", "top_logprobs", "seed"
    ];

    public static JsonObject ToResponsesRequest(JsonObject chat)
    {
        var responses = new JsonObject();
        CopyIfPresent(chat, responses, "model");
        CopyIfPresent(chat, responses, "temperature");
        CopyIfPresent(chat, responses, "top_p");
        CopyIfPresent(chat, responses, "stream");
        CopyIfPresent(chat, responses, "user");
        CopyIfPresent(chat, responses, "metadata");
        CopyIfPresent(chat, responses, "parallel_tool_calls");

        var max = LlmJson.Int(chat["max_completion_tokens"]) ?? LlmJson.Int(chat["max_tokens"]);
        if (max != null)
            responses["max_output_tokens"] = max.Value;

        var effort = LlmJson.Str(chat["reasoning_effort"]);
        if (!string.IsNullOrEmpty(effort))
            responses["reasoning"] = new JsonObject { ["effort"] = effort };

        MapResponseFormat(chat, responses);
        MapToolsToResponses(chat, responses);
        MapToolChoiceToResponses(chat, responses);
        MapMessagesToResponses(chat, responses);
        return responses;
    }

    public static JsonObject ToChatRequest(JsonObject responses)
    {
        var chat = new JsonObject();
        CopyIfPresent(responses, chat, "model");
        CopyIfPresent(responses, chat, "temperature");
        CopyIfPresent(responses, chat, "top_p");
        CopyIfPresent(responses, chat, "stream");
        // OpenAI 兼容上游只有收到 stream_options.include_usage 才会在流末尾给出 token。
        // 对照 responses-proxy：流式 Responses→Chat 固定带上这一项，再把 prompt/completion/total 填回 usage。
        if (LlmJson.Bool(responses["stream"]))
            chat["stream_options"] = new JsonObject { ["include_usage"] = true };
        CopyIfPresent(responses, chat, "user");
        CopyIfPresent(responses, chat, "metadata");
        CopyIfPresent(responses, chat, "parallel_tool_calls");

        var max = LlmJson.Int(responses["max_output_tokens"]);
        if (max != null)
            chat["max_completion_tokens"] = max.Value;

        if (responses["reasoning"] is JsonObject reasoning)
        {
            var effort = LlmJson.Str(reasoning["effort"]);
            if (!string.IsNullOrEmpty(effort))
                chat["reasoning_effort"] = effort;
        }

        MapResponseFormatBack(responses, chat);
        MapToolsToChat(responses, chat);
        MapToolChoiceToChat(responses, chat);
        MapInputToMessages(responses, chat);
        return chat;
    }

    public static JsonObject ToResponsesResponse(JsonObject chat)
    {
        var choice = FirstObject(chat["choices"] as JsonArray);
        var message = choice?["message"] as JsonObject;
        var text = ExtractText(message?["content"]);
        var output = new JsonArray();
        // 对照 responses-proxy to_responses_format，并补 AI SDK 6 非流式必填的 annotations。
        // 缺 id 或 annotations 时，正文能落库，客户端仍报 Invalid JSON response。
        if (!string.IsNullOrEmpty(text) || message?["tool_calls"] is not JsonArray)
        {
            output.Add(new JsonObject
            {
                ["type"] = "message",
                ["id"] = $"msg_{Guid.NewGuid():N}",
                ["role"] = "assistant",
                ["status"] = "completed",
                ["content"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "output_text",
                        ["text"] = text ?? string.Empty,
                        ["annotations"] = new JsonArray()
                    }
                }
            });
        }

        if (message?["tool_calls"] is JsonArray calls)
        {
            foreach (var node in calls)
            {
                if (node is not JsonObject call)
                    continue;
                var fn = call["function"] as JsonObject;
                var callId = LlmJson.Str(call["id"]) ?? $"call_{Guid.NewGuid():N}";
                output.Add(new JsonObject
                {
                    ["type"] = "function_call",
                    ["id"] = callId,
                    ["call_id"] = callId,
                    ["name"] = LlmJson.Str(fn?["name"]) ?? string.Empty,
                    ["arguments"] = LlmJson.Str(fn?["arguments"]) ?? string.Empty
                });
            }
        }

        var usage = chat["usage"] as JsonObject;
        var details = usage?["completion_tokens_details"] as JsonObject;
        var finish = LlmJson.Str(choice?["finish_reason"]);
        var status = finish == "length" ? "incomplete" : "completed";
        var response = new JsonObject
        {
            ["id"] = LlmJson.Str(chat["id"]) ?? $"resp_{Guid.NewGuid():N}",
            ["object"] = "response",
            ["created_at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["status"] = status,
            ["model"] = LlmJson.Str(chat["model"]) ?? string.Empty,
            ["output"] = output,
            ["usage"] = new JsonObject
            {
                ["input_tokens"] = LlmJson.Int(usage?["prompt_tokens"]) ?? 0,
                ["output_tokens"] = LlmJson.Int(usage?["completion_tokens"]) ?? 0,
                ["total_tokens"] = LlmJson.Int(usage?["total_tokens"]) ?? 0,
                ["output_tokens_details"] = new JsonObject
                {
                    ["reasoning_tokens"] = LlmJson.Int(details?["reasoning_tokens"]) ?? 0
                }
            }
        };
        if (status == "incomplete")
            response["incomplete_details"] = new JsonObject { ["reason"] = "max_output_tokens" };
        return response;
    }

    public static JsonObject ToChatResponse(JsonObject responses)
    {
        var text = new System.Text.StringBuilder();
        var toolCalls = new JsonArray();
        if (responses["output"] is JsonArray output)
        {
            foreach (var node in output)
            {
                if (node is not JsonObject item)
                    continue;
                var type = LlmJson.Str(item["type"]);
                if (type is "message" or null)
                    text.Append(ExtractText(item["content"]));
                else if (type == "function_call")
                {
                    toolCalls.Add(new JsonObject
                    {
                        ["id"] = LlmJson.Str(item["call_id"]) ?? LlmJson.Str(item["id"]) ?? string.Empty,
                        ["type"] = "function",
                        ["function"] = new JsonObject
                        {
                            ["name"] = LlmJson.Str(item["name"]) ?? string.Empty,
                            ["arguments"] = LlmJson.Str(item["arguments"]) ?? string.Empty
                        }
                    });
                }
            }
        }
        else
        {
            text.Append(LlmJson.Str(responses["output_text"]) ?? string.Empty);
        }

        var message = new JsonObject
        {
            ["role"] = "assistant",
            ["content"] = text.ToString()
        };
        if (toolCalls.Count > 0)
            message["tool_calls"] = toolCalls;

        var finish = toolCalls.Count > 0 ? "tool_calls" : "stop";
        var usage = responses["usage"] as JsonObject;
        var prompt = LlmJson.Int(usage?["input_tokens"]) ?? LlmJson.Int(usage?["prompt_tokens"]) ?? 0;
        var completion = LlmJson.Int(usage?["output_tokens"]) ?? LlmJson.Int(usage?["completion_tokens"]) ?? 0;
        var total = LlmJson.Int(usage?["total_tokens"]) ?? prompt + completion;

        return new JsonObject
        {
            ["id"] = LlmJson.Str(responses["id"]) ?? $"chatcmpl-{Guid.NewGuid():N}",
            ["object"] = "chat.completion",
            ["model"] = LlmJson.Str(responses["model"]) ?? string.Empty,
            ["choices"] = new JsonArray
            {
                new JsonObject
                {
                    ["index"] = 0,
                    ["message"] = message,
                    ["finish_reason"] = finish
                }
            },
            ["usage"] = new JsonObject
            {
                ["prompt_tokens"] = prompt,
                ["completion_tokens"] = completion,
                ["total_tokens"] = total
            }
        };
    }

    private static void MapMessagesToResponses(JsonObject chat, JsonObject responses)
    {
        if (chat["messages"] is not JsonArray messages)
            return;

        var instructions = new List<string>();
        var input = new JsonArray();
        foreach (var node in messages)
        {
            if (node is not JsonObject message)
                continue;
            var role = LlmJson.Str(message["role"]) ?? "user";
            if (role is "system" or "developer")
            {
                var text = ExtractText(message["content"]);
                if (!string.IsNullOrEmpty(text))
                    instructions.Add(text);
                continue;
            }

            if (role == "tool")
            {
                input.Add(new JsonObject
                {
                    ["type"] = "function_call_output",
                    ["call_id"] = LlmJson.Str(message["tool_call_id"]) ?? string.Empty,
                    ["output"] = ExtractText(message["content"]) ?? string.Empty
                });
                continue;
            }

            if (role == "assistant")
            {
                var text = ExtractText(message["content"]);
                if (!string.IsNullOrEmpty(text))
                {
                    input.Add(new JsonObject
                    {
                        ["type"] = "message",
                        ["role"] = "assistant",
                        ["content"] = new JsonArray
                        {
                            new JsonObject { ["type"] = "output_text", ["text"] = text }
                        }
                    });
                }

                if (message["tool_calls"] is JsonArray calls)
                {
                    foreach (var callNode in calls)
                    {
                        if (callNode is not JsonObject call)
                            continue;
                        var fn = call["function"] as JsonObject;
                        input.Add(new JsonObject
                        {
                            ["type"] = "function_call",
                            ["call_id"] = LlmJson.Str(call["id"]) ?? string.Empty,
                            ["name"] = LlmJson.Str(fn?["name"]) ?? string.Empty,
                            ["arguments"] = LlmJson.Str(fn?["arguments"]) ?? string.Empty
                        });
                    }
                }
                continue;
            }

            input.Add(new JsonObject
            {
                ["type"] = "message",
                ["role"] = "user",
                ["content"] = ToResponsesContent(message["content"])
            });
        }

        if (instructions.Count > 0)
            responses["instructions"] = string.Join("\n\n", instructions);
        responses["input"] = input;
    }

    private static JsonArray ToResponsesContent(JsonNode? content)
    {
        var parts = new JsonArray();
        if (content is null)
            return parts;
        if (LlmJson.Str(content) is string text)
        {
            parts.Add(new JsonObject { ["type"] = "input_text", ["text"] = text });
            return parts;
        }

        if (content is not JsonArray array)
            return parts;

        foreach (var node in array)
        {
            if (node is not JsonObject part)
                continue;
            var type = LlmJson.Str(part["type"]);
            if (type == "text")
            {
                parts.Add(new JsonObject { ["type"] = "input_text", ["text"] = LlmJson.Str(part["text"]) ?? string.Empty });
            }
            else if (type == "image_url")
            {
                var url = ImageUrlString(part["image_url"]);
                if (!string.IsNullOrEmpty(url))
                    parts.Add(new JsonObject { ["type"] = "input_image", ["image_url"] = url });
            }
        }
        return parts;
    }

    private static void MapInputToMessages(JsonObject responses, JsonObject chat)
    {
        var messages = new JsonArray();
        var instructions = LlmJson.Str(responses["instructions"]);
        if (!string.IsNullOrEmpty(instructions))
            messages.Add(new JsonObject { ["role"] = "system", ["content"] = instructions });

        var input = responses["input"];
        if (LlmJson.Str(input) is string inputText)
        {
            messages.Add(new JsonObject { ["role"] = "user", ["content"] = inputText });
        }
        else if (input is JsonArray items)
        {
            JsonObject? pendingAssistant = null;
            JsonArray? pendingCalls = null;
            void FlushAssistant()
            {
                if (pendingAssistant == null && pendingCalls == null)
                    return;
                var message = pendingAssistant ?? new JsonObject { ["role"] = "assistant", ["content"] = string.Empty };
                if (pendingCalls is { Count: > 0 })
                    message["tool_calls"] = pendingCalls;
                messages.Add(message);
                pendingAssistant = null;
                pendingCalls = null;
            }

            foreach (var node in items)
            {
                if (node is not JsonObject item)
                    continue;
                var type = LlmJson.Str(item["type"]);
                if (type == "function_call")
                {
                    pendingCalls ??= new JsonArray();
                    pendingCalls.Add(new JsonObject
                    {
                        ["id"] = LlmJson.Str(item["call_id"]) ?? string.Empty,
                        ["type"] = "function",
                        ["function"] = new JsonObject
                        {
                            ["name"] = LlmJson.Str(item["name"]) ?? string.Empty,
                            ["arguments"] = LlmJson.Str(item["arguments"]) ?? string.Empty
                        }
                    });
                    continue;
                }

                if (type == "function_call_output")
                {
                    FlushAssistant();
                    messages.Add(new JsonObject
                    {
                        ["role"] = "tool",
                        ["tool_call_id"] = LlmJson.Str(item["call_id"]) ?? string.Empty,
                        ["content"] = LlmJson.Str(item["output"]) ?? ExtractText(item["output"]) ?? string.Empty
                    });
                    continue;
                }

                FlushAssistant();
                var role = LlmJson.Str(item["role"]) ?? "user";
                if (role == "assistant")
                {
                    pendingAssistant = new JsonObject
                    {
                        ["role"] = "assistant",
                        ["content"] = ExtractText(item["content"]) ?? string.Empty
                    };
                    continue;
                }

                messages.Add(new JsonObject
                {
                    ["role"] = role,
                    ["content"] = ToChatContent(item["content"])
                });
            }
            FlushAssistant();
        }

        chat["messages"] = messages;
    }

    private static JsonNode ToChatContent(JsonNode? content)
    {
        if (content is null)
            return string.Empty;
        if (LlmJson.Str(content) is string text)
            return text;
        if (content is not JsonArray array)
            return ExtractText(content) ?? string.Empty;

        var parts = new JsonArray();
        foreach (var node in array)
        {
            if (node is not JsonObject part)
                continue;
            var type = LlmJson.Str(part["type"]);
            if (type is "input_text" or "output_text" or "text")
            {
                parts.Add(new JsonObject { ["type"] = "text", ["text"] = LlmJson.Str(part["text"]) ?? string.Empty });
            }
            else if (type == "input_image")
            {
                var url = LlmJson.Str(part["image_url"]) ?? ImageUrlString(part["image_url"]);
                if (!string.IsNullOrEmpty(url))
                {
                    parts.Add(new JsonObject
                    {
                        ["type"] = "image_url",
                        ["image_url"] = new JsonObject { ["url"] = url }
                    });
                }
            }
        }

        if (parts.Count == 1 && parts[0] is JsonObject only && LlmJson.Str(only["type"]) == "text")
            return LlmJson.Str(only["text"]) ?? string.Empty;
        return parts;
    }

    private static void MapToolsToResponses(JsonObject chat, JsonObject responses)
    {
        if (chat["tools"] is not JsonArray tools)
            return;
        var mapped = new JsonArray();
        foreach (var node in tools)
        {
            if (node is not JsonObject tool)
                continue;
            var fn = tool["function"] as JsonObject ?? tool;
            var item = new JsonObject
            {
                ["type"] = "function",
                ["name"] = LlmJson.Str(fn["name"]) ?? string.Empty
            };
            if (fn["description"] != null)
                item["description"] = fn["description"]!.DeepClone();
            if (fn["parameters"] != null)
                item["parameters"] = fn["parameters"]!.DeepClone();
            mapped.Add(item);
        }
        responses["tools"] = mapped;
    }

    private static void MapToolsToChat(JsonObject responses, JsonObject chat)
    {
        if (responses["tools"] is not JsonArray tools)
            return;
        var mapped = new JsonArray();
        foreach (var node in tools)
        {
            if (node is not JsonObject tool)
                continue;
            var fn = new JsonObject { ["name"] = LlmJson.Str(tool["name"]) ?? string.Empty };
            if (tool["description"] != null)
                fn["description"] = tool["description"]!.DeepClone();
            if (tool["parameters"] != null)
                fn["parameters"] = tool["parameters"]!.DeepClone();
            mapped.Add(new JsonObject { ["type"] = "function", ["function"] = fn });
        }
        chat["tools"] = mapped;
    }

    private static void MapToolChoiceToResponses(JsonObject chat, JsonObject responses)
    {
        // 类级守卫（PC: tool_choice-without-tools）：源请求无非空 tools 时丢弃 tool_choice，否则上游 400。
        if (chat["tools"] is not JsonArray { Count: > 0 })
            return;
        var choice = chat["tool_choice"];
        if (choice is null)
            return;
        if (LlmJson.Str(choice) is string text)
        {
            responses["tool_choice"] = text;
            return;
        }

        if (choice is JsonObject obj)
        {
            var fn = obj["function"] as JsonObject;
            var name = LlmJson.Str(fn?["name"]) ?? LlmJson.Str(obj["name"]);
            if (!string.IsNullOrEmpty(name))
                responses["tool_choice"] = new JsonObject { ["type"] = "function", ["name"] = name };
        }
    }

    private static void MapToolChoiceToChat(JsonObject responses, JsonObject chat)
    {
        // 类级守卫（PC: tool_choice-without-tools）：源请求无非空 tools 时丢弃 tool_choice，否则上游 400。
        if (responses["tools"] is not JsonArray { Count: > 0 })
            return;
        var choice = responses["tool_choice"];
        if (choice is null)
            return;
        if (LlmJson.Str(choice) is string text)
        {
            chat["tool_choice"] = text;
            return;
        }

        if (choice is JsonObject obj)
        {
            var name = LlmJson.Str(obj["name"]);
            if (!string.IsNullOrEmpty(name))
            {
                chat["tool_choice"] = new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject { ["name"] = name }
                };
            }
        }
    }

    private static void MapResponseFormat(JsonObject chat, JsonObject responses)
    {
        if (chat["response_format"] is not JsonObject format)
            return;
        var type = LlmJson.Str(format["type"]);
        if (type == "json_object")
        {
            responses["text"] = new JsonObject
            {
                ["format"] = new JsonObject { ["type"] = "json_object" }
            };
            return;
        }

        if (type == "json_schema" && format["json_schema"] is JsonObject schema)
        {
            var mapped = new JsonObject { ["type"] = "json_schema" };
            if (schema["name"] != null)
                mapped["name"] = schema["name"]!.DeepClone();
            if (schema["schema"] != null)
                mapped["schema"] = schema["schema"]!.DeepClone();
            if (schema["strict"] != null)
                mapped["strict"] = schema["strict"]!.DeepClone();
            responses["text"] = new JsonObject { ["format"] = mapped };
        }
    }

    private static void MapResponseFormatBack(JsonObject responses, JsonObject chat)
    {
        if (responses["text"] is not JsonObject text || text["format"] is not JsonObject format)
            return;
        var type = LlmJson.Str(format["type"]);
        if (type == "json_object")
        {
            chat["response_format"] = new JsonObject { ["type"] = "json_object" };
            return;
        }

        if (type == "json_schema")
        {
            var schema = new JsonObject();
            if (format["name"] != null)
                schema["name"] = format["name"]!.DeepClone();
            if (format["schema"] != null)
                schema["schema"] = format["schema"]!.DeepClone();
            if (format["strict"] != null)
                schema["strict"] = format["strict"]!.DeepClone();
            chat["response_format"] = new JsonObject { ["type"] = "json_schema", ["json_schema"] = schema };
        }
    }

    internal static string? ExtractText(JsonNode? content)
    {
        if (content is null)
            return null;
        if (LlmJson.Str(content) is string text)
            return text;
        if (content is not JsonArray array)
            return null;
        var parts = new List<string>();
        foreach (var node in array)
        {
            if (node is not JsonObject part)
                continue;
            var type = LlmJson.Str(part["type"]);
            if (type is "text" or "input_text" or "output_text" or null)
            {
                var piece = LlmJson.Str(part["text"]);
                if (!string.IsNullOrEmpty(piece))
                    parts.Add(piece);
            }
        }
        return parts.Count == 0 ? null : string.Join(string.Empty, parts);
    }

    private static string? ImageUrlString(JsonNode? image)
    {
        if (LlmJson.Str(image) is string url)
            return url;
        if (image is JsonObject obj)
            return LlmJson.Str(obj["url"]);
        return null;
    }

    private static JsonObject? FirstObject(JsonArray? array)
        => array is { Count: > 0 } && array[0] is JsonObject obj ? obj : null;

    private static void CopyIfPresent(JsonObject from, JsonObject to, string name)
    {
        if (from[name] != null)
            to[name] = from[name]!.DeepClone();
    }
}
