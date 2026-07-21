using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenForgeSelf.Backend.Entities;
using OpenForgeSelf.Backend.Services;
using OpenForgeSelf.Backend.Services.AI;
using OpenForgeSelf.Backend.Services.AI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Controllers.UnifiedAI;

[ApiController]
[Route("v1/anthropic")]
[Authorize("ApiKeyPolicy")]
public class AnthropicMessagesController : ControllerBase
{
    private readonly AIProviderRegistry _registry;
    private readonly ILogService _logService;
    private readonly IChatRecordService _chatRecordService;

    public AnthropicMessagesController(AIProviderRegistry registry, ILogService logService, IChatRecordService chatRecordService)
    {
        _registry = registry;
        _logService = logService;
        _chatRecordService = chatRecordService;
    }

    [HttpPost("messages")]
    public async Task<IActionResult> CreateMessage([FromBody] AnthropicMessageRequest request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        ChatRecord? record = null;

        try
        {
            if (request == null || request.Messages == null || request.Messages.Count == 0)
                return BadRequest(new { error = new { type = "invalid_request_error", message = "messages is required" } });

            if (string.IsNullOrEmpty(request.Model))
                return BadRequest(new { error = new { type = "invalid_request_error", message = "model is required" } });

            if (request.MaxTokens <= 0)
                return BadRequest(new { error = new { type = "invalid_request_error", message = "max_tokens is required and must be > 0" } });

            var provider = _registry.GetProviderByModel(request.Model);
            if (provider == null)
                return BadRequest(new { error = new { type = "invalid_request_error", message = $"No provider found for model '{request.Model}'" } });

            var unifiedRequest = ConvertToUnifiedRequest(request);

            _logService.Info("Anthropic Message 请求 - 模型: {0}, 提供者: {1}, 消息数: {2}",
                request.Model, provider.ProviderName, request.Messages.Count);

            // 创建聊天记录
            var sessionId = Request.Headers["X-Session-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
            record = new ChatRecord
            {
                SessionId = sessionId,
                Style = "Anthropic_Messages",
                Model = request.Model,
                RequestMethod = "POST",
                RequestPath = "/v1/anthropic/messages",
                RequestHeaders = JsonSerializer.Serialize(Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString())),
                RequestBody = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = false }),
                Temperature = request.Temperature ?? 1.0,
                MaxTokens = request.MaxTokens,
                MessageCount = request.Messages?.Count ?? 0,
                ToolCallCount = request.Tools?.Count ?? 0,
                HasReasoning = false,
                CreatedTime = DateTime.Now
            };

            if (request.Stream)
            {
                await HandleStreamAsync(unifiedRequest, provider, request.Model, record, stopwatch, cancellationToken);
                return new EmptyResult();
            }
            else
            {
                var response = await provider.ChatAsync(unifiedRequest, cancellationToken);
                var anthropicResponse = ConvertToAnthropicResponse(response, request.Model);

                // 更新记录
                stopwatch.Stop();
                record.ResponseStatus = 200;
                record.ResponseHeaders = JsonSerializer.Serialize(Response.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()));
                record.ResponseBody = JsonSerializer.Serialize(anthropicResponse, new JsonSerializerOptions { WriteIndented = false });
                record.DurationMs = stopwatch.ElapsedMilliseconds;
                await _chatRecordService.SaveRecordAsync(record);

                return Ok(anthropicResponse);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // 更新错误记录
            if (record != null)
            {
                record.ResponseStatus = 500;
                record.ResponseBody = JsonSerializer.Serialize(new { error = ex.Message });
                record.DurationMs = stopwatch.ElapsedMilliseconds;
                await _chatRecordService.SaveRecordAsync(record);
            }

