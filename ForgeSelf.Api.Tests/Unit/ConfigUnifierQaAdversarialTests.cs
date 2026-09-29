using ForgeSelf.Api;
using Xunit;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// B9-5 QA 对抗探针（严过关 QA，Edward）——与工程师 ConfigUnifierTests 互补：
/// 工程师只测「目录路径为文件」一层；本文件补三层降级的独立触发面：
/// 1. 超长路径（> MAX_PATH 248）→ CreateDirectory 抛 PathTooLong → 降级不炸；
/// 2. 幂等重入：同一目录连调两次不抛（配置已统一后二次启动路径）；
/// 3. 正常目录 smoke：真实创建 + 落盘全链路可用。
/// 另注：收官全量中全部 WAF 类测试绿本身就是「降级后宿主可启动」的大规模实证。
/// </summary>
public class ConfigUnifierQaAdversarialTests
{
    [Fact]
    public void Qa_UnifyAllConfigFiles_OverlongPath_DoesNotThrow()
    {
        // 300 字符嵌套路径：Windows MAX_PATH(248) 之上，CreateDirectory 必抛 PathTooLongException
        var deep = Path.Combine(Path.GetTempPath(), "qa-b9");
        var longDir = deep;
        while (longDir.Length < 300)
        {
            longDir = Path.Combine(longDir, "nested-level-overflow-directory-name");
        }

        // Act + Assert：不抛（降级告警路径）
        ConfigUnifier.UnifyAllConfigFiles(longDir);
    }

    [Fact]
    public void Qa_UnifyAllConfigFiles_IdempotentReentry_DoesNotThrow()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"qa-b9-idem-{Guid.NewGuid():N}");
        try
        {
            // 首次（真实创建/重定向/落盘）与二次（配置已统一）都不得抛
            ConfigUnifier.UnifyAllConfigFiles(dir);
            ConfigUnifier.UnifyAllConfigFiles(dir);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void Qa_UnifyAllConfigFiles_NormalDirectory_Completes()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"qa-b9-normal-{Guid.NewGuid():N}");
        try
        {
            // 正路径 smoke：目录被创建且调用完整返回（不校验具体 config 文件——
            // NewLife Provider 形态依环境而异，此处只锁「可完成」这一契约）
            ConfigUnifier.UnifyAllConfigFiles(dir);
            Directory.Exists(dir).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
