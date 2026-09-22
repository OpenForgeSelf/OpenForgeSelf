using FluentAssertions;
using ForgeSelf.Api.Plugins.AgentHub.Entities;
using ForgeSelf.Api.Plugins.AgentHub.Models;
using ForgeSelf.Api.Plugins.AgentHub.Profiles;
using ForgeSelf.Api.Plugins.AgentHub.Services;
using XCode;
using XCode.DataAccessLayer;
using Xunit;
// 二义性消解：插件自有的 TaskStatus（任务状态常量）与 System.Threading.Tasks.TaskStatus 同名
using TaskStatus = ForgeSelf.Api.Plugins.AgentHub.Services.TaskStatus;

namespace ForgeSelf.Api.Tests.Plugins.AgentHub;

/// <summary>
/// DelegationRuntime 前置校验与收尾测试（design §6 的 G5/G6/G7）。
///
/// 这里的断言全部通过**公开 API** 观察（CreateAsync / RecoverOrphans / ListEvents），
/// 不碰私有方法——避免把测试钉死在实现细节上。
///
/// 覆盖要点：
/// - G5：宿主重启后遗留的非终态任务必须被标为 Interrupted（否则永远卡 Running）；
/// - G6/G7：cwd 白名单外的目录直接拒绝，**不进进程**（Fail 返回，不抛）；
/// - 前置校验：空提示词、停用 agent、无交互口、危险权限模式均被拒且给出可读原因；
/// - 事件流：入队即落 Meta 事件（stage=queued），事件 Seq 单调递增（SSE 续读依赖）；
/// - 选路失败时给出「没有可用候选」而非静默。
///
/// 库管理方式：用 <see cref="XCodeTestFixture"/>（每类一份临时库），**不要**在本类内调用
/// <c>DAL.AddConnStr("AgentHub", ...)</c> —— DAL 连接串是**进程级**共享的，本类覆盖会让
/// 其它 AgentHub 测试类读到本类的库，表现为「厂商标识已被占用」这类串扰错误。
/// 实体表的创建由 fixture 的 EnsureTablesCreated（InitConnection）完成；
/// 生产路径上则由 AgentHubPlugin.EnsureTablesCreated 自行建表（插件 DLL 晚于宿主建表加载）。
/// </summary>
[Collection("XCode")]
public class AgentHubDelegationRuntimeTests : IClassFixture<XCodeTestFixture>
{
    public AgentHubDelegationRuntimeTests(XCodeTestFixture fixture)
    {
        _ = fixture;

        // 前置自检：表必须真的存在。若此断言失败，说明建表链路（fixture/插件）没生效，
        // 应去修建表而不是在测试里绕——避免把环境问题伪装成业务失败。
        var count = DelegationTask.FindCount();
        count.Should().BeGreaterThanOrEqualTo(0, "DelegationTask 表必须可查（表存在即通过，0 行也合法）");

        // XCode 实体级缓存为进程级全局单例，跨测试共享；建表后清缓存保证读到的是本库数据。
        AgentDefinition.Meta.Cache.Clear("test reset");
        AgentAccessPoint.Meta.Cache.Clear("test reset");
        DelegationTask.Meta.Cache.Clear("test reset");
        DelegationEvent.Meta.Cache.Clear("test reset");
    }

    /// <summary>用假 transport 构造 runtime（不做真实进程调用）。</summary>
    private static DelegationRuntime MakeRuntime(out AgentRegistry registry)
    {
        var loader = new ProfileLoader();
        loader.LoadAll(null);
        registry = new AgentRegistry(loader);
        var broker = new PermissionBroker(registry);
        return new DelegationRuntime(registry, loader, broker, new FakeTransport());
    }

    private static String Uniq(String p) => $"{p}-{Guid.NewGuid():N}"[..16];

    /// <summary>登记一个 agent 并返回其 Id；同时清掉该 vendor 的历史行避免唯一约束冲突。</summary>
    private static Int32 MakeAgent(AgentRegistry registry, String vendor, String? defaultCwd = null, Boolean enabled = true)
    {
        foreach (var a in registry.List().Where(a => a.Vendor == vendor).ToList())
        {
            registry.Delete(a.Id);
        }

        var dto = registry.Create(new AgentSaveRequest
        {
            Name = Uniq("rt"),
            Vendor = vendor,
            Enabled = enabled,
            DefaultCwd = defaultCwd
        });
        return dto.Id;
    }

    [Fact]
    public async Task G7_工作目录不在白名单_直接拒绝不进进程()
    {
        var runtime = MakeRuntime(out var registry);
        var allowedDir = Path.Combine(Path.GetTempPath(), "agenthub-allow");
        var otherDir = Path.Combine(Path.GetTempPath(), "agenthub-other");
        Directory.CreateDirectory(allowedDir);
        Directory.CreateDirectory(otherDir);

        try
        {
            var agentId = MakeAgent(registry, "opencode", allowedDir);
            // 收紧白名单：只允许 allowedDir
            registry.Update(agentId, new AgentSaveRequest
            {
                Policy = new AgentPolicy { AllowedCwds = [allowedDir] }
            });

            var task = await runtime.CreateAsync(new DelegationRequest
            {
                AgentId = agentId,
                Prompt = "做点事",
                Cwd = otherDir
            });

            // 安全铁证：白名单外的目录必须在**创建阶段**就被挡下，不能进到进程
            task.Status.Should().Be(TaskStatus.Failed);
            task.Message.Should().Contain("白名单");
        }
        finally
        {
            // 数据安全铁律：测试自建目录只创建、不自动删除
        }
    }

