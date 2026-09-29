using ForgeSelf.Abstractions;
using ForgeSelf.Api.Services;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// B9-6 门禁（R5）：大工具结果 spill——超过 <c>SpillThresholdBytes</c> 的 tool 结果
/// 在模型可见投影（DeriveMessages）中截断为引用；完整结果仍走 tools/result 事件与日志（R4：UI 走事件流不受影响）。
/// </summary>
public class SessionEventProjectionSpillTests
{
    private const string SessionId = "spill-s1";

    [Fact]
    public void Derive_ToolResult_UnderThreshold_PassesThrough()
    {
        var result = DeriveWithResult(new string('a', 100));
        result.Should().HaveCount(1);
        result[0].Content.Should().Be(new string('a', 100));
        result[0].CallId.Should().Be("call-1");
    }

    [Fact]
    public void Derive_ToolResult_OverThreshold_TruncatedWithMarker()
    {
        var original = new string('b', 40_000);   // 默认 32KiB 阈值之上
        var result = DeriveWithResult(original);

        result.Should().HaveCount(1);
        result[0].Content.Should().Contain("[tool_result spilled");
        result[0].Content.Should().Contain("40000");                       // 原始大小留痕
        result[0].Content.Should().Contain("call-1");                      // CallId 引用留痕（模型可回查）
        result[0].Content.Length.Should().BeLessThan(original.Length);     // 确实截断
        result[0].CallId.Should().Be("call-1");
    }

    [Fact]
    public void Derive_ToolResult_ExactlyAtThreshold_PassesThrough()
    {
        var original = new string('c', 32 * 1024);   // 边界：等于阈值不 spill
        var result = DeriveWithResult(original);
        result[0].Content.Should().Be(original);
    }

    [Fact]
    public void Threshold_IsConfigurable()
    {
        SessionEventProjection.SpillThresholdBytes = 10;
        try
        {
            var result = DeriveWithResult(new string('d', 11));
            result[0].Content.Should().Contain("[tool_result spilled");
        }
        finally
        {
            SessionEventProjection.SpillThresholdBytes = SessionEventProjection.DefaultSpillThresholdBytes;
        }
    }

    private static IReadOnlyList<global::ForgeSelf.Abstractions.Message> DeriveWithResult(string resultJson)
    {
        var events = new SessionEvent[]
        {
            new ToolResultEvent(0, SessionId, DateTimeOffset.Now, "call-1", "demo_tool", resultJson, ToolOutcome.Ok, 1),
        };
        return SessionEventProjection.Derive(events);
    }
}
