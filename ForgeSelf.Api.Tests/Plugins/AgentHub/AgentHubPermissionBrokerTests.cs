using System;
using FluentAssertions;
using ForgeSelf.Api.Plugins.AgentHub.Models;
using ForgeSelf.Api.Plugins.AgentHub.Profiles;
using ForgeSelf.Api.Plugins.AgentHub.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AgentHub;

/// <summary>
/// PermissionBroker 权限中枢测试（A+C 模型，design §7）。
///
/// 覆盖要点——每一条都是「宁可拒绝也不误放」的安全约束：
/// - 未授信 → 挂起等人工，**不自动放行**；
/// - 授信且类别命中范围 → 自动放行（Source=trusted）；
/// - 授信但类别**不在**范围内 → 仍走人工（授信不是全权）；
/// - 人工放行恒为「仅本次」（OnceOnly=true，不记忆）；
/// - 审批超时 → 按**拒绝**处理（绝不默认放行）；
/// - CancelAll → 挂起的申请全部作废（防挂死）；
/// - 裁决非授权路径 → 不能凭空放行。
/// </summary>
[Collection("XCode")]
public class AgentHubPermissionBrokerTests : IClassFixture<XCodeTestFixture>
{
    private readonly XCodeTestFixture _fixture;

    public AgentHubPermissionBrokerTests(XCodeTestFixture fixture)
    {
        _fixture = fixture;
    }

    private static AgentRegistry MakeRegistry()
    {
        var loader = new ProfileLoader();
        loader.LoadAll(null);
        return new AgentRegistry(loader);
    }

    /// <summary>登记一个指定 vendor 的 agent，返回 (registry, broker, agentId)。</summary>
    private static (AgentRegistry Registry, PermissionBroker Broker, Int32 AgentId) Setup(String vendor)
    {
        var registry = MakeRegistry();
        foreach (var a in registry.List().Where(a => a.Vendor == vendor).ToList())
        {
            registry.Delete(a.Id);
        }

        var dto = registry.Create(new AgentSaveRequest
        {
            Name = $"pb-{Guid.NewGuid():N}"[..16],
            Vendor = vendor
        });
        return (registry, new PermissionBroker(registry), dto.Id);
    }

    [Fact]
    public async Task 未授信_权限申请挂起等人工_不自动放行()
    {
        var (registry, broker, agentId) = Setup("opencode");

        // 起一个无人答复的申请，超时设 1 秒——必须落到「拒绝」而非「允许」
        var verdict = await broker.RequestAsync(
            taskId: 9001, agentId: agentId, kind: "write_file",
            detail: @"D:\x\a.cs", timeoutSeconds: 1);

        verdict.Allowed.Should().BeFalse("未授信且无人审批，安全侧必须拒绝");
        verdict.Source.Should().Be("timeout");
        verdict.OnceOnly.Should().BeTrue();

        registry.Delete(agentId);
    }

    [Fact]
    public async Task 授信且类别命中范围_自动放行()
    {
        var (registry, broker, agentId) = Setup("opencode");
        registry.SetTrust(agentId, true, ["write_file", "exec_command"]);

        var verdict = await broker.RequestAsync(
            taskId: 9002, agentId: agentId, kind: "write_file", detail: @"D:\x\a.cs");

        verdict.Allowed.Should().BeTrue();
        verdict.Source.Should().Be("trusted", "命中授信范围应是自动放行");
        verdict.OnceOnly.Should().BeFalse("授信放行是持续的（范围不变则一直放行）");

        registry.Delete(agentId);
    }

    [Fact]
    public async Task 授信但类别不在范围内_仍走人工不自动放行()
    {
        var (registry, broker, agentId) = Setup("opencode");
        // 只授信了写文件，没有授信执行命令
        registry.SetTrust(agentId, true, ["write_file"]);

        var verdict = await broker.RequestAsync(
            taskId: 9003, agentId: agentId, kind: "exec_command",
            detail: "rm -rf /tmp/x", timeoutSeconds: 1);

        // 安全铁证：授信 ≠ 全权，范围外的申请不得蹭授信自动放行
        verdict.Allowed.Should().BeFalse();
        verdict.Source.Should().Be("timeout", "范围外应挂起等人工，无人答则超时拒绝");

        registry.Delete(agentId);
    }

