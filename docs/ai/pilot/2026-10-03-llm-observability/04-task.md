# Agent Task

> 阶段：Stage 4｜把任务变成 **Agent 可以直接执行的工作单元**，零自我决策空间。
> 前序工件：00-repository-understanding / 01-intent / 02-spec / 03-plan 齐备。**当前状态：待闸门1 批准，未批不得执行。**

## Task ID

PILOT-033（统一 LLM 可观测性，工件目录 `2026-10-03-llm-observability`）

> **2026-10-05 整合**：**PILOT-032 CostScope 已并入本 pilot**（032 三份 spec 归档入 `archive/032-cost-scope/` 并删除 `specs/032-cost-scope/`）。本 pilot 现为 **cost + trace + viz 统一闭环**，成本阶段0（单价目录 CRUD / 纯函数成本引擎 / 惰性物化日汇总 / 预算 / 模型解析）纳入范围（详见 02-spec FR-3 / 03-plan A2 / 本文件 Scope）。

## Objective

在**不引入新依赖、不写宿主库、只读聚合**的前提下，补齐 LLM 可观测性的三处已确认真空缺：**① 成本归因**（token→金额，按模型/供应商/日/风格）、**② trace 关联**（`ChatTurn`↔`AgentRun`，可回放一次 Agent 运行的调用瀑布）、**③ LLM 专项可视化**（成本/延迟/错误率/模型分布 dashboard + dsh activity-timeline 接真实数据），并让用户**不被假数据与不完整口径误导**。

## 闸门1 待裁决项（未获答复前，对应任务单元**不得执行**）

| # | 裁决点 | 建议 | 阻塞的任务单元 |
| --- | --- | --- | --- |
| **U-3** | `ChatTurn` 加 `AgentRunId`/`TraceId` 两列 = **DB 结构变更**（规范 §1 硬性约束 2，须升级审批） | 批准（关联准确）；被拒则走 U-3a 近似方案并标注 | **T4** |
| **U-2** | usage 缺口修法：(a) `ChatController` 迁 Unified 面（**接缝迁移，有回归风险**）/ (b) 双源聚合 / (c) 显式标注 | **(c) 本轮 + 另立任务做 (a)** | **T1** |
| ~~U-1~~ | 本轮是否含 032 的成本引擎/单价目录/预算 | **已并入**：032 已合入本 pilot，成本阶段0 纳入范围（见 FR-3），不再待裁决 | — |
| ~~U-4~~ | 模型单价来源契约落位 | **已确定（沿用 032 A）**：`ITurnTelemetryQuery`（Abstractions）+ `CostModelPrice`（CostScope 插件自有库），不得硬编码 | — |

## 原子任务分解（A1–A13 **+ A3b**，共 14 项）

> 把统一 pilot 视作一次迭代，继续拆成**原子任务**（单一关注点 / 独立可验证 / 单 commit 范围 / 零跨切风险）。依赖方向单向（DAG），根节点无前置；被 U-2 / U-3 阻塞的任务单独成块。
>
> **2026-10-06 全量拆解完成**：14 项**全部**有对应原子卡片（目标 / 落位 / Allowed / 范围外 / AC / 验证 / 依赖·风险 / 对应条款），不等执行时再补。**A3b 为本次拆解补立**——宿主 `TurnTelemetryQueryService` 取数实现此前只作为 A10 的依赖项被提及、无归属任务。
>
> **落位口径（2026-10-05 输入N+4 用户拍板「以插件为主，宿主侧的也迁移到插件」）**：下表凡标 **【插件】** 者，实现落 `Plugins/CostScope/`；宿主只保留 ① 只读取数接缝（`ForgeSelf.Api/Services/CostScope/TurnTelemetryQueryService.cs` + `AppBuilder` 一行 DI）② `ChatTurn` 新列（A9，宿主库结构搬不动）③ `features.ts` 登记（SSOT 属宿主）。依据见 03-plan「Plan 偏差记录」2026-10-05 各行（实测：19 个插件 csproj 只引用 Core+Abstractions）。

| ID | 原子任务 | 落位 | 阻塞 | 依赖 | 状态 |
| --- | --- | --- | --- | --- | --- |
| **A1** | 遥测只读契约 `ITurnTelemetryQuery` + DTO | Abstractions（契约天然在共享层） | — | 根 | ✅ 完成（2026-10-05，`dotnet test` 6/6） |
| **A2** | CostScope 插件骨架（plugin.json / CostScopePlugin / Model.xml 4 表 / DI 注册） | 【插件】+ 宿主 `XCodeConfig.PluginDbs` | — | A1 | ⚠️ 骨架完成，**构建图缺口本批补**（宿主 csproj 缺 CostScope 引用，见 A2 卡片更正） |
| **A3** | 纯函数成本计算器 `CostCalculationService`（无 DB，单测锁死） | 【插件】 | — | A1 | ✅ 完成（2026-10-05，`dotnet test --filter CostCalculationService` **17/17**，`.temp/a3-final.log`；两条反向探针实红见 A3 卡片） |
| **A3b** | **宿主只读取数实现 `TurnTelemetryQueryService`**（契约的宿主侧实现 + DI 一行） | 宿主（XCode 实体在宿主，插件编译期拿不到） | — | A1 | ✅ 完成（2026-10-06，`dotnet test --filter TurnTelemetryQueryService` **13/13**；只读守卫反向探针实红 1 条，还原复绿；见 A3b 卡片「验证口径」） |
| **A4** | 模型→供应商 4 级解析 `ModelPriceResolver` | 【插件】 | — | A1 + **A3b**（**前置：先扩契约** `GetModelCatalogAsync` + `ModelIdentityDto`） | ✅ 完成（2026-10-06，`dotnet test --filter CostScopeTests` **33/33** = A3 17 + A4 16；反向探针「前缀硬切」⇒ **2 红**，还原复绿，见 A4 卡片） |
| **A5** | 单价目录 CRUD（`CostModelPrice` + `PriceCatalogService`，**端点并入 A11**） | 【插件】 | — | A2 + **A3b** | ✅ 完成（2026-10-06，`--filter CostScopeTests` 含本项 **23/23**；全量 69/69；静默覆盖反向探针实红后复绿） |
| **A6** | 预算规则（`CostBudget` + `BudgetService`，**端点并入 A11**） | 【插件】 | — | A2/A3 + **A3b**（可与 A7 并行） | ✅ 完成（2026-10-06，**52/52**；全量 121/121；剩余额度截断探针实红 2 条后复绿） |
| **A7** | 只读聚合 + 惰性日汇总物化（`CostAggregationService` + `CostTurnDaySummary` + `TelemetryProjectionService`） | 【插件】 | — | A2/A3 + **A3b** + A4/A5 | ✅ 完成（2026-10-06：聚合 24 + 物化 12，**36/36**；全量回归 **161/161**；契约 6/6；两条反向探针实红后复绿） |
| **A8** | Usage 接缝迁移（`ChatController`→Unified 面） | 宿主（**主聊天链路本体，搬不动**） | **U-2 已裁决 = (c)** | — | 🚫 **本轮不做**（2026-10-06 用户裁决：按「功能一律插件化、主流程只调插件实现」的方向，**不迁宿主接缝**，重复实现可后置 ⇒ 转为 FR-1.1/1.2 显式标注 + 独立 TODO） |
| **A9** | ~~Trace FK（`ChatTurn.AgentRunId` + `TraceId` 加列）~~ | — | ~~U-3~~ | — | 🚫 **已取消**（2026-10-06 用户裁决：宿主保持抽象，**插件消费宿主已产出的数据**做关联，不给宿主加列） |
| **A10** | Trace 关联 + waterfall 聚合（**插件侧**用宿主轮次 + AIAgent 的 AgentRun 关联） | 【插件】 | — | A3b（宿主取数）+ AIAgent 侧 `IAgentRunTelemetryProvider` | ✅ 完成（2026-10-06，trace **13/13**；全量回归 **180/180**；关联判定探针实红 5 条后复绿） |
| **A11** | 查询端点（插件 `CostController`，类级 `[Authorize("ApiKeyPolicy")]`）+ 集成测试 | 【插件】 | — | A10 + A3/A4 | ✅ 完成（2026-10-06，**18 端点 / 31 条单测**；全量回归 **211/211**；非法 `by=` 探针实红 3 条后复绿） |
| **A12** | 可视化仪表盘 + 单价/预算/trace 页签（插件自带 `web/`） | 【插件】 | — | A11 | ✅ 完成（2026-10-06，**9/9 守卫测试**；全量回归 **220/220**；端点边界探针实红后复绿）。**注**：`features.ts` 本仓不存在，视图注册走 `plugin.json` 的 `frontend.views` |
| **A13** | ~~dsh 时间线接真实数据（panel.tsx 换数据源）~~ | — | — | — | 🚫 **取消（2026-10-06 用户澄清：`dsh-ui-bundle` 是独立客户端，与本 pilot 功能无关）** |

### A1 原子卡片（✅ 已完成 · 2026-10-05）

> 验证证据（Verified，非 Inferred）：`ForgeSelf.Abstractions` 编译通过；`dotnet test --filter "FullyQualifiedName~TurnTelemetryContract"` → **6/6 通过 / 0 失败 / 109ms**。新增文件：`ITurnTelemetryQuery.cs`、`TurnTelemetryDtos.cs`、`ForgeSelf.Abstractions.Tests/TurnTelemetryContractTests.cs`。未改任何既有文件、未触碰 DB。

**目标**：在 `ForgeSelf.Abstractions` 定义成本与 trace 共用的**只读**遥测契约，作为整个 pilot 的数据访问 seam。本任务**不引入任何运行时行为变更**，仅为下游（A2 插件实现、A3 计算、A10 聚合）提供稳定形状。

**范围（Allowed · 仅新增）**
- `ForgeSelf.Abstractions/ITurnTelemetryQuery.cs`（接口：聚合 `ChatTurn` + `SessionEvent` + `AgentRun`/`AgentStepRun` + `UsageRecord` 的只读投影）
- `ForgeSelf.Abstractions/TurnTelemetryDtos.cs`（DTO：`TurnTelemetryRecord` / `AgentRunTelemetry` / `AgentStepTelemetry` / `TurnTelemetryQueryFilter`，全部 `record` 不可变）
- `ForgeSelf.Abstractions.Tests/TurnTelemetryContractTests.cs`（xUnit：字段映射 + 架构边界守卫）

**范围外（明确不做）**
- 不实现该接口（实现留 A2 CostScope 插件侧）
- 不改任何现有调用方、`ChatController`、`SessionEvents.cs`
- 不含成本计算逻辑（留 A3）、不含 DB 变更

**验收（AC）**
- **AC1** `dotnet build ForgeSelf.Abstractions` 绿，契约与 DTO 编译通过。
- **AC2** DTO 字段覆盖 02-spec FR-3.9 列清单（`SessionKey`/`Style`/`Model`/`PromptTokens`/`CompletionTokens`/`DurationMs`/`CreatedTime` + `AgentRunId` 关联键），且全部为不可变 `record`。
- **AC3** 单元契约测试：构造样本 DTO，断言聚合投影字段映射正确（数据驱动，从 SSOT 加载断言值，非硬编码）。
- **AC4** 架构边界守卫测试：`ForgeSelf.Abstractions` 程序集不反向引用 `ForgeSelf.Api` / `Plugins.*`（无循环依赖）。

**验证**：`dotnet build ForgeSelf.Abstractions` + `dotnet test --filter "FullyQualifiedName~TurnTelemetryContract"` 绿。
**依赖 / 风险**：无前置 / **L1**（纯新增、零侵入）。
**对应 pilot 条款**：02-spec FR-3.9、03-plan §A2、本文件 Scope「Allowed 后端」首两项。

### A2 原子卡片（⚠️ 骨架完成 · 构建图缺口本批补 · 2026-10-05）

> **本卡片首版（A2 当日）的自述有两条不实，按实态更正**：
> ① 「`dotnet build Plugins/CostScope/CostScope.csproj` 0 错误」是**手工在插件目录单独构建**拿到的——宿主 `ForgeSelf.Api.csproj:106-130` 的 17 条插件构建顺序引用里**没有 CostScope**，所以 `dotnet build ForgeSelf.Api` 根本不编译该插件，`publish`/CI 产物里 `Plugins/CostScope/` 只有 `plugin.json` 没有 DLL（与 `:127` DesignSystem 注释记录的 MSB3030 同病）。A3 补上该行后，实测**宿主构建产物含 `$(OutDir)Plugins/CostScope/CostScope.dll`**，且与插件目录产物 **md5 相同**（`6ce9dc3c10c7c77c29c0debc1494044a`）。
> ② 「DI 注册完成」不实——`ForgeSelf.Api.Tests.csproj` 也缺 CostScope 的 `ProjectReference`，导致 A3 第一次跑测直接 **error CS0234**（命名空间 `ForgeSelf.Api.Plugins` 中不存在 `CostScope`）。补引用后测试才取到被测类型。
> 其余自述（4 表 Model.xml / xcode 8 件 / `XCodeConfig.PluginDbs` 登记 / 未实现业务）**核对为真**。

**目标**：建立 CostScope 插件的最小可编译骨架，把成本数据的存储契约（4 张表）以 XCode 模型形式落定，并在宿主侧登记插件自有库连接名，使库随插件加载自动建连串/建表。

**范围（Allowed · 仅骨架）**
- `Plugins/CostScope/plugin.json`：插件 Id `cost-scope`、入口 `CostScope.dll`/`CostScopePlugin`、前端视图 `CostScopeView`（route `/cost-scope`，menu 成本观测）、权限 `read_config`/`write_config`。
- `Plugins/CostScope/CostScope.csproj`：net10.0、AssemblyName `CostScope`、引用 NewLife.Core/XCode + `ForgeSelf.Core`/`ForgeSelf.Abstractions`（与 AIAgent 同构）。
- `Plugins/CostScope/CostScopePlugin.cs`：`IPlugin.Apply` → 取 `plugin.json` 的 Id（不硬编码）→ `ctx.EnsurePluginDataDirectory()` → `ctx.Get<IServiceCollection>()?.AddSingleton<CostScopeDbNaming>()`（DI 占位，供 A3–A7 注入成本服务）。附 `CostScopeDbNaming` 常量类（ConnName=`CostScope`、PluginId=`cost-scope`，单一真源）。
- `Plugins/CostScope/Data/Model.xml`：连接名 `CostScope`，命名空间 `ForgeSelf.Api.Plugins.CostScope.Entities`，4 表真源（`CostModelPrice`/`CostBudget`/`CostTurnDaySummary`/`CostSettings`，含索引）。
- `Plugins/CostScope/Data/Entities/*.cs`：经 `xcode Model.xml` 生成（8 件 `.cs` + `.Biz.cs`），**禁手改**（铁律9）；实测 `CostModelPrice` 的 `[BindTable(..., ConnName = "CostScope", ...)]` 连接名正确。
- `ForgeSelf.Api/Data/XCodeConfig.cs`：`PluginDbs` 字典追加 `["CostScope"] = "cost-scope"`，使宿主在 `AddXCode`/`InitializeXCodeDatabase` 中自动建连接串与建表（库文件落 `Plugins/cost-scope/CostScope.db`，与 `ctx.GetPluginDataDirectory()` 同路径）。

