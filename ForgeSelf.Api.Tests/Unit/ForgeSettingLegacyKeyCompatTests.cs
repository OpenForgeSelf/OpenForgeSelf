using System.Reflection;
using NewLife.Configuration;
using ForgeSelf.Api.Models;
using Xunit;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// B5（2026-11-02-b5-legacy-cleanup）AC6：ForgeSetting 旧键兼容读。
/// 历史上的运行时开关（UseMafRuntime/TaskLoopMode）从未进过 ForgeSetting（00-repo-understanding F9：全仓 grep 0 命中），
/// 本测试锁死的行为是「旧环境手改配置带入的残留键不崩、不被模型吸收、下次保存被清洗」——
/// 防未来有人把开关字段加回 ForgeSetting 时静默复活运行时分支（B5 收敛为默认常开，架构 §3.2）。
/// </summary>
/// <remarks>
/// 不触碰 <see cref="ForgeSetting.Current"/> 进程级单例（SharedGlobalState 纪律）：
/// 全程走 <see cref="XmlConfigProvider"/> 的 provider 级 Load/Save 与临时文件。
/// </remarks>
public class ForgeSettingLegacyKeyCompatTests
{
    private static string WriteTempConfig(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"forge-setting-compat-{Guid.NewGuid():N}.config");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Load_WithLegacyResidueKeys_DoesNotThrowAndIgnoresThem()
    {
        var path = WriteTempConfig("""
            <?xml version="1.0" encoding="utf-8"?>
            <ForgeSetting>
              <PortNumber>7200</PortNumber>
              <DefaultModel>OpenAI:gpt-4o</DefaultModel>
              <UseMafRuntime>true</UseMafRuntime>
              <TaskLoopMode>react</TaskLoopMode>
            </ForgeSetting>
            """);
        try
        {
            var provider = new XmlConfigProvider { FileName = path };

            // 不崩：残留键只是被忽略，不得抛异常（NewLife 绑定器对未知键静默跳过）
            // （Load 的字符串参数是配置树位置而非文件路径，文件经 FileName 指定）
            var setting = provider.Load<ForgeSetting>();

            // 已知键正常绑定（残留不影响既有键解析）
            Assert.Equal(7200, setting.PortNumber);
            Assert.Equal("OpenAI:gpt-4o", setting.DefaultModel);

            // 不生效：运行时开关字段不存在于模型（编译期已删除，运行期反射复核）
            Assert.Null(typeof(ForgeSetting).GetProperty("UseMafRuntime", BindingFlags.Public | BindingFlags.Instance));
            Assert.Null(typeof(ForgeSetting).GetProperty("TaskLoopMode", BindingFlags.Public | BindingFlags.Instance));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Save_AfterLoadingLegacyConfig_WashesOutResidueKeys()
    {
        var path = WriteTempConfig("""
            <?xml version="1.0" encoding="utf-8"?>
            <ForgeSetting>
              <PortNumber>7201</PortNumber>
              <UseMafRuntime>true</UseMafRuntime>
              <TaskLoopMode>react</TaskLoopMode>
            </ForgeSetting>
            """);
        try
        {
            var provider = new XmlConfigProvider { FileName = path };
            var setting = provider.Load<ForgeSetting>();
            setting.ProxyMode = "direct"; // 触发一次真实属性变更

            // 回写：模型无残留键属性 → 保存产物不再携带（残留被清洗，配置文件自愈）
            provider.Save(setting);
            var saved = File.ReadAllText(path);

            Assert.DoesNotContain("UseMafRuntime", saved);
            Assert.DoesNotContain("TaskLoopMode", saved);
            Assert.Contains("direct", saved);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
