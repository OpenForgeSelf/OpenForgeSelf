using System.Reflection;
using ForgeSelf.Api.Security;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// 机器派生密钥提供器测试：确定性、长度、指纹非明文、熵源降级可达。
/// 本测试不碰 XCode、不碰进程级配置文件，故无需归入任何串行集合。
/// </summary>
public class MachineKeyProviderTests
{
    [Fact]
    public void GetMachineKey_同进程两次调用_结果一致()
    {
        var provider = new MachineKeyProvider();

        var first = provider.GetMachineKey();
        var second = provider.GetMachineKey();

        second.Should().Equal(first, "密钥派生结果必须确定，否则重启后旧密文将不可解");
    }

    [Fact]
    public void GetMachineKey_返回32字节AES256密钥()
    {
        new MachineKeyProvider().GetMachineKey().Should().HaveCount(32);
    }

    [Fact]
    public void GetMachineKey_返回副本_外部修改不污染内部缓存()
    {
        var provider = new MachineKeyProvider();

        var key = provider.GetMachineKey();
        key[0] = (byte)(key[0] ^ 0xFF);

        provider.GetMachineKey().Should().NotEqual(key, "必须克隆返回，避免调用方改写内部缓存的密钥");
    }

    [Fact]
    public void GetKeyFingerprint_为8位小写hex()
    {
        var provider = new MachineKeyProvider();
        var fingerprint = provider.GetKeyFingerprint();

        fingerprint.Should().HaveLength(8);
        fingerprint.Should().MatchRegex("^[0-9a-f]{8}$");
        // 指纹绝不等于密钥本体（长度与内容都不该是密钥）
        fingerprint.Should().NotBe(Convert.ToHexString(provider.GetMachineKey()));
    }

    [Fact]
    public void CollectEntropy_非空且带ForgeSelf前缀()
    {
        var entropy = (string)InvokePrivateStatic("CollectEntropy");

        entropy.Should().StartWith("ForgeSelf|");
        // 前缀之外必须有真实熵值（说明至少命中了一个熵源）
        entropy.Should().NotBe("ForgeSelf|");
    }

    [Fact]
    public void Derive_同一熵确定_不同熵不同()
    {
        var a = (byte[])InvokePrivateStatic("Derive", "ForgeSelf|entropy-a");
        var b = (byte[])InvokePrivateStatic("Derive", "ForgeSelf|entropy-a");
        var c = (byte[])InvokePrivateStatic("Derive", "ForgeSelf|entropy-b");

        a.Should().HaveCount(32);
        a.Should().Equal(b, "PBKDF2 对同一熵必须派生出同一密钥");
        a.Should().NotEqual(c, "不同熵必须派生出不同密钥，否则等于密钥写死");
    }

    /// <summary>
    /// 调用 <see cref="MachineKeyProvider"/> 的私有静态方法，避免为测试而放宽可见性。
    /// </summary>
    private static object InvokePrivateStatic(string methodName, params object?[] args)
    {
        var method = typeof(MachineKeyProvider).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull($"私有静态方法 {methodName} 必须存在");
        return method!.Invoke(null, args)!;
    }
}
