# Plan

> 阶段：Stage 3｜**必须具体到真实文件路径**，禁止只写「修改 Service、增加测试」。
> Task ID：PILOT-033 ｜ 日期：2026-10-03
> ⚠️ 本 Plan 为**闸门1 待批稿**，未获批准前不得进入 Implement。

## Files To Change

> `reason` 一律基于本回合实测读码结论。

### A. 宿主后端（`ForgeSelf.Api/`）——**按 2026-10-05 输入N+4 收缩为「结构 + 取数接缝」两类**

> 业务（成本/解析/聚合/trace/端点/界面）**全部迁 `Plugins/CostScope/`**，见 §A2。本节剩下的每一项都有「为什么搬不动」的实测依据（见文末偏差表 2026-10-05 各行的证据）。

- file: `ForgeSelf.Api/Entities/Model.xml`
  reason: `ChatTurn` 表结构真源（XCode 由它生成）。铁律 9：先改 Model.xml 再 `xcode`。**属 DB 结构变更 → U-3 裁决点**（A9）。

- file: `ForgeSelf.Api/Entities/ChatTurn.cs` + `ChatTurn.Model.cs` / `ChatTurn.Biz.cs`
  reason: FR-2 加 `AgentRunId`/`TraceId` 两列。实测该文件 28 个字段全为 `BindColumn`+`DataObjectField` 形态，新列须同形；生成物**必须走 `Model.xml` → `xcode`**（铁律 9，禁手改）。表在宿主库 ⇒ 不可能搬进插件（FR-4.6/BR-5 禁插件写宿主库）。

- file: `ForgeSelf.Abstractions/ITurnTelemetryQuery.cs` + `TurnTelemetryDtos.cs`
  reason: 插件取宿主数据的**唯一合法通道**（A1 已建）。程序集分层决定：插件只引用 Core+Abstractions（实测 19/19），所以「宿主实现 + 插件消费」是本仓既有形态（先例 `IUsageStatsService`）。⚠️ **A4 需在此扩一个模型目录方法 + `ModelIdentityDto`**（实读 A1 产物缺此项，见偏差表）。

- file: `ForgeSelf.Api/Services/CostScope/TurnTelemetryQueryService.cs`（新建，**留在宿主**）
  reason: `ITurnTelemetryQuery` 的只读实现（读 `ChatTurn`/`SessionEventEntity`/`UsageRecord`/`AIModel`/`AIProvider`）。留宿主的理由是**编译期事实**不是偏好：这些 XCode 实体在 `ForgeSelf.Api/Entities/`，插件程序集引用不到。注册为 Scoped（与 `AppBuilder.cs:192` 的 `IUsageStatsService` 同形）。

- file: `ForgeSelf.Api/AppBuilder.cs`
  reason: **仅**注册上面这一条只读接缝（+1 行 DI）。不再注册成本/聚合/端点（那些随 §A2 进插件）。

- file: `ForgeSelf.Api/ForgeSelf.Api.csproj`（**A2 缺口修复**）
  reason: `:106-130` 的 17 条插件构建顺序引用**漏了 CostScope** ⇒ `dotnet build ForgeSelf.Api` 不构建该插件、`publish`/CI 产物里 `Plugins/CostScope/` 只有 `plugin.json` 无 DLL（与 `:127` DesignSystem 的 MSB3030 同病）。补一行 `ReferenceOutputAssembly="false"`。判据：宿主构建产物里存在 `$(OutDir)Plugins/CostScope/CostScope.dll`。

- file: `ForgeSelf.Api/Controllers/ChatController.cs`
  reason: **仅当 U-2 裁决为 (a) 时**改（FR-1.3）。实测 `:25` 注入 `IAIService`、`:126` 调 `ChatAsync`（`Task<string>` 无 usage）、`:203` 调 `ChatStreamAsync`。**默认口径（U-2 = c）下此文件不动**。

- ~~file: `ForgeSelf.Api/Services/AI/CostCalculator.cs`~~ / ~~`ModelPriceResolver.cs`~~ / ~~`UsageAggregator.cs`~~ / ~~`TraceAggregator.cs`~~ / ~~`ForgeSelf.Api/Controllers/LlmObservabilityController.cs`~~（**2026-10-05 输入N+4 取消**）
  reason: 用户拍板「以插件为主，宿主侧的也迁移到插件」。这些是业务与表现层，经 `ITurnTelemetryQuery` 就能拿到所需数据 ⇒ 全部改落 `Plugins/CostScope/{Services,Controllers}/`（见 §A2），宿主侧不留副本（消灭双真相）。

