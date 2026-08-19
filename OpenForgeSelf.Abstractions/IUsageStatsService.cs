namespace OpenForgeSelf.Abstractions;

/// <summary>
/// 使用统计服务接口
/// </summary>
public interface IUsageStatsService
{
    /// <summary>
    /// 记录使用
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="toolId">工具ID</param>
    /// <param name="actionType">操作类型</param>
    /// <param name="durationMs">持续时间（毫秒）</param>
    /// <param name="metadata">元数据</param>
    /// <param name="userAgent">用户代理</param>
    /// <param name="ipAddress">IP地址</param>
    /// <returns>记录ID</returns>
    Task<long> RecordUsageAsync(
        string pluginId,
        string toolId,
        string actionType,
        long? durationMs = null,
        Dictionary<string, object>? metadata = null,
        string? userAgent = null,
        string? ipAddress = null);

    /// <summary>
    /// 查询使用记录
    /// </summary>
    /// <param name="pluginId">插件ID（可选）</param>
    /// <param name="toolId">工具ID（可选）</param>
    /// <param name="startDate">开始日期（可选）</param>
    /// <param name="endDate">结束日期（可选）</param>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页大小</param>
    /// <returns>分页使用记录</returns>
    Task<PagedResult<UsageRecord>> GetUsageRecordsAsync(
        string? pluginId = null,
        string? toolId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 20);

    /// <summary>
    /// 获取每日汇总
    /// </summary>
    /// <param name="pluginId">插件ID（可选）</param>
    /// <param name="toolId">工具ID（可选）</param>
    /// <param name="startDate">开始日期（可选）</param>
    /// <param name="endDate">结束日期（可选）</param>
    /// <returns>每日汇总列表</returns>
    Task<List<UsageDailySummary>> GetDailySummaryAsync(
        string? pluginId = null,
        string? toolId = null,
        DateTime? startDate = null,
        DateTime? endDate = null);

    /// <summary>
    /// 获取最常用工具
    /// </summary>
    /// <param name="pluginId">插件ID（可选）</param>
    /// <param name="limit">返回数量限制</param>
    /// <param name="period">时间周期（7d/30d/90d）</param>
    /// <returns>常用工具列表</returns>
    Task<List<TopToolItem>> GetTopToolsAsync(
        string? pluginId = null,
        int limit = 10,
        string period = "30d");

    /// <summary>
    /// 获取使用趋势
    /// </summary>
    /// <param name="pluginId">插件ID（可选）</param>
    /// <param name="period">时间周期（7d/30d/90d）</param>
    /// <returns>使用趋势数据</returns>
    Task<List<UsageTrendPoint>> GetUsageTrendAsync(
        string? pluginId = null,
        string period = "7d");

    /// <summary>
    /// 获取工具排名
    /// </summary>
    /// <param name="pluginId">插件ID（可选）</param>
    /// <param name="startDate">开始日期（可选）</param>
    /// <param name="endDate">结束日期（可选）</param>
    /// <returns>工具排名列表</returns>
    Task<List<ToolRankingItem>> GetToolRankingAsync(
        string? pluginId = null,
        DateTime? startDate = null,
        DateTime? endDate = null);

    /// <summary>
    /// 获取个人工具库总览统计
    /// </summary>
    /// <param name="timeRange">时间范围（7d/30d/90d/all）</param>
    /// <returns>个人工具库统计数据</returns>
    Task<PersonalLibraryStatsDto> GetPersonalLibraryStatsAsync(string timeRange = "30d");

    /// <summary>
    /// 获取能力成长曲线
    /// </summary>
    /// <param name="days">天数</param>
    /// <returns>按日期分组的成长曲线数据</returns>
    Task<List<GrowthCurvePoint>> GetGrowthCurveAsync(int days = 30);

    /// <summary>
    /// 获取节省时间估算
    /// </summary>
    /// <returns>节省时间估算数据</returns>
    Task<TimeSavedEstimateDto> GetTimeSavedEstimateAsync();
}
