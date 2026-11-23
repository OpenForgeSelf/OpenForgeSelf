# Repository Understanding

> Task ID：PILOT-087｜日期：2026-11-23｜任务：sems 运行功能「停止」无反馈缺陷修复

## 1. 项目用途与技术栈（实测复核）

- 宿主：ForgeSelf.Api（.NET 10 + SQLite + NewLife.XCode）+ ForgeSelf.Web（Vue 3.5 + Vite + TS + pnpm）。
- 插件：`Plugins/Sems`（id `sems`），后端 Controllers/Services + 自带 `web/`（独立构建 ESM，宿主远程加载）。
- 测试：后端 `ForgeSelf.Api.Tests/Plugins/Sems/`（xUnit，RunnerService 真实进程集成测试已有）；插件前端 vitest（`Plugins/Sems/web/src/*.spec.ts`、`workbench/regression-fixes.spec.ts`）；插件层 e2e `ForgeSelf.Web/e2e/plugins/sems/sems.spec.ts`。

## 2. 触碰区域现状（逐文件读过）

### 后端停止链路
- `Plugins/Sems/Services/RunnerService.cs`：`IRunnerService.Stop(commandId)` 返回 `bool`。会话表 `_launched`（ConcurrentDictionary）在进程 Exited / 停止成功时移除条目。**会话不存在（进程早已退出、宿主/插件重启）与「杀进程失败」都折叠成 `false`**，调用方无法区分。
- `StopExternal(pid)` 同样只返回 `bool`：`taskkill` 失败（含「进程不存在」）一律 false。
- `Plugins/Sems/Controllers/ProjectCommandsController.cs:92-98`：`Stop` → false 时 404 `{success:false, message:"未找到存活的会话：{id}"}`。
- `Plugins/Sems/Controllers/RunsController.cs:43-50`：`StopExternal` → false 时 404「无法停止进程（不存在或无权限）」。
- `Plugins/Sems/ToolExtensions.cs:484-538`：MCP 工具 `sems_stop_command`/`sems_stop_run` 直接调 `Stop`/`StopExternal`，false → Fail 文案。

### 前端停止链路（两条入口）
- 详情区（运行页任务详情）：`workbench/workbenchStore.ts:757-766` `stopTask()` → POST `/api/commands/{id}/stop`；成功 toast「已停止」，失败 toast「停止失败：…」。**不解析结果语义；失败后状态不收敛，任务永远停在 running。**
- 详情区停止按钮 `views/ProjectTasksView.vue:164`：`:disabled="status !== 'running'"` —— **状态一旦卡在 running 之外的错误侧（done/idle 但用户认为在跑），点击无任何反应（禁用态无声）**。
- 会话面板：`RunPanel.vue` `stopBy()` 经 `confirmOps.runWithConfirm`，done→success toast、failed→error toast、cancelled→静默（弹窗本身即反馈）；结束条目隐藏停止按钮。
- 状态机卡「运行中」的根因：`workbenchStore.ts:696-723` `pullTaskLogs()` 对 **404 + running** 直接 `return`（注释称启动瞬态），但日志缓冲区在**宿主/插件重启后不存在**，该分支变成无限轮询 404、永不收敛（用户实测 `GET /api/commands/6/logs?after=0` 404 即此态）。`taskStatusOf` 还会从持久化 `runHistory` 恢复 `running` 状态，页面刷新后同样卡住。

### 用户现场佐证（:51888 运行实例）
- `GET /api/commands/6/logs?after=0` → 404：该命令无日志缓冲（非本面板启动/宿主或插件重启过）→ 与「进程早已停止、界面仍可点停止且无反馈」同根因。
- `GET /api/smart-terminal/inbound` → items 空列表：SmartTerminal 插件的跨进程收件箱语义，空=正常，与本次缺陷无关（已核实 `Plugins/SmartTerminal/docs/03-架构与接口.md`）。

## 3. 需求真源

- `Plugins/Sems/docs/01-requirements.md` F09（停止会话）——本轮在该节增补停止反馈验收标准。
- `Plugins/Sems/docs/02-test-plan.md` F09——同步补测试步骤/预期。

## 4. 测试基建（本批要落测试的位置）

- 后端：`ForgeSelf.Api.Tests/Plugins/Sems/RunnerServiceTests.cs`（真实进程：`Stop_UnknownCommand_ReturnsFalse`、`StopExternal_UnknownPid_ReturnsFalse` 等，直接改造为新语义断言）；`SemsControllerTests.cs`（Mock IRunnerService，断言状态码矩阵）；`SemsToolExtensionTests.cs`（工具文案契约）。
- 前端：`Plugins/Sems/web/src/workbench/regression-fixes.spec.ts` 的 harness 模式（stubGlobal localStorage + vi.mock('../http') + resetModules）——新建 `workbench/stop-feedback.spec.ts` 复用该模式。
- e2e：`e2e/plugins/sems/sems.spec.ts` 已有「启动→停止（确认路径）」冒烟段，本批不改其主链路。

## 5. 适用问题类（docs/11-troubleshooting/problem-classes/README.md tags 检索）

| PC | 结论 |
|---|---|
| PC-024（前端轮询面错误处理缺失：静默吞 404/终态错误继续轮询） | **命中（lens 复发）**：sems `pullTaskLogs` 对「404+running」无限吞错轮询、终态不收敛，属 PC-024 不变量在 sems 轮询面的同型缺口（该面在 PC-024 立类时只应用了「冷态 404 提示一次」半个模式，缺「恢复型/终态核对收敛」）。按技能铁律 3，同根因修复属本任务范围：补「404+running → 核对 /api/runs 一次 → 不在则收敛为已结束并留痕一次」，PC-024 增记复发与守卫。 |
| PC-009 / PC-008（响应 data 与前端契约 / 204 空体） | 相邻但不同根因（本批响应始终有 JSON 体），不改 fetch 封装。lens 进 Spec：停止响应为完整 JSON 对象，前端按 `outcome` 字段解析。 |
| PC-004（Agent 成功回报 ≠ 进程真实退出，候选） | 相邻主题（状态与真实进程一致性），但面不同（AIAgent 回报 vs sems 手动停止），不并批；lens 记入 Spec。 |

## 6. 候选低风险任务与选择理由

本批就是用户指认缺陷：sems 停止操作在「进程已不在」时无反馈、状态不收敛。改动面集中在 sems 插件内（后端停止结果语义 + 前端停止反馈/收敛），不触宿主内核、不触鉴权、无 DB 迁移，属插件内缺陷修复（风险 L2）。