### A2. CostScope 插件（`Plugins/CostScope/`）——**本 pilot 业务层唯一落位（2026-10-05 输入N+4）**

> 032 原 design §2/§3/§8/§14 为本节真源，保留于 `archive/032-cost-scope/design.md`。
> 契约文件（`ITurnTelemetryQuery`/`TurnTelemetryDtos`）与宿主只读实现见 §A（A1 已完成，实现待 A5 起用）；本节不再重复列。

- file: `Plugins/CostScope/plugin.json` + `CostScopePlugin.cs`（**A2 已建**）
  reason: 运行时 id `cost-scope`、入口 `CostScopePlugin`、`frontend` 声明菜单/路由/entry（铁律19①：界面入口只声明一处）。`Apply` 内取数据目录 + 注册插件侧服务。

- file: `Plugins/CostScope/Data/Model.xml`（**A2 已建**，真源）→ `xcode Model.xml`
  reason: 插件自有 4 表（`CostModelPrice`/`CostBudget`/`CostTurnDaySummary`/`CostSettings`），铁律 9 先改 Model.xml 再 xcode，禁手改生成物；铁律 12 插件自行建表（`Data/CostScopeTables.cs` 真源）。

- file: `Plugins/CostScope/Services/CostCalculationService.cs`（新建，**A3**）
  reason: FR-3.1/3.4 成本**纯函数**（零 IO，NFR-3）。自宿主 `Services/AI/CostCalculator.cs` 迁入并**取消宿主副本**——单位口径（每 1M token、`decimal(18,8)`）只在这里存在一份。

- file: `Plugins/CostScope/Services/ModelPriceResolver.cs`（新建，**A4**，取代宿主同名文件）
  reason: FR-3.8 模型→供应商 4 级解析（ChatModelId → UpstreamModelId/Alias → 大小写 → **未归属不猜**）。数据源改为经 `ITurnTelemetryQuery` 扩出的模型目录投影（§A 偏差行已记：A1 产物尚无此方法，A4 先扩契约）+ 插件自有 `CostModelPrice`。

- file: `Plugins/CostScope/Services/CostAggregationService.cs`（新建，**A5/A6/A7**，取代宿主 `UsageAggregator.cs`）
  reason: FR-4.2~4.4 的四维聚合（模型/供应商/日/风格）+ 延迟分位 + 错误率；输入是 `ITurnTelemetryQuery` 返回的只读 DTO，**插件不碰宿主库**（FR-4.6/BR-5）。未配单价进 `unpricedModels` 且不计总额（BR-2）。

- file: `Plugins/CostScope/Services/PriceCatalogService.cs` + `BudgetService.cs` + `TelemetryProjectionService.cs`（新建，**A5/A6/A7**）
  reason: 单价目录 CRUD（FR-3.5）、预算规则与达成率（FR-3.6）、`CostTurnDaySummary` 惰性物化（FR-3.7，无 `IHostedService`，规避插件热重载不重启坑 032 D3）。

- file: `Plugins/CostScope/Services/TraceProjectionService.cs`（新建，**A10**，取代宿主 `TraceAggregator.cs`）
  reason: FR-4.5 waterfall：以 `AgentRunId` 为根串 `TurnTelemetryRecord` + `AgentRunTelemetry.Steps`，耗时非负、按时间排序（AC4）。

- file: `Plugins/CostScope/Controllers/CostController.cs`（新建，**A11**，**类级 `[Authorize("ApiKeyPolicy")]`**）
  reason: **合并原计划的两处端点面**（032 的 14 端点 + 033 的 `LlmObservabilityController`）为插件单一控制器：overview/summary/daily/breakdown/latency/errors/turns/prices CRUD/budgets CRUD/summary rebuild/settings + trace。端点前缀按插件惯例 `api/cost-scope/...`；未知 `by=` 返回 400 不静默空表（AC8）。

