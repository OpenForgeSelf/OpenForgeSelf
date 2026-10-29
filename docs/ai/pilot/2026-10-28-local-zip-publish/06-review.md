# 06 · Review — 审查（Reviewer 视角 · 2026-10-28）

## 审查范围

本任务 = 「按 `plugin-publish-verify` 发布本地 zip（2.4.16）」+ 发布前撞到的**构建链环境缺陷修复**（`scripts/release/patch-esbuild.mjs`）。
对照依据：`docs/04-standards/ai-native-engineering-workflow.md` v1.2.0 九问（§Stage 8）。

## 九问复核

| # | 问题 | 结论 | 依据 |
|---|------|------|------|
| 1 | 是否真正满足 Intent？ | **PASS** | 用户要「按技能流程发布本地 zip」：六步全走完（门禁 → 起源码走查 → 闸门2.5 放行 → release-local -Sign → 用户侧更新通知 → 产物核对），产出 2.4.16.2610281727 签名包并落本地更新源 |
| 2 | 是否符合 Spec？ | **PASS** | FR-1~FR-5 逐条落地；FR-2（esbuild 修法）落在仓库既有钩子而非一次性脚本（AGENTS §5.0「缺正规入口 → 先补工具再走」） |
| 3 | 是否超出 Scope？ | **PASS** | 唯一源码改动 = `scripts/release/patch-esbuild.mjs`（构建链）+ 新增 1 条 live 走查 spec + pilot 工件。**未动任何宿主/插件业务代码** |
| 4 | 是否改了不该改的文件？ | **PASS** | 业务文件零改动；`.temp/` 与 `artifacts/` 为构建产物（不入 git）；`TODO.md` / 日记 / pilot 工件为流程要求 |
| 5 | 测试是否覆盖 Acceptance Criteria？ | **PASS** | AC-1~AC-8 均有对应实测输出（编译/单测/前端 check+test/design:check/插件 build/冒烟 e2e 8-8/工具注册实证/包内核验）；唯一红项（并行会话 4 例）已定性并交代 |
| 6 | 是否存在明显回归风险？ | **PASS（附条件）** | esbuild workaround 2 只在**本地平台包存在时**生效，否则保留原 env 覆盖（不破坏无 optional deps 的安装）；已幂等（重复执行输出 `already patched`）且对 host 与插件目录均验证。**附带影响**：写入 pnpm store 硬链接文件（`lib/main.js` / `bin/esbuild`）会使 store 内容的哈希与文件名不再一致 ⇒ 未来 `pnpm install --force` 可能重新下载原始文件，但发布链每次都会重跑 patch ⇒ 自愈 |
| 7 | 是否存在架构不一致？ | **PASS** | 修法落在 `scripts/release/patch-esbuild.mjs`——发布链本就把环境补丁收敛在此（已有 sandbox safe-delete workaround），未新增旁路 |
| 8 | Evidence 是否足以证明任务完成？ | **PASS** | 05-evidence 分门禁/走查/发布/产物核对四段，全部为真实输出（读日志正文，不拿 exit code 当证据） |
| 9 | （缺陷类）同类扫查与守卫是否落地？ | **N/A** | 本任务非缺陷类；但对 esbuild 这类**环境错配**做了同类扫查：12 个插件前端 + 宿主 = 13 个 web 目录**全部**命中同一根因（共享硬链接 ⇒ 一次修全部），已在 patch 脚本内按「本地平台包优先」通用化，不是一次性修例 |

## Check 汇总

| Check | 结论 |
|-------|------|
| Requirement | PASS |
| Scope | PASS |
| Test | PASS |
| Architecture | PASS |

## Risk

**L1**（低）：发布物含 2 处非本批内容 —— ① 5 个未验收批次的改动（工作树快照的固有属性，已向用户交代并获放行）；② 并行会话 Anthropic 出站面 4 例红测（已登记 TODO，随下一版修正）。二者均不影响本批自身正确性，且本地更新源不自动推、升级由用户决策。

## Findings

