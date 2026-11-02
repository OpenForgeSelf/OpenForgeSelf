using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Controllers;
using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Services;
using ForgeSelf.Api.Tests.Plugins;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// B4 权限策略：实体落库（AC2）、零配置红线（AC3）、策略生效（AC4）、
/// 闸门消费（AC5 管线级）、审计可查（AC7 端点级）、防双问服务面（AC8）。
/// </summary>
[Collection("XCode")]
public class PermissionProfileServiceTests
{
    private readonly PermissionProfileService _svc = new();

    /// <summary>与宿主 JSON 配置一致（camelCase），供控制器匿名对象断言用。</summary>
    private static readonly JsonSerializerOptions CamelJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public PermissionProfileServiceTests()
    {
        _ = new XCodeTestFixture();
        PermissionProfile.EnsureCreated();
        PermissionDecisionLog.EnsureCreated();
        foreach (var row in PermissionProfile.FindAll().ToArray()) row.Delete();
        foreach (var row in PermissionDecisionLog.FindAll().ToArray()) row.Delete();
        foreach (var row in ToolApproval.FindAll().ToArray()) row.Delete();
    }

    // ---- AC2 实体落库 ----

    [Fact]
    public async Task Save_List_Delete_Roundtrip_Persists()
    {
        var saved = await _svc.SaveAsync(new PermissionProfileDto
        {
            AgentId = "agent.generalist",
            Enabled = true,
            Rules = [new ToolPermissionRule { ToolName = "write_file", Effect = PermissionEffect.Ask }],
            Remark = "测试策略",
        });

        saved.Should().NotBeNull();
        saved!.Id.Should().BeGreaterThan(0);

        var list = await _svc.ListAsync();
        list.Should().ContainSingle(p => p.Id == saved.Id && p.AgentId == "agent.generalist");
        list.Single(p => p.Id == saved.Id).Rules.Should().ContainSingle(r => r.ToolName == "write_file" && r.Effect == PermissionEffect.Ask);

        (await _svc.DeleteAsync(saved.Id)).Should().BeTrue();
        (await _svc.ListAsync()).Should().NotContain(p => p.Id == saved.Id);
        (await _svc.DeleteAsync(saved.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Save_DuplicateAgentId_ReturnsNull_Conflict()
    {
        (await _svc.SaveAsync(new PermissionProfileDto { AgentId = "agent.dup", Enabled = true }))
            .Should().NotBeNull();
        (await _svc.SaveAsync(new PermissionProfileDto { AgentId = "agent.dup", Enabled = true }))
            .Should().BeNull("同 AgentId 仅一条策略");
    }

    [Fact]
    public async Task EnsureCreated_IsIdempotent()
    {
        PermissionProfile.EnsureCreated().Should().BeTrue();
        PermissionProfile.EnsureCreated().Should().BeTrue("幂等建表");
        PermissionDecisionLog.EnsureCreated().Should().BeTrue();
    }

    // ---- AC3 零配置红线：无 profile = registry 层沉默（内置默认） ----

    [Fact]
    public async Task NoProfile_DefaultPolicy_IsRegistrySilent_Allow()
    {
        var d = await _svc.DecideAsync("s-none", "write_file", """{"path":"x.txt"}""");
        d.Effect.Should().Be(PermissionEffect.Allow, "高风险工具的审批由 MAF 包装承担，registry 层不重复决策");
        d.Source.Should().Be("default-policy");

        var d2 = await _svc.DecideAsync("s-none", "read_file", "{}");
        d2.Effect.Should().Be(PermissionEffect.Allow);
    }

    [Fact]
    public async Task NoProfile_Decision_IsLogged_ForAudit()
    {
        await _svc.DecideAsync("s-audit", "read_file", "{}");
        var logs = PermissionDecisionLog.FindAll();
        logs.Should().Contain(l => l.ToolName == "read_file" && l.Effect == "Allow" && l.Source == "default-policy");
    }

    // ---- AC4 策略可配且生效 ----

    [Fact]
    public async Task Deny_Rule_Denies()
    {
        await SaveGlobalRule(new ToolPermissionRule { ToolName = "run_terminal_command", Effect = PermissionEffect.Deny });
        var d = await _svc.DecideAsync(null, "run_terminal_command", """{"command":"dir"}""");
        d.Effect.Should().Be(PermissionEffect.Deny);
        d.Reason.Should().Contain("权限策略拒绝");
    }

    [Fact]
    public async Task Ask_Rule_Asks()
    {
        await SaveGlobalRule(new ToolPermissionRule { ToolName = "read_file", Effect = PermissionEffect.Ask });
        var d = await _svc.DecideAsync(null, "read_file", """{"path":"a.txt"}""");
        d.Effect.Should().Be(PermissionEffect.Ask);
    }

    [Fact]
    public async Task Allow_WithPathWhitelist_Hit_AllowsWithoutApproval()
    {
        await SaveGlobalRule(new ToolPermissionRule
        {
            ToolName = "write_file",
            Effect = PermissionEffect.Allow,
            PathAllowlist = [@"D:\workspace"],
        });
        var d = await _svc.DecideAsync(null, "write_file", @"{""path"":""d:/workspace/sub/a.txt""}");
        d.Effect.Should().Be(PermissionEffect.Allow, "白名单命中 = 免审批快路径（核心价值）");
        d.Reason.Should().Contain("白名单命中");
    }

    [Fact]
    public async Task Allow_WithPathWhitelist_Miss_Ask()
    {
        await SaveGlobalRule(new ToolPermissionRule
        {
            ToolName = "delete_file",
            Effect = PermissionEffect.Allow,
            PathAllowlist = [@"D:\workspace\trash"],
        });
        var d = await _svc.DecideAsync(null, "delete_file", @"{""path"":""C:/Windows/system32/evil.dll""}");
        d.Effect.Should().Be(PermissionEffect.Ask, "路径白名单未命中回退人工审批（现行为等价）");
    }

    [Fact]
    public async Task Allow_WithDomainWhitelist_Hit_Allows_Miss_Denies()
    {
        await SaveGlobalRule(new ToolPermissionRule
        {
            ToolName = "browser_open",
            Effect = PermissionEffect.Allow,
            DomainAllowlist = ["example.com", "localhost"],
        });

        (await _svc.DecideAsync(null, "browser_open", """{"url":"https://app.example.com/page"}"""))
            .Effect.Should().Be(PermissionEffect.Allow, "子域命中");
        (await _svc.DecideAsync(null, "browser_open", """{"url":"http://localhost:7301/api"}"""))
            .Effect.Should().Be(PermissionEffect.Allow);
        (await _svc.DecideAsync(null, "browser_open", """{"url":"https://evil.example.net"}"""))
            .Effect.Should().Be(PermissionEffect.Deny, "域名白名单外拒绝跳转（与现插件 domain_not_allowed 语义等价）");
        (await _svc.DecideAsync(null, "browser_open", """{"url":"about:blank"}"""))
            .Effect.Should().Be(PermissionEffect.Allow, "浏览器内部地址与现插件语义一致");
        (await _svc.DecideAsync(null, "browser_open", """{"url":"file:///C:/x"}"""))
            .Effect.Should().Be(PermissionEffect.Deny, "非 http(s) 与现插件语义一致拒绝");
    }

    [Fact]
    public async Task Allow_WithCommandWhitelist_Hit_Allows_Miss_Ask()
    {
        await SaveGlobalRule(new ToolPermissionRule
        {
            ToolName = "run_terminal_command",
            Effect = PermissionEffect.Allow,
            CommandAllowlist = ["git status", "git diff"],
        });
        (await _svc.DecideAsync(null, "run_terminal_command", """{"command":"git status --short"}"""))
            .Effect.Should().Be(PermissionEffect.Allow, "前缀命中");
        (await _svc.DecideAsync(null, "run_terminal_command", """{"command":"format C:"}"""))
            .Effect.Should().Be(PermissionEffect.Ask);
    }

    [Fact]
    public async Task Allowlist_Configured_ButArgNotExtractable_FailSafeAsk()
    {
        await SaveGlobalRule(new ToolPermissionRule
        {
            ToolName = "write_file",
            Effect = PermissionEffect.Allow,
            PathAllowlist = [@"D:\workspace"],
        });
        var d = await _svc.DecideAsync(null, "write_file", "{not-json");
        d.Effect.Should().Be(PermissionEffect.Ask, "参数不可提取 fail-safe 转审批");
    }

    [Fact]
    public async Task Wildcard_Rule_AppliesAsFallback()
    {
        await SaveGlobalRule(new ToolPermissionRule { ToolName = "*", Effect = PermissionEffect.Deny });
        var d = await _svc.DecideAsync(null, "get_current_time", "{}");
        d.Effect.Should().Be(PermissionEffect.Deny, "兜底规则命中未单独配置的工具");
    }

    [Fact]
    public async Task AgentProfile_Overrides_Global_And_UnknownAgent_FallsToGlobal()
    {
        await SaveGlobalRule(new ToolPermissionRule { ToolName = "read_file", Effect = PermissionEffect.Ask });
        var agentProfile = await _svc.SaveAsync(new PermissionProfileDto
        {
            AgentId = "agent.generalist",
            Enabled = true,
            Rules = [new ToolPermissionRule { ToolName = "read_file", Effect = PermissionEffect.Allow }],
        });
        agentProfile.Should().NotBeNull();

        _svc.RememberSessionAgent("sess-1", "agent.generalist");
        (await _svc.DecideAsync("sess-1", "read_file", "{}")).Effect.Should().Be(PermissionEffect.Allow, "Agent 精确 profile 优先");
        (await _svc.DecideAsync("sess-unknown", "read_file", "{}")).Effect.Should().Be(PermissionEffect.Ask, "未记忆会话回落全局");
    }

    [Fact]
    public async Task Disabled_Profile_Skipped_FallsThrough()
    {
        await SaveGlobalRule(new ToolPermissionRule { ToolName = "read_file", Effect = PermissionEffect.Deny });
        var p = await _svc.SaveAsync(new PermissionProfileDto
        {
            AgentId = "agent.disabled",
            Enabled = false,
            Rules = [new ToolPermissionRule { ToolName = "read_file", Effect = PermissionEffect.Allow }],
        });
        _svc.RememberSessionAgent("sess-off", "agent.disabled");
        (await _svc.DecideAsync("sess-off", "read_file", "{}")).Effect.Should().Be(PermissionEffect.Deny, "禁用 profile 不参与决策，回落全局");
    }

    // ---- AC5 pre-execute 闸门消费（真管线：ToolRegistry.ExecuteAsync） ----

    [Fact]
    public async Task Registry_PreExecute_ConsumesPolicy_Deny_BlocksExecution()
    {
        await SaveGlobalRule(new ToolPermissionRule { ToolName = "echo_tool", Effect = PermissionEffect.Deny });

        var bus = new ForgeSelf.Core.EventBus();
        var svc = _svc;
        bus.OnSerial<ToolExecution, PreToolDecision?>("tools/pre-execute", exec =>
        {
            var d = svc.DecideAsync(exec!.SessionId, exec.ToolName, exec.ArgsJson).GetAwaiter().GetResult();
            return Task.FromResult<PreToolDecision?>(d.Effect switch
            {
                PermissionEffect.Deny => new PreToolDecision.Deny(d.Reason),
                PermissionEffect.Ask => new PreToolDecision.Ask(d.Reason),
                _ => new PreToolDecision.Allow(),
            });
        });
        var registry = new ToolRegistry(events: bus);
        registry.RegisterTool(new FakeToolFunctionExtension
        {
            Id = "b4.deny",
            Name = "echo_tool",
            ExecuteHandler = _ => throw new InvalidOperationException("策略拒绝后不应执行工具本体"),
        });

        var result = await registry.ExecuteAsync(new ToolExecution
        { CallId = "c1", ToolName = "echo_tool", ArgsJson = "{}", SessionId = "s-gate" });

        result.Success.Should().BeFalse("策略拒绝经闸门 1 短路");
        result.ErrorMessage.Should().Contain("权限策略拒绝");
    }

    [Fact]
    public async Task Registry_PreExecute_AllowWhitelist_Executes()
    {
        await SaveGlobalRule(new ToolPermissionRule
        {
            ToolName = "echo_tool",
            Effect = PermissionEffect.Allow,
            CommandAllowlist = [], // 非终端/文件/浏览器类工具：无白名单语义直接放行
        });

        var bus = new ForgeSelf.Core.EventBus();
        var svc = _svc;
        bus.OnSerial<ToolExecution, PreToolDecision?>("tools/pre-execute", exec =>
        {
            var d = svc.DecideAsync(exec!.SessionId, exec.ToolName, exec.ArgsJson).GetAwaiter().GetResult();
            return Task.FromResult<PreToolDecision?>(d.Effect switch
            {
                PermissionEffect.Deny => new PreToolDecision.Deny(d.Reason),
                PermissionEffect.Ask => new PreToolDecision.Ask(d.Reason),
                _ => new PreToolDecision.Allow(),
            });
        });
        var registry = new ToolRegistry(events: bus);
        registry.RegisterTool(new FakeToolFunctionExtension
        {
            Id = "b4.allow",
            Name = "echo_tool",
            ExecuteHandler = _ => Task.FromResult("""{"echo":true}"""),
        });

        var result = await registry.ExecuteAsync(new ToolExecution
        { CallId = "c2", ToolName = "echo_tool", ArgsJson = """{"text":"hi"}""", SessionId = "s-gate2" });

        result.Success.Should().BeTrue("策略放行后工具真实执行");
    }

    // ---- AC8 防双问（服务面）：规则存在性判定 ----

    [Fact]
    public async Task HasRuleForTool_True_WhenAgentOrGlobalRuleExists()
    {
        (await _svc.HasRuleForToolAsync("sess-x", "write_file")).Should().BeFalse("未配置 = 保持 MAF 包装");

        await SaveGlobalRule(new ToolPermissionRule { ToolName = "write_file", Effect = PermissionEffect.Ask });
        (await _svc.HasRuleForToolAsync("sess-x", "write_file")).Should().BeTrue("全局规则命中，MAF 包装让位");

        _svc.RememberSessionAgent("sess-y", "agent.generalist");
        await _svc.SaveAsync(new PermissionProfileDto
        {
            AgentId = "agent.generalist",
            Rules = [new ToolPermissionRule { ToolName = "run_terminal_command", Effect = PermissionEffect.Allow }],
        });
        (await _svc.HasRuleForToolAsync("sess-y", "run_terminal_command")).Should().BeTrue("Agent 规则命中");
        (await _svc.HasRuleForToolAsync("sess-x", "run_terminal_command")).Should().BeFalse("未记忆会话 + 全局无此规则");
        (await _svc.HasRuleForToolAsync("sess-x", "delete_file")).Should().BeFalse("该会话 Agent 与全局均无 delete_file 规则");
    }

    // ---- AC7 审计端点（控制器级） ----

    [Fact]
    public async Task Audit_Endpoint_Union_Filters_Paginates()
    {
        // 两条策略决策 + 一条审批记录
        await _svc.DecideAsync("s-a1", "read_file", """{"path":"a.txt"}""");
        await _svc.DecideAsync("s-a1", "write_file", """{"path":"b.txt"}""");
        var approval = await new PersistentApprovalService().CreatePendingAsync(new ApprovalCreateRequest
        { SessionId = "s-a1", ToolName = "write_file", ArgsJson = """{"path":"b.txt"}""" });

        var controller = new PermissionsController(_svc);

        // 全量
        var all = await controller.QueryAudit(toolName: null, sessionId: "s-a1", kind: null, from: null, to: null, 1, 50, CancellationToken.None);
        var okAll = Assert.IsType<OkObjectResult>(all);
        var payloadAll = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(okAll.Value, CamelJson));
        payloadAll.GetProperty("total").GetInt32().Should().Be(3, "决策 2 + 审批 1 联合");

        // kind 过滤
        var onlyApprovals = await controller.QueryAudit(null, "s-a1", "tool-approval", null, null, 1, 50, CancellationToken.None);
        var okApprovals = Assert.IsType<OkObjectResult>(onlyApprovals);
        var payloadApprovals = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(okApprovals.Value, CamelJson));
        payloadApprovals.GetProperty("total").GetInt32().Should().Be(1);
        payloadApprovals.GetProperty("items").EnumerateArray().Should().Contain(i =>
            i.GetProperty("kind").GetString() == "tool-approval"
            && i.GetProperty("approvalId").GetInt64() == approval.Id);

        // toolName 过滤
        var byTool = await controller.QueryAudit("read_file", "s-a1", null, null, null, 1, 50, CancellationToken.None);
        var okByTool = Assert.IsType<OkObjectResult>(byTool);
        var payloadByTool = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(okByTool.Value, CamelJson));
        payloadByTool.GetProperty("total").GetInt32().Should().Be(1);

        // 分页
        var page2 = await controller.QueryAudit(null, "s-a1", null, null, null, 2, 2, CancellationToken.None);
        var okPage2 = Assert.IsType<OkObjectResult>(page2);
        var payloadPage2 = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(okPage2.Value, CamelJson));
        payloadPage2.GetProperty("items").GetArrayLength().Should().Be(1, "3 条第 2 页（pageSize=2）剩 1 条");

