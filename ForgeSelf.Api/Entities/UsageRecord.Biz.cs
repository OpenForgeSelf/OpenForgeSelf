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

public partial class UsageRecord : Entity<UsageRecord>
{
    #region 对象操作
    // 控制最大缓存数量，Find/FindAll查询方法在表行数小于该值时走实体缓存
    // 统计/追加型表：高写入、需即时可见且分页查询依赖 pageParam.TotalCount；
    // 实体缓存路径不回填 TotalCount、且会返回全量行忽略分页，故禁用全表缓存（设为 0）
    private static Int32 MaxCacheCount = 0;

    static UsageRecord()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(DurationMs));

        // 拦截器 UserInterceptor、TimeInterceptor、IPInterceptor
    }

    /// <summary>验证并修补数据，返回验证结果，或者通过抛出异常的方式提示验证失败。</summary>
    /// <param name="method">添删改方法</param>
    public override Boolean Valid(DataMethod method)
    {
        //if (method == DataMethod.Delete) return true;
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return true;

        // 这里验证参数范围，建议抛出参数异常，指定参数名，前端用户界面可以捕获参数异常并聚焦到对应的参数输入框
        if (PluginId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(PluginId), "插件ID不能为空！");
        if (ToolId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(ToolId), "工具ID不能为空！");
        if (ActionType.IsNullOrEmpty()) throw new ArgumentNullException(nameof(ActionType), "操作类型不能为空！");

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

    //    if (XTrace.Debug) XTrace.WriteLine("开始初始化UsageRecord[使用记录]数据……");

    //    var entity = new UsageRecord();
    //    entity.PluginId = "abc";
    //    entity.ToolId = "abc";
    //    entity.ActionType = "abc";
    //    entity.UserAgent = "abc";
    //    entity.IpAddress = "abc";
    //    entity.DurationMs = 0;
    //    entity.Timestamp = DateTime.Now;
    //    entity.MetadataJson = "abc";
    //    entity.WorkflowExecutionId = 0;
    //    entity.StepId = "abc";
    //    entity.Insert();

    //    if (XTrace.Debug) XTrace.WriteLine("完成初始化UsageRecord[使用记录]数据！");
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

    #region 高级查询

    // Select Count(Id) as Id,PluginId From UsageRecord Where CreateTime>'2020-01-24 00:00:00' Group By PluginId Order By Id Desc limit 20
    static readonly FieldCache<UsageRecord> _PluginIdCache = new(nameof(PluginId))
    {
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    };

    /// <summary>获取插件ID列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    /// <returns></returns>
    public static IDictionary<String, String> GetPluginIdList() => _PluginIdCache.FindAllName();

    // Select Count(Id) as Id,ToolId From UsageRecord Where CreateTime>'2020-01-24 00:00:00' Group By ToolId Order By Id Desc limit 20
    static readonly FieldCache<UsageRecord> _ToolIdCache = new(nameof(ToolId))
    {
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    };

    /// <summary>获取工具ID列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    /// <returns></returns>
    public static IDictionary<String, String> GetToolIdList() => _ToolIdCache.FindAllName();

    // Select Count(Id) as Id,ActionType From UsageRecord Where CreateTime>'2020-01-24 00:00:00' Group By ActionType Order By Id Desc limit 20
    static readonly FieldCache<UsageRecord> _ActionTypeCache = new(nameof(ActionType))
    {
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    };

    /// <summary>获取操作类型列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    /// <returns></returns>
    public static IDictionary<String, String> GetActionTypeList() => _ActionTypeCache.FindAllName();
    #endregion

    #region 业务操作
    public IUsageRecordModel ToModel()
    {
        var model = new UsageRecord();
        model.Copy(this);

        return model;
    }

    #endregion
}
