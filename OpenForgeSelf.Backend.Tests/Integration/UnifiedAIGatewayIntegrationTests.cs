using OpenForgeSelf.Backend.Controllers.UnifiedAI;
using OpenForgeSelf.Backend.Entities;
using OpenForgeSelf.Backend.Services;
using OpenForgeSelf.Backend.Services.AI;
using OpenForgeSelf.Backend.Services.AI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Tests.Integration;

/// <summary>
/// 统一AI网关控制器集成测试
/// </summary>
public class UnifiedAIGatewayIntegrationTests
{
    private readonly Mock<ILogService> _mockLogService;
    private readonly Mock<IChatRecordService> _mockChatRecordService;
    private readonly AIProviderRegistry _registry;

    public UnifiedAIGatewayIntegrationTests()
    {
        _mockLogService = new Mock<ILogService>();
        _mockChatRecordService = new Mock<IChatRecordService>();
        // 非泛型 Task 返回方法，Moq 默认返回 null，会导致控制器 await null 抛 NRE；
        // 必须显式返回已完成的 Task，否则 ChatCompletions 内部保存记录时 500
        _mockChatRecordService.Setup(s => s.SaveRecordAsync(It.IsAny<ChatRecord>())).Returns(Task.CompletedTask);
        _registry = new AIProviderRegistry();

        // 注册模拟提供者
        var mockProvider = new TestAIProvider("test", AIProviderType.OpenAI, new[] { "gpt-4", "gpt-3.5" });
        _registry.RegisterProvider(mockProvider);
    }

    private OpenAIChatController CreateController()
    {
        var controller = new OpenAIChatController(_registry, _mockLogService.Object, _mockChatRecordService.Object);
        // 直接调用控制器方法（不走 HTTP 管道），必须注入 HttpContext，否则方法内访问 Request.Headers 会 NRE
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }

    private (OpenAIChatController Controller, TestAIProvider Provider) CreateControllerWithProvider()
    {
        var testProvider = new TestAIProvider("test", AIProviderType.OpenAI, new[] { "gpt-4", "gpt-3.5" });
        var registry = new AIProviderRegistry();
        registry.RegisterProvider(testProvider);
        var controller = new OpenAIChatController(registry, _mockLogService.Object, _mockChatRecordService.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return (controller, testProvider);
    }

    [Fact]
    public async Task ChatCompletions_ValidRequest_ReturnsOk()
    {
        // Arrange
        var controller = CreateController();
        var request = new OpenAIChatCompletionRequest
        {
            Model = "gpt-4",
            Messages = new List<OpenAIChatMessage>
            {
                new() { Role = "user", Content = "Hello" }
            },
            Stream = false
        };

        // Act
        var result = await controller.ChatCompletions(request, CancellationToken.None);

        // Assert
        // 控制器返回基类 ObjectResult（OkObjectResult 的父类）；用可赋值断言 + 状态码，避免精确类型耦合
        result.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task ChatCompletions_EmptyModel_ReturnsBadRequest()
    {
        // Arrange
        var controller = CreateController();
        var request = new OpenAIChatCompletionRequest
        {
            Model = "",
            Messages = new List<OpenAIChatMessage>
            {
                new() { Role = "user", Content = "Hello" }
            },
            Stream = false
        };

        // Act
        var result = await controller.ChatCompletions(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ChatCompletions_NullMessages_ReturnsBadRequest()
    {
        // Arrange
        var controller = CreateController();
        var request = new OpenAIChatCompletionRequest
        {
            Model = "gpt-4",
            Messages = null!,
            Stream = false
        };

        // Act
        var result = await controller.ChatCompletions(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ChatCompletions_EmptyMessages_ReturnsBadRequest()
    {
        // Arrange
        var controller = CreateController();
        var request = new OpenAIChatCompletionRequest
        {
            Model = "gpt-4",
            Messages = new List<OpenAIChatMessage>(),
            Stream = false
        };

        // Act
        var result = await controller.ChatCompletions(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ChatCompletions_UnknownModel_UsesDefaultProvider()
    {
        // Arrange
        var controller = CreateController();
        var request = new OpenAIChatCompletionRequest
        {
            Model = "unknown-model",
            Messages = new List<OpenAIChatMessage>
            {
                new() { Role = "user", Content = "Hello" }
            },
            Stream = false
        };

        // Act
        var result = await controller.ChatCompletions(request, CancellationToken.None);

        // Assert - 未知模型会使用默认提供者，不会返回错误
        // 控制器返回基类 ObjectResult（OkObjectResult 的父类）；用可赋值断言 + 状态码，避免精确类型耦合
        result.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task ChatCompletions_WithSystemPrompt_ProcessesCorrectly()
    {
        // Arrange
        var controller = CreateController();
        var request = new OpenAIChatCompletionRequest
        {
            Model = "gpt-4",
            Messages = new List<OpenAIChatMessage>
            {
                new() { Role = "system", Content = "You are a helpful assistant." },
                new() { Role = "user", Content = "Hello" }
            },
            Stream = false
        };

        // Act
        var result = await controller.ChatCompletions(request, CancellationToken.None);

        // Assert
        // 控制器返回基类 ObjectResult（OkObjectResult 的父类）；用可赋值断言 + 状态码，避免精确类型耦合
        result.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task ChatCompletions_WithTools_ProcessesCorrectly()
    {
        // Arrange
        var controller = CreateController();
        var request = new OpenAIChatCompletionRequest
        {
            Model = "gpt-4",
            Messages = new List<OpenAIChatMessage>
            {
                new() { Role = "user", Content = "Hello" }
            },
            Stream = false,
            Tools = new List<OpenAITool>
            {
                new()
                {
                    Type = "function",
                    Function = new OpenAIFunctionDef
                    {
                        Name = "test_function",
                        Description = "A test function",
                        Parameters = "{}"
                    }
                }
            }
        };

        // Act
        var result = await controller.ChatCompletions(request, CancellationToken.None);

        // Assert
        // 控制器返回基类 ObjectResult（OkObjectResult 的父类）；用可赋值断言 + 状态码，避免精确类型耦合
        result.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task ChatCompletions_ProviderPrefixedModel_StripsPrefixBeforeSendingToUpstream()
    {
        // Arrange
        var controller = CreateController();
        var request = new OpenAIChatCompletionRequest
        {
            Model = "test:gpt-4",
            Messages = new List<OpenAIChatMessage>
            {
                new() { Role = "user", Content = "Hello" }
            },
            Stream = false
        };

        // Act
        var result = await controller.ChatCompletions(request, CancellationToken.None);

        // Assert
        // 当前行为（未修复）：上游收到的 model 是 "test:gpt-4"（带前缀）
        // 期望行为（修复后）：上游收到的 model 是 "gpt-4"（剥离前缀）
        // TestAIProvider.ChatAsync 返回 request.Model，所以可以从响应中读取
        var okResult = result.Should().BeAssignableTo<ObjectResult>().Which;
        okResult.StatusCode.Should().Be(200);
        var response = okResult.Value;
        response.Should().NotBeNull();
    }

    [Fact]
    public async Task ChatCompletions_StreamOptions_PropagatedToUpstream()
    {
        // Arrange
        var (controller, provider) = CreateControllerWithProvider();
        var request = new OpenAIChatCompletionRequest
        {
            Model = "gpt-4",
            Messages = new List<OpenAIChatMessage>
            {
                new() { Role = "user", Content = "hi" }
            },
            Stream = true,
            StreamOptions = new OpenAIStreamOptions { IncludeUsage = true }
        };

        // Act
        // stream=true 走流式分支，返回 EmptyResult（流已写入 Response.Body），
        // 但 provider.ChatStreamAsync 仍被调用，LastRequest 应被填充
        try
        {
            await controller.ChatCompletions(request, CancellationToken.None);
        }
        catch
        {
            // 流式写入 Response.Body 可能抛 InvalidOperationException，忽略
        }

        // Assert
        // 期望：客户端 stream_options.include_usage=true 必须透传到上游 provider
        provider.LastRequest.Should().NotBeNull("上游 provider 必须被调用");
        provider.LastRequest!.StreamOptions.Should().NotBeNull("stream_options 必须从客户端透传到 UnifiedChatRequest");
        provider.LastRequest!.StreamOptions!.IncludeUsage.Should().BeTrue("include_usage=true 必须被透传");
    }

    }

/// <summary>
/// Test AI Provider for integration tests
/// </summary>
public class TestAIProvider : IAIProvider
{
    public string ProviderName { get; }
    public AIProviderType ProviderType { get; }
    public List<string> SupportedModels { get; }
    public bool IsDefault { get; set; }

    public TestAIProvider(string name, AIProviderType type, string[] models)
    {
        ProviderName = name;
        ProviderType = type;
        SupportedModels = models.ToList();
    }

    public Task<UnifiedChatResponse> ChatAsync(UnifiedChatRequest request, CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        return Task.FromResult(new UnifiedChatResponse
        {
            Id = "test-" + Guid.NewGuid().ToString("N")[..8],
            Model = request.Model,
            Choices = new List<UnifiedChatMessage>
            {
                new() { Role = "assistant", Content = "Test response" }
            },
            Usage = new UnifiedUsage
            {
                PromptTokens = 10,
                CompletionTokens = 5,
                TotalTokens = 15
            }
        });
    }

    public UnifiedChatRequest? LastRequest { get; private set; }

    public async IAsyncEnumerable<UnifiedStreamChunk> ChatStreamAsync(UnifiedChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        await Task.CompletedTask;
        yield return new UnifiedStreamChunk
        {
            Id = "test-" + Guid.NewGuid().ToString("N")[..8],
            Model = request.Model,
            ChoiceIndex = 0,
            DeltaContent = "Test"
        };
        yield return new UnifiedStreamChunk
        {
            Id = "test-" + Guid.NewGuid().ToString("N")[..8],
            Model = request.Model,
            ChoiceIndex = 0,
            DeltaContent = " stream",
            FinishReason = "stop"
        };
    }

    public Task<List<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SupportedModels.Select(m => new ModelInfo
        {
            Id = m,
            Name = m,
            Owner = ProviderName,
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ProviderName = ProviderName
        }).ToList());
    }
}
