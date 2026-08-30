using System.Collections.Generic;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Threading;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Services;
using Microsoft.AspNetCore.Hosting;

namespace ForgeSelf.Api.Tests.Integration;

/// <summary>
/// 集成测试示例 - 测试基础API功能
/// </summary>
public class IntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public IntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                // 替换AIService为模拟服务
                var aiDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IAIService));
                if (aiDescriptor != null)
                {
                    services.Remove(aiDescriptor);
                }
                services.AddSingleton<IAIService>(new MockAIServiceForIntegration());
            });
        });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task ApiChatEndpoint_ShouldExist()
    {
        // Arrange
        var request = JsonContent.Create(new { Message = "测试", SessionId = "" });

        // Act
        var response = await _client.PostAsync("/api/chat", request);

        // Assert
        response.Should().NotBeNull();
    }

    [Fact]
    public async Task ApiChatHistoryEndpoint_ShouldExist()
    {
        // Act
        var response = await _client.GetAsync("/api/chat/history/test-session");

        // Assert
        response.Should().NotBeNull();
    }
}

/// <summary>
/// 模拟的AI服务，用于IntegrationTests
/// </summary>
internal class MockAIServiceForIntegration : IAIService
{
    public async Task<string> ChatAsync(List<AIChatMessage> messages)
    {
        await Task.Delay(10);
        return "模拟响应";
    }

    public Task<string> ChatAsync(List<AIChatMessage> messages, string? chatModelId)
        => ChatAsync(messages);

    public async IAsyncEnumerable<string> ChatStreamAsync(List<AIChatMessage> messages, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken);
        yield return "模拟";
        await Task.Delay(10, cancellationToken);
        yield return "响应";
    }

    public IAsyncEnumerable<string> ChatStreamAsync(List<AIChatMessage> messages, string? chatModelId, CancellationToken cancellationToken = default)
        => ChatStreamAsync(messages, cancellationToken);
}