        // 非法 kind → 400
        var bad = await controller.QueryAudit(null, null, "bogus", null, null, 1, 50, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(bad);
    }

    [Fact]
    public async Task Save_Endpoint_Conflict_Maps409_And_Delete_Maps404()
    {
        var controller = new PermissionsController(_svc);
        (await controller.SaveProfile(0, new PermissionProfileDto { AgentId = "agent.ep" }, CancellationToken.None))
            .Result.Should().BeOfType<OkObjectResult>();
        var conflict = await controller.SaveProfile(0, new PermissionProfileDto { AgentId = "agent.ep" }, CancellationToken.None);
        Assert.IsType<ConflictObjectResult>(conflict.Result);

        var del = await controller.DeleteProfile(999999, CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(del);
    }

    [Fact]
    public void Controller_WithoutService_Returns503()
    {
        var controller = new PermissionsController(null);
        var list = controller.ListProfiles(CancellationToken.None);
        list.Result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(503);
    }

    private async Task SaveGlobalRule(ToolPermissionRule rule)
    {
        var existing = await _svc.ListAsync();
        var global = existing.FirstOrDefault(p => p.AgentId == string.Empty);
        var rules = global == null ? new List<ToolPermissionRule> { rule } : global.Rules.ToList();
        if (global != null) rules.Add(rule);
        var saved = await _svc.SaveAsync(new PermissionProfileDto
        {
            Id = global?.Id ?? 0,
            AgentId = string.Empty,
            Enabled = true,
            Rules = rules,
        });
        saved.Should().NotBeNull("全局 profile 保存成功");
    }
}
