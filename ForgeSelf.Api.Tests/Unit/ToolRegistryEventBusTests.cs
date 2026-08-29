using ForgeSelf.Core;
using ForgeSelf.Api.Services;
using ForgeSelf.Api.Tests.Plugins;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// 验证 ToolRegistry 执行管道接入的 tools/pre-execute、tools/execute、tools/post-execute
/// 三个事件拦截点（T7）。
/// </summary>
public class ToolRegistryEventBusTests
{
    [Fact]
    public async Task ExecuteToolWithResultAsync_WithHandlers_TriggersEventsInOrder()
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
        bus.OnSerial<ToolCallContext, string?>("tools/pre-execute", _ =>
        {
            order.Add("pre-execute");
            return Task.FromResult<string?>(null);
        });
        bus.On<ToolCallContext>("tools/execute", _ =>
        {
            order.Add("execute");
            return Task.CompletedTask;
        });
        bus.On<ToolCallContext>("tools/post-execute", _ =>
        {
            order.Add("post-execute");
            return Task.CompletedTask;
        });

        // Act
        var result = await registry.ExecuteToolWithResultAsync("order_tool", "{}");

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
        bus.OnSerial<ToolCallContext, string?>("tools/pre-execute", _ => Task.FromResult<string?>("denied by policy"));
        bus.On<ToolCallContext>("tools/execute", _ =>
        {
            laterCalls.Add("execute");
            return Task.CompletedTask;
        });
        bus.On<ToolCallContext>("tools/post-execute", _ =>
        {
            laterCalls.Add("post-execute");
            return Task.CompletedTask;
        });

        // Act
        var result = await registry.ExecuteToolWithResultAsync("deny_tool", "{}");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("denied by policy");
        result.Result.Should().Contain("PERMISSION_DENIED");
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
        ToolCallContext? executeCtx = null;
        ToolCallContext? postCtx = null;
        bus.OnSerial<ToolCallContext, string?>("tools/pre-execute", _ =>
        {
            preCalls++;
            return Task.FromResult<string?>(null);
        });
        bus.On<ToolCallContext>("tools/execute", ctx =>
        {
            executeCtx = ctx;
            return Task.CompletedTask;
        });
        bus.On<ToolCallContext>("tools/post-execute", ctx =>
        {
            postCtx = ctx;
            return Task.CompletedTask;
        });

        // Act
        var result = await registry.ExecuteToolWithResultAsync("success_tool", "{}");

        // Assert
        result.Success.Should().BeTrue();
        preCalls.Should().Be(1);

        executeCtx.Should().NotBeNull();
        executeCtx!.Success.Should().BeTrue();
        executeCtx.Result.Should().Be("{\"data\":\"hello\"}");

        postCtx.Should().NotBeNull();
        postCtx!.Success.Should().BeTrue();
        postCtx.Result.Should().Be("{\"data\":\"hello\"}");
    }
}
