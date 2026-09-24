using FluentAssertions;
using ForgeSelf.Api.Plugins.AgentHub.Profiles;
using ForgeSelf.Api.Plugins.AgentHub.Services;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AgentHub;

/// <summary>
/// AgentProbeService 探测 / AgentHubSettingsStore 设置存储测试。
///
/// 覆盖要点（输入 5：探测支持附加扫描目录）：
/// - 附加扫描目录命中：CLI 装在不进 PATH 的目录（pnpm/bun/scoop/自定义安装）也能被 ResolveExecutable 找到；
/// - Windows 扩展名补全在附加目录同样生效（codex → codex.exe）；
/// - 绝对路径直接命中（回归）；
/// - 设置存储：默认值 / 保存落盘 / 重新加载读回 / 规范化（空白、重复、尾分隔符剔除）/ 损坏文件不抛且退回默认。
///
/// 数据安全铁律（plugin-development §四 铁律 10）：测试临时目录只创建、只使用，永不删除。
/// </summary>
public class AgentHubProbeSettingsTests
{
    /// <summary>独立的随机临时目录（随机后缀，永不删除）。</summary>
    private static String TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"agenthub-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>构造探测服务：注册表用 mock，profile 加载器不加载任何 profile，设置指向指定目录。</summary>
    private static AgentProbeService MakeProbe(String configPath, params String[] searchDirectories)
    {
        var store = new AgentHubSettingsStore(configPath);
        if (searchDirectories.Length > 0)
        {
            store.Save(new AgentHubSettings { SearchDirectories = searchDirectories.ToList() });
        }
        return new AgentProbeService(Mock.Of<IAgentRegistry>(), new ProfileLoader(), store);
    }

    // ────────────────────────── ResolveExecutable ──────────────────────────

    [Fact]
    public void ResolveExecutable_附加扫描目录_非PATH安装也能命中()
    {
        var dir = TempDir();
        var name = $"agenthub-agent-{Guid.NewGuid():N}";
        var exe = Path.Combine(dir, name + ".exe");
        File.WriteAllBytes(exe, []);

        var probe = MakeProbe(Path.Combine(TempDir(), "config.json"), dir);

        // 附加目录里的可执行文件，即使不在 PATH 也应被解析到完整路径
        probe.ResolveExecutable(name).Should().Be(exe);
    }

    [Fact]
    public void ResolveExecutable_附加扫描目录_Windows扩展名补全生效()
    {
        var dir = TempDir();
        var name = $"agenthub-agent-{Guid.NewGuid():N}";
        var exe = Path.Combine(dir, name + ".cmd");
        File.WriteAllBytes(exe, []);

        var probe = MakeProbe(Path.Combine(TempDir(), "config.json"), dir);

        // 无扩展名写法也能在附加目录里补全 .cmd
        probe.ResolveExecutable(name).Should().Be(exe);
    }

    [Fact]
    public void ResolveExecutable_绝对路径_直接命中()
    {
        var dir = TempDir();
        var exe = Path.Combine(dir, "claude.exe");
        File.WriteAllBytes(exe, []);

        var probe = MakeProbe(Path.Combine(TempDir(), "config.json"));

        probe.ResolveExecutable(exe).Should().Be(exe);
    }

    [Fact]
    public void ResolveExecutable_附加目录为空或不存在_返回null不抛()
    {
        var probe = MakeProbe(Path.Combine(TempDir(), "config.json"));
        probe.ResolveExecutable("definitely-not-installed-agent-xyz").Should().BeNull();

        // 配置里给了一个不存在的目录，不应抛异常
        var probe2 = MakeProbe(Path.Combine(TempDir(), "config.json"), Path.Combine(TempDir(), "no-such-dir"));
        probe2.ResolveExecutable("definitely-not-installed-agent-xyz").Should().BeNull();
    }

    // ────────────────────────── AgentHubSettingsStore ──────────────────────────

    [Fact]
    public void SettingsStore_文件不存在_默认空目录列表()
    {
        var store = new AgentHubSettingsStore(Path.Combine(TempDir(), "config.json"));
        store.Current.SearchDirectories.Should().BeEmpty();
    }

    [Fact]
    public void SettingsStore_保存落盘_新实例重载读回()
    {
        var dir = TempDir();
        var configPath = Path.Combine(dir, "config.json");
        var store = new AgentHubSettingsStore(configPath);

        store.Save(new AgentHubSettings { SearchDirectories = [dir] });

        // 文件真实落盘
        File.Exists(configPath).Should().BeTrue("保存后 config.json 应落盘");

        // 新实例（模拟重启/热重载）应读回
        var reloaded = new AgentHubSettingsStore(configPath);
        reloaded.Current.SearchDirectories.Should().Equal([dir]);
    }

    [Fact]
    public void SettingsStore_规范化_去空白去重去尾分隔符()
    {
        var store = new AgentHubSettingsStore(Path.Combine(TempDir(), "config.json"));
        var saved = store.Save(new AgentHubSettings
        {
            SearchDirectories =
            [
                "  C:\\Tools\\Agent  ",
                "C:\\Tools\\Agent\\",
                "C:\\Tools\\Agent",
                "",
                "   ",
            ]
        });

        saved.SearchDirectories.Should().Equal(["C:\\Tools\\Agent"]);
    }

    [Fact]
    public void SettingsStore_损坏文件_退回默认且不抛()
    {
        var dir = TempDir();
        var configPath = Path.Combine(dir, "config.json");
        File.WriteAllText(configPath, "{ 这不是合法 JSON !!!");

        var store = new AgentHubSettingsStore(configPath);
        store.Current.SearchDirectories.Should().BeEmpty("损坏配置应退回默认，不阻断插件");
    }
}
