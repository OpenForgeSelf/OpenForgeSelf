using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.Reflection;
using NewLife.Threading;
using NewLife.Web;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;
using XCode.Membership;
using XCode.Shards;

namespace ForgeSelf.Api.Entities;

public partial class SessionEventEntity : Entity<SessionEventEntity>
{
    #region 对象操作
    // 控制最大缓存数量，Find/FindAll查询方法在表行数小于该值时走实体缓存。
    // 会话日志为纯追加型：写入后需立即可回放（Request → DeriveMessages），
    // 实体缓存会返回陈旧数据，故禁用（同 ChatMessage 的处理）。
    private static Int32 MaxCacheCount = 0;

    /// <summary>连接名（与 BindTable 的 ConnName 一致，供存储实现与测试共用）。</summary>
    public const String ConnName = "ForgeSelf";

    /// <summary>
    /// 确保本实体所在库与表就绪（幂等）。
    /// 走 <see cref="EntityFactory.InitConnection"/> 全量建表：XCode 12 的按需自动建表只在实体 Meta
    /// 首次初始化时触发，反射取 Meta → CreateTable 的路径已实证静默失效（见 Plugins/AgentHub/Data/AgentHubTables.cs）。
    /// 宿主 <c>XCodeConfig.InitializeXCodeDatabase</c> 在 Testing 环境下直接返回，测试侧不会建表，
    /// 故持久化存储必须自持这一步，否则首条 Append 即 "no such table"。
    /// </summary>
    /// <param name="connName">连接名；默认即本实体的 <see cref="ConnName"/>。</param>
    /// <returns>连接是否成功打开（含建表）。</returns>
    public static Boolean EnsureCreated(String connName = ConnName)
    {
        try
        {
            EntityFactory.InitConnection(connName);

            // 探活确认库已开（先开库，否则只落空库文件不建表）
            var dal = DAL.Create(connName);
            var version = dal.Db.ServerVersion;
            XTrace.Log.Debug("[SessionEvent] 数据库初始化完成 (ServerVersion={0})", version);
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[SessionEvent] 数据库初始化失败: {0}", ex.Message);
            return false;
        }
    }

    /// <summary>验证并修补数据，返回验证结果，或者通过抛出异常的方式提示验证失败。</summary>
    /// <param name="method">添删改方法</param>
    public override Boolean Valid(DataMethod method)
    {
        if (!HasDirty) return true;

        if (SessionId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(SessionId), "会话ID不能为空！");
        if (Type.IsNullOrEmpty()) throw new ArgumentNullException(nameof(Type), "事件类型名不能为空！");
        if (PayloadJson == null) throw new ArgumentNullException(nameof(PayloadJson), "事件载荷不能为空！");

        if (!base.Valid(method)) return false;

        return true;
    }
    #endregion

    #region 扩展属性
    #endregion

    #region 业务操作
    /// <summary>按会话 ID 取全部事件行（按主键升序；append-only 表无需排序参数）。</summary>
    /// <param name="sessionId">会话 ID。</param>
    /// <returns>事件行列表（按 Id 升序）。</returns>
    public static IList<SessionEventEntity> FindAllBySessionIdOrdered(String sessionId)
        => FindAllBySessionId(sessionId).OrderBy(e => e.Id).ToList();
    #endregion
}
