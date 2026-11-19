namespace ForgeSelf.Api.Security;

/// <summary>
/// 机器派生密钥提供器。
/// 用本机稳定的机器标识（Windows MachineGuid / Linux machine-id / 计算机名）经 PBKDF2 派生出
/// 32 字节对称密钥，使密文「出本机即不可解」，从而避免把密钥硬编码在程序里。
/// </summary>
/// <remarks>
/// 熵源刻意<b>不包含用户 SID</b>：本机既可能以桌面交互模式运行，也可能以 Windows 服务
/// （LocalSystem）模式运行，两种模式的用户 SID 不同。若纳入 SID，同一份密文在两种运行形态下
/// 会派生出不同密钥，导致「服务模式读不到桌面模式写的密文」的密钥漂移故障。
/// 机器级标识（HKLM MachineGuid）在两种模式下读到同一个值，故只取机器级熵源。
/// </remarks>
public interface IMachineKeyProvider
{
    /// <summary>
    /// 取得本机派生的对称密钥（32 字节 / 256 bit）。
    /// 每次调用返回副本，调用方可安全持有；密钥本体在进程内只派生一次并缓存。
    /// </summary>
    /// <returns>32 字节密钥副本</returns>
    byte[] GetMachineKey();

    /// <summary>
    /// 取得密钥指纹（<c>SHA256(密钥)</c> 的前 8 位小写 hex）。
    /// 供日志比对「两台机器/两个进程是否用同一把密钥」使用；
    /// <b>严禁</b>在日志中打印密钥本体或熵源原文。
    /// </summary>
    /// <returns>8 位小写 hex 指纹</returns>
    string GetKeyFingerprint();
}
