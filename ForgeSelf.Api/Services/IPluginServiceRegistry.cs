using Microsoft.Extensions.DependencyInjection;

namespace ForgeSelf.Api.Services;

/// <summary>
/// 插件可变 MS DI 容器注册表（插件服务提供器与宿主隔绝）。
/// 宿主容器通过 <see cref="CollectForwardDescriptors"/> 产出的转发描述符（一律 Transient）把插件服务类型
/// 路由到本注册表；本注册表按 <see cref="Resolve(Type)"/> 索引到对应插件内置好的子 <see cref="IServiceProvider"/>。
/// 每个子 provider 仅含插件自身注册的服务，不透传宿主服务——插件与宿主通过 Abstractions 契约接口交互，
/// 生命周期语义完全隔离。
/// 插件卸载（<see cref="Unmount"/>）后其服务类型从索引摘除并 Dispose 子 provider，宿主解析即失败（不再返回旧实例）。
/// </summary>
public interface IPluginServiceRegistry
{
    /// <summary>挂载插件服务集合：记录集合并为每个 ServiceType 建立索引（同类型后注册覆盖前注册）。不会立即构建 provider。</summary>
    void Mount(string pluginId, IServiceCollection services);

    /// <summary>卸载插件：Dispose 该插件子 provider 并摘除其所有 ServiceType 索引（仅当仍指向本插件时）。</summary>
    void Unmount(string pluginId);

    /// <summary>按服务类型解析：查索引取该插件子 provider 的 GetService；未索引/未构建返回 null。</summary>
    object? Resolve(Type serviceType);

    /// <summary>
    /// 产出宿主转发描述符（去重 ServiceType）。所有转发一律 <see cref="ServiceLifetime.Transient"/>，
    /// 由子 provider 自身管理真实生命周期；卸载后工厂再次执行即抛 <see cref="InvalidOperationException"/>。
    /// </summary>
    IEnumerable<ServiceDescriptor> CollectForwardDescriptors();

    /// <summary>
    /// 宿主应用 <c>Build()</c> 之后调用一次：为所有已挂载插件构建子 provider。
    /// 之后新挂载的插件（热启用/重挂载）会在 <see cref="Mount"/> 内直接构建。
    /// 每个子 provider 仅含插件自身注册的服务（含 <see cref="IContext"/>），不透传宿主服务；
    /// 插件服务经 <see cref="IContext"/> 运行期获取宿主 seed 的 curated 契约。
    /// </summary>
    void BuildAll();

    /// <summary>插件是否已挂载（供热启用路径幂等复用判断）。</summary>
    bool IsMounted(string pluginId);
}
