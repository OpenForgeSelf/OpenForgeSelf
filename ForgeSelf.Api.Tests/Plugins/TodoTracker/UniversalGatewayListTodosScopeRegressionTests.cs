using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.McpCenter.Services;
using ForgeSelf.Api.Plugins.TodoTracker;
using ForgeSelf.Api.Plugins.TodoTracker.Models;
using ForgeSelf.Api.Plugins.TodoTracker.Services;
using ForgeSelf.Api.Services;
using ForgeSelf.Core;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.TodoTracker;

/// <summary>
/// 成功回归（PILOT-062 修复批 · 2026-10-17，由复现批特征化测试翻转而来，
/// 前情见 <c>docs/ai/pilot/2026-10-17-gateway-list-todos-scope-repro/</c>）：
/// 经 McpCenter 通用网关（MCP tools/call → <see cref="UniversalToolForwarder"/>）调用
/// <c>todo_list</c>，入参 {parameters:"{}", tool:"todo_list"}，必须返回 <c>success:true</c> 成功封套。
///
/// 时序仍按宿主 AppBuilder 真实顺序复刻，且<b>工具在 seed 之前构造</b>（与缺陷时序相同）——
/// 修复后工具构造期零解析、执行期经 <see cref="PluginContextDiExtensions.CreateHostScope"/>
/// 即时解析，seed 完成后同一批工具实例自然可用：本用例即「seed 时序无关」的机械证据。
/// 若有人把解析改回构造期 eager 写法，本条翻红（架构守卫
/// <c>PluginToolConstructorDiGuardTests</c> 同步拦截写法本身）。
/// </summary>
public class UniversalGatewayListTodosScopeRegressionTests
{
    [Fact]
    public async Task UniversalTool_ListTodos_ToolBuiltBeforeSeed_Returns_Success_Envelope()
    {
        // ── 时序复刻（与 ForgeSelf.Api/AppBuilder.cs 一致）──────────────────
        // ① 宿主根 Context + 插件 Fiber 子上下文（PluginManager 为每插件 Derive）。
        using var root = new Context();
        var pluginCtx = (Context)root.Derive();

        // ② RegisterAllServices 立即触发插件 Apply：此刻宿主 provider 尚未 seed，
        //    工具构造只存原始上下文（修复后不再 eager 解析，缺陷不复存在）。
        var tool = new ListTodosToolFunction(pluginCtx)
        {
            Id = "todotracker.tool.todo_list",
            Name = "todo_list",
            PluginId = "TodoTracker",
            Description = "列出待办/任务（成功回归用例最小定义）。",
            ParametersJsonSchema = TodoToolSchemas.ListTodos
        };

        // ③ builder.Build() 之后 ProvideHostServices 才把宿主根 provider seed 进 root Context。
        //    注册 fake ITodoService 作数据回传证据（修复前这一步永不可达：CreateScope 先抛）。
        var fake = new Mock<ITodoService>();
        fake.Setup(s => s.GetTodosAsync(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new ForgeSelf.Api.Plugins.TodoTracker.Models.PagedResult<TodoDto>
            {
                Items = new List<TodoDto> { new() { Id = 42, Title = "PILOT-062 回归样例" } },
                Total = 1,
                Page = 1,
                PageSize = 20
            });
        using var host = new ServiceCollection()
            .AddSingleton<ITodoService>(fake.Object)
            .BuildServiceProvider();
        root.Register(typeof(IServiceProvider), host);

        // ④ 工具发现接线：真实宿主注册表注册该工具，并把 IToolRegistry seed 进根 Context 供网关软依赖解析。
        var registry = new ToolRegistry();
        registry.RegisterTool(tool);
        root.Register<IToolRegistry>(registry);

        // ⑤ MCP tools/call：网关万能工具按用户原始入参转发。
        var store = new ExternalServersStore(Path.Combine(Path.GetTempPath(), "mcpcenter-repro", Guid.NewGuid().ToString("N")));
        var forwarder = new UniversalToolForwarder(pluginCtx, new McpClientManager(store));

        var result = await forwarder.ForwardAsync("""{"parameters":"{}","tool":"todo_list"}""");

        // ── 断言：成功封套 + fake 数据回传（错误串不再出现） ──────────────────
        using var doc = JsonDocument.Parse(result.Text);
        var root0 = doc.RootElement;
        root0.GetProperty("success").GetBoolean().Should().BeTrue(
            $"修复目标：网关调用成功；实际返回：{result.Text}");
        var data = root0.GetProperty("data");
        data.GetProperty("Total").GetInt32().Should().Be(1);
        data.GetProperty("Items")[0].GetProperty("Title").GetString().Should().Be("PILOT-062 回归样例");
        fake.VerifyAll();
    }
}
