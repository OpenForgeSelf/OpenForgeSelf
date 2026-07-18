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

public partial class ChatRecord : Entity<ChatRecord>
{
    #region 对象操作
    private static Int32 MaxCacheCount = 1000;

    static ChatRecord()
    {
        Meta.Interceptors.Add<TimeInterceptor>();
    }

    /// <summary>验证并修补数据，返回验证结果，或者通过抛出异常的方式提示验证失败。</summary>
    /// <param name="method">添删改方法</param>
    public override Boolean Valid(DataMethod method)
    {
        if (!HasDirty) return true;

        if (SessionId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(SessionId), "会话ID不能为空！");

        if (!base.Valid(method)) return false;

        return true;
    }
    #endregion

    #region 扩展属性
    #endregion

    #region 高级查询
    #endregion

    #region 业务操作
    public IChatRecord ToModel()
    {
        var model = new ChatRecord();
        model.Copy(this);

        return model;
    }
    #endregion
}
