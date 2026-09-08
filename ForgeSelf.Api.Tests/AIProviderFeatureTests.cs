using System.Net.Http;
using ForgeSelf.Abstractions;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using ForgeSelf.Api.Controllers;
using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Security;
using ForgeSelf.Api.Services;
using ForgeSelf.Api.Services.AI;

namespace ForgeSelf.Api.Tests;

/// <summary>
/// AI Provider 配置数据库化特性测试：覆盖 T012（写后存活）、T017（播种幂等）、
/// T019/T020/T021（掩码、留空保留、加密往返）及注册表重载。
/// 使用 XCodeTestFixture 提供的临时 SQLite 库，.ApiKey 经加密服务落库。
/// </summary>
[Collection("XCode")]
public class AIProviderFeatureTests : IClassFixture<XCodeTestFixture>
{
    public AIProviderFeatureTests(XCodeTestFixture fixture)
    {
        _ = fixture;
    }

    private sealed class TestCtx
    {
        public AIProviderController Controller { get; init; } = null!;
        public AIProviderRegistry Registry { get; init; } = null!;
        public IAIProviderService Service { get; init; } = null!;
        public ISecretEncryptionService Encryption { get; init; } = null!;
    }

    private static TestCtx MakeCtx()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Encryption:Key"] = "test-encryption-key-0123456789" })
            .Build();
        var encryption = new AesSecretEncryptionService(config);
        var repo = new AIProviderRepository(encryption);
        var modelService = new AIModelService(repo, encryption, new TestHttpClientFactory(), new NullLogService());
        var service = new AIProviderService(repo, encryption, new TestHttpClientFactory(), modelService);
        var registry = new AIProviderRegistry();
        var httpClientFactory = new TestHttpClientFactory();
        var log = new NullLogService();
        var controller = new AIProviderController(service, encryption, registry, httpClientFactory, log, modelService);
        return new TestCtx { Controller = controller, Registry = registry, Service = service, Encryption = encryption };
    }

    private static void ClearTable()
    {
        foreach (var e in AIProvider.FindAll()) e.Delete();
    }

    private static AIProviderRequest SampleRequest(string? apiKey = "sk-secret-123") => new(
        Name: "OpenAI",
        ProviderType: "OpenAI",
        Endpoint: "https://api.openai.com/v1",
        ApiKey: apiKey,
        SupportedModels: new() { "gpt-4o", "gpt-4o-mini" },
        IsDefault: true,
        TimeoutSeconds: 120,
        VisionModel: "gpt-4o",
        EnableMultimodal: true,
        VisionPromptTemplate: "describe the image");

    private static string? ResultToJson(IActionResult result)
    {
        if (result is not ObjectResult or) return null;
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        return JsonSerializer.Serialize(or.Value, opts);
    }

    // T012：写后存活——POST 后注册表立即包含新提供方，无需重启
    [Fact]
    public async Task Create_ReloadsRegistry_AndDbStoresEncryptedKey()
    {
        ClearTable();
        var ctx = MakeCtx();

        var result = await ctx.Controller.Create(SampleRequest());
        result.Should().BeOfType<CreatedAtActionResult>();

        // 注册表已包含新提供方（保存即生效）
        ctx.Registry.GetAllProviders().Should().ContainSingle(p => p.ProviderName == "OpenAI");

        // DB 中 ApiKey 为密文，且可解密还原（T018 加密落库）
        var stored = AIProvider.FindAll().Should().ContainSingle().Subject;
        stored.ApiKey.Should().NotBe("sk-secret-123");
        ctx.Encryption.Decrypt(stored.ApiKey).Should().Be("sk-secret-123");

        // 响应仅含掩码，绝不明文（T019）
        var json = ResultToJson(result)!;
        json.Should().Contain("apiKeyMasked");
        json.Should().NotContain("sk-secret-123");
    }

    // T019：列表/详情仅返回掩码
    [Fact]
    public async Task GetAll_ReturnsMaskedKey_NoPlaintext()
    {
        ClearTable();
        var ctx = MakeCtx();
        await ctx.Controller.Create(SampleRequest());

        var json = ResultToJson(ctx.Controller.GetAll())!;
        json.Should().Contain("apiKeyMasked");
        json.Should().NotContain("sk-secret-123");
    }

    // T020：编辑留空 ApiKey 保留原密文
    [Fact]
    public async Task Update_WithEmptyApiKey_KeepsExistingCipher()
    {
        ClearTable();
        var ctx = MakeCtx();
        await ctx.Controller.Create(SampleRequest());
        var id = ctx.Service.GetAll().Should().ContainSingle().Subject.Id;

        var updateReq = SampleRequest(apiKey: "");
        var result = await ctx.Controller.Update(id, updateReq);
        result.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(200);

        var stored = AIProvider.FindById(id)!;
        ctx.Encryption.Decrypt(stored.ApiKey).Should().Be("sk-secret-123");
    }

    // T017：播种幂等——DB 空时写入一条，再次调用不重复
    [Fact]
    public void EnsureSeeded_IsIdempotent()
    {
        ClearTable();
        var ctx = MakeCtx();
        var seed = new AIProviderConfig
        {
            Name = "default",
            ProviderType = AIProviderType.OpenAI,
            Endpoint = "https://api.example.com/v1",
            ApiKey = "sk-seed",
            IsDefault = true,
            SupportedModels = new() { "gpt-4o" }
        };

        ctx.Service.EnsureSeeded(new[] { seed });
        ctx.Service.GetAll().Should().HaveCount(1);

        ctx.Service.EnsureSeeded(new[] { seed });
        ctx.Service.GetAll().Should().HaveCount(1);

        var stored = AIProvider.FindAll().Should().ContainSingle().Subject;
        ctx.Encryption.Decrypt(stored.ApiKey).Should().Be("sk-seed");
        stored.IsDefault.Should().BeTrue();
    }

    // T021：加密往返 + 掩码格式
    [Fact]
    public void Encryption_RoundTrip_And_Mask()
    {
        var ctx = MakeCtx();
        var plain = "sk-abcdef123456";
        var cipher = ctx.Encryption.Encrypt(plain);
        cipher.Should().NotBe(plain);
        ctx.Encryption.Decrypt(cipher).Should().Be(plain);

        var masked = ctx.Encryption.Mask(plain);
        masked.Should().StartWith("sk-");
        masked.Should().Contain("****");
        masked.Should().EndWith("3456");
        masked.Should().NotBe(plain);
    }

    // US2（T015 简化）：ReloadAsync 从配置列表原子重建注册表
    [Fact]
    public async Task ReloadAsync_PopulatesRegistryFromConfigs()
    {
        var ctx = MakeCtx();
        var configs = new List<AIProviderConfig>
        {
            new() { Name = "A", ProviderType = AIProviderType.OpenAI, Endpoint = "https://a/v1", ApiKey = "k", SupportedModels = new() { "m1" } },
            new() { Name = "B", ProviderType = AIProviderType.Custom, Endpoint = "https://b/v1", ApiKey = "k", SupportedModels = new() { "m2" } }
        };

        await ctx.Registry.ReloadAsync(new TestHttpClientFactory(), configs);

        ctx.Registry.GetAllProviders().Should().HaveCount(2);
        ctx.Registry.GetProviderByName("A").Should().NotBeNull();
        ctx.Registry.GetProviderByName("B").Should().NotBeNull();
    }

    // 契约：删除唯一默认提供方应拒绝（400）
    [Fact]
    public async Task Delete_DefaultWithNoAlternative_Rejected()
    {
        ClearTable();
        var ctx = MakeCtx();
        await ctx.Controller.Create(SampleRequest());
        var id = ctx.Service.GetAll().Should().ContainSingle().Subject.Id;

        var result = await ctx.Controller.Delete(id);
        result.Should().BeOfType<BadRequestObjectResult>();
        ctx.Service.GetAll().Should().HaveCount(1);
    }

    // 契约：新增重名应拒绝（400）
    [Fact]
    public async Task Create_DuplicateName_Rejected()
    {
        ClearTable();
        var ctx = MakeCtx();
        await ctx.Controller.Create(SampleRequest());

        var dup = SampleRequest(apiKey: "sk-another") with { Endpoint = "https://other/v1" };
        var result = await ctx.Controller.Create(dup);
        result.Should().BeOfType<BadRequestObjectResult>();
    }
}

/// <summary>测试用 IHttpClientFactory，每次返回新 HttpClient</summary>
public class TestHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new HttpClient();
    public HttpClient CreateClient() => new HttpClient();
}

/// <summary>无操作日志服务，供测试使用</summary>
public class NullLogService : ILogService
{
    public void Info(string message, params object[] args) { }
    public void Warn(string message, params object[] args) { }
    public void Error(string message, params object[] args) { }
    public void Debug(string message, params object[] args) { }
}
