# Specification

> 阶段：Stage 2｜从 Repository Understanding 与 Intent 推导。  
> Task ID：`2026-11-19-oar-agenthub-runtime`  
> 本 Spec 覆盖：**本批文档/决策交付物** + **后续实现必须遵守的目标规格**（实现另开阶段 pilot，闸门1 批准后才改代码）。

## Functional Requirements

### FR-D（本批文档）

| ID | 要求 |
| --- | --- |
| FR-D1 | 固化最终架构：AgentHub + OAR Node Worker + `IAgentRuntime` 公共契约 |
| FR-D2 | 纠偏调研稿：`IAgentLoop` 已删；对照物为 `IAgent`/`IAgentRegistry`（本机）与 `IAgentDelegation`（外部薄委派） |
| FR-D3 | 明确 ADR-005 边界：OAR ≠ 平行本机 Runtime |
| FR-D4 | 定义 `IAgentDelegation` 与 `IAgentRuntime` 共存策略 |
| FR-D5 | 列出 P0–P4 与第一期范围 / 缓做项 |

### FR-R（目标运行时规格 — 实现阶段遵守）

| ID | 要求 |
| --- | --- |
| FR-R1 | AgentHub 内新增长期 Node Worker，经 JSON-RPC/JSONL over stdio 调用 `@botiverse/oar`（精确版本锁定） |
| FR-R2 | 新增 `OarTransport` + `IAgentTransportRegistry`；按 `AccessPoint.Transport` 选路；保留 `CliTransport` |
| FR-R3 | `AgentAccessPoint` 增加独立 `RuntimeId`（不与 Vendor 混用） |
| FR-R4 | 新增持久化 `AgentHubSession`；对外只暴露 `SessionKey`；内部映射 `OarSessionId` |
| FR-R5 | 事件双层：底层 Record 持久化 + 上层业务事件；`AgentHubSequence` 单调递增，另存 `OarSequence`+`Generation` |
| FR-R6 | 控制结果统一 `AgentControlResult{Accepted,Code,Reason}`，禁止用 bool 掩盖拒绝 |
| FR-R7 | Abstractions 新增 `IAgentRuntime` + DTO；AgentHub 同实例 `services` + `ctx.Register` |
| FR-R8 | 其他插件只依赖 `IAgentRuntime`/`IAgentDelegation`，不引用 AgentHub 程序集或 OAR 类型 |
| FR-R9 | Node&lt;24 或 Worker 不可用时：OAR 路径标记不可用，**CLI 路径仍可用** |
| FR-R10 | 不自动安装/升级第三方 Agent；安装须用户确认 |

## Input

| 输入 | 来源 |
| --- | --- |
| OAR 调研方案全文 | 用户输入1 |
| 仓库现状 | AgentHub / Abstractions / 架构文档 / ADR-005 |
| OAR 公开资料 | GitHub `botiverse/oar`、npm `@botiverse/oar`（验证基线 0.109.0） |

## Output

| 产物 | 路径 |
| --- | --- |
| 调研归档 | `docs/06-research/007-oar-agenthub-runtime.md` |
| 架构真源 | `docs/01-architecture/agenthub-oar-runtime.md` |
| ADR | `docs/07-decisions/007-oar-agenthub-runtime.md` |
| pilot 工件 | `docs/ai/pilot/2026-11-19-oar-agenthub-runtime/` |
| 真源索引 | `docs/README.md` §一 新增行 |
| 缓做台账 | `docs/07-decisions/not-taken-decisions.md` 新增条目 |

## Business Rules

