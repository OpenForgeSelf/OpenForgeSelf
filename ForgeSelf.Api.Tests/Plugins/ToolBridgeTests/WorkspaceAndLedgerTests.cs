using System.Text.Json.Nodes;
using ForgeSelf.Api.Plugins.ToolBridge.Models;
using ForgeSelf.Api.Plugins.ToolBridge.Services;

namespace ForgeSelf.Api.Tests.Plugins.ToolBridgeTests;

/// <summary>
/// AC10 / AC11 + BC-8/BC-11：工作根设置与解析、回合台账落盘。
/// 关键口径：**判"设置是否真生效"要换一个新建实例去读文件**（不吃当次内存状态），
/// 与 DesignSystem AC13 的教训同族；台账目录同样只创建不删除（铁律 10）。
/// </summary>
public class WorkspaceAndLedgerTests
{
    // ---------- TryResolve ----------

    [Theory]
    [InlineData("", "")]
    [InlineData("a.txt", "a.txt")]
    [InlineData("sub/a.txt", "sub/a.txt")]
    [InlineData(@"sub\\a.txt", "sub/a.txt")]
    public void 相对路径解析到工作根内(string input, string expectRelative)
    {
        var sandbox = Scratch.NewSandbox(nameof(WorkspaceAndLedgerTests), out _);

        var ok = sandbox.TryResolve(input, out var full, out var error);

        ok.Should().BeTrue(error);
        error.Should().BeNull();
        sandbox.ToRelative(full).Should().Be(expectRelative);
        full.Should().StartWith(sandbox.Current().Root);
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("sub/../../escape.txt")]
    [InlineData("..")]
    public void 含上级段的路径一律拒(string input)
    {
        var sandbox = Scratch.NewSandbox(nameof(WorkspaceAndLedgerTests), out _);

        var ok = sandbox.TryResolve(input, out _, out var error);

        ok.Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(@"D:\somewhere\file.txt")]
    [InlineData("/etc/hosts")]
    public void 绝对路径一律拒并说明要相对路径(string input)
    {
        var sandbox = Scratch.NewSandbox(nameof(WorkspaceAndLedgerTests), out _);

        var ok = sandbox.TryResolve(input, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("绝对路径");
    }

    // ---------- TrySetRoot ----------

    [Fact]
    public void 默认工作根是插件数据目录下的workspace()
    {
        var sandbox = Scratch.NewSandbox(nameof(WorkspaceAndLedgerTests), out var dataDir);

        var info = sandbox.Current();

        info.Source.Should().Be("default");
        info.Root.Should().Be(Path.Combine(dataDir, "workspace"));
        info.Exists.Should().BeTrue("默认根按需创建，用户不用先手建目录");
        Directory.Exists(info.Root).Should().BeTrue();
    }

    [Fact]
    public void 设置相对路径_被拒()
    {
        var sandbox = Scratch.NewSandbox(nameof(WorkspaceAndLedgerTests), out _);

        var ok = sandbox.TrySetRoot("relative/dir", true, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("绝对路径");
    }

    [Fact]
    public void 设置盘符根_属危险根_未确认即拒_BC11()
    {
        var sandbox = Scratch.NewSandbox(nameof(WorkspaceAndLedgerTests), out _);
        var driveRoot = Path.GetPathRoot(Path.GetTempPath())!;

        var ok = sandbox.TrySetRoot(driveRoot, false, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("危险工作根");
        error.Should().Contain("confirmUnsafe");
        sandbox.Current().Root.Should().Be(sandbox.DefaultRoot, "被拒不得改动当前工作根");
    }

    [Fact]
    public void 位于Git仓库树内的目录_未确认即拒_确认后放行_BC11()
    {
        var sandbox = Scratch.NewSandbox(nameof(WorkspaceAndLedgerTests), out _);
        // 造一个带 .git 的假仓库根，用它里面的子目录当候选工作根。
        var repo = Scratch.NewDir("fakegit");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        var insideRepo = Path.Combine(repo, "src");

        var denied = sandbox.TrySetRoot(insideRepo, false, out _, out var error);
        denied.Should().BeFalse();
        error.Should().Contain("Git 仓库树内");

        var allowed = sandbox.TrySetRoot(insideRepo, true, out var info, out var error2);
        allowed.Should().BeTrue(error2);
        info!.Root.Should().Be(insideRepo);
        info.Dangerous.Should().BeTrue("用户显式确认过的危险根必须一直标着");
    }

    [Fact]
    public void 工作根设置_换新建实例仍能读到_AC10()
    {
        var sandbox = Scratch.NewSandbox(nameof(WorkspaceAndLedgerTests), out var dataDir);
        var target = Path.Combine(Scratch.NewDir("rootcase"), "workspace");

        // 带 confirm：scratch 可能正好落在仓库树内（跑测前按 §5.0 把 TEMP 指进 .temp/tmp），
        // 本用例测的是**持久化**，危险根判定另有上面的 BC-11 用例负责。
        sandbox.TrySetRoot(target, true, out _, out var error).Should().BeTrue(error);

        // 换一个实例（等价于插件热重载/宿主重启）：判据必须来自 settings.json 而不是内存。
        var reopened = new SandboxRoot(dataDir);
        var info = reopened.Current();

        info.Root.Should().Be(target);
        info.Source.Should().Be("settings");
        File.Exists(Path.Combine(dataDir, "settings.json")).Should().BeTrue();
        Directory.Exists(target).Should().BeTrue();
    }

    [Fact]
    public void 合法设置不留下半截JSON_临时文件已改名()
    {
        var sandbox = Scratch.NewSandbox(nameof(WorkspaceAndLedgerTests), out var dataDir);
        var target = Path.Combine(Scratch.NewDir("atomic"), "ws");

        sandbox.TrySetRoot(target, true, out _, out _);

        File.Exists(Path.Combine(dataDir, "settings.json")).Should().BeTrue();
        File.Exists(Path.Combine(dataDir, "settings.json.tmp")).Should().BeFalse("BR-6：临时文件必须改名而不是残留");
    }

    // ---------- TurnLedger ----------

    private static TurnRecord Record(string turnId, string text, int recognized) => new()
    {
        TurnId = turnId,
        CreatedAt = DateTimeOffset.Now.ToString("O"),
        WorkspaceRoot = "x",
        Text = text,
        Stats = new TurnStats { Recognized = recognized, Executed = recognized },
        Calls = Enumerable.Range(0, recognized)
            .Select(i => new ParsedCall { Tool = "read_file", RawName = "read_file", Via = "test", Fragment = $"f{i}" })
            .ToList(),
        ResultTextJson = ResultFormatter.BuildJson(new List<ToolResult>(), new List<UnknownCall>(), new List<UnparsedFragment>()),
        ResultTextPlain = ResultFormatter.BuildPlain(new List<ToolResult>(), new List<UnknownCall>(), new List<UnparsedFragment>())
    };

    [Fact]
    public void 一轮落一个文件_列表倒序可读_AC11()
    {
        var ledger = new TurnLedger(Scratch.NewDir("ledger1"));

        var first = Record(TurnLedger.NewTurnId(), "第一段粘贴原文", 1);
        Thread.Sleep(1200); // turnId 含秒级时间码，确保两条可排序
        var second = Record(TurnLedger.NewTurnId(), "第二段粘贴原文", 3);

        ledger.TryAppend(first, out var e1).Should().BeTrue(e1);
        ledger.TryAppend(second, out var e2).Should().BeTrue(e2);

        File.Exists(Path.Combine(ledger.LedgerDirectory, second.TurnId + ".json")).Should().BeTrue();

        var list = ledger.List(take: 10, skip: 0);
        list.Total.Should().Be(2);
        list.Items[0].TurnId.Should().Be(second.TurnId, "列表必须倒序（最新在前）");
        list.Items[0].Stats.Recognized.Should().Be(3);
        list.Items[0].Preview.Should().Be("第二段粘贴原文");
        list.Items[0].Corrupt.Should().BeFalse();

        var full = ledger.Get(second.TurnId, out var getError);
        getError.Should().BeNull();
        full!.Text.Should().Be("第二段粘贴原文");
        full.Calls.Should().HaveCount(3);
    }

    [Fact]
    public void 分页参数生效且take有上限()
    {
        var dir = Scratch.NewDir("ledger2");
        var ledger = new TurnLedger(dir);
        for (var i = 0; i < 4; i++)
        {
            Thread.Sleep(1100);
            ledger.TryAppend(Record(TurnLedger.NewTurnId(), $"第{i}轮", i), out _);
        }

        ledger.List(take: 2, skip: 0).Items.Should().HaveCount(2);
        ledger.List(take: 2, skip: 2).Items.Should().HaveCount(2);
        ledger.List(take: 9999, skip: 0).Items.Should().HaveCount(4, "take 会被夹到 200，但总数本来就少");
        ledger.List(take: 2, skip: 0).Total.Should().Be(4);
    }

    [Fact]
    public void 损坏记录只标corrupt_不炸列表_BC8()
    {
        var dir = Scratch.NewDir("ledger3");
        var ledger = new TurnLedger(dir);
        Thread.Sleep(1100);
        ledger.TryAppend(Record(TurnLedger.NewTurnId(), "好的记录", 1), out _);

        // 手写一条坏文件（测试自己的 scratch 目录，不碰任何真实库）
        var corruptPath = Path.Combine(ledger.LedgerDirectory, $"20200101000000-{Guid.NewGuid().ToString("N")[..8]}.json");
        File.WriteAllText(corruptPath, "{ 这不是合法 JSON ");

        var list = ledger.List(10, 0);

        list.Items.Should().HaveCount(2);
        list.Items.Count(i => i.Corrupt).Should().Be(1);
        list.Items.Count(i => !i.Corrupt).Should().Be(1);
    }

    [Fact]
    public void 非法turnId_拒绝并说明防路径穿越()
    {
        var ledger = new TurnLedger(Scratch.NewDir("ledger4"));

        var record = ledger.Get("../../etc/passwd", out var error);

        record.Should().BeNull();
        error.Should().Contain("非法");
    }

    [Fact]
    public void 台账目录不存在时列表为空而不是抛()
    {
        var ledger = new TurnLedger(Path.Combine(Scratch.NewDir("ledger5"), "not-created-yet"));

        var list = ledger.List(10, 0);

        list.Items.Should().BeEmpty();
        list.Total.Should().Be(0);
    }

    [Fact]
    public void 记录里同时存有粘贴原文与两种回粘文本_AC11()
    {
        var ledger = new TurnLedger(Scratch.NewDir("ledger6"));
        var record = Record(TurnLedger.NewTurnId(), "原文", 2);

        ledger.TryAppend(record, out _);
        var read = ledger.Get(record.TurnId, out _);

        read!.ResultTextJson.Should().Contain(PromptBuilder.ResultMarker);
        read.ResultTextPlain.Should().Contain("mode=plain");
        read.SpecVersion.Should().Be(ToolSpec.SpecVersion);
    }
}
