using FluentAssertions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.TodoTracker.Entities;
using ForgeSelf.Api.Plugins.TodoTracker.Models;
using ForgeSelf.Api.Plugins.TodoTracker.Services;
using ForgeSelf.Core;
using XCode;
using Xunit;
using TodoStatus = ForgeSelf.Api.Plugins.TodoTracker.TodoStatus;

namespace ForgeSelf.Api.Tests.Plugins.TodoTracker;

/// <summary>
/// 批量委派状态回读（UX 批次 FR-3.0/3.3）：列表实时徽标的数据源。
///
/// 验证：只返回有委派的任务、接缝缺席返回空列表不报错、AgentName 按可用清单填充、
/// 未知 agent 兜底 null（前端显示 agent#id）、字段集契约（DTO ↔ 前端 TS 类型）。
/// </summary>
[Collection("XCode")]
public class TodoAgentStatusBatchTests : IClassFixture<XCodeTestFixture>
{
    private readonly TodoProjectService _projects;
    private readonly TodoService _todos;
    private readonly TaskExecutionService _records = new();
    private readonly FakeDelegation _fake = new();
    private readonly Context _ctx;

    public TodoAgentStatusBatchTests(XCodeTestFixture fixture)
    {
        _ctx = new Context();
        _ctx.Register<IProjectRegistry>(new NoopRegistry());
        _ctx.Register<IAgentDelegation>(_fake);
        _projects = new TodoProjectService(_ctx);
        _todos = new TodoService(_projects, _records);
        _ = fixture;
    }

    private TodoDispatchService Dispatch(IAgentDelegation? seam = null)
    {
        var ctx = new Context();
        ctx.Register<IProjectRegistry>(new NoopRegistry());
        if (seam != null) ctx.Register<IAgentDelegation>(seam);
        return new TodoDispatchService(_records, new AgentTaskGateway(ctx), _projects, ctx);
    }

    [Fact]
    public async Task 批量回读_只返回有委派的任务且AgentName按可用清单填充()
    {
        var delegated = await ReadyTask("批量-已委派");
        var result = await Dispatch(_fake).DelegateAsync(delegated.Id,
            new DelegateToAgentRequest { PermissionMode = "read-only", AgentId = 5 }, "manual");
        result.Ok.Should().BeTrue(result.Error);

        var plain = await ReadyTask("批量-未委派");

        var statuses = await Dispatch(_fake).AgentStatusesAsync([delegated.Id, plain.Id, 999_999]);

        statuses.Should().ContainSingle("只有有委派的任务才该返回；未委派与不存在的 id 一律跳过");
        statuses[0].TodoId.Should().Be(delegated.Id);
        statuses[0].TaskKey.Should().Be(_fake.SubmitResult.TaskKey);
        statuses[0].AgentName.Should().Be("codex", "agent 名从网关可用清单按 AgentId 解析，列表徽标才显示得出名字");
        statuses[0].Status.Should().Be(_fake.Snapshot!.Status);
    }

    [Fact]
    public async Task 批量回读_空ids与无委派任务都返回空列表()
    {
        (await Dispatch(_fake).AgentStatusesAsync([])).Should().BeEmpty();

        var plain = await ReadyTask("批量-普通任务");
        (await Dispatch(_fake).AgentStatusesAsync([plain.Id])).Should().BeEmpty();
    }

    [Fact]
    public async Task 批量回读_接缝缺席返回空列表而不报错()
    {
        // 先造一条真实委派记录（接缝在场时），再用无接缝的服务批量读：必须空列表、不许 503 冒泡成错误徽标
        var delegated = await ReadyTask("批量-接缝缺席");
        var ok = await Dispatch(_fake).DelegateAsync(delegated.Id,
            new DelegateToAgentRequest { PermissionMode = "read-only", AgentId = 5 }, "manual");
        ok.Ok.Should().BeTrue(ok.Error);
        (await _todos.GetTodoByIdAsync(delegated.Id))!.AgentTaskKey.Should().NotBeNullOrEmpty();

        var statuses = await Dispatch(seam: null).AgentStatusesAsync([delegated.Id]);
        statuses.Should().BeEmpty("agent-hub 未装/未启用时列表不该冒出 503 错误徽标，静默无徽标即可");
    }

