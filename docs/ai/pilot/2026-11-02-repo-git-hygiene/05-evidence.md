# 05 Evidence — 仓库 git 卫生与待提交批次收口

> 只记实际发生（Verified = 亲自跑过拿到真实输出；Inferred = 依据代码/记录推断；Unknown = 未验证）。取证时间：2026-11-02。

## 1 git 卫生实测（Verified）

- `git ls-files -i -c --exclude-standard`（修正前）：29 个 `.agents/skills/**` 文件 + `.github/workflows/release.yml` + `TODO.md` + `.forgeself/memory/2026-10-16.md`、`2026-10-20.md` 被标记「已跟踪但被忽略」——`.agents/`、`.github/` 与 2026-09-24/09-27 的入库决定冲突。
- `.gitignore` 修正后复测：`git check-ignore .agents/skills/ui-ux-design/SKILL.md` exit=1（不再忽略）；`git check-ignore .github/workflows/release.yml` exit=1（不再忽略）；`git check-ignore .forgeself/memory/2026-11-02.md` exit=0（仍忽略，符合规则）。
- `git rm --cached TODO.md .forgeself/memory/2026-10-16.md .forgeself/memory/2026-10-20.md`：停止跟踪，本地文件保留（实测 git status 不再出现 TODO.md 修改条目）。
- `TODO.md.bak` → `.trash/2026-11-02-git-hygiene/TODO.md.bak`（归档非删除）。

## 2 待提交批次工件门禁预演（Verified，pwsh 实跑）

门禁命令：`scripts/verify-pilot-artifacts.ps1 -TaskId <dir> -EnforceHeader -EnforceAcCoverage -EnforceInteraction` + `scripts/check-problem-classes.ps1 -PilotTask <dir>`。

| pilot 目录 | 修正前 | 补件 | 修正后 |
| --- | --- | --- | --- |
| 2026-10-30-aiagent-goal-sse-evidence-fix | FAIL ×4（缺 05/06/07 + 交互声明） | 补 05/06/07（交叉引用 PILOT-077）+ 声明 | **PASS** |
| 2026-10-30-imgateway-chat-list | FAIL ×2（04 无头部块 + AC1-7 无对账） | 04 头部块 + 05 §AC 对账 | **PASS** |
| 2026-10-30-imgateway-null-model-404 | PASS | — | PASS |
| 2026-10-30-release-version-from-csproj | FAIL ×1（04 无头部块） | 04 头部块（内联 risk） | **PASS** |
| 2026-10-30-responses-delta-item-id | FAIL ×1（缺交互声明） | mini-task 补声明 | **PASS** |
| 2026-10-30-responses-text-part | FAIL ×1（缺交互声明） | mini-task 补声明 | **PASS** |
| 2026-10-31-imgateway-layout-direction-fix | PASS | — | PASS |
| 2026-10-31-aiagent-ui-design-brief | FAIL ×4 | **不补**（闸门1 待批纯计划件，brief 未写） | 不提交，留待办 |
| 2026-10-30-responses-incomplete-status | FAIL ×4 | **不补**（闸门1 待批纯计划件，代码未实现——控制器 diff 无 incomplete 映射实测） | 不提交，留待办 |

- 全量 `check-problem-classes.ps1`：**19/19 OK**（PC-001..PC-019 签名/allowlist/guards 全通过，含本轮新增 PC-016）。
- `scripts/hooks/commit-msg` 非文档 trailer 规则实跑暴露：chore 提交缺 `Pilot:` trailer 被拒（第一次 commit A 实测），按规则立本目录后重提。

## 3 提交序列记录（Verified）

分 7 批提交（A 卫生 / B ImGateway / C AIAgent 404 / D Responses / E PILOT-076 / F 规范+门禁 / G 工件补录+台账索引），每批经 pre-commit（工件门禁 + design:check + 台账扫描）与 commit-msg（Pilot trailer）双钩子。逐批结果见 07。

## Unknown

- 各代码批次的构建/测试结果沿用其批次记录（见各自 05），本任务未复跑后端测试（本任务零代码改动，快档不触发）。
