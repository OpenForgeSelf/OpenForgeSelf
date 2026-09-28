using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins;
using ForgeSelf.Api.Plugins.Abstractions;
using ForgeSelf.Api.Plugins.Services;
using ForgeSelf.Api.Models.Plugins;
using ForgeSelf.Api.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeSelf.Api.Tests.Plugins;

/// <summary>
/// PluginVersionService 版本化布局测试。
/// 2026-09-28 输入31 去 _backups：新版本直落 stage 到 versions/&lt;ver&gt;/（无备份目录），
/// 更新 = 激活已 staged 版本（切 current 指针 + 同步清单 + 热切换），回滚走 versions/ 内保留版本。
/// </summary>
public class PluginVersionServiceTests
{
    private readonly TempPluginDirectory _tempDir;
    private readonly PluginManager _manager;
    private readonly PluginVersionService _service;

    public PluginVersionServiceTests()
    {
        _tempDir = new TempPluginDirectory();
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();
        _manager = new PluginManager(sp, Mock.Of<IPermissionChecker>());
        _manager.SetPluginsDirectory(_tempDir.RootPath);
        _service = new PluginVersionService(_manager,
            new PluginUpdateSettingsService(new PluginUpdateSettings(), Path.Combine(Path.GetTempPath(), "pvs-plugin-update-settings-test.json")),
            new PluginPackagerService(_manager));
        _service.Initialize(_tempDir.RootPath);
    }

    /// <summary>把 <c>version</c> 直落 stage 到 versions/&lt;ver&gt;/（等同包源/侧载产物的磁盘形态）。</summary>
    private static void StageVersion(TempPluginDirectory tempDir, string pluginId, string version)
    {
        var versionDir = Path.Combine(tempDir.RootPath, pluginId, "versions", version);
        Directory.CreateDirectory(versionDir);
        File.WriteAllText(Path.Combine(versionDir, "plugin.json"),
            JsonSerializer.Serialize(new PluginMetadata
            {
                Id = pluginId,
                Name = "Test",
                Version = version,
                EntryAssembly = "fake.dll",
                EntryType = "fake.Plugin"
            }));
        File.WriteAllBytes(Path.Combine(versionDir, "fake.dll"), new byte[] { 1, 2, 3 });
    }

    [Fact]
    public void UpdatePlugin_StagesNewVersionAndSwitchesCurrentPointer()
    {
        var pluginId = "test.version.plugin";
        _tempDir.CreatePluginManifest(pluginId, m =>
        {
            m.Version = "1.0.0";
            m.EntryAssembly = "fake.dll";
            m.EntryType = "fake.Plugin";
        });
        _manager.DiscoverPlugins();

        // 直落产物：versions/<id>/2.0.0/（去 _backups 后 side-by-side 唯一 stage 位置）
        StageVersion(_tempDir, pluginId, "2.0.0");

        var result = _service.UpdatePlugin(pluginId);

        result.Should().BeTrue();

        var pluginDir = Path.Combine(_tempDir.RootPath, pluginId);
        Directory.Exists(Path.Combine(pluginDir, "versions", "2.0.0")).Should().BeTrue();
        File.Exists(Path.Combine(pluginDir, "versions", "2.0.0", "fake.dll")).Should().BeTrue();
        PluginVersionLayout.ReadCurrentVersion(pluginDir).Should().Be("2.0.0");

        // 活动清单已同步到新版本
        var activeManifest = JsonSerializer.Deserialize<PluginMetadata>(
            File.ReadAllText(Path.Combine(pluginDir, "plugin.json")));
        activeManifest!.Version.Should().Be("2.0.0");

        // 内存元数据已刷新
        _manager.GetPluginMetadata(pluginId)!.Version.Should().Be("2.0.0");
    }

    [Fact]
    public void UpdatePlugin_AlreadyLatest_ReturnsTrueWithoutChanges()
    {
        var pluginId = "test.latest.plugin";
        _tempDir.CreatePluginManifest(pluginId, m => m.Version = "2.0.0");
        _manager.DiscoverPlugins();

        // versions/ 内已有与当前相同的版本目录（如历史 stage 残留）→ 无更高版本，不激活
        StageVersion(_tempDir, pluginId, "2.0.0");

        var result = _service.UpdatePlugin(pluginId);

        result.Should().BeTrue();
        PluginVersionLayout.ReadCurrentVersion(Path.Combine(_tempDir.RootPath, pluginId))
            .Should().BeNull();
    }

