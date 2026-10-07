using System.Text.Json;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.McpCenter.Services;

/// <summary>
/// MCP JSON-RPC 2.0 分发器（纯逻辑，可脱离 HTTP 单测）。
/// 支持方法：initialize / notifications/initialized / ping / tools/list / tools/call；
/// 支持单条与批处理；通知（无 id）不返回响应体。
/// 协议版本：2025-11-25（MCP 2.0）/ 2025-06-18（Streamable HTTP），兼容 2025-03-26 / 2024-11-05 客户端声明；
/// 客户端声明未知版本时回退默认 2025-06-18（向后兼容 1.x）。
/// </summary>
/// <remarks>
/// ⚠️ **自 2.4.0 起本类不再参与 MCP 网关链路**：传输层与协议协商已交由官方 MCP C# SDK
/// （ModelContextProtocol.AspNetCore 2.2.0）托管，见 <see cref="McpGatewayServer"/> 的 AddMcpServer + MapMcp；
/// 对外工具改由 <see cref="McpUniversalTool"/> 以 SDK 方式暴露。
/// **本类保留仅为回退路径与既有单测（McpJsonRpcHandlerTests）**，逻辑未改动、不对外提供服务；
/// 待官方 SDK 链路稳定运行后随其测试一并下线（已登记 TODO）。
/// 注意：本类的「未知版本静默回退默认」行为与官方 SDK 的「显式报错 -32022 + supported 列表」不一致，
/// 回退到本类即恢复旧行为，属已知取舍。
/// </remarks>
public sealed class McpJsonRpcHandler
{
    public const string ProtocolVersion = "2025-06-18";
    private const string DefaultProtocolVersion = "2025-06-18";

    private static readonly string[] SupportedProtocolVersions = { "2025-11-25", "2025-06-18", "2025-03-26", "2024-11-05" };

    private const int ParseError = -32700;
    private const int InvalidRequest = -32600;
    private const int MethodNotFound = -32601;
    private const int InvalidParams = -32602;
    private const int InternalError = -32603;

    private readonly UniversalToolForwarder _forwarder;
    private readonly string _serverName;
    private readonly string _serverVersion;

    public McpJsonRpcHandler(UniversalToolForwarder forwarder, string serverName, string serverVersion)
    {
        _forwarder = forwarder;
        _serverName = serverName;
        _serverVersion = serverVersion;
    }

