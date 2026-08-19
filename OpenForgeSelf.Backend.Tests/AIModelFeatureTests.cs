using System.Net.Http;
using OpenForgeSelf.Abstractions;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using OpenForgeSelf.Backend.Controllers;
using OpenForgeSelf.Backend.Entities;
using OpenForgeSelf.Backend.Security;
using OpenForgeSelf.Backend.Services;
using OpenForgeSelf.Backend.Services.AI;
using OpenForgeSelf.Backend.Services.AI.Models;
using XCode;

namespace OpenForgeSelf.Backend.Tests;

/// <summary>
/// AIModel 特性测试：覆盖 T011（fetch upsert 统计/401 保留/空列表）、
/// T018（GetGroups 分组/空组/enabledOnly）、T024（SetEnabled/UpdateEditable 锁定字段/重拉保留/级联删除）、
/// T025/T030（路由禁用模型不返回）。
/// 复用 XCodeTestFixture 的临时 SQLite 库；上游 HTTP 以 FakeHandler 桩接，无需真实网络。
/// </summary>
[Collection("XCode")]
public class AIModelFeatureTests : IClassFixture<XCodeTestFixture>
{
    public AIModelFeatureTests(XCodeTestFixture fixture)
    {
        _ = fixture;
    }

    private static readonly IConfiguration Config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Encryption:Key"] = "test-encryption-key-0123456789" })
        .Build();

    private static ISecretEncryptionService Encryption => new AesSecretEncryptionService(Config);

    private static AIProvider InsertProvider(string name = "OpenAI", string type = "OpenAI", string endpoint = "https://api.openai.com/v1")
    {
        var enc = Encryption;
        var p = new AIProvider
        {
            Name = name,
            ProviderType = type,
            Endpoint = endpoint,
            ApiKey = enc.Encrypt("sk-test"),
            SupportedModels = "gpt-4o",
            IsDefault = true,
            TimeoutSeconds = 120,
            EnableMultimodal = true,
            CreateTime = DateTime.Now,
            UpdateTime = DateTime.Now,
        };
        p.Insert();
        return p;
    }

    private static void ClearAll()
    {
        foreach (var m in AIModel.FindAll()) m.Delete();
        foreach (var p in AIProvider.FindAll()) p.Delete();
    }

    private static AIModelService MakeService(HttpMessageHandler? handler = null)
    {
        var enc = Encryption;
        var repo = new AIProviderRepository(enc);
        IHttpClientFactory factory = handler == null
            ? new TestHttpClientFactory()
            : new HandlerHttpClientFactory(handler);
        return new AIModelService(repo, enc, factory, new NullLogService());
    }

    // 构造 OpenAI /models 风格响应
    private static string ModelsJson(params (string id, string owner)[] models)
    {
        var obj = new
        {
            @object = "list",
            data = models.Select(m => new { id = m.id, @object = "model", owned_by = m.owner }).ToArray(),
        };
        return JsonSerializer.Serialize(obj);
    }

