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
[Route("v1/responses")]
[Authorize("ApiKeyPolicy")]
public class OpenAIResponsesController : ControllerBase
{
    private readonly AIProviderRegistry _registry;
    private readonly ILogService _logService;
    private readonly IChatTurnService _chatTurnService;
    private readonly IChatTurnStreamRecorder _streamRecorder;
    private readonly IChatSessionService _chatSessionService;

    public OpenAIResponsesController(AIProviderRegistry registry, ILogService logService, IChatTurnService chatTurnService, IChatTurnStreamRecorder streamRecorder, IChatSessionService chatSessionService)
    {
        _registry = registry;
        _logService = logService;
        _chatTurnService = chatTurnService;
        _streamRecorder = streamRecorder;
        _chatSessionService = chatSessionService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateResponse([FromBody] OpenAIResponseRequest request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        ChatTurn? turn = null;
        ChatSession? chatSession = null;

        try
        {
            if (request == null || request.Input == null)
                return BadRequest(new { error = new { message = "input is required", type = "invalid_request_error" } });

            if (string.IsNullOrEmpty(request.Model))
                return BadRequest(new { error = new { message = "model is required", type = "invalid_request_error" } });

            // 解析提供方前缀和上游模型 ID（格式：provider:model_id）
            var (provider, upstreamModelId) = ResolveProviderAndModel(request.Model);
            if (provider == null)
                return BadRequest(new { error = new { message = $"No provider found for model '{request.Model}'", type = "invalid_request_error" } });

            // 使用上游模型 ID 替换请求中的模型名，确保上游 API 收到正确的模型名
            request.Model = upstreamModelId;
            var unifiedRequest = ConvertToUnifiedRequest(request);

            _logService.Info("Responses API 请求 - 模型: {0}, 提供者: {1}",
                request.Model, provider.ProviderName);

            // 会话归并
            var conversationKey = ChatSessionResolver.ResolveConversationKey(Request, request);
            var clientKind = ChatSessionResolver.DeriveClientKind(Request);
            var firstUserMsg = ChatSessionResolver.ExtractFirstUserMessage(request);
            chatSession = await _chatSessionService.UpsertSessionAsync(conversationKey, SessionSource.Proxy, request.Model, clientKind, "OpenAI_Responses", firstUserMsg, 1, provider: provider.ProviderName);

            // 创建聊天轮次
            turn = new ChatTurn
            {
                ChatSessionId = chatSession.Id,
                TurnIndex = chatSession.RequestCount,
                SessionKey = chatSession.SessionKey,
                UserPreview = ChatSessionResolver.Truncate(firstUserMsg, 500),
                Style = "OpenAI_Responses",
                Model = request.Model,
                RequestMethod = "POST",
                RequestPath = "/v1/responses",
                RequestHeaders = JsonSerializer.Serialize(Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString())),
                RequestBody = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = false }),
                Temperature = request.Temperature ?? 1.0,
                MaxTokens = request.MaxOutputTokens ?? 0,
                MessageCount = 1,
                ToolCallCount = request.Tools?.Count ?? 0,
                HasReasoning = false,
                CreatedTime = DateTime.Now
            };

            if (request.Stream)
            {
                await HandleStreamAsync(unifiedRequest, provider, request.Model, turn, stopwatch, cancellationToken);
                return new EmptyResult();
            }
            else
            {
                var response = await provider.ChatAsync(unifiedRequest, cancellationToken);
                var responsesResponse = ConvertToResponsesResponse(response, request.Model);

                // 更新轮次
                stopwatch.Stop();
                turn.ResponseStatus = 200;
                turn.ResponseHeaders = JsonSerializer.Serialize(Response.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()));
                turn.ResponseBody = JsonSerializer.Serialize(responsesResponse, new JsonSerializerOptions { WriteIndented = false });
                turn.DurationMs = stopwatch.ElapsedMilliseconds;
                turn.PromptTokens = response.Usage?.PromptTokens ?? 0;
                turn.CompletionTokens = response.Usage?.CompletionTokens ?? 0;
                turn.TotalTokens = response.Usage?.TotalTokens ?? 0;
                turn.AssistantPreview = ChatSessionResolver.Truncate(response.Choices.FirstOrDefault()?.Content, 500);
                await _chatTurnService.SaveTurnAsync(turn);
                await _chatSessionService.RecordTurnStatsAsync(chatSession.Id, 200, turn.PromptTokens, turn.CompletionTokens);

                return Ok(responsesResponse);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // 更新错误轮次
            if (turn != null)
            {
                turn.ResponseStatus = 500;
                turn.ResponseBody = JsonSerializer.Serialize(new { error = ex.Message });
                turn.DurationMs = stopwatch.ElapsedMilliseconds;
                turn.ErrorMessage = ChatSessionResolver.Truncate(ex.Message, 1000);
                await _chatTurnService.SaveTurnAsync(turn);
                await _chatSessionService.RecordTurnStatsAsync(chatSession?.Id ?? 0, 500, 0, 0);
            }

