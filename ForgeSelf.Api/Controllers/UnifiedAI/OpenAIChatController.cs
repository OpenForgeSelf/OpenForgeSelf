using System.Text.Json;
using System.Text.Json.Serialization;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Services.AI.Proxy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Controllers.UnifiedAI;

[ApiController]
[Route("v1")]
[Authorize("ApiKeyPolicy")]
public class OpenAIChatController : ControllerBase
{
    private readonly ILlmProxyPipeline _pipeline;
    private readonly ILogService _logService;
    private readonly ILlmTurnAudit _audit;

    public OpenAIChatController(
        ILlmProxyPipeline pipeline,
        ILogService logService,
        ILlmTurnAudit audit)
    {
        _pipeline = pipeline;
        _logService = logService;
        _audit = audit;
    }

    [HttpPost("chat/completions")]
    public Task<IActionResult> ChatCompletions(CancellationToken cancellationToken)
        => LlmProxyHttp.ForwardAsync(
            this, _pipeline, LlmWireProtocol.ChatCompletions, "OpenAI_Chat", "/v1/chat/completions",
            _logService, _audit, cancellationToken);
}

#region OpenAI Request DTOs

public class OpenAIChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<OpenAIChatMessage>? Messages { get; set; }

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 1.0;

    [JsonPropertyName("top_p")]
    public double TopP { get; set; } = 1.0;

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("stream_options")]
    public OpenAIStreamOptions? StreamOptions { get; set; }

    [JsonPropertyName("tools")]
    public List<OpenAITool>? Tools { get; set; }

    [JsonPropertyName("tool_choice")]
    public string? ToolChoice { get; set; }

    [JsonPropertyName("n")]
    public int N { get; set; } = 1;

    [JsonPropertyName("stop")]
    public string? Stop { get; set; }

    [JsonPropertyName("presence_penalty")]
    public double PresencePenalty { get; set; } = 0;

    [JsonPropertyName("frequency_penalty")]
    public double FrequencyPenalty { get; set; } = 0;
}

public class OpenAIStreamOptions
{
    [JsonPropertyName("include_usage")]
    public bool IncludeUsage { get; set; }
}

public class OpenAIChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// OpenAI 多模态消息 content 可以是 string（纯文本）或 array（多模态内容）
    /// 数组格式: [{type: "text", text: "..."}, {type: "image_url", image_url: {...}}]
    /// </summary>
    [JsonPropertyName("content")]
    [JsonConverter(typeof(ChatContentJsonConverter))]
    public object? Content { get; set; }

    /// <summary>
    /// 获取纯文本内容（如果有）
    /// </summary>
    public string? GetTextContent()
    {
        if (Content == null) return null;
        if (Content is string s) return s;
        // 如果是数组，尝试提取文本部分
        if (Content is IEnumerable<object> list)
        {
            var textParts = list
                .Where(c => c is JsonElement je && je.TryGetProperty("type", out var t) && t.GetString() == "text")
                .Select(c => (c as JsonElement?)?.GetProperty("text").GetString())
                .Where(s => !string.IsNullOrEmpty(s));
            return string.Join("\n", textParts);
        }
        return Content.ToString();
    }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("tool_calls")]
    public List<OpenAIToolCall>? ToolCalls { get; set; }

    [JsonPropertyName("tool_call_id")]
    public string? ToolCallId { get; set; }
}

/// <summary>
/// 处理 OpenAI 多模态消息的 Content 字段
/// </summary>
public class ChatContentJsonConverter : JsonConverter<object>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return reader.GetString();

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = new List<object>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                var item = JsonSerializer.Deserialize<object>(ref reader, options);
                list.Add(item!);
            }
            return list;
        }

        return JsonSerializer.Deserialize<object>(ref reader, options);
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
        }
        else if (value is string s)
        {
            writer.WriteStringValue(s);
        }
        else if (value is IEnumerable<object> list)
        {
            writer.WriteStartArray();
            foreach (var item in list)
                JsonSerializer.Serialize(writer, item, options);
            writer.WriteEndArray();
        }
        else
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}

public class OpenAITool
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public OpenAIFunctionDef? Function { get; set; }
}

public class OpenAIFunctionDef
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("parameters")]
    public object? Parameters { get; set; }
}

public class OpenAIToolCall
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public OpenAIFunctionCall? Function { get; set; }
}

public class OpenAIFunctionCall
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public string Arguments { get; set; } = string.Empty;
}

public class OpenAIStreamChunk
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = "chat.completion.chunk";

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<OpenAIStreamChoice>? Choices { get; set; }

    /// <summary>
    /// 流式用量（已用 token）。上游在 include_usage=true 时于末尾发送 usage 哨兵分片，网关需透传，否则前端拿不到 token 统计。
    /// </summary>
    [JsonPropertyName("usage")]
    public OpenAIUsage? Usage { get; set; }
}

/// <summary>
/// 流式 / 非流式共用的 token 用量结构（prompt / completion / total）。
/// </summary>
public class OpenAIUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

public class OpenAIStreamChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("delta")]
    public OpenAIStreamDelta? Delta { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public class OpenAIStreamDelta
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("tool_calls")]
    public List<OpenAIToolCall>? ToolCalls { get; set; }
}

#endregion
