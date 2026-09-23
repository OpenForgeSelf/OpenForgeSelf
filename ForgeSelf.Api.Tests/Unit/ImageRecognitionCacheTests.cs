using ForgeSelf.Api.Services.AI;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// 图片识别本地缓存单元测试（输入31：统一聊天接口多模态图片识别缓存）。
/// </summary>
public class ImageRecognitionCacheTests
{
    private readonly string _root;

    public ImageRecognitionCacheTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ofos-img-cache-tests", Guid.NewGuid().ToString("N"));
    }

    private LocalFileImageRecognitionCache CreateCache() => new(_root);

    [Fact]
    public async Task SetAsync_ThenTryGet_ReturnsCachedResult()
    {
        var cache = CreateCache();

        await cache.SetAsync("session-1", "key-a", "图片里是一只猫");
        var result = await cache.TryGetAsync("session-1", "key-a");

        result.Should().Be("图片里是一只猫");
    }

    [Fact]
    public async Task TryGet_UnknownKey_ReturnsNull()
    {
        var cache = CreateCache();

        var result = await cache.TryGetAsync("session-1", "never-set");

        result.Should().BeNull();
    }

    [Fact]
    public async Task DifferentSessions_AreIsolated()
    {
        var cache = CreateCache();

        await cache.SetAsync("session-1", "key-a", "结果A");

        (await cache.TryGetAsync("session-2", "key-a")).Should().BeNull();
        (await cache.TryGetAsync("session-1", "key-a")).Should().Be("结果A");
    }

    [Fact]
    public async Task SetAsync_SessionIdWithPathSeparators_StaysUnderRoot()
    {
        var cache = CreateCache();
        var malicious = "..\\..\\evil/session";

        await cache.SetAsync(malicious, "key-a", "结果");

        // 缓存文件必须仍位于根目录之内（防止路径穿越）
        var rootFull = new DirectoryInfo(_root).FullName;
        var files = Directory.GetFiles(_root, "*", SearchOption.AllDirectories);
        files.Should().NotBeEmpty();
        foreach (var f in files)
        {
            Path.GetFullPath(f).Should().StartWith(rootFull);
        }

        // 同键可读回（清洗后会话名保持一致）
        (await cache.TryGetAsync(malicious, "key-a")).Should().Be("结果");
    }

    [Fact]
    public async Task SetAsync_SameKeyTwice_LastWriteWins()
    {
        var cache = CreateCache();

        await cache.SetAsync("s", "k", "旧结果");
        await cache.SetAsync("s", "k", "新结果");

        (await cache.TryGetAsync("s", "k")).Should().Be("新结果");
    }

    [Fact]
    public async Task TryGet_CorruptedCacheFile_ReturnsNullInsteadOfThrowing()
    {
        var cache = CreateCache();
        await cache.SetAsync("s", "k", "正常结果");

        // 找到缓存文件并写入垃圾内容模拟损坏
        var file = Directory.GetFiles(_root, "k.json", SearchOption.AllDirectories).Single();
        await File.WriteAllTextAsync(file, "{这不是合法 JSON");

        var result = await cache.TryGetAsync("s", "k");
        result.Should().BeNull();
    }

    [Fact]
    public void ComputeKey_SameUrlImage_SameKey()
    {
        var img1 = new ImageInfo { Source = "url", Url = "https://example.com/a.jpg", Detail = "auto" };
        var img2 = new ImageInfo { Source = "url", Url = "https://example.com/a.jpg", Detail = "auto" };

        ImageRecognitionCacheKey.Compute(img1, "vision-model")
            .Should().Be(ImageRecognitionCacheKey.Compute(img2, "vision-model"));
    }

    [Fact]
    public void ComputeKey_DifferentUrl_DifferentKey()
    {
        var img1 = new ImageInfo { Source = "url", Url = "https://example.com/a.jpg" };
        var img2 = new ImageInfo { Source = "url", Url = "https://example.com/b.jpg" };

        ImageRecognitionCacheKey.Compute(img1).Should().NotBe(ImageRecognitionCacheKey.Compute(img2));
    }

    [Fact]
    public void ComputeKey_DifferentVisionModel_DifferentKey()
    {
        var img = new ImageInfo { Source = "url", Url = "https://example.com/a.jpg" };

        ImageRecognitionCacheKey.Compute(img, "model-a")
            .Should().NotBe(ImageRecognitionCacheKey.Compute(img, "model-b"));
    }

    [Fact]
    public void ComputeKey_Base64AndUrl_DifferentKey()
    {
        var url = new ImageInfo { Source = "url", Url = "https://example.com/a.jpg" };
        var b64 = new ImageInfo { Source = "base64", Base64Data = "aGVsbG8=", MediaType = "image/png" };

        ImageRecognitionCacheKey.Compute(url).Should().NotBe(ImageRecognitionCacheKey.Compute(b64));
    }
}
