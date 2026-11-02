using System;
using System.Collections.Generic;
using System.Linq;
using NewLife;
using NewLife.Log;
using XCode;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Entities;

/// <summary>权限策略 profile 业务面（B4）：建表 + JSON 规则序列化 + 冲突检测。</summary>
public partial class PermissionProfile : Entity<PermissionProfile>
{
    /// <summary>连接名（与 BindTable 一致）。</summary>
    public const String ConnName = "ForgeSelf";

    /// <summary>确保表就绪（幂等）。</summary>
    public static Boolean EnsureCreated(String connName = ConnName)
    {
        try
        {
            EntityFactory.InitConnection(connName);
            var dal = DAL.Create(connName);
            var version = dal.Db.ServerVersion;
            XTrace.Log.Debug("[PermissionProfile] 数据库初始化完成 (ServerVersion={0})", version);
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[PermissionProfile] 数据库初始化失败: {0}", ex.Message);
            return false;
        }
    }
}
