using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Security;
using ForgeSelf.Api.Services.Vault;
using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// 凭据保险库存储/授权/审计测试（PILOT-086；XCode 临时库）。
/// 覆盖：Grant 授权矩阵（AC-7）、吊销/过期 Fail Closed（AC-8）、审计落行与脱敏（AC-10）。
/// </summary>
[Collection("XCode")]
public class CredentialVaultStoreTests : IClassFixture<XCodeTestFixture>
{
    private readonly SecretStore _store;

    public CredentialVaultStoreTests(XCodeTestFixture fixture)
    {
        _ = fixture;
        var keyDir = Path.Combine(Path.GetTempPath(), "vaultkek_" + Guid.NewGuid().ToString("N"));
        _store = new SecretStore(new VaultKeyProvider(keyDir), new CredentialBroker());
    }

    private static UseContext Ctx(string subjectId = "host", string operation = "use", string target = "") =>
        new() { Actor = "host:test", SubjectType = "host", SubjectId = subjectId, Operation = operation, Target = target };

    private SecretMetaDto CreateSecret(string value = "sk-test-plaintext-12345", DateTime? expiresAt = null) =>
        _store.Create(new CreateSecretRequest
        {
            Name = "测试凭据",
            SecretType = "ApiKey",
            Provider = "openai",
            Value = value,
            ExpiresAt = expiresAt ?? DateTime.MinValue,
        }, "test");

    // ───────────────────────── 创建与明文红线 ─────────────────────────

    [Fact]
    public void 创建_生成sec引用_轮值1_active_且明文不入任何列()
    {
        var plain = "sk-test-plaintext-12345";
        var meta = CreateSecret(plain);

        meta.RefId.Should().StartWith("sec_").And.HaveLength(36);
        meta.Ref.Should().Contain(meta.RefId).And.Contain("vault");
        meta.RotationVersion.Should().Be(1);
        meta.Status.Should().Be("active");

        var entity = StoredSecret.FindByRefId(meta.RefId)!;
        entity.Should().NotBeNull();
        entity.FormatVersion.Should().Be("ev1");
        entity.KeyId.Should().StartWith("kek1-");
        entity.Status.Should().Be("active");

        // 明文红线：所有文本列都不含明文
        var columns = new[]
        {
            entity.Name, entity.SecretType, entity.Provider, entity.OwnerType, entity.OwnerId,
            entity.MetadataJson, entity.RefId, entity.Status, entity.FormatVersion, entity.KeyId,
            entity.WrappedDek, entity.WrapNonce, entity.WrapTag, entity.Nonce, entity.Ciphertext, entity.Tag,
        };
        columns.Should().NotContain(c => c != null && c.Contains(plain), "明文不得落任何列");
        entity.Ciphertext.Should().NotBeNullOrEmpty().And.NotContain(plain);
        entity.WrappedDek.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("", "ApiKey")]
    [InlineData("   ", "ApiKey")]
    [InlineData("名称", "")]
    [InlineData("名称", "NotAType")]
    public void 创建_非法参数_抛VaultOperationException(string name, string secretType)
    {
        var act = () => _store.Create(new CreateSecretRequest { Name = name, SecretType = secretType, Value = "v" }, "test");
        act.Should().Throw<VaultOperationException>();
    }

    [Fact]
    public void 创建_空值_拒绝()
    {
        var act = () => _store.Create(new CreateSecretRequest { Name = "x", SecretType = "Token", Value = "" }, "test");
        act.Should().Throw<VaultOperationException>().WithMessage("*不能为空*");
    }

    [Fact]
    public void 创建_元数据非法JSON_拒绝()
    {
        var act = () => _store.Create(new CreateSecretRequest { Name = "x", SecretType = "Token", Value = "v", MetadataJson = "{bad" }, "test");
        act.Should().Throw<VaultOperationException>().WithMessage("*JSON*");
    }

    // ───────────────────────── Grant 授权矩阵（AC-7） ─────────────────────────

    [Fact]
    public void 无Grant_使用拒绝并落审计denied()
    {
        var meta = CreateSecret();

        var (value, decision) = _store.GetValueForHost(meta.RefId, Ctx());

        value.Should().BeNull();
        decision.Allowed.Should().BeFalse();
        decision.ReasonCode.Should().Be("no_grant");

        var audit = SecretAuditEvent.FindAllRecent(meta.Id, 10);
        audit.Should().Contain(e => e.Action == "use_denied" && e.Result == "denied" && e.ReasonCode == "no_grant");
    }

