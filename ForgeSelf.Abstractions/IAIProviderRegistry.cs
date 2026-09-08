namespace ForgeSelf.Abstractions;

/// <summary>
/// AI 提供方注册表契约。实现位于宿主（AIProviderRegistry），由宿主经
/// PluginManager.HostProvidedServiceContracts seed 进根上下文；插件经 ctx.Get&lt;IAIProviderRegistry&gt;()
/// 消费，按 chatModelId（形如 "provider:upstreamModelId"）解析出具体 <see cref="IAIProvider"/>。
/// </summary>
public interface IAIProviderRegistry
{
    /// <summary>
    /// 按项目聊天模型 id（格式 <c>提供商:原始模型id</c>）定位提供方；未命中或被禁用返回 null（由调用方回退默认）。
    /// </summary>
    IAIProvider? GetProviderByChatModelId(string chatModelId);

    /// <summary>按模型名定位提供方（支持 "提供商:模型" 前缀）。</summary>
    IAIProvider? GetProviderByModel(string modelName);

    /// <summary>取默认提供方。</summary>
    IAIProvider? GetDefaultProvider();
}
