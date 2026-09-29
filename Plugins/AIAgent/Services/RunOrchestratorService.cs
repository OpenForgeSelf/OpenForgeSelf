using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Entities;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Api.Plugins.AIAgent.Services.ToolFunctions;
using NewLife.Data;
using NewLife.Log;
using XCode;
using ForgeSelf.Core;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

/// <summary>
/// 单步执行结果（B7 自 StepRunLoopService 迁入：编排器出口容器，供落库与决策）。
/// </summary>
public class StepRunResult
{
    /// <summary>LLM 调 complete_step 完成。</summary>
    public bool Completed { get; set; }

    /// <summary>complete_step 声明的产出。</summary>
    public string? Output { get; set; }

    /// <summary>LLM 调 request_help 或回合收束未出口卡住。</summary>
    public bool Stuck { get; set; }

    /// <summary>卡住原因（request_help.reason 原文 / 挂起或收束说明）。</summary>
    public string? StuckReason { get; set; }

    /// <summary>运行异常失败。</summary>
    public bool Failed { get; set; }

    /// <summary>失败错误信息。</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>本步 token 用量汇总。</summary>
    public long TokensUsed { get; set; }

    /// <summary>本步工具调用轨迹（名/参数摘要/结果摘要/成败）。</summary>
    public List<StepToolCallTrace> ToolCalls { get; set; } = new();

    /// <summary>组装给 LLM 的上下文（InputJson 落库用）。</summary>
    public string? InputJson { get; set; }
}

