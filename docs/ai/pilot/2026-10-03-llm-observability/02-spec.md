# Specification

> 阶段：Stage 2｜必须从真实 Repository Understanding 与 Intent 推导。
> 规则：① 所有内容与实际项目一致；② 不得发明不存在的接口、类、模块；③ 不确定点显式记录为 `Unknown`，不得自行假定。
> Task ID：PILOT-033 ｜ 日期：2026-10-03
> ⚠️ **闸门1 待裁决**：`FR-2`（`ChatTurn` 加列 = DB 结构变更，触规范 §1 硬性约束 2）、`U-2`（usage 缺口修法）、`U-3`（DB 结构变更审批）。成本阶段0（U-1）已随 032 并入本 pilot 解决，不再待裁决。

## 依赖前提（Blocking Prerequisite）

> **本任务 FR-1 有一个实测确认的前置阻断项，未解决则聚合结果系统性偏低。**

`ForgeSelf.Api/Controllers/ChatController.cs` 的 **`:131` 与 `:216`** 两处均为 `ToolCalls: null, Usage: null, FinishReason: "stop"` 硬编码（**本回合 `grep -n "Usage"` 实测**）。

### ⚠️ 实测更正：032 的「一行透传」结论**不成立**

032 design §15 U6 写「`ILlmRuntime` 已返回 `UsageInfo`……**只是一行透传**」。**本回合读码实测发现该判断有误**，必须更正：

| 事实 | 依据（本回合实测） |
| --- | --- |
| `ChatController` 注入的是 **`IAIService`**，不是 `ILlmRuntime` | `ChatController.cs:25` `private readonly IAIService _aiService;`；`:36`/`:43` 构造注入 |
| `IAIService.ChatAsync` **返回 `Task<string>`**——**签名里根本没有 usage** | `ForgeSelf.Api/Services/AIService.cs:15-31`：四个方法全是 `Task<string>` / `IAsyncEnumerable<string>` |
| `ILlmRuntime` **只有 `StreamAsync`**，且不返回 usage | `ForgeSelf.Abstractions/ILlmRuntime.cs:60` `IAsyncEnumerable<StreamChunk> StreamAsync(...)`；usage 只挂在 `StreamChunk.Usage`（:47）与 `Message.Usage`（:21） |
| legacy 适配器**已知有损**、取不到用量 | `ForgeSelf.Api/Services/AIServiceLlmRuntime.cs:19-20` 原文：「legacy 流式接口只产纯文本分片，**无法取到用量**……Usage 保持 null（provider 升级到 Unified 面后自然填充）」 |
| 带 usage 的面是 **Unified 面** | `IAIProvider.ChatAsync` → `UnifiedChatResponse.Usage`（`ForgeSelf.Abstractions/UnifiedChatModels.cs:110-119`）；`IChatCompletion.CompleteAsync` → `ChatCompletionResult` |

**更正后的结论**：主聊天链路要拿到 usage，**必须把 `ChatController` 从 legacy `IAIService`（纯文本）切到 Unified 面**（`IChatCompletion` 或 `IAIProvider`），这**不是一行改动**，而是一次**接缝迁移**（改注入 + 改调用 + 校验工具调用/流式行为不回归）。

**这使 U-2 的代价与风险显著高于 032 的估计**，故本轮**不擅自决定**，列为闸门1 裁决点（见 U-2），并给出三选项与推荐。

**后果（若不修）**：app 主聊天与 agent 会话（走 `ChatController`）的 token **永远不会进任何聚合** → 「今天花了多少」只统计到统一 AI 网关那一部分，聚合结果系统性偏低。

**处置**：`FR-1` 独立成阶段 T001，**先于其余 FR**；若 U-2 裁决为「本轮不做」，则 T001 转为「在文档与页面显式标注『主聊天用量当前不可见，主因：legacy 纯文本接缝』」，**不得让用户误以为总量完整**（对齐 BR-2 的数据诚实原则）。


---

## Functional Requirements

### FR-1 usage 缺口显式化（前置阻断的处理方式）

> ⚠️ 经读码实测，修复需**接缝迁移**而非一行透传（见「依赖前提」与 U-2）。故本轮 FR-1 的默认口径是**显式标注**，而非直接迁移。

