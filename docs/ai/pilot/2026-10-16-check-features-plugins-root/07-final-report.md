# 07 Final Report — check-features 插件根路径清账（2026-10-16）

## 交付

- `ForgeSelf.Web/scripts/check-features.mjs`：插件根候选化（仓库根 `Plugins/` + 旧 `ForgeSelf.Api/Plugins`）、递归命名匹配（视图/控制器信号可命中插件侧）、基建排除清单补 `Dev`/`Update`。
- 本 pilot 工件（mini-task + 05/06/07）补记立档，清偿 `2026-10-07-todo-agent-dispatch` 06-review §3 挂账。

## 结论

功能清单校验器在当前仓库布局下恢复真信号判定：22 项全一致、exit 0、无假红；宿主全量单测同批刷新 985/985 不新增红。

## Validation / Review

- Validation：见 `05-evidence.md`（§1 实跑 exit 0 + §4 AC 覆盖，来源等级 Verified）；
- Review：见 `06-review.md`（Final Decision = APPROVED）。

## 状态

- ✅ 修复验证通过并随本批提交；
- 提交 trailer：`Pilot: 2026-10-16-check-features-plugins-root`；
- TODO.md「check-features 既有 5 处不一致」条目移除；10-07 挂账其余 3 条（global-setup SQLite provider、`Plugins/*/web/dist` 手工构建等）不在本批范围，维持原状。