- file: `Plugins/CostScope/web/**`（新建，**A12**）
  reason: 观测界面全部插件自带（铁律3）：总览 dashboard（成本时序/模型分布/延迟 P50-P95/错误率，FR-5.1）、`PricingPanel`（FR-3.5/FR-5.4 未配单价显式列出 + 去配单价入口）、`BudgetPanel`（FR-3.6 超支横幅）、**`TracePanel`（FR-5.2 瀑布，形态改为插件内页签——宿主前端不能 import 插件组件，铁律4）**、`http.ts`（自取 `localStorage['forge_api_token']`）。样式仅 `--el-*` + `color-mix()`（FR-5.5）。产物契约：`web/dist/index.js` + `style.css`，导出名 == `views[0]`。

### B. 前端（`ForgeSelf.Web/`）——**按输入N+4 收缩为「SSOT 登记 + dsh 客户端」两处**

- file: `ForgeSelf.Web/src/data/features.ts`
  reason: **前端功能 SSOT**（AGENTS.md §2.3），登记动作本质属于宿主清单 ⇒ **不迁插件**。新增 `cost-scope`（或 `llm-observability`）条目 + `signals` 指向**插件视图与插件控制器**，供 `scripts/check-features.mjs` 双向校验（AC10）。

- file: `dsh-ui-bundle/my-dsh-activity-timeline-client/src/panel.tsx`
  reason: FR-5.3 接真实数据。实测 `:53-58` `MOCK_ENTRIES`、`:145` `entries ?? MOCK_ENTRIES`、`:28` footer 自陈 mock、`:137-143` props 已预留 `[key: string]: unknown` 注入位。**props shape 不变**（U-7）。独立客户端仓，与本拍板无关，不动。

- ~~file: `ForgeSelf.Web/src/views/LlmObservabilityView.vue`~~ / ~~`src/components/chatrecords/TraceWaterfall.vue`~~ / ~~`src/services/llmObservabilityApi.ts`~~ / ~~`src/router/index.ts`~~ / ~~`src/components/chatrecords/ChatRecordDetail.vue`~~（**2026-10-05 输入N+4 取消**）
  reason: 界面归插件（`plugin-development` 铁律3），且宿主前端**不能 import 插件组件**（铁律4）⇒ dashboard/瀑布/api 层全部进 `Plugins/CostScope/web/`（§A2），路由与菜单由 `plugin.json frontend` 声明（铁律19①）。**连带结果**：FR-5.2 的「挂进 `ChatRecordDetail.vue`」改为插件内 trace 页签，宿主该文件**零改动**（AC11 的 Forbidden 逐路径核对据此更新）。

### C. 测试（**按输入N+4 落位：插件业务测试进 `Plugins/CostScopeTests/`**）

> 仓内既有形态（实测目录）：`ForgeSelf.Api.Tests/Plugins/McpCenterTests/`、`Plugins/DesignSystemTests/` —— 插件业务测试仍住在 `ForgeSelf.Api.Tests`，按插件建子目录，并在 `ForgeSelf.Api.Tests.csproj` 加一条插件 `ProjectReference`（该文件已有 **16** 条同类，实测 grep 计数）。**沿用此形态，不另起测试工程。**

- file: `ForgeSelf.Api.Tests/Plugins/CostScopeTests/CostCalculationServiceTests.cs`（新建，**A3**）
  reason: AC3 纯函数锁死（1M token 基准 / 小数精度 / 0 tokens / 负值拒绝 / 改价后重算 / 未配单价 Unknown 不计 0）。原 `Unit/CostCalculatorTests.cs` 随被测类一起改名换目录，**判据内容不变**。
- file: `ForgeSelf.Api.Tests/Plugins/CostScopeTests/ModelPriceResolverTests.cs`（新建，A4）
  reason: 四级回退 + 未归属不猜（BC-3/BC-5）。
- file: `ForgeSelf.Api.Tests/Plugins/CostScopeTests/CostAggregationServiceTests.cs`（新建，A5/A6/A7）
  reason: AC2/AC5/AC1 覆盖度、未配单价不计总额、分位数与手算一致（原 `UsageAggregatorTests` 改名换目录）。
- file: `ForgeSelf.Api.Tests/Plugins/CostScopeTests/TraceProjectionServiceTests.cs`（新建，A10）
  reason: AC4 waterfall 时序（原 `TraceAggregatorTests` 改名换目录）。
