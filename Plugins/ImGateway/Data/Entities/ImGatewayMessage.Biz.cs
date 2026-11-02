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

namespace ForgeSelf.Api.Plugins.ImGateway.Entities;

public partial class ImGatewayMessage : Entity<ImGatewayMessage>
{
    #region 对象操作
    // 控制最大缓存数量，Find/FindAll查询方法在表行数小于该值时走实体缓存
    private static Int32 MaxCacheCount = 1000;

    static ImGatewayMessage()
    {

        // 拦截器 UserInterceptor、TimeInterceptor、IPInterceptor

        // 实体缓存
        // var ec = Meta.Cache;
        // ec.Expire = 60;
    }

    /// <summary>验证并修补数据，返回验证结果，或者通过抛出异常的方式提示验证失败。</summary>
    /// <param name="method">添删改方法</param>
    public override Boolean Valid(DataMethod method)
    {
        //if (method == DataMethod.Delete) return true;
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return true;

        // 这里验证参数范围，建议抛出参数异常，指定参数名，前端用户界面可以捕获参数异常并聚焦到对应的参数输入框
        if (ChannelKey.IsNullOrEmpty()) throw new ArgumentNullException(nameof(ChannelKey), "ChannelKey cannot be empty!");
        if (ChannelType == null) throw new ArgumentNullException(nameof(ChannelType), "ChannelType cannot be empty!");
        if (Direction == null) throw new ArgumentNullException(nameof(Direction), "Direction cannot be empty!");
        if (Content == null) throw new ArgumentNullException(nameof(Content), "Content cannot be empty!");

        // 建议先调用基类方法，基类方法会做一些统一处理
        if (!base.Valid(method)) return false;

        // 在新插入数据或者修改了指定字段时进行修正

        return true;
    }

    ///// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    //[EditorBrowsable(EditorBrowsableState.Never)]
    //protected override void InitData()
    //{
    //    // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
    //    if (Meta.Session.Count > 0) return;

    //    if (XTrace.Debug) XTrace.WriteLine("开始初始化ImGatewayMessage[IM网关消息记录（收_发双向]数据……");

    //    var entity = new ImGatewayMessage();
    //    entity.ChannelKey = "abc";
    //    entity.ChannelType = "abc";
    //    entity.Direction = "abc";
    //    entity.Content = "abc";
    //    entity.SessionId = "abc";
    //    entity.PlatformMsgId = "abc";
    //    entity.Status = "abc";
    //    entity.ErrorText = "abc";
    //    entity.CreatedAt = DateTime.Now;
    //    entity.Insert();

    //    if (XTrace.Debug) XTrace.WriteLine("完成初始化ImGatewayMessage[IM网关消息记录（收_发双向]数据！");
    //}

    ///// <summary>已重载。基类先调用Valid(true)验证数据，然后在事务保护内调用OnInsert</summary>
    ///// <returns></returns>
    //public override Int32 Insert()
    //{
    //    return base.Insert();
    //}

    ///// <summary>已重载。在事务保护范围内处理业务，位于Valid之后</summary>
    ///// <returns></returns>
    //protected override Int32 OnDelete()
    //{
    //    return base.OnDelete();
    //}
    #endregion

    #region 扩展属性
    #endregion

    #region 业务操作
    /// <summary>方向常量：收到。</summary>
    public const String DirectionIn = "in";

    /// <summary>方向常量：回复。</summary>
    public const String DirectionOut = "out";

    /// <summary>状态常量：正常。</summary>
    public const String StatusOk = "ok";

    /// <summary>状态常量：失败。</summary>
    public const String StatusError = "error";

    /// <summary>查询上限（单次最多返回条数，超出取边界值）。</summary>
    public const Int32 MaxPageSize = 200;

    /// <summary>
    /// 按 ChannelKey 游标分页查询消息：取 beforeId（不含）之前的最新 limit 条，升序输出。
    /// 走 ChannelKey+Id 索引；limit 钳制 1..MaxPageSize；beforeId=0 表示从最新开始。
    /// </summary>
    /// <param name="channelKey">通道会话用户复合键</param>
    /// <param name="limit">条数（钳制 1..200，缺省 50）</param>
    /// <param name="beforeId">游标：只取 Id 小于该值的消息（0=不启用）</param>
    /// <returns>升序消息列表 + 是否还有更早消息</returns>
    public static (IList<ImGatewayMessage> Rows, Boolean HasMore) FindPageByChannelKey(String channelKey, Int32 limit, Int64 beforeId)
    {
        if (channelKey.IsNullOrEmpty()) return ([], false);
        if (limit < 1) limit = 1;
        if (limit > MaxPageSize) limit = MaxPageSize;

        var where = _.ChannelKey == channelKey;
        if (beforeId > 0) where &= _.Id < beforeId;

        // 多取一条判断 hasMore，避免额外 COUNT 查询；走 ChannelKey+Id 索引
        var page = new PageParameter { PageIndex = 0, PageSize = limit + 1, Sort = _.Id, Desc = true };
        var rows = FindAll(where, page);
        var hasMore = rows.Count > limit;
        if (hasMore) rows = rows.Take(limit).ToList();

        // Id Desc 取回的窗口倒序，反转为升序输出（便于直接渲染）
        rows = rows.OrderByDescending(r => r.Id).Reverse().ToList();
        return (rows, hasMore);
    }

    /// <summary>取该联系人最新一条消息（无消息返回 null）。</summary>
    public static ImGatewayMessage FindLatestByChannelKey(String channelKey)
    {
        if (channelKey.IsNullOrEmpty()) return null;
        var page = new PageParameter { PageSize = 1, Sort = _.Id, Desc = true };
        return FindAll(_.ChannelKey == channelKey, page).FirstOrDefault();
    }
    #endregion
}
