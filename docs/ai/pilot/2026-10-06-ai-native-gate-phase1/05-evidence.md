# Evidence

> 阶段：Stage 7｜只记录实际发生的事情。来源等级：Verified（亲自跑过，有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。

## Task
2026-10-06-ai-native-gate-phase1

## Changed Files
- `AGENTS.md`（v2.0）、`.gitignore`、`.gitattributes`
- `.github/workflows/ci.yml`（新增）
- `scripts/classify-risk.ps1`、`scripts/verify-pilot-gate.ps1`、`scripts/backfill-feature-ids.ps1`（新增）
- `scripts/verify-pilot-artifacts.ps1`、`scripts/install-git-hooks.ps1`（改）
- `scripts/hooks/pre-commit`（改）、`scripts/hooks/commit-msg`（新增）
- `docs/04-standards/risk-policy.md`、`docs/04-standards/feature-requirement-ids.md`（新增）
- `docs/04-standards/agent-workflow.md`（加 Part D）
- `docs/18-templates/ai-pilot/04-task.tpl.md`、`docs/18-templates/feature.md`
- `docs/19-archive/AGENTS-v1-2026-10-06.md`（新增）、`docs/15-roadmap/epics/README.md`（新增）、`docs/README.md`
- `docs/02-features/034-mcp-center.md`、`038-plugin-local-update-source.md`、`036-design-system.md`（样例回填）
- `docs/02-features/*.md`（35 篇注入 front-matter，累计 39 篇；冲突映射 F028a/b、F031a/b、F036a/b）
- `docs/03-design/interaction-patterns.md`（交互模式库 P1–P7）、`docs/03-design/patterns.md`
- `scripts/check-style-tokens.mjs` + `scripts/style-token-baseline.json`（样式棘轮基线）

## Build
Command: `pwsh -NoProfile -Command '<Parser::ParseFile over scripts/*.ps1>'`
Result: PASS（来源等级：Verified）
```text
PARSE-OK scripts/classify-risk.ps1
PARSE-OK scripts/verify-pilot-gate.ps1
PARSE-OK scripts/verify-pilot-artifacts.ps1
PARSE-OK scripts/install-git-hooks.ps1
PARSE-OK scripts/backfill-feature-ids.ps1
```

## Unit Test
Command: `sh -n scripts/hooks/pre-commit scripts/hooks/commit-msg`
Result: PASS（来源等级：Verified — 由用户在 Git Bash 执行，无语法错误输出）

## Integration Test
Command: `pwsh -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-09-30-sign-default-off`
Result: PASS rc=0（来源等级：Verified）
```text
[mode] 轻量（mini-task.md）
PASS: 全部 PILOT 工件链齐全（00-07 八件 或 轻量 mini-task.md + 05/06/07；含关键节）
```
Command: `... -TaskId 2026-10-01-design-system-m1-agent-tools -EnforceHeader -EnforceAcCoverage`
Result: FAIL rc=1（来源等级：Verified — 缺 04 头部块，符合预期）

## E2E
Result: N/A（本任务不改运行时代码，无产品行为变更）

## Static Analysis
Command: `grep -c` 断言关键代码在位（`--others`、`3b) 回写检查`）
Result: PASS（来源等级：Verified）

## 样式棘轮（style-tokens）
Command: `node scripts/check-style-tokens.mjs --update` → `node scripts/check-style-tokens.mjs`
Result: PASS rc=0（来源等级：Verified）
```text
[style-tokens] 基线已更新 -> scripts\style-token-baseline.json
[style-tokens] 合计 2559 处（ForgeSelf.Web/src:1638, Plugins/*/web/src 合计 921）
[style-tokens] 硬编码色值合计 2559（基线 2559）
[style-tokens] PASS：无新增硬编码色值
```

## 需求 ID 回填（-Apply）
Command: `pwsh -NoProfile -File scripts/backfill-feature-ids.ps1 -Apply -FrontMatterOnly`
Result: 待处理 35 篇 / 跳过 4 篇 / 已落盘 35 篇，合计 39 篇（来源等级：Verified）
```text
汇总：待处理 35 篇，跳过 4 篇，已落盘 35 篇（-Apply）
```

