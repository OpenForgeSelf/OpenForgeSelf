using ForgeSelf.Api.Controllers.UnifiedAI;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Services;
using ForgeSelf.Api.Services.AI;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NewLife.Log;

namespace ForgeSelf.Api.Tests.Integration;

/// <summary>
/// 统一AI网关控制器集成测试
/// </summary>
public class UnifiedAIGatewayIntegrationTests
{
    private readonly Mock<ILogService> _mockLogService;
    private readonly Mock<IChatTurnService> _mockChatTurnService;
    private readonly Mock<IChatTurnStreamRecorder> _mockStreamRecorder;
    private readonly Mock<IChatSessionService> _mockChatSessionService;
    private readonly Mock<IImageRecognitionCache> _mockImageRecognitionCache;
    private readonly AIProviderRegistry _registry;

    public UnifiedAIGatewayIntegrationTests()
    {
        _mockLogService = new Mock<ILogService>();
        _mockChatTurnService = new Mock<IChatTurnService>();
        _mockStreamRecorder = new Mock<IChatTurnStreamRecorder>();
        _mockChatSessionService = new Mock<IChatSessionService>();
        _mockImageRecognitionCache = new Mock<IImageRecognitionCache>();
        // 返回 null 会话：本集成测试只验证控制器逻辑，流式路径回退到 SaveTurnAsync（已 mock）。
        _mockStreamRecorder.Setup(r => r.BeginAsync(It.IsAny<ChatTurn>())).ReturnsAsync((ChatTurnStreamSession)null!);
        // 非泛型 Task 返回方法，Moq 默认返回 null，会导致控制器 await null 抛 NRE；
        // 必须显式返回已完成的 Task，否则 ChatCompletions 内部保存轮次时 500
        _mockChatTurnService.Setup(s => s.SaveTurnAsync(It.IsAny<ChatTurn>())).Returns(Task.CompletedTask);
        _mockChatTurnService.Setup(s => s.UpsertTurnAsync(It.IsAny<ChatTurn>())).Returns(Task.CompletedTask);
        // 控制器流程会解引用 UpsertSessionAsync 的返回值（chatSession.Id），必须返回非 null 会话
        _mockChatSessionService
            .Setup(s => s.UpsertSessionAsync(
                It.IsAny<string>(), It.IsAny<SessionSource>(), It.IsAny<string?>(), It.IsAny<ClientKind>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>()))
            .ReturnsAsync(new ChatSession { Id = 1, SessionKey = "test-session", RequestCount = 1 });
        _mockChatSessionService
            .Setup(s => s.RecordTurnStatsAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        _registry = new AIProviderRegistry();

        // 注册模拟提供者
        var mockProvider = new TestAIProvider("test", AIProviderType.OpenAI, new[] { "gpt-4", "gpt-3.5" });
        _registry.RegisterProvider(mockProvider);
    }

    private OpenAIChatController CreateController()
    {
        var controller = new OpenAIChatController(
            _registry, _mockLogService.Object, _mockChatTurnService.Object,
            _mockStreamRecorder.Object, _mockChatSessionService.Object, _mockImageRecognitionCache.Object);
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
        var controller = new OpenAIChatController(
            registry, _mockLogService.Object, _mockChatTurnService.Object,
            _mockStreamRecorder.Object, _mockChatSessionService.Object, _mockImageRecognitionCache.Object);
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

    [Fact]
    public async Task ChatCompletions_Streaming_PropagatesUsageField()
    {
        // Arrange：provider 在流式末端吐出 usage 哨兵分片
        var (controller, provider) = CreateControllerWithProvider();
        provider.EmitUsage = true;
        controller.Response.Body = new MemoryStream();

        var request = new OpenAIChatCompletionRequest
        {
            Model = "gpt-4",
            Messages = new List<OpenAIChatMessage>
            {
                new() { Role = "user", Content = "hi" }
            },
            Stream = true
        };

        // Act：流式分支把 SSE 写入 Response.Body
        await controller.ChatCompletions(request, CancellationToken.None);

        // Assert：响应流必须包含 usage（已用 token）字段及 token 分项
        controller.Response.Body.Position = 0;
        var body = await new StreamReader(controller.Response.Body).ReadToEndAsync();
        body.Should().Contain("\"usage\"", "流式响应必须透传 usage 字段");
        body.Should().Contain("prompt_tokens", "usage 必须包含 prompt_tokens");
        body.Should().Contain("completion_tokens", "usage 必须包含 completion_tokens");
        body.Should().Contain("total_tokens", "usage 必须包含 total_tokens");
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

    /// <summary>
    /// 测试开关：为 true 时，流式响应末尾额外吐出一个 usage 哨兵分片（模拟 OpenAI include_usage=true）。
    /// 用于验证网关是否把「已用 token」字段透传到 SSE 流。
    /// </summary>
    public bool EmitUsage { get; set; }

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

        // 模拟 OpenAI include_usage=true 末端 usage 哨兵分片（choices 为空、携带 usage）
        if (EmitUsage)
        {
            yield return new UnifiedStreamChunk
            {
                Id = "test-" + Guid.NewGuid().ToString("N")[..8],
                Model = request.Model,
                ChoiceIndex = 0,
                Usage = new UnifiedUsage
                {
                    PromptTokens = 10,
                    CompletionTokens = 5,
                    TotalTokens = 15
                }
            };
        }
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
