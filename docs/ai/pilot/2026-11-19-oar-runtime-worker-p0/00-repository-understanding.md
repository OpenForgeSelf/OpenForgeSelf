# Repository Understanding

> 阶段：Stage 0｜Task ID：`2026-11-19-oar-runtime-worker-p0`

## 项目结构

- 宿主 `ForgeSelf.Api` / 前端 `ForgeSelf.Web` / 插件 `Plugins/AgentHub`
- OAR 定案真源：`docs/01-architecture/agenthub-oar-runtime.md`；ADR-007；调研 007
- 设计批 pilot：`docs/ai/pilot/2026-11-19-oar-agenthub-runtime/`（闸门1 已批）

## 技术栈

| 层 | 技术 | 依据 |
| --- | --- | --- |
| Worker | Node ≥24 ESM + TypeScript | OAR `engines.node`；本批 `Plugins/AgentHub/RuntimeWorker/package.json` |
| OAR | `@botiverse/oar@0.109.0` 精确锁 | ADR-007 |
| 协议 | JSON-RPC 2.0 / JSONL stdio | 架构真源 §8 |
| 探针 | `@botiverse/oar/testing` `scriptedRuntime` | OAR testing 出口 |

## 架构特点

- OAR 不进 .NET：AgentHub 内 Node Worker；P1 再接 `OarWorkerClient`
- 禁止启动自动 `install`；P0 仅 list / installation 只读 + scripted 全链路

## 测试方式

```bash
cd Plugins/AgentHub/RuntimeWorker
# Node ≥24
pnpm install && pnpm run smoke && pnpm run build
```

## 主要目录职责

| 路径 | 职责 |
| --- | --- |
| `Plugins/AgentHub/RuntimeWorker/src/` | Worker 实现 |
| `Plugins/AgentHub/RuntimeWorker/src/smoke.ts` | P0 12 步探针 |