**范围外（明确不做）**
- 不实现 `CostCalculator`/`PriceCatalogService`/`BudgetService`/`CostAggregationService`（留 A3–A7）。
- 不新建 `Controllers/CostController.cs`、不建前端 `web/`（留 A11/A12）。
- 不改宿主任何既有表结构、`ChatController`、`ChatTurn`。

**验收（AC）**
- **AC1** `dotnet build Plugins/CostScope/CostScope.csproj` 绿（0 错误）。
- **AC2** `dotnet build ForgeSelf.Api` 绿，**且产物内存在 `Plugins/CostScope/CostScope.dll`**（本条为 A3 补强的判据——原 AC2 只验「0 错误」，验不出插件没进构建图）。
- **AC3** 生成实体连接名 == `CostScope`（与 Model.xml `ConnName`、PluginDbs 键、`CostScopeDbNaming.ConnName` 三者一致，单一真源无漂移）。
- **AC4** 插件 Id `cost-scope` 与 plugin.json 的 `Id`、`CostScopeDbNaming.PluginId`、`XCodeConfig.PluginDbs` 值三者一致。
- **AC5（A3 补）** `ForgeSelf.Api.Tests` 可引用插件程序集（`ProjectReference` 已登记），否则插件业务无法写单测。

**验证**：`dotnet build ForgeSelf.Api` → 0 错误 + `Plugins/CostScope/CostScope.dll` FOUND（md5 与插件目录产物一致，Verified）；`dotnet test --filter CostCalculationService` 能编译并执行 = AC5 成立（Verified）。
**依赖 / 风险**：依赖 A1（契约已就绪，但 A2 不消费它，仅同 pilot 内）；**L1**（纯新增、零侵入宿主结构；本批补的 csproj 两行为构建登记）。
**对应 pilot 条款**：02-spec FR-3.1–FR-3.9、03-plan §A2、03-plan「Plan 偏差记录」2026-10-05 最后一行。

### A3 原子卡片（✅ 已完成 · 2026-10-05 · 落位经输入N+4 拍板改到插件侧）

> **验证证据（Verified）**：`dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~CostCalculationService"`（`DOTNET_CLI_UI_LANGUAGE=en`，`TEMP/TMP` 已按 §5.0 重定向）→ **Passed! Failed: 0, Passed: 17, Total: 17, Duration: 51 ms**（`.temp/a3-final.log`，EXIT=0）。
> **反向探针两条（都实红，证明断言能失败）**：
> ① **MUTATION-A** 计价单位 `TokensPerPriceUnit` 1e6 → 1e3 ⇒ **Failed: 9 / Passed: 8**（读数取自当回合命令输出，未落日志文件——如实标注）；
> ② **MUTATION-B** 把「未配单价」静默计 0（`inputCost = 0m`）⇒ **Failed: 10 / Passed: 7**（`.temp/a3-probeB.log`），红在 `未配单价_判未知_不静默计零`、`零元单价与未配单价_可区分`、`聚合_未知单价不计入总额` 三格——正是 BR-2 的守门用例。两次均还原后复绿。
> **测试抓到的是我自己的算错**：首版金值写 `1000/1e6×2.4 + 2000/1e6×4.8 = 0.0144`，实跑 **Expected 0.0144 / Actual 0.0120**（`.temp/a3-test2.log` Failed:1）——实现是对的，用例注释里的手算错了，已把金值改正为 `0.012` 并补全推导。**不是实现缺陷，记为测试自纠。**
> **本任务净改动（4 个文件）**：新增 `Plugins/CostScope/Services/CostCalculationService.cs`、新增 `ForgeSelf.Api.Tests/Plugins/CostScopeTests/CostCalculationServiceTests.cs`、`ForgeSelf.Api/ForgeSelf.Api.csproj` +1 行构建顺序引用（A2 缺口）、`ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj` +1 行 `ProjectReference`。**未触 DB、未动 `ChatController`/`SessionEvents.cs`、未创建任何宿主侧成本文件（F0 零命中）、未 git commit。**

**目标**：落地 FR-3 的**成本纯函数引擎**——单位换算（每 100 万 tokens）与「未配单价 = 未知」这两条口径由单测完全锁死，作为 A5/A6/A7 聚合与 A11 端点的唯一成本真源。零 IO、零 DB、零行为变更（尚无调用方）。

**落位（2026-10-05 输入N+4 拍板）**：`Plugins/CostScope/Services/CostCalculationService.cs`。宿主侧 `ForgeSelf.Api/Services/AI/CostCalculator.cs` **取消**，不留第二份真相（03-plan 偏差表第 1 行）。

**范围（Allowed）**
- `Plugins/CostScope/Services/CostCalculationService.cs`：`CostInput`（token + 可空双向单价）、`CostAmount`（分段可各自未知；`Total` 在有未知段时是**下界**）、`CostCalculationService.Calculate(...)`、`IsPriceValid`；常量 `TokensPerPriceUnit=1e6` / `CostDecimals=8` / `MaxPricePer1M=1e6`。
- `ForgeSelf.Api.Tests/Plugins/CostScopeTests/CostCalculationServiceTests.cs`：9 个测试方法展开 **17 格**（单位换算 Theory 7 行、负 token Theory 2 行、非法单价 Theory 2 行，其余 6 个各 1 格）。
- `ForgeSelf.Api/ForgeSelf.Api.csproj` / `ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj`：各 +1 行插件引用登记（A2 缺口 + 测试可引用）。

**范围外（明确不做）**
- 不做单价 CRUD（A5）、不做聚合/分位/覆盖度（A5/A6/A7）、不做模型→供应商解析（A4）、不建端点（A11）、不碰 `CostModelPrice` 表读写。
- 不接 `ITurnTelemetryQuery`（本任务零 IO 是判据的一部分）。
- 不改宿主业务代码。

**验收（AC）**
- **AC3-1（对应 pilot AC3）**：1M token 基准、小数精度到第 8 位、0 tokens、负 token 抛、非法单价（负/超界）抛、改价后重算、幂等（无隐藏状态）——全部单测锁死。
- **AC3-2**：未配单价 ⇒ `IsFullyUnknown` 且**不计入总额**（BR-2「绝不默默计 0」）；且「0 元单价」与「未配单价」**可区分**。
- **AC3-3**：单边有价 ⇒ 总额是**下界**（`HasUnknownPart`/`HasKnownPart` 可判），聚合方据此进 `unpricedModels`。
- **AC3-4**：两条反向探针实红（MUTATION-A/B），还原后复绿。
- **AC3-5**：F0 逐路径核对——宿主侧成本文件零创建（`git status --porcelain -- ForgeSelf.Api/Services/AI ForgeSelf.Api/Controllers/LlmObservabilityController.cs` 空输出）。

**验证**：`dotnet test --filter CostCalculationService` **17/17 通过**（`.temp/a3-final.log` 正文）+ 反向探针 A/B 实红（9 红 / 10 红，还原复绿）。
**中档全量（§5.6，碰宿主工程 + 共享测试基建 ⇒ 必跑）**：`dotnet test ForgeSelf.Api.Tests` → **Failed: 16, Passed: 2358, Skipped: 0, Total: 2374, Duration: 16 m 26 s**（`.temp/a3-full-suite.log` 正文，EXIT=1）。
- **CostScope 在全量里零命中**（`grep -ac "CostScope\|CostCalculation"` = 0）⇒ 本批 17 条新用例无红。
- **基线对表（新增的红才是我的）**：16 条红＝`WorkflowPlanningIntegrationTests` 6 + `ScriptRunnerDiIntegrationTests.GetRuntimes` 1（同族，`TODO.md`「插件端点注册时序在 Testing 环境反复显形」已记录；隔离复跑正文 `GET /api/scripts/runtimes - 404`）+ `UpdateServiceTests.ApplyUpdateAsync_*` 8（并行会话 B 轮同族已记为并发/共享 TMP 偶发）+ `Unit.ScriptRunnerTests.CancelAsync_ForRunningScript` 1（**此前无记录，本批现场新定性**：`Expected Status Cancelled{4} but found Timeout{5}`，隔离三连跑 **2 败 1 过** ⇒ 竞态 flake，已入 `TODO.md` P2）。**⇒ 16 条红无一由本批引入**（本批净改动 4 个文件，均不涉及这些被测面）。
- **未验证档位（如实标注）**：宿主前端门禁（`pnpm run check`/`test`）**未跑**——本批零前端改动；**插件层 e2e / 隔离实例走查 / 运行实例只读复验未做**（本批无插件界面与端点，A11/A12 起才成立；且宿主未重启加载新插件）。
- **交付物影响（实测只读）**：用户已升级的 `OpenForgeSelf-2.7.3.2610051746-win-x64.zip` 内 `versions/*/plugins/` = **18 个插件、无 cost-scope** ⇒ A2 构建图缺口确实漏到交付物；本批修复后宿主构建产物已含该 DLL（md5 与插件目录一致），但**未重出新包**，「包里真的带上 CostScope」留待下次发布按 `plugin-publish-verify` 按包验真复验。
**依赖 / 风险**：依赖 A1（同 pilot 语境，本任务不消费契约）；**L1**（纯新增 + 两行构建登记；无调用方、无 DB、无端点）。
**对应 pilot 条款**：02-spec FR-3.1~3.4 / BR-1~BR-2 / BC-1、03-plan §A2 `CostCalculationService`、本文件 AC3。

### A3b 原子卡片（✅ 已完成 · 2026-10-06）

> **验证证据（Verified）**：`dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~TurnTelemetryQueryService"` → **已通过！失败 0 / 通过 13 / 总计 13**（`.temp/a3b-verify2.log`，EXIT=0）；宿主 `dotnet build ForgeSelf.Api` → **0 个错误**。
> **反向探针（MUTATION，实红）**：在实现里加入一个写库方法 `private static int ProbeWrite(ChatTurn t) => t.Save();`（无调用方，不产生真实写入）⇒ **失败 1 / 通过 12**，红在 `实现源码_无写库调用_只读`（AC3b-1/BR-5 的守门用例）；还原后复绿 13/13（`.temp/a3b-mutation.log`）。
> **本任务净改动（5 个文件）**：新建 `ForgeSelf.Api/Services/CostScope/TurnTelemetryQueryService.cs`、`ForgeSelf.Abstractions/IAgentRunTelemetryProvider.cs`、`ForgeSelf.Api.Tests/Services/TurnTelemetryQueryServiceTests.cs`；改 `ForgeSelf.Api/AppBuilder.cs`（+1 行 DI 注册 + 1 行 `using`）。
> ⚠️ **验证口径（如实标注）**：A3b 收口时**并行会话把 `Plugins/McpCenter/Services/DshMcpConfigWriter.cs` 改成编译错误**（`error CS0165: 使用了未赋值的局部变量"newline"`，文件于 00:39:52 被改）⇒ 任何含插件的构建都会失败。为不阻塞本任务验证，改用 `dotnet build ForgeSelf.Api -p:BuildProjectReferences=false` + `dotnet test -p:BuildProjectReferences=false`（**跳过插件构建**）完成验证；该错误**非本任务引入、未修改该文件**。待对方修复后须补跑一次完整 `dotnet build ForgeSelf.Api`。

> ⚠️ **本卡片为补立，非既有编号调整**：宿主 `ForgeSelf.Api/Services/CostScope/TurnTelemetryQueryService.cs` 此前在 DAG 里**只作为 A10 的依赖项被提及**（「A9 + 宿主 `TurnTelemetryQueryService`」），**没有任何原子任务归属它**；而 **A4 需要 `GetModelCatalogAsync`、A5–A7 需要轮次/用量、A10 需要 trace 数据，全部依赖它**。若不单独立卡，实施期会出现「插件业务已就绪但取不到宿主数据」的空转。⇒ 补立 A3b 作为插件侧一切取数的前置。

**目标**：在宿主实现 `ITurnTelemetryQuery`（A1 契约）的**只读**版本，作为插件获取宿主 `ChatTurn` / `SessionEvent` / `AgentRun`+`AgentStepRun` / `UsageRecord` / `AIModel`+`AIProvider` 数据的**唯一合法通道**。宿主只做取数，**不含任何成本/聚合业务**（F0）。

**落位**：宿主 `ForgeSelf.Api/Services/CostScope/TurnTelemetryQueryService.cs` + `AppBuilder.cs` 一行 DI。
> **为什么必须留宿主**（编译期事实，非偏好）：这些 XCode 实体在 `ForgeSelf.Api/Entities/`，而实测 **19 个插件 csproj 只引用 `ForgeSelf.Core` + `ForgeSelf.Abstractions`**，零个引用 `ForgeSelf.Api` ⇒ 插件代码编译期拿不到宿主实体。仓内同构先例：`IUsageStatsService`（Abstractions）→ 宿主实现注册 `AppBuilder.cs:192` → 插件消费（DevTools/MemorySystem/ScriptRunner）。

**范围（Allowed）**
- `ForgeSelf.Api/Services/CostScope/TurnTelemetryQueryService.cs`（新建）：实现 `GetTurnTelemetryAsync` / `QueryTurnsAsync` / `GetAgentRunTelemetryAsync`；注册 **`Scoped`**（与 `AppBuilder.cs:192` 的 `IUsageStatsService` 同形）。
- `ForgeSelf.Api/AppBuilder.cs`：**仅** +1 行注册上面这条接缝。
- `ForgeSelf.Abstractions/ITurnTelemetryQuery.cs` + `TurnTelemetryDtos.cs`：配合 **A4 前置**扩 `GetModelCatalogAsync` + `ModelIdentityDto`（归属见 A4 卡片；本任务保证契约与实现签名一致）。

**范围外（明确不做）**
- 不含成本计算（A3）、不含聚合/分位/覆盖度（A7）、不含 trace 组装（A10）、不建端点（A11）。
- 不写宿主库（F1）；不改 `ChatController`（F5）、不改 `SessionEvents.cs` record（F6）。

**验收（AC）**
- **AC3b-1**：三个既有方法 + `GetModelCatalogAsync` 全部有宿主实现，且实现**只读**（文件内零 `Insert`/`Update`/`Delete`/`ExecuteNonQuery`）——静态扫描或反射断言。
- **AC3b-2**：DI 注册生效（宿主启动后可从 `IServiceProvider` 解析到 `ITurnTelemetryQuery`）。
- **AC3b-3**：契约↔实现签名一致（`ForgeSelf.Abstractions` + `ForgeSelf.Api` 编译 0 错误；A1 的 6/6 契约测试仍绿）。
- **AC3b-4**：F1 守卫——实现内零宿主库写调用。