    /// <summary>
    /// 处理一个请求体（单条或批处理）。返回响应 JSON；通知或空批返回 null（调用方回 202 空体）。
    /// </summary>
    public async Task<string?> HandleRequestAsync(string requestJson, CancellationToken cancellationToken = default)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(requestJson);
        }
        catch (JsonException ex)
        {
            XTrace.Log.Warn("[McpCenter] JSON-RPC 解析错误: {0}", ex.Message);
            return Error(default, ParseError, "Parse error: 请求体不是合法 JSON");
        }

        using (doc)
        {
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                return await HandleBatchAsync(root, cancellationToken);
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                return Error(default, InvalidRequest, "Invalid Request: 顶层必须是 JSON 对象或数组");
            }

            return await HandleSingleAsync(root, cancellationToken);
        }
    }

    private async Task<string?> HandleBatchAsync(JsonElement batch, CancellationToken cancellationToken)
    {
        if (batch.GetArrayLength() == 0)
        {
            return Error(default, InvalidRequest, "Invalid Request: 批处理数组不能为空");
        }

        var responses = new List<string>();
        foreach (var item in batch.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                responses.Add(Error(default, InvalidRequest, "Invalid Request: 批内元素必须是 JSON 对象"));
                continue;
            }

            var response = await HandleSingleAsync(item, cancellationToken);
            if (response != null)
            {
                responses.Add(response);
            }
        }

        return responses.Count == 0 ? null : $"[{string.Join(",", responses)}]";
    }

    private async Task<string?> HandleSingleAsync(JsonElement request, CancellationToken cancellationToken)
    {
        JsonElement id = default;
        var hasId = request.TryGetProperty("id", out id);

        if (!request.TryGetProperty("jsonrpc", out var jsonrpc) || jsonrpc.GetString() != "2.0")
        {
            return Error(id, InvalidRequest, "Invalid Request: jsonrpc 必须为 \"2.0\"");
        }

        if (!request.TryGetProperty("method", out var methodProp) || string.IsNullOrWhiteSpace(methodProp.GetString()))
        {
            return Error(id, InvalidRequest, "Invalid Request: 缺少 method");
        }

        var method = methodProp.GetString()!;
        var hasParams = request.TryGetProperty("params", out var requestParams);
        var requestParamsValue = hasParams ? requestParams : default;

        // 通知（无 id）：不返回响应体
        if (!hasId)
        {
            await HandleNotificationAsync(method, hasParams ? requestParamsValue : default, cancellationToken);
            return null;
        }

        try
        {
            switch (method)
            {
                case "initialize":
                    return await HandleInitializeAsync(id, hasParams ? requestParamsValue : default, cancellationToken);
                case "ping":
                    return Response(id, """{}""");
                case "tools/list":
                    return await HandleToolsListAsync(id, cancellationToken);
                case "tools/call":
                    return await HandleToolsCallAsync(id, hasParams ? requestParamsValue : default, cancellationToken);
                default:
                    return Error(id, MethodNotFound, $"Method not found: {method}");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[McpCenter] JSON-RPC 方法执行异常 [{0}]: {1}", method, ex.Message);
            return Error(id, InternalError, $"Internal error: {ex.Message}");
        }
    }

    private Task HandleNotificationAsync(string method, JsonElement requestParams, CancellationToken cancellationToken)
    {
        switch (method)
        {
            case "notifications/initialized":
            case "notifications/cancelled":
                XTrace.Log.Debug("[McpCenter] 收到通知: {0}", method);
                break;
            default:
                XTrace.Log.Debug("[McpCenter] 收到未知通知（忽略）: {0}", method);
                break;
        }

        return Task.CompletedTask;
    }

    private Task<string> HandleInitializeAsync(JsonElement id, JsonElement requestParams, CancellationToken cancellationToken)
    {
        var negotiated = DefaultProtocolVersion;
        if (requestParams.ValueKind == JsonValueKind.Object &&
            requestParams.TryGetProperty("protocolVersion", out var clientVersion) &&
            clientVersion.ValueKind == JsonValueKind.String)
        {
            var requested = clientVersion.GetString();
            if (SupportedProtocolVersions.Contains(requested, StringComparer.Ordinal))
            {
                negotiated = requested!;
            }
        }

        XTrace.Log.Info("[McpCenter] initialize 完成，协商协议版本 {0}（客户端声明 {1}）",
            negotiated,
            requestParams.ValueKind == JsonValueKind.Object && requestParams.TryGetProperty("protocolVersion", out var cv) ? cv.GetString() : "(无)");

        var result = JsonSerializer.Serialize(new
        {
            protocolVersion = negotiated,
            capabilities = new { tools = new { listChanged = false } },
            serverInfo = new { name = _serverName, version = _serverVersion, description = "OpenForgeSelf MCP Center（万能工具网关，支持 MCP 2.0）" }
        });

        return Task.FromResult(Response(id, result));
    }

    private Task<string> HandleToolsListAsync(JsonElement id, CancellationToken cancellationToken)
    {
        using var defDoc = JsonDocument.Parse(UniversalToolForwarder.ToolDefinitionJson);
        var tool = defDoc.RootElement.Clone();
        var result = JsonSerializer.Serialize(new { tools = new[] { tool } });
        XTrace.Log.Info("[McpCenter] tools/list 返回 1 个工具（万能工具网关）");
        return Task.FromResult(Response(id, result));
    }

    private async Task<string> HandleToolsCallAsync(JsonElement id, JsonElement requestParams, CancellationToken cancellationToken)
    {
        // 参数结构：{ name: 工具名, arguments?: 对象 }
        string toolName;
        var argumentsJson = "{}";

        if (requestParams.ValueKind != JsonValueKind.Object)
        {
            return Error(id, InvalidParams, "Invalid params: tools/call 的 params 必须是对象 {name, arguments}");
        }

        if (!requestParams.TryGetProperty("name", out var nameProp) || string.IsNullOrWhiteSpace(nameProp.GetString()))
        {
            return Error(id, InvalidParams, "Invalid params: 缺少 name（唯一可用工具名 universal_tool）");
        }
        toolName = nameProp.GetString()!;

        // 对外仅接受万能工具（用户指定：整体对外只有一个工具）
        if (!toolName.Equals(UniversalToolForwarder.ToolName, StringComparison.Ordinal))
        {
            return Error(id, InvalidParams,
                $"Invalid params: 未知工具 '{toolName}'，本网关对外仅暴露 1 个工具 {UniversalToolForwarder.ToolName}（内部按名转发请经它传 tool 参数）");
        }

        if (requestParams.TryGetProperty("arguments", out var argsProp))
        {
            argumentsJson = argsProp.ValueKind switch
            {
                JsonValueKind.Object => argsProp.GetRawText(),
                JsonValueKind.String => argsProp.GetString() ?? "{}",
                _ => "{}"
            };
        }

        var forwarded = await _forwarder.ForwardAsync(argumentsJson);

        var result = JsonSerializer.Serialize(new
        {
            content = new[] { new { type = "text", text = forwarded.Text } },
            isError = forwarded.IsError
        });

        XTrace.Log.Info("[McpCenter] tools/call 完成，isError={0}，结果长度={1}", forwarded.IsError, forwarded.Text.Length);
        return Response(id, result);
    }

    private static string Response(JsonElement id, string resultJson)
    {
        using var resultDoc = JsonDocument.Parse(resultJson);
        var result = resultDoc.RootElement.Clone();
        return JsonSerializer.Serialize(new { jsonrpc = "2.0", id, result });
    }

    private static string Error(JsonElement id, int code, string message)
    {
        var error = new { code, message };
        if (id.ValueKind == JsonValueKind.Undefined)
        {
            return JsonSerializer.Serialize(new { jsonrpc = "2.0", id = default(object), error });
        }
        return JsonSerializer.Serialize(new { jsonrpc = "2.0", id, error });
    }
}
