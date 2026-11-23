using System.Text.Json;
using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Security;
using ForgeSelf.Api.Services.Vault;
using ForgeSelf.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// <see cref="SecretsController"/> 测试（PILOT-086）：
/// 类级鉴权守卫（AC-12 / PC-002）、明文红线（AC-9）、404/500 外壳。
/// </summary>
[Collection("XCode")]
public class SecretsControllerTests : IClassFixture<XCodeTestFixture>
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly SecretStore _store;

    public SecretsControllerTests(XCodeTestFixture fixture)
    {
        _ = fixture;
        var keyDir = Path.Combine(Path.GetTempPath(), "vaultkek_" + Guid.NewGuid().ToString("N"));
        _store = new SecretStore(new VaultKeyProvider(keyDir), new CredentialBroker());
    }

    private SecretsController NewController() => new(_store);

    private SecretMetaDto CreateOne(string value = "sk-plain-controller-42") =>
        _store.Create(new CreateSecretRequest { Name = "控制器测试凭据", SecretType = "ApiKey", Value = value }, "test");

    // ───────────────────────── 鉴权守卫（PC-002） ─────────────────────────

    [Fact]
    public void 类级必须挂ApiKeyPolicy_防PC002漏鉴权()
    {
        var attributes = typeof(SecretsController)
            .GetCustomAttributes(inherit: false)
            .OfType<AuthorizeAttribute>()
            .ToList();

        attributes.Should().NotBeEmpty("管理面控制器必须类级 [Authorize]");
        attributes.Should().Contain(a => a.Policy == "ApiKeyPolicy",
            "必须显式声明 ApiKeyPolicy（PC-002：AIProviderController 曾漏鉴权）");
        attributes.Should().OnlyContain(a => a.Policy == "ApiKeyPolicy", "不得引入其它鉴权策略");
    }

    // ───────────────────────── 明文红线（AC-9） ─────────────────────────

    [Fact]
    public void 创建_响应不含明文与密文材料()
    {
        var plain = "sk-plain-controller-42";
        var controller = NewController();

        var result = controller.Create(new CreateSecretRequest
        {
            Name = "红线检查", SecretType = "ApiKey", Value = plain,
        });

        var body = BodyOf(result.Result);
        body.Should().Contain("\"success\":true");
        body.Should().NotContain(plain, "创建响应不得回显明文");
        body.Should().NotContainAny("wrappedDek", "ciphertext", "nonce", "wrapTag", "\"tag\"", "keyId",
            "响应不得携带任何信封材料字段");
    }

    [Fact]
    public void 列表与详情_不含明文与密文材料()
    {
        CreateOne("list-plain-777");
        var meta = CreateOne("detail-plain-888");

        var listBody = BodyOf(NewController().List());
        listBody.Should().NotContain("list-plain-777").And.NotContain("detail-plain-888");
        listBody.Should().NotContainAny("wrappedDek", "ciphertext", "nonce", "\"tag\"", "keyId");

        var oneBody = BodyOf(NewController().GetOne(meta.RefId));
        oneBody.Should().NotContain("detail-plain-888");
        oneBody.Should().NotContainAny("wrappedDek", "ciphertext", "nonce", "\"tag\"", "keyId");
    }

    [Fact]
    public void 轮换_响应不回显新旧值()
    {
        var meta = CreateOne("rotate-old-value");
        var controller = NewController();

        var result = controller.SetValue(meta.RefId, new SetSecretValueRequest { Value = "rotate-new-value" });

        var body = BodyOf(result.Result);
        body.Should().Contain("\"success\":true");
        body.Should().NotContain("rotate-old-value").And.NotContain("rotate-new-value");
        body.Should().NotContainAny("wrappedDek", "ciphertext", "nonce", "\"tag\"", "keyId");
    }

    [Fact]
    public void 审计端点_返回事件且不含明文()
    {
        var meta = CreateOne("audit-plain-999");
        _store.Revoke(meta.RefId, "test");

        var result = NewController().Audit(meta.Id, 100);

        var body = BodyOf(result);
        body.Should().Contain("\"success\":true");
        body.Should().Contain("revoke");
        body.Should().NotContain("audit-plain-999", "审计响应不得含明文");
    }

    // ───────────────────────── 404 / 业务失败 / 500 外壳 ─────────────────────────

    [Fact]
    public void 详情_不存在_返回404外壳()
    {
        var result = NewController().GetOne(999999.ToString());

        var objectResult = result.Result as NotFoundObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(404);
        ParseShell(JsonSerializer.Serialize(objectResult.Value, WebJson)).Error.Should().Contain("凭据不存在");
    }

    [Fact]
    public void 轮换_目标不存在_404()
    {
        var result = NewController().SetValue("sec_0123456789abcdef0123456789abcdef", new SetSecretValueRequest { Value = "x" });
        (result.Result as NotFoundObjectResult).Should().NotBeNull();
    }

    [Fact]
    public void 创建_非法类型_业务失败外壳()
    {
        var result = NewController().Create(new CreateSecretRequest { Name = "x", SecretType = "BadType", Value = "v" });

        var objectResult = result.Result as OkObjectResult;
        objectResult.Should().NotBeNull("业务失败按宿主外壳惯例返回 200 + success:false");
        var shell = ParseShell(JsonSerializer.Serialize(objectResult!.Value, WebJson));
        shell.Success.Should().BeFalse();
        shell.Error.Should().Contain("凭据类型非法");
    }

    [Fact]
    public void 库异常_返回500且body为successFalse外壳_不透堆栈()
    {
        var controller = new SecretsController(new ThrowingStore());

        var result = controller.List();

        var objectResult = result.Result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(500);
        var json = JsonSerializer.Serialize(objectResult.Value, WebJson);
        json.Should().NotContain("   at ", "不得把异常堆栈透出到响应体");
        var shell = ParseShell(json);
        shell.Success.Should().BeFalse();
        shell.Error.Should().Contain("获取凭据列表失败");
    }

    /// <summary>把匿名响应对象序列化后解析为外壳字段</summary>
    private static (bool Success, string Error) ParseShell(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return (
            root.GetProperty("success").GetBoolean(),
            root.TryGetProperty("error", out var error) ? error.GetString() ?? "" : "");
    }

    private static string BodyOf(ActionResult<object>? action)
    {
        action.Should().NotBeNull();
        var ok = action!.Result as OkObjectResult;
        ok.Should().NotBeNull("预期成功外壳（HTTP 200）");
        return JsonSerializer.Serialize(ok!.Value, WebJson);
    }

    /// <summary>全部方法必抛的桩：模拟宿主库不可用</summary>
    private sealed class ThrowingStore : ISecretStore
    {
        public SecretMetaDto Create(CreateSecretRequest request, string actor) => throw new InvalidOperationException("模拟库不可用");
        public List<SecretMetaDto> List() => throw new InvalidOperationException("模拟库不可用");
        public SecretMetaDto? Get(string idOrRef) => throw new InvalidOperationException("模拟库不可用");
        public SecretMetaDto? Update(string idOrRef, UpdateSecretRequest request, string actor) => throw new InvalidOperationException("模拟库不可用");
        public SecretMetaDto? SetSecretValue(string idOrRef, string newValue, string actor) => throw new InvalidOperationException("模拟库不可用");
        public SecretMetaDto? Revoke(string idOrRef, string actor) => throw new InvalidOperationException("模拟库不可用");
        public bool Delete(string idOrRef, string actor) => throw new InvalidOperationException("模拟库不可用");
        public List<GrantDto> ListGrants(string idOrRef) => throw new InvalidOperationException("模拟库不可用");
        public GrantDto? CreateGrant(string idOrRef, CreateGrantRequest request, string actor) => throw new InvalidOperationException("模拟库不可用");
        public bool RevokeGrant(string idOrRef, long grantId, string actor) => throw new InvalidOperationException("模拟库不可用");
        public List<AuditEventDto> GetAudit(long? secretId, int limit) => throw new InvalidOperationException("模拟库不可用");
        public (string? Value, GrantDecision Decision) GetValueForHost(string idOrRef, UseContext context) => throw new InvalidOperationException("模拟库不可用");
    }
}
