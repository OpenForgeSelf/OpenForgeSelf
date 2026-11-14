# AI-Native Pilot Result（最终汇报）

> 任务结束强制格式｜载体：markdown 正文直发群消息（规范 §5；仅超单消息上限或用户明确要求时用附件，且附正文摘要）
> 状态只报事实，禁止模糊表述（对齐 AGENTS.md §10.4/§10.5）。

## 1. Repository Understanding

我确认了（依据 00-repository-understanding.md 与实读）：

- 技术栈：.NET 10 + SQLite + NewLife.XCode 宿主（`ForgeSelf.Api`）+ Vue 3 SPA（`ForgeSelf.Web`，pnpm）；测试体系 xUnit（`ForgeSelf.Api.Tests`）+ vitest + Playwright e2e。
- 提交链路：`scripts/hooks/pre-commit` 四道门（工件链齐备 / 设计守卫 / 拒绝列表外置 / 提交内容卫生）+ commit-msg 要求 `Pilot: <dir>` trailer（非纯文档变更）。
- 流程约束：AGENTS §0 工件链门禁（00–07 八件齐全方可提交）、§5.6 门禁分档、脚本一律 pwsh、验证判据看日志正文不拿 exit code 当证据。
- 卫生门禁真源落位：`docs/04-standards/agent-workflow/b-engineering-platform.md` §B13（单一真源，已取代 §B7 旧表述）。

## 2. Selected Task

**提交内容卫生门禁（PILOT-081）**：L1 四类绝对地址规则（`hygiene.abs-fileurl` / `hygiene.abs-drive` / `hygiene.abs-posix` / `hygiene.abs-unc`）+ L2 本地拒绝列表（私有文件，路径与行数公开、内容永不入库），拦在 pre-commit 第 4 道门；**棘轮口径 = 新增行零违规**（存量 717 处按 not-taken 060② 不清理）；四口径守卫 `-Staged / -All / -Files / -DiffFile`。

## 3. Changed Files

四批原子提交（B2/B4a 于本报告定稿后立即提交，清单如下；提交哈希见最终群消息与当天工作日记）：

| 批 | 内容 | 状态 |
| --- | --- | --- |
| B1 | pilot 工件 00–04（5 件，+243 行） | ✅ `605d3bc` |
| B3 | `docs/06-research/005/006` + `docs/README.md` 索引 + `not-taken-decisions.md` 059（4 件，+428/−2；纯文档免 Pilot trailer） | ✅ `88a583b` |
| B2 | `scripts/check-commit-hygiene.ps1`（新增）、`scripts/hooks/pre-commit`（第 4 道门）、`AGENTS.md`、`DESIGN.md`、`b-engineering-platform.md`（§B13）、`b-engineering-daily.md`（§B2 PILOT-076）、`agent-workflow/README.md`、`not-taken-decisions.md`（060）、`.agents/skills/commit-hygiene-audit/SKILL.md`、`ForgeSelf.Api.Tests/CommitHygieneGuardTests.cs`（新增 6 例）、pilot 00–07（本目录） | 本批提交（含本报告） |
| B4a | `.gitignore`（+8 行：`.grok/` 子目录白名单，放行 agents/workflows/rules/personas/roles）+ `.grok/` 4 件（2 代理 + 1 规则 + 1 工作流） | 本批提交 |

不触碰（全程零修改）：`.agents/worktrees/**`；模型别名批（并行会话在途）；db-admin 批（等待闸门 1）；`.forgeself/commit-hygiene.denylist.txt`（只读引用：12 行，内容未入库未外泄）；存量 717 处命中行（060②）。

## 4. Validation

Build:

- ✅ 三份 `.ps1`（守卫脚本 / pre-commit / verify-pilot-artifacts）`Parser::ParseFile` 解析错误数 = 0，UTF-8 BOM 三字节 239/187/191 齐（日志 `.temp/tmp/` 现场复核）。Verified。
- ✅ `install-git-hooks.ps1` 安装 exit 0，四道门就位（`pilot-hygiene-installhooks.log`）。Verified。