- file: `ForgeSelf.Api.Tests/Plugins/CostScopeTests/CostControllerAuthTests.cs` + `CostControllerEndpointTests.cs`（新建，A11）
  reason: AC8 无 token **401** + 非法 `by=` **400**；鉴权特性用反射断言（先例 `McpAdminAuthTests`，铁律17）。原 `Integration/LlmObservabilityControllerTests.cs` 合并至此。
- file: `ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj`（改）
  reason: 追加 `..\Plugins\CostScope\CostScope.csproj` 的 `ProjectReference`（无它则测试程序集看不到被测类型）。⚠️ 此举令本批触发 **§5.6 中档**（碰宿主工程 + 共享测试基建）⇒ 收口前跑后端**全量** `dotnet test`。
- file: `Plugins/CostScope/web/src/**/*.spec.ts`（新建，A12）
  reason: 插件前端单测（vitest，先例 DesignSystem）。原计划的宿主 `ForgeSelf.Web/src/views/LlmObservabilityView.test.ts` **随视图迁到插件侧**。
- file: `ForgeSelf.Web/e2e/plugins/cost-scope/cost-scope.spec.ts`（新建，A12/A13）
  reason: 插件层 e2e（零 mock、真实前后端，按 `e2e-testing`），替代原 `e2e/llm-observability.spec.ts`；含 AC6 空态反例探针与 AC7 展示值与端点逐项一致。
- file: `ForgeSelf.Web/e2e/menu-route-consistency.spec.ts`（改，A12）
  reason: 铁律19③——插件 route 新增必须同步该回归。

## Implementation Steps

> 每个步骤结束跑一次对应门禁；**Plan 与仓库实际不符 → 先记「Plan 偏差记录」再改 Plan**（规范 §2 Stage 5.7）。

1. **T0 前置校验（不可跳过）**
   - 复跑 U-3 裁决结论确认（`ChatTurn` 加列是否获批）。
   - 读 `archive/032-cost-scope/design.md` §12 D1~D10（032 已并入本 pilot），确认成本口径**不冲突**（尤其 D2 纯函数、D5 未配单价显式、D10 单位）。
   - 确认 U-4（单价来源）已随 032 并入确定：`ITurnTelemetryQuery`（Abstractions 契约）+ `CostModelPrice` 表（CostScope 插件自有库，本 pilot 读写皆在插件内，见 FR-3.9），**价格来源不得硬编码**。

2. **T1 成本纯函数（可与 T3 并行）**〔= 原子任务 **A3**，2026-10-05 按输入N+4 改落插件侧〕
   - 新建 `Plugins/CostScope/Services/CostCalculationService.cs`：`CalculateCost(promptTokens, completionTokens, inputPricePer1M, outputPricePer1M)`，`decimal(18,8)`，零 IO；未配单价 → `Unknown`（不返回 0）。
   - 新建 `ForgeSelf.Api.Tests/Plugins/CostScopeTests/CostCalculationServiceTests.cs`：**先把测试写红再实现**（用户铁律：复现 bug/缺口的测试必须断言正确行为让测试失败以暴露缺陷）。
   - 前置：`ForgeSelf.Api.Tests.csproj` 加 CostScope `ProjectReference`；`ForgeSelf.Api.csproj` 补构建顺序引用（A2 缺口）。
   - 门禁：`dotnet build ForgeSelf.Api`（含插件）+ `dotnet test --filter CostScope`。

3. **T2 价目表与解析**〔= A4/A5〕
   - `Plugins/CostScope/Services/ModelPriceResolver.cs` 四级回退（数据源 = `ITurnTelemetryQuery` 扩出的模型目录投影 + 插件 `CostModelPrice`）；`CostScopeTests/ModelPriceResolverTests.cs`。
   - ⚠️ 开工前先扩 Abstractions 契约（A1 产物无 `GetModelCatalogAsync`/`ModelIdentityDto`，见偏差表）。
   - 门禁：`dotnet test --filter CostScope`。

