using System.Text.Json;
using OpenForgeSelf.Backend.Entities;
using OpenForgeSelf.Backend.Models.UsageStats;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using XCode;
using XCode.DataAccessLayer;
using WorkflowUsageRecordEntity = OpenForgeSelf.Backend.Entities.WorkflowUsageRecord;
using UsageRecordEntity = OpenForgeSelf.Backend.Entities.UsageRecord;

namespace OpenForgeSelf.Backend.Services.UsageStats;

public class WorkflowUsageService : IWorkflowUsageService
{
    public WorkflowUsageService()
    {
    }

    public Task<long> RecordWorkflowExecutionAsync(RecordWorkflowExecutionRequest request, string? ipAddress = null)
    {
        try
        {
            XTrace.Log.Debug("记录工作流执行: workflowId={0}, executionId={1}", request.WorkflowId, request.ExecutionId);

            var record = new WorkflowUsageRecordEntity
            {
                WorkflowId = request.WorkflowId,
                WorkflowName = request.WorkflowName,
                ExecutionId = request.ExecutionId,
                StartTime = request.StartTime,
                EndTime = request.EndTime ?? DateTime.MinValue,
                Status = (int)request.Status,
                DurationSeconds = request.DurationSeconds,
                InputVariablesJson = request.InputVariables != null ? JsonSerializer.Serialize(request.InputVariables) : null,
                OutputResultJson = request.OutputResult != null ? JsonSerializer.Serialize(request.OutputResult) : null,
                ToolCallCount = request.ToolCallCount,
                StepCount = request.StepCount,
                TriggeredBy = request.TriggeredBy,
                IpAddress = ipAddress,
                ErrorMessage = request.ErrorMessage
            };

            record.Insert();

            XTrace.Log.Debug("工作流执行记录成功: recordId={0}", record.Id);
            return Task.FromResult(record.Id);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("记录工作流执行失败: workflowId={0}, error={1}", request.WorkflowId, ex.Message);
            throw;
        }
    }

    public Task UpdateWorkflowExecutionAsync(long executionId, WorkflowExecutionStatus status, DateTime endTime, double durationSeconds, object? outputResult = null, string? errorMessage = null, int toolCallCount = 0)
    {
        try
        {
            XTrace.Log.Debug("更新工作流执行记录: executionId={0}, status={1}", executionId, status);

            var exp = WorkflowUsageRecordEntity._.ExecutionId == executionId;
            var record = WorkflowUsageRecordEntity.Find(exp);

            if (record == null)
            {
                XTrace.Log.Warn("工作流执行记录不存在: executionId={0}", executionId);
                return Task.CompletedTask;
            }

            record.Status = (int)status;
            record.EndTime = endTime;
            record.DurationSeconds = durationSeconds;
            record.ToolCallCount = toolCallCount;
            record.ErrorMessage = errorMessage;

            if (outputResult != null)
            {
                record.OutputResultJson = JsonSerializer.Serialize(outputResult);
            }

            record.Update();

            XTrace.Log.Debug("工作流执行记录更新成功: executionId={0}", executionId);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("更新工作流执行记录失败: executionId={0}, error={1}", executionId, ex.Message);
            throw;
        }
    }

