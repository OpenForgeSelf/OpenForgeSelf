using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Security;
using ForgeSelf.Api.Services;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// 启动期密文迁移测试：幂等、失败不写回、版本标记推进，以及「可疑结果绝不覆盖原值」的破坏性路径防御。
/// </summary>
/// <remarks>
/// 本类会改写 <see cref="ForgeSetting"/>（NewLife Config&lt;T&gt; 的进程级全局配置文件），
/// 故归入 <c>SharedGlobalState</c> 集合串行执行，并在 <see cref="Dispose"/> 中还原现场。
/// </remarks>
[Collection("SharedGlobalState")]
public class SecretMigrationServiceTests : IDisposable
{
    /// <summary>用旧硬编码兼容键预生成的 v1 密文（与 AesSecretEncryptionV2Tests 同一常量）</summary>
    private const string LegacyV1Cipher = AesSecretEncryptionV2Tests.LegacyV1Cipher;

    /// <summary><see cref="LegacyV1Cipher"/> 对应的明文</summary>
    private const string LegacyPlainKey = AesSecretEncryptionV2Tests.LegacyPlainKey;

    /// <summary>与 AesSecretEncryptionV2Tests 一致的当前密钥源（用于构造 padding 碰撞密文）</summary>
    private const string CurrentKeySource = "unit-test-current-key";

    private readonly ISecretEncryptionService _encryption;
    private readonly SecretMigrationService _service;
    private readonly String _originalApiToken;
    private readonly Int32 _originalVersion;

    public SecretMigrationServiceTests()
    {
        _encryption = new AesSecretEncryptionService(new ConfigurationBuilder().Build(), new MachineKeyProvider());
        _service = new SecretMigrationService(_encryption);

        var setting = ForgeSetting.Current;
        _originalApiToken = setting.ApiToken;
        _originalVersion = setting.SecretMigrationVersion;
    }

    public void Dispose()
    {
        // 还原全局配置现场，避免影响后续测试与真实运行实例
        var setting = ForgeSetting.Current;
        setting.ApiToken = _originalApiToken;
        setting.SecretMigrationVersion = _originalVersion;
        setting.Save();
    }

    #region 幂等 / 版本标记

    [Fact]
    public void MigrateOnce_首次执行_把v1主密钥重封装为v2并推进版本()
    {
        ResetSetting(LegacyV1Cipher, 0);

        var count = _service.MigrateOnce();

        count.Should().BeGreaterOrEqualTo(1, "ApiToken 是 v1 旧密文，必然被重封装");
        ForgeSetting.Current.SecretMigrationVersion.Should().Be(SecretMigrationService.CurrentVersion);
        ForgeSetting.Current.ApiToken.Should().StartWith(AesSecretEncryptionService.V2Prefix);
        _encryption.Decrypt(ForgeSetting.Current.ApiToken).Should().Be(LegacyPlainKey, "重封装不得改变明文");
    }

    [Fact]
    public void MigrateOnce_第二次执行_返回0且不重写()
    {
        ResetSetting(LegacyV1Cipher, 0);
        _service.MigrateOnce();
        var afterFirst = ForgeSetting.Current.ApiToken;

        var second = _service.MigrateOnce();

        second.Should().Be(0, "版本标记已推进，迁移必须只发生一次");
        ForgeSetting.Current.ApiToken.Should().Be(afterFirst);
    }

    [Fact]
    public void MigrateOnce_版本已达当前版本_直接跳过()
    {
        ResetSetting(LegacyV1Cipher, SecretMigrationService.CurrentVersion);

        _service.MigrateOnce().Should().Be(0);
        ForgeSetting.Current.ApiToken.Should().Be(LegacyV1Cipher, "跳过时不得触碰原值");
    }

    [Fact]
    public void MigrateOnce_解密失败_保留原值不写回但推进版本()
    {
        var undecryptable = Convert.ToBase64String(new byte[64]);
        ResetSetting(undecryptable, 0);

        _service.MigrateOnce();

        ForgeSetting.Current.ApiToken.Should().Be(undecryptable, "解密失败必须保留原 v1 值，绝不写回假数据");
        ForgeSetting.Current.SecretMigrationVersion.Should().Be(SecretMigrationService.CurrentVersion);
    }

