using System.ClientModel;
using System.ClientModel.Primitives;
using System.Reflection;
using System.Text;
using FluentAssertions;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using Xunit;

// Responses API 在 openai-dotnet 2.10.0 仍标 OPENAI001 实验性（U-5 事实）；按需在适配器同样处理
#pragma warning disable OPENAI001

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// PILOT-074 步骤1：openai-dotnet 2.10.0 wire 实证探针（02-spec Unknown U-1/U-2/U-3/U-5）。
/// 用 FakeHandler 捕获 SDK 实际出站请求/喂入预设响应，把「SDK 到底发什么、能拿到什么」钉成可回归事实。
/// 结论回填 03-plan 偏差表；本文件随后演化为 AC-1/AC-4 断言的基座。
/// </summary>
public class OpenAIWireProbeTests
{
    private const string ChatCompletionJson = """
        {"id":"chatcmpl-1","object":"chat.completion","created":1,"model":"m",
         "choices":[{"index":0,"message":{"role":"assistant","content":"ok"},"finish_reason":"stop"}],
         "usage":{"prompt_tokens":3,"completion_tokens":2,"total_tokens":5}}
        """;

    private static OpenAIClient CreateClient(HttpMessageHandler handler)
        => new(new ApiKeyCredential("test-key"), new OpenAIClientOptions
        {
            Endpoint = new Uri("http://localhost:5001/v1"),
            Transport = new HttpClientPipelineTransport(new HttpClient(handler)),
        });