    [Fact]
    public async Task G7_工作目录不存在_拒绝()
    {
        var runtime = MakeRuntime(out var registry);
        var agentId = MakeAgent(registry, "opencode");

        var bogus = Path.Combine(Path.GetTempPath(), $"no-such-{Guid.NewGuid():N}");

        var task = await runtime.CreateAsync(new DelegationRequest
        {
            AgentId = agentId,
            Prompt = "做点事",
            Cwd = bogus
        });

        task.Status.Should().Be(TaskStatus.Failed);
        task.Message.Should().Contain("不存在");
    }

    [Fact]
    public async Task G7_目录在白名单内_放行到排队态()
    {
        var runtime = MakeRuntime(out var registry);
        var dir = Path.Combine(Path.GetTempPath(), $"agenthub-ok-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        try
        {
            var agentId = MakeAgent(registry, "opencode", dir);
            registry.Update(agentId, new AgentSaveRequest
            {
                Policy = new AgentPolicy { AllowedCwds = [dir] }
            });

            var task = await runtime.CreateAsync(new DelegationRequest
            {
                AgentId = agentId,
                Prompt = "做点事",
                Cwd = dir
            });

            task.Status.Should().Be(TaskStatus.Queued);
        }
        finally
        {
            // 数据安全铁律：测试自建目录只创建、不自动删除
        }
    }

    [Fact]
    public async Task 空提示词_拒绝()
    {
        var runtime = MakeRuntime(out var registry);
        var agentId = MakeAgent(registry, "opencode");

        var task = await runtime.CreateAsync(new DelegationRequest { AgentId = agentId, Prompt = "   " });

        task.Status.Should().Be(TaskStatus.Failed);
        task.Message.Should().Contain("提示词");
    }

    [Fact]
    public async Task 目标agent不存在_拒绝并指名()
    {
        var runtime = MakeRuntime(out _);

        var task = await runtime.CreateAsync(new DelegationRequest { AgentId = 999999, Prompt = "做点事" });

        task.Status.Should().Be(TaskStatus.Failed);
        task.Message.Should().Contain("999999");
    }

    [Fact]
    public async Task 目标agent已停用_拒绝()
    {
        var runtime = MakeRuntime(out var registry);
        var agentId = MakeAgent(registry, "claude", enabled: false);

        var task = await runtime.CreateAsync(new DelegationRequest { AgentId = agentId, Prompt = "做点事" });

        task.Status.Should().Be(TaskStatus.Failed);
        task.Message.Should().Contain("已停用");
    }

    [Fact]
    public async Task 无交互口_拒绝并提示()
    {
        var runtime = MakeRuntime(out var registry);
        foreach (var a in registry.List().Where(a => a.Vendor == "qodercli").ToList())
        {
            registry.Delete(a.Id);
        }
        // 显式不建交互口（空数组）
        var dto = registry.Create(new AgentSaveRequest
        {
            Name = Uniq("noap"),
            Vendor = "qodercli",
            AccessPoints = []
        });

        var task = await runtime.CreateAsync(new DelegationRequest { AgentId = dto.Id, Prompt = "做点事" });

        task.Status.Should().Be(TaskStatus.Failed);
        task.Message.Should().Contain("交互口");

        registry.Delete(dto.Id);
    }

    [Theory]
    [InlineData("yolo")]
    [InlineData("danger-full-access")]
    public async Task 危险权限模式_拒绝(String mode)
    {
        var runtime = MakeRuntime(out var registry);
        var agentId = MakeAgent(registry, "opencode");

        var task = await runtime.CreateAsync(new DelegationRequest
        {
            AgentId = agentId,
            Prompt = "做点事",
            PermissionMode = mode
        });

        task.Status.Should().Be(TaskStatus.Failed);
        task.Message.Should().Contain("禁止");
    }

    [Fact]
    public async Task 选路无候选_给出可操作提示()
    {
        var runtime = MakeRuntime(out var registry);
        // 清空全部 agent，保证选路必然无候选
        foreach (var a in registry.List().ToList()) registry.Delete(a.Id);

        var task = await runtime.CreateAsync(new DelegationRequest { Prompt = "做点事" });

        task.Status.Should().Be(TaskStatus.Failed);
        task.Message.Should().Contain("候选");
    }

    [Fact]
    public async Task 入队即落Meta事件_含stage等于queued()
    {
        var runtime = MakeRuntime(out var registry);
        var agentId = MakeAgent(registry, "opencode");

        var task = await runtime.CreateAsync(new DelegationRequest { AgentId = agentId, Prompt = "做点事" });

        task.Status.Should().Be(TaskStatus.Queued);

        var events = DelegationRuntime.ListEvents(task.Id, 0);
        events.Should().NotBeEmpty("入队必须留痕，否则 SSE 接上时一片空白");

        var first = events[0];
        first.Type.Should().Be(AgentEventTypes.Meta);
        first.PayloadJson.Should().Contain("queued");
        first.Seq.Should().BeGreaterThan(0, "SSE 断线续读依赖 Seq > 0");
    }

    [Fact]
    public async Task 事件Seq单调递增_供SSE续读()
    {
        var runtime = MakeRuntime(out var registry);
        var agentId = MakeAgent(registry, "opencode");

        var t1 = await runtime.CreateAsync(new DelegationRequest { AgentId = agentId, Prompt = "第一个" });
        var t2 = await runtime.CreateAsync(new DelegationRequest { AgentId = agentId, Prompt = "第二个" });

        var e1 = DelegationRuntime.ListEvents(t1.Id, 0);
        var e2 = DelegationRuntime.ListEvents(t2.Id, 0);

        // 任务各自独立编号，但同一任务内必须严格递增
        e1.Select(e => e.Seq).Should().BeInAscendingOrder();
        e2.Select(e => e.Seq).Should().BeInAscendingOrder();
    }

    [Fact]
    public void G5_宿主重启_遗留非终态任务被标Interrupted()
    {
        var runtime = MakeRuntime(out var registry);

        // 造一个「遗留 Running」的任务（模拟宿主崩在运行中），直接落库
        var agentId = MakeAgent(registry, "opencode");
        var apId = registry.GetDefaultAccessPoint(agentId)!.Id;

        var entity = new DelegationTask
        {
            AgentId = agentId,
            AccessPointId = apId,
            Prompt = "宿主重启前没跑完的任务",
            Status = TaskStatus.Running,
            PermissionMode = "read-only",
            CreatedBy = "test"
        };
        entity.Insert();

        var recovered = runtime.RecoverOrphans();

        recovered.Should().BeGreaterThan(0, "库里存在非终态任务时必须收尾");

        var after = DelegationTask.FindById(entity.Id)!;
        after.Status.Should().Be(TaskStatus.Interrupted);
        after.ErrorCode.Should().Be("host_restart");
        after.EndTime.Should().BeAfter(DateTime.MinValue, "收尾要写上结束时间，否则耗时统计为负");

        registry.Delete(agentId);
    }

    [Fact]
    public void G5_无遗留任务_收尾返回0()
    {
        var runtime = MakeRuntime(out _);

        // 先把现有非终态任务都收掉，再跑一次必须是 0（幂等）
        runtime.RecoverOrphans();
        runtime.RecoverOrphans().Should().Be(0, "收尾必须幂等，重复调用不再改任何行");
    }

    [Fact]
    public async Task List_按状态过滤_且带上agent名()
    {
        var runtime = MakeRuntime(out var registry);
        var agentId = MakeAgent(registry, "opencode");

        var task = await runtime.CreateAsync(new DelegationRequest { AgentId = agentId, Prompt = "过滤用" });

        var queued = runtime.List(TaskStatus.Queued, 200);
        queued.Should().Contain(t => t.Id == task.Id);
        queued.First(t => t.Id == task.Id).AgentName.Should().NotBeNullOrEmpty("UI 列表要直接显示 agent 名");
    }

    [Fact]
    public async Task GetByKey_按对外标识取到同一条()
    {
        var runtime = MakeRuntime(out var registry);
        var agentId = MakeAgent(registry, "opencode");

        var task = await runtime.CreateAsync(new DelegationRequest { AgentId = agentId, Prompt = "按 key 取" });

        var byKey = runtime.GetByKey(task.TaskKey);
        byKey.Should().NotBeNull();
        byKey!.Id.Should().Be(task.Id);
    }

    [Fact]
    public async Task Cancel_对不存在或已终态任务_返回false()
    {
        var runtime = MakeRuntime(out _);

        var ok = await runtime.CancelAsync(987654);
        ok.Should().BeFalse("取消不存在的任务必须如实返回 false，不能假装成功");
    }

    [Fact]
    public void Stats_包含各状态计数与总数()
    {
        var runtime = MakeRuntime(out _);

        var stats = DelegationRuntime.Stats();

        stats.Should().ContainKey("Total");
        stats.Should().ContainKey(TaskStatus.Queued);
        stats.Should().ContainKey(TaskStatus.Running);
        stats.Should().ContainKey(TaskStatus.AwaitingPermission);
    }

    // ──────────────────── 测试替身 ────────────────────

    /// <summary>假 transport：不真起进程，只吐一条固定事件（本类只测前置校验与收尾）。</summary>
    private sealed class FakeTransport : IAgentTransport
    {
        public String Kind => "Fake";

        public async IAsyncEnumerable<AgentEvent> StreamAsync(
            AgentAccessPointDto ap, AgentRunRequest req, AgentSession session,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.Yield();
            yield return AgentEvent.FromText("fake output");
        }

        public Task CancelAsync(AgentSession session) => Task.CompletedTask;
    }
}
