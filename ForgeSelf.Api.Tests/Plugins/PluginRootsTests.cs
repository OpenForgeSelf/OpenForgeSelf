using System.Text.Json;
using FluentAssertions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins;
using ForgeSelf.Api.Plugins.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins;

/// <summary>
/// 插件根两路扫描（2026-10-04 输入18）：内置根（随版本发布，业务层旁边 plugins/）+
/// 数据目录根（{数据根}/plugins，用户自行安装的插件包）。同 Id 跨根由版本号裁决。
/// </summary>
public class PluginRootsTests : IDisposable
{
    private readonly TempPluginDirectory _builtin = new();
    private readonly TempPluginDirectory _userData = new();

    private static PluginManager CreateManager()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var permission = new Mock<IPermissionChecker>();
        return new PluginManager(services, permission.Object);
    }

    private static string WriteManifest(string root, string dirName, string pluginId, string version)
    {
        var dir = Path.Combine(root, dirName);
        Directory.CreateDirectory(dir);
        var meta = PluginManifestGenerator.CreateBasic(pluginId);
        meta.Version = version;
        var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        File.WriteAllText(Path.Combine(dir, "plugin.json"), json);
        return dir;
    }

    [Fact]
    public void 两路根各有一个插件_两路都应发现()
    {
        WriteManifest(_builtin.RootPath, "AlphaPlugin", "plugin.alpha", "1.0.0");
        WriteManifest(_userData.RootPath, "beta", "plugin.beta", "2.0.0");

        var manager = CreateManager();
        manager.SetPluginsDirectory(_builtin.RootPath);
        manager.AddPluginRoot(_userData.RootPath, "数据目录");

        var found = manager.DiscoverPlugins();

        found.Select(f => f.Id).Should().BeEquivalentTo("plugin.alpha", "plugin.beta");
        manager.PluginRoots.Should().ContainInOrder(_builtin.RootPath, _userData.RootPath);
        manager.PluginsDirectory.Should().Be(_builtin.RootPath, "兼容属性仍指内置根（安装/版本服务的既有语义不变）");
    }

    [Fact]
    public void 同Id数据目录版本更高_生效取数据目录那份()
    {
        var builtinDir = WriteManifest(_builtin.RootPath, "DesignSystem", "design-system", "3.0.0");
        var userDataDir = WriteManifest(_userData.RootPath, "design-system-installed", "design-system", "3.1.0");

        var manager = CreateManager();
        manager.SetPluginsDirectory(_builtin.RootPath);
        manager.AddPluginRoot(_userData.RootPath, "数据目录");

        var found = manager.DiscoverPlugins();

        found.Should().HaveCount(1, "同 Id 只应生效一份");
        found[0].Version.Should().Be("3.1.0");
        found[0].PluginDirectory.Should().Be(userDataDir);
        found[0].PluginDirectory.Should().NotBe(builtinDir);
    }

    [Fact]
    public void 同Id版本相同_保留内置根不覆盖()
    {
        var builtinDir = WriteManifest(_builtin.RootPath, "SamePlugin", "plugin.same", "1.2.0");
        WriteManifest(_userData.RootPath, "same-installed", "plugin.same", "1.2.0");

        var manager = CreateManager();
        manager.SetPluginsDirectory(_builtin.RootPath);
        manager.AddPluginRoot(_userData.RootPath, "数据目录");

        var found = manager.DiscoverPlugins();

        found.Should().HaveCount(1);
        found[0].PluginDirectory.Should().Be(builtinDir, "版本相等时保留先扫到的内置根，避免被副本夺走");
    }

    [Fact]
    public void 数据目录里的插件数据子目录_不得被当插件()
    {
        WriteManifest(_builtin.RootPath, "RealPlugin", "plugin.real", "1.0.0");
        // 模拟 {数据根}/plugins/{插件Id}/ 的插件数据形态：只有 db，没有 plugin.json
        var dataOnly = Path.Combine(_userData.RootPath, "design-system");
        Directory.CreateDirectory(dataOnly);
        File.WriteAllText(Path.Combine(dataOnly, "ForgeSelf.db"), "not-a-plugin");

        var manager = CreateManager();
        manager.SetPluginsDirectory(_builtin.RootPath);
        manager.AddPluginRoot(_userData.RootPath, "数据目录");

        var found = manager.DiscoverPlugins();

        found.Should().HaveCount(1);
        found[0].Id.Should().Be("plugin.real");
        Directory.Exists(dataOnly).Should().BeTrue("阳性对照：数据子目录确实在扫描路径下，只是没有 plugin.json");
    }

    [Fact]
    public void 内置根缺失但数据目录有插件_仍应发现且不抛()
    {
        // 现场形态：版本目录里没有 plugins/（历史包被外置掏空），数据目录有用户装的包
        var missingBuiltin = Path.Combine(_builtin.RootPath, "not-created-yet");
        WriteManifest(_userData.RootPath, "FromDataRoot", "plugin.fromdata", "1.0.0");

        var manager = CreateManager();
        manager.SetPluginsDirectory(missingBuiltin);
        manager.AddPluginRoot(_userData.RootPath, "数据目录");

        var found = manager.DiscoverPlugins();

        found.Should().HaveCount(1);
        found[0].Id.Should().Be("plugin.fromdata");
    }

    [Fact]
    public void 同一根重复追加_只保留一路()
    {
        var manager = CreateManager();
        manager.SetPluginsDirectory(_builtin.RootPath);
        manager.AddPluginRoot(_builtin.RootPath, "数据目录");
        manager.AddPluginRoot(null, "数据目录");

        manager.PluginRoots.Should().HaveCount(1);
    }

    [Theory]
    [InlineData("3.1.0", "3.0.0", 1)]
    [InlineData("1.0", "1.0.1", -1)]
    [InlineData("2.7.2.0", "2.7.2.0", 0)]
    [InlineData("v3.1.0", "3.0.0", 0)]      // 不可解析 ⇒ 视为相等（保留内置根）
    [InlineData("1.0.0-beta", "1.0.0", 0)]   // 同上：畸形版本号不得夺走生效份
    public void 版本比较_畸形值一律视为相等(string candidate, string kept, int expected)
    {
        PluginManager.ComparePluginVersions(candidate, kept).Should().Be(expected);
    }

    public void Dispose()
    {
        // TempPluginDirectory 本身不管清理（既有测试共用同一份夹具，不改它的语义），此处自行清干净。
        foreach (var root in new[] { _builtin.RootPath, _userData.RootPath })
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); }
            catch { /* 临时目录清理失败不影响断言 */ }
        }
    }
}
