# Review（Stage 8 审查）

> 任务：2026-10-17-problem-class-closure（infra 全量级）。Reviewer 九问重查（第 9 问 v1.2.0 缺陷类专用，本任务为 infra，按机制自反性用"机制九问"执行）。

## 九问核查

| # | 问题 | 结论 |
|---|------|------|
| 1 | 满足 Intent？ | PASS——四层知识模型（Case→PC→Guard→Lens）全链落地：8 份 reference + 2 模板 + 7 PC + 4 case + G2/G3 守卫 + pre-commit/AGENTS/规范/模板/docs README 接入，与设计稿逐节对照无缺件（阶段四按方案缓行并在案） |
| 2 | 符合 Spec？ | PASS——front-matter 机器子集、G1-G4 判据、allowlist 必带到期日、positive control 防空转均按 02-spec 实现；偏差 1 处（工件门禁由 check-problem-classes.ps1 -PilotTask 承载而非自改 L4 脚本 verify-pilot-artifacts.ps1）已在 00/03 记录并被 04-task Verification 采纳 |
| 3 | 超出 Scope？ | PASS——改动全部落在机制接入与守卫文件；Scheduler 缺陷仅登记 TODO+allowlist 豁免未顺手修（守范围控制） |
| 4 | 改了不该改的文件？ | PASS——git status 本批文件与 03-plan Expected Files 一致；`Plugins/TodoTracker/*` 等工作区其他改动属并行批次，未触碰 |
| 5 | 测试覆盖 AC？ | PASS——AC 逐条对应：8/8 绿（Verified）；扫描器 7 PC 全过；红绿往返实证门禁有效 |
| 6 | 明显回归风险？ | PASS（低）——pre-commit 新挂接对旧 pilot 目录（日期<2026-10-17）自动跳过；台账扫描仅在暂存触碰 11-troubleshooting/扫描器时触发，不影响日常提交 |
| 7 | 架构不一致？ | PARTIAL→已改——首实现有 6 处语言层缺陷（见 Findings），全部真跑暴露、修复、回归绿；PC-002 判据与宿主鉴权模型（控制器级 [Authorize]）对齐，无脑补规则 |
| 8 | Evidence 足以证明完成？ | PASS——05 全项 Verified 带命令与退出码；偏差（Debug 锁）如实披露不掩饰 |
| 9 | 同类扫查与守卫落地？（机制自反） | PASS——本任务建立了扫查工具本身并当场自食：PC-001 首跑逮到 Scheduler 真违例，按机制处置（TODO+豁免+台账记录）；悬空教训：本 Review 全部发现均已落 PC/TODO，见 07 §12 |

## Findings

- **Critical**：无。
- **Major**（均已修复，回归绿为证）：
  1. C# 非 verbatim 字符串 `"\b"` 是退格控制符而非 regex 词边界——PC-001 判据曾恒不匹配（`r"..."` Python 风格前缀同类）；
  2. 测试内相对路径 `@"ForgeSelf.Web\src"` 按 vstest 工作目录（bin）解析——DirectoryNotFoundException；
  3. `[Globalization.DateTimeParseOptions]` 短命名空间在 pwsh 不存在——-PilotTask 直接崩。
- **Minor**（已处置/在案）：Moq `Match` 二义性（全限定名）；pre-commit 误插空语句行（已删）；RepositoryScript 基线红 1 项非本批（TODO P2 在案）。
- **机制级观察**：三处 Major 全是「写的时候自以为对、首跑即崩」——恰是本机制存在的理由；若无强制真跑门禁（§0 出口清单），这些守卫会以"已建立"的姿态空转。守卫的守卫（RegistryTests 阳性对照）+ 真跑验证双保险起效。

## Risk

**L1（低）**——纯增量基建：文档/技能/新测试/脚本挂接；对既有流程唯一侵入是 pre-commit 新分支（条件严格：日期阈值 + task_kind 判定 + 暂存路径过滤），旧任务与日常提交不受影响。回滚点：git 未提交，`git checkout --` + 删新目录即可整体撤销。

## Final Decision

**APPROVED**（待闸门2：用户批准后提交；技能目录 `git add -f`）
