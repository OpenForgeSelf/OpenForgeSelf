using System.Text.Json;
using OpenForgeSelf.Backend.Plugins.AIAgent.Services;
using OpenForgeSelf.Backend.Plugins.WorkflowEngine.Models;
using OpenForgeSelf.Backend.Plugins.WorkflowEngine.Services;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.AIAgent.Controllers;

[ApiController]
[Route("api/ai-agent/workflow")]
public class AIWorkflowController : ControllerBase
{
    private readonly IWorkflowPlannerService _workflowPlannerService;
    private readonly IToolSelectorService _toolSelectorService;
    private readonly IServiceProvider _serviceProvider;

    public AIWorkflowController(
        IWorkflowPlannerService workflowPlannerService,
        IToolSelectorService toolSelectorService,
        IServiceProvider serviceProvider)
    {
        _workflowPlannerService = workflowPlannerService;
        _toolSelectorService = toolSelectorService;
        _serviceProvider = serviceProvider;
    }

    [HttpPost("plan")]
    public async Task<ActionResult<object>> PlanWorkflow([FromBody] PlanWorkflowRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.UserRequest))
            {
                return BadRequest(new { success = false, error = "用户需求描述不能为空" });
            }

            XTrace.Log.Info("[AIWorkflowController] 收到工作流规划请求，需求长度: {0}", request.UserRequest.Length);

            var workflow = await _workflowPlannerService.PlanWorkflowAsync(request.UserRequest, request.AvailableTools);

            var response = new
            {
                success = true,
                workflow = new
                {
                    name = workflow.Name,
                    description = workflow.Description,
                    category = workflow.Category,
                    steps = workflow.Steps.Select(s => new
                    {
                        id = s.Id,
                        name = s.Name,
                        description = s.Description,
                        type = s.Type.ToString(),
                        toolName = s.ToolName,
                        parametersJson = s.ParametersJson,
                        conditionExpression = s.ConditionExpression,
                        nextStepId = s.NextStepId,
                        errorHandling = new
                        {
                            maxRetries = s.ErrorHandling?.MaxRetries ?? 0,
                            retryDelayMs = s.ErrorHandling?.RetryDelayMs ?? 1000,
                            continueOnError = s.ErrorHandling?.ContinueOnError ?? false
                        }
                    }).ToList(),
                    variables = workflow.Variables.Select(v => new
                    {
                        name = v.Name,
                        type = v.Type,
                        defaultValue = v.DefaultValue,
                        description = v.Description
                    }).ToList(),
                    startStepId = workflow.StartStepId,
                    status = workflow.Status.ToString()
                },
                stepCount = workflow.Steps.Count,
                message = "工作流规划成功"
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIWorkflowController] 工作流规划失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = "工作流规划失败", details = ex.Message });
        }
    }

    [HttpPost("plan/prompt")]
    public async Task<ActionResult<object>> GetPlanPrompt([FromBody] PlanWorkflowRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.UserRequest))
            {
                return BadRequest(new { success = false, error = "用户需求描述不能为空" });
            }

            var prompt = await _workflowPlannerService.GetPlanPromptAsync(request.UserRequest, request.AvailableTools);

            return Ok(new { success = true, prompt });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIWorkflowController] 获取规划Prompt失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = "获取规划Prompt失败", details = ex.Message });
        }
    }

    [HttpPost("execute")]
    public async Task<ActionResult<object>> ExecuteWorkflow([FromBody] AIExecuteWorkflowRequest request)
    {
        try
        {
            XTrace.Log.Info("[AIWorkflowController] 收到工作流执行请求");

            var service = _serviceProvider.GetRequiredService<IWorkflowService>();

            WorkflowExecutionDto execution;
            if (request.WorkflowId.HasValue)
            {
                execution = await service.ExecuteWorkflowAsync(request.WorkflowId.Value, new ExecuteWorkflowRequest
                {
                    InputVariables = request.InputVariables,
                    TriggeredBy = "api"
                });
            }
            else if (request.WorkflowDefinition != null)
            {
                var createRequest = new CreateWorkflowRequest
                {
                    Name = request.WorkflowDefinition.Name ?? "AI生成的工作流",
                    Description = request.WorkflowDefinition.Description,
                    Category = request.WorkflowDefinition.Category,
                    Steps = request.WorkflowDefinition.Steps,
                    Variables = request.WorkflowDefinition.Variables,
                    StartStepId = request.WorkflowDefinition.StartStepId
                };

                var created = await service.CreateWorkflowAsync(createRequest);
                execution = await service.ExecuteWorkflowAsync(created.Id, new ExecuteWorkflowRequest
                {
                    InputVariables = request.InputVariables,
                    TriggeredBy = "api"
                });
            }
            else
            {
                return BadRequest(new { success = false, error = "必须提供 workflowId 或 workflowDefinition" });
            }

            var response = new
            {
                success = true,
                execution = new
                {
                    id = execution.Id,
                    workflowId = execution.WorkflowId,
                    workflowName = execution.WorkflowName,
                    status = execution.Status.ToString(),
                    startTime = execution.StartTime,
                    progress = execution.Progress,
                    currentStepId = execution.CurrentStepId,
                    triggeredBy = execution.TriggeredBy
                },
                message = "工作流已启动"
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIWorkflowController] 工作流执行失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = "工作流执行失败", details = ex.Message });
        }
    }

    [HttpGet("{executionId}/status")]
    public async Task<ActionResult<object>> GetExecutionStatus(long executionId)
    {
        try
        {
            XTrace.Log.Info("[AIWorkflowController] 查询工作流执行状态，executionId: {0}", executionId);

            var service = _serviceProvider.GetRequiredService<IWorkflowService>();

            var execution = await service.GetExecutionDetailAsync(executionId);
            if (execution == null)
            {
                return NotFound(new { success = false, error = "执行记录不存在" });
            }

            var response = new
            {
                success = true,
                execution = new
                {
                    id = execution.Id,
                    workflowId = execution.WorkflowId,
                    workflowName = execution.WorkflowName,
                    status = execution.Status.ToString(),
                    startTime = execution.StartTime,
                    endTime = execution.EndTime,
                    currentStepId = execution.CurrentStepId,
                    progress = execution.Progress,
                    errorMessage = execution.ErrorMessage,
                    logs = execution.Logs,
                    stepResults = execution.StepResults,
                    variables = execution.Variables,
                    triggeredBy = execution.TriggeredBy
                },
                message = "获取状态成功"
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIWorkflowController] 查询工作流执行状态失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = "查询执行状态失败", details = ex.Message });
        }
    }

    [HttpGet("tools/match")]
    public ActionResult<object> MatchTools([FromQuery] string taskDescription)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(taskDescription))
            {
                return BadRequest(new { success = false, error = "任务描述不能为空" });
            }

            XTrace.Log.Info("[AIWorkflowController] 工具匹配，任务: {0}", taskDescription);

            var rankedTools = _toolSelectorService.RankTools(taskDescription);

            var result = rankedTools.Select(t => new
            {
                toolName = t.Tool.Name,
                toolId = t.Tool.Id,
                pluginId = t.Tool.PluginId,
                description = t.Tool.Description,
                matchScore = t.MatchScore,
                matchedKeywords = t.MatchedKeywords
            }).ToList();

            return Ok(new { success = true, tools = result, total = result.Count });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIWorkflowController] 工具匹配失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = "工具匹配失败", details = ex.Message });
        }
    }
}

public class PlanWorkflowRequest
{
    public string UserRequest { get; set; } = string.Empty;
    public List<string>? AvailableTools { get; set; }
}

public class AIExecuteWorkflowRequest
{
    public long? WorkflowId { get; set; }
    public WorkflowDefinition? WorkflowDefinition { get; set; }
    public Dictionary<string, object?>? InputVariables { get; set; }
    public string? TriggeredBy { get; set; }
}
