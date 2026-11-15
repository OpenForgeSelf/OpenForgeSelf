# AI-Native Pilot Result（最终汇报）

> 任务结束强制格式。状态只报事实。

## 1. Repository Understanding

我确认了：项目为 pnpm 10.13.1（Node >= 20）双清单形态——`ForgeSelf.Web/` 既有清单承载全部前端脚本；git hooks 安装单一真源 = `scripts/install-git-hooks.ps1`（幂等、git 外硬抛）；`.github/flows/frontend-check.yml` 在 `ForgeSelf.Web` 内以 `pnpm install --frozen-lockfile` 还原依赖；`.gitignore` 仅禁 `package-lock.json`（`pnpm-lock.yaml` 入库）；`TODO.md` 与 `.forgeself/` 已 gitignored。

## 2. Selected Task

2026-11-15-postinstall-hook-install（轻量单文件 mini-task）：输入两项诉求——① 新环境还原依赖时自动安装 git hooks（不靠自觉），② 根目录加 package.json 透传前端脚本；完成后提交（推送未授权）。

## 3. Changed Files

- 新增：`package.json`（根清单：private + packageManager 同源 pnpm@10.13.1 + `postinstall` + 20 条透传脚本）、`scripts/postinstall-install-hooks.cjs`（粘合层：判断该不该装，失败模式显式）、本 pilot 目录 4 件
- 修改：`ForgeSelf.Web/package.json`（scripts 首位 `postinstall` 指向同一粘合层）、`pnpm-lock.yaml`（自动生成）、`scripts/README.md`（自动安装说明 + 手动兜底）、`AGENTS.md`（§0 门禁条补自动安装/兜底）、`docs/04-standards/agent-workflow/b-engineering-platform.md`（§B13 补 2026-11-15 机制句）

## 4. Validation

Build: N/A（无编译面改动；`scripts/**` 触发中档全量测试如下）

Unit Test: 中档全量 `dotnet test` — 失败 25 / 通过 3756 / 总计 3781（约 14m33s）；与最近全量基线（24 红）逐族对表 + 1 项已登记工作区存量 → **新增红 0**（红清单比对 Verified，存量归属 Inferred）。快档：`pnpm run design:check` exit 0（1761 ≤ 基线 2501）、`pnpm run check` exit 0、`pnpm run test -- --run` exit 0（118 文件 / 1223 用例 / 24.34s）——全部经根 package.json 透传实测（AC6）。触发链：根目录与 ForgeSelf.Web 内各一次 hook 移走 → install → 还原且散列一致（AC3/AC4，含 `--frozen-lockfile` CI 形态）。失败路径三态探针实测（AC5）：非 git → skip exit 0；pwsh 缺失 → skip + 手动兜底提示 exit 0；安装脚本缺失 → 透传非零（pwsh 退出码 64）。卫生守卫 `-Staged` PASS（files=7 lines=96 hits=0）；提交时 pre-commit 对全量暂存集重跑为最终判据。

E2E: N/A（无 UI 与 e2e 触发面，§5.6 未达深档条件）

## 5. Evidence

证据与分级全文见 [05-evidence.md](./05-evidence.md)：AC1–AC7 逐条对应；关键结论均为 Verified，两处 Inferred 显式标注（CI 侧 postinstall 触发为机制同源推断；DesignAgentToolContract 存量归属引跨会话日记）。

## 6. Review

八问全过、四项 Check 全 PASS、Risk L1、无 Critical/Major，Final Decision = **APPROVED**，全文见 [06-review.md](./06-review.md)。

## 7. Risk

L1。诚实边界（不得宣称「全自动」）：postinstall 覆盖「仓库根或 ForgeSelf.Web 内执行 pnpm install」两类还原（含 CI frontend-check）；**不覆盖** `pnpm install --ignore-scripts`、`Plugins/*/web` 目录内安装、其他任意子目录安装——这些场景依赖 AGENTS §0 已写明的手动兜底命令。回滚点：移除根清单 `postinstall`、Web 清单 `postinstall`、根清单 `packageManager` 三处接线即回到纯手动安装形态（mini-task 决策记录在案）。

