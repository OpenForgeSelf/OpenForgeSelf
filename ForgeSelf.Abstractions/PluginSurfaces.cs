namespace ForgeSelf.Abstractions;

/// <summary>
/// 插件前端 surface 类型（Plugin-First 架构约定，见 DESIGN.md「Plugin-First 架构约定」）。
/// 一个插件可在 manifest 的 <c>frontend.surfaces</c> 中声明它向宿主贡献哪些 UI 面；
/// 宿主据此把清单条目分发到 TabBar / 首页面板 / 快捷动作 / 状态栏徽标等不同消费位。
/// </summary>
/// <remarks>
/// 设计取舍：用 <see cref="string"/> 常量而非枚举，是为了让宿主对「未来新增的 surface」
/// 保持向后兼容——未知值在加载时被 <see cref="NormalizeAll"/> 清洗并记日志，不阻断插件加载。
/// </remarks>
public static class PluginSurfaces
{
    /// <summary>Tab 面：宿主顶栏/侧边栏的独立页面路由（带 route+views）。</summary>
    public const string Tab = "tab";

    /// <summary>Home-widget 面：首页（@forge/home）面板上的一个 slot，可被其他插件贡献/替换。</summary>
    public const string HomeWidget = "home-widget";

    /// <summary>Quick-action 面：全局快捷动作（composer / 命令面板入口）。</summary>
    public const string QuickAction = "quick-action";

    /// <summary>Status-badge 面：状态栏（StatusBar）上的状态徽标。</summary>
    public const string StatusBadge = "status-badge";

    private static readonly HashSet<string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        Tab, HomeWidget, QuickAction, StatusBadge
    };

    /// <summary>全部已知 surface（规范形式：小写连字符）。</summary>
    public static IReadOnlyList<string> All { get; } = new[] { Tab, HomeWidget, QuickAction, StatusBadge };

    /// <summary>判断原始字符串是否为已知 surface（大小写/首尾空白不敏感）。</summary>
    public static bool IsKnown(string? value)
        => !string.IsNullOrWhiteSpace(value) && Known.Contains(value.Trim());

    /// <summary>
    /// 把原始 surface 字符串归一化为规范形式；未知/空白返回 <c>null</c>。
    /// 用于宿主在加载清单时清洗 surfaces，未知项不抛错（向后兼容），由调用方决定是否记日志。
    /// </summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim().ToLowerInvariant();
        return Known.Contains(v) ? v : null;
    }

    /// <summary>
    /// 批量归一化：保留已知项（去重、保序），未知项经 <paramref name="onUnknown"/> 回调上报。
    /// 宿主在 <c>LoadPluginManifest</c> 中调用，把插件自报的 surfaces 清洗成规范集合。
    /// </summary>
    public static List<string> NormalizeAll(IEnumerable<string>? raw, Action<string>? onUnknown = null)
    {
        var result = new List<string>();
        if (raw == null) return result;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in raw)
        {
            var norm = Normalize(item);
            if (norm == null)
            {
                onUnknown?.Invoke(item ?? string.Empty);
                continue;
            }
            if (seen.Add(norm)) result.Add(norm);
        }
        return result;
    }
}
