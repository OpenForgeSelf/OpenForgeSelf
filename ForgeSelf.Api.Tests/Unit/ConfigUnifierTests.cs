using ForgeSelf.Api;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// B9-5 门禁：ConfigUnifier 启动期健壮化——配置目录不可写/不可建时<b>降级 + 告警</b>，
/// 不抛异常炸掉 WebApplicationFactory / 宿主启动（B7/B8 两次实证 UnauthorizedAccessException 全量爆）。
/// </summary>
public class ConfigUnifierTests
{
    [Fact]
    public void UnifyAllConfigFiles_DirectoryPathIsExistingFile_DoesNotThrow()
    {
        // Arrange：configDirectory 传入一个已存在的「文件」路径——Directory.CreateDirectory 必失败，
        // 旧实现直接抛 IOException/UnauthorizedAccessException 炸启动；健壮化后降级为告警并返回。
        var filePath = Path.Combine(Path.GetTempPath(), $"configunifier-tests-{Guid.NewGuid():N}.config");
        try
        {
            File.WriteAllText(filePath, "i am a file, not a directory");

            // Act + Assert：不抛
            ConfigUnifier.UnifyAllConfigFiles(filePath);
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
