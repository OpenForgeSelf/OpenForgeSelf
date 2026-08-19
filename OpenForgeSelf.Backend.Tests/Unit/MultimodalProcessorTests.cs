using OpenForgeSelf.Backend.Services.AI;
using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Backend.Services.AI.Models;

namespace OpenForgeSelf.Backend.Tests.Unit;

/// <summary>
/// 多模态处理器单元测试
/// </summary>
public class MultimodalProcessorTests
{
    private readonly AIProviderRegistry _registry;
    private readonly AIProviderConfig _config;

    public MultimodalProcessorTests()
    {
        _registry = new AIProviderRegistry();
        _config = new AIProviderConfig
        {
            Name = "test-vision",
            VisionModel = "gpt-4-vision",
            EnableMultimodal = true,
            VisionPromptTemplate = "请描述这张图片"
        };

        // 注册一个模拟的视觉提供者
        var visionProvider = new MockVisionProvider("vision-provider", AIProviderType.OpenAI, new[] { "gpt-4-vision" });
        _registry.RegisterProvider(visionProvider);
    }

    [Fact]
    public void HasImages_NoImages_ReturnsFalse()
    {
        // Arrange
        var processor = new MultimodalProcessor(_registry, _config);
        var request = new UnifiedChatRequest
        {
            Model = "gpt-4",
            Messages = new List<UnifiedChatMessage>
            {
                new() { Role = "user", Content = "Hello, how are you?" }
            }
        };

        // Act
        var result = processor.HasImages(request);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasImages_WithImageUrlInContentBlocks_ReturnsTrue()
    {
        // Arrange
        var processor = new MultimodalProcessor(_registry, _config);
        var request = new UnifiedChatRequest
        {
            Model = "gpt-4",
            Messages = new List<UnifiedChatMessage>
            {
                new()
                {
                    Role = "user",
                    ContentBlocks = new List<ContentBlock>
                    {
                        new() { Type = "text", Text = "描述这张图片" },
                        new() { Type = "image_url", ImageUrl = "https://example.com/image.jpg" }
                    }
                }
            }
        };

        // Act
        var result = processor.HasImages(request);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasImages_WithImageUrlInText_ReturnsTrue()
    {
        // Arrange
        var processor = new MultimodalProcessor(_registry, _config);
        var request = new UnifiedChatRequest
        {
            Model = "gpt-4",
            Messages = new List<UnifiedChatMessage>
            {
                new() { Role = "user", Content = "请看这张图片 https://example.com/photo.jpg 并描述它" }
            }
        };

        // Act
        var result = processor.HasImages(request);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasImages_WithBase64Image_ReturnsTrue()
    {
        // Arrange
        var processor = new MultimodalProcessor(_registry, _config);
        var request = new UnifiedChatRequest
        {
            Model = "gpt-4",
            Messages = new List<UnifiedChatMessage>
            {
                new()
                {
                    Role = "user",
                    ContentBlocks = new List<ContentBlock>
                    {
                        new() { Type = "image_base64", ImageBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==" }
                    }
                }
            }
        };

        // Act
        var result = processor.HasImages(request);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("https://example.com/image.jpg", true)]
    [InlineData("http://example.com/photo.png", true)]
    [InlineData("https://cdn.example.com/images/test.webp", true)]
    [InlineData("Just a plain text message", false)]
    [InlineData("Check out https://example.com/image.gif?size=large", true)]
    [InlineData("No images here", false)]
    [InlineData("https://example.com/video.mp4", false)]
    public void ContainsImageUrl_VariousUrls_ReturnsExpected(string text, bool expected)
    {
        // Act
        var result = MultimodalProcessor.ContainsImageUrl(text);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ExtractImageUrls_MultipleUrls_ExtractsAll()
    {
        // Arrange
        var text = "Image 1: https://example.com/1.jpg and Image 2: https://example.com/2.png";

        // Act
        var result = MultimodalProcessor.ExtractImageUrls(text);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain("https://example.com/1.jpg");
        result.Should().Contain("https://example.com/2.png");
    }

    [Fact]
    public void ExtractImageUrls_NoUrls_ReturnsEmptyList()
    {
        // Arrange
        var text = "No image URLs here";

        // Act
        var result = MultimodalProcessor.ExtractImageUrls(text);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractImageUrls_DuplicateUrls_ReturnsAll()
    {
        // 注意：ExtractImageUrls 方法返回所有匹配，不会去重
        // 去重应该在调用者层面处理
        var text = "Same image: https://example.com/image.jpg and again: https://example.com/image.jpg";

        // Act
        var result = MultimodalProcessor.ExtractImageUrls(text);

        // Assert - 方法返回所有匹配项
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ProcessAsync_NoImages_ReturnsOriginalRequest()
    {
        // Arrange
        var processor = new MultimodalProcessor(_registry, _config);
        var request = new UnifiedChatRequest
        {
            Model = "gpt-4",
            Messages = new List<UnifiedChatMessage>
            {
                new() { Role = "user", Content = "Hello" }
            }
        };

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Should().BeSameAs(request);
    }

    [Fact]
    public async Task ProcessAsync_WithImage_CallsVisionProvider()
    {
        // Arrange
        var processor = new MultimodalProcessor(_registry, _config);
        var request = new UnifiedChatRequest
        {
            Model = "gpt-4",
            Messages = new List<UnifiedChatMessage>
            {
                new()
                {
                    Role = "user",
                    ContentBlocks = new List<ContentBlock>
                    {
                        new() { Type = "text", Text = "描述这张图片" },
                        new() { Type = "image_url", ImageUrl = "https://example.com/test.jpg" }
                    }
                }
            }
        };

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.SystemPrompt.Should().Contain("[多模态图片识别结果]");
    }

    [Fact]
    public async Task ProcessAsync_MultipleImages_ProcessesAll()
    {
        // Arrange
        var processor = new MultimodalProcessor(_registry, _config);
        var request = new UnifiedChatRequest
        {
            Model = "gpt-4",
            Messages = new List<UnifiedChatMessage>
            {
                new()
                {
                    Role = "user",
                    ContentBlocks = new List<ContentBlock>
                    {
                        new() { Type = "image_url", ImageUrl = "https://example.com/1.jpg" },
                        new() { Type = "image_url", ImageUrl = "https://example.com/2.jpg" },
                        new() { Type = "image_url", ImageUrl = "https://example.com/3.jpg" }
                    }
                }
            }
        };

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.SystemPrompt.Should().Contain("3 张图片");
    }

    [Fact]
    public void Constructor_NoVisionModel_SetsVisionProviderToNull()
    {
        // Arrange
        var config = new AIProviderConfig
        {
            Name = "test",
            VisionModel = null
        };

        // Act
        var processor = new MultimodalProcessor(_registry, config);

        // Assert
        // processor 初始化成功，不抛出异常
    }

    [Fact]
    public void Constructor_VisionModelNotFound_DoesNotThrow()
    {
        // Arrange
        var config = new AIProviderConfig
        {
            Name = "test",
            VisionModel = "nonexistent-model"
        };

        // Act
        var processor = new MultimodalProcessor(_registry, config);

        // Assert - 不抛出异常
    }

    [Fact]
    public async Task ProcessAsync_RemovesImageUrlsFromContent()
    {
        // Arrange
        var processor = new MultimodalProcessor(_registry, _config);
        var request = new UnifiedChatRequest
        {
            Model = "gpt-4",
            Messages = new List<UnifiedChatMessage>
            {
                new() { Role = "user", Content = "看这个 https://example.com/image.jpg 图片" }
            }
        };

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        var userMessage = result.Messages.FirstOrDefault(m => m.Role == "user");
        userMessage.Should().NotBeNull();
        userMessage!.Content.Should().NotContain("https://example.com/image.jpg");
        userMessage.Content.Should().Contain("[图片]");
    }

    [Fact]
    public async Task ProcessAsync_VisionModelWithProviderPrefix_RoutesAndStripsPrefix()
    {
        // 验证 VisionModel="default:qwen/qwen3-vl-4b" 既能经前缀路由到 default 提供方，
        // 又能在上游请求中剥离 "default:" 前缀，仅用裸上游模型 id（否则上游不识别）。
        var capturing = new CapturingVisionProvider("default", AIProviderType.OpenAI, new[] { "qwythos-9b-v2" });
        _registry.RegisterProvider(capturing);

        var prefixedConfig = new AIProviderConfig
        {
            Name = "gpustack",
            VisionModel = "default:qwen/qwen3-vl-4b",
            EnableMultimodal = true,
            VisionPromptTemplate = "请描述这张图片"
        };

        var processor = new MultimodalProcessor(_registry, prefixedConfig);
        var request = new UnifiedChatRequest
        {
            Model = "qwythos-9b-v2",
            Messages = new List<UnifiedChatMessage>
            {
                new() { Role = "user", ContentBlocks = new List<ContentBlock>
                {
                    new() { Type = "image_url", ImageUrl = "https://example.com/test.jpg" }
                }}
            }
        };

        // Act
        var result = await processor.ProcessAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.SystemPrompt.Should().Contain("[多模态图片识别结果]");
        // 上游收到的必须是裸模型 id，而非带 provider: 前缀
        capturing.LastModel.Should().Be("qwen/qwen3-vl-4b");
    }
}

/// <summary>
/// Mock Vision Provider for testing
/// </summary>
public class MockVisionProvider : IAIProvider
{
    public string ProviderName { get; }
    public AIProviderType ProviderType { get; }
    public List<string> SupportedModels { get; }
    public bool IsDefault { get; set; }

    public MockVisionProvider(string name, AIProviderType type, string[] models)
    {
        ProviderName = name;
        ProviderType = type;
        SupportedModels = models.ToList();
    }

    public Task<UnifiedChatResponse> ChatAsync(UnifiedChatRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new UnifiedChatResponse
        {
            Id = "vision-" + Guid.NewGuid().ToString("N")[..8],
            Model = request.Model,
            Choices = new List<UnifiedChatMessage>
            {
                new() { Role = "assistant", Content = "这张图片显示了一个测试场景，包含建筑物和天空。" }
            }
        });
    }

    public async IAsyncEnumerable<UnifiedStreamChunk> ChatStreamAsync(UnifiedChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield return new UnifiedStreamChunk
        {
            Id = "vision-" + Guid.NewGuid().ToString("N")[..8],
            Model = request.Model,
            ChoiceIndex = 0,
            DeltaContent = "这张图片显示了一个测试场景，包含建筑物和天空。"
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

/// <summary>
/// 捕获上游请求模型名的 Mock 视觉提供方，用于验证 VisionModel 前缀剥离。
/// </summary>
public class CapturingVisionProvider : IAIProvider
{
    public string ProviderName { get; }
    public AIProviderType ProviderType { get; }
    public List<string> SupportedModels { get; }
    public bool IsDefault { get; set; }
    public string? LastModel { get; private set; }

    public CapturingVisionProvider(string name, AIProviderType type, string[] models)
    {
        ProviderName = name;
        ProviderType = type;
        SupportedModels = models.ToList();
    }

    public Task<UnifiedChatResponse> ChatAsync(UnifiedChatRequest request, CancellationToken cancellationToken = default)
    {
        LastModel = request.Model;
        return Task.FromResult(new UnifiedChatResponse
        {
            Id = "cap-" + Guid.NewGuid().ToString("N")[..8],
            Model = request.Model,
            Choices = new List<UnifiedChatMessage> { new() { Role = "assistant", Content = "识别结果" } }
        });
    }

    public async IAsyncEnumerable<UnifiedStreamChunk> ChatStreamAsync(UnifiedChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield return new UnifiedStreamChunk { Id = "cap", Model = request.Model, ChoiceIndex = 0, DeltaContent = "识别结果" };
    }

    public Task<List<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SupportedModels.Select(m => new ModelInfo
        {
            Id = m,
            Name = m,
            Owner = ProviderName,
            Created = 0,
            ProviderName = ProviderName
        }).ToList());
}
