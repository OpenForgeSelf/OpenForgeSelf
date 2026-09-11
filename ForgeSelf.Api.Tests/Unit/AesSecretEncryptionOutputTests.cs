using Microsoft.Extensions.Configuration;
using ForgeSelf.Api.Security;
using Xunit.Abstractions;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// 专用加密输出测试：用与后端运行一致的方式（AesSecretEncryptionService，AES-256-CBC + 随机 IV）
/// 加密指定明文并输出密文。空配置 → 与后端相同：配置/环境变量均未设置时回退内置默认密钥。
/// </summary>
public class AesSecretEncryptionOutputTests
{
    private readonly ITestOutputHelper _output;

    public AesSecretEncryptionOutputTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Encrypt_CsSkKey_OutputsCiphertext()
    {
        const string plain = "cs-sk-<REDACTED>";

        // 空配置 → 与后端运行一致：无 Encryption:Key / FORGESELF_ENCRYPTION_KEY 时回退机器派生密钥
        var encryption = new AesSecretEncryptionService(new ConfigurationBuilder().Build(), new MachineKeyProvider());

        var cipher = encryption.Encrypt(plain);

        // 往返保证：密文可用同一密钥体系解密还原
        encryption.Decrypt(cipher).Should().Be(plain);

        _output.WriteLine($"ENCRYPTED_RESULT={cipher}");
    }
}
