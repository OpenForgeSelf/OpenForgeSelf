using System.IO;
using System.Linq;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Plugins.Sems;
using ForgeSelf.Api.Plugins.Sems.Services;
using ForgeSelf.Api.Services;
using ForgeSelf.Core;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.Sems;

/// <summary>
/// sems 对外工具（13 个 <c>sems_*</c>）测试：命名唯一性与前缀约束、JSON Schema 合法性与 required 生效、
/// Description 可发现性（铁律 18）、经宿主 <see cref="ToolRegistry"/> 注册后可被枚举/调用、
/// 以及 executeAsync 端到端往返（登记→列→加命令→改→删→移除）。
/// 数据层用真实 <see cref="HostProjectRegistry"/> + 临时 SQLite（零 mock），进程能力用 Mock
/// <see cref="IRunnerService"/>（确定性）；测试目录<b>只创建不删除</b>（技能铁律 10）。
/// </summary>
[Collection("XCode")]
public class SemsToolExtensionTests
{
    /// <summary>spec FR-D1 约定的 13 个工具名（顺序无关，集合必须精确相等）。</summary>
    private static readonly string[] ExpectedTools =
    {
        "sems_list_projects", "sems_register_project", "sems_update_project", "sems_remove_project",
        "sems_list_commands", "sems_add_command", "sems_update_command", "sems_delete_command",
        "sems_list_runs", "sems_check_runs", "sems_run_command", "sems_stop_command", "sems_stop_run"
    };

    private static readonly string[] DestructiveTools =
    {
        "sems_remove_project", "sems_delete_command", "sems_stop_command", "sems_stop_run"
    };

    private readonly string _dbDir;
    private readonly HostProjectRegistry _registry;
    private readonly Mock<IRunnerService> _runner = new();
    private readonly IServiceProvider _services;
    private readonly List<IToolFunctionExtension> _tools;

    public SemsToolExtensionTests()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfSemsTool_{Guid.NewGuid():N}");
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

        var sc = new ServiceCollection();
        sc.AddSingleton<IProjectService>(new ProjectService(ctx.Object, _runner.Object));
        sc.AddSingleton<IRunnerService>(_runner.Object);
        _services = sc.BuildServiceProvider();

