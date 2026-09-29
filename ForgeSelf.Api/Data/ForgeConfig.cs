using System.IO;
using NewLife.Configuration;
using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Data;

/// <summary>
/// 配置文件基类：统一把配置文件根收敛到用户数据目录
/// （开发 → 程序目录 data/config，否则 → ~/.forgeself/config；2026-09-29 输入37 目录小写统一）。
/// 继承本类的配置类自动归位，无需各自写路径（避免单个漏设）。
/// </summary>
/// <remarks>
/// NewLife <see cref="Config{TConfig}"/> 默认把配置存到 <c>Config\{Name}.config</c>（相对程序目录）。
/// 基类静态构造在 Provider 初始化后覆盖 <see cref="FileConfigProvider.FileName"/> 为绝对路径，
/// 使所有派生配置类统一归位。
/// </remarks>
public abstract class ForgeConfig<TConfig> : Config<TConfig> where TConfig : ForgeConfig<TConfig>, new()
{
    static ForgeConfig()
    {
        if (Provider is FileConfigProvider fcp)
        {
            var name = typeof(TConfig).Name;
            fcp.FileName = Path.Combine(DataLocationService.ResolveHostDataDirectory(), "config", name + ".config");
        }
    }
}
