# Plan

## Files To Change

- `Plugins/AgentHub/RuntimeWorker/**`（新建）
- `docs/ai/pilot/2026-11-19-oar-runtime-worker-p0/**`
- `docs/02-features/032-agent-hub.md`（P0 进度一句）

## Files Forbidden

- `ForgeSelf.Abstractions/**` / `Plugins/AgentHub/**/*.cs` / `Plugins/AgentHub/web/**`
- 自动 install / 停宿主

## Implementation Steps

1. 脚手架 package.json（锁 OAR）+ tsconfig
2. registry / sessions / handlers / stdio / smoke
3. Node24 下 smoke + build
4. 写 pilot 05–07 + 032 回写
5. commit / push / 更新 PR

## Test Plan

快档：`pnpm run smoke` + `pnpm run build`（Worker 目录）
