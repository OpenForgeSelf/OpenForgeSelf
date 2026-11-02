using System.Text.Json;
using FluentAssertions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Api.Plugins.TodoTracker.Models;
using ForgeSelf.Api.Plugins.TodoTracker.Services;
using ForgeSelf.Core;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.TodoTracker;

/// <summary>
/// B2① 复现测试（TODO 233「执行记录 filesChanged 结构化字段未持久化」· 战役批 B2）：
/// todo id=56 实证——内置 agent 明明新建过文件（第 4 条回报写明「新建 browser-dispatch-check-20261025.txt」），
/// 但 `GET /api/todos/{id}/records` 回读 filesChanged 全为 []。
///
/// 按真实链路两段复现（先红后绿，红灯原文留 05-evidence）：
/// - AC-T1（快照段）：Run 步骤 ToolCallsJson 为「轨迹列表再套一层 JSON 字符串」的双编码形态
///   （RunOrchestratorService.MarkStepCompleted 的现状写法），FindAsync 快照应提取出 write_file 目标路径
///   —— 现状 ExtractFilePaths 正则对 `\"path\\\":` 不命中 ⇒ 红。
/// - AC-T2（落库段）：内置成功终态自动回写留痕（AppendAdvanceRecordAsync → AppendSystemAsync），
///   状态快照里已带 FilesChanged，回写记录的结构化字段应非空 —— 现状 AppendSystemAsync 无此参数 ⇒ 红。
/// </summary>
[Collection("XCode")]
public class BuiltInFilesChangedPersistenceTests : IClassFixture<XCodeTestFixture>
{
    /// <summary>与 TODO 233 同形态的回报文件名（browser-dispatch-check-YYYYMMDD.txt）。</summary>
    private const string ReportedFile = "browser-dispatch-check-20261102.txt";

    private readonly TodoProjectService _projects;
    private readonly TodoService _todos;
    private readonly TaskExecutionService _records = new();
    private readonly FakeBuiltIn _fake = new();

    public BuiltInFilesChangedPersistenceTests(XCodeTestFixture fixture)
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

    // ==================== AC-T1：快照段（工具轨迹 → BuiltInAgentSnapshot.FilesChanged） ====================

    [Fact]
    public async Task 步骤工具轨迹含write_file时快照应提取出目标路径()
    {
        var runId = 5601L;

        // 现网形态：MarkStepCompleted 用 JsonSerializer.Serialize(result.ToolCalls) 落库，
        // ArgumentsSummary 是再套一层 JSON 字符串的原始参数（RunOrchestratorService.TruncateForTrace，200 截断），
        // 内层引号全部转义为 \"。
        var argsJson = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["path"] = @"D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\.temp\b2-repro\" + ReportedFile,
            ["content"] = "demo"
        });
        if (argsJson.Length > 200) argsJson = argsJson[..200];
        var toolCallsJson = JsonSerializer.Serialize(new List<StepToolCallTrace>
        {
            new() { Name = "write_file", ArgumentsSummary = argsJson, Success = true }
        });

        var detail = new AgentRunDetailResponse
        {
            Run = new AgentRunDto { Id = runId, Status = AgentRunStatus.Completed, AgentName = "程序员" },
            Steps =
            [
                new AgentStepRunDto
                {
                    Id = 1, RunId = runId, StepIndex = 0, StepId = "s1", Name = "写文件",
                    Status = AgentStepStatus.Completed, OutputJson = "已创建 " + ReportedFile,
                    ToolCallsJson = toolCallsJson
                }
            ]
        };

        var orchestrator = new Mock<IRunOrchestratorService>();
        orchestrator.Setup(o => o.GetRunDetailAsync(runId)).ReturnsAsync(detail);
        var svc = new BuiltInAgentExecutionService(orchestrator.Object, null!, null!);

        var snapshot = await svc.FindAsync(runId);

        snapshot.Should().NotBeNull();
        snapshot!.Status.Should().Be("completed");
        snapshot.FilesChanged.Should().NotBeEmpty(
            "Run 步骤确实调用了 write_file 写文件，结构化文件清单不得在快照段丢失（TODO 233 第 4 条回报形态）");
        snapshot.FilesChanged.Should().Contain(f => f.EndsWith(ReportedFile),
            "提取结果必须指向回报里写明的那个文件（双编码 JSON 里的 path 参数）");
    }

    // ==================== AC-T2：落库段（自动回写留痕 → TaskExecution.FilesChanged） ====================

    [Fact]
    public async Task 内置成功自动回写留痕应携带结构化文件清单()
    {
        var created = await ReadyTask("B2①-自动回写带清单");
        var ok = await Dispatch(_fake).DelegateAsync(created.Id,
            new DelegateToAgentRequest { Engine = "builtin", AgentRoleId = "agent.programmer", PermissionMode = "read-only" }, "manual");
        ok.Ok.Should().BeTrue(ok.Error);

        // 快照变为成功终态且快照里带文件清单（ReadBuiltInStatusAsync 已映射进 AgentStatusDto.FilesChanged）
        _fake.Snapshot = new BuiltInAgentSnapshot
        {
            RunId = 1, Status = "Completed", Terminal = true, ElapsedMs = 4321,
            ResultSummary = "完成：新建了 " + ReportedFile,
            FilesChanged = [ReportedFile],
            AgentName = "程序员"
        };
        var done = await Dispatch(_fake).AgentStatusAsync(created.Id);
        done.Ok.Should().BeTrue();
        done.Status.Should().Be("Succeeded");

        var after = await _todos.GetTodoByIdAsync(created.Id);
        after!.Stage.Should().Be("Review");

        var records = await _records.ListAsync(created.Id, 1, 50);
        var advance = records.Items.FirstOrDefault(r => r.Action.Contains("执行回写"));
        advance.Should().NotBeNull("成功终态必须自动推进并留一条「agent 执行回写」记录");
        advance!.FilesChanged.Should().NotBeEmpty(
            "回写留痕时状态快照已带文件清单，落库不得丢弃（TODO 233：5 条记录 filesChanged 全空）");
        advance.FilesChanged.Should().ContainSingle(f => f.Path == ReportedFile);
    }

    // ==================== 替身 ====================

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
            return Task.FromResult(BuiltInAgentOutcome.Ok(1, "程序员", "Pending"));
        }

        public Task<BuiltInAgentSnapshot?> FindAsync(long runId, CancellationToken ct = default) =>
            Task.FromResult(Snapshot);

        public IReadOnlyList<BuiltInAgentOption> ListAgents() =>
        [
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
