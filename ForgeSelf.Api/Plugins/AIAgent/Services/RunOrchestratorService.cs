using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Entities;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using NewLife.Data;
using NewLife.Log;
using XCode;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

public interface IRunOrchestratorService
{
    /// <summary>创建并驱动一次计划驱动执行（SSE 流）：建 Run → 规划 → 逐步执行 → 终局合成。</summary>
    IAsyncEnumerable<AgentLoopEvent> RunAsync(RunRequest request, CancellationToken ct);

    /// <summary>从当前步骤继续执行（重试语义，SSE 流）；仅 Status ∈ {Stuck, Failed} 允许。</summary>
    IAsyncEnumerable<AgentLoopEvent> ResumeAsync(long id, CancellationToken ct);

    /// <summary>以同 Plan 新建 Run 从头执行。</summary>
    Task<AgentRunDto?> RestartAsync(long id);

    /// <summary>取消（置 Cancelled；终态拒绝）。</summary>
    Task<AgentRunDto?> CancelAsync(long id);

    /// <summary>人工介入（skip/override）卡住步骤并推进。</summary>
    Task<AgentRunDto?> InterveneAsync(long id, int stepIndex, InterveneRequest request);

    /// <summary>Run 列表（分页，RetrieveTotalCount=true；status 为空=不过滤）。</summary>
    Task<AgentRunListResponse> ListRunsAsync(string? sessionId, AgentRunStatus? status, int page, int pageSize);

    /// <summary>Run 摘要 DTO。</summary>
    Task<AgentRunDto?> GetRunAsync(long id);

    /// <summary>Run 详情（含步骤列表）。</summary>
    Task<AgentRunDetailResponse?> GetRunDetailAsync(long id);
}

/// <summary>
/// 计划驱动执行引擎主控（tasks.md T015，design.md §5）：
/// RunAsync/ResumeAsync 共享 <see cref="ExecuteStepsCoreAsync"/> 步骤循环（每步一次
/// RunAgentLoopAsync，出口由 StepRunLoopService 判定）；InterveneAsync 映射
/// skip/override 到 StepRun 字段（design.md D6）；状态迁移统一走 <see cref="RunStateMachine"/>。
/// </summary>
public class RunOrchestratorService : IRunOrchestratorService
{
    private static readonly JsonSerializerOptions PlanJsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// SSE 结构化 payload 序列化选项（camelCase，对齐前端 http.ts 解析约定）：
    /// plan_created/step_started/step_completed/run_stuck 的内层 JSON 必须 camelCase，
    /// 否则前端拿到的 stepIndex/plan.steps 为 undefined（实测 NaN 步骤/目标不显示，029 联调踩坑）。
    /// </summary>
    private static readonly JsonSerializerOptions SsePayloadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IPlanGeneratorService _planGenerator;
    private readonly IStepRunLoopService _stepRunLoop;
    private readonly IAgentRegistryService? _agentRegistry;

    public RunOrchestratorService(
        IPlanGeneratorService planGenerator,
        IStepRunLoopService stepRunLoop,
        IAgentRegistryService? agentRegistry = null)
    {
        _planGenerator = planGenerator;
        _stepRunLoop = stepRunLoop;
        _agentRegistry = agentRegistry;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AgentLoopEvent> RunAsync(RunRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.TaskInput))
        {
            yield return new AgentLoopEvent { Type = "error", Content = "任务原文（taskInput）不能为空" };
            yield break;
        }

        // 1. 建 Run（Pending → Planning）。
        var run = CreateRun(request);
        run.Insert();
        XTrace.Log.Info("[RunOrchestrator] 新建执行 Run {0}（Agent={1}），进入规划", run.Id, run.AgentId);

        // 2. 规划：有/无工作流 → submit_plan 提交 Plan。
        AgentPlan plan = null!;
        string? planError = null;
        try
        {
            plan = await _planGenerator.GeneratePlanAsync(run, request.WorkflowId, request.ChatModelId, ct);
        }
        catch (Exception ex)
        {
            run.Status = (int)AgentRunStatus.Failed;
            run.Update();
            XTrace.Log.Error("[RunOrchestrator] 规划失败（Run {0}）: {1}", run.Id, ex);
            planError = $"规划失败: {ex.Message}";
        }
        if (planError != null)
        {
            yield return new AgentLoopEvent { Type = "error", Content = planError };
            yield break;
        }

