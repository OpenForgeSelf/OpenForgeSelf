using ForgeSelf.Api.Services;

namespace ForgeSelf.Api.Plugins;

/// <summary>
/// 插件发行串解析。比较委托宿主 <see cref="UpdateChecker.CompareSemVer"/>，
/// 这样第 4 段 10 位时间码不会被 <see cref="Version"/> 的 16 位上限丢掉。
/// </summary>
internal static class PluginReleaseVersion
{
    /// <summary>
    /// 只接受纯数字段（1–4 段）。<c>v</c> 前缀、<c>-</c> 预发布、<c>+</c> 构建元数据返回 null。
    /// </summary>
    public static UpdateChecker.SemVer? TryParseStrict(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var s = raw.Trim();
        if (s.Contains('-') || s.Contains('+') || s.StartsWith('v') || s.StartsWith('V'))
            return null;

        return UpdateChecker.ParseSemVer(s);
    }
}
