using System.Runtime.CompilerServices;
using OpenForgeSelf.Backend.Services.AI.Models;

namespace OpenForgeSelf.Backend.Services.AI;

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