**验证**：`dotnet build ForgeSelf.Api` 0 错误 + `dotnet test --filter TurnTelemetryContract`（6/6 仍绿）+ 新增实现的服务解析/只读断言单测。
**依赖 / 风险**：依赖 A1；**L1**（宿主内新增只读实现 + 1 行 DI；不改任何既有表/控制器/调用方）。
**对应 pilot 条款**：02-spec FR-3.9 / FR-4.1 / BR-5 / BR-6、03-plan §A `TurnTelemetryQueryService.cs`、04-task F0 / F1 / F11。

### A4 原子卡片（✅ 已完成 · 2026-10-06）

> **验证证据（Verified，非 Inferred）**：
> ① `dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~CostScopeTests"` → **已通过！失败 0 / 通过 33 / 总计 33 / 98 ms**（`.temp/a4-final.log`，EXIT=0）= A3 的 17 条 + A4 新增 16 条。
> ② `dotnet test ForgeSelf.Abstractions.Tests --filter "FullyQualifiedName~TurnTelemetryContract"` → **6/6 通过**（加性扩展未破坏 A1 契约；`FakeTelemetryQuery` 已同步补 `GetModelCatalogAsync`）。
> ③ **反向探针（MUTATION，实红）**：把级别 3 的大小写相等**故意改成前缀匹配**（`name.StartsWith(m.ChatModelId, OrdinalIgnoreCase)`，即 BC-3 明禁的「按前缀硬切」）⇒ **失败 2 / 通过 14 / 总计 16**（`.temp/a4-mutation.log`，EXIT=1）。变红的正是两条守门用例：
> - `Match_前缀相似但未收录_归未归属_绝不按前缀硬切`：`Expected sibling.Level to be Unattributed{4}, but found CaseInsensitive{3}`；
> - `ResolveAsync_按级别分别累计_命中率正确`：`Expected s.Attributed to be 3L, but found 4L`。
> 还原后复绿（33/33）。
> **本任务净改动（5 个文件）**：新建 `Plugins/CostScope/Services/ModelPriceResolver.cs`、`ForgeSelf.Api.Tests/Plugins/CostScopeTests/ModelPriceResolverTests.cs`；改 `ForgeSelf.Abstractions/ITurnTelemetryQuery.cs`（+1 方法）、`TurnTelemetryDtos.cs`（+`ModelIdentityDto`）、`ForgeSelf.Abstractions.Tests/TurnTelemetryContractTests.cs`（fake 同步，否则编译断）。**未开端点、未做单价 CRUD、未改宿主实体、未触 DB、未 git commit。**

**目标**：FR-3.8 模型→供应商 **4 级回退**解析（ChatModelId → UpstreamModelId/Alias → 大小写不敏感 → **未归属，不猜**）+ 命中率统计。**绝不按前缀硬切**（BC-3/BC-5）。

**前置（必做 · 03-plan 偏差表 2026-10-05 实测行）**：A1 产物 `ITurnTelemetryQuery` 实读**只有三个方法**，`TurnTelemetryDtos.cs` **无** `ModelIdentityDto`（032 提过但未落地）⇒ **A4 开工前先扩契约**：`ITurnTelemetryQuery` 增 `GetModelCatalogAsync`，`TurnTelemetryDtos.cs` 增 `ModelIdentityDto`。宿主侧对应实现由 **A3b** 提供。

**落位**：【插件】`Plugins/CostScope/Services/ModelPriceResolver.cs`（**取代**宿主同名文件；F0 禁止在宿主建）。

**范围（Allowed）**
- `ForgeSelf.Abstractions/ITurnTelemetryQuery.cs` + `TurnTelemetryDtos.cs`：**扩** `GetModelCatalogAsync` + `ModelIdentityDto`（`ChatModelId` / `UpstreamModelId` / `Alias` / `ProviderName`，不可变 `record`）。
- `Plugins/CostScope/Services/ModelPriceResolver.cs`：4 级回退 + 命中率统计（命中数 / 未归属数）。
- `ForgeSelf.Api.Tests/Plugins/CostScopeTests/ModelPriceResolverTests.cs`。

**范围外（明确不做）**
- 不做单价 CRUD（A5）、不算成本（A3 已落）、不建端点（A11）、不改 `AIModel`/`AIProvider` 实体（只读）。

**验收（AC）**
- **AC4-1**：回退顺序正确——ChatModelId 命中 > UpstreamModelId/Alias > 大小写不敏感 > 未归属。
- **AC4-2**：**未命中归「未归属」**，绝不做前缀猜测（**反例探针**：把前缀硬切塞进实现 ⇒ 用例必红）。
- **AC4-3**：一个模型名出现在多个 provider 的 `SupportedModels` 时按 032 §3.2 优先级裁决（BC-5）。
- **AC4-4**：命中率统计可查（命中数 / 未归属数），供 FR-3.8 与页面标注。
- **AC4-5**：契约扩展是**加性**的，A1 的 6/6 契约测试仍绿（不破坏既有三方法）。

**验证（已完成）**：`dotnet test --filter "FullyQualifiedName~CostScopeTests"` → **33/33**（`.temp/a4-final.log`）；`dotnet test --filter TurnTelemetryContract` → **6/6**；反向探针（前缀硬切）**实红 2 条**后还原复绿（`.temp/a4-mutation.log`）。
**依赖 / 风险**：A1 + **A3b（宿主取数实现）**；**L1**（Abstractions 为加性扩展，插件内新增）。
> ⚠️ **A3b 未完成，运行时接线仍缺**：本任务的 `ResolveAsync` 需 `ITurnTelemetryQuery` 实例，而宿主 `TurnTelemetryQueryService`（A3b）尚未落地 ⇒ 本任务用**契约替身**（`FakeCatalogQuery`）完成单测，**未做 DI 注册**（留 A3b 一起接线）。纯函数 `Match` 已完全锁死，不受此影响。
**对应 pilot 条款**：02-spec FR-3.8 / BC-3 / BC-5、03-plan §A2 `ModelPriceResolver.cs` + T2 + 偏差表 2026-10-05「A4 取数契约有缺口」行、04-task AC3e / F0。

### A5 原子卡片（✅ 已完成 · 2026-10-06）

> **验证证据（Verified）**：`dotnet test --filter "FullyQualifiedName~CostScopeTests|FullyQualifiedName~TurnTelemetryQueryService"` → **已通过！失败 0 / 通过 69 / 总计 69**（`.temp/a5-final.log`，EXIT=0）= A3 17 + A4 16 + **A5 23** + A3b 13；`TurnTelemetryContract` **6/6**。
> **反向探针（MUTATION，实红）**：把「重复模型名显式拒绝」改成**静默覆盖**（FR-3.5 明禁）⇒ **失败 1 / 通过 22**，红在 `Save_同模型不同内容_显式拒绝_不静默覆盖`（`.temp/a5-mutation.log`）；还原后复绿 23/23。
> **测试两次抓到真实缺陷（非测试问题）**：① 实体层 `Provider` 必填（XCode `Valid()`），而服务层允许空值 ⇒ 补**显式校验**并新增 `Save_供应商为空_抛ArgumentException_而非撞实体层异常`；② `Save` 的重复拒绝分支原写成 `!IsSameDraft(existing, draft)`，而该方法比较的是「模型 vs 模型」恒为 true ⇒ **拒绝分支形同虚设**（`Save_同模型不同内容` 首跑红），改为纯内容比较。
> **已知缺口（如实标注）**：`Data/Model.xml` 里 `CostModelPrice.Model` 的索引**非唯一**（XCode 因此只生成 `FindAllByModel` 列表方法）⇒ FR-3.5 的「模型唯一索引」目前**只在服务层保证**（显式拒绝重复），DB 级唯一索引已记为后续项。
> **本任务净改动（2 文件）**：新建 `Plugins/CostScope/Services/PriceCatalogService.cs`、`ForgeSelf.Api.Tests/Plugins/CostScopeTests/PriceCatalogServiceTests.cs`（23 条）。未开端点（A11）、未改宿主、未触宿主库、未 git commit。

**目标**：FR-3.5 单价目录 CRUD 的**业务层**——`CostModelPrice`（插件自有库）+ `PriceCatalogService`；删除单价后相关历史成本转 `Unknown`（可撤销，**不删用量**）；单价是**用户本地配置**，不上传、不进 git（BR-8）。为 FR-3.3「未配单价 ⇒ Unknown + 显式列出」提供价格来源（价格**不得硬编码**，FR-3.9）。

**落位**：【插件】`Plugins/CostScope/Services/PriceCatalogService.cs`。

> **端点口径校正**：DAG 行写「+ 插件端点」，但 03-plan §A2 把**所有端点（`prices CRUD` 亦在内）合并进 A11 的单一 `CostController`** ⇒ **本任务不开端点**，避免 A5 与 A11 各建一个控制器形成双真相。端点判据（401/400）随 A11 统一验。

**范围（Allowed）**
- `Plugins/CostScope/Services/PriceCatalogService.cs`：列表 / 新增 / 改 / 删 + 按模型查单价（供 A7 聚合取价）；删除单价 ⇒ 该模型历史成本转 `Unknown`。
- `Plugins/CostScope/Data/CostScopeTables.cs`（建表真源，铁律 12；A2 已含则复用，不另起）。
- `ForgeSelf.Api.Tests/Plugins/CostScopeTests/PriceCatalogServiceTests.cs`（**按用户测试铁律**：样本模型名/单价以模板串构造、断言跟着写入值走，**不硬编码与真实数据同名的金值**；读写走插件自有库 fixture）。

**范围外（明确不做）**
- 不开端点（A11）、不做聚合（A7）、不做页面（A12）、不改宿主任何文件。

**验收（AC）**
- **AC5-1**：CRUD 全通（列表/新增/改/删），且**写只落插件自有库**（F1/F3 守卫：零宿主库写）。
- **AC5-2**：删除单价后该模型成本转 `Unknown`（**不静默计 0**），用量行不删（FR-3.5）。
- **AC5-3**：单价边界校验——非法价格（负 / 超 `[0,1e6]`）被拒（Error Handling「非法价格/阈值 → 400」）。
- **AC5-4**：模型唯一索引生效，重复模型名有明确裁决（不静默覆盖）。

**验证**：`dotnet test --filter CostScope`（含 `PriceCatalogServiceTests`）+ `dotnet build ForgeSelf.Api` 0 错误。
**依赖 / 风险**：A2（表与骨架）+ A3b（取数）+ A3（成本口径）；**L1**（插件内新增 + 插件自有库写；不碰宿主）。
**对应 pilot 条款**：02-spec FR-3.3 / FR-3.5 / FR-3.9 / BR-2 / BR-8 / BC-2、Error Handling 表、03-plan §A2 `PriceCatalogService.cs`、04-task AC3b / F1 / F3 / F15。

### A6 原子卡片（✅ 已完成 · 2026-10-06）

> **验证证据（Verified）**：`dotnet test --filter "FullyQualifiedName~BudgetService"` → **52/52 通过**（`.temp/a6-test2.log`，EXIT=0）；全量回归 `--filter "CostScopeTests|TurnTelemetryQueryService"` → **121/121 通过**（A3 17 + A4 16 + A5 23 + **A6 52** + A3b 13，`.temp/a6-final.log`）；宿主 `dotnet build ForgeSelf.Api` **0 错误**。
> **反向探针（MUTATION，实红）**：删掉 `Evaluate` 里「剩余额度下限截断为 0」⇒ **失败 2 / 通过 50**，红在 `Evaluate_超支_判超支且剩余截断为0` 与 `Evaluate_脏数据限额为0_不做除法且按已用判超支`（`.temp/a6-mutation.log`）；还原后复绿 52/52。
> **两处测试自纠（实现是对的，错在用例）**：① 季度边界用例期望数据写错（4 月属 Q2、起始月是 4，我写成 1）；② 「左闭右开」用例误把「下月首日」当本月边界——`IsInPeriod` 按传入时刻重算窗口，下月首日属**下一月**才是正确行为，已改为断言「相邻窗口首尾不重叠 + 本月最后一刻仍属本月」。
> **设计要点**：`Evaluate(budget, usedAmount)` 把「已用金额」作为**入参**，本服务不查库 ⇒ A6 与 A7 无耦合、可各自单测。周期边界按 U-8 用本地 `DateTime` 计算。
> **本任务净改动（2 文件）**：新建 `Plugins/CostScope/Services/BudgetService.cs`、`ForgeSelf.Api.Tests/Plugins/CostScopeTests/BudgetServiceTests.cs`。未开端点（A11）、未改宿主、未触宿主库、未 git commit。

**目标**：FR-3.6 预算规则——`CostBudget`（级别 Model/Provider/Global × 周期 day/month/quarter/year/all + 限额 + 预警比）+ **实时达成率**；超支**显式横幅**（页面内，**不发通知**）。

**落位**：【插件】`Plugins/CostScope/Services/BudgetService.cs`。**端点口径同 A5**：`budgets CRUD` 并入 A11 的 `CostController`，本任务不开端点。

**范围（Allowed）**
- `Plugins/CostScope/Services/BudgetService.cs`：CRUD + 达成率计算（当期已用 / 限额）；周期边界按 **U-8 设备本地时区**；三级作用域分别取数。
- `ForgeSelf.Api.Tests/Plugins/CostScopeTests/BudgetServiceTests.cs`。

**范围外（明确不做）**
- 不发通知 / 不接消息通道（FR-3.6 明文「不发通知」）；不做页面（A12）；不开端点（A11）；不改宿主。

**验收（AC）**
- **AC6-1**：五个周期（day/month/quarter/year/all）边界正确（按 U-8 本地时区，非 UTC）。
- **AC6-2**：达成率 = 已用 / 限额；达预警比触发横幅标记；**超支（>100%）状态可判**。
- **AC6-3**：三级作用域（Model/Provider/Global）分别取数并正确汇总，不混算。
- **AC6-4**：写只落插件自有库（F1/F3）。

**验证**：`dotnet test --filter CostScope`（含 `BudgetServiceTests`）。
**依赖 / 风险**：A2 + A3b（取数）+ A3（成本）；「已用金额」依赖 A7 聚合——**可与 A7 并行**（本任务用注入的聚合器替身），串联验算留 A7。**L1**。
**对应 pilot 条款**：02-spec FR-3.6 / U-8、03-plan §A2 `BudgetService.cs`、04-task AC3c / F1 / F3。

### A7 原子卡片（✅ 已完成 · 2026-10-06）

