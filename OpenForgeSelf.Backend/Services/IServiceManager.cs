using System.ServiceProcess;

namespace OpenForgeSelf.Backend.Services;

/// <summary>
/// Windows 服务管理器接口。
/// 封装服务的安装/卸载/状态检测/启停控制，用于解耦 <see cref="TrayIconManager"/>
/// 对 <see cref="ServiceManager"/> 的直接依赖，便于单元测试。
/// 参考 data-model.md §1 ServiceInstallState 状态驱动逻辑：
///   - IsInstalled == false → 菜单显示「安装服务」
///   - IsInstalled == true  → 菜单显示「卸载服务」、「重启服务」
/// </summary>
public interface IServiceManager
{
    /// <summary>查询服务是否已安装到 SCM。</summary>
    bool IsInstalled();

    /// <summary>获取服务当前运行状态。若服务未安装，返回 <see cref="ServiceControllerStatus.Stopped"/>。</summary>
    ServiceControllerStatus GetStatus();

    /// <summary>安装 Windows 服务。需要管理员权限。</summary>
    void Install();

    /// <summary>卸载 Windows 服务。需要管理员权限。</summary>
    void Uninstall();

    /// <summary>重启服务：停止后启动。</summary>
    void Restart();

    /// <summary>启动服务。</summary>
    void Start();

    /// <summary>停止服务。</summary>
    void Stop();
}