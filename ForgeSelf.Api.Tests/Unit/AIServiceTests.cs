using System.Net;
using ForgeSelf.Abstractions;
using System.Text;
using System.Text.Json;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Services;
using ForgeSelf.Api.Services.AI;
using ForgeSelf.Api.Services.AI.Models;
// 宿主旧版 AI 消息模型与 Abstractions.AIChatMessage 同名，用别名消除 CS0104 歧义。
using LegacyAIChatMessage = ForgeSelf.Api.Models.AIChatMessage;
using LegacyAIChatMessageDelta = ForgeSelf.Api.Models.AIChatMessageDelta;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// AI代理服务单元测试
/// </summary>
public class AIServiceTests
{
    private readonly Mock<IConfigurationService> _mockConfigService;
    private readonly Mock<ILogService> _mockLogService;
    private readonly AIConfig _testConfig;

    public AIServiceTests()
    {
        _mockConfigService = new Mock<IConfigurationService>();
        _mockLogService = new Mock<ILogService>();
        _testConfig = new AIConfig
        {
            ApiEndpoint = "https://api.test.com/v1/chat/completions",
            ApiKey = "test-api-key",
            ModelName = "test-model"
        };
        _mockConfigService.Setup(x => x.GetAIConfig()).Returns(_testConfig);
    }

    /// <summary>
    /// 创建模拟的HttpClient
    /// </summary>
    private HttpClient CreateMockHttpClient(HttpResponseMessage response)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(response);

