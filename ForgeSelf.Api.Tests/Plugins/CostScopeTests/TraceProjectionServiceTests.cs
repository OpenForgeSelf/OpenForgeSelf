using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.CostScope.Services;

namespace ForgeSelf.Api.Tests.Plugins.CostScopeTests;

/// <summary>
/// A10 原子任务：插件侧 trace 关联 + waterfall 单测（FR-4.5 / AC4 / BC-4）。
/// </summary>
/// <remarks>
/// <b>输入口径说明</b>：两侧数据都是**合成样本**（宿主轮次来自 <c>ChatTurn</c> 投影、运行来自 AgentRun），
/// 仓内没有真实 trace 可驱动；时间全部用<b>固定构造时刻</b>（非 <c>DateTime.Now</c>）以保证可重复。
/// 断言一律按<b>规则</b>（关联/排序/非负/未归属）而非某个写死的时间戳金值。
/// </remarks>
public class TraceProjectionServiceTests
{
    private static readonly DateTime T0 = new(2026, 10, 6, 10, 0, 0);

    private readonly TraceProjectionService _sut = new();

    private static TurnTelemetryRecord Turn(
        long id,
        string sessionKey,
        int offsetSeconds,
        string model = "gpt-4o",
        long durationMs = 1000,
        int prompt = 100,
        int completion = 50) => new()
        {
            Id = id,
            ChatSessionId = 1,
            TurnIndex = (int)id,
            SessionKey = sessionKey,
            Style = "openai",
            Model = model,
            PromptTokens = prompt,
            CompletionTokens = completion,
            FirstTokenMs = 100,
            DurationMs = durationMs,
            ResponseStatus = 200,
            CreatedTime = T0.AddSeconds(offsetSeconds),
            AgentRunId = null,
        };

    private static AgentRunTelemetry Run(
        string agentRunId,
        string sessionKey,
        int startOffsetSeconds,
        int endOffsetSeconds,
        bool withTime = true,
        params AgentStepTelemetry[] steps) => new()
        {
            AgentRunId = agentRunId,
            SessionKey = sessionKey,
            StepCount = steps.Length,
            TotalTokens = steps.Sum(s => (long)s.Tokens),
            StartedTime = withTime ? T0.AddSeconds(startOffsetSeconds) : null,
            EndedTime = withTime ? T0.AddSeconds(endOffsetSeconds) : null,
            Steps = steps,
        };

    private static AgentStepTelemetry Step(int idx, string name, int startOffsetSeconds, long durationMs) => new()
    {
        StepIndex = idx,
        Kind = "tool",
        Name = name,
        DurationMs = durationMs,
        Tokens = 0,
        StartedAt = T0.AddSeconds(startOffsetSeconds),
    };

    // ---------- 基本形状 ----------

    [Fact]
    public void Build_同时含LLM与tool节点_按开始时间升序()
    {
        var turns = new[] { Turn(1, "sk-1", 0) };
        var runs = new[]
        {
            Run("run-1", "sk-1", -10, 100, true,
                Step(0, "search", 5, 500),
                Step(1, "write", 50, 300)),
        };

        var wf = _sut.Build("run-1", turns, runs);

        wf.AgentRunId.Should().Be("run-1");
        wf.Nodes.Should().HaveCount(3);
        wf.Nodes.Select(n => n.Start).Should().BeInAscendingOrder();
        wf.Nodes.Count(n => n.Kind == TraceNodeKind.Llm).Should().Be(1);
        wf.Nodes.Count(n => n.Kind == TraceNodeKind.Tool).Should().Be(2);
    }

    [Fact]
    public void Build_未指定run时_自动选关联轮次最多的运行()
    {
        var turns = new[] { Turn(1, "sk-1", 0), Turn(2, "sk-1", 20) };
        var runs = new[]
        {
            // run-a 的窗口 [-100,-90] 加上默认 ±30s 容差后仍覆盖不到任何轮次 ⇒ 关联数 0，被排除
            Run("run-a", "sk-1", -100, -90, true),
            Run("run-b", "sk-1", 0, 60, true),    // 覆盖 2 条
        };

        var wf = _sut.Build(null, turns, runs);

        wf.AgentRunId.Should().Be("run-b");
        wf.LinkedTurns.Should().Be(2);
    }

    [Fact]
    public void Build_关联数并列时_取时间最早者_保证结果可重复()
    {
        var turns = new[] { Turn(1, "sk-1", 0) };
        var runs = new[]
        {
            Run("run-late", "sk-1", 0, 60, true),
            Run("run-early", "sk-1", -20, 60, true),
        };

        // 两条都关联到 1 条 ⇒ 并列；按「时间最早」稳定裁决，避免同样的输入给出不稳定的 runId
        _sut.Build(null, turns, runs).AgentRunId.Should().Be("run-early");
    }

    [Fact]
    public void Build_指定run不存在_返回未关联而非回退到别的run()
    {
        var turns = new[] { Turn(1, "sk-1", 0) };
        var runs = new[] { Run("run-a", "sk-1", -10, 100, true) };

        var wf = _sut.Build("run-not-exist", turns, runs);

        wf.AgentRunId.Should().BeNull("指定不存在的 run 不得静默回退到其他 run");
        wf.UnlinkedTurns.Should().Be(1);
        wf.CorrelationNote.Should().Be(TraceProjectionService.UnlinkedNote);
    }

    // ---------- BC-4：未关联如实上报 ----------