- **FR-1.1**：观测端点与页面**显式声明**「当前统计仅覆盖统一 AI 网关链路；app 主聊天（legacy 纯文本接缝）用量不可见」，**不得让用户把部分覆盖误读为全量**。
- **FR-1.2**：聚合结果中，`unpricedModels` 之外**增加覆盖度提示**（例如「已覆盖 N 次调用 / 检测到 M 次主聊天调用未计入」）。
- **FR-1.3**：若 U-2 裁决为 (a) 接缝迁移，则本 FR 升级为：把 `ChatController` 从 `IAIService`（`Task<string>`，无 usage）切到带 usage 的 Unified 面，并**配套 e2e 回归**确保工具调用与流式行为不回归。
- **FR-1.4**：任何口径下，**无 usage 时保持 `null`，不伪造 0**（"0 token" 与 "无数据" 必须可区分）。
- **FR-1.5**：**保留** `FinishReason: "stop"` 现状不动（本任务不顺手改语义）。


### FR-2 trace 关联（ChatTurn ↔ AgentRun）

- **FR-2.1**：`ChatTurn` 增列 `AgentRunId`（`Int64?`，可空）与 `TraceId`（`String`，可空，长度 64）。
- **FR-2.2**：`AgentRun` 侧沿用既有 `AgentId`/`SessionId`（**不加列**；实测已有，见 `00` 实测表）。
- **FR-2.3**：Agent 执行路径在调用 LLM 时把当前 `AgentRunId` 写入 `ChatTurn`。
- **FR-2.4**：`ChatTurn` 增索引（`AgentRunId`），供按 run 反查其下 LLM 调用。
- **FR-2.5**：历史数据（既有 `ChatTurn` 行）`AgentRunId` 为 `null`，**不追溯回填**。

### FR-3 成本引擎（阶段0，已并入 PILOT-032 CostScope）

> 032 的详细成本设计（API 14 端点、4 表、任务书 T001–T011、决策 D1–D10）保留于 `archive/032-cost-scope/design.md`。本 pilot 整合其口径，不重建重复设计，仅做统一表述。
>
> **落位原则（2026-10-05 输入N+4 用户拍板「以插件为主，宿主侧的也迁移到插件」）**：FR-3~FR-5 的**业务与表现层唯一落位 `Plugins/CostScope/`**（成本纯函数 / 解析 / 聚合 / trace / 端点 / 界面 / 自有 4 表）。宿主侧只保留两样搬不动的东西：① `ITurnTelemetryQuery` 的**只读取数实现**（因为 `ChatTurn`/`SessionEventEntity` 等 XCode 实体在 `ForgeSelf.Api/Entities/`，而实测 19 个插件 csproj 只引用 `ForgeSelf.Core`+`ForgeSelf.Abstractions`，插件编译期拿不到宿主实体）；② `ChatTurn` 的新列（宿主库结构）。宿主侧**不留任何成本/聚合副本**。

- **FR-3.1**：成本计算为**纯函数**，签名 `CalculateCost(int promptTokens, int completionTokens, decimal inputPricePer1M, decimal outputPricePer1M) -> decimal`；零 IO，可完全单测锁死单位换算（沿用 032 D2/D10）。**落位 `Plugins/CostScope/Services/CostCalculationService.cs`**。
- **FR-3.2**：单位统一 **每 100 万 token**，`decimal(18,8)` 存储、`decimal(18,6)` 聚合，前端文案写清「元 / 100 万 tokens」（避免 1000 倍误差）。
- **FR-3.3**：**未配单价的模型，成本为 `Unknown` 并显式列出模型名**，绝不默默计 0（032 D5：计 0 会让用户误以为「没花钱」，是数据欺骗）。**「0 元单价」与「未配单价」必须可区分**。
- **FR-3.4**：改单价后**历史成本自动重算**（成本不入明细层，纯函数在聚合时算）。
- **FR-3.5 单价目录 CRUD**：`CostModelPrice` 表（模型唯一索引 + 输入/输出单价 + 供应商名 + 启用态）；提供列表/新增/改/删端点；单价是**用户本地配置**，不上传、不进 git（032 §9）。删除单价 → 历史成本转 `Unknown` 可撤销，不删用量。
- **FR-3.6 预算规则**：`CostBudget` 表（级别 Model/Provider/Global × 周期 day/month/quarter/year/all + 限额 + 预警比）；提供预算列表/新增/改/删与实时达成率；超支显式横幅（页面内，不发通知）。
- **FR-3.7 惰性物化日汇总**：`CostTurnDaySummary` 表（Day×Model 唯一索引）由 `TelemetryProjectionService` 查询时即时重算（当日未结束每次重算、已结束缓存到跨日），**无 `IHostedService`**（规避插件热重载不重启坑，032 D3）；插件**只写自有库**，对宿主库只读（032 D4/铁律10/12）。
- **FR-3.8 模型→供应商解析**：4 级回退（ChatModelId → UpstreamModelId/Alias → 大小写 → 未归属，绝不按前缀硬切），命中率统计（032 §3.2）；解析不中显式上报「未归属」而非猜测。**落位 `Plugins/CostScope/Services/ModelPriceResolver.cs`**；其所需的模型/供应商目录**经 `ITurnTelemetryQuery` 扩出的只读投影取得**（实读 A1 产物尚无此方法 ⇒ A4 开工前先扩契约，见 03-plan 偏差表）。
- **FR-3.9 数据源契约**：成本/用量聚合统一走 `ITurnTelemetryQuery`（位于 `ForgeSelf.Abstractions`，只读、分页、按时间/模型过滤，聚合 ChatTurn + SessionEvent + UsageRecord 三源）——见 FR-4.1。价格来源**不得硬编码**在本 pilot 内。

