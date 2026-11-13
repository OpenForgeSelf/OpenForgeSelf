using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Controllers.UnifiedAI;
using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Services;
using ForgeSelf.Api.Services.AI;
using ForgeSelf.Api.Services.AI.Providers;
using ForgeSelf.Api.Services.AI.Proxy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ForgeSelf.Api.Tests.Integration;

/// <summary>
/// 公开 Chat 入口只把原始 body 送进管道，再把管道的状态码和字节写回。
/// </summary>
public class UnifiedAIGatewayIntegrationTests
{
    private readonly Mock<ILogService> _log = new();
    private readonly Mock<IChatTurnService> _turns = new();
    private readonly Mock<IChatTurnStreamRecorder> _recorder = new();
    private readonly Mock<IChatSessionService> _sessions = new();
    private readonly Mock<ILlmTurnAudit> _audit = new();

    public UnifiedAIGatewayIntegrationTests()
    {
        _recorder.Setup(r => r.BeginAsync(It.IsAny<ChatTurn>())).ReturnsAsync((ChatTurnStreamSession)null!);
        _turns.Setup(s => s.SaveTurnAsync(It.IsAny<ChatTurn>())).Returns(Task.CompletedTask);
        _turns.Setup(s => s.UpsertTurnAsync(It.IsAny<ChatTurn>())).Returns(Task.CompletedTask);
        _sessions.Setup(s => s.UpsertSessionAsync(
                It.IsAny<string>(), It.IsAny<SessionSource>(), It.IsAny<string?>(), It.IsAny<ClientKind>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>()))
            .ReturnsAsync(new ChatSession { Id = 1, SessionKey = "test-session", RequestCount = 1 });
        _sessions.Setup(s => s.RecordTurnStatsAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task ChatCompletions_ForwardsBody_AndRewritesPrefixedModel()
    {
        var (controller, capture) = Create();
        await controller.ChatCompletions(CancellationToken.None);

        controller.Response.StatusCode.Should().Be(200);
        var sent = JsonNode.Parse(capture.Body!)!.AsObject();
        sent["model"]!.GetValue<string>().Should().Be("gpt-4");
        sent["stream_options"]!["include_usage"]!.GetValue<bool>().Should().BeTrue();
        sent["messages"].Should().BeNull();
    }

    [Fact]
    public async Task ChatCompletions_EmptyModel_Returns400WithoutUpstreamCall()
    {
        var (controller, capture) = Create("""{"model":""}""");
        await controller.ChatCompletions(CancellationToken.None);

        controller.Response.StatusCode.Should().Be(400);
        capture.Count.Should().Be(0);
    }

    [Fact]
    public async Task ChatCompletions_StreamCopiesUpstreamUsageBytes()
    {
        var sse = "data: {\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":5,\"total_tokens\":15}}\n\n";
        var (controller, _) = Create("""{"model":"test:gpt-4","stream":true}""", sse, "text/event-stream");
        await controller.ChatCompletions(CancellationToken.None);

        controller.Response.Body.Position = 0;
        var body = await new StreamReader(controller.Response.Body).ReadToEndAsync();
        body.Should().Be(sse);
        body.Should().Contain("prompt_tokens");
    }

    [Fact]
    public async Task ChatCompletions_Upstream429_PassesStatusAndBody()
    {
        var error = """{"error":{"message":"rate_limit_error 429"}}""";
        var (controller, _) = Create("""{"model":"test:gpt-4","stream":true}""", error, "application/json", HttpStatusCode.TooManyRequests);
        await controller.ChatCompletions(CancellationToken.None);

        controller.Response.StatusCode.Should().Be(429);
        controller.Response.Body.Position = 0;
        var body = await new StreamReader(controller.Response.Body).ReadToEndAsync();
        body.Should().Contain("rate_limit_error");
        body.Should().NotContain("data: [DONE]");
    }

    [Fact]
    public async Task ChatCompletions_SlowAuditSave_DoesNotHoldResponse()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _sessions.Setup(s => s.UpsertSessionAsync(
                It.IsAny<string>(), It.IsAny<SessionSource>(), It.IsAny<string?>(), It.IsAny<ClientKind>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>()))
            .Returns(async () =>
            {
                started.TrySetResult();
                await release.Task;
                return new ChatSession { Id = 1, SessionKey = "test-session", RequestCount = 1 };
            });

        var services = new ServiceCollection();
        services.AddSingleton<ILogService>(_log.Object);
        services.AddSingleton<IChatTurnService>(_turns.Object);
        services.AddSingleton<IChatTurnStreamRecorder>(_recorder.Object);
        services.AddSingleton<IChatSessionService>(_sessions.Object);
        services.AddScoped<ILlmTurnAuditStore, LlmTurnAuditStore>();
        services.AddSingleton<LlmTurnAuditQueue>();
        await using var provider = services.BuildServiceProvider();
        var queue = provider.GetRequiredService<LlmTurnAuditQueue>();
        await queue.StartAsync(CancellationToken.None);
        try
        {
            var (controller, _) = Create(audit: queue);
            var sw = Stopwatch.StartNew();
            await controller.ChatCompletions(CancellationToken.None);
            sw.Stop();

            sw.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(800));
            controller.Response.StatusCode.Should().Be(200);
            controller.Response.Body.Position = 0;
            var body = await new StreamReader(controller.Response.Body).ReadToEndAsync();
            body.Should().Contain("\"id\":\"ok\"");
            await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            release.TrySetResult();
            await queue.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ChatCompletions_AuditEnqueueThrows_StillReturnsUpstreamBody()
    {
        _audit.Setup(a => a.Enqueue(It.IsAny<LlmTurnAuditWork>()))
            .Throws(new InvalidOperationException("SQLite需要重建表"));

        var (controller, _) = Create();
        await controller.ChatCompletions(CancellationToken.None);

        controller.Response.StatusCode.Should().Be(200);
        controller.Response.Body.Position = 0;
        var body = await new StreamReader(controller.Response.Body).ReadToEndAsync();
        body.Should().Contain("\"id\":\"ok\"");
    }

    private (OpenAIChatController Controller, CaptureHandler Capture) Create(
        string requestJson = """{"model":"test:gpt-4","stream_options":{"include_usage":true}}""",
        string response = """{"id":"ok"}""",
        string media = "application/json",
        HttpStatusCode status = HttpStatusCode.OK,
        ILlmTurnAudit? audit = null)
    {
        var capture = new CaptureHandler(response, media, status);
        var config = new AIProviderConfig
        {
            Name = "test",
            ProviderType = AIProviderType.OpenAI,
            Endpoint = "https://upstream.test/v1",
            ApiKey = "sk",
            SupportedModels = ["gpt-4"],
            IsDefault = true,
            TimeoutSeconds = 30
        };
        var registry = new AIProviderRegistry();
        registry.RegisterProvider(new OpenAICompatibleProvider(config, new HttpClient(capture)));
        var controller = new OpenAIChatController(
            new LlmProxyPipeline(registry), _log.Object, audit ?? _audit.Object);
        var http = new DefaultHttpContext();
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(requestJson));
        http.Response.Body = new MemoryStream();
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (controller, capture);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        private readonly string _response;
        private readonly string _media;
        private readonly HttpStatusCode _status;
        public int Count { get; private set; }
        public string? Body { get; private set; }

        public CaptureHandler(string response, string media, HttpStatusCode status)
        {
            _response = response;
            _media = media;
            _status = status;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Count++;
            Body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_response, Encoding.UTF8, _media)
            };
        }
    }
}
