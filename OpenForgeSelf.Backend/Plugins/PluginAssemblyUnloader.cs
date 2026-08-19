namespace OpenForgeSelf.Backend.Plugins;

/// <summary>
/// ALC 卸载后的强制回收与文件锁探测辅助，支撑「破除文件锁」与「延迟删除」：
/// 可回收 <see cref="System.Runtime.Loader.AssemblyLoadContext"/> 卸载后，其程序集与底层
/// 文件句柄须经 GC 与终结器两轮才真正释放；删除旧版本目录前用 <c>FileShare.None</c>
/// 独占打开试探，被占用则跳过（绝不阻塞）。
/// </summary>
public static class PluginAssemblyUnloader
{
    /// <summary>
    /// 强制回收：GC.Collect(2, Forced) + WaitForPendingFinalizers + 再次 Collect，
    /// 促使可回收 ALC 的句柄与终结器在删除前释放。
    /// </summary>
    public static void ForceCollect()
    {
        GC.Collect(2, GCCollectionMode.Forced);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced);
    }

    /// <summary>
    /// 探测单个文件是否可独占打开（FileShare.None）。true = 无锁，可删除/覆盖。
    /// </summary>
    public static bool TryOpenExclusive(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// 尝试删除目录：先逐个文件 <see cref="TryOpenExclusive"/> 探测，全部可独占才删除；
    /// 任一文件被占用则整体跳过并返回 false（不抛异常），留待下一轮重试。
    /// </summary>
    public static bool TryDeleteDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return true;

        try
        {
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                if (!TryOpenExclusive(file))
                    return false;
            }

            Directory.Delete(path, true);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
