using FluentAssertions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.TodoTracker.Models;
using ForgeSelf.Api.Plugins.TodoTracker.Services;
using ForgeSelf.Core;
using XCode;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.TodoTracker;

/// <summary>
/// 「一键交给本工具 AI Agent」（内置引擎，PILOT-…）：真库 + 假内置执行接缝。
///
/// 验证：引擎分流（agenthub/builtin）、run key 回填与引擎标记、内置接缝缺席降级
/// （不改状态不写成功痕）、状态词表映射（Pending→Queued、Planning/Running→Running、
/// Completed→Succeeded、Stuck→Running 非终态、Failed/Cancelled 直转）、快照回读与 404 兜底。
/// </summary>
[Collection("XCode")]
public class TodoBuiltInDispatchTests : IClassFixture<XCodeTestFixture>
{
    private readonly TodoProjectService _projects;
    private readonly TodoService _todos;
    private readonly TaskExecutionService _records = new();
    private readonly FakeBuiltIn _fake = new();

    public TodoBuiltInDispatchTests(XCodeTestFixture fixture)
    {
        _projects = new TodoProjectService(CtxWith());
        _todos = new TodoService(_projects, _records);
        _ = fixture;
    }

    private Context CtxWith(IBuiltInAgentExecution? builtin = null)
    {
        var ctx = new Context();
        ctx.Register<IProjectRegistry>(new NoopRegistry());
        if (builtin != null) ctx.Register<IBuiltInAgentExecution>(builtin);
        return ctx;
    }

    private TodoDispatchService Dispatch(IBuiltInAgentExecution? builtin = null)
        => new(_records, new AgentTaskGateway(new Context()), _projects, CtxWith(builtin));

    private async Task<TodoDto> ReadyTask(string title) => await _todos.CreateTodoAsync(new CreateTodoRequest
    {
        Title = title,
        Stage = "Ready",
        ProjectPath = @"D:\src\my-proj\OpenForgeSelf\OpenForgeSelf",
        Objective = "可验证目标",
        Content = "正文内容",
        Acceptance = "- [ ] 判据一",
        Verification = "dotnet test"
    });

    [Fact]
    public async Task 内置委派应回填run键并进入执行中()
    {
        var created = await ReadyTask("内置-委派");

        var result = await Dispatch(_fake).DelegateAsync(created.Id,
            new DelegateToAgentRequest { Engine = "builtin", AgentRoleId = "agent.programmer", PermissionMode = "read-only" }, "manual");

        result.Ok.Should().BeTrue(result.Error);
        result.TaskKey.Should().Be("run:1");
        result.AgentName.Should().Be("程序员");
        result.SeamMissing.Should().BeFalse();

        var after = await _todos.GetTodoByIdAsync(created.Id);
        after!.Stage.Should().Be("Running", "内置委派成功即进入执行中");
        after.AgentTaskKey.Should().Be("run:1");
        after.AgentEngine.Should().Be("builtin");
        after.AgentId.Should().Be(7, "agent.programmer = 序号 7");
        _fake.LastRequest!.Cwd.Should().Be(@"D:\src\my-proj\OpenForgeSelf\OpenForgeSelf",
            "委派必须把任务的项目根作为内置引擎工作目录传下去，否则文件工具全部失效（实测 stuck）");

        var records = await _records.ListAsync(created.Id, 1, 20);
        records.Items.Should().Contain(r => r.Action.Contains("本工具 AI Agent"), "留痕必须点名引擎");
    }

    [Fact]
    public async Task 内置接缝缺席不应改状态也不写成功痕()
    {
        var created = await ReadyTask("内置-接缝缺席");
        var before = (await _todos.GetTodoByIdAsync(created.Id))!.Stage;

        var result = await Dispatch().DelegateAsync(created.Id,
            new DelegateToAgentRequest { Engine = "builtin", PermissionMode = "read-only" }, "manual");

        result.Ok.Should().BeFalse();
        result.Error.Should().Contain("ai-agent");
        result.SeamMissing.Should().BeTrue("接缝缺席要打专属标记，前端才能区分「没装」与「委派失败」");
        result.TaskKey.Should().BeEmpty();

        var after = await _todos.GetTodoByIdAsync(created.Id);
        after!.Stage.Should().Be(before, "委派失败不能把任务推到执行中");
        after.AgentTaskKey.Should().BeEmpty();
        after.AgentEngine.Should().Be("agenthub", "未委派过就保持默认引擎，不残留 builtin 标记");
    }

