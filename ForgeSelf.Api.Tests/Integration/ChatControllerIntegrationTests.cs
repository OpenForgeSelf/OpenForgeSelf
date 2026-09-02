using System.Net;
using System.Net.Http.Json;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Services;
using Microsoft.AspNetCore.Hosting;
using XCode;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Tests.Integration;

/// <summary>
/// ChatController集成测试
/// </summary>
[Collection("XCode")]
public class ChatControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private HttpClient _client;
    private readonly string _tempDbDir;

    public ChatControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _tempDbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfChat_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDbDir);
        var openForgeDb = Path.Combine(_tempDbDir, "ForgeSelf.db");

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // 替换AIService为模拟服务
                var aiDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IAIService));
                if (aiDescriptor != null)
                {
                    services.Remove(aiDescriptor);
                }

                services.AddSingleton<IAIService>(_ => new MockAIService());
            });

            builder.UseEnvironment("Testing");
        });

        _client = _factory.CreateClient();

        // 隔离 XCode 的 ForgeSelf 连接到临时库：
        // 先触发宿主构建（AddXCode 此时已按配置/物理库注册连接），再覆盖为临时库，
        // 避免聊天消息写入/读取落到持久库（历史数据污染、跨运行串扰）。
        var _ = _factory.Services;
        DAL.AddConnStr("ForgeSelf", $"Data Source={openForgeDb}", null, "SQLite");
        EntityFactory.InitConnection("ForgeSelf");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDbDir)) Directory.Delete(_tempDbDir, true);
        }
        catch
        {
            // 临时目录清理失败不影响测试
        }
    }

    /// <summary>
    /// 创建聊天请求
    /// </summary>
    private JsonContent CreateChatRequest(string message, string? sessionId = null)
    {
        return JsonContent.Create(new ChatRequest
        {
            Message = message,
            SessionId = sessionId ?? string.Empty
        });
    }

    [Fact]
    public async Task SendMessage_WithValidRequest_ShouldReturnSuccess()
    {
        // Arrange
        var request = CreateChatRequest("你好，请介绍一下你自己");

        // Act
        var response = await _client.PostAsync("/api/chat", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendMessage_WithValidRequest_ShouldReturnChatResponse()
    {
        // Arrange
        var request = CreateChatRequest("测试消息");

        // Act
        var response = await _client.PostAsync("/api/chat", request);
        var content = await response.Content.ReadAsStringAsync();
        var chatResponse = JsonSerializer.Deserialize<ChatResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        chatResponse.Should().NotBeNull();
        chatResponse!.SessionId.Should().NotBeEmpty();
        chatResponse.Role.Should().Be("assistant");
        chatResponse.Content.Should().NotBeEmpty();
        chatResponse.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SendMessage_WithEmptyMessage_ShouldReturnBadRequest()
    {
        // Arrange
        var request = CreateChatRequest("");

        // Act
        var response = await _client.PostAsync("/api/chat", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SendMessage_WithNullMessage_ShouldReturnBadRequest()
    {
        // Arrange
        var request = JsonContent.Create(new ChatRequest
        {
            Message = null!,
            SessionId = string.Empty
        });

        // Act
        var response = await _client.PostAsync("/api/chat", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SendMessage_WithExistingSessionId_ShouldUseSameSession()
    {
        // Arrange
        var sessionId = "test-session-123";
        var request1 = CreateChatRequest("第一条消息", sessionId);
        var request2 = CreateChatRequest("第二条消息", sessionId);

        // Act
        var response1 = await _client.PostAsync("/api/chat", request1);
        var response2 = await _client.PostAsync("/api/chat", request2);

        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();

        var chatResponse1 = JsonSerializer.Deserialize<ChatResponse>(content1, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        var chatResponse2 = JsonSerializer.Deserialize<ChatResponse>(content2, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        chatResponse1!.SessionId.Should().Be(sessionId);
        chatResponse2!.SessionId.Should().Be(sessionId);
    }

    [Fact]
    public async Task SendMessage_ShouldSaveMessagesToDatabase()
    {
        // Arrange
        var request = CreateChatRequest("数据库测试消息");

        // Act
        var response = await _client.PostAsync("/api/chat", request);
        var content = await response.Content.ReadAsStringAsync();
        var chatResponse = JsonSerializer.Deserialize<ChatResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert - 通过历史记录API验证消息已保存
        var historyResponse = await _client.GetAsync($"/api/chat/history/{chatResponse!.SessionId}");
        var historyContent = await historyResponse.Content.ReadAsStringAsync();
        var history = JsonSerializer.Deserialize<List<ChatResponse>>(historyContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        history.Should().NotBeNull();
        history!.Should().HaveCount(2); // 用户消息 + AI响应
        history.Should().Contain(m => m.Role == "user");
        history.Should().Contain(m => m.Role == "assistant");
    }

    [Fact]
    public async Task GetHistory_WithValidSessionId_ShouldReturnMessages()
    {
        // Arrange
        // 使用合法的会话ID（≤50字符，与生产中 Guid.NewGuid().ToString("N") 风格一致），
        // 避免超过 ChatMessage.SessionId 的字符长度限制导致插入失败（500）
        var sessionId = "history-" + Guid.NewGuid().ToString("N")[..16];

        // 先发送一些消息
        await _client.PostAsync("/api/chat", CreateChatRequest("消息1", sessionId));
        await _client.PostAsync("/api/chat", CreateChatRequest("消息2", sessionId));

        // Act
        var response = await _client.GetAsync($"/api/chat/history/{sessionId}");
        var content = await response.Content.ReadAsStringAsync();
        var history = JsonSerializer.Deserialize<List<ChatResponse>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        history.Should().NotBeNull();
        history!.Should().HaveCount(4); // 2用户消息 + 2AI响应
    }

    [Fact]
    public async Task GetHistory_WithEmptySessionId_ShouldReturnNotFound()
    {
        // Act - 空sessionId会导致路由不匹配
        var response = await _client.GetAsync("/api/chat/history/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetHistory_WithNonExistentSession_ShouldReturnEmptyList()
    {
        // Arrange
        var sessionId = "non-existent-session";

        // Act
        var response = await _client.GetAsync($"/api/chat/history/{sessionId}");
        var content = await response.Content.ReadAsStringAsync();
        var history = JsonSerializer.Deserialize<List<ChatResponse>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        history.Should().NotBeNull();
        history!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetHistory_WithLimit_ShouldReturnLimitedMessages()
    {
        // Arrange
        var sessionId = "limit-test-session";

        // 发送多条消息
        for (int i = 0; i < 5; i++)
        {
            await _client.PostAsync("/api/chat", CreateChatRequest($"消息{i}", sessionId));
        }

        // Act
        var response = await _client.GetAsync($"/api/chat/history/{sessionId}?limit=5");
        var content = await response.Content.ReadAsStringAsync();
        var history = JsonSerializer.Deserialize<List<ChatResponse>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        history.Should().NotBeNull();
        history!.Count.Should().BeLessOrEqualTo(5);
    }

    [Fact]
    public async Task DeleteSession_WithValidSessionId_ShouldDeleteMessages()
    {
        // Arrange
        var sessionId = "delete-test-session";
        await _client.PostAsync("/api/chat", CreateChatRequest("要删除的消息", sessionId));

        // Act
        var response = await _client.DeleteAsync($"/api/chat/session/{sessionId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // 经历史接口验证消息已被删除（避免依赖 EF 内存库等无关实现）
        var historyResponse = await _client.GetAsync($"/api/chat/history/{sessionId}");
        var historyContent = await historyResponse.Content.ReadAsStringAsync();
        var history = JsonSerializer.Deserialize<List<ChatResponse>>(historyContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        history.Should().NotBeNull();
        history!.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteSession_WithEmptySessionId_ShouldReturnNotFound()
    {
        // Act - 空sessionId会导致路由不匹配
        var response = await _client.DeleteAsync("/api/chat/session/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteSession_WithNonExistentSession_ShouldReturnSuccess()
    {
        // Arrange
        var sessionId = "non-existent-delete-session";

        // Act
        var response = await _client.DeleteAsync($"/api/chat/session/{sessionId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendMessage_WithWhitespaceMessage_ShouldReturnBadRequest()
    {
        // Arrange
        var request = CreateChatRequest("   ");

        // Act
        var response = await _client.PostAsync("/api/chat", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SendMessage_ShouldReturnCorrectContentType()
    {
        // Arrange
        var request = CreateChatRequest("测试内容类型");

        // Act
        var response = await _client.PostAsync("/api/chat", request);

        // Assert
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task SendMessage_WithLongMessage_ShouldHandleCorrectly()
    {
        // Arrange
        var longMessage = new string('测', 1000);
        var request = CreateChatRequest(longMessage);

        // Act
        var response = await _client.PostAsync("/api/chat", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var chatResponse = JsonSerializer.Deserialize<ChatResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        chatResponse.Should().NotBeNull();
    }

    [Fact]
    public async Task SendMessage_MultipleRequests_ShouldMaintainSessionContext()
    {
        // Arrange
        var sessionId = "context-session-" + Guid.NewGuid().ToString("N");
        var request1 = CreateChatRequest("你好", sessionId);
        var request2 = CreateChatRequest("再见", sessionId);

        // Act
        await _client.PostAsync("/api/chat", request1);
        await _client.PostAsync("/api/chat", request2);

        // Assert - 验证历史消息包含所有对话
        var historyResponse = await _client.GetAsync($"/api/chat/history/{sessionId}");
        var historyContent = await historyResponse.Content.ReadAsStringAsync();
        var history = JsonSerializer.Deserialize<List<ChatResponse>>(historyContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        history.Should().HaveCount(4); // 2用户 + 2助手
        history![0].Content.Should().Be("你好");
        history[2].Content.Should().Be("再见");
    }

    [Fact]
    public async Task SendMessage_WithVeryLongMessage_ShouldReturnSuccess_Not500()
    {
        // 复现 N+6「文本超长 500」：此前 Content 列限长 2000，XCode 长度校验抛错致 500；
        // 改为文本类型(-1)后，超长消息应正常持久化并返回 200。
        var longMessage = new string('中', 5000);
        var request = CreateChatRequest(longMessage);

        var response = await _client.PostAsync("/api/chat", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var chatResponse = JsonSerializer.Deserialize<ChatResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        chatResponse.Should().NotBeNull();
        chatResponse!.Content.Should().NotBeEmpty();
    }
}

/// <summary>
/// 模拟的AI服务，用于测试
/// </summary>
internal class MockAIService : IAIService
{
    public async Task<string> ChatAsync(List<AIChatMessage> messages)
    {
        // 返回模拟响应
        await Task.Delay(10); // 模拟延迟
        return $"模拟响应: 收到 {messages.Count} 条消息";
    }

    public Task<string> ChatAsync(List<AIChatMessage> messages, string? chatModelId)
        => ChatAsync(messages);

    public async IAsyncEnumerable<string> ChatStreamAsync(List<AIChatMessage> messages, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken);
        yield return "模拟";
        await Task.Delay(10, cancellationToken);
        yield return "流式";
        await Task.Delay(10, cancellationToken);
        yield return "响应";
    }

    public IAsyncEnumerable<string> ChatStreamAsync(List<AIChatMessage> messages, string? chatModelId, CancellationToken cancellationToken = default)
        => ChatStreamAsync(messages, cancellationToken);
}
