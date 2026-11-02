# Specification — B2 结果硬化（filesChanged / Verify 闸门 / 结果收据）

> Task ID：`2026-11-02-maf-runtime-hardening/01-b2-fileschanged-verify`｜Stage 2｜推导自 00/01

## Functional Requirements

### FR-1 filesChanged 落库修复（B2①）

- FR-1.1 `TaskExecutionService.AppendSystemAsync` 增加 `filesChanged`（`IReadOnlyList<ChangedFileDto>?`，缺省 null）入参；非空时经 `FileChangeList.ToStorage` 落实体 `FilesChanged`（与 `AppendAsync` 同一归一真源，不另立写法）。
- FR-1.2 `TodoDispatchService.AppendAdvanceRecordAsync` 把 `status.FilesChanged` 传入留痕——自动回写记录的结构化字段与 `AgentStatusDto` 一致。
- FR-1.3 `BuiltInAgentExecutionService` 快照文件提取改为**结构化解析优先**：按 `List<StepToolCallTrace>` 反序列化 `ToolCallsJson`（复用编排器同一类型），对每条 trace 的 `ArgumentsSummary` 解析 JSON 取 `path`/`file_path`（仅写类工具 `write_file`/`create_file`/`patch`），解析失败再回退正则；正则保持现有能力（裸 JSON 参数文本）。
- FR-1.4 问题类闭环（`problem-class-closure`）：复现红灯 → 同类扫查（「结构化字段序列化后落库/读回丢失」签名）→ 守卫（真库回读守卫测试）→ case + PC 归档 `docs/11-troubleshooting/`。

### FR-2 Run 级 Verify 闸门（B2②）

- FR-2.1 `RunRequest` 增加 `ExitCriteria`（`List<RunExitCriterion>?`：`Text` 验收标准文本 + `Type` 断言类型）；随 PlanJson 持久化（与 SuccessCriteria 同机制，不改 DB 结构承载计划面）。
- FR-2.2 `AgentRun` 增列：`ExitCriteria`（String -1，标准 JSON 快照）、`VerifyStatus`（Int32：0=None 未配置 / 1=Pending / 2=Passed / 3=Failed）、`VerifyReport`（String -1，Verify 结果 JSON：每条判据 id/text/type/passed/detail）、`ResultReceipt`（String -1，结构化收据 JSON）。`AgentStepRun` 增列 `EvidenceJson`（String -1，本步证据：文件清单/工具轨迹摘要）。全部经 `Data/Model.xml` + `xcode` 生成，禁手改生成件。
- FR-2.3 计划任务 Run：全部步骤完成（或目标模式达成收束）后、置 Completed 前，若配置了 ExitCriteria → 进入 **Verify 阶段**：逐条执行确定性校验并把结论+证据落 `VerifyReport`/`EvidenceJson`/`AgentRun.VerifyStatus`；任何一条未过 → Run 置 Stuck（理由带未过明细），VerifyStatus=Failed，不假 Completed。
- FR-2.4 断言类型 V1（确定性，零 LLM）：
  - `file_exists`：工作区内文件存在（经 `IProjectWorkspaceService` 解析相对路径；绝对路径按 FilePathGuard 同口径拒绝越界）。
  - `output_contains`：终局合成文本/末步产出包含指定文本。
  - `files_changed_not_empty`：本次 Run 证据链中文件清单非空（防「说做了但零产物」）。
  - 未知类型：按失败处理并写明原因（不静默放行）。
- FR-2.5 SessionEvent 轨迹：新增 `RunVerifyEvent`（`run/verify`，载荷=RunId/Status/逐条结论摘要）追加进 Run 所在会话日志（计划任务 runId 即 sessionId 兜底 `run:{id}` 口径）；注册进 `SessionEventMap`；`DeriveMessages` 不入模型历史（与 approval-request 同类，纯轨迹事件）。
- FR-2.6 开放任务 Run（MafTaskLoopAgent）：`AgentOptions` 增加 `ExitCriteria`（同为 `RunExitCriterion` 列表）；回合收束（task_complete / turn end）后执行同一确定性校验器，结果以 `run/verify` SessionEvent 落会话日志。校验器经 `IContext` 懒解析（`RunVerifyService` 挂插件上下文，与 GoalPursuit 同款注入形态）。
- FR-2.7 校验器实现为独立类 `RunVerifyService`（Plugins/AIAgent/MafLoop/），编排器与 MafTaskLoopAgent 共用；命令执行类断言（测试类校验）本批**不开放**（见 Boundary）。

