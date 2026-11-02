using ForgeSelf.Abstractions;
using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Services;
using FluentAssertions;
using Xunit;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>P0 可持久化审批：未批准不执行语义、批准参数不变、拒绝、超时 fail-closed。</summary>
[Collection("XCode")]
public class PersistentApprovalServiceTests
{
    public PersistentApprovalServiceTests()
    {
        _ = new XCodeTestFixture();
        foreach (var row in ToolApproval.FindAll().ToArray())
        {
            row.Delete();
        }
    }

    [Fact]
    public async Task CreatePending_ThenList_ReturnsCamelCaseFields()
    {
        var svc = new PersistentApprovalService();
        var created = await svc.CreatePendingAsync(new ApprovalCreateRequest
        {
            SessionId = "s-list",
            ToolName = "write_file",
            ArgsJson = """{"path":"a.txt","content":"x"}""",
            Description = "写文件",
            MeaiRequestId = "req-1",
            CallId = "call-1",
            Timeout = TimeSpan.FromMinutes(5),
        });

        created.Id.Should().BeGreaterThan(0);
        created.Status.Should().Be(nameof(ApprovalStatus.Pending));
        created.ArgsHash.Should().NotBeNullOrEmpty();
        created.RiskLevel.Should().Be(HighRiskToolPolicy.RiskHigh);

        var list = await svc.ListBySessionAsync("s-list");
        list.Should().ContainSingle(x => x.Id == created.Id && x.ToolName == "write_file");
    }

    [Fact]
    public async Task Unapproved_AskAsync_TimesOut_FailClosed()
    {
        var svc = new PersistentApprovalService();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(80));

        // Ask 自带超时；此处用极短外层 ct 触发 cancel → Expired → false
        var approved = await svc.AskAsync("s-timeout", "run_terminal_command", """{"command":"git status"}""", "测超时", cts.Token);

        approved.Should().BeFalse();
        var list = await svc.ListBySessionAsync("s-timeout");
        list.Should().ContainSingle();
        list[0].Status.Should().BeOneOf(nameof(ApprovalStatus.Expired), nameof(ApprovalStatus.Pending));
        // 极端竞态下 Pending 尚未来得及标 Expired；再 Decide 也不该放行已过期语义——再等一轮 Expire
        await Task.Delay(50);
        list = await svc.ListBySessionAsync("s-timeout");
        list[0].Status.Should().Be(nameof(ApprovalStatus.Expired));
    }

    [Fact]
    public async Task Approve_PreservesOriginalArgs_AndContinuationCarriesSameArgs()
    {
        var svc = new PersistentApprovalService();
        const string args = """{"path":"docs/a.md","content":"hello-approval"}""";
        var pending = await svc.CreatePendingAsync(new ApprovalCreateRequest
        {
            SessionId = "s-approve",
            ToolName = "write_file",
            ArgsJson = args,
            MeaiRequestId = "meai-appr",
            CallId = "c-appr",
        });

        var decided = await svc.DecideAsync(pending.Id, approved: true, note: "ok");
        decided.Should().NotBeNull();
        decided!.Status.Should().Be(nameof(ApprovalStatus.Approved));
        decided.ArgsJson.Should().Contain("hello-approval");
        decided.ArgsHash.Should().Be(pending.ArgsHash);

        var cont = await svc.ConsumeContinuationsAsync("s-approve");
        cont.Should().ContainSingle();
        cont[0].Approved.Should().BeTrue();
        cont[0].ArgsJson.Should().Contain("hello-approval");
        cont[0].ToolName.Should().Be("write_file");
        cont[0].MeaiRequestId.Should().Be("meai-appr");
        cont[0].CallId.Should().Be("c-appr");

        // 二次消费为空（防重复执行）
        (await svc.ConsumeContinuationsAsync("s-approve")).Should().BeEmpty();
    }

    [Fact]
    public async Task Deny_FeedsContinuation_ApprovedFalse()
    {
        var svc = new PersistentApprovalService();
        var pending = await svc.CreatePendingAsync(new ApprovalCreateRequest
        {
            SessionId = "s-deny",
            ToolName = "delete_file",
            ArgsJson = """{"path":"x.bin"}""",
            MeaiRequestId = "meai-deny",
            CallId = "c-deny",
        });

        var decided = await svc.DecideAsync(pending.Id, approved: false, note: "不安全");
        decided!.Status.Should().Be(nameof(ApprovalStatus.Denied));
        decided.Note.Should().Be("不安全");

        var cont = await svc.ConsumeContinuationsAsync("s-deny");
        cont.Should().ContainSingle();
        cont[0].Approved.Should().BeFalse();
        cont[0].Note.Should().Be("不安全");
        cont[0].ArgsJson.Should().Contain("x.bin");
    }

    [Fact]
    public async Task AskAsync_ApproveWhileWaiting_ReturnsTrue_WithSameToolArgs()
    {
        var svc = new PersistentApprovalService();
        var session = "s-ask-ok";
        const string args = """{"command":"dotnet --info"}""";

        var askTask = svc.AskAsync(session, "run_terminal_command", args, "终端", CancellationToken.None);

        // 等 Pending 落盘
        ApprovalDto? pending = null;
        for (var i = 0; i < 40 && pending == null; i++)
        {
            await Task.Delay(25);
            var list = await svc.ListBySessionAsync(session);
            pending = list.FirstOrDefault(x => x.Status == nameof(ApprovalStatus.Pending));
        }

        pending.Should().NotBeNull("AskAsync 应已创建 Pending");
        pending!.ArgsJson.Should().Contain("dotnet --info");

        await svc.DecideAsync(pending.Id, approved: true, note: null);
        var ok = await askTask;
        ok.Should().BeTrue();
    }

    [Fact]
    public void HighRiskToolPolicy_RequiresListedTools_Only()
    {
        HighRiskToolPolicy.RequiresApproval("write_file").Should().BeTrue();
        HighRiskToolPolicy.RequiresApproval("run_terminal_command").Should().BeTrue();
        HighRiskToolPolicy.RequiresApproval("delete_file").Should().BeTrue();
        HighRiskToolPolicy.RequiresApproval("read_file").Should().BeFalse();
        HighRiskToolPolicy.RequiresApproval("list_files").Should().BeFalse();
        HighRiskToolPolicy.RequiresApproval("get_current_time").Should().BeFalse();
    }
}
