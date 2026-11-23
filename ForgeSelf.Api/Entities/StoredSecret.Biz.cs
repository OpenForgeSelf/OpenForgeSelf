using System;
using System.ComponentModel;
using NewLife;
using XCode;

namespace ForgeSelf.Api.Entities;

/// <summary>存储机密业务逻辑半边（基类声明 + 时间戳/校验 + 扩展属性）。</summary>
public partial class StoredSecret : Entity<StoredSecret>
{
    #region 对象操作
    // 控制最大缓存数量，Find/FindAll 查询方法在表行数小于该值时走实体缓存
    private static Int32 MaxCacheCount = 1000;

    /// <summary>验证并修补数据，返回验证结果，或者通过抛出异常的方式提示验证失败。</summary>
    /// <param name="method">添删改方法</param>
    public override Boolean Valid(DataMethod method)
    {
        if (!HasDirty) return true;
        if (!base.Valid(method)) return false;

        if (RefId.IsNullOrEmpty()) RefId = ForgeSelf.Abstractions.SecretRef.New().Id;

        // 字段命名非 TimeInterceptor 默认的 CreateTime/UpdateTime，手动维护 CreatedAt/UpdatedAt
        if (method == DataMethod.Insert && !Dirtys[nameof(CreatedAt)]) CreatedAt = DateTime.Now;
        if (!Dirtys[nameof(UpdatedAt)]) UpdatedAt = DateTime.Now;

        return true;
    }
    #endregion

    #region 扩展属性
    /// <summary>是否已吊销</summary>
    public Boolean IsRevoked => !string.Equals(Status, "active", StringComparison.Ordinal) || RevokedAt > DateTime.MinValue;

    /// <summary>是否已过期（ExpiresAt 为 MinValue 表示永不过期）</summary>
    public Boolean IsExpired => ExpiresAt > DateTime.MinValue && ExpiresAt <= DateTime.Now;

    /// <summary>是否可用（未吊销且未过期）</summary>
    public Boolean IsUsable => !IsRevoked && !IsExpired;
    #endregion

    #region 业务操作
    /// <summary>转为模型对象（IStoredSecretModel）。</summary>
    public IStoredSecretModel ToModel()
    {
        var model = new StoredSecret();
        model.Copy(this);
        return model;
    }
    #endregion
}
