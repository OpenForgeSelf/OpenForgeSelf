using System.Net.Http;
using System.Text;
using FluentAssertions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Services.AI;
using ForgeSelf.Api.Services.AI.Providers;
using Xunit;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// PILOT-074：OpenAISdkAdapter / OpenAIResponsesProvider 行为测试（AC-1..AC-5，BC-4 回归守卫）。
/// 零 mock 出站协议：FakeHandler 抓 SDK 实际 wire 请求 + 回放预设响应（与 AIModelFeatureTests 同套基建）。
/// </summary>
public class OpenAISdkAdapterTests
{
    private static AIProviderConfig MakeConfig(string type, string endpoint)
    {
        return new AIProviderConfig
        {
            Name = "测试提供方",
            Endpoint = endpoint,
            ApiKey = "sk-test",
            TimeoutSeconds = 30,
            ProviderType = System.Enum.Parse<AIProviderType>(type),
        };
    }

    private static UnifiedChatRequest MakeSimpleRequest()
    {
        return new UnifiedChatRequest
        {
            Model = "gpt-test",
            Messages = new List<UnifiedChatMessage>
            {
                new() { Role = "user", Content = "你好" }
            }
        };
    }

    private static HttpResponseMessage ChatJson()
    {
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"id\":\"chatcmpl-x1\",\"object\":\"chat.completion\",\"model\":\"gpt-test\"," +
                "\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":\"你好，世界\"},\"finish_reason\":\"stop\"}]," +
                "\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":5,\"total_tokens\":15}}",
                Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage ResponsesJson()
    {
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"id\":\"resp-x1\",\"object\":\"response\",\"status\":\"completed\",\"model\":\"gpt-test\"," +
                "\"output\":[{\"type\":\"message\",\"role\":\"assistant\",\"content\":[{\"type\":\"output_text\",\"text\":\"hi\"}]}]," +
                "\"usage\":{\"input_tokens\":7,\"output_tokens\":3,\"total_tokens\":10}}",
                Encoding.UTF8, "application/json")
        };
    }

    // ==================== AC-5：URL 推导 ====================

    [Theory]
    [InlineData("https://api.example.com", "https://api.example.com/v1/responses")]
    [InlineData("https://api.example.com/", "https://api.example.com/v1/responses")]
    [InlineData("https://api.example.com/v1", "https://api.example.com/v1/responses")]
    [InlineData("https://api.example.com/v1/", "https://api.example.com/v1/responses")]
    [InlineData("https://gw.example.com/v1/responses", "https://gw.example.com/v1/responses")]
    public async Task Responses_Endpoint_FourForms_Normalize_ToResponsesUrl(string endpoint, string expectedUrl)
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHandler(req =>
        {
            captured = req;
            return ResponsesJson();
        });
        var http = new HttpClient(handler);
        var cfg = MakeConfig("OpenAIResponses", endpoint);

        await OpenAISdkAdapter.ResponsesChatAsync(cfg, MakeSimpleRequest(), http, "测试提供方");

        captured!.RequestUri!.ToString().Should().Be(expectedUrl);
    }

    [Theory]
    [InlineData("https://api.example.com", "https://api.example.com/v1/chat/completions")]
    [InlineData("https://api.example.com/v1", "https://api.example.com/v1/chat/completions")]
    [InlineData("https://api.example.com/gw", "https://api.example.com/gw/chat/completions")]
    [InlineData("https://api.example.com/gw/chat/completions", "https://api.example.com/gw/chat/completions")]
    public async Task Chat_BaseDerivation_Matches_LegacyNormalization(string endpoint, string expectedUrl)
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHandler(req =>
        {
            captured = req;
            return ChatJson();
        });
        var http = new HttpClient(handler);
        var cfg = MakeConfig("OpenAI", endpoint);

        await OpenAISdkAdapter.ChatAsync(cfg, MakeSimpleRequest(), http, "测试提供方");

        captured!.RequestUri!.ToString().Should().Be(expectedUrl);
    }

    // ==================== 输入6/9：Anthropic 型 → 官方 OpenAI 兼容层 ====================

    [Theory]
    [InlineData("https://api.anthropic.com", "https://api.anthropic.com/v1/chat/completions")]
    [InlineData("https://api.anthropic.com/v1", "https://api.anthropic.com/v1/chat/completions")]
    [InlineData("https://api.anthropic.com/v1/messages", "https://api.anthropic.com/v1/chat/completions")]
    [InlineData("https://gw.example.com/anthropic", "https://gw.example.com/anthropic/v1/chat/completions")]
    public async Task Anthropic_Type_Chat_Goes_To_OpenAICompatEndpoint(string endpoint, string expectedUrl)
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHandler(req =>
        {
            captured = req;
            return ChatJson();
        });
        var http = new HttpClient(handler);
        var cfg = MakeConfig("Anthropic", endpoint);

        await OpenAISdkAdapter.ChatAsync(cfg, MakeSimpleRequest(), http, "测试提供方");

        captured!.RequestUri!.ToString().Should().Be(expectedUrl);
    }

    // ==================== AC-4：chat 非流式金master（Unified 映射保真） =======================

    [Fact]
    public async Task ChatAsync_Maps_FakeWireResponse_ToUnified()
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHandler(req =>
        {
            captured = req;
            return ChatJson();
        });
        var http = new HttpClient(handler);
        var cfg = MakeConfig("OpenAI", "https://api.example.com/v1");

        var unified = await OpenAISdkAdapter.ChatAsync(cfg, MakeSimpleRequest(), http, "测试提供方");

        unified.Id.Should().Be("chatcmpl-x1");
        unified.Model.Should().Be("gpt-test");
        unified.ProviderName.Should().Be("测试提供方");
        unified.FinishReason.Should().Be("stop");
        unified.Choices.Should().HaveCount(1);
        unified.Choices[0].Role.Should().Be("assistant");
        unified.Choices[0].Content.Should().Be("你好，世界");
        unified.Usage!.PromptTokens.Should().Be(10);
        unified.Usage.CompletionTokens.Should().Be(5);
        unified.Usage.TotalTokens.Should().Be(15);

        captured!.Headers.ToString().Should().Contain("Bearer sk-test");
    }

    /// <summary>U-1 实证：SDK 对 MaxTokens 实际发出的 wire 字段名（max_completion_tokens 或 max_tokens，恰其一）。</summary>
    [Fact]
    public async Task U1_MaxTokens_WireFieldName_ExactlyOne()
    {
        string? body = null;
        var handler = new CaptureBodyHandler(() => ChatJson(), b => body = b);
        var http = new HttpClient(handler);
        var cfg = MakeConfig("OpenAI", "https://api.example.com/v1");

        var request = MakeSimpleRequest();
        request.MaxTokens = 128;

        await OpenAISdkAdapter.ChatAsync(cfg, request, http, "测试提供方");

        body.Should().NotBeNull();
        var hasNew = body!.Contains("max_completion_tokens");
        var hasLegacy = body.Contains("\"max_tokens\"");
        (hasNew ^ hasLegacy).Should().BeTrue($"MaxTokens 必须恰好映射为一种字段：body={body}");
        // U-1 钉板：实测为 max_completion_tokens（SDK 默认）；若上游兼容性需要 max_tokens走 BC-3 决策
        hasNew.Should().BeTrue($"U-1 实测应为 max_completion_tokens：body={body}");
    }

    /// <summary>BC-4 回归守卫：Custom 类型 chat 出站不再抛（既有实现对 Custom URL 推导必抛）。</summary>
    [Fact]
    public async Task Custom_Type_Chat_Does_Not_Throw()
    {
        var handler = new FakeHandler(_ => ChatJson());
        var http = new HttpClient(handler);
        var cfg = MakeConfig("Custom", "https://local.example.com");

        var act = async () => await OpenAISdkAdapter.ChatAsync(cfg, MakeSimpleRequest(), http, "测试提供方");

        await act.Should().NotThrowAsync("Custom 视同 OpenAI 兼容（BC-4）");
    }

    // ==================== chat 流式：内容/usage 哨兵/终止块 ====================

    [Fact]
    public async Task ChatStreamAsync_Yields_Content_Usage_Terminator()
    {
        var sse = string.Concat(
            "data: {\"id\":\"chatcmpl-s1\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\"}}]}\n\n",
            "data: {\"id\":\"chatcmpl-s1\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"你\"}}]}\n\n",
            "data: {\"id\":\"chatcmpl-s1\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"好\"}}]}\n\n",
            "data: {\"id\":\"chatcmpl-s1\",\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}],\"usage\":{\"prompt_tokens\":3,\"completion_tokens\":2,\"total_tokens\":5}}\n\n",
            "data: [DONE]\n\n");
        var handler = new FakeHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(sse, Encoding.UTF8, "text/event-stream")
        });
        var http = new HttpClient(handler);
        var cfg = MakeConfig("OpenAI", "https://api.example.com/v1");

        var chunks = new List<UnifiedStreamChunk>();
        await foreach (var c in OpenAISdkAdapter.ChatStreamAsync(cfg, MakeSimpleRequest(), http, "测试提供方"))
            chunks.Add(c);

        chunks.Where(c => !string.IsNullOrEmpty(c.DeltaContent))
            .Select(c => c.DeltaContent)
            .Should().ContainInOrder("你", "好");
        chunks.Should().Contain(c => c.Usage != null && c.Usage.TotalTokens == 5);
        var last = chunks.Last();
        last.IsDone.Should().BeTrue();
        last.FinishReason.Should().Be("stop");
    }

    // ==================== Responses 流式：文本 delta / usage / 终止 ====================

    [Fact]
    public async Task ResponsesStreamAsync_Yields_TextDelta_Usage_Terminator()
    {
        var sse = string.Concat(
            "event: response.output_text.delta\n",
            "data: {\"type\":\"response.output_text.delta\",\"delta\":\"he\"}\n\n",
            "event: response.completed\n",
            "data: {\"type\":\"response.completed\",\"response\":{\"id\":\"resp-s1\",\"object\":\"response\",\"status\":\"completed\",\"model\":\"gpt-test\",\"usage\":{\"input_tokens\":4,\"output_tokens\":2,\"total_tokens\":6}}}\n\n");
        var handler = new FakeHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(sse, Encoding.UTF8, "text/event-stream")
        });
        var http = new HttpClient(handler);
        var cfg = MakeConfig("OpenAIResponses", "https://api.example.com/v1");

        var chunks = new List<UnifiedStreamChunk>();
        await foreach (var c in OpenAISdkAdapter.ResponsesStreamAsync(cfg, MakeSimpleRequest(), http, "测试提供方"))
            chunks.Add(c);

        chunks.Where(c => !string.IsNullOrEmpty(c.DeltaContent))
            .Select(c => c.DeltaContent)
            .Should().Contain("he");
        chunks.Should().Contain(c => c.Usage != null && c.Usage.TotalTokens == 6);
        chunks.Last().IsDone.Should().BeTrue();
    }

    // ==================== FR-6：注册表按 ProviderType 分派 ====================

    [Fact]
    public async Task Registry_Dispatches_OpenAIResponses_To_NewProvider()
    {
        var registry = new AIProviderRegistry();
        var factory = new HandlerHttpClientFactory(new FakeHandler(_ => ChatJson()));
        var configs = new List<AIProviderConfig>
        {
            new()
            {
                Name = "r1",
                Endpoint = "https://a.example.com/v1",
                ProviderType = AIProviderType.OpenAIResponses,
                SupportedModels = new List<string> { "gpt-test" },
                TimeoutSeconds = 30,
            },
            new()
            {
                Name = "r2",
                Endpoint = "https://b.example.com/v1",
                ProviderType = AIProviderType.OpenAI,
                SupportedModels = new List<string> { "gpt-test" },
                TimeoutSeconds = 30,
            },
        };

        await registry.ReloadAsync(factory, configs);

        registry.GetProviderByName("r1").Should().BeOfType<OpenAIResponsesProvider>();
        registry.GetProviderByName("r2").Should().BeOfType<OpenAICompatibleProvider>();
    }

    // ==================== AC-7：AIService 回退 reasoning_content 回放（U-2 放行路径） ====================

    /// <summary>deepseek 系响应形状：正文空、reasoning_content 承载思考文本。</summary>
    private static string DeepseekReasoningJson() =>
        """{"id":"chatcmpl-r1","object":"chat.completion","created":1730000000,"model":"deepseek-test","choices":[{"index":0,"message":{"role":"assistant","content":"","reasoning_content":"深度思考过程"},"finish_reason":"stop"}],"usage":{"prompt_tokens":3,"completion_tokens":2,"total_tokens":5}}""";

    [Fact]
    public async Task ChatAsync_ReasoningFallback_ExtractsReasoningWhenContentEmpty()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(DeepseekReasoningJson(), Encoding.UTF8, "application/json")
        });
        var http = new HttpClient(handler);
        var cfg = MakeConfig("OpenAI", "https://api.example.com/v1");

        var withFlag = await OpenAISdkAdapter.ChatAsync(cfg, MakeSimpleRequest(), http, "测试提供方",
            CancellationToken.None, reasoningAsContentFallback: true);
        withFlag.Choices[0].Content.Should().BeEmpty();
        withFlag.Choices[0].ReasoningContent.Should().Be("深度思考过程");

        // 开关关闭（主链路默认）不做往返提取：ReasoningContent 保持空，不付 roundtrip 成本
        var withoutFlag = await OpenAISdkAdapter.ChatAsync(cfg, MakeSimpleRequest(), http, "测试提供方");
        withoutFlag.Choices[0].ReasoningContent.Should().BeNull();
    }

    [Fact]
    public async Task ChatStreamAsync_ReasoningFallback_ReplaysReasoningAsContent()
    {
        var sse = string.Concat(
            "data: {\"id\":\"c-r\",\"object\":\"chat.completion.chunk\",\"model\":\"deepseek-test\",\"choices\":[{\"index\":0,\"delta\":{\"reasoning_content\":\"思考片段\"}}]}\n\n",
            "data: {\"id\":\"c-r\",\"object\":\"chat.completion.chunk\",\"model\":\"deepseek-test\",\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}\n\n",
            "data: [DONE]\n\n");
        var handler = new FakeHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(sse, Encoding.UTF8, "text/event-stream")
        });
        var http = new HttpClient(handler);
        var cfg = MakeConfig("OpenAI", "https://api.example.com/v1");

        var chunks = new List<UnifiedStreamChunk>();
        await foreach (var c in OpenAISdkAdapter.ChatStreamAsync(cfg, MakeSimpleRequest(), http, "测试提供方",
            CancellationToken.None, reasoningAsContentFallback: true))
            chunks.Add(c);

        // 旧回退语义：reasoning 增量以正文增量回放（AIService 迁移前的 delta.ReasoningContent → content 路径）
        chunks.Select(c => c.DeltaContent).Should().Contain("思考片段");
        chunks.Last().IsDone.Should().BeTrue();
    }
}

/// <summary>两段式捕获 handler：先读请求体再回预设响应</summary>
public class CaptureBodyHandler : HttpMessageHandler
{
    private readonly Func<HttpResponseMessage> _respond;
    private readonly Action<string> _onBody;

    public CaptureBodyHandler(Func<HttpResponseMessage> respond, Action<string> onBody)
    {
        _respond = respond;
        _onBody = onBody;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;
        _onBody(body);
        return Task.FromResult(_respond());
    }
}