1. **OAR 是 AgentHub 内部 Provider**，不是宿主全局依赖；未启用 AgentHub 时 `ctx.Get<IAgentRuntime>()` 为 null，消费者降级。
2. **能力以 Runtime 声明为准**，禁止按 Agent 名称猜测 Steer/Resume 等。
3. **权限只能收紧不能放宽**：请求权限 ⊆ AgentPolicy；无限权限模式继续拒绝。
4. **`IAgentDelegation` 保留**：既有 todo-tracker 不因本改造被迫迁移；实现上可由同一 Provider 适配，语义仍为「入队+读回」。
5. **`IAgentRuntime` 职责**：外部 Coding Agent 的发现、会话、任务、输入、控制、事件；**不**替代 MAF/`IAgent` 本机循环。
6. **推理正文默认不暴露**给前端/消费插件（可保留状态元信息）。
7. **Session 恢复 ≠ 进程恢复**：崩溃后原 Task → Interrupted；可 Resume Session 开新一轮，不得把中断 Task 标成功。

## Boundary Conditions

| 边界 | 行为 |
| --- | --- |
| 无 Node / Node&lt;24 | OAR unavailable；CLI 继续 |
| OAR 不支持某控制 | `Accepted=false` + Code/Reason |
| 插件热更新卸载 | `ctx.Effect` 关闭会话并杀 Worker 进程树；共享表摘除 `IAgentRuntime` |
| cwd 越白名单 | 拒绝启动（含模型探测用目录） |
| 宿主重启 | 运行中 Task → Interrupted；Session 可按持久化信息尝试 Resume |

## Error Handling

- Worker stdout **仅**协议消息；日志走 stderr。
- 协议超时、Worker 崩溃：Session/Task 标记 Interrupted 或 Failed，带可解释错误码；允许受控重启 Worker。
- 公共契约失败：不抛未文档化异常给插件；控制类返回 `AgentControlResult`；提交类可沿用 Delegation 的 Outcome 风格或等价明确结果（实现阶段定稿，不得静默成功）。

## Compatibility

| 项 | 要求 |
| --- | --- |
| 现有 CLI 委派 | 行为回归：未改 Transport 的 AccessPoint 仍走 Cli |
| `IAgentDelegation` | 方法与 DTO 保持可用；可内部转发，不强制破契约 |
| ADR-005/006 | 不引入平行本机 Runtime |
| 事件词表 | 扩展须同步前端/e2e；旧类型保留 |

## Non-functional Requirements

- Worker 延迟启动（首次 OAR 调用）；卸载必释放。
- OAR 依赖精确锁版本 + `pnpm-lock.yaml` 入库。
- 优先按 Runtime 独立入口加载（降低 heap），根入口一次性全载列为后期优化。
- 原始 Record 脱敏、长度限制、访问控制。
- 单测优先用 `@botiverse/oar/testing` Scripted Runtime；真实 Agent 冒烟限 Codex/Claude/OpenCode。

## Failure Modes

| 模式 | 本设计如何应对 |
| --- | --- |
| 跨插件服务实例分叉（seams §7/§8） | 强制同实例注册；测试断言 Get 与控制器同源 |
| 把 OAR 当调度器 | 保留 DelegationRuntime/Permission/Session/EventStore |
| 用 OAR seq 当全局序 | AgentHubSequence + Generation |
| Node 环境不足当全站故障 | CLI 降级路径 |
| 与 ADR-005 冲突误读 | ADR-007 + README 速查显式对照 |

## Interaction Design（交互设计）

### 本批

**无界面交互**（仅文档/决策）。管理面与工具行为预登如下，供 P3 实现 Spec 继承。

### 目标态：管理面（P3）

| 交互点 | 触发 | 结果 | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| Runtime 列表 | 打开 Agent 管理 | 展示 Agent/Runtime/安装/认证/版本/能力/可用性 | Ok/Degraded/Unavailable | 加载骨架→表格 | 无 Runtime：说明需 Node24+ 或改用 CLI | Node 不足时横幅提示，不阻断 CLI Agent |
| 探测 | 点「检查」 | 刷新安装+认证（分开展示） | probing/done/error | 按钮 loading | — | 不把「已安装」显示成「已认证」 |
| 安装 | 点「安装」 | 先展示 installPlan → 确认 → 执行 → 再探测 | plan/confirming/installing | 确认框 + 结果 toast | 无计划 | 禁止静默自动装 |
| 会话列表 | 打开 Session 视图 | 列表：状态/Agent/cwd/模型/最近任务 | Idle/Running/… | 加载/错误可重试 | 无会话引导创建 | — |
| 继续对话 | 输入并发送 | 经 SessionKey 投递；事件面板追加 | 依能力 Deliver/Queue/拒 | 拒绝时展示 Code/Reason | — | 不支持 Steer 时禁用并给原因 |
| 终止/关闭 | Abort / Dispose | 控制结果回显；状态更新 | — | toast | — | Abort Accepted ≠ 任务已结束，需等事件 |

