using System.IO.Compression;
using System.Text.Json;
using FluentAssertions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Models.Plugins;
using ForgeSelf.Api.Plugins;
using ForgeSelf.Api.Plugins.Abstractions;
using ForgeSelf.Api.Plugins.Services;
using ForgeSelf.Api.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeSelf.Api.Tests.Plugins;

/// <summary>
/// PluginVersionService 插件更新源（本地包目录）测试（2026-09-28，输入27；输入31 去 _backups）。
/// 手工构造 .forgeself-plugin 包（plugin.json + 假入口 DLL），覆盖：
/// 纯包源发现与更新 / 版本基准（高于 versions/ 现有 staged 才列）/ 包版本不高于当前 / 包 Id 不匹配 /
/// 包缺入口 DLL / 无效包跳过 / 清空目录 = 停用。
/// 全部落在 TempPluginDirectory 隔离目录（不碰真实数据根，遵守插件开发铁律 10/11）。
/// </summary>
public class PluginVersionUpdateSourceTests
{
    private const string PluginId = "srcpkgplugin";

    private readonly TempPluginDirectory _tempDir;
    private readonly string _packageSourceDir;
    private readonly PluginManager _manager;
    private readonly PluginUpdateSettingsService _updateSettings;
    private readonly PluginVersionService _service;

