using System.Diagnostics;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.Sems.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeSelf.Api.Plugins.Sems;

/// <summary>
/// sems 对外工具的统一基类：参数解析、作用域与服务解析、<c>{success,data|error}</c> 封套、异常兜底、用量上报。
/// 能力全部委托 sems 服务层（<see cref="IProjectService"/> / <see cref="IRunnerService"/>），
/// 与自带界面的 HTTP 端点共用同一份业务实现 —— 界面能做的，外部经 McpCenter 也能做，结果一致。
/// 注册进宿主 <c>IToolRegistry</c> 后，外部经 mcp-center 的
/// <c>universal_tool{tool:"list_tools"}</c> 枚举、<c>universal_tool{tool:"sems_*"}</c> 调用（McpCenter 无需改动）。
/// </summary>
public abstract class SemsToolFunctionBase : IToolFunctionExtension
{
    private readonly IServiceProvider? _services;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PluginId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ParametersJsonSchema { get; set; } = string.Empty;

    protected SemsToolFunctionBase(string pluginId, string toolName, string description, string schema,
        IServiceProvider? services)
    {
        PluginId = pluginId;
        Name = toolName;
        Id = $"sems.tool.{toolName["sems_".Length..]}";
        Description = description;
        ParametersJsonSchema = schema;
        _services = services;
    }

    /// <summary>执行入口。任何异常都必须转成 <c>{success:false,error}</c>，不得抛给 universal_tool 转发层。</summary>
    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        string result;
        try
        {
            // 传入的 _services 是插件 IContext（自有服务表，无 IServiceScopeFactory）；
            // 宿主把根 IServiceProvider seed 进 Context，经它回落宿主容器才能解析 Apply 里注册的服务。
            var provider = (_services?.GetService(typeof(IServiceProvider)) as IServiceProvider) ?? _services;
            using var scope = provider?.CreateScope();
            if (scope == null)
            {
                result = Fail("服务提供者未初始化");
            }
            else
            {
                JsonElement args;
                using (var doc = string.IsNullOrWhiteSpace(parameters)
                           ? JsonDocument.Parse("{}")
                           : JsonDocument.Parse(parameters))
                {
                    args = doc.RootElement.Clone();
                }

                result = Handle(scope.ServiceProvider, args);
            }
        }
        catch (JsonException ex)
        {
            result = Fail($"参数 JSON 非法：{ex.Message}");
        }
        catch (Exception ex)
        {
            result = Fail($"执行失败：{ex.Message}");
        }

        stopwatch.Stop();
        await this.RecordUsageAsync(_services, Name, stopwatch.ElapsedMilliseconds);
        return result;
    }

    /// <summary>子类实现：只做「取参 → 调 sems 服务 → 组装封套」，业务规则一律留在服务层。</summary>
    protected abstract string Handle(IServiceProvider services, JsonElement args);

    /// <summary>取字符串参数（缺失或非字符串返回 null）。</summary>
    protected static string? GetStr(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out var el)) return null;
        return el.ValueKind == JsonValueKind.String ? el.GetString() : null;
    }

    /// <summary>取整数参数（缺失或类型不符返回 false）。</summary>
    protected static bool TryGetInt(JsonElement args, string name, out int value)
    {
        value = 0;
        return args.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out value);
    }

    /// <summary>缺必填 id 时的统一错误（引导调用方先枚举）。</summary>
    protected static string MissingInt(string name, string fromTool) =>
        Fail($"参数 {name}（整数）必填，请先调用 {fromTool} 取得；全部 sems 工具可用 universal_tool{{\"tool\":\"list_tools\"}} 枚举");

    /// <summary>成功封套。</summary>
    protected static string Ok(object? data) => JsonSerializer.Serialize(new { success = true, data });

    /// <summary>失败封套。</summary>
    protected static string Fail(string error) => JsonSerializer.Serialize(new { success = false, error });

    /// <summary>把 sems 服务结果转成工具封套（与 HTTP 端点共用同一状态语义）。</summary>
    protected static string From<T>(SemsResult<T> result) =>
        result.Success ? Ok(result.Data) : Fail(result.Error ?? "操作失败");

    protected IProjectService? Projects(IServiceProvider services) => services.GetService<IProjectService>();

    protected IRunnerService? Runner(IServiceProvider services) => services.GetService<IRunnerService>();

    /// <summary>服务未注册（插件装配异常）。</summary>
    protected static string ServiceMissing(string service) => Fail($"{service} 未注册（sems 插件未完成装配）");

    /// <summary>项目对外投影（含运行命令概要）。</summary>
    protected static object Shape(ProjectInfo p) => new
    {
        id = p.Id,
        root = p.Root,
        name = p.Name,
        type = p.Type,
        description = p.Description,
        tags = p.Tags,
        source = p.Source,
        pathExists = p.PathExists,
        isGitRepo = p.IsGitRepo,
        createdAt = p.CreatedAt,
        lastActiveAt = p.LastActiveAt,
        commandCount = p.Commands.Count,
        commands = p.Commands
            .Select(c => new { id = c.Id, name = c.Name, script = c.Script, url = c.Url, sort = c.Sort })
            .ToList()
    };

    /// <summary>运行会话对外投影。</summary>
    protected static object Shape(RunSession s) => new
    {
        commandId = s.CommandId,
        projectId = s.ProjectId,
        projectName = s.ProjectName,
        commandName = s.CommandName,
        pid = s.Pid,
        startedAt = s.StartedAt,
        origin = s.Origin
    };
}

