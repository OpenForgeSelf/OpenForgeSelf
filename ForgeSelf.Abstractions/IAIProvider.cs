namespace ForgeSelf.Abstractions;

/// <summary>
/// AI 提供方契约。实现位于宿主（如 OpenAICompatibleProvider），经 <see cref="IAIProviderRegistry"/>
/// 按 chatModelId 解析；插件经 ctx.Get&lt;IAIProviderRegistry&gt;() 消费，不直接依赖宿主程序集。
/// </summary>
public interface IAIProvider
{
    string ProviderName { get; }
    AIProviderType ProviderType { get; }
    List<string> SupportedModels { get; }
    bool IsDefault { get; }

    Task<UnifiedChatResponse> ChatAsync(UnifiedChatRequest request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<UnifiedStreamChunk> ChatStreamAsync(UnifiedChatRequest request, CancellationToken cancellationToken = default);

    Task<List<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default);
}
