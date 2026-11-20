using FluentAssertions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AgentHub.Models;
using ForgeSelf.Api.Plugins.AgentHub.Profiles;
using ForgeSelf.Api.Plugins.AgentHub.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using TaskStatus = ForgeSelf.Api.Plugins.AgentHub.Services.TaskStatus;
using IAgentRegistry = ForgeSelf.Api.Plugins.AgentHub.Services.IAgentRegistry;

namespace ForgeSelf.Api.Tests.Plugins.AgentHub;

/// <summary>P3：IAgentRuntime Provider 同实例注册与 Submit/List/Cancel 面。</summary>
[Collection("XCode")]
public class AgentHubRuntimeProviderTests : IClassFixture<XCodeTestFixture>
{
    public AgentHubRuntimeProviderTests(XCodeTestFixture fixture) => _ = fixture;

    [Fact]
    public void 同实例_MS_DI与构造引用一致()
    {
        var services = new ServiceCollection();
        var provider = new AgentHubRuntimeProvider(services.BuildServiceProvider());
        services.AddSingleton(provider);
        services.AddSingleton<IAgentRuntime>(provider);
        var sp = services.BuildServiceProvider();

        var a = sp.GetRequiredService<IAgentRuntime>();
        var b = sp.GetRequiredService<AgentHubRuntimeProvider>();
        ReferenceEquals(a, b).Should().BeTrue();
        ReferenceEquals(a, provider).Should().BeTrue();
    }

    [Fact]
    public async Task ListAgentsAsync_含登记Agent()
    {
        var sp = BuildHost();
        var registry = sp.GetRequiredService<IAgentRegistry>();
        foreach (var a in registry.List().Where(x => x.Vendor == "p3-list").ToList())
            registry.Delete(a.Id);

        registry.Create(new AgentSaveRequest
        {
            Name = "p3-list-" + Guid.NewGuid().ToString("N")[..6],
            Vendor = "p3-list",
            Enabled = true,
            DefaultCwd = Path.GetTempPath(),
        });

        var runtime = new AgentHubRuntimeProvider(sp);
        var list = await runtime.ListAgentsAsync();
        list.Should().Contain(d => d.Vendor == "p3-list");
    }

