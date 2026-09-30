using System;
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

public partial class RunCommand : Entity<RunCommand>
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

        // 字段命名非 TimeInterceptor 默认的 CreateTime/UpdateTime，
        // 故在 Valid 内手动维护 CreatedAt/UpdatedAt（插入或字段未显式赋值时）。
        if (method == DataMethod.Insert && !Dirtys[nameof(CreatedAt)]) CreatedAt = DateTime.Now;
        if (!Dirtys[nameof(UpdatedAt)]) UpdatedAt = DateTime.Now;

        return true;
    }
    #endregion

    #region 扩展属性
    #endregion

    #region 高级查询
    #endregion

    #region 业务操作
    /// <summary>转为模型对象（IRunCommandModel）。</summary>
    public IRunCommandModel ToModel()
    {
        var model = new RunCommand();
        model.Copy(this);
        return model;
    }

    /// <summary>级联删除某项目的全部运行命令（直查库，不走实体缓存），返回删除条数。</summary>
    public static Int32 DeleteByProjectId(Int32 projectId)
    {
        var count = 0;
        foreach (var command in FindAllByProjectId(projectId))
        {
            command.Delete();
            count++;
        }
        return count;
    }
    #endregion
}
