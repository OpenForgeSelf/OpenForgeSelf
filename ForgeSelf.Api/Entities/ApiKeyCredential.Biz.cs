using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.Reflection;
using NewLife.Threading;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;
using XCode.Membership;
using XCode.Shards;

namespace ForgeSelf.Api.Entities;

public partial class ApiKeyCredential : Entity<ApiKeyCredential>
{
    #region 对象操作
    // 控制最大缓存数量，Find/FindAll 查询方法在表行数小于该值时走实体缓存
    private static Int32 MaxCacheCount = 1000;

    /// <summary>验证并修补数据，返回验证结果，或者通过抛出异常的方式提示验证失败。</summary>
    /// <param name="method">添删改方法</param>
    public override Boolean Valid(DataMethod method)
    {
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return true;

        // 建议先调用基类方法，基类方法会做一些统一处理
        if (!base.Valid(method)) return false;

        if (Name.IsNullOrEmpty()) Name = "密钥";

        // 字段命名非 TimeInterceptor 默认的 CreateTime/UpdateTime，
        // 故在 Valid 内手动维护 CreatedAt/UpdatedAt（插入或字段未显式赋值时）。
        if (method == DataMethod.Insert && !Dirtys[nameof(CreatedAt)]) CreatedAt = DateTime.Now;
        if (!Dirtys[nameof(UpdatedAt)]) UpdatedAt = DateTime.Now;

        return true;
    }
    #endregion

    #region 扩展属性
    /// <summary>是否已过期（ExpiresAt 为 MinValue 表示永不过期）</summary>
    public Boolean IsExpired => ExpiresAt > DateTime.MinValue && ExpiresAt <= DateTime.Now;
    #endregion

    #region 高级查询
    #endregion

    #region 业务操作
    /// <summary>转为模型对象（IApiKeyCredentialModel）。</summary>
    public IApiKeyCredentialModel ToModel()
    {
        var model = new ApiKeyCredential();
        model.Copy(this);
        return model;
    }
    #endregion
}
