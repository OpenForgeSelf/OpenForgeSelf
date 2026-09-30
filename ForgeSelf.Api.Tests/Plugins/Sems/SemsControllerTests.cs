using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Plugins.Sems.Controllers;
using ForgeSelf.Api.Plugins.Sems.Services;
using ForgeSelf.Api.Services;
using ForgeSelf.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.Sems;

/// <summary>
/// sems 控制器测试：新增三端点（POST/DELETE api/projects、GET api/projects/browse）的状态码矩阵，
/// 既有端点响应形状不变，以及<b>管理面鉴权铁律</b>（技能铁律 17）——
/// Plugins/Sems/Controllers 下每个控制器类必须带类级 <c>[Authorize("ApiKeyPolicy")]</c>（反射 Theory，仿 McpAdminAuthTests）。
/// 控制器直接实例化 + 真实 <see cref="HostProjectRegistry"/>（临时 SQLite），零 mock 数据层；测试目录只创建不删除。
/// </summary>
[Collection("XCode")]
public class SemsControllerTests
{
    private readonly string _dbDir;
    private readonly HostProjectRegistry _registry;
    private readonly Mock<IRunnerService> _runner = new();
    private readonly ProjectsController _projects;
    private readonly ProjectCommandsController _commands;
    private readonly RunsController _runs;

    public SemsControllerTests()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfSemsCtl_{Guid.NewGuid():N}");
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
        var ctx = new Mock<IContext>();
        ctx.Setup(c => c.Get<IProjectRegistry>()).Returns(_registry);
        _runner.Setup(r => r.Current()).Returns(new List<RunSession>());

