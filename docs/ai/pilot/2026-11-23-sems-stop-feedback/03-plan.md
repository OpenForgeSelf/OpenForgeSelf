# Plan

> Task ID：PILOT-087｜实现顺序：先红灯（测试复现）→ 后端语义 → 前端反馈 → 文档同步 → 门禁

## 1. 红灯复现（先于实现）

| 步骤 | 文件 | 动作 | 预期结果 |
| --- | --- | --- | --- |
| R1 | `ForgeSelf.Api.Tests/Plugins/Sems/RunnerServiceTests.cs` | 新增/改造：`Stop_UnknownCommand_ReturnsAlreadyStopped`、真实进程「启动→停→再停 → AlreadyStopped」、`StopExternal` 二次调用 → AlreadyStopped | 旧 `bool` 契约下编译失败/断言失败 = 红 |
| R2 | `ForgeSelf.Api.Tests/Plugins/Sems/SemsControllerTests.cs` | 新增三态矩阵断言：stopped→200+outcome、already-stopped→200+outcome、failed→409+success:false | 红（旧 404 契约） |
| R3 | `Plugins/Sems/web/src/workbench/stop-feedback.spec.ts`（新） | 复用 regression-fixes harness：①404+running 无限轮询不收敛（现状即红）②stopTask 对 404 只报失败不收敛（现状即红） | 红 |

## 2. 后端实现（类级修复）

| 文件 | 动作 |
| --- | --- |
| `Plugins/Sems/Services/RunnerService.cs` | 新增 `StopOutcome` 枚举 + `StopResult`；`IRunnerService.Stop/StopExternal` 签名改返回 `StopResult`；`KillTree` 改回传 `(bool Ok, string? Error)`；会话不存在/进程已退出路径 → `AlreadyStopped`（含如实 Reason），真正失败 → `Failed` |
| `Plugins/Sems/Services/IRunnerService.cs`（接口若独立成文件则同步） | 接口签名同步 |
| `Plugins/Sems/Controllers/ProjectCommandsController.cs` | `Stop`：Stopped/AlreadyStopped → 200 `{success:true, outcome, message}`；Failed → 409 `{success:false, outcome:"failed", message}` |
| `Plugins/Sems/Controllers/RunsController.cs` | `StopExternal` 同映射 |
| `Plugins/Sems/ToolExtensions.cs` | 两工具：Stopped → Ok（输出键不变）；AlreadyStopped → 逐字沿用既有 Fail 文案（F25）；Failed → Fail 带原因 |

## 3. 前端实现

| 文件 | 动作 |
| --- | --- |
| `Plugins/Sems/web/src/workbench/workbenchStore.ts` | `stopTask`：outcome 三分支（stopped / already-stopped / failed）+ not-running 早退分支（含 toast 与刷新）；`pullTaskLogs` 404+running 分支增加「核对 /api/runs 一次 → 不在则 markTaskDone + 留痕一次」（PC-024） |
| `Plugins/Sems/web/src/confirmOps.ts` | `ConfirmResult` 增加 `data?: unknown`，runWithConfirm 透传 action 结果 |
| `Plugins/Sems/web/src/RunPanel.vue` | `stopBy` 按 outcome 分支反馈（already-stopped → info 提示 + 刷新列表） |
| `Plugins/Sems/web/src/workbench/views/ProjectTasksView.vue` | 停止按钮：选中即启用（非 running 时点击走 not-running 反馈）；进行中禁用防重复提交 |
| `Plugins/Sems/web/src/types.ts` | 新增 `StopRunResp { success, outcome?, message? }` |

## 4. 绿灯 + 回归

- R1-R3 全绿；`RunnerServiceTests`/`SemsControllerTests`/`SemsToolExtensionTests` 全量跑（sems 过滤集）。
- 插件前端 `pnpm run check` + `pnpm run test`（含新 stop-feedback.spec.ts）。
- `pnpm run design:check`、sems 冒烟 e2e（e2e/plugins/sems 定向）。

## 5. 文档与归档

- `Plugins/Sems/docs/01-requirements.md` F09 增补 AC-F09-1…7 + 版本行。
- `Plugins/Sems/docs/02-test-plan.md` F09 同步步骤/预期。
- PC-024 增记复发 + 新 case；`cases/README.md`、`problem-classes/README.md` 索引同步；`pwsh scripts/check-problem-classes.ps1` 绿。
- 05-evidence / 06-review / 07-final-report（07 含问题类归档/重构提案/悬空教训三节）。

## 6. 同类扫查清单（problem-class-closure P3）

- sems 其余「吞 404/静默」面：RunPanel `refresh()` catch 静默（有 ended 兜底，可接受）；工作台 `refreshRunningCount` 失败静默（计数停旧值——低危，记 TODO 观察项，不在本批扩）。
- 其余插件轮询面（SmartTerminal 已有 PC-024 守卫；其他插件本轮不改——扫查结论记 07）。