> **验证证据（Verified）**：`--filter "TelemetryProjectionService|CostAggregationService"` → **36/36 通过**（物化 12 + 聚合 24，`.temp/a7b-test3.log`）；全量回归 `--filter "CostScopeTests|TurnTelemetryQueryService"` → **161/161 通过**（`.temp/a7-final3.log`，EXIT=0）；`TurnTelemetryContract` **6/6**；`Plugins/CostScope` 构建 **0 错误**、宿主 `ForgeSelf.Api` 构建 **0 错误**。
> **反向探针两条（都实红）**：① 聚合层「未配单价返回非下界」（= 静默当 0 元，BR-2 明禁）⇒ **实红 2 条**（`.temp/a7-mutation.log`）；② 物化层「让已结束日也重算」（= 废掉缓存）⇒ **实红 2 条**（`.temp/a7b-mutation.log`，红在 `QueryWithCache_已结束日读缓存_当日每次重算` 与 `QueryWithCache_清缓存后历史日回落实时值`）。均还原后复绿。
> **✅ 已完成（契约扩展）**：`TurnTelemetryRecord` 补 `FirstTokenMs` / `ResponseStatus` / `ErrorMessage` + A3b `ToRecord` 赋值 + A1 契约测试 SSOT 清单同步。
> **✅ 已完成（`CostAggregationService`）**：`Overview`（今日/本月/总额 + 未配单价清单 + TopModels + 覆盖度）、`Breakdown`（模型/供应商/风格/日四维）、`Latency`（首字与总耗时**分别**出 P50/P95/P99 + 样本数）、`Errors`（失败率 + 按 ErrorMessage 归类 + 未回填状态单列）、`Daily` / `DailyByModel`、`Percentiles`（最近秩法）。成本口径一律委托 A3 纯函数。
> **✅ 已完成（FR-3.7 `TelemetryProjectionService`）**：查询时惰性物化进 `CostTurnDaySummary`——**当日每次重算、已结束日读缓存**（两条语义由同一用例同时证明），`InvalidateAll()` 供改价后清缓存（FR-3.4），**零 `IHostedService`**（静态扫描全插件源码守门）。
> **表结构口径修正（铁律 9：改 Model.xml 后重跑 xcode）**：`CostTurnDaySummary` 原以 `SessionKey` 为 Master，与 FR-3.7 的 **Day×Model** 口径不符 ⇒ 已改为 `Day` 为 Master、`SessionKey` 降为「来源标注」（跨会话汇总写 `*`，可空），并新增 `CostIsLowerBound` 列（把 BR-2 的「下界」状态**持久化**，否则缓存会把 0 当成已核算成本）。
> **测试抓到 2 个真实实现 bug（已修）**：① `Breakdown` 首入桶时 `DimensionItem.Key` 为 null（record struct 的 string 默认值）⇒ 空引用；② **A5 设计缺陷**：`Save` 拒绝「重复且内容不同」⇒ **根本无法改单价**，与 FR-3.4 冲突 ⇒ 新增显式 `Update` 通道（要求条目已存在，不做 upsert）。
> **三处测试自纠 + 一处守卫误报（实现均正确）**：分位样本数期望写错；`Breakdown` 是「维度排行榜」按成本降序（时序升序是 `Daily()` 职责）；测试助手改价误用新增通道；「零 IHostedService」守卫扫到了**注释里**的类型名 ⇒ 已加 `StripComments` 先剥注释再扫。
> **口径决策（如实标注）**：BR-4 字面 `ResponseStatus != 200` 计失败；`== 0`（上游未回填）按字面也计失败，同时**单列 `UnknownStatus`**，避免「未回填」被当成「真失败」而不自知。
> **本任务净改动（8 文件）**：改 `ForgeSelf.Abstractions/TurnTelemetryDtos.cs`、`ForgeSelf.Abstractions.Tests/TurnTelemetryContractTests.cs`、`ForgeSelf.Api/Services/CostScope/TurnTelemetryQueryService.cs`、`Plugins/CostScope/Services/PriceCatalogService.cs`、`Plugins/CostScope/Data/Model.xml` + 重生成 `Data/Entities/CostTurnDaySummary.*`；新建 `Plugins/CostScope/Services/CostAggregationService.cs`、`TelemetryProjectionService.cs` + 对应 3 份测试。未开端点（A11）、未触宿主库、未 git commit。
> ⚠️ **验证绕行（如实标注）**：收口时并行会话的新插件 `Plugins/ToolBridge` 处于半成品（`CommandExecutor.cs:58` 缺 `using System.ComponentModel;` ⇒ `Win32Exception` 找不到），导致任何含插件的构建失败。**该文件非本任务引入、未修改**；本任务改用 `dotnet build ForgeSelf.Api -p:BuildProjectReferences=false` 完成验证（CostScope 与宿主均 0 错误）。待对方修复后须补跑一次完整 `dotnet build ForgeSelf.Api`。

> **历史读数（2026-10-06 聚合阶段，已被上方终态取代）**：`--filter "CostAggregationService|PriceCatalogService"` → **51/51**（A7 24 + A5 27，`.temp/a7-test3.log`）；全量回归 → **149/149**（`.temp/a7-final.log`）。
> **反向探针（MUTATION，实红）**：让「未配单价」返回**非下界**（模拟被静默当作已核算 0 元，BR-2 明禁）⇒ **失败 2 / 通过 22**，红在 `Overview_未配单价模型_计入未配清单且总额标为下界` 与 `删除单价后_该模型转未配单价且成本转下界`（`.temp/a7-mutation.log`）；还原后复绿。
> **✅ 已完成（契约扩展）**：`TurnTelemetryRecord` 补 `FirstTokenMs` / `ResponseStatus` / `ErrorMessage` 三字段 + A3b `ToRecord` 赋值 + A1 契约测试 SSOT 清单同步。
> **✅ 已完成（`CostAggregationService`）**：`Overview`（今日/本月/总额 + 未配单价清单 + TopModels + 覆盖度）、`Breakdown`（模型/供应商/风格/日四维）、`Latency`（首字与总耗时**分别**出 P50/P95/P99 + 样本数）、`Errors`（失败率 + 按 ErrorMessage 归类 + 未回填状态单列）、`Daily`（日趋势）、`Percentiles`（最近秩法）。
> **（FR-3.7 惰性物化已于同日补齐，见上方终态证据）**
> **测试抓到两个真实实现 bug（已修）**：① `Breakdown` 首次入桶时 `DimensionItem.Key` 为 null（record struct 默认）⇒ `acc.Key.Length` 空引用，改 `string.IsNullOrEmpty`；② **A5 的设计缺陷**：`Save` 拒绝「重复且内容不同」⇒ **根本无法改单价**，与 FR-3.4 直接冲突 ⇒ 新增显式 `Update` 通道（要求条目已存在，不做 upsert）。
> **两处测试自纠（实现正确）**：分位样本数期望写错（三条记录里两条有首字延迟）；`Breakdown` 是「维度排行榜」按成本降序，**时序升序是 `Daily()` 的职责**，用例断言写反了。
> **口径决策（如实标注）**：BR-4 字面要求 `ResponseStatus != 200` 计入失败；`ResponseStatus == 0`（上游未回填）按字面也计入失败，同时**单列 `UnknownStatus`**，避免「未回填」被当成「真失败」而不自知。
> **本任务净改动（5 文件）**：改 `ForgeSelf.Abstractions/TurnTelemetryDtos.cs`、`ForgeSelf.Abstractions.Tests/TurnTelemetryContractTests.cs`、`ForgeSelf.Api/Services/CostScope/TurnTelemetryQueryService.cs`、`Plugins/CostScope/Services/PriceCatalogService.cs`；新建 `Plugins/CostScope/Services/CostAggregationService.cs` + 对应测试。未开端点（A11）、未触宿主库、未 git commit。

> ⚠️ **2026-10-06 实测发现的前置缺口（✅ 已在本次补齐）**：`TurnTelemetryRecord`（A1 契约 DTO）实读只有
> `Id / ChatSessionId / TurnIndex / SessionKey / Style / Model / PromptTokens / CompletionTokens / DurationMs / CreatedTime / AgentRunId`，
> **缺 `ResponseStatus`、`FirstTokenMs`、`ErrorMessage`** —— 而 A7 要做的三件事全都依赖它们：
> FR-4.3 延迟分位要 `FirstTokenMs`（且须与 `DurationMs` **分别出**分位）、FR-4.4 错误率与 BR-4 失败轮次判定要 `ResponseStatus != 200`、错误分布归类要 `ErrorMessage`。
> ⇒ **A7 开工第一步：扩 `TurnTelemetryRecord` 这 3 个字段 + 在 A3b 的 `TurnTelemetryQueryService.ToRecord` 里赋值**（宿主 `ChatTurn` 三列均已存在，实读确认），并同步 A1 契约测试。
> **另一处口径修正（因 U-2 裁决 = (c)）**：AC1 要求「已覆盖 N / 未计入 M（主聊天 legacy 接缝）」且「M 计数正确」。
> 但 U-2=(c) 下主聊天**恒不写入** `ChatTurn`，M **不可计算** ⇒ 不得伪造数字。
> 处置：覆盖度只报**可计算部分**（已覆盖 N / 总记录数 / 未配单价模型数）+ **显式常量声明**「主聊天链路当前不可观测（U-2=(c)）」，把 M 记为 `Unknown` 而非填 0。
**目标**：两件事——① FR-4.2~4.4 **只读聚合**（`CostAggregationService`：模型/供应商/日/风格四维 + 延迟 P50/P95/P99 + 错误率 + **覆盖度**）② FR-3.7 **惰性物化日汇总**（`CostTurnDaySummary` + `TelemetryProjectionService`，当日未结束每次重算、已结束缓存，**无 `IHostedService`**）。

**落位**：【插件】`Plugins/CostScope/Services/CostAggregationService.cs` + `TelemetryProjectionService.cs`。

**范围（Allowed）**
- `CostAggregationService.cs`：**入参是 `ITurnTelemetryQuery` 返回的只读 DTO**（插件**不直连宿主库**，FR-4.6/BR-5）；失败轮次**不计成本**（BR-4）；未配单价进 `unpricedModels` **且不计总额**（BR-2）；覆盖度 N/M（AC1）。
- `TelemetryProjectionService.cs`：`CostTurnDaySummary` 惰性物化（**写插件自有库**），**零 `IHostedService`**（032 D3：规避插件热重载不重启的坑）。
- `ForgeSelf.Api.Tests/Plugins/CostScopeTests/CostAggregationServiceTests.cs`。

**范围外（明确不做）**
- 不开端点（A11）；不做页面（A12）；不改宿主；**不写宿主库**（F1/BR-5）。

**验收（AC）**
- **AC7-1**：四维聚合（模型/供应商/日/风格）返回 token + 金额 + 轮次 + 失败数；未配单价进 `unpricedModels` **且不计入总额**（BR-2；**反例探针**：让未知单价计入总额 ⇒ 必红）。
- **AC7-2**：延迟 P50/P95/P99 与手算一致，`FirstTokenMs` 与 `DurationMs` **分别出**（AC5）；样本不足时按 BC-7 **随分位数返回样本数**。
- **AC7-3**：错误率 = 失败/总数；`ResponseStatus != 200` 计入失败**且不计成本**（BR-4）；错误分布按 `ErrorMessage` 归类。
- **AC7-4**：覆盖度「已覆盖 N / 未计入 M（主聊天 legacy 接缝）」正确（AC1）；**无 usage 时为 `null`，不伪造 0**（FR-1.4/BC-1）。
- **AC7-5**：日汇总惰性物化——**当日每次重算、已结束读缓存**；**零 `IHostedService`**（静态扫描断言）；写只落插件自有库（FR-3.7/032 D4）。
- **AC7-6**：**改价后历史重算**（FR-3.4）——改单价 ⇒ 总额随之变化（成本不入明细层，聚合时现算）。

**验证**：`dotnet test --filter CostScope` + 反向探针（未配单价计入总额 ⇒ 必红）+ `dotnet build ForgeSelf.Api` 0 错误。
**依赖 / 风险**：A2 + A3b（取数）+ A3（成本）+ A4（供应商解析）+ A5（单价）；**L2**（聚合口径是 AC1/AC2/AC5 主战场，且首次向插件自有库写汇总表，须验 F1/F3）。
**对应 pilot 条款**：02-spec FR-1.2 / FR-1.4 / FR-3.4 / FR-3.7 / FR-4.2~4.4 / BR-2 / BR-4 / BR-5 / BC-1 / BC-6 / BC-7、03-plan §A2 + T3、04-task AC1 / AC2 / AC3d / AC5。

### A8 原子卡片（🚫 本轮不做 · 2026-10-06 用户裁决 U-2 = (c)）

> **裁决（用户原话）**：「未来所有功能偏插件化，主流程调用插件实现。按这个逻辑决策，**重复实现也没关系，记录待办后面再处理**。」
> ⇒ **不迁宿主 `ChatController` 接缝**（不做 FR-1.3 的接缝迁移）。本任务整体转为：① 在端点与页面**显式标注覆盖度缺口**（FR-1.1/FR-1.2，并入 A11/A12）；② 主聊天用量缺口**登记独立 TODO 后置处理**。
> **A9 已与此解耦**：A9 的写入侧是 Agent 路径（`ReactLoopAgent`），与主聊天 `ChatController` 无关 ⇒ A8 不再是 A9 的前置。

**目标**：FR-1.3 —— 把 `ChatController` 从 legacy `IAIService`（`Task<string>`，**签名即无 usage**）切到带 usage 的 **Unified 面**（`IAIProvider.ChatAsync` → `UnifiedChatResponse.Usage`，或 `IChatCompletion`），使主聊天链路的 token 进入聚合。

**阻塞**：**U-2**（闸门1 未裁决）。
> **02-spec 实测更正：032 的「一行透传」结论不成立**——`ChatController.cs:25` 注入 `IAIService`，其 `ChatAsync` 返回 `Task<string>`（`AIService.cs:20-31`）**签名里根本没有 usage**；`ILlmRuntime` 只有 `StreamAsync`，且 legacy 适配器已知有损（`AIServiceLlmRuntime.cs:19-20` 原文「无法取到用量」）。⇒ 这是**接缝迁移**（改注入 + 改调用 + 回归校验），**不是一行改动**。

**落位**：宿主 `ForgeSelf.Api/Controllers/ChatController.cs`（主聊天链路本体，搬不动）。

**范围（Allowed · 仅当 U-2 裁决为 (a) 时才做）**
- `ForgeSelf.Api/Controllers/ChatController.cs`：改注入（`IAIService` → `IChatCompletion`/`IAIProvider`）+ 改调用（`:126` `ChatAsync`、`:203` `ChatStreamAsync`）+ 填充 `Usage`（**`:131` 与 `:216` 两处**——032 §15 U6 只记了 `:131`）。
- 配套 **e2e 回归**：工具调用与流式行为不回归（AC1b）。

**范围外（明确不做）**
- **U-2 = (c) 时本任务整体不做** ⇒ 改为在端点与页面**显式标注缺口**（FR-1.1/FR-1.2），**不得让用户把部分覆盖误读为全量**。
- 不改 `SessionEvents.cs` 的 record 定义（F6，040 正在触碰该文件）。
- 默认口径下 `ChatController` **零改动**（F5）。