    [Fact]
    public void RollbackPlugin_SwitchesCurrentToExistingVersion()
    {
        var pluginId = "test.rollback.plugin";
        _tempDir.CreatePluginManifest(pluginId, m =>
        {
            m.Version = "2.0.0";
            m.EntryAssembly = "fake.dll";
            m.EntryType = "fake.Plugin";
        });
        _manager.DiscoverPlugins();

        // 预置两个已安装版本
        var pluginDir = Path.Combine(_tempDir.RootPath, pluginId);
        var v1 = Path.Combine(pluginDir, "versions", "1.0.0");
        var v2 = Path.Combine(pluginDir, "versions", "2.0.0");
        Directory.CreateDirectory(v1);
        Directory.CreateDirectory(v2);
        File.WriteAllBytes(Path.Combine(v1, "fake.dll"), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(v2, "fake.dll"), new byte[] { 2 });
        File.WriteAllText(Path.Combine(v1, "plugin.json"),
            JsonSerializer.Serialize(new PluginMetadata
            {
                Id = pluginId,
                Name = "T",
                Version = "1.0.0",
                EntryAssembly = "fake.dll",
                EntryType = "fake.Plugin"
            }));
        File.WriteAllText(Path.Combine(v2, "plugin.json"),
            JsonSerializer.Serialize(new PluginMetadata
            {
                Id = pluginId,
                Name = "T",
                Version = "2.0.0",
                EntryAssembly = "fake.dll",
                EntryType = "fake.Plugin"
            }));
        PluginVersionLayout.WriteCurrentVersion(pluginDir, "2.0.0");

        var result = _service.RollbackPlugin(pluginId, "1.0.0");

        result.Should().BeTrue();
        PluginVersionLayout.ReadCurrentVersion(pluginDir).Should().Be("1.0.0");
        _manager.GetPluginMetadata(pluginId)!.Version.Should().Be("1.0.0");
    }

    [Fact]
    public void RollbackPlugin_UnknownVersion_ReturnsFalseWithoutChanges()
    {
        var pluginId = "test.rollback.unknown";
        _tempDir.CreatePluginManifest(pluginId, m => m.Version = "1.0.0");
        _manager.DiscoverPlugins();

        // 去 _backups 后：versions/ 内不存在该版本 → 无备份可恢复，直接失败
        var result = _service.RollbackPlugin(pluginId, "9.9.9");

        result.Should().BeFalse();
        PluginVersionLayout.ReadCurrentVersion(Path.Combine(_tempDir.RootPath, pluginId))
            .Should().BeNull();
    }

    [Fact]
    public void UpdatePlugin_PrunesOlderVersions_KeepsTwoMostRecent()
    {
        var pluginId = "test.prune.plugin";
        _tempDir.CreatePluginManifest(pluginId, m =>
        {
            m.Version = "1.0.0";
            m.EntryAssembly = "fake.dll";
            m.EntryType = "fake.Plugin";
        });
        _manager.DiscoverPlugins();

        // 依次直落 2.0.0 / 3.0.0 / 4.0.0 并逐个激活
        foreach (var version in new[] { "2.0.0", "3.0.0", "4.0.0" })
        {
            StageVersion(_tempDir, pluginId, version);
            _service.UpdatePlugin(pluginId).Should().BeTrue();
        }

        var versionsDir = Path.Combine(_tempDir.RootPath, pluginId, "versions");
        Directory.GetDirectories(versionsDir).Select(Path.GetFileName)
            .Should().BeEquivalentTo("3.0.0", "4.0.0");
        PluginVersionLayout.ReadCurrentVersion(Path.Combine(_tempDir.RootPath, pluginId))
            .Should().Be("4.0.0");
    }

    [Fact]
    public void GetPluginVersions_ListsAllVersionDirectoriesIncludingStaged()
    {
        var pluginId = "test.merge.plugin";
        _tempDir.CreatePluginManifest(pluginId, m =>
        {
            m.Version = "1.0.0";
            m.EntryAssembly = "fake.dll";
            m.EntryType = "fake.Plugin";
        });
        _manager.DiscoverPlugins();

        // 已安装快照：versions/1.0.0（当前版本，扁平迁移/更新产生的）
        var pluginDir = Path.Combine(_tempDir.RootPath, pluginId);
        var v1 = Path.Combine(pluginDir, "versions", "1.0.0");
        Directory.CreateDirectory(v1);
        File.WriteAllBytes(Path.Combine(v1, "fake.dll"), new byte[] { 1 });
        PluginVersionLayout.WriteCurrentVersion(pluginDir, "1.0.0");

        // 已直落 staged：versions/2.0.0（待更新的新版本，未切 current）
        StageVersion(_tempDir, pluginId, "2.0.0");

        var versions = _service.GetPluginVersions(pluginId);

        versions.Select(v => v.Version).Should().BeEquivalentTo("2.0.0", "1.0.0");
    }

    [Fact]
    public void GetPluginVersions_InstalledSnapshotOnly_WhenNoStaged()
    {
        var pluginId = "test.installed.only";
        _tempDir.CreatePluginManifest(pluginId, m =>
        {
            m.Version = "1.0.0";
            m.EntryAssembly = "fake.dll";
            m.EntryType = "fake.Plugin";
        });
        _manager.DiscoverPlugins();

        var pluginDir = Path.Combine(_tempDir.RootPath, pluginId);
        var v1 = Path.Combine(pluginDir, "versions", "1.0.0");
        Directory.CreateDirectory(v1);
        File.WriteAllBytes(Path.Combine(v1, "fake.dll"), new byte[] { 1 });
        PluginVersionLayout.WriteCurrentVersion(pluginDir, "1.0.0");

        var versions = _service.GetPluginVersions(pluginId);

        versions.Select(v => v.Version).Should().Contain("1.0.0");
    }
}
