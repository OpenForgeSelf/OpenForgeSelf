using ForgeSelf.Abstractions;
using NewLife.Log;
using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Security;

namespace ForgeSelf.Api.Services;

/// <summary>
/// 启动期一次性密文迁移服务：把 v1 旧密文（无 <c>v2:</c> 前缀）用当前密钥重封装为 v2。
/// </summary>
/// <remarks>
/// <para><b>定位</b>：这是<b>可选优化</b>，不是可用性前置条件。即便迁移整体失败，
/// <see cref="AesSecretEncryptionService.Decrypt"/> 的 v1 回退链仍保证老密文永久可读，产品功能不受影响。
/// 迁移的真实价值是「让旧硬编码串彻底不再保护任何数据」。</para>
/// <para><b>幂等</b>：以 <see cref="ForgeSetting.SecretMigrationVersion"/> 为标记，<c>&lt; 2</c> 才执行，
/// 成功后置 2 并保存。刻意不复用 <c>IsFirstInit</c>（其语义必须保持不变）。</para>
/// <para><b>失败策略</b>：单条解密失败只记 WARN 并跳过，<b>绝不写回、绝不生成假数据</b>，
/// 保留原 v1 值等下次启动再试；全程 try/catch，迁移异常<b>绝不阻断启动</b>。</para>
/// </remarks>
public class SecretMigrationService
{
    /// <summary>当前迁移版本号。小于此值才执行迁移</summary>
    public const int CurrentVersion = 2;

    private readonly ISecretEncryptionService _encryption;

    public SecretMigrationService(ISecretEncryptionService encryption)
    {
        _encryption = encryption;
    }

    /// <summary>
    /// 执行一次密文迁移扫描。
    /// 扫描范围：<see cref="ForgeSetting.ApiToken"/> + <see cref="AIProvider"/> 表全部行的 <c>ApiKey</c>。
    /// 判定：值非空且不以 <c>v2:</c> 开头 → 需迁移。
    /// </summary>
    /// <returns>成功重封装的条数；已达版本标记返回 0</returns>
    public int MigrateOnce()
    {
        var setting = ForgeSetting.Current;
        if (setting.SecretMigrationVersion >= CurrentVersion) return 0;

        var success = 0;
        var skipped = 0;
        var failed = 0;

        try
        {
            var settingResult = MigrateSetting();
            success += settingResult.Success;
            skipped += settingResult.Skipped;
            failed += settingResult.Failed;

            var providerResult = MigrateProviders();
            success += providerResult.Success;
            skipped += providerResult.Skipped;
            failed += providerResult.Failed;

            // 无论是否有失败项都推进版本：失败项已被跳过并保留原值，
            // 下次启动不会再重复处理已成功的部分（跳过项自然仍会被再次扫描到时也是幂等的）
            setting.SecretMigrationVersion = CurrentVersion;
            setting.Save();
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("密文迁移失败（已跳过，不影响功能）: {0}", ex.Message);
        }

        XTrace.Log.Info("密文迁移完成：成功 {0} 条，跳过 {1} 条，失败 {2} 条", success, skipped, failed);
        return success;
    }

    /// <summary>
    /// 迁移 <see cref="ForgeSetting.ApiToken"/>。
    /// 空值或已是 v2 → 跳过；解密失败 → 记 WARN 并保留原值。
    /// </summary>
    private (int Success, int Skipped, int Failed) MigrateSetting()
    {
        var setting = ForgeSetting.Current;
        var cipher = setting.ApiToken;

        if (string.IsNullOrEmpty(cipher) || cipher.StartsWith(AesSecretEncryptionService.V2Prefix, StringComparison.Ordinal))
            return (0, 1, 0);

        if (!_encryption.TryDecrypt(cipher, out var plain) || string.IsNullOrEmpty(plain))
        {
            XTrace.Log.Warn("ForgeSetting.ApiToken 的旧密文无法在本机解密，保留原值（可能由其它机器加密）");
            return (0, 0, 1);
        }

        if (!IsSafeToRewrite(plain))
        {
            XTrace.Log.Warn("ForgeSetting.ApiToken 的旧密文解密结果可疑（疑似错误密钥误判），保留原值不写回");
            return (0, 0, 1);
        }

        setting.ApiToken = _encryption.Encrypt(plain);
        setting.Save();
        return (1, 0, 0);
    }

    /// <summary>
    /// 迁移 <see cref="AIProvider"/> 表全部行的 <c>ApiKey</c>。
    /// 表不存在或查询异常时安全返回「全部跳过」，绝不影响启动。
    /// </summary>
    private (int Success, int Skipped, int Failed) MigrateProviders()
    {
        var success = 0;
        var skipped = 0;
        var failed = 0;

        try
        {
            var providers = AIProvider.FindAll();
            foreach (var provider in providers)
            {
                var cipher = provider.ApiKey;
                if (string.IsNullOrEmpty(cipher) || cipher.StartsWith(AesSecretEncryptionService.V2Prefix, StringComparison.Ordinal))
                {
                    skipped++;
                    continue;
                }

                if (!_encryption.TryDecrypt(cipher, out var plain) || string.IsNullOrEmpty(plain))
                {
                    XTrace.Log.Warn("AIProvider[{0}] 的旧密文无法在本机解密，跳过该行（保留原值）", provider.Id);
                    failed++;
                    continue;
                }

                if (!IsSafeToRewrite(plain))
                {
                    XTrace.Log.Warn("AIProvider[{0}] 的旧密文解密结果可疑（疑似错误密钥误判），跳过该行（保留原值）", provider.Id);
                    failed++;
                    continue;
                }

                provider.ApiKey = _encryption.Encrypt(plain);
                provider.Save();
                success++;
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("扫描 AIProvider 密文失败（已跳过，不影响功能）: {0}", ex.Message);
        }

        return (success, skipped, failed);
    }

    /// <summary>
    /// 迁移写回前的最后一道闸：只有「明文合理」且「往返一致」才允许覆盖原值。
    /// </summary>
    /// <remarks>
    /// 迁移是<b>唯一</b>会覆盖原值的破坏性路径——原值一旦被乱码覆盖、版本号又被推进，就<b>永不重试</b>，
    /// 等于静默数据丢失。所以这里宁可误判为「不安全」走「保留原值」分支：
    /// 保留 v1 不会伤功能（v1 回退链仍在），丢失明文才会。
    /// 合理性判据复用 <see cref="AesSecretEncryptionService.IsPlausiblePlaintext"/>（冗余防御，正常已由解密侧保证）。
    /// </remarks>
    private bool IsSafeToRewrite(string plain)
    {
        if (string.IsNullOrEmpty(plain)) return false;

        if (!AesSecretEncryptionService.IsPlausiblePlaintext(plain)) return false;

        // 往返一致：证明「即将写入的新密文」确实能解回同一明文，杜绝写出无法还原的数据
        return _encryption.TryDecrypt(_encryption.Encrypt(plain), out var roundTrip) && roundTrip == plain;
    }
}
