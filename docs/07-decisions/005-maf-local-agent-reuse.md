# ADR-005：本机 Agent 复用 MAF，不另起 LocalBot / 平行 Agent Runtime

- 状态：Accepted（2026-11-02）· **产品品牌口径由 [ADR-006](006-maf-first-class-greenfield.md) 升级**（本 ADR 的「不另起 Runtime / 复用 MAF」精神仍有效；「本批只写文档、不定一等品牌」的窄边界已被 006 取代）
- 关联：ADR-001（Cordis 内核）· ADR-002（万能工具 / 终端走 ToolRegistry）· ADR-006（MAF 一等公民 + 绿场）· PILOT-051（MAF 任务循环）· `docs/02-features/021-ai-agent.md`「执行链路现状快照」
- 背景Link：外部分析「本机 Agent Runtime / LocalBot」方案方向合理（Chat 入口、写/执行类审批、可恢复记录），但架构落点须改——本仓默认引擎已是 MAF，再造 Orchestrator / Planner / Run Manager / 平行状态机 = 重复实现。用户裁决：主路径已是 MAF，不要重复实现。原批只写文档；其后用户升级为 MAF 一等公民产品化（见 ADR-006）。

## 决策

1. **不另起 LocalBot / 平行 Agent Runtime**。默认引擎已是 `MafTaskLoopAgent`（`UseMafRuntime=true`）；工具经 `RegistryToolFunction` 进入现有六闸门 `ToolRegistry.ExecuteAsync`。产品方向可保留（入口不止 Chat、高风险操作要审批、留下可恢复记录），执行内核不得平行重建。
2. **MAF 已有、禁止重写**：
   - **任务循环**：`ChatClientAgent` + `FunctionInvokingChatClient`，外层 `task_complete` 续跑（PILOT-051）。计划驱动仍走现有 `RunOrchestrator` + `submit_plan` / `complete_step` / `request_help`，不并成第三套 loop。
   - **取消**：`RunAsync` 的 `CancellationToken` 已链到 `RegistryToolFunction` → `ToolExecution.Signal`。
   - **审批协议**（包已有、本仓未接）：`Microsoft.Extensions.AI` 10.5.1 — `ApprovalRequiredAIFunction` 不直接执行，发出 `ToolApprovalRequestContent`（含原始 `FunctionCallContent`）；用户回 `ToolApprovalResponseContent` 后，库按**原参数**执行或生成拒绝结果喂回模型。
   - **轨迹**：FreeLoop 用会话事件日志（`tool/call`、`tool/result`、turn/step），**不强制**再写一份 `AgentRun`。
3. **本仓还缺（后续单独 pilot，本 ADR 只记边界）**：
   - 高风险工具尚未包成 `ApprovalRequiredAIFunction`。
   - `MafTaskLoopAgent.cs` 约 595–607 行自定义 `FunctionInvoker` 直接 `InvokeAsync`；`PumpRunAsync` 未识别 `ToolApprovalRequestContent`。
   - `IApprovalService` 默认 `NoopApprovalService` 恒拒绝；批准对象必须是那条 `ToolApprovalRequestContent`（禁止批准后再让模型改参数）。
   - UI 复用 AIAgent Composer 已有审批卡（目前无后端生产者）。
   - **终端超时不杀进程**：`RunTerminalCommandTool` 超时后 process continues in background，自建 CTS，不吃调用方 token——MAF 取消传不到子进程的宿主缺口。
4. **明确不做**（详见 `not-taken-decisions.md` 050/051）：新建 Local Agent Runtime；新建 `specs/NNN-local-agent-runtime/`（`specs/` 已弃用）；Browser / Routines / Memory 隔离 / 本地模型适配后置且不在本批重做；`AgentHub.PermissionBroker` 只服务外部 CLI 委派，不搬进 MAF 循环。
5. **后续开工接审批**：单独开 `docs/ai/pilot/YYYY-MM-DD-maf-tool-approval/`，闸门1 后再改 `MafLoop` 与 `IApprovalService`。**本批只写文档。**

## 依据

- 仓库事实（2026-11-02 只读核查 + 执行链路快照）：`BuildAgentOptions` 默认 `TaskLoopMode=true` + `UseMafRuntime=true` → `MafTaskLoopAgent`；工具执行真源仍是宿主 `ToolRegistry`（与 ADR-002 一致）。
- 重复实现代价：平行 Orchestrator / 状态机 / 工具调度会与现有 FreeLoop + PlanDriven 双路径、六闸门、会话事件日志分叉，回归面与维护成本高于「接上 MAF 已有审批协议」。
- 用户裁决：主路径已是 MAF，不要重复实现；审批接线另立 pilot，本批文档定界。

## 后果

- 正面：产品目标（审批、可恢复记录）落在既有 MAF + ToolRegistry 上，避免第二套 Runtime；后续审批批有明确缺口清单与禁止项。
- 负面：审批协议在包内可用但本仓未接线——高风险工具今日仍可经 FunctionInvoker 直调；终端超时后子进程可能残留（宿主缺口，属后续批）。
- 边界：本 ADR 不定审批 UI/协议实现细节（留给 `maf-tool-approval` pilot）；不定 Browser / Routines / Memory 隔离 / 本地模型；不定 AgentHub PermissionBroker 语义扩展。
