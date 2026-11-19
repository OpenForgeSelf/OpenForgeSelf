namespace ForgeSelf.Api.Plugins.AgentHub.Services;

/// <summary>
/// 按 AccessPoint.Transport 选路的传输注册表（ADR-007 / 架构真源 §6）。
/// 禁止再 DI 单一 <see cref="IAgentTransport"/> 导致多实现无法选路。
/// </summary>
public interface IAgentTransportRegistry
{
    /// <summary>登记一种 transport（同 Kind 后写覆盖）。</summary>
    void Register(IAgentTransport transport);

    /// <summary>
    /// 按 Kind 解析（大小写不敏感）。空/未知默认回落 Cli（若已登记）。
    /// 找不到时抛 <see cref="KeyNotFoundException"/>。
    /// </summary>
    IAgentTransport Resolve(string? kind);

    /// <summary>已登记的 Kind 列表。</summary>
    IReadOnlyCollection<string> RegisteredKinds { get; }
}
