using System.Net;
using System.Text;
using System.Text;
using System.Text.Json.Nodes;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Services.AI;
using ForgeSelf.Api.Services.AI.Providers;
using ForgeSelf.Api.Services.AI.Proxy;
using Microsoft.Extensions.AI;

namespace ForgeSelf.Api.Tests.Unit;

public class LlmProxyPipelineTests
{
    [Fact]
    public async Task SameProtocol_ForwardsUnknownFieldsAndImage_RewritesModelOnly()
    {
        var (pipeline, capture) = CreateChat();
        var body = """
            {
              "model": "test:gpt-4",
              "seed": 7,
              "reasoning_effort": "low",
              "unknown_field": { "k": 1 },
              "messages": [
                {
                  "role": "user",
                  "content": [
                    { "type": "text", "text": "see" },
                    { "type": "image_url", "image_url": { "url": "https://img.example/a.png", "detail": "high" } }
                  ]
                }
              ]
            }
            """;

        var result = await pipeline.ExecuteAsync(new LlmProxyRequest
        {
            Protocol = LlmWireProtocol.ChatCompletions,
            BodyJson = body
        });

        result.StatusCode.Should().Be(200);
        capture.Count.Should().Be(1);
        capture.Uri!.AbsoluteUri.Should().Be("https://upstream.test/v1/chat/completions");
        capture.Auth.Should().Be("Bearer sk-test");
        var sent = JsonNode.Parse(capture.Body!)!.AsObject();
        sent["model"]!.GetValue<string>().Should().Be("gpt-4");
        sent["seed"]!.GetValue<int>().Should().Be(7);
        sent["reasoning_effort"]!.GetValue<string>().Should().Be("low");
        sent["unknown_field"]!["k"]!.GetValue<int>().Should().Be(1);
        sent["messages"]![0]!["content"]![1]!["image_url"]!["url"]!.GetValue<string>()
            .Should().Be("https://img.example/a.png");
        await result.DisposeAsync();
    }

    [Fact]
    public async Task EmptyModel_Is400_AndDoesNotCallUpstream()
    {
        var (pipeline, capture) = CreateChat();
        var result = await pipeline.ExecuteAsync(new LlmProxyRequest
        {
            Protocol = LlmWireProtocol.ChatCompletions,
            BodyJson = """{"model":"","messages":[]}"""
        });

        result.StatusCode.Should().Be(400);
        capture.Count.Should().Be(0);
        await result.DisposeAsync();
    }

    [Fact]
    public async Task SameProtocolStream_CopiesUpstreamBytes()
    {
        var sse = "data: {\"choices\":[{\"delta\":{\"content\":\"A\"}}]}\n\ndata: [DONE]\n\n";
        var (pipeline, _) = CreateChat(sse, "text/event-stream");
        var result = await pipeline.ExecuteAsync(new LlmProxyRequest
        {
            Protocol = LlmWireProtocol.ChatCompletions,
            BodyJson = """{"model":"test:gpt-4","stream":true,"messages":[{"role":"user","content":"hi"}]}"""
        });

        result.Stream.Should().BeTrue();
        using var reader = new StreamReader(result.Body!);
        var text = await reader.ReadToEndAsync();
        text.Should().Be(sse);
        await result.DisposeAsync();
    }

    [Fact]
    public async Task Upstream429_PassesStatusAndBody()
    {
        var error = """{"error":{"message":"rate_limit_error","type":"rate_limit_error"}}""";
        var (pipeline, _) = CreateChat(error, "application/json", HttpStatusCode.TooManyRequests);
        var result = await pipeline.ExecuteAsync(new LlmProxyRequest
        {
            Protocol = LlmWireProtocol.ChatCompletions,
            BodyJson = """{"model":"test:gpt-4","messages":[{"role":"user","content":"hi"}]}"""
        });

        result.StatusCode.Should().Be(429);
        result.Stream.Should().BeFalse();
        using var reader = new StreamReader(result.Body!);
        (await reader.ReadToEndAsync()).Should().Be(error);
        await result.DisposeAsync();
    }

