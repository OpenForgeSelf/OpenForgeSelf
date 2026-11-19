namespace ForgeSelf.Api.Tests;

/// <summary>
/// 用例依赖 Windows 命令或盘符。Linux 上跳过，不记成通过。
/// </summary>
public sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "依赖 Windows，留到 Windows 环境再跑";
    }
}

/// <summary>同 <see cref="WindowsOnlyFactAttribute"/>，用于 Theory。</summary>
public sealed class WindowsOnlyTheoryAttribute : TheoryAttribute
{
    public WindowsOnlyTheoryAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "依赖 Windows，留到 Windows 环境再跑";
    }
}
