using System.IO;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;

namespace ForgeSelf.Api.Plugins.Sems.Services;

/// <summary>
/// 项目查询服务：读取「宿主共享项目清单」（AIAgent 选定工作目录时写入），供 sems 首页展示。
/// sems 与 AIAgent 之间仅共享存储、无 HTTP/DI 依赖，见 <see cref="SemsShared"/>。
/// </summary>
public interface IProjectService
{
    /// <summary>全部已登记项目（按最近活动倒序）。</summary>
    List<ProjectRecord> GetProjects();

    /// <summary>项目数（供首页统计）。</summary>
    int Count { get; }
}

public class ProjectService : IProjectService
{
    private readonly IContext _ctx;
    private readonly string _filePath;
    private static readonly object ReadLock = new();

    // 宿主契约（IDataLocationService）须经 Cordis 上下文 ctx.Get<T>() 获取：
    // 插件服务在子容器构建，仅含插件自身服务 + IContext，构造注入宿主契约会解析失败（500）。
    public ProjectService(IContext ctx)
    {
        _ctx = ctx;
        _filePath = SemsShared.GetProjectsFilePath(
            (_ctx.Get<IDataLocationService>() ?? throw new InvalidOperationException("宿主未提供 IDataLocationService 契约")).GetHostDataDirectory());
    }

    public int Count => GetProjects().Count;

    public List<ProjectRecord> GetProjects()
    {
        lock (ReadLock)
        {
            if (!File.Exists(_filePath))
            {
                return new List<ProjectRecord>();
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                var wrapper = JsonSerializer.Deserialize<ProjectsFile>(json, JsonOpts);
                var projects = wrapper?.Projects ?? new List<ProjectRecord>();

                // 目录已不存在（移动/删除）的项目标记为不可达，便于首页如实展示。
                foreach (var p in projects)
                {
                    p.PathExists = !string.IsNullOrWhiteSpace(p.Root) && Directory.Exists(p.Root);
                    p.IsGitRepo = p.PathExists && Directory.Exists(Path.Combine(p.Root, ".git"));
                }

                return projects;
            }
            catch (Exception)
            {
                // 文件损坏/被并发占写时按空处理，不崩首页
                return new List<ProjectRecord>();
            }
        }
    }

    private class ProjectsFile
    {
        public List<ProjectRecord> Projects { get; set; } = new();
    }

    private static readonly JsonSerializerOptions JsonOpts = new();
}