# mini-task · SEMS 运行日志与交互

## Intent
用户反馈 SEMS 难用：启动命令后看不到输出日志，运行时长不刷新。

## Spec（摘要）
- Launched 会话：后端捕获 stdout/stderr，GET `/api/commands/{id}/logs?after=N` 增量拉取。
- 前端：有运行中会话时 1s 刷新耗时、2s 刷新列表与日志；启动后自动展开该命令日志。
- Detected 会话：无日志 API（仅 Launched）。

## Interaction Design
- 运行面板每条 Launched 会话可展开「输出日志」区（等宽、可滚动、stderr 标红）。
- 点命令「启动」→ 运行面板自动选中并展开对应日志。

## Plan
- `RunnerService.cs`：重定向管道 + 环形缓冲 + GetLogs
- `ProjectCommandsController.cs`：logs 端点
- `RunPanel.vue` / `SemsView.vue` / `CommandList.vue`：轮询、计时、聚焦
- `types.ts` + 单测

## Task
Allowed: Plugins/Sems/**, ForgeSelf.Api.Tests/Plugins/Sems/**
Verify: `dotnet test --filter Sems` + `cd Plugins/Sems/web && pnpm run check && pnpm run test && pnpm run build`
