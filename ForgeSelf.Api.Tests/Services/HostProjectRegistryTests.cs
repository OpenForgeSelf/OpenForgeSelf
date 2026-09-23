using System.IO;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Services;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace ForgeSelf.Api.Tests.Services;

/// <summary>
/// <see cref="HostProjectRegistry"/> 行为测试：登记 / 唯一键冲突 /
/// 档案更新 / 运行命令 CRUD / 旧 JSON 一次性迁移导入。
/// 使用临时 SQLite 库（XCode），归入 XCode 串行集合避免连接串串扰。
/// </summary>
[Collection("XCode")]
public class HostProjectRegistryTests
{
    private readonly string _dbDir;
    private readonly string _hostDataDir;

    public HostProjectRegistryTests()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfProj_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dbDir);

        _hostDataDir = Path.Combine(_dbDir, "hostdata");
        Directory.CreateDirectory(_hostDataDir);

        // HostProjectRegistry 不依赖 MS DI，直接以 hostDataDir 构造即可。
        // 仅需为 ForgeSelf 连接名建临时 SQLite 库并建表。
        DAL.AddConnStr("ForgeSelf", $"Data Source={Path.Combine(_dbDir, "ForgeSelf.db")}", null, "SQLite");
        EntityFactory.InitConnection("ForgeSelf");

        // XCode 的实体级缓存（Meta.Cache）为进程级全局单例，跨测试共享且不会因连接串切换而失效，
        // 会导致 FindAll/FindByXxx 返回旧库的缓存而非当前临时库。每个测试重置连接后清空缓存，保证隔离。
        Project.Meta.Cache.Clear("test reset");
        RunCommand.Meta.Cache.Clear("test reset");
        Project.Meta.Cache.Expire = 0;
        RunCommand.Meta.Cache.Expire = 0;
    }

    private HostProjectRegistry Create() => new(_hostDataDir);

    [Fact]
    public void Register_Creates_Project_And_Returns_True()
    {
        var dir = CreateTempProjectDir("demo");
        var registry = Create();

        var ok = registry.Register(dir, out var error);

        Assert.True(ok);
        Assert.Null(error);
        var all = registry.GetAll();
        Assert.Single(all);
        Assert.Equal(dir, all[0].Root);
        Assert.Equal("demo", all[0].Name);
        Assert.Equal("ai-agent", all[0].Source);
    }

    [Fact]
    public void Register_NonexistentDirectory_ReturnsFalse_WithError()
    {
        var registry = Create();
        var ok = registry.Register(Path.Combine(_dbDir, "no-such-dir-xyz"), out var error);

        Assert.False(ok);
        Assert.False(string.IsNullOrEmpty(error));
        Assert.Empty(registry.GetAll());
    }

    [Fact]
    public void Register_SameRoot_Twice_RefreshesLastActiveAt_NotDuplicate()
    {
        var dir = CreateTempProjectDir("refresh");
        var registry = Create();

        registry.Register(dir, out _);
        var first = registry.GetAll().Single();
        var firstId = first.Id;
        var firstActive = first.LastActiveAt;

        // 略等以确保时间推进
        Thread.Sleep(20);
        registry.Register(dir, out _);

        var all = registry.GetAll();
        Assert.Single(all);
        Assert.Equal(firstId, all[0].Id);
        Assert.True(all[0].LastActiveAt >= firstActive);
    }

    [Fact]
    public void Update_Edits_Profile_Fields()
    {
        var dir = CreateTempProjectDir("editme");
        var registry = Create();
        registry.Register(dir, out _);
        var id = registry.GetAll().Single().Id;

        var ok = registry.Update(id, new ProjectUpdate
        {
            Name = "Renamed",
            Type = "frontend",
            Description = "demo desc",
            Tags = "a,b"
        });

        Assert.True(ok);
        var updated = registry.Get(id);
        Assert.NotNull(updated);
        Assert.Equal("Renamed", updated!.Name);
        Assert.Equal("frontend", updated.Type);
        Assert.Equal("demo desc", updated.Description);
        Assert.Equal("a,b", updated.Tags);
    }

    [Fact]
    public void Update_UnknownId_ReturnsFalse()
    {
        var registry = Create();
        Assert.False(registry.Update(99999, new ProjectUpdate { Name = "x" }));
    }

    [Fact]
    public void Command_CRUD_Works()
    {
        var dir = CreateTempProjectDir("withcmd");
        var registry = Create();
        registry.Register(dir, out _);
        var projectId = registry.GetAll().Single().Id;

        var cmdId = registry.AddCommand(projectId, new RunCommandInfo
        {
            Name = "前端 dev",
            Script = "npm run dev",
            Url = "http://localhost:5173",
            Sort = 1
        });
        Assert.True(cmdId > 0);

        var cmds = registry.GetCommands(projectId);
        Assert.Single(cmds);
        Assert.Equal("前端 dev", cmds[0].Name);
        Assert.Equal("npm run dev", cmds[0].Script);
        Assert.Equal("http://localhost:5173", cmds[0].Url);
        Assert.True(cmds[0].HasUrl);

        // Update（部分更新语义：仅更新传入的非 null 字段，未传字段保持原值）
        Assert.True(registry.UpdateCommand(cmdId, new RunCommandUpdate
        {
            Name = "dev",
            Script = "yarn dev",
            Sort = 5
        }));
        var updated = registry.GetCommands(projectId).Single();
        Assert.Equal("dev", updated.Name);
        Assert.Equal("yarn dev", updated.Script);
        Assert.Equal("http://localhost:5173", updated.Url); // 未传 Url → 保持原值
        Assert.Equal(5, updated.Sort);

        // Delete
        Assert.True(registry.DeleteCommand(cmdId));
        Assert.Empty(registry.GetCommands(projectId));
        Assert.False(registry.DeleteCommand(cmdId));
    }

    [Fact]
    public void Get_Returns_Commands_Inline()
    {
        var dir = CreateTempProjectDir("inline");
        var registry = Create();
        registry.Register(dir, out _);
        var projectId = registry.GetAll().Single().Id;
        registry.AddCommand(projectId, new RunCommandInfo { Name = "c1", Script = "s1" });

        var info = registry.Get(projectId);
        Assert.NotNull(info);
        Assert.Single(info!.Commands);
    }

    [Fact]
    public void AddCommand_UnknownProject_ReturnsZero()
    {
        var registry = Create();
        Assert.Equal(0, registry.AddCommand(99999, new RunCommandInfo { Name = "x", Script = "y" }));
    }

    [Fact]
    public void Migration_Imports_LegacyJson_When_TableEmpty()
    {
        // 写入旧共享 JSON（SemsShared 约定路径；字段名与 ProjectRecord 一致 = PascalCase，
        // 与旧 ProjectRegistryService 的默认序列化输出一致）。
        var sharedDir = Path.Combine(_hostDataDir, "Shared");
        Directory.CreateDirectory(sharedDir);
        var legacy = new
        {
            Projects = new[]
            {
                new ProjectRecord
                {
                    Root = CreateTempProjectDir("legacy-a"),
                    Name = "LegacyA",
                    Source = "ai-agent",
                    SelectedAt = new DateTime(2026, 1, 1),
                    LastActivityAt = new DateTime(2026, 1, 2)
                },
                new ProjectRecord
                {
                    Root = CreateTempProjectDir("legacy-b"),
                    Name = "LegacyB",
                    Source = "manual",
                    SelectedAt = new DateTime(2026, 2, 1),
                    LastActivityAt = new DateTime(2026, 2, 2)
                }
            }
        };
        File.WriteAllText(Path.Combine(sharedDir, "sems-projects.json"),
            JsonSerializer.Serialize(legacy));

        var registry = Create();
        var all = registry.GetAll();

        Assert.Equal(2, all.Count);
        // 按 LastActiveAt 倒序：legacy-b 应在前
        Assert.Equal("legacy-b", Path.GetFileName(all[0].Root.TrimEnd(Path.DirectorySeparatorChar)));
        // 默认值
        Assert.Equal("", all[0].Type);
        Assert.Equal("", all[0].Tags);
        Assert.Equal("manual", all[0].Source);
    }

    [Fact]
    public void Migration_Skips_When_TableAlreadyHasData()
    {
        var dir = CreateTempProjectDir("existing");
        var registry = Create();
        registry.Register(dir, out _);

        // 即便存在 legacy JSON，已建库不应再导入
        var sharedDir = Path.Combine(_hostDataDir, "Shared");
        Directory.CreateDirectory(sharedDir);
        File.WriteAllText(Path.Combine(sharedDir, "sems-projects.json"),
            JsonSerializer.Serialize(new
            {
                Projects = new[] { new ProjectRecord { Root = CreateTempProjectDir("should-not-import"), Name = "X" } }
            }));

        // 触发 EnsureMigrated（GetAll）
        var all = registry.GetAll();
        Assert.Single(all);
        Assert.DoesNotContain(all, p => p.Name == "X");
    }

    [Fact]
    public void Migration_Idempotent_AcrossMultipleInstances_DoesNotDuplicate()
    {
        // 写入旧共享 JSON（2 条记录）。
        var sharedDir = Path.Combine(_hostDataDir, "Shared");
        Directory.CreateDirectory(sharedDir);
        File.WriteAllText(Path.Combine(sharedDir, "sems-projects.json"),
            JsonSerializer.Serialize(new
            {
                Projects = new[]
                {
                    new ProjectRecord { Root = CreateTempProjectDir("idem-a"), Name = "IdemA" },
                    new ProjectRecord { Root = CreateTempProjectDir("idem-b"), Name = "IdemB" }
                }
            }));

        // 两个独立实例（各自 _migrated 标志独立）共享同一 SQLite 库与 legacy JSON。
        // 第一个实例 GetAll 触发迁移导入 2 条；第二个实例再次 GetAll 时表已非空 → 跳过导入。
        var first = Create();
        Assert.Equal(2, first.GetAll().Count);

        var second = Create();
        Assert.Equal(2, second.GetAll().Count); // 不重复导入，仍为 2 条

        // 落到库里确实只有 2 条（无重复 Root）
        var all = second.GetAll();
        Assert.Equal(2, all.Count);
        Assert.Equal(2, all.Select(p => p.Root).Distinct().Count());
    }

    private string CreateTempProjectDir(string name)
    {
        var path = Path.Combine(_dbDir, "projects", name);
        Directory.CreateDirectory(path);
        return path;
    }
}