    [Fact]
    public void Build_无任何运行_如实返回未关联_不静默挂靠()
    {
        var turns = new[] { Turn(1, "sk-1", 0), Turn(2, "sk-1", 10) };

        var wf = _sut.Build(null, turns, []);

        wf.AgentRunId.Should().BeNull();
        wf.UnlinkedTurns.Should().Be(2);
        wf.LinkedTurns.Should().Be(0);
        wf.Nodes.Should().HaveCount(2, "未关联的轮次仍作为 LLM 节点展示，但不带 runId");
        wf.Nodes.Should().OnlyContain(n => n.AgentRunId == null, "未关联节点不得挂到某个 run（BC-4）");
    }

    [Fact]
    public void Build_部分轮次落在窗外_未关联数正确()
    {
        var turns = new[] { Turn(1, "sk-1", 0), Turn(2, "sk-1", 5000) };
        var runs = new[] { Run("run-1", "sk-1", 0, 60, true) };

        var wf = _sut.Build("run-1", turns, runs);

        wf.LinkedTurns.Should().Be(1);
        wf.UnlinkedTurns.Should().Be(1, "窗外 5000s 的轮次必须计入未关联");
    }

    // ---------- 关联规则 ----------

    [Fact]
    public void IsLinked_会话键不同_不关联()
    {
        var run = Run("run-1", "sk-A", 0, 100, true);

        TraceProjectionService.IsLinked(Turn(1, "sk-B", 10), run).Should().BeFalse();
    }

    [Fact]
    public void IsLinked_会话键相同且时间在窗内_关联()
    {
        var run = Run("run-1", "sk-A", 10, 100, true);

        TraceProjectionService.IsLinked(Turn(1, "sk-A", 50), run).Should().BeTrue();
    }

    [Fact]
    public void IsLinked_时间在窗外超容差_不关联()
    {
        var run = Run("run-1", "sk-A", 0, 10, true);

        // 容差默认 30s ⇒ 41s 已在窗外
        TraceProjectionService.IsLinked(Turn(1, "sk-A", 41), run).Should().BeFalse();
    }

    [Fact]
    public void IsLinked_运行无时间锚点_降级为仅按会话键()
    {
        var run = Run("run-1", "sk-A", 0, 0, withTime: false);

        TraceProjectionService.IsLinked(Turn(1, "sk-A", 99999), run).Should().BeTrue("无锚点时只看会话键");
        TraceProjectionService.IsLinked(Turn(2, "sk-B", 0), run).Should().BeFalse("会话键不同仍不关联");
    }

    [Fact]
    public void Build_运行无时间锚点_标记为近似()
    {
        var turns = new[] { Turn(1, "sk-1", 0) };
        var runs = new[] { Run("run-1", "sk-1", 0, 0, withTime: false) };

        var wf = _sut.Build("run-1", turns, runs);

        wf.IsApproximate.Should().BeTrue("无时间锚点时关联置信度低，必须标近似");
        wf.CorrelationNote.Should().Contain("近似");
    }

    [Fact]
    public void Build_有时间锚点_仍标注关联口径为近似()
    {
        var turns = new[] { Turn(1, "sk-1", 0) };
        var runs = new[] { Run("run-1", "sk-1", -10, 100, true) };

        var wf = _sut.Build("run-1", turns, runs);

        // 宿主无外键 ⇒ 本质上永远是推断，页面不得把推断说成事实
        wf.CorrelationNote.Should().Contain("近似");
    }

    // ---------- AC4：耗时非负 ----------

    [Fact]
    public void Build_上游给出负耗时_截断为0不抛()
    {
        var turns = new[] { Turn(1, "sk-1", 0, durationMs: -50) };
        var runs = new[]
        {
            Run("run-1", "sk-1", -10, 100, true, Step(0, "bad", 5, -999)),
        };

        var wf = _sut.Build("run-1", turns, runs);

        wf.Nodes.Should().OnlyContain(n => n.DurationMs >= 0, "耗时不得为负（AC4）");
    }

    // ---------- 节点字段 ----------

    [Fact]
    public void Build_LLM节点带模型与token合计()
    {
        var turns = new[] { Turn(1, "sk-1", 0, model: "claude-x", prompt: 200, completion: 100) };
        var runs = new[] { Run("run-1", "sk-1", -10, 100, true) };

        var node = _sut.Build("run-1", turns, runs).Nodes.Single(n => n.Kind == TraceNodeKind.Llm);

        node.Model.Should().Be("claude-x");
        node.Tokens.Should().Be(300);
        node.Label.Should().Be("claude-x");
    }

    [Fact]
    public void Build_零token_保持null不伪造0()
    {
        var turns = new[] { Turn(1, "sk-1", 0, prompt: 0, completion: 0) };
        var runs = new[] { Run("run-1", "sk-1", -10, 100, true) };

        var node = _sut.Build("run-1", turns, runs).Nodes.Single(n => n.Kind == TraceNodeKind.Llm);

        node.Tokens.Should().BeNull("无 usage 时保持 null，不伪造 0（FR-1.4）");
    }

    [Fact]
    public void Build_工具节点缺名时回退为类型()
    {
        var turns = new[] { Turn(1, "sk-1", 0) };
        var step = new AgentStepTelemetry { StepIndex = 0, Kind = "think", DurationMs = 10, StartedAt = T0 };
        var runs = new[] { Run("run-1", "sk-1", -10, 100, true, step) };

        var node = _sut.Build("run-1", turns, runs).Nodes.Single(n => n.Kind == TraceNodeKind.Tool);

        node.Label.Should().Be("think");
    }

    [Fact]
    public void Build_空输入_返回空瀑布不抛()
    {
        var wf = _sut.Build(null, [], []);

        wf.Nodes.Should().BeEmpty();
        wf.AgentRunId.Should().BeNull();
    }

    [Fact]
    public void Build_turns为null_抛()
    {
        var act = () => _sut.Build("run-1", null!, []);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Build_runs为null_抛()
    {
        var act = () => _sut.Build("run-1", [], null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