### FR-4 只读聚合端点

- **FR-4.1**：新增只读查询契约（即 032 定义的 **`ITurnTelemetryQuery`**，位于 `ForgeSelf.Abstractions`，只读、分页、按时间/模型过滤），聚合宿主三源：`ChatTurn`（统一 AI 网关写入）、`SessionEvent` 的 assistant message（app/agent 会话，**依赖 FR-1 才有 usage**）、`AgentRun`/`AgentStepRun`（Agent 轨迹）+ `UsageRecord`（工具维度）。**实现落宿主**（`ForgeSelf.Api/Services/CostScope/TurnTelemetryQueryService.cs`，只读取数、注册 Scoped），**消费落插件**。
- **FR-4.2**：成本/用量总览端点：按 模型 / 供应商 / 日 / 风格 四维聚合，返回 token、金额、轮次、失败数。**聚合器 `Plugins/CostScope/Services/CostAggregationService.cs`（入参是 FR-4.1 的只读 DTO），端点并入插件 `CostController`**。
- **FR-4.3**：延迟统计端点：返回 P50/P95/P99（输入 `FirstTokenMs` 与 `DurationMs` 两组，分别出分位数）。**同上，落插件**。
- **FR-4.4**：错误率端点：按时间窗返回失败数/总数与错误分布（`ErrorMessage` 归类）。**同上，落插件**。
- **FR-4.5**：trace waterfall 端点：以 `AgentRunId` 为根，返回该 run 下 LLM 调用（`ChatTurn`）与工具步骤（`AgentStepRun`）的时序节点，含每节点起止时间、耗时、模型、token。**聚合器 `Plugins/CostScope/Services/TraceProjectionService.cs`，端点落插件 `CostController`**（原计划的宿主 `LlmObservabilityController` 已取消，2026-10-05 输入N+4）。
- **FR-4.6**：**插件永不写宿主库**（032 D4 / 铁律 10）：聚合只读，写面若需要则只写插件/服务自有表。
- **FR-4.7**：所有管理面控制器**类级 `[Authorize]`**（铁律 17）——本 pilot 的管理面就是插件 `CostController`，同样适用（插件控制器不会自动被保护，见 `plugin-development` 铁律 17 的 mcp-center 反例）。

### FR-5 前端可视化

- **FR-5.1**：新增 LLM 观测 dashboard 视图：成本时序、按模型分布、延迟 P50/P95、错误率。**落位 `Plugins/CostScope/web/`**（`plugin-development` 铁律3「界面归插件，不归宿主」；2026-10-05 输入N+4）。
- **FR-5.2**：增加 **trace 瀑布视图**：以 AgentRun 为根展示 LLM 调用与工具步骤的时序。**形态更正**：原计划「挂进宿主 `ChatRecordDetail.vue`」改为**插件内 trace 页签**——宿主前端不能 import 插件组件（铁律4，插件是独立预编译产物），故宿主 `ChatRecordDetail.vue` 本批**零改动**。
- **FR-5.3**：`dsh-ui-bundle/.../panel.tsx` 的 `MOCK_ENTRIES`（:53-58）替换为真实数据源；**props shape 不变**（:137-143 已声明 `[key: string]: unknown` 预留注入位）。
- **FR-5.4**：未配单价的模型在页面**显式列出并给「去配单价」入口**，**不显示 ¥0.00 假象**（032 D5/图 5 O4）。
- **FR-5.5**：仅用 `--el-*` token + `color-mix()`，**0 自定义 token**。
- **FR-5.6**：新视图登记进宿主 `features.ts`（SSOT 属宿主清单，**不迁插件**），`scripts/check-features.mjs` 双向校验通过；菜单/路由由 `plugin.json frontend` 声明一处（铁律19①），route 变更同步 `e2e/menu-route-consistency.spec.ts`（铁律19③）。

