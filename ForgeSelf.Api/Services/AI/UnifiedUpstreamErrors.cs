using System.ClientModel;
using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ForgeSelf.Api.Services.AI;

/// <summary>
/// 统一 AI 网关：上游 HTTP/SDK 错误解析与流式 SSE 错误回写（OpenAI Chat / Responses / Anthropic）。
/// 凡 <see cref="ClientResultException"/> / <see cref="HttpRequestException"/>（含 InnerException 链）一律按上游 HTTP 状态透传，不压成 500。
/// </summary>
public static class UnifiedUpstreamErrors
{
    public readonly record struct Resolved(
        int StatusCode,
        string Message,
        string ErrorType,
        bool IsUpstream,
        string? UpstreamErrorCode = null,
        JsonElement? UpstreamErrorRoot = null);

    public static Resolved Resolve(Exception ex)
    {
        var root = UnwrapUpstream(ex);
        if (root == null)
            return new Resolved(500, ex.Message, "internal_error", false);

        if (root is ClientResultException cre)
            return ResolveClientResult(cre);

        var httpEx = (HttpRequestException)root;
        var httpStatus = (int)(httpEx.StatusCode ?? HttpStatusCode.BadGateway);
        return new Resolved(httpStatus, httpEx.Message, ErrorTypeForStatus(httpStatus), true);
    }

    /// <summary>非流式 JSON：优先回传上游 <c>error</c> 对象（若 SDK 已解析），否则网关构造 OpenAI 形。</summary>
    public static object ToErrorBody(Resolved err)
    {
        if (err.UpstreamErrorRoot is { } root && root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("error", out var errorEl) && errorEl.ValueKind == JsonValueKind.Object)
                return JsonSerializer.Deserialize<object>(root.GetRawText()) ?? FallbackErrorBody(err);
            if (root.TryGetProperty("type", out _) && root.TryGetProperty("message", out _))
                return new { error = JsonSerializer.Deserialize<object>(root.GetRawText()) };
        }

        return FallbackErrorBody(err);
    }

    private static object FallbackErrorBody(Resolved err)
        => new
        {
            error = new
            {
                message = err.Message,
                type = err.ErrorType,
                code = err.UpstreamErrorCode ?? err.StatusCode.ToString()
            }
        };

    private static Resolved ResolveClientResult(ClientResultException cre)
    {
        var status = cre.Status > 0 ? cre.Status : (int)HttpStatusCode.BadGateway;
        var message = cre.Message;
        var errorType = ErrorTypeForStatus(status);
        string? upstreamCode = null;
        JsonElement? upstreamRoot = null;

        if (TryParseJsonObject(message, out var parsedFromMessage))
        {
            upstreamRoot = parsedFromMessage;
            if (parsedFromMessage.TryGetProperty("error", out var errObj) && errObj.ValueKind == JsonValueKind.Object)
            {
                if (errObj.TryGetProperty("message", out var msgEl) && msgEl.ValueKind == JsonValueKind.String)
                    message = msgEl.GetString() ?? message;
                if (errObj.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
                    errorType = typeEl.GetString() ?? errorType;
                if (errObj.TryGetProperty("code", out var codeEl) && codeEl.ValueKind == JsonValueKind.String)
                    upstreamCode = codeEl.GetString();
            }
        }

        return new Resolved(status, message, errorType, true, upstreamCode, upstreamRoot);
    }

    private static Exception? UnwrapUpstream(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is ClientResultException or HttpRequestException)
                return current;
        }
        return null;
    }

    private static bool TryParseJsonObject(string text, out JsonElement root)
    {
        root = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var start = text.IndexOf('{');
        if (start < 0)
            return false;
        try
        {
            using var doc = JsonDocument.Parse(text[start..]);
            root = doc.RootElement.Clone();
            return root.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static void TrySetStatusCode(HttpResponse response, int statusCode)
    {
        if (!response.HasStarted)
            response.StatusCode = statusCode;
    }

    /// <summary>OpenAI Chat Completions 流式：<c>data: {"error":…}</c> + <c>[DONE]</c>。</summary>
    public static async Task WriteOpenAiChatStreamErrorAsync(
        HttpResponse response,
        JsonSerializerOptions jsonOptions,
        Resolved err,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(ToErrorBody(err), jsonOptions);
        await response.WriteAsync($"data: {json}\n\n", cancellationToken);
        await response.WriteAsync("data: [DONE]\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
        TrySetStatusCode(response, err.StatusCode);
    }

    /// <summary>OpenAI Responses 流式：同 Chat 的 error 行 + [DONE]（客户端可读上游语义）。</summary>
    public static Task WriteOpenAiResponsesStreamErrorAsync(
        HttpResponse response,
        JsonSerializerOptions jsonOptions,
        Resolved err,
        CancellationToken cancellationToken = default)
        => WriteOpenAiChatStreamErrorAsync(response, jsonOptions, err, cancellationToken);

    /// <summary>Anthropic Messages 流式：<c>event: error</c> + snake_case error 体。</summary>
    public static async Task WriteAnthropicStreamErrorAsync(
        HttpResponse response,
        Resolved err,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            type = "error",
            error = new
            {
                type = err.ErrorType,
                message = err.Message
            }
        };
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            // PC-027：中文保持原文，禁止默认 \uXXXX 转义
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        var json = JsonSerializer.Serialize(payload, jsonOptions);
        await response.WriteAsync("event: error\n", cancellationToken);
        await response.WriteAsync($"data: {json}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
        TrySetStatusCode(response, err.StatusCode);
    }

    private static string ErrorTypeForStatus(int status) => status switch
    {
        429 => "rate_limit_error",
        401 or 403 => "authentication_error",
        >= 400 and < 500 => "invalid_request_error",
        >= 500 => "upstream_error",
        _ => "upstream_error"
    };
}
