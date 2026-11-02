# 05-evidence · B3 任务中心（2026-11-02-maf-runtime-hardening）

> Evidence 只记实际发生（Verified / Inferred / Unknown 分级）。
> 状态：**全部实测完成**（前端三项门禁、后端构建+过滤集、集成验证、走查截图+读图）。

## 1. 前端门禁（快档）—— Verified

执行环境：`$env:NO_PROXY='localhost,127.0.0.1,::1'`、`$env:TEMP=$env:TMP=<repo>\.temp\tmp`（§5.0 前置）。

### 1.1 `pnpm run check`（vue-tsc -b + eslint + 组件规范守卫）

- 结果：**PASS（exit 0）**，`0 errors, 166 warnings`（warnings 全部为 vue/max-attributes-per-line、vue/attributes-order 等既有风格类）。
- 修复轨迹：`taskCenterApi.test.ts` 的 fetch mock 三次类型打磨（TS2352/TS2493 → TS6133 → `(_url: unknown, _init?: RequestInit)` 元组取参）。
- 组件规范：`[component-conventions] PASS（story 4 个 / ct.spec 4 个，均已注册、配对、标注）`。

### 1.2 `pnpm run test`（vitest）

- 结果：**PASS**，`Test Files 91 passed (91) / Tests 1059 passed (1059)`。
- 本批新增：`src/services/__tests__/taskCenterApi.test.ts` 18 用例（Scheduler 状态字典映射 ×6、日志原文优选 ×1、三源聚合容错 ×3、SSE 续跑 ×5、run-now 封套 ×3）。
- 行为同步：`src/stores/tabs.test.ts` DEFAULT_TABS 断言 4 → 5（新增 `/task-center` 默认标签，属本批契约变更，非迁就式改断言——行为变更点已注释）。

### 1.3 `pnpm run design:check`（棘轮守卫）

- 结果原文：
  - `[style-tokens] 硬编码色值合计 1759（基线 2501）` → `PASS：无新增硬编码色值，较基线减少 742 处`
  - `[design-compliance] a(token族)=209 b(胶囊)=0 c(玻璃过头)=0 d(蓝紫渐变)=0（基线同）` → `PASS`
- 说明：TaskCenterView 全部走既有 token（--fg-*/--gold-*/--cinnabar-400/--el-color-*/color-mix），features.ts 新条目 color/bgColor 用 `var(--gold-400)`/`var(--gold-tint)`。

## 2. 后端构建 + 过滤集测试 —— Verified

### 2.1 `dotnet build ForgeSelf.Api.Tests`

- 第一次构建（lead 授予窗口 #1）：4 errors。
  - **我的**：CS0102 `TaskSourceSummary` 属性 `Ok` 与工厂方法 `Ok` 同名冲突 → 已修（工厂方法改名 `CreateOk`/`CreateFail`，3 处调用点同步；lead 批准）。
  - **非我写面（B2/verify-dev 半落地）**：CS0246 `AgentRunModels.cs(140)` `RunExitCriterion`、`(185)` `RunVerifyStatus`（全仓 grep 无定义）；CS0535 `TodoTracker/TaskExecutionService.cs(21)` `AppendSystemAsync(13 参)` 接口不匹配。lead 裁决由 verify-dev 落齐。
  - 原始日志：`.temp/tmp/b3-dotnet-build.log`。
- 第二次构建（窗口 #2，verify-dev 编译绿后，含其在 ：295 的 `(DateTimeOffset?)` 三元最小修复）：**0 错误**（462 警告均既有），7.2s。原始日志：`.temp/tmp/b3-dotnet-build2.log`。

### 2.2 `dotnet test --filter TasksOverview`

