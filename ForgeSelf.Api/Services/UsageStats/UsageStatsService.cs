using System.Text.Json;
using XCodeUsageRecord = ForgeSelf.Api.Entities.UsageRecord;
using XCodeUsageDailySummary = ForgeSelf.Api.Entities.UsageDailySummary;
using XCodeWorkflowUsageRecord = ForgeSelf.Api.Entities.WorkflowUsageRecord;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Models.UsageStats;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using XCode;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Services.UsageStats;

/// <summary>
/// 使用统计服务实现
/// </summary>
public class UsageStatsService : IUsageStatsService
{
    private readonly IScriptLibraryStatsProvider? _scriptStatsProvider;

    /// <summary>
    /// 构造函数（测试/无插件场景：脚本库统计不可用，返回默认值）
    /// </summary>
    public UsageStatsService()
    {
    }

    /// <summary>
    /// 构造函数：经共享契约读取脚本库统计（ADR D2），插件未加载时安全降级为空统计。
    /// </summary>
    public UsageStatsService(IServiceProvider serviceProvider)
    {
        _scriptStatsProvider = serviceProvider.GetService<IScriptLibraryStatsProvider>();
    }

    /// <inheritdoc />
    public Task<long> RecordUsageAsync(
        string pluginId,
        string toolId,
        string actionType,
        long? durationMs = null,
        Dictionary<string, object>? metadata = null,
        string? userAgent = null,
        string? ipAddress = null)
    {
        try
        {
            XTrace.Log.Debug("记录使用统计: pluginId={0}, toolId={1}, actionType={2}", pluginId, toolId, actionType);

            var record = new XCodeUsageRecord
            {
                PluginId = pluginId,
                ToolId = toolId,
                ActionType = actionType,
                DurationMs = durationMs ?? 0,
                Timestamp = DateTime.UtcNow,
                UserAgent = userAgent,
                IpAddress = ipAddress,
                MetadataJson = metadata != null ? JsonSerializer.Serialize(metadata) : null
            };

            record.Insert();

            UpdateDailySummary(record);

            XTrace.Log.Debug("使用统计记录成功: recordId={0}", record.Id);
            return Task.FromResult(record.Id);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("记录使用统计失败: pluginId={0}, toolId={1}, error={2}", pluginId, toolId, ex.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public Task<PagedResult<UsageRecord>> GetUsageRecordsAsync(
        string? pluginId = null,
        string? toolId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 20)
    {
        try
        {
            XTrace.Log.Debug("查询使用记录: pluginId={0}, toolId={1}, page={2}, pageSize={3}", pluginId, toolId, page, pageSize);

            var exp = new WhereExpression();

            if (!string.IsNullOrWhiteSpace(pluginId))
                exp &= XCodeUsageRecord._.PluginId == pluginId;

            if (!string.IsNullOrWhiteSpace(toolId))
                exp &= XCodeUsageRecord._.ToolId == toolId;

            if (startDate.HasValue)
                exp &= XCodeUsageRecord._.Timestamp >= startDate.Value;

            if (endDate.HasValue)
                exp &= XCodeUsageRecord._.Timestamp <= endDate.Value;

            var pageParam = new PageParameter
            {
                PageIndex = page - 1,
                PageSize = pageSize,
                Sort = XCodeUsageRecord._.Timestamp,
                Desc = true
            };

            var items = XCodeUsageRecord.FindAll(exp, pageParam);
            // 显式 COUNT 取总数：pageParam.TotalCount 在部分 XCode 分页路径下不回填，
            // 用 FindCount 保证总数准确且与实体缓存开关无关
            var total = (int)XCodeUsageRecord.FindCount(exp);

            return Task.FromResult(new PagedResult<UsageRecord>
            {
                Items = items.Select(ToModel).ToList(),
                Total = total,
                Page = page,
                PageSize = pageSize
            });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("查询使用记录失败: {0}", ex.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public Task<List<UsageDailySummary>> GetDailySummaryAsync(
        string? pluginId = null,
        string? toolId = null,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        try
        {
            XTrace.Log.Debug("获取每日汇总: pluginId={0}, toolId={1}", pluginId, toolId);

            var exp = new WhereExpression();

            if (!string.IsNullOrWhiteSpace(pluginId))
                exp &= XCodeUsageDailySummary._.PluginId == pluginId;

            if (!string.IsNullOrWhiteSpace(toolId))
                exp &= XCodeUsageDailySummary._.ToolId == toolId;

            if (startDate.HasValue)
                exp &= XCodeUsageDailySummary._.Date >= startDate.Value;

            if (endDate.HasValue)
                exp &= XCodeUsageDailySummary._.Date <= endDate.Value;

            var list = XCodeUsageDailySummary.FindAll(exp)
                .OrderByDescending(s => s.Date)
                .ThenBy(s => s.PluginId)
                .ThenBy(s => s.ToolId)
                .ToList();

            return Task.FromResult(list.Select(ToModel).ToList());
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取每日汇总失败: {0}", ex.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public Task<List<TopToolItem>> GetTopToolsAsync(
        string? pluginId = null,
        int limit = 10,
        string period = "30d")
    {
        try
        {
            XTrace.Log.Debug("获取最常用工具: pluginId={0}, limit={1}, period={2}", pluginId, limit, period);

            var startDate = GetPeriodStartDate(period);

            var exp = new WhereExpression();

            if (!string.IsNullOrWhiteSpace(pluginId))
                exp &= XCodeUsageRecord._.PluginId == pluginId;

            exp &= XCodeUsageRecord._.Timestamp >= startDate;

            var records = XCodeUsageRecord.FindAll(exp);

            var topTools = records
                .GroupBy(r => new { r.PluginId, r.ToolId })
                .Select(g => new TopToolItem
                {
                    PluginId = g.Key.PluginId,
                    ToolId = g.Key.ToolId,
                    UseCount = g.Count(),
                    TotalDurationMs = g.Sum(r => r.DurationMs)
                })
                .OrderByDescending(t => t.UseCount)
                .Take(limit)
                .ToList();

            return Task.FromResult(topTools);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取最常用工具失败: {0}", ex.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public Task<List<UsageTrendPoint>> GetUsageTrendAsync(
        string? pluginId = null,
        string period = "7d")
    {
        try
        {
            XTrace.Log.Debug("获取使用趋势: pluginId={0}, period={1}", pluginId, period);

            var startDate = GetPeriodStartDate(period);
            var endDate = DateTime.UtcNow.Date;

            var exp = new WhereExpression();

            if (!string.IsNullOrWhiteSpace(pluginId))
                exp &= XCodeUsageRecord._.PluginId == pluginId;

            exp &= XCodeUsageRecord._.Timestamp >= startDate & XCodeUsageRecord._.Timestamp <= endDate.AddDays(1);

            var records = XCodeUsageRecord.FindAll(exp);

            var trendData = records
                .GroupBy(r => r.Timestamp.Date)
                .Select(g => new UsageTrendPoint
                {
                    Date = g.Key,
                    UseCount = g.Count(),
                    TotalDurationMs = g.Sum(r => r.DurationMs)
                })
                .OrderBy(t => t.Date)
                .ToList();

            var result = FillMissingDates(trendData, startDate, endDate);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取使用趋势失败: {0}", ex.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public Task<List<ToolRankingItem>> GetToolRankingAsync(
        string? pluginId = null,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        try
        {
            XTrace.Log.Debug("获取工具排名: pluginId={0}", pluginId);

            var exp = new WhereExpression();

            if (!string.IsNullOrWhiteSpace(pluginId))
                exp &= XCodeUsageRecord._.PluginId == pluginId;

            if (startDate.HasValue)
                exp &= XCodeUsageRecord._.Timestamp >= startDate.Value;

            if (endDate.HasValue)
                exp &= XCodeUsageRecord._.Timestamp <= endDate.Value;

            var records = XCodeUsageRecord.FindAll(exp);

            var rankings = records
                .GroupBy(r => new { r.PluginId, r.ToolId })
                .Select(g => new ToolRankingItem
                {
                    PluginId = g.Key.PluginId,
                    ToolId = g.Key.ToolId,
                    UseCount = g.Count(),
                    TotalDurationMs = g.Sum(r => r.DurationMs),
                    Rank = 0
                })
                .OrderByDescending(t => t.UseCount)
                .ThenByDescending(t => t.TotalDurationMs)
                .ToList();

            for (int i = 0; i < rankings.Count; i++)
            {
                rankings[i].Rank = i + 1;
            }

            return Task.FromResult(rankings);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取工具排名失败: {0}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// 更新每日汇总
    /// </summary>
    /// <param name="record">使用记录</param>
    private void UpdateDailySummary(XCodeUsageRecord record)
    {
        try
        {
            var date = record.Timestamp.Date;

            var exp = new WhereExpression();
            exp &= XCodeUsageDailySummary._.Date == date;
            exp &= XCodeUsageDailySummary._.PluginId == record.PluginId;
            exp &= XCodeUsageDailySummary._.ToolId == record.ToolId;

            var summary = XCodeUsageDailySummary.Find(exp);

            if (summary == null)
            {
                summary = new XCodeUsageDailySummary
                {
                    Date = date,
                    PluginId = record.PluginId,
                    ToolId = record.ToolId,
                    UseCount = 1,
                    TotalDurationMs = record.DurationMs,
                    UniqueUsers = !string.IsNullOrWhiteSpace(record.IpAddress) ? 1 : 0
                };
                summary.Insert();
            }
            else
            {
                summary.UseCount++;
                summary.TotalDurationMs += record.DurationMs;

                if (!string.IsNullOrWhiteSpace(record.IpAddress))
                {
                    var uniqueIpsExp = new WhereExpression();
                    uniqueIpsExp &= XCodeUsageRecord._.Timestamp >= date & XCodeUsageRecord._.Timestamp < date.AddDays(1);
                    uniqueIpsExp &= XCodeUsageRecord._.PluginId == record.PluginId;
                    uniqueIpsExp &= XCodeUsageRecord._.ToolId == record.ToolId;
                    uniqueIpsExp &= XCodeUsageRecord._.IpAddress != null;

                    var uniqueIps = XCodeUsageRecord.FindAll(uniqueIpsExp)
                        .Select(r => r.IpAddress)
                        .Distinct()
                        .Count();
                    summary.UniqueUsers = uniqueIps;
                }

                summary.Update();
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("更新每日汇总失败: {0}", ex.Message);
        }
    }

    /// <summary>
    /// 获取周期开始日期
    /// </summary>
    /// <param name="period">周期字符串（7d/30d/90d）</param>
    /// <returns>开始日期</returns>
    private static DateTime GetPeriodStartDate(string period)
    {
        var days = period.ToLowerInvariant() switch
        {
            "7d" => 7,
            "30d" => 30,
            "90d" => 90,
            _ => 30
        };

        return DateTime.UtcNow.Date.AddDays(-days + 1);
    }

    /// <summary>
    /// 填充缺失的日期数据
    /// </summary>
    /// <param name="trendData">趋势数据</param>
    /// <param name="startDate">开始日期</param>
    /// <param name="endDate">结束日期</param>
    /// <returns>填充后的趋势数据</returns>
    private static List<UsageTrendPoint> FillMissingDates(
        List<UsageTrendPoint> trendData,
        DateTime startDate,
        DateTime endDate)
    {
        var dataDict = trendData.ToDictionary(t => t.Date.Date);
        var result = new List<UsageTrendPoint>();

        for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
        {
            if (dataDict.TryGetValue(date, out var point))
            {
                result.Add(point);
            }
            else
            {
                result.Add(new UsageTrendPoint
                {
                    Date = date,
                    UseCount = 0,
                    TotalDurationMs = 0
                });
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<PersonalLibraryStatsDto> GetPersonalLibraryStatsAsync(string timeRange = "30d")
    {
        try
        {
            XTrace.Log.Debug("获取个人工具库统计: timeRange={0}", timeRange);

            var startDate = GetPeriodStartDate(timeRange);

            var stats = new PersonalLibraryStatsDto();

            try
            {
                if (_scriptStatsProvider != null)
                {
                    var libraryStats = await _scriptStatsProvider.GetLibraryStatsAsync();
                    stats.ScriptCount = libraryStats.ScriptCount;
                    stats.CodeSnippetCount = libraryStats.CodeSnippetCount;
                    stats.FavoriteCount = libraryStats.FavoriteCount;
                    stats.TopScripts = libraryStats.TopScripts;
                }
            }
            catch (Exception ex)
            {
                XTrace.Log.Warn("获取脚本统计数据失败: {0}", ex.Message);
            }

            try
            {
                var exp = XCodeUsageRecord._.Timestamp >= startDate;
                var usageRecords = XCodeUsageRecord.FindAll(exp);

                var totalUsage = new
                {
                    Count = usageRecords.Count,
                    TotalDuration = usageRecords.Sum(r => r.DurationMs)
                };

                stats.TotalUsageCount = totalUsage.Count;
                stats.TotalUsageDurationSeconds = totalUsage.TotalDuration / 1000.0;
            }
            catch (Exception ex)
            {
                XTrace.Log.Warn("获取使用统计数据失败: {0}", ex.Message);
            }

            try
            {
                var topTools = await GetTopToolsAsync(null, 10, timeRange);
                stats.TopTools = topTools;
            }
            catch (Exception ex)
            {
                XTrace.Log.Warn("获取常用工具失败: {0}", ex.Message);
            }

            try
            {
                var workflowExp = XCodeWorkflowUsageRecord._.StartTime >= startDate;
                var workflowRecords = XCodeWorkflowUsageRecord.FindAll(workflowExp);

                var totalWorkflows = XCodeWorkflowUsageRecord.FindAll()
                    .Select(r => r.WorkflowId)
                    .Distinct()
                    .Count();

                stats.WorkflowCount = totalWorkflows;

                var popularWorkflows = workflowRecords
                    .GroupBy(r => new { r.WorkflowId, r.WorkflowName })
                    .Select(g => new PopularWorkflowDto
                    {
                        WorkflowId = g.Key.WorkflowId,
                        WorkflowName = g.Key.WorkflowName,
                        ExecutionCount = g.Count(),
                        SuccessRate = g.Count() > 0
                            ? (double)g.Count(r => r.Status == (int)WorkflowExecutionStatus.Success) / g.Count() * 100
                            : 0,
                        AverageDurationSeconds = g.Average(r => r.DurationSeconds)
                    })
                    .OrderByDescending(w => w.ExecutionCount)
                    .Take(10)
                    .ToList();

                for (int i = 0; i < popularWorkflows.Count; i++)
                {
                    popularWorkflows[i].Rank = i + 1;
                }

                stats.TopWorkflows = popularWorkflows;
            }
            catch (Exception ex)
            {
                XTrace.Log.Warn("获取工作流统计数据失败: {0}", ex.Message);
            }

            return stats;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取个人工具库统计失败: {0}", ex.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<GrowthCurvePoint>> GetGrowthCurveAsync(int days = 30)
    {
        try
        {
            XTrace.Log.Debug("获取成长曲线: days={0}", days);

            var startDate = DateTime.UtcNow.Date.AddDays(-days + 1);
            var endDate = DateTime.UtcNow.Date;

            var usageExp = XCodeUsageRecord._.Timestamp >= startDate & XCodeUsageRecord._.Timestamp <= endDate.AddDays(1);
            var usageRecords = XCodeUsageRecord.FindAll(usageExp);

            var usageData = usageRecords
                .GroupBy(r => r.Timestamp.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    UsageCount = g.Count(),
                    TotalDurationMs = g.Sum(r => r.DurationMs)
                })
                .ToDictionary(r => r.Date);

            var newScriptsByDate = new Dictionary<DateTime, int>();
            var newSnippetsByDate = new Dictionary<DateTime, int>();

            try
            {
                if (_scriptStatsProvider != null)
                {
                    // 与旧 EF 查询一致：结束时间取 endDate.AddDays(1)（闭区间）。
                    var creationStats = await _scriptStatsProvider.GetCreationStatsAsync(startDate, endDate.AddDays(1));
                    newScriptsByDate = creationStats.NewScriptsByDate;
                    newSnippetsByDate = creationStats.NewCodeSnippetsByDate;
                }
            }
            catch (Exception ex)
            {
                XTrace.Log.Warn("获取脚本/片段创建数据失败: {0}", ex.Message);
            }

            var newWorkflowsByDate = new Dictionary<DateTime, int>();
            try
            {
                var workflowExp = XCodeWorkflowUsageRecord._.StartTime >= startDate & XCodeWorkflowUsageRecord._.StartTime <= endDate.AddDays(1);
                var workflowRecords = XCodeWorkflowUsageRecord.FindAll(workflowExp);

                newWorkflowsByDate = workflowRecords
                    .GroupBy(r => r.StartTime.Date)
                    .Select(g => new { Date = g.Key, Count = g.Select(r => r.WorkflowId).Distinct().Count() })
                    .ToDictionary(r => r.Date, r => r.Count);
            }
            catch (Exception ex)
            {
                XTrace.Log.Warn("获取工作流创建数据失败: {0}", ex.Message);
            }

            var result = new List<GrowthCurvePoint>();
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var point = new GrowthCurvePoint
                {
                    Date = date,
                    UsageCount = usageData.TryGetValue(date, out var usage) ? usage.UsageCount : 0,
                    NewScripts = newScriptsByDate.TryGetValue(date, out var scripts) ? scripts : 0,
                    NewCodeSnippets = newSnippetsByDate.TryGetValue(date, out var snippets) ? snippets : 0,
                    NewWorkflows = newWorkflowsByDate.TryGetValue(date, out var workflows) ? workflows : 0,
                    NewTools = 0,
                    TotalDurationSeconds = usageData.TryGetValue(date, out var ud) ? ud.TotalDurationMs / 1000.0 : 0
                };
                result.Add(point);
            }

            return result;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取成长曲线失败: {0}", ex.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<TimeSavedEstimateDto> GetTimeSavedEstimateAsync()
    {
        try
        {
            XTrace.Log.Debug("获取节省时间估算");

            var averageSavedSecondsPerToolUse = 120.0;
            var averageSavedSecondsPerScriptUse = 300.0;
            var averageSavedSecondsPerWorkflowUse = 600.0;

            var totalSavedSeconds = 0.0;
            var totalUsageCount = 0;
            var byCategory = new List<TimeSavedCategoryItem>();

            try
            {
                var toolUsageByPlugin = XCodeUsageRecord.FindAll()
                    .GroupBy(r => r.PluginId)
                    .Select(g => new
                    {
                        PluginId = g.Key,
                        Count = g.Count()
                    })
                    .ToList();

                foreach (var item in toolUsageByPlugin)
                {
                    var savedSeconds = item.Count * averageSavedSecondsPerToolUse;
                    totalSavedSeconds += savedSeconds;
                    totalUsageCount += item.Count;

                    byCategory.Add(new TimeSavedCategoryItem
                    {
                        Category = item.PluginId,
                        UsageCount = item.Count,
                        TimeSavedSeconds = savedSeconds
                    });
                }
            }
            catch (Exception ex)
            {
                XTrace.Log.Warn("获取工具使用统计失败: {0}", ex.Message);
            }

            try
            {
                if (_scriptStatsProvider != null)
                {
                    var totals = await _scriptStatsProvider.GetUsageTotalsAsync();

                    var scriptSavedSeconds = totals.ScriptUsageCount * averageSavedSecondsPerScriptUse;
                    totalSavedSeconds += scriptSavedSeconds;
                    totalUsageCount += totals.ScriptUsageCount;

                    byCategory.Add(new TimeSavedCategoryItem
                    {
                        Category = "脚本",
                        UsageCount = totals.ScriptUsageCount,
                        TimeSavedSeconds = scriptSavedSeconds
                    });

                    var snippetSavedSeconds = totals.CodeSnippetUsageCount * 60.0;
                    totalSavedSeconds += snippetSavedSeconds;
                    totalUsageCount += totals.CodeSnippetUsageCount;

                    byCategory.Add(new TimeSavedCategoryItem
                    {
                        Category = "代码片段",
                        UsageCount = totals.CodeSnippetUsageCount,
                        TimeSavedSeconds = snippetSavedSeconds
                    });
                }
            }
            catch (Exception ex)
            {
                XTrace.Log.Warn("获取脚本使用统计失败: {0}", ex.Message);
            }

            try
            {
                var workflowUsageCount = (int)XCodeWorkflowUsageRecord.FindCount();
                var workflowSavedSeconds = workflowUsageCount * averageSavedSecondsPerWorkflowUse;
                totalSavedSeconds += workflowSavedSeconds;
                totalUsageCount += workflowUsageCount;

                byCategory.Add(new TimeSavedCategoryItem
                {
                    Category = "工作流",
                    UsageCount = workflowUsageCount,
                    TimeSavedSeconds = workflowSavedSeconds
                });
            }
            catch (Exception ex)
            {
                XTrace.Log.Warn("获取工作流使用统计失败: {0}", ex.Message);
            }

            return new TimeSavedEstimateDto
            {
                TotalTimeSavedSeconds = totalSavedSeconds,
                TotalUsageCount = totalUsageCount,
                AverageSavedSecondsPerUse = totalUsageCount > 0 ? totalSavedSeconds / totalUsageCount : 0,
                ByCategory = byCategory.OrderByDescending(c => c.TimeSavedSeconds).ToList()
            };
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取节省时间估算失败: {0}", ex.Message);
            throw;
        }
    }

    private static UsageRecord ToModel(XCodeUsageRecord entity)
    {
        return new UsageRecord
        {
            Id = entity.Id,
            PluginId = entity.PluginId,
            ToolId = entity.ToolId,
            ActionType = entity.ActionType,
            UserAgent = entity.UserAgent,
            IpAddress = entity.IpAddress,
            DurationMs = entity.DurationMs,
            // SQLite/XCode 读出 DateTime 时丢失 Kind（变为 Unspecified）；
            // 写入侧始终用 DateTime.UtcNow，读出统一指定为 Utc，保证时间语义正确
            Timestamp = entity.Timestamp.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(entity.Timestamp, DateTimeKind.Utc)
                : entity.Timestamp.ToUniversalTime(),
            MetadataJson = entity.MetadataJson,
            WorkflowExecutionId = entity.WorkflowExecutionId > 0 ? entity.WorkflowExecutionId : null,
            StepId = entity.StepId
        };
    }

    private static UsageDailySummary ToModel(XCodeUsageDailySummary entity)
    {
        return new UsageDailySummary
        {
            Id = entity.Id,
            Date = entity.Date,
            PluginId = entity.PluginId,
            ToolId = entity.ToolId,
            UseCount = entity.UseCount,
            TotalDurationMs = entity.TotalDurationMs,
            UniqueUsers = entity.UniqueUsers
        };
    }
}
