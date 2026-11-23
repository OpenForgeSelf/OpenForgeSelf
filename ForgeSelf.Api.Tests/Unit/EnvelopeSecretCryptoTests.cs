using System.Security.Cryptography;
using ForgeSelf.Api.Security;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// AES-256-GCM 信封加密测试（PILOT-086 FR-6 / AC-1~4）。
/// 对应方案验收清单：往返、篡改拒绝（密文/nonce/tag/AAD/包封材料）、同明文异密文、错误 KEK 失败。
/// </summary>
public class EnvelopeSecretCryptoTests
{
    private static VaultKeyMaterial NewKek() => new()
    {
        KeyId = VaultKeyProvider.BuildKeyId(RandomNumberGenerator.GetBytes(32)),
        Key = RandomNumberGenerator.GetBytes(32),
        ProtectionMode = "test",
    };

    private static string NewAad() => EnvelopeSecretCrypto.BuildAad(SecretRefForTest(), "host", "host", 1);

    private static string SecretRefForTest() => "sec_" + Guid.NewGuid().ToString("N");

    /// <summary>把 base64 字段的第一字节翻转（篡改模拟）</summary>
    private static string FlipFirstByte(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        bytes[0] ^= 0xFF;
        return Convert.ToBase64String(bytes);
    }

    [Fact]
    public void 往返_加密后解密还原明文()
    {
        var kek = NewKek();
        var aad = NewAad();
        var plain = "sk-proj-abcdef123456";

        var payload = EnvelopeSecretCrypto.Encrypt(plain, aad, kek);

        payload.FormatVersion.Should().Be("ev1");
        payload.KeyId.Should().Be(kek.KeyId);
        EnvelopeSecretCrypto.Decrypt(payload, aad, kek.Key).Should().Be(plain);
    }

    [Fact]
    public void 往返_长明文10KB_可还原()
    {
        var kek = NewKek();
        var aad = NewAad();
        var plain = new string('x', 10_000) + "🔑尾部多字节";

        var payload = EnvelopeSecretCrypto.Encrypt(plain, aad, kek);

        EnvelopeSecretCrypto.Decrypt(payload, aad, kek.Key).Should().Be(plain);
    }

    [Fact]
    public void 同一明文两次加密_Nonce与密文均不同_每次随机DEK()
    {
        var kek = NewKek();
        var aad = NewAad();

        var p1 = EnvelopeSecretCrypto.Encrypt("same-secret", aad, kek);
        var p2 = EnvelopeSecretCrypto.Encrypt("same-secret", aad, kek);

        p1.Nonce.Should().NotBe(p2.Nonce, "方案验收 §11.1：同一明文多次加密必须产生不同 Nonce");
        p1.Ciphertext.Should().NotBe(p2.Ciphertext, "DEK 每次随机 → 密文必然不同");
        p1.WrappedDek.Should().NotBe(p2.WrappedDek);
    }

    public static TheoryData<string> 篡改字段 => new()
    {
        nameof(EnvelopePayload.Ciphertext),
        nameof(EnvelopePayload.Nonce),
        nameof(EnvelopePayload.Tag),
        nameof(EnvelopePayload.WrappedDek),
        nameof(EnvelopePayload.WrapNonce),
        nameof(EnvelopePayload.WrapTag),
    };

    [Theory]
    [MemberData(nameof(篡改字段))]
    public void 篡改任一材料字段_解密失败(string fieldName)
    {
        var kek = NewKek();
        var aad = NewAad();
        var payload = EnvelopeSecretCrypto.Encrypt("secret", aad, kek) with { };

        var tampered = fieldName switch
        {
            nameof(EnvelopePayload.Ciphertext) => payload with { Ciphertext = FlipFirstByte(payload.Ciphertext) },
            nameof(EnvelopePayload.Nonce) => payload with { Nonce = FlipFirstByte(payload.Nonce) },
            nameof(EnvelopePayload.Tag) => payload with { Tag = FlipFirstByte(payload.Tag) },
            nameof(EnvelopePayload.WrappedDek) => payload with { WrappedDek = FlipFirstByte(payload.WrappedDek) },
            nameof(EnvelopePayload.WrapNonce) => payload with { WrapNonce = FlipFirstByte(payload.WrapNonce) },
            nameof(EnvelopePayload.WrapTag) => payload with { WrapTag = FlipFirstByte(payload.WrapTag) },
            _ => throw new InvalidOperationException(fieldName),
        };

        var act = () => EnvelopeSecretCrypto.Decrypt(tampered, aad, kek.Key);
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void AAD不匹配_解密失败_行字段被改即不可解()
    {
        var kek = NewKek();
        var refId = SecretRefForTest();
        var payload = EnvelopeSecretCrypto.Encrypt(
            "secret", EnvelopeSecretCrypto.BuildAad(refId, "host", "host", 1), kek);

        // 模拟「轮值版本被回滚/所有者被改」：AAD 变了 → 拒绝
        var wrongAad = EnvelopeSecretCrypto.BuildAad(refId, "host", "host", 2);
        var act = () => EnvelopeSecretCrypto.Decrypt(payload, wrongAad, kek.Key);
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void 错误KEK_解密失败_仅复制数据库无法解密()
    {
        var kek = NewKek();
        var aad = NewAad();
        var payload = EnvelopeSecretCrypto.Encrypt("secret", aad, kek);

        var otherKek = RandomNumberGenerator.GetBytes(32);
        var act = () => EnvelopeSecretCrypto.Decrypt(payload, aad, otherKek);
        act.Should().Throw<CryptographicException>("方案验收 §11.3：无 KEK 时不可解");
    }

    [Fact]
    public void 解密失败_错误信息不携带密文材料()
    {
        var kek = NewKek();
        var aad = NewAad();
        var original = EnvelopeSecretCrypto.Encrypt("secret", aad, kek);
        var tampered = original with { Ciphertext = FlipFirstByte(original.Ciphertext) };

        var act = () => EnvelopeSecretCrypto.Decrypt(tampered, aad, kek.Key);

        var ex = act.Should().Throw<CryptographicException>().Which;
        ex.Message.Should().Contain("篡改");
        ex.Message.Should().NotContain(original.WrappedDek, "错误信息不得携带密文材料");
        ex.Message.Should().NotContain(original.Ciphertext, "错误信息不得携带密文材料");
        ex.Message.Should().NotContain("secret", "错误信息不得携带明文");
    }

    [Fact]
    public void 空明文_拒绝加密()
    {
        var act = () => EnvelopeSecretCrypto.Encrypt("", NewAad(), NewKek());
        act.Should().Throw<ArgumentException>();
    }
}
