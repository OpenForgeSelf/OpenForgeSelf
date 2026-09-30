using System.IO;
using System.Linq;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Plugins.Sems.Services;
using ForgeSelf.Api.Services;
using ForgeSelf.Core;
using Moq;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.Sems;

/// <summary>
/// <see cref="ProjectService"/> 行为测试：sems 内部登记 / 档案编辑 / 移除（含存活会话守卫与级联）/
/// 命令 CRUD 校验 / 目录浏览 / 接缝缺席降级 / 列表附带命令概要。
/// 用真实 <see cref="HostProjectRegistry"/> + 临时 SQLite 库（零 mock 数据层），
/// 进程存活判定用 Mock <see cref="IRunnerService"/>（确定性，不真起进程）。
/// 测试目录<b>只创建不删除</b>（技能铁律 10）。
/// </summary>
[Collection("XCode")]
public class ProjectServiceTests
{
    private readonly string _dbDir;
    private readonly HostProjectRegistry _registry;
    private readonly Mock<IContext> _ctx = new();
    private readonly Mock<IRunnerService> _runner = new();

    public ProjectServiceTests()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfSemsSvc_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dbDir);

        var hostDataDir = Path.Combine(_dbDir, "hostdata");
        Directory.CreateDirectory(hostDataDir);

        DAL.AddConnStr("ForgeSelf", $"Data Source={Path.Combine(_dbDir, "ForgeSelf.db")}", null, "SQLite");
        EntityFactory.InitConnection("ForgeSelf");
        Project.Meta.Cache.Clear("test reset");
        RunCommand.Meta.Cache.Clear("test reset");
        Project.Meta.Cache.Expire = 0;
        RunCommand.Meta.Cache.Expire = 0;

        _registry = new HostProjectRegistry(hostDataDir);
        _ctx.Setup(c => c.Get<IProjectRegistry>()).Returns(_registry);
        _runner.Setup(r => r.Current()).Returns(new List<RunSession>());
    }

    private ProjectService CreateService() => new(_ctx.Object, _runner.Object);

    /// <summary>接缝缺席时的服务（模拟宿主提供方被摘除）。</summary>
    private ProjectService CreateServiceWithoutRegistry() => new(new Mock<IContext>().Object, _runner.Object);

    private string CreateProjectDir(string name)
    {
        var path = Path.Combine(_dbDir, "projects", name);
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public void Register_WritesManualSource_AndReturnsProject()
    {
        var dir = CreateProjectDir("alpha");

        var result = CreateService().Register(dir);

        Assert.True(result.Success);
        Assert.Equal(SemsOpStatus.Ok, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(dir, result.Data!.Root);
        Assert.Equal("manual", result.Data.Source);
        Assert.Equal("alpha", result.Data.Name);
    }

    [Fact]
    public void Register_WithNameOverride_UsesGivenName()
    {
        var dir = CreateProjectDir("beta");

        var result = CreateService().Register(dir, "我的服务");

        Assert.True(result.Success);
        Assert.Equal("我的服务", result.Data!.Name);
        Assert.Equal("manual", result.Data.Source);
    }

    [Fact]
    public void Register_MissingDirectory_ReturnsInvalid_WithReason()
    {
        var result = CreateService().Register(Path.Combine(_dbDir, "not-here"));

        Assert.False(result.Success);
        Assert.Equal(SemsOpStatus.Invalid, result.Status);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("目录不存在", result.Error);
        Assert.Empty(_registry.GetAll());
    }

    [Fact]
    public void Register_SameTwice_DoesNotDuplicate_AndKeepsEditedName()
    {
        var dir = CreateProjectDir("dup");
        var svc = CreateService();
        var first = svc.Register(dir);
        Assert.True(first.Success);
        Assert.True(svc.UpdateProject(first.Data!.Id, new ProjectUpdate { Name = "改名后" }).Success);

        var second = svc.Register(dir);

        Assert.True(second.Success);
        Assert.Equal(first.Data!.Id, second.Data!.Id);
        Assert.Equal("改名后", second.Data.Name);
        Assert.Single(svc.GetProjects());
    }

    [Fact]
    public void GetProjects_FillsCommands_ForEveryProject()
    {
        // 回归守卫：宿主 GetAll() 不附带 Commands，服务层必须补齐 ——
        // 否则首页「运行命令」统计恒 0、快捷访问 url 为空、「启动全部」遍历空列表。
        var dirA = CreateProjectDir("cmd-a");
        var dirB = CreateProjectDir("cmd-b");
        var svc = CreateService();
        var a = svc.Register(dirA).Data!;
        var b = svc.Register(dirB).Data!;
        svc.AddCommand(a.Id, new RunCommandInfo { Name = "dev", Script = "echo a" });
        svc.AddCommand(a.Id, new RunCommandInfo { Name = "build", Script = "echo b" });
        svc.AddCommand(b.Id, new RunCommandInfo { Name = "serve", Script = "echo c" });

        var projects = svc.GetProjects();

        Assert.Equal(2, projects.Count);
        Assert.Equal(2, projects.Single(p => p.Id == a.Id).Commands.Count);
        Assert.Single(projects.Single(p => p.Id == b.Id).Commands);
    }

    [Fact]
    public void UpdateProject_UnknownId_ReturnsNotFound()
    {
        var result = CreateService().UpdateProject(424242, new ProjectUpdate { Name = "x" });

        Assert.Equal(SemsOpStatus.NotFound, result.Status);
        Assert.Equal(404, result.StatusCode);
        Assert.Contains("项目不存在", result.Error);
    }

    [Fact]
    public void RemoveProject_DeletesProjectAndCommands_KeepsDirectoryOnDisk()
    {
        var dir = CreateProjectDir("drop");
        var svc = CreateService();
        var project = svc.Register(dir).Data!;
        svc.AddCommand(project.Id, new RunCommandInfo { Name = "dev", Script = "echo" });

        var result = svc.RemoveProject(project.Id);

        Assert.True(result.Success);
        Assert.Null(_registry.Get(project.Id));
        Assert.Empty(_registry.GetCommands(project.Id));
        Assert.True(Directory.Exists(dir)); // 只删档案，磁盘绝不能被触碰
    }

    [Fact]
    public void RemoveProject_WithLiveSession_ReturnsConflict_AndChangesNothing()
    {
        var dir = CreateProjectDir("busy");
        var svc = CreateService();
        var project = svc.Register(dir).Data!;
        var commandId = svc.AddCommand(project.Id, new RunCommandInfo { Name = "dev", Script = "echo" }).Data;
        _runner.Setup(r => r.Current()).Returns(new List<RunSession>
        {
            new() { CommandId = commandId, ProjectId = project.Id, Pid = 4321, Origin = "Launched" }
        });

        var result = svc.RemoveProject(project.Id);

        Assert.False(result.Success);
        Assert.Equal(SemsOpStatus.Conflict, result.Status);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains("请先停止", result.Error);
        // 被拒时必须零数据变更
        Assert.NotNull(_registry.Get(project.Id));
        Assert.Single(_registry.GetCommands(project.Id));
    }

    [Fact]
    public void RemoveProject_UnknownId_ReturnsNotFound()
    {
        var result = CreateService().RemoveProject(909090);

        Assert.Equal(SemsOpStatus.NotFound, result.Status);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public void Commands_Add_Validates_Name_And_Script()
    {
        var dir = CreateProjectDir("cmds");
        var svc = CreateService();
        var projectId = svc.Register(dir).Data!.Id;

        Assert.Equal(SemsOpStatus.Invalid, svc.AddCommand(projectId, new RunCommandInfo { Name = " ", Script = "echo" }).Status);
        Assert.Equal(SemsOpStatus.Invalid, svc.AddCommand(projectId, new RunCommandInfo { Name = "x", Script = "" }).Status);
        Assert.Equal(SemsOpStatus.NotFound, svc.AddCommand(999999, new RunCommandInfo { Name = "x", Script = "echo" }).Status);

        var ok = svc.AddCommand(projectId, new RunCommandInfo { Name = "dev", Script = "echo dev" });
        Assert.True(ok.Success);
        Assert.True(ok.Data > 0);
    }

    [Fact]
    public void Commands_Update_And_Delete_UnknownId_ReturnsNotFound()
    {
        var result = CreateService().UpdateCommand(707070, new RunCommandUpdate { Name = "x" });
        Assert.Equal(SemsOpStatus.NotFound, result.Status);
        Assert.Equal(404, result.StatusCode);

        var deleted = CreateService().DeleteCommand(707070);
        Assert.Equal(SemsOpStatus.NotFound, deleted.Status);
    }

    [Fact]
    public void Commands_RoundTrip_List_Update_Delete()
    {
        var dir = CreateProjectDir("cmdrt");
        var svc = CreateService();
        var projectId = svc.Register(dir).Data!.Id;
        var commandId = svc.AddCommand(projectId, new RunCommandInfo
        {
            Name = "dev",
            Script = "pnpm dev",
            Url = "http://localhost:7002",
            Sort = 5
        }).Data;

        var listed = svc.GetCommands(projectId);
        Assert.True(listed.Success);
        var command = Assert.Single(listed.Data!);
        Assert.Equal("dev", command.Name);
        Assert.Equal("http://localhost:7002", command.Url);
        Assert.Equal(5, command.Sort);

        Assert.True(svc.UpdateCommand(commandId, new RunCommandUpdate { Name = "dev2", Sort = 1 }).Success);
        Assert.Equal("dev2", Assert.Single(svc.GetCommands(projectId).Data!).Name);

        Assert.True(svc.DeleteCommand(commandId).Success);
        Assert.Empty(svc.GetCommands(projectId).Data!);
    }

    [Fact]
    public void GetCommands_UnknownProject_ReturnsNotFound()
    {
        var result = CreateService().GetCommands(303030);

        Assert.Equal(SemsOpStatus.NotFound, result.Status);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public void Browse_WithoutPath_ListsDrives()
    {
        var result = CreateService().Browse(null);

        Assert.True(result.Success);
        Assert.Null(result.Data!.Path);
        // 本机至少有一个可枚举驱动器；条目必须是盘符形式
        Assert.NotEmpty(result.Data.Directories);
        Assert.All(result.Data.Directories, d => Assert.EndsWith(@":\", d.Path));
    }

    [Fact]
    public void Browse_WithDirectory_ListsChildDirectoriesOnly_WithParent()
    {
        var parentDir = CreateProjectDir("browse");
        Directory.CreateDirectory(Path.Combine(parentDir, "child-one"));
        Directory.CreateDirectory(Path.Combine(parentDir, "child-two"));
        File.WriteAllText(Path.Combine(parentDir, "a-file.txt"), "not a directory");

        var result = CreateService().Browse(parentDir);

        Assert.True(result.Success);
        Assert.Equal(parentDir, result.Data!.Path);
        Assert.Equal(Path.GetDirectoryName(parentDir), result.Data.Parent);
        var names = result.Data.Directories.Select(d => d.Name).ToList();
        Assert.Contains("child-one", names);
        Assert.Contains("child-two", names);
        // 只列目录，绝不把文件列进来
        Assert.DoesNotContain("a-file.txt", names);
        Assert.All(result.Data.Directories, d => Assert.True(Directory.Exists(d.Path)));
    }

    [Fact]
    public void Browse_MissingDirectory_ReturnsInvalid()
    {
        var result = CreateService().Browse(Path.Combine(_dbDir, "no-such-folder"));

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("目录不存在", result.Error);
    }

    [Fact]
    public void RegistryUnavailable_Degrades_WithoutThrowing()
    {
        var svc = CreateServiceWithoutRegistry();

        Assert.False(svc.IsAvailable);
        Assert.Empty(svc.GetProjects());
        Assert.Equal(0, svc.Count);

        var register = svc.Register(CreateProjectDir("deg"));
        Assert.Equal(SemsOpStatus.Unavailable, register.Status);
        Assert.Equal(503, register.StatusCode);

        Assert.Equal(SemsOpStatus.Unavailable, svc.UpdateProject(1, new ProjectUpdate { Name = "x" }).Status);
        Assert.Equal(SemsOpStatus.Unavailable, svc.RemoveProject(1).Status);
        Assert.Equal(SemsOpStatus.Unavailable, svc.GetCommands(1).Status);
        Assert.Equal(SemsOpStatus.Unavailable, svc.AddCommand(1, new RunCommandInfo { Name = "n", Script = "s" }).Status);
        Assert.Equal(SemsOpStatus.Unavailable, svc.UpdateCommand(1, new RunCommandUpdate { Name = "x" }).Status);
        Assert.Equal(SemsOpStatus.Unavailable, svc.DeleteCommand(1).Status);

        // 目录浏览不依赖宿主接缝，接缝缺席时仍然可用（插件内部自洽）
        var browse = svc.Browse(null);
        Assert.True(browse.Success);
    }

    [Fact]
    public void IsAvailable_Reflects_SeamPresence()
    {
        Assert.True(CreateService().IsAvailable);
        Assert.False(CreateServiceWithoutRegistry().IsAvailable);
    }
}