- **Critical**：无
- **Major**：无
- **Minor 1**：第一次产物核对脚本用 `-like` 配正则转义（`2\.4\.16...`）导致 L1/L3 计数失真（误报 0 条）⇒ 已改写为纯通配符并重核（L1=20、L3=0）。属一次性核对脚本，未入库。
- **Minor 2**：包内 `AIAgent.dll` 的 `FileVersion=1.0.0.0`（该插件 csproj 未写版本）⇒ **dll FileVersion 不是插件版本的可靠信号**，版本徽标真源是 `plugin.json`。与 TODO.md 已登记的「包内插件版本徽标与实际内容可能不一致」同域，本批未新增机制层守卫。
- **Minor 3**：发起 walkthrough 的首个 live spec 断言写错（用了宿主 `.view-title` 而非插件的 `.version-badge`；且把外部 MCP 服务器的无前缀工具当成宿主注册表工具）⇒ 连续 3 次红均为**断言错误**而非产品缺陷，已修正后 1 passed。

## Final Decision

**APPROVED** —— 任务范围内可验证面全部 Verified；两处 Minor 均为核对脚本/断言层面的问题，未进仓、不影响交付物；唯一红项属他批在飞改动，已定性、已交代、已登记 TODO。

---

# 第二轮 Review — 2.4.17 发布（2026-10-29 00:3x）

## 九问复核（第二轮）

| # | 问题 | 结论 | 依据 |
|---|------|------|------|
| 1 | 是否真正满足 Intent？ | **PASS** | 2.4.17.2610290009 签名包落本地更新源；text-tools 消失、四插件版本与工作树一致，边界批核心交付进包 |
| 2 | 是否符合 Spec？ | **PASS** | R1-R7 逐条落地；R2 判定如实降格为「17/18 + 归因」并经用户披露后放行（闸门2.5 知情同意） |
| 3 | 是否超出 Scope？ | **PASS** | 发布动作 + 残留清理（.trash 可回滚）+ 测试夹具修正（ExtensionPointManagerTests/XCodeConfigTests——属本批边界行为变化的夹具跟随，闸门1 范围内）；发布链脚本零改动 |
| 4 | 是否改了不该改的文件？ | **PASS** | 业务代码零改动；清的 4 处全是 gitignored 构建产物/缓存，且全部移入 `.trash/` 未永久删除 |
| 5 | 测试是否覆盖 AC？ | **PASS** | AC-R1~R7 全部有实测输出；AC-R6 的 probe 子串误报已标注语义（类名/子串），权威判据=走查名册 |
| 6 | 明显回归风险？ | **PASS（附条件）** | ① file-tools e2e 红为存量 P2 缺陷（已挂 TODO 关联）；② 包内 `versions/<ver>/publish/` 27 条存量残留（2.4.16 同款、宿主不扫、惰性）——已记 TODO 治理，随下一版收敛 |
| 7 | 架构不一致？ | **PASS** | QQNT 布局不变量 L1/L2/L3 成立；发布链脚本未动 |
| 8 | Evidence 足够？ | **PASS** | 05-evidence 第二轮含三轮发布根因链、终版机器判据全表、探针与签名独立复核 |
| 9 | 缺陷类闭环？ | **部分** | 本次暴露的「发布链产物残留」属**发布链基建缺陷**（非业务代码缺陷）：本轮做了修复-验证闭环（清残留→重打→L1=19），类级守卫（staging 前按 slnx 校验包内插件数 / sanitize 清 publish/）已记 TODO P2，待专项批落地（触发 scripts/release/** 改动=中档门禁，不在本发布批内顺手做） |

## Findings（第二轮）

- **Critical**：无
- **Major**：无（缺陷包 2355/2359 未出 `artifacts/release` 即被撤下，未进更新源、未通知用户——无错误交付发生）
- **Minor 4**：release-local 三轮才绿，暴露发布链对 `artifacts/publish`、Api `obj/bin`、项目根 `publish/` 三处陈旧产物**零清扫**；同类旁证 = `.temp/e2e/*/publish/publish/` 几十份双层倍增残留。已记 TODO（含三点修法），本轮以手工清扫替代。
- **Minor 5**：probe-dll-string 子串语义对「改名的旧名」判不出 ABSENT（`text_stats`⊂`dev_text_stats`、`folder_stats`=类名）——本轮以走查名册作权威判据；后续可给探针加词边界模式（记入 TODO 同条）。

## Final Decision（第二轮）

**APPROVED** —— 终版包 2.4.17.2610290009 全判据 Verified；两处 Minor 均为发布链治理项，已登记 TODO 不随本批扩修；用户已知情放行（含 file-tools e2e 存量红）。
