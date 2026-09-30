using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Entities;
using NewLife;
using NewLife.Log;

namespace ForgeSelf.Api.Services;

/// <summary>
/// 项目工作区宿主实现（L1 能力接缝 <see cref="IProjectRegistry"/>）。
/// 由宿主 seed 进插件 root 上下文，常驻 app 生命周期；AIAgent（登记触发源）与
/// sems（面板消费方）平等经 <c>ctx.Get&lt;IProjectRegistry&gt;()</c> 消费。
/// 数据落在宿主库（ConnName=ForgeSelf）的 Project / RunCommand 两张表。
/// 构造时惰性一次性迁移：旧共享 JSON（<see cref="SemsShared.GetProjectsFilePath"/>）导入为库记录。
/// </summary>
public class HostProjectRegistry : IProjectRegistry
{
    private readonly string? _legacyJsonPath;

    /// <summary>构造。传入宿主数据根目录（解析旧共享 JSON 迁移源）。</summary>
    /// <param name="hostDataDirectory">宿主数据根（通常由 IDataLocationService.GetHostDataDirectory 提供）。</param>
    public HostProjectRegistry(string hostDataDirectory)
    {
        if (!string.IsNullOrWhiteSpace(hostDataDirectory))
            // spec028：旧共享 JSON 路径来源内联（原 SemsShared.GetProjectsFilePath 已被 IProjectRegistry 数据库接缝取代，
            // 不再依赖 SemsShared 路径常量，避免 Abstractions 中遗留契约的引用）。
            _legacyJsonPath = Path.Combine(hostDataDirectory, "Shared", "sems-projects.json");

        // 构造期不触碰数据库连接；首次使用时再惰性迁移（避免无 DB 依赖的单元测试构造失败）。
    }

    /// <summary>登记或更新一个项目根（来源按 <c>ai-agent</c> 记，保持既有调用方语义）。</summary>
    public bool Register(string root, out string? error) => Register(root, null, out error);

