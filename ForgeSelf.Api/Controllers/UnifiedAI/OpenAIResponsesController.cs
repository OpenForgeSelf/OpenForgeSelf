using System.Text.Json.Serialization;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Services.AI.Proxy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Controllers.UnifiedAI;

[ApiController]
[Route("v1/responses")]
[Authorize("ApiKeyPolicy")]
public class OpenAIResponsesController : ControllerBase
{
    private readonly ILlmProxyPipeline _pipeline;
    private readonly ILogService _logService;
    private readonly ILlmTurnAudit _audit;

    public OpenAIResponsesController(
        ILlmProxyPipeline pipeline,
        ILogService logService,
        ILlmTurnAudit audit)
    {
        _pipeline = pipeline;
        _logService = logService;
        _audit = audit;
    }

    [HttpPost]
    public Task<IActionResult> CreateResponse(CancellationToken cancellationToken)
        => LlmProxyHttp.ForwardAsync(
            this, _pipeline, LlmWireProtocol.Responses, "OpenAI_Responses", "/v1/responses",
            _logService, _audit, cancellationToken);
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
