using FluentAssertions;
using ForgeSelf.Api.Models.Plugins;
using ForgeSelf.Api.Services;

namespace ForgeSelf.Api.Tests.Services;

/// <summary>
/// PluginUpdateSettingsService 测试（2026-09-28，输入27：插件本地目录更新源配置运行时可变 + 落盘重载）。
/// </summary>
public class PluginUpdateSettingsServiceTests
{
    [Fact]
    public void Default_IsUnconfigured()
    {
        var settingsFile = SettingsFilePath();
        var service = new PluginUpdateSettingsService(new PluginUpdateSettings(), settingsFile);

        service.Current.LocalDir.Should().Be("");
    }

    [Fact]
    public void Update_MutatesSharedInstance()
    {
        var settingsFile = SettingsFilePath();
        var service = new PluginUpdateSettingsService(new PluginUpdateSettings(), settingsFile);

        service.Update(c => c.LocalDir = "D:\\plugin-packages");

        service.Current.LocalDir.Should().Be("D:\\plugin-packages");
    }

    [Fact]
    public void NewInstance_LoadsPersistedConfig()
    {
        var settingsFile = SettingsFilePath();
        var service = new PluginUpdateSettingsService(new PluginUpdateSettings(), settingsFile);

        service.Update(c => c.LocalDir = "D:\\plugin-packages");

        // 模拟重启：新实例从用户配置读回（快照语义）
        var fresh = new PluginUpdateSettingsService(new PluginUpdateSettings(), settingsFile);

        fresh.Current.LocalDir.Should().Be("D:\\plugin-packages");
    }

    [Fact]
    public void ClearLocalDir_DisablesUpdateSource()
    {
        var settingsFile = SettingsFilePath();
        var service = new PluginUpdateSettingsService(new PluginUpdateSettings(), settingsFile);
        service.Update(c => c.LocalDir = "D:\\plugin-packages");

        service.Update(c => c.LocalDir = "");

        service.Current.LocalDir.Should().Be("");

        var fresh = new PluginUpdateSettingsService(new PluginUpdateSettings(), settingsFile);
        fresh.Current.LocalDir.Should().Be("");
    }

    [Fact]
    public void MissingSettingsFile_FallsBackToInitial()
    {
        var settingsFile = Path.Combine(
            Path.GetTempPath(), $"plugin-settings-missing-{Guid.NewGuid():N}.json");

        var service = new PluginUpdateSettingsService(new PluginUpdateSettings(), settingsFile);

        service.Current.LocalDir.Should().Be("");
    }

    private static string SettingsFilePath()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"plugin-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "plugin-update-settings.json");
    }
}