3b. **T1b 成本插件（阶段0，并入自 032）**〔**A1/A2 已完成 2026-10-05**；其余并入 T1/T2/T3/T5/T6，不再单列〕
   - ✅ 已完成：`ForgeSelf.Abstractions/ITurnTelemetryQuery.cs` + `TurnTelemetryDtos.cs`（A1，6/6 契约测试）；`Plugins/CostScope/{plugin.json,CostScope.csproj,CostScopePlugin.cs}` + `Data/Model.xml` → `xcode` 4 表 8 件实体 + 宿主 `XCodeConfig.PluginDbs` 登记（A2）。
   - ⏳ 待补（本批缺口，见偏差表最后一行）：`ForgeSelf.Api.csproj` 缺 CostScope 构建顺序引用 ⇒ 宿主构建/发布产物拿不到该插件 DLL。
   - ❌ 取消（与输入N+4 冲突的旧写法）：「宿主 `TurnTelemetryQueryService.cs` 承担成本/聚合逻辑」「14 端点落在宿主控制器」——宿主实现**只做取数**，业务在插件。

4. **T3 只读聚合**〔= A5/A6/A7 + A10 的数据面，落插件〕
   - `Plugins/CostScope/Services/CostAggregationService.cs`：**入参是 `ITurnTelemetryQuery` 返回的只读 DTO**（宿主实现负责读 `ChatTurn`/`SessionEventEntity`/`UsageRecord`，插件不直连宿主库，FR-4.6）；按 模型/供应商/日/风格 聚合；失败轮次不计成本（BR-4）。
   - `CostScopeTests/CostAggregationServiceTests.cs`：AC2（未配单价显式且不计总额）、AC1（覆盖度 N/M）、AC5（P50/P95/P99 与手算一致）。

5. **T4 trace 关联**〔= A9 + A10〕
   - `ForgeSelf.Api/Entities/Model.xml` 加两列 → `xcode Model.xml`（铁律 9，**禁手改生成物**）。
   - Agent 执行路径（`Plugins/AIAgent/Services/ReactLoopAgent.cs`）在调 LLM 处写入 `ChatTurn.AgentRunId`。
   - `Plugins/CostScope/Services/TraceProjectionService.cs` + `CostScopeTests/TraceProjectionServiceTests.cs`（AC4）。
   - ⚠️ 若 U-3 未批准加列 → **退回 U-3a 替代方案**（`SessionId`+时间窗近似关联），并在页面标注「关联为近似」。

6. **T5 端点**〔= A11，落插件〕
   - `Plugins/CostScope/Controllers/CostController.cs`：**类级 `[Authorize("ApiKeyPolicy")]`**（铁律 17）；未知 `by=` 返回 400（BC 禁静默空表）；路由前缀 `api/cost-scope/...`。
   - 服务注册在 `CostScopePlugin.Apply` 内（插件侧 DI），宿主 `AppBuilder.cs` **只**保留 `ITurnTelemetryQuery` 一条。
   - 门禁：`dotnet build ForgeSelf.Api` + `dotnet test --filter CostScope`；插件控制器要随宿主加载生效（`StageAllPlugins` 产物含 `Plugins/CostScope/CostScope.dll`）。

7. **T6 前端**〔= A12，落插件 `web/`〕
   - `Plugins/CostScope/web/`：dashboard（成本/延迟/错误率/模型分布）+ `PricingPanel`（未配单价**显式列出 + 去配单价入口**，不显示 ¥0.00 假象）+ `BudgetPanel`（超支横幅）+ `TracePanel`（瀑布；**不做宿主挂载**，见偏差表）+ `http.ts`。
   - 骨架按 `plugin-frontend-scaffold`；产物 `web/dist/index.js` + `style.css`，导出名 == `plugin.json` 的 `views[0]`。
   - 宿主侧只登记 `features.ts`（SSOT）；菜单/路由由 `plugin.json frontend` 声明（铁律19①），并同步 `e2e/menu-route-consistency.spec.ts`（铁律19③）。
   - 样式**仅 `--el-*` + `color-mix()`**（NFR-6）。
   - 门禁：`cd Plugins/CostScope/web && pnpm run build`（沙箱内改用 `plugin-development` §3.2 出树构建兜底）+ 宿主 `pnpm run check`。

8. **T7 dsh timeline 接真实数据**
   - `panel.tsx` 把 `MOCK_ENTRIES` 换成真实数据源；**props shape 不变**（U-7）。
   - **反例探针必做**（AC6）：切到空会话必须显示空态，**若仍显示 mock 4 条则判红**。