    [Fact]
    public async Task 人工放行_仅本次_OnceOnly恒为真()
    {
        var (registry, broker, agentId) = Setup("opencode");

        // 后台起申请，主线程待其进入挂起态后由「审批台」放行
        var requestTask = broker.RequestAsync(
            taskId: 9004, agentId: agentId, kind: "write_file",
            detail: @"D:\x\b.cs", timeoutSeconds: 30);

        var pending = await WaitForPending(broker, 9004);
        pending.Should().NotBeNull("申请应进入待审批列表，供审批台看到");

        broker.Resolve(9004, pending!.RequestId, allowed: true, note: "这次可以").Should().BeTrue();

        var verdict = await requestTask;
        verdict.Allowed.Should().BeTrue();
        verdict.Source.Should().Be("human");
        verdict.OnceOnly.Should().BeTrue("A+C：人工放行只对本次有效，不做长期记忆");
        verdict.Reason.Should().Be("这次可以");

        registry.Delete(agentId);
    }

    [Fact]
    public async Task 人工拒绝_按拒绝返回()
    {
        var (registry, broker, agentId) = Setup("opencode");

        var requestTask = broker.RequestAsync(
            taskId: 9005, agentId: agentId, kind: "exec_command",
            detail: "curl http://x", timeoutSeconds: 30);

        var pending = await WaitForPending(broker, 9005);
        broker.Resolve(9005, pending!.RequestId, allowed: false).Should().BeTrue();

        var verdict = await requestTask;
        verdict.Allowed.Should().BeFalse();
        verdict.Source.Should().Be("human");

        registry.Delete(agentId);
    }

    [Fact]
    public async Task 裁决不存在的申请_返回false不抛()
    {
        var (registry, broker, agentId) = Setup("opencode");

        // 陌生 taskId / 陌生 requestId 都不能「凭空」制造放行
        broker.Resolve(99999, "no-such-request", allowed: true).Should().BeFalse();
        await Task.CompletedTask;

        registry.Delete(agentId);
    }

    [Fact]
    public async Task CancelAll_挂起的申请全部作废_按拒绝返回且不挂死()
    {
        var (registry, broker, agentId) = Setup("opencode");

        var t1 = broker.RequestAsync(9006, agentId, "write_file", "a.cs", timeoutSeconds: 60);
        var t2 = broker.RequestAsync(9006, agentId, "exec_command", "ls", timeoutSeconds: 60);

        await WaitForPending(broker, 9006, expected: 2);
        broker.PendingCount.Should().Be(2);

        // 任务被取消 → 所有待审批必须立即结算，否则执行侧永久 await
        broker.CancelAll(9006);

        var v1 = await t1;
        var v2 = await t2;
        v1.Allowed.Should().BeFalse();
        v2.Allowed.Should().BeFalse();
        v1.Source.Should().Be("cancelled");
        broker.PendingCount.Should().Be(0, "作废后不得残留待审批项（UI 徽标会虚高）");

        registry.Delete(agentId);
    }

    [Fact]
    public async Task 结算后_待审批列表移除该项()
    {
        var (registry, broker, agentId) = Setup("opencode");

        var requestTask = broker.RequestAsync(9007, agentId, "write_file", "c.cs", timeoutSeconds: 30);
        var pending = await WaitForPending(broker, 9007);

        broker.Resolve(9007, pending!.RequestId, allowed: true);
        await requestTask;

        // 等一小会儿让 finally 里的清理跑完
        await Task.Delay(50);
        broker.ListPending(9007).Should().BeEmpty();

        registry.Delete(agentId);
    }

    [Fact]
    public void PendingCount_反映所有任务的待审批总数()
    {
        var (registry, broker, agentId) = Setup("opencode");

        broker.PendingCount.Should().Be(0);

        registry.Delete(agentId);
    }

    /// <summary>轮询等待某个任务出现 N 个待审批项（避免依赖固定 sleep 的脆弱写法）。</summary>
    private static async Task<PendingPermission?> WaitForPending(
        PermissionBroker broker, Int32 taskId, Int32 expected = 1)
    {
        for (var i = 0; i < 200; i++)
        {
            var list = broker.ListPending(taskId);
            if (list.Count >= expected) return list[0];
            await Task.Delay(10);
        }
        return null;
    }
}
