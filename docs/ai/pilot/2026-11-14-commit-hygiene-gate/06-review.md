# Review

> 阶段：Stage 8｜Reviewer 视角重查 Intent → Spec → Plan → Task → Code → Test → Evidence 全链。
> 结论只报事实；CHANGES_REQUIRED 时须指明回退到哪个阶段。
> 证据分级沿用 05-evidence.md：Verified（亲跑）/ Inferred（凭代码/输出推断）/ Unknown（未验证）。

## 审查八问（逐项回答）

1. **实现是否真正满足 Intent？** 满足。Intent 要求「提交内容卫生门禁：L1 四类绝对地址 + L2 本地拒绝列表，拦在 pre-commit，棘轮只拦新增行」——交付为 `scripts/check-commit-hygiene.ps1`（四口径）+ pre-commit 第 4 道门 + 测试 6 例 + 登记五处 + 拒绝列表（本地私有）。活体证据：B3 首次提交被真实拒绝（`hygiene.abs-drive docs/06-research/005-loopforge-feature-analysis.md:3`，exit 1），脱敏后同链重试通过 `88a583b`（PASS files=4 lines=428 hits=0）。Verified。

2. **实现是否符合 Spec？** 符合。02-spec AC1–AC9 逐条核对：AC1 四类规则阳性集（测试断言逐条验证，6/6 绿，8 s，exit 0，日志 `.temp/tmp/pilot-hygiene-filteredtest.log`）；AC2 阴性集 0 命中（同日志）；AC3 -DiffFile 行号=新侧行号（`pilot-hygiene-diffscan.log` PASS files=8 lines=56 hits=0，193 行批 diff 原文）；AC4 拒绝列表子串+regex 命中、缺失文件时 L2 跳过且注明（`pilot-hygiene-allscan.log` denylist=0 + 「拒绝列表文件不存在，L2 已跳过」注记，与真提交演练的双向覆盖）；AC5 文本断言 + 真提交演练双半（见问 1，`pilot-hygiene-staged-empty.log` 空暂存 PASS files=0 为附加口径）；AC6 快档 6/6 绿 + 中档全量 26 红/3752 绿/3778（红线归属全在既有基线与已登记偶发红，TODO.md L17/L185 台账，本批零触及）；AC7 -All 盘点本批文件命中 10 处、全为存量行（锚定 `^\[hygiene\] FAIL \S+ <path>:` 核对：platform.md:118；daily.md:103/104/105/376；not-taken-decisions.md:287/383/384——其余 20 件 0 命中；新增行由 -DiffFile 0 命中反向证明干净）；AC8 登记五处齐备（§B13 / AGENTS §2.3+§2.4+§5.6 / 技能 SKILL.md / not-taken 060 / AGENTS+DESIGN 清理行，均入 B2 变更清单）；AC9 本批新增 17 件全内容 0 命中 + 改动 3 件新增行 0 命中（-Files/-DiffFile 互证，10 处存量为既有行，棘轮不拦，060② 记录不清理）。全部 Verified。

3. **是否超出了 Scope？** 未超出。B2/B4a 变更清单与 04-task Allowed 清单一致；发现的三条边界（CI 接入 / 存量清理 / 机械检测）按流程记入 `not-taken-decisions.md` 060 而非顺手动工。模型别名批（AIModelFeatureTests / ModelsController / AIModel.Biz / 文档 004 及其 pilot 目录）与 db-admin 批全程零触碰，保持未收口原状。Verified。

4. **是否修改了不应该修改的文件？** 无。未触碰：`.agents/worktrees/**`（本会话工作目录即在其中，业务操作全部经绝对路径指向主检出）；`ForgeSelf.Api` 源逻辑（本批唯一 API 侧文件是测试工程新增 `CommitHygieneGuardTests.cs`）；`.forgeself/commit-hygiene.denylist.txt`（只读引用，路径+行数计 12，内容未入库未外泄，§B13 铁律）；并行会话在途批。存量 717 处命中（-All 主检出：files=3310 lines=535550 hits=717，exit 1 属盘点口径预期）按 060② 明确不清理，未动一行存量。Verified。