**验收（AC · 仅 (a) 时）**
- **AC8-1**：主聊天会话事件带**非空** `Usage`（AC1b）。
- **AC8-2**：工具调用与流式 e2e **无回归**。
- **AC8-3**：覆盖度里的「未计入 M」归零（AC1）。

**验证**：`dotnet test --filter ChatController` + `dotnet test --filter "ChatTurnStreamRecorderTests|ChatTurnServiceTests"`（录制回归面）+ 流式/工具调用 e2e。
**依赖 / 风险**：A1；**L3**（触及主聊天链路本体）。**U-2a 风险**：迁到 `IChatCompletion` 会改变工具调用与 Agent 路由行为（走 Agent 上下文 vs legacy 直连 provider）⇒ 裁决 (a) 前**必须先读 `AIAgentChatCompletion`/`ReactLoopAgent` 确认行为差异并配 e2e 回归**。
**对应 pilot 条款**：02-spec FR-1.1~1.5 / U-2 / U-2a / 依赖前提段、03-plan §A `ChatController.cs` + T1、04-task F5 / F6。

### A9 原子卡片（🚫 **取消加列** · 2026-10-06 用户裁决：宿主保持抽象，关联做在插件侧）

> **裁决（用户原话）**：「能否做成插件消费模型请求消息、时间进行处理，按照之前约定的，**插件消费宿主产生的一切，宿主只是抽象，向 dsh cordis 看齐**。」
> ⇒ **不给宿主 `ChatTurn` 加任何列**（不再需要 Model.xml、不碰生产主聊天表），trace 关联改为**插件侧用宿主已产生的数据做**：
> 插件经 `ITurnTelemetryQuery`（宿主已写入的轮次：模型/时间/token/耗时）+ `IAgentRunTelemetryProvider`（AIAgent 插件自己的 AgentRun/AgentStepRun）**在插件内按「会话键 + 时间窗邻近」关联**。
> 这与已确立的方向一致（输入N+4「以插件为主，宿主侧也迁移到插件」；U-2 裁决「功能一律插件化，主流程只调插件实现」）。宿主只负责**如实产出**轮次数据，不为观测加工字段。
>
> 🛑 **同时记录的既有债（不在本 pilot 处理）**：宿主 30 个实体**只有生成物、没有 Model.xml**（见下方实测），铁律 9/11 在宿主侧从未被满足。已按「不动生产表」原则搁置，另立 TODO。

> 🛑 **实测结论：宿主根本没有 Model.xml，铁律 9 对宿主实体当前不可执行。**
> 全仓搜索：10 个 `Model.xml` **全部属插件**；`ForgeSelf.Api/Entities/` 下 30 个实体文件均为 **2026-09-25 一次性生成**，仓库内**无对应模型源**。⇒ 03-plan 写的「`ForgeSelf.Api/Entities/Model.xml`（加两列真源）→ 跑 xcode」**路径不存在**。
>
> **重建可行性实测（已按安全阀在临时目录生成 + 逐行 diff，未覆盖任何生产文件）**：
> 差异 **341 + 140 行**，且 `Models/ChatTurnModel.cs`、`Interfaces/IChatTurnModel.cs` **根本没生成**。三类差异：
> ① **索引名不一致**：仓库 `IX_ChatTurn_ChatSessionId_Turn` vs 按当前列名生成的 `IX_ChatTurn_ChatSessionId_TurnIndex` ⇒ XCode `CheckDeleteIndex` 默认开启，迁移时可能**删旧索引建新索引**（真实 schema 风险）；
> ② **接口实现丢失**：生成的类是 `public partial class ChatTurn`，仓库版是 `: IChatTurnModel, IEntity<IChatTurnModel>` ⇒ 说明原模型开启过 IModel 生成且模板为 `I{name}Model`，我的 Option 块未复现 ⇒ 重新生成会**丢接口、编译失败**；
> ③ 列描述文案更详细（无 schema 影响，但证明原始模型信息已丢失，无法字节级还原）。
>
> **⇒ 按用户指定的安全阀判定：重建 Model.xml 达不到「只出现本次新增列」的要求，ABORT，未修改任何生产实体文件、未碰生产表结构。**
> **根因定性**：这不是本任务的缺口，而是**宿主实体的既有债**——30 个实体有生成物无模型源，铁律 9/11（Model.xml 为真源、禁手改生成物）在宿主侧**从来没被满足过**。需用户裁决后续路径（已记 TODO）。


**目标**：FR-2 trace 外键——`ChatTurn` 增 `AgentRunId`（`Int64?` 可空）+ `TraceId`（`String` 可空，长 64）+ `AgentRunId` 索引（FR-2.1/2.4）；历史行保持 `null`，**不追溯回填**（FR-2.5）。

**阻塞**：**U-3**（`ChatTurn` 加列 = **DB 结构变更**，触规范 §1 硬性约束 2，须闸门1 **明确批准**）。

**落位**：宿主 `ForgeSelf.Api/Entities/Model.xml`（真源）→ `xcode` → `ChatTurn.cs` / `.Model.cs` / `.Biz.cs`（**生成物禁手改**，F11/铁律9）。

**范围（Allowed）**
- `ForgeSelf.Api/Entities/Model.xml`：加两列（均可空）+ `AgentRunId` 索引。
- `xcode Model.xml` 重新生成三个生成物文件（**禁手改**）。
- 写入侧：`Plugins/AIAgent/Services/ReactLoopAgent.cs`——**仅**在调 LLM 处写入 `ChatTurn.AgentRunId`，**不改 Agent 行为**（FR-2.3）。

**范围外（明确不做）**
- 不给 `AgentRun` 加列（F7：实测已有 `AgentId`/`SessionId`/`StepCount`/`TotalTokens`，复用即可）。
- **不回填历史数据**（FR-2.5）；不改 `SessionEvents.cs`（F6）。

**验收（AC）**
- **AC9-1**：`Model.xml` 两列 + 索引就绪，xcode 生成物与模型一致（生成后 `git diff` 只落在生成物）。
- **AC9-2**：XCode 自动迁移通过（列均可空、向后兼容；宿主启动建表/迁移无错）。
- **AC9-3**：Agent 路径写入后，**新** `ChatTurn` 行带非空 `AgentRunId`。
- **AC9-4（替代方案）**：若 U-3 否决加列 ⇒ 按 **U-3a (i)** 改为 `SessionId`+时间窗**近似关联**，并在页面/文档**标注「关联为近似」**（不冒充精确关联）。

**验证**：`dotnet build ForgeSelf.Api` 0 错误 + `dotnet test --filter "ChatTurnStreamRecorderTests|ChatTurnServiceTests"` + 宿主启动建表/迁移无错。
**依赖 / 风险**：A8（写入路径依赖 usage 链路）+ **U-3 裁决**；**L3**（宿主库结构变更，须用户批准，未批即实施属流程违规）。
**对应 pilot 条款**：02-spec FR-2.1~2.5 / U-3 / U-3a、03-plan §A `Model.xml` + T4、04-task F7 / F11。

### A10 原子卡片（✅ 已完成 · 2026-10-06 · A9 取消后在插件侧完成关联）

> **验证证据（Verified）**：`--filter "TraceProjectionService|TurnTelemetryContract"` → **19/19 通过**（trace 13 + 契约 6，`.temp/a10-test3.log`）；全量回归 `--filter "CostScopeTests|TurnTelemetryQueryService"` → **180/180 通过**（`.temp/a10-final.log`，EXIT=0）；`Plugins/CostScope` 构建 **0 错误**。
> **反向探针（MUTATION，实红）**：把关联判定短路为「任何轮次都算关联」⇒ **失败 5 / 通过 14**，红在 `Build_部分轮次落在窗外_未关联数正确`、`IsLinked_会话键不同_不关联`、`IsLinked_时间在窗外超容差_不关联`、`IsLinked_运行无时间锚点_降级为仅按会话键`、`Build_未指定run时_自动选关联轮次最多的运行`（`.temp/a10-mutation.log`）；还原后复绿 19/19。
> **架构口径（A9 取消的直接后果）**：宿主**不加外键列**、只如实产出轮次数据；关联**全在插件侧**做——插件拿「宿主轮次（`ITurnTelemetryQuery`）」+「AgentRun 及其步骤（`IAgentRunTelemetryProvider`）」，按**同一会话键 + 时间窗邻近（默认 ±30s）**推断。
> **关联规则（逐条可单测）**：① 会话键相同（两侧都有值时）；② 轮次时间落在运行窗口 ± 容差内；③ 运行缺时间锚点 ⇒ **降级为仅按会话键并标记近似**；④ 任何无法关联的轮次**如实计入未关联**、绝不静默挂靠（BC-4）；⑤ 指定了不存在的 runId ⇒ 返回未关联，**不静默回退**到别的 run。
> **数据诚实**：耗时负值截断为 0（AC4）；零 token 时 `Tokens` 保持 `null` **不伪造 0**（FR-1.4）；`CorrelationNote` **始终**声明「关联为近似」——宿主无外键，本质是推断，页面不得说成事实。
> **契约加性扩展**：`AgentRunTelemetry` 增 `StartedTime`/`EndedTime`，`AgentStepTelemetry` 增 `Name`/`StartedAt`（时间锚点来自 `AgentRun.CreateTime/UpdateTime`、`AgentStepRun.StartedAt`）；契约测试 6/6 仍绿。
> **一处测试自纠**：用例没算清默认 ±30s 容差，run-a 的窗口实际也覆盖了 2 条成平局、按「时间最早」规则选中 —— 实现正确，已改用例（并补一条「并列取最早 ⇒ 结果可重复」）。
> **遗留（如实标注）**：`IAgentRunTelemetryProvider` 的**实现方在 AIAgent 插件侧**（尚未实现）⇒ 当前 A10 以<b>入参</b>方式可单测，运行时接线需 AIAgent 侧注册该 provider。
> **本任务净改动（4 文件）**：新建 `Plugins/CostScope/Services/TraceProjectionService.cs`、`ForgeSelf.Api.Tests/Plugins/CostScopeTests/TraceProjectionServiceTests.cs`；改 `ForgeSelf.Abstractions/TurnTelemetryDtos.cs`（+4 字段）。未开端点（A11）、未触宿主库、未 git commit。

**目标**：FR-4.5 trace waterfall **聚合器**——以 `AgentRunId` 为根串 `TurnTelemetryRecord`（LLM 调用）+ `AgentRunTelemetry.Steps`（工具步骤），节点含起止时间/耗时/模型/token，**耗时非负且按时间排序**（AC4）。

**落位**：【插件】`Plugins/CostScope/Services/TraceProjectionService.cs`（**取代**已取消的宿主 `TraceAggregator.cs`，F0）。取数经 **A3b** 宿主接缝，插件不直连宿主库。

**范围（Allowed）**
- `Plugins/CostScope/Services/TraceProjectionService.cs`：消费 `ITurnTelemetryQuery` 的 DTO，输出 `{ runId, nodes: [{ kind: llm|tool, ...timing, model?, tokens? }] }`（Output 表形状）。
- `ForgeSelf.Api.Tests/Plugins/CostScopeTests/TraceProjectionServiceTests.cs`。

**范围外（明确不做）**
- 不开端点（A11）；不做瀑布 UI（A12 的 `TracePanel`）；不改 `AgentRun`/`AgentStepRun` 实体（F7）。

**验收（AC）**
- **AC10-1**：waterfall **同时含 LLM 节点与 tool 节点**，按时间升序。
- **AC10-2**：耗时**非负**（**反例探针**：注入负耗时 ⇒ 必红）。
- **AC10-3**：`AgentRunId` 为 null（历史行/非 Agent 路径）⇒ **明确返回「该轮次无关联 AgentRun」，不静默挂到某个 run**（BC-4）。
- **AC10-4**：只读，零宿主库写（F1/BR-5）。

**验证**：`dotnet test --filter CostScope`（含 `TraceProjectionServiceTests`）+ 反向探针。
**依赖 / 风险**：A9（外键）+ **A3b（宿主取数实现）**；**L2**（依赖 A9；若 U-3 否决则退化为 U-3a 近似关联，并在输出与页面标注「关联为近似」）。
**对应 pilot 条款**：02-spec FR-4.5 / BC-4 / BR-5 / Output trace waterfall、03-plan §A2 `TraceProjectionService.cs` + T4、04-task AC4。

### A11 原子卡片（✅ 已完成 · 2026-10-06）

> **验证证据（Verified）**：`--filter "CostController"` → **31/31 通过**（`.temp/a11-test6.log`）；全量回归 `--filter "CostScopeTests|TurnTelemetryQueryService"` → **211/211 通过**（`.temp/a11-final.log`，EXIT=0）；`TurnTelemetryContract` **6/6**；`Plugins/CostScope` 构建 **0 错误**。
> **反向探针（MUTATION，实红）**：让非法/空 `by=` 也放行（模拟「静默按默认维度返回」）⇒ **失败 3 / 通过 28**，红在 `Breakdown_非法维度_返回400且附可用值` 的 `unknown`/`99`/`""` 三行（`.temp/a11-mutation.log`）；还原后复绿 31/31。
> **端点面（18 个，路由前缀 `api/cost-scope`）**：读 `overview` / `summary` / `daily` / `breakdown?by=` / `latency` / `errors` / `turns`（分页）/ `trace?agentRunId=` / `settings`；写 `prices` GET·POST·PUT·DELETE、`budgets` GET·POST·PUT·DELETE、`budgets/achievement`、`summary/rebuild`。
> **鉴权（AC8）**：类级 `[Authorize("ApiKeyPolicy")]`（插件控制器**不会自动被保护**，铁律 17），**反射断言**守住——含「任何端点都不得标 `[AllowAnonymous]`」的结构性守卫。
> **错误纪律**：非法维度 → **400 + 附可用值**；服务层入参校验异常（`ArgumentException` 含 `ArgumentOutOfRangeException`）经 `Guard` **统一映射 400**（刻意不捕获其它异常，数据访问失败应如实冒泡为 500，不伪装成参数错）。
> **聚合器按请求新建（刻意不进 DI）**：模型目录在**每次请求内取一次快照**注入聚合器——单例聚合器会持有**陈旧目录**导致模型归错供应商。DI 只注册无状态/仅持插件库句柄的 `PriceCatalogService`、`BudgetService`、`TraceProjectionService`。
> **分页与时间窗**：`turns` 分页默认 page=1/size=100、**size 上限 500**（NFR-4 防全表扫）；时间窗默认**近 30 天**（032 P0）。
> **不泄露（NFR-5）**：`turns` 只返回元数据（token/耗时/状态/错误文本），**不含请求/响应体**。
> **两处测试自纠**：① 无关联时返回的说明是「未关联到任何 AgentRun」而非「近似」——无关联比近似更直白，断言写错已改；② `by=` 空串我原先实现成「未指定→默认 model」，按「禁静默」收紧为 **400**（未传 `null` 才默认，显式传空串是客户端错误）。
> **一处实现 bug（编译期拦下）**：`LoadRunsAsync` 被误改为 `static` 却仍读实例字段 `_telemetry` ⇒ CS0120，编译期即暴露，已修。
> **遗留（如实标注）**：**BC-8「插件未启用返回明确『未启用』而非 404 空页」无法由本控制器实现**——插件未加载时路由根本不存在，属**宿主插件装载层**职责，已记 TODO。
> **测试方式说明（如实标注）**：本轮为**直接实例化控制器**的单测（不起 Web 主机），因此 **401 的真实链路未验证**——类级 `[Authorize]` 的存在性由反射守住，实际 401 需宿主起运行时+e2e 复核。
> **本任务净改动（4 文件）**：新建 `Plugins/CostScope/Controllers/CostController.cs`、`ForgeSelf.Api.Tests/Plugins/CostScopeTests/CostControllerTests.cs`；改 `Plugins/CostScope/CostScopePlugin.cs`（DI 注册）。未触宿主库、未 git commit。

