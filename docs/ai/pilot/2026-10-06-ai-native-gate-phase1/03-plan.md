# Plan

> 阶段：Stage 3｜具体到真实文件。

## Implementation Steps
1. 建立判级脚本 `scripts/classify-risk.ps1`（触发表、内容触发、规模触发、未跟踪文件）。
2. 升级 `scripts/verify-pilot-artifacts.ps1`：轻量模式 + 04 头部块 + AC 覆盖。
3. 新增 `scripts/verify-pilot-gate.ps1`：trailer + 工件 + 范围 + 回写。
4. 重写 `scripts/hooks/pre-commit`（pwsh + 回退）；新增 `scripts/hooks/commit-msg`。
5. 更新 `scripts/install-git-hooks.ps1` 安装两个钩子。
6. 新增 `.github/workflows/ci.yml`（pilot-gate + 风险级）。
7. 修正 `.gitignore`（解除 `.github/workflows/` 忽略）与 `.gitattributes`（commit-msg 锁 LF）。
8. 修正 `AGENTS.md`、`docs/README.md`、`docs/04-standards/risk-policy.md` 的失实路径/表述。
9. 阶段 2：`docs/04-standards/feature-requirement-ids.md` + `docs/18-templates/feature.md` + `scripts/backfill-feature-ids.ps1`。
10. 归档 v1：`docs/19-archive/AGENTS-v1-2026-10-06.md`；规则库追加 Part D。

## Files To Change
| 文件 | 理由 |
|---|---|
| `scripts/classify-risk.ps1` | 新增判级 |
| `scripts/verify-pilot-gate.ps1` | 新增上游闸门 |
| `scripts/verify-pilot-artifacts.ps1` | 轻量+头部+AC 覆盖 |
| `scripts/hooks/pre-commit`、`commit-msg` | 钩子链 |
| `scripts/install-git-hooks.ps1` | 安装两钩子 |
| `scripts/backfill-feature-ids.ps1` | 回填工具 |
| `.github/workflows/ci.yml` | CI |
| `AGENTS.md`、`docs/README.md`、`docs/04-standards/*` | 文档修正/新增 |
| `docs/18-templates/feature.md`、`ai-pilot/04-task.tpl.md` | 模板 |
| `.gitignore`、`.gitattributes` | 忽略/行尾 |

## Test Plan
- 语法：`Parser::ParseFile` 校验 5 个脚本。
- 工件校验：对轻量目录与缺头部块目录分别跑，验证 PASS/FAIL。
- 回填：`backfill-feature-ids.ps1` dry-run 覆盖 39 篇。
- 判级：在能执行 git 的环境跑 `-Base HEAD`（已知 9 文件/960 行）。
- 本 pilot：对自身跑 `verify-pilot-artifacts.ps1 -EnforceHeader -EnforceAcCoverage`。

## Verification
- Build：N/A（无编译产物改动）
- Unit：脚本语法检查（Parser）
- Integration：`verify-pilot-artifacts.ps1` 对真实 pilot 目录
- E2E：N/A
- Other：回填 dry-run；判级（受限环境标注 Unknown）