/// <summary>单步工具调用轨迹项（ToolCallsJson 落库用）。</summary>
public class StepToolCallTrace
{
    public string Name { get; set; } = string.Empty;
    public string? ArgumentsSummary { get; set; }
    public string? ResultSummary { get; set; }
    public bool Success { get; set; } = true;
}

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
/// 计划驱动执行引擎主控（tasks.md T015，design.md §5；B7 薄壳化）：
/// RunAsync/ResumeAsync 共享 <see cref="ExecuteStepsCoreAsync"/> 步骤循环——每步骤经统一
/// ReactLoopAgent 状态机跑一个 turn（出口工具 complete_step/request_help 声明出口）；
/// InterveneAsync 映射 skip/override/steer（design.md D6 + B7 语义统一）；
/// 状态迁移统一走 <see cref="RunStateMachine"/>。
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
    private readonly IAIAgentService _aiAgentService;
    private readonly IAgentRegistryService? _agentRegistry;
    private readonly IContext? _ctx;
    private readonly RunFlowToolSet? _runFlowTools;
    private readonly IInbox? _inboxOverride;
    private readonly TimeSpan? _stepTimeout;

    /// <summary>宿主收件箱（B7 软依赖）：优先显式注入（测试），否则经 Cordis 上下文运行期懒解析。</summary>
    private IInbox? Inbox => _inboxOverride ?? _ctx?.Get<IInbox>();

    /// <summary>
    /// B7 切片 2 起的装配面：编排器薄壳化——步骤执行改由统一 ReactLoopAgent 状态机承担
    /// （每 029 步骤 = 一个 turn），本类只做「建 Run → 规划 → 逐步派发 → 出口判定 → 落库 → 终局合成」。
    /// </summary>
    /// <param name="planGenerator">规划阶段（scratch-session submit_plan 出口，B7 统一循环）。</param>
    /// <param name="aiAgentService">统一循环入口：CreateAgent 产出 ReactLoopAgent 状态机实例。</param>
    /// <param name="agentRegistry">Agent 人设注册表（可选，人格注入用）。</param>
    /// <param name="ctx">Cordis 上下文（可选；用于运行期懒解析宿主 <see cref="IInbox"/>——插件子容器不含宿主契约）。</param>
    /// <param name="runFlowTools">029 特殊工具集（complete_step/request_help schema 经 AgentOptions.ExtraTools 挂载）。</param>
    /// <param name="inbox">宿主收件箱显式注入（测试用；生产走 ctx 懒解析）。</param>
    /// <param name="stepTimeout">单步骤超时覆盖（默认 45s；测试可缩短）。</param>
    public RunOrchestratorService(
        IPlanGeneratorService planGenerator,
        IAIAgentService aiAgentService,
        IAgentRegistryService? agentRegistry = null,
        IContext? ctx = null,
        RunFlowToolSet? runFlowTools = null,
        IInbox? inbox = null,
        TimeSpan? stepTimeout = null)
    {
        _planGenerator = planGenerator;
        _aiAgentService = aiAgentService;
        _agentRegistry = agentRegistry;
        _ctx = ctx;
        _runFlowTools = runFlowTools;
        _inboxOverride = inbox;
        _stepTimeout = stepTimeout;
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
        var planningStopwatch = System.Diagnostics.Stopwatch.StartNew();   // B9-3：规划耗时元数据
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
        planningStopwatch.Stop();
        if (planError != null)
        {
            yield return new AgentLoopEvent { Type = "error", Content = planError };
            yield break;
        }

        // 3. Plan 落库 + 事件（plan_created，B9-3 附规划模型/耗时元数据）。
        run.PlanJson = JsonSerializer.Serialize(plan);
        run.StepCount = plan.Steps.Count;
        run.Status = (int)AgentRunStatus.Running;
        run.Update();
        yield return new AgentLoopEvent
        {
            Type = "plan_created",
            Content = JsonSerializer.Serialize(new
            {
                plan = plan,
                planJson = run.PlanJson,
                planningModel = request.ChatModelId,
                planningDurationMs = planningStopwatch.ElapsedMilliseconds
            }, SsePayloadOptions)
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

        // 保留现场：从 CurrentStepIndex 重跑；前序 Completed 步骤产出由步骤系统提示注入上下文（T011）。
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

        // ---- B7：steer 介入——输入走收件箱通道（统一循环的唯一输入入口），不改双表运行视图 ----
        // 运行中（Running）→ steer（NextStep，下一 step 边界生效）；已停（Stuck/Failed/Pending）→ followup（NextTurn，下一回合）。
        if (string.Equals(request.Action, "steer", StringComparison.OrdinalIgnoreCase))
        {
            var status = (AgentRunStatus)run.Status;
            if (status is AgentRunStatus.Completed or AgentRunStatus.Cancelled)
                throw new InvalidOperationException($"状态 {status} 为终态，不可介入");
            if (string.IsNullOrWhiteSpace(request.Output))
                throw new InvalidOperationException("steer 介入必须提供指令内容（Output）");
            if (string.IsNullOrWhiteSpace(run.SessionId))
                throw new InvalidOperationException("执行记录未关联会话（SessionId 为空），无法 steer");

            var inbox = Inbox ?? throw new InvalidOperationException("宿主收件箱不可用（IInbox 未接线）");
            if (status == AgentRunStatus.Running)
                inbox.Steer(run.SessionId, request.Output);
            else
                inbox.Followup(run.SessionId, request.Output);

            XTrace.Log.Info("[RunOrchestrator] 介入 Run {0}：steer 通道（状态 {1}）", id, status);
            return Task.FromResult<AgentRunDto?>(ToDto(run));
        }

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

            // ---- B7 薄壳化：步骤执行 = 统一 ReactLoopAgent 状态机的一个回合 ----
            // 步骤指令经收件箱 followup 唤醒新 turn（模型可见 = 已记录）；complete_step/request_help
            // 为出口工具（声明即出口，不真实执行）；turn 收束即步骤出口判定，双表照旧落库、SSE 面不变。
            var result = new StepRunResult();
            var sessionId = string.IsNullOrWhiteSpace(run.SessionId) ? $"run:{run.Id}" : run.SessionId!;
            string? exitTool = null;
            string? exitArgs = null;
            string? turnError = null;
            var turnReason = TurnEndReason.Completed;
            var callNames = new Dictionary<string, string>();

            var agent = _aiAgentService.CreateAgent(sessionId, BuildStepAgentOptions(run, plan, step, chatModelId));
            Inbox?.Followup(sessionId,
                $"任务原文：{run.TaskInput}\n\n请开始执行本步「{step.Name}」。完成或求助均通过对应工具提交。");

            await foreach (var frame in agent.RunAsync(ct))
            {
                switch (frame)
                {
                    case AssistantDelta delta:
                        if (string.IsNullOrEmpty(delta.Content)) break; // 空增量不产 SSE 噪音（与旧路径同面）
                        yield return new AgentLoopEvent { Type = "content", Content = delta.Content };
                        break;

                    case ToolStarted started:
                        callNames[started.CallId] = started.ToolName;
                        if (IsStepExitTool(started.ToolName)) break; // 出口工具不进 SSE（编排器统一转译）
                        yield return new AgentLoopEvent { Type = "tool_call", Name = started.ToolName, Arguments = started.ArgsJson };
                        break;

                    case ToolCompleted completed:
                    {
                        if (!callNames.TryGetValue(completed.CallId, out var doneName)
                            || IsStepExitTool(doneName)) break;
                        yield return new AgentLoopEvent
                        {
                            Type = "tool_result",
                            Name = doneName,
                            Success = completed.Outcome == ToolOutcome.Ok,
                        };
                        break;
                    }

                    case ExitToolInvoked exit:
                        exitTool = exit.ToolName;
                        exitArgs = exit.ArgsJson;
                        break;

                    case TurnFailed tf:
                        turnError = tf.Error;
                        break;

                    case TurnCompleted tc:
                        turnReason = tc.Reason;
                        break;
                }
            }

            // 出口判定 → 出口容器（落库与 SSE 转译与旧路径共用同一套代码）。
            if (turnError != null)
            {
                result.Failed = true;
                result.ErrorMessage = turnError;
            }
            else if (string.Equals(exitTool, RequestHelpToolName, StringComparison.OrdinalIgnoreCase))
            {
                result.Stuck = true;
                result.StuckReason = ParseStepReason(exitArgs);
            }
            else if (string.Equals(exitTool, CompleteStepToolName, StringComparison.OrdinalIgnoreCase))
            {
                result.Completed = true;
                result.Output = ParseStepOutput(exitArgs);
            }
            else
            {
                // 无出口声明：挂起（请求无进展超时）或回合收束未提交 complete_step → 卡住保留现场（T011）
                var timeout = (_stepTimeout ?? TimeSpan.FromSeconds(45)).TotalSeconds;
                result.Stuck = true;
                result.StuckReason = turnReason == TurnEndReason.Suspended
                    ? $"步骤执行超过 {timeout:0}s 挂起（请求无进展超时），已保留现场等待人工介入"
                    : "步骤回合未调用 complete_step 声明完成即收束，已暂停等待人工介入";
            }

            // 出口落库（complete_step / request_help / 挂起 / 异常）。
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

    #region 统一循环装配（B7 薄壳化：步骤 → 一个 turn）

    private const string CompleteStepToolName = "complete_step";
    private const string RequestHelpToolName = "request_help";

    /// <summary>是否步骤出口工具（声明即出口，不真实执行）。</summary>
    private bool IsStepExitTool(string toolName)
        => string.Equals(toolName, CompleteStepToolName, StringComparison.OrdinalIgnoreCase)
           || string.Equals(toolName, RequestHelpToolName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 每步骤一个 turn 的运行选项：步骤系统提示（人格 + Plan 全景 + 前序产出 + 本步目标）+
    /// 步骤工具白名单 + 出口工具语义 + 029 特殊工具 schema（经 ExtraTools 挂载，不随白名单过滤）。
    /// </summary>
    private AgentOptions BuildStepAgentOptions(AgentRun run, AgentPlan plan, AgentPlanStep step, string? chatModelId)
    {
        return new AgentOptions
        {
            ModelId = chatModelId ?? string.Empty,
            SystemPrompt = BuildStepSystemPrompt(run, plan, step),
            ToolAllowlist = step.AllowedTools,
            ExitToolNames = new[] { CompleteStepToolName, RequestHelpToolName },
            ExtraTools = _runFlowTools?.Stepping(),
            StepTimeout = _stepTimeout ?? TimeSpan.FromSeconds(45),
        };
    }

    /// <summary>步骤系统提示：Agent 人格 + Plan 全景 + 前序产出摘要 + 本步 objective/expectedOutput（平移自 StepRunLoopService）。</summary>
    private string BuildStepSystemPrompt(AgentRun run, AgentPlan plan, AgentPlanStep step)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是计划驱动执行引擎的步骤执行者。按照给定的执行计划逐步完成任务。");
        sb.AppendLine();

        // Agent 人格注入（与统一循环的 ResolveSystemPrompt 同源）。
        var agent = _agentRegistry?.GetAgent(run.AgentId);
        if (agent != null)
        {
            sb.AppendLine($"## 角色设定（Agent「{agent.Name}」）");
            if (!string.IsNullOrWhiteSpace(agent.Description))
                sb.AppendLine(agent.Description);
            if (!string.IsNullOrWhiteSpace(agent.SystemPrompt))
                sb.AppendLine(agent.SystemPrompt);
            sb.AppendLine();
        }

        // Plan 全景 + 当前位置标注。
        sb.AppendLine("## 执行计划全景");
        sb.AppendLine($"目标：{plan.Goal}");
        for (var i = 0; i < plan.Steps.Count; i++)
        {
            var p = plan.Steps[i];
            var marker = p.Id == step.Id ? " ← 当前步骤" : string.Empty;
            sb.AppendLine($"- [{i + 1}/{plan.Steps.Count}] {p.Name}：{p.Objective}{marker}");
        }
        sb.AppendLine();

        // 前序已完成步骤的产出摘要（resume 后仍进入上下文，T011/D5）。
        var priorOutputs = LoadPriorOutputs(run.Id, step.Id);
        if (priorOutputs.Count > 0)
        {
            sb.AppendLine("## 前序步骤产出（可引用，勿重复执行）");
            foreach (var (stepId, name, output) in priorOutputs)
            {
                sb.AppendLine($"### 步骤 {stepId}（{name}）产出");
                sb.AppendLine(TruncateStepText(output, 2000));
            }
            sb.AppendLine();
        }

        // 本步目标。
        sb.AppendLine("## 当前步骤");
        sb.AppendLine($"步骤名：{step.Name}");
        sb.AppendLine($"目标：{step.Objective}");
        if (!string.IsNullOrWhiteSpace(step.ExpectedOutput))
            sb.AppendLine($"期望产出：{step.ExpectedOutput}");
        if (step.AllowedTools is { Count: > 0 })
            sb.AppendLine($"本步允许工具：{string.Join(", ", step.AllowedTools)}");
        sb.AppendLine();

        sb.AppendLine("执行要求：");
        sb.AppendLine($"- 只完成当前步骤「{step.Name}」，不要提前执行后续步骤。");
        sb.AppendLine("- 完成后调用 complete_step 工具提交本步产出（output 字段）。");
        sb.AppendLine("- 无法推进时调用 request_help 工具说明卡住原因，等待人工介入。");
        sb.AppendLine("- 不要用自由文本声明完成，必须通过工具提交。");
        return sb.ToString();
    }

    /// <summary>读取前序已完成步骤的产出（StepRun.Status=Completed，按 StepIndex 升序；平移自 StepRunLoopService）。</summary>
    private static List<(string StepId, string Name, string Output)> LoadPriorOutputs(long runId, string currentStepId)
    {
        var result = new List<(string, string, string)>();
        try
        {
            var steps = AgentStepRun.FindAllByRunId(runId)
                .Where(s => s.Status == (int)AgentStepStatus.Completed && s.StepId != currentStepId)
                .OrderBy(s => s.StepIndex);
            foreach (var s in steps)
            {
                if (string.IsNullOrWhiteSpace(s.OutputJson)) continue;
                result.Add((s.StepId ?? string.Empty, s.Name ?? string.Empty, s.OutputJson));
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[RunOrchestrator] 读取前序产出失败: {0}", ex.Message);
        }
        return result;
    }

    /// <summary>complete_step 出口参数 → output（平移自 StepRunLoopService）。</summary>
    private static string? ParseStepOutput(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments)) return string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            return doc.RootElement.TryGetProperty("output", out var o) ? o.GetString() : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>request_help 出口参数 → reason（平移自 StepRunLoopService）。</summary>
    private static string? ParseStepReason(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments)) return "（未说明原因）";
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            return doc.RootElement.TryGetProperty("reason", out var r) && !string.IsNullOrWhiteSpace(r.GetString())
                ? r.GetString()
                : "（未说明原因）";
        }
        catch
        {
            return "（参数解析失败）";
        }
    }

    private static string TruncateStepText(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLength ? value : value[..maxLength] + "…";
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
