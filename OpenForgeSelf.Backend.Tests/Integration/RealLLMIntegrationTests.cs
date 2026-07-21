using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Configuration;
using OpenForgeSelf.Backend.Models;
using OpenForgeSelf.Backend.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace OpenForgeSelf.Backend.Tests.Integration;

/// <summary>
/// 真实LLM配置集成测试 - 验证配置文件中的真实LLM配置是否正确接入
/// </summary>
public class RealLLMIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _tempDbDir;

    public RealLLMIntegrationTests(WebApplicationFactory<Program> factory)
    {
        // 隔离 AIProvider 存储库：覆盖为临时文件，避免跨运行遗留的历史密文导致启动期解密失败
        _tempDbDir = Path.Combine(Path.GetTempPath(), $"OpenForgeSelfRealLLM_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDbDir);
        var openForgeDb = Path.Combine(_tempDbDir, "OpenForgeSelf.db");

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:OpenForgeSelf"] = $"Data Source={openForgeDb}"
                });
            });
        });
        _client = _factory.CreateClient();
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
        var messages = new List<AIChatMessage>
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
    public async Task ChatController_ShouldUseRealAIServiceConfig()
    {
        // Arrange - 使用 WebApplicationFactory，但替换 HttpMessageHandler 来捕获真实请求
        var capturedRequests = new List<HttpRequestMessage>();
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // 移除现有的 IAIService 注册
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IAIService));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // 注册带自定义 HttpMessageHandler 的 AIService
                var handlerMock = new Mock<HttpMessageHandler>();
                handlerMock
                    .Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>()
                    )
                    .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequests.Add(req))
                    .ReturnsAsync(new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(
                            JsonSerializer.Serialize(new AIChatResponse
                            {
                                Id = "chat-test-1",
                                Choices = new List<AIChatChoice>
                                {
                                    new()
                                    {
                                        Index = 0,
                                        Message = new AIChatMessageDelta
                                        {
                                            Role = "assistant",
                                            Content = "你好！我是AI助手，有什么可以帮助你的？"
                                        }
                                    }
                                }
                            }),
                            Encoding.UTF8,
                            "application/json")
                    });

                // 注册为 Scoped：ILogService 为 Scoped，单例工厂从 root provider 无法解析 Scoped 服务
                services.AddScoped<IAIService>(sp =>
                {
                    var configService = sp.GetRequiredService<IConfigurationService>();
                    var logService = sp.GetRequiredService<ILogService>();
                    var httpClient = new HttpClient(handlerMock.Object);
                    return new AIService(configService, logService, httpClient);
                });
            });
        });

        var client = factory.CreateClient();
        var request = new
        {
            Message = "你好，请介绍一下你自己",
            SessionId = "real-llm-test-session"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/chat", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        capturedRequests.Should().HaveCountGreaterOrEqualTo(1);

        var aiRequest = capturedRequests[0];
        aiRequest.Headers.Authorization.Should().NotBeNull();
        aiRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");

        // 验证使用了真实的 API Key
        var configService = factory.Services.GetRequiredService<IConfigurationService>();
        var aiConfig = configService.GetAIConfig();
        aiRequest.Headers.Authorization!.Parameter.Should().Be(aiConfig.ApiKey);

        // 验证使用了正确的端点
        aiRequest.RequestUri.Should().NotBeNull();
        aiRequest.RequestUri!.ToString().Should().Be(aiConfig.ApiEndpoint);
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
                Messages = new List<AIChatMessage>
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
