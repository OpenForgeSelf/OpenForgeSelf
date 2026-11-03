# 05 Evidence：SEMS 运行日志与运行面板 UX（2026-11-03）

## E1 实现（Verified）

| 项 | 证据 |
|---|---|
| 输出捕获 | `Plugins/Sems/Services/RunnerService.cs`：Launch 重定向 stdout/stderr，`RunLogBuffer` 环形缓冲 + `ProcessOutputText` 解码 |
| API | `GET /api/commands/{id}/logs?after=N` → `ProjectCommandsController.GetLogs` |
| 前端 | `RunPanel.vue`：1s 耗时 tick、2s 列表/日志轮询、「输出日志」展开；`SemsView.runOne` → `focusLog` |
| 版本 | `Plugins/Sems/plugin.json` 1.2.0 → **1.2.1** |

## E2 验证（Verified）

| 命令 | 结果 |
|---|---|
| `dotnet test --filter FullyQualifiedName~ForgeSelf.Api.Tests.Plugins.Sems` | **103/103**（含 `GetLogs_Captures_Stdout_From_Short_Command`） |
| `cd Plugins/Sems/web && pnpm run test` | **23/23** |
| `cd Plugins/Sems/web && pnpm run build` | exit 0（dist/index.js ~70kB） |
| `release-local.ps1 -Sign`（用户授权 20:29） | **2.4.22.2611032030** zip 落 `D:\src\my-proj\OpenForgeSelf\updates`（176.4 MB，签名 ok） |

## E3 未做

- 插件层 e2e 冒烟（本批未改 spec；走查截图未补）
- :51888 运行实例只读复验（待用户点自动更新后）
