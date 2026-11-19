# Repository Understanding

> Task ID：`2026-11-19-oar-remaining-close`｜Stage 0

## 项目结构

- 解决方案 `ForgeSelf.slnx`；宿主 `ForgeSelf.Api/` + 前端 `ForgeSelf.Web/`；插件 `Plugins/AgentHub/`（含 `RuntimeWorker/` Node≥24 + `@botiverse/oar@0.109.0`）。
- 测试：`ForgeSelf.Api.Tests/Plugins/AgentHub/*`；e2e `ForgeSelf.Web/e2e/plugins/agent-hub/`。
- 真源：`docs/01-architecture/agenthub-oar-runtime.md`、`docs/02-features/032-agent-hub.md`、gap-close pilot、`/opt/cursor/artifacts/oar-remaining-gaps.md`。

## 技术栈

| 层 | 技术 | 依据 |
| --- | --- | --- |
| 宿主 API | .NET 10 + ApiKeyPolicy | `AgentHub*Controller.cs` |
| 插件 Web | Vue 3 + Vite（独立 dist） | `Plugins/AgentHub/web/` |
| Worker | Node≥24 stdio JSON-RPC | `RuntimeWorker/package.json` |
| 测试 | xUnit / Playwright | Api.Tests + e2e |

## 架构特点

- OAR P0–P4 + gap-close 已落地；§9 sessions/runtimes API 齐；agents/tasks **缺**类级 ApiKeyPolicy（F1）。
- UI：`listRuntimes`/`listSessions`/`closeSession` 有封装；Session 页无 input/abort/records/stream；Runtime 无 probe/models UI（F3/F4）。
- Worker 有 `runtime.installPlan`，**无** `runtime.install` RPC；架构 §8 允许「计划只读 + 用户确认后执行」。

## 测试方式

- 快档：`dotnet test --filter AgentHub` + 插件 `pnpm run build` + 1 条 Session e2e。
- HTTP 探针落盘 `/opt/cursor/artifacts/oar-todo-verify/`。

## 构建命令

```bash
dotnet build Plugins/AgentHub/AgentHub.csproj
cd Plugins/AgentHub/web && pnpm run build
cd Plugins/AgentHub/RuntimeWorker && pnpm run build
```

## 适用问题类

无新增 PC（鉴权漏挂同类于铁律 17；补守卫即可）。既有 ToolBridge/DesignSystem Auth 测试模式可复用。

## 候选低风险任务

F1 加 Authorize 属性；F3/F4/F5 前端接线；F2 最小 installPlan+confirm+install。

## 选择该任务的原因

用户明确要求按 gaps 清单闭合未完成三项并走查取证；缓做 D* 不实现。