        var service = new ProjectService(ctx.Object, _runner.Object);
        _projects = new ProjectsController(service);
        _commands = new ProjectCommandsController(service, _runner.Object);
        _runs = new RunsController(_runner.Object);
    }

    private string CreateProjectDir(string name)
    {
        var path = Path.Combine(_dbDir, "projects", name);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>把 ActionResult&lt;object&gt; 解成 (状态码, JSON 体)。</summary>
    private static (int Status, JsonElement Body) Decode(IActionResult result)
    {
        var (status, value) = result switch
        {
            OkObjectResult ok => (ok.StatusCode ?? 200, ok.Value),
            ObjectResult obj => (obj.StatusCode ?? 500, obj.Value),
            _ => throw new InvalidOperationException($"未预期的结果类型：{result.GetType().Name}")
        };
        var body = JsonSerializer.SerializeToElement(value ?? new { }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return (status, body);
    }

    private static int StatusOf(ActionResult<object> action) => Decode(action.Result!).Status;

    /// <summary>经接口登记一个项目，返回其 id（断言 200）。</summary>
    private int CreateProject(string name)
    {
        var dir = CreateProjectDir(name);
        StatusOf(_projects.Register(new ProjectRegisterRequest { Root = dir })).Should().Be(200);
        return _registry.GetAll().Single().Id;
    }

    /// <summary>经接口新增一条命令，返回其 id（断言 200）。</summary>
    private int CreateCommand(int projectId, string name = "dev", string script = "echo")
    {
        var added = Decode(_commands.AddCommand(projectId, new RunCommandInfo { Name = name, Script = script }).Result!);
        added.Status.Should().Be(200);
        return added.Body.GetProperty("id").GetInt32();
    }

    [Fact]
    public void Every_Sems_Controller_Class_Carries_ApiKeyPolicy_Authorization()
    {
        var controllers = typeof(ProjectsController).Assembly
            .GetTypes()
            .Where(t => t.Namespace == "ForgeSelf.Api.Plugins.Sems.Controllers"
                        && t.IsClass && !t.IsAbstract
                        && typeof(ControllerBase).IsAssignableFrom(t))
            .ToList();

        // 冗余 RunnerController 已移除（028 已知问题 #1）→ 现存三个控制器
        controllers.Should().HaveCount(3);
        foreach (var controller in controllers)
        {
            var auth = controller.GetCustomAttribute<AuthorizeAttribute>();
            auth.Should().NotBeNull($"{controller.Name} 必须带类级 [Authorize]");
            auth!.Policy.Should().Be("ApiKeyPolicy", $"{controller.Name} 必须挂 ApiKeyPolicy");
        }
    }

    [Fact]
    public void Redundant_RunnerController_Type_Is_Gone()
    {
        // api/runner/* 与 api/runs + api/commands 完全重复且零引用，已收敛（028 已知问题 #1）
        typeof(ProjectsController).Assembly
            .GetType("ForgeSelf.Api.Plugins.Sems.Controllers.RunnerController")
            .Should().BeNull("RunnerController 应已移出插件源码");
    }

    [Fact]
    public void Register_Endpoint_Creates_Manual_Project_And_Returns_It()
    {
        var dir = CreateProjectDir("ctl-add");

        var action = _projects.Register(new ProjectRegisterRequest { Root = dir });

        action.Result.Should().BeOfType<OkObjectResult>();
        var (status, body) = Decode(action.Result!);
        status.Should().Be(200);
        body.GetProperty("success").GetBoolean().Should().BeTrue();
        body.GetProperty("project").GetProperty("source").GetString().Should().Be("manual");
        body.GetProperty("project").GetProperty("root").GetString().Should().Be(dir);
    }

    [Fact]
    public void Register_Endpoint_Missing_Directory_Returns_400_With_Message()
    {
        var action = _projects.Register(new ProjectRegisterRequest { Root = Path.Combine(_dbDir, "ghost") });

        var (status, body) = Decode(action.Result!);
        status.Should().Be(400);
        body.GetProperty("success").GetBoolean().Should().BeFalse();
        body.GetProperty("message").GetString().Should().Contain("目录不存在");
    }

    [Fact]
    public void Register_Endpoint_Null_Body_Returns_400()
    {
        var action = _projects.Register(null!);
        Decode(action.Result!).Status.Should().Be(400);
    }

    [Fact]
    public void Remove_Endpoint_Deletes_Project_Then_List_Drops_It()
    {
        var dir = CreateProjectDir("ctl-del");
        StatusOf(_projects.Register(new ProjectRegisterRequest { Root = dir })).Should().Be(200);
        var id = _registry.GetAll().Single().Id;

        StatusOf(_projects.Remove(id)).Should().Be(200);

        Assert.Empty(_registry.GetAll());
        // 只删档案：磁盘目录必须原样存在（技能铁律 10）
        Assert.True(Directory.Exists(dir));
    }

    [Fact]
    public void Remove_Endpoint_Unknown_Id_Returns_404()
    {
        var (status, body) = Decode(_projects.Remove(424242).Result!);

        status.Should().Be(404);
        body.GetProperty("message").GetString().Should().Contain("项目不存在");
    }

    [Fact]
    public void Remove_Endpoint_With_Live_Session_Returns_409_And_Keeps_Data()
    {
        var dir = CreateProjectDir("ctl-busy");
        _projects.Register(new ProjectRegisterRequest { Root = dir });
        var projectId = _registry.GetAll().Single().Id;
        _runner.Setup(r => r.Current()).Returns(new List<RunSession>
        {
            new() { CommandId = 1, ProjectId = projectId, Pid = 7777, Origin = "Launched" }
        });

        var (status, body) = Decode(_projects.Remove(projectId).Result!);

        status.Should().Be(409);
        body.GetProperty("message").GetString().Should().Contain("请先停止");
        Assert.NotNull(_registry.Get(projectId));
    }

    [Fact]
    public void Browse_Endpoint_Lists_Child_Directories_Only()
    {
        var dir = CreateProjectDir("ctl-browse");
        Directory.CreateDirectory(Path.Combine(dir, "sub-one"));
        File.WriteAllText(Path.Combine(dir, "note.txt"), "x");

        var action = _projects.Browse(dir);

        var (status, body) = Decode(action.Result!);
        status.Should().Be(200);
        body.GetProperty("success").GetBoolean().Should().BeTrue();
        body.GetProperty("path").GetString().Should().Be(dir);
        var names = body.GetProperty("directories").EnumerateArray()
            .Select(d => d.GetProperty("name").GetString()).ToList();
        names.Should().Contain("sub-one");
        names.Should().NotContain("note.txt");
    }

    [Fact]
    public void Browse_Endpoint_Without_Path_Lists_Drives()
    {
        var (status, body) = Decode(_projects.Browse(null).Result!);

        status.Should().Be(200);
        body.GetProperty("path").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("directories").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public void Browse_Endpoint_Missing_Directory_Returns_400()
    {
        var (status, body) = Decode(_projects.Browse(Path.Combine(_dbDir, "nope")).Result!);

        status.Should().Be(400);
        body.GetProperty("message").GetString().Should().Contain("目录不存在");
    }

    [Fact]
    public void Get_List_Includes_Commands_Per_Project()
    {
        // 回归守卫：文档口径「列表含每项目命令概要」必须成立（曾恒为空 → 统计/快捷访问/启动全部静默失效）
        var dir = CreateProjectDir("ctl-list");
        _projects.Register(new ProjectRegisterRequest { Root = dir });
        var projectId = _registry.GetAll().Single().Id;
        _commands.AddCommand(projectId, new RunCommandInfo { Name = "dev", Script = "echo dev" });

        var (status, body) = Decode(_projects.GetAll().Result!);

        status.Should().Be(200);
        body.GetProperty("total").GetInt32().Should().Be(1);
        var project = body.GetProperty("projects")[0];
        project.GetProperty("commands").GetArrayLength().Should().Be(1);
        project.GetProperty("commands")[0].GetProperty("name").GetString().Should().Be("dev");

        var countBody = Decode(_projects.GetCount().Result!).Body;
        countBody.GetProperty("total").GetInt32().Should().Be(1);
    }

    [Fact]
    public void Commands_Endpoints_Keep_Existing_Response_Shapes()
    {
        var dir = CreateProjectDir("ctl-cmds");
        _projects.Register(new ProjectRegisterRequest { Root = dir });
        var projectId = _registry.GetAll().Single().Id;

        var added = Decode(_commands.AddCommand(projectId, new RunCommandInfo { Name = "dev", Script = "echo" }).Result!);
        added.Status.Should().Be(200);
        added.Body.GetProperty("id").GetInt32().Should().BeGreaterThan(0);
        var commandId = added.Body.GetProperty("id").GetInt32();

        var listed = Decode(_commands.GetCommands(projectId).Result!);
        listed.Status.Should().Be(200);
        listed.Body.GetProperty("commands").GetArrayLength().Should().Be(1);

        var updated = Decode(_commands.UpdateCommand(commandId, new RunCommandUpdate { Name = "dev2" }).Result!);
        updated.Status.Should().Be(200);

        var deleted = Decode(_commands.DeleteCommand(commandId).Result!);
        deleted.Status.Should().Be(200);

        // 既有校验语义保持：空名/空脚本 → 400；项目不存在 → 404
        Decode(_commands.AddCommand(projectId, new RunCommandInfo { Name = " ", Script = "echo" }).Result!)
            .Status.Should().Be(400);
        Decode(_commands.AddCommand(987654, new RunCommandInfo { Name = "x", Script = "echo" }).Result!)
            .Status.Should().Be(404);
        Decode(_commands.GetCommands(987654).Result!).Status.Should().Be(404);
    }

    [Fact]
    public void Run_Endpoint_Conflicts_When_Launch_Returns_Null()
    {
        _runner.Setup(r => r.Launch(9)).Returns((RunSession?)null);

        var (status, body) = Decode(_commands.Run(9).Result!);

        status.Should().Be(409);
        body.GetProperty("success").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void Run_And_Stop_Endpoints_On_Launched_Session()
    {
        var projectId = CreateProject("ctl-run");
        var commandId = CreateCommand(projectId, "dev");

        _runner.Setup(r => r.Launch(commandId)).Returns(new RunSession
        {
            CommandId = commandId, ProjectId = projectId, CommandName = "dev", Pid = 5150, Origin = "Launched"
        });
        var ran = Decode(_commands.Run(commandId).Result!);
        ran.Status.Should().Be(200);
        ran.Body.GetProperty("session").GetProperty("pid").GetInt32().Should().Be(5150);

        _runner.Setup(r => r.Stop(commandId)).Returns(true);
        StatusOf(_commands.Stop(commandId)).Should().Be(200);

        _runner.Setup(r => r.Stop(commandId)).Returns(false);
        StatusOf(_commands.Stop(commandId)).Should().Be(404);

        _runner.Setup(r => r.Current()).Returns(new List<RunSession>());
        StatusOf(_runs.ListRuns()).Should().Be(200);

        _runner.Setup(r => r.CheckAndMerge()).Returns(new List<RunSession>());
        var checkedRuns = Decode(_runs.Check().Result!);
        checkedRuns.Status.Should().Be(200);
        checkedRuns.Body.GetProperty("total").GetInt32().Should().Be(0);

        _runner.Setup(r => r.StopExternal(4321)).Returns(true);
        StatusOf(_runs.StopExternal(4321)).Should().Be(200);
        _runner.Setup(r => r.StopExternal(4321)).Returns(false);
        StatusOf(_runs.StopExternal(4321)).Should().Be(404);
    }

    [Fact]
    public void All_Endpoints_Return_503_When_Seam_Unavailable()
    {
        var emptyCtx = new Mock<IContext>();
        var service = new ProjectService(emptyCtx.Object, _runner.Object);
        var controller = new ProjectsController(service);
        var commandController = new ProjectCommandsController(service, _runner.Object);

        Decode(controller.Register(new ProjectRegisterRequest { Root = CreateProjectDir("deg") }).Result!)
            .Status.Should().Be(503);
        Decode(controller.Update(1, new ProjectUpdate { Name = "x" }).Result!).Status.Should().Be(503);
        Decode(controller.Remove(1).Result!).Status.Should().Be(503);
        Decode(commandController.GetCommands(1).Result!).Status.Should().Be(503);
        Decode(commandController.AddCommand(1, new RunCommandInfo { Name = "n", Script = "s" }).Result!)
            .Status.Should().Be(503);
        Decode(commandController.UpdateCommand(1, new RunCommandUpdate { Name = "n" }).Result!).Status.Should().Be(503);
        Decode(commandController.DeleteCommand(1).Result!).Status.Should().Be(503);

        // 列表与计数走降级（空数据 200），browse 不依赖接缝仍 200
        var list = Decode(controller.GetAll().Result!);
        list.Status.Should().Be(200);
        list.Body.GetProperty("total").GetInt32().Should().Be(0);
        Decode(controller.Browse(null).Result!).Status.Should().Be(200);
    }
}
