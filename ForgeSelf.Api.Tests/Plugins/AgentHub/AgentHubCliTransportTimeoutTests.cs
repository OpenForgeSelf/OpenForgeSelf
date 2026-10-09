using FluentAssertions;
using ForgeSelf.Api.Plugins.AgentHub.Models;
using ForgeSelf.Api.Plugins.AgentHub.Profiles;
using ForgeSelf.Api.Plugins.AgentHub.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AgentHub;

/// <summary>
/// CliTransport 超时回归（PILOT-055 走查实证修复）。
///
/// 缺陷现场：子进程<b>静默无输出</b>（如 opencode 对模型端点错误静默重试不退出）时，
/// 旧实现把 <c>timeoutCts</c> 放在 stdout 读取循环<b>之后</b>——<c>ReadLineAsync(ct)</c> 永远阻塞，
/// 超时杀进程逻辑根本执行不到，任务恒 Running（策略超时形同虚设）。
///
/// 本用例：拉起一个 300 秒的静默子进程（powershell Start-Sleep，无 stdout），TimeoutMs=2000，
/// 断言 20s 内流结束、出现带英文 "timeout" 标记的 Error 事件（供 ClassifyError 归类，防中文文案落入 upstream_error）
/// 与 Exit 事件。Mutation 探针：没有修复时 ReadLineAsync 恒阻塞 → 20s CancelAfter 触发 OCE → 本用例必红。
/// </summary>
public class AgentHubCliTransportTimeoutTests
{
    [Fact]
    public async Task 静默子进程_超时后必须被杀且错误分类为timeout()
    {
        var loader = new ProfileLoader();
        loader.LoadAll(null);

        var transport = new CliTransport(loader);
        var ap = new AgentAccessPointDto
        {
            Id = 1,
            AgentId = 1,
            Vendor = "opencode",
            Mode = "OneShot",
            Transport = "Cli",
            Executable = "powershell",
            // 注意：模板里**不能自带双引号**——SplitTemplate 原样保留引号，
            // 引号会被 ArgumentList 当字面字符传给子进程（powershell 会把带引号串当字符串表达式求值打印后退出 0，
            // 实踩）。用 {prompt} 注入（先切分后替换，prompt 内空格不拆项）即可拿到无引号单参数。
            ArgsTemplate = "-Command {prompt}",
            PromptInjection = "Arg",
            IsDefault = true
        };
        var req = new AgentRunRequest
        {
            // 300 秒无输出静默子进程（修复前：ReadLineAsync 恒阻塞，超时永不触发）
            Prompt = "Start-Sleep -Seconds 300",
            Cwd = null,
            PermissionMode = "read-only",
            TimeoutMs = 2000
        };
        var session = new AgentSession { TaskId = 1, AccessPointId = 1 };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var events = new List<AgentEvent>();
        try
        {
            await foreach (var evt in transport.StreamAsync(ap, req, session, cts.Token))
                events.Add(evt);
        }
        catch (OperationCanceledException)
        {
            Assert.Fail("超时修复回归：静默子进程 20s 内未被终止（旧缺陷：timeoutCts 在 stdout 循环后才生效，超时永远不触发）");
        }

        events.Should().Contain(e => e.Type == AgentEventTypes.Exit,
            "进程必须被终止并回流 Exit 事件（否则终态判定走不到）");
        events.Should().Contain(e => e.Type == AgentEventTypes.Error && (e.Text ?? "").Contains("timeout"),
            "超时终止必须回流带英文 timeout 标记的 Error 事件（ClassifyError 需要它归类，中文文案会落入 upstream_error）");
    }
}
