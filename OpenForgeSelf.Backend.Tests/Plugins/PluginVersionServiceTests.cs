using System.Text.Json;
using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Backend.Plugins;
using OpenForgeSelf.Backend.Plugins.Abstractions;
using OpenForgeSelf.Backend.Plugins.Services;
using Microsoft.Extensions.DependencyInjection;

namespace OpenForgeSelf.Backend.Tests.Plugins;

public class PluginVersionServiceTests : IDisposable
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
        _service = new PluginVersionService(_manager);
        _service.Initialize(_tempDir.RootPath);
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

        // 下载产物：_backups/<id>/2.0.0/
        var backupVersionDir = Path.Combine(_tempDir.RootPath, "_backups", pluginId, "2.0.0");
        Directory.CreateDirectory(backupVersionDir);
        File.WriteAllText(
            Path.Combine(backupVersionDir, "plugin.json"),
            JsonSerializer.Serialize(new PluginMetadata
            {
                Id = pluginId,
                Name = "Test",
                Version = "2.0.0",
                EntryAssembly = "fake.dll",
                EntryType = "fake.Plugin"
            }));
        File.WriteAllBytes(Path.Combine(backupVersionDir, "fake.dll"), new byte[] { 1, 2, 3 });

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

        var backupVersionDir = Path.Combine(_tempDir.RootPath, "_backups", pluginId, "2.0.0");
        Directory.CreateDirectory(backupVersionDir);
        File.WriteAllText(Path.Combine(backupVersionDir, "plugin.json"), "{}");

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

        // 依次产生 2.0.0 / 3.0.0 / 4.0.0 三个下载产物并逐个更新
        foreach (var version in new[] { "2.0.0", "3.0.0", "4.0.0" })
        {
            var backupVersionDir = Path.Combine(_tempDir.RootPath, "_backups", pluginId, version);
            Directory.CreateDirectory(backupVersionDir);
            File.WriteAllText(Path.Combine(backupVersionDir, "plugin.json"),
                JsonSerializer.Serialize(new PluginMetadata
                {
                    Id = pluginId,
                    Name = "T",
                    Version = version,
                    EntryAssembly = "fake.dll",
                    EntryType = "fake.Plugin"
                }));
            File.WriteAllBytes(Path.Combine(backupVersionDir, "fake.dll"), new byte[] { 1 });

            _service.UpdatePlugin(pluginId).Should().BeTrue();
        }

        var versionsDir = Path.Combine(_tempDir.RootPath, pluginId, "versions");
        Directory.GetDirectories(versionsDir).Select(Path.GetFileName)
            .Should().BeEquivalentTo("3.0.0", "4.0.0");
        PluginVersionLayout.ReadCurrentVersion(Path.Combine(_tempDir.RootPath, pluginId))
            .Should().Be("4.0.0");
    }

    public void Dispose() => _tempDir.Dispose();
}