    public Task<WorkflowStatsDto?> GetWorkflowStatsAsync(long workflowId, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            XTrace.Log.Debug("获取工作流统计: workflowId={0}", workflowId);

            var exp = new WhereExpression();
            exp &= WorkflowUsageRecordEntity._.WorkflowId == workflowId;

            if (startDate.HasValue)
                exp &= WorkflowUsageRecordEntity._.StartTime >= startDate.Value;

            if (endDate.HasValue)
                exp &= WorkflowUsageRecordEntity._.StartTime <= endDate.Value;

            var records = WorkflowUsageRecordEntity.FindAll(exp);

            if (records.Count == 0)
                return Task.FromResult<WorkflowStatsDto?>(null);

            var successCount = records.Count(r => r.Status == (int)WorkflowExecutionStatus.Success);
            var failedCount = records.Count(r => r.Status == (int)WorkflowExecutionStatus.Failed);
            var cancelledCount = records.Count(r => r.Status == (int)WorkflowExecutionStatus.Cancelled);

            var stats = new WorkflowStatsDto
            {
                WorkflowId = workflowId,
                WorkflowName = records.First().WorkflowName,
                TotalExecutions = records.Count,
                SuccessCount = successCount,
                FailedCount = failedCount,
                CancelledCount = cancelledCount,
                SuccessRate = records.Count > 0 ? (double)successCount / records.Count * 100 : 0,
                AverageDurationSeconds = records.Average(r => r.DurationSeconds),
                TotalDurationSeconds = records.Sum(r => r.DurationSeconds),
                AverageToolCallCount = (int)records.Average(r => r.ToolCallCount),
                AverageStepCount = (int)records.Average(r => r.StepCount)
            };

            return Task.FromResult<WorkflowStatsDto?>(stats);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取工作流统计失败: workflowId={0}, error={1}", workflowId, ex.Message);
            throw;
        }
    }

    public Task<List<PopularWorkflowDto>> GetPopularWorkflowsAsync(int limit = 10, string period = "30d")
    {
        try
        {
            XTrace.Log.Debug("获取常用工作流排行: limit={0}, period={1}", limit, period);

            var startDate = GetPeriodStartDate(period);

            var exp = WorkflowUsageRecordEntity._.StartTime >= startDate;
            var records = WorkflowUsageRecordEntity.FindAll(exp);

            var popularWorkflows = records
                .GroupBy(r => new { r.WorkflowId, r.WorkflowName })
                .Select(g => new PopularWorkflowDto
                {
                    WorkflowId = g.Key.WorkflowId,
                    WorkflowName = g.Key.WorkflowName,
                    ExecutionCount = g.Count(),
                    SuccessRate = g.Count() > 0
                        ? (double)g.Count(r => r.Status == (int)WorkflowExecutionStatus.Success) / g.Count() * 100
                        : 0,
                    AverageDurationSeconds = g.Average(r => r.DurationSeconds),
                    Rank = 0
                })
                .OrderByDescending(w => w.ExecutionCount)
                .ThenByDescending(w => w.SuccessRate)
                .Take(limit)
                .ToList();

            for (int i = 0; i < popularWorkflows.Count; i++)
            {
                popularWorkflows[i].Rank = i + 1;
            }

            return Task.FromResult(popularWorkflows);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取常用工作流排行失败: {0}", ex.Message);
            throw;
        }
    }

    public Task<List<WorkflowUsageTrendDto>> GetWorkflowUsageTrendAsync(long workflowId, int days = 7)
    {
        try
        {
            XTrace.Log.Debug("获取工作流使用趋势: workflowId={0}, days={1}", workflowId, days);

            var startDate = DateTime.UtcNow.Date.AddDays(-days + 1);
            var endDate = DateTime.UtcNow.Date;

            var exp = new WhereExpression();
            exp &= WorkflowUsageRecordEntity._.WorkflowId == workflowId;
            exp &= WorkflowUsageRecordEntity._.StartTime >= startDate & WorkflowUsageRecordEntity._.StartTime <= endDate.AddDays(1);

            var records = WorkflowUsageRecordEntity.FindAll(exp);

            var trendData = records
                .GroupBy(r => r.StartTime.Date)
                .Select(g => new WorkflowUsageTrendDto
                {
                    Date = g.Key,
                    ExecutionCount = g.Count(),
                    SuccessCount = g.Count(r => r.Status == (int)WorkflowExecutionStatus.Success),
                    FailedCount = g.Count(r => r.Status == (int)WorkflowExecutionStatus.Failed),
                    TotalDurationSeconds = g.Sum(r => r.DurationSeconds)
                })
                .OrderBy(t => t.Date)
                .ToList();

            var result = FillMissingTrendDates(trendData, startDate, endDate);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取工作流使用趋势失败: workflowId={0}, error={1}", workflowId, ex.Message);
            throw;
        }
    }

    public Task<WorkflowTotalStatsDto> GetTotalStatsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            XTrace.Log.Debug("获取工作流总体统计");

            var exp = new WhereExpression();

            if (startDate.HasValue)
                exp &= WorkflowUsageRecordEntity._.StartTime >= startDate.Value;

            if (endDate.HasValue)
                exp &= WorkflowUsageRecordEntity._.StartTime <= endDate.Value;

            var records = WorkflowUsageRecordEntity.FindAll(exp);

            var totalWorkflows = WorkflowUsageRecordEntity.FindAll()
                .Select(r => r.WorkflowId)
                .Distinct()
                .Count();

            var successCount = records.Count(r => r.Status == (int)WorkflowExecutionStatus.Success);
            var failedCount = records.Count(r => r.Status == (int)WorkflowExecutionStatus.Failed);
            var cancelledCount = records.Count(r => r.Status == (int)WorkflowExecutionStatus.Cancelled);

            var stats = new WorkflowTotalStatsDto
            {
                TotalWorkflows = totalWorkflows,
                TotalExecutions = records.Count,
                SuccessCount = successCount,
                FailedCount = failedCount,
                CancelledCount = cancelledCount,
                SuccessRate = records.Count > 0 ? (double)successCount / records.Count * 100 : 0,
                TotalDurationSeconds = records.Sum(r => r.DurationSeconds),
                AverageDurationSeconds = records.Count > 0 ? records.Average(r => r.DurationSeconds) : 0,
                TotalToolCalls = records.Sum(r => r.ToolCallCount)
            };

            return Task.FromResult(stats);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取工作流总体统计失败: {0}", ex.Message);
            throw;
        }
    }

    public Task<List<WorkflowToolRankingDto>> GetToolUsageInWorkflowsAsync(DateTime? startDate = null, DateTime? endDate = null, int limit = 10)
    {
        try
        {
            XTrace.Log.Debug("获取工作流内工具使用排行");

            var exp = new WhereExpression();
            exp &= UsageRecordEntity._.WorkflowExecutionId > 0;

            if (startDate.HasValue)
                exp &= UsageRecordEntity._.Timestamp >= startDate.Value;

            if (endDate.HasValue)
                exp &= UsageRecordEntity._.Timestamp <= endDate.Value;

            var records = UsageRecordEntity.FindAll(exp);

            var rankings = records
                .GroupBy(r => new { r.PluginId, r.ToolId })
                .Select(g => new WorkflowToolRankingDto
                {
                    PluginId = g.Key.PluginId,
                    ToolId = g.Key.ToolId,
                    UseCount = g.Count(),
                    WorkflowCount = g.Select(r => r.WorkflowExecutionId).Distinct().Count(),
                    TotalDurationMs = g.Sum(r => r.DurationMs),
                    Rank = 0
                })
                .OrderByDescending(t => t.UseCount)
                .ThenByDescending(t => t.WorkflowCount)
                .Take(limit)
                .ToList();

            for (int i = 0; i < rankings.Count; i++)
            {
                rankings[i].Rank = i + 1;
            }

            return Task.FromResult(rankings);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取工作流内工具使用排行失败: {0}", ex.Message);
            throw;
        }
    }

    public Task RecordToolUsageInWorkflowAsync(string pluginId, string toolId, string actionType, long durationMs, long workflowExecutionId, string? stepId = null, Dictionary<string, object>? metadata = null)
    {
        try
        {
            XTrace.Log.Debug("记录工作流内工具使用: workflowExecutionId={0}, toolId={1}", workflowExecutionId, toolId);

            var record = new UsageRecordEntity
            {
                PluginId = pluginId,
                ToolId = toolId,
                ActionType = actionType,
                DurationMs = durationMs,
                Timestamp = DateTime.UtcNow,
                MetadataJson = metadata != null ? JsonSerializer.Serialize(metadata) : null,
                WorkflowExecutionId = workflowExecutionId,
                StepId = stepId
            };

            record.Insert();
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("记录工作流内工具使用失败: toolId={0}, error={1}", toolId, ex.Message);
            throw;
        }
    }

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

    private static List<WorkflowUsageTrendDto> FillMissingTrendDates(
        List<WorkflowUsageTrendDto> trendData,
        DateTime startDate,
        DateTime endDate)
    {
        var dataDict = trendData.ToDictionary(t => t.Date.Date);
        var result = new List<WorkflowUsageTrendDto>();

        for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
        {
            if (dataDict.TryGetValue(date, out var point))
            {
                result.Add(point);
            }
            else
            {
                result.Add(new WorkflowUsageTrendDto
                {
                    Date = date,
                    ExecutionCount = 0,
                    SuccessCount = 0,
                    FailedCount = 0,
                    TotalDurationSeconds = 0
                });
            }
        }

        return result;
    }
}