Unit Test:

- ✅ 快档：`CommitHygieneGuardTests` 过滤集 6/6 绿，约 8 s，exit 0（`pilot-hygiene-filteredtest.log`）。Verified。
- ✅ 中档全量（§5.6 触发条件②③：碰 `scripts/**` + 共享测试基建）：3778 例 = 26 红 / 3752 绿，14m14s（`pilot081-fulltest2.log`）。26 红逐条对表：全部归属既有基线与已登记偶发红（TODO.md L17/L185 台账，含 ForgeConfig 已知偶发），**本批零新增红**。Verified。

E2E:

- 无——本批不涉 UI / 端到端面；验证面 = 守卫四口径 + 测试 + 真提交演练（下）。

链路级活体验证（AC5）：

- ✅ B3 首次提交被第 4 道门**真实拒绝**：exit 1，`hygiene.abs-drive docs/06-research/005-loopforge-feature-analysis.md:3`（未复现违规原文）；脱敏该行后同链重试通过：`PASS (files=4 lines=428 hits=0)`，提交 `88a583b`。Verified。
- ✅ 空暂存区演练：`-Staged` PASS `(files=0 lines=0 hits=0)`（`pilot-hygiene-staged-empty-main.log`）。Verified。

## 5. Evidence

完整证据链 → `docs/ai/pilot/2026-11-14-commit-hygiene-gate/05-evidence.md`（AC1–AC9 逐条 + 6 条 Known Limitations + 5 条 Unresolved；原始日志均在 `.temp/tmp/`，gitignored 不入库，路径已逐一核实）。关键读数：

| 口径 | 结果 | 日志 |
| --- | --- | --- |
| `-All` 主检出盘点（非阻断口径） | FAIL（files=3310 lines=535550 hits=717；fileurl 1 / drive 520 / posix 24 / unc 107 / denylist 65） | `pilot-hygiene-allscan-main.log` |
| `-All` 工作区检出对照 | FAIL（files=3304 hits=656 + 「拒绝列表文件不存在，L2 已跳过」注记 = AC4 负向活证） | `pilot-hygiene-allscan.log` |
| `-DiffFile` 本批 diff（193 行，8 件受管改动） | **PASS (files=8 lines=56 hits=0)** = AC9 新增行零违规主证 | `pilot-hygiene-diffscan.log` |
| `-Files` 本批 17 件自扫 | FAIL 10 处，全为**存量行**（锚定核验：platform.md:118；daily.md:103/104/105/376；not-taken-decisions.md:287/383/384；其余 20 件 0 命中），棘轮不拦 | `pilot-hygiene-selfscan.log` |
| `-Files` B4a 五件（.gitignore + 4 件 `.grok/`） | **PASS (files=5 lines=288 hits=0)** | `pilot-hygiene-b4a-files.log` |
| `-Staged` 空暂存区 | PASS (files=0 lines=0 hits=0) | `pilot-hygiene-staged-empty-main.log` |
| design:check | PASS（1761 vs 2501，−740；token a=209/b=0/c=0/d=0） | `pilot-hygiene-designcheck.log` |

## 6. Review

`06-review.md`：八问逐项 PASS，Requirement/Scope/Test/Architecture 四检 PASS，Risk L1，**Final Decision = APPROVED**（Minor 3 条均已落位：CWD 解析注意点入 05 KL + §B13；CI 接入不做入 060①；首轮全量卡死入 05 KL）。

## 7. Risk

L1——新增门禁影响提交链路（保守失败：被拦即拒绝、修复后重试，已有活体闭环）；存量 717 处不清理与拒绝列表不进 CI 为已登记决策（060①②）；无业务逻辑改动，全量测试零新增红。

## 8. Problems Found

