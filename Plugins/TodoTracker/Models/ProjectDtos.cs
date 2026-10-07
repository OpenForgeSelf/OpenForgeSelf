namespace ForgeSelf.Api.Plugins.TodoTracker.Models;

/// <summary>
/// 项目视图（PILOT-054 · FR-2）。项目档案的<b>唯一真相在宿主</b> <c>IProjectRegistry</c>
/// （<c>ForgeSelf.Abstractions/IProjectRegistry.cs</c>），本插件只做「归一 + 匹配 + 关联 + 计数」，
/// 不在插件里再造一张项目表（否则同一事物两份真相）。
/// </summary>
public class TodoProjectDto
{
    /// <summary>宿主项目 Id。</summary>
    public int Id { get; set; }

    /// <summary>项目根（宿主存的原始值）。</summary>
    public string Root { get; set; } = string.Empty;

    /// <summary>归一后的同一性判据（本插件 ProjectPathCanonicalizer 产出）。</summary>
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];

    /// <summary>登记来源（ai-agent/manual/todo-tracker/…）。</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>目录当前是否可达。</summary>
    public bool PathExists { get; set; }

    /// <summary>是否 git 仓库。</summary>
    public bool IsGitRepo { get; set; }

    /// <summary>本项目下的任务总数。</summary>
    public int TaskCount { get; set; }

    /// <summary>本项目下未完成（Stage&lt;Done）任务数。</summary>
    public int OpenTaskCount { get; set; }

    public DateTime LastActiveAt { get; set; }
}

/// <summary>路径归一 + 匹配/登记请求。</summary>
public class ResolveProjectRequest
{
    /// <summary>项目路径，任何写法（D:\proj、D:/proj、/d/proj、/mnt/d/proj、~/code/proj）。</summary>
    public string? Path { get; set; }
}

/// <summary>路径归一 + 匹配/登记结果（<b>不抛异常</b>，失败以 Ok=false + Error 原文返回）。</summary>
public class ResolveProjectResult
{
    public bool Ok { get; set; }

    /// <summary>失败原因原文（可直接展示；成功时为空）。</summary>
    public string? Error { get; set; }

    /// <summary>归一后的项目根。</summary>
    public string Root { get; set; } = string.Empty;

    /// <summary>同一性判据。</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>用户原始写法（回显用，让用户看见"我写的被理解成什么"）。</summary>
    public string Raw { get; set; } = string.Empty;

    /// <summary>匹配到的宿主项目 Id，0=没匹配上。</summary>
    public int ProjectId { get; set; }

    /// <summary>项目名（匹配到时）。</summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>本次是否新登记了宿主项目档案。</summary>
    public bool Registered { get; set; }

    /// <summary>宿主能力接缝是否可用（缺席时 Ok=false，且 Error 说明是接缝缺席而非路径问题）。</summary>
    public bool RegistryAvailable { get; set; }

    /// <summary>失败。</summary>
    public static ResolveProjectResult Failed(string error, string raw, bool available = true) =>
        new() { Ok = false, Error = error, Raw = raw, RegistryAvailable = available };
}

/// <summary>给任务关联/换关联项目。</summary>
public class LinkProjectRequest
{
    /// <summary>项目路径（任何写法）。与 <see cref="ProjectId"/> 二者至少给一个。</summary>
    public string? Path { get; set; }

    /// <summary>已知的宿主项目 Id。与 <see cref="Path"/> 同时给出时校验一致，不一致即 400。</summary>
    public int? ProjectId { get; set; }
}
