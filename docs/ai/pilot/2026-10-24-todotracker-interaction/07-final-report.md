# AI-Native Pilot 结果：TodoTracker 交互调整

## 1. 仓库理解

已阅读 TodoTracker 插件、交互原型、AI-Native 流程规范及插件相关技能；方案经用户批准后实施。

## 2. 选定任务

依据桌面待办原型改善任务列表和看板、阶段筛选、详情结构及动作反馈，并验证真实任务闭环。

## 3. 变更文件

见 `05-evidence.md` 的变更文件清单。另有本次 pilot 文档及 TodoTracker 功能文档更新。

## 4. 验证

- 插件构建：`pnpm run build` PASS；插件 eslint 0 error（151 warning）。全前端 `pnpm run check` / `pnpm run build` exit 0。
- 后端构建：`dotnet build ForgeSelf.slnx -c Debug` 0 错误。
- 后端测试：TodoTracker 域 + AC14 定向集 **181/181 通过**（含 `CompleteThenReopen_Flow_ShouldFlipStatus`、`GetTodos_WithPriority_ShouldFilterAcrossMatchingTasks`、`ExitToolTurn_RequiresToolChoice`、`ChatStream_ForwardsRequiredToolChoice`）。
- 冒烟 e2e：唯一一条 `e2e/plugins/todo-tracker` **1 passed / 59.8s**，截图 `ForgeSelf.Web/screenshots/e2e/todo-tracker/todo-tracker-main-flow.png` 已读图。
- 真实 Agent：隔离实例真实下发内置 Agent，Run 1 两步均 Completed，待办经「待验收」流转到「完成」（承接工件 `2026-10-25-todo-builtin-agent-done`）。
- AC1～AC14 逐条证据：见 `05-evidence.md` §验收清单逐条核对。

## 5. 证据

详细输出、截图、真实 Agent 状态与限制见 `05-evidence.md`。真实运行实例使用隔离数据目录；源码服务仍运行在本地端口 7301/7399。

## 6. 审查

见 `06-review.md`。结论：**APPROVED**。

## 7. 风险

L2。无遗留阻断项。发布已按 §8.5 批准后执行（2026-10-25）：本地目录更新源出包 `2.4.14.2610250925`（签名 3/3 有效、SHA256 一致、L1/L2 布局通过），运行实例 `:51888` 经宿主 update-agent 自更新至该版本；只读复验通过（token 200、`todo-tracker`=1.2.0 / `ai-agent`=1.9.5、徽标 v1.2.0、流水线与看板/详情三页签/优先级筛选实走，截图存 `ForgeSelf.Web/screenshots/live-51888/`）。

## 8. 发现的问题

- 旧完成端点 HTTP 500 空引用：R2 已修（三处同类空安全），兼容测试转绿。
- 真实模型请求 401：根因为内置引擎硬编码不存在的模型 + 静默回退 + 空响应当成功（PC-013），已修并真实跑通。

## 9. 流程评价

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PASS |
| Code → Test | PASS |
| Test → Evidence | PASS |
| Evidence → Review | PASS |

## 10. 问题类归档

PC-013「内置引擎静默模型回退 + 空响应当成功」已归档（`docs/11-troubleshooting/problem-classes/PC-013-silent-model-fallback-empty-response.md`），案例见 `docs/11-troubleshooting/cases/2026-10-25-builtin-agent-silent-model-fallback.md`。

## 11. 重构提案

R0：本次无跨模块重构提案；AC14 只是把既有 `ToolChoice` 字段接到既有请求构造处，未改动架构接缝。

## 12. 悬空教训清单

- ~~真实 Agent 凭据/可用执行端依赖~~：已由 `2026-10-25-todo-builtin-agent-done` 真实跑通闭环。
- ~~最终插件构建、e2e 和源码 UI 走查~~：已在 §4 全部复跑通过。

## 13. 后续收口（2026-10-25）

延续任务 `docs/ai/pilot/2026-10-25-todo-builtin-agent-done/` 已闭环本报告遗留项，本报告 BLOCKED 结论据此更新为「代码与验收完成，发布已完成并复验通过」：

- **R3 真实下发到 Done**：根因为内置引擎硬编码不存在的模型 + `ResolveProvider` 静默回退 + 空响应当成功（PC-013）。修复后隔离实例待办 10 真实下发 → Run 1 `status=3 Completed` → `stage=Done`（详见该工件 `05-evidence.md`）。
- **R2 旧完成端点 500**：根因为 `ActorHint()`/基址拼接在单元直构控制器下 NRE（同类扫查 `TodosController` / `TaskExecutionsController` / `TodoDispatchController` 三处），已修；`CompleteThenReopen_Flow_ShouldFlipStatus` 转绿，TodoTracker 域 165/165。
- **R1 构建 / e2e / UI 走查**：插件前端 `pnpm run build` PASS；冒烟 e2e 1 passed / 59.8s（修正过期选择器 `.td-head-title input` → `#td-title`）；截图 `ForgeSelf.Web/screenshots/e2e/todo-tracker/todo-tracker-main-flow.png` 已读图。
- **发布与复验（2026-10-25 已完成）**：本地目录更新源出包 `2.4.14.2610250925`（`-Sign` 校验 3/3 Valid、SHA256 一致、L1/L2 通过），`:51888` 由 update-agent 自更新；只读复验通过（版本徽标 v1.2.0、流水线合计闭合、看板 8 列、详情三页签、Draft 仅「就绪」、优先级筛选 P2 → 共 1 条），截图 6 张存 `ForgeSelf.Web/screenshots/live-51888/`。
