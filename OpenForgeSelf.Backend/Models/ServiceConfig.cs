namespace OpenForgeSelf.Backend.Models;

/// <summary>
/// Windows 服务配置，从 appsettings.json 的 "Service" 节读取。
/// </summary>
public class ServiceConfig
{
    /// <summary>Windows 服务标识名（如 "OpenForgeSelf"）</summary>
    public string ServiceName { get; set; } = "OpenForgeSelf";

    /// <summary>Windows 服务显示名称（如 "OpenForgeSelf Server"）</summary>
    public string DisplayName { get; set; } = "OpenForgeSelf Server";

    /// <summary>服务描述</summary>
    public string Description { get; set; } = "铸己匣 OpenForgeSelf 后端服务";

    /// <summary>安装后是否自动启动</summary>
    public bool AutoStart { get; set; } = true;

    /// <summary>服务模式下是否显示托盘图标</summary>
    public bool ShowTrayInServiceMode { get; set; } = false;
}