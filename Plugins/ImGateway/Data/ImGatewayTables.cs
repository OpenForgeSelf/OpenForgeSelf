using ForgeSelf.Api.Plugins.ImGateway.Entities;
using NewLife.Log;
using XCode;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Plugins.ImGateway.Data;

/// <summary>
/// IM 网关插件建表唯一真源。
/// 采用项目既有范式（Scheduler / ScriptRunner / AgentHub 已跑通）：**不手动建表**，
/// 只需 <c>DAL.Create(conn).Db.ServerVersion</c> 打开一次连接；
/// XCode 在 <c>Migration=On</c> 下会于首次连接时自动完成建库建表。
/// 宿主建库发生在 app.Build() 之后，而插件 Apply 在 builder.Build() 阶段执行，
/// 若此时不主动探活，插件内任何实体查询都会命中"库文件存在但无表"的空库，报 no such table。
/// ⚠ 不要用「反射取 Meta 属性 → CreateTable/Resolve」那类写法（XCode 12 下静默失效）。
/// </summary>
public static class ImGatewayTables
{
    /// <summary>插件数据连接名（独立 SQLite 库，与宿主数据库解耦）。</summary>
    public const string ConnName = "ImGateway";

    /// <summary>本插件全部实体类型（新增实体必须登记在此）。</summary>
    public static readonly Type[] EntityTypes =
    {
        typeof(ImGatewayChannel),
        typeof(ImGatewaySession),
        typeof(ImGatewayProcessedMsg),
    };

    /// <summary>
    /// 确保库与表就绪（幂等）。插件启动时调用一次。
    /// </summary>
    public static void EnsureCreated()
    {
        try
        {
            var dal = DAL.Create(ConnName);
            // 探活即建表：Migration=On 时 XCode 在首次连接自动检查并创建缺失的表
            var version = dal.Db.ServerVersion;
            XTrace.Log.Info("[ImGateway] 数据库初始化完成 (ServerVersion={0}, 实体={1})", version, EntityTypes.Length);
        }
        catch (Exception ex)
        {
            // 不静默：失败必须显式 Error，否则症状是"部分表存在、某张表神秘缺失"，与真因相距极远
            XTrace.Log.Error("[ImGateway] 数据库初始化失败: {0}", ex.Message);
            throw;
        }
    }
}
