using NewLife.Log;
using XCode;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Plugins.TodoTracker.Data;

/// <summary>
/// TodoTracker 插件的建表入口（单一真源）。
///
/// 为什么必须插件自己建表：宿主 <c>XCodeConfig.EnsureTablesCreated</c> 只扫「当前已加载程序集」，
/// 插件 DLL 加载晚于宿主建表 → 插件实体不在其列，生产库会出现「表神秘缺失 → no such table」
/// （plugin-development 铁律 12；AgentHub 曾因此功能全废）。
/// 原实现只在 <c>Apply</c> 里取一次 <c>Todo.Meta.TableName</c> 触发静态构造，属"靠宿主顺手建表"，PILOT-054 起改为自持。
///
/// 形状取仓库实采先例 <c>Plugins/FileTools/Data/FileToolsTables.cs</c> / <c>AgentHubTables.cs</c>：
/// 用 <see cref="EntityFactory.InitConnection"/> 全量建表，再探活确认；
/// ⚠ 不用 <c>TableItem.Create(...).DataTable</c> + <c>dal.SetTables(...)</c>（该写法在本仓 0 命中，照抄编译不过）。
/// ⚠ 探活禁用 <c>dal.Session.Query("SELECT 1")</c>（库文件尚未创建时抛 NullReferenceException）。
///
/// 连接串由宿主 <c>ForgeSelf.Api/Data/XCodeConfig.cs</c> 的 <c>PluginDbs["TodoTracker"]="todo-tracker"</c>
/// 统一注册，库文件落 <c>{数据根}/Plugins/todo-tracker/TodoTracker.db</c>；插件内禁止自注册 <c>DAL.AddConnStr</c>。
/// </summary>
public static class TodoTrackerTables
{
    /// <summary>连接名（与实体 BindTable 的 ConnName、Model.xml 的 ConnName 一致）</summary>
    public const String ConnName = "TodoTracker";

    /// <summary>本插件全部实体类型（新增实体必须登记在此）</summary>
    public static readonly Type[] EntityTypes =
    [
        typeof(Entities.Todo),
        typeof(Entities.TaskExecution)
    ];

    /// <summary>确保 TodoTracker 库与两张表就绪（幂等）。失败绝不静默吞：返回 false 并由调用方告警。</summary>
    public static Boolean EnsureCreated()
    {
        try
        {
            // 必须先全量建表再探活：首个实体在连接未就绪时只会建空库不建表（时序坑，铁律 12）
            EntityFactory.InitConnection(ConnName);

            var dal = DAL.Create(ConnName);
            var version = dal.Db.ServerVersion;
            XTrace.Log.Info("[todo-tracker] 数据库就绪 (ConnName={0} ServerVersion={1})", ConnName, version);
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[todo-tracker] 建表失败: {0}", ex.Message);
            return false;
        }
    }
}
