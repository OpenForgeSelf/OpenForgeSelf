using System.Text.Json;
using FluentAssertions;
using ForgeSelf.Api.Plugins.AgentHub.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AgentHub;

public class OarPromptTurnGateTests
{
    [Theory]
    [InlineData("turn_ended", true)]
    [InlineData("text_delta", false)]
    [InlineData(null, false)]
    public void IsTerminalEventType_仅终态为真(String? type, Boolean expected) =>
        OarPromptTurnGate.IsTerminalEventType(type).Should().Be(expected);

    [Fact]
    public void IsSettled_idle无证据_不得结算() =>
        OarPromptTurnGate.IsSettled("idle", false, false, false, false).Should().BeFalse();

    [Fact]
    public void IsSettled_idle有lastTurnOutcome_可结算() =>
        OarPromptTurnGate.IsSettled("idle", true, false, false, false).Should().BeTrue();

    [Fact]
    public void IsSettled_running后idle_可结算() =>
        OarPromptTurnGate.IsSettled("idle", false, false, false, true).Should().BeTrue();

    [Fact]
    public void TryParseAgentStatus_读Worker封套()
    {
        using var doc = JsonDocument.Parse("""{"status":{"value":{"kind":"idle","lastTurnOutcome":{"kind":"completed"}},"seq":3}}""");
        OarPromptTurnGate.TryParseAgentStatus(doc.RootElement, out var kind, out var hasOutcome, out _).Should().BeTrue();
        kind.Should().Be("idle");
        hasOutcome.Should().BeTrue();
    }

    [Fact]
    public async Task WaitUntilSettled_running后idle才返回()
    {
        var polls = 0;
        var result = await OarPromptTurnGate.WaitUntilSettledAsync(
            _ =>
            {
                polls++;
                var json = polls < 3
                    ? """{"status":{"value":{"kind":"running","phase":"responding","sinceSeq":1,"lastEventAt":1},"seq":1}}"""
                    : """{"status":{"value":{"kind":"idle","lastTurnOutcome":{"kind":"completed"}},"seq":4}}""";
                return Task.FromResult(JsonDocument.Parse(json).RootElement.Clone());
            },
            () => false,
            TimeSpan.FromMilliseconds(5),
            TimeSpan.FromSeconds(5),
            CancellationToken.None);
        result.Ok.Should().BeTrue();
        polls.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task WaitUntilSettled_超时返回TimedOut()
    {
        var result = await OarPromptTurnGate.WaitUntilSettledAsync(
            _ => Task.FromResult(JsonDocument.Parse("""{"status":{"value":{"kind":"running","phase":"thinking","sinceSeq":1,"lastEventAt":1},"seq":1}}""").RootElement.Clone()),
            () => false,
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromMilliseconds(80),
            CancellationToken.None);
        result.Ok.Should().BeFalse();
        result.Reason.Should().Be(OarTurnSettleReason.TimedOut);
    }

    [Fact]
    public async Task WaitUntilSettled_终端事件可立即结算()
    {
        var result = await OarPromptTurnGate.WaitUntilSettledAsync(
            _ => throw new InvalidOperationException("status not required"),
            () => true,
            TimeSpan.FromMilliseconds(5),
            TimeSpan.FromSeconds(5),
            CancellationToken.None);
        result.Ok.Should().BeTrue();
        result.Reason.Should().Be(OarTurnSettleReason.TerminalEvent);
    }
}
