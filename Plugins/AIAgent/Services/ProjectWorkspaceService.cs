using System.Text;
using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

// 契约上移（2026-10-28 工具命名空间统一）：IProjectWorkspaceService 与 ProjectFileEntry 的接口定义
// 已移至 ForgeSelf.Abstractions/IProjectWorkspaceService.cs（mcp-center 公共工具跨插件消费，架构铁律 2）。
// 本文件保留实现 ProjectWorkspaceService，继承 Abstractions 中的接口。

public class ProjectWorkspaceService : IProjectWorkspaceService
{
    private const string RootLockKey = nameof(ProjectWorkspaceService);
    /// <summary>默认根候选 (a)：环境变量名。</summary>
    public const string DefaultRootEnvVariable = "FORGESELF_DEFAULT_PROJECT_ROOT";

    private string? _root;
    private string? _defaultRoot;

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

    public string? DefaultRoot
    {
        get
        {
            lock (RootLockKey)
            {
                _defaultRoot ??= ResolveDefaultRoot(
                    Environment.GetEnvironmentVariable(DefaultRootEnvVariable),
                    AppContext.BaseDirectory);
                return _defaultRoot;
            }
        }
    }

    /// <summary>
    /// 默认根解析纯函数（DefaultRoot 缓存未命中时调用；internal 供测试工程直测三级候选）。
    /// </summary>
    internal static string? ResolveDefaultRoot(string? envVariable, string probeStart)
    {
        // 优先级 (a)：环境变量 FORGESELF_DEFAULT_PROJECT_ROOT 存在且为已存在目录。
        if (!string.IsNullOrWhiteSpace(envVariable)
            && Directory.Exists(envVariable.Trim()))
        {
            return Path.GetFullPath(envVariable.Trim());
        }

        // 优先级 (b)：程序基目录逐级上溯，第一个含 ForgeSelf.slnx 或 .git 的祖先目录（含自身）。
        var dir = Path.GetFullPath(probeStart);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "ForgeSelf.slnx")) || Directory.Exists(Path.Combine(dir, ".git")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        // 优先级 (c)：无法确定 → null，由调用方给引导错误。
        return null;
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
        // 根优先级：GUI 选定（ProjectRoot）> 回退根（DefaultRoot）；两者皆 null → 引导错误。
        var root = ProjectRoot
            ?? DefaultRoot
            ?? throw new InvalidOperationException(
                "未选择项目目录且未设置 FORGESELF_DEFAULT_PROJECT_ROOT，请在左侧选择工作目录或设置环境变量");

        var normalized = relativePath?.Replace('/', Path.DirectorySeparatorChar) ?? string.Empty;

        // 绝对路径直通：不与 root 拼接，但仍必须落在 root 前缀内（防穿越语义不放松）。
        string combined;
        if (Path.IsPathRooted(normalized))
        {
            try
            {
                combined = Path.GetFullPath(normalized);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"路径非法：{ex.Message}");
            }
        }
        else
        {
            try
            {
                combined = Path.GetFullPath(Path.Combine(root, normalized));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"路径非法：{ex.Message}");
            }
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