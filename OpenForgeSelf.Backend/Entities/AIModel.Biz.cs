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

namespace OpenForgeSelf.Backend.Entities;

public partial class AIModel : Entity<AIModel>
{
    #region 对象操作
    // 控制最大缓存数量，Find/FindAll查询方法在表行数小于该值时走实体缓存
    private static Int32 MaxCacheCount = 1000;

    static AIModel()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(ProviderId));

        // 拦截器 UserInterceptor、TimeInterceptor、IPInterceptor
        Meta.Interceptors.Add<TimeInterceptor>();

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
        if (ProviderId <= 0) throw new ArgumentNullException(nameof(ProviderId), "归属供应商Id必须大于0！");
        if (ProviderName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(ProviderName), "归属供应商名不能为空！");
        if (UpstreamModelId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(UpstreamModelId), "上游原始模型标识不能为空！");

        // 项目聊天模型id 由「提供商:原始模型id」派生，缺失时自动补齐（保证复制按钮与路由键稳定）
        if (ChatModelId.IsNullOrEmpty())
            ChatModelId = $"{ProviderName}:{UpstreamModelId}";

        // 建议先调用基类方法，基类方法会做一些统一处理
        if (!base.Valid(method)) return false;

        // 在新插入数据或者修改了指定字段时进行修正
        if (method == DataMethod.Insert && CreateTime.Year < 2000) CreateTime = DateTime.Now;
        if (UpdateTime.Year < 2000) UpdateTime = DateTime.Now;

        return true;
    }

    ///// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    //[EditorBrowsable(EditorBrowsableState.Never)]
    //protected override void InitData()
    //{
    //    // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
    //    if (Meta.Session.Count > 0) return;

    //    if (XTrace.Debug) XTrace.WriteLine("开始初始化AIModel[供应商模型记录]数据……");

    //    var entity = new AIModel();
    //    entity.ProviderId = 0;
    //    entity.ProviderName = "abc";
    //    entity.UpstreamModelId = "abc";
    //    entity.ChatModelId = "abc";
    //    entity.Alias = "abc";
    //    entity.Capabilities = "abc";
    //    entity.Enabled = true;
    //    entity.Owner = "abc";
    //    entity.LastSyncTime = DateTime.Now;
    //    entity.Insert();

    //    if (XTrace.Debug) XTrace.WriteLine("完成初始化AIModel[供应商模型记录]数据！");
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

    /// <summary>按供应商名与上游模型标识查询单条记录（聊天路由禁用检查用）。</summary>
    /// <param name="providerName">归属供应商名（聊天 id 前缀）</param>
    /// <param name="upstreamModelId">上游原始模型标识（聊天 id 后缀）</param>
    /// <returns>匹配记录；不存在返回 null</returns>
    public static AIModel? FindByProviderNameAndUpstreamModelId(String providerName, String upstreamModelId)
    {
        if (providerName.IsNullOrEmpty() || upstreamModelId.IsNullOrEmpty()) return null;

        // 小表走实体缓存，避免每次路由都打库
        if (Meta.Session.Count < 1000)
            return Meta.Cache.Find(e => e.ProviderName.EqualIgnoreCase(providerName) && e.UpstreamModelId.EqualIgnoreCase(upstreamModelId));

        return Find(_.ProviderName == providerName & _.UpstreamModelId == upstreamModelId);
    }

    // Select Count(Id) as Id,Category From AIModel Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
    //static readonly FieldCache<AIModel> _CategoryCache = new(nameof(Category))
    //{
    //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    //};

    ///// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    ///// <returns></returns>
    //public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
    #endregion

    #region 业务操作
    public IAIModelModel ToModel()
    {
        var model = new AIModel();
        model.Copy(this);

        return model;
    }

    #endregion
}
