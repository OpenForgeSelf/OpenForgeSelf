# Repository Understanding

> 阶段：Stage 0｜Task：oar-agenthub-ux-real｜2026-11-19

## 项目结构

OpenForgeSelf：宿主 `ForgeSelf.Api` + `ForgeSelf.Web` + 插件 `Plugins/AgentHub`（含 `RuntimeWorker` Node OAR 适配、`web/` Vue 远程加载界面）。

## 技术栈

| 层 | 技术 | 依据 |
| --- | --- | --- |
| 插件前端 | Vue 3 + Vite lib ESM | `Plugins/AgentHub/web/vite.config.ts` |
| OAR Worker | Node + `@botiverse/oar@0.109.0` | `Plugins/AgentHub/RuntimeWorker/package.json` |
| 契约 | `IAgentRuntime` / `AgentDescriptor` | `ForgeSelf.Abstractions/AgentRuntimeContracts.cs` |
| 消费投影 | `AgentHubRuntimeProvider.ListAgentsAsync` | `Plugins/AgentHub/Services/AgentHubRuntimeProvider.cs` |

## 架构特点

- Runtime 列表 = 登记 Agent + Worker `runtime.list` 补充；既往补充项一律 `Available=true`（假绿）。
- `scripted` 在 Worker registry 常驻，供 smoke / 协议测试；曾进入产品 UI。
- Brand 图标在 `@botiverse/oar/assets/brands/*.svg`，list 协议刻意剥离 SVG。

## 测试方式

- Worker：`cd Plugins/AgentHub/RuntimeWorker && pnpm run smoke`
- 后端过滤：`dotnet test --filter AgentHub`
- 插件前端：`cd Plugins/AgentHub/web && pnpm run build`
- e2e：`e2e/plugins/agent-hub` 1 冒烟

## 适用问题类

无既有 PC 命中「假绿 Runtime」；本批新增产品诚实性约束（非缺陷复发闭环）。

## 候选低风险任务

本任务即：去模拟 + 品牌图标 + Session/Runtime UX 分区重设计。
