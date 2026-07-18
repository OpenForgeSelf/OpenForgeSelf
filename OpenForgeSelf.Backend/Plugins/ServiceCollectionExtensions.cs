using OpenForgeSelf.Backend.Plugins;
using OpenForgeSelf.Backend.Plugins.Abstractions;
using OpenForgeSelf.Backend.Plugins.Services;
using Microsoft.Extensions.DependencyInjection;

namespace OpenForgeSelf.Backend.Plugins;

/// <summary>
/// 插件服务依赖注入扩展方法
/// </summary>
public static class PluginServiceCollectionExtensions
{
    /// <summary>
    /// 添加插件管理器服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="pluginsDirectory">插件目录路径</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddPluginManager(this IServiceCollection services, string? pluginsDirectory = null)
    {
        services.AddSingleton<IPermissionChecker, DefaultPermissionChecker>();
        services.AddSingleton<PluginManager>();
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
                PluginsDirectory = Path.Combine(AppContext.BaseDirectory, "Plugins")
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