    [Fact]
    public void 内置状态词表应映射到todo展示词表()
    {
        TodoDispatchService.MapBuiltInStatus("Pending").Should().Be("Queued");
        TodoDispatchService.MapBuiltInStatus("Planning").Should().Be("Running");
        TodoDispatchService.MapBuiltInStatus("Running").Should().Be("Running");
        TodoDispatchService.MapBuiltInStatus("Completed").Should().Be("Succeeded");
        TodoDispatchService.MapBuiltInStatus("Failed").Should().Be("Failed");
        TodoDispatchService.MapBuiltInStatus("Cancelled").Should().Be("Cancelled");
        // Stuck 非终态：保持 Running 展示（可 resume / 人工介入），由前端 resultSummary 带出卡住原因
        TodoDispatchService.MapBuiltInStatus("Stuck").Should().Be("Running");
        TodoDispatchService.MapBuiltInStatus("  planning ").Should().Be("Running", "大小写与空白不应影响映射");
    }

    [Fact]
    public async Task 内置状态回读应按快照映射且不存在时404兜底()
    {
        var created = await ReadyTask("内置-状态回读");
        var ok = await Dispatch(_fake).DelegateAsync(created.Id,
            new DelegateToAgentRequest { Engine = "builtin", PermissionMode = "read-only" }, "manual");
        ok.Ok.Should().BeTrue();

        _fake.Snapshot = new BuiltInAgentSnapshot
        {
            RunId = 1,
            Status = "Completed",
            Terminal = true,
            ElapsedMs = 1234,
            ResultSummary = "完成：写好了 ok.txt",
            FilesChanged = ["ok.txt"],
            AgentName = "程序员"
        };
        var done = await Dispatch(_fake).AgentStatusAsync(created.Id);
        done.Ok.Should().BeTrue();
        done.Status.Should().Be("Succeeded");
        done.Terminal.Should().BeTrue();
        done.AgentName.Should().Be("程序员");
        done.FilesChanged.Should().ContainSingle(f => f.Path == "ok.txt");

        // 卡住：非终态 + 展示 Running
        _fake.Snapshot = new BuiltInAgentSnapshot { RunId = 1, Status = "Stuck", Terminal = false, ElapsedMs = 5000, ResultSummary = "模型通道无响应" };
        var stuck = await Dispatch(_fake).AgentStatusAsync(created.Id);
        stuck.Ok.Should().BeTrue();
        stuck.Status.Should().Be("Running");
        stuck.Terminal.Should().BeFalse();
        stuck.ErrorCode.Should().Be("stuck");

        // 快照丢失：404 文案兜底（AgentHub.db 升级重置等场景，不许当成功）
        _fake.Snapshot = null;
        var gone = await Dispatch(_fake).AgentStatusAsync(created.Id);
        gone.Ok.Should().BeFalse();
        gone.Error.Should().Contain("不存在");
    }

    [Fact]
    public async Task 内置成功终态读状态应自动推进待验收并留痕()
    {
        var created = await ReadyTask("内置-自动回写");
        var ok = await Dispatch(_fake).DelegateAsync(created.Id,
            new DelegateToAgentRequest { Engine = "builtin", PermissionMode = "read-only" }, "manual");
        ok.Ok.Should().BeTrue();

        // 委派后仍 Running，且没有回写记录（只到 Running）
        var running = await _todos.GetTodoByIdAsync(created.Id);
        running!.Stage.Should().Be("Running");
        (await _records.ListAsync(created.Id, 1, 50)).Items.Should().NotContain(r => r.Action.Contains("执行回写"));

        // 快照变为成功终态 → 读一次状态即自动推进（不用手动点「记为执行记录」）
        _fake.Snapshot = new BuiltInAgentSnapshot
        {
            RunId = 1, Status = "Completed", Terminal = true, ElapsedMs = 4321,
            ResultSummary = "完成：写好了 ok-builtin.txt", FilesChanged = ["ok-builtin.txt"], AgentName = "程序员"
        };
        var done = await Dispatch(_fake).AgentStatusAsync(created.Id);
        done.Ok.Should().BeTrue();
        done.Status.Should().Be("Succeeded");

        var after = await _todos.GetTodoByIdAsync(created.Id);
        after!.Stage.Should().Be("Review", "成功终态必须自动离开执行中，否则列表一直挂着执行中与已成功打架（输入9）");
        var records = await _records.ListAsync(created.Id, 1, 50);
        records.Items.Should().Contain(r => r.Action == "agent 执行回写：Succeeded" && r.StageTo == "Review");
    }