    [Fact]
    public void MigrateOnce_主密钥为空_安全跳过()
    {
        ResetSetting("", 0);

        _service.MigrateOnce();

        ForgeSetting.Current.ApiToken.Should().BeEmpty();
        ForgeSetting.Current.SecretMigrationVersion.Should().Be(SecretMigrationService.CurrentVersion);
    }

    #endregion

    #region 破坏性路径防御（写回前必须证明结果可信）

    [Fact]
    public void MigrateOnce_padding碰撞密文_迁移后真实明文不丢失()
    {
        // 历史加密服务若「第一个不抛异常的候选就返回」，会把当前密钥侧解出的乱码写回。
        // 修复后解密侧只采信合理明文，故迁移拿到的就是真实明文，重封装后明文必须原样保留。
        var encryption = CreateServiceWithKey(CurrentKeySource);
        var crafted = AesSecretEncryptionV2Tests.FindPaddingCollisionCipher(LegacyPlainKey, CurrentKeySource, out _);

        ResetSetting(crafted, 0);
        new SecretMigrationService(encryption).MigrateOnce();

        ForgeSetting.Current.ApiToken.Should().StartWith(AesSecretEncryptionService.V2Prefix);
        encryption.Decrypt(ForgeSetting.Current.ApiToken)
            .Should().Be(LegacyPlainKey, "迁移是唯一会覆盖原值的路径，绝不能把明文换成乱码");
    }

    [Fact]
    public void MigrateOnce_解密结果不合理_保留原值不写回()
    {
        // 模拟「解密返回乱码但被判为成功」的旧行为：迁移必须自行拦下，绝不写回
        var garbage = "\uFFFD\u0001\u0002乱码";
        AesSecretEncryptionV2Tests.IsPlausiblePlaintext(garbage).Should().BeFalse("前置条件：该结果必须被判为不合理");

        var stub = new StubEncryptionService(cipher => garbage);
        ResetSetting(LegacyV1Cipher, 1);

        new SecretMigrationService(stub).MigrateOnce();

        ForgeSetting.Current.ApiToken.Should().Be(LegacyV1Cipher, "解密结果可疑时必须保留原值，不得写回");
        ForgeSetting.Current.SecretMigrationVersion.Should().Be(SecretMigrationService.CurrentVersion);
    }

    [Fact]
    public void MigrateOnce_往返不一致_保留原值不写回()
    {
        // 明文看着合理，但「新密文解不回同一明文」说明写出去就无法还原 → 必须拒绝
        var stub = new StubEncryptionService(cipher => cipher == "v2:stub" ? "sk-别的值" : "sk-看起来合理");
        ResetSetting(LegacyV1Cipher, 1);

        new SecretMigrationService(stub).MigrateOnce();

        ForgeSetting.Current.ApiToken.Should().Be(LegacyV1Cipher, "往返不一致时必须保留原值");
        ForgeSetting.Current.SecretMigrationVersion.Should().Be(SecretMigrationService.CurrentVersion);
    }

    #endregion

    /// <summary>把全局配置重置到测试所需初始状态</summary>
    private static void ResetSetting(string apiToken, int migrationVersion)
    {
        var setting = ForgeSetting.Current;
        setting.ApiToken = apiToken;
        setting.SecretMigrationVersion = migrationVersion;
        setting.Save();
    }

    private static AesSecretEncryptionService CreateServiceWithKey(string keySource)
        => new(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:Key"] = keySource,
            }).Build(),
            new MachineKeyProvider());

    /// <summary>
    /// 可编排的加密服务桩：用于制造「解密结果可疑」的场景（真实实现修复后已不会产出可疑结果）。
    /// </summary>
    private sealed class StubEncryptionService : ISecretEncryptionService
    {
        private readonly Func<string, string?> _decrypt;

        public StubEncryptionService(Func<string, string?> decrypt)
        {
            _decrypt = decrypt;
        }

        public string Encrypt(string plainText) => "v2:stub";

        public string Decrypt(string cipherText) => _decrypt(cipherText) ?? throw new CryptographicException("桩：解密失败");

        public bool TryDecrypt(string cipherText, out string plainText)
        {
            var result = _decrypt(cipherText);
            plainText = result ?? string.Empty;
            return result != null;
        }

        public string Mask(string plainText) => string.Empty;
    }
}