    public PluginVersionUpdateSourceTests()
    {
        _tempDir = new TempPluginDirectory();
        _packageSourceDir = Path.Combine(Path.GetTempPath(), $"plugin-pkg-src-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_packageSourceDir);

        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();
        _manager = new PluginManager(sp, Mock.Of<IPermissionChecker>());
        _manager.SetPluginsDirectory(_tempDir.RootPath);

        var settingsFile = Path.Combine(Path.GetTempPath(), $"pvsrc-settings-{Guid.NewGuid():N}.json");
        _updateSettings = new PluginUpdateSettingsService(new PluginUpdateSettings(), settingsFile);
        _updateSettings.Update(c => c.LocalDir = _packageSourceDir);

        _service = new PluginVersionService(_manager, _updateSettings, new PluginPackagerService(_manager));
        _service.Initialize(_tempDir.RootPath);
    }

    [Fact]
    public void PackageSource_FindsAndUpdatesHigherVersion_WhenNoStagedVersion()
    {
        CreateInstalledPlugin("1.0.0");
        var pkg = CreatePackage("2.0.0", includeEntryDll: true);
        _manager.DiscoverPlugins();

        var updates = _service.CheckForUpdates();

        var info = updates.Should().ContainSingle(u => u.PluginId == PluginId).Subject;
        info.LatestVersion.Should().Be("2.0.0");
        info.Source.Should().Be("package");

        var ok = _service.UpdatePlugin(PluginId);
        ok.Should().BeTrue();

        // 更新后：生效版本 2.0.0 + 版本化布局 current 指针
        _manager.GetPluginMetadata(PluginId)!.Version.Should().Be("2.0.0");
        var pluginDir = Path.Combine(_tempDir.RootPath, PluginId);
        Directory.Exists(Path.Combine(pluginDir, "versions", "2.0.0")).Should().BeTrue();
        File.ReadAllText(Path.Combine(pluginDir, "current")).Trim().Should().Be("2.0.0");
    }

    [Fact]
    public void PackageSource_IgnoresPackageWhenStagedHasHigherVersion()
    {
        CreateInstalledPlugin("1.0.0");
        // versions/ 已直落 3.0.0（更高）→ 包 2.0.0 不列为可更新
        var stagedDir = Path.Combine(_tempDir.RootPath, PluginId, "versions", "3.0.0");
        Directory.CreateDirectory(stagedDir);
        File.WriteAllText(Path.Combine(stagedDir, "plugin.json"),
            JsonSerializer.Serialize(new PluginMetadata
            {
                Id = PluginId,
                Name = "T",
                Version = "3.0.0",
                EntryAssembly = $"{PluginId}.dll",
                EntryType = $"{PluginId}.PluginEntry"
            }));
        CreatePackage("2.0.0", includeEntryDll: true);
        _manager.DiscoverPlugins();

        var updates = _service.CheckForUpdates();

        updates.Should().ContainSingle(u => u.PluginId == PluginId);
        updates.Single(u => u.PluginId == PluginId).LatestVersion.Should().Be("3.0.0");
        updates.Single(u => u.PluginId == PluginId).Source.Should().Be("staged");
    }

    [Fact]
    public void PackageSource_IgnoresPackageNotHigherThanCurrent()
    {
        CreateInstalledPlugin("1.0.0");
        CreatePackage("1.0.0", includeEntryDll: true);
        _manager.DiscoverPlugins();

        var updates = _service.CheckForUpdates();

        updates.Should().NotContain(u => u.PluginId == PluginId);
    }

    [Fact]
    public void PackageSource_IgnoresPackageWithDifferentPluginId()
    {
        CreateInstalledPlugin("1.0.0");
        CreatePackage("2.0.0", includeEntryDll: true, pluginId: "other-plugin");
        _manager.DiscoverPlugins();

        var updates = _service.CheckForUpdates();

        updates.Should().NotContain(u => u.PluginId == PluginId);
    }

    [Fact]
    public void PackageSource_IgnoresPackageMissingEntryDll()
    {
        CreateInstalledPlugin("1.0.0");
        CreatePackage("2.0.0", includeEntryDll: false);
        _manager.DiscoverPlugins();

        var updates = _service.CheckForUpdates();

        updates.Should().NotContain(u => u.PluginId == PluginId);
    }

    [Fact]
    public void PackageSource_IgnoresInvalidPackage()
    {
        CreateInstalledPlugin("1.0.0");
        // plugin.json 缺 Version 的无效包
        var pkgPath = Path.Combine(_packageSourceDir, $"{PluginId}-2.0.0.forgeself-plugin");
        var staging = Path.Combine(Path.GetTempPath(), $"pkg-invalid-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        File.WriteAllText(Path.Combine(staging, "plugin.json"),
            JsonSerializer.Serialize(new { id = PluginId, name = "Bad" }));
        ZipFile.CreateFromDirectory(staging, pkgPath);
        Directory.Delete(staging, true);
        _manager.DiscoverPlugins();

        var updates = _service.CheckForUpdates();

        updates.Should().NotContain(u => u.PluginId == PluginId);
    }

    [Fact]
    public void PackageSource_DisabledWhenLocalDirCleared()
    {
        CreateInstalledPlugin("1.0.0");
        CreatePackage("2.0.0", includeEntryDll: true);
        _manager.DiscoverPlugins();
        _service.CheckForUpdates().Should().ContainSingle(u => u.PluginId == PluginId);

        // 清空 LocalDir = 停用更新源
        _updateSettings.Update(c => c.LocalDir = "");

        _service.CheckForUpdates().Should().NotContain(u => u.PluginId == PluginId);
    }

    private void CreateInstalledPlugin(string version)
    {
        _tempDir.CreatePluginManifest(PluginId, m =>
        {
            m.Version = version;
            m.EntryAssembly = $"{PluginId}.dll";
            m.EntryType = $"{PluginId}.PluginEntry";
        });
        _tempDir.CreateFakeAssembly(Path.Combine(_tempDir.RootPath, PluginId), $"{PluginId}.dll");
    }

    private string CreatePackage(string version, bool includeEntryDll, string pluginId = PluginId)
    {
        var staging = Path.Combine(Path.GetTempPath(), $"pkg-stage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);

        var manifest = new
        {
            id = pluginId,
            name = $"Package {pluginId}",
            version,
            entryAssembly = $"{pluginId}.dll",
            entryType = $"{pluginId}.PluginEntry"
        };
        File.WriteAllText(Path.Combine(staging, "plugin.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        if (includeEntryDll)
            File.WriteAllBytes(Path.Combine(staging, $"{pluginId}.dll"), new byte[] { 0x4D, 0x5A });

        var pkgPath = Path.Combine(_packageSourceDir, $"{pluginId}-{version}.forgeself-plugin");
        ZipFile.CreateFromDirectory(staging, pkgPath);
        Directory.Delete(staging, true);
        return pkgPath;
    }
}