    [Fact]
    public async Task ChatToResponses_MapsStandardFields_AndDropsSeed()
    {
        var (pipeline, capture) = CreateResponses("""
            {
              "id": "resp_1",
              "object": "response",
              "status": "completed",
              "model": "gpt-4",
              "output": [
                { "type": "message", "role": "assistant", "content": [ { "type": "output_text", "text": "ok" } ] }
              ],
              "usage": { "input_tokens": 3, "output_tokens": 4, "total_tokens": 7 }
            }
            """);

        var result = await pipeline.ExecuteAsync(new LlmProxyRequest
        {
            Protocol = LlmWireProtocol.ChatCompletions,
            BodyJson = """
                {
                  "model": "test:gpt-4",
                  "seed": 9,
                  "max_tokens": 10,
                  "max_completion_tokens": 20,
                  "reasoning_effort": "low",
                  "messages": [
                    { "role": "system", "content": "be brief" },
                    { "role": "user", "content": [ { "type": "image_url", "image_url": { "url": "https://img.example/a.png" } } ] }
                  ],
                  "tools": [ { "type": "function", "function": { "name": "lookup", "description": "d", "parameters": { "type": "object" } } } ]
                }
                """
        });

        capture.Uri!.AbsoluteUri.Should().Be("https://upstream.test/v1/responses");
        var sent = JsonNode.Parse(capture.Body!)!.AsObject();
        sent["model"]!.GetValue<string>().Should().Be("gpt-4");
        sent["seed"].Should().BeNull();
        sent["max_output_tokens"]!.GetValue<int>().Should().Be(20);
        sent["reasoning"]!["effort"]!.GetValue<string>().Should().Be("low");
        sent["instructions"]!.GetValue<string>().Should().Be("be brief");
        sent["input"]![0]!["content"]![0]!["type"]!.GetValue<string>().Should().Be("input_image");
        sent["input"]![0]!["content"]![0]!["image_url"]!.GetValue<string>().Should().Be("https://img.example/a.png");
        sent["tools"]![0]!["name"]!.GetValue<string>().Should().Be("lookup");
        foreach (var dropped in ChatResponsesBridge.ChatToResponsesDropped)
            sent[dropped].Should().BeNull();

        using var reader = new StreamReader(result.Body!);
        var back = JsonNode.Parse(await reader.ReadToEndAsync())!.AsObject();
        back["object"]!.GetValue<string>().Should().Be("chat.completion");
        back["choices"]![0]!["message"]!["content"]!.GetValue<string>().Should().Be("ok");
        back["usage"]!["prompt_tokens"]!.GetValue<int>().Should().Be(3);
        await result.DisposeAsync();
    }

    [Fact]
    public async Task ChatToResponsesStream_ReusesStableItemId()
    {
        var sse = """
            data: {"choices":[{"delta":{"content":"A"}}]}

            data: {"choices":[{"delta":{"content":"B"}}]}

            data: [DONE]

            """;
        var (pipeline, _) = CreateChat(sse, "text/event-stream");
        var result = await pipeline.ExecuteAsync(new LlmProxyRequest
        {
            Protocol = LlmWireProtocol.Responses,
            BodyJson = """{"model":"test:gpt-4","stream":true,"input":"hi"}"""
        });

        using var reader = new StreamReader(result.Body!);
        var text = await reader.ReadToEndAsync();
        var ids = new List<string>();
        foreach (var line in text.Split('\n'))
        {
            if (!line.StartsWith("data: ", StringComparison.Ordinal) || line.Contains("[DONE]"))
                continue;
            var node = JsonNode.Parse(line["data: ".Length..]);
            if (node?["type"]?.GetValue<string>() == "response.output_text.delta")
                ids.Add(node["item_id"]!.GetValue<string>());
        }

        ids.Should().Equal(ids[0], ids[0]);
        text.Should().Contain("response.output_item.added");
        await result.DisposeAsync();
    }

    [Fact]
    public async Task ResponsesToChatStream_RequestsUsage_AndMapsThreeTokenFields()
    {
        var sse = """
            data: {"choices":[{"delta":{"content":"hi"}}]}

            data: {"id":"chatcmpl-1","object":"chat.completion.chunk","choices":[],"usage":{"prompt_tokens":18,"completion_tokens":3,"total_tokens":21}}

            data: [DONE]

            """;
        var (pipeline, capture) = CreateChat(sse, "text/event-stream");
        var result = await pipeline.ExecuteAsync(new LlmProxyRequest
        {
            Protocol = LlmWireProtocol.Responses,
            BodyJson = """{"model":"test:gpt-4","stream":true,"input":"hi"}"""
        });

        var sent = JsonNode.Parse(capture.Body!)!.AsObject();
        sent["stream"]!.GetValue<bool>().Should().BeTrue();
        sent["stream_options"]!["include_usage"]!.GetValue<bool>().Should().BeTrue();

        using var reader = new StreamReader(result.Body!);
        var text = await reader.ReadToEndAsync();
        JsonObject? completed = null;
        foreach (var line in text.Split('\n'))
        {
            if (!line.StartsWith("data: ", StringComparison.Ordinal) || line.Contains("[DONE]"))
                continue;
            if (JsonNode.Parse(line["data: ".Length..]) is not JsonObject node)
                continue;
            if (node["type"]?.GetValue<string>() == "response.completed")
                completed = node;
        }

        completed.Should().NotBeNull();
        var usage = completed!["response"]!["usage"]!.AsObject();
        usage["input_tokens"]!.GetValue<int>().Should().Be(18);
        usage["output_tokens"]!.GetValue<int>().Should().Be(3);
        usage["total_tokens"]!.GetValue<int>().Should().Be(21);
        await result.DisposeAsync();
    }