五态：加载 / 空 / 出错 / 无权限（401/策略拒绝）/ 正常 — P3 亮暗两态走查。

### 无界面交互（工具 / 契约，P2–P3）

| 通道 | 入参要点 | 成功 | 错误提示 |
| --- | --- | --- | --- |
| `IAgentRuntime.SubmitAsync` | Prompt + AgentId/RuntimeId/Tag + 可选 SessionKey/cwd/权限 | 返回 TaskKey+SessionKey | 策略/白名单/无 Runtime：明确文案 |
| `WatchTaskAsync` | taskKey + afterSequence | 事件流 | 任务不存在 |
| `hub_ask_agent` | 改为 SessionKey 续接 | 同现工具成功形态 | 禁止回传 OarSessionId |
| `IAgentDelegation` | 保持现契约 | 入队 Outcome | 503=接缝缺失 |

### 视觉规格

本批无 UI 改动。P3 继承 `DESIGN.md` + 现有 `AgentHubView`；新增 Runtime/Session 区块不引入独立色板；禁止把内部 reasoning 正文默认渲染。

### 交互验收标准（实现阶段）

- Runtime「安装」与「认证」分列，永不合并为单一布尔。
- 能力不支持的操作：控件禁用 + Reason，或返回拒绝结果可见。
- 异步探测：≤100ms 出局部 loading；完成前不先空态成功。

### 走查符合性

P3 走查绑定 `ui-ux-design` 清单 + 插件六步；本批文档交付不走 UI 走查。

## Acceptance Criteria

### 本批（闸门1 文档包）

- [x] AC1：ADR-007 / 架构真源 / 调研 007 / pilot 00–04 / gate1-proposal 存在且交叉链接正确
- [x] AC2：README §一 可 30 秒定位到 OAR/`IAgentRuntime` 真源
- [x] AC3：明确写出与 `IAgent`、`IAgentDelegation`、ADR-005 的边界
- [x] AC4：缓做项已入 not-taken
- [x] AC5：无 `Plugins/AgentHub/**/*.cs` / `ForgeSelf.Abstractions/**/*.cs` 业务变更（本批）

### 后续实现总 AC（批准后分阶段验收）

- [x] AC-R1：P0 Worker 完成 list/install/auth/models/session/prompt/events/resume/cancel/dispose 探针（`2026-11-19-oar-runtime-worker-p0`）
- [ ] AC-R2：Cli 回归 + Oar 选路（P2）
- [ ] AC-R3：`ctx.Get<IAgentRuntime>()` 可提交/监视/续接/取消（P3）
- [ ] AC-R4：Session 持久化与 Sequence 单调（P2）
- [ ] AC-R5：权限与 cwd 守卫仍有效（P2 回归）

## Unknown

| 不确定点 | 影响 | 处理 |
| --- | --- | --- |
| OAR 0.109.0 与本机已装 Claude/Codex/OpenCode 的实测兼容矩阵 | P0 范围 | P0 探针记录；不兼容则钉版本或暂缓该 Runtime |
| 云/开发机 Node 多为 22 | Worker 无法跑 | 文档标 Node≥24 前置；缺省降级 CLI；P0 环境需升 Node |
| `IAgentRuntime` 方法最终命名是否与草案一字不差 | 契约稳定性 | 闸门1 批准形态；实现可小幅对齐现有 DTO 词表，变更记 ADR 附录 |
| Session 表字段是否一次加全 | DB 变更面 | P2 最小字段集；高风险须闸门1 明示批准 XCode 实体扩展 |
