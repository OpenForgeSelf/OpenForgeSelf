using ForgeSelf.Api.Plugins;
using ForgeSelf.Api.Plugins.Abstractions;
using ForgeSelf.Api.Plugins.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeSelf.Api.Plugins;

/// <summary>
/// 插件服务依赖注入扩展方法
/// </summary>
public static class PluginServiceCollectionExtensions
{
    /// <summary>
    /// 添加插件管理器服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="existing">已构造的 <see cref="PluginManager"/> 实例（启动装载时复用 bootstrap 创建的实例，避免重复注册）。为 null 时按类型注册。</param>
    /// <param name="pluginsDirectory">插件目录路径</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddPluginManager(
        this IServiceCollection services,
        PluginManager? existing = null,
        string? pluginsDirectory = null)
    {
        services.AddSingleton<IPermissionChecker, DefaultPermissionChecker>();

        if (existing != null)
        {
            services.AddSingleton(existing);
        }
        else
        {
            services.AddSingleton<PluginManager>();
        }

        services.AddSingleton<ExtensionPointManager>();
        services.AddSingleton<PluginVersionService>();
        services.AddSingleton<PluginPackagerService>();
        services.AddSingleton<PluginInstallerService>();
        services.AddSingleton<PluginScaffolderService>();

        if (!string.IsNullOrEmpty(pluginsDirectory))
        {
            services.AddSingleton(new PluginOptions
            {
                PluginsDirectory = pluginsDirectory
            });
        }
        else
        {
            services.AddSingleton(sp => new PluginOptions
            {
                PluginsDirectory = Path.Combine(AppContext.BaseDirectory, "plugins")
            });
        }

        return services;
    }
}

/// <summary>
/// 插件配置选项
/// </summary>
public class PluginOptions
{
    /// <summary>
    /// 获取或设置插件目录路径
    /// </summary>
    public string PluginsDirectory { get; set; } = string.Empty;
}