    [Fact]
    public async Task ResponsesNonStream_MessageHasIdStatusAndAnnotations()
    {
        var chat = """
            {"id":"chatcmpl-1","object":"chat.completion","model":"m","choices":[{"index":0,"message":{"role":"assistant","content":"Hi there","tool_calls":[{"id":"call_1","type":"function","function":{"name":"lookup","arguments":"{}"}}]},"finish_reason":"tool_calls"}],"usage":{"prompt_tokens":15,"completion_tokens":4,"total_tokens":19,"completion_tokens_details":{"reasoning_tokens":2}}}
            """;
        var (pipeline, _) = CreateChat(chat);
        var result = await pipeline.ExecuteAsync(new LlmProxyRequest
        {
            Protocol = LlmWireProtocol.Responses,
            BodyJson = """{"model":"test:gpt-4","input":"hi"}"""
        });

        using var reader = new StreamReader(result.Body!);
        var root = JsonNode.Parse(await reader.ReadToEndAsync())!.AsObject();
        root["created_at"]!.GetValue<long>().Should().BeGreaterThan(0);
        root["status"]!.GetValue<string>().Should().Be("completed");

        var message = root["output"]!.AsArray().First(n => n!["type"]!.GetValue<string>() == "message")!.AsObject();
        message["id"]!.GetValue<string>().Should().StartWith("msg_");
        message["status"]!.GetValue<string>().Should().Be("completed");
        message["content"]![0]!["text"]!.GetValue<string>().Should().Be("Hi there");
        message["content"]![0]!["annotations"]!.AsArray().Should().BeEmpty();

        var call = root["output"]!.AsArray().First(n => n!["type"]!.GetValue<string>() == "function_call")!.AsObject();
        call["id"]!.GetValue<string>().Should().Be("call_1");
        call["call_id"]!.GetValue<string>().Should().Be("call_1");

        var usage = root["usage"]!.AsObject();
        usage["input_tokens"]!.GetValue<int>().Should().Be(15);
        usage["output_tokens"]!.GetValue<int>().Should().Be(4);
        usage["total_tokens"]!.GetValue<int>().Should().Be(19);
        usage["output_tokens_details"]!["reasoning_tokens"]!.GetValue<int>().Should().Be(2);
        await result.DisposeAsync();
    }

    [Fact]
    public async Task AnthropicImage_BecomesChatDataUrl_WithoutSecondCall()
    {
        var (pipeline, capture) = CreateChat("""{"id":"c","choices":[{"message":{"role":"assistant","content":"seen"},"finish_reason":"stop"}],"usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}""");
        var result = await pipeline.ExecuteAsync(new LlmProxyRequest
        {
            Protocol = LlmWireProtocol.AnthropicMessages,
            BodyJson = """
                {
                  "model": "test:gpt-4",
                  "max_tokens": 16,
                  "system": "sys",
                  "messages": [
                    {
                      "role": "user",
                      "content": [
                        { "type": "image", "source": { "type": "base64", "media_type": "image/png", "data": "aaaa" } }
                      ]
                    }
                  ]
                }
                """
        });

        capture.Count.Should().Be(1);
        var sent = JsonNode.Parse(capture.Body!)!.AsObject();
        sent["model"]!.GetValue<string>().Should().Be("gpt-4");
        sent["messages"]![1]!["content"]![0]!["image_url"]!["url"]!.GetValue<string>()
            .Should().Be("data:image/png;base64,aaaa");
        using var reader = new StreamReader(result.Body!);
        var back = JsonNode.Parse(await reader.ReadToEndAsync())!.AsObject();
        back["type"]!.GetValue<string>().Should().Be("message");
        back["content"]![0]!["text"]!.GetValue<string>().Should().Be("seen");
        await result.DisposeAsync();
    }

