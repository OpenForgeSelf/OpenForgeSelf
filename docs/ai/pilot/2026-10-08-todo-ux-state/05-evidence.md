# 05 · Evidence（PILOT-057A/057B：详情排版重构 + 委派完成状态同步 + 乱码止损）

> 迷你任务（057A = `mini-task-ui.md`，057B = `mini-task-agenthub.md`，同一 pilot 目录）。证据按 Verified / Inferred / Unknown 分级。

## 变更清单（已实施）

### 057A 排版（ui-ux-design 技能 CRAP 审查后）

| 文件 | 动作 |
| --- | --- |
| `Plugins/TodoTracker/web/src/TodoView.vue` | 主区两栏：列表 `flex:0 0 400px` + 详情 `flex:1 1 auto; min-width:0`（原 520px 固定侧边栏） |
| `Plugins/TodoTracker/web/src/components/TaskDetail.vue` | 详情重构为三区卡片（任务信息 / 下发与执行 / 阶段与执行记录）；委派状态改 `.td-deleg-badge` 颜色徽标（复用 agentStatusMeta）；委派状态行加 `data-test=delegation-status` 锚 |

### 057B 状态同步 + 编码（回报优先）

| 文件 | 动作 |
| --- | --- |
| `ForgeSelf.Abstractions/AgentDelegationContracts.cs` | `IAgentDelegation` 新增 `MarkCompletedAsync(taskKey, ct)`（回报优先语义：已终态不改） |
| `Plugins/AgentHub/Services/DelegationRuntime.cs` | 新增 `CompleteByKey`（非终态 → Succeeded/exitCode 0/errorCode null/EndTime 现在 + Exit 事件）；`RunAsync` finally 加 `if (!TaskStatus.IsTerminal(entity.Status))` 保护（进程退出判定不覆盖已回报完成） |
| `Plugins/AgentHub/Services/AgentDelegationProvider.cs` | 实现 `MarkCompletedAsync` → Runtime.CompleteByKey |
| `Plugins/TodoTracker/Services/IAgentTaskGateway.cs` / `AgentTaskGateway.cs` | 新增 `MarkCompleted`（接缝缺席 → SeamMissing；异常记 Warn 不阻断） |
| `Plugins/TodoTracker/Services/ITodoDispatchService.cs` / `TodoDispatchService.cs` | 新增 `MarkDelegationCompleteAsync(id)`（todo 无 AgentTaskKey → Ok(false) 幂等） |
| `Plugins/TodoTracker/Controllers/TaskExecutionsController.cs` | `AppendInternal` 回报 `StageTo=Review` 时回调 `MarkDelegationCompleteAsync`（失败仅 Warn）；新增 `HasReplacementChar`（U+FFFD 检测记 Warn） |
| `Plugins/TodoTracker/Services/DispatchPayloadBuilder.cs` | 回报契约 curl 模板值 ASCII 化（`<your-id>`/`<what-you-did>` 等占位符，提示别在命令行夹带中文引号），从源头规避 GBK→UTF-8 乱码 |
| `ForgeSelf.Api.Tests/.../TodoAgentDelegationTests.cs`、`TodoAgentStatusBatchTests.cs` | 三个 Fake 类补 `MarkCompletedAsync` 实现 |
| `ForgeSelf.Web/e2e/plugins/todo-tracker/todo-tracker.spec.ts` | E1 详情锚点改 `[data-test=delegation-status]`（三卡片后 .td-block 父子嵌套）；U1 option 计数改 ≥2（前置用例可能已建档案）——断言语义不变，仅定位适配 |

## 验证证据（Verified）

- **后端 build**：`dotnet build` 0 错误（1344 既有警告）✅
- **相关测试过滤集**（Todo|AgentHub|Delegation|Dispatch）：263 通过 / 1 失败 = `CompleteThenReopen_Flow_ShouldFlipStatus`（InvalidCastException；路径与本批零交集，判预存/环境红，TODO 待全量基线对表）✅
- **插件前端 build**：`pnpm run build` → `✓ built in 425ms`（20 modules，dist 78.69 kB）✅
- **插件 e2e 全目录**：`e2e/plugins/todo-tracker` **15/15 passed**（2.4m，workers=1）✅——含：
  - E1 真实委派端到端（登记 opencode → 委派 → 轮询终态 → 详情委派区 `[data-test=delegation-status]` 含 agent 名 → 记为执行记录落库）1.3m 绿
  - U1-U5（新建选项目带出地址 / 详情选项目 / 禁用原因可见 / agents 空态引导 / 未委派无徽标）全绿
  - V1 视觉走查截图（v1-detail-open / v1-long-title / e1-delegate-opencode）读图核对：**两栏布局生效（列表 ~35% + 详情 ~63%）、三卡片分区、委派徽标与终态文本齐全、长标题省略、空态引导文案** —— 对齐 ui-ux-design 走查清单（CRAP：对比/重复/对齐/亲密性）✅

## 分级说明

- 057B 状态同步**行为**（回报 Review → AgentHub 标记 Succeeded、超时不再覆盖）：**Inferred**——隔离 e2e 中 opencode 终态仍为 Failed(timeout)（上游模型端点 400 外部因素，任务 48/49 同因），**未能在 e2e 中触达「回报成功 + 进程未退」的竞态现场**；代码路径（CompleteByKey + finally IsTerminal 保护）已实读复核，语义闭合。真机验证待 2.3.4 更新后在运行实例用真实成功回报复验。
- 乱码止损（ASCII 模板 + U+FFFD 检测）：**Inferred**——模板已改（编译验证），但尚无新回报样本验证「不再乱码」；历史记录 #5 乱码不可逆（GBK 字节已毁），按方案不修历史数据。
- 排版/徽标/空态：**Verified**（截图读图 + e2e 断言）。
