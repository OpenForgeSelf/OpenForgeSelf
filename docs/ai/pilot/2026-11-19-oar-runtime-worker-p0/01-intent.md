# Intent

> Task ID：`2026-11-19-oar-runtime-worker-p0`

## Problem

定案已批准，但仓库尚无 RuntimeWorker；无法验证 OAR 0.109.0 在本机协议面上的 list/session/prompt/events/resume/cancel/dispose。

## Why

P1 `.NET` 客户端依赖稳定的 Worker 协议与可重复探针；先用 scripted 打通，避免绑定本机 Claude/Codex 安装。

## Expected Outcome

`Plugins/AgentHub/RuntimeWorker` 可 `pnpm run smoke` 12/12；`pnpm run build` 出 `dist/`；stdio `worker.hello` / `runtime.list` 可用。

## Constraints

- 精确锁 `@botiverse/oar@0.109.0`；Node ≥24
- 禁止自动 install 第三方 Agent
- 本批不改 `.cs` / 不接 DI / 不改 UI
- 闸门1：用户 seq6「提交一把，然后开始实现」

## Success Criteria

- smoke `ok: true`，passed=12
- `tsc` 无错
- 协议方法覆盖架构 §8 核心面
