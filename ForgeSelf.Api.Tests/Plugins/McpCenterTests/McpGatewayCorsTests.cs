using System.Net;
using System.Net.Sockets;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.McpCenter.Services;
using ForgeSelf.Core;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.McpCenterTests;

public sealed class McpGatewayCorsTests
{
    [Fact]
    public async Task McpEndpoint_AllowsConfiguredOriginPreflightAndKeepsBearerGuard()
    {
        var config = new McpGatewayConfig
        {
            ListenHost = "127.0.0.1",
            Port = GetFreePort(),
            Token = "cors-test-token",
            AllowedOrigins = ["https://client.example"]
        };
        var store = new ExternalServersStore(Path.Combine(Path.GetTempPath(), "mcp-cors-tests", Guid.NewGuid().ToString("N")));
        var forwarder = new UniversalToolForwarder(new Mock<IContext>().Object, new McpClientManager(store));
        await using var server = new McpGatewayServer(config, forwarder, "test");
        await server.StartAsync();
        Assert.True(server.IsRunning);

        using var client = new HttpClient { BaseAddress = new Uri(config.ListenUrl) };

        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/mcp");
        preflight.Headers.Add("Origin", "https://client.example");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "authorization,content-type,accept");
        using var preflightResponse = await client.SendAsync(preflight);

        Assert.Equal(HttpStatusCode.NoContent, preflightResponse.StatusCode);
        Assert.Equal("https://client.example", preflightResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains("POST", preflightResponse.Headers.GetValues("Access-Control-Allow-Methods").Single());
        Assert.Contains("authorization", preflightResponse.Headers.GetValues("Access-Control-Allow-Headers").Single(), StringComparison.OrdinalIgnoreCase);

        using var unauthorized = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        };
        unauthorized.Headers.Add("Origin", "https://client.example");
        unauthorized.Headers.Add("Accept", "application/json, text/event-stream");
        using var unauthorizedResponse = await client.SendAsync(unauthorized);

        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedResponse.StatusCode);
        Assert.Equal("https://client.example", unauthorizedResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());

        using var deniedPreflight = new HttpRequestMessage(HttpMethod.Options, "/mcp");
        deniedPreflight.Headers.Add("Origin", "https://other.example");
        deniedPreflight.Headers.Add("Access-Control-Request-Method", "POST");
        using var deniedResponse = await client.SendAsync(deniedPreflight);

        Assert.Equal(HttpStatusCode.Forbidden, deniedResponse.StatusCode);
        Assert.False(deniedResponse.Headers.Contains("Access-Control-Allow-Origin"));

        using var deniedRequest = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        };
        deniedRequest.Headers.Add("Origin", "https://other.example");
        deniedRequest.Headers.Add("Authorization", "Bearer cors-test-token");
        deniedRequest.Headers.Add("Accept", "application/json, text/event-stream");
        using var deniedActualResponse = await client.SendAsync(deniedRequest);
        Assert.False(deniedActualResponse.Headers.Contains("Access-Control-Allow-Origin"));

        using var healthRequest = new HttpRequestMessage(HttpMethod.Get, "/health");
        healthRequest.Headers.Add("Origin", "https://client.example");
        using var healthResponse = await client.SendAsync(healthRequest);
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
        Assert.False(healthResponse.Headers.Contains("Access-Control-Allow-Origin"));

        using var subpathRequest = new HttpRequestMessage(HttpMethod.Post, "/mcp/extra")
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        };
        subpathRequest.Headers.Add("Origin", "https://client.example");
        using var subpathResponse = await client.SendAsync(subpathRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, subpathResponse.StatusCode);
        Assert.False(subpathResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