            _logService.Error("Responses API 处理失败: {0}", ex.Message);
            return StatusCode(500, new { error = new { message = ex.Message, type = "internal_error" } });
        }
    }

    private async Task HandleStreamAsync(UnifiedChatRequest request, IAIProvider provider, string modelName, ChatTurn turn, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("X-Accel-Buffering", "no");

        var responseId = $"resp_{Guid.NewGuid():N}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var responseBody = new StringBuilder();
        ChatTurnStreamSession? streamSession = null;

        try
        {
            streamSession = await _streamRecorder.BeginAsync(turn);
            var createdEvent = new
            {
                type = "response.created",
                response = new
                {
                    id = responseId,
                    @object = "response",
                    created_at = created,
                    model = modelName,
                    status = "in_progress",
                    output = new object[0],
                    usage = new { }
                }
            };
            await WriteSseAsync(createdEvent);
            responseBody.Append(JsonSerializer.Serialize(createdEvent));

            var startEvent = new
            {
                type = "response.output_text.delta",
                output_index = 0,
                content_index = 0,
                delta = string.Empty
            };
            await WriteSseAsync(startEvent);
            responseBody.Append(JsonSerializer.Serialize(startEvent));

            await foreach (var chunk in provider.ChatStreamAsync(request, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (!string.IsNullOrEmpty(chunk.DeltaContent))
                {
                    var deltaEvent = new
                    {
                        type = "response.output_text.delta",
                        output_index = 0,
                        content_index = 0,
                        delta = chunk.DeltaContent
                    };
                    await WriteSseAsync(deltaEvent);
                    responseBody.Append(JsonSerializer.Serialize(deltaEvent));
                }

                if (streamSession != null)
                    await streamSession.AppendChunkAsync(chunk.DeltaContent);

                if (turn.FirstTokenMs == 0 && !string.IsNullOrEmpty(chunk.DeltaContent))
                    turn.FirstTokenMs = stopwatch.ElapsedMilliseconds;
            }

            var completedEvent = new
            {
                type = "response.completed",
                response = new
                {
                    id = responseId,
                    @object = "response",
                    created_at = created,
                    model = modelName,
                    status = "completed",
                    output = new object[]
                    {
                        new
                        {
                            type = "message",
                            id = $"msg_{Guid.NewGuid():N}",
                            status = "completed",
                            role = "assistant",
                            content = new object[]
                            {
                                new { type = "output_text", text = string.Empty }
                            }
                        }
                    },
                    usage = new { input_tokens = 0, output_tokens = 0, total_tokens = 0 }
                }
            };
            await WriteSseAsync(completedEvent);
            responseBody.Append(JsonSerializer.Serialize(completedEvent));
            await WriteSseRawAsync("[DONE]");
            responseBody.Append("[DONE]");

            // 更新轮次
            stopwatch.Stop();
            turn.ResponseStatus = 200;
            turn.ResponseHeaders = JsonSerializer.Serialize(Response.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()));
            turn.ResponseBody = responseBody.ToString();
            turn.DurationMs = stopwatch.ElapsedMilliseconds;
            turn.AssistantPreview = ChatSessionResolver.Truncate(turn.ResponseText, 500);
            if (streamSession != null)
            {
                await streamSession.CompleteAsync(responseBody.ToString(), 200, stopwatch.ElapsedMilliseconds);
                await _chatSessionService.RecordTurnStatsAsync(turn.ChatSessionId, 200, turn.PromptTokens, turn.CompletionTokens);
            }
            else
            {
                await _chatTurnService.SaveTurnAsync(turn);
                await _chatSessionService.RecordTurnStatsAsync(turn.ChatSessionId, 200, turn.PromptTokens, turn.CompletionTokens);
            }
        }
        catch (Exception ex)
        {
            _logService.Error("Responses API 流式响应异常: {0}", ex.Message);

            // 更新错误轮次
            stopwatch.Stop();
            turn.ResponseStatus = 500;
            turn.ResponseBody = JsonSerializer.Serialize(new { error = ex.Message });
            turn.DurationMs = stopwatch.ElapsedMilliseconds;
            turn.ErrorMessage = ChatSessionResolver.Truncate(ex.Message, 1000);
            if (streamSession != null)
            {
                await streamSession.FailAsync(500, stopwatch.ElapsedMilliseconds);
                await _chatSessionService.RecordTurnStatsAsync(turn.ChatSessionId, 500, 0, 0);
            }
            else
            {
                await _chatTurnService.SaveTurnAsync(turn);
                await _chatSessionService.RecordTurnStatsAsync(turn.ChatSessionId, 500, 0, 0);
            }
        }
    }

    private async Task WriteSseAsync<T>(T data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        await Response.WriteAsync($"data: {json}\n\n");
        await Response.Body.FlushAsync();
    }

    private async Task WriteSseRawAsync(string data)
    {
        await Response.WriteAsync($"data: {data}\n\n");
        await Response.Body.FlushAsync();
    }

    /// <summary>
    /// 解析模型名中的提供方前缀，返回 (提供方, 上游模型ID)。
    /// 支持格式 "provider:model_id" 和 "model_id"（无前缀时使用默认提供方）。
    /// </summary>
    private (IAIProvider? Provider, string UpstreamModelId) ResolveProviderAndModel(string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
            return (null, modelName);

        var idx = modelName.IndexOf(':');
        if (idx > 0)
        {
            var providerName = modelName[..idx];
            var upstreamModelId = modelName[(idx + 1)..];
            var provider = _registry.GetProviderByName(providerName);
            if (provider != null)
                return (provider, upstreamModelId);
        }

        // 无前缀或提供方不存在，回退到 GetProviderByModel
        var fallbackProvider = _registry.GetProviderByModel(modelName);
        return (fallbackProvider, modelName);
    }

    private UnifiedChatRequest ConvertToUnifiedRequest(OpenAIResponseRequest request)
    {
        var unified = new UnifiedChatRequest
        {
            Model = request.Model,
            Temperature = request.Temperature ?? 1.0,
            TopP = request.TopP ?? 1.0,
            MaxTokens = request.MaxOutputTokens,
            Stream = request.Stream,
            Messages = new List<UnifiedChatMessage>(),
            SystemPrompt = request.Instructions
        };

        if (request.Input is JsonElement inputElement)
        {
            if (inputElement.ValueKind == JsonValueKind.String)
            {
                unified.Messages.Add(new UnifiedChatMessage
                {
                    Role = "user",
                    Content = inputElement.GetString() ?? string.Empty
                });
            }
            else if (inputElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in inputElement.EnumerateArray())
                {
                    var role = item.GetProperty("role").GetString() ?? "user";
                    var contentProp = item.GetProperty("content");

                    string contentText = string.Empty;
                    if (contentProp.ValueKind == JsonValueKind.String)
                    {
                        contentText = contentProp.GetString() ?? string.Empty;
                    }
                    else if (contentProp.ValueKind == JsonValueKind.Array)
                    {
                        var texts = new List<string>();
                        foreach (var block in contentProp.EnumerateArray())
                        {
                            var type = block.GetProperty("type").GetString();
                            if (type == "input_text")
                            {
                                texts.Add(block.GetProperty("text").GetString() ?? string.Empty);
                            }
                        }
                        contentText = string.Join("\n", texts);
                    }

                    unified.Messages.Add(new UnifiedChatMessage
                    {
                        Role = role,
                        Content = contentText
                    });
                }
            }
        }

        if (request.Tools != null && request.Tools.Count > 0)
        {
            unified.Tools = request.Tools
                .Where(t => t.Type == "function" && t.Function != null)
                .Select(t => new UnifiedToolDefinition
                {
                    Name = t.Function!.Name,
                    Description = t.Function.Description ?? string.Empty,
                    Parameters = t.Function.Parameters ?? new object()
                }).ToList();
        }

        return unified;
    }

    private object ConvertToResponsesResponse(UnifiedChatResponse response, string requestModel)
    {
        var firstChoice = response.Choices.FirstOrDefault();
        var messageId = $"msg_{Guid.NewGuid():N}";

        var outputItems = new List<object>();

        if (firstChoice != null && !string.IsNullOrEmpty(firstChoice.Content))
        {
            outputItems.Add(new
            {
                type = "message",
                id = messageId,
                status = "completed",
                role = "assistant",
                content = new[]
                {
                    new { type = "output_text", text = firstChoice.Content }
                }
            });
        }

        if (firstChoice?.ToolCalls != null && firstChoice.ToolCalls.Count > 0)
        {
            foreach (var tc in firstChoice.ToolCalls)
            {
                object argsObj;
                try
                {
                    argsObj = JsonDocument.Parse(tc.Arguments).RootElement;
                }
                catch
                {
                    argsObj = new { };
                }

                outputItems.Add(new
                {
                    type = "function_call",
                    id = tc.Id,
                    call_id = tc.Id,
                    name = tc.Name,
                    arguments = argsObj
                });
            }
        }

        var status = response.FinishReason switch
        {
            "stop" or "end_turn" => "completed",
            "tool_calls" or "tool_use" => "completed",
            _ => "completed"
        };

        return new
        {
            id = response.Id,
            @object = "response",
            created_at = response.Created,
            model = string.IsNullOrEmpty(response.Model) ? requestModel : response.Model,
            status = status,
            output = outputItems,
            usage = new
            {
                input_tokens = response.Usage?.PromptTokens ?? 0,
                output_tokens = response.Usage?.CompletionTokens ?? 0,
                total_tokens = response.Usage?.TotalTokens ?? 0
            }
        };
    }
}

#region Responses API DTOs

public class OpenAIResponseRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("input")]
    public object? Input { get; set; }

    [JsonPropertyName("instructions")]
    public string? Instructions { get; set; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }

    [JsonPropertyName("max_output_tokens")]
    public int? MaxOutputTokens { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("tools")]
    public List<OpenAIResponseTool>? Tools { get; set; }

    [JsonPropertyName("previous_response_id")]
    public string? PreviousResponseId { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}

public class OpenAIResponseTool
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public OpenAIResponseFunction? Function { get; set; }
}

public class OpenAIResponseFunction
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("parameters")]
    public object? Parameters { get; set; }
}

#endregion