        return new HttpClient(handlerMock.Object);
    }

    /// <summary>
    /// 创建AI响应JSON
    /// </summary>
    private string CreateAIResponseJson(string content)
    {
        var response = new AIChatResponse
        {
            Id = "test-id",
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = _testConfig.ModelName,
            Choices = new List<AIChatChoice>
            {
                new AIChatChoice
                {
                    Index = 0,
                    Message = new LegacyAIChatMessageDelta
                    {
                        Role = "assistant",
                        Content = content
                    },
                    FinishReason = "stop"
                }
            }
        };
        return JsonSerializer.Serialize(response);
    }

    [Fact]
    public async Task ChatAsync_WithValidMessages_ShouldReturnAIResponse()
    {
        // Arrange
        var expectedContent = "这是一个测试响应";
        var responseJson = CreateAIResponseJson(expectedContent);
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };
        var httpClient = CreateMockHttpClient(httpResponse);
        var service = new AIService(_mockConfigService.Object, _mockLogService.Object, httpClient);

        var messages = new List<LegacyAIChatMessage>
        {
            new LegacyAIChatMessage { Role = "user", Content = "你好" }
        };

        // Act
        var result = await service.ChatAsync(messages);

        // Assert
        result.Should().Be(expectedContent);
        _mockLogService.Verify(x => x.Info(It.IsAny<string>(), It.IsAny<object[]>()), Times.AtLeastOnce());
    }

    [Fact]
    public async Task ChatAsync_WhenNoChoices_ShouldReturnFallbackResponse()
    {
        // Arrange
        var response = new AIChatResponse
        {
            Id = "test-id",
            Choices = new List<AIChatChoice>()
        };
        var responseJson = JsonSerializer.Serialize(response);
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };
        var httpClient = CreateMockHttpClient(httpResponse);
        var service = new AIService(_mockConfigService.Object, _mockLogService.Object, httpClient);

        var messages = new List<LegacyAIChatMessage>
        {
            new LegacyAIChatMessage { Role = "user", Content = "测试" }
        };

        // Act
        var result = await service.ChatAsync(messages);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain("降级响应");
        _mockLogService.Verify(x => x.Warn(It.IsAny<string>(), It.IsAny<object[]>()), Times.Once());
    }

    [Fact]
    public async Task ChatAsync_WhenHttpRequestFails_ShouldReturnFallbackResponse()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.InternalServerError,
            Content = new StringContent("Error", Encoding.UTF8, "application/json")
        };
        var httpClient = CreateMockHttpClient(httpResponse);
        var service = new AIService(_mockConfigService.Object, _mockLogService.Object, httpClient);

        var messages = new List<LegacyAIChatMessage>
        {
            new LegacyAIChatMessage { Role = "user", Content = "测试" }
        };

        // Act
        var result = await service.ChatAsync(messages);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain("降级响应");
        _mockLogService.Verify(x => x.Error(It.IsAny<string>(), It.IsAny<object[]>()), Times.Once());
    }

    [Fact]
    public async Task ChatAsync_WithMultipleMessages_ShouldReturnResponse()
    {
        // Arrange
        var expectedContent = "多轮对话响应";
        var responseJson = CreateAIResponseJson(expectedContent);
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };
        var httpClient = CreateMockHttpClient(httpResponse);
        var service = new AIService(_mockConfigService.Object, _mockLogService.Object, httpClient);

        var messages = new List<LegacyAIChatMessage>
        {
            new LegacyAIChatMessage { Role = "user", Content = "你好" },
            new LegacyAIChatMessage { Role = "assistant", Content = "你好！有什么可以帮助你的？" },
            new LegacyAIChatMessage { Role = "user", Content = "请介绍一下你自己" }
        };

        // Act
        var result = await service.ChatAsync(messages);

        // Assert
        result.Should().Be(expectedContent);
    }

    [Fact]
    public async Task ChatAsync_ShouldUseCorrectAuthorizationHeader()
    {
        // Arrange
        var httpRequest = new HttpRequestMessage();
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => httpRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(CreateAIResponseJson("test"), Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var service = new AIService(_mockConfigService.Object, _mockLogService.Object, httpClient);

        var messages = new List<LegacyAIChatMessage>
        {
            new LegacyAIChatMessage { Role = "user", Content = "测试" }
        };

        // Act
        await service.ChatAsync(messages);

        // Assert
        httpRequest.Headers.Authorization.Should().NotBeNull();
        httpRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        httpRequest.Headers.Authorization!.Parameter.Should().Be(_testConfig.ApiKey);
    }

    [Fact]
    public async Task ChatStreamAsync_WithValidMessages_ShouldReturnStreamContent()
    {
        // Arrange
        var streamContent = "data: {\"id\":\"test\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"你好\"}}]}\n\ndata: [DONE]\n\n";
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(streamContent, Encoding.UTF8, "application/json")
        };
        var httpClient = CreateMockHttpClient(httpResponse);
        var service = new AIService(_mockConfigService.Object, _mockLogService.Object, httpClient);

        var messages = new List<LegacyAIChatMessage>
        {
            new LegacyAIChatMessage { Role = "user", Content = "你好" }
        };

        // Act
        var results = new List<string>();
        await foreach (var chunk in service.ChatStreamAsync(messages))
        {
            results.Add(chunk);
        }

        // Assert
        results.Should().Contain("你好");
        _mockLogService.Verify(x => x.Info(It.IsAny<string>(), It.IsAny<object[]>()), Times.AtLeastOnce());
    }

    [Fact]
    public async Task ChatStreamAsync_WhenHttpRequestFails_ShouldReturnEmptyStream()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.InternalServerError,
            Content = new StringContent("Error", Encoding.UTF8, "application/json")
        };
        var httpClient = CreateMockHttpClient(httpResponse);
        var service = new AIService(_mockConfigService.Object, _mockLogService.Object, httpClient);

        var messages = new List<LegacyAIChatMessage>
        {
            new LegacyAIChatMessage { Role = "user", Content = "测试" }
        };

        // Act
        var results = new List<string>();
        await foreach (var chunk in service.ChatStreamAsync(messages))
        {
            results.Add(chunk);
        }

        // Assert
        results.Should().BeEmpty();
        _mockLogService.Verify(x => x.Error(It.IsAny<string>(), It.IsAny<object[]>()), Times.Once());
    }

    [Fact]
    public async Task ChatStreamAsync_WithCancellation_ShouldStopStreaming()
    {
        // Arrange
        var streamContent = "data: {\"choices\":[{\"delta\":{\"content\":\"chunk1\"}}]}\n\ndata: {\"choices\":[{\"delta\":{\"content\":\"chunk2\"}}]}\n\n";
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(streamContent)))
        };
        var httpClient = CreateMockHttpClient(httpResponse);
        var service = new AIService(_mockConfigService.Object, _mockLogService.Object, httpClient);

        var messages = new List<LegacyAIChatMessage>
        {
            new LegacyAIChatMessage { Role = "user", Content = "测试" }
        };

        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        // Act
        var results = new List<string>();
        try
        {
            await foreach (var chunk in service.ChatStreamAsync(messages, cts.Token))
            {
                results.Add(chunk);
                if (results.Count >= 1)
                {
                    cts.Cancel();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - 流应该被中断
        results.Count.Should().BeLessOrEqualTo(2);
    }

    [Fact]
    public async Task ChatStreamAsync_WithInvalidJson_ShouldSkipInvalidLines()
    {
        // Arrange
        var streamContent = "data: {\"choices\":[{\"delta\":{\"content\":\"有效内容\"}}]}\n\ndata: invalid-json\n\ndata: [DONE]\n\n";
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(streamContent, Encoding.UTF8, "application/json")
        };
        var httpClient = CreateMockHttpClient(httpResponse);
        var service = new AIService(_mockConfigService.Object, _mockLogService.Object, httpClient);

        var messages = new List<LegacyAIChatMessage>
        {
            new LegacyAIChatMessage { Role = "user", Content = "测试" }
        };

        // Act
        var results = new List<string>();
        await foreach (var chunk in service.ChatStreamAsync(messages))
        {
            results.Add(chunk);
        }

        // Assert
        results.Should().Contain("有效内容");
        _mockLogService.Verify(x => x.Warn(It.IsAny<string>(), It.IsAny<object[]>()), Times.Once());
    }

    [Fact]
    public async Task ChatAsync_WithEmptyMessageContent_ShouldStillSendRequest()
    {
        // Arrange
        var responseJson = CreateAIResponseJson("响应");
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };
        var httpClient = CreateMockHttpClient(httpResponse);
        var service = new AIService(_mockConfigService.Object, _mockLogService.Object, httpClient);

        var messages = new List<LegacyAIChatMessage>
        {
            new LegacyAIChatMessage { Role = "user", Content = "" }
        };

        // Act
        var result = await service.ChatAsync(messages);

        // Assert
        result.Should().Be("响应");
    }

    [Fact]
    public void Constructor_ShouldSetHttpClientTimeout()
    {
        // Arrange
        var httpClient = new HttpClient();

        // Act
        var service = new AIService(_mockConfigService.Object, _mockLogService.Object, httpClient);

        // Assert
        httpClient.Timeout.Should().Be(TimeSpan.FromMinutes(5));
    }

    #region chatModelId 路由（ChatController → IAIService 按所选模型路由提供方）

    /// <summary>
    /// 构造带 MockAIProvider 的注册表（复用 AIProviderRegistryTests 的测试替身）。
    /// </summary>
    private static AIProviderRegistry CreateRegistryWithProvider(string providerName, string[] models)
    {
        var registry = new AIProviderRegistry();
        registry.RegisterProvider(new MockAIProvider(providerName, AIProviderType.OpenAI, models));
        return registry;
    }

    [Fact]
    public async Task ChatAsync_WithChatModelId_ShouldRouteThroughProviderRegistry()
    {
        // Arrange：注册表命中模型 → 应走提供方，不走旧配置 HTTP
        var registry = CreateRegistryWithProvider("test-provider", new[] { "gpt-test-model" });
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(CreateAIResponseJson("旧配置路径的响应"), Encoding.UTF8, "application/json")
        };
        var httpClient = CreateMockHttpClient(httpResponse);
        var service = new AIService(_mockConfigService.Object, _mockLogService.Object, httpClient, registry);

        var messages = new List<LegacyAIChatMessage>
        {
            new LegacyAIChatMessage { Role = "user", Content = "你好" }
        };

        // Act
        var result = await service.ChatAsync(messages, "gpt-test-model");

        // Assert
        result.Should().Be("Mock response");
    }

    [Fact]
    public async Task ChatAsync_WithChatModelIdButNoProviderMatch_ShouldFallbackToLegacyConfig()
    {
        // Arrange：注册表为空（无任何提供方）→ 回退旧配置 HTTP 路径
        var registry = new AIProviderRegistry();
        var expectedContent = "旧配置路径的响应";
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(CreateAIResponseJson(expectedContent), Encoding.UTF8, "application/json")
        };
        var httpClient = CreateMockHttpClient(httpResponse);
        var service = new AIService(_mockConfigService.Object, _mockLogService.Object, httpClient, registry);

        var messages = new List<LegacyAIChatMessage>
        {
            new LegacyAIChatMessage { Role = "user", Content = "测试" }
        };

        // Act
        var result = await service.ChatAsync(messages, "no-such-model");

        // Assert
        result.Should().Be(expectedContent);
        _mockLogService.Verify(x => x.Warn(It.IsAny<string>(), It.IsAny<object[]>()), Times.AtLeastOnce());
    }

    [Fact]
    public async Task ChatAsync_WithChatModelIdButNullRegistry_ShouldFallbackToLegacyConfig()
    {
        // Arrange：未注入注册表 → 回退旧配置 HTTP 路径
        var expectedContent = "旧配置路径的响应";
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(CreateAIResponseJson(expectedContent), Encoding.UTF8, "application/json")
        };
        var httpClient = CreateMockHttpClient(httpResponse);
        AIService service = new AIService(_mockConfigService.Object, _mockLogService.Object, httpClient);

        var messages = new List<LegacyAIChatMessage>
        {
            new LegacyAIChatMessage { Role = "user", Content = "测试" }
        };

        // Act
        var result = await service.ChatAsync(messages, "gpt-test-model");

        // Assert
        result.Should().Be(expectedContent);
    }

    [Fact]
    public async Task ChatStreamAsync_WithChatModelId_ShouldRouteThroughProviderRegistry()
    {
        // Arrange
        var registry = CreateRegistryWithProvider("test-provider", new[] { "gpt-test-model" });
        var streamContent = "data: {\"choices\":[{\"delta\":{\"content\":\"旧配置流\"}}]}\n\ndata: [DONE]\n\n";
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(streamContent, Encoding.UTF8, "application/json")
        };
        var httpClient = CreateMockHttpClient(httpResponse);
        var service = new AIService(_mockConfigService.Object, _mockLogService.Object, httpClient, registry);

        var messages = new List<LegacyAIChatMessage>
        {
            new LegacyAIChatMessage { Role = "user", Content = "你好" }
        };

        // Act
        var results = new List<string>();
        await foreach (var chunk in service.ChatStreamAsync(messages, "gpt-test-model"))
        {
            results.Add(chunk);
        }

        // Assert
        results.Should().Contain("Mock stream response");
        results.Should().NotContain("旧配置流");
    }
    #endregion
}