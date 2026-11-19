using System.Collections.Concurrent;

namespace ForgeSelf.Api.Plugins.AgentHub.Services;

/// <summary><see cref="IAgentTransportRegistry"/> 默认实现（进程内字典）。</summary>
public sealed class AgentTransportRegistry : IAgentTransportRegistry
{
    private readonly ConcurrentDictionary<string, IAgentTransport> _map =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public IReadOnlyCollection<string> RegisteredKinds => _map.Keys.ToArray();

    /// <inheritdoc />
    public void Register(IAgentTransport transport)
    {
        ArgumentNullException.ThrowIfNull(transport);
        if (string.IsNullOrWhiteSpace(transport.Kind))
            throw new ArgumentException("transport.Kind 不能为空", nameof(transport));
        _map[transport.Kind] = transport;
    }

    /// <inheritdoc />
    public IAgentTransport Resolve(string? kind)
    {
        var key = string.IsNullOrWhiteSpace(kind) ? "Cli" : kind.Trim();
        if (_map.TryGetValue(key, out var t))
            return t;

        // 未知 Kind：优先回落 Cli，便于旧 AccessPoint 与测试替身
        if (!key.Equals("Cli", StringComparison.OrdinalIgnoreCase)
            && _map.TryGetValue("Cli", out var cli))
            return cli;

        throw new KeyNotFoundException($"未登记的 Agent Transport：{key}（已登记：{string.Join(',', _map.Keys)}）");
    }
}
