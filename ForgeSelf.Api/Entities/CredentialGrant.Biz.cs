using System;
using System.ComponentModel;
using NewLife;
using XCode;

namespace ForgeSelf.Api.Entities;

/// <summary>凭据授权业务逻辑半边（基类声明 + 时间戳/校验 + 扩展属性）。</summary>
public partial class CredentialGrant : Entity<CredentialGrant>
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

        if (method == DataMethod.Insert && !Dirtys[nameof(CreatedAt)]) CreatedAt = DateTime.Now;

        return true;
    }
    #endregion

    #region 扩展属性
    /// <summary>是否已撤销（RevokedAt 非 MinValue）</summary>
    public Boolean IsRevoked => RevokedAt > DateTime.MinValue;

    /// <summary>是否已过期</summary>
    public Boolean IsExpired => ExpiresAt > DateTime.MinValue && ExpiresAt <= DateTime.Now;

    /// <summary>是否未生效</summary>
    public Boolean IsNotYetValid => ValidFrom > DateTime.MinValue && ValidFrom > DateTime.Now;

    /// <summary>当前是否有效（未撤销/已生效/未过期）</summary>
    public Boolean IsActive => !IsRevoked && !IsExpired && !IsNotYetValid;
    #endregion

    #region 业务操作
    /// <summary>转为模型对象（ICredentialGrantModel）。</summary>
    public ICredentialGrantModel ToModel()
    {
        var model = new CredentialGrant();
        model.Copy(this);
        return model;
    }
    #endregion
}
