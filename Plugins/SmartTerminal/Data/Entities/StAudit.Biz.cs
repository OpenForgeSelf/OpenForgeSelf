using System;
using XCode;

namespace ForgeSelf.Api.Plugins.SmartTerminal.Entities;

/// <summary>只追加审计</summary>
public partial class StAudit : Entity<StAudit>
{
    static StAudit()
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