        // 3. Plan 落库 + 事件（plan_created）。
        run.PlanJson = JsonSerializer.Serialize(plan);
        run.StepCount = plan.Steps.Count;
        run.Status = (int)AgentRunStatus.Running;
        run.Update();
        yield return new AgentLoopEvent
        {
            Type = "plan_created",
            Content = JsonSerializer.Serialize(new { plan = plan, planJson = run.PlanJson }, SsePayloadOptions)
        };

        // 4. 逐步执行（SSE 事件透传）。
        await foreach (var ev in ExecuteStepsCoreAsync(run, plan, request.ChatModelId, 0, ct))
            yield return ev;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AgentLoopEvent> ResumeAsync(long id, [EnumeratorCancellation] CancellationToken ct)
    {
        var run = AgentRun.FindById(id);
        if (run == null)
        {
            yield return new AgentLoopEvent { Type = "error", Content = $"执行记录 {id} 不存在" };
            yield break;
        }

        var from = (AgentRunStatus)run.Status;
        if (from is not (AgentRunStatus.Stuck or AgentRunStatus.Failed))
        {
            yield return new AgentLoopEvent { Type = "error", Content = $"当前状态 {from} 不可恢复（仅 Stuck/Failed 可 resume）" };
            yield break;
        }
        if (string.IsNullOrWhiteSpace(run.PlanJson))
        {
            yield return new AgentLoopEvent { Type = "error", Content = "无执行计划，无法恢复" };
            yield break;
        }

        AgentPlan? plan = null;
        string? parseError = null;
        try
        {
            plan = JsonSerializer.Deserialize<AgentPlan>(run.PlanJson, PlanJsonOptions);
        }
        catch (Exception ex)
        {
            parseError = $"执行计划解析失败: {ex.Message}";
        }
        if (parseError != null)
        {
            yield return new AgentLoopEvent { Type = "error", Content = parseError };
            yield break;
        }
        if (plan == null || plan.Steps.Count == 0)
        {
            yield return new AgentLoopEvent { Type = "error", Content = "执行计划为空，无法恢复" };
            yield break;
        }

        // 保留现场：从 CurrentStepIndex 重跑；前序 Completed 步骤产出由 StepRunLoopService 注入上下文（T011）。
        // 注：V1 不持久化 ChatModelId，resume 回退默认 provider（可后续加列）。
        run.Status = (int)AgentRunStatus.Running;
        run.Update();
        XTrace.Log.Info("[RunOrchestrator] 恢复 Run {0}（原状态 {1}），从步骤 {2} 继续", run.Id, from, run.CurrentStepIndex);

        await foreach (var ev in ExecuteStepsCoreAsync(run, plan, null, Math.Max(0, run.CurrentStepIndex), ct))
            yield return ev;
    }

    /// <inheritdoc />
    public Task<AgentRunDto?> RestartAsync(long id)
    {
        var run = AgentRun.FindById(id);
        if (run == null) return Task.FromResult<AgentRunDto?>(null);
        if (string.IsNullOrWhiteSpace(run.PlanJson))
            throw new InvalidOperationException("原执行记录无 Plan，无法重开");

        var newRun = new AgentRun
        {
            AgentId = run.AgentId,
            AgentName = run.AgentName,
            SessionId = run.SessionId,
            WorkflowId = run.WorkflowId,
            WorkflowName = run.WorkflowName,
            TaskInput = run.TaskInput,
            PlanJson = run.PlanJson,          // 同 Plan 复用（D2 不可变快照）
            Status = (int)AgentRunStatus.Pending,
            StepCount = run.StepCount,
            CurrentStepIndex = 0,
        };
        newRun.Insert();

        // 同步推进新 Run 到 Running（复用原 Plan，无需重新规划）。
        newRun.Status = (int)AgentRunStatus.Running;
        newRun.Update();

        XTrace.Log.Info("[RunOrchestrator] 重开 Run：{0} → {1}（同 Plan 复用）", run.Id, newRun.Id);
        return Task.FromResult<AgentRunDto?>(ToDto(newRun));
    }

    /// <inheritdoc />
    public Task<AgentRunDto?> CancelAsync(long id)
    {
        var run = AgentRun.FindById(id);
        if (run == null) return Task.FromResult<AgentRunDto?>(null);

        var from = (AgentRunStatus)run.Status;
        if (!RunStateMachine.CanTransitionRun(from, AgentRunStatus.Cancelled))
            throw new InvalidOperationException($"状态 {from} 不允许取消（终态不可取消）");

        run.Status = (int)AgentRunStatus.Cancelled;
        run.Update();
        XTrace.Log.Info("[RunOrchestrator] 取消 Run {0}", run.Id);
        return Task.FromResult<AgentRunDto?>(ToDto(run));
    }

