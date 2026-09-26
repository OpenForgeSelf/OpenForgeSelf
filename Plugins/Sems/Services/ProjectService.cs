using ForgeSelf.Abstractions;
using ForgeSelf.Core;

namespace ForgeSelf.Api.Plugins.Sems.Services;

/// <summary>
/// sems 项目查询适配层：经宿主级 <see cref="IProjectRegistry"/> 接缝（L1 契约）读取项目清单供首页展示。
/// 构造仅注入 <see cref="IContext"/>；<b>每次调用都经 ctx.Get 重新解析宿主接缝</b>，
/// 不把解析到的 <see cref="IProjectRegistry"/> 实例缓存为字段（提供方热重载后共享表自动摘除，null 走降级）。
/// 旧的 JSON 直读逻辑已移除——JSON 仅作为宿主实现（HostProjectRegistry）的一次性迁移源。
/// </summary>
public interface IProjectService
{
    /// <summary>全部已登记项目（按最近活动倒序）。</summary>
    List<ProjectInfo> GetProjects();

    /// <summary>项目数（供首页统计）。</summary>
    int Count { get; }
}

public class ProjectService : IProjectService
{
    private readonly IContext _ctx;

    public ProjectService(IContext ctx)
    {
        _ctx = ctx;
    }

    private IProjectRegistry? Registry => _ctx.Get<IProjectRegistry>();

    public int Count => GetProjects().Count;

    public List<ProjectInfo> GetProjects()
    {
        return Registry?.GetAll() ?? new List<ProjectInfo>();
    }
}
