using ForgeSelf.Abstractions;
using Microsoft.Extensions.Configuration;
using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Security;
using ForgeSelf.Api.Services;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// API 子密钥服务测试（创建 / 命中 / 停用 / 过期 / 主密钥回退 / 轮换）。
/// </summary>
/// <remarks>
/// 碰 XCode（临时 SQLite + 唯一索引），故归入 <c>XCode</c> 集合并复用 <see cref="XCodeTestFixture"/>；
/// 认证判定经 <see cref="ApiKeyService.ResolveByToken"/> 直接单测，不走 HTTP
/// （本项目禁止用 WebApplicationFactory 覆盖连接串）。
/// </remarks>
[Collection("XCode")]
public class ApiKeyServiceTests : IClassFixture<XCodeTestFixture>, IDisposable
{
    private const string MasterPlainKey = "sk-00000000000000000000000000000002";

    private readonly ISecretEncryptionService _encryption;
    private readonly ApiKeyService _service;
    private readonly String _originalApiToken;

    public ApiKeyServiceTests(XCodeTestFixture fixture)
    {
        _encryption = new AesSecretEncryptionService(new ConfigurationBuilder().Build(), new MachineKeyProvider());
        _service = new ApiKeyService(_encryption);

        // 主密钥回退用例需要配置里有主密钥密文；测试结束还原
        var setting = ForgeSetting.Current;
        _originalApiToken = setting.ApiToken;
        setting.ApiToken = _encryption.Encrypt(MasterPlainKey);
        setting.Save();

        // 每个用例从干净表开始（XCodeTestFixture 为每类独立临时库，此处再兜底清空）
        foreach (var existed in ApiKeyCredential.FindAll())
        {
            existed.Delete();
        }
    }

    public void Dispose()
    {
        try
        {
            foreach (var existed in ApiKeyCredential.FindAll())
            {
                existed.Delete();
            }
        }
        catch
        {
            // 清理失败不影响断言结果
        }

        var setting = ForgeSetting.Current;
        setting.ApiToken = _originalApiToken;
        setting.Save();
    }

    [Fact]
    public void Create_生成的子密钥_可被ResolveByToken命中()
    {
        var created = _service.Create(new CreateApiKeyRequest { Name = "测试密钥", Remark = "单测" });

        created.Should().NotBeNull();
        created!.PlainKey.Should().StartWith("sk-");
        created.PlainKey.Should().HaveLength(35, "sk- + 32 位 hex");
        created.AuthHeader.Should().Be($"Authorization: Bearer {created.PlainKey}");

        var identity = _service.ResolveByToken(created.PlainKey);

        identity.Should().NotBeNull();
        identity!.KeyId.Should().Be(created.Item.Id);
        identity.KeyName.Should().Be("测试密钥");
        identity.AuthMethod.Should().Be("ApiKeyEntry");
    }

    [Fact]
    public void ResolveByToken_停用后返回null_且不回退主密钥()
    {
        var created = _service.Create(new CreateApiKeyRequest { Name = "待停用" });
        _service.Toggle(created!.Item.Id, false);

        // 若回退主密钥，这里会拿到 AuthMethod=BearerApiKey 的非空身份 → 断言 null 即证明未回退
        var identity = _service.ResolveByToken(created.PlainKey);

        identity.Should().BeNull("停用即终态，吊销才有意义");
    }

    [Fact]
    public void ResolveByToken_过期后返回null_且不回退主密钥()
    {
        var created = _service.Create(new CreateApiKeyRequest
        {
            Name = "待过期",
            ExpiresAt = DateTime.Now.AddMinutes(-1),
        });

        _service.ResolveByToken(created!.PlainKey).Should().BeNull();
    }

    [Fact]
    public void ResolveByToken_未过期_正常命中()
    {
        var created = _service.Create(new CreateApiKeyRequest
        {
            Name = "未来过期",
            ExpiresAt = DateTime.Now.AddDays(1),
        });

        _service.ResolveByToken(created!.PlainKey).Should().NotBeNull();
    }

