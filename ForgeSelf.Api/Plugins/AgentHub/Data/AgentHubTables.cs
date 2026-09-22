using NewLife.Log;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Plugins.AgentHub.Data;

/// <summary>
/// AgentHub 插件的建表入口（单一真源）。
///
/// 项目既有范式（Scheduler / ScriptRunner 等已跑通插件）：**不手动建表**，
/// 只需 <c>DAL.Create(conn).Db.ServerVersion</c> 打开一次连接；
/// XCode 在 <c>Migration=On</c> 下会于首次连接时自动完成建库建表。
/// 宿主 <c>XCodeConfig.InitializeXCodeDatabase</c> 对每个连接名做的也正是这件事。
///
/// 插件为何要自己做这一步：宿主建库发生在 <c>app.Build()</c> 之后，
/// 而插件 <c>Apply</c> 在 <c>builder.Build()</c> 阶段执行 —— 插件跑在宿主建表之前。
/// 若此时不主动探活，插件内任何实体查询都会命中"库文件存在但无表"的空库，报 no such table。
///
/// ⚠ 不要用「反射取 Meta 属性 → CreateTable/Resolve」那类写法：
/// XCode 12 下 <c>Meta</c> 不是可取值的静态属性，该路径静默失效（历史遗留写法，容易误以为已建表）。
/// </summary>
public static class AgentHubTables
{
    /// <summary>AgentHub 插件的 XCode 连接名（与实体 BindTable 的 ConnName 一致）</summary>
    public const String ConnName = "AgentHub";

    /// <summary>AgentHub 的全部实体类型（新增实体必须登记在此，供测试自检表可用性）</summary>
    public static readonly Type[] EntityTypes =
    [
        typeof(Entities.AgentDefinition),
        typeof(Entities.AgentAccessPoint),
        typeof(Entities.DelegationTask),
        typeof(Entities.DelegationEvent)
    ];

    /// <summary>
    /// 确保 AgentHub 库与表就绪（幂等）。
    /// 打开一次连接即可触发 XCode 的自动建表（与 Scheduler/ScriptRunner 同款做法）。
    /// </summary>
    /// <returns>连接是否成功打开（含建表）</returns>
    public static Boolean EnsureCreated()
    {
        try
        {
            var dal = DAL.Create(ConnName);

            // 探活即建表：Migration=On 时 XCode 在首次连接自动检查并创建缺失的表。
            // 与宿主 XCodeConfig 及各插件的初始化写法一致。
            var version = dal.Db.ServerVersion;
            XTrace.Log.Info("[AgentHub] 数据库初始化完成 (ServerVersion={0})", version);

            return true;
        }
        catch (Exception ex)
        {
            // 不静默：失败必须显式 Error，否则症状是"部分表存在、某张表神秘缺失"，与真因相距极远
            XTrace.Log.Error("[AgentHub] 数据库初始化失败: {0}", ex.Message);
            return false;
        }
    }
}
