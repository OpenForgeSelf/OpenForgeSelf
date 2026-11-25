---
risk: { level: L1, triggers: ["scripts/hooks/commit-msg", "scripts/verify-pilot-gate.ps1", ".gitignore"], raised_by_agent: false }
task_kind: feature
feature: none
expected_files:
  - .gitignore
  - scripts/pilot-exemption-allowlist.json
  - scripts/check-pilot-exemption.ps1
  - scripts/hooks/commit-msg
  - scripts/verify-pilot-gate.ps1
  - ForgeSelf.Api.Tests/RepositoryScriptTests.cs
  - docs/04-standards/ai-native-engineering-workflow.md
rollback: 回退本提交中的白名单脚本、配置与提交钩子改动即可恢复原 strict Pilot 门禁；.gitignore 豁免随回退消失。
---

# 轻量任务：提交 Pilot 工件豁免白名单（新输入）

> 来源：用户明确要求「增加一个配置，白名单以内的文件修改，不需要工件，避免小改都要工件」。

## Intent

新增仓库级提交豁免配置；白名单命中的文件可免 Pilot trailer 与工件；未命中的文件保持现有严格门禁。本地 commit-msg 与 CI verify-pilot-gate 使用同一判定实现。

## Spec

- 配置格式：仓库内 JSON，支持精确路径与 glob。
- 默认豁免：仓库忽略配置 `.gitignore`、本机活队列 TODO、`.forgeself/**`、文档目录/Markdown、README 文件。
- 判定边界：任一非豁免文件仍要求 Pilot trailer；混合提交中只豁免白名单项，不能绕过其余改动。
- 配置缺失或格式非法时：判定失败，提交不得静默放行。

## Plan

1. Add `scripts/pilot-exemption-allowlist.json` as the single allowlist source.
2. Add `scripts/check-pilot-exemption.ps1` to classify changed files and emit unexempt files.
3. Update `scripts/hooks/commit-msg` to check the allowlist before requiring a trailer.
4. Update `scripts/verify-pilot-gate.ps1` to filter the same code diff before trailer and scope checks.
5. Add a xUnit repository guard that verifies shared implementation and mixed-change behavior.

## Task

- Allowed:
  - 以上六个脚本/测试/配置及直接相关规范文档。
- Forbidden:
  - 不豁免源码、插件、发布脚本或任意未登记的文件。
  - 不引入新依赖，不修改业务运行逻辑，不绕过 --no-verify 的已有风险边界。

## Interaction Design（无界面交互声明）

无界面交互：纯提交基础设施与门禁配置变更，无用户界面。

## Acceptance Criteria

- AC1：仅修改 `.gitignore` 时 commit-msg 不要求 Pilot trailer。
- AC2：混合修改 `.gitignore` 与源码时，源码仍要求 Pilot trailer。
- AC3：commit-msg 与 CI 使用同一份配置和判定脚本。
- AC4：配置缺失或非法时不静默豁免。

## Verification

- `dotnet test --filter PilotExemptionAllowlist_MustBeSharedByCommitAndCi`
- `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-pilot-exemption.ps1 -ChangedFilesPath <temporary-list>`
- `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-gate.ps1 -Base HEAD~1 -Head HEAD`
