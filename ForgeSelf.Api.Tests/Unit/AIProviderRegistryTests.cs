using ForgeSelf.Api.Services.AI;
using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// AI提供者注册表单元测试
/// </summary>
public class AIProviderRegistryTests
{
    private readonly AIProviderRegistry _registry;

    public AIProviderRegistryTests()
    {
        _registry = new AIProviderRegistry();
    }

    [Fact]
    public void RegisterProvider_ValidProvider_ShouldAddToList()
    {
        // Arrange
        var mockProvider = new MockAIProvider("test-provider", AIProviderType.OpenAI, new[] { "gpt-4", "gpt-3.5" });

        // Act
        _registry.RegisterProvider(mockProvider);

        // Assert
        var providers = _registry.GetAllProviders();
        providers.Should().HaveCount(1);
        providers[0].ProviderName.Should().Be("test-provider");
    }

    [Fact]
    public void RegisterProvider_DuplicateName_ShouldNotAdd()
    {
        // Arrange
        var provider1 = new MockAIProvider("test-provider", AIProviderType.OpenAI, new[] { "gpt-4" });
        var provider2 = new MockAIProvider("test-provider", AIProviderType.OpenAI, new[] { "claude-3" });

        // Act
        _registry.RegisterProvider(provider1);
        _registry.RegisterProvider(provider2);

        // Assert
        var providers = _registry.GetAllProviders();
        providers.Should().HaveCount(1);
    }

    [Fact]
    public void RegisterProvider_CaseInsensitive_ShouldTreatAsSame()
    {
        // Arrange
        var provider1 = new MockAIProvider("TestProvider", AIProviderType.OpenAI, new[] { "gpt-4" });
        var provider2 = new MockAIProvider("TESTPROVIDER", AIProviderType.OpenAI, new[] { "claude-3" });

        // Act
        _registry.RegisterProvider(provider1);
        _registry.RegisterProvider(provider2);

        // Assert
        var providers = _registry.GetAllProviders();
        providers.Should().HaveCount(1);
    }

    [Fact]
    public void GetProviderByName_ExistingProvider_ShouldReturnProvider()
    {
        // Arrange
        var mockProvider = new MockAIProvider("openai", AIProviderType.OpenAI, new[] { "gpt-4" });
        _registry.RegisterProvider(mockProvider);

        // Act
        var result = _registry.GetProviderByName("openai");

        // Assert
        result.Should().NotBeNull();
        result!.ProviderName.Should().Be("openai");
    }