/// <summary>sems_list_projects</summary>
public class SemsListProjectsToolFunction : SemsToolFunctionBase
{
    public SemsListProjectsToolFunction(string pluginId, IServiceProvider? services)
        : base(pluginId, "sems_list_projects",
            "列出 sems（软件工程管理系统）已登记的全部项目及其运行命令概要：id/root/name/type/tags/source/pathExists/isGitRepo/commands。" +
            "项目 id 是其余 sems_* 工具的入参来源。无参数。全部 sems 工具可用 universal_tool{\"tool\":\"list_tools\"} 枚举。",
            @"{ ""type"": ""object"", ""properties"": {} }", services) { }

    protected override string Handle(IServiceProvider services, JsonElement args)
    {
        var svc = Projects(services);
        if (svc == null) return ServiceMissing(nameof(IProjectService));

        var projects = svc.GetProjects();
        return Ok(new { total = projects.Count, projects = projects.Select(Shape).ToList() });
    }
}

/// <summary>sems_register_project</summary>
public class SemsRegisterProjectToolFunction : SemsToolFunctionBase
{
    public SemsRegisterProjectToolFunction(string pluginId, IServiceProvider? services)
        : base(pluginId, "sems_register_project",
            "登记一个项目：root 为**本机已存在的目录绝对路径**（必填），name 可选（留空取目录名）。" +
            "来源记 manual，与 AIAgent 选目录登记的 ai-agent 互不覆盖；同目录重复登记只刷新活跃时间，不会改动已编辑的档案。" +
            "返回登记后的项目（含 id，供其余 sems_* 工具使用）。全部 sems 工具可用 universal_tool{\"tool\":\"list_tools\"} 枚举。",
            @"{
  ""type"": ""object"",
  ""properties"": {
    ""root"": { ""type"": ""string"", ""description"": ""项目根目录绝对路径，必须已存在，如 D:\\src\\my-proj\\Demo"" },
    ""name"": { ""type"": ""string"", ""description"": ""项目显示名（可选，留空取目录名）"" }
  },
  ""required"": [""root""]
}", services) { }

    protected override string Handle(IServiceProvider services, JsonElement args)
    {
        var svc = Projects(services);
        if (svc == null) return ServiceMissing(nameof(IProjectService));

        var root = GetStr(args, "root");
        if (string.IsNullOrWhiteSpace(root)) return Fail("参数 root（目录绝对路径）不能为空");

        var result = svc.Register(root, GetStr(args, "name"));
        if (!result.Success) return Fail(result.Error ?? "登记失败");
        return Ok(new { project = Shape(result.Data!) });
    }
}