    // ---- tool_choice 类级守卫（PC：tool_choice-without-tools）----
    // 合约：目标请求 tools 缺失或为空数组时，tool_choice 必须被丢弃；否则上游 400
    // "When using tool_choice, tools must be set. (parameter=tool_choice)"。

    private const string ResponsesOkJson =
        """{"id":"resp_1","object":"response","status":"completed","model":"gpt-4","output":[{"type":"message","role":"assistant","status":"completed","content":[{"type":"output_text","text":"ok"}]}],"usage":{"input_tokens":3,"output_tokens":4,"total_tokens":7}}""";

    [Fact]
    public async Task ResponsesInbound_ToolChoiceWithoutTools_NotForwarded()
    {
        var (pipeline, capture) = CreateResponses(ResponsesOkJson);
        var result = await pipeline.ExecuteAsync(new LlmProxyRequest
        {
            Protocol = LlmWireProtocol.Responses,
            BodyJson = """{"model":"test:gpt-4","tool_choice":"auto","input":"hi"}"""
        });

        capture.Count.Should().Be(1);
        var sent = JsonNode.Parse(capture.Body!)!.AsObject();
        sent["tool_choice"].Should().BeNull();
        await result.DisposeAsync();
    }

    [Fact]
    public async Task ResponsesInbound_EmptyTools_ToolChoiceNotForwarded()
    {
        var (pipeline, capture) = CreateResponses(ResponsesOkJson);
        var result = await pipeline.ExecuteAsync(new LlmProxyRequest
        {
            Protocol = LlmWireProtocol.Responses,
            BodyJson = """{"model":"test:gpt-4","tools":[],"tool_choice":"required","input":"hi"}"""
        });

        capture.Count.Should().Be(1);
        var sent = JsonNode.Parse(capture.Body!)!.AsObject();
        sent["tool_choice"].Should().BeNull();
        await result.DisposeAsync();
    }

    [Fact]
    public async Task ChatInbound_ToolChoiceWithoutTools_NotForwardedToResponses()
    {
        var (pipeline, capture) = CreateResponses(ResponsesOkJson);
        var result = await pipeline.ExecuteAsync(new LlmProxyRequest
        {
            Protocol = LlmWireProtocol.ChatCompletions,
            BodyJson = """{"model":"test:gpt-4","tool_choice":"required","messages":[{"role":"user","content":"hi"}]}"""
        });

        capture.Count.Should().Be(1);
        var sent = JsonNode.Parse(capture.Body!)!.AsObject();
        sent["tool_choice"].Should().BeNull();
        await result.DisposeAsync();
    }

    [Fact]
    public async Task ResponsesInbound_WithTools_ToolChoicePreserved()
    {
        var (pipeline, capture) = CreateResponses(ResponsesOkJson);
        var result = await pipeline.ExecuteAsync(new LlmProxyRequest
        {
            Protocol = LlmWireProtocol.Responses,
            BodyJson = """
                {"model":"test:gpt-4","tools":[{"type":"function","name":"lookup","description":"d","parameters":{"type":"object","properties":{}}}],"tool_choice":"required","input":"hi"}
                """
        });

        capture.Count.Should().Be(1);
        var sent = JsonNode.Parse(capture.Body!)!.AsObject();
        sent["tool_choice"]!.GetValue<string>().Should().Be("required");
        sent["tools"]!.AsArray().Count.Should().Be(1);
        await result.DisposeAsync();
    }

    private const string ChatSseOk =
        "data: {\"id\":\"c1\",\"object\":\"chat.completion.chunk\",\"created\":1,\"model\":\"gpt-4\",\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\",\"content\":\"ok\"},\"finish_reason\":null}]}\n\ndata: [DONE]\n\n";

