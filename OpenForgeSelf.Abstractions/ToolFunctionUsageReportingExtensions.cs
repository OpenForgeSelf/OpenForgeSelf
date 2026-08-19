using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace OpenForgeSelf.Abstractions;

/// <summary>
/// 工具函数使用统计上报的共享扩展方法（宿主与插件共用，消除插件对宿主程序集的依赖）。
/// 各插件的工具函数类统一复用此方法，避免在每个工具函数类中逐字复制 <c>RecordUsageAsync</c>。
/// 只依赖 <see cref="IToolFunctionExtension"/> / <see cref="IUsageStatsService"/>（均在本程序集）与
/// <see cref="IServiceProvider"/>；不引用宿主 Backend，因此插件拆为独立程序集后可直接使用。
/// </summary>
public static class ToolFunctionUsageReportingExtensions
{
    /// <summary>
    /// 记录一次工具函数使用（异步、容错）。<paramref name="serviceProvider"/> 为 null 或
    /// <see cref="IUsageStatsService"/> 未注册时静默跳过，不影响工具函数主流程。
    /// </summary>
    public static async Task RecordUsageAsync(
        this IToolFunctionExtension toolFunction,
        IServiceProvider? serviceProvider,
        string actionType,
        long durationMs,
        Dictionary<string, object>? metadata = null)
    {
        try
        {
            if (serviceProvider == null) return;

            using var scope = serviceProvider.CreateScope();
            var usageStatsService = scope.ServiceProvider.GetService<IUsageStatsService>();
            if (usageStatsService != null)
            {
                await usageStatsService.RecordUsageAsync(
                    toolFunction.PluginId,
                    toolFunction.Id,
                    actionType,
                    durationMs,
                    metadata);
            }
        }
        catch (Exception ex)
        {
            // Abstractions 不能引用 NewLife（XTrace），这里仅输出调试信息，生产环境静默跳过。
            Debug.WriteLine($"[UsageStats] 记录使用统计失败: {ex.Message}");
        }
    }
}