## 8. Problems Found

- 探针B 首跑环境变量注入失误（普通变量未落 `$env:`），跑成幂等重装、已重测纠正——机制教训已记 06 Minor。
- 全量基线红总数跨会话漂移（24↔26，`ScriptRunnerTests.CancelAsync` 抖动进出）；本轮通过，诚实备注于 05。
- 不做事决策（NOT-to-do）：不改 `scripts/install-git-hooks.ps1`（单一真源）；不编辑 `docs/04-standards/risk-policy.md`（改为 07 §11 提案）；不建 `pnpm-workspace.yaml`；不替 `Plugins/*/web` 补安装接线；`not-taken-decisions.md` 本轮未追加（保守起见未触碰该文件，本节为唯一留档处）。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PASS |
| Code → Test | PASS |
| Test → Evidence | PASS |
| Evidence → Review | PASS |

### 9.5 本批自复盘（必填 · 2026-10-31 用户复盘要求立）

- **经验提取（保留打法）**：
  1. 「失败模式三态探针法」——对粘合层脚本用临时副本 + 环境收窄一次跑齐全部分支（skip/兜底/透传），每个分支都有真实输出可入证据，成本分钟级。
  2. 「接线点最小化」——自动安装只加三处接线、零改动既有单一真源，回滚叙述随之变成一句话；跨平台脆弱点（JSON 内嵌多层引号）收敛进独立 `.cjs` 文件而非 package.json 内联。
  3. 「基线红逐族对表 + 显式归属等级」——全量红 25 逐族对照最近全量日记，存量标 Inferred、比对动作标 Verified，新增红结论才站得住。
- **待改进**：
  1. 探针期环境变量注入出错一次（PATH 收窄未生效）——机制修正：环境操作一律直写 `$env:` 命名空间并先打一行「收窄后可达性探针」（本轮已现场补做 `pwsh-resolvable=no`）。
  2. AC1「透传脚本全量同源」缺乏机械守卫，本批靠人工清单——已落 P3 棘轮待办（见 §11）。
  3. 轻量模式 AC 覆盖机械校验空转：`verify-pilot-artifacts.ps1` §④ 的 `-EnforceAcCoverage` 仅当 02-*.md 存在时生效（`Get-Artifact 2` 无文件即整段跳过、无 WARN），mini-task 的 ACn 不会对 05 机械校验——本批 05 曾缺 AC1/AC2/AC7 记号而门禁两次放行，收尾自查发现后补齐。轻量模式改从 mini-task 提取 ACn 校验已落 P3 待办。

## 10. 问题类归档

无。理由：本批为基础设施接线（功能类），非缺陷/回归/返工类任务，无复现红灯 → 同类扫查 → 守卫闭环诉求；pre-commit 缺陷类 07 三节门禁对本任务自动跳过（非 defect task_kind）。

## 11. 重构提案

R1（局部，07 披露，两项均待拍板后执行，本轮未动手）：
1. **根/Web scripts 同步棘轮**：新增守卫校验根 package.json 透传脚本集合与 ForgeSelf.Web scripts 的包含关系（根 ⊆ Web 或显式豁免表），漂移即红。已落 `TODO.md`（P3，来源:输入5）。
2. **风险策略层补机制行**：`docs/04-standards/risk-policy.md` L4 段补「新环境还原即自动安装门禁 hook」一句（现仅有 §B13 承载）——属文档一责多处的取舍，待拍板后再动。

## 12. 悬空教训清单（必须为空）

无。本报告所有遗留项均已落位：透传棘轮 P3 → `TODO.md`（含逐字检索词「scripts 同步棘轮」）；风险策略 L4 提案与 c-changelog 行 → 本文件 §11 与汇报待拍板项（拍板后即执行或落 TODO）；`gate-logs/` 不入库处置 → 汇报待拍板项。
