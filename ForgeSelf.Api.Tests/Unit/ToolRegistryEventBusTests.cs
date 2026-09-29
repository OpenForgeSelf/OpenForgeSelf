using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using ForgeSelf.Api.Services;
using ForgeSelf.Api.Tests.Plugins;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// 验证 ToolRegistry 六闸门管线接入的事件拦截点（T7 → B8/042）：
/// tools/pre-execute（Serial 三态）、tools/execute / tools/post-execute（Waterfall）。
/// </summary>
/// <remarks>
/// B8-7：tools/execute、tools/post-execute 由广播 EmitAsync 修正为 waterfall ——
/// 本文件 3 条用例的监听注册由 <c>bus.On&lt;ToolCallContext&gt;</c> 同步改为
/// <c>bus.OnWaterfall&lt;ToolExecution, ...&gt;</c>（payload 也随三态管线升级为 ToolExecution）。
/// </remarks>
public class ToolRegistryEventBusTests
{
    /// <summary>B9：六闸门执行面的测试入参（旧单工具入口形态）。</summary>
    private static ToolExecution Exec(string toolName, string argsJson = "{}")
        => new() { CallId = string.Empty, ToolName = toolName, ArgsJson = argsJson, SessionId = string.Empty };

    [Fact]
    public async Task ExecuteAsync_WithHandlers_TriggersEventsInOrder()
    {
        // Arrange
        var bus = new EventBus();
        var registry = new ToolRegistry(events: bus);
        registry.RegisterTool(new FakeToolFunctionExtension
        {
            Id = "evt.order",
            Name = "order_tool",
            ExecuteHandler = _ => Task.FromResult("{\"ok\":true}")
        });

        var order = new List<string>();
        bus.OnSerial<ToolExecution, PreToolDecision?>("tools/pre-execute", _ =>
        {
            order.Add("pre-execute");
            return Task.FromResult<PreToolDecision?>(null);
        });
        bus.OnWaterfall<ToolExecution, ToolExecutionResult>("tools/execute", async (exec, next) =>
        {
            order.Add("execute");
            return await next();
        });
        bus.OnWaterfall<ToolExecution, PostToolDecision?>("tools/post-execute", async (exec, next) =>
        {
            order.Add("post-execute");
            return await next();
        });

        // Act
        var result = await registry.ExecuteAsync(Exec("order_tool", "{}"));

        // Assert
        result.Success.Should().BeTrue();
        order.Should().Equal("pre-execute", "execute", "post-execute");
    }

    [Fact]
    public async Task PreExecute_ReturnsDeny_ToolNotExecuted_AndNoExecuteOrPost()
    {
        // Arrange
        var bus = new EventBus();
        var registry = new ToolRegistry(events: bus);
        var executed = false;
        registry.RegisterTool(new FakeToolFunctionExtension
        {
            Id = "evt.deny",
            Name = "deny_tool",
            ExecuteHandler = _ =>
            {
                executed = true;
                return Task.FromResult("{}");
            }
        });

        var laterCalls = new List<string>();
        bus.OnSerial<ToolExecution, PreToolDecision?>("tools/pre-execute",
            _ => Task.FromResult<PreToolDecision?>(new PreToolDecision.Deny("denied by policy")));
        bus.OnWaterfall<ToolExecution, ToolExecutionResult>("tools/execute", async (exec, next) =>
        {
            laterCalls.Add("execute");
            return await next();
        });
        bus.OnWaterfall<ToolExecution, PostToolDecision?>("tools/post-execute", async (exec, next) =>
        {
            laterCalls.Add("post-execute");
            return await next();
        });

        // Act
        var result = await registry.ExecuteAsync(Exec("deny_tool", "{}"));

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("denied by policy");
        result.Result.Should().Contain("PERMISSION_DENIED");
        result.Outcome.Should().Be(ToolOutcome.Denied);
        executed.Should().BeFalse();
        laterCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteTool_Success_ExecuteAndPostCarrySuccessAndResult()
    {
        // Arrange
        var bus = new EventBus();
        var registry = new ToolRegistry(events: bus);
        registry.RegisterTool(new FakeToolFunctionExtension
        {
            Id = "evt.success",
            Name = "success_tool",
            ExecuteHandler = _ => Task.FromResult("{\"data\":\"hello\"}")
        });

        var preCalls = 0;
        ToolExecution? executeExec = null;
        ToolExecution? postExec = null;
        bus.OnSerial<ToolExecution, PreToolDecision?>("tools/pre-execute", _ =>
        {
            preCalls++;
            return Task.FromResult<PreToolDecision?>(null);
        });
        bus.OnWaterfall<ToolExecution, ToolExecutionResult>("tools/execute", async (exec, next) =>
        {
            executeExec = exec;
            return await next();
        });
        bus.OnWaterfall<ToolExecution, PostToolDecision?>("tools/post-execute", async (exec, next) =>
        {
            postExec = exec;
            return await next();
        });

        // Act
        var result = await registry.ExecuteAsync(Exec("success_tool", "{}"));

        // Assert
        result.Success.Should().BeTrue();
        preCalls.Should().Be(1);

        executeExec.Should().NotBeNull();
        executeExec!.Success.Should().BeTrue();               // 执行视图已回填
        executeExec.ResultJson.Should().Be("{\"data\":\"hello\"}");

        postExec.Should().NotBeNull();
        postExec!.Success.Should().BeTrue();
        postExec.ResultJson.Should().Be("{\"data\":\"hello\"}");
    }

    /// <summary>
    /// B9 grep 守门：旧工具执行面（ExecuteToolWithResultAsync / ExecuteToolWithTimeoutAsync / ToolCallContext）
    /// 在生产代码中 0 引用（注释提及不算；与 B5 <c>IAgentLoop_Removed</c> 同款实现）。
    /// </summary>
    [Fact]
    public void LegacyToolExecutionFace_Removed()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

        var productionDirs = new[]
        {
            Path.Combine(repoRoot, "ForgeSelf.Abstractions"),
            Path.Combine(repoRoot, "ForgeSelf.Core"),
            Path.Combine(repoRoot, "ForgeSelf.Api"),
            Path.Combine(repoRoot, "Plugins"),
        };

        // 只禁「真正的旧执行面引用」：注释里提到旧名（说明为何删除）不算命中
        var banned = new[] { "ToolCallContext", "ExecuteToolWithResultAsync", "ExecuteToolWithTimeoutAsync" };
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
            "旧工具执行面 ToolCallContext/ExecuteToolWithResultAsync/ExecuteToolWithTimeoutAsync 仍在生产代码中出现: "
            + string.Join(" | ", hits));
    }
}
