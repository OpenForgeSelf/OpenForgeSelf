using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Backend.Models.UsageStats;
using OpenForgeSelf.Backend.Services.UsageStats;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Controllers;

/// <summary>
/// 使用统计控制器
/// </summary>
[ApiController]
[Route("api/usagestats")]
public class UsageStatsController : ControllerBase
{
    private readonly IUsageStatsService _usageStatsService;
    private readonly IWorkflowUsageService _workflowUsageService;
    private readonly IWorkflowRecommendationService _workflowRecommendationService;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="usageStatsService">使用统计服务</param>
    /// <param name="workflowUsageService">工作流使用统计服务</param>
    /// <param name="workflowRecommendationService">工作流推荐服务</param>
    public UsageStatsController(
        IUsageStatsService usageStatsService,
        IWorkflowUsageService workflowUsageService,
        IWorkflowRecommendationService workflowRecommendationService)
    {
        _usageStatsService = usageStatsService;
        _workflowUsageService = workflowUsageService;
        _workflowRecommendationService = workflowRecommendationService;
    }

    /// <summary>
    /// 获取使用记录
    /// </summary>
    /// <param name="pluginId">插件ID（可选）</param>
    /// <param name="toolId">工具ID（可选）</param>
    /// <param name="startDate">开始日期（可选）</param>
    /// <param name="endDate">结束日期（可选）</param>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页大小</param>
    /// <returns>分页使用记录</returns>
    [HttpGet("records")]
    public async Task<ActionResult<ApiResponse<PagedResult<UsageRecord>>>> GetUsageRecords(
        [FromQuery] string? pluginId = null,
        [FromQuery] string? toolId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            XTrace.Log.Info("获取使用记录: pluginId={0}, toolId={1}, page={2}", pluginId, toolId, page);

            var result = await _usageStatsService.GetUsageRecordsAsync(
                pluginId, toolId, startDate, endDate, page, pageSize);

            return Ok(ApiResponse<PagedResult<UsageRecord>>.Ok(result, "获取使用记录成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取使用记录失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<PagedResult<UsageRecord>>.Error("获取使用记录失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 获取每日汇总
    /// </summary>
    /// <param name="pluginId">插件ID（可选）</param>
    /// <param name="toolId">工具ID（可选）</param>
    /// <param name="startDate">开始日期（可选）</param>
    /// <param name="endDate">结束日期（可选）</param>
    /// <returns>每日汇总列表</returns>
    [HttpGet("summary/daily")]
    public async Task<ActionResult<ApiResponse<List<UsageDailySummary>>>> GetDailySummary(
        [FromQuery] string? pluginId = null,
        [FromQuery] string? toolId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            XTrace.Log.Info("获取每日汇总: pluginId={0}, toolId={1}", pluginId, toolId);

            var result = await _usageStatsService.GetDailySummaryAsync(
                pluginId, toolId, startDate, endDate);

            return Ok(ApiResponse<List<UsageDailySummary>>.Ok(result, "获取每日汇总成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取每日汇总失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<UsageDailySummary>>.Error("获取每日汇总失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 获取最常用工具
    /// </summary>
    /// <param name="pluginId">插件ID（可选）</param>
    /// <param name="limit">返回数量限制</param>
    /// <param name="period">时间周期（7d/30d/90d）</param>
    /// <returns>常用工具列表</returns>
    [HttpGet("tools/top")]
    public async Task<ActionResult<ApiResponse<List<TopToolItem>>>> GetTopTools(
        [FromQuery] string? pluginId = null,
        [FromQuery] int limit = 10,
        [FromQuery] string period = "30d")
    {
        try
        {
            XTrace.Log.Info("获取最常用工具: pluginId={0}, limit={1}, period={2}", pluginId, limit, period);

            var result = await _usageStatsService.GetTopToolsAsync(pluginId, limit, period);

            return Ok(ApiResponse<List<TopToolItem>>.Ok(result, "获取最常用工具成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取最常用工具失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<TopToolItem>>.Error("获取最常用工具失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 获取使用趋势
    /// </summary>
    /// <param name="pluginId">插件ID（可选）</param>
    /// <param name="period">时间周期（7d/30d/90d）</param>
    /// <returns>使用趋势数据</returns>
    [HttpGet("trend")]
    public async Task<ActionResult<ApiResponse<List<UsageTrendPoint>>>> GetUsageTrend(
        [FromQuery] string? pluginId = null,
        [FromQuery] string period = "7d")
    {
        try
        {
            XTrace.Log.Info("获取使用趋势: pluginId={0}, period={1}", pluginId, period);

            var result = await _usageStatsService.GetUsageTrendAsync(pluginId, period);

            return Ok(ApiResponse<List<UsageTrendPoint>>.Ok(result, "获取使用趋势成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取使用趋势失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<UsageTrendPoint>>.Error("获取使用趋势失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 获取工具排名
    /// </summary>
    /// <param name="pluginId">插件ID（可选）</param>
    /// <param name="startDate">开始日期（可选）</param>
    /// <param name="endDate">结束日期（可选）</param>
    /// <returns>工具排名列表</returns>
    [HttpGet("tools/ranking")]
    public async Task<ActionResult<ApiResponse<List<ToolRankingItem>>>> GetToolRanking(
        [FromQuery] string? pluginId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            XTrace.Log.Info("获取工具排名: pluginId={0}", pluginId);

            var result = await _usageStatsService.GetToolRankingAsync(pluginId, startDate, endDate);

            return Ok(ApiResponse<List<ToolRankingItem>>.Ok(result, "获取工具排名成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取工具排名失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<ToolRankingItem>>.Error("获取工具排名失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 手动记录使用（供插件调用）
    /// </summary>
    /// <param name="request">记录使用请求</param>
    /// <returns>记录ID</returns>
    [HttpPost("record")]
    public async Task<ActionResult<ApiResponse<long>>> RecordUsage([FromBody] RecordUsageRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.PluginId) ||
                string.IsNullOrWhiteSpace(request.ToolId) ||
                string.IsNullOrWhiteSpace(request.ActionType))
            {
                return BadRequest(ApiResponse<long>.Error("插件ID、工具ID和操作类型不能为空", 400));
            }

            XTrace.Log.Info("手动记录使用: pluginId={0}, toolId={1}, actionType={2}",
                request.PluginId, request.ToolId, request.ActionType);

            var userAgent = Request.Headers["User-Agent"].ToString();
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            var recordId = await _usageStatsService.RecordUsageAsync(
                request.PluginId,
                request.ToolId,
                request.ActionType,
                request.DurationMs,
                request.Metadata,
                userAgent,
                ipAddress);

            return Ok(ApiResponse<long>.Ok(recordId, "记录使用成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("记录使用失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<long>.Error("记录使用失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 获取工作流总体统计
    /// </summary>
    /// <param name="startDate">开始日期（可选）</param>
    /// <param name="endDate">结束日期（可选）</param>
    /// <returns>工作流总体统计</returns>
    [HttpGet("workflows/stats")]
    public async Task<ActionResult<ApiResponse<WorkflowTotalStatsDto>>> GetWorkflowTotalStats(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            XTrace.Log.Info("获取工作流总体统计");

            var result = await _workflowUsageService.GetTotalStatsAsync(startDate, endDate);

            return Ok(ApiResponse<WorkflowTotalStatsDto>.Ok(result, "获取工作流总体统计成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取工作流总体统计失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<WorkflowTotalStatsDto>.Error("获取工作流总体统计失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 获取单个工作流统计
    /// </summary>
    /// <param name="workflowId">工作流ID</param>
    /// <param name="startDate">开始日期（可选）</param>
    /// <param name="endDate">结束日期（可选）</param>
    /// <returns>单个工作流统计</returns>
    [HttpGet("workflows/{workflowId}/stats")]
    public async Task<ActionResult<ApiResponse<WorkflowStatsDto>>> GetWorkflowStats(
        long workflowId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            XTrace.Log.Info("获取工作流统计: workflowId={0}", workflowId);

            var result = await _workflowUsageService.GetWorkflowStatsAsync(workflowId, startDate, endDate);

            if (result == null)
            {
                return NotFound(ApiResponse<WorkflowStatsDto>.Error("工作流统计不存在", 404));
            }

            return Ok(ApiResponse<WorkflowStatsDto>.Ok(result, "获取工作流统计成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取工作流统计失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<WorkflowStatsDto>.Error("获取工作流统计失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 获取常用工作流排行
    /// </summary>
    /// <param name="limit">返回数量限制</param>
    /// <param name="period">时间周期（7d/30d/90d）</param>
    /// <returns>常用工作流排行</returns>
    [HttpGet("workflows/popular")]
    public async Task<ActionResult<ApiResponse<List<PopularWorkflowDto>>>> GetPopularWorkflows(
        [FromQuery] int limit = 10,
        [FromQuery] string period = "30d")
    {
        try
        {
            XTrace.Log.Info("获取常用工作流排行: limit={0}, period={1}", limit, period);

            var result = await _workflowUsageService.GetPopularWorkflowsAsync(limit, period);

            return Ok(ApiResponse<List<PopularWorkflowDto>>.Ok(result, "获取常用工作流排行成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取常用工作流排行失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<PopularWorkflowDto>>.Error("获取常用工作流排行失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 获取推荐工作流
    /// </summary>
    /// <param name="userId">用户ID（可选）</param>
    /// <param name="context">上下文关键词（可选）</param>
    /// <param name="limit">返回数量限制</param>
    /// <returns>推荐工作流列表</returns>
    [HttpGet("workflows/recommendations")]
    public async Task<ActionResult<ApiResponse<List<WorkflowRecommendationDto>>>> GetRecommendedWorkflows(
        [FromQuery] string? userId = null,
        [FromQuery] string? context = null,
        [FromQuery] int limit = 5)
    {
        try
        {
            XTrace.Log.Info("获取推荐工作流: userId={0}, context={1}, limit={2}", userId, context, limit);

            var result = await _workflowRecommendationService.GetRecommendedWorkflowsAsync(userId, context, limit);

            return Ok(ApiResponse<List<WorkflowRecommendationDto>>.Ok(result, "获取推荐工作流成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取推荐工作流失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<WorkflowRecommendationDto>>.Error("获取推荐工作流失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 获取工作流使用趋势
    /// </summary>
    /// <param name="workflowId">工作流ID</param>
    /// <param name="days">天数</param>
    /// <returns>使用趋势数据</returns>
    [HttpGet("workflows/trend")]
    public async Task<ActionResult<ApiResponse<List<WorkflowUsageTrendDto>>>> GetWorkflowUsageTrend(
        [FromQuery] long workflowId,
        [FromQuery] int days = 7)
    {
        try
        {
            XTrace.Log.Info("获取工作流使用趋势: workflowId={0}, days={1}", workflowId, days);

            var result = await _workflowUsageService.GetWorkflowUsageTrendAsync(workflowId, days);

            return Ok(ApiResponse<List<WorkflowUsageTrendDto>>.Ok(result, "获取工作流使用趋势成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取工作流使用趋势失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<WorkflowUsageTrendDto>>.Error("获取工作流使用趋势失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 获取工作流内工具使用排行
    /// </summary>
    /// <param name="startDate">开始日期（可选）</param>
    /// <param name="endDate">结束日期（可选）</param>
    /// <param name="limit">返回数量限制</param>
    /// <returns>工具使用排行</returns>
    [HttpGet("workflows/tools-ranking")]
    public async Task<ActionResult<ApiResponse<List<WorkflowToolRankingDto>>>> GetWorkflowToolsRanking(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int limit = 10)
    {
        try
        {
            XTrace.Log.Info("获取工作流内工具使用排行");

            var result = await _workflowUsageService.GetToolUsageInWorkflowsAsync(startDate, endDate, limit);

            return Ok(ApiResponse<List<WorkflowToolRankingDto>>.Ok(result, "获取工作流内工具使用排行成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取工作流内工具使用排行失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<WorkflowToolRankingDto>>.Error("获取工作流内工具使用排行失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 获取个人工具库总览统计
    /// </summary>
    /// <param name="timeRange">时间范围（7d/30d/90d/all）</param>
    /// <returns>个人工具库统计数据</returns>
    [HttpGet("personal-library")]
    public async Task<ActionResult<ApiResponse<PersonalLibraryStatsDto>>> GetPersonalLibraryStats(
        [FromQuery] string timeRange = "30d")
    {
        try
        {
            XTrace.Log.Info("获取个人工具库统计: timeRange={0}", timeRange);

            var result = await _usageStatsService.GetPersonalLibraryStatsAsync(timeRange);

            return Ok(ApiResponse<PersonalLibraryStatsDto>.Ok(result, "获取个人工具库统计成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取个人工具库统计失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<PersonalLibraryStatsDto>.Error("获取个人工具库统计失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 获取能力成长曲线
    /// </summary>
    /// <param name="days">天数</param>
    /// <returns>成长曲线数据</returns>
    [HttpGet("growth-curve")]
    public async Task<ActionResult<ApiResponse<List<GrowthCurvePoint>>>> GetGrowthCurve(
        [FromQuery] int days = 30)
    {
        try
        {
            XTrace.Log.Info("获取成长曲线: days={0}", days);

            var result = await _usageStatsService.GetGrowthCurveAsync(days);

            return Ok(ApiResponse<List<GrowthCurvePoint>>.Ok(result, "获取成长曲线成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取成长曲线失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<GrowthCurvePoint>>.Error("获取成长曲线失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 获取节省时间估算
    /// </summary>
    /// <returns>节省时间估算数据</returns>
    [HttpGet("time-saved")]
    public async Task<ActionResult<ApiResponse<TimeSavedEstimateDto>>> GetTimeSavedEstimate()
    {
        try
        {
            XTrace.Log.Info("获取节省时间估算");

            var result = await _usageStatsService.GetTimeSavedEstimateAsync();

            return Ok(ApiResponse<TimeSavedEstimateDto>.Ok(result, "获取节省时间估算成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取节省时间估算失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<TimeSavedEstimateDto>.Error("获取节省时间估算失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 获取上下文推荐
    /// </summary>
    /// <param name="request">推荐请求</param>
    /// <returns>上下文推荐结果</returns>
    [HttpGet("recommendations/contextual")]
    public async Task<ActionResult<ApiResponse<ContextualRecommendationDto>>> GetContextualRecommendations(
        [FromQuery] string? currentPage = null,
        [FromQuery] string? currentAction = null,
        [FromQuery] int limit = 5)
    {
        try
        {
            XTrace.Log.Info("获取上下文推荐: currentPage={0}, currentAction={1}", currentPage, currentAction);

            var request = new ContextualRecommendationRequest
            {
                CurrentPage = currentPage,
                CurrentAction = currentAction,
                Limit = limit
            };

            var result = await _workflowRecommendationService.GetContextualRecommendationsAsync(request);

            return Ok(ApiResponse<ContextualRecommendationDto>.Ok(result, "获取上下文推荐成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取上下文推荐失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<ContextualRecommendationDto>.Error("获取上下文推荐失败: " + ex.Message));
        }
    }

    /// <summary>
    /// 获取保存建议
    /// </summary>
    /// <param name="request">保存建议请求</param>
    /// <returns>保存建议结果</returns>
    [HttpPost("recommendations/save-suggestion")]
    public async Task<ActionResult<ApiResponse<SaveAsSuggestionDto>>> GetSaveAsSuggestion(
        [FromBody] SaveAsSuggestionRequest request)
    {
        try
        {
            XTrace.Log.Info("获取保存建议: actionType={0}", request.ActionType);

            var result = await _workflowRecommendationService.GetSaveAsSuggestionAsync(request);

            return Ok(ApiResponse<SaveAsSuggestionDto>.Ok(result, "获取保存建议成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取保存建议失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<SaveAsSuggestionDto>.Error("获取保存建议失败: " + ex.Message));
        }
    }
}
