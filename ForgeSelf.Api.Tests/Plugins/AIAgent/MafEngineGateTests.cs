using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AIAgent;

/// <summary>
/// B5 引擎门禁：MafTaskLoopAgent turn/step 状态机（041 架构师清单的可移植子集）。
/// </summary>
/// <remarks>
/// 判据要点：一切持久事实必须能从会话日志 <c>Replay</c> 重建（帧只是 live 视图）；
/// 断言一律打在<b>日志事件序</b>上。ReactLoopAgent 专属语义（ToolChoice=required 强制、
/// 请求重试、MaxTokens 粘滞）随引擎删除（生产默认引擎自 PILOT-051 起即为 MAF，无回归），
/// 记录于 04-task 偏差；其余判据由 MAF 引擎承载。
/// </remarks>
[Collection("XCode")]
public class MafEngineGateTests
{
    // ---- 1. 最短路径：无工具，1 turn 1 step（纯问答） ----
    [Fact]
    public async Task SingleTurn_NoTools_Completes()
    {
        var fixture = new Fixture();
        fixture.Provider.Enqueue(Script.Text("你好，有什么可以帮你？", finish: "stop"));

        var agent = fixture.CreateAgent();
        agent.Inbox.Followup(fixture.SessionId, "第一轮正常消息");

        var frames = await fixture.RunAsync(agent);

        // 日志事件序逐条比对（MAF：request/header + assistant/message 由 ForgeModelCallLogger 落）
        Assert.Equal(
            new[]
            {
                "turn/start", "step/start", "user/message", "request/header",
                "assistant/message", "step/end", "turn/end"
            },
            fixture.EventTypes());

        Assert.Equal(new[] { "user", "assistant" }, fixture.DerivedRoles());
        Assert.Contains(frames, f => f is TurnCompleted);
        Assert.Equal(AgentStatus.Idle, agent.Status);
    }

    // ---- 2. 含工具调用：MAF 内层函数循环在 step 内吃完「工具对」（与 ReactLoop 一请求一步不同），
    //         中间纯文本才断步；线 462 续跑至 task_complete 出口。tool/call+result 成对、注册表只见声明工具 ----
    [Fact]
    public async Task TwoSteps_WithTool()
    {
        var fixture = new Fixture();
        fixture.Provider.Enqueue(Script.ToolCall("call-1", "get_current_time", "{}"));
        fixture.Provider.Enqueue(Script.Text("现在是 12:00", finish: "stop"));
        fixture.Provider.Enqueue(Script.ToolCall("call-2", "task_complete", "{\"summary\":\"时间已告知\"}"));

        var agent = fixture.CreateAgent();
        agent.Inbox.Followup(fixture.SessionId, "现在几点？");
        var frames = await fixture.RunAsync(agent);

        var types = fixture.EventTypes();
        Assert.Equal(2, types.Count(t => t == "step/start"));
        Assert.Equal(2, types.Count(t => t == "step/end"));
        Assert.Equal(2, types.Count(t => t == "tool/call"));
        Assert.Equal(2, types.Count(t => t == "tool/result"));

        // 配对不变量：tool/call 必须早于配对 tool/result
        Assert.True(types.IndexOf("tool/call") < types.IndexOf("tool/result"),
            "tool/call 必须早于配对的 tool/result");

        // 工具确实被执行（task_complete 出口短路不进注册表，注册表只见 get_current_time）
        Assert.Equal(new[] { "get_current_time" }, fixture.Tools.Executions.Select(e => e.Name).ToArray());
        // 工具结果进了模型上下文（第 2 次请求能看见 tool 角色消息）
        Assert.Contains(fixture.Provider.Requests[1].Messages, m => m.Role == "tool");

        // 任务循环收束：出口经 ExitToolInvoked 帧交付，turn 正常 Completed
        var exit = Assert.Single(frames.OfType<ExitToolInvoked>());
        Assert.Equal("task_complete", exit.ToolName);
        Assert.Contains(frames, f => f is TurnCompleted { Reason: TurnEndReason.Completed });
    }

    // ---- 3. MaxSteps 兜底：连续 tool_call 超限 → 强制收束且 turn/end 存在 ----
    [Fact]
    public async Task MaxSteps_Enforced()
    {
        var fixture = new Fixture(maxStepsPerTurn: 2);
        fixture.Provider.Enqueue(Script.ToolCall("call-1", "get_current_time", "{}"), repeatLast: true);

        var agent = fixture.CreateAgent();
        agent.Inbox.Followup(fixture.SessionId, "无限调用工具");
        await fixture.RunAsync(agent);

        var types = fixture.EventTypes();
        Assert.Equal(2, types.Count(t => t == "step/start"));
        Assert.Single(types.Where(t => t == "turn/end"));
        Assert.Equal(types[^1], "turn/end");
    }

    // ---- 4. 注册表注销：DisposeAsync 后 TryGetAsync 返回 null（会话复用选项同步走 MafTaskLoopAgent.ApplyOptions） ----
    [Fact]
    public async Task Dispose_Unregisters()
    {
        var registry = new AgentRuntimeRegistry((sessionId, options) =>
        {
            var fixture = new Fixture();
            fixture.Provider.Enqueue(Script.Text("你好", finish: "stop"), repeatLast: true);
            return fixture.CreateAgent(sessionId);
        });

        var agent = await registry.GetOrCreateAsync("s-dispose", new AgentOptions());
        Assert.NotNull(await registry.TryGetAsync("s-dispose"));
        Assert.Equal("s-dispose", agent.SessionId);

        Assert.True(await registry.DisposeAsync("s-dispose"));
        Assert.Null(await registry.TryGetAsync("s-dispose"));
        Assert.False(await registry.DisposeAsync("s-dispose"));
    }

    // ---- 5. 旧契约与已删引擎/开关 grep 断言：生产代码 0 命中（B5 AC2/AC5/AC8 守卫） ----
    [Fact]
    public void LegacyContract_Removed()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

        var productionDirs = new[]
        {
            Path.Combine(repoRoot, "ForgeSelf.Abstractions"),
            Path.Combine(repoRoot, "ForgeSelf.Core"),
            Path.Combine(repoRoot, "ForgeSelf.Api"),
            Path.Combine(repoRoot, "Plugins"),
        };

        // 只禁「真正的旧契约/已删类型引用」：注释里提到旧名（说明为何删除）不算命中；
        // AgentRunRequest 不在此列（AgentHub 有同名但语义不同的自有 DTO，禁了会误伤）。
        var banned = new[]
        {
            "IAgentLoop", "InMemoryAgentLoop", "TurnEvent",
            "ReactLoopAgent", "UseMafRuntime", "TaskLoopMode",
            "AgentCoordinatorService", "AgentExecutorService"
        };
        var hits = new List<string>();

        foreach (var dir in productionDirs)
        {
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                {
                    continue;
                }

                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    var trimmed = lines[i].TrimStart();
                    if (trimmed.StartsWith("//", StringComparison.Ordinal))
                    {
                        continue; // 注释行（含 /// 与 //）不算引用
                    }

                    if (banned.Any(b => lines[i].Contains(b, StringComparison.Ordinal)))
                    {
                        hits.Add($"{Path.GetRelativePath(repoRoot, file)}:{i + 1}: {lines[i].Trim()}");
                    }
                }
            }
        }

        Assert.True(hits.Count == 0,
            "旧契约/删引擎与开关仍在生产代码中出现: "
            + string.Join(" | ", hits));
    }
}
