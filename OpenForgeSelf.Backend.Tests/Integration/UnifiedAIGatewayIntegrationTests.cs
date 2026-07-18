using OpenForgeSelf.Backend.Controllers.UnifiedAI;
using OpenForgeSelf.Backend.Entities;
using OpenForgeSelf.Backend.Services;
using OpenForgeSelf.Backend.Services.AI;
using OpenForgeSelf.Backend.Services.AI.Models;
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
        _registry = new AIProviderRegistry();

        // 注册模拟提供者
        var mockProvider = new TestAIProvider("test", AIProviderType.OpenAI, new[] { "gpt-4", "gpt-3.5" });
        _registry.RegisterProvider(mockProvider);
    }

    private OpenAIChatController CreateController()
    {
        return new OpenAIChatController(_registry, _mockLogService.Object, _mockChatRecordService.Object);
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
        result.Should().BeOfType<OkObjectResult>();
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
        result.Should().BeOfType<OkObjectResult>();
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
        result.Should().BeOfType<OkObjectResult>();
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
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ListModels_ReturnsOk()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.ListModels(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void GetModel_ExistingModel_ReturnsOk()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = controller.GetModel("gpt-4");

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void GetModel_NonExistingModel_ReturnsNotFound()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = controller.GetModel("nonexistent-model");

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
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

    public async IAsyncEnumerable<UnifiedStreamChunk> ChatStreamAsync(UnifiedChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
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
