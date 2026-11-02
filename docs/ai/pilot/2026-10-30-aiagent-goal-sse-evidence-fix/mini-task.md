---
risk:
  level: L1
task_kind: defect
feature: aiagent-goal-sse-evidence-fix
expected_files:
  - Plugins/AIAgent/Controllers/AgentRunsController.cs
  - Plugins/AIAgent/Services/RunOrchestratorService.cs
  - Plugins/AIAgent/MafLoop/GoalPursuitService.cs
  - ForgeSelf.Api.Tests/Plugins/AIAgent/GoalRunSseEvidenceTests.cs
rollback: 还原上述四个文件即可回到现状（goal 事件走 default 分支 + ToolCallsJson 恒 "[]"）
---

# 轻量任务：AIAgent 目标模式 SSE 事件映射 + 步骤工具轨迹证据链修复

> **[2026-10-30 21:2x 并入] 本任务已并入全量批次 `docs/ai/pilot/2026-10-30-aiagent-loop-defect-fix-batch/`（00-04 已产出）**：用户输入4 追加 4 个 UI 缺陷后总文件数超轻量裁剪线（≤3 文件），两处 P1（本文缺陷 A/B = 新批 D1/D2）与 UI 批合并走全量工件链，闸门1 一并请示。本文件保留作根因分析原始记录。

> 来源：2026-10-30 用户指令「测试 aiagent 插件能否独立完成一个任务」的实测发现（dev-stack :7301 实证，Run #2 与聊天会话 mvsag86i-x9gb4b 可复现）。
> 证据：`ForgeSelf.Web/screenshots/dev/ai-agent/goal-run-stuck-detail.png`、`loop-chat-turn-complete.png`、`.temp/dev-stack-backend.log` 19:36–19:42 段。

## Intent（为什么 / 做什么 / 到什么程度）

目标模式（PILOT-051 v1.9.7）的两个验收缺陷，实测均复现：

1. **缺陷 A（前端事件流崩溃，AC-UI2 不通过）**：`AgentRunsController.WriteSseEventAsync`（:309-335）的 payload 白名单只含旧五类事件（plan_created/step_started/step_completed/run_stuck/done），**新增的 `goal_round_started`/`goal_evaluated` 落入 default 分支**输出 `{type, content}`；前端 `http.ts:512` 读 `obj.payload` 得 undefined → `RunRecordPanel.vue:316` 的 `p.round` 抛 `TypeError: Cannot read properties of undefined (reading 'round')` → SSE 循环中断，事件行显示「连接失败」。实测：Run #2 启动后仅收到「已生成执行计划」即断。
2. **缺陷 B（工具轨迹断链 → 目标评估误报，终局误判 Stuck）**：`RunOrchestratorService.ExecuteStepsCoreAsync`（:656-709）的帧循环只把 `ToolStarted/ToolCompleted` 转发 SSE，**从不填充 `result.ToolCalls`** → `MarkStepCompleted:829` 序列化恒得 `"[]"`（实测 Run #2 九步全为 `[]`，而日志证明 write_file/read_file 真实执行成功）；且 `GoalPursuitService.EvaluateAsync` 的评估提示词只喂「任务原文 + 步骤产出摘要 + 判据」（该服务自述"无工具 LLM 调用"），拿不到工具轨迹 → 评估器三轮均以「缺工具执行证据」判未达成 → **文件实际已创建且内容正确，Run 却被置 Stuck**，纠正预算两轮空转。

完成程度：目标模式 Run 的 SSE 事件行能实时显示「目标评估 第 N/M 轮」与评估结论；真实调过工具的步骤 `toolCallsJson` 非空且被评估提示词引用；复测同型目标 Run 达成 → `Completed`（而非误报 Stuck）。

## Spec（行为定义）

- `goal_round_started`/`goal_evaluated` 与既有五类事件同构：SSE data 为 `{type, payload: <JSON字符串>}`；payload 结构不变（`AgentRunModels.cs:223/238` 已定义）。
- 步骤落库：`AgentStepRun.ToolCallsJson` = 本步真实工具调用轨迹数组（name/args/result/success），退出工具（complete_step/request_help）不入轨迹。
- 评估输入：每步附「产出摘要 + 工具调用轨迹摘要（工具名/成败/关键返回截断）」，评估器可据此核证据；提示词其余语义不变。
- 兼容：非目标模式 Run 行为不变；`run_stuck`/`done` 等既有事件字节面不变（守卫测试锁）。

## Plan（具体到真实文件）

1. `AgentRunsController.cs`：`WriteSseEventAsync` 的 switch 增加 `case "goal_round_started": case "goal_evaluated":` → `{type, payload = ev.Content}`（与 run_stuck 同形）。
2. `RunOrchestratorService.cs`：帧循环内聚合 `StepToolCallTrace`（ToolStarted 建条目、ToolCompleted 回填 result/success），步骤收束前写入 `result.ToolCalls`（`MarkStepCompleted`/`MarkStepStuck` 已有序列化，无需改）。
3. `GoalPursuitService.cs`：评估提示词的步骤清单附每步工具轨迹摘要（从步骤实体 ToolCallsJson 反序列化，截断防爆）。
4. 新增守卫测试 `GoalRunSseEvidenceTests`：① goal 两事件 SSE 映射断言（反射或直调 WriteSseEventAsync 私有静态→改 internal，或对事件流做集成断言，实现时定）；② 步骤执行后 `result.ToolCalls` 非空断言（脚本 agent 驱动）；③ 既有五类事件映射不回归。

## Task（Allowed / Forbidden + 验证命令）

**Allowed**：上述 4 个文件；`Plugins/AIAgent/web/src/components/RunRecordPanel.vue` 与 `web/src/http.ts` 的可选防御加固（`p?` 兜底，防止后端旧包再毒化前端）——若纳入则文件数超 3，按规范升全量工件或拆批，闸门1 时拍板。

**Forbidden**：不改评估判定语义（BR-4）；不改聊天链路（场景A已验证通过，不回归）；不改 plugin.json 版本外的清单字段；不发布、不停 `:51888`、不停 dev-stack 用户侧实例。

**验证（快档）**：
```
cd ForgeSelf.Api && dotnet build
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~AIAgent"
（若触及前端）cd Plugins/AIAgent/web && pnpm run build
dev-stack 复测：同型目标 Run（写文件+读回）→ 事件行出现「目标评估 第 1/3 轮」→ 达成 → Completed；步骤详情 toolCallsJson 非空
```
后置（闸门3 前，按 plugin-development §四）：`e2e/plugins/ai-agent` 冒烟 e2e（TODO 遗留项②，正好以「创建目标 Run → 事件可见」为主链路）→ dev 走查截图 → 本地发布（须用户同意）→ :51888 更新后运行实例复验。

## Interaction Design（无界面交互声明）

**无界面交互**：本批为宿主侧 SSE 封套映射与工具轨迹证据链修复（已并入 PILOT-077 作 D1/D2），无任何新增 UI/交互点；用户可见变化 = 既有 Run 详情事件行恢复对 goal_round_started/goal_evaluated 的正常渲染（修复前 TypeError 致「连接失败」断流），事件行文案与布局沿用既有组件，无新增按钮/输入/状态面。

## 代价与风险

- 改动集中在 ai-agent 插件内 3 个文件 + 1 测试，宿零触及；滚 =原文件。
- 风险：评估提示词加长可能放大 token 消耗（轨迹截断缓解）；本地小模型对证据的遵循度仍可能不足（属模型质量，不在本批硬修范围，纠正步骤「占位」退化单列 TODO 观察）。
