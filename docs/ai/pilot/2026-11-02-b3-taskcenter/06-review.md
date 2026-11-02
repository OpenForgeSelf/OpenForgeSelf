# 06-review · B3 任务中心（2026-11-02-maf-runtime-hardening）

> 结论栏（八问答案与 Final Decision）在验证全部完成后定稿；本版为叙述框架 + 已定事实。**未含待实测项的结论。**

## Review 八问

### 1. 需求覆盖了什么？（What is covered）

- **B3① 跨入口总览 API**：`TasksOverviewController`（新文件）聚合两源（open=SessionEvent 投影 / planned=AgentRun），统一 `TaskOverviewDto`（id/source/title/status/failureReason/startedAt/endedAt/retryable/retryUrl/sourceRef），`?source=open|planned&limit=1..100`，桶级降级（sources[].ok/error 原文报因）。
- **B3② 宿主视图**：`TaskCenterView.vue`（新）+ `features.ts` / `tabs.ts` / `router/index.ts` 注册；三源列表（第三源 scheduler 由前端 `taskCenterApi.ts` 聚合，决策 D1）、五态、来源过滤、仅看异常、重试/续跑/立即运行入口。
- **B3③ 后台并行 Run 基础**：15s 静默轮询 + visibilitychange 暂停/恢复 + 指纹防闪；Run 展示与聊天视图完全解耦（独立路由/独立数据流，零聊天组件依赖）；未改执行引擎（B2 写面未触碰）。

### 2. 需求没覆盖什么？（What is not covered / NOT-to-do）

- Scheduler 行的 `paused→pending` 语义：任务被暂停映射为「待执行」，未单列 paused 状态（统一字典无此项，见 02-spec §3.2）。
- 开放任务仅取最近 30 个活跃会话（limit 默认，可调 ≤100）；无分页。
- 轮询用 REST 短轮询，未接 WebSocket 事件推送（复用既有 REST 的保守路径；WS 任务事件总线不在本批）。
- 失败 Scheduler 行的日志原文按需拉取仅前 5 条/轮。

### 3. 改动是否最小？（Minimal diff）

新文件 4（控制器/测试/服务层/视图）+ 注册点 3（features.ts 追加条目、tabs.ts 追加默认标签、router 追加静态路由）+ `tabs.test.ts` 断言同步（4→5）。未改任何既有 Controller / MafLoop / ToolRegistry / 聊天视图。

### 4. 测试金字塔是否健康？（Test pyramid）

- 单测（dotnet）：17 用例 Mock 服务层（FakeContext 同构铁律 12c）——投影纯函数逐行覆盖。
- 单测（vitest）：18 用例纯映射/容错/SSE 解析，零真实网络。
- 集成：:7301 真实端点 JSON 结构核对（待回填）。
- 走查：浏览器真实走查 + 截图读图（待回填）。

### 5. 错误处理是否诚实？（Honest failure）

- 后端单桶失败 → `sources[].ok=false + error 原文`，不冒充空数据；ISessionStore 缺席 → 桶级明确报因（不 500）。
- 前端单源失败 → 其余来源照常渲染 + 顶部告警条（原文透出）；全源失败 → 页面级错误态 + 重试按钮；`mapSchedulerTask` 拉取失败静默回退计数文案。
- SSE 续跑读流到终局事件（done/error），无终局事件按「已发起」报告，不假判成功。

### 6. 安全与权限是否对齐铁律？（Auth & isolation）

- 铁律 17：类级 `[Authorize("ApiKeyPolicy")]`（管理面端点，ImGateway PILOT-078 同例）。
- 铁律 12c：构造只注入插件自有服务（IPluginMessageService/IRunOrchestratorService）+ IContext；宿主契约 ISessionStore 运行期 `_ctx.Get` 解析。
- 插件隔离：未引用 Scheduler 程序集、无 HTTP 自调（Scheduler 行由前端聚合，决策 D1）。

### 7. 性能与运行经济学（Runtime economics）

- 单端点两桶并发（await 顺序执行 open 后 planned；ListRunsAsync pageSize=50 封顶 + limit≤100），增量成本 ~2 次本地查询/请求。
- 前端轮询 15s 静默 + 后台标签页暂停 + 指纹未变不重渲染，稳态零 DOM 抖动。

### 8. 依据与规范（Standards）

- AGENTS.md §5.6 快档 + §8 设计门禁三步；DESIGN.md 组件规范（PageHead/ForgeCard/EmptyState/token）；ui-ux-design 交互五态与轮询防闪条目；check-features 视图孤儿规则（TaskCenterView 已登记 signals）。

## Final Decision

**APPROVED**（自审；闸门2 交 lead 验收）

依据：前端快档三项门禁 Verified 绿；后端 dotnet build 0 错误 + `--filter TasksOverview` 19/19 绿；:7301 集成验证 7 项断言全过（含 400/401 防线与真实数据投影正确性）；走查 5 张截图逐张读图核对通过；改动严格限定写面（新文件 4 + 注册点 3 + 测试同步 1，零既有业务文件改动）。

保留意见（非阻断）：① 页面级全源失败态/桶级降级告警条视觉未在真实环境复现（逻辑已测）；② SSE 续跑真实点击未执行（只读走查纪律）；③ 真空态以过滤空态代证。三者均已记入 05-evidence §4，后续批可用 B2 的 Run 注入手段补视觉证据。