    [Fact]
    public void GetProviderByName_NonExistingProvider_ShouldReturnNull()
    {
        // Act
        var result = _registry.GetProviderByName("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetProviderByModel_ExactMatch_ShouldReturnProvider()
    {
        // Arrange
        var provider1 = new MockAIProvider("openai", AIProviderType.OpenAI, new[] { "gpt-4", "gpt-3.5" });
        var provider2 = new MockAIProvider("anthropic", AIProviderType.Anthropic, new[] { "claude-3" });
        _registry.RegisterProvider(provider1);
        _registry.RegisterProvider(provider2);

        // Act
        var result = _registry.GetProviderByModel("gpt-4");

        // Assert
        result.Should().NotBeNull();
        result!.ProviderName.Should().Be("openai");
    }

    [Fact]
    public void GetProviderByModel_PrefixMatch_ShouldReturnProvider()
    {
        // Arrange
        var provider = new MockAIProvider("openai", AIProviderType.OpenAI, new[] { "gpt-4" });
        _registry.RegisterProvider(provider);

        // Act
        var result = _registry.GetProviderByModel("gpt-4-turbo");

        // Assert
        result.Should().NotBeNull();
        result!.ProviderName.Should().Be("openai");
    }

    [Fact]
    public void GetProviderByModel_Wildcard_ShouldReturnDefault()
    {
        // Arrange
        var provider = new MockAIProvider("openai", AIProviderType.OpenAI, new[] { "gpt-4" });
        _registry.RegisterProvider(provider);

        // Act
        var result = _registry.GetProviderByModel("*");

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void GetProviderByModel_Empty_ShouldReturnDefault()
    {
        // Arrange
        var provider = new MockAIProvider("openai", AIProviderType.OpenAI, new[] { "gpt-4" });
        _registry.RegisterProvider(provider);

        // Act
        var result = _registry.GetProviderByModel("");

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void GetDefaultProvider_WithDefaultFlag_ShouldReturnProvider()
    {
        // Arrange
        var provider1 = new MockAIProvider("openai", AIProviderType.OpenAI, new[] { "gpt-4" });
        var provider2 = new MockAIProvider("anthropic", AIProviderType.Anthropic, new[] { "claude-3" });
        provider2.IsDefault = true;
        _registry.RegisterProvider(provider1);
        _registry.RegisterProvider(provider2);

        // Act
        var result = _registry.GetDefaultProvider();

        // Assert
        result.Should().NotBeNull();
        result!.ProviderName.Should().Be("anthropic");
    }

    [Fact]
    public void GetDefaultProvider_NoDefaultFlag_ShouldReturnFirst()
    {
        // Arrange
        var provider1 = new MockAIProvider("openai", AIProviderType.OpenAI, new[] { "gpt-4" });
        var provider2 = new MockAIProvider("anthropic", AIProviderType.Anthropic, new[] { "claude-3" });
        _registry.RegisterProvider(provider1);
        _registry.RegisterProvider(provider2);

        // Act
        var result = _registry.GetDefaultProvider();

        // Assert
        result.Should().NotBeNull();
        result!.ProviderName.Should().Be("openai");
    }

    [Fact]
    public void GetAllProviders_ShouldReturnCopy()
    {
        // Arrange
        var provider1 = new MockAIProvider("openai", AIProviderType.OpenAI, new[] { "gpt-4" });
        var provider2 = new MockAIProvider("anthropic", AIProviderType.Anthropic, new[] { "claude-3" });
        _registry.RegisterProvider(provider1);
        _registry.RegisterProvider(provider2);

        // Act
        var providers = _registry.GetAllProviders();
        providers.Clear();

        // Assert - 原始列表应该不受影响
        _registry.GetAllProviders().Should().HaveCount(2);
    }

    [Fact]
    public void GetModelById_ExistingModel_ShouldReturnModelInfo()
    {
        // Arrange
        var provider = new MockAIProvider("openai", AIProviderType.OpenAI, new[] { "gpt-4" });
        _registry.RegisterProvider(provider);

        // Act
        var result = _registry.GetModelById("gpt-4");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("gpt-4");
        result.Owner.Should().Be("openai");
        result.ProviderName.Should().Be("openai");
    }

    [Fact]
    public void GetModelById_NonExistingModel_ShouldReturnNull()
    {
        // Arrange
        var provider = new MockAIProvider("openai", AIProviderType.OpenAI, new[] { "gpt-4" });
        _registry.RegisterProvider(provider);

        // Act
        var result = _registry.GetModelById("nonexistent-model");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllModelsAsync_ShouldAggregateModelsFromAllProviders()
    {
        // Arrange
        var provider1 = new MockAIProvider("openai", AIProviderType.OpenAI, new[] { "gpt-4", "gpt-3.5" });
        var provider2 = new MockAIProvider("anthropic", AIProviderType.Anthropic, new[] { "claude-3" });
        _registry.RegisterProvider(provider1);
        _registry.RegisterProvider(provider2);

        // Act
        var result = await _registry.GetAllModelsAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public void GetProviderByModel_ProviderPrefix_ShouldRouteToNamedProvider()
    {
        // 验证 "提供商:上游模型id" 前缀能显式路由到指定提供方（多模态视觉模型场景）：
        // 视觉模型不在任何提供方 SupportedModels 时，前缀仍能命中命名提供方。
        var local = new MockAIProvider("default", AIProviderType.OpenAI, new[] { "qwythos-9b-v2" });
        var remote = new MockAIProvider("gpustack", AIProviderType.OpenAI, new[] { "qwen3-vl" });
        remote.IsDefault = true;
        _registry.RegisterProvider(local);
        _registry.RegisterProvider(remote);

        // Act
        var result = _registry.GetProviderByModel("default:qwen/qwen3-vl-4b");

        // Assert
        result.Should().NotBeNull();
        result!.ProviderName.Should().Be("default");
    }

    [Fact]
    public void GetProviderByModel_ProviderPrefixUnknownProvider_FallsBackToDefault()
    {
        // 前缀中的提供方不存在时，应回退到默认提供方，而非抛异常。
        var remote = new MockAIProvider("gpustack", AIProviderType.OpenAI, new[] { "qwen3-vl" });
        remote.IsDefault = true;
        _registry.RegisterProvider(remote);

        // Act
        var result = _registry.GetProviderByModel("unknownprovider:qwen/qwen3-vl-4b");

        // Assert
        result.Should().NotBeNull();
        result!.ProviderName.Should().Be("gpustack");
    }
}

/// <summary>
/// Mock AI Provider for testing
/// </summary>
public class MockAIProvider : IAIProvider
{
    public string ProviderName { get; }
    public AIProviderType ProviderType { get; }
    public List<string> SupportedModels { get; }
    public bool IsDefault { get; set; }

    public MockAIProvider(string name, AIProviderType type, string[] models)
    {
        ProviderName = name;
        ProviderType = type;
        SupportedModels = models.ToList();
    }

    public Task<UnifiedChatResponse> ChatAsync(UnifiedChatRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new UnifiedChatResponse
        {
            Id = "mock-" + Guid.NewGuid().ToString("N")[..8],
            Model = request.Model,
            Choices = new List<UnifiedChatMessage>
            {
                new() { Role = "assistant", Content = "Mock response" }
            }
        });
    }

    public async IAsyncEnumerable<UnifiedStreamChunk> ChatStreamAsync(UnifiedChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield return new UnifiedStreamChunk
        {
            Id = "mock-" + Guid.NewGuid().ToString("N")[..8],
            Model = request.Model,
            ChoiceIndex = 0,
            DeltaContent = "Mock stream response"
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