**目标**：FR-4 端点面**合并**为插件**单一控制器** `CostController`——overview / summary / daily / breakdown / latency / errors / turns + **prices CRUD** + **budgets CRUD** + summary rebuild + settings + **trace**；**类级 `[Authorize("ApiKeyPolicy")]`**（铁律 17）；未知 `by=` 返回 **400** 不静默空表。

**落位**：【插件】`Plugins/CostScope/Controllers/CostController.cs`（**取代**已取消的宿主 `LlmObservabilityController.cs`，F0）。

**范围（Allowed）**
- `Plugins/CostScope/Controllers/CostController.cs`：路由前缀 `api/cost-scope/...`；**类级鉴权**；聚合 A5/A6/A7/A10 的服务。
- `ForgeSelf.Api.Tests/Plugins/CostScopeTests/CostControllerAuthTests.cs` + `CostControllerEndpointTests.cs`。
- 服务注册在 `CostScopePlugin.Apply`（插件侧 DI）；宿主 `AppBuilder.cs` **只**保留 `ITurnTelemetryQuery` 一条。

**范围外（明确不做）**
- 不做页面（A12）；**不在宿主建任何观测控制器**（F0）；不改 `UsageStatsController`（F2，属独立 TODO）。

**验收（AC）**
- **AC11-1**：**类级** `[Authorize]` 存在（**反射断言**，先例 `McpAdminAuthTests`）；**无 token 调用 ⇒ 401**（AC8）。
- **AC11-2**：非法 `by=`（非法维度枚举）⇒ **400** + 明确错误，**禁止静默返回空表**（Error Handling 表）。
- **AC11-3**：端点输出形状符合 02-spec **Output 表**六类（overview / daily / breakdown / latency / errors / trace）。
- **AC11-4**：分页/限流（NFR-4）；时间窗默认**近 30 天**（032 P0）。
- **AC11-5**：**敏感数据（请求/响应体）不进端点返回**（NFR-5）；明细回看走既有 `ChatRecordsController`。
- **AC11-6**：插件控制器**随宿主加载生效**——`StageAllPlugins` 产物含 `Plugins/CostScope/CostScope.dll`（A2/A3 已修构建图缺口）。
- **AC11-7**：插件未启用时返回明确「未启用」，**不 404 空页**（BC-8）。

**验证**：`dotnet build ForgeSelf.Api`（产物含该 DLL）+ `dotnet test --filter CostScope`（鉴权/400 集成测试）+ 401 实测。
**依赖 / 风险**：A10 + A3/A4 + A5/A6/A7（服务齐备）；**L2**（首个对外面，涉及鉴权与错误码口径，属铁律 17 高风险面）。
**对应 pilot 条款**：02-spec FR-4.2~4.5 / FR-4.7 / NFR-4 / NFR-5 / BC-8 / Error Handling 表、03-plan §A2 `CostController.cs` + T5、04-task AC8 / F0 / F2。

### A12 原子卡片（✅ 已完成 · 2026-10-06 · 含前端构建与类型检查已跑通）

> 🔧 **构建修复（2026-10-06 22:1x，用户报「插件构建失败」后处理，已 Verified）**
> 根因**不在业务代码**（`dotnet build` solution **0 错误**），是**前端构建链**：
> ① **pnpm 11 默认拦截依赖 postinstall** ⇒ `esbuild@0.25.0` 的 postinstall（放置平台二进制）被拒，
>    `pnpm install` 以 `ERR_PNPM_IGNORED_BUILDS` 退出 1，连带 `pnpm build` 失败。
>    **关键坑**：pnpm 11 **不再读 `package.json` 里的 `pnpm` 字段**（日志明确提示
>    `The "pnpm" field in package.json is no longer read by pnpm`）⇒ 设置必须放 **`pnpm-workspace.yaml`**。
>    仓内 DesignSystem / FileTools / McpCenter / AgentHub 四个插件的 `pnpm-workspace.yaml`
>    已含 `allowBuilds.esbuild: true` + `onlyBuiltDependencies: [esbuild]`——**本插件漏了这个文件**。
> ② **vite 配置漏了 lib 模式**：非 lib 模式以 `index.html` 为入口，插件没有 HTML 入口 ⇒
>    `Could not resolve entry module "index.html"`。已按仓内约定改为 `build.lib`（entry `src/index.ts`、
>    `formats: ['es']`、`fileName: () => 'index.js'`）+ `cssCodeSplit: false` + `assetFileNames: 'style[extname]'`。
> **验证（Verified）**：`pnpm install` ✓（esbuild postinstall: Done）→ `pnpm check`（vue-tsc）**0 错误** ✓
> → `pnpm build` ✓ 产物 `dist/index.js` 27.65 kB + `dist/style.css` 4.63 kB（**正是契约要求的固定名**）；
> 产物实测含 `export { st as CostScopeView, st as default }`、保留裸 `from "vue"`（external 生效，无 Vue 双实例）；
> A12 守卫测试回归 **9/9**。
> **顺带修掉两个真实类型错误**：`DashboardPanel` 里 `dimension` 声明后从未使用（死代码，`noUnusedLocals` 报出）、
> `vite.config.ts` 缺 `@types/node`。已新增 `web/tsconfig.json`（含 `types: ["vite/client","node"]`）与 `@types/node` 依赖。
> **产物不入库**：`.gitignore` 第 30 行忽略 `dist/`、第 9 行忽略 `node_modules/`（与其它插件一致）⇒ 宿主发布流程负责产出。
>
> **原始验证证据**：`--filter "CostScopeWebAsset"` → **9/9 通过**（`.temp/a12-test3.log`）；全量回归 → **220/220**（`.temp/a12-final.log`）；`TurnTelemetryContract` **6/6**；反向探针（`http.ts` 真调 `api/usage-stats`）**实红 1 条**后复绿。
> **反向探针（MUTATION，实红）**：在 `http.ts` 真的引入 `'/api/usage-stats'` ⇒ **实红 1 条**（`前端源码_禁止调用宿主观测端点`，`.temp/a12-mutation.log`）；还原后复绿 9/9。
> **前端资产（`Plugins/CostScope/web/`）**：`package.json` / `vite.config.ts`（ESM、入口 `index.js`、vue 外部依赖）/ `src/index.ts`（具名导出 `CostScopeView` + default）/ `src/http.ts`（统一信封剥离、401/404 专门提示）/ `src/CostScopeView.vue`（四页签）/ `src/components/{Dashboard,Price,Budget,Trace}Panel.vue`。
> **端点边界（FR-4.6 / 铁律 14）**：前端**只调** `api/cost-scope/*`；守卫静态扫描 `web/src/**` 禁止出现 `api/usage`、`api/usage-stats`、`api/chat-records`、`api/llm-observability`（混用会出现「两套数字对不上」）。
> **数据驱动的契约对齐**：视图名与入口**从 `plugin.json` 的 `frontend` 读取**（SSOT），不断言写死的 `CostScopeView`/`web/dist/index.js`——否则改了 plugin.json 忘了改前端，测试反而全绿。
> **诚实性标记进界面（守门测试钉死）**：`mainChatObservable`（主聊天不可观测）、`costIsLowerBound`（未配单价只算下界）、`correlationNote`（关联为近似）三者**必须**出现在界面上；超支**只做页面横幅**且断言**不得**出现 `Notification`/`ElMessage`（不发通知，FR-3.6）。
> **Plan 偏差（已记）**：04-task 原写「`ForgeSelf.Api/wwwroot/js/features.ts` + `features.ts` 登记」——**本仓不存在 features.ts**（全仓 maxdepth3 搜索无果）；插件视图的实际注册机制是 `plugin.json` 的 `frontend.views`（A2 已写）+ 宿主加载器按名取组件，故**无需**改 features.ts。
> **两处测试问题（实现/资产均正确）**：① 守卫**误报**——扫到了 `http.ts` 注释里「严禁调用 api/usage…」这句说明文字 ⇒ 已加 `StripComments`（同时处理 `//`、`/* */`、`<!-- -->`）先剥注释再扫（与 A7 的 `IHostedService` 守卫同一类教训）；② **我的死循环 bug**——`RepoRoot()` 的 `while` 漏了 `dir = dir.Parent`，导致测试挂起 **11 分钟**才被我发现并用 `TaskStop` 终止（A7 的同款方法当初有这行，只有新写的漏了）。
> **未做（如实标注）**：**未执行 Vite 构建**（`web/dist` 产物未生成）——构建产物交由宿主发布流程；`vue-tsc` 类型检查也未跑（需 `pnpm install`，本轮未做）。故 **A12 的「界面可用」未经运行时验证**，验证的是「资产齐备 + 契约对齐 + 边界与诚实性守门」。
> **本任务净改动（9 文件）**：新建 `Plugins/CostScope/web/{package.json,vite.config.ts}`、`web/src/{index.ts,http.ts,CostScopeView.vue}`、`web/src/components/{Dashboard,Price,Budget,Trace}Panel.vue`、`ForgeSelf.Api.Tests/Plugins/CostScopeTests/CostScopeWebAssetTests.cs`。未触宿主库、未 git commit。

**目标**：FR-5 可视化——插件自带 `web/`：总览 **dashboard**（成本时序 / 模型分布 / 延迟 P50-P95 / 错误率）+ `PricingPanel`（未配单价**显式列出 + 去配单价入口**，**不显示 ¥0.00 假象**）+ `BudgetPanel`（超支横幅）+ **`TracePanel`（瀑布页签）** + `http.ts`；并登记宿主 `features.ts`（SSOT）。

**落位**：【插件】`Plugins/CostScope/web/**` + 宿主 `ForgeSelf.Web/src/data/features.ts`（SSOT 登记，**不迁插件**）+ `ForgeSelf.Web/e2e/menu-route-consistency.spec.ts`（铁律19③）。

**范围（Allowed）**
- `Plugins/CostScope/web/`：骨架按 `plugin-frontend-scaffold`；产物 `web/dist/index.js` + `style.css`，**导出名 == `plugin.json` 的 `views[0]`**。
- 样式**仅 `--el-*` + `color-mix()`**，**0 自定义 token**（FR-5.5/NFR-6）。
- `ForgeSelf.Web/src/data/features.ts`：登记 `cost-scope` + `signals` 指向插件视图与插件控制器。
- `ForgeSelf.Web/e2e/menu-route-consistency.spec.ts`：route 同步。
- `Plugins/CostScope/web/src/**/*.spec.ts`（插件侧 vitest）+ `ForgeSelf.Web/e2e/plugins/cost-scope/cost-scope.spec.ts`（插件层 e2e，**零 mock、真实登录**）。

**范围外（明确不做）**
- **宿主前端零改动**（F14：`router/index.ts` / `ChatRecordDetail.vue` / `components.d.ts` 一律不动）。
- **瀑布不做宿主挂载**（铁律4：宿主前端不能 import 插件组件）⇒ 改为**插件内 trace 页签**；宿主 `ChatRecordDetail.vue` 因此零改动（FR-5.2 形态更正）。

**验收（AC）**
- **AC12-1**：dashboard 展示值与后端端点**逐项一致**（**禁 mock**，AC7）。
- **AC12-2**：未配单价模型**显式列出 + 「去配单价」入口**，**不显示 `¥0.00`**（FR-5.4）；单价目录为空时**首屏阻塞式提示**「请先配置单价」（BC-2）。
- **AC12-3**：预算超支横幅（**页面内，不发通知**）。
- **AC12-4**：`features.ts` 登记 + `node scripts/check-features.mjs` **退出码 0**（AC10）；菜单/路由由 `plugin.json frontend` **一处声明**（铁律19①）+ route 同步 e2e（铁律19③）。
- **AC12-5**：**0 自定义 token**（`check-theme-tokens.mjs` 若存在）。
- **AC12-6**：插件前端 `pnpm run build` 通过 + 宿主 `pnpm run check` **无新增错误**（AC9）。

**验证**：`cd Plugins/CostScope/web && pnpm run build` + 宿主 `pnpm run check` / `test` + `node scripts/check-features.mjs` + 插件层 e2e（**显式端口** `E2E_FRONTEND_PORT`/`E2E_BACKEND_PORT`、按 §5.0 设 `NO_PROXY` 与 `TEMP/TMP`）。
**依赖 / 风险**：A11（端点）；**L2**（首次建插件前端产物链，含 dist 契约与导出名一致性；e2e 受本机代理/TEMP 坑影响，须按 §5.0 前置）。
**对应 pilot 条款**：02-spec FR-5.1~5.6 / BC-2 / NFR-6、03-plan §A2 `web/**` + T6/T8、04-task AC7 / AC10 / F14。

### A13 原子卡片（⏳ 待执行）

**目标**：FR-5.3 —— `dsh-ui-bundle/.../panel.tsx` 的 `MOCK_ENTRIES`（`:53-58`）替换为**真实数据源**；**props shape 不变**（`:137-143` 已预留 `[key: string]: unknown` 注入位，U-7）。

**落位**：`dsh-ui-bundle/my-dsh-activity-timeline-client/src/panel.tsx`（**独立客户端仓**，与「以插件为主」的拍板无关，不动归属）。

**范围（Allowed）**
- 把 `MOCK_ENTRIES` 换成真实数据源（宿主事件 → **沿用既有 schema** `{ time, kind: todo|decision|action|result, text }`）。
- **反例探针（必做）**：切到空会话 ⇒ 显示空态，**若仍显示 mock 4 条则判红**（AC6）。

**范围外（明确不做）**
- **不改 props shape**（U-7）；不改 DSH 侧框架契约；不动插件/宿主业务代码。