    [Fact]
    public void 有匹配Grant_使用允许并返回明文_审计use_allowed()
    {
        var plain = "sk-grant-ok-9876";
        var meta = CreateSecret(plain);
        _store.CreateGrant(meta.RefId, new CreateGrantRequest { SubjectType = "host", SubjectId = "host", Operation = "use" }, "test");

        var (value, decision) = _store.GetValueForHost(meta.RefId, Ctx());

        decision.Allowed.Should().BeTrue();
        decision.GrantId.Should().BeGreaterThan(0);
        value.Should().Be(plain, "授权后明文唯一出口返回原值");

        var audit = SecretAuditEvent.FindAllRecent(meta.Id, 10);
        audit.Should().Contain(e => e.Action == "use_allowed" && e.Result == "allowed");
    }

    [Fact]
    public void Grant_主体不匹配_拒绝()
    {
        var meta = CreateSecret();
        _store.CreateGrant(meta.RefId, new CreateGrantRequest { SubjectType = "host", SubjectId = "host", Operation = "use" }, "test");

        var (_, decision) = _store.GetValueForHost(meta.RefId, Ctx(subjectId: "other"));

        decision.Allowed.Should().BeFalse();
        decision.ReasonCode.Should().Be("no_grant");
    }

    [Fact]
    public void Grant_操作不匹配_拒绝()
    {
        var meta = CreateSecret();
        _store.CreateGrant(meta.RefId, new CreateGrantRequest { SubjectType = "host", SubjectId = "host", Operation = "test-connection" }, "test");

        var (_, decision) = _store.GetValueForHost(meta.RefId, Ctx(operation: "use"));

        decision.Allowed.Should().BeFalse();
    }

    [Fact]
    public void Grant_目标不匹配_拒绝_目标匹配_允许()
    {
        var meta = CreateSecret();
        _store.CreateGrant(meta.RefId, new CreateGrantRequest
        {
            SubjectType = "host", SubjectId = "host", Operation = "use", TargetConstraint = "api.openai.com",
        }, "test");

        var (_, denied) = _store.GetValueForHost(meta.RefId, Ctx(target: "api.anthropic.com"));
        denied.Allowed.Should().BeFalse();
        denied.ReasonCode.Should().Be("no_grant");

        var (_, ok) = _store.GetValueForHost(meta.RefId, Ctx(target: "api.openai.com"));
        ok.Allowed.Should().BeTrue();
    }

    [Fact]
    public void Grant_已过期_拒绝()
    {
        var meta = CreateSecret();
        _store.CreateGrant(meta.RefId, new CreateGrantRequest
        {
            SubjectType = "host", SubjectId = "host", Operation = "use", ExpiresAt = DateTime.Now.AddMinutes(-1),
        }, "test");

        var (_, decision) = _store.GetValueForHost(meta.RefId, Ctx());
        decision.Allowed.Should().BeFalse();
        decision.ReasonCode.Should().Be("no_grant");
    }

    [Fact]
    public void Grant_未生效_拒绝()
    {
        var meta = CreateSecret();
        _store.CreateGrant(meta.RefId, new CreateGrantRequest
        {
            SubjectType = "host", SubjectId = "host", Operation = "use", ValidFrom = DateTime.Now.AddHours(1),
        }, "test");

        var (_, decision) = _store.GetValueForHost(meta.RefId, Ctx());
        decision.Allowed.Should().BeFalse();
    }

    [Fact]
    public void Grant_撤销后立即拒绝_无缓存()
    {
        var meta = CreateSecret();
        var grant = _store.CreateGrant(meta.RefId, new CreateGrantRequest { SubjectType = "host", SubjectId = "host", Operation = "use" }, "test")!;
        _store.GetValueForHost(meta.RefId, Ctx()).Decision.Allowed.Should().BeTrue();

        _store.RevokeGrant(meta.RefId, grant.Id, "test");

        var (_, decision) = _store.GetValueForHost(meta.RefId, Ctx());
        decision.Allowed.Should().BeFalse();
        decision.ReasonCode.Should().Be("no_grant");
    }

    [Fact]
    public void 撤销不属于该凭据的Grant_报grant_mismatch()
    {
        var a = CreateSecret();
        var b = CreateSecret();
        var grant = _store.CreateGrant(a.RefId, new CreateGrantRequest { SubjectType = "host", SubjectId = "host", Operation = "use" }, "test")!;

        var act = () => _store.RevokeGrant(b.RefId, grant.Id, "test");
        act.Should().Throw<VaultOperationException>().WithMessage("*不属于*");
    }

    // ───────────────────────── Secret 状态（AC-8） ─────────────────────────

    [Fact]
    public void 凭据吊销后_使用拒绝_轮换拒绝()
    {
        var meta = CreateSecret();
        _store.CreateGrant(meta.RefId, new CreateGrantRequest { SubjectType = "host", SubjectId = "host", Operation = "use" }, "test");

        _store.Revoke(meta.RefId, "test");

        var (_, decision) = _store.GetValueForHost(meta.RefId, Ctx());
        decision.Allowed.Should().BeFalse();
        decision.ReasonCode.Should().Be("secret_revoked");

        var act = () => _store.SetSecretValue(meta.RefId, "new", "test");
        act.Should().Throw<VaultOperationException>().WithMessage("*吊销*");
    }

