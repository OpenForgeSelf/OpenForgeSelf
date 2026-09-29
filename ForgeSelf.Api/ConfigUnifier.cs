using System.IO;
using System.Reflection;
using NewLife;
using NewLife.Agent;
using NewLife.Configuration;
using XCode;

namespace ForgeSelf.Api;

/// <summary>
/// 统一收敛所有 NewLife <see cref="Config{TConfig}"/> 配置文件的落盘位置。
/// </summary>
/// <remarks>
/// 在宿主启动时（必须早于任何 <c>Config&lt;T&gt;.Current</c> 访问）调用一次，
/// 把已知框架/项目 <see cref="Config{TConfig}"/> 子类的 <see cref="FileConfigProvider.FileName"/>
/// 重定向到数据根的 <c>Config/</c> 子目录，避免 XCode.config / Core.config / Agent.config 等散落到程序目录或输出目录。
/// <para>
/// 分工：项目自有配置类应继承 <see cref="Data.ForgeConfig{TConfig}"/>（静态构造自动覆盖，仍列于此处便于统一与可读）；
/// 本类负责框架自带配置类（XCodeSetting、NewLife.Setting、NewLife.Agent.Setting 等）。
/// 一处设置、全部生效，新增库的配置文件不会漏设。
/// </para>
/// <para>
/// 注：<see cref="Config{TConfig}.Provider"/> 是开放泛型基类上的静态属性，反射 GetValue/SetValue 会被 .NET 禁止
/// （ContainsGenericParameters），故此处用编译期已知类型直接访问 .Provider，而非反射遍历设置。
/// 新增框架 Config&lt;T&gt; 需收敛时，在 <see cref="KnownConfigs"/> 追加一项即可。
/// </para>
/// </remarks>
public static class ConfigUnifier
{
    // 已知 Config<T> 子类（框架 + 项目）：Type 用于文件名推算，GetProvider 用于取Provider设置路径。
    // 项目自有配置类应继承 Data.ForgeConfig<TConfig>（自动覆盖，仍列于此便于统一与可读）。
    // 新增框架 Config<T> 需收敛时，在此追加一项（Type 取类型，GetProvider 返回该类型的 Provider）。
    private static readonly (Type Type, Func<IConfigProvider?> GetProvider)[] KnownConfigs =
    {
        (typeof(XCodeSetting), () => XCodeSetting.Provider),
        (typeof(NewLife.Setting), () => NewLife.Setting.Provider),
        (typeof(NewLife.Agent.Setting), () => NewLife.Agent.Setting.Provider),
        (typeof(Models.ForgeSetting), () => Models.ForgeSetting.Provider)
    };

    /// <summary>
    /// 把已知 Config&lt;T&gt; 子类的配置文件路径统一重定向到 configDirectory。
    /// </summary>
    /// <param name="configDirectory">统一配置目录（绝对路径）；不存在则创建。</param>
    /// <remarks>
    /// B9-5 健壮化（B7 立项）：目录创建/重定向/落盘任一失败（如目录被运行中实例句柄占用、路径不可写）
    /// 一律<b>降级 + 告警</b>，不抛异常炸掉 WebApplicationFactory / 宿主启动——配置统一是体验优化，
    /// 不值得让宿主起不来；NewLife 框架会按默认路径兜底工作。
    /// </remarks>
    public static void UnifyAllConfigFiles(string configDirectory)
    {
        if (string.IsNullOrWhiteSpace(configDirectory))
            throw new ArgumentNullException(nameof(configDirectory));

        try
        {
            Directory.CreateDirectory(configDirectory);
        }
        catch (Exception ex)
        {
            NewLife.Log.XTrace.Log.Warn(
                "[ConfigUnifier] 配置目录不可用（{0}），跳过配置统一并降级为框架默认路径: {1}", configDirectory, ex.Message);
            return;
        }

        foreach (var (type, getProvider) in KnownConfigs)
        {
            try
            {
                var provider = getProvider();
                if (provider is FileConfigProvider fcp)
                {
                    fcp.FileName = Path.Combine(configDirectory, GetConfigName(type) + ".config");
                }
            }
            catch (Exception ex)
            {
                // 单个配置类重定向失败不影响其余（降级为该配置类走默认路径）
                NewLife.Log.XTrace.Log.Warn(
                    "[ConfigUnifier] 重定向 {0} 失败（已降级）: {1}", type.Name, ex.Message);
            }
        }

        // 重定向后立即强制框架配置落盘到统一目录。
        // NewLife Config<T>.Current 仅在「文件已存在(!IsNew) 或 Runtime.CreateConfigOnMissing」时才 Save()，
        // 故 Core 等配置加载后不会自动写文件，统一目录迟迟不齐、分散判定困难，违背「所有 Config 统一、不漏」初衷。
        // 此处 .Current 加载后再显式 Save()，强制持久化到重定向后的统一路径。
        // B9-5：Save 失败（文件被占用等）降级告警——宿主/测试启动不受阻，配置文件留在可用的旧位置。
        TrySaveCurrent("XCodeSetting", () => XCodeSetting.Provider is FileConfigProvider, () => XCodeSetting.Current.Save());
        TrySaveCurrent("NewLife.Setting", () => NewLife.Setting.Provider is FileConfigProvider, () => NewLife.Setting.Current.Save());
        TrySaveCurrent("NewLife.Agent.Setting", () => NewLife.Agent.Setting.Provider is FileConfigProvider, () => NewLife.Agent.Setting.Current.Save());
        // ForgeSetting 由宿主 DI/启动流程负责落盘（涉及密钥加密，时机在配置统一之后），
        // 不在此提前触发，避免早于加密服务就绪而被调用。
    }

    /// <summary>B9-5：带降级的配置落盘——Save 异常仅告警不抛出。</summary>
    private static void TrySaveCurrent(string name, Func<bool> isFileProvider, Action save)
    {
        try
        {
            if (!isFileProvider()) return;
            save();
        }
        catch (Exception ex)
        {
            NewLife.Log.XTrace.Log.Warn(
                "[ConfigUnifier] {0} 配置落盘失败（已降级，宿主继续启动）: {1}", name, ex.Message);
        }
    }

    /// <summary>
    /// 复刻 NewLife <see cref="Config{TConfig}"/> 静态构造的命名规则：
    /// 优先取 <see cref="ConfigAttribute"/> 的 Name；否则取类型名并去除 Config/Setting 后缀。
    /// 保证重定向后的文件名与 NewLife 默认写入的文件名完全一致，旧位置不残留可用文件。
    /// </summary>
    private static string GetConfigName(Type type)
    {
        var att = type.GetCustomAttribute<ConfigAttribute>(true)?.Name;
        if (!string.IsNullOrEmpty(att))
        {
            // 个别框架 Config<T> 的 ConfigAttribute.Name 已含扩展名（如 NewLife.Agent.Setting 的 "Agent.config"），
            // 先剥离扩展名，由调用方统一追加 .config，避免双重扩展名（Agent.config.config）。
            const string ext = ".config";
            return att!.EndsWith(ext, StringComparison.OrdinalIgnoreCase)
                ? att.Substring(0, att.Length - ext.Length)
                : att;
        }

        var name = type.Name;
        if (name.EndsWith("Config", StringComparison.Ordinal) && name != "Config")
            name = name.Substring(0, name.Length - "Config".Length);
        if (name.EndsWith("Setting", StringComparison.Ordinal) && name != "Setting")
            name = name.Substring(0, name.Length - "Setting".Length);
        return name;
    }
}
