# Evidence

## Root cause（Verified）

CI `Frontend Check` / job `check` / step `pnpm run check` @ `549b08b`  
Run: https://github.com/OpenForgeSelf/OpenForgeSelf/actions/runs/44525817715  

首错：`Plugins/DbAdmin/web/... Cannot find module 'vue'`；随后 SmartTerminal `Property … on type '{}'`（vue 解析失败级联）。

本地对照：

| 条件 | `vue-tsc -b` |
| --- | --- |
| 插件 web 有 node_modules | exit 0 |
| 移走 DbAdmin+SmartTerminal node_modules | exit 2，与 CI 同首错 |

## Fix

`.github/workflows/frontend-check.yml`：check 前对 story 链接插件执行 `pnpm install --frozen-lockfile`。

## Local verify

见 `/opt/cursor/artifacts/gap-close-ci-fix-local-check.log`（装 deps 后 `pnpm run check`）。