/// <summary>sems_update_project</summary>
public class SemsUpdateProjectToolFunction : SemsToolFunctionBase
{
    public SemsUpdateProjectToolFunction(string pluginId, IServiceProvider? services)
        : base(pluginId, "sems_update_project",
            "编辑项目档案（部分更新，传谁改谁）：id（必填，来自 sems_list_projects），name/type/description/tags 可选；" +
            "tags 为逗号分隔文本。type 常用值 frontend/backend/fullstack/library/tool/other，也可自定义。" +
            "全部 sems 工具可用 universal_tool{\"tool\":\"list_tools\"} 枚举。",
            @"{
  ""type"": ""object"",
  ""properties"": {
    ""id"": { ""type"": ""integer"", ""description"": ""项目 id（sems_list_projects 的 id 字段）"" },
    ""name"": { ""type"": ""string"", ""description"": ""项目显示名"" },
    ""type"": { ""type"": ""string"", ""description"": ""项目类型（frontend/backend/fullstack/library/tool/other 或自定义）"" },
    ""description"": { ""type"": ""string"", ""description"": ""项目描述"" },
    ""tags"": { ""type"": ""string"", ""description"": ""标签，逗号分隔"" }
  },
  ""required"": [""id""]
}", services) { }

    protected override string Handle(IServiceProvider services, JsonElement args)
    {
        var svc = Projects(services);
        if (svc == null) return ServiceMissing(nameof(IProjectService));
        if (!TryGetInt(args, "id", out var id)) return MissingInt("id", "sems_list_projects");

        var update = new ProjectUpdate
        {
            Name = GetStr(args, "name"),
            Type = GetStr(args, "type"),
            Description = GetStr(args, "description"),
            Tags = GetStr(args, "tags")
        };
        return From(svc.UpdateProject(id, update));
    }
}

/// <summary>sems_remove_project</summary>
public class SemsRemoveProjectToolFunction : SemsToolFunctionBase
{
    public SemsRemoveProjectToolFunction(string pluginId, IServiceProvider? services)
        : base(pluginId, "sems_remove_project",
            "【不可恢复】移除项目档案并级联删除其全部运行命令记录。id 必填（sems_list_projects）。" +
            "只删数据库里的档案，**绝不触碰磁盘目录与文件**。项目仍有本面板启动的存活进程时会被拒绝（请先 sems_stop_command）。" +
            "全部 sems 工具可用 universal_tool{\"tool\":\"list_tools\"} 枚举。",
            @"{
  ""type"": ""object"",
  ""properties"": {
    ""id"": { ""type"": ""integer"", ""description"": ""待移除的项目 id"" }
  },
  ""required"": [""id""]
}", services) { }

    protected override string Handle(IServiceProvider services, JsonElement args)
    {
        var svc = Projects(services);
        if (svc == null) return ServiceMissing(nameof(IProjectService));
        if (!TryGetInt(args, "id", out var id)) return MissingInt("id", "sems_list_projects");

        var result = svc.RemoveProject(id);
        if (!result.Success) return Fail(result.Error ?? "移除失败");
        return Ok(new { removed = true, id, note = "仅删除项目档案与其运行命令记录，磁盘目录与文件未被触碰" });
    }
}

/// <summary>sems_list_commands</summary>
public class SemsListCommandsToolFunction : SemsToolFunctionBase
{
    public SemsListCommandsToolFunction(string pluginId, IServiceProvider? services)
        : base(pluginId, "sems_list_commands",
            "列出某项目的全部运行命令（按 sort 升序）：id/name/script/url/sort。projectId 必填（sems_list_projects）。" +
            "返回的 id 即 sems_run_command / sems_update_command / sems_delete_command 的 commandId。" +
            "全部 sems 工具可用 universal_tool{\"tool\":\"list_tools\"} 枚举。",
            @"{
  ""type"": ""object"",
  ""properties"": {
    ""projectId"": { ""type"": ""integer"", ""description"": ""所属项目 id"" }
  },
  ""required"": [""projectId""]
}", services) { }

