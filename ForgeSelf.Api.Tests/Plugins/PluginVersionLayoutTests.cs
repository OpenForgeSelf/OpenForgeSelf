using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins;

namespace ForgeSelf.Api.Tests.Plugins;

public class PluginVersionLayoutTests
{
    private readonly string _root;

    public PluginVersionLayoutTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"pvl_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void WriteAndReadCurrentVersion_Roundtrips()
    {
        var pluginDir = Path.Combine(_root, "p1");
        Directory.CreateDirectory(pluginDir);

        PluginVersionLayout.ReadCurrentVersion(pluginDir).Should().BeNull();

        PluginVersionLayout.WriteCurrentVersion(pluginDir, "2.0.0");
        PluginVersionLayout.ReadCurrentVersion(pluginDir).Should().Be("2.0.0");

        PluginVersionLayout.WriteCurrentVersion(pluginDir, "3.0.0");
        PluginVersionLayout.ReadCurrentVersion(pluginDir).Should().Be("3.0.0");
    }

    [Fact]
    public void ResolveEntryAssemblyPath_SideBySide_ReturnsVersionedDll()
    {
        var pluginDir = Path.Combine(_root, "p1");
        var versionDir = Path.Combine(pluginDir, "versions", "1.2.0");
        Directory.CreateDirectory(versionDir);
        File.WriteAllBytes(Path.Combine(versionDir, "p1.dll"), new byte[] { 1, 2, 3 });
        PluginVersionLayout.WriteCurrentVersion(pluginDir, "1.2.0");

        var metadata = new PluginMetadata { PluginDirectory = pluginDir, EntryAssembly = "p1.dll" };

        PluginVersionLayout.ResolveEntryAssemblyPath(metadata)
            .Should().Be(Path.Combine(versionDir, "p1.dll"));
    }

    [Fact]
    public void ResolveEntryAssemblyPath_FlatLayout_ReturnsFlatDll()
    {
        var pluginDir = Path.Combine(_root, "p1");
        Directory.CreateDirectory(pluginDir);
        var flat = Path.Combine(pluginDir, "p1.dll");
        File.WriteAllBytes(flat, new byte[] { 1 });

        var metadata = new PluginMetadata { PluginDirectory = pluginDir, EntryAssembly = "p1.dll" };

        PluginVersionLayout.ResolveEntryAssemblyPath(metadata).Should().Be(flat);
    }

    [Fact]
    public void ResolveEntryAssemblyPath_NoDll_ReturnsNull()
    {
        var pluginDir = Path.Combine(_root, "p1");
        Directory.CreateDirectory(pluginDir);

        var metadata = new PluginMetadata { PluginDirectory = pluginDir, EntryAssembly = "p1.dll" };

        PluginVersionLayout.ResolveEntryAssemblyPath(metadata).Should().BeNull();
    }

    [Fact]
    public void ResolveEntryAssemblyPath_CurrentPointsToMissingVersion_FallsBackToFlat()
    {
        var pluginDir = Path.Combine(_root, "p1");
        Directory.CreateDirectory(pluginDir);
        var flat = Path.Combine(pluginDir, "p1.dll");
        File.WriteAllBytes(flat, new byte[] { 1 });
        PluginVersionLayout.WriteCurrentVersion(pluginDir, "9.9.9"); // 版本目录不存在

        var metadata = new PluginMetadata { PluginDirectory = pluginDir, EntryAssembly = "p1.dll" };

        PluginVersionLayout.ResolveEntryAssemblyPath(metadata).Should().Be(flat);
    }

    [Fact]
    public void TryOpenExclusive_FreeFile_ReturnsTrue()
    {
        var file = Path.Combine(_root, "a.dll");
        File.WriteAllBytes(file, new byte[] { 1 });

        PluginAssemblyUnloader.TryOpenExclusive(file).Should().BeTrue();
    }

    [Fact]
    public void TryOpenExclusive_LockedFile_ReturnsFalse()
    {
        var file = Path.Combine(_root, "a.dll");
        File.WriteAllBytes(file, new byte[] { 1 });

        using (var locker = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            PluginAssemblyUnloader.TryOpenExclusive(file).Should().BeFalse();
        }
    }

    [Fact]
    public void TryDeleteDirectory_LockedFile_SkipsWithoutThrowing()
    {
        var dir = Path.Combine(_root, "v");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "a.dll");
        File.WriteAllBytes(file, new byte[] { 1 });

        using (var locker = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            PluginAssemblyUnloader.TryDeleteDirectory(dir).Should().BeFalse();
            Directory.Exists(dir).Should().BeTrue();
        }

        PluginAssemblyUnloader.TryDeleteDirectory(dir).Should().BeTrue();
        Directory.Exists(dir).Should().BeFalse();
    }

    [Fact]
    public void TryDeleteDirectory_MissingDirectory_ReturnsTrue()
    {
        PluginAssemblyUnloader.TryDeleteDirectory(Path.Combine(_root, "missing")).Should().BeTrue();
    }
}
