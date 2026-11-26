using System.Text.Json.Nodes;
using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Services.AI.Proxy;

/// <summary>
/// 出站前按提供方改报文/头。新厂商加一个实现并注册 <see cref="ILlmUpstreamAffinity"/>，不要往管道里堆 if。
/// </summary>
public interface ILlmUpstreamAffinity
{
    bool Matches(LlmAffinityContext ctx);

    void Apply(LlmAffinityContext ctx);
}

/// <summary>一次出站的可变上下文。策略只改 <see cref="Body"/> 和 <see cref="OutboundHeaders"/>。</summary>
public sealed class LlmAffinityContext
{
    public required string ProviderName { get; init; }

    public required string Endpoint { get; init; }

    public required string UpstreamModelId { get; init; }

    public required LlmWireProtocol Protocol { get; init; }

    public required JsonObject Body { get; init; }

    public required IReadOnlyDictionary<string, string> InboundHeaders { get; init; }

    public Dictionary<string, string> OutboundHeaders { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool TryInbound(string name, out string value)
    {
        foreach (var pair in InboundHeaders)
        {
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(pair.Value))
            {
                value = pair.Value;
                return true;
            }
        }

        value = "";
        return false;
    }
}

/// <summary>按注册顺序应用所有命中的策略（一家提供方通常只命中一个）。</summary>
public sealed class LlmUpstreamAffinityChain
{
    private readonly ILlmUpstreamAffinity[] _policies;

    public LlmUpstreamAffinityChain(IEnumerable<ILlmUpstreamAffinity>? policies)
        => _policies = policies?.ToArray() ?? [];

    public void Apply(LlmAffinityContext ctx)
    {
        foreach (var policy in _policies)
        {
            if (policy.Matches(ctx))
                policy.Apply(ctx);
        }
    }
}
