# mini-task — frontend-check.yml 的 pnpm 安装修复（CI 债）

## Intent（为什么）
PR#2 的 Frontend Check 挂在「Unable to locate executable file: pnpm」：workflow 用 `corepack enable` 装包管理器，在新 runner 上失效；且 setup-node `cache "pnpm 在 pnpm 未进 PATH 时先跑 `pnpm store path` 即崩（失败点在 corepack 步之前）。

## Spec（改什么、到什么程度）
`.github/workflows/frontend-check.yml`：在 setup-node **之前**插入 `pnpm/action-setup@v4`（version 10.13.1，对齐 ForgeSelf.Web/package.json 的 packageManager 字段与 lockfileVersion 9.0），删除 `corepack enable` 步；保留 node 20 + cache: pnpm + cache-dependency-path 不变。
- Interaction Design：**无界面交互**（纯 CI workflow 改动，无用户可见 UI；验证 = 本地 `pnpm install --frozen-lockfile` + `pm run check` 走通 + 与 release.yml 安装方式同参）。
- AC1 action-setup 先于 setup-node、corepack 步移除、参数与 release.yml 同源。
- AC2 本地 frozen-lockfile 安装 + `pnpm run check` 全绿（模拟 CI 主路径）。

## Plan
单文件改动；本地验证（无 push 凭据，CI 运行时留待下次 push 实测）。

## Task（Allowed/Forbidden + 验证）
Allowed：仅 `.github/workflows/frontend-check.yml` 与本轻量工件。
Forbidden：不动 release.yml（已正确）、不动 lockfile、不改 Node 版本。
验证：`cd ForgeSelf.Web && pnpm install --frozen-lockfile && pnpm run check`（日志 wt-qa/.temp/pnpm-check-ta.log）。