### FR-3 Run 结果收据（B2③）

- FR-3.1 收据模型 `RunResultReceipt`（Abstractions 或插件 Models，camelCase JSON 落库）：`RunId` / `Status` / `Summary`（终局合成首段或 result 摘要）/ `FilesChanged[]`（path+change，从步骤轨迹聚合）/ `Verifications[]`（ExitCriteria 结论；未配置则空数组）/ `Evidence[]`（文件清单/轨迹引用/工具成败计数）/ `FrozenAt`。
- FR-3.2 计划任务：Run 置 Completed 同时生成收据写 `AgentRun.ResultReceipt`；**冻结** = 终态写入一次，后续写路径（restart/cancel/verify 重跑）不再覆盖既有收据（新 Run 新收据）。
- FR-3.3 开放任务：MafTaskLoopAgent 出口处（`ExitToolInvoked`/turn 收束）从会话日志本回合 tool/call|tool/result 聚合收据，以新增 `RunReceiptEvent`（`run/receipt`）冻结进 SessionEvent 日志（append-only 天然不可变）。
- FR-3.4 `AgentRunDto` / `AgentRunDetailResponse` 透出 `verifyStatus` / `verifyReport` / `resultReceipt`；`RunRecordPanel.vue` 详情头部下新增「结果收据」区块：Verify 徽标 + 判据清单（✓/✗ + 文本）+ 文件清单 chips + 证据行；收据为空/未配置时区块不渲染（不摆空占位）。

## Input

- B2①：既有库内 TaskExecution 行 + `AgentStatusDto.FilesChanged` + `AgentStepRun.ToolCallsJson`（现网 todo 56 形态）。
- B2②：`RunRequest.ExitCriteria`（REST `POST /api/ai-agent/runs` 新字段，camelCase 可空）；开放任务经 `AgentOptions.ExitCriteria`。
- B2③：Run 步骤实体（OutputJson/ToolCallsJson/EvidenceJson）+ VerifyReport。

## Output

- 落库：`TaskExecution.FilesChanged`（非空 JSON 数组）、`AgentRun.{ExitCriteria,VerifyStatus,VerifyReport,ResultReceipt}`、`AgentStepRun.EvidenceJson`。
- 事件：`run/verify`、`run/receipt` 两类 SessionEvent。
- DTO/SSE：AgentRunDto 三字段；RunRecordPanel 收据区块。
- 测试：`BuiltInFilesChangedPersistenceTests`（TodoTracker 真库）、`RunVerifyGateTests`（编排器真库+SessionEvent）、`MafLoopReceiptTests`（MafFixture）+ schema/守卫测试。

## Business Rules

- BR-1 `AppendSystemAsync` 是系统留痕通用口：新增 filesChanged 为**可选参数**，既有全部调用点行为不变（不传=空，向后兼容）。
- BR-2 配了 ExitCriteria 的 Run **不允许无验证就 Completed**——Verify 全过才置 Completed；Verify 异常（服务缺席/断言类型未知）按未过处理并写明原因，禁止静默放行（与 GoalPursuit fail-safe 同哲学）。
- BR-3 未配置 ExitCriteria 的 Run 行为完全不变（VerifyStatus=0 None，不产 run/verify 事件，收据照常生成）。
- BR-4 收据与 Verify 报告是**事实投影**：只从已落库步骤实体/会话日志聚合，不重新执行工具、不写文件系统。
- BR-5 SessionEvent 新类型必须三步走（record + Map 注册 + DeriveMessages case），且**不入模型历史**。
- BR-6 文件路径校验复用 `IProjectWorkspaceService.TrySetProjectRoot` 同款解析与 `FilePathGuard` 越界防线，不在校验器内自造路径逻辑。

## Boundary Conditions

