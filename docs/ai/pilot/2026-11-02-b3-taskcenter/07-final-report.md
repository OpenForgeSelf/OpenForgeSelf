# 07-final-report · B3 任务中心（2026-11-02-maf-runtime-hardening）

> 状态：**✅ COMPLETED（无阻断风险）**——实施、验证、集成、走查全部闭环；三项诚实的视觉未复现项见 §6（逻辑已测，非阻断）。

## 1. 交付内容（What shipped）

| 文件 | 动作 | 说明 |
|------|------|------|
| `Plugins/AIAgent/Controllers/TasksOverviewController.cs` | 新 | GET api/ai-agent/tasks-overview：open（SessionEvent 投影）+ planned（AgentRun）双桶统一 DTO；桶级降级；铁律 12c/17 |
| `ForgeSelf.Api.Tests/Integration/TasksOverviewControllerTests.cs` | 新 | 17 用例（Mock 服务层 + FakeContext），投影规则表逐行 + 契约断言 |
| `ForgeSelf.Web/src/services/taskCenterApi.ts` | 新 | 三源聚合（+scheduler TaskType=3 映射）、SSE 续跑消费、run-now、失败原因按需拉取 |
| `ForgeSelf.Web/src/services/__tests__/taskCenterApi.test.ts` | 新 | 18 用例（映射/容错/SSE/封套，零真实网络） |
| `ForgeSelf.Web/src/views/TaskCenterView.vue` | 新 | 任务中心视图：五态、过滤、仅看异常、15s 静默轮询、指纹防闪、token 全对齐 |
| `ForgeSelf.Web/src/data/features.ts` | 改 | 追加 task-center 条目（token 色值，signals 末置） |
| `ForgeSelf.Web/src/stores/tabs.ts` | 改 | DEFAULT_TABS 追加 /task-center |
| `ForgeSelf.Web/src/stores/tabs.test.ts` | 改 | 断言同步 4→5（行为变更点注释） |
| `ForgeSelf.Web/src/router/index.ts` | 改 | 追加静态路由 /task-center（写面偏差，mini-task 已声明） |
| `02-b3-taskcenter/` 工件 | 新 | mini-task.md、02-spec.md（含 Interaction Design）、05/06/07 |

## 2. 关键决策（Decisions）

- **D1 Scheduler 行前端聚合**：插件间禁止程序集引用/HTTP 自调（ScheduledAgentRunContracts.cs 既有裁决），Abstractions 无 Scheduler 读侧契约 → 后端只聚合 open+planned，scheduler 行由前端调既有 GET /api/scheduler/tasks 过滤 TaskType=3。
- **D2 开放任务投影**：会话枚举（插件自有 IPluginMessageService）+ ISessionStore.Replay 逐会话扫 turn 事件（宿主契约经 IContext 运行期解析，铁律 12c 同 AIChatController）。
- **D3 统一状态字典**：running/planning/pending/completed/failed/stuck/cancelled/scheduled/unknown（02-spec §3.2 为契约真源）。
- **D4 鉴权**：类级 ApiKeyPolicy（铁律 17 + PILOT-078 先例）。
- **D5 工厂方法改名**：`TaskSourceSummary.Ok/Fail` 方法与 `Ok` 属性同名冲突（CS0102）→ `CreateOk/CreateFail`（lead 批准）。

## 3. 验证结果（Verified / Inferred / Unknown）

| 门禁 | 结果 | 等级 |
|------|------|------|
| pnpm run check | PASS 0 errors（166 warnings 既有风格类） | Verified |
| pnpm run test | PASS 91 文件 / 1059 用例 | Verified |
| pnpm run design:check | PASS（1759 ≤ 基线 2501；a209/b0/c0/d0 无新增） | Verified |
| dotnet build | 窗口 #1：修我的 CS0102；窗口 #2（verify-dev 落齐 B2 类型后）：**0 错误** | Verified |
| dotnet test --filter TasksOverview | **19/19 全过**（首跑 3 失败均为测试装配/断言问题，已修正复绿） | Verified |
| :7301 集成验证 | 7 项断言全过：success=True、open 12 + planned 4、投影正确（审批卡住/失败原因原文）、source 过滤、limit 钳位、400/401 防线 | Verified |
| 走查截图 + 读图 | 5 张逐张读图核对通过（screenshots/dev/taskcenter-20261102/） | Verified |

原文明细 → 05-evidence.md。

## 4. 代价与收益（运行经济学）

- 新增端点每请求 ~2 次本地查询（open 枚举+Replay、planned 分页 50 封顶），无新增表/迁移，无后台常驻。
- 前端 15s 静默轮询 + 后台暂停 + 指纹防闪：稳态零请求节流外开销为零 DOM 重排。
- 收益：三入口任务状态一屏可见 + 失败可重试入口，补齐 MAF Run 可观测的「总览」缺口。

## 5. NOT-to-do（同步 docs/07-decisions/not-taken-decisions.md）

- 未接 WebSocket 任务事件总线（保守复用 REST，后续批可选）。
- 未做开放任务分页/搜索（30 条窗口已覆盖个人规模）。
- 未把 paused 提升为独立状态（统一状态字典最小化，02-spec §3.2）。
- 未改 MafLoop / 既有 AgentRunsController / ToolRegistry（B2 写面与禁区）。

## 6. 风险与移交（Risks & handoff）

- 三项诚实的视觉未复现项（逻辑已测、非阻断）：① 全源失败态/桶级降级告警条未在真实环境复现（现环境两桶全 ok）；② 真空态以过滤空态代证（真实数据非空）；③ SSE 续跑按钮未真实点击（只读走查纪律，避免真实改写 Run 状态）。后续批可用 B2 的 Run 注入手段补视觉证据。
- 续跑 SSE 端点为流式响应，B2（verify-dev）与我的服务层已共同约定 done/error 终局事件；如 B2 调整事件名需同步 `taskCenterApi.ts`（一处常量级修改）。
- dev-stack 前端 :7399 曾下线（pid 消失无报错尾部），lead 已统一重拉并恢复。

## 7. 最终状态（五步自检）

1. **Requirement Check**：B3①②③ 全部满足——统一 DTO 总览 API（三入口：后端两桶 + 前端 scheduler 桶）、宿主视图（五态/过滤/重试入口/设计对齐）、15s 静默轮询与解耦展示（未触碰执行引擎）。✅
2. **Implementation Check**：写面内新文件 4 + 注册点 3 + 测试同步 1；铁律 12c/17 落实；插件隔离（无 Scheduler 程序集引用/HTTP 自调）。✅
3. **Validation Check**：7 项门禁全 Verified 绿（见 §3 表）。
4. **Evidence Check**：05-evidence 全量原始结果（门禁日志、集成断言表、截图读图结论）。
5. **Risk Check**：§6 三项视觉未复现（逻辑已测）+ SSE 事件名耦合 B2（已声明同步点）。无其他。

**结论：✅ COMPLETED，可交付，建议 Code Review（lead 闸门2 验收 + 统一提交）。**