9. **T8 e2e**
   - `ForgeSelf.Web/e2e/plugins/cost-scope/cost-scope.spec.ts`（插件层 e2e 目录约定）：真实登录、禁 mock。
   - ⚠️ **显式指定端口**：`E2E_FRONTEND_PORT=7402 E2E_BACKEND_PORT=7502`（本回合日记已记：并行会话共用机器时端口争抢会致 `ERR_CONNECTION_REFUSED`）；跑前按 AGENTS.md §5.0 设 `NO_PROXY` 与 `TEMP/TMP`。

10. **T9 文档 + 门禁收口**
    - `docs/02-features/` 功能文档；`not-taken-decisions.md` 记「不做事」决策（如阶段3 质量评估不做）。
    - 跑 `scripts/verify-pilot-artifacts.ps1`（05/06/07 齐备后 PASS）。

## Test Plan

| # | 测试 | 类型 | 判据要点 |
| --- | --- | --- | --- |
| 1 | `CostCalculationService`（插件）单位换算 | Unit | 1M token 基准、小数精度、0 tokens、负值拒绝、改价后历史重算 |
| 2 | `ModelPriceResolver`（插件）四级回退 | Unit | 未命中归「未归属」，**不按前缀硬切** |
| 3 | `CostAggregationService`（插件）成本/分位/覆盖度 | Unit | 未配单价不计总额且显式列出；P50/P95/P99 与手算一致 |
| 4 | `TraceProjectionService`（插件）waterfall | Unit | 含 LLM 节点 + tool 节点，耗时非负、按时间排序 |
| 5 | `CostController`（插件） | Integration | 无 token **401**；非法 `by=` **400**（不静默空表）；反射断言类级鉴权特性（先例 `McpAdminAuthTests`） |
| 6 | `Plugins/CostScope/web` 视图 | Component（插件侧 vitest） | 展示值与端点返回逐项一致 |
| 7 | dsh timeline | e2e | 真实会话条目数一致；**空会话显示空态**（反例探针） |
| 8 | dashboard（插件界面，经宿主加载） | e2e | 成本/延迟/错误率与后端逐项一致（禁 mock） |
| 9 | **回归**：宿主 `ChatRecordDetail` 既有展示 | Unit/e2e | 输入N+4 后**宿主该文件零改动** ⇒ 判据降为「未被本批触碰」（`git diff` 空） |

**验证纪律**：e2e 禁 mock、真实登录；禁手写一次性 `temp/*.cjs`（AGENTS.md 红线）；断言必须**能红**——每个新判据都要有反例探针记录（用户铁律）。

## Verification

### Build

```bash
cd ForgeSelf.Api && dotnet build
cd ForgeSelf.Abstractions && dotnet build
```

### Unit Test

```bash
cd ForgeSelf.Api.Tests && dotnet test --filter "CostCalculator|ModelPriceResolver|UsageAggregator|TraceAggregator|LlmObservability"
cd ForgeSelf.Web && pnpm run check && pnpm run test
```

### Integration Test

```bash
cd ForgeSelf.Api.Tests && dotnet test --filter "LlmObservabilityControllerTests|ChatRecordsControllerIntegrationTests"
# ChatTurnStreamRecorderTests 一并跑（FR-1/FR-2 触及录制的回归面）
cd ForgeSelf.Api.Tests && dotnet test --filter "ChatTurnStreamRecorderTests|ChatTurnServiceTests"
```

### E2E

```bash
cd ForgeSelf.Web
E2E_FRONTEND_PORT=7402 E2E_BACKEND_PORT=7502 npx playwright test e2e/llm-observability.spec.ts --workers=1
```

### Other Checks

```bash
# ① features SSOT 双向校验（AC10）
node scripts/check-features.mjs
# ② 工件链门禁（05/06/07 齐备后）
pwsh scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-03-llm-observability
# ③ 前端 token 合规（0 自定义 token）
cd ForgeSelf.Web && node scripts/check-theme-tokens.mjs   # 若存在该脚本
```

## Plan 偏差记录