    /// <inheritdoc />
    public Task<AgentRunDto?> InterveneAsync(long id, int stepIndex, InterveneRequest request)
    {
        var run = AgentRun.FindById(id);
        if (run == null) return Task.FromResult<AgentRunDto?>(null);

        var runStatus = (AgentRunStatus)run.Status;
        if (runStatus is AgentRunStatus.Completed or AgentRunStatus.Cancelled)
            throw new InvalidOperationException($"状态 {runStatus} 为终态，不可介入");

        var stepRun = AgentStepRun.FindAllByRunIdAndStepIndex(id, stepIndex).FirstOrDefault();
        if (stepRun == null)
            throw new InvalidOperationException($"步骤 {stepIndex} 不存在");

        var stepStatus = (AgentStepStatus)stepRun.Status;
        if (stepStatus is not (AgentStepStatus.Stuck or AgentStepStatus.Pending))
            throw new InvalidOperationException($"步骤状态 {stepStatus} 不可介入（仅 Stuck/Pending）");

        // D6：skip → Skipped；override → HumanOverride + Completed（跳过 LLM）。
        switch (request.Action)
        {
            case "skip":
                stepRun.Status = (int)AgentStepStatus.Skipped;
                stepRun.HumanNote = request.Note;
                stepRun.CompletedAt = DateTime.Now;
                break;

            case "override":
                stepRun.HumanOverride = request.Output;
                stepRun.HumanNote = request.Note;
                stepRun.Status = (int)AgentStepStatus.Completed;
                stepRun.OutputJson = request.Output;
                stepRun.CompletedAt = DateTime.Now;
                break;

            default:
                throw new InvalidOperationException($"介入动作 {request.Action} 非法（仅 skip/override）");
        }
        stepRun.Update();

        // 推进：卡住步骤已处理 → 指向下一步，Run 回 Running（resume 从新位置继续）。
        run.CurrentStepIndex = stepIndex + 1;
        run.Status = (int)AgentRunStatus.Running;
        run.Update();

        XTrace.Log.Info("[RunOrchestrator] 介入 Run {0} 步骤 {1}：{2}", run.Id, stepIndex, request.Action);
        return Task.FromResult<AgentRunDto?>(ToDto(run));
    }

    /// <inheritdoc />
    public Task<AgentRunListResponse> ListRunsAsync(string? sessionId, AgentRunStatus? status, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var exp = new WhereExpression();
        if (!string.IsNullOrWhiteSpace(sessionId))
            exp &= AgentRun._.SessionId == sessionId;
        if (status != null)
            exp &= AgentRun._.Status == (int)status.Value;

        var pageParam = new PageParameter
        {
            PageIndex = page - 1,
            PageSize = pageSize,
            Sort = AgentRun.__.CreateTime,
            Desc = true,
            RetrieveTotalCount = true // 项目硬约束：保证 total 正确
        };
        var list = AgentRun.FindAll(exp, pageParam);
        var total = (int)pageParam.TotalCount;

        return Task.FromResult(new AgentRunListResponse
        {
            Success = true,
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = list.Select(ToDto).ToList()
        });
    }

    /// <inheritdoc />
    public Task<AgentRunDto?> GetRunAsync(long id)
    {
        var run = AgentRun.FindById(id);
        return Task.FromResult<AgentRunDto?>(run == null ? null : ToDto(run));
    }

    /// <inheritdoc />
    public Task<AgentRunDetailResponse?> GetRunDetailAsync(long id)
    {
        var run = AgentRun.FindById(id);
        if (run == null) return Task.FromResult<AgentRunDetailResponse?>(null);

        var steps = AgentStepRun.FindAllByRunId(id)
            .OrderBy(s => s.StepIndex)
            .Select(ToStepDto)
            .ToList();

        return Task.FromResult<AgentRunDetailResponse?>(new AgentRunDetailResponse
        {
            Run = ToDto(run),
            Steps = steps
        });
    }

    #region 核心步骤循环（RunAsync / ResumeAsync 共享）

