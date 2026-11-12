using System;
using XCode;

namespace ForgeSelf.Api.Plugins.SmartTerminal.Entities;

/// <summary>命令片段</summary>
public partial class StSnippet : Entity<StSnippet>
{
    static StSnippet()
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