    private static HttpResponseMessage JsonResponse(string json)
        => new(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage SseResponse(string sse)
        => new(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(sse, Encoding.UTF8, "text/event-stream")
        };

    /// <summary>U-1：chat 非流式请求的输出上限字段名（max_tokens vs max_completion_tokens）与基础 wire 形状。</summary>
    [Fact]
    public async Task Probe_U1_ChatComplete_CapturesWireBody()
    {
        string? body = null;
        string? path = null;
        var handler = new FakeHandler(req =>
        {
            path = req.RequestUri!.PathAndQuery;
            body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonResponse(ChatCompletionJson);
        });

        var chat = CreateClient(handler).GetChatClient("m");
        var options = new ChatCompletionOptions();
        options.MaxOutputTokenCount = 128;
        options.Temperature = 0.7f;
        options.TopP = 0.9f;
        var messages = new List<ChatMessage> { ChatMessage.CreateUserMessage("hi") };

        var completion = (await chat.CompleteChatAsync(messages, options)).Value;

        completion.Content[0].Text.Should().Be("ok");
        body.Should().NotBeNull();
        path.Should().Be("/v1/chat/completions");
        // 事实钉板：实施后按实测结果二选一收紧
        body!.Should().MatchRegex("max_(completion_tokens|tokens)");
    }

    /// <summary>U-3：流式请求是否自动携带 include_usage；usage 哨兵分片能否经 ChatCompletionUpdate.Usage 取回。</summary>
    [Fact]
    public async Task Probe_U3_ChatStreaming_UsageBehavior()
    {
        string? body = null;
        const string sse = """
            data: {"id":"c1","object":"chat.completion.chunk","created":1,"model":"m","choices":[{"index":0,"delta":{"role":"assistant","content":"he"},"finish_reason":null}]}

            data: {"id":"c1","object":"chat.completion.chunk","created":1,"model":"m","choices":[{"index":0,"delta":{"content":"llo"},"finish_reason":null}]}

            data: {"id":"c1","object":"chat.completion.chunk","created":1,"model":"m","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}

            data: {"id":"c1","object":"chat.completion.chunk","created":1,"model":"m","choices":[],"usage":{"prompt_tokens":3,"completion_tokens":2,"total_tokens":5}}

            data: [DONE]

            """;
        var handler = new FakeHandler(req =>
        {
            body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return SseResponse(sse);
        });

        var chat = CreateClient(handler).GetChatClient("m");
        var options = new ChatCompletionOptions();
        var messages = new List<ChatMessage> { ChatMessage.CreateUserMessage("hi") };

        var sawUsage = false;
        var text = new StringBuilder();
        await foreach (var upd in chat.CompleteChatStreamingAsync(messages, options))
        {
            foreach (var part in upd.ContentUpdate)
            {
                text.Append(part.Text);
            }

            if (upd.Usage is { } usage)
            {
                sawUsage = true;
                usage.TotalTokenCount.Should().Be(5);
            }
        }

        text.ToString().Should().Be("hello");
        sawUsage.Should().BeTrue();
        // 事实钉板：SDK 是否自动置 include_usage（true=自动；false=需显式设置，结论回填偏差表）
        body!.Contains("include_usage").Should().BeTrue(
            $"SDK 流式请求体未自动包含 include_usage；body={body}");
    }

    /// <summary>U-2：上游 deepseek 系扩展字段 reasoning_content 在 SDK 响应侧是否可达。</summary>
    [Fact]
    public async Task Probe_U2_ChatCompletion_ReasoningContentReachability()
    {
        const string Json = """
            {"id":"chatcmpl-2","object":"chat.completion","created":1,"model":"m",
             "choices":[{"index":0,"message":{"role":"assistant","content":"","reasoning_content":"深度思考中"},"finish_reason":"stop"}],
             "usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}
            """;
        var handler = new FakeHandler(_ => JsonResponse(Json));
        var completion = (await CreateClient(handler).GetChatClient("m")
            .CompleteChatAsync(new List<ChatMessage> { ChatMessage.CreateUserMessage("hi") }, new ChatCompletionOptions())).Value;

        completion.Content[0].Text.Should().BeEmpty();

        // 路径A 已排除：ChatCompletion 无 AdditionalBinaryDataProperties 实例属性（CS1061 实证）
        // 路径B：ModelReaderWriter 往返后再解析 JSON
        var roundtrip = ModelReaderWriter.Write(completion).ToString();
        var viaRoundtrip = roundtrip != null && roundtrip.Contains("深度思考中");

        // 事实钉板：roundtrip 可达则 FR-8 迁移门放行（AC-7），否则 AIService 回退保留手写
        var roundtripHead = roundtrip == null ? "null" : roundtrip.Substring(0, Math.Min(400, roundtrip.Length));
        viaRoundtrip.Should().BeTrue($"reasoning_content 不可达；roundtripHead={roundtripHead}");
    }

    /// <summary>U-5：Responses API 端到端最小回路——wire 形状（instructions/input/扁平 tools/store:false/arguments 字符串）与响应映射。</summary>
    [Fact]
    public async Task Probe_U5_ResponsesCreate_WireAndResult()
    {
        string? body = null;
        string? path = null;
        const string Json = """
            {"id":"resp_1","object":"response","created_at":1,"status":"completed","model":"m",
             "output":[
               {"type":"message","id":"msg_1","role":"assistant","status":"completed",
                "content":[{"type":"output_text","text":"hello","annotations":[]}]},
               {"type":"function_call","id":"fc_1","call_id":"call_1","name":"get_weather","arguments":"{\"city\":\"Paris\"}","status":"completed"}],
             "usage":{"input_tokens":3,"output_tokens":2,"total_tokens":5}}
            """;
        var handler = new FakeHandler(req =>
        {
            path = req.RequestUri!.PathAndQuery;
            body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonResponse(Json);
        });

        var responses = CreateClient(handler).GetResponsesClient();
        var options = new CreateResponseOptions
        {
            Instructions = "you are helpful",
            InputItems =
            {
                ResponseItem.CreateUserMessageItem("hi"),
                ResponseItem.CreateFunctionCallItem("call_0", "f0", BinaryData.FromString("{}")),
                ResponseItem.CreateFunctionCallOutputItem("call_0", "done"),
            },
            MaxOutputTokenCount = 128,
            StoredOutputEnabled = false,
        };
        options.Tools.Add(ResponseTool.CreateFunctionTool(
            "get_weather", BinaryData.FromString("{\"type\":\"object\",\"properties\":{}}"), strictModeEnabled: false, functionDescription: "查天气"));

        var result = (await responses.CreateResponseAsync(options)).Value;

        result.OutputItems.Should().HaveCount(2);
        path.Should().Be("/v1/responses");
        body.Should().NotBeNull();
        // wire 事实钉板
        body!.Should().Contain("\"instructions\":\"you are helpful\"");
        body!.Should().Contain("\"type\":\"function_call_output\"");
        body.Should().Contain("\"store\":false");
        // Responses 规范：function_call.arguments 为 JSON 字符串（非嵌套对象）
        body.Should().Contain("\"arguments\":\"{}\"");
        // 工具为扁平结构（无 chat 风格的 function 包裹层）
        body.Should().Contain("\"type\":\"function\",\"name\":\"get_weather\"");
    }

    /// <summary>U-5b：Responses 流式类型化事件（output_text.delta / completed+usage）经 SDK 可达。</summary>
    [Fact]
    public async Task Probe_U5b_ResponsesStreaming_TypedEvents()
    {
        const string Sse = """
            event: response.created
            data: {"type":"response.created","sequence_number":0,"response":{"id":"resp_1","object":"response","created_at":1,"status":"in_progress","model":"m","output":[]}}

            event: response.output_text.delta
            data: {"type":"response.output_text.delta","sequence_number":1,"item_id":"msg_1","output_index":0,"content_index":0,"delta":"he"}

            event: response.output_text.delta
            data: {"type":"response.output_text.delta","sequence_number":2,"item_id":"msg_1","output_index":0,"content_index":0,"delta":"llo"}

            event: response.completed
            data: {"type":"response.completed","sequence_number":3,"response":{"id":"resp_1","object":"response","created_at":1,"status":"completed","model":"m","output":[{"type":"message","id":"msg_1","role":"assistant","status":"completed","content":[{"type":"output_text","text":"hello","annotations":[]}]}],"usage":{"input_tokens":3,"output_tokens":2,"total_tokens":5}}}

            event: response.output_item.added
            data: {"type":"response.output_item.added","sequence_number":4,"output_index":0,"item":{"type":"message","id":"msg_1","role":"assistant","status":"in_progress","content":[]}}

            """;
        var handler = new FakeHandler(_ => SseResponse(Sse));

        var responses = CreateClient(handler).GetResponsesClient();
        // SDK 事实：流式必须显式 StreamingEnabled=true（否则 InvalidOperationException）
        var options = new CreateResponseOptions { StreamingEnabled = true, InputItems = { ResponseItem.CreateUserMessageItem("hi") } };

        var text = new StringBuilder();
        ResponseResult? final = null;
        var seenTypes = new List<string>();
        await foreach (var upd in responses.CreateResponseStreamingAsync(options))
        {
            seenTypes.Add(upd.GetType().Name);
            if (upd is StreamingResponseOutputTextDeltaUpdate delta)
            {
                text.Append(delta.Delta);
            }

            if (upd is StreamingResponseCompletedUpdate done)
            {
                final = done.Response;
            }
        }

        text.ToString().Should().Be("hello");
        final.Should().NotBeNull($"收到的事件类型序列: [{string.Join(", ", seenTypes)}]");
        final!.Usage!.TotalTokenCount.Should().Be(5);
    }
}
