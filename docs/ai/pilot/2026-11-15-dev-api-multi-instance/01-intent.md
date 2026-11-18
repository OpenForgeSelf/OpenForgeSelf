# Intent

> 阶段：Stage 1｜Task ID：2026-11-15-dev-api-multi-instance

## Problem

根目录 `pnpm dev` 应提供完整的本地开发体验，用户明确要求它启动宿主后端与宿主前端；仓库已有 `scripts/dev-stack.ps1` 承担该职责，不应另造 API-only 默认入口。

## Why

用户从仓库根执行一个熟悉的命令即可进入完整开发栈，减少入口混淆并复用现有整栈生命周期管理。

## Expected Outcome

`pnpm dev` 调用现有整栈启动器并启动后端 API 与前端 dev server；`pnpm run dev:web` 仍可单独启动前端。

## Constraints

- 仅改善本地开发/测试入口；生产宿主默认单实例语义保持不变。
- 不改数据库结构、鉴权、安全逻辑、发布流程或 `dev-stack.ps1` 的整栈启停行为。
- 不覆盖现存用户对 `package.json` 的其他工作区改动。
- 保持 `dev-stack.ps1` 当前固定状态文件、停止语义与端口顺延行为，不引入第二份整栈管理逻辑。

## Success Criteria

1. 根 `pnpm dev` 路由到 `scripts/dev-stack.ps1`，启动后端 `/api/health` 与前端页面。
2. `pnpm run dev:web` 仍可单独启动前端；既有 `dev-stack.ps1 -Stop` 停止语义不变。
3. 仓库守卫锁定根脚本路由到现有整栈入口；相关构建、回归测试和问题类检查按门禁执行。