    [Fact]
    public async Task 批量回读_委派给未知agent时AgentName为null()
    {
        // AgentId=99 不在可用清单（清单只有 codex id=5）⇒ AgentName 必须 null，前端兜底 agent#99
        var created = await ReadyTask("批量-未知agent");
        var row = Todo.FindById(created.Id)!;
        row.AgentTaskKey = "key-unknown-agent";
        row.AgentId = 99;
        row.Update();

        var statuses = await Dispatch(_fake).AgentStatusesAsync([row.Id]);
        statuses.Should().ContainSingle();
        statuses[0].AgentName.Should().BeNull("未知/已失效 agent 给 null，前端显示 agent#id 而不是编一个名字");
    }

    [Fact]
    public void 字段集契约_AgentStatusDto与前端TS类型一致()
    {
        // 前端 types.ts 的 AgentStatus 是这份 DTO 的镜像；扩字段必须两边同步（此用例红 = 正确提醒）
        var expected = new[]
        {
            "Ok", "Error", "TodoId", "TaskKey", "AgentName", "Status", "Terminal",
            "ExitCode", "ErrorCode", "ElapsedMs", "ResultSummary", "FilesChanged",
            "Cwd", "Verification", "NotFound", "StatusCode"
        };
        typeof(AgentStatusDto).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(expected,
            "AgentStatusDto 字段清单是前后端契约；新增 AgentName 已同步，前端 TS 类型必须一致");
    }

    private async Task<TodoDto> ReadyTask(string title) => await _todos.CreateTodoAsync(new CreateTodoRequest
    {
        Title = title,
        Stage = "Ready",
        ProjectPath = LocateRepoRoot(),
        Objective = "可验证目标",
        Content = "正文内容",
        Acceptance = "- [ ] 判据一",
        Verification = "dotnet test"
    });

    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx"))) return dir.FullName;
            dir = dir.Parent;   // 必须推进指针（plugin-development §B）
        }
        throw new FileNotFoundException("找不到仓库根（ForgeSelf.slnx）");
    }

    /// <summary>可编排的假接缝：记录请求、返回预设提交结果与快照、暴露可用 agent 清单。</summary>
    private sealed class FakeDelegation : IAgentDelegation
    {
        public AgentDelegationOutcome SubmitResult { get; init; } = AgentDelegationOutcome.Ok("key-batch-1", 5, "codex", "Queued");
        public AgentDelegationSnapshot? Snapshot { get; init; } = new()
        {
            TaskKey = "key-batch-1", Status = "Running", Terminal = false,
            ElapsedMs = 1_234, Artifacts = []
        };

        public Task<AgentDelegationOutcome> SubmitAsync(AgentDelegationRequest request, CancellationToken ct = default) =>
            Task.FromResult(SubmitResult);

        public Task<AgentDelegationSnapshot?> FindAsync(string taskKey, CancellationToken ct = default) =>
            Task.FromResult(string.Equals(taskKey, Snapshot?.TaskKey, StringComparison.OrdinalIgnoreCase) ? Snapshot : null);

        public Task<bool> MarkCompletedAsync(string taskKey, CancellationToken ct = default) =>
            Task.FromResult(Snapshot != null && Snapshot.Status != "Succeeded"
                            && string.Equals(taskKey, Snapshot.TaskKey, StringComparison.OrdinalIgnoreCase));

        public IReadOnlyList<AgentDelegationAgent> ListAvailableAgents() =>
            [new AgentDelegationAgent { Id = 5, Name = "codex", Vendor = "codex" }];
    }

    /// <summary>空实现注册表：批量状态用例不涉及项目关联，只让 TodoProjectService 能构造。</summary>
    private sealed class NoopRegistry : IProjectRegistry
    {
        public bool Register(string root, out string? error) => Register(root, null, out error);
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