- ExitCriteria 配了但 Run 中途 Stuck/Failed/Cancelled：不进 Verify（VerifyStatus 保持/置 None 不误报），收据照常冻结（Status 如实）。
- 断言 `command`（测试类校验=执行命令）**本批不做**：执行命令必须走 ToolRegistry 六闸门（架构 §3.3），而出口外断言执行命令会绕开审批面；留 V2 经 `IToolRegistry.ExecuteAsync` 接线（07 重构提案节登记）。
- AgentRun 新列对**存量行**：XCode Migration 自动补列，旧行 VerifyStatus 默认 0、收据空 → UI 不渲染区块（兼容零成本）。
- `AgentRunDto` JSON 新字段全部可空/缺省——B3 任务中心与既有前端旧版本反序列化不受影响。

## Error Handling

- xcode 生成失败：报告 lead，不手改生成件（铁律 9）。
- Verify 单条断言异常：该条 passed=false + detail=异常原文，整体 Failed → Stuck；不抛出中断收据生成。
- 收据 JSON 序列化失败（理论不可达）：落空串 + 日志告警，UI 区块不渲染。
- DLL 锁（dev-stack 持有 bin）：不硬闯——构建失败带 MSB3027 即报告 lead（按战役批约束）。

## Compatibility

- 插件 web：`RunRecordPanel.vue` 只增区块不改既有列/状态机；`types.ts` 增可选字段。构建 `pnpm run build` 过门禁。
- REST：`RunRequest` 新字段可空 → 既有调用（todo 委派、内置引擎、e2e）零改动照常工作。
- SessionEvent：新类型只增不改，`UnknownSessionEventException` 防线靠注册消除。

## Non-functional Requirements

- 收据/Verify 落库均为**单次**写（不逐轮重写）；Verify 校验器纯内存+文件系统读，无网络调用，超时继承步骤循环语义。
- 测试全部走 `[Collection("XCode")]` 真库守卫模式（MemoryTimestampGuardTests 模式），禁止 mock 业务数据。

## Interaction Design（交互设计 · 必写节）

> 涉 UI 面：`RunRecordPanel.vue`（智能体插件「执行记录」弹窗）新增收据区块。其余为后端/数据面 = 无新交互。

### 交互规格（点什么出现什么）

| 交互点 | 触发 | 结果（点什么出现什么） | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| 结果收据区块 | 打开执行记录弹窗并选中一个 Run | 详情头部（状态徽标行）下方出现「结果收据」卡片：Verify 徽标（通过/未通过/未配置）+ 判据清单（✓/✗ + 标准文本 + 未过原因）+ 「涉及文件」chips 列表 + 证据行（工具调用 N 次·成功 M 次） | run.verifyStatus ∈ {未配置,待验证,通过,未通过} × 收据有无 | 静态展示，无操作 | Run 无收据（旧数据/未配置）→ **整个区块不渲染**（不摆空占位） | 判据 >5 条折叠为「展开全部」；长路径 chips 截断中段 + title 全文 |
| 文件 chip | hover | title 显示完整路径 | - | 无点击行为（V1 不做跳转） | 无文件 → 「涉及文件」行不渲染 | 路径去重（同路径只出现一次） |

### 视觉规格（目标态）

- **对齐线与几何**：区块容器 = 详情列内容同宽（`width:100%`，与 `.rrp__detail-head` 左缘对齐）；标题行 13px 半粗（`var(--el-font-size-base, 13px)` 上浮），条目行 12px（`--el-font-size-extra-small`）；块间距 12px，行距 6px（既有 `.rrp__label`/`.rrp__pre` 节奏）。
- **主题矩阵**：卡片 bg = 无独立底色（跟随详情列，避免再开一层灰面）；Verify 徽标 = 通过 `--el-color-success`、未通过 `--el-color-danger`、未配置 `--el-text-color-secondary` 灰字（不用徽标底）；文件 chip bg = `--el-fill-color-light`、字 = `--el-text-color-regular`；✓/✗ 前缀 = success/danger 色圆点。light/dark 均走 `--el-*` 变量，无裸色值（design:check 红线）。
- **状态 × 主题**：两种主题下同一套 token，无差异化处理（Element Plus 变量自带双主题）。
- **来源教义**：DESIGN.md「新建页面与查询执行规范」— 复用既有卡片节奏，不新增裸色/胶囊；与既有 `.rrp__badge`（状态徽标）同形制。

