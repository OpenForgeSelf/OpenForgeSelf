---
task_id: 2026-10-06-ai-native-gate-phase1
feature: none
requirements: []
risk: { level: L4, triggers: ["AGENTS.md", "scripts/hooks/**", "docs/18-templates/**", ".github/workflows/**"], raised_by_agent: false, note: "用户在 2026-10-06 明确指示执行；L4 由人授权，非 Agent 自改" }
gate1: { mode: human, at: 2026-10-06T17:16, ref: "用户指令：按以下流程操作更新 / 按你推荐的来，把能做的都做了" }
gate2: { mode: reviewer, at: null, decision: null, note: "待独立会话复跑验证命令后签署" }
gate3: { mode: human, at: null, note: "未提交；留待用户执行" }
expected_files:
  - AGENTS.md
  - .gitignore
  - .gitattributes
  - .github/workflows/ci.yml
  - scripts/classify-risk.ps1
  - scripts/verify-pilot-gate.ps1
  - scripts/verify-pilot-artifacts.ps1
  - scripts/hooks/pre-commit
  - scripts/hooks/commit-msg
  - scripts/install-git-hooks.ps1
  - scripts/backfill-feature-ids.ps1
  - docs/04-standards/risk-policy.md
  - docs/04-standards/feature-requirement-ids.md
  - docs/04-standards/agent-workflow.md
  - docs/18-templates/ai-pilot/04-task.tpl.md
  - docs/18-templates/feature.md
  - docs/19-archive/AGENTS-v1-2026-10-06.md
  - docs/15-roadmap/epics/README.md
  - docs/README.md
  - docs/ai/pilot/2026-10-06-ai-native-gate-phase1/
  - docs/02-features/034-mcp-center.md
  - docs/02-features/038-plugin-local-update-source.md
  - docs/02-features/036-design-system.md
  - docs/03-design/interaction-patterns.md
  - docs/03-design/patterns.md
  - scripts/check-style-tokens.mjs
  - scripts/style-token-baseline.json
rollback: "未提交，工作区整体可还原：git checkout -- <file>；新增文件 git clean -f"
writeback: { feature_doc: "none", reason: "本任务改流程与闸门机制，无对应 docs/02-features 功能文档" }
---

# Agent Task

> 阶段：Stage 4｜把任务变成 Agent 可直接执行的工作单元。Task ID：2026-10-06-ai-native-gate-phase1

## Objective
为 AI-Native 开发流程补齐机械化闸门（判级 / 工件 / trailer / 范围 / 回写 / CI），并使宪法层文档与真实仓库一致；同时落地功能文档稳定需求 ID 的规范、模板与回填工具。

## Scope

### Allowed
见 front-matter `expected_files`。仅限脚本、钩子、CI、模板、docs 文档；不含任何运行时代码。

### Forbidden
- `ForgeSelf.Api/**`、`ForgeSelf.Web/**`、`Plugins/**` 的代码
- `docs/02-features/*.md` 的**正文改造**（本批仅注入 front-matter；需求清单回填只做 3 篇样例）
- 停/启/杀用户运行中的宿主进程
- 提交 git（留给用户）

## Acceptance Criteria
- [ ] AC1 判级脚本存在且语法有效（Parser OK）
- [ ] AC2 工件链校验对轻量目录 PASS、对缺头部块目录 FAIL
- [ ] AC3 工件链校验含 04 头部块字段与 AC 覆盖检查
- [ ] AC4 pre-commit 使用 pwsh 且缺失时回退并告警
- [ ] AC5 commit-msg 强制 `Pilot:` trailer
- [ ] AC6 CI 工作流存在，且非纯文档提交被门禁覆盖
- [ ] AC7 闸门含回写检查
- [ ] AC8 判级脚本计入未跟踪新文件
- [ ] AC9 需求 ID 规范/模板/回填工具就绪，dry-run 覆盖 39 篇
- [ ] AC10 本 pilot 工件链自身可被门禁 PASS
- [ ] AC11 样式棘轮 `scripts/check-style-tokens.mjs` 基线生成且复跑 PASS（允许存量、禁止新增硬编码色值）
- [ ] AC12 39 篇功能文档全部注入 front-matter；交互模式库 `03-design/interaction-patterns.md` 已在 `docs/README.md` 登记

## Expected Files
见 front-matter `expected_files`。

## Verification Commands
```bash
sh -n scripts/hooks/pre-commit scripts/hooks/commit-msg
pwsh -NoProfile -Command '$e=$null; foreach($f in (Get-ChildItem scripts/*.ps1)){[void][System.Management.Automation.Language.Parser]::ParseFile($f.FullName,[ref]$null,[ref]$e); if($e.Count){"FAIL $f"}else{"OK $f"}}'
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-06-ai-native-gate-phase1 -EnforceHeader -EnforceAcCoverage
pwsh -NoProfile -File scripts/classify-risk.ps1 -Base HEAD
pwsh -NoProfile -File scripts/verify-pilot-gate.ps1 -Base HEAD~4 -Head HEAD
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/backfill-feature-ids.ps1 -Apply -FrontMatterOnly
node scripts/check-style-tokens.mjs --update
node scripts/check-style-tokens.mjs
```
