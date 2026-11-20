using System.Text.Json;

namespace ForgeSelf.Api.Plugins.AgentHub.Services;

/// <summary>
/// OAR prompt 结算判定（PC-037）：Exit/Succeeded 只能由 turn 完成证据推出，
/// 禁止短 sleep 冒充成功。
/// </summary>
internal static class OarPromptTurnGate
{
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(50);

    public static Boolean IsTerminalEventType(String? type)
    {
        if (String.IsNullOrWhiteSpace(type)) return false;
        var t = type.Trim().ToLowerInvariant();
        return t is "turn_ended" or "exit" or "done" or "complete" or "completed"
            or "turn_complete" or "process_exited" or "session_released"
            or "agent_end" or "run_ended" or "exited";
    }

    public static Boolean TryParseAgentStatus(
        JsonElement sessionStatusResult,
        out String? kind,
        out Boolean hasLastTurnOutcome,
        out Boolean released)
    {
        kind = null;
        hasLastTurnOutcome = false;
        released = false;
        if (sessionStatusResult.ValueKind != JsonValueKind.Object) return false;

        var statusNode = sessionStatusResult;
        if (sessionStatusResult.TryGetProperty("status", out var wrapped)
            && wrapped.ValueKind == JsonValueKind.Object)
            statusNode = wrapped;

        JsonElement valueNode = statusNode;
        if (statusNode.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Object)
            valueNode = value;
        if (valueNode.ValueKind != JsonValueKind.Object) return false;

        kind = valueNode.TryGetProperty("kind", out var k) ? k.GetString() : null;
        hasLastTurnOutcome = valueNode.TryGetProperty("lastTurnOutcome", out var lto)
            && lto.ValueKind is JsonValueKind.Object or JsonValueKind.String;
        released = valueNode.TryGetProperty("released", out var rel)
            && rel.ValueKind == JsonValueKind.True;
        return !String.IsNullOrWhiteSpace(kind);
    }

    public static Boolean IsSettled(
        String? statusKind,
        Boolean hasLastTurnOutcome,
        Boolean released,
        Boolean terminalEventSeen,
        Boolean everSawRunning)
    {
        if (terminalEventSeen) return true;
        if (released) return true;
        if (!String.Equals(statusKind, "idle", StringComparison.OrdinalIgnoreCase))
            return false;
        return hasLastTurnOutcome || everSawRunning;
    }

    public static Boolean IsRunningKind(String? statusKind) =>
        String.Equals(statusKind, "running", StringComparison.OrdinalIgnoreCase);

    public static async Task<OarTurnSettleResult> WaitUntilSettledAsync(
        Func<CancellationToken, Task<JsonElement>> getStatus,
        Func<Boolean> terminalEventSeen,
        TimeSpan pollInterval,
        TimeSpan timeout,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(getStatus);
        ArgumentNullException.ThrowIfNull(terminalEventSeen);
        if (pollInterval <= TimeSpan.Zero) pollInterval = DefaultPollInterval;
        if (timeout <= TimeSpan.Zero) timeout = TimeSpan.FromMinutes(10);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(timeout);
        var everSawRunning = false;
        try
        {
            while (!linked.IsCancellationRequested)
            {
                if (terminalEventSeen())
                    return OarTurnSettleResult.Settled(OarTurnSettleReason.TerminalEvent);

                JsonElement statusEl;
                try { statusEl = await getStatus(linked.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { throw; }
                catch
                {
                    await Task.Delay(pollInterval, linked.Token).ConfigureAwait(false);
                    continue;
                }

                if (TryParseAgentStatus(statusEl, out var kind, out var hasOutcome, out var released))
                {
                    if (IsRunningKind(kind)) everSawRunning = true;
                    if (IsSettled(kind, hasOutcome, released, terminalEventSeen(), everSawRunning))
                    {
                        var reason = terminalEventSeen() ? OarTurnSettleReason.TerminalEvent
                            : released ? OarTurnSettleReason.Released
                            : OarTurnSettleReason.IdleStatus;
                        return OarTurnSettleResult.Settled(reason);
                    }
                }
                await Task.Delay(pollInterval, linked.Token).ConfigureAwait(false);
            }
            return ct.IsCancellationRequested ? OarTurnSettleResult.Cancelled() : OarTurnSettleResult.TimedOut();
        }
        catch (OperationCanceledException)
        {
            return ct.IsCancellationRequested ? OarTurnSettleResult.Cancelled() : OarTurnSettleResult.TimedOut();
        }
    }
}

internal enum OarTurnSettleReason { TerminalEvent, IdleStatus, Released, TimedOut, Cancelled }

internal readonly struct OarTurnSettleResult
{
    public Boolean Ok { get; init; }
    public OarTurnSettleReason Reason { get; init; }
    public static OarTurnSettleResult Settled(OarTurnSettleReason reason) => new() { Ok = true, Reason = reason };
    public static OarTurnSettleResult TimedOut() => new() { Ok = false, Reason = OarTurnSettleReason.TimedOut };
    public static OarTurnSettleResult Cancelled() => new() { Ok = false, Reason = OarTurnSettleReason.Cancelled };
}
