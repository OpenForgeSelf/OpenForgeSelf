using System.IO;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

/// <summary>
/// 项目登记服务：当会话选定工作目录（一个目录视为一个项目）时，把该项目写入「宿主共享文件」。
/// sems 插件读取同一共享文件展示项目列表，实现跨插件的项目登记解耦（AIAgent 写、sems 读）。
/// </summary>
public interface IProjectRegistryService
{
    /// <summary>登记（或更新）一个项目根，返回登记结果。目录不存在返回 false + 错误。</summary>
    bool Register(string root, out string? error);

    /// <summary>读取当前全部已登记项目（按最近活动倒序）。文件不存在返回空列表。</summary>
    List<ProjectRecord> GetAll();
}

public class ProjectRegistryService : IProjectRegistryService
{
    private static readonly object RegistryLock = new();
    private readonly string _filePath;

    // 宿主契约（IDataLocationService）经 Cordis 上下文 ctx.Get<T>() 获取：
    // 插件服务在子容器构建，仅含插件自身服务 + IContext，构造注入宿主契约会解析失败。
    public ProjectRegistryService(IContext ctx)
    {
        _filePath = SemsShared.GetProjectsFilePath(
            (ctx.Get<IDataLocationService>() ?? throw new InvalidOperationException("宿主未提供 IDataLocationService 契约")).GetHostDataDirectory());
    }

    public bool Register(string root, out string? error)
    {
        root = root?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(root))
        {
            error = "项目目录不能为空";
            return false;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(root);
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

        var now = DateTime.Now;
        var records = GetAll();
        var existing = records.FirstOrDefault(r =>
            string.Equals(r.Root, fullPath, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            existing.Name = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar));
            existing.LastActivityAt = now;
            existing.Source = "ai-agent";
        }
        else
        {
            records.Add(new ProjectRecord
            {
                Root = fullPath,
                Name = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar)),
                Source = "ai-agent",
                SelectedAt = now,
                LastActivityAt = now
            });
        }

        Save(records);
        error = null;
        return true;
    }

    public List<ProjectRecord> GetAll()
    {
        lock (RegistryLock)
        {
            if (!File.Exists(_filePath))
            {
                return new List<ProjectRecord>();
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                var wrapper = JsonSerializer.Deserialize<ProjectsFile>(json,
                    JsonOpts);
                return wrapper?.Projects ?? new List<ProjectRecord>();
            }
            catch (Exception)
            {
                // 文件损坏/被并发占写时按空处理，不崩面板
                return new List<ProjectRecord>();
            }
        }
    }

    private void Save(List<ProjectRecord> records)
    {
        var ordered = records
            .OrderByDescending(r => r.LastActivityAt)
            .ToList();
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        lock (RegistryLock)
        {
            File.WriteAllText(_filePath,
                JsonSerializer.Serialize(new ProjectsFile { Projects = ordered }, JsonOpts));
        }
    }

    private class ProjectsFile
    {
        public List<ProjectRecord> Projects { get; set; } = new();
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true
    };
}