## Input

| 输入 | 来源 | 说明 |
| --- | --- | --- |
| 时间窗 `from`/`to` | 查询参数 | 默认近 30 天（032 P0 口径） |
| `model` / `style` / `provider` | 查询参数（可选） | 维度过滤 |
| `AgentRunId` | waterfall 端点参数 | trace 根锚点 |
| `ChatTurn` 行 | 宿主表 | token / 延迟 / 状态 / 模型 |
| `SessionEvent` 行 | 宿主表 | 主聊天 usage（**依赖 FR-1**） |
| `AgentRun` / `AgentStepRun` 行 | 插件 AIAgent 表 | Agent 轨迹 |
| 模型单价 | `CostModelPrice` 表（CostScope 插件自有库，随 032 并入）| 不得硬编码在本 pilot 内 |

## Output

| 输出 | 形状 | 消费者 |
| --- | --- | --- |
| 成本总览 | `{ todayCost, monthCost, tokens, turns, failCount, topModels[], unpricedModels[], budgetsRemaining? }` | dashboard |
| 日趋势 | `[{ day, cost, promptTokens, completionTokens, turns, failCount }]` | 成本时序图 |
| 维度排行 | `{ by=model\|provider\|style\|day, items[] }` | 分布图 |
| 延迟分位 | `{ firstToken: {p50,p95,p99}, duration: {p50,p95,p99} }` | 延迟图 |
| 错误率 | `{ total, failed, rate, buckets[] }` | 错误率图 |
| trace waterfall | `{ runId, nodes: [{ kind: llm\|tool, ...timing, model?, tokens? }] }` | 瀑布视图 |
| 活动条目 | `[{ time, kind: todo\|decision\|action\|result, text }]` | dsh timeline（**沿用 panel.tsx 既有 schema**） |

## Business Rules

| # | 规则 | 依据 |
| --- | --- | --- |
| BR-1 | 成本 = `prompt/1e6 × inputPrice + completion/1e6 × outputPrice`，纯函数、不入明细层 | 032 D2/D10 |
| BR-2 | 未配单价 → `Unknown` + 显式列出，**不静默计 0** | 032 D5 |
| BR-3 | 成本以 **token 为真值**（上游返回的 usage），非估算 | 行业口径（Helicone 300+ 模型价目表思路） |
| BR-4 | `ResponseStatus != 200` 的轮次计入失败数，**不计成本** | 032 §4 降级要点 |
| BR-5 | 插件/服务**只读宿主库**，写入只落自有表 | 铁律 10/12、032 D4 |
| BR-6 | 真相源唯一：统计一律从 `ChatTurn`/`SessionEvent` 派生，**不双写** | 项目 `SessionEventEntity` 真相源约定 |
| BR-7 | 聚合**不改变**既有 `ChatTurn` 数据（除 FR-2 新增列） | 规范 §1 约束 5/6 |
| BR-8 | 单价与成本均为**本地数据**，不上传、不进 git | 032 §9 |
| BR-9 | 阶段3（质量评估/LLM-as-judge）**本轮不做** | 01-intent Expected Outcome |

## Boundary Conditions

| # | 边界 | 期望行为 |
| --- | --- | --- |
| BC-1 | 上游未返回 usage（`Usage == null`） | 计入 `UnpricedTurns`/无 token，**不伪造 0**；连续 N 轮空 usage → 口径降级为「仅次数」并明示（032 图 5 G3/O3） |
| BC-2 | 单价目录为空 | 首屏阻塞式提示「请先配置单价」，**不渲染 ¥0.00**（032 图 5 G4/O4） |
| BC-3 | 模型名无法解析到供应商 | 归「未归属」+ 命中率统计，**绝不按前缀硬切**（032 §3.2 解析顺序 4 级回退） |
| BC-4 | `AgentRunId` 为 null（历史行/非 Agent 路径） | waterfall 端点返回明确「该轮次无关联 AgentRun」，不静默挂到某个 run |
| BC-5 | 一个模型名出现在多个 provider 的 `SupportedModels` | 按 032 §3.2 优先级（ChatModelId > UpstreamModelId > Alias > 大小写回退），仍不中即未知归属 |
| BC-6 | 时间窗内无数据 | 返回空集合 + 显式空态，**不返回 500** |
| BC-7 | 延迟样本数 < 分位要求 | 分位数按实际样本计算，样本数随分位数一并返回 |
| BC-8 | 插件/扩展未启用 | 端点返回明确的「未启用」，不 404 空页 |