    [Fact]
    public void 凭据已过期_使用拒绝()
    {
        var meta = CreateSecret(expiresAt: DateTime.Now.AddMinutes(-5));
        _store.CreateGrant(meta.RefId, new CreateGrantRequest { SubjectType = "host", SubjectId = "host", Operation = "use" }, "test");

        var (_, decision) = _store.GetValueForHost(meta.RefId, Ctx());
        decision.Allowed.Should().BeFalse();
        decision.ReasonCode.Should().Be("secret_expired");
    }

    // ───────────────────────── 轮换与删除 ─────────────────────────

    [Fact]
    public void 轮换_新DEK新Nonce_版本递增_旧值不可再用_审计update_value()
    {
        var meta = CreateSecret("old-value-aaa");
        _store.CreateGrant(meta.RefId, new CreateGrantRequest { SubjectType = "host", SubjectId = "host", Operation = "use" }, "test");
        // XCode 实体缓存返回同一实例（轮换会原地改写），轮换前必须捕获标量快照
        var before = StoredSecret.FindByRefId(meta.RefId)!;
        var versionBefore = before.RotationVersion;
        var nonceBefore = before.Nonce;
        var ciphertextBefore = before.Ciphertext;

        var updated = _store.SetSecretValue(meta.RefId, "new-value-bbb", "test")!;

        updated.RotationVersion.Should().Be(versionBefore + 1);
        var after = StoredSecret.FindByRefId(meta.RefId)!;
        after.RotationVersion.Should().Be(updated.RotationVersion);
        after.Nonce.Should().NotBe(nonceBefore, "每次轮换必须换 Nonce");
        after.Ciphertext.Should().NotBe(ciphertextBefore);

        var (value, _) = _store.GetValueForHost(meta.RefId, Ctx());
        value.Should().Be("new-value-bbb");

        var columns = new[] { after.Name, after.MetadataJson, after.Nonce, after.Ciphertext, after.Tag, after.WrappedDek };
        columns.Should().NotContain(c => c != null && c.Contains("old-value-aaa"), "旧值不得残留");

        SecretAuditEvent.FindAllRecent(meta.Id, 10).Should().Contain(e => e.Action == "update_value");
    }

    [Fact]
    public void 删除_先吊销全部授权_行消失_审计保留()
    {
        var meta = CreateSecret();
        _store.CreateGrant(meta.RefId, new CreateGrantRequest { SubjectType = "host", SubjectId = "host", Operation = "use" }, "test");

        _store.Delete(meta.RefId, "test").Should().BeTrue();

        StoredSecret.FindByRefId(meta.RefId).Should().BeNull("行已删除");
        _store.Delete(meta.RefId, "test").Should().BeFalse("二次删除返回 false");

        var audit = SecretAuditEvent.FindAllRecent(meta.Id, 20);
        audit.Should().Contain(e => e.Action == "delete");
        audit.Should().Contain(e => e.Action == "grant_revoked" && e.ReasonCode == "secret_deleted", "删除时授权被连带吊销");
    }

    // ───────────────────────── 元数据与审计查询 ─────────────────────────

    [Fact]
    public void 更新元数据_生效并审计update()
    {
        var meta = CreateSecret();
        var updated = _store.Update(meta.RefId, new UpdateSecretRequest { Name = "改名后", Provider = "github" }, "test")!;

        updated.Name.Should().Be("改名后");
        updated.Provider.Should().Be("github");
        SecretAuditEvent.FindAllRecent(meta.Id, 10).Should().Contain(e => e.Action == "update");
    }

    [Fact]
    public void 不存在的Id_Get与Update返回null()
    {
        _store.Get(987654321.ToString()).Should().BeNull();
        _store.Update(987654321.ToString(), new UpdateSecretRequest(), "test").Should().BeNull();
    }

    [Fact]
    public void 审计查询_脱敏且limit封顶500()
    {
        var meta = CreateSecret("audit-plain-777");
        _store.Revoke(meta.RefId, "test");

        var events = _store.GetAudit(meta.Id, 100);
        events.Should().NotBeEmpty();
        events.Should().OnlyContain(e => !e.Actor.Contains("audit-plain-777"), "审计不得含明文");

        _store.GetAudit(meta.Id, 100_000).Should().HaveCountLessOrEqualTo(500);
        _store.GetAudit(meta.Id, 0).Should().NotBeNull();
    }

    [Fact]
    public void List_只含元数据_多凭据全返回()
    {
        CreateSecret("l1");
        CreateSecret("l2");

        var list = _store.List();
        list.Should().HaveCountGreaterOrEqualTo(2);
        list.Should().OnlyContain(m => m.RefId.StartsWith("sec_"));
    }
}