    /// <summary>
    /// 逐步执行核心：从 <paramref name="startIndex"/> 依次驱动每一步（design.md §5 伪码）：
    /// step_started →（透传 content/tool_call/tool_result/usage）→ complete_step（step_completed）/ request_help（run_stuck）。
    /// 卡住/失败即 yield break 保留现场；全部完成走终局合成 done。
    /// </summary>
    private async IAsyncEnumerable<AgentLoopEvent> ExecuteStepsCoreAsync(
        AgentRun run, AgentPlan plan, string? chatModelId, int startIndex,
        [EnumeratorCancellation] CancellationToken ct)
    {
        long totalTokens = run.TotalTokens; // resume 续接已累积 token
        for (var i = startIndex; i < plan.Steps.Count; i++)
        {
            var step = plan.Steps[i];
            run.CurrentStepIndex = i;
            run.Update();

            // 复用已存在的步骤记录（resume 现场：Stuck/Failed → Running，RetryCount+1）；否则新建。
            var stepRun = FindOrCreateStepRun(run, step, i);

            yield return new AgentLoopEvent
            {
                Type = "step_started",
                Content = JsonSerializer.Serialize(new StepStartedPayload
                {
                    RunId = run.Id, StepIndex = i, StepId = step.Id, Name = step.Name, Objective = step.Objective
                }, SsePayloadOptions)
            };

            // 人工补位已有产出（override 后续跑本步）→ 直接采纳，跳过 LLM。
            if (!string.IsNullOrWhiteSpace(stepRun.HumanOverride))
            {
                stepRun.Status = (int)AgentStepStatus.Completed;
                stepRun.CompletedAt = DateTime.Now;
                stepRun.Update();
                yield return new AgentLoopEvent
                {
                    Type = "step_completed",
                    Content = JsonSerializer.Serialize(new StepCompletedPayload { RunId = run.Id, StepIndex = i, Output = stepRun.HumanOverride })
                };
                continue;
            }

            // 步骤循环：复用 RunAgentLoopAsync（挂 complete_step/request_help + allowedTools 白名单）。
            var result = new StepRunResult();
            await foreach (var ev in _stepRunLoop.RunStepLoopAsync(run, plan, step, result, chatModelId, null, ct))
            {
                if (ev.Type == "complete_step" || ev.Type == "request_help")
                    break; // 出口事件：result 已携带结果，统一在循环外落库
                yield return ev; // 透传 content/tool_call/tool_result/usage
            }

            // 出口落库（complete_step / request_help / 迭代超限 / 异常）。
            if (result.Completed)
            {
                MarkStepCompleted(stepRun, result, step);
                yield return new AgentLoopEvent
                {
                    Type = "step_completed",
                    Content = JsonSerializer.Serialize(new StepCompletedPayload { RunId = run.Id, StepIndex = i, Output = result.Output }, SsePayloadOptions)
                };
            }
            else if (result.Stuck)
            {
                MarkStepStuck(stepRun, result);
                run.Status = (int)AgentRunStatus.Stuck;
                run.StuckReason = result.StuckReason;
                run.Update();
                XTrace.Log.Warn("[RunOrchestrator] Run {0} 步骤 {1} 卡住: {2}", run.Id, step.Id, result.StuckReason);
                yield return new AgentLoopEvent
                {
                    Type = "run_stuck",
                    Content = JsonSerializer.Serialize(new RunStuckPayload { RunId = run.Id, StepIndex = i, Reason = result.StuckReason }, SsePayloadOptions)
                };
                yield break; // 保留现场
            }
            else if (result.Failed)
            {
                stepRun.Status = (int)AgentStepStatus.Failed;
                stepRun.ErrorMessage = result.ErrorMessage;
                stepRun.Update();
                run.Status = (int)AgentRunStatus.Failed;
                run.StuckReason = result.ErrorMessage;
                run.Update();
                XTrace.Log.Error("[RunOrchestrator] Run {0} 步骤 {1} 失败: {2}", run.Id, step.Id, result.ErrorMessage);
                yield return new AgentLoopEvent { Type = "error", Content = $"步骤 {step.Id} 失败: {result.ErrorMessage}" };
                yield break;
            }

            totalTokens += result.TokensUsed;
            run.TotalTokens = totalTokens;
            run.Update();
        }

        // 终局合成：汇总各步 Completed 产出（V1 确定性聚合，不额外 LLM 调用）。
        var final = SynthesisAsync(run, plan);
        run.Status = (int)AgentRunStatus.Completed;
        run.Update();
        XTrace.Log.Info("[RunOrchestrator] Run {0} 完成，token 汇总 {1}", run.Id, run.TotalTokens);
        yield return new AgentLoopEvent { Type = "done", Content = final, Usage = null };
    }

    private static AgentStepRun FindOrCreateStepRun(AgentRun run, AgentPlanStep step, int index)
    {
        var existing = AgentStepRun.FindAllByRunIdAndStepIndex(run.Id, index).FirstOrDefault();
        if (existing != null)
        {
            // resume：卡住/失败步骤重跑。
            if ((AgentStepStatus)existing.Status is AgentStepStatus.Stuck or AgentStepStatus.Failed)
            {
                existing.Status = (int)AgentStepStatus.Running;
                existing.RetryCount += 1;
                existing.StartedAt = DateTime.Now;
                existing.Update();
            }
            return existing;
        }

        var stepRun = new AgentStepRun
        {
            RunId = run.Id,
            StepIndex = index,
            StepId = step.Id,
            Name = step.Name,
            Objective = step.Objective,
            Status = (int)AgentStepStatus.Running,
            StartedAt = DateTime.Now,
        };
        stepRun.Insert();
        return stepRun;
    }