    protected override string Handle(IServiceProvider services, JsonElement args)
    {
        var svc = Projects(services);
        if (svc == null) return ServiceMissing(nameof(IProjectService));
        if (!TryGetInt(args, "projectId", out var projectId)) return MissingInt("projectId", "sems_list_projects");

        var result = svc.GetCommands(projectId);
        if (!result.Success) return Fail(result.Error ?? "查询失败");
        return Ok(new
        {
            projectId,
            total = result.Data!.Count,
            commands = result.Data
                .Select(c => new { id = c.Id, name = c.Name, script = c.Script, url = c.Url, sort = c.Sort })
                .ToList()
        });
    }
}

/// <summary>sems_add_command</summary>
public class SemsAddCommandToolFunction : SemsToolFunctionBase
{
    public SemsAddCommandToolFunction(string pluginId, IServiceProvider? services)
        : base(pluginId, "sems_add_command",
            "为项目新增一条运行命令。必填 projectId（sems_list_projects）、name（显示名）、script（在项目根执行的命令行，" +
            "如 pnpm dev / dotnet run）；可选 url（运行后访问地址）、sort（排序，默认 0）。返回新 commandId。" +
            "命令随后可用 sems_run_command 启动。全部 sems 工具可用 universal_tool{\"tool\":\"list_tools\"} 枚举。",
            @"{
  ""type"": ""object"",
  ""properties"": {
    ""projectId"": { ""type"": ""integer"", ""description"": ""所属项目 id"" },
    ""name"": { ""type"": ""string"", ""description"": ""命令显示名，如「前端 dev」"" },
    ""script"": { ""type"": ""string"", ""description"": ""运行脚本（在项目根目录以 cmd /c 执行）"" },
    ""url"": { ""type"": ""string"", ""description"": ""运行后访问地址（可选）"" },
    ""sort"": { ""type"": ""integer"", ""description"": ""排序，默认 0，越小越前"" }
  },
  ""required"": [""projectId"", ""name"", ""script""]
}", services) { }

    protected override string Handle(IServiceProvider services, JsonElement args)
    {
        var svc = Projects(services);
        if (svc == null) return ServiceMissing(nameof(IProjectService));
        if (!TryGetInt(args, "projectId", out var projectId)) return MissingInt("projectId", "sems_list_projects");

        var command = new RunCommandInfo
        {
            ProjectId = projectId,
            Name = GetStr(args, "name") ?? string.Empty,
            Script = GetStr(args, "script") ?? string.Empty,
            Url = GetStr(args, "url"),
            Sort = TryGetInt(args, "sort", out var sort) ? sort : 0
        };

        var result = svc.AddCommand(projectId, command);
        if (!result.Success) return Fail(result.Error ?? "新增失败");
        return Ok(new { commandId = result.Data, projectId });
    }
}

/// <summary>sems_update_command</summary>
public class SemsUpdateCommandToolFunction : SemsToolFunctionBase
{
    public SemsUpdateCommandToolFunction(string pluginId, IServiceProvider? services)
        : base(pluginId, "sems_update_command",
            "编辑运行命令（部分更新，传谁改谁）：commandId 必填（sems_list_commands）；name/script/url/sort 可选。" +
            "全部 sems 工具可用 universal_tool{\"tool\":\"list_tools\"} 枚举。",
            @"{
  ""type"": ""object"",
  ""properties"": {
    ""commandId"": { ""type"": ""integer"", ""description"": ""命令 id"" },
    ""name"": { ""type"": ""string"", ""description"": ""命令显示名"" },
    ""script"": { ""type"": ""string"", ""description"": ""运行脚本"" },
    ""url"": { ""type"": ""string"", ""description"": ""运行后访问地址"" },
    ""sort"": { ""type"": ""integer"", ""description"": ""排序"" }
  },
  ""required"": [""commandId""]
}", services) { }

