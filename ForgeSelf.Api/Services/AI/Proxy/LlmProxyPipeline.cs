using System.IO.Pipelines;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Services.AI.Providers;

namespace ForgeSelf.Api.Services.AI.Proxy;

/// <summary>
/// 唯一的模型往返算法：读报文 → 解析别名并改 <c>model</c> → 按提供方选线上协议 → 同协议原样 POST，或标准桥接后再 POST → 响应按入站协议送回。
/// 调用方不得自己改别名、选协议或另起客户端。
/// </summary>
public sealed class LlmProxyPipeline : ILlmProxyPipeline
{
    private readonly AIProviderRegistry _registry;

    public LlmProxyPipeline(AIProviderRegistry registry)
    {
        _registry = registry;
    }

    public async Task<LlmProxyResult> ExecuteAsync(LlmProxyRequest request, CancellationToken cancellationToken = default)
    {
        if (JsonNode.Parse(string.IsNullOrWhiteSpace(request.BodyJson) ? "null" : request.BodyJson) is not JsonObject body)
            return Error(request.Protocol, 400, "请求体必须是 JSON 对象");

        var model = LlmJson.Str(body["model"]);
        if (string.IsNullOrWhiteSpace(model))
            return Error(request.Protocol, 400, "model is required");

        var (provider, upstreamModelId) = _registry.ResolveUpstreamModel(model);
        if (provider == null)
            return Error(request.Protocol, 400, $"No provider found for model '{model}'");

        body["model"] = upstreamModelId;
        var outboundProtocol = WireOf(provider);
        var wantStream = LlmJson.Bool(body["stream"]);
        JsonObject outboundNode;
        try
        {
            outboundNode = request.Protocol == outboundProtocol
                ? body
                : ToOutbound(body, request.Protocol, outboundProtocol);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Error(request.Protocol, 400, ex.Message);
        }

        // 类级守卫（PC: tool_choice-without-tools）：同协议透传与桥接产物在此汇合——
        // 任何出站报文，tools 缺失或为空时必须丢弃 tool_choice，否则上游 400。
        if (outboundNode["tool_choice"] is not null && outboundNode["tools"] is not JsonArray { Count: > 0 })
            outboundNode.Remove("tool_choice");

        var outboundJson = LlmJson.Write(outboundNode);
        HttpResponseMessage response;
        try
        {
            response = await OpenAIRawForwarder.PostAsync(provider, outboundProtocol, outboundJson, wantStream, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var status = ex is InvalidOperationException ? 400 : 502;
            return Error(request.Protocol, status, ex.Message);
        }

        var statusCode = (int)response.StatusCode;
        var media = response.Content.Headers.ContentType?.MediaType;
        if (string.IsNullOrEmpty(media))
            media = wantStream ? "text/event-stream" : "application/json";
        var sse = media.Contains("event-stream", StringComparison.OrdinalIgnoreCase);
        var streamToClient = sse || (wantStream && statusCode is >= 200 and < 300);

        if (request.Protocol == outboundProtocol || statusCode >= 400)
        {
            var raw = await response.Content.ReadAsStreamAsync(cancellationToken);
            return new LlmProxyResult
            {
                StatusCode = statusCode,
                Stream = streamToClient && statusCode < 400 || sse,
                MediaType = media,
                OutboundJson = outboundJson,
                Body = new UpstreamBody(response, raw)
            };
        }

        if (!wantStream)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            response.Dispose();
            JsonObject translated;
            try
            {
                if (JsonNode.Parse(text) is not JsonObject obj)
                {
                    return Bytes(statusCode, media, outboundJson, text);
                }
                translated = ToInboundResponse(obj, outboundProtocol, request.Protocol);
            }
            catch
            {
                return Bytes(statusCode, media, outboundJson, text);
            }

            return Bytes(statusCode, "application/json", outboundJson, LlmJson.Write(translated));
        }

        var pipe = new Pipe();
        var upstream = await response.Content.ReadAsStreamAsync(cancellationToken);
        _ = TranslateAsync(upstream, response, pipe.Writer, outboundProtocol, request.Protocol, cancellationToken);
        return new LlmProxyResult
        {
            StatusCode = statusCode,
            Stream = true,
            MediaType = "text/event-stream",
            OutboundJson = outboundJson,
            Body = pipe.Reader.AsStream()
        };
    }

    private static async Task TranslateAsync(
        Stream upstream,
        HttpResponseMessage response,
        PipeWriter writer,
        LlmWireProtocol outbound,
        LlmWireProtocol inbound,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var target = writer.AsStream(leaveOpen: true);
            await WireStreamTranslator.CopyAsync(upstream, target, outbound, inbound, cancellationToken);
            await writer.CompleteAsync();
        }
        catch (Exception ex)
        {
            await writer.CompleteAsync(ex);
        }
        finally
        {
            await upstream.DisposeAsync();
            response.Dispose();
        }
    }

    internal static LlmWireProtocol WireOf(IAIProvider provider)
        => provider.ProviderType == AIProviderType.OpenAIResponses || provider is OpenAIResponsesProvider
            ? LlmWireProtocol.Responses
            : LlmWireProtocol.ChatCompletions;

    private static JsonObject ToOutbound(JsonObject inbound, LlmWireProtocol from, LlmWireProtocol to)
    {
        var chat = from switch
        {
            LlmWireProtocol.AnthropicMessages => AnthropicProtocolBridge.ToChat(inbound),
            LlmWireProtocol.Responses => ChatResponsesBridge.ToChatRequest(inbound),
            _ => inbound
        };
        return to switch
        {
            LlmWireProtocol.Responses => ChatResponsesBridge.ToResponsesRequest(chat),
            LlmWireProtocol.AnthropicMessages => chat,
            _ => chat
        };
    }

    private static JsonObject ToInboundResponse(JsonObject upstream, LlmWireProtocol outbound, LlmWireProtocol inbound)
    {
        var chat = outbound == LlmWireProtocol.Responses
            ? ChatResponsesBridge.ToChatResponse(upstream)
            : upstream;
        return inbound switch
        {
            LlmWireProtocol.Responses => ChatResponsesBridge.ToResponsesResponse(chat),
            LlmWireProtocol.AnthropicMessages => AnthropicProtocolBridge.FromChatResponse(chat),
            _ => chat
        };
    }

    private static LlmProxyResult Bytes(int status, string media, string outboundJson, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return new LlmProxyResult
        {
            StatusCode = status,
            Stream = false,
            MediaType = media,
            OutboundJson = outboundJson,
            Body = new MemoryStream(bytes)
        };
    }

    private static LlmProxyResult Error(LlmWireProtocol protocol, int status, string message)
    {
        JsonObject body = protocol == LlmWireProtocol.AnthropicMessages
            ? new JsonObject
            {
                ["type"] = "error",
                ["error"] = new JsonObject { ["type"] = "invalid_request_error", ["message"] = message }
            }
            : new JsonObject
            {
                ["error"] = new JsonObject { ["message"] = message, ["type"] = "invalid_request_error" }
            };
        return Bytes(status, "application/json", string.Empty, LlmJson.Write(body));
    }
}
