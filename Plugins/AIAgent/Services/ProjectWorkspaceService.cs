using System.Text;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

/// <summary>项目目录下的一项（文件或子目录）。</summary>
public class ProjectFileEntry
{
    public string Name { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 项目工作区：管理「Agent 选择的一个目录当做一个项目」。
/// 选定的根目录作为项目根，所有读/写/列目录都相对该根进行，并做路径穿越防护。
/// </summary>
public interface IProjectWorkspaceService
{
    /// <summary>是否已选定项目根目录。</summary>
    bool IsProjectSet { get; }

    /// <summary>当前项目根目录绝对路径；未选中时为 null。</summary>
    string? ProjectRoot { get; }

    /// <summary>设定项目根目录。返回 false 并给出错误（目录不存在/不是目录）。</summary>
    bool TrySetProjectRoot(string root, out string? error);

    /// <summary>安全解析相对路径为绝对路径；越界/不存在时抛 <see cref="InvalidOperationException"/>。</summary>
    string ResolveSafePath(string relativePath);

    /// <summary>列出相对路径下的目录项（顶层传空串）。</summary>
    List<ProjectFileEntry> ListEntries(string relativePath);

    /// <summary>读取文件内容（UTF-8）。</summary>
    string ReadFile(string relativePath);

    /// <summary>写入文件内容（UTF-8，自动创建/覆盖），父目录不存在时自动创建。</summary>
    void WriteFile(string relativePath, string content);

    /// <summary>删除文件。</summary>
    void DeleteFile(string relativePath);
}

public class ProjectWorkspaceService : IProjectWorkspaceService
{
    private const string RootLockKey = nameof(ProjectWorkspaceService);
    private string? _root;

    public bool IsProjectSet => ProjectRoot != null;

    public string? ProjectRoot
    {
        get
        {
            lock (RootLockKey)
            {
                return _root;
            }
        }
    }

    public bool TrySetProjectRoot(string root, out string? error)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            error = "目录不能为空";
            return false;
        }

        string fullPath;
        try
        {
            fullPath = System.IO.Path.GetFullPath(root.Trim());
        }
        catch (Exception ex)
        {
            error = $"路径非法：{ex.Message}";
            return false;
        }

        if (!Directory.Exists(fullPath))
        {
            error = $"目录不存在：{fullPath}";
            return false;
        }

        lock (RootLockKey)
        {
            _root = fullPath;
        }

        error = null;
        return true;
    }

    public string ResolveSafePath(string relativePath)
    {
        var root = ProjectRoot
            ?? throw new InvalidOperationException("未选择项目目录，请先在左侧选择工作目录");

        var normalized = relativePath?.Replace('/', Path.DirectorySeparatorChar) ?? string.Empty;
        string combined;
        try
        {
            combined = Path.GetFullPath(Path.Combine(root, normalized));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"路径非法：{ex.Message}");
        }

        var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!combined.Equals(root, StringComparison.OrdinalIgnoreCase)
            && !combined.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("拒绝访问项目目录之外的路径");
        }

        return combined;
    }

    public List<ProjectFileEntry> ListEntries(string relativePath)
    {
        var dirPath = ResolveSafePath(relativePath);
        if (!Directory.Exists(dirPath))
        {
            throw new InvalidOperationException($"目录不存在：{dirPath}");
        }

        var entries = new List<ProjectFileEntry>();
        foreach (var path in Directory.GetFileSystemEntries(dirPath))
        {
            var isDirectory = Directory.Exists(path) && !File.Exists(path);
            var name = Path.GetFileName(path);
            var rel = string.IsNullOrWhiteSpace(relativePath)
                ? name
                : $"{relativePath.Trim('/').Trim('\\')}/{name}";

            var entry = new ProjectFileEntry
            {
                Name = name,
                IsDirectory = isDirectory,
                RelativePath = rel,
            };

            if (!isDirectory)
            {
                var info = new FileInfo(path);
                entry.Size = info.Length;
                entry.UpdatedAt = info.LastWriteTime;
            }
            else
            {
                entry.UpdatedAt = Directory.GetLastWriteTime(path);
            }

            entries.Add(entry);
        }

        entries.Sort((a, b) =>
        {
            var dirCmp = b.IsDirectory.CompareTo(a.IsDirectory);
            return dirCmp != 0
                ? dirCmp
                : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });
        return entries;
    }

    public string ReadFile(string relativePath)
    {
        var fullPath = ResolveSafePath(relativePath);
        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException($"文件不存在：{fullPath}");
        }

        // 二进制/超大文件按文本截断处理，避免前端/日志爆炸。
        return File.Exists(fullPath)
            ? File.ReadAllText(fullPath, Encoding.UTF8)
            : string.Empty;
    }

    public void WriteFile(string relativePath, string content)
    {
        var fullPath = ResolveSafePath(relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(fullPath, content ?? string.Empty, Encoding.UTF8);
    }

    public void DeleteFile(string relativePath)
    {
        var fullPath = ResolveSafePath(relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
        else
        {
            throw new InvalidOperationException($"文件不存在：{fullPath}");
        }
    }
}