1. B3 首提被本守卫真实拦截（`hygiene.abs-drive docs/06-research/005…:3`，exit 1）→ 脱敏后通过——门禁按设计工作，非缺陷（流程损耗 1 次重试）。
2. `-All` 首跑在 worktree 执行扫的是当前检出（守卫从 CWD 解析仓库根）→ 主检出重跑定数 717；运维注意点入 05 KL 与 §B13。
3. `-Files` 数组参数经 `pwsh -File` 传递绑定失败 → 改 in-process 调用（`& script -Files $paths`）解决。
4. 全量测试首轮（11:26）与并行会话共享构建节点卡死 → `dotnet build-server shutdown` 后重跑取得有效读数。
5. 05-evidence 首稿 22 处证据日志路径写错（误写根目录 `gate-logs/`，实际在 `.temp/tmp/`）→ 逐一修正。

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
  1. **棘轮口径先行**：验收口径定为「新增行零违规」（`-DiffFile` 主证），与存量盘点（`-All`，非阻断）口径分离，避免与 717 处存量纠缠——已固化于 §B13。
  2. **文本断言 + 真提交演练双半**：AC5 一半是测试断言，一半是真提交被拒→修复→通过（`88a583b`）；活体证据强于纯文本。
  3. **共享构建节点先独占再跑全量**：`dotnet build-server shutdown` 后重跑，避免并行 dotnet 卡死假象（§5.0 已有此规，本批再次实证）。
  4. **数组参数 in-process 调用**：`& <script> -Files $paths` 规避 `pwsh -File` 数组绑定失败。
- **待改进（机制修正）**：
  1. 05-evidence 首稿在未核实落点时先写证据路径（`gate-logs/`），后核实全在 `.temp/tmp/`，22 处返工——修正：证据文件引用一律先列目录核实落点再落稿（05 模板「只记录实际发生」的执行细则）。
  2. 全量测试首轮未先做构建节点独占（同经验 3）即起跑，浪费一轮——修正同上（规则已在，执行前置）。

## 10. 问题类归档

**无。** 理由：本批为守卫基建任务（新增性质），非缺陷修复 / 回归 / 返工，全批未产生新缺陷；唯一拦截事件（B3 首提）由本批交付的守卫自身当场捕获并同批修复（逃逸 = 0），其上游同因类（入库内容含绝对地址）已在 `b-engineering-daily.md` §B2 PILOT-076 作同因第四面补种记录，本批交付的 §B13 + 第 4 道门即该类的守卫层。台账核查：`docs/11-troubleshooting/problem-classes/README.md` 现有 PC-001–PC-033，无对应问题类，本批不新开 PC。

## 11. 重构提案

- **R0（主判）**：本批交付即守卫层，单文件四口径约 260 行、职责清晰，无即时重构需求；存量 717 处按 060② 不清理、CI 接入按 060① 不做，两者均为已登记决策，不构成重构欠账。
- **R1（局部 · 披露 · 未激活）**：① 守卫脚本仓库根目前从 CWD 解析，跨检出/未来 CI 接入场景可加显式 `-RepoRoot` 参数（局部改动）——挂 060① 重审触发；② 若 060② 存量清理被重审激活，按文件局部脱敏分批推进。两项均不本批动工。

## 12. 悬空教训清单（必须为空）

**空。** 逐项落位映射：

| 发现 | 落点 |
| --- | --- |
| CI 接入不做 | `not-taken-decisions.md` 060① |
| 存量 717 处不清理 | `not-taken-decisions.md` 060② |
| ML 人名/业务信息机械检测不做 | `not-taken-decisions.md` 060③ |
| CWD 解析仓库根注意点 | 05 KL + `b-engineering-platform.md` §B13 + 06 Minor |
| 全量 26 红归属 | TODO.md 既有台账（L17/L185），本批零新增 |
| 首轮全量卡死 / `-Files` 绑定 / 05 落点返工 | 05 KL + 07 §8/§9.5，无待办悬空 |
| 并行会话在途批 / db-admin 批未收口 | 非本批范围，如实上报交回，不落 TODO（避免越界认领） |