    protected override string Handle(IServiceProvider services, JsonElement args)
    {
        var svc = Projects(services);
        if (svc == null) return ServiceMissing(nameof(IProjectService));
        if (!TryGetInt(args, "commandId", out var commandId)) return MissingInt("commandId", "sems_list_commands");

        var hasSort = TryGetInt(args, "sort", out var sort);
        var update = new RunCommandUpdate
        {
            Name = GetStr(args, "name"),
            Script = GetStr(args, "script"),
            Url = GetStr(args, "url"),
            Sort = hasSort ? sort : null
        };
        return From(svc.UpdateCommand(commandId, update));
    }
}

/// <summary>sems_delete_command</summary>
public class SemsDeleteCommandToolFunction : SemsToolFunctionBase
{
    public SemsDeleteCommandToolFunction(string pluginId, IServiceProvider? services)
        : base(pluginId, "sems_delete_command",
            "【不可恢复】删除一条运行命令。commandId 必填（sems_list_commands）。只删命令记录，不影响磁盘文件与已运行的进程。" +
            "全部 sems 工具可用 universal_tool{\"tool\":\"list_tools\"} 枚举。",
            @"{
  ""type"": ""object"",
  ""properties"": {
    ""commandId"": { ""type"": ""integer"", ""description"": ""待删除的命令 id"" }
  },
  ""required"": [""commandId""]
}", services) { }

    protected override string Handle(IServiceProvider services, JsonElement args)
    {
        var svc = Projects(services);
        if (svc == null) return ServiceMissing(nameof(IProjectService));
        if (!TryGetInt(args, "commandId", out var commandId)) return MissingInt("commandId", "sems_list_commands");

        var result = svc.DeleteCommand(commandId);
        if (!result.Success) return Fail(result.Error ?? "删除失败");
        return Ok(new { deleted = true, commandId });
    }
}

/// <summary>sems_list_runs</summary>
public class SemsListRunsToolFunction : SemsToolFunctionBase
{
    public SemsListRunsToolFunction(string pluginId, IServiceProvider? services)
        : base(pluginId, "sems_list_runs",
            "当前运行列表：由 sems 面板/工具启动且仍存活的会话（commandId/projectId/projectName/commandName/pid/startedAt/origin=Launched）。" +
            "无参数。要看本机其他相关进程用 sems_check_runs。全部 sems 工具可用 universal_tool{\"tool\":\"list_tools\"} 枚举。",
            @"{ ""type"": ""object"", ""properties"": {} }", services) { }

    protected override string Handle(IServiceProvider services, JsonElement args)
    {
        var runner = Runner(services);
        if (runner == null) return ServiceMissing(nameof(IRunnerService));

        var runs = runner.Current();
        return Ok(new { total = runs.Count, runs = runs.Select(Shape).ToList() });
    }
}

/// <summary>sems_check_runs</summary>
public class SemsCheckRunsToolFunction : SemsToolFunctionBase
{
    public SemsCheckRunsToolFunction(string pluginId, IServiceProvider? services)
        : base(pluginId, "sems_check_runs",
            "触发本机进程检测（WMI 查询 Win32_Process + 按项目根路径与类型归属判定），与存活会话合并返回。" +
            "Detected 条目的 commandId 为 0、startedAt 可能为空，停止它们须用 sems_stop_run 传 pid。" +
            "只读、不落库。WMI 不可用时降级为仅 Launched 并在 note 说明。全部 sems 工具可用 universal_tool{\"tool\":\"list_tools\"} 枚举。",
            @"{ ""type"": ""object"", ""properties"": {} }", services) { }

    protected override string Handle(IServiceProvider services, JsonElement args)
    {
        var runner = Runner(services);
        if (runner == null) return ServiceMissing(nameof(IRunnerService));

        var runs = runner.CheckAndMerge();
        return Ok(new
        {
            total = runs.Count,
            runs = runs.Select(Shape).ToList(),
            note = "Detected 条目来自 WMI 检测，commandId=0；停止请用 sems_stop_run 传 pid"
        });
    }
}

