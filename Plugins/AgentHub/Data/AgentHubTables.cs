using NewLife.Log;
using XCode;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Plugins.AgentHub.Data;

/// <summary>
/// AgentHub 插件的建表入口（单一真源）。
///
/// 显式 SetTables 建全表（建表铁律 12 的正确姿势）：**不依赖 XCode 自动建表**——
/// XCode 12 的 Migration 自动建表只在「实体 Meta 首次初始化」时按需触发；
/// 若某实体只被裸 SQL 查询（如 DelegationTask ← DelegationRuntime.List），
/// 自动建表永不触发 → 全新环境 no such table（2026-09-23 e2e 隔离宿主实证）。
/// 宿主 <c>XCodeConfig.InitializeXCodeDatabase</c> 对每个连接名做的是同一件事（InitConnection 全量建表）。
///
/// 插件为何要自己做这一步：宿主建库发生在 <c>app.Build()</c> 之后，
/// 而插件 <c>Apply</c> 在 <c>builder.Build()</c> 阶段执行 —— 插件跑在宿主建表之前。
/// 若此时不主动建表，插件内任何实体查询都会命中"库文件存在但无表"的空库，报 no such table。
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
    /// 用 <see cref="EntityFactory.InitConnection"/> 全量建表：不依赖「实体 Meta 首次初始化」的
    /// 按需自动建表——DelegationTask 只被裸 SQL 查询（DelegationRuntime.List），从未经实体 Meta 初始化，
    /// 自动建表永不触发 → 全新环境 no such table（2026-09-23 e2e 隔离宿主实证）。
    /// InitConnection 在 Migration 开启时对连接下所有实体全量建表（测试夹具
    /// <c>XCodeTestFixture.EnsureTablesCreated</c> 同款路径，已实证可建 DelegationTask）。
    /// </summary>
    /// <returns>连接是否成功打开（含建表）</returns>
    public static Boolean EnsureCreated()
    {
        try
        {
            // 全量建表：EntityTypes 对应实体的 BindTable ConnName 均为 AgentHub，InitConnection 一并建齐
            EntityFactory.InitConnection(ConnName);

            // 探活确认库已开（先开库，否则只落空库文件不建表）
            var dal = DAL.Create(ConnName);
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
