# mini-task：check-features.mjs 插件根路径兼容（10-07 挂账清偿）

- 日期：2026-10-16（补记立档：修复本身由前序会话完成并验证，本工件为「验收 + 提交」的门禁记录，不新增事实）
- 类型：工具/门禁脚本缺陷修复（纯 Node 检查器，零产品运行时影响、零后端依赖）
- 来源：`docs/ai/pilot/2026-10-07-todo-agent-dispatch/06-review.md` §3 挂账——「`check-features.mjs` 的 `pluginsDir` 过期（指向已搬走的 `ForgeSelf.Api/Plugins`），16 项假红」；TODO.md「check-features 既有 5 处不一致」同源。

## Intent / Spec

仓库插件根已迁至顶层 `Plugins/`，但 `ForgeSelf.Web/scripts/check-features.mjs` 的插件扫描根仍指向 `ForgeSelf.Api/Plugins`，导致视图/控制器信号校验大面积误报（历史证据：10-07 pilot 05「21 项不一致（16 项假红）」）。修复目标：检查器在**当前布局**下恢复真信号判定，输出可信（exit 0 或真不一致）。

## Scope

- 仅 `ForgeSelf.Web/scripts/check-features.mjs`：
  - `pluginsDir` 单根 → `pluginsDirs` 候选根（仓库根 `Plugins/` 优先 + `ForgeSelf.Api/Plugins` 兼容保留）；
  - 新增 `hasNamedFile`（递归找 `<X>.vue` / `<X>Controller.cs`）与 `hasPluginFile`（按插件目录 × area 扫描），视图/控制器信号先查宿主、再查插件根；
  - 基础设施排除清单补 `Dev` / `Update`（宿主开发调试/自更新基建，CONTROLLER_DENY + PLUGIN_DENY）。
- 禁止：改动 `features.ts` 清单数据来"喂"检查器；改动任何后端/前端产品代码。

## Acceptance Criteria

- [ ] AC1 `node ForgeSelf.Web/scripts/check-features.mjs` exit 0（当前树 22 项全一致，无假红）。
- [ ] AC2 宿主全量 vitest 不新增红（检查器变更不进运行时；本次刷新 985/985 含 `features.test.ts` 3 例）。
- [ ] AC3 Evidence / Review 如实记录（含「修复由前序会话完成、本批补记清账」的来源说明），从 TODO 挂账中移除。

## Plan / Task

1. 读 10-07 pilot 05/06 确认挂账根因与修复范围 → 与当前 diff 对表（diff 即该修法，无夹带）。
2. 实跑检查器 + 宿主全量测试刷新证据。
3. 精确暂存 `check-features.mjs` + 本 pilot 目录，`Pilot:` trailer 提交。
