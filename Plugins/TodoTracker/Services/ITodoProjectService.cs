using ForgeSelf.Api.Plugins.TodoTracker.Models;

namespace ForgeSelf.Api.Plugins.TodoTracker.Services;

/// <summary>
/// 项目关联服务（PILOT-054 · FR-2）。项目档案真相在宿主 <c>IProjectRegistry</c>，
/// 本服务只负责「路径归一 → 匹配 → 必要时登记 → 回读 Id」与「项目列表 + 任务计数」。
/// </summary>
public interface ITodoProjectService
{
    /// <summary>宿主能力接缝是否在场（缺席时解析一律失败，且失败原因写的是"接缝未就绪"而非"路径非法"）。</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// 归一一条路径。只做归一与目录存在性判定，<b>不触碰注册表</b>（用于"先归一再比对"）。
    /// </summary>
    ProjectPathCanonicalizer.Result Canonicalize(string? path);

    /// <summary>
    /// 归一 + 匹配宿主项目档案；<paramref name="registerIfMissing"/> 为真且未命中时登记一条新档案
    /// （来源记 <c>todo-tracker</c>）。不抛业务异常，失败以 <see cref="ResolveProjectResult.Error"/> 原文返回。
    /// </summary>
    ResolveProjectResult Resolve(string? path, bool registerIfMissing);

    /// <summary>按宿主项目 Id 取档案（含任务计数）。不存在返回 null。</summary>
    TodoProjectDto? Get(int projectId);

    /// <summary>全部项目（按最近活动倒序）+ 每项目的任务数/未完成数。</summary>
    IReadOnlyList<TodoProjectDto> ListProjects();

    /// <summary>批量取项目名（列表页投影用，避免逐行查注册表）。</summary>
    Dictionary<int, string> NamesFor(IEnumerable<int> projectIds);
}