/// <summary>sems_run_command</summary>
public class SemsRunCommandToolFunction : SemsToolFunctionBase
{
    public SemsRunCommandToolFunction(string pluginId, IServiceProvider? services)
        : base(pluginId, "sems_run_command",
            "启动一条**已登记**的运行命令（在项目根目录执行 cmd /c <脚本>）。commandId 必填，须先用 sems_list_commands 取得。" +
            "本工具不接受任意脚本字符串。同命令已有存活会话时拒绝（重复启动）。返回 session（含 pid）。" +
            "全部 sems 工具可用 universal_tool{\"tool\":\"list_tools\"} 枚举。",
            @"{
  ""type"": ""object"",
  ""properties"": {
    ""commandId"": { ""type"": ""integer"", ""description"": ""已登记的命令 id"" }
  },
  ""required"": [""commandId""]
}", services) { }

    protected override string Handle(IServiceProvider services, JsonElement args)
    {
        var runner = Runner(services);
        if (runner == null) return ServiceMissing(nameof(IRunnerService));
        if (!TryGetInt(args, "commandId", out var commandId)) return MissingInt("commandId", "sems_list_commands");

        var session = runner.Launch(commandId);
        if (session == null)
            return Fail("命令不存在或已有存活会话（重复启动被拒绝）；请先用 sems_list_commands 确认 commandId");

        return Ok(new { session = Shape(session) });
    }
}

/// <summary>sems_stop_command</summary>
public class SemsStopCommandToolFunction : SemsToolFunctionBase
{
    public SemsStopCommandToolFunction(string pluginId, IServiceProvider? services)
        : base(pluginId, "sems_stop_command",
            "停止「由 sems 面板/工具启动」的会话（按 commandId 取其 PID 杀整棵进程树）。commandId 必填（sems_list_runs）。" +
            "【不可恢复】进程被强制终止；外部捕获的进程请用 sems_stop_run 传 pid。全部 sems 工具可用 universal_tool{\"tool\":\"list_tools\"} 枚举。",
            @"{
  ""type"": ""object"",
  ""properties"": {
    ""commandId"": { ""type"": ""integer"", ""description"": ""要停止的命令 id"" }
  },
  ""required"": [""commandId""]
}", services) { }

    protected override string Handle(IServiceProvider services, JsonElement args)
    {
        var runner = Runner(services);
        if (runner == null) return ServiceMissing(nameof(IRunnerService));
        if (!TryGetInt(args, "commandId", out var commandId)) return MissingInt("commandId", "sems_list_runs");

        if (!runner.Stop(commandId))
            return Fail($"未找到本面板启动的存活会话：{commandId}（外部捕获进程请用 sems_stop_run 按 pid 停止）");

        return Ok(new { stopped = true, commandId });
    }
}

/// <summary>sems_stop_run</summary>
public class SemsStopRunToolFunction : SemsToolFunctionBase
{
    public SemsStopRunToolFunction(string pluginId, IServiceProvider? services)
        : base(pluginId, "sems_stop_run",
            "【高危·不可恢复】按 PID 杀整棵进程树停止进程，用于 sems_check_runs 捕获的外部进程。pid 必填（sems_list_runs / sems_check_runs）。" +
            "杀树可能波及该进程下的他人子进程，调用前必须先确认归属。全部 sems 工具可用 universal_tool{\"tool\":\"list_tools\"} 枚举。",
            @"{
  ""type"": ""object"",
  ""properties"": {
    ""pid"": { ""type"": ""integer"", ""description"": ""进程 PID"" }
  },
  ""required"": [""pid""]
}", services) { }

    protected override string Handle(IServiceProvider services, JsonElement args)
    {
        var runner = Runner(services);
        if (runner == null) return ServiceMissing(nameof(IRunnerService));
        if (!TryGetInt(args, "pid", out var pid)) return MissingInt("pid", "sems_check_runs");

        if (!runner.StopExternal(pid))
            return Fail($"无法停止进程（不存在或无权限）：{pid}");

        return Ok(new { stopped = true, pid, note = "已杀整棵进程树，可能波及该进程的子进程" });
    }
}