            _logService.Error("Anthropic Messages 处理失败: {0}", ex.Message);
            return StatusCode(500, new { error = new { type = "api_error", message = ex.Message } });
        }
    }

    private async Task HandleStreamAsync(UnifiedChatRequest request, IAIProvider provider, string modelName, ChatRecord record, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("X-Accel-Buffering", "no");

        var messageId = $"msg_{Guid.NewGuid():N}";
        var responseBody = new StringBuilder();

        try
        {
            var messageStart = new
            {
                type = "message_start",
                message = new
                {
                    id = messageId,
                    type = "message",
                    role = "assistant",
                    model = modelName,
                    content = new object[0],
                    stop_reason = (string?)null,
                    stop_sequence = (string?)null,
                    usage = new { input_tokens = 0, output_tokens = 0 }
                }
            };
            await WriteSseAsync("message_start", messageStart);
            responseBody.Append(JsonSerializer.Serialize(messageStart));

            var contentBlockStart = new
            {
                type = "content_block_start",
                index = 0,
                content_block = new { type = "text", text = string.Empty }
            };
            await WriteSseAsync("content_block_start", contentBlockStart);
            responseBody.Append(JsonSerializer.Serialize(contentBlockStart));

            await foreach (var chunk in provider.ChatStreamAsync(request, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (!string.IsNullOrEmpty(chunk.DeltaContent))
                {
                    var contentBlockDelta = new
                    {
                        type = "content_block_delta",
                        index = 0,
                        delta = new { type = "text_delta", text = chunk.DeltaContent }
                    };
                    await WriteSseAsync("content_block_delta", contentBlockDelta);
                    responseBody.Append(JsonSerializer.Serialize(contentBlockDelta));
                }
            }

            var contentBlockStop = new
            {
                type = "content_block_stop",
                index = 0
            };
            await WriteSseAsync("content_block_stop", contentBlockStop);
            responseBody.Append(JsonSerializer.Serialize(contentBlockStop));

            var messageDelta = new
            {
                type = "message_delta",
                delta = new
                {
                    stop_reason = "end_turn",
                    stop_sequence = (string?)null
                },
                usage = new { output_tokens = 0 }
            };
            await WriteSseAsync("message_delta", messageDelta);
            responseBody.Append(JsonSerializer.Serialize(messageDelta));

            await WriteSseAsync("message_stop", new { type = "message_stop" });
            responseBody.Append("{\"type\":\"message_stop\"}");

            // 更新记录
            stopwatch.Stop();
            record.ResponseStatus = 200;
            record.ResponseHeaders = JsonSerializer.Serialize(Response.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()));
            record.ResponseBody = responseBody.ToString();
            record.DurationMs = stopwatch.ElapsedMilliseconds;
            await _chatRecordService.SaveRecordAsync(record);
        }
        catch (Exception ex)
        {
            _logService.Error("Anthropic 流式响应异常: {0}", ex.Message);

            // 更新错误记录
            stopwatch.Stop();
            record.ResponseStatus = 500;
            record.ResponseBody = JsonSerializer.Serialize(new { error = ex.Message });
            record.DurationMs = stopwatch.ElapsedMilliseconds;
            await _chatRecordService.SaveRecordAsync(record);
        }
    }

    private async Task WriteSseAsync<T>(string eventName, T data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        await Response.WriteAsync($"event: {eventName}\n");
        await Response.WriteAsync($"data: {json}\n\n");
        await Response.Body.FlushAsync();
    }

    private UnifiedChatRequest ConvertToUnifiedRequest(AnthropicMessageRequest request)
    {
        var unified = new UnifiedChatRequest
        {
            Model = request.Model,
            MaxTokens = request.MaxTokens,
            Stream = request.Stream,
            Temperature = request.Temperature ?? 1.0,
            TopP = request.TopP ?? 1.0,
            Messages = new List<UnifiedChatMessage>(),
            SystemPrompt = request.System
        };

        foreach (var msg in request.Messages)
        {
            var contentText = ExtractContentText(msg.Content);
            var unifiedMsg = new UnifiedChatMessage
            {
                Role = msg.Role,
                Content = contentText
            };

            var toolUses = ExtractToolUses(msg.Content);
            if (toolUses != null && toolUses.Count > 0)
            {
                unifiedMsg.ToolCalls = toolUses;
            }

            var toolResults = ExtractToolResults(msg.Content);
            if (toolResults != null && toolResults.Count > 0 && toolResults[0] != null)
            {
                unifiedMsg.ToolCallId = toolResults[0]!.ToolUseId;
                unifiedMsg.Content = toolResults[0]!.ContentText ?? string.Empty;
            }

            unified.Messages.Add(unifiedMsg);
        }

        if (request.Tools != null && request.Tools.Count > 0)
        {
            unified.Tools = request.Tools.Select(t => new UnifiedToolDefinition
            {
                Name = t.Name,
                Description = t.Description ?? string.Empty,
                Parameters = t.InputSchema ?? new object()
            }).ToList();
        }

        return unified;
    }

    private static string ExtractContentText(AnthropicContent content)
    {
        if (content.IsText)
            return content.TextValue ?? string.Empty;

        if (content.Blocks != null)
        {
            var texts = content.Blocks
                .Where(b => b.Type.Equals("text", StringComparison.OrdinalIgnoreCase))
                .Select(b => b.Text)
                .Where(t => !string.IsNullOrEmpty(t));
            return string.Join("\n", texts);
        }

        return string.Empty;
    }

    private static List<UnifiedToolCall>? ExtractToolUses(AnthropicContent content)
    {
        if (content.Blocks == null) return null;

        var toolUses = content.Blocks
            .Where(b => b.Type.Equals("tool_use", StringComparison.OrdinalIgnoreCase))
            .Select(b => new UnifiedToolCall
            {
                Id = b.Id ?? string.Empty,
                Name = b.Name ?? string.Empty,
                Arguments = b.Input != null
                    ? JsonSerializer.Serialize(b.Input, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
                    : string.Empty
            })
            .ToList();

        return toolUses.Count > 0 ? toolUses : null;
    }

    private static List<AnthropicToolResultBlock?>? ExtractToolResults(AnthropicContent content)
    {
        if (content.Blocks == null) return null;

        var toolResults = content.Blocks
            .Where(b => b.Type.Equals("tool_result", StringComparison.OrdinalIgnoreCase))
            .Select(b => new AnthropicToolResultBlock
            {
                ToolUseId = b.ToolUseId,
                ContentText = b.Content?.ToString()
            })
            .ToList();

        return toolResults.Count > 0 ? toolResults! : null;
    }

    private object ConvertToAnthropicResponse(UnifiedChatResponse response, string requestModel)
    {
        var firstChoice = response.Choices.FirstOrDefault();
        var contentBlocks = new List<object>();

        if (firstChoice != null && !string.IsNullOrEmpty(firstChoice.Content))
        {
            contentBlocks.Add(new { type = "text", text = firstChoice.Content });
        }

        if (firstChoice?.ToolCalls != null && firstChoice.ToolCalls.Count > 0)
        {
            foreach (var tc in firstChoice.ToolCalls)
            {
                object inputObj;
                try
                {
                    inputObj = JsonDocument.Parse(tc.Arguments).RootElement;
                }
                catch
                {
                    inputObj = new { };
                }

                contentBlocks.Add(new
                {
                    type = "tool_use",
                    id = tc.Id,
                    name = tc.Name,
                    input = inputObj
                });
            }
        }

        var stopReason = response.FinishReason switch
        {
            "stop" or "end_turn" => "end_turn",
            "tool_calls" or "tool_use" => "tool_use",
            "max_tokens" => "max_tokens",
            "stop_sequence" => "stop_sequence",
            _ => "end_turn"
        };

        return new
        {
            id = string.IsNullOrEmpty(response.Id) ? $"msg_{Guid.NewGuid():N}" : response.Id,
            type = "message",
            role = "assistant",
            content = contentBlocks,
            model = string.IsNullOrEmpty(response.Model) ? requestModel : response.Model,
            stop_reason = stopReason,
            stop_sequence = (string?)null,
            usage = new
            {
                input_tokens = response.Usage?.PromptTokens ?? 0,
                output_tokens = response.Usage?.CompletionTokens ?? 0
            }
        };
    }
}