    /// <summary>
    /// 登记或更新一个项目根并指定来源。Root 已存在时<b>只刷新 LastActiveAt</b>，
    /// 不覆写 Name/Source/Type/Description/Tags —— 手工编辑优先于自动登记。目录不存在返回 false + 错误。
    /// </summary>
    public bool Register(string root, string? source, out string? error)
    {
        error = null;
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
        var existing = Project.FindByRoot(fullPath);
        if (existing != null)
        {
            // 只刷新活跃时间；Name 仅在历史空值时补齐，绝不覆写用户已改的名字
            if (string.IsNullOrWhiteSpace(existing.Name))
                existing.Name = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar));
            existing.LastActiveAt = now;
            existing.Save();
            return true;
        }

        var project = new Project
        {
            Root = fullPath,
            Name = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar)),
            Type = string.Empty,
            Description = string.Empty,
            Tags = string.Empty,
            Source = string.IsNullOrWhiteSpace(source) ? "ai-agent" : source.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
            LastActiveAt = now
        };
        project.Insert();
        return true;
    }

    /// <summary>全量项目（按 LastActiveAt 倒序）。</summary>
    public List<ProjectInfo> GetAll()
    {
        EnsureMigrated();
        return Project.FindAll()
            .OrderByDescending(e => e.LastActiveAt)
            .Select(ToInfo).ToList();
    }

    /// <summary>单项目（含运行命令）。</summary>
    public ProjectInfo? Get(int id)
    {
        EnsureMigrated();
        var project = Project.FindById(id);
        if (project == null) return null;
        var info = ToInfo(project);
        info.Commands = RunCommand.FindAllByProjectId(id)
            .OrderBy(c => c.Sort)
            .Select(ToCommandInfo).ToList();
        return info;
    }

    /// <summary>档案编辑（Name/Type/Description/Tags）。</summary>
    public bool Update(int id, ProjectUpdate update)
    {
        EnsureMigrated();
        var project = Project.FindById(id);
        if (project == null) return false;

        if (update.Name != null) project.Name = update.Name;
        if (update.Type != null) project.Type = update.Type;
        if (update.Description != null) project.Description = update.Description;
        if (update.Tags != null) project.Tags = update.Tags;
        project.Save();
        return true;
    }

    /// <summary>
    /// 移除项目档案并级联删除其运行命令。只删数据库行，<b>不触碰磁盘目录与文件</b>。
    /// </summary>
    public bool Remove(int id, out string? error)
    {
        error = null;
        EnsureMigrated();
        var project = Project.FindById(id);
        if (project == null)
        {
            error = $"项目不存在：{id}";
            return false;
        }

        var deleted = RunCommand.DeleteByProjectId(id);
        var root = project.Root;
        project.Delete();
        XTrace.Log.Info("项目工作区：已移除项目 {0}（Root={1}，级联删除命令 {2} 条，磁盘文件未触碰）",
            id, root, deleted);
        return true;
    }

    /// <summary>新增运行命令，返回新命令 Id（≤0 表示失败）。</summary>
    public int AddCommand(int projectId, RunCommandInfo command)
    {
        EnsureMigrated();
        var project = Project.FindById(projectId);
        if (project == null) return 0;

        var entity = new RunCommand
        {
            ProjectId = projectId,
            Name = command.Name,
            Script = command.Script,
            Url = command.Url ?? string.Empty,
            Sort = command.Sort,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        entity.Insert();
        return entity.Id;
    }

    /// <summary>更新运行命令。</summary>
    public bool UpdateCommand(int commandId, RunCommandUpdate update)
    {
        EnsureMigrated();
        var entity = RunCommand.FindById(commandId);
        if (entity == null) return false;

        if (update.Name != null) entity.Name = update.Name;
        if (update.Script != null) entity.Script = update.Script;
        if (update.Url != null) entity.Url = update.Url;
        if (update.Sort != null) entity.Sort = update.Sort.Value;
        entity.Save();
        return true;
    }

    /// <summary>删除运行命令。</summary>
    public bool DeleteCommand(int commandId)
    {
        EnsureMigrated();
        var entity = RunCommand.FindById(commandId);
        if (entity == null) return false;
        entity.Delete();
        return true;
    }

    /// <summary>某项目的全部运行命令（按 Sort 升序）。</summary>
    public List<RunCommandInfo> GetCommands(int projectId)
    {
        EnsureMigrated();
        return RunCommand.FindAllByProjectId(projectId)
            .OrderBy(c => c.Sort)
            .Select(ToCommandInfo).ToList();
    }

    #region 转换
    private static ProjectInfo ToInfo(Project p)
    {
        var exists = !string.IsNullOrWhiteSpace(p.Root) && Directory.Exists(p.Root);
        return new ProjectInfo
        {
            Id = p.Id,
            Root = p.Root,
            Name = p.Name,
            Type = p.Type,
            Description = p.Description,
            Tags = p.Tags,
            Source = p.Source,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
            LastActiveAt = p.LastActiveAt,
            PathExists = exists,
            IsGitRepo = exists && Directory.Exists(Path.Combine(p.Root, ".git"))
        };
    }

    private static RunCommandInfo ToCommandInfo(RunCommand c) => new()
    {
        Id = c.Id,
        ProjectId = c.ProjectId,
        Name = c.Name,
        Script = c.Script,
        Url = string.IsNullOrEmpty(c.Url) ? null : c.Url,
        Sort = c.Sort,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };
    #endregion

    #region 一次性迁移
    private bool _migrated;
    private readonly object _migrateLock = new();

    /// <summary>
    /// 惰性一次性迁移：<c>Project</c> 表为空 且 旧共享 JSON 存在 → 导入全部记录到库。
    /// 原 JSON 文件保留不删（安全约束）。表已有数据则跳过（防重导）。
    /// </summary>
    private void EnsureMigrated()
    {
        if (_migrated) return;
        lock (_migrateLock)
        {
            if (_migrated) return;
            try
            {
                if (Project.FindCount() == 0 && _legacyJsonPath != null && File.Exists(_legacyJsonPath))
                {
                    ImportLegacyJson(_legacyJsonPath);
                }
            }
            catch (Exception ex)
            {
                // 迁移失败不应阻断正常读写；记录日志后继续以空库运行。
                XTrace.Log.Warn("项目工作区迁移失败（已跳过）：{0}", ex.Message);
            }
            finally
            {
                _migrated = true;
            }
        }
    }

    private static void ImportLegacyJson(string jsonPath)
    {
        var json = File.ReadAllText(jsonPath);
        var wrapper = JsonSerializer.Deserialize<LegacyProjectsFile>(json);
        if (wrapper?.Projects == null || wrapper.Projects.Count == 0) return;

        var now = DateTime.Now;
        foreach (var rec in wrapper.Projects)
        {
            if (string.IsNullOrWhiteSpace(rec.Root)) continue;
            var fullPath = rec.Root;
            try { fullPath = Path.GetFullPath(rec.Root); }
            catch { /* 保留原值 */ }

            var selected = rec.SelectedAt == default ? now : rec.SelectedAt;
            var lastActive = rec.LastActivityAt == default ? selected : rec.LastActivityAt;

            var project = new Project
            {
                Root = fullPath,
                Name = string.IsNullOrWhiteSpace(rec.Name)
                    ? Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar))
                    : rec.Name,
                Type = string.Empty,
                Description = string.Empty,
                Tags = string.Empty,
                Source = string.IsNullOrWhiteSpace(rec.Source) ? "ai-agent" : rec.Source,
                CreatedAt = selected,
                UpdatedAt = lastActive,
                LastActiveAt = lastActive
            };
            project.Insert();
        }

        XTrace.Log.Info("项目工作区已从旧共享 JSON 迁移 {0} 条记录（原文件保留）：{1}",
            wrapper.Projects.Count, jsonPath);
    }

    private class LegacyProjectsFile
    {
        public List<LegacyProjectRecord> Projects { get; set; } = new();
    }

    /// <summary>
    /// 旧共享 JSON（<c>Shared/sems-projects.json</c>）单条记录的反序列化 DTO。
    /// 字段名与已 Obsolete 的 <see cref="ForgeSelf.Abstractions.ProjectRecord"/> 完全一致，
    /// 以兼容历史文件；迁移完成后本类型取代对 Abstractions.ProjectRecord 的引用（spec028）。
    /// </summary>
    private class LegacyProjectRecord
    {
        public string Root { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Source { get; set; } = "ai-agent";
        public DateTime SelectedAt { get; set; } = DateTime.Now;
        public DateTime LastActivityAt { get; set; } = DateTime.Now;
    }
    #endregion
}
