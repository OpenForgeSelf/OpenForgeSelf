using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Plugins.Dev;

/// <summary>
/// dev-only shadow-copy 装载：把插件产物复制到 <c>%TEMP%/forge-dev-shadow/&lt;pluginId&gt;/&lt;contentHash&gt;/</c>，
/// ALC 从副本加载 —— 源 DLL 不被进程 mmap 锁定，因此可反复「重新编译 → 同版本热重载」，
/// 彻底绕开 <c>PluginVersionService.UpdatePlugin</c> 的版本严格递增校验与 DLL 独占锁。
/// </summary>
/// <remarks>
/// 关键约束：
/// 1. <b>排除宿主共享程序集</b>（ForgeSelf.* / NewLife.* / XCode / MX 的 dll+pdb，名单与
///    <c>scripts/publish-plugin.ps1:145-149</c> 一致）—— 它们由宿主默认 ALC 加载，混入副本会造成类型身份分裂
///    （packaging-upgrade-backup.md R8②：宿主启动即崩）。
/// 2. <c>AssemblyDependencyResolver</c> 依赖入口旁的 <c>.deps.json</c> 解析私有依赖 → 必须<b>整目录复制</b>
///    （含 deps.json 与全部私有依赖），否则插件 NuGet 依赖解析失败。
/// 3. 目录名 = 内容哈希：产物变化自然产生新目录；旧目录在旧 ALC 卸载后于下次 Prepare 时尽力删除
///    （仍被锁的跳过，不阻塞），dev 宿主启动时全量清扫，保证无泄漏（AC-6）。
/// 4. 仅 dev 总闸下使用；Production 调用方不触碰本类。
/// </remarks>
public static class PluginShadowCopy
{
    /// <summary>shadow 根目录名（%TEMP% 下）。</summary>
    public const string RootFolderName = "forge-dev-shadow";

    /// <summary>宿主共享程序集文件名匹配（与 publish-plugin.ps1 排除名单一致；dll 与 pdb 同名单）。</summary>
    private static readonly Func<string, bool> IsHostSharedAssembly = fileName =>
        Regex.IsMatch(fileName, @"^ForgeSelf\..*\.(dll|pdb)$", RegexOptions.IgnoreCase) ||
        Regex.IsMatch(fileName, @"^NewLife\..*\.(dll|pdb)$", RegexOptions.IgnoreCase) ||
        Regex.IsMatch(fileName, @"^(XCode|MX)\.(dll|pdb)$", RegexOptions.IgnoreCase);

    /// <summary>已为各插件准备的 shadow 入口路径（diagnostics 用）。</summary>
    private static readonly ConcurrentDictionary<string, string> Prepared = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>shadow 根目录绝对路径。</summary>
    public static string ShadowRoot => Path.Combine(Path.GetTempPath(), RootFolderName);

    /// <summary>
    /// 解析插件的 shadow 入口程序集路径；无法解析源产物时返回 null（调用方回落原路径/内嵌逻辑）。
    /// </summary>
    /// <param name="metadata">插件元数据（PluginDirectory 必须已填充）。</param>
    /// <param name="resolvedPath">宿主常规解析得到的入口路径（可为 null —— 源码树布局下常规解析会失败）。</param>
    /// <returns>shadow 入口 DLL 绝对路径；无可用源产物时 null。</returns>
    public static string? Prepare(PluginMetadata metadata, string? resolvedPath)
    {
        var pluginDir = metadata.PluginDirectory;
        if (string.IsNullOrWhiteSpace(pluginDir) || string.IsNullOrWhiteSpace(metadata.EntryAssembly))
            return null;

        // 1. 定位源入口：常规解析结果 → 源码树 bin/<config>/<tfm> 回退。
        var sourceDir = resolvedPath != null
            ? Path.GetDirectoryName(resolvedPath)
            : ResolveSourceBinDirectory(pluginDir);
        if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
            return null;

        var sourceEntry = Path.Combine(sourceDir, metadata.EntryAssembly);
        if (!File.Exists(sourceEntry))
            return null;

        try
        {
            // 2. 内容寻址目录名：对（相对路径 + 文件内容哈希）全量聚合，产物任何变化 → 新目录。
            var hash = ComputeDirectoryHash(sourceDir);
            var targetDir = Path.Combine(ShadowRoot, metadata.Id, hash);

            if (!Directory.Exists(targetDir))
            {
                CopyDirectory(sourceDir, targetDir);
            }

            var targetEntry = Path.Combine(targetDir, metadata.EntryAssembly);
            if (!File.Exists(targetEntry))
                return null;

            Prepared[metadata.Id] = targetEntry;

            // 3. 尽力清理同插件其它 hash 目录：旧 ALC 已卸载时其文件不再被锁，可删；
            //    仍被锁（并发实例/卸载失败）则跳过，不阻塞本次装载。
            PruneSiblings(metadata.Id, hash);
            return targetEntry;
        }
        catch (Exception ex)
        {
            // shadow 失败不致命：调用方回落原路径（行为等同改动前），仅记日志。
            NewLife.Log.XTrace.Log.Warn("dev shadow copy 失败（回落直接装载）[{0}]: {1}", metadata.Id, ex.Message);
            return null;
        }
    }

