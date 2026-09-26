using ForgeSelf.Abstractions;
using ForgeSelf.Core;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

/// <summary>
/// AIAgent 项目登记适配层：把宿主级 <see cref="IProjectRegistry"/> 接缝（L1 契约）暴露给 AIAgent 内部使用。
/// 构造仅注入 <see cref="IContext"/>；<b>每次调用都经 ctx.Get 重新解析宿主接缝</b>，
/// 不把解析到的 <see cref="IProjectRegistry"/> 实例缓存为字段（提供方热重载后共享表自动摘除，null 走降级）。
/// 旧的 JSON 直写逻辑已移除——JSON 仅作为宿主实现（HostProjectRegistry）的一次性迁移源。
/// </summary>
public interface IProjectRegistryService
{
    /// <summary>登记（或更新）一个项目根，返回登记结果。目录不存在返回 false + 错误。</summary>
    bool Register(string root, out string? error);

    /// <summary>读取当前全部已登记项目（按最近活动倒序）。接缝不可用时返回空列表。</summary>
    List<ProjectInfo> GetAll();
}

public class ProjectRegistryService : IProjectRegistryService
{
    private readonly IContext _ctx;

    public ProjectRegistryService(IContext ctx)
    {
        _ctx = ctx;
    }

    private IProjectRegistry? Registry => _ctx.Get<IProjectRegistry>();

    public bool Register(string root, out string? error)
    {
        var registry = Registry;
        if (registry == null)
        {
            error = "项目登记服务不可用（宿主接缝未就绪）";
            return false;
        }
        return registry.Register(root, out error);
    }

    public List<ProjectInfo> GetAll()
    {
        return Registry?.GetAll() ?? new List<ProjectInfo>();
    }
}