        _tools = BuildTools(_services);
    }

    private static List<IToolFunctionExtension> BuildTools(IServiceProvider services, string pluginId = "sems") => new()
    {
        new SemsListProjectsToolFunction(pluginId, services),
        new SemsRegisterProjectToolFunction(pluginId, services),
        new SemsUpdateProjectToolFunction(pluginId, services),
        new SemsRemoveProjectToolFunction(pluginId, services),
        new SemsListCommandsToolFunction(pluginId, services),
        new SemsAddCommandToolFunction(pluginId, services),
        new SemsUpdateCommandToolFunction(pluginId, services),
        new SemsDeleteCommandToolFunction(pluginId, services),
        new SemsListRunsToolFunction(pluginId, services),
        new SemsCheckRunsToolFunction(pluginId, services),
        new SemsRunCommandToolFunction(pluginId, services),
        new SemsStopCommandToolFunction(pluginId, services),
        new SemsStopRunToolFunction(pluginId, services)
    };

    private IToolFunctionExtension Tool(string name) =>
        _tools.Single(t => t.Name == name);

    private static JsonElement Payload(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static bool IsSuccess(string json) => Payload(json).GetProperty("success").GetBoolean();

    private static string? ErrorOf(string json) =>
        Payload(json).TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;

    private string CreateProjectDir(string name)
    {
        var path = Path.Combine(_dbDir, "projects", name);
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public void Tool_Set_Is_Exactly_The_Thirteen_Expected()
    {
        Assert.Equal(13, _tools.Count);
        Assert.Equal(ExpectedTools.OrderBy(n => n), _tools.Select(t => t.Name).OrderBy(n => n));
        Assert.All(_tools, t => Assert.StartsWith("sems_", t.Name));
        Assert.All(_tools, t => Assert.Equal("sems", t.PluginId));
        // Id 必须插件内前缀化且全局唯一（UnregisterTool 按 Id 摘除）
        Assert.Equal(13, _tools.Select(t => t.Id).Distinct().Count());
        Assert.All(_tools, t => Assert.StartsWith("sems.tool.", t.Id));
    }

    [Fact]
    public void Every_Schema_Is_Valid_Object_With_Properties_And_Subset_Required()
    {
        foreach (var tool in _tools)
        {
            using var doc = JsonDocument.Parse(tool.ParametersJsonSchema);
            var root = doc.RootElement;
            Assert.Equal("object", root.GetProperty("type").GetString());
            Assert.True(root.TryGetProperty("properties", out var props), $"{tool.Name} 缺 properties");
            Assert.Equal(JsonValueKind.Object, props.ValueKind);

            if (root.TryGetProperty("required", out var required))
            {
                foreach (var r in required.EnumerateArray())
                {
                    var name = r.GetString();
                    Assert.False(string.IsNullOrEmpty(name));
                    Assert.True(props.TryGetProperty(name!, out _), $"{tool.Name} 的 required 项 {name} 不在 properties 中");
                }
            }
        }
    }

    [Fact]
    public void Registry_Accepts_All_Tools_And_Exposes_Definitions()
    {
        var registry = new ToolRegistry();
        foreach (var tool in _tools) registry.RegisterTool(tool);

        Assert.Equal(13, registry.GetAllTools().Count());
        // GetToolDefinitions 会解析 schema；schema 非法的工具在这里会被静默丢弃 → 必须仍是 13
        Assert.Equal(13, registry.GetToolDefinitions().Count);
        Assert.NotNull(registry.GetTool("sems_register_project"));
    }

    [Fact]
    public void ValidateParameters_Rejects_Missing_Required_For_Every_Tool_That_Requires()
    {
        var registry = new ToolRegistry();
        foreach (var tool in _tools) registry.RegisterTool(tool);

        var withRequired = _tools.Where(t =>
            JsonDocument.Parse(t.ParametersJsonSchema).RootElement.TryGetProperty("required", out _)).ToList();
        Assert.NotEmpty(withRequired);

        foreach (var tool in withRequired)
        {
            var result = registry.ValidateParameters(tool.Name, "{}");
            Assert.False(result.IsValid, $"{tool.Name} 空参数应被 required 拒绝");
            Assert.Contains(result.Errors, e => e.Contains("缺少必填参数"));
        }

        // 无 required 的工具（list_projects / list_runs / check_runs）空参数必须放行
        foreach (var tool in _tools.Except(withRequired))
            Assert.True(registry.ValidateParameters(tool.Name, "{}").IsValid, tool.Name);
    }

    [Fact]
    public void Descriptions_Guided_Discovery_And_Id_Provenance()
    {
        foreach (var tool in _tools)
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.Description));
            // 铁律 18：外部调用方据说明即可发现全部工具
            Assert.Contains("list_tools", tool.Description);
        }

        // 需要 id 的工具必须写明 id 来源
        Assert.Contains("sems_list_projects", Tool("sems_update_project").Description);
        Assert.Contains("sems_list_projects", Tool("sems_remove_project").Description);
        Assert.Contains("sems_list_commands", Tool("sems_update_command").Description);
        Assert.Contains("sems_list_commands", Tool("sems_run_command").Description);
        Assert.Contains("sems_check_runs", Tool("sems_stop_run").Description);

        // 破坏性工具必须明示不可恢复
        foreach (var name in DestructiveTools)
            Assert.Contains("不可恢复", Tool(name).Description);

        // 不得提供「执行任意脚本」入口：启动工具只接受已登记的 commandId
        Assert.Contains("不接受任意脚本", Tool("sems_run_command").Description);
        using var runSchema = JsonDocument.Parse(Tool("sems_run_command").ParametersJsonSchema);
        Assert.False(runSchema.RootElement.GetProperty("properties").TryGetProperty("script", out _));
    }

    [Fact]
    public async Task RoundTrip_Register_List_Commands_Add_Update_Run_Stop_Delete_Remove()
    {
        var dir = CreateProjectDir("toolrt");

        var registered = await Tool("sems_register_project").ExecuteAsync(JsonSerializer.Serialize(new { root = dir }));
        Assert.True(IsSuccess(registered));
        var projectId = Payload(registered).GetProperty("data").GetProperty("project").GetProperty("id").GetInt32();
        Assert.Equal("manual", Payload(registered).GetProperty("data").GetProperty("project").GetProperty("source").GetString());

        var listed = await Tool("sems_list_projects").ExecuteAsync("{}");
        Assert.True(IsSuccess(listed));
        var data = Payload(listed).GetProperty("data");
        Assert.Equal(1, data.GetProperty("total").GetInt32());
        Assert.Equal(dir, data.GetProperty("projects")[0].GetProperty("root").GetString());

        var added = await Tool("sems_add_command").ExecuteAsync(
            JsonSerializer.Serialize(new { projectId, name = "dev", script = "echo dev", url = "http://localhost:1", sort = 2 }));
        Assert.True(IsSuccess(added));
        var commandId = Payload(added).GetProperty("data").GetProperty("commandId").GetInt32();

        var commands = await Tool("sems_list_commands").ExecuteAsync(
            JsonSerializer.Serialize(new { projectId }));
        Assert.True(IsSuccess(commands));
        Assert.Equal(commandId, Payload(commands).GetProperty("data").GetProperty("commands")[0].GetProperty("id").GetInt32());

        var updated = await Tool("sems_update_command").ExecuteAsync(
            JsonSerializer.Serialize(new { commandId, name = "dev2" }));
        Assert.True(IsSuccess(updated));

        var projUpdated = await Tool("sems_update_project").ExecuteAsync(
            JsonSerializer.Serialize(new { id = projectId, type = "frontend", tags = "a,b" }));
        Assert.True(IsSuccess(projUpdated));

        _runner.Setup(r => r.Launch(commandId)).Returns(new RunSession
        {
            CommandId = commandId, ProjectId = projectId, ProjectName = "toolrt",
            CommandName = "dev2", Pid = 4242, Origin = "Launched"
        });
        var ran = await Tool("sems_run_command").ExecuteAsync(
            JsonSerializer.Serialize(new { commandId }));
        Assert.True(IsSuccess(ran));
        Assert.Equal(4242, Payload(ran).GetProperty("data").GetProperty("session").GetProperty("pid").GetInt32());

        _runner.Setup(r => r.Stop(commandId)).Returns(true);
        var stopped = await Tool("sems_stop_command").ExecuteAsync(
            JsonSerializer.Serialize(new { commandId }));
        Assert.True(IsSuccess(stopped));

        var cmdDeleted = await Tool("sems_delete_command").ExecuteAsync(
            JsonSerializer.Serialize(new { commandId }));
        Assert.True(IsSuccess(cmdDeleted));

        var removed = await Tool("sems_remove_project").ExecuteAsync(
            JsonSerializer.Serialize(new { id = projectId }));
        Assert.True(IsSuccess(removed));
        Assert.Empty(_registry.GetAll());
        // 磁盘目录不能被触碰（技能铁律 10）
        Assert.True(Directory.Exists(dir));
    }

    [Fact]
    public async Task Register_MissingDirectory_Returns_Failure_With_Reason()
    {
        var json = await Tool("sems_register_project").ExecuteAsync(
            JsonSerializer.Serialize(new { root = Path.Combine(_dbDir, "nope") }));

        Assert.False(IsSuccess(json));
        Assert.Contains("目录不存在", ErrorOf(json));
    }

    [Fact]
    public async Task Missing_Required_Arg_Returns_Friendly_Error_Naming_The_List_Tool()
    {
        var json = await Tool("sems_update_project").ExecuteAsync("{}");
        Assert.False(IsSuccess(json));
        Assert.Contains("sems_list_projects", ErrorOf(json));

        var cmdJson = await Tool("sems_add_command").ExecuteAsync(
            JsonSerializer.Serialize(new { name = "x", script = "echo" }));
        Assert.False(IsSuccess(cmdJson));
        Assert.Contains("projectId", ErrorOf(cmdJson));
    }

    [Fact]
    public async Task Unknown_Ids_Return_Failure_Not_Exception()
    {
        var update = await Tool("sems_update_project").ExecuteAsync(
            JsonSerializer.Serialize(new { id = 998877, name = "x" }));
        Assert.False(IsSuccess(update));

        var remove = await Tool("sems_remove_project").ExecuteAsync(
            JsonSerializer.Serialize(new { id = 998877 }));
        Assert.False(IsSuccess(remove));

        var list = await Tool("sems_list_commands").ExecuteAsync(
            JsonSerializer.Serialize(new { projectId = 998877 }));
        Assert.False(IsSuccess(list));

        var cmdUpdate = await Tool("sems_update_command").ExecuteAsync(
            JsonSerializer.Serialize(new { commandId = 998877, name = "x" }));
        Assert.False(IsSuccess(cmdUpdate));

        var cmdDelete = await Tool("sems_delete_command").ExecuteAsync(
            JsonSerializer.Serialize(new { commandId = 998877 }));
        Assert.False(IsSuccess(cmdDelete));
    }

    [Fact]
    public async Task Run_And_Stop_Failures_Are_Expressed_As_Tool_Errors()
    {
        _runner.Setup(r => r.Launch(It.IsAny<int>())).Returns((RunSession?)null);
        var launch = await Tool("sems_run_command").ExecuteAsync(
            JsonSerializer.Serialize(new { commandId = 5 }));
        Assert.False(IsSuccess(launch));
        Assert.Contains("sems_list_commands", ErrorOf(launch));

        _runner.Setup(r => r.Stop(It.IsAny<int>())).Returns(false);
        var stop = await Tool("sems_stop_command").ExecuteAsync(
            JsonSerializer.Serialize(new { commandId = 5 }));
        Assert.False(IsSuccess(stop));

        _runner.Setup(r => r.StopExternal(It.IsAny<int>())).Returns(false);
        var stopFail = await Tool("sems_stop_run").ExecuteAsync(
            JsonSerializer.Serialize(new { pid = 999999 }));
        Assert.False(IsSuccess(stopFail));

        _runner.Setup(r => r.StopExternal(It.IsAny<int>())).Returns(true);
        var stopOk = await Tool("sems_stop_run").ExecuteAsync(
            JsonSerializer.Serialize(new { pid = 1234 }));
        Assert.True(IsSuccess(stopOk));
    }

    [Fact]
    public async Task Runs_Tools_Project_Sessions_For_External_Callers()
    {
        _runner.Setup(r => r.Current()).Returns(new List<RunSession>
        {
            new() { CommandId = 7, ProjectId = 3, ProjectName = "p", CommandName = "dev", Pid = 1111, Origin = "Launched" }
        });
        _runner.Setup(r => r.CheckAndMerge()).Returns(new List<RunSession>
        {
            new() { CommandId = 0, ProjectId = 3, ProjectName = "p", CommandName = "node", Pid = 2222, Origin = "Detected" }
        });

        var list = await Tool("sems_list_runs").ExecuteAsync("{}");
        Assert.True(IsSuccess(list));
        var listed = Payload(list).GetProperty("data").GetProperty("runs")[0];
        Assert.Equal(1111, listed.GetProperty("pid").GetInt32());
        Assert.Equal("Launched", listed.GetProperty("origin").GetString());

        var check = await Tool("sems_check_runs").ExecuteAsync("{}");
        Assert.True(IsSuccess(check));
        var data = Payload(check).GetProperty("data");
        Assert.Equal(2222, data.GetProperty("runs")[0].GetProperty("pid").GetInt32());
        Assert.Contains("pid", data.GetProperty("note").GetString());
    }

    [Fact]
    public async Task Malformed_Json_Never_Throws()
    {
        var json = await Tool("sems_list_projects").ExecuteAsync("{not json");
        Assert.False(IsSuccess(json));
        Assert.Contains("参数 JSON 非法", ErrorOf(json));

        // 空参数对无 required 的工具等价于 {}
        Assert.True(IsSuccess(await Tool("sems_list_projects").ExecuteAsync("")));
    }

    [Fact]
    public async Task Missing_Services_Returns_Failure_Envelope()
    {
        var orphan = BuildTools(new ServiceCollection().BuildServiceProvider());

        var json = await orphan.Single(t => t.Name == "sems_list_projects").ExecuteAsync("{}");
        Assert.False(IsSuccess(json));
        Assert.Contains("未注册", ErrorOf(json));
    }

    [Fact]
    public async Task PluginId_Flows_Into_Every_Tool()
    {
        var tools = BuildTools(_services, "sems");
        Assert.All(tools, t => Assert.Equal("sems", t.PluginId));

        // 不同插件 id 下 Id 仍唯一（供 UnregisterTool 按 Id 摘除）
        Assert.All(tools, t => Assert.StartsWith("sems.tool.", t.Id));
        Assert.True(IsSuccess(await tools.Single(t => t.Name == "sems_list_projects").ExecuteAsync("{}")));
    }

    [Fact]
    public async Task Context_Wrapped_Provider_Resolves_Host_Container()
    {
        // 模拟宿主真实链路：工具拿到的是插件 IContext（GetService 只查自有表），
        // 宿主把根 IServiceProvider seed 进 Context —— 基类必须经它回落宿主容器解析 IProjectService。
        // 回归背景：基类曾直接 _services.CreateScope()，宿主下报
        // "No service for type 'IServiceScopeFactory' has been registered"（e2e MCP 链路实抓）。
        var services = _services;
        var ctxLike = new Mock<IServiceProvider>();
        ctxLike.Setup(p => p.GetService(typeof(IServiceProvider))).Returns(services);

        var json = await new SemsListProjectsToolFunction("sems", ctxLike.Object).ExecuteAsync("{}");
        Assert.True(IsSuccess(json));
    }
}
