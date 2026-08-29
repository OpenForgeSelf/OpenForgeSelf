using System.Collections.Generic;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeSelf.Api.Tests.Integration;

public class WorkflowPlanningIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public WorkflowPlanningIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                var aiDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IAIService));
                if (aiDescriptor != null)
                {
                    services.Remove(aiDescriptor);
                }
                services.AddSingleton<IAIService>(new MockWorkflowAIService());
            });
        });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task PlanWorkflow_ShouldReturnWorkflowDefinition()
    {
        var request = new
        {
            UserRequest = "帮我读取一个文件然后统计行数",
            AvailableTools = (List<string>?)null
        };

        var response = await _client.PostAsJsonAsync("/api/ai-agent/workflow/plan", request);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("success").GetBoolean().Should().BeTrue();
        content.GetProperty("workflow").GetProperty("steps").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PlanWorkflow_EmptyRequest_ShouldReturnBadRequest()
    {
        var request = new
        {
            UserRequest = "",
            AvailableTools = (List<string>?)null
        };

        var response = await _client.PostAsJsonAsync("/api/ai-agent/workflow/plan", request);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PlanWorkflow_ShouldHaveValidStepStructure()
    {
        var request = new
        {
            UserRequest = "创建一个两步骤的工作流",
            AvailableTools = (List<string>?)null
        };

        var response = await _client.PostAsJsonAsync("/api/ai-agent/workflow/plan", request);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        var workflow = content.GetProperty("workflow");
        workflow.GetProperty("name").GetString().Should().NotBeNullOrEmpty();
        workflow.GetProperty("steps").GetArrayLength().Should().BeGreaterThan(0);

        var firstStep = workflow.GetProperty("steps")[0];
        firstStep.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        firstStep.GetProperty("name").GetString().Should().NotBeNullOrEmpty();
        firstStep.GetProperty("type").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetPlanPrompt_ShouldReturnPrompt()
    {
        var request = new
        {
            UserRequest = "测试工作流规划",
            AvailableTools = (List<string>?)null
        };

        var response = await _client.PostAsJsonAsync("/api/ai-agent/workflow/plan/prompt", request);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("success").GetBoolean().Should().BeTrue();
        content.GetProperty("prompt").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task MatchTools_ShouldReturnRankedTools()
    {
        var response = await _client.GetAsync("/api/ai-agent/workflow/tools/match?taskDescription=文件操作");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("success").GetBoolean().Should().BeTrue();
        content.GetProperty("tools").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task MatchTools_EmptyDescription_ShouldReturnBadRequest()
    {
        var response = await _client.GetAsync("/api/ai-agent/workflow/tools/match?taskDescription=");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetExecutionStatus_NonExistentId_ShouldReturnNotFound()
    {
        var response = await _client.GetAsync("/api/ai-agent/workflow/99999/status");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }
}

internal class MockWorkflowAIService : IAIService
{
    public async Task<string> ChatAsync(List<AIChatMessage> messages)
    {
        await Task.Delay(10);

        var lastMessage = messages.LastOrDefault(m => m.Role == "user");
        if (lastMessage?.Content != null && lastMessage.Content.Contains("工作流") && lastMessage.Content.Contains("JSON"))
        {
            return @"
{
  ""name"": ""文件读取与统计工作流"",
  ""description"": ""读取文件并统计行数"",
  ""category"": ""文件操作"",
  ""startStepId"": ""step_1"",
  ""steps"": [
    {
      ""id"": ""step_1"",
      ""name"": ""读取文件"",
      ""description"": ""读取指定路径的文件内容"",
      ""type"": ""Tool"",
      ""toolName"": ""read_file"",
      ""parametersJson"": ""{'filePath':'test.txt'}"",
      ""nextStepId"": ""step_2"",
      ""errorHandling"": {
        ""maxRetries"": 2,
        ""retryDelayMs"": 1000,
        ""continueOnError"": false
      }
    },
    {
      ""id"": ""step_2"",
      ""name"": ""统计行数"",
      ""description"": ""统计文件内容的行数"",
      ""type"": ""Tool"",
      ""toolName"": ""count_lines"",
      ""parametersJson"": ""{'content':'${step_1.result}'}"",
      ""nextStepId"": null,
      ""errorHandling"": {
        ""maxRetries"": 1,
        ""retryDelayMs"": 500,
        ""continueOnError"": false
      }
    }
  ],
  ""variables"": [
    {
      ""name"": ""filePath"",
      ""type"": ""string"",
      ""defaultValue"": ""test.txt"",
      ""description"": ""文件路径""
    }
  ]
}";
        }

        return "模拟响应";
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(List<AIChatMessage> messages, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken);
        yield return "模拟";
        await Task.Delay(10, cancellationToken);
        yield return "响应";
    }
}
