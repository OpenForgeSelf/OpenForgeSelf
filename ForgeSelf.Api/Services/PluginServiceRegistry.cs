using System.Collections.Concurrent;
using ForgeSelf.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeSelf.Api.Services;

/// <summary>
/// <see cref="IPluginServiceRegistry"/> 的默认实现（宿主单例）。
/// 索引 ServiceType → pluginId；每个插件持有独立的子 provider（含插件自身注册的服务）。
/// 彻底 Cordis 模式：本注册表<b>不再</b>向子容器转发宿主契约描述符。
/// 插件服务的宿主依赖经由 Cordis <see cref="IContext"/> 在运行期以 <c>ctx.Get&lt;T&gt;()</c> 获取
/// （宿主在根 <c>Context</c> 中 seed 的 curated 契约，见 <c>PluginManager.ProvideHostServices</c>）；
/// 插件 Service 构造函数注入 <see cref="IContext"/> 即可经父级链继承宿主 seed 的契约，无需 MS DI 转发。
/// 因此 <see cref="BuildAll"/> 改为无参，子容器仅承载插件自有服务（含 <see cref="IContext"/>）。
/// </summary>
public sealed class PluginServiceRegistry : IPluginServiceRegistry
{
    private readonly ConcurrentDictionary<Type, string> _index = new();
    private readonly ConcurrentDictionary<string, IServiceCollection> _collections = new();
    private readonly ConcurrentDictionary<string, IServiceProvider> _providers = new();
    private readonly object _buildLock = new();

    /// <inheritdoc />
    public void Mount(string pluginId, IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // 幂等重挂载：先清理该插件旧挂载，避免残留旧 provider 与旧索引。
        Unmount(pluginId);

        _collections[pluginId] = services;

        foreach (var descriptor in services)
        {
            _index[descriptor.ServiceType] = pluginId;
        }

        // BuildAll 已完成时，热启用/重挂载路径需立即构建，避免该插件服务解析失败。
        if (_built)
        {
            var provider = BuildPluginProvider(services);
            _providers.AddOrUpdate(pluginId, provider, (_, old) =>
            {
                (old as IDisposable)?.Dispose();
                return provider;
            });
        }
    }

    /// <inheritdoc />
    public void Unmount(string pluginId)
    {
        foreach (var entry in _index.Where(e => e.Value == pluginId).ToList())
        {
            _index.TryRemove(entry.Key, out _);
        }

        _collections.TryRemove(pluginId, out _);

        if (_providers.TryRemove(pluginId, out var provider))
        {
            (provider as IDisposable)?.Dispose();
        }
    }

    /// <inheritdoc />
    public object? Resolve(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        return _index.TryGetValue(serviceType, out var pluginId)
            && _providers.TryGetValue(pluginId, out var provider)
            ? provider.GetService(serviceType)
            : null;
    }

    /// <inheritdoc />
    public string? GetOwnerPluginId(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return _index.TryGetValue(serviceType, out var pluginId) ? pluginId : null;
    }

    /// <inheritdoc />
    public IEnumerable<ServiceDescriptor> CollectForwardDescriptors()
    {
        foreach (var serviceType in _index.Keys)
        {
            // 「插件→插件」契约（如 IWorkflowAIAdvisor）不进宿主转发（调研 §5.6 裁决 E）：
            // 消费方在兄弟插件内经 ctx.Get<T>() 于运行期解析，宿主转发会造成同一契约双通道解析
            //（且 Transient 工厂会破坏 eager 单例语义）。
            if (serviceType == typeof(IWorkflowAIAdvisor))
                continue;

            yield return ServiceDescriptor.Describe(
                serviceType,
                _ => Resolve(serviceType) ?? throw new InvalidOperationException($"插件服务已卸载: {serviceType}"),
                ServiceLifetime.Transient);
        }
    }

    /// <inheritdoc />
    public void BuildAll()
    {
        lock (_buildLock)
        {
            _built = true;

            foreach (var kvp in _collections)
            {
                var provider = BuildPluginProvider(kvp.Value);
                _providers.AddOrUpdate(kvp.Key, provider, (_, old) =>
                {
                    (old as IDisposable)?.Dispose();
                    return provider;
                });
            }
        }
    }

    /// <inheritdoc />
    public bool IsMounted(string pluginId) => _collections.ContainsKey(pluginId);

    private bool _built;

    /// <summary>
    /// 构建单个插件的子 provider：仅含插件自身注册的服务（含 <see cref="IContext"/>）。
    /// 彻底 Cordis 模式，<b>不</b>转发宿主契约描述符——插件服务的宿主依赖经
    /// <see cref="IContext"/> 在运行期以 <c>ctx.Get&lt;T&gt;()</c> 获取，而非 MS DI 构造注入。
    /// </summary>
    private IServiceProvider BuildPluginProvider(IServiceCollection pluginServices)
    {
        var child = new ServiceCollection();
        IList<ServiceDescriptor> list = child;
        foreach (var descriptor in pluginServices)
        {
            list.Add(descriptor);
        }

        // 显式关闭作用域校验：插件子 provider 是「每上下文单例」容器（Cordis 设计，docs/02-features/027-cordis-kernel.md：
        // 插件服务 = 每上下文单例，不实现伪 Scoped）。其 scoped 服务在此容器中解析为<b>俘获单例</b>（与宿主请求
        // scope 隔离），属预期语义；若开启 ValidateScopes，宿主在 Development 默认开启校验，会从该 root provider
        // 解析 scoped 时抛「Cannot resolve scoped service ... from root provider」→ 500。关闭后该异常不会触发，
        // 且语义与「每上下文单例」一致。此选项与默认一致（手动 BuildServiceProvider 默认 false），显式写出以锁定契约、
        // 防止未来误开校验重引入 500。
        return child.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = false });
    }
}
