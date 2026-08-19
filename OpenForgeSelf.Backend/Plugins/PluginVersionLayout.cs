using OpenForgeSelf.Abstractions;

namespace OpenForgeSelf.Backend.Plugins;

/// <summary>
/// side-by-side 版本目录布局的纯静态辅助：版本目录路径计算、<c>current</c> 指针读写、
/// 入口程序集磁盘路径解析。所有方法无外部依赖，便于单元测试。
/// </summary>
/// <remarks>
/// 布局约定：
/// <code>
/// Plugins/&lt;id&gt;/
///   plugin.json                 —— 活动清单（小文本，可安全原子覆盖）
///   current                     —— 文本文件，存当前生效 semver（如 "1.0.0"）
///   versions/&lt;semver&gt;/&lt;entry.dll&gt; —— 每个已安装版本的不可变快照
/// </code>
/// </remarks>
public static class PluginVersionLayout
{
    /// <summary>版本目录名。</summary>
    public const string VersionsFolderName = "versions";

    /// <summary>current 指针文件名。</summary>
    public const string CurrentPointerFileName = "current";

    /// <summary>版本目录根（Plugins/&lt;id&gt;/versions）。</summary>
    public static string VersionsDirectory(string pluginDir) =>
        Path.Combine(pluginDir, VersionsFolderName);

    /// <summary>某个版本目录（Plugins/&lt;id&gt;/versions/&lt;semver&gt;）。</summary>
    public static string VersionDirectory(string pluginDir, string version) =>
        Path.Combine(VersionsDirectory(pluginDir), version);

    /// <summary>current 指针文件完整路径。</summary>
    public static string CurrentPointerPath(string pluginDir) =>
        Path.Combine(pluginDir, CurrentPointerFileName);

    /// <summary>读取当前生效版本；指针不存在或为空时返回 null。</summary>
    public static string? ReadCurrentVersion(string pluginDir)
    {
        var pointer = CurrentPointerPath(pluginDir);
        if (!File.Exists(pointer))
            return null;

        var value = File.ReadAllText(pointer).Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// 写入当前生效版本。先写临时文件再 <see cref="File.Move(string,string,bool)"/> 覆盖，
    /// 保证读取方不会读到半截内容（原子切换指针）。
    /// </summary>
    public static void WriteCurrentVersion(string pluginDir, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        Directory.CreateDirectory(pluginDir);

        var pointer = CurrentPointerPath(pluginDir);
        var temp = pointer + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temp, version);
        File.Move(temp, pointer, true);
    }

    /// <summary>
    /// 解析插件入口程序集的真实磁盘路径；内嵌插件（无独立 DLL）返回 null。
    /// 解析顺序：side-by-side（versions/&lt;current&gt;/&lt;entryAssembly&gt;）→ 旧版扁平布局
    /// （&lt;pluginDir&gt;/&lt;entryAssembly&gt;）→ 无独立程序集（null，主程序集回退）。
    /// </summary>
    public static string? ResolveEntryAssemblyPath(PluginMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata.PluginDirectory))
            return null;
        if (string.IsNullOrWhiteSpace(metadata.EntryAssembly))
            return null;

        var current = ReadCurrentVersion(metadata.PluginDirectory);
        if (!string.IsNullOrWhiteSpace(current))
        {
            var candidate = Path.Combine(VersionDirectory(metadata.PluginDirectory, current), metadata.EntryAssembly);
            if (File.Exists(candidate))
                return candidate;
        }

        var flat = Path.Combine(metadata.PluginDirectory, metadata.EntryAssembly);
        return File.Exists(flat) ? flat : null;
    }
}