**验收（AC）**
- **AC13-1**：dsh timeline 显示**真实数据**，条目数与数据源一致。
- **AC13-2**：**空会话显示空态而非 mock 4 条**（反例探针**必红**，AC6）。
- **AC13-3**：props shape 不变（`git diff` 只落在数据源与映射，**不动 `:137-143` 声明**）。

**验证**：e2e + 反例探针；`pnpm run check` 无新增错误。
**依赖 / 风险**：A10（trace/活动数据来源）；**L1**（独立客户端仓单文件换数据源）。**风险 U-7**：`ctx.sessions` 注入的实际结构契约未知 ⇒ 实施时**先读 DSH Slot 框架契约确认；契约未知则只做「宿主事件 → 既有 schema」映射，不改 DSH 侧**（U-9：`AgentStepRun` 未确认前页面标注「以 AgentRun/AgentStepRun 记录为准」）。
**对应 pilot 条款**：02-spec FR-5.3 / Output 活动条目 / U-7 / U-9、03-plan §B `panel.tsx` + T7、04-task AC6。

## Scope

> **2026-10-05 输入N+4 重写**：业务/端点/界面唯一落位 `Plugins/CostScope/`。下面 Allowed 里 `ForgeSelf.Api/**` 只剩「取数接缝 + 宿主库结构 + 构建登记」三类，`ForgeSelf.Web/**` 只剩「SSOT 登记」一处。依据与实测见 03-plan「Plan 偏差记录」。

### Allowed

**后端（宿主：只读取数接缝 + 结构 + 构建登记）**
- `ForgeSelf.Api/Entities/Model.xml`（加两列真源，A9/U-3）
- `ForgeSelf.Api/Entities/ChatTurn.cs` + `ChatTurn.Model.cs` / `ChatTurn.Biz.cs`（XCode **生成物**，须经 `xcode Model.xml` 产生，**禁手改**）
- `ForgeSelf.Api/Services/CostScope/TurnTelemetryQueryService.cs`（新建，`ITurnTelemetryQuery` 的**只读取数**实现，不含成本/聚合业务）
- `ForgeSelf.Api/AppBuilder.cs`（**仅** +1 行注册上面那条接缝）
- `ForgeSelf.Api/ForgeSelf.Api.csproj`（+1 行 CostScope 构建顺序引用 = **A2 缺口修复**）
- `Plugins/AIAgent/Services/ReactLoopAgent.cs`（**仅**写入 `AgentRunId`，不改 Agent 行为）
- `ForgeSelf.Api/Controllers/ChatController.cs`（**仅当 U-2 裁决为 (a)**，默认不动）
- `ForgeSelf.Abstractions/ITurnTelemetryQuery.cs` + `TurnTelemetryDtos.cs`（A1 已建；A4 需在此**扩** `GetModelCatalogAsync` + `ModelIdentityDto`）

**后端（插件：本 pilot 业务层唯一落位）**
- `Plugins/CostScope/plugin.json` + `CostScopePlugin.cs`（A2 已建，运行时 id `cost-scope`；服务注册在 `Apply` 内）
- `Plugins/CostScope/Data/Model.xml`（A2 已建，4 表真源 `CostModelPrice`/`CostBudget`/`CostTurnDaySummary`/`CostSettings` → `xcode`）+ `Data/CostScopeTables.cs`（建表真源，铁律 12）
- `Plugins/CostScope/Services/CostCalculationService.cs`（**A3 已建**，纯函数成本引擎）
- `Plugins/CostScope/Services/ModelPriceResolver.cs`（A4，取代宿主同名文件）
- `Plugins/CostScope/Services/{PriceCatalogService,BudgetService,TelemetryProjectionService,CostAggregationService}.cs`（A5/A6/A7）
- `Plugins/CostScope/Services/TraceProjectionService.cs`（A10，取代宿主 `TraceAggregator.cs`）
- `Plugins/CostScope/Controllers/CostController.cs`（A11，类级 `[Authorize("ApiKeyPolicy")]`，**取代**原宿主 `LlmObservabilityController`）

**前端**
- `Plugins/CostScope/web/**`（A12 新建：dashboard + `PricingPanel` + `BudgetPanel` + `TracePanel` + `http.ts`；产物 `web/dist/index.js`+`style.css`，导出名 == `views[0]`）
- `ForgeSelf.Web/src/data/features.ts`（SSOT 登记，宿主清单不迁插件）
- `ForgeSelf.Web/e2e/menu-route-consistency.spec.ts`（新插件 route 同步，铁律19③）
- `dsh-ui-bundle/my-dsh-activity-timeline-client/src/panel.tsx`（**仅**换数据源，props shape 不变）

**测试**
- `ForgeSelf.Api.Tests/Plugins/CostScopeTests/*.cs`（A3 起：`CostCalculationServiceTests` / `ModelPriceResolverTests` / `CostAggregationServiceTests` / `TraceProjectionServiceTests` / `CostController*Tests`）
- `ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj`（+1 行 CostScope `ProjectReference`，否则测试取不到被测类型）
- `Plugins/CostScope/web/src/**/*.spec.ts`（A12 插件侧 vitest）
- `ForgeSelf.Web/e2e/plugins/cost-scope/cost-scope.spec.ts`（A12/A13 插件层 e2e，零 mock）

**文档**
- `docs/02-features/`（新增功能文档）
- `docs/07-decisions/not-taken-decisions.md`（记「不做事」决策）
- `docs/ai/pilot/2026-10-03-llm-observability/{05-evidence,06-review,07-final-report}.md`

### Forbidden

| # | 禁止 | 依据 |
| --- | --- | --- |
| F0 | **在宿主侧新建成本/聚合/trace 服务或观测端点/视图**（`ForgeSelf.Api/Services/AI/Cost*`、`ModelPriceResolver`、`UsageAggregator`、`TraceAggregator`、`Controllers/LlmObservabilityController.cs`、`ForgeSelf.Web/src/views/LlmObservabilityView.vue`、`components/chatrecords/TraceWaterfall.vue`、`services/llmObservabilityApi.ts` 一律**不得创建**） | 2026-10-05 输入N+4「以插件为主，宿主侧的也迁移到插件」——消灭同一事实两套落位的双真相 |
| F1 | **写宿主库**：观测/成本代码不得对 `ChatTurn`/`SessionEvent`/`AgentRun` 执行 `INSERT/UPDATE/DELETE`（新列写入仅由既有 LLM 录制路径完成） | 规范 §1 约束 5；铁律 10；032 D4 |
| F2 | **改 `UsageStatsController` 补 `[Authorize]`** | 属独立 TODO（032 D9），不顺手改 |
| F3 | **在宿主库（`ForgeSelf.db`）新增成本/单价/预算表** | 成本数据须落在 CostScope 插件自有库（032 铁律12）；宿主库只读（F1） |
| F4 | **引入新依赖**（Langfuse SDK / OpenTelemetry / 图表库等） | 规范 §1 约束 4；NFR-1 |
| F5 | **改 `ChatController`** | 默认口径 U-2=c 不动；仅裁决 (a) 时按 T1 范围改 |
| F6 | **改 `SessionEvents.cs` 的 record 定义** | 040（B1–B8）正在触碰该文件，避免冲突面（Compatibility 条） |
| F7 | **改 `AgentRun` 实体加列** | 实测已有 `AgentId`/`SessionId`/`StepCount`/`TotalTokens`，复用即可 |
| F8 | **停/启/杀任何用户运行实例**（含 `:51888`） | AGENTS.md 发布规范铁律 1 |
| F9 | **自动 `git commit` / `push`** | 用户铁律：须显式授权 |
| F10 | **手写一次性 `temp/*.cjs` 作为验证** | AGENTS.md 红线 |
| F11 | **手改 XCode 生成物**（`ChatTurn.Model.cs`/`.Biz.cs`） | 铁律 9：走 `Model.xml` → `xcode` |
| F12 | **阶段3 质量评估 / LLM-as-judge** | 01-intent 范围界定；登记独立 TODO |
| F13 | **改其他无关文件** | 规范 §1 约束 6 |
| F14 | **改宿主 `ForgeSelf.Web/src/router/index.ts` / `components/chatrecords/ChatRecordDetail.vue` / `components.d.ts`** | 界面归插件、路由由 `plugin.json frontend` 声明（`plugin-development` 铁律3/19①）；宿主 `ChatRecordDetail.vue` 本批零改动 |
| F15 | **插件里自注册 `DAL.AddConnStr`** | 铁律 12：连接串由宿主 `XCodeConfig.PluginDbs` 统管 |

## Acceptance Criteria

> 与 02-spec AC 一一对应。**全部为闸门1 确认项。**

- [ ] **AC0（前置）**：闸门1 四项裁决（U-1/U-2/U-3/U-4）已获用户明确答复，且结论已回写 02-spec
- [ ] **AC1**：端点与页面显示覆盖度「已覆盖 N / 未计入 M（主聊天 legacy 接缝）」；无 usage 为 `null` 不伪造 0
- [ ] **AC1b**（仅 U-2=a 时）：主聊天会话事件带非空 `Usage`，且工具调用/流式 e2e 无回归
- [ ] **AC2**：成本端点按 模型/供应商/日/风格 四维返回 token+金额；未配单价模型进 `unpricedModels` **且不计入总额**
- [ ] **AC3**：成本纯函数被单测锁死（1M token 基准、小数精度、0 tokens、负值拒绝、改价后历史重算）
- [ ] **AC3b**：单价目录 CRUD 端点可用；未配单价的模型进 `unpricedModels` 且页面显式列出 + 给「去配单价」入口（FR-3.5 / 032 D5）
- [ ] **AC3c**：预算规则 CRUD + 实时达成率；超支显式横幅（FR-3.6）
- [ ] **AC3d**：`CostTurnDaySummary` 惰性物化（当日未结束每次重算、已结束缓存），插件只写自有库（FR-3.7）
- [ ] **AC3e**：`ITurnTelemetryQuery` 契约在 Abstractions、宿主实现只读三源；模型→供应商 4 级回退 + 未归属不猜（FR-3.8 / FR-3.9）
- [ ] **AC4**：waterfall 以 AgentRun 为根返回 LLM 调用 + 工具步骤时序，耗时非负且按时间排序
- [ ] **AC5**：延迟 P50/P95/P99 与手算一致（`FirstTokenMs` 与 `DurationMs` 分别出）
- [ ] **AC6**：dsh timeline 显示真实数据；**空会话显示空态而非 mock**（反例探针必红）
- [ ] **AC7**：dashboard 展示值与后端端点逐项一致（e2e + 组件测试，**禁 mock**）
- [ ] **AC8**：新端点无 token 调用返回 **401**；非法 `by=` 返回 **400**（不静默空表）
- [ ] **AC9**：`dotnet build` + `dotnet test` 通过；`pnpm run check` + `pnpm run test` 通过且**无新增错误**
- [ ] **AC10**：`features.ts` 登记新视图，`node scripts/check-features.mjs` 退出码 0
- [ ] **AC11**：Forbidden 逐路径 `git diff` 为空（`git status --porcelain -- <Forbidden 路径>` 零输出）
- [ ] **AC12**：`05-evidence.md` 按 **Verified / Inferred / Unknown** 三级标注，无「应该通过」类表述
- [ ] **AC13**：`06-review.md` 八问复核 + Final Decision；`07-final-report.md` 十一节齐备
- [ ] **AC14**：`scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-03-llm-observability` → **PASS**

## Expected Files

> 2026-10-05 输入N+4：业务/端点/界面唯一落 `Plugins/CostScope/`；宿主只剩「取数接缝 + 表结构 + 构建/DI/SSOT 登记」。

**新增 — 插件（业务层唯一落位）**
```
Plugins/CostScope/plugin.json
Plugins/CostScope/CostScopePlugin.cs
Plugins/CostScope/CostScope.csproj
Plugins/CostScope/Data/Model.xml
Plugins/CostScope/Data/CostScopeTables.cs                    # 建表真源（铁律12）
Plugins/CostScope/Data/Entities/Cost{ModelPrice,Budget,TurnDaySummary,Settings}*.cs   # xcode 生成 8 件
Plugins/CostScope/Services/CostCalculationService.cs         # ✅ A3 已完成
Plugins/CostScope/Services/ModelPriceResolver.cs             # A4
Plugins/CostScope/Services/PriceCatalogService.cs            # A5
Plugins/CostScope/Services/BudgetService.cs                  # A6
Plugins/CostScope/Services/TelemetryProjectionService.cs     # A7（惰性物化日汇总）
Plugins/CostScope/Services/CostAggregationService.cs         # A5/A6/A7（四维聚合 + 分位 + 错误率）
Plugins/CostScope/Services/TraceProjectionService.cs         # A10
Plugins/CostScope/Controllers/CostController.cs              # A11（取代宿主 LlmObservabilityController）
Plugins/CostScope/web/**                                     # A12（dashboard/Pricing/Budget/Trace + http.ts）
```

**新增 — 共享契约与宿主接缝**
```
ForgeSelf.Abstractions/ITurnTelemetryQuery.cs                # ✅ A1 已完成（A4 需扩 GetModelCatalogAsync）
ForgeSelf.Abstractions/TurnTelemetryDtos.cs                  # ✅ A1 已完成（A4 需加 ModelIdentityDto）
ForgeSelf.Api/Services/CostScope/TurnTelemetryQueryService.cs # 只读取数实现（搬不动的那一块）
```

**新增 — 测试**
```
ForgeSelf.Abstractions.Tests/TurnTelemetryContractTests.cs                     # ✅ A1
ForgeSelf.Api.Tests/Plugins/CostScopeTests/CostCalculationServiceTests.cs      # ✅ A3（17 格）
ForgeSelf.Api.Tests/Plugins/CostScopeTests/ModelPriceResolverTests.cs          # A4
ForgeSelf.Api.Tests/Plugins/CostScopeTests/CostAggregationServiceTests.cs      # A5/A6/A7
ForgeSelf.Api.Tests/Plugins/CostScopeTests/TraceProjectionServiceTests.cs      # A10
ForgeSelf.Api.Tests/Plugins/CostScopeTests/CostControllerAuthTests.cs          # A11（反射断言类级鉴权）
ForgeSelf.Api.Tests/Plugins/CostScopeTests/CostControllerEndpointTests.cs      # A11
Plugins/CostScope/web/src/**/*.spec.ts                                         # A12
ForgeSelf.Web/e2e/plugins/cost-scope/cost-scope.spec.ts                        # A12/A13
```

**修改（≤8，全部为登记/结构类）**
```
ForgeSelf.Api/ForgeSelf.Api.csproj            # ✅ A3 已改：+1 行 CostScope 构建顺序引用（A2 缺口）
ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj # ✅ A3 已改：+1 行 CostScope ProjectReference
ForgeSelf.Api/Data/XCodeConfig.cs             # ✅ A2 已改：PluginDbs 登记
ForgeSelf.Api/Entities/Model.xml              # A9 加两列（U-3 批准后）
ForgeSelf.Api/Entities/ChatTurn.Model.cs      # A9 xcode 生成
ForgeSelf.Api/Entities/ChatTurn.Biz.cs        # A9 xcode 生成
ForgeSelf.Api/AppBuilder.cs                   # +1 行注册只读取数接缝
ForgeSelf.Web/src/data/features.ts            # SSOT 登记（宿主清单，不迁插件）
ForgeSelf.Web/e2e/menu-route-consistency.spec.ts  # A12 新插件 route 同步（铁律19③）
Plugins/AIAgent/Services/ReactLoopAgent.cs    # A9 仅写 AgentRunId
```