- 首跑：15 过 / 3 失败——三处均为**测试装配/断言问题**，非实现缺陷：
  - 两个 limit 用例用无 Setup 的 `Mock.Of<ISessionStore>()`（Replay 返回 null → 投影 NRE → 桶级报错）→ 换 `EmptyStore()` 助手；控制器同步加一行 null 防御（`Replay(...) ?? 空数组`）。
  - `GetOverview_NoData_...` 断言与行为不符（种子了 1 个无 turn 事件的会话，正确行为=1 条 unknown 行）→ 拆为 `GetOverview_NoSessionsAndNoRuns_EmptyItemsBothOk` + `GetOverview_SessionWithoutTurnEvents_ProducesUnknownRow`。
- 复跑：**通过 19 / 失败 0 / 总计 19**（93 ms）。原始日志：`.temp/tmp/b3-dotnet-test2.log`。

## 3. 集成验证（:7301 真实 dev 后端）—— Verified

全部经 `Invoke-RestMethod` + Bearer token 实测：

| 断言 | 结果 |
|------|------|
| GET /api/ai-agent/tasks-overview | `success=True`，sources=[open count=12 ok, planned count=4 ok]，items=16 |
| 投影正确性（真实数据） | stuck 行 failureReason=「等待工具审批：write_file」（未闭合审批投影）；failed 行=「执行失败（Response status code…）」（attempt ErrorKind）；planned stuck 行 failureReason 取自 StuckReason 原文 |
| `?source=planned` | sources 仅 planned，items 4 条且 source 全为 planned |
| 失败项契约 | `{"id":"planned:4","status":"stuck","retryable":true,"retryUrl":"/api/ai-agent/runs/4/resume",…}` |
| `?limit=1&source=open` | items=1（钳位生效） |
| `?source=bogus` | HTTP 400 |
| 错误 token | HTTP 401（铁律 17 ApiKeyPolicy 生效） |

## 4. 走查截图（:7399，已逐张读图）—— Verified

落点：`ForgeSelf.Web/screenshots/dev/taskcenter-20261102/`

| 文件 | 内容与读图结论 |
|------|----------------|
| 01-normal-list.png | 默认标签含「任务中心」；PageHead（eyebrow Tasks·任务中心 + 刷新 + 更新时间）；全部 16 条，来源色标/徽标/失败原因/时间/重试按钮齐全；CRAP 对齐无遮挡 |
| 02-planned-filter.png | 计划任务段选中（金染高亮），仅 4 条 planned 行，标题截断+title 提示 |
| 03-only-issues.png | 仅看异常：恰 8 条（5 open + 3 planned），全部失败/卡住徽标 + 失败原因 + 重试 |
| 04-filter-empty.png | 定时任务+仅看异常 → 「当前过滤条件下没有任务」（区分真空态与过滤空） |
| 05-row-nav-aiagent.png | 行点击 → openPage 跳转 /ai-agent 并定位对应会话（Run 展示与聊天解耦的来源页入口） |

**走查符合性（ui-ux-design 清单）**：PageHead/ForgeCard/EmptyState 组件规范 ✓；token 引用无裸色值（design:check 棘轮同证）✓；无胶囊 9999px 按钮 ✓；mono 字体用于时间/原因/统计 ✓；状态徽标五色映射语义一致 ✓。

**诚实的未复现项（Unknown，视觉）**：
- 页面级全源失败态与桶级降级告警条未在真实环境复现（现环境两桶全 ok）——逻辑由 19 个后端用例 + 3 个前端容错用例覆盖（Verified），视觉未走查。
- 真空态（零任务）未复现（真实数据非空），以过滤空态代证 EmptyState 渲染路径；EmptyState 组件本身有独立测试。
- SSE 续跑按钮未真实点击（会真实改写 Run 状态，违反只读走查纪律）；SSE 消费逻辑由 5 个前端用例覆盖。

## 5. 环境事件

- dev-stack 前端 :7399 于走查前下线（frontendPid 7476 不存在，frontend 日志停在 20:39:33 无报错尾部）；按分工未自行启停，已报 lead。lead 窗口 #1 期间已停 dev-stack 释放 DLL 锁，重拉由 lead 统一执行。