    [Fact]
    public void Roll_旧明文失效_新明文命中_且Id不变()
    {
        var created = _service.Create(new CreateApiKeyRequest { Name = "待轮换" });
        var originalId = created!.Item.Id;
        var oldPlain = created.PlainKey;

        var rolled = _service.Roll(originalId);

        rolled.Should().NotBeNull();
        rolled!.Item.Id.Should().Be(originalId, "轮换是换明文，不是换条目");
        rolled.PlainKey.Should().NotBe(oldPlain);

        _service.ResolveByToken(oldPlain).Should().BeNull("旧值必须立即失效");
        _service.ResolveByToken(rolled.PlainKey).Should().NotBeNull();
    }

    [Fact]
    public void ResolveByToken_主密钥明文_返回KeyId0且认证方式为BearerApiKey()
    {
        var identity = _service.ResolveByToken(MasterPlainKey);

        identity.Should().NotBeNull();
        identity!.KeyId.Should().Be(0);
        identity.KeyName.Should().Be("主密钥");
        identity.AuthMethod.Should().Be("BearerApiKey");
    }

    [Fact]
    public void ResolveByToken_未知令牌_返回null()
    {
        _service.ResolveByToken("sk-00000000000000000000000000000000").Should().BeNull();
        _service.ResolveByToken("").Should().BeNull();
    }

    [Fact]
    public void List_DTO只含掩码_不含明文与密文材料()
    {
        var created = _service.Create(new CreateApiKeyRequest { Name = "列表密钥" });

        var items = _service.List();

        items.Should().HaveCount(1);
        var item = items[0];
        item.MaskedKey.Should().NotBeEmpty();
        item.MaskedKey.Should().NotBe(created!.PlainKey, "列表绝不能出现明文");
        item.MaskedKey.Should().Contain("****");
        item.CanDecrypt.Should().BeTrue();
        item.ExpiresAt.Should().BeNull("MinValue 必须转成 null");
        item.LastUsedAt.Should().BeNull();

        // DTO 类型本身就不暴露 KeyCipher / KeyHash
        typeof(ApiKeyItemDto).GetProperty("KeyCipher").Should().BeNull();
        typeof(ApiKeyItemDto).GetProperty("KeyHash").Should().BeNull();
        typeof(ApiKeyItemDto).GetProperty("PlainKey").Should().BeNull();
    }

    [Fact]
    public void Toggle_停用后可再次启用()
    {
        var created = _service.Create(new CreateApiKeyRequest { Name = "启停" });

        _service.Toggle(created!.Item.Id, false);
        _service.List()[0].Enabled.Should().BeFalse();

        _service.Toggle(created.Item.Id, true);
        _service.List()[0].Enabled.Should().BeTrue();
        _service.ResolveByToken(created.PlainKey).Should().NotBeNull();
    }

    [Fact]
    public void Delete_删除后不可再命中()
    {
        var created = _service.Create(new CreateApiKeyRequest { Name = "待删除" });

        _service.Delete(created!.Item.Id).Should().BeTrue();

        _service.List().Should().BeEmpty();
        _service.ResolveByToken(created.PlainKey).Should().BeNull();
        _service.Delete(created.Item.Id).Should().BeFalse("重复删除返回 false");
    }

    [Fact]
    public void Update_重命名与备注_不存在的条目返回null()
    {
        var created = _service.Create(new CreateApiKeyRequest { Name = "原名" });

        var updated = _service.Update(created!.Item.Id, new UpdateApiKeyRequest { Name = "新名", Remark = "新备注" });

        updated.Should().NotBeNull();
        updated!.Name.Should().Be("新名");
        updated.Remark.Should().Be("新备注");

        _service.Update(999999, new UpdateApiKeyRequest { Name = "不存在" }).Should().BeNull();
    }

    [Fact]
    public void Create_未指定名称_自动生成密钥N()
    {
        var created = _service.Create(new CreateApiKeyRequest());

        created.Should().NotBeNull();
        created!.Item.Name.Should().StartWith("密钥");
    }
}
