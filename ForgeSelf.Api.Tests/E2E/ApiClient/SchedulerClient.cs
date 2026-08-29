using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.Scheduler.Models;

namespace ForgeSelf.Api.Tests.E2E.ApiClient;

/// <summary>
/// 定时任务调度器 API 客户端
/// </summary>
public class SchedulerClient : BaseApiClient
{
    public SchedulerClient(HttpClient client, string? baseUrl = null) : base(client, baseUrl)
    {
    }

    /// <summary>
    /// 获取任务列表
    /// </summary>
    public async Task<ForgeSelf.Api.Plugins.Scheduler.Models.PagedResult<ScheduledTaskDto>?> GetTasksAsync(string? keyword = null, ScheduledTaskStatus? status = null, int page = 1, int pageSize = 20)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["page"] = page.ToString(),
            ["pageSize"] = pageSize.ToString()
        };

        if (!string.IsNullOrWhiteSpace(keyword))
            queryParams["keyword"] = keyword;

        if (status.HasValue)
            queryParams["status"] = ((int)status.Value).ToString();

        return await GetAsync<ForgeSelf.Api.Plugins.Scheduler.Models.PagedResult<ScheduledTaskDto>>("api/scheduler/tasks", queryParams);
    }

    /// <summary>
    /// 获取任务详情
    /// </summary>
    public async Task<ScheduledTaskDto?> GetTaskAsync(long id)
    {
        return await GetAsync<ScheduledTaskDto>($"api/scheduler/tasks/{id}");
    }

    /// <summary>
    /// 创建任务
    /// </summary>
    public async Task<ScheduledTaskDto?> CreateTaskAsync(CreateScheduledTaskRequest request)
    {
        return await PostAsync<ScheduledTaskDto>("api/scheduler/tasks", request);
    }

    /// <summary>
    /// 更新任务
    /// </summary>
    public async Task<ScheduledTaskDto?> UpdateTaskAsync(long id, UpdateScheduledTaskRequest request)
    {
        return await PutAsync<ScheduledTaskDto>($"api/scheduler/tasks/{id}", request);
    }

    /// <summary>
    /// 删除任务
    /// </summary>
    public async Task<bool> DeleteTaskAsync(long id)
    {
        var response = await DeleteAsync<ApiResponse>($"api/scheduler/tasks/{id}");
        return response?.Success == true;
    }

    /// <summary>
    /// 切换任务状态
    /// </summary>
    public async Task<ScheduledTaskDto?> ToggleTaskStatusAsync(long id, bool enabled)
    {
        return await PostAsync<ScheduledTaskDto>($"api/scheduler/tasks/{id}/toggle", new { enabled });
    }

    /// <summary>
    /// 立即执行任务
    /// </summary>
    public async Task<bool> RunNowAsync(long id)
    {
        var response = await PostAsync<ApiResponse>($"api/scheduler/tasks/{id}/run-now", null);
        return response?.Success == true;
    }

    /// <summary>
    /// 获取任务执行日志
    /// </summary>
    public async Task<ForgeSelf.Api.Plugins.Scheduler.Models.PagedResult<ScheduledTaskLogDto>?> GetTaskLogsAsync(long id, int page = 1, int pageSize = 20)
    {
        return await GetAsync<ForgeSelf.Api.Plugins.Scheduler.Models.PagedResult<ScheduledTaskLogDto>>($"api/scheduler/tasks/{id}/logs", new Dictionary<string, string>
        {
            ["page"] = page.ToString(),
            ["pageSize"] = pageSize.ToString()
        });
    }

    /// <summary>
    /// 解析 Cron 表达式
    /// </summary>
    public async Task<CronParseResult?> ParseCronAsync(string cronExpression, int count = 5, string timeZone = "Asia/Shanghai")
    {
        return await PostAsync<CronParseResult>("api/scheduler/parse-cron", new CronParseRequest
        {
            CronExpression = cronExpression,
            Count = count,
            TimeZone = timeZone
        });
    }
}