> 实现中发现 Plan 与仓库实际不符时，先在此记录偏差，再修正 Plan，不得直接绕过。
> **以下为 Stage 3 读码期间已发现的偏差（非实现期），已同步修正到本 Plan 与 02-spec。**

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 2026-10-03 Stage 3 | **U6「一行透传」不成立**：`ChatController` 注入 `IAIService`（`ChatController.cs:25`），其 `ChatAsync` 返回 `Task<string>`（`AIService.cs:20-31`）**签名即无 usage**；`ILlmRuntime` 只有 `StreamAsync` 且 legacy 适配器**已知有损**（`AIServiceLlmRuntime.cs:19-20` 原文「无法取到用量」） | 032 design §15 U6 与本任务初稿均写「只差一行透传」 | **更正**：修复需把 `ChatController` 迁到带 usage 的 **Unified 面**（`IAIProvider.ChatAsync`→`UnifiedChatResponse.Usage` / `IChatCompletion`）= **接缝迁移**，非一行。U-2 选项重写为 (a) 迁移 /(b) 双源聚合 /(c) 显式标注；**默认建议 (c) + 另立任务做 (a)** |
| 2026-10-03 Stage 3 | U6 断点**是两处**（`:131` 与 `:216`），032 §15 U6 只记 `:131` | 032 记一处 | 本任务更正为两处；`FR-1.4`/`AC1` 均按两处表述 |
| 2026-10-03 Stage 3 | 加列须走 `Model.xml` → `xcode`（铁律 9），不能只改 `ChatTurn.cs` | 初稿只写改 `ChatTurn.cs` | Files To Change 补 `Model.xml` + 三个生成物文件，并注明**禁手改生成物** |
| 2026-10-03 Stage 3 | `AgentRun` 已有 `AgentId`/`SessionId`/`StepCount`/`TotalTokens`（实测 `AgentRun.cs:41-145`） | 初稿拟给 `AgentRun` 加列 | **不加** `AgentRun` 列，复用既有字段（减小 DB 变更面，利于 U-3 裁决） |
| **2026-10-05 输入N+4（用户拍板）** | **同一「纯函数成本引擎」在本 Plan 里有两处落位**：`§A:25 ForgeSelf.Api/Services/AI/CostCalculator.cs` 与 `§A2:59 Plugins/CostScope/Services/CostCalculationService.cs` —— 032 并入 033 时把两边的文件清单同时抄了进来，形成**双真相**（我上一轮向用户报为该缺口） | 两套宿主/插件并存的清单 | **用户裁定「以插件为主，宿主侧的也迁移到插件」** ⇒ 成本/解析/聚合/trace/端点/界面**唯一落位 `Plugins/CostScope/`**；`ForgeSelf.Api/Services/AI/{CostCalculator,ModelPriceResolver,UsageAggregator,TraceAggregator}.cs` 与 `ForgeSelf.Api/Controllers/LlmObservabilityController.cs` **全部取消**（见下 §A 改写）。**唯一留在宿主的**＝`ITurnTelemetryQuery` 的只读实现 `TurnTelemetryQueryService.cs`（原因见下一行，有实测依据，非我偏好） |
| 2026-10-05 实测 | **插件程序集不能引用宿主**：`ForgeSelf.Api/ForgeSelf.Api.csproj:106-130` 用 `ReferenceOutputAssembly="false"` 只建构建顺序，注释原文「避免插件类型进入默认 ALC 造成与 PluginLoadContext 的双重加载」；实测 **19 个插件 csproj 的 `ProjectReference` 一律只有 `ForgeSelf.Core` + `ForgeSelf.Abstractions`**，零个引用 `ForgeSelf.Api`。而 `ChatTurn`/`SessionEventEntity` 是 `ForgeSelf.Api/Entities/` 下的 XCode 实体 ⇒ 插件代码**编译期就拿不到** | 「把宿主服务整体搬进插件」听起来一句话 | **搬得动的是业务，搬不动的是取数**：聚合/解析/成本/端点/界面入插件；插件经 **Abstractions 契约 + DI** 取宿主数据。仓内已有同构先例（实测）：`IUsageStatsService`（Abstractions）→ 宿主实现注册 `AppBuilder.cs:192 AddScoped<IUsageStatsService, UsageStatsService>` → 插件消费 `Plugins/DevTools/DevToolsPlugin.cs:265`、`Plugins/MemorySystem/MemorySystemPlugin.cs:356`、`Plugins/ScriptRunner/Services/CodeSnippetToolFunctions.cs:360` |
| 2026-10-05 实测 | **A4 的取数契约有缺口**：`ModelPriceResolver`（迁插件）要读 `AIModel.ChatModelId/UpstreamModelId/Alias/ProviderName` 与 `AIProvider`，但实读 A1 产物 `ForgeSelf.Abstractions/ITurnTelemetryQuery.cs` **只有三个方法**（`GetTurnTelemetryAsync`/`QueryTurnsAsync`/`GetAgentRunTelemetryAsync`），`TurnTelemetryDtos.cs` 也**没有** 032 提过的 `ModelIdentityDto` | 03-plan `:50` 写「DTO（TurnTelemetryItem/Page、ToolUsageItem/Page、ModelIdentityDto）」 | **A1 产物与本 Plan 表述不符**（我上一轮把 032 的 DTO 清单抄进了 Plan，未与 A1 实际产物对账）。处置：A4 开工前**先扩契约**（`ITurnTelemetryQuery` 增 `GetModelCatalogAsync` + `ModelIdentityDto`），A3 不受影响；已把该项挂到 A4 卡片前置，不再当作既有能力 |
| 2026-10-05 输入N+4 | **前端界面归插件**（`plugin-development` 铁律3「界面归插件，不归宿主」）+ 宿主前端**不能 import 插件组件**（铁律4） | `§B`：宿主新建 `LlmObservabilityView.vue`/`TraceWaterfall.vue`/`llmObservabilityApi.ts` + `router/index.ts` 新增路由，并把瀑布**挂进宿主 `ChatRecordDetail.vue`** | 界面全部改落 `Plugins/CostScope/web/`（dashboard/单价/预算/**trace 页签** + `http.ts`），入口由 `plugin.json frontend` 声明（铁律19①）；**FR-5.2 的挂载点从「宿主 ChatRecordDetail 内嵌」改为「插件内 trace 页签」**，宿主 `ChatRecordDetail.vue` 因此**零改动**；`ForgeSelf.Web/src/data/features.ts` 登记**留在宿主**（SSOT 本质是宿主清单，AC10 不变） |
| 2026-10-05 输入N+4 | **A2 交付有构建图缺口**：`ForgeSelf.Api.csproj` 的 17 条插件 `ProjectReference` 里**没有 CostScope** ⇒ `dotnet build ForgeSelf.Api` 不会构建该插件，`publish`/CI 产物里 `Plugins/CostScope/` 只有 `plugin.json` 无 DLL；A2 当时手工 `dotnet build Plugins/CostScope/CostScope.csproj` 拿到了 0 错误，**恰好掩盖了这一点** | A2 只登记了 `XCodeConfig.PluginDbs` | 本批补 `ForgeSelf.Api.csproj` 一行 `ReferenceOutputAssembly="false"`（与 `:127` DesignSystem「此前缺此行 → Stage 复制不到 DLL（MSB3030）」同病同源），判据改为**从宿主构建产物里读到 `$(OutDir)Plugins/CostScope/CostScope.dll`**，而非插件目录自建 |
| **2026-10-06 实测（A3b 实施时）** | **`AgentRun`/`AgentStepRun` 不在宿主库**：实读 `ForgeSelf.Api/Entities/` 只有 `ChatTurn`/`SessionEventEntity`/`UsageRecord`/`AIModel`/`AIProvider`，`find -iname "AgentRun*.cs"` **零命中**——它们是 **AIAgent 插件自有库**的表。而 §A 写「`TurnTelemetryQueryService` 读 ChatTurn/SessionEvent/**AgentRun**+AgentStepRun/UsageRecord」，宿主编译期根本拿不到 | 宿主实现直接读 AgentRun | **把 AgentRun 这一段从宿主实现里外置**：新增 `ForgeSelf.Abstractions/IAgentRunTelemetryProvider`（由持有数据的 AIAgent 插件实现并注册）；宿主 `TurnTelemetryQueryService` 构造注入**可选**的该接口，**未注册时按契约返回 `null`**（契约原文即「无对应运行返回 null」，不伪造空对象）。A10 起由 AIAgent 插件侧注册实现。另：`ChatTurn` 无 `AgentRunId` 列（A9/U-3 未批）⇒ 投影里 `AgentRunId` 恒为 null，不伪造（FR-1.4） |
