using FluentAssertions;
using ForgeSelf.Api.Plugins.AgentHub.Models;
using ForgeSelf.Api.Plugins.AgentHub.Profiles;
using ForgeSelf.Api.Plugins.AgentHub.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AgentHub;

/// <summary>
/// AgentRegistry 注册表测试：登记、去重、profile 预填、授信、危险权限拒绝。
///
/// 覆盖要点（design §6 / §7）：
/// - 同名 / 同 vendor 重复登记必须被拒（多份同 CLI 会让职责不清）；
/// - 不传交互口 → 按 profile 预填；传空数组 → 显式不建；
/// - 危险权限模式（yolo / danger-full-access）**任何情况下不得作为默认值写入**；
/// - 授信必须带范围——空范围等于「全权」，必须拒绝（A+C 模型的安全底线）；
/// - 选路按能力面 + 标签过滤，且只返回启用的。
/// </summary>
[Collection("XCode")]
public class AgentHubRegistryTests : IClassFixture<XCodeTestFixture>
{
    private readonly XCodeTestFixture _fixture;

    public AgentHubRegistryTests(XCodeTestFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>构造一个指向真实内置 profile 目录的加载器（AssemblyBaseDir 下的 Data/Profiles）。</summary>
    private static AgentRegistry MakeRegistry()
    {
        var loader = new ProfileLoader();
        loader.LoadAll(null);
        return new AgentRegistry(loader);
    }

    /// <summary>生成唯一名称，避免同一测试库内多次登记撞唯一约束。</summary>
    private static String Uniq(String prefix) => $"{prefix}-{Guid.NewGuid():N}"[..20];

    [Fact]
    public void Create_不传交互口_按profile预填默认交互口()
    {
        var registry = MakeRegistry();
        var vendor = "opencode";
        PurgeVendor(registry, vendor);

        var dto = registry.Create(new AgentSaveRequest
        {
            Name = Uniq("oc"),
            Vendor = vendor
        });

        dto.Should().NotBeNull();
        // profile 里声明了 executable / argsTemplate，预填后交互口必须非空且带上可执行文件
        dto!.AccessPoints.Should().NotBeEmpty("不传交互口时按 profile 预填");
        dto.AccessPoints.Should().OnlyContain(ap => !String.IsNullOrEmpty(ap.Executable));
        dto.AccessPoints.Should().Contain(ap => ap.IsDefault, "必须有一个默认交互口");

        registry.Delete(dto.Id);
    }

    [Fact]
    public void Create_显式传空交互口数组_不建任何交互口()
    {
        var registry = MakeRegistry();
        var vendor = "claude";
        PurgeVendor(registry, vendor);

        var dto = registry.Create(new AgentSaveRequest
        {
            Name = Uniq("cl"),
            Vendor = vendor,
            AccessPoints = []   // 空数组 = 显式不建（区别于 null = 按 profile 预填）
        });

        dto!.AccessPoints.Should().BeEmpty("空数组表示显式清空，与「不传」语义不同");

        registry.Delete(dto.Id);
    }

    [Fact]
    public void Create_同名重复登记_被拒绝()
    {
        var registry = MakeRegistry();
        var vendor = "qodercli";
        PurgeVendor(registry, vendor);
        var name = Uniq("dup");

        var first = registry.Create(new AgentSaveRequest { Name = name, Vendor = vendor });

        var act = () => registry.Create(new AgentSaveRequest { Name = name, Vendor = "custom-x" });

        act.Should().Throw<ArgumentException>().WithMessage("*已存在同名*");

        registry.Delete(first.Id);
    }

    [Fact]
    public void Create_同vendor重复登记_被拒绝()
    {
        var registry = MakeRegistry();
        var vendor = "codex";
        PurgeVendor(registry, vendor);

        var first = registry.Create(new AgentSaveRequest { Name = Uniq("cx"), Vendor = vendor });

        // 同一份 CLI 装两个实例会让「选路」语义不清，故按 vendor 唯一
        var act = () => registry.Create(new AgentSaveRequest { Name = Uniq("cx2"), Vendor = vendor });

        act.Should().Throw<ArgumentException>().WithMessage("*已被 Agent*占用*");

        registry.Delete(first.Id);
    }

    [Theory]
    [InlineData("yolo")]
    [InlineData("danger-full-access")]
    [InlineData("dangerously-skip-permissions")]
    public void Create_危险权限模式_一律拒绝不得作为默认(String mode)
    {
        var registry = MakeRegistry();

        var act = () => registry.Create(new AgentSaveRequest
        {
            Name = Uniq("danger"),
            Vendor = Uniq("vd"),
            Policy = new AgentPolicy { PermissionMode = mode }
        });

        // 安全铁证：危险模式永远不能成为 agent 的默认权限
        act.Should().Throw<ArgumentException>().WithMessage("*禁止使用*");
    }

    [Fact]
    public void Create_未指定策略_默认只读()
    {
        var registry = MakeRegistry();
        var vendor = "claude";
        PurgeVendor(registry, vendor);

        var dto = registry.Create(new AgentSaveRequest { Name = Uniq("ro"), Vendor = vendor });

        dto!.Policy.PermissionMode.Should().Be("read-only", "默认必须是最安全的一档");

        registry.Delete(dto.Id);
    }

    [Fact]
    public void Create_未指定能力矩阵_按profile填充六个能力面()
    {
        var registry = MakeRegistry();
        var vendor = "opencode";
        PurgeVendor(registry, vendor);

        var dto = registry.Create(new AgentSaveRequest { Name = Uniq("cap"), Vendor = vendor });

        // 矩阵必须逐格显式（有值即 true/false），不留白
        dto!.Capabilities.Facets.Should().NotBeEmpty("能力矩阵按 profile 预填");
        dto.Capabilities.Supports(AgentFacets.Driving).Should().BeTrue("opencode 支持委派执行");

        registry.Delete(dto.Id);
    }

    [Fact]
    public void SetTrust_带范围_授信成功且范围落库()
    {
        var registry = MakeRegistry();
        var vendor = "opencode";
        PurgeVendor(registry, vendor);
        var dto = registry.Create(new AgentSaveRequest { Name = Uniq("tr"), Vendor = vendor });

        var updated = registry.SetTrust(dto!.Id, true, ["write_file", "exec_command"]);

        updated!.Trusted.Should().BeTrue();
        updated.TrustedScopes.Should().BeEquivalentTo(["write_file", "exec_command"]);

        // 重新读一遍，确认真的落库而非只在内存
        var reloaded = registry.Get(dto.Id);
        reloaded!.Trusted.Should().BeTrue();
        reloaded.TrustedScopes.Should().BeEquivalentTo(["write_file", "exec_command"]);

        registry.Delete(dto.Id);
    }

    [Fact]
    public void SetTrust_空范围_被拒绝()
    {
        var registry = MakeRegistry();
        var vendor = "claude";
        PurgeVendor(registry, vendor);
        var dto = registry.Create(new AgentSaveRequest { Name = Uniq("noScope"), Vendor = vendor });

        // 安全铁证：无范围授信 = 全权，必须拒绝
        var actNull = () => registry.SetTrust(dto!.Id, true, null);
        var actEmpty = () => registry.SetTrust(dto!.Id, true, []);

        actNull.Should().Throw<ArgumentException>().WithMessage("*必须指定范围*");
        actEmpty.Should().Throw<ArgumentException>().WithMessage("*必须指定范围*");

        registry.Delete(dto.Id);
    }

    [Fact]
    public void SetTrust_取消授信_范围被清空()
    {
        var registry = MakeRegistry();
        var vendor = "opencode";
        PurgeVendor(registry, vendor);
        var dto = registry.Create(new AgentSaveRequest { Name = Uniq("rev"), Vendor = vendor });

        registry.SetTrust(dto!.Id, true, ["read_file"]);
        var revoked = registry.SetTrust(dto.Id, false, null);

        revoked!.Trusted.Should().BeFalse();
        revoked.TrustedScopes.Should().BeEmpty("取消授信后残留范围会误导 UI");

        registry.Delete(dto.Id);
    }

    [Fact]
    public void SelectCandidates_按能力面过滤_且不返回停用的()
    {
        var registry = MakeRegistry();
        var vendor = "codex";
        PurgeVendor(registry, vendor);

        var enabled = registry.Create(new AgentSaveRequest
        {
            Name = Uniq("sel-on"),
            Vendor = vendor,
            Enabled = true
        });
        var disabled = registry.Create(new AgentSaveRequest
        {
            Name = Uniq("sel-off"),
            // vendor 唯一，这里换一个未占用的自定义 vendor 名
            Vendor = Uniq("vd"),
            Enabled = false
        });

        var candidates = registry.SelectCandidates(AgentFacets.Driving, null);

        candidates.Should().NotContain(a => a.Id == disabled!.Id, "停用的 agent 不得进入选路候选");
        // codex profile 声明支持 F1_Driving，故启用的那个应在候选里
        candidates.Should().Contain(a => a.Id == enabled!.Id);

        registry.Delete(enabled.Id);
        registry.Delete(disabled.Id);
    }

    [Fact]
    public void Delete_级联删除交互口()
    {
        var registry = MakeRegistry();
        var vendor = "opencode";
        PurgeVendor(registry, vendor);

        var dto = registry.Create(new AgentSaveRequest { Name = Uniq("cas"), Vendor = vendor });
        var apId = dto!.AccessPoints[0].Id;

        registry.Delete(dto.Id).Should().BeTrue();

        registry.Get(dto.Id).Should().BeNull();
        registry.GetAccessPoint(apId).Should().BeNull("删除 agent 必须级联清掉交互口，否则留下孤儿行");
    }

    [Fact]
    public void UpdateHealth_回写健康状态与版本()
    {
        var registry = MakeRegistry();
        var vendor = "claude";
        PurgeVendor(registry, vendor);
        var dto = registry.Create(new AgentSaveRequest { Name = Uniq("hl"), Vendor = vendor });
        var apId = dto!.AccessPoints[0].Id;

        registry.UpdateHealth(apId, "Degraded", "0.9.1", "profile 断言未通过");

        var ap = registry.GetAccessPoint(apId);
        ap!.Health.Should().Be("Degraded");
        ap.LastVersion.Should().Be("0.9.1");
        ap.LastError.Should().Contain("profile 断言");

        registry.Delete(dto.Id);
    }

    /// <summary>清掉某 vendor 的历史登记，避免同 vendor 唯一约束让测试相互干扰。</summary>
    private static void PurgeVendor(AgentRegistry registry, String vendor)
    {
        foreach (var a in registry.List().Where(a => a.Vendor == vendor).ToList())
        {
            registry.Delete(a.Id);
        }
    }
}
