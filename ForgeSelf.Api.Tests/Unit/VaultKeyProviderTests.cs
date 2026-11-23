using System.Security.Cryptography;
using ForgeSelf.Api.Security;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// VaultKeyProvider 测试（PILOT-086 FR-5 / AC-5 / AC-6）。
/// KEK：随机生成、同目录稳定、跨目录不同、文件与 SQLite 分离、损坏 Fail Closed。
/// </summary>
public class VaultKeyProviderTests
{
    private static string NewKeyDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vaultkek_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void 空目录参数_抛ArgumentException()
    {
        var act = () => new VaultKeyProvider("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void 同一目录_两实例取同一把KEK_且KeyId稳定()
    {
        var dir = NewKeyDir();
        try
        {
            var p1 = new VaultKeyProvider(dir);
            var m1 = p1.GetCurrentKey();

            var p2 = new VaultKeyProvider(dir); // 模拟进程重启：重新从文件加载
            var m2 = p2.GetCurrentKey();

            m2.KeyId.Should().Be(m1.KeyId);
            m2.Key.Should().Equal(m1.Key);
            m2.ProtectionMode.Should().Be(m1.ProtectionMode);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void 不同目录_密钥互不相同_随机生成非机器派生()
    {
        var dirA = NewKeyDir();
        var dirB = NewKeyDir();
        try
        {
            var a = new VaultKeyProvider(dirA).GetCurrentKey();
            var b = new VaultKeyProvider(dirB).GetCurrentKey();

            a.Key.Should().NotEqual(b.Key);
            a.KeyId.Should().NotBe(b.KeyId);
            a.KeyId.Should().MatchRegex("^kek1-[0-9a-f]{8}$");
        }
        finally
        {
            Directory.Delete(dirA, recursive: true);
            Directory.Delete(dirB, recursive: true);
        }
    }

    [Fact]
    public void KEK文件_落在指定目录且不落明文可读形态_DPAPI下为密文_非Windows下0600()
    {
        var dir = NewKeyDir();
        try
        {
            var m = new VaultKeyProvider(dir).GetCurrentKey();
            var file = Path.Combine(dir, VaultKeyProvider.KeyFileName);

            File.Exists(file).Should().BeTrue("KEK 必须持久化到指定目录（与 SQLite 分离）");

            var fileBytes = File.ReadAllBytes(file);
            if (OperatingSystem.IsWindows())
            {
                // DPAPI：文件是密文（长度 > 32 且与明文不同），无法直接读出 KEK
                fileBytes.Length.Should().BeGreaterThan(32, "DPAPI 密文含头部，不应是裸 32 字节");
                fileBytes.Should().NotEqual(m.Key, "Windows 下 KEK 不得以明文落盘");
                m.ProtectionMode.Should().Be(VaultKeyProvider.ModeDpapi);
            }
            else
            {
                // 非 Windows：文件即 KEK（已声明限制），但权限必须 0600
                var mode = File.GetUnixFileMode(file);
                (mode & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute))
                    .Should().Be(UnixFileMode.None, "非 Windows 下 KEK 文件必须 0600");
                m.ProtectionMode.Should().Be(VaultKeyProvider.ModeProtectedFile);
            }
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void KEK文件损坏_FailClosed_抛CryptographicException_不回退明文()
    {
        var dir = NewKeyDir();
        try
        {
            File.WriteAllBytes(Path.Combine(dir, VaultKeyProvider.KeyFileName), RandomNumberGenerator.GetBytes(10));

            var act = () => new VaultKeyProvider(dir).GetCurrentKey();
            act.Should().Throw<CryptographicException>();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