    private static void MarkStepCompleted(AgentStepRun stepRun, StepRunResult result, AgentPlanStep step)
    {
        stepRun.Status = (int)AgentStepStatus.Completed;
        stepRun.OutputJson = result.Output;
        stepRun.InputJson = result.InputJson ?? stepRun.InputJson;
        stepRun.ToolCallsJson = JsonSerializer.Serialize(result.ToolCalls);
        stepRun.TokensUsed = result.TokensUsed;
        stepRun.DurationMs = Math.Max(0, (long)(DateTime.Now - stepRun.StartedAt).TotalMilliseconds);
        stepRun.CompletedAt = DateTime.Now;
        stepRun.Update();
    }

    private static void MarkStepStuck(AgentStepRun stepRun, StepRunResult result)
    {
        stepRun.Status = (int)AgentStepStatus.Stuck;
        stepRun.StuckReason = result.StuckReason;
        stepRun.InputJson = result.InputJson ?? stepRun.InputJson;
        stepRun.ToolCallsJson = JsonSerializer.Serialize(result.ToolCalls);
        stepRun.TokensUsed = result.TokensUsed;
        stepRun.Update();
    }

    /// <summary>终局合成：汇总各步 Completed 产出（V1 确定性拼接；LLM 润色留 V2）。</summary>
    private static string SynthesisAsync(AgentRun run, AgentPlan plan)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {plan.Goal}");
        sb.AppendLine();
        var completed = AgentStepRun.FindAllByRunId(run.Id)
            .Where(s => s.Status == (int)AgentStepStatus.Completed)
            .OrderBy(s => s.StepIndex);
        foreach (var s in completed)
        {
            sb.AppendLine($"## 步骤 {s.StepId}（{s.Name}）");
            sb.AppendLine(string.IsNullOrWhiteSpace(s.OutputJson) ? "（无产出）" : s.OutputJson);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    #endregion

    #region 实体/DTO 映射

    private AgentRun CreateRun(RunRequest request)
    {
        var agent = _agentRegistry?.GetAgent(request.AgentId);
        return new AgentRun
        {
            AgentId = request.AgentId,
            AgentName = agent?.Name ?? request.AgentId,
            SessionId = request.SessionId,
            TaskInput = request.TaskInput,
            Status = (int)AgentRunStatus.Planning,
        };
    }

    private static AgentRunDto ToDto(AgentRun run) => new()
    {
        Id = run.Id,
        AgentId = run.AgentId,
        AgentName = run.AgentName,
        SessionId = run.SessionId,
        WorkflowId = run.WorkflowId > 0 ? run.WorkflowId : null,
        WorkflowName = string.IsNullOrWhiteSpace(run.WorkflowName) ? null : run.WorkflowName,
        TaskInput = run.TaskInput,
        PlanJson = run.PlanJson,
        Status = (AgentRunStatus)run.Status,
        CurrentStepIndex = run.Status == (int)AgentRunStatus.Pending ? null : run.CurrentStepIndex,
        StuckReason = string.IsNullOrWhiteSpace(run.StuckReason) ? null : run.StuckReason,
        StepCount = run.StepCount,
        TotalTokens = run.TotalTokens,
        CreateTime = run.CreateTime,
        UpdateTime = run.UpdateTime,
    };

    private static AgentStepRunDto ToStepDto(AgentStepRun s) => new()
    {
        Id = s.Id,
        RunId = s.RunId,
        StepIndex = s.StepIndex,
        StepId = s.StepId,
        Name = s.Name,
        Objective = s.Objective,
        Status = (AgentStepStatus)s.Status,
        InputJson = s.InputJson,
        OutputJson = s.OutputJson,
        ToolCallsJson = s.ToolCallsJson,
        ErrorMessage = s.ErrorMessage,
        StuckReason = s.StuckReason,
        HumanNote = s.HumanNote,
        HumanOverride = s.HumanOverride,
        RetryCount = s.RetryCount,
        TokensUsed = s.TokensUsed,
        DurationMs = s.DurationMs,
        StartedAt = s.StartedAt,
        CompletedAt = s.CompletedAt,
    };

    #endregion
}
