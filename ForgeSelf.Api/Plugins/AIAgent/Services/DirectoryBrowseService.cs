namespace ForgeSelf.Api.Plugins.AIAgent.Services;

/// <summary>磁盘目录浏览项（仅目录；供前端「选择工作目录」弹窗展示与逐级进入）。</summary>
public class BrowseDirectoryEntry
{
    /// <summary>目录名（磁盘根时为卷名，如 "C:\" 或 "/"）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>目录绝对路径（前端再次请求时透传）。</summary>
    public string Path { get; set; } = string.Empty;
}

/// <summary>
/// 磁盘目录浏览：为「选择工作目录」弹窗提供宿主磁盘的逐级浏览能力。
/// 与 <see cref="ProjectWorkspaceService"/> 无关——浏览发生在「选定项目根之前」，
/// 因此不依赖/不读写 <see cref="IProjectWorkspaceService"/> 的任何状态。
/// </summary>
public interface IDirectoryBrowseService
{
    /// <summary>
    /// 列出 <paramref name="path"/> 下的子目录；path 为空时列出磁盘根（此电脑）。
    /// path 非法/不存在时抛异常（由控制器统一转 BadRequest）。
    /// </summary>
    /// <returns>当前目录路径（path 为空时为空串）、上级目录（已在磁盘根时为 null）、子目录列表。</returns>
    (string CurrentPath, string? ParentPath, List<BrowseDirectoryEntry> Items) List(string? path);
}

public class DirectoryBrowseService : IDirectoryBrowseService
{
    public (string CurrentPath, string? ParentPath, List<BrowseDirectoryEntry> Items) List(string? path)
    {
        // 空路径 = 磁盘根（此电脑）：列出可用卷；parent = null 表示没有上一级。
        if (string.IsNullOrWhiteSpace(path))
        {
            var drives = new List<BrowseDirectoryEntry>();
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady) continue; // 光驱/未挂载卷直接跳过
                    drives.Add(new BrowseDirectoryEntry { Name = drive.Name, Path = drive.Name });
                }
                catch
                {
                    // 个别卷（可移动设备抽离瞬间）读属性抛异常时跳过，不影响整体浏览
                }
            }

            return (string.Empty, null, drives);
        }

        string full;
        try
        {
            full = Path.GetFullPath(path.Trim());
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"路径非法：{ex.Message}");
        }

        if (!Directory.Exists(full))
        {
            throw new InvalidOperationException($"目录不存在：{full}");
        }

        var items = new List<BrowseDirectoryEntry>();
        try
        {
            // 只列子目录（选择工作目录不关心文件）；无权限目录进入时会报错，但列表本身不该打不开。
            foreach (var dir in Directory.GetDirectories(full))
            {
                var name = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(name)) continue;
                items.Add(new BrowseDirectoryEntry { Name = name, Path = dir });
            }
        }
        catch (UnauthorizedAccessException)
        {
            // 整层不可读（如系统保护目录）：返回空列表，不炸整个弹窗。
        }

        items.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        var parent = Directory.GetParent(full)?.FullName;

        return (full, parent, items);
    }
}
