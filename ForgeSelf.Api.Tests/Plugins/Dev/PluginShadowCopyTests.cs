using ForgeSelf.Api.Plugins.Dev;
using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Tests.Plugins.Dev;

/// <summary>
/// shadow-copy 装载核心测试：复制完整性、宿主共享 DLL 排除、内容寻址目录、bin 布局解析、陈旧清理。
/// 全部用临时目录真实文件驱动（不 mock 文件系统）。
/// </summary>
public class PluginShadowCopyTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _pluginDir;
    private readonly string _binDir;

    public PluginShadowCopyTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "forge-shadow-tests-" + Guid.NewGuid().ToString("N"));
        _pluginDir = Path.Combine(_tempRoot, "demo");
        _binDir = Path.Combine(_pluginDir, "bin", "Debug", "net10.0");
        Directory.CreateDirectory(_binDir);
    }

    public void Dispose()
    {
        // 清理测试产生的 shadow 根与临时根
        var shadowPluginDir = Path.Combine(PluginShadowCopy.ShadowRoot, "demo-shadow-test");
        if (Directory.Exists(shadowPluginDir)) Directory.Delete(shadowPluginDir, recursive: true);
        if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true);
    }

    private PluginMetadata CreateMetadata(string id = "demo-shadow-test")
    {
        return new PluginMetadata
        {
            Id = id,
            Name = "Demo",
            Version = "1.0.0",
            EntryAssembly = "Demo.dll",
            PluginDirectory = _pluginDir
        };
    }

    private void WriteBinFiles()
    {
        File.WriteAllText(Path.Combine(_binDir, "Demo.dll"), "demo-entry-bytes");
        File.WriteAllText(Path.Combine(_binDir, "Demo.deps.json"), "{\"dependencies\":{}}");
        File.WriteAllText(Path.Combine(_binDir, "Demo.pdb"), "pdb-bytes");
        // 宿主共享程序集 —— 必须被排除
        File.WriteAllText(Path.Combine(_binDir, "ForgeSelf.Abstractions.dll"), "host-shared");
        File.WriteAllText(Path.Combine(_binDir, "NewLife.Core.dll"), "host-shared");
        File.WriteAllText(Path.Combine(_binDir, "XCode.dll"), "host-shared");
        // 插件私有依赖 —— 必须被复制
        File.WriteAllText(Path.Combine(_binDir, "Demo.Private.dll"), "private-dep");
    }

    [Fact]
    public void Prepare_SourceBinLayout_CopiesEntryAndDepsExcludingHostShared()
    {
        WriteBinFiles();
        var metadata = CreateMetadata();

        // 源码树布局：常规解析为 null（无 versions/ 与扁平 DLL），Prepare 应回退 bin/<config>/<tfm>
        var shadowPath = PluginShadowCopy.Prepare(metadata, null);

        shadowPath.Should().NotBeNull();
        File.Exists(shadowPath).Should().BeTrue();
        File.ReadAllText(shadowPath!).Should().Be("demo-entry-bytes");

        var shadowDir = Path.GetDirectoryName(shadowPath)!;
        File.Exists(Path.Combine(shadowDir, "Demo.deps.json")).Should().BeTrue();
        File.Exists(Path.Combine(shadowDir, "Demo.Private.dll")).Should().BeTrue();

        // 宿主共享程序集绝不能进 shadow 目录（R8②：类型分裂 → 宿主启动即崩）
        File.Exists(Path.Combine(shadowDir, "ForgeSelf.Abstractions.dll")).Should().BeFalse();
        File.Exists(Path.Combine(shadowDir, "NewLife.Core.dll")).Should().BeFalse();
        File.Exists(Path.Combine(shadowDir, "XCode.dll")).Should().BeFalse();
    }

    [Fact]
    public void Prepare_SameContent_StableHashDir()
    {
        WriteBinFiles();
        var metadata = CreateMetadata();

        var first = PluginShadowCopy.Prepare(metadata, null);
        var second = PluginShadowCopy.Prepare(metadata, null);

        first.Should().Be(second);
    }

    [Fact]
    public void Prepare_ContentChanged_NewHashDir()
    {
        WriteBinFiles();
        var metadata = CreateMetadata();

        var first = PluginShadowCopy.Prepare(metadata, null);
        File.WriteAllText(Path.Combine(_binDir, "Demo.dll"), "CHANGED-bytes");
        var second = PluginShadowCopy.Prepare(metadata, null);

        second.Should().NotBe(first);
        File.ReadAllText(second!).Should().Be("CHANGED-bytes");
    }

    [Fact]
    public void Prepare_PrunesStaleHashDirs_WhenNotLocked()
    {
        WriteBinFiles();
        var metadata = CreateMetadata();

        var first = PluginShadowCopy.Prepare(metadata, null);
        File.WriteAllText(Path.Combine(_binDir, "Demo.dll"), "CHANGED-bytes");
        var second = PluginShadowCopy.Prepare(metadata, null);

        // 旧目录无进程锁定 → 应被清理；同插件仅剩当前 hash 目录（AC-6）
        var pluginShadowRoot = Path.Combine(PluginShadowCopy.ShadowRoot, metadata.Id);
        Directory.GetDirectories(pluginShadowRoot).Should().ContainSingle(d => d == Path.GetDirectoryName(second));
    }

    [Fact]
    public void Prepare_NoSourceOutput_ReturnsNull()
    {
        // 不写任何 bin 文件 → 无法解析源产物
        var metadata = CreateMetadata();

        var shadowPath = PluginShadowCopy.Prepare(metadata, null);

        shadowPath.Should().BeNull();
    }

    [Fact]
    public void Prepare_ResolvedPathUsed_WhenProvided()
    {
        WriteBinFiles();
        var metadata = CreateMetadata();

        var shadowPath = PluginShadowCopy.Prepare(metadata, Path.Combine(_binDir, "Demo.dll"));

        shadowPath.Should().NotBeNull();
        File.ReadAllText(shadowPath!).Should().Be("demo-entry-bytes");
    }
}
