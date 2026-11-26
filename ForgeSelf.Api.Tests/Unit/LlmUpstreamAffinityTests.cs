using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Services.AI;
using ForgeSelf.Api.Services.AI.Providers;
using ForgeSelf.Api.Services.AI.Proxy;

namespace ForgeSelf.Api.Tests.Unit;

public class LlmUpstreamAffinityTests
{
    private readonly XaiGrokAffinity _xai = new();

    [Theory]
    [InlineData("https://api.x.ai/v1", "grok", "grok-4.6", true)]
    [InlineData("https://api.x.ai/v1", "other", "gpt-4", true)]
    [InlineData("https://upstream.test/v1", "grok", "gpt-4", true)]
    [InlineData("https://upstream.test/v1", "test", "grok-4.6", true)]
    [InlineData("https://upstream.test/v1", "test", "gpt-4", false)]
    public void Matches_UsesEndpointProviderOrModel_NotResponseBody(string endpoint, string provider, string model, bool expected)
    {
        _xai.Matches(Ctx(endpoint, provider, model)).Should().Be(expected);
    }

    [Fact]
    public void Apply_NoClientHints_WritesPromptCacheKeyFromStaticPrefix_DoesNotInventConvId()
    {
        var ctx = Ctx("https://api.x.ai/v1", "grok", "grok-4.6", """
            {
              "model": "grok-4.6",
              "messages": [
                { "role": "system", "content": "you are grok" },
                { "role": "user", "content": "hi" }
              ],
              "tools": [ { "type": "function", "function": { "name": "lookup" } } ]
            }
            """);

        _xai.Apply(ctx);

        var key = ctx.Body["prompt_cache_key"]!.GetValue<string>();
        key.Should().HaveLength(64);
        key.Should().MatchRegex("^[0-9a-f]{64}$");
        ctx.OutboundHeaders.Should().NotContainKey("x-grok-conv-id");
    }

    [Fact]
    public void Apply_SamePrefix_IsStable_ToolsAndSystemChangeTheKey()
    {
        var a = Ctx("https://api.x.ai/v1", "grok", "grok-4.6", Body("sys", "lookup"));
        var b = Ctx("https://api.x.ai/v1", "grok", "grok-4.6", Body("sys", "lookup"));
        var c = Ctx("https://api.x.ai/v1", "grok", "grok-4.6", Body("other", "lookup"));
        var d = Ctx("https://api.x.ai/v1", "grok", "grok-4.6", Body("sys", "other_tool"));
        _xai.Apply(a);
        _xai.Apply(b);
        _xai.Apply(c);
        _xai.Apply(d);

        a.Body["prompt_cache_key"]!.GetValue<string>().Should().Be(b.Body["prompt_cache_key"]!.GetValue<string>());
        a.Body["prompt_cache_key"]!.GetValue<string>().Should().NotBe(c.Body["prompt_cache_key"]!.GetValue<string>());
        a.Body["prompt_cache_key"]!.GetValue<string>().Should().NotBe(d.Body["prompt_cache_key"]!.GetValue<string>());
    }

    [Fact]
    public void Apply_ExistingPromptCacheKey_IsNotOverwritten()
    {
        var ctx = Ctx("https://api.x.ai/v1", "grok", "grok-4.6", """
            { "model": "grok-4.6", "prompt_cache_key": "client-key", "messages": [ { "role": "system", "content": "s" } ] }
            """);

        _xai.Apply(ctx);

        ctx.Body["prompt_cache_key"]!.GetValue<string>().Should().Be("client-key");
    }

    [Fact]
    public void Apply_InboundConvId_IsCopied_AndDoesNotInjectKeyThatWouldOverrideIt()
    {
        var ctx = Ctx("https://api.x.ai/v1", "grok", "grok-4.6", """
            { "model": "grok-4.6", "messages": [ { "role": "system", "content": "s" }, { "role": "user", "content": "hi" } ] }
            """, headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["X-Grok-Conv-Id"] = "conv_from_client"
            });

        _xai.Apply(ctx);

