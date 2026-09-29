using System.Text;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Entities;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Api.Plugins.AIAgent.Services.ToolFunctions;
using ForgeSelf.Core;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

public interface IPlanGeneratorService
{
    /// <summary>
    /// 规划阶段：组装规划上下文（Agent 人格 + 任务 + 工作流定义转草稿如有）→ 复用工具循环，
    /// 等 LLM 调 <c>submit_plan</c> 提交 Plan DSL；失败/未提交回退草稿或单步计划。
    /// </summary>
    Task<AgentPlan> GeneratePlanAsync(AgentRun run, long workflowId, string? chatModelId, CancellationToken ct);
}

/// <summary>
/// 计划驱动执行的规划阶段（tasks.md T013，design.md §5 PlanPhaseAsync）：
/// 单次 RunAgentLoopAsync 调用内让 LLM 用 submit_plan 工具提交结构化 Plan；绝不自由文本解析。
/// 有工作流时把工作流定义转步骤草稿注入提示词（LLM 在其基础上补全），LLM 无响应时回退草稿。
/// </summary>
public class PlanGeneratorService : IPlanGeneratorService
{
    private const string SubmitPlanToolName = "submit_plan";

    /// <summary>规划请求级无进展超时（秒）：上游不可达/冷启动阻塞时及时收束走回退（029 联调踩坑：无限规划中）。</summary>
    private const int PlanningTimeoutSeconds = 20;

    /// <summary>规划回合步数上限：正常一步出 submit_plan；给 2 步容忍一次工具迭代，杜绝无限规划。</summary>
    private const int PlanningMaxSteps = 2;

    private readonly IAIAgentService _aiAgentService;
    private readonly RunFlowToolSet _tools;
    private readonly IAgentRegistryService? _agentRegistry;
    private readonly IWorkflowService? _workflowService;
    private readonly IContext? _ctx;
    private readonly IInbox? _inboxOverride;

    /// <summary>宿主收件箱（B7 软依赖）：优先显式注入（测试），否则经 Cordis 上下文运行期懒解析。</summary>
    private IInbox? Inbox => _inboxOverride ?? _ctx?.Get<IInbox>();

    public PlanGeneratorService(
        IAIAgentService aiAgentService,
        RunFlowToolSet tools,
        IAgentRegistryService? agentRegistry = null,
        IWorkflowService? workflowService = null,
        IContext? ctx = null,
        IInbox? inbox = null)
    {
        _aiAgentService = aiAgentService;
        _tools = tools;
        _agentRegistry = agentRegistry;
        _workflowService = workflowService;
        _ctx = ctx;
        _inboxOverride = inbox;
    }

    /// <inheritdoc />
    public async Task<AgentPlan> GeneratePlanAsync(AgentRun run, long workflowId, string? chatModelId, CancellationToken ct)
    {
        // 1. 有工作流：取定义转草稿（Plan 骨架），供 LLM 补全；取不到则回退无草稿。
        AgentPlan? draft = null;
        if (workflowId > 0 && _workflowService != null)
        {
            try
            {
                var wf = await _workflowService.GetWorkflowAsync(workflowId);
                if (wf != null)
                {
                    draft = BuildDraftFromWorkflow(wf);
                    XTrace.Log.Info("[PlanGenerator] 工作流 {0}（{1}）转 Plan 草稿 {2} 步",
                        wf.Id, wf.Name, draft.Steps.Count);
                }
            }
            catch (Exception ex)
            {
                XTrace.Log.Warn("[PlanGenerator] 读取工作流 {0} 失败，回退无草稿规划: {1}", workflowId, ex.Message);
            }
        }

        // 2. 跑一次规划循环，等 submit_plan 提交 Plan。
        var plan = await TryPlanByLLMAsync(run, draft, chatModelId, ct);

        // 3. 回退：LLM 未提交 → 草稿（如有）→ 单步计划。
        if (plan != null) return plan;
        if (draft != null) return draft;
        return BuildFallbackPlan(run);
    }

