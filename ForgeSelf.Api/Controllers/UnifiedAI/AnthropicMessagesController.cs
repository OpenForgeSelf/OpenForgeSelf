using System.Text.Json;
using System.Text.Json.Serialization;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Services.AI.Proxy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Controllers.UnifiedAI;

[ApiController]
[Route("v1/anthropic")]
[Authorize("ApiKeyPolicy")]
public class AnthropicMessagesController : ControllerBase
{
    private readonly ILlmProxyPipeline _pipeline;
    private readonly ILogService _logService;
    private readonly ILlmTurnAudit _audit;

    public AnthropicMessagesController(
        ILlmProxyPipeline pipeline,
        ILogService logService,
        ILlmTurnAudit audit)
    {
        _pipeline = pipeline;
        _logService = logService;
        _audit = audit;
    }

    [HttpPost("messages")]
    public Task<IActionResult> CreateMessage(CancellationToken cancellationToken)
        => LlmProxyHttp.ForwardAsync(
            this, _pipeline, LlmWireProtocol.AnthropicMessages, "Anthropic_Messages", "/v1/anthropic/messages",
            _logService, _audit, cancellationToken);
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