        ctx.OutboundHeaders["x-grok-conv-id"].Should().Be("conv_from_client");
        ctx.Body["prompt_cache_key"].Should().BeNull();
    }

    [Fact]
    public void Chain_OnlyMatchingPolicyRuns_OpenAIPlaceholderDoesNotFireOnXai()
    {
        var spy = new RecordingAffinity("openai.com");
        var chain = new LlmUpstreamAffinityChain([_xai, spy]);
        var ctx = Ctx("https://api.x.ai/v1", "grok", "grok-4.6", Body("s", "t"));

        chain.Apply(ctx);

        spy.Applied.Should().BeFalse();
        ctx.Body["prompt_cache_key"].Should().NotBeNull();
    }

    [Fact]
    public async Task Pipeline_Xai_NoClientHints_WritesPromptCacheKey()
    {
        var (pipeline, capture) = CreateXai(new XaiGrokAffinity());
        var result = await pipeline.ExecuteAsync(new LlmProxyRequest
        {
            Protocol = LlmWireProtocol.ChatCompletions,
            BodyJson = """
                {
                  "model": "grok:grok-4.6",
                  "messages": [
                    { "role": "system", "content": "you are grok" },
                    { "role": "user", "content": "hi" }
                  ]
                }
                """
        });

        result.StatusCode.Should().Be(200);
        var key = JsonNode.Parse(capture.Body!)!["prompt_cache_key"]!.GetValue<string>();
        key.Should().HaveLength(64);
        capture.Header("x-grok-conv-id").Should().BeNull();
        await result.DisposeAsync();
    }

    [Fact]
    public async Task Pipeline_XaiProvider_InjectsKeyAndForwardsConvId_StripsAuthorizationFromAffinity()
    {
        var (pipeline, capture) = CreateXai(new XaiGrokAffinity());
        var result = await pipeline.ExecuteAsync(new LlmProxyRequest
        {
            Protocol = LlmWireProtocol.ChatCompletions,
            BodyJson = """
                {
                  "model": "grok:grok-4.6",
                  "messages": [
                    { "role": "system", "content": "you are grok" },
                    { "role": "user", "content": "hi" }
                  ]
                }
                """,
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["x-grok-conv-id"] = "sess-1",
                ["Authorization"] = "Bearer should-not-leak"
            }
        });

        result.StatusCode.Should().Be(200);
        capture.Header("x-grok-conv-id").Should().Be("sess-1");
        capture.Header("Authorization").Should().Be("Bearer sk-xai");
        JsonNode.Parse(capture.Body!)!["prompt_cache_key"].Should().BeNull();
        await result.DisposeAsync();
    }

    [Fact]
    public async Task Pipeline_NonXaiProvider_LeavesBodyAndHeadersAlone()
    {
        var (pipeline, capture) = CreateChat(new XaiGrokAffinity());
        var result = await pipeline.ExecuteAsync(new LlmProxyRequest
        {
            Protocol = LlmWireProtocol.ChatCompletions,
            BodyJson = """{"model":"test:gpt-4","messages":[{"role":"system","content":"s"},{"role":"user","content":"hi"}]}""",
            Headers = new Dictionary<string, string> { ["x-grok-conv-id"] = "sess-1" }
        });

        result.StatusCode.Should().Be(200);
        JsonNode.Parse(capture.Body!)!["prompt_cache_key"].Should().BeNull();
        capture.Header("x-grok-conv-id").Should().BeNull();
        await result.DisposeAsync();
    }

    private static LlmAffinityContext Ctx(string endpoint, string provider, string model, string? body = null, IReadOnlyDictionary<string, string>? headers = null)
        => new()
        {
            ProviderName = provider,
            Endpoint = endpoint,
            UpstreamModelId = model,
            Protocol = LlmWireProtocol.ChatCompletions,
            Body = JsonNode.Parse(body ?? """{"model":"x","messages":[]}""")!.AsObject(),
            InboundHeaders = headers ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };

    private static string Body(string system, string tool)
        => $$"""
            {
              "model": "grok-4.6",
              "messages": [ { "role": "system", "content": "{{system}}" }, { "role": "user", "content": "hi" } ],
              "tools": [ { "type": "function", "function": { "name": "{{tool}}" } } ]
            }
            """;

    private static (LlmProxyPipeline Pipeline, CaptureHandler Capture) CreateChat(params ILlmUpstreamAffinity[] affinities)
        => Create(AIProviderType.OpenAI, "test", "https://upstream.test/v1", "gpt-4", "sk-test", affinities);

    private static (LlmProxyPipeline Pipeline, CaptureHandler Capture) CreateXai(params ILlmUpstreamAffinity[] affinities)
        => Create(AIProviderType.OpenAI, "grok", "https://api.x.ai/v1", "grok-4.6", "sk-xai", affinities);

    private static (LlmProxyPipeline Pipeline, CaptureHandler Capture) Create(
        AIProviderType type, string name, string endpoint, string model, string key, ILlmUpstreamAffinity[] affinities)
    {
        var capture = new CaptureHandler();
        var http = new HttpClient(capture);
        var config = new AIProviderConfig
        {
            Name = name,
            ProviderType = type,
            Endpoint = endpoint,
            ApiKey = key,
            SupportedModels = [model],
            IsDefault = true,
            TimeoutSeconds = 30
        };
        IAIProvider provider = new OpenAICompatibleProvider(config, http);
        var registry = new AIProviderRegistry();
        registry.RegisterProvider(provider);
        return (new LlmProxyPipeline(registry, affinities), capture);
    }

    private sealed class RecordingAffinity : ILlmUpstreamAffinity
    {
        private readonly string _hostFragment;
        public RecordingAffinity(string hostFragment) => _hostFragment = hostFragment;
        public bool Applied { get; private set; }
        public bool Matches(LlmAffinityContext ctx)
            => ctx.Endpoint.Contains(_hostFragment, StringComparison.OrdinalIgnoreCase);
        public void Apply(LlmAffinityContext ctx) => Applied = true;
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }
        private readonly Dictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);

        public string? Header(string name) => _headers.TryGetValue(name, out var value) ? value : null;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            foreach (var header in request.Headers)
                _headers[header.Key] = string.Join(",", header.Value);
            Body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"ok","choices":[{"message":{"role":"assistant","content":"hi"}}]}""", Encoding.UTF8, "application/json")
            };
        }
    }
}