    /// <summary>
    /// 单次规划回合（B7 统一循环）：scratch-session（<c>plan:{runId}</c>）上一个 turn，
    /// 任务原文经收件箱 followup 唤醒，submit_plan 为出口工具（声明即出口）——
    /// 规划事实与一切持久事实一样走会话日志（不再有无日志的 ad-hoc 旁路）；
    /// 未提交/无进展超时/异常一律返回 null 走回退（草稿/单步计划）。
    /// </summary>
    private async Task<AgentPlan?> TryPlanByLLMAsync(AgentRun run, AgentPlan? draft, string? chatModelId, CancellationToken ct)
    {
        var sessionId = $"plan:{run.Id}";

        // 规划只挂 submit_plan（ExtraTools 不随白名单过滤；空白名单屏蔽宿主全部注册工具）。
        var options = new AgentOptions
        {
            ModelId = chatModelId ?? string.Empty,
            SystemPrompt = BuildPlanningSystemPrompt(run, draft),
            ToolAllowlist = new List<string>(),
            ExitToolNames = new[] { SubmitPlanToolName },
            ExtraTools = _tools.Planning(),
            StepTimeout = TimeSpan.FromSeconds(PlanningTimeoutSeconds),
            MaxStepsPerTurn = PlanningMaxSteps,
        };

        try
        {
            var agent = _aiAgentService.CreateAgent(sessionId, options);
            Inbox?.Followup(sessionId, run.TaskInput);

            // B7 终验修复（QA 实锋试中危缺陷）：收到出口后<b>不得提前 return</b>——
            // 提前释放迭代器会跳过状态机其后的 step/end、ConfirmIfCommitted（R2 确认消费）
            // 与 turn/end，导致规划 turn 不收束、scratch 会话收件箱输入永不确认。
            // 正确姿势：循环内只记录，继续消费帧至自然结束（状态机收口），循环外统一返回。
            AgentPlan? submitted = null;
            await foreach (var frame in agent.RunAsync(ct))
            {
                if (frame is ExitToolInvoked exit
                    && string.Equals(exit.ToolName, SubmitPlanToolName, StringComparison.OrdinalIgnoreCase)
                    && submitted == null)
                {
                    submitted = TryParsePlan(exit.ArgsJson);
                    if (submitted != null)
                    {
                        XTrace.Log.Info("[PlanGenerator] LLM 提交 Plan：goal={0}, steps={1}",
                            submitted.Goal, submitted.Steps.Count);
                    }
                }
            }

            if (submitted != null)
            {
                return submitted;
            }

            // 回合收束未出口（无进展超时挂起 / 未调用 submit_plan）→ 回退。
            XTrace.Log.Warn("[PlanGenerator] 规划回合未收到 submit_plan，走回退");
            return null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // 规划超时（非用户取消）：记日志走回退，不回滚整 Run。
            XTrace.Log.Warn("[PlanGenerator] 规划回合 {0}s 超时未收到 submit_plan，走回退", PlanningTimeoutSeconds);
            return null;
        }
        catch (Exception ex)
        {
            // 规划调用异常（如上游 provider 500）不回滚整 Run：记日志后走回退（草稿/单步计划），
            // 与「LLM 未提交 submit_plan → 回退」同路径（design.md §5 回退语义）。
            XTrace.Log.Warn("[PlanGenerator] 规划回合异常，走回退: {0}", ex.Message);
            return null;
        }
    }

