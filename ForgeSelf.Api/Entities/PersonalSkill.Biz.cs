using System;
using NewLife;
using NewLife.Log;
using XCode;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Entities;

public partial class PersonalSkill : Entity<PersonalSkill>
{
    /// <summary>0 = 查询走数据库，避免实体缓存挡住刚写入的行。</summary>
    private static Int32 MaxCacheCount = 0;

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
            XTrace.Log.Debug("[PersonalSkill] 数据库初始化完成 (ServerVersion={0})", version);
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[PersonalSkill] 数据库初始化失败: {0}", ex.Message);
            return false;
        }
    }

    /// <inheritdoc />
    public override Boolean Valid(DataMethod method)
    {
        if (!HasDirty) return true;
        if (SkillId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(SkillId), "技能Id不能为空！");
        if (method == DataMethod.Insert && Name.IsNullOrEmpty())
            throw new ArgumentNullException(nameof(Name), "技能名称不能为空！");
        if (method == DataMethod.Insert && !Dirtys[nameof(CreatedAt)]) CreatedAt = DateTime.UtcNow;
        if (!Dirtys[nameof(UpdatedAt)]) UpdatedAt = DateTime.UtcNow;
        return base.Valid(method);
    }
}
