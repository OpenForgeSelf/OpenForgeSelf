namespace OpenForgeSelf.Abstractions;

/// <summary>
/// 统一数据存放目录服务：宿主与插件共用，落盘位置由运行形态决定。
/// <list type="bullet">
/// <item>开发模式（ASPNETCORE_ENVIRONMENT=Development / dotnet run）：程序目录 <c>Data/</c>（调试数据不污染用户目录）。</item>
/// <item>发布 exe / Windows 服务：用户主目录 <c>~/.forgeself</c>（如 <c>C:\Users\12504\.forgeself</c>）。</item>
/// </list>
/// 所有写盘路径（SQLite 库、插件数据、配置文件 *.config、图片缓存）都应经此服务获取，避免散落写死 <c>AppContext.BaseDirectory</c> 导致单点遗漏。
/// </summary>
public interface IDataLocationService
{
    /// <summary>
    /// 数据根下的插件数据子目录名（宿主与插件库/数据的唯一真源）。
    /// 目录服务（插件数据目录）与宿主数据库路径派生共用此常量，避免两侧命名漂移。
    /// </summary>
    public const string PluginDataRootName = "Plugins";

    /// <summary>宿主全部数据/配置/缓存根目录（绝对路径）。</summary>
    string GetHostDataDirectory();

    /// <summary>插件专属数据根目录（<c>{宿主数据根}/Plugins/{pluginId}</c>，绝对路径）。</summary>
    string GetPluginDataDirectory(string pluginId);

    /// <summary>确保插件数据目录存在并返回其绝对路径（插件落盘前调用，免去各自建目录）。</summary>
    string EnsurePluginDataDirectory(string pluginId)
    {
        var dir = GetPluginDataDirectory(pluginId);
        Directory.CreateDirectory(dir);
        return dir;
    }
}
