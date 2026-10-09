using FluentAssertions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.TodoTracker.Controllers;
using ForgeSelf.Api.Plugins.TodoTracker.Models;
using ForgeSelf.Api.Plugins.TodoTracker.Services;
using ForgeSelf.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.TodoTracker;

/// <summary>
/// 「一键交给 AgentHub 执行」的能力接缝侧（PILOT-054 · FR-6 / AC-8）。
///
/// 这一层要证明的是<b>依赖方向的形状</b>，不是 AgentHub 会不会跑（那是 agent-hub 自己的用例/e2e）：
/// ① 接缝缺席必须报"能力缺席"并可单独判定（→ 控制器出 503），不许伪装成功；
/// ② 提供方业务拒绝的原因必须原文透传（→ 400），不许混成"没装插件"；
/// ③ 本插件源码里不许出现直连别的插件的 HTTP 路径（architecture-design 铁律 2/3）。
/// </summary>
public class TodoAgentDelegationTests
{
    private const string SeamMessage = AgentTaskGateway.SeamNotAvailableMessage;

    [Fact]
    public async Task 接缝缺席时_应报能力缺席而不是静默成功()
    {
        var gateway = new AgentTaskGateway(new Context());   // 空上下文：什么都没注册

        gateway.IsAvailable.Should().BeFalse();
        gateway.AvailableAgents().Should().BeEmpty();

        var submit = await gateway.SubmitAsync(new AgentDelegationRequest { Prompt = "干活", Cwd = @"D:\p" });
        submit.Success.Should().BeFalse();
        submit.SeamMissing.Should().BeTrue("缺席要单独可判，控制器据此出 503 而不是 400");
        submit.Error.Should().Be(SeamMessage);

        var query = await gateway.Query("whatever");
        query.SeamMissing.Should().BeTrue();
        query.Error.Should().Be(SeamMessage);
    }

    [Fact]
    public async Task 接缝在场时_提交成功应把taskKey交出且请求原样送达()
    {
        var fake = new FakeDelegation { SubmitResult = AgentDelegationOutcome.Ok("key-123", 5, "codex", "Queued") };
        var ctx = new Context();
        ctx.Register<IAgentDelegation>(fake);
        var gateway = new AgentTaskGateway(ctx);

        var result = await gateway.SubmitAsync(new AgentDelegationRequest
        {
            Prompt = "干活",
            AgentId = 5,
            Cwd = @"D:\project",
            PermissionMode = "read-only",
            CreatedBy = "todo-tracker"
        });

        result.Success.Should().BeTrue(result.Error);
        result.Value!.TaskKey.Should().Be("key-123");
        gateway.IsAvailable.Should().BeTrue();

        // createdBy 是审计与统计归因的依据，不能被中间层吃掉
        fake.LastRequest!.CreatedBy.Should().Be("todo-tracker");
        fake.LastRequest.Cwd.Should().Be(@"D:\project");
        fake.LastRequest.PermissionMode.Should().Be("read-only");
    }

    [Fact]
    public async Task 提供方业务失败_原因原文透传且不算缺席()
    {
        var ctx = new Context();
        ctx.Register<IAgentDelegation>(new FakeDelegation
        {
            SubmitResult = AgentDelegationOutcome.Fail("工作目录「D:\\project」不在 agent「codex」的白名单内")
        });

        var result = await new AgentTaskGateway(ctx)
            .SubmitAsync(new AgentDelegationRequest { Prompt = "干活", Cwd = @"D:\project" });

        result.Success.Should().BeFalse();
        result.SeamMissing.Should().BeFalse("这是 agent-hub 的业务裁决，用户该去改白名单，不是去装插件");
        result.Error.Should().Contain("白名单");
    }

    [Fact]
    public async Task 请求为空应直接拒绝_不惊动提供方()
    {
        var fake = new FakeDelegation();
        var ctx = new Context();
        ctx.Register<IAgentDelegation>(fake);

        var result = await new AgentTaskGateway(ctx).SubmitAsync(null!);

        result.Success.Should().BeFalse();
        fake.SubmitCallCount.Should().Be(0, "空请求不该打到提供方");
    }

    [Fact]
    public async Task 提供方抛异常_应转成可读失败而不是冒泡到调用面()
    {
        var ctx = new Context();
        ctx.Register<IAgentDelegation>(new ThrowingDelegation());

        var result = await new AgentTaskGateway(ctx).SubmitAsync(new AgentDelegationRequest { Prompt = "干活" });

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("提交委派失败");
        // 只读自检能力同样不许冒泡
        new AgentTaskGateway(ctx).AvailableAgents().Should().BeEmpty();
    }

    [Fact]
    public async Task 读回状态_提供方查不到时应报不存在而非缺席()
    {
        var ctx = new Context();
        ctx.Register<IAgentDelegation>(new FakeDelegation { Snapshot = null });

        var result = await new AgentTaskGateway(ctx).Query("missing-key");

        result.Success.Should().BeFalse();
        result.SeamMissing.Should().BeFalse();
        result.Error.Should().Contain("不存在");
    }

    [Fact]
    public async Task 控制器应把能力缺席映射成503_把业务拒绝映射成400()
    {
        var dispatch = new Mock<ITodoDispatchService>();
        dispatch.Setup(d => d.DelegateAsync(It.IsAny<int>(), It.IsAny<DelegateToAgentRequest?>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(new DelegateToAgentResultDto { Ok = false, Error = SeamMessage, SeamMissing = true });
        var controller = new TodoDispatchController(new Mock<ITodoService>().Object, dispatch.Object)
        {
            // 控制器要拿 Request 拼回报地址（BaseUrl），没有 ControllerContext 时 Request 直接抛异常 ⇒ 会被 catch 成 500，
            // 把"该报 503"的用例带偏成"报 500"。给一个真实 DefaultHttpContext 而不是绕过那段代码。
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var absent = await controller.DelegateToAgent(1, new DelegateToAgentRequest());
        GetStatus(absent.Result).Should().Be(StatusCodes.Status503ServiceUnavailable);

        dispatch.Setup(d => d.DelegateAsync(It.IsAny<int>(), It.IsAny<DelegateToAgentRequest?>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(new DelegateToAgentResultDto { Ok = false, Error = "工作目录不在白名单内", SeamMissing = false });
        var rejected = await controller.DelegateToAgent(1, new DelegateToAgentRequest());
        GetStatus(rejected.Result).Should().Be(400);
    }

    [Fact]
    public void 本插件源码不得直连其他插件的HTTP端点()
    {
        // 静态守卫前先剥注释（plugin-development §B：文件里为了说明"不许出现 X"必然会出现 X 的名字，直接 Contains 必自误）
        var root = LocatePluginSourceDir();
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var text = StripComments(File.ReadAllText(file));
            if (text.Contains("api/agent-hub", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("/api/ai-agent", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("/api/scheduler", StringComparison.OrdinalIgnoreCase))
            {
                offenders.Add(Path.GetFileName(file));
            }
        }

        offenders.Should().BeEmpty("委派一律经 IAgentDelegation 能力接缝");

        // 阳性对照：守卫确实读到了源码，且剥注释后仍认得出"能力接缝"这条正路
        var gateway = StripComments(File.ReadAllText(Path.Combine(root, "Services", "AgentTaskGateway.cs")));
        gateway.Should().Contain("IAgentDelegation");
    }

    /// <summary>取状态码。用 BeAssignableTo 而不是 BeOfType：BadRequestObjectResult/NotFoundObjectResult 都是 ObjectResult 的子类，精确类型断言会把"返回了 400"误报成类型错误。</summary>
    private static int GetStatus(IActionResult actionResult) =>
        actionResult.Should().BeAssignableTo<ObjectResult>().Subject.StatusCode!.Value;

    private static string LocatePluginSourceDir()
    {
        // 必须先认仓库根（有 ForgeSelf.slnx 的那一层）再拼 Plugins/TodoTracker：
        // 直接找 "Plugins/TodoTracker" 会先撞上测试输出目录里那份**发布态插件**（只有 DLL 和 plugin.json），
        // 于是守卫扫的是产物而不是源码 —— 实测就是这么报 DirectoryNotFoundException 的。
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx")))
                return Path.Combine(dir.FullName, "Plugins", "TodoTracker");
            dir = dir.Parent;   // 必须推进指针，否则死循环（plugin-development §B 实测挂过 11 分钟）
        }

        throw new FileNotFoundException("找不到仓库根（ForgeSelf.slnx），静态守卫无法执行");
    }

    private static string StripComments(string source) =>
        System.Text.RegularExpressions.Regex.Replace(
            System.Text.RegularExpressions.Regex.Replace(source, @"/\*.*?\*/", "",
                System.Text.RegularExpressions.RegexOptions.Singleline),
            @"//.*$", "", System.Text.RegularExpressions.RegexOptions.Multiline);

    /// <summary>可编排的假接缝：记录收到的请求，返回预设结果。</summary>
    private sealed class FakeDelegation : IAgentDelegation
    {
        public AgentDelegationRequest? LastRequest { get; set; }
        public int SubmitCallCount { get; set; }
        public AgentDelegationOutcome SubmitResult { get; init; } = AgentDelegationOutcome.Ok("k", 1, "agent", "Queued");
        public AgentDelegationSnapshot? Snapshot { get; init; } = new() { TaskKey = "k", Status = "Running" };

        public Task<AgentDelegationOutcome> SubmitAsync(AgentDelegationRequest request, CancellationToken ct = default)
        {
            SubmitCallCount++;
            LastRequest = request;
            return Task.FromResult(SubmitResult);
        }

        public Task<AgentDelegationSnapshot?> FindAsync(string taskKey, CancellationToken ct = default) =>
            Task.FromResult(string.Equals(taskKey, Snapshot?.TaskKey, StringComparison.OrdinalIgnoreCase) ? Snapshot : null);

        /// <summary>标记完成：命中已终态快照或未知 key 视为无需标记（false），否则 true。</summary>
        public Task<bool> MarkCompletedAsync(string taskKey, CancellationToken ct = default) =>
            Task.FromResult(Snapshot != null && Snapshot.Status != "Succeeded"
                            && string.Equals(taskKey, Snapshot.TaskKey, StringComparison.OrdinalIgnoreCase));

        public IReadOnlyList<AgentDelegationAgent> ListAvailableAgents() =>
            [new AgentDelegationAgent { Id = 5, Name = "codex", Vendor = "codex" }];
    }

    private sealed class ThrowingDelegation : IAgentDelegation
    {
        public Task<AgentDelegationOutcome> SubmitAsync(AgentDelegationRequest request, CancellationToken ct = default) =>
            throw new InvalidOperationException("提供方库锁");

        public Task<AgentDelegationSnapshot?> FindAsync(string taskKey, CancellationToken ct = default) =>
            throw new InvalidOperationException("提供方库锁");

        public Task<bool> MarkCompletedAsync(string taskKey, CancellationToken ct = default) =>
            throw new InvalidOperationException("提供方库锁");

        public IReadOnlyList<AgentDelegationAgent> ListAvailableAgents() => throw new InvalidOperationException("提供方库锁");
    }
}