    /// <summary>获取指定插件最近一次准备的 shadow 入口路径；未准备过返回 null。</summary>
    public static string? GetShadowPath(string pluginId) =>
        Prepared.TryGetValue(pluginId, out var p) ? p : null;

    /// <summary>shadow 目录统计（diagnostics 用）：插件数 / 目录数 / 总字节数。</summary>
    public static (int PluginCount, int DirCount, long TotalBytes) GetStats()
    {
        var pluginCount = 0;
        var dirCount = 0;
        long total = 0;

        if (!Directory.Exists(ShadowRoot))
            return (0, 0, 0);

        foreach (var pluginDir in Directory.GetDirectories(ShadowRoot))
        {
            pluginCount++;
            foreach (var hashDir in Directory.GetDirectories(pluginDir))
            {
                dirCount++;
                total += SumSize(hashDir);
            }
        }

        return (pluginCount, dirCount, total);
    }

    /// <summary>dev 宿主启动清扫：删除所有「未被进程锁定」的 shadow 目录（活跃实例的目录被锁自动跳过）。</summary>
    public static void CleanupUnlocked()
    {
        if (!Directory.Exists(ShadowRoot))
            return;

        foreach (var pluginDir in Directory.GetDirectories(ShadowRoot))
        {
            foreach (var hashDir in Directory.GetDirectories(pluginDir))
            {
                TryDeleteDirectory(hashDir);
            }

            // 插件目录空了也一并删
            if (!Directory.EnumerateFileSystemEntries(pluginDir).Any())
            {
                TryDeleteDirectory(pluginDir);
            }
        }
    }

    /// <summary>
    /// 源码树布局解析：插件目录下 <c>bin/&lt;config&gt;/&lt;tfm&gt;</c>（DevMode.BuildConfiguration / TargetFramework，
    /// 可经 FORGESELF_DEV_CONFIG / FORGESELF_DEV_TFM 覆盖）。
    /// </summary>
    private static string? ResolveSourceBinDirectory(string pluginDir)
    {
        var bin = Path.Combine(pluginDir, "bin", DevMode.BuildConfiguration, DevMode.TargetFramework);
        return Directory.Exists(bin) ? bin : null;
    }

    private static long SumSize(string dir)
    {
        long sum = 0;
        try
        {
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                try { sum += new FileInfo(f).Length; } catch { /* 文件被并发删除时忽略 */ }
            }
        }
        catch { /* 目录被并发删除时忽略 */ }

        return sum;
    }

    private static string ComputeDirectoryHash(string sourceDir)
    {
        using var sha = SHA256.Create();
        var entries = Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(sourceDir, p))
            .Where(rel => !IsHostSharedAssembly(Path.GetFileName(rel)))
            .OrderBy(rel => rel, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var rel in entries)
        {
            var nameBytes = Encoding.UTF8.GetBytes(rel.ToLowerInvariant());
            sha.TransformBlock(nameBytes, 0, nameBytes.Length, null, 0);

            var content = SHA256.HashData(File.ReadAllBytes(Path.Combine(sourceDir, rel)));
            sha.TransformBlock(content, 0, content.Length, null, 0);
        }

        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant()[..12];
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileName(file);
            if (IsHostSharedAssembly(fileName))
                continue;

            var rel = Path.GetRelativePath(sourceDir, file);
            var target = Path.Combine(targetDir, rel);
            var targetParent = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(targetParent))
                Directory.CreateDirectory(targetParent);

            File.Copy(file, target, true);
        }
    }

    private static void PruneSiblings(string pluginId, string keepHash)
    {
        var pluginShadowDir = Path.Combine(ShadowRoot, pluginId);
        if (!Directory.Exists(pluginShadowDir))
            return;

        foreach (var dir in Directory.GetDirectories(pluginShadowDir))
        {
            if (string.Equals(Path.GetFileName(dir), keepHash, StringComparison.OrdinalIgnoreCase))
                continue;

            TryDeleteDirectory(dir);
        }
    }

    /// <summary>尽力删除：被锁（活跃 ALC 映射中）则跳过，绝不让清理阻塞装载。</summary>
    private static void TryDeleteDirectory(string dir)
    {
        try
        {
            Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // 被锁定/并发占用 → 保留，等下次清扫
        }
    }
}
