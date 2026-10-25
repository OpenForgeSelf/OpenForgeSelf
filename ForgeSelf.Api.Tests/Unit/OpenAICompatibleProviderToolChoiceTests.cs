using System.Net;
using System.Text;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Services.AI.Providers;
using Xunit;

namespace ForgeSelf.Api.Tests.Unit;

public class OpenAICompatibleProviderToolChoiceTests
{
    [Fact]
    public async Task ChatStream_ForwardsRequiredToolChoice()
    {
        var handler = new CapturingHandler();
        var provider = new OpenAICompatibleProvider(new AIProviderConfig
        {
            Name = "测试提供方",
            ProviderType = AIProviderType.OpenAI,
            Endpoint = "https://example.test/v1",
            ApiKey = "test-key",
            TimeoutSeconds = 5
        }, new HttpClient(handler));
        using var parameters = JsonDocument.Parse("""{"type":"object","properties":{}}""");
        var request = new UnifiedChatRequest
        {
            Model = "tool-model",
            Stream = true,
            ToolChoice = "required",
            Messages = new List<UnifiedChatMessage> { new() { Role = "user", Content = "完成当前步骤" } },
            Tools = new List<UnifiedToolDefinition>
            {
                new() { Name = "complete_step", Description = "提交步骤产出", Parameters = parameters.RootElement.Clone() }
            }
        };

        await foreach (var _ in provider.ChatStreamAsync(request)) { }

        using var body = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("required", body.RootElement.GetProperty("tool_choice").GetString());
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("data: [DONE]\n\n", Encoding.UTF8, "text/event-stream")
            };
        }
    }
}
