namespace ForgeSelf.Abstractions;

/// <summary>
/// 插件生命周期事件载荷：插件加载成功发 <c>plugin/loaded</c>，卸载成功发 <c>plugin/unloaded</c>。
/// </summary>
public sealed class PluginLifecycleEvent
{
    /// <summary>插件唯一标识（plugin.json 的 Id）。</summary>
    public string PluginId { get; init; } = string.Empty;

    /// <summary>插件显示名。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>插件版本。</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>动作：loaded / unloaded。</summary>
    public string Action { get; init; } = string.Empty;
}