5. **测试是否覆盖 Acceptance Criteria？** 覆盖。6 个 [Fact]（无 `using Xunit;`，依赖 global usings）覆盖 AC1/AC2/AC3/AC4/互斥用法错（exit 2）；AC5 由真提交演练活体补全；AC7/AC8/AC9 由守卫四口径 + 变更清单核对覆盖。过滤集 6/6 绿（8 s，exit 0）；全量 3778 例 26 红，红线归属台账在案、非本批引入。Verified。

6. **是否存在明显回归风险？** 低。守卫与测试均为新增旁路文件，不改业务逻辑；pre-commit 新增第 4 道门只影响提交链路，失败即拒绝（保守失败），已有真实拒绝→修复→通过闭环验证。全量测试无新增红。风险点已在 05 Known Limitations 全量披露（棘轮口径、拒绝列表不进 CI、-All 非阻断、repo-root-from-CWD 运维注意点）。Verified。

7. **是否存在架构不一致？** 无。脚本归 `scripts/`（仓内工具惯例）；守卫真源落 `b-engineering-platform.md` §B13（单一真源，与 03-plan 修正一致：§B13 取代 §B7 表述）；技能落 `.agents/skills/commit-hygiene-audit/SKILL.md` 并登记 AGENTS §2.4；决策归 `docs/07-decisions/not-taken-decisions.md`；同因第四面补种记录 `b-engineering-daily.md` §B2 PILOT-076。各层各归其位，无第二真源复制。Verified（结构核对）。

8. **Evidence 是否足以证明任务完成？** 足以。05-evidence.md 每项带真实命令、真实输出与日志路径（`.temp/tmp/`），无 Inferred/Unknown 冒充 Verified；关键链路（拦截→脱敏→重试通过）为活体证据而非纯文本断言；基线红有台账归属；未收口项（并行批、db-admin、日志不入库）如实列为 Unresolved。Verified。

## Requirement Check

PASS——Intent 全部满足（见八问 1/2/5）；AC1–AC9 全绿；B1/B3 已提交（`605d3bc`/`88a583b`），B2/B4a 变更就绪待提交（属收尾动作，非实现缺口）。

## Scope Check

PASS——变更清单 = 04-task Allowed 清单 + 收尾批（.gitignore 放行 `.grok` 五个子目录白名单 + `.grok` 4 件代理/规则/工作流文件，经守卫 -Files 口径核验后入库）；Forbidden 清单零触碰；三条不做走 060 登记。

## Test Check

PASS——快档过滤集 6/6 绿（~8 s，exit 0）；中档全量（AGENTS §5.6 触发条件②③：碰 `scripts/**` + 共享测试基建）3778 例 26 红/3752 绿，红线全数归属既有基线与已登记偶发红，本批零新增；真提交演练（拒绝→脱敏→通过 `88a583b`）为链路级活体验证。

## Architecture Check

PASS——真源唯一（§B13），技能登记，决策归档，脚本/测试/hook 分层归位；无跨层重复承载。

## Risk

L1——新增门禁影响提交链路（保守失败可即时修复，已有活体闭环）；存量不清理与拒绝列表不进 CI 为已登记决策（060①②）；无业务逻辑改动。

## Findings

### Critical

无。

### Major

无。

### Minor

1. **守卫 repo-root-from-CWD**：跨检出（worktree）使用须先切到目标检出根，否则扫的是当前检出（-All 对照实测：工作区检出 files=3304 hits=656 vs 主检出 files=3310 hits=717，两者差异同时暴露 L2 跳过注记差异）。已在 05 Known Limitations 记为运维注意点，§B13 亦有说明。非缺陷（常规约定）。
2. **-All 口径不可作 CI 判绿**：有存量即 exit 1（主检出 717 处），属盘点非阻断口径；CI 接入已按 060① 不做。
3. **全量测试首轮（11:26）卡死废弃**：与并行会话共享构建节点相关；`dotnet build-server shutdown` 后重跑取得有效读数。流程损耗，非产品缺陷，已记 05 Known Limitations。

## Final Decision

APPROVED

依据：八问全过、AC1–AC9 证据齐备且全部可复核（命令+日志在盘）、Scope 零越界、测试无新增红、真提交演练闭环、登记五处齐备。遗留项均为已登记决策（060）或并行批未收口（如实列 Unresolved，不属本批缺陷）。
