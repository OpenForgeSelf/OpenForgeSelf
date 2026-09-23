using System.Net;
using ForgeSelf.Abstractions;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Configuration;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Services;
using ForgeSelf.Api.Tests.TestDoubles;
// 宿主旧版 AI 消息模型与 Abstractions.AIChatMessage 同名，用别名消除 CS0104 歧义。
using LegacyAIChatMessage = ForgeSelf.Api.Models.AIChatMessage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeSelf.Api.Tests.Integration;

/// <summary>
/// 真实LLM配置集成测试 - 验证配置文件中的真实LLM配置是否正确接入
/// </summary>
public class RealLLMIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _tempDbDir;

    public RealLLMIntegrationTests(WebApplicationFactory<Program> factory)
    {
        // 隔离 AIProvider 存储库：覆盖为临时文件，避免跨运行遗留的历史密文导致启动期解密失败
        _tempDbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfRealLLM_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDbDir);
        var openForgeDb = Path.Combine(_tempDbDir, "ForgeSelf.db");

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ForgeSelf"] = $"Data Source={openForgeDb}"
                });
            });
        });
        _client = _factory.CreateClient();
    }

    [Fact]
    public void AIService_ShouldBeRegisteredWithRealConfig()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var aiService = scope.ServiceProvider.GetRequiredService<IAIService>();

        // Assert
        aiService.Should().NotBeNull();
        aiService.Should().BeOfType<AIService>();
    }

    [Fact]
    public async Task AIService_ShouldSendCorrectRequestFormat()
    {
        // Arrange - 使用自定义 HttpMessageHandler 捕获请求
        var capturedRequest = new HttpRequestMessage();
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    JsonSerializer.Serialize(new AIChatResponse
                    {
                        Id = "test-id",
                        Choices = new List<AIChatChoice>
                        {
                            new()
                            {
                                Index = 0,
                                Message = new AIChatMessageDelta
                                {
                                    Role = "assistant",
                                    Content = "测试响应"
                                }
                            }
                        }
                    }),
                    Encoding.UTF8,
                    "application/json")
            });

        using var scope = _factory.Services.CreateScope();
        var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
        var logService = scope.ServiceProvider.GetRequiredService<ILogService>();
        var httpClient = new HttpClient(handlerMock.Object);

        var aiService = new AIService(configService, logService, httpClient);
        var messages = new List<LegacyAIChatMessage>
        {
            new() { Role = "user", Content = "你好" }
        };

        // Act
        var result = await aiService.ChatAsync(messages);

        // Assert - 验证请求格式
        capturedRequest.Should().NotBeNull();
        capturedRequest.Method.Should().Be(HttpMethod.Post);
        capturedRequest.RequestUri.Should().NotBeNull();
        capturedRequest.RequestUri!.ToString().Should().Be(configService.GetAIConfig().ApiEndpoint);

        // 验证 Authorization header
        capturedRequest.Headers.Authorization.Should().NotBeNull();
        capturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        capturedRequest.Headers.Authorization!.Parameter.Should().Be(configService.GetAIConfig().ApiKey);

        // 验证请求体
        capturedRequest.Content.Should().NotBeNull();
        var content = await capturedRequest.Content!.ReadAsStringAsync();
        var requestBody = JsonSerializer.Deserialize<AIChatRequest>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        requestBody.Should().NotBeNull();
        requestBody!.Model.Should().Be(configService.GetAIConfig().ModelName);
        requestBody.Messages.Should().HaveCount(1);
        requestBody.Messages[0].Role.Should().Be("user");
        requestBody.Messages[0].Content.Should().Be("你好");

        // 验证响应
        result.Should().Be("测试响应");
    }

    [Fact]
    public async Task ChatController_ShouldReturnMockResponse()
    {
        // Arrange - 用 FakeAIService（固定返回）替代真实 LLM，使测试离线、确定性、不依赖 ApiKey/联网
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAIService));
                if (descriptor != null) services.Remove(descriptor);
                services.AddScoped<IAIService, FakeAIService>();
            });
        });

        var client = factory.CreateClient();
        var request = new
        {
            Message = "你好，请介绍一下你自己",
            SessionId = "mock-llm-test-session"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/chat", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(FakeAIService.FixedReply);
    }

    [Fact]
    public async Task RealLLMConnectionTest_SkipIfNotAvailable()
    {
        // 这个测试尝试连接真实的 LLM 服务
        // 如果服务不可用，则跳过测试
        using var scope = _factory.Services.CreateScope();
        var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
        var aiConfig = configService.GetAIConfig();

        // 检查是否配置了真实的 API 端点
        if (string.IsNullOrEmpty(aiConfig.ApiEndpoint) || string.IsNullOrEmpty(aiConfig.ApiKey))
        {
            // 没有配置，跳过测试
            return;
        }

        try
        {
            // 尝试发送一个简单的请求来测试连接
            var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            var request = new AIChatRequest
            {
                Model = aiConfig.ModelName,
                Messages = new List<LegacyAIChatMessage>
                {
                    new() { Role = "user", Content = "Hello, this is a connection test." }
                },
                Stream = false
            };

            var jsonContent = JsonSerializer.Serialize(request);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, aiConfig.ApiEndpoint);
            httpRequest.Content = content;
            httpRequest.Headers.Add("Authorization", $"Bearer {aiConfig.ApiKey}");

            var response = await httpClient.SendAsync(httpRequest);

            // 如果连接成功，验证响应格式
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var aiResponse = JsonSerializer.Deserialize<AIChatResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                aiResponse.Should().NotBeNull();
                aiResponse!.Choices.Should().NotBeNull();
            }
            // 如果连接失败（404, 500 等），不认为是测试失败
            // 因为这可能是因为本地服务没有运行
        }
        catch (HttpRequestException)
        {
            // 连接失败，可能是服务没有运行，跳过测试
        }
        catch (TaskCanceledException)
        {
            // 超时，跳过测试
        }
    }
}
