using System;
using XCode;

namespace ForgeSelf.Api.Plugins.SmartTerminal.Entities;

/// <summary>凭据句柄（材料由宿主加密保管）</summary>
public partial class StSecretRef : Entity<StSecretRef>
{
    static StSecretRef()
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