    private static HttpMessageHandler OpenAiHandler(string json) => new FakeHandler(_ =>
        new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

    // ===== T011：fetch upsert 统计 =====
    [Fact]
    public async Task FetchForProvider_Upserts_AndReportsStats()
    {
        ClearAll();
        var provider = InsertProvider();
        var svc = MakeService(OpenAiHandler(ModelsJson(("gpt-4o", "openai"), ("gpt-4o-mini", "openai"))));

        var r = await svc.FetchForProviderAsync(provider.Id);

        r.Fetched.Should().Be(2);
        r.Added.Should().Be(2);
        r.Updated.Should().Be(0);
        r.Models.Should().HaveCount(2);
        AIModel.FindAllByProviderId(provider.Id).Should().HaveCount(2);
        AIModel.FindByProviderNameAndUpstreamModelId("OpenAI", "gpt-4o")!.ChatModelId.Should().Be("OpenAI:gpt-4o");
    }

    [Fact]
    public async Task FetchForProvider_SecondRun_UpdatesAndKeeps()
    {
        ClearAll();
        var provider = InsertProvider();
        var svc1 = MakeService(OpenAiHandler(ModelsJson(("gpt-4o", "openai"), ("gpt-4o-mini", "openai"))));
        await svc1.FetchForProviderAsync(provider.Id);

        // 上游仅返回 gpt-4o（gpt-4o-mini 不再返回）
        var svc2 = MakeService(OpenAiHandler(ModelsJson(("gpt-4o", "openai"))));
        var r2 = await svc2.FetchForProviderAsync(provider.Id);

        r2.Fetched.Should().Be(1);
        r2.Updated.Should().Be(1);
        r2.Kept.Should().Be(1); // 库中有但上游未返回 -> 保留
        AIModel.FindAllByProviderId(provider.Id).Should().HaveCount(2); // 不删除保留项
    }

    [Fact]
    public async Task FetchForProvider_EmptyList_FetchedZero_NoInsert()
    {
        ClearAll();
        var provider = InsertProvider();
        var svc = MakeService(OpenAiHandler(ModelsJson()));

        var r = await svc.FetchForProviderAsync(provider.Id);

        r.Fetched.Should().Be(0);
        r.Added.Should().Be(0);
        AIModel.FindAllByProviderId(provider.Id).Should().BeEmpty();
    }

    [Fact]
    public async Task FetchModels_Upstream401_Returns502_KeepsExisting()
    {
        ClearAll();
        var provider = InsertProvider();
        // 预存一条既有模型
        new AIModel
        {
            ProviderId = provider.Id,
            ProviderName = "OpenAI",
            UpstreamModelId = "gpt-legacy",
            ChatModelId = "OpenAI:gpt-legacy",
            Enabled = true,
            CreateTime = DateTime.Now,
            UpdateTime = DateTime.Now,
        }.Insert();

        var svc = MakeService(new FakeHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized)));
        var repo = new AIProviderRepository(Encryption);
        var ctrl = new AIProviderController(
            new AIProviderService(repo, Encryption, new TestHttpClientFactory(), svc), Encryption, new AIProviderRegistry(),
            new TestHttpClientFactory(), new NullLogService(), svc);

        var result = await ctrl.FetchModels(provider.Id);
        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.ObjectResult>().Which.StatusCode.Should().Be(502);

