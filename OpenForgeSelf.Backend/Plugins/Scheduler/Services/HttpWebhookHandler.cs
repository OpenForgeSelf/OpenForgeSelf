using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenForgeSelf.Backend.Plugins.Scheduler.Models;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.Scheduler.Services;

public class HttpWebhookHandler
{
    private readonly HttpClient _httpClient;

    public HttpWebhookHandler()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
    }

    public async Task<string> ExecuteAsync(ScheduledTask task)
    {
        try
        {
            XTrace.Log.Info("[HttpWebhookHandler] 执行HTTP Webhook任务: {0} (URL: {1})", task.Name, task.TargetId);

            var url = task.TargetId;
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("Webhook URL不能为空");
            }

            var config = ParseWebhookConfig(task.InputParameters);

            var method = config.Method?.ToUpperInvariant() ?? "GET";
            var headers = config.Headers ?? new Dictionary<string, string>();
            var body = config.Body;

            using var request = new HttpRequestMessage(new HttpMethod(method), url);

            foreach (var header in headers)
            {
                if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
                {
                    XTrace.Log.Debug("[HttpWebhookHandler] 跳过无效的Header: {0}", header.Key);
                }
            }

            if (method is "POST" or "PUT" or "PATCH" && body != null)
            {
                var contentType = headers.TryGetValue("Content-Type", out var ct)
                    ? ct
                    : "application/json";

                request.Content = new StringContent(body, Encoding.UTF8, contentType);
            }

            using var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                XTrace.Log.Warn("[HttpWebhookHandler] HTTP请求失败: {0} {1}, 状态码: {2}",
                    method, url, (int)response.StatusCode);

                throw new HttpRequestException(
                    $"HTTP请求失败，状态码: {(int)response.StatusCode} ({response.StatusCode})\n" +
                    $"响应内容: {Truncate(responseContent, 500)}");
            }

            XTrace.Log.Info("[HttpWebhookHandler] HTTP请求成功: {0} {1}, 状态码: {2}",
                method, url, (int)response.StatusCode);

            return $"HTTP {method} 请求成功，状态码: {(int)response.StatusCode}\n响应: {Truncate(responseContent, 200)}";
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[HttpWebhookHandler] 执行HTTP Webhook任务失败: {0}", ex.Message);
            throw;
        }
    }

    private static WebhookConfig ParseWebhookConfig(string? inputParameters)
    {
        var config = new WebhookConfig
        {
            Method = "GET",
            Headers = new Dictionary<string, string>(),
            Body = null
        };

        if (string.IsNullOrWhiteSpace(inputParameters))
            return config;

        try
        {
            var json = JsonDocument.Parse(inputParameters);
            var root = json.RootElement;

            if (root.TryGetProperty("method", out var methodProp))
                config.Method = methodProp.GetString() ?? "GET";

            if (root.TryGetProperty("headers", out var headersProp) && headersProp.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in headersProp.EnumerateObject())
                {
                    config.Headers[prop.Name] = prop.Value.GetString() ?? string.Empty;
                }
            }

            if (root.TryGetProperty("body", out var bodyProp))
            {
                config.Body = bodyProp.ValueKind switch
                {
                    JsonValueKind.String => bodyProp.GetString(),
                    _ => bodyProp.GetRawText()
                };
            }
        }
        catch (JsonException ex)
        {
            XTrace.Log.Warn("[HttpWebhookHandler] 解析Webhook配置失败: {0}", ex.Message);
        }

        return config;
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text[..maxLength] + "...";
    }

    private class WebhookConfig
    {
        public string Method { get; set; } = "GET";
        public Dictionary<string, string> Headers { get; set; } = new();
        public string? Body { get; set; }
    }
}