## 验收标准覆盖（AC → 证据）
| AC | 结论 | 来源等级 | 证据 |
|---|---|---|---|
| AC1 判级脚本存在且语法有效 | PASS | Verified | Parser 输出 PARSE-OK |
| AC2 轻量目录 PASS / 缺头部块 FAIL | PASS | Verified | 两次 verify 分别 rc=0 与 rc=1 |
| AC3 校验含头部字段与 AC 覆盖检查 | PASS | Verified（代码在位） | `-EnforceHeader` / `-EnforceAcCoverage` 分支；行为由 AC2 两项体现 |
| AC4 pre-commit 用 pwsh 且缺失回退告警 | PASS | Inferred | 钩子含 pwsh 分支与 powershell.exe 回退告警；未真实触发 |
| AC5 commit-msg 强制 Pilot trailer | PASS | Inferred | 钩子文件存在且含 trailer 校验；未真实触发 |
| AC6 CI 存在且覆盖非纯文档提交 | PASS | Verified（存在） | `ci.yml` 已写；`.gitignore` 已解除忽略。CI 运行：Unknown |
| AC7 闸门含回写检查 | PASS | Inferred | `verify-pilot-gate.ps1` 含 `3b) 回写检查` 段 |
| AC8 判级计入未跟踪新文件 | PASS | Inferred | 含 `git ls-files --others --exclude-standard` |
| AC9 需求 ID 规范/模板/回填工具就绪，dry-run 覆盖 39 篇 | PASS | Verified | `backfill-feature-ids.ps1` dry-run 输出 39 篇 PLAN，冲突字母 F028a/b、F031a/b、F036a/b 正确；3 篇样例已填真实 ID |
| AC10 本 pilot 工件链可被门禁 PASS | PASS | Verified | `verify-pilot-artifacts.ps1 -TaskId 2026-10-06-ai-native-gate-phase1 -EnforceHeader -EnforceAcCoverage` |
| AC11 样式棘轮基线生成且复跑 PASS | PASS | Verified | `--update` 写入基线 2559 处；复跑 rc=0「无新增硬编码色值」 |
| AC12 39 篇注入 front-matter + 交互模式库登记 | PASS | Verified | `grep -l "^---" docs/02-features/*.md` = 39；`docs/README.md` 已指向 `03-design/interaction-patterns.md` |

## Known Limitations
1. **本机 pwsh 无法执行 git**（桌面 AppContainer：`无法在管道中间运行文档: git.exe`，`Get-Command git` 为空）。因此 `classify-risk.ps1` / `verify-pilot-gate.ps1` 的**执行行为在本机未验证**（来源：Unknown）；其判级数据源侧已由 Git Bash 证实（`git diff HEAD --name-only` = 9 文件 / 960 行）。脚本已改为在 git 不可执行时 `exit 2` 大声失败（不静默放行），支持 `GIT_EXE` 覆盖。
2. `pre-commit` / `commit-msg` 未被真实提交触发过。
3. `ci.yml` 未在 GitHub 上运行过；GitHub runner 的 pwsh 可用性未实测。
4. 39 篇功能文档已全部注入 front-matter；仅 3 篇（F034/F036a/F038）回填了完整需求清单（R01…），其余 36 篇的清单为后续批次。
5. `scripts/check-style-tokens.mjs` 的基数为**存量**（2559 处），棘轮只保证「不新增」，不减少存量。

## Unresolved Issues
- 独立 Review 未完成（本件为实现者自查，见 06）。
- `risk-policy.md` 已由用户批准（`status: approved`），分级闸门自 2026-10-06 生效；**判级回放校准**（最近 5 个 pilot）仍未完成。
- 3 组撞号文档（F028a/b、F031a/b、F036a/b）的文件名是否改名待用户拍板。