    [Fact]
    public async Task ChatClient_ToolModeRequiredWithoutTools_OmitsToolChoice()
    {
        var (pipeline, capture) = CreateChat(ChatSseOk, "text/event-stream");
        var client = new LlmProxyChatClient(pipeline, "test:gpt-4");

        await foreach (var _ in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, [new TextContent("hi")])],
            new ChatOptions { ToolMode = ChatToolMode.RequireAny }))
        {
        }

        capture.Count.Should().Be(1);
        var sent = JsonNode.Parse(capture.Body!)!.AsObject();
        sent["tool_choice"].Should().BeNull();
    }

    [Fact]
    public async Task ChatClient_ToolModeRequiredWithTools_KeepsRequired()
    {
        var (pipeline, capture) = CreateChat(ChatSseOk, "text/event-stream");
        var client = new LlmProxyChatClient(pipeline, "test:gpt-4");

        await foreach (var _ in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, [new TextContent("hi")])],
            new ChatOptions
            {
                ToolMode = ChatToolMode.RequireAny,
                Tools = [AIFunctionFactory.Create(() => "ok", "echo_tool", "回显工具")]
            }))
        {
        }

        capture.Count.Should().Be(1);
        var sent = JsonNode.Parse(capture.Body!)!.AsObject();
        sent["tool_choice"]!.GetValue<string>().Should().Be("required");
        sent["tools"]!.AsArray().Count.Should().Be(1);
    }

    [Fact]
    public async Task MockProvider_AnswersFromRulesWithoutHttp()
    {
        var config = new AIProviderConfig
        {
            Name = "mock",
            ProviderType = AIProviderType.Mock,
            Endpoint = "mock://local",
            SupportedModels = ["mock"],
            IsDefault = true
        };
        var provider = new MockLlmProvider(config, new MockLlmDocument
        {
            Found = true,
            Rules = [new MockLlmRule("ping", "pong")],
            Fallback = "未命中"
        });
        var registry = new AIProviderRegistry();
        registry.RegisterProvider(provider);
        var client = new LlmProxyChatClient(new LlmProxyPipeline(registry), "mock:mock");

        var text = new StringBuilder();
        await foreach (var update in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, [new TextContent("请 ping 一下")])]))
        {
            text.Append(update.Text);
        }

        text.ToString().Should().Be("pong");
    }

    [Fact]
    public async Task MockProvider_FirstTurnEmitsToolCall_FollowUpReturnsText()
    {
        var root = Path.Combine(Path.GetTempPath(), "mock-cwd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var previous = MockLlmBindings.ProjectRoot;
        MockLlmBindings.ProjectRoot = () => root;
        try
        {
            var config = new AIProviderConfig
            {
                Name = "mock",
                ProviderType = AIProviderType.Mock,
                Endpoint = "mock://local",
                SupportedModels = ["mock"],
                IsDefault = true
            };
            var provider = new MockLlmProvider(config, new MockLlmDocument
            {
                Found = true,
                Fallback = "未命中",
                Rules =
                [
                    new MockLlmRule(
                        "列出当前项目根目录",
                        "列完了。",
                        [new MockLlmToolCall("list_files", """{"cwd":"{{cwd}}"}""")])
                ]
            });
            var registry = new AIProviderRegistry();
            registry.RegisterProvider(provider);
            var pipeline = new LlmProxyPipeline(registry);

            var first = await pipeline.ExecuteAsync(new LlmProxyRequest
            {
                Protocol = LlmWireProtocol.ChatCompletions,
                BodyJson = """{"model":"mock:mock","stream":true,"messages":[{"role":"user","content":"请调用 list_files 工具列出当前项目根目录"}]}"""
            });
            var firstText = await new StreamReader(first.Body!).ReadToEndAsync();
            firstText.Should().Contain("list_files");
            firstText.Should().Contain("tool_calls");
            firstText.Should().Contain("mock-cwd-");
            SseText(firstText).Should().NotContain("列完了");

            var second = await pipeline.ExecuteAsync(new LlmProxyRequest
            {
                Protocol = LlmWireProtocol.ChatCompletions,
                BodyJson = """{"model":"mock:mock","stream":true,"messages":[{"role":"user","content":"请调用 list_files 工具列出当前项目根目录"},{"role":"tool","content":"README.md"}]}"""
            });
            var secondText = await new StreamReader(second.Body!).ReadToEndAsync();
            SseText(secondText).Should().Contain("列完了");
            secondText.Should().NotContain("list_files");
        }
        finally
        {
            MockLlmBindings.ProjectRoot = previous;
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task MockProvider_StepsWalkTextToolTextTool()
    {
        var provider = new MockLlmProvider(
            new AIProviderConfig { Name = "mock", ProviderType = AIProviderType.Mock, IsDefault = true },
            new MockLlmDocument
            {
                Found = true,
                Fallback = "未命中",
                Rules =
                [
                    new MockLlmRule(
                        "多轮",
                        "结束",
                        Steps:
                        [
                            new MockLlmStep("先看目录。", [new MockLlmToolCall("list_files", "{}")]),
                            new MockLlmStep("目录列出来了。", [new MockLlmToolCall("read_file", "{}")]),
                            new MockLlmStep("两轮都完成了。", [new MockLlmToolCall("task_complete", """{"summary":"完成"}""")])
                        ])
                ]
            });

        MockLlmTurn At(params string[] roles)
        {
            return provider.Describe(new UnifiedChatRequest
            {
                Messages = roles.Select(role => new UnifiedChatMessage { Role = role, Content = "多轮" }).ToList()
            });
        }

        var first = At("user");
        first.Content.Should().Be("先看目录。");
        first.ToolCalls.Select(call => call.Name).Should().Equal("list_files");

        var second = At("user", "assistant", "tool");
        second.Content.Should().Be("目录列出来了。");
        second.ToolCalls.Select(call => call.Name).Should().Equal("read_file");

        var split = At("user", "assistant", "assistant", "tool");
        split.Content.Should().Be("目录列出来了。");
        split.ToolCalls.Select(call => call.Name).Should().Equal("read_file");

        var third = At("user", "assistant", "tool", "assistant", "tool");
        third.Content.Should().Be("两轮都完成了。");
        third.ToolCalls.Select(call => call.Name).Should().Equal("task_complete");

        var registry = new AIProviderRegistry();
        registry.RegisterProvider(provider);
        var pipeline = new LlmProxyPipeline(registry);
        var streamed = await pipeline.ExecuteAsync(new LlmProxyRequest
        {
            Protocol = LlmWireProtocol.ChatCompletions,
            BodyJson = """{"model":"mock:mock","stream":true,"messages":[{"role":"user","content":"多轮"}]}"""
        });
        var sse = await new StreamReader(streamed.Body!).ReadToEndAsync();
        sse.Should().Contain("list_files");
        SseText(sse).Should().Contain("先看目录");
    }

    private static string SseText(string sse)
    {
        var sb = new StringBuilder();
        foreach (var line in sse.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("data:", StringComparison.Ordinal)) continue;
            var payload = trimmed["data:".Length..].Trim();
            if (payload == "[DONE]") continue;
            var content = JsonNode.Parse(payload)?["choices"]?[0]?["delta"]?["content"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(content)) sb.Append(content);
        }
        return sb.ToString();
    }

    private static (LlmProxyPipeline Pipeline, CaptureHandler Capture) CreateChat(
        string response = """{"id":"ok","choices":[{"message":{"role":"assistant","content":"hi"}}]}""",
        string media = "application/json",
        HttpStatusCode status = HttpStatusCode.OK)
        => Create(AIProviderType.OpenAI, response, media, status);

    private static (LlmProxyPipeline Pipeline, CaptureHandler Capture) CreateResponses(
        string response,
        string media = "application/json",
        HttpStatusCode status = HttpStatusCode.OK)
        => Create(AIProviderType.OpenAIResponses, response, media, status);

    private static (LlmProxyPipeline Pipeline, CaptureHandler Capture) Create(
        AIProviderType type, string response, string media, HttpStatusCode status)
    {
        var capture = new CaptureHandler(response, media, status);
        var http = new HttpClient(capture);
        var config = new AIProviderConfig
        {
            Name = "test",
            ProviderType = type,
            Endpoint = "https://upstream.test/v1",
            ApiKey = "sk-test",
            SupportedModels = ["gpt-4"],
            IsDefault = true,
            TimeoutSeconds = 30
        };
        IAIProvider provider = type == AIProviderType.OpenAIResponses
            ? new OpenAIResponsesProvider(config, http)
            : new OpenAICompatibleProvider(config, http);
        var registry = new AIProviderRegistry();
        registry.RegisterProvider(provider);
        return (new LlmProxyPipeline(registry), capture);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        private readonly string _response;
        private readonly string _media;
        private readonly HttpStatusCode _status;

        public CaptureHandler(string response, string media, HttpStatusCode status)
        {
            _response = response;
            _media = media;
            _status = status;
        }

        public int Count { get; private set; }
        public string? Body { get; private set; }
        public Uri? Uri { get; private set; }
        public string? Auth { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Count++;
            Uri = request.RequestUri;
            Auth = request.Headers.Authorization?.ToString();
            Body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            var message = new HttpResponseMessage(_status)
            {
                Content = new StringContent(_response, Encoding.UTF8, _media)
            };
            return message;
        }
    }
}
