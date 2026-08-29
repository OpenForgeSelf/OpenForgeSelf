namespace ForgeSelf.Abstractions;

/// <summary>脚本库统计快照（宿主 UsageStats 服务跨程序集消费）。</summary>
public class ScriptLibraryStats
{
    public int ScriptCount { get; set; }
    public int CodeSnippetCount { get; set; }
    public int FavoriteCount { get; set; }
    public List<TopScriptItem> TopScripts { get; set; } = new();
}

/// <summary>脚本库新增趋势（按天，键为本地日期）。</summary>
public class ScriptCreationStats
{
    public Dictionary<DateTime, int> NewScriptsByDate { get; set; } = new();
    public Dictionary<DateTime, int> NewCodeSnippetsByDate { get; set; } = new();
}

/// <summary>脚本库使用总量（实体 UsageCount 为 Int32，与拆分前 EF Sum 语义一致）。</summary>
public class ScriptUsageTotals
{
    public int ScriptUsageCount { get; set; }
    public int CodeSnippetUsageCount { get; set; }
}

/// <summary>推荐用脚本摘要。</summary>
public class ScriptRecommendationItem
{
    public long ScriptId { get; set; }
    public string ScriptName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Language { get; set; } = string.Empty;
    public int UsageCount { get; set; }
}

/// <summary>推荐用代码片段摘要。</summary>
public class CodeSnippetRecommendationItem
{
    public long SnippetId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Language { get; set; } = string.Empty;
    public int UsageCount { get; set; }
}

/// <summary>
/// 脚本库统计读取契约（实现：ScriptRunner 插件 <c>ScriptLibraryStatsProvider</c>）。
/// 宿主 UsageStats / Recommendation 服务经此接口跨程序集读取脚本/片段数据，
/// 避免直接依赖 ScriptRunner 插件程序集（ADR D2）。
/// </summary>
public interface IScriptLibraryStatsProvider
{
    Task<ScriptLibraryStats> GetLibraryStatsAsync();
    Task<ScriptCreationStats> GetCreationStatsAsync(DateTime start, DateTime end);
    Task<ScriptUsageTotals> GetUsageTotalsAsync();
    Task<List<ScriptRecommendationItem>> GetTopScriptsAsync(int take);
    Task<List<CodeSnippetRecommendationItem>> GetTopCodeSnippetsAsync(int take);
}