**已取消（原计划宿主侧，2026-10-05 输入N+4）**
```
ForgeSelf.Api/Services/AI/{CostCalculator,ModelPriceResolver,UsageAggregator,TraceAggregator}.cs
ForgeSelf.Api/Controllers/LlmObservabilityController.cs
ForgeSelf.Web/src/views/LlmObservabilityView.vue
ForgeSelf.Web/src/components/chatrecords/TraceWaterfall.vue
ForgeSelf.Web/src/components/chatrecords/ChatRecordDetail.vue   # 不再挂载，零改动
ForgeSelf.Web/src/services/llmObservabilityApi.ts
ForgeSelf.Web/src/router/index.ts                               # 路由由 plugin.json frontend 声明
ForgeSelf.Web/src/views/LlmObservabilityView.test.ts
ForgeSelf.Web/e2e/llm-observability.spec.ts                     # 改走 e2e/plugins/cost-scope/
```

**工件（8）**：`docs/ai/pilot/2026-10-03-llm-observability/00`~`07`

## Verification Commands

> 2026-10-05 输入N+4 后按插件落位更新；跑测前按 AGENTS.md §5.0 设 `TEMP/TMP`（e2e 另设 `NO_PROXY`）。

```bash
# ── Build ──
cd ForgeSelf.Api && dotnet build           # 含插件（A3 起 CostScope 进构建图），并核对产物 Plugins/CostScope/CostScope.dll
cd ForgeSelf.Abstractions && dotnet build
cd Plugins/CostScope && dotnet build       # 插件单独构建（快速回路）

# ── Unit（插件业务，落 ForgeSelf.Api.Tests/Plugins/CostScopeTests）──
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~CostScope"
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~TurnTelemetryContract"

# ── Regression（受影响面）──
cd ForgeSelf.Api.Tests && dotnet test --filter "ChatTurnStreamRecorderTests|ChatTurnServiceTests|ChatSessionServiceTests|ChatRecordsControllerIntegrationTests"

# ── 宿主全量（§5.6 中档：碰宿主源码/工程/共享测试基建即必跑）──
cd ForgeSelf.Api.Tests && dotnet test

# ── Frontend（宿主侧仅剩 features SSOT 与 e2e）──
cd ForgeSelf.Web && pnpm run check && pnpm run test
cd Plugins/CostScope/web && pnpm run build   # A12 起；沙箱内改用 plugin-development §3.2 出树构建兜底

# ── e2e（禁 mock、真实登录；必须显式指定私有端口）──
cd ForgeSelf.Web
E2E_FRONTEND_PORT=7402 E2E_BACKEND_PORT=7502 npx playwright test e2e/plugins/cost-scope/cost-scope.spec.ts --workers=1

# ── SSOT 校验 ──
node scripts/check-features.mjs

# ── 工件链门禁（05/06/07 齐备后）──
pwsh scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-03-llm-observability

# ── 范围纪律（Forbidden 逐路径核对，须零输出）──
git status --porcelain -- ForgeSelf.Api/Controllers/UsageStatsController.cs ForgeSelf.Api/Services/UsageStats/ ForgeSelf.Abstractions/SessionEvents.cs Plugins/AIAgent/Data/Entities/AgentRun.cs
# F0（宿主侧不得再建成本/观测业务）：
git status --porcelain -- ForgeSelf.Api/Services/AI ForgeSelf.Api/Controllers/LlmObservabilityController.cs ForgeSelf.Web/src/views/LlmObservabilityView.vue ForgeSelf.Web/src/components/chatrecords/ ForgeSelf.Web/src/services/llmObservabilityApi.ts ForgeSelf.Web/src/router/index.ts
```

## 收口：原子任务全部有归属（2026-10-06）

**14 项 = 11 完成（A1/A2/A3/A3b/A4/A5/A6/A7/A10/A11/A12）+ 3 取消（A8/A9/A13）。**
本 pilot 的**代码交付已完成**；但**不等于功能可用**，以下事项仍未做，按性质分列（不掩盖）：

### 甲·功能性缺口（缺了功能在真实环境不成立）

| # | 事项 | 影响 |
|---|---|---|
| 1 | **AIAgent 侧 `IAgentRunTelemetryProvider` 未实现**（A3b 已外置该接口，A10 靠入参单测） | trace 页签在真实环境**拿不到 AgentRun**，只会显示「未关联」；关联/瀑布等于不可用 |
| 2 | ~~A12 未执行 Vite 构建~~ → ✅ **已解决（2026-10-06 22:1x）**：`pnpm install` / `pnpm check`（vue-tsc 0 错误）/ `pnpm build` 全通过，产物 `dist/index.js` + `dist/style.css` | 已解决；`dist/` 被 gitignore，由宿主发布流程产出 |

### 乙·验证缺口（做了，未被真实链路验证）

| # | 事项 | 影响 |
|---|---|---|
| 3 | 端点 **401 真实链路未验证**（类级 `[Authorize]` 仅由反射守住，未起运行时） | 鉴权声明写错不会被发现 |
| 4 | **无 e2e**：A11 全部为「直接实例化控制器」单测，**没有一条真实 HTTP 请求** | 路由、模型绑定、序列化、真实鉴权均未验证 |
| 5 | 界面**未运行时验证** | 见 #2 |

### 丙·按决策保留的已知缺口（已显式标注，非遗漏）

| # | 事项 | 决策依据 |
|---|---|---|
| 6 | **主聊天链路用量不可见**（`ChatController` 走 `IAIService`，签名即无 usage） | U-2 裁决 (c)：不迁接缝；端点与界面已显式声明，独立 TODO 已立 |
| 7 | **trace 关联是近似推断**（会话键 + 时间窗 ±30s），非事实外键 | A9 取消（宿主保持抽象）；界面顶部固定显示 `correlationNote` |
| 8 | `CostModelPrice.Model` **DB 级唯一索引未加**，FR-3.5 唯一性只在服务层保证 | Model.xml 索引非唯一（XCode 因此只生成 `FindAllByModel`） |
| 9 | **BC-8**：插件未启用时返回「未启用」而非 404 空页 | 插件未加载时路由不存在，**控制器内无法实现**，属宿主装载层职责 |

### 丁·既有技术债（非本 pilot 引入）

| # | 事项 |
|---|---|
| 10 | **宿主 30 个实体只有生成物、没有 Model.xml**（均生成于 2026-09-25）⇒ 铁律 9/11 在宿主侧从未被满足；实测重建不可行（索引名不一致会触发 `CheckDeleteIndex`、接口生成缺失），已 ABORT |

### 戊·收尾动作

| # | 事项 |
|---|---|
| 11 | **全部改动未 git 提交**（按用户铁律：只改文件，提交需显式授权） |

## 代码审查记录（2026-10-06 22:4x，Karpathy「结论先行 + 技术核对」）

**结论：发现 4 个真实缺陷（1 个 P0 功能完全不可用、2 个 P1 静默错数、1 个 P2 噪音），全部已修 + 补回归测试 + 探针实红。**

| 级别 | 缺陷 | 根因 | 修法 | 回归测试 |
|---|---|---|---|---|
| **P0** | `PUT /api/cost-scope/budgets` **必然 500**（编辑预算功能完全不可用） | `CostController.SafeUpdateBudget` 调的是 `BudgetService.Save`（**新增**通道），而 `Save` 对「同名 + 内容不同」抛 `InvalidOperationException`；`Guard` 只捕获 `ArgumentException` ⇒ 异常冒泡成 500 | `BudgetService` 补 `Update` 通道（要求已存在、不做 upsert）；控制器改走 `Update`；`Guard` 增捕 `InvalidOperationException` ⇒ **409**（状态冲突是客户端可纠正的，不该伪装成 500） | `UpdateBudget_存在且内容不同_改成功_不返回500`（**这正是当初漏掉的那类用例**——A11 只测了改价成功、没测改预算） |
| **P1** | **provider 作用域预算的「已用」恒为 0** | `BudgetAchievement` 只用 `Breakdown(by: Model)` 建字典，却拿 provider 预算的 `Target`（**供应商名**）去查 ⇒ 永远查不到 | 按作用域选字典：`global` 取全额、`provider` 用 `Provider` 维度、`model` 用 `Model` 维度 | `BudgetAchievement_provider作用域_按供应商维度实算_不恒为0` |
| **P1** | **历史日的失败数在缓存路径上凭空消失** | `CostTurnDaySummary` **表里没有 `FailCount` 列**，`ToPoint` 只能填 0 ⇒ 已结束日读缓存时失败数恒为 0（日趋势里失败数消失，且是静默的） | `Model.xml` 加 `FailCount` 列 + 重跑 `xcode` 重生成实体；`Upsert` 写入、`ToPoint` 还原 | `QueryWithCache_历史日的失败数从缓存还原_不静默丢成0` |
| **P2** | `unattributedModels` **语义混乱且制造噪音** | ① 把「没配单价」也塞进未归属（与 `unpricedModels` 双重计数，看不出到底缺什么）；② resolver 未接线时**全部模型**都进未归属 | 未归属只表示「有解析器但解析不出」；resolver 为 null 时不产出该列表 | `Overview_未接线供应商解析器_未归属列表为空_不制造噪音` + `Overview_有解析器但解析不出_计入未归属` |

**修复过程中我自己又引入并被测试抓住的一个 bug（如实记录）**：第一版 `Update` 直接调 `ApplyTo` 而**绕过全部校验** ⇒ 非法作用域/周期/限额能经 PUT 写进库。被新写的 `Update_非法内容_抛入参异常` 逮住。
⇒ 处置不是补一个洞，而是**抽出共用 `Validate` 让 Save / Update 走同一份校验**（根治，两个通道口径必然一致）。

**验证证据**：`--filter "CostScopeTests|TurnTelemetryQueryService"` → **227/227 通过**（`.temp/review-final.log`，EXIT=0，修复前基线 220/220，净增 7 条回归测试）；`TurnTelemetryContract` **6/6**；`Plugins/CostScope` 与宿主 `ForgeSelf.Api` 构建均 **0 错误**。
**P0 探针（MUTATION，实红）**：把 `SafeUpdateBudget` 退回 `Save` ⇒ **实红 1 条**（`UpdateBudget_存在且内容不同_改成功_不返回500`，`.temp/review-mutation.log`）；还原后复绿 32/32。

**审查中发现但本轮不改的性能/整洁项（低优先，如实列出）**：
- `LoadAsync` 的供应商解析 lambda **每条记录调一次** `ModelPriceResolver.Match`（未按模型记忆化）⇒ 大时间窗下重复做字符串匹配，建议加 `Dictionary` 缓存。
- `turns` 端点返回 `AgentRunId` 字段，但 A9 取消后该字段**恒为 null**（死字段）。
- 定价解析 `ModelPriceResolver` 本身本轮未逐行复核（属 A4 已验收范围）。

---

## 执行顺序与停止点（原子任务 DAG）

```
A1 遥测只读契约（根 ✅ 2026-10-05）
  │
  ├── A2 CostScope 插件骨架 ✅（A3 批补掉宿主 csproj 构建图缺口）
  │     └── A5 单价目录 CRUD（服务层；端点并入 A11）
  │
  ├── A3 纯函数成本计算器 ✅（17/17 + 两条反向探针实红）
  │
  ├── A3b 宿主只读取数实现 TurnTelemetryQueryService ⏳（**2026-10-06 补立**）
  │     │   —— 插件侧 A4/A5/A6/A7/A10 取宿主数据的唯一通道，此前无归属任务
  │     ├── A4 模型→供应商 4 级解析 ⏳（**前置**：先扩契约
  │     │       `GetModelCatalogAsync` + `ModelIdentityDto`，A1 产物实测没有）
  │     └── 供 A5/A6/A7/A10 消费（插件不直连宿主库，FR-4.6 / BR-5）
  │
  └── A8 Usage 接缝迁移 ⛔（**U-2**；默认 (c) 显式标注 ⇒ 整体跳过，裁决 (a) 才做）
        │ U-2 =(a) 才继续
        ↓
      A9 Trace FK 加列 ⛔（**U-3**，宿主库结构变更，须批准）
        │ U-3 批准后才继续
        ↓
      A10 Trace 聚合器（插件 `TraceProjectionService`）
        │
        ├─────────────────────────┐
        ↓                         ↓
      A11 插件 `CostController`   A13 dsh 时间线接真实数据
          （依赖 A10 + A3/A4      （依赖 A10；`panel.tsx` 换数据源，
           + A5/A6/A7 服务齐备）   **props shape 不变**）
        │
        ↓
      A12 插件 `web/` 可视化（dashboard + Pricing/Budget/Trace 三页签）
          + 宿主 `features.ts` 登记（SSOT 属宿主清单，不迁插件）
```

**⛔ 停止点**：
1. **A1 / A2 / A3 / A3b / A4 / A5 已执行完毕并验证**（2026-10-06）。**闸门1 已裁决**：U-2 = **(c) 不做主聊天接缝迁移**（按「功能一律插件化、主流程只调插件实现」方向，重复实现可后置）⇒ **A8 本轮不做**，转 FR-1.1/1.2 显式标注 + 独立 TODO；U-3 = **已批准**加列 ⇒ **A9 解除阻塞、且已与 A8 解耦，可立即执行**。
2. **剩余可执行**：A6、A7（服务层）→ A9（宿主加列，已批）→ A10 → A11（含覆盖度显式标注 FR-1.1/1.2）→ A12 → A13。
2. 任一验证失败 → **如实记录失败**（命令 + 真实输出 + 归因），**不得伪造通过**；修不了则标 `BLOCKED` 上报。
3. 发现 Plan 与仓库实际不符 → **先记 03-plan「Plan 偏差记录」再改 Plan**，不得绕过。（2026-10-05 已按此执行：输入N+4 的落位变更先记 5 行偏差再改 §A/§A2/§B/§C 与本文件。）
4. 05/06/07 **必须真实跑过 build/test 后**才产出；Evidence 按 Verified/Inferred/Unknown 标注，**禁止三者混用**。
5. **档位如实申报**：A3 碰了 `ForgeSelf.Api.csproj`（宿主工程）+ `ForgeSelf.Api.Tests.csproj`（共享测试基建）⇒ **§5.6 中档全量后端测试必跑**；宿主前端本批零改动 ⇒ 未跑前端门禁，汇报中不得声称「全绿」。