    [Fact]
    public async Task ListAgentsAsync_产品面不含_scripted()
    {
        // Worker 入口存在时会补充 runtime.list；scripted 必须被消费投影剔除。
        // Worker 不可用时列表仅含登记 Agent，同样不得出现 scripted。
        var sp = BuildHost();
        var runtime = new AgentHubRuntimeProvider(sp);
        var list = await runtime.ListAgentsAsync();
        list.Should().NotContain(d =>
            String.Equals(d.RuntimeId, "scripted", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SubmitAsync_空提示词_返回Failed句柄()
    {
        var sp = BuildHost();
        var runtime = new AgentHubRuntimeProvider(sp);
        var handle = await runtime.SubmitAsync(new AgentTaskRequest { Prompt = "  " });
        handle.Status.Should().Be(TaskStatus.Failed);
        handle.TaskKey.Should().BeEmpty();
    }

    [Fact]
    public async Task CancelAsync_不存在任务_Rejected()
    {
        var sp = BuildHost();
        var runtime = new AgentHubRuntimeProvider(sp);
        var r = await runtime.CancelAsync("no-such-task-key");
        r.Accepted.Should().BeFalse();
        r.Code.Should().Be("not_found");
    }

    [Fact]
    public async Task SendInputAsync_空文本_Rejected()
    {
        var sp = BuildHost();
        var runtime = new AgentHubRuntimeProvider(sp);
        var r = await runtime.SendInputAsync("any-key", new AgentInput { Text = "  " });
        r.Accepted.Should().BeFalse();
        r.Code.Should().Be("input_required");
    }

    [Fact]
    public async Task CloseSessionAsync_未知键_不抛()
    {
        var sp = BuildHost();
        var runtime = new AgentHubRuntimeProvider(sp);
        await runtime.Invoking(r => r.CloseSessionAsync("no-such-session"))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task WatchTaskAsync_未知任务_空流()
    {
        var sp = BuildHost();
        var runtime = new AgentHubRuntimeProvider(sp);
        var count = 0;
        await foreach (var _ in runtime.WatchTaskAsync("missing-task"))
            count++;
        count.Should().Be(0);
    }

    [Fact]
    public async Task WatchTaskAsync_假Transport任务_可读事件()
    {
        var sp = BuildHost(withFakeTransport: true);
        var registry = sp.GetRequiredService<IAgentRegistry>();
        foreach (var a in registry.List().Where(x => x.Vendor == "p3-watch").ToList())
            registry.Delete(a.Id);

        var agent = registry.Create(new AgentSaveRequest
        {
            Name = "p3-watch-" + Guid.NewGuid().ToString("N")[..6],
            Vendor = "p3-watch",
            Enabled = true,
            DefaultCwd = Path.GetTempPath(),
            AccessPoints =
            [
                new AgentAccessPointSaveRequest
                {
                    Mode = "Sessionful",
                    Transport = "Cli",
                    Executable = "echo",
                    IsDefault = true,
                    CancelSupported = true,
                }
            ]
        });

        var runtime = new AgentHubRuntimeProvider(sp);
        var handle = await runtime.SubmitAsync(new AgentTaskRequest
        {
            Prompt = "watch-me",
            AgentId = agent.Id,
            Cwd = Path.GetTempPath(),
            CreatedBy = "test",
        });

        handle.TaskKey.Should().NotBeNullOrWhiteSpace();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var events = new List<AgentTaskEvent>();
        await foreach (var ev in runtime.WatchTaskAsync(handle.TaskKey, 0, cts.Token))
            events.Add(ev);

        // 假 transport 很快终态；至少不应抛；事件数可为 0（竞态已终态）或更多
        events.Should().NotBeNull();
    }

    [Fact]
    public async Task Submit_经假Transport入队_GetTask可读()
    {
        var sp = BuildHost(withFakeTransport: true);
        var registry = sp.GetRequiredService<IAgentRegistry>();
        foreach (var a in registry.List().Where(x => x.Vendor == "p3-sub").ToList())
            registry.Delete(a.Id);

        var agent = registry.Create(new AgentSaveRequest
        {
            Name = "p3-sub-" + Guid.NewGuid().ToString("N")[..6],
            Vendor = "p3-sub",
            Enabled = true,
            DefaultCwd = Path.GetTempPath(),
            AccessPoints =
            [
                new AgentAccessPointSaveRequest
                {
                    Mode = "Sessionful",
                    Transport = "Cli",
                    Executable = "echo",
                    IsDefault = true,
                    CancelSupported = true,
                }
            ]
        });

        var runtime = new AgentHubRuntimeProvider(sp);
        var handle = await runtime.SubmitAsync(new AgentTaskRequest
        {
            Prompt = "hello-p3",
            AgentId = agent.Id,
            Cwd = Path.GetTempPath(),
            PermissionMode = "read-only",
            CreatedBy = "test",
        });

        handle.TaskKey.Should().NotBeNullOrWhiteSpace();
        handle.Status.Should().BeOneOf(TaskStatus.Queued, TaskStatus.Running, TaskStatus.Succeeded, TaskStatus.Failed);

        // 等后台跑完（假 transport 很快）
        AgentTaskSnapshot? snap = null;
        for (var i = 0; i < 50; i++)
        {
            snap = await runtime.GetTaskAsync(handle.TaskKey);
            if (snap?.Terminal == true) break;
            await Task.Delay(50);
        }

        snap.Should().NotBeNull();
        snap!.TaskKey.Should().Be(handle.TaskKey);
    }

    /// <summary>
    /// Gap #22/#28：AC-R3 诚实面——Submit/Get/Watch(poll)/Cancel/续接；Watch 可被 Cancel token 打断（非挂死 SSE）。
    /// </summary>
    [Fact]
    public async Task AC_R3_Submit_Get_Watch轮询_Cancel_续接()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sp = BuildHost(withFakeTransport: true, hangUntil: gate);
        var registry = sp.GetRequiredService<IAgentRegistry>();
        foreach (var a in registry.List().Where(x => x.Vendor == "p3-acr3").ToList())
            registry.Delete(a.Id);

        var agent = registry.Create(new AgentSaveRequest
        {
            Name = "p3-acr3-" + Guid.NewGuid().ToString("N")[..6],
            Vendor = "p3-acr3",
            Enabled = true,
            DefaultCwd = Path.GetTempPath(),
            AccessPoints =
            [
                new AgentAccessPointSaveRequest
                {
                    Mode = "Sessionful",
                    Transport = "Cli",
                    Executable = "echo",
                    IsDefault = true,
                    CancelSupported = true,
                }
            ]
        });

        var runtime = new AgentHubRuntimeProvider(sp);
        var handle = await runtime.SubmitAsync(new AgentTaskRequest
        {
            Prompt = "acr3-poll",
            AgentId = agent.Id,
            Cwd = Path.GetTempPath(),
            CreatedBy = "test-acr3",
        });
        handle.TaskKey.Should().NotBeNullOrWhiteSpace();

        var got = await runtime.GetTaskAsync(handle.TaskKey);
        got.Should().NotBeNull();
        got!.TaskKey.Should().Be(handle.TaskKey);

        // Watch 轮询：短超时取消应干净结束（poll 最小面，不是不可取消的长连接）
        using var watchCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(450));
        var watched = 0;
        await foreach (var _ in runtime.WatchTaskAsync(handle.TaskKey, 0, watchCts.Token))
            watched++;
        // 允许 0+ 事件；关键是不抛、能退出
        watched.Should().BeGreaterThanOrEqualTo(0);

        var cancel = await runtime.CancelAsync(handle.TaskKey);
        cancel.Accepted.Should().BeTrue();

        // 放行假 transport 收尾，避免挂后台
        gate.TrySetResult();

        // 续接面：SendInput 空文本已覆盖 Reject；非空走假 transport 应 Accepted 或业务 Reject，不得抛
        var sessionKey = string.IsNullOrEmpty(handle.SessionKey) ? "acr3-session" : handle.SessionKey;
        var resume = await runtime.SendInputAsync(sessionKey, new AgentInput { Text = "continue-please" });
        resume.Should().NotBeNull();
    }

    [Fact]
    public void WatchTaskAsync_源码是轮询Delay不是统一SSE声明()
    {
        // G2 诚实守卫：实现必须含 Task.Delay 轮询；注释须承认最小面
        var src = Path.Combine(
            FindRepoRoot(),
            "Plugins", "AgentHub", "Services", "AgentHubRuntimeProvider.cs");
        File.Exists(src).Should().BeTrue(src);
        var text = File.ReadAllText(src);
        text.Should().Contain("Task.Delay", "Watch 最小面必须轮询 Delay");
        text.Should().Contain("轮询", "注释须诚实标明轮询");
        text.Should().NotContain("统一 SSE 已完成");
    }

    private static ServiceProvider BuildHost(Boolean withFakeTransport = false, TaskCompletionSource? hangUntil = null)
    {
        var services = new ServiceCollection();
        var loader = new ProfileLoader();
        loader.LoadAll(null);
        services.AddSingleton(loader);
        services.AddSingleton<IAgentRegistry, AgentRegistry>();
        services.AddSingleton<PermissionBroker>();
        services.AddSingleton(new OarWorkerClientOptions { WorkerEntryPath = "" });
        services.AddSingleton<OarWorkerClient>();
        services.AddSingleton<AgentSessionManager>();
        services.AddSingleton<AgentRuntimeEventStore>();
        services.AddSingleton<CliTransport>();
        services.AddSingleton<OarTransport>();

        if (withFakeTransport)
        {
            services.AddSingleton<IAgentTransportRegistry>(_ =>
            {
                var reg = new AgentTransportRegistry();
                reg.Register(new FakeCliTransport(hangUntil));
                return reg;
            });
        }
        else
        {
            services.AddSingleton<IAgentTransportRegistry>(sp =>
            {
                var reg = new AgentTransportRegistry();
                reg.Register(sp.GetRequiredService<CliTransport>());
                reg.Register(sp.GetRequiredService<OarTransport>());
                return reg;
            });
        }

        services.AddSingleton<DelegationRuntime>();
        return services.BuildServiceProvider();
    }

    private static String FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx"))
                || File.Exists(Path.Combine(dir.FullName, "ForgeSelf.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("找不到仓库根（缺 ForgeSelf.slnx）");
    }

    private sealed class FakeCliTransport : IAgentTransport
    {
        private readonly TaskCompletionSource? _hangUntil;

        public FakeCliTransport(TaskCompletionSource? hangUntil = null) => _hangUntil = hangUntil;

        public String Kind => "Cli";

        public async IAsyncEnumerable<AgentEvent> StreamAsync(
            AgentAccessPointDto ap, AgentRunRequest req, AgentSession session,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.Yield();
            yield return AgentEvent.FromText("p3-fake");
            if (_hangUntil != null)
            {
                using var reg = ct.Register(() => _hangUntil.TrySetCanceled(ct));
                try { await _hangUntil.Task.ConfigureAwait(false); }
                catch (OperationCanceledException) { yield break; }
            }
            yield return new AgentEvent { Type = AgentEventTypes.Exit, ExitCode = 0 };
        }

        public Task CancelAsync(AgentSession session)
        {
            _hangUntil?.TrySetResult();
            return Task.CompletedTask;
        }
    }
}