    [Fact]
    public async Task 自动推进应幂等不重复写回写记录()
    {
        var created = await ReadyTask("内置-自动回写幂等");
        var ok = await Dispatch(_fake).DelegateAsync(created.Id,
            new DelegateToAgentRequest { Engine = "builtin", PermissionMode = "read-only" }, "manual");
        ok.Ok.Should().BeTrue();

        _fake.Snapshot = new BuiltInAgentSnapshot { RunId = 1, Status = "Completed", Terminal = true, ElapsedMs = 100, ResultSummary = "完成", FilesChanged = [], AgentName = "程序员" };
        await Dispatch(_fake).AgentStatusAsync(created.Id);
        (await _todos.GetTodoByIdAsync(created.Id))!.Stage.Should().Be("Review");
        var once = await _records.ListAsync(created.Id, 1, 50);
        once.Items.Count(r => r.Action.Contains("执行回写")).Should().Be(1);

        // 已 Review 再读（列表 12s 轮询 / 详情刷新）：不再推进、不再重复留痕
        await Dispatch(_fake).AgentStatusAsync(created.Id);
        await Dispatch(_fake).AgentStatusAsync(created.Id);
        var twice = await _records.ListAsync(created.Id, 1, 50);
        twice.Items.Count(r => r.Action.Contains("执行回写")).Should().Be(1, "自动回写必须幂等，轮询不能刷出重复记录");
    }

    [Fact]
    public async Task 失败终态不应自动推进保持由人判()
    {
        var created = await ReadyTask("内置-失败不动");
        var ok = await Dispatch(_fake).DelegateAsync(created.Id,
            new DelegateToAgentRequest { Engine = "builtin", PermissionMode = "read-only" }, "manual");
        ok.Ok.Should().BeTrue();

        _fake.Snapshot = new BuiltInAgentSnapshot { RunId = 1, Status = "Failed", Terminal = true, ElapsedMs = 200, ResultSummary = "模型 400", FilesChanged = [], AgentName = "程序员" };
        var done = await Dispatch(_fake).AgentStatusAsync(created.Id);
        done.Ok.Should().BeTrue();
        done.Status.Should().Be("Failed");

        var after = await _todos.GetTodoByIdAsync(created.Id);
        after!.Stage.Should().Be("Running", "失败/取消维持「由人判」语义：状态停在执行中，由人工决定下一步");
        (await _records.ListAsync(created.Id, 1, 50)).Items.Should().NotContain(r => r.Action.Contains("执行回写"));
    }

    private sealed class FakeBuiltIn : IBuiltInAgentExecution
    {
        public BuiltInAgentSnapshot? Snapshot { get; set; } = new()
        {
            RunId = 1, Status = "Pending", Terminal = false, ElapsedMs = 0,
            ResultSummary = "已创建", FilesChanged = [], AgentName = "程序员"
        };

        public BuiltInAgentRequest? LastRequest { get; private set; }

        public Task<BuiltInAgentOutcome> StartAsync(BuiltInAgentRequest request, CancellationToken ct = default)
        {
            LastRequest = request;
            request.Prompt.Should().NotBeNullOrWhiteSpace("内置委派必须把四栏组装成提示词正文");
            request.CreatedBy.Should().Be("todo-tracker");
            return Task.FromResult(BuiltInAgentOutcome.Ok(1, "程序员", "Pending"));
        }

        public Task<BuiltInAgentSnapshot?> FindAsync(long runId, CancellationToken ct = default) =>
            Task.FromResult(Snapshot);

        public IReadOnlyList<BuiltInAgentOption> ListAgents() =>
        [
            new() { RoleId = "agent.coordinator", Name = "协调者" },
            new() { RoleId = "agent.analyst", Name = "分析师" },
            new() { RoleId = "agent.critic", Name = "评论家" },
            new() { RoleId = "agent.generalist", Name = "通用助手" },
            new() { RoleId = "agent.writer", Name = "写作者" },
            new() { RoleId = "agent.researcher", Name = "研究员" },
            new() { RoleId = "agent.programmer", Name = "程序员" }
        ];
    }

    private sealed class NoopRegistry : IProjectRegistry
    {
        public int Count => 0;
        public bool Register(string root, out string? error) { error = null; return true; }
        public bool Register(string root, string? source, out string? error) { error = null; return true; }
        public List<ProjectInfo> GetAll() => [];
        public ProjectInfo? Get(int id) => null;
        public bool Update(int id, ProjectUpdate update) => false;
        public bool Remove(int id, out string? error) { error = null; return false; }
        public int AddCommand(int projectId, RunCommandInfo command) => 0;
        public bool UpdateCommand(int commandId, RunCommandUpdate update) => false;
        public bool DeleteCommand(int commandId) => false;
        public List<RunCommandInfo> GetCommands(int projectId) => [];
    }
}
