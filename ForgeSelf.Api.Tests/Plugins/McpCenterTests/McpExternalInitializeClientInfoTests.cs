using System.Net;
using System.Text;
using System.Text.Json;
using ForgeSelf.Api.Plugins.McpCenter.Services.McpClient;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.McpCenterTests;

/// <summary>
/// 类守卫 G1（2026-10-17 复现批立红，修复批转绿）：外部 MCP 客户端 initialize 必带规范必填 clientInfo，
/// 且握手完成后必发 notifications/initialized 通知。
/// 历史缺陷（PC-008）：三传输 initialize 缺 clientInfo → 严格服务器（DeepWiki）回 -32602
/// Invalid request parameters，「连接/测试」全灭；现场证据见
/// docs/ai/pilot/2026-10-17-mcpcenter-external-mcp-repro/05-evidence.md（运行实例 400 原文 + A/B 直连对照）。
/// 规范依据（2025-06-18）：InitializeRequest.params.clientInfo{name,version} 必填；
/// 客户端收到 initialize result 后 MUST 发送 notifications/initialized。
/// </summary>
public class McpExternalInitializeClientInfoTests
{
    /// <summary>捕获全部请求体并恒回合法 initialize result 的本地替身 handler（不发真实外网请求）。</summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content != null)
                RequestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));

            var resultJson = """{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":"2025-06-18","capabilities":{},"serverInfo":{"name":"Fake","version":"1"}}}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(resultJson, Encoding.UTF8, "application/json")
            };
        }
    }

    [Fact]
    public async Task StreamableHttp_Initialize_CarriesMandatoryClientInfo_ThenSendsInitializedNotification()
    {
        var handler = new CapturingHandler();
        var transport = new StreamableHttpMcpTransport("https://fake.example/mcp", null, handler);
        try
        {
            await transport.InitializeAsync(McpClientSession.SupportedProtocolVersions, CancellationToken.None);

            // 第一发：initialize，params 必带 clientInfo{name,version}
            Assert.True(handler.RequestBodies.Count >= 1, "initialize 请求未发出");
            using var initDoc = JsonDocument.Parse(handler.RequestBodies[0]);
            Assert.Equal("initialize", initDoc.RootElement.GetProperty("method").GetString());
            var prms = initDoc.RootElement.GetProperty("params");

            Assert.True(
                prms.TryGetProperty("clientInfo", out var clientInfo) &&
                clientInfo.ValueKind == JsonValueKind.Object,
                "initialize 缺少规范必填的 clientInfo（DeepWiki 实测不带即 -32602 Invalid request parameters，连接/测试全灭）");
            Assert.True(
                clientInfo.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(name.GetString()),
                "clientInfo.name 必填非空");
            Assert.True(
                clientInfo.TryGetProperty("version", out var version) && version.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(version.GetString()),
                "clientInfo.version 必填非空（缺 version 即重演 2026-10-17 stateless 批 e2e 的 SDK 严格反序列化 500 同族问题）");

            // 第二发：notifications/initialized（无 id 的 JSON-RPC 通知）
            var notified = handler.RequestBodies.Skip(1).Any(body =>
            {
                using var d = JsonDocument.Parse(body);
                return d.RootElement.TryGetProperty("method", out var m)
                    && m.GetString() == "notifications/initialized"
                    && !d.RootElement.TryGetProperty("id", out _);
            });
            Assert.True(notified, "握手完成后未发送 notifications/initialized 通知（规范要求 MUST 发送）");
        }
        finally
        {
            await transport.CloseAsync();
        }
    }
}
