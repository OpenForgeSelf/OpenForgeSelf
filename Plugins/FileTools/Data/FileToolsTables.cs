using NewLife.Log;
using XCode;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Plugins.FileTools.Data;

/// <summary>
/// FileTools 插件的建表入口（单一真源）。批次C 起 FileTools 首次拥有插件库
/// （<c>{数据根}/Plugins/file-tools/FileTools.db</c>，连接名 FileTools）。
///
/// 形状取仓库实采先例 <c>Plugins/AgentHub/Data/AgentHubTables.cs</c>：
/// 用 <see cref="EntityFactory.InitConnection"/> 全量建表，再探活确认。
/// ⚠ 不采用 plugin-development 铁律12 文字里的 <c>TableItem.Create(...).DataTable</c> +
/// <c>dal.SetTables(...)</c> —— 该写法在本仓 0 命中（仅存在于 XCodeTestFixture 的注释里），
/// 照抄不会建表（规格偏差记录 D-1）。
///
/// 宿主侧连接串由 <c>ForgeSelf.Api/Data/XCodeConfig.cs</c> 的 <c>PluginDbs</c> 注册（本批已加
/// <c>["FileTools"]="file-tools"</c>）。插件内禁止自注册 DAL.AddConnStr（会造出第二份互不可见的库）。
/// </summary>
public static class FileToolsTables
{
    /// <summary>连接名（与实体 BindTable 的 ConnName、Model.xml 的 ConnName 一致）</summary>
    public const String ConnName = "FileTools";

    /// <summary>本插件全部实体类型（新增实体必须登记在此）</summary>
    public static readonly Type[] EntityTypes =
    [
        typeof(Entities.ScanSnapshot),
        typeof(Entities.ScanFolderEntry)
    ];

    /// <summary>确保 FileTools 库与表就绪（幂等）。失败不静默吞：返回 false 并由调用方告警。</summary>
    public static Boolean EnsureCreated()
    {
        try
        {
            EntityFactory.InitConnection(ConnName);

            var dal = DAL.Create(ConnName);
            // 探活统一用 ServerVersion：Session.Query("SELECT 1") 在库文件尚未创建时抛 NRE，会误报初始化失败
            var version = dal.Db.ServerVersion;
            XTrace.Log.Info("[FileTools] 数据库初始化完成 (ServerVersion={0})", version);
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[FileTools] 数据库初始化失败: {0}", ex.Message);
            return false;
        }
    }
}
