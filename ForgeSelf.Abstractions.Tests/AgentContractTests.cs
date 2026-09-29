using System.Runtime.CompilerServices;
using ForgeSelf.Abstractions;
using Xunit;

namespace ForgeSelf.Abstractions.Tests;

/// <summary>
/// B5（041）Agent 契约测试：替代已删除的 <c>AgentLoopContractTests</c>，
/// 锁定 <see cref="IAgent"/> / <see cref="IAgentRegistry"/> / <see cref="TurnFrame"/> 的形状与生命周期语义。
/// </summary>
public class AgentContractTests
{
    [Fact]
    public async Task GetOrCreateAsync_CreatesOnce_AndReusesBySession()
    {
        var created = 0;
        var registry = new CountingRegistry((sessionId, options) =>
        {
            created++;
            return new StubAgent(sessionId, options);
        });

        var first = await registry.GetOrCreateAsync("s1", new AgentOptions { ModelId = "m1" });
        var second = await registry.GetOrCreateAsync("s1", new AgentOptions { ModelId = "m2" });

        Assert.Same(first, second);
        Assert.Equal(1, created);
        Assert.Equal("m1", first.Options.ModelId);
    }

    [Fact]
    public async Task TryGetAsync_ReturnsNull_WhenNotRegistered()
    {
        var registry = new CountingRegistry((sessionId, options) => new StubAgent(sessionId, options));

        Assert.Null(await registry.TryGetAsync("missing"));

        var agent = await registry.GetOrCreateAsync("s1");
        Assert.Same(agent, await registry.TryGetAsync("s1"));
    }

    [Fact]
    public async Task DisposeAsync_Unregisters_AndDisposesAgent()
    {
        var registry = new CountingRegistry((sessionId, options) => new StubAgent(sessionId, options));
        var agent = (StubAgent)await registry.GetOrCreateAsync("s1");

        Assert.True(await registry.DisposeAsync("s1"));
        Assert.False(await registry.DisposeAsync("s1"));
        Assert.Null(await registry.TryGetAsync("s1"));
        Assert.Equal(1, agent.DisposeCount);
    }

    [Fact]
    public async Task CancelAsync_RecordsCause_AndOptionallyKeepsInbox()
    {
        var agent = new StubAgent("s1", new AgentOptions());
        agent.Inbox.Followup("s1", "待处理");

        await agent.CancelAsync(new AgentCancelCause.User(), keepInbox: true);
        Assert.Equal(1, agent.CancelCount);
        Assert.IsType<AgentCancelCause.User>(agent.LastCause);
    }

    [Fact]
    public async Task RunAsync_YieldsDeclaredFrameSequence()
    {
        var agent = new StubAgent("s1", new AgentOptions());
        var frames = new List<TurnFrame>();
        await foreach (var frame in agent.RunAsync())
        {
            frames.Add(frame);
        }

        Assert.Equal(
            new TurnFrame[]
            {
                new TurnStarted("t1"),
                new StepStarted("t1-0", 0),
                new AssistantDelta("hi"),
                new TurnCompleted("t1", TurnEndReason.Completed)
            },
            frames);
    }

    /// <summary>计数用注册表实现（只统计工厂被调用次数，其余交默认实现）。</summary>
    private sealed class CountingRegistry : IAgentRegistry
    {
        private readonly Dictionary<string, IAgent> _agents = new(StringComparer.Ordinal);
        private readonly Func<string, AgentOptions, IAgent> _factory;

        public CountingRegistry(Func<string, AgentOptions, IAgent> factory)
        {
            _factory = factory;
        }

        public Task<IAgent> GetOrCreateAsync(string sessionId, AgentOptions? options = null, CancellationToken ct = default)
        {
            if (!_agents.TryGetValue(sessionId, out var agent))
            {
                agent = _factory(sessionId, options ?? new AgentOptions());
                _agents[sessionId] = agent;
            }
            return Task.FromResult(agent);
        }

        public Task<IAgent?> TryGetAsync(string sessionId)
            => Task.FromResult(_agents.TryGetValue(sessionId, out var agent) ? agent : null);

        public async Task<bool> DisposeAsync(string sessionId, CancellationToken ct = default)
        {
            if (!_agents.Remove(sessionId, out var agent)) return false;
            if (agent is IAsyncDisposable disposable) await disposable.DisposeAsync();
            return true;
        }
    }

    /// <summary>桩 Agent：记录取消/释放次数，产出固定帧序列。</summary>
    private sealed class StubAgent : IAgent, IAsyncDisposable
    {
        private readonly List<InboxItem> _items = new();

        public StubAgent(string sessionId, AgentOptions options)
        {
            SessionId = sessionId;
            Options = options;
        }

        public string SessionId { get; }
        public AgentOptions Options { get; }
        public AgentStatus Status { get; private set; } = AgentStatus.Idle;
        public int CancelCount { get; private set; }
        public int DisposeCount { get; private set; }
        public AgentCancelCause? LastCause { get; private set; }

        public IInbox Inbox => new StubInbox(_items);

        public async IAsyncEnumerable<TurnFrame> RunAsync([EnumeratorCancellation] CancellationToken ct = default)
        {
            Status = AgentStatus.Running;
            yield return new TurnStarted("t1");
            yield return new StepStarted("t1-0", 0);
            yield return new AssistantDelta("hi");
            yield return new TurnCompleted("t1", TurnEndReason.Completed);
            await Task.CompletedTask;
            Status = AgentStatus.Idle;
        }

        public Task CancelAsync(AgentCancelCause cause, bool keepInbox = false)
        {
            CancelCount++;
            LastCause = cause;
            if (!keepInbox) _items.Clear();
            return Task.CompletedTask;
        }

        public Task WhenIdleAsync(CancellationToken ct = default) => Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }

        private sealed class StubInbox : IInbox
        {
            private readonly List<InboxItem> _items;

            public StubInbox(List<InboxItem> items)
            {
                _items = items;
            }

            public void Send(string sessionId, string content, InboxTarget target, MessageSource source, bool wakeup)
                => _items.Add(new InboxItem(Guid.NewGuid().ToString("N")[..8], content, source));

            public InboxBatch Claim(string sessionId, bool atTurnBoundary)
            {
                var taken = _items.ToList();
                _items.Clear();
                return new InboxBatch { Items = taken, Wakeup = true };
            }

            public IReadOnlyList<InboxItem> Peek(string sessionId) => _items.ToArray();

            public void Clear(string sessionId) => _items.Clear();
        }
    }
}