## Error Handling

| 场景 | 处理 |
| --- | --- |
| 契约层（宿主表结构）漂移 | 抛 `ContractBrokenException` → 前端明确报「数据源变更，需更新插件」，**不渲染空白页**（032 §7） |
| 分页取数中途异常 | 该日标记 `partiallyRefreshed=true`，**已算出的其余部分正常返回**，不整页失败（032 §4） |
| 未鉴权 | 401（类级 `[Authorize]`，铁律 17） |
| 非法维度枚举 | 400 + 明确错误，**禁止静默返回空表**（032 §11 第 2 问） |
| 非法价格/阈值 | 后端拒绝并返回 400（单价 ∈ [0,1e6] 等） |
| 未知路由/参数 | 400 + 可用值列表 |

## Compatibility

- **向后兼容**：`ChatTurn` 新增列均可空，XCode 自动迁移，既有读路径不受影响。
- **旧前端不受影响**：新增端点与视图为增量；`ChatRecordDetail.vue` 现有展示保持。
- **`SessionEvent` 结构不变**：只改 `ChatController` 填充 `Usage` 的值，**不改 record 形状**。
- **040（B1–B8）改造并行中**：`SessionEvents.cs` 正在被 040 触碰（已加 `CallId`）。本任务**只改 `ChatController` 的填充值，不改 `SessionEvents.cs` record 定义**，降低与 040 的冲突面（契约稳定性同 032 U5）。

## Non-functional Requirements

| # | 要求 | 说明 |
| --- | --- | --- |
| NFR-1 | **零新增外部依赖** | 复用 XCode + 现有前端栈 |
| NFR-2 | 聚合**只读**，不在 LLM 请求路径上增加 IO | 观测不得拖慢主链路 |
| NFR-3 | 成本计算零 IO（纯函数） | 可完全单测锁死 |
| NFR-4 | 端点响应须分页/限流，避免大时间窗一次性全表扫 | |
| NFR-5 | 敏感数据（请求/响应体）**不进入观测端点返回** | 只返回聚合与元数据；明细回看走既有 `ChatRecordsController` |
| NFR-6 | 前端 0 自定义 token | 仅 `--el-*` + `color-mix()` |
| NFR-7 | 改动原子化、可回滚 | 单一 commit 粒度（**须用户授权才提交**） |

## Acceptance Criteria

> 逐条可测；闸门1 用户确认的就是这里的清单（与 01-intent SC1–SC10 对齐，编号一一对应）。

| # | AC | 对应 SC | 验证 |
| --- | --- | --- | --- |
| AC1 | 覆盖度显式：端点与页面均显示「已覆盖 N / 未计入 M（主聊天 legacy 接缝）」；无 usage 时为 `null` 不伪造 0 | SC1 | 单测（未计入计数正确）+ 页面断言 |
| AC1b | （仅当 U-2 裁决为 (a)）`ChatController` 迁到 Unified 面后，主聊天会话事件带非空 `Usage`，且工具调用/流式 e2e 无回归 | SC1 | 集成测试 + e2e 回归 |
| AC2 | 成本聚合端点按 模型/供应商/日/风格 四维返回 token+金额；未配单价模型进 `unpricedModels` 且不计入总额 | SC2 | 单测 |
| AC3 | 成本纯函数：1M token 基准、小数精度、0 tokens、改价后历史重算 | SC3 | 单测锁死 |
| AC4 | waterfall 端点以 AgentRun 为根返回 LLM 调用 + 工具步骤时序，耗时非负且按时间排序 | SC4 | 单测 |
| AC5 | 延迟端点 P50/P95/P99 与手算一致（FirstTokenMs 与 DurationMs 分别出） | SC5 | 单测 |
| AC6 | dsh timeline 显示真实数据；**空会话显示空态而非 mock 4 条** | SC6 | e2e + 反例探针（切空会话必红） |
| AC7 | dashboard 展示值与后端端点逐项一致（禁 mock） | SC7 | e2e + 组件测试 |
| AC8 | 新增端点无 token 调用返回 401 | SC8 | 反射断言 + 集成测试 |
| AC9 | `dotnet build` + `dotnet test` 通过；`pnpm run check` + `pnpm run test` 通过且无新增错误 | SC9 | 真实命令输出 |
| AC10 | `features.ts` 登记新视图，`scripts/check-features.mjs` 双向校验退出码 0 | SC10 | 脚本 |

