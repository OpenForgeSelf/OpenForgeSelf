using System;
using XCode;

namespace ForgeSelf.Api.Plugins.SmartTerminal.Entities;

/// <summary>已保存的本机/SSH 配置</summary>
public partial class StProfile : Entity<StProfile>
{
    static StProfile()
    {
    }

    public override Boolean Valid(DataMethod method)
    {
        if (!HasDirty) return true;
        if (!base.Valid(method)) return false;
        if (method == DataMethod.Insert && CreateTime == DateTime.MinValue)
            CreateTime = DateTime.Now;
        return true;
    }
}
