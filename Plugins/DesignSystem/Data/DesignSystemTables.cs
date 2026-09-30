using NewLife.Log;
using XCode;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Plugins.DesignSystem.Data;

/// <summary>
/// DesignSystem 插件的建表入口（单一真源）。
///
/// 为什么插件要自己建表：宿主 <c>XCodeConfig.EnsureTablesCreated</c> 只扫「当前已加载程序集」，
/// 而插件 <c>Apply</c> 与插件 DLL 的加载都晚于宿主建表 → 本插件的 12 张表不会被宿主建出来，
/// 结果是「库文件存在但无表」，首个查询即 <c>no such table</c>（建表铁律 12）。
///
/// 库文件位置由宿主 <c>XCodeConfig.PluginDbs</c> 登记（连接名 DesignSystem → 插件 id design-system），
/// 落 <c>{数据根}/Plugins/design-system/DesignSystem.db</c>，与 <c>ctx.EnsurePluginDataDirectory()</c> 重合。
/// </summary>
public static class DesignSystemTables
{
    /// <summary>XCode 连接名（须与实体 BindTable 的 ConnName、宿主 PluginDbs 登记项一致）</summary>
    public const String ConnName = "DesignSystem";

    /// <summary>全部实体类型（新增实体必须登记在此，供测试自检表可用性）</summary>
    public static readonly Type[] EntityTypes =
    [
        typeof(Entities.DesignProject),
        typeof(Entities.DesignTheme),
        typeof(Entities.DesignToken),
        typeof(Entities.DesignShadowLayer),
        typeof(Entities.DesignComponent),
        typeof(Entities.DesignComponentVariant),
        typeof(Entities.DesignIcon),
        typeof(Entities.DesignAsset),
        typeof(Entities.DesignScreen),
        typeof(Entities.DesignFontFace),
        typeof(Entities.DesignAudit),
        typeof(Entities.DesignRelease),
    ];

    /// <summary>
    /// 确保 DesignSystem 库与 12 张表就绪（幂等，可重复调用）。
    /// 用 <see cref="EntityFactory.InitConnection"/> 全量建表：不依赖「实体 Meta 首次初始化」的按需自动建表。
    /// </summary>
    /// <returns>连接是否成功打开并完成建表</returns>
    public static Boolean EnsureCreated()
    {
        try
        {
            EntityFactory.InitConnection(ConnName);

            // 先开库再判建表结果：ServerVersion 触发物理连接打开，否则可能只落空库文件
            var dal = DAL.Create(ConnName);
            var version = dal.Db.ServerVersion;
            XTrace.Log.Info("[DesignSystem] 数据库初始化完成 (ServerVersion={0})", version);
            return true;
        }
        catch (Exception ex)
        {
            // 绝不静默吞：吞掉后症状是「部分表存在、某张表神秘缺失」，与真因相距极远
            XTrace.Log.Error("[DesignSystem] 数据库初始化失败: {0}", ex.Message);
            return false;
        }
    }
}
