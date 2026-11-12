using NewLife.Log;
using XCode;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Plugins.DbAdmin.Data;

/// <summary>
/// DbAdmin 插件自有库建表入口（单一真源，形状同 FileToolsTables）。
/// 连接串由宿主 <c>XCodeConfig.PluginDbs["DbAdmin"]="db-admin"</c> 注册；插件内禁止自注册该连接名。
/// 注意：用户连接（<c>DbAdminU_*</c>）是另一回事，由 <see cref="Services.DbConnectionService"/> 运行时 AddConnStr。
/// </summary>
public static class DbAdminTables
{
    /// <summary>连接名（与 Model.xml ConnName 一致）</summary>
    public const String ConnName = "DbAdmin";

    /// <summary>本插件全部实体类型</summary>
    public static readonly Type[] EntityTypes =
    [
        typeof(Entities.DbConnection),
        typeof(Entities.DbQueryHistory),
        typeof(Entities.DbApproval),
        typeof(Entities.DbAuditLog),
    ];

    /// <summary>确保库与表就绪（幂等）。失败返回 false 并记录错误，不静默吞。</summary>
    public static Boolean EnsureCreated()
    {
        try
        {
            EntityFactory.InitConnection(ConnName);
            var version = DAL.Create(ConnName).Db.ServerVersion;
            XTrace.Log.Info("[DbAdmin] 数据库初始化完成 (ServerVersion={0})", version);
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DbAdmin] 数据库初始化失败: {0}", ex.Message);
            return false;
        }
    }
}
