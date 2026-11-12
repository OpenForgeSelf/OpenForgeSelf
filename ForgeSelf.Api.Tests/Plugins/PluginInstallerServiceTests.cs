using System.IO.Compression;
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
/// 再次安装只暂存到 versions/，不切换 current。包文件是 .zip。
/// </summary>
public class PluginInstallerServiceTests
{
    private readonly TempPluginDirectory _tempDir;
    private readonly PluginManager _manager;
    private readonly PluginPackagerService _packagerService;
    private readonly PluginInstallerService _installer;

    public PluginInstallerServiceTests()
    {
        _tempDir = new TempPluginDirectory();
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();
        _manager = new PluginManager(sp, Mock.Of<IPermissionChecker>());
        _manager.SetPluginsDirectory(_tempDir.RootPath);
        _packagerService = new PluginPackagerService(_manager);
        var versionService = new PluginVersionService(_manager,
            new PluginUpdateSettingsService(new PluginUpdateSettings(), Path.Combine(Path.GetTempPath(), "pis-plugin-update-settings-test.json")),
            _packagerService);
        versionService.Initialize(_tempDir.RootPath);
        _installer = new PluginInstallerService(_manager, _packagerService, versionService);
        _installer.Initialize(_tempDir.RootPath);
    }

    /// <summary>构造 .zip 包（zip 根 = plugin.json + 入口 DLL 占位）。</summary>
    private string CreatePackage(string pluginId, string version)
    {
        var packagePath = Path.Combine(_tempDir.RootPath, $"{pluginId}-{version}.zip");
        using var fs = File.Create(packagePath);
        using (var archive = new ZipArchive(fs, ZipArchiveMode.Create, true))
        {
            var manifest = archive.CreateEntry("plugin.json");
            using (var sw = new StreamWriter(manifest.Open()))
            {
                sw.Write(JsonSerializer.Serialize(new PluginMetadata
                {
                    Id = pluginId,
                    Name = "Test",
                    Version = version,
                    EntryAssembly = "fake.dll",
                    EntryType = "fake.Plugin"
                }));
            }
            var dll = archive.CreateEntry("fake.dll");
            using var dllStream = dll.Open();
            dllStream.WriteByte(0x42);
        }
        return packagePath;
    }

    [Fact]
    public void UpdateFromPackage_StagesWithoutActivating()
    {
        var pluginId = "test.installer.upgrade";
        _tempDir.CreatePluginManifest(pluginId, m =>
        {
            m.Version = "1.0.0";
            m.EntryAssembly = "fake.dll";
            m.EntryType = "fake.Plugin";
        });
        _manager.DiscoverPlugins();

        var packagePath = CreatePackage(pluginId, "2.0.0");
        var result = _installer.UpdateFromPackage(packagePath);

        result.Should().NotBeNull();
        result!.Version.Should().Be("2.0.0");

        var pluginDir = Path.Combine(_tempDir.RootPath, pluginId);
        Directory.Exists(Path.Combine(pluginDir, "versions", "2.0.0")).Should().BeTrue();
        File.Exists(Path.Combine(pluginDir, "versions", "2.0.0", "fake.dll")).Should().BeTrue();

        PluginVersionLayout.ReadCurrentVersion(pluginDir).Should().BeNull();
        var activeManifest = JsonSerializer.Deserialize<PluginMetadata>(
            File.ReadAllText(Path.Combine(pluginDir, "plugin.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        activeManifest!.Version.Should().Be("1.0.0");
        _manager.GetPluginMetadata(pluginId)!.Version.Should().Be("1.0.0");
    }

    [Fact]
    public void UpdateFromPackage_RejectsSameVersion()
    {
        var pluginId = "test.installer.samever";
        _tempDir.CreatePluginManifest(pluginId, m =>
        {
            m.Version = "1.0.0";
            m.EntryAssembly = "fake.dll";
            m.EntryType = "fake.Plugin";
        });
        _manager.DiscoverPlugins();

        var packagePath = CreatePackage(pluginId, "1.0.0");
        var act = () => _installer.UpdateFromPackage(packagePath);

        // 版本化布局只允许单调升级：包版本未高于当前 → 拒绝覆盖式更新
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*拒绝覆盖式更新*");
        var pluginDir = Path.Combine(_tempDir.RootPath, pluginId);
        PluginVersionLayout.ReadCurrentVersion(pluginDir).Should().BeNull();
    }

    [Fact]
    public void UpdateFromPackage_RejectsLowerVersion()
    {
        var pluginId = "test.installer.lowerver";
        _tempDir.CreatePluginManifest(pluginId, m =>
        {
            m.Version = "2.0.0";
            m.EntryAssembly = "fake.dll";
            m.EntryType = "fake.Plugin";
        });
        _manager.DiscoverPlugins();

        var packagePath = CreatePackage(pluginId, "1.5.0");
        var act = () => _installer.UpdateFromPackage(packagePath);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*未高于当前生效版本*");
    }

    [Fact]
    public void UpdateFromPackage_RejectsInvalidVersionString()
    {
        var pluginId = "test.installer.badver";
        _tempDir.CreatePluginManifest(pluginId, m =>
        {
            m.Version = "1.0.0";
            m.EntryAssembly = "fake.dll";
            m.EntryType = "fake.Plugin";
        });
        _manager.DiscoverPlugins();

        var packagePath = CreatePackage(pluginId, "v2.0.0"); // 非法版本号（防路径穿越）
        var act = () => _installer.UpdateFromPackage(packagePath);

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*包版本号非法*");
    }
}