### 交互验收标准（并入 AC）

- AC-UI-1 选中带收据的 Run → 收据区块可见且含 Verify 徽标 + 文件 chips；选中无收据 Run → 区块不出现。
- AC-UI-2 Verify 未通过时判据行逐条显示 ✓/✗ 与未过原因，颜色随结论。
- AC-UI-3 插件前端 `pnpm run build` 过（类型零错）；`design:check`（宿主侧跑）不新增违规。

### 走查符合性（DoD 绑定）

- 快档门禁含插件 build；UI 走查以 dev-stack 起源码前后端对 RunRecordPanel 截图读图核对（布局/对比度/token），证据进 05-evidence（受 dev-stack 占用约束时按战役批约定与 lead 协调窗口）。

## Acceptance Criteria

- AC-1（红→绿）真库测试：构造「步骤轨迹含 write_file 路径」的 Run → `BuiltInAgentExecutionService.FindAsync` 快照 `FilesChanged` 非空（FR-1.3）；走 auto-advance 留痕 → `TaskExecution` 回读 `FilesChanged` 非空（FR-1.1/1.2）。修复前跑必须红（05-evidence 留红灯原文）。
- AC-2 守卫：`AppendSystemAsync` 带 filesChanged 落库回读非空；不带时为空且既有调用零变化。
- AC-3（Verify 闸门）真库测试：配 `ExitCriteria`（file_exists 命中/未命中两组）的 Run 跑完步骤 → Completed 的 Run `VerifyStatus=2` 且 `VerifyReport` 逐条 passed；未命中 → Run=Stuck + `VerifyStatus=3` + 未过明细；会话日志出现 `run/verify` 事件且类型已注册。
- AC-4（断言类型）`output_contains` 命中/未命中、`files_changed_not_empty` 空/非空、未知类型=失败，行为符合 BR-2。
- AC-5（收据）Run Completed 后 `ResultReceipt` 非空且含 filesChanged/verifications/evidence/frozenAt；再触发 cancel/restart 不改写既有收据（冻结）；开放任务回合结束出现 `run/receipt` 事件（MafFixture）。
- AC-6（兼容）无 ExitCriteria 的 Run：行为与现状一致（Status 流转、事件序列、无 run/verify 事件；收据仍生成）。
- AC-7（UI）AC-UI-1/2/3 + `pnpm run build` 绿。
- AC-8 问题类闭环：case 落 `docs/11-troubleshooting/cases/`、PC 建/更新、`scripts/check-problem-classes.ps1` 绿、07 含三节。

## Unknown

| 不确定点 | 影响 | 处理方式（询问/搁置/保守假设并标注） |
| --- | --- | --- |
| `xcode` 重生成后与既有 `.Biz.cs` 分部类是否零冲突 | 实体编译 | 生成后 git diff 复核（Biz 文件不应出现在 diff）；异常即报 lead |
| 走查窗口与 dev-stack 占用冲突 | UI 走查取证 | 与 lead 协调窗口；先出代码+构建证据，走查按窗口补 |
| 计划任务 runId 会话日志的 sessionId 兜底（`run:{id}`）是否已有 store 落点 | run/verify 事件落点 | 保守假设沿用既有 `sessionId = run:{id}` 口径（RunOrchestratorService.cs:657 同款），测试内以 InMemorySessionStore 断言 |

## 修订记录（实现期回写 · 2026-11-02）

- **FR-2.1 修订**：ExitCriteria 落 `AgentRun.ExitCriteria` 独立列（String -1），**不随 PlanJson 持久化**。理由：PlanJson 是计划面缓存（可被规则回退重建），判据是运行面事实；分列后 resume/审计不依赖 Plan 反序列化成功。承载列与 FR-2.2 同批建列（Model.xml + xcode）。
- **机器签名约定补充（FR-1.4）**：`check-problem-classes.ps1` 的 grep 签名只支持反模式（`expect: no-match`）；正向「必须存在」断言一律用 `kind: test` 签名承载（PC-023 S1/S2 即此形态）。
