using FluentAssertions;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Services;

namespace ForgeSelf.Api.Tests.Services;

/// <summary>
/// UpdateSettingsService 测试（2026-09-27：更新地址运行时可变 + 落盘重载）。
/// </summary>
public class UpdateSettingsServiceTests
{
    [Fact]
    public void Update_MutatesSharedInstance()
    {
        var settingsFile = SettingsFilePath();
        var initial = new UpdateConfig { Provider = "github", GitHubRepo = "OpenForgeSelf/OpenForgeSelf", LocalDir = "" };
        var service = new UpdateSettingsService(initial, settingsFile);

        service.Update(c => { c.Provider = "local"; c.LocalDir = "D:\\updates"; });

        service.Current.Provider.Should().Be("local");
        service.Current.LocalDir.Should().Be("D:\\updates");
    }

    [Fact]
    public void NewInstance_LoadsPersistedConfig()
    {
        var settingsFile = SettingsFilePath();
        var initial = new UpdateConfig { Provider = "github", GitHubRepo = "OpenForgeSelf/OpenForgeSelf", LocalDir = "" };
        var service = new UpdateSettingsService(initial, settingsFile);

        service.Update(c => { c.Provider = "local"; c.LocalDir = "D:\\updates"; });

        // 模拟重启：用另一份初始配置构造新实例，用户配置文件（整份快照）覆盖 appsettings 基座
        var freshInitial = new UpdateConfig { Provider = "github", GitHubRepo = "other/other", LocalDir = "" };
        var fresh = new UpdateSettingsService(freshInitial, settingsFile);

        fresh.Current.Provider.Should().Be("local");
        fresh.Current.LocalDir.Should().Be("D:\\updates");
        // 快照语义：用户保存过的字段一律以快照为准（而非基座），GitHubRepo 来自首次保存时的整份配置
        fresh.Current.GitHubRepo.Should().Be("OpenForgeSelf/OpenForgeSelf");
    }

    [Fact]
    public void MissingSettingsFile_FallsBackToInitial()
    {
        var settingsFile = Path.Combine(
            Path.GetTempPath(), $"forge-settings-missing-{Guid.NewGuid():N}.json");
        var initial = new UpdateConfig { Provider = "github", GitHubRepo = "OpenForgeSelf/OpenForgeSelf" };

        var service = new UpdateSettingsService(initial, settingsFile);

        service.Current.Provider.Should().Be("github");
        service.Current.GitHubRepo.Should().Be("OpenForgeSelf/OpenForgeSelf");
    }

    private static string SettingsFilePath()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"forge-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "update-settings.json");
    }
}