        // 既有保留，未新增
        AIModel.FindAllByProviderId(provider.Id).Should().ContainSingle();
    }

    // ===== T018：GetGroups 分组 =====
    [Fact]
    public void GetGroups_GroupsByProvider_EmptyGroupHasEmptyModels()
    {
        ClearAll();
        var a = InsertProvider(name: "OpenAI", type: "OpenAI");
        var b = InsertProvider(name: "Local", type: "Custom", endpoint: "https://local/v1");
        new AIModel { ProviderId = a.Id, ProviderName = "OpenAI", UpstreamModelId = "gpt-4o", ChatModelId = "OpenAI:gpt-4o", Enabled = true, CreateTime = DateTime.Now, UpdateTime = DateTime.Now }.Insert();
        new AIModel { ProviderId = a.Id, ProviderName = "OpenAI", UpstreamModelId = "gpt-4o-mini", ChatModelId = "OpenAI:gpt-4o-mini", Enabled = true, CreateTime = DateTime.Now, UpdateTime = DateTime.Now }.Insert();

        var svc = MakeService();
        var groups = svc.GetGroups();

        groups.Should().HaveCount(2);
        groups.Single(g => g.ProviderName == "OpenAI").Models.Should().HaveCount(2);
        groups.Single(g => g.ProviderName == "Local").Models.Should().BeEmpty();
    }

    [Fact]
    public void GetGroups_EnabledOnly_FiltersDisabled()
    {
        ClearAll();
        var a = InsertProvider(name: "OpenAI");
        new AIModel { ProviderId = a.Id, ProviderName = "OpenAI", UpstreamModelId = "m1", ChatModelId = "OpenAI:m1", Enabled = true, CreateTime = DateTime.Now, UpdateTime = DateTime.Now }.Insert();
        new AIModel { ProviderId = a.Id, ProviderName = "OpenAI", UpstreamModelId = "m2", ChatModelId = "OpenAI:m2", Enabled = false, CreateTime = DateTime.Now, UpdateTime = DateTime.Now }.Insert();

        var svc = MakeService();
        svc.GetGroups(enabledOnly: true).Single().Models.Should().ContainSingle(m => m.UpstreamModelId == "m1");
        svc.GetGroups().Single().Models.Should().HaveCount(2);
    }

    // ===== T024：SetEnabled / UpdateEditable / 级联删除 =====
    [Fact]
    public void SetEnabled_Persists()
    {
        ClearAll();
        var a = InsertProvider();
        var m = new AIModel { ProviderId = a.Id, ProviderName = "OpenAI", UpstreamModelId = "gpt-4o", ChatModelId = "OpenAI:gpt-4o", Enabled = true, CreateTime = DateTime.Now, UpdateTime = DateTime.Now };
        m.Insert();

        MakeService().SetEnabled(m.Id, false);
        AIModel.FindById(m.Id)!.Enabled.Should().BeFalse();
    }

    [Fact]
    public void UpdateEditable_OnlyAliasCapabilities_LockedFieldsUnchanged()
    {
        ClearAll();
        var a = InsertProvider();
        var m = new AIModel { ProviderId = a.Id, ProviderName = "OpenAI", UpstreamModelId = "gpt-4o", ChatModelId = "OpenAI:gpt-4o", Enabled = true, CreateTime = DateTime.Now, UpdateTime = DateTime.Now };
        m.Insert();

        MakeService().UpdateEditable(m.Id, "主力", new() { "vision", "stream" }, null);
        var after = AIModel.FindById(m.Id)!;
        after.Alias.Should().Be("主力");
        after.Capabilities.Should().Be("vision,stream");
        after.UpstreamModelId.Should().Be("gpt-4o"); // 锁定不变
        after.ChatModelId.Should().Be("OpenAI:gpt-4o"); // 恒为 provider:id
    }

    [Fact]
    public async Task FetchAfterDisable_PreservesDisabledState()
    {
        ClearAll();
        var a = InsertProvider();
        var svc = MakeService(OpenAiHandler(ModelsJson(("gpt-4o", "openai"))));
        await svc.FetchForProviderAsync(a.Id);

        var m = AIModel.FindByProviderNameAndUpstreamModelId("OpenAI", "gpt-4o")!;
        m.Enabled.Should().BeTrue();

        svc.SetEnabled(m.Id, false);
        await svc.FetchForProviderAsync(a.Id); // 重拉仅刷新元数据，保留 Enabled=false

        AIModel.FindByProviderNameAndUpstreamModelId("OpenAI", "gpt-4o")!.Enabled.Should().BeFalse();
    }

    [Fact]
    public void DeleteByProvider_Cascades()
    {
        ClearAll();
        var a = InsertProvider();
        new AIModel { ProviderId = a.Id, ProviderName = "OpenAI", UpstreamModelId = "gpt-4o", ChatModelId = "OpenAI:gpt-4o", Enabled = true, CreateTime = DateTime.Now, UpdateTime = DateTime.Now }.Insert();

        MakeService().DeleteByProvider(a.Id);
        AIModel.FindAllByProviderId(a.Id).Should().BeEmpty();
    }

    // ===== T025/T030：路由禁用模型不返回（FR-013） =====
    [Fact]
    public async Task GetProviderByChatModelId_DisabledModel_ReturnsNull()
    {
        ClearAll();
        var a = InsertProvider(name: "OpenAI");
        new AIModel { ProviderId = a.Id, ProviderName = "OpenAI", UpstreamModelId = "gpt-4o", ChatModelId = "OpenAI:gpt-4o", Enabled = false, CreateTime = DateTime.Now, UpdateTime = DateTime.Now }.Insert();

        var registry = new AIProviderRegistry();
        var cfg = new AIProviderConfig
        {
            Name = "OpenAI",
            ProviderType = AIProviderType.OpenAI,
            Endpoint = "https://api.openai.com/v1",
            ApiKey = "k",
            SupportedModels = new() { "gpt-4o" },
        };
        await registry.ReloadAsync(new TestHttpClientFactory(), new[] { cfg });

        // 禁用 -> 不参与路由
        registry.GetProviderByChatModelId("OpenAI:gpt-4o").Should().BeNull();

        // 启用后恢复
        var m = AIModel.FindByProviderNameAndUpstreamModelId("OpenAI", "gpt-4o")!;
        m.Enabled = true;
        m.Save();
        registry.GetProviderByChatModelId("OpenAI:gpt-4o").Should().NotBeNull();
    }
}

/// <summary>测试用 HttpMessageHandler，按请求返回预设响应</summary>
public class FakeHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _fn;

    public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> fn) => _fn = fn;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(_fn(request));
}

/// <summary>测试用 IHttpClientFactory，返回带指定 Handler 的 HttpClient</summary>
public class HandlerHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    public HandlerHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

    public HttpClient CreateClient(string name) => new HttpClient(_handler);
    public HttpClient CreateClient() => new HttpClient(_handler);
}
