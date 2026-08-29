namespace ForgeSelf.Abstractions;

/// <summary>
/// 定时任务调度器宿主契约（实现：Scheduler 插件 <c>TaskScheduler</c>）。
/// 宿主 AppBuilder 经此接口在应用启动后启动/停止 DI 单例调度器，避免直接依赖 Scheduler 插件程序集（ADR D2）。
/// 插件内部仍使用其自身的 <c>ITaskScheduler</c> 完整契约。
/// </summary>
public interface ISchedulerHost
{
    Task StartAsync();
    Task StopAsync();
}