**范围纪律**：AC1–AC10 之外不算完成。

## Unknown

| # | 不确定点 | 影响 | 处理方式 |
| --- | --- | --- | --- |
| **U-1** | **本轮范围是否含阶段0（成本引擎 + 单价目录 + 预算）？** | 032 已于 2026-10-05 并入本 pilot → **成本阶段0 已纳入范围**（FR-3 全量：纯函数 + 单价目录 CRUD + 预算 + 日汇总 + 模型解析）。032 原 design 保留于 `archive/032-cost-scope/design.md` | **已并入，无需再裁决** |
| **U-2** | **U6 修法选型**（**本回合读码后选项已改，原 032 的「一行透传」不成立**）：<br/>(a) **`ChatController` 迁到 Unified 面**（改注入 `IAIService`→`IChatCompletion`/`IAIProvider`，取 `UnifiedChatResponse.Usage`）——彻底解决，但属**接缝迁移**，须回归工具调用与流式行为；<br/>(b) 契约聚合 `ChatTurn`+`SessionEvent` 双源——可回溯历史，但**不解决未来数据缺失**（主聊天仍写 null）；<br/>(c) 本轮不修，仅**显式标注缺口** | (a) 触及主聊天链路，有回归风险；(b) 治标；(c) 数据不完整但零风险 | **闸门1 裁决**。建议 **本轮 (c) 显式标注 + 另立任务做 (a)**：(a) 的迁移应独立立项并配 e2e 回归，不宜搭在观测任务里顺带做（规范 §1 约束 5/6） |
| **U-2a** | 若裁决 (a)：迁到 `IChatCompletion` 会**改变工具调用与 Agent 路由行为**（`IChatCompletion` 走 Agent 上下文，legacy `IAIService` 直连 provider） | 可能改变主聊天的行为语义（不止是加个字段） | 裁决 (a) 时必须先读 `AIAgentChatCompletion`/`ReactLoopAgent` 确认行为差异，并配 e2e 回归；**本工件不代做此分析** |

| **U-3** | **`ChatTurn` 加列属 DB 结构变更**（规范 §1 硬性约束 2） | 未批即实施 = 流程违规 | **闸门1 明确批准**；或裁决替代方案（见下） |
| **U-3a** | 若不加列，trace 关联的替代方案：(i) 靠 `SessionKey`/`SessionId` + 时间窗近似关联（弱）；(ii) 靠 `SessionEvent` 的 `CallId` 侧写关联（040 已引入 `CallId`，但 ChatTurn 侧无此列） | 决定是否必须动 DB | 闸门1 一并裁决；**默认按加列设计**（关联准确），加列被否则退化为 (i) 并在文档标注「关联为近似」 |
| **U-4** | **模型单价来源契约落位** | 032 已并入，契约落位随之确定：`ITurnTelemetryQuery`（Abstractions 只读契约）+ `CostModelPrice` 表（CostScope 插件自有库，本 pilot 读写皆在插件内） | **已确定（沿用 032 A 方案）**：契约在 `ForgeSelf.Abstractions`，实现在宿主并注册；价格来源由插件自有库承载，**不得硬编码** |
| **U-5** | 040（B1–B8）改造是否改 `SessionEvent`/`ChatTurn` 结构 | 契约稳定性 | 契约只依赖少量稳定列（`ChatTurn` 的 tokens/duration/time 均为 `Int*`/`DateTime`）；若 040 改表，只改实现不动 DTO（同 032 U5） |
| **U-6** | 质量评估（阶段3）何时立项 | 本轮不做 | 登记独立 TODO，不在本工件范围 |
| **U-7** | dsh timeline 的真实数据源具体形态（`ctx.sessions` 注入的实际结构） | FR-5.3 实现方式 | 实施时读 DSH Slot 框架契约确认；**契约未知则只做「宿主事件 → 既有 schema」的映射，不改 DSH 侧** |
| **U-8** | 币种与时区 | 金额展示、日界线 | 沿用 032 U3/U8：**CNY 无税**、**设备本地时区**，设置页可见口径 |
| **U-9** | `AgentStepRun` 是否覆盖所有 Agent 工具步骤（有无漏记） | waterfall 完整性 | 实施时读 AIAgent 插件执行路径确认；未确认前在页面标注「以 AgentRun/AgentStepRun 记录为准」 |
