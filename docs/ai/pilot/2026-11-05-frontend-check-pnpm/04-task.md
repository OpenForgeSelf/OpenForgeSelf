---
task: 2026-11-05-frontend-check-pnpm
risk: { level: L1, triggers: [.github/workflows/frontend-check.yml CI 配置单文件], raised_by_agent: false }
feature: 100
requirements: [INFRA-1]
rollback: git revert 单提交即可；workflow 文件无运行时影响
expected_files:
  - .github/workflows/frontend-check.yml
  - docs/ai/pilot/2026-11-05-frontend-check-pnpm/**
---

# 04 · Task（范围声明 · 轻量任务的机器可读范围补件）

mini-task.md 承载 Intent/Spec/Plan/Task；本文件仅补 verify-pilot-gate 范围守卫所需的 expected_files 机器声明。

## Allowed
- .github/workflows/frontend-check.yml（pnpm 安装顺序修复）与本工件目录。

## Forbidden
- 其余 workflow、lockfile、Node 版本、仓库脚本。