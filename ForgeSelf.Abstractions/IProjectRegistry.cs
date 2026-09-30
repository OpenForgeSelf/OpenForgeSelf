using System;
using System.Collections.Generic;

namespace ForgeSelf.Abstractions;

/// <summary>
/// 项目工作区（宿主级核心流程）能力接缝（L1 契约）。
/// 由宿主 <c>HostProjectRegistry</c> 实现并 seed 进插件 root 上下文（常驻 app 生命周期）。
/// 提供方只定义契约与数据寿命，<b>不绑定单一登记触发源</b>：AIAgent 选目录、sems 面板手工添加、
/// 未来任何插件都经 <c>ctx.Get&lt;IProjectRegistry&gt;()</c> 平等登记/消费（见 docs/01-architecture/host-capability-seams.md §4.1 方案 B）。
/// 消费方必须<b>每次用每次 Get</b>，禁止把解析到的实例缓存为字段（提供方热重载后共享表自动摘除）。
/// </summary>
public interface IProjectRegistry
{
    /// <summary>登记或更新一个项目根（等价 <see cref="Register(string,string,out string?)"/> 且来源为 <c>ai-agent</c>）。Root 已存在则刷新 LastActiveAt。目录不存在返回 false + 错误。</summary>
    bool Register(string root, out string? error);

    /// <summary>
    /// 登记或更新一个项目根，并指定登记来源（<c>ai-agent</c> / <c>manual</c> 等）。
    /// Root 已存在时<b>只刷新 LastActiveAt</b>，不覆写 Name/Source/Type/Description/Tags（手工编辑优先于自动登记）。
    /// <paramref name="source"/> 为空/null 时按 <c>ai-agent</c> 记（保持既有调用方语义）。目录不存在返回 false + 错误。
    /// </summary>
    bool Register(string root, string? source, out string? error);

    /// <summary>全量项目（按 LastActiveAt 倒序）。无项目返回空列表。</summary>
    List<ProjectInfo> GetAll();

    /// <summary>单项目（含运行命令）。不存在返回 null。</summary>
    ProjectInfo? Get(int id);

    /// <summary>档案编辑（Name/Type/Description/Tags）。</summary>
    bool Update(int id, ProjectUpdate update);

    /// <summary>
    /// 移除项目档案，并<b>级联删除其全部运行命令</b>。只删数据库记录，不触碰磁盘目录与文件。
    /// 不存在返回 false + 错误。调用方若需拒绝「有存活运行会话」的项目，应在本方法之前自行校验。
    /// </summary>
    bool Remove(int id, out string? error);

    /// <summary>新增运行命令，返回新命令 Id（≤0 表示失败）。</summary>
    int AddCommand(int projectId, RunCommandInfo command);

    /// <summary>更新运行命令。</summary>
    bool UpdateCommand(int commandId, RunCommandUpdate update);

    /// <summary>删除运行命令。</summary>
    bool DeleteCommand(int commandId);

    /// <summary>某项目的全部运行命令（按 Sort 升序）。</summary>
    List<RunCommandInfo> GetCommands(int projectId);
}

/// <summary>项目信息（L1 契约 DTO，替代旧 <see cref="ProjectRecord"/>）。</summary>
public class ProjectInfo
{
    /// <summary>项目 Id（自增主键）。</summary>
    public int Id { get; set; }

    /// <summary>项目根目录绝对路径（唯一键）。</summary>
    public string Root { get; set; } = string.Empty;

    /// <summary>项目名（默认取根目录名）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>类型：frontend/backend/fullstack/library/tool/other 或自定义文本。</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>描述。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>标签，逗号分隔（SQLite 无数组，渲染层 split）。</summary>
    public string Tags { get; set; } = string.Empty;

    /// <summary>登记来源（ai-agent/manual）。</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>首次登记时间。</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>更新时间。</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>最近活动时间（选择目录时刷新）。</summary>
    public DateTime LastActiveAt { get; set; }

    /// <summary>项目根目录当前是否可达（仅消费端计算展示，不落盘）。</summary>
    public bool PathExists { get; set; }

    /// <summary>是否为 git 仓库（根目录含 .git；仅消费端计算展示，不落盘）。</summary>
    public bool IsGitRepo { get; set; }

    /// <summary>该项目的运行命令列表（Get/GetAll 时附带）。</summary>
    public List<RunCommandInfo> Commands { get; set; } = new();
}

/// <summary>运行命令信息（L1 契约 DTO）。</summary>
public class RunCommandInfo
{
    /// <summary>命令 Id（自增主键）。</summary>
    public int Id { get; set; }

    /// <summary>所属项目 Id。</summary>
    public int ProjectId { get; set; }

    /// <summary>显示名（如「前端 dev」）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>运行脚本（在项目根执行）。</summary>
    public string Script { get; set; } = string.Empty;

    /// <summary>运行后访问地址（可空）。</summary>
    public string? Url { get; set; }

    /// <summary>排序，默认 0。</summary>
    public int Sort { get; set; }

    /// <summary>是否有可访问 URL（派生展示字段）。</summary>
    public bool HasUrl => !string.IsNullOrWhiteSpace(Url);

    /// <summary>创建时间。</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>更新时间。</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>项目档案编辑（Name/Type/Description/Tags）。</summary>
public class ProjectUpdate
{
    /// <summary>项目名。</summary>
    public string? Name { get; set; }

    /// <summary>类型。</summary>
    public string? Type { get; set; }

    /// <summary>描述。</summary>
    public string? Description { get; set; }

    /// <summary>标签，逗号分隔。</summary>
    public string? Tags { get; set; }
}

/// <summary>运行命令编辑（Name/Script/Url/Sort）。</summary>
public class RunCommandUpdate
{
    /// <summary>显示名。</summary>
    public string? Name { get; set; }

    /// <summary>运行脚本。</summary>
    public string? Script { get; set; }

    /// <summary>运行后访问地址（可空；传 null 表示清空）。</summary>
    public string? Url { get; set; }

    /// <summary>排序。</summary>
    public int? Sort { get; set; }
}