#region Anthropic DTOs

public class AnthropicMessageRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<AnthropicMessage>? Messages { get; set; }

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 1024;

    [JsonPropertyName("system")]
    public string? System { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }

    [JsonPropertyName("tools")]
    public List<AnthropicTool>? Tools { get; set; }

    [JsonPropertyName("tool_choice")]
    public object? ToolChoice { get; set; }

    [JsonPropertyName("stop_sequences")]
    public List<string>? StopSequences { get; set; }
}

public class AnthropicMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    [JsonConverter(typeof(AnthropicContentConverter))]
    public AnthropicContent? Content { get; set; }
}

public class AnthropicContent
{
    public bool IsText { get; set; }
    public string? TextValue { get; set; }
    public List<AnthropicContentBlock>? Blocks { get; set; }
}

public class AnthropicContentBlock
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("input")]
    public object? Input { get; set; }

    [JsonPropertyName("tool_use_id")]
    public string? ToolUseId { get; set; }

    [JsonPropertyName("content")]
    public object? Content { get; set; }
}

public class AnthropicTool
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("input_schema")]
    public object? InputSchema { get; set; }
}

public class AnthropicToolResultBlock
{
    public string? ToolUseId { get; set; }
    public string? ContentText { get; set; }
}

public class AnthropicContentConverter : JsonConverter<AnthropicContent>
{
    public override AnthropicContent? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return new AnthropicContent { IsText = true, TextValue = reader.GetString() };

            case JsonTokenType.StartArray:
                var blocks = JsonSerializer.Deserialize<List<AnthropicContentBlock>>(ref reader, options);
                return new AnthropicContent { IsText = false, Blocks = blocks };

            default:
                throw new JsonException($"Unexpected token type: {reader.TokenType} for content");
        }
    }

    public override void Write(Utf8JsonWriter writer, AnthropicContent value, JsonSerializerOptions options)
    {
        if (value.IsText)
        {
            writer.WriteStringValue(value.TextValue);
        }
        else if (value.Blocks != null)
        {
            JsonSerializer.Serialize(writer, value.Blocks, options);
        }
        else
        {
            writer.WriteStringValue(string.Empty);
        }
    }
}

#endregion
