using ForgeSelf.Api.Services.AI;
using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// MultimodalProcessor 图片识别缓存行为测试（输入31：统一聊天接口多模态图片识别缓存）。
/// 覆盖：同会话同图命中缓存、会话间隔离、历史图+新图仅补识别新图、无会话/无缓存回退原行为、失败不缓存。
/// </summary>
public class MultimodalProcessorCacheTests
{
    private readonly string _cacheRoot;
    private readonly AIProviderRegistry _registry;
    private readonly AIProviderConfig _config;
    private readonly CountingVisionProvider _visionProvider;
    private readonly LocalFileImageRecognitionCache _cache;

    public MultimodalProcessorCacheTests()
    {
        _cacheRoot = Path.Combine(Path.GetTempPath(), "ofos-mm-cache-tests", Guid.NewGuid().ToString("N"));
        _registry = new AIProviderRegistry();
        _config = new AIProviderConfig
        {
            Name = "test-vision",
            VisionModel = "vision-x",
            EnableMultimodal = true,
            VisionPromptTemplate = "请描述这张图片"
        };
        _visionProvider = new CountingVisionProvider("vision-provider", AIProviderType.OpenAI, new[] { "vision-x" });
        _registry.RegisterProvider(_visionProvider);
        _cache = new LocalFileImageRecognitionCache(_cacheRoot);
    }

    private static UnifiedChatRequest RequestWithImages(params string[] urls)
    {
        var blocks = urls.Select(u => new ContentBlock { Type = "image_url", ImageUrl = u }).ToList();
        return new UnifiedChatRequest
        {
            Model = "main-model",
            Messages = new List<UnifiedChatMessage>
            {
                new() { Role = "user", Content = "请描述", ContentBlocks = blocks }
            }
        };
    }

    [Fact]
    public async Task SameSession_SameImage_SecondRequest_HitsCache()
    {
        var processor = new MultimodalProcessor(_registry, _config, _cache);

        await processor.ProcessAsync(RequestWithImages("https://example.com/a.jpg"), "session-1");
        _visionProvider.CallCount.Should().Be(1);

        var second = await processor.ProcessAsync(RequestWithImages("https://example.com/a.jpg"), "session-1");

        _visionProvider.CallCount.Should().Be(1, "同会话第二次请求相同图片应命中缓存，不再调用视觉模型");
        second.SystemPrompt.Should().Contain("[多模态图片识别结果]");
        second.SystemPrompt.Should().Contain("识别结果#1", "复用缓存内容而非新识别结果");
    }

    [Fact]
    public async Task DifferentSessions_DoNotShareCache()
    {
        var processor = new MultimodalProcessor(_registry, _config, _cache);

        await processor.ProcessAsync(RequestWithImages("https://example.com/a.jpg"), "session-1");
        await processor.ProcessAsync(RequestWithImages("https://example.com/a.jpg"), "session-2");

        _visionProvider.CallCount.Should().Be(2, "缓存按会话 id 分文件夹隔离，跨会话不共享");
    }

    [Fact]
    public async Task HistoryImagePlusNewImage_OnlyNewImageCallsVision()
    {
        var processor = new MultimodalProcessor(_registry, _config, _cache);

        await processor.ProcessAsync(RequestWithImages("https://example.com/a.jpg"), "session-1");
        _visionProvider.CallCount.Should().Be(1);

        // 模拟会话内新消息：携带历史图片 + 新增图片的完整上下文
        var second = await processor.ProcessAsync(
            RequestWithImages("https://example.com/a.jpg", "https://example.com/b.jpg"), "session-1");

        _visionProvider.CallCount.Should().Be(2, "历史图片命中缓存，仅新增图片调用视觉模型");
        second.SystemPrompt.Should().Contain("识别结果#1", "历史图片复用缓存结果");
        second.SystemPrompt.Should().Contain("识别结果#2", "新图片产生新识别结果");
    }

    [Fact]
    public async Task NoSessionId_CacheBypassed_EveryRequestCallsVision()
    {
        var processor = new MultimodalProcessor(_registry, _config, _cache);

        await processor.ProcessAsync(RequestWithImages("https://example.com/a.jpg"));
        await processor.ProcessAsync(RequestWithImages("https://example.com/a.jpg"));

        _visionProvider.CallCount.Should().Be(2, "未传会话 id 时不启用缓存，保持原有行为");
    }

    [Fact]
    public async Task NoCacheInjected_CacheBypassed_EveryRequestCallsVision()
    {
        var processor = new MultimodalProcessor(_registry, _config);

        await processor.ProcessAsync(RequestWithImages("https://example.com/a.jpg"), "session-1");
        await processor.ProcessAsync(RequestWithImages("https://example.com/a.jpg"), "session-1");

        _visionProvider.CallCount.Should().Be(2, "未注入缓存时保持原有行为");
    }

    [Fact]
    public async Task VisionFailure_NotCached_NextRequestRetries()
    {
        var processor = new MultimodalProcessor(_registry, _config, _cache);
        _visionProvider.FailNext = true;

        var first = await processor.ProcessAsync(RequestWithImages("https://example.com/a.jpg"), "session-1");

        first.SystemPrompt.Should().Contain("[图片识别失败", "识别失败时注入失败占位说明");
        _visionProvider.CallCount.Should().Be(0);

        await processor.ProcessAsync(RequestWithImages("https://example.com/a.jpg"), "session-1");

        _visionProvider.CallCount.Should().Be(1, "失败占位结果不写入缓存，下次请求应重试识别");
    }
}

/// <summary>
/// 计数视觉提供方：记录 ChatAsync 调用次数，返回带序号的识别结果（用于断言缓存复用），支持模拟失败。
/// </summary>
public class CountingVisionProvider : IAIProvider
{
    public string ProviderName { get; }
    public AIProviderType ProviderType { get; }
    public List<string> SupportedModels { get; }
    public bool IsDefault { get; set; }

    /// <summary>成功调用次数（失败不计入）。</summary>
    public int CallCount { get; private set; }

    /// <summary>为 true 时下一次 ChatAsync 抛异常并自动复位。</summary>
    public bool FailNext { get; set; }

    public CountingVisionProvider(string name, AIProviderType type, string[] models)
    {
        ProviderName = name;
        ProviderType = type;
        SupportedModels = models.ToList();
    }

    public Task<UnifiedChatResponse> ChatAsync(UnifiedChatRequest request, CancellationToken cancellationToken = default)
    {
        if (FailNext)
        {
            FailNext = false;
            throw new InvalidOperationException("模拟视觉模型失败");
        }

        CallCount++;
        return Task.FromResult(new UnifiedChatResponse
        {
            Id = "cnt-" + CallCount,
            Model = request.Model,
            Choices = new List<UnifiedChatMessage>
            {
                new() { Role = "assistant", Content = $"识别结果#{CallCount}" }
            }
        });
    }

    public async IAsyncEnumerable<UnifiedStreamChunk> ChatStreamAsync(UnifiedChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield return new UnifiedStreamChunk
        {
            Id = "cnt-stream",
            Model = request.Model,
            ChoiceIndex = 0,
            DeltaContent = "识别结果"
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
