using System.Net;
using System.Text;
using System.Text.Json;
using ForgeSelf.Api.Plugins.McpCenter.Services.McpClient;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.McpCenterTests;

/// <summary>
/// 类守卫（PC-008 M3-b，v2.5.0）：Streamable HTTP 传输的会话复用——
/// 服务器在 initialize 响应返回 Mcp-Session-Id 头时，后续所有 POST/GET 必须回传
/// Mcp-Session-Id 与协商后的 Mcp-Protocol-Version 头（MCP 2025-06-18 规范）；
/// 无状态服务器（不返回会话头，如 DeepWiki）则不得凭空携带。
/// </summary>
public class McpStreamableSessionHeaderTests
{
    private sealed class SessionFakeHandler : HttpMessageHandler
    {
        public List<(string? Method, HttpRequestMessage Request, string Body)> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content != null
                ? await request.Content.ReadAsStringAsync(cancellationToken)
                : string.Empty;
            string? method = null;
            if (!string.IsNullOrEmpty(body))
            {
                using var d = JsonDocument.Parse(body);
                method = d.RootElement.TryGetProperty("method", out var m) ? m.GetString() : null;
            }
            Requests.Add((method, request, body));

            var resultJson = method switch
            {
                "initialize" => """{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2025-06-18","capabilities":{},"serverInfo":{"name":"Fake","version":"1"}}}""",
                "tools/list" => """{"jsonrpc":"2.0","id":2,"result":{"tools":[]}}""",
                _ => """{"jsonrpc":"2.0","id":3,"result":{}}"""
            };
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(resultJson, Encoding.UTF8, "application/json")
            };
            // 模拟有状态服务器：initialize 响应携带会话头
            if (method == "initialize")
                resp.Headers.TryAddWithoutValidation("Mcp-Session-Id", "sess-42");
            return resp;
        }
    }

    [Fact]
    public async Task SessionId_From_Initialize_Response_Is_Echoed_On_Subsequent_Requests()
    {
        var handler = new SessionFakeHandler();
        var transport = new StreamableHttpMcpTransport("https://fake.example/mcp", null, handler);
        try
        {
            await transport.InitializeAsync(McpClientSession.SupportedProtocolVersions, CancellationToken.None);
            await transport.ListToolsAsync(CancellationToken.None);

            var listReq = handler.Requests.First(r => r.Method == "tools/list").Request;
            Assert.True(
                listReq.Headers.TryGetValues("Mcp-Session-Id", out var sid) && sid.SequenceEqual(new[] { "sess-42" }),
                "后续请求未回传服务器下发的 Mcp-Session-Id");
            Assert.True(
                listReq.Headers.TryGetValues("Mcp-Protocol-Version", out var pv) && pv.SequenceEqual(new[] { "2025-06-18" }),
                "后续请求未携带协商后的 Mcp-Protocol-Version");

            // initialized 通知同样须带会话头（通知发生在 initialize 之后）
            var notifyReq = handler.Requests.First(r => r.Method == "notifications/initialized").Request;
            Assert.True(notifyReq.Headers.Contains("Mcp-Session-Id"), "initialized 通知未回传会话头");
        }
        finally
        {
            await transport.CloseAsync();
        }
    }

    [Fact]
    public async Task StatelessServer_NoSessionHeader_Is_Carried()
    {
        // 无会话头服务器（DeepWiki 型）：initialize 不带 clientInfo 之外的会话语义，后续请求不得凭空造头
        var handler = new NoSessionFakeHandler();
        var transport = new StreamableHttpMcpTransport("https://fake.example/mcp", null, handler);
        try
        {
            await transport.InitializeAsync(McpClientSession.SupportedProtocolVersions, CancellationToken.None);
            await transport.ListToolsAsync(CancellationToken.None);

            var listReq = handler.Requests.First(r => r.Method == "tools/list").Request;
            Assert.False(listReq.Headers.Contains("Mcp-Session-Id"), "无状态会话不应携带 Mcp-Session-Id");
        }
        finally
        {
            await transport.CloseAsync();
        }
    }

    private sealed class NoSessionFakeHandler : HttpMessageHandler
    {
        public List<(string? Method, HttpRequestMessage Request)> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string? method = null;
            if (request.Content != null)
            {
                var body = await request.Content.ReadAsStringAsync(cancellationToken);
                using var d = JsonDocument.Parse(body);
                method = d.RootElement.TryGetProperty("method", out var m) ? m.GetString() : null;
            }
            Requests.Add((method, request));
            var resultJson = method == "tools/list"
                ? """{"jsonrpc":"2.0","id":2,"result":{"tools":[]}}"""
                : """{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2025-06-18","capabilities":{},"serverInfo":{"name":"Fake","version":"1"}}}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(resultJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
