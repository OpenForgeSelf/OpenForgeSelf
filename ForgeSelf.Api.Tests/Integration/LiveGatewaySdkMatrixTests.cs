using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;

#pragma warning disable OPENAI001

namespace ForgeSelf.Api.Tests.Integration;

/// <summary>
/// 打正在运行的本地宿主。没有 FORGE_LIVE_URL 时直接返回，避免 CI 打外网。
/// 本机：FORGE_LIVE_URL、FORGE_LIVE_TOKEN、FORGE_LIVE_MODEL。
/// </summary>
public class LiveGatewaySdkMatrixTests
{
    private const string Prompt = "Reply with the single word pong and nothing else.";

    [Fact]
    public async Task SdkMatrix_ChatAndResponses_StreamAndPlain_AllReturnText()
    {
        var baseUrl = Environment.GetEnvironmentVariable("FORGE_LIVE_URL");
        var token = Environment.GetEnvironmentVariable("FORGE_LIVE_TOKEN");
        var model = Environment.GetEnvironmentVariable("FORGE_LIVE_MODEL");
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(model))
            return;

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(180) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        http.DefaultRequestHeaders.TryAddWithoutValidation("X-Probe", new string('z', 1500));
        var transport = new HttpClientPipelineTransport(http);
        var client = new OpenAIClient(new ApiKeyCredential(token), new OpenAIClientOptions
        {
            Endpoint = new Uri(baseUrl.TrimEnd('/') + "/"),
            Transport = transport,
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
        });

        var failures = new List<string>();
        await Run(failures, "chat-plain", () => ChatPlain(client, model));
        await Run(failures, "chat-stream", () => ChatStream(client, model));
        await Run(failures, "responses-plain", () => ResponsesPlain(client, model));
        await Run(failures, "responses-stream", () => ResponsesStream(client, model));
        await Run(failures, "anthropic-plain", () => Anthropic(http, baseUrl, model, stream: false));
        await Run(failures, "anthropic-stream", () => Anthropic(http, baseUrl, model, stream: true));

        failures.Should().BeEmpty(string.Join(Environment.NewLine, failures));
    }

    private static async Task Run(List<string> failures, string name, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            failures.Add(name + ": " + ex.GetType().Name + " " + Trim(ex.Message));
        }
    }

    private static async Task ChatPlain(OpenAIClient client, string model)
    {
        var options = new ChatCompletionOptions { MaxOutputTokenCount = 1024 };
        var result = (await client.GetChatClient(model).CompleteChatAsync(
            [ChatMessage.CreateUserMessage(Prompt)], options)).Value;
        var text = string.Concat(result.Content.Select(p => p.Text));
        text.Should().NotBeNullOrWhiteSpace();
        result.Usage.TotalTokenCount.Should().BeGreaterThan(0);
    }

    private static async Task ChatStream(OpenAIClient client, string model)
    {
        var options = new ChatCompletionOptions { MaxOutputTokenCount = 1024 };
        var text = new StringBuilder();
        var sawUsage = false;
        await foreach (var upd in client.GetChatClient(model).CompleteChatStreamingAsync(
            [ChatMessage.CreateUserMessage(Prompt)], options))
        {
            foreach (var part in upd.ContentUpdate)
                text.Append(part.Text);
            if (upd.Usage is { TotalTokenCount: > 0 })
                sawUsage = true;
        }

        text.ToString().Should().NotBeNullOrWhiteSpace();
        sawUsage.Should().BeTrue();
    }

    private static async Task ResponsesPlain(OpenAIClient client, string model)
    {
        var options = new CreateResponseOptions
        {
            Model = model,
            MaxOutputTokenCount = 1024,
            StoredOutputEnabled = false,
            InputItems = { ResponseItem.CreateUserMessageItem(Prompt) },
        };
        var result = (await client.GetResponsesClient().CreateResponseAsync(options)).Value;
        TextOf(result).Should().NotBeNullOrWhiteSpace();
        result.Usage!.TotalTokenCount.Should().BeGreaterThan(0);
        result.OutputItems.OfType<MessageResponseItem>().Should().Contain(m => !string.IsNullOrEmpty(m.Id));
    }

    private static async Task ResponsesStream(OpenAIClient client, string model)
    {
        var options = new CreateResponseOptions
        {
            Model = model,
            MaxOutputTokenCount = 1024,
            StoredOutputEnabled = false,
            StreamingEnabled = true,
            InputItems = { ResponseItem.CreateUserMessageItem(Prompt) },
        };
        var text = new StringBuilder();
        ResponseResult? final = null;
        await foreach (var upd in client.GetResponsesClient().CreateResponseStreamingAsync(options))
        {
            if (upd is StreamingResponseOutputTextDeltaUpdate delta)
                text.Append(delta.Delta);
            if (upd is StreamingResponseCompletedUpdate done)
                final = done.Response;
        }

        text.ToString().Should().NotBeNullOrWhiteSpace();
        final.Should().NotBeNull();
        final!.Usage!.TotalTokenCount.Should().BeGreaterThan(0);
        final.OutputItems.OfType<MessageResponseItem>().Should().Contain(m => !string.IsNullOrEmpty(m.Id));
    }

    private static async Task Anthropic(HttpClient http, string baseUrl, string model, bool stream)
    {
        var body = new JsonObject
        {
            ["model"] = model,
            ["max_tokens"] = 1024,
            ["stream"] = stream,
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "user", ["content"] = Prompt }
            }
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, baseUrl.TrimEnd('/') + "/anthropic/messages")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseContentRead);
        var text = await resp.Content.ReadAsStringAsync();
        resp.IsSuccessStatusCode.Should().BeTrue(Trim(text));
        if (stream)
        {
            text.Should().Contain("text_delta");
            text.Should().Contain("message_stop");
        }
        else
        {
            var root = JsonNode.Parse(text)!.AsObject();
            root["type"]!.GetValue<string>().Should().Be("message");
            root["content"]![0]!["text"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
            root["usage"]!["input_tokens"]!.GetValue<int>().Should().BeGreaterThan(0);
            root["usage"]!["output_tokens"]!.GetValue<int>().Should().BeGreaterThan(0);
        }
    }

    private static string TextOf(ResponseResult result)
    {
        var sb = new StringBuilder();
        foreach (var item in result.OutputItems.OfType<MessageResponseItem>())
        {
            foreach (var part in item.Content)
                sb.Append(part.Text);
        }
        return sb.ToString();
    }

    private static string Trim(string text)
    {
        text = text.ReplaceLineEndings(" ");
        return text.Length <= 400 ? text : text[..400];
    }
}
