# Mini-Task — 发布链 pnpm install 非 TTY 下清 modules 确认中断（输入34/35 2026-11-24）

> 轻量工件（单文件缺陷修复：Intent/Spec/Plan/Task 合并；规范 §11 裁剪档 + problem-class-closure 技能）。
> 来源：输入34 原文「智能终端不支持确认，报错 ERR_PNPM_ABORTED_REMOVE_MODULES_DIR_NO_TTY … If you are running pnpm in CI, set the CI environment variable to 'true', or set 'confirmModulesPurge' to 'false'.」

## 适用问题类（Stage 0）

`docs/11-troubleshooting/problem-classes/README.md` tags 检索：命中 **PC-043**（发链 pnpm install 失败族）与 **PC-047**（发链中断族）邻域，但根因不同（本批=非 TTY 交互确认，非平台短路/非 safe-delete）→ 非复发，新类独立归档，lens 进 Spec。

## Interaction Design（交互设计）

**无界面交互**——本批仅改发布基建脚本 `build-frontend.ps1`；唯一「使用者」是开发者/CI 终端，输出文案不变。

## Intent（为什么/做什么/到什么程度）

`release-local.ps1 -Sign` 在智能终端（无 TTY、无 CI 变量）跑到 `build-frontend.ps1:39` 的 `pnpm install --frozen-lockfile`，pnpm 检测到 node_modules 需重建时弹 `Proceed? (Y/n)`，无 TTY 直接 abort，发布链中断。脚本自称 "Usable locally and in CI"，但只在 CI（默认 `CI=true`）能过——本地无 TTY 场景的契约破了。

做到什么程度：install 加 `--config.confirmModulesPurge=false`，显式等价人工按 Y；保留 `--frozen-lockfile` 的锁文件约束；不改 `release-local.ps1`、不改全局配置；CI 行为不变。

## Spec（不变量 / Failure Modes）

- 不变量：**任何非交互执行路径（CI / 智能终端 / 管道）不得依赖 TTY 交互确认**；需要确认时必须在脚本层显式给出与人工确认等价的语义。
- 边界：`confirmModulesPurge=false` 只关这一个确认，不改 pnpm 其它安全提示；锁文件一致性仍由 `--frozen-lockfile` 保证。
- Failure Modes（PC-043/047 lens）：① 平台短路误判——不涉及平台探测，不适用；② safe-delete 批量删除守卫——本批不改任何删除行为，不适用；③ 若 pnpm 版本不接受该 config key → 参数被忽略，行为退回原状（已实测 pnpm 10.13.1/10.33.1 均接受，exit=0）。

## Plan（具体到真实文件）

1. `scripts/release/build-frontend.ps1:39`：install 命令加 `--config.confirmModulesPurge=false` + 中文注释说明非 TTY 理由（已改）。
2. 同类扫查：全仓 `pnpm install` 调用点——`.github/workflows/frontend-check.yml`（CI 自带 CI=true，不受影响）、`scripts/publish-plugin-full.ps1:50`（仅在 node_modules 缺失时裸 install，无 purge 确认路径）、pilot 文档/技能中的示例命令（非执行路径）→ **同根因命中 0**（详见 case §4）。
3. 验证：单插件 `build-frontend.ps1 -PluginsOnly -Plugin SmartTerminal` 真实跑；pnpm 参数接受度实测。
4. 归档：case + PC + 三处索引 + 05/06/07。

## Task（Allowed/Forbidden + 门禁）

- Allowed：上列 build-frontend.ps1 单点改动 + 归档文档。Forbidden：不动 release-local.ps1、不动 `.npmrc`/全局 pnpm 配置、不动 CI workflow、不改 lockfile。
- Gate1：用户回复「a」明确批准方案 A（输入35），授权即闸门1。
- 门禁：轻量档——pnpm 参数实测 + 单插件构建真跑 + 语法检查；不提交（用户未授权 commit）。

## Evidence / Review

见同目录 `05-evidence.md` / `06-review.md`。
