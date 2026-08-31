using System;

namespace ForgeSelf.Abstractions;

/// <summary>
/// sems（软件工程管理系统）与其他插件共享的「项目登记」契约。
///
/// 去耦思路：AIAgent 在会话选定工作目录（一个目录即一个项目）时，会把该项目登记写入共享文件；
/// sems 只读取这份共享文件来展示项目列表，两插件之间不产生 HTTP/DI 依赖，仅依赖共享存储。
/// 共享路径由各插件经 <see cref="IDataLocationService.GetHostDataDirectory"/> 派生，保证两端一致。
/// </summary>
public static class SemsShared
{
    /// <summary>共享项目登记文件（相对宿主数据根 <c>Shared/</c> 目录）。</summary>
    public const string ProjectsFileRelative = "Shared/sems-projects.json";

    /// <summary>派生共享项目登记文件的绝对路径（宿主数据根经调用方提供）。</summary>
    public static string GetProjectsFilePath(string hostDataRoot)
        => Path.Combine(hostDataRoot, "Shared", "sems-projects.json");
}

/// <summary>一条项目登记记录（AIAgent 写、sems 读的共享契约）。</summary>
public class ProjectRecord
{
    /// <summary>项目根目录绝对路径（唯一键）。</summary>
    public string Root { get; set; } = string.Empty;

    /// <summary>项目名（默认取根目录名）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>来源（登记方，默认 ai-agent）。</summary>
    public string Source { get; set; } = "ai-agent";

    /// <summary>首次登记时间。更新选择时刷新为最近一次。</summary>
    public DateTime SelectedAt { get; set; } = DateTime.Now;

    /// <summary>最近一次被选中的时间。</summary>
    public DateTime LastActivityAt { get; set; } = DateTime.Now;

    /// <summary>项目根目录当前是否可达（仅 sems 端计算展示，不落盘）。</summary>
    public bool PathExists { get; set; }

    /// <summary>是否为 git 仓库（根目录含 .git；仅 sems 端计算展示，不落盘）。</summary>
    public bool IsGitRepo { get; set; }
}