using System.Text.Json;
using ForgeSelf.Abstractions;
using NewLife.Data;
using NewLife.Log;
using XCode;
using WorkflowDefEntity = ForgeSelf.Api.Plugins.WorkflowEngine.Entities.WorkflowDefinition;
using WorkflowExecEntity = ForgeSelf.Api.Plugins.WorkflowEngine.Entities.WorkflowExecution;

namespace ForgeSelf.Api.Plugins.WorkflowEngine.Services;

public class WorkflowService : IWorkflowService
{
    private readonly IWorkflowScheduler _scheduler;

    public WorkflowService(IWorkflowScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    public Task<WorkflowDefinitionDto> CreateWorkflowAsync(CreateWorkflowRequest request)
    {
        XTrace.Log.Info("[WorkflowService] 创建工作流，Name: {0}", request.Name);

        var entity = new WorkflowDefEntity
        {
            Name = request.Name,
            Description = request.Description,
            Category = request.Category,
            Icon = request.Icon,
            StepsJson = JsonSerializer.Serialize(request.Steps),
            VariablesJson = JsonSerializer.Serialize(request.Variables),
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            IsFavorite = false,
            UsageCount = 0,
            Status = (int)WorkflowStatus.Draft,
            StartStepId = request.StartStepId
        };

        entity.Insert();

        XTrace.Log.Info("[WorkflowService] 工作流创建成功，Id: {0}", entity.Id);

        return Task.FromResult(EntityToDto(entity));
    }

    public Task<WorkflowDefinitionDto?> UpdateWorkflowAsync(long id, UpdateWorkflowRequest request)
    {
        XTrace.Log.Info("[WorkflowService] 更新工作流，Id: {0}", id);

        var entity = WorkflowDefEntity.FindById(id);
        if (entity == null)
        {
            XTrace.Log.Warn("[WorkflowService] 工作流不存在，Id: {0}", id);
            return Task.FromResult<WorkflowDefinitionDto?>(null);
        }

        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.Category = request.Category;
        entity.Icon = request.Icon;
        entity.StepsJson = JsonSerializer.Serialize(request.Steps);
        entity.VariablesJson = JsonSerializer.Serialize(request.Variables);
        entity.UpdatedAt = DateTime.Now;
        entity.Status = (int)request.Status;
        entity.StartStepId = request.StartStepId;

        entity.Update();

        XTrace.Log.Info("[WorkflowService] 工作流更新成功，Id: {0}", id);

        return Task.FromResult<WorkflowDefinitionDto?>(EntityToDto(entity));
    }

    public Task<bool> DeleteWorkflowAsync(long id)
    {
        XTrace.Log.Info("[WorkflowService] 删除工作流，Id: {0}", id);

        var entity = WorkflowDefEntity.FindById(id);
        if (entity == null)
        {
            XTrace.Log.Warn("[WorkflowService] 工作流不存在，Id: {0}", id);
            return Task.FromResult(false);
        }

        entity.Delete();

        XTrace.Log.Info("[WorkflowService] 工作流删除成功，Id: {0}", id);

        return Task.FromResult(true);
    }

    public Task<WorkflowDefinitionDto?> GetWorkflowAsync(long id)
    {
        var entity = WorkflowDefEntity.FindById(id);
        if (entity == null) return Task.FromResult<WorkflowDefinitionDto?>(null);

        return Task.FromResult<WorkflowDefinitionDto?>(EntityToDto(entity));
    }

    public Task<PagedResult<WorkflowDefinitionDto>> ListWorkflowsAsync(string? keyword = null, string? category = null, int page = 1, int pageSize = 20)
    {
        XTrace.Log.Debug("[WorkflowService] 查询工作流列表，keyword: {0}, category: {1}, page: {2}, pageSize: {3}",
            keyword, category, page, pageSize);

        var exp = new WhereExpression();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var keywordLower = keyword.ToLower();
            exp &= (WorkflowDefEntity._.Name.Contains(keyword) | WorkflowDefEntity._.Description.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            exp &= WorkflowDefEntity._.Category == category;
        }

        var pageParam = new PageParameter
        {
            PageIndex = page - 1,
            PageSize = pageSize,
            Sort = WorkflowDefEntity.__.UpdatedAt,
            Desc = true,
            RetrieveTotalCount = true
        };

        var entities = WorkflowDefEntity.FindAll(exp, pageParam);
        var total = (int)pageParam.TotalCount;

        var items = entities.Select(EntityToDto).ToList();

        XTrace.Log.Info("[WorkflowService] 查询工作流列表成功，总数: {0}, 当前页数量: {1}", total, items.Count);

        return Task.FromResult(new PagedResult<WorkflowDefinitionDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public Task<PagedResult<WorkflowExecutionDto>> GetExecutionsAsync(long? workflowId = null, int page = 1, int pageSize = 20)
    {
        XTrace.Log.Debug("[WorkflowService] 查询执行记录，workflowId: {0}, page: {1}, pageSize: {2}",
            workflowId, page, pageSize);

        var exp = new WhereExpression();

        if (workflowId.HasValue)
        {
            exp &= WorkflowExecEntity._.WorkflowId == workflowId.Value;
        }

        var pageParam = new PageParameter
        {
            PageIndex = page - 1,
            PageSize = pageSize,
            Sort = WorkflowExecEntity.__.StartTime,
            Desc = true,
            RetrieveTotalCount = true
        };

        var entities = WorkflowExecEntity.FindAll(exp, pageParam);
        var total = (int)pageParam.TotalCount;

        var items = entities.Select(ExecutionEntityToDto).ToList();

        XTrace.Log.Info("[WorkflowService] 查询执行记录成功，总数: {0}, 当前页数量: {1}", total, items.Count);

        return Task.FromResult(new PagedResult<WorkflowExecutionDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<WorkflowExecutionDetailDto?> GetExecutionDetailAsync(long executionId)
    {
        XTrace.Log.Debug("[WorkflowService] 获取执行详情，executionId: {0}", executionId);

        var execution = await _scheduler.GetExecutionAsync(executionId);
        if (execution == null) return null;

        return new WorkflowExecutionDetailDto
        {
            Id = execution.Id,
            WorkflowId = execution.WorkflowId,
            WorkflowName = execution.WorkflowName,
            Status = execution.Status,
            StartTime = execution.StartTime,
            EndTime = execution.EndTime,
            CurrentStepId = execution.CurrentStepId,
            Logs = execution.Logs,
            ResultsJson = execution.ResultsJson,
            ErrorMessage = execution.ErrorMessage,
            Variables = execution.Variables,
            StepResults = execution.StepResults,
            Progress = execution.Progress,
            TriggeredBy = execution.TriggeredBy
        };
    }

    public Task<bool> FavoriteWorkflowAsync(long id, bool isFavorite)
    {
        XTrace.Log.Info("[WorkflowService] 收藏/取消收藏工作流，Id: {0}, isFavorite: {1}", id, isFavorite);

        var entity = WorkflowDefEntity.FindById(id);
        if (entity == null)
        {
            XTrace.Log.Warn("[WorkflowService] 工作流不存在，Id: {0}", id);
            return Task.FromResult(false);
        }

        entity.IsFavorite = isFavorite;
        entity.UpdatedAt = DateTime.Now;

        entity.Update();

        XTrace.Log.Info("[WorkflowService] 工作流收藏状态更新成功，Id: {0}, isFavorite: {1}", id, isFavorite);

        return Task.FromResult(true);
    }

    public Task<bool> IncrementUsageCountAsync(long id)
    {
        XTrace.Log.Debug("[WorkflowService] 增加使用次数，Id: {0}", id);

        var entity = WorkflowDefEntity.FindById(id);
        if (entity == null) return Task.FromResult(false);

        entity.UsageCount++;
        entity.UpdatedAt = DateTime.Now;

        entity.Update();

        return Task.FromResult(true);
    }

    public Task<List<WorkflowTemplateDto>> GetTemplatesAsync()
    {
        XTrace.Log.Debug("[WorkflowService] 获取推荐模板");

        var templates = new List<WorkflowTemplateDto>
        {
            new()
            {
                Id = "template-file-processing",
                Name = "文件批量处理",
                Description = "批量重命名、清理和压缩文件的工作流模板",
                Category = "文件处理",
                Icon = "fa-file-archive",
                Tags = new List<string> { "文件", "批量", "自动化" },
                StepCount = 3
            },
            new()
            {
                Id = "template-data-fetch",
                Name = "数据采集与分析",
                Description = "从HTTP API获取数据并进行分析处理的工作流模板",
                Category = "数据处理",
                Icon = "fa-chart-line",
                Tags = new List<string> { "API", "数据", "分析" },
                StepCount = 4
            },
            new()
            {
                Id = "template-approval",
                Name = "审批流程",
                Description = "多级审批流程模板，支持条件判断和通知",
                Category = "流程管理",
                Icon = "fa-check-double",
                Tags = new List<string> { "审批", "流程", "通知" },
                StepCount = 5
            },
            new()
            {
                Id = "template-report",
                Name = "定时报告生成",
                Description = "定时生成数据报告并发送通知的工作流模板",
                Category = "报表统计",
                Icon = "fa-file-alt",
                Tags = new List<string> { "报表", "定时", "通知" },
                StepCount = 4
            },
            new()
            {
                Id = "template-monitor-alert",
                Name = "监控告警",
                Description = "系统监控与告警通知工作流模板",
                Category = "监控运维",
                Icon = "fa-bell",
                Tags = new List<string> { "监控", "告警", "自动化" },
                StepCount = 3
            }
        };

        return Task.FromResult(templates);
    }

    public async Task<WorkflowExecutionDto> ExecuteWorkflowAsync(long id, ExecuteWorkflowRequest request)
    {
        XTrace.Log.Info("[WorkflowService] 执行工作流，Id: {0}", id);

        await IncrementUsageCountAsync(id);

        var execution = await _scheduler.QueueExecutionAsync(
            id,
            request.InputVariables,
            request.TriggeredBy);

        return new WorkflowExecutionDto
        {
            Id = execution.Id,
            WorkflowId = execution.WorkflowId,
            WorkflowName = execution.WorkflowName,
            Status = execution.Status,
            StartTime = execution.StartTime,
            EndTime = execution.EndTime,
            CurrentStepId = execution.CurrentStepId,
            ErrorMessage = execution.ErrorMessage,
            Progress = execution.Progress,
            TriggeredBy = execution.TriggeredBy
        };
    }

    private static WorkflowDefinitionDto EntityToDto(WorkflowDefEntity entity)
    {
        var steps = !string.IsNullOrEmpty(entity.StepsJson)
            ? JsonSerializer.Deserialize<List<WorkflowStep>>(entity.StepsJson) ?? new List<WorkflowStep>()
            : new List<WorkflowStep>();

        var variables = !string.IsNullOrEmpty(entity.VariablesJson)
            ? JsonSerializer.Deserialize<List<WorkflowVariable>>(entity.VariablesJson) ?? new List<WorkflowVariable>()
            : new List<WorkflowVariable>();

        return new WorkflowDefinitionDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Category = entity.Category,
            Icon = entity.Icon,
            Steps = steps,
            Variables = variables,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            IsFavorite = entity.IsFavorite,
            UsageCount = entity.UsageCount,
            Status = (WorkflowStatus)entity.Status,
            StartStepId = entity.StartStepId
        };
    }

    private static WorkflowExecutionDto ExecutionEntityToDto(WorkflowExecEntity entity)
    {
        return new WorkflowExecutionDto
        {
            Id = entity.Id,
            WorkflowId = entity.WorkflowId,
            WorkflowName = entity.WorkflowName,
            Status = (WorkflowStatus)entity.Status,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            CurrentStepId = entity.CurrentStepId,
            ErrorMessage = entity.ErrorMessage,
            Progress = entity.Progress,
            TriggeredBy = entity.TriggeredBy
        };
    }
}
