using System.Diagnostics;
using System.Text;
using ForgeSelf.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Services.AI.Proxy;

/// <summary>
/// 三个公开入站入口共用的 HTTP 外壳：读原始 body，调用管道，把状态码和字节原样写回。
/// 旁路记录只入队，不参与本次响应的完成。
/// </summary>
public static class LlmProxyHttp
{
    public static async Task<IActionResult> ForwardAsync(
        ControllerBase controller,
        ILlmProxyPipeline pipeline,
        LlmWireProtocol protocol,
        string style,
        string path,
        ILogService log,
        ILlmTurnAudit audit,
        CancellationToken cancellationToken)
    {
        var http = controller.HttpContext;
        string raw;
        using (var reader = new StreamReader(http.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
            raw = await reader.ReadToEndAsync(cancellationToken);

        var watch = Stopwatch.StartNew();
        var inbound = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in http.Request.Headers)
            inbound[header.Key] = header.Value.ToString();

        await using var result = await pipeline.ExecuteAsync(
            new LlmProxyRequest { Protocol = protocol, BodyJson = raw, Headers = inbound }, cancellationToken);

        http.Response.StatusCode = result.StatusCode;
        http.Response.ContentType = result.Stream ? "text/event-stream" : result.MediaType;
        if (result.Stream)
        {
            http.Response.Headers.CacheControl = "no-cache";
            http.Response.Headers.Connection = "keep-alive";
            http.Response.Headers["X-Accel-Buffering"] = "no";
        }

        var captured = new StringBuilder();
        if (result.Body != null)
        {
            var buffer = new byte[8192];
            while (true)
            {
                var read = await result.Body.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                    break;
                await http.Response.Body.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                if (captured.Length < 8000)
                {
                    var take = Math.Min(read, 8000 - captured.Length);
                    captured.Append(Encoding.UTF8.GetString(buffer, 0, take));
                }
            }
            await http.Response.Body.FlushAsync(cancellationToken);
        }

        watch.Stop();
        EnqueueAudit(http, log, audit, style, path, result, captured.ToString(), watch.ElapsedMilliseconds);
        return new EmptyResult();
    }

    private static void EnqueueAudit(
        Microsoft.AspNetCore.Http.HttpContext http,
        ILogService log,
        ILlmTurnAudit audit,
        string style,
        string path,
        LlmProxyResult result,
        string responseText,
        long durationMs)
    {
        try
        {
            var headers = new Dictionary<string, string>(http.Request.Headers.Count);
            foreach (var header in http.Request.Headers)
                headers[header.Key] = header.Value.ToString();

            audit.Enqueue(new LlmTurnAuditWork
            {
                Style = style,
                Path = path,
                StatusCode = result.StatusCode,
                Stream = result.Stream,
                OutboundJson = result.OutboundJson,
                ResponseText = responseText,
                DurationMs = durationMs,
                Headers = headers
            });
        }
        catch (Exception ex)
        {
            log.Error("LLM 代理旁路记录入队失败（不影响响应）: {0}", ex.Message);
        }
    }
}