    /// <summary>从 submit_plan 工具参数 JSON 解析 Plan DSL（容错 JSON 包裹/转义）。</summary>
    private static AgentPlan? TryParsePlan(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments)) return null;
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            if (!doc.RootElement.TryGetProperty("plan", out var plan) || plan.ValueKind != JsonValueKind.Object)
                return null;
            var agentPlan = plan.Deserialize<AgentPlan>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (agentPlan == null || agentPlan.Steps.Count == 0) return null;
            // 步骤 id 缺失时按序号补齐，保证 StepRun 索引可对。
            for (var i = 0; i < agentPlan.Steps.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(agentPlan.Steps[i].Id))
                    agentPlan.Steps[i].Id = $"s{i + 1}";
            }
            return agentPlan;
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[PlanGenerator] submit_plan 参数解析失败: {0}", ex.Message);
            return null;
        }
    }

    /// <summary>工作流定义 → Plan 草稿（步骤平铺，children 递归展开；allowedTools 按工具名挂）。</summary>
    private static AgentPlan BuildDraftFromWorkflow(WorkflowDefinitionDto wf)
    {
        var plan = new AgentPlan
        {
            Goal = wf.Description ?? wf.Name,
        };

        var index = 0;
        void AppendSteps(IReadOnlyList<WorkflowStep> steps)
        {
            foreach (var s in steps)
            {
                index++;
                var tools = new List<string>();
                if (!string.IsNullOrWhiteSpace(s.ToolName)) tools.Add(s.ToolName);
                plan.Steps.Add(new AgentPlanStep
                {
                    Id = $"s{index}",
                    Name = s.Name,
                    Objective = string.IsNullOrWhiteSpace(s.Description) ? s.Name : s.Description!,
                    ExpectedOutput = $"完成「{s.Name}」",
                    AllowedTools = tools.Count > 0 ? tools : null,
                    Mandatory = true
                });
                if (s.ChildrenSteps is { Count: > 0 })
                    AppendSteps(s.ChildrenSteps);
            }
        }
        AppendSteps(wf.Steps);

        if (plan.Steps.Count == 0)
            plan.Steps.Add(new AgentPlanStep { Id = "s1", Name = wf.Name, Objective = wf.Name, ExpectedOutput = "完成工作流目标" });
        return plan;
    }

    /// <summary>LLM 未提交 Plan 且无草稿时的兜底：单步计划（全工具）。</summary>
    private static AgentPlan BuildFallbackPlan(AgentRun run)
    {
        return new AgentPlan
        {
            Goal = run.TaskInput,
            Steps =
            {
                new AgentPlanStep
                {
                    Id = "s1",
                    Name = "完成整体任务",
                    Objective = run.TaskInput,
                    ExpectedOutput = "任务的完整交付物",
                }
            }
        };
    }

    /// <summary>规划系统提示词：Agent 人格 + 任务 + 工作流草稿（如有）+ 提交指令。</summary>
    private string BuildPlanningSystemPrompt(AgentRun run, AgentPlan? draft)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是计划驱动执行引擎的规划器。你的任务：把用户任务拆解为可逐步执行的执行计划。");
        sb.AppendLine();
        sb.AppendLine("要求：");
        sb.AppendLine("- 用 submit_plan 工具提交计划（goal + steps[]），不要输出自由文本 JSON。");
        sb.AppendLine("- steps 每步包含 id/name/objective（给执行 LLM 的指令）/expectedOutput/allowedTools（空=全部工具）。");
        sb.AppendLine("- 步骤 2-6 步为宜，顺序执行，粒度适中（每步可独立验证）。");
        sb.AppendLine("- 需要工具的分步列出 allowedTools（如 read_file/write_file/list_files/calculate 等）。");
        sb.AppendLine();

        // Agent 人格注入（与 RunAgentLoopAsync ResolveSystemPrompt 同源）。
        var agent = _agentRegistry?.GetAgent(run.AgentId);
        if (agent != null)
        {
            sb.AppendLine($"## 规划视角（Agent「{agent.Name}」）");
            if (!string.IsNullOrWhiteSpace(agent.Description))
                sb.AppendLine(agent.Description);
            if (!string.IsNullOrWhiteSpace(agent.SystemPrompt))
                sb.AppendLine(agent.SystemPrompt);
            sb.AppendLine();
        }

        // 工作流草稿注入：LLM 在其基础上补全/调整，不要求必须照抄。
        if (draft is { Steps.Count: > 0 })
        {
            sb.AppendLine("## 工作流草稿（已有步骤骨架，可在此基础补全/调整）");
            foreach (var s in draft.Steps)
            {
                sb.AppendLine($"- {s.Id} {s.Name}：{s.Objective}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("现在为用户任务生成计划并调用 submit_plan 提交。");
        return sb.ToString();
    }
}
