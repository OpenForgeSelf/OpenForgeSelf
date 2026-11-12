---
task: 2026-11-05-probe-dll-restore
risk: { level: L1, triggers: [scripts/probe-dll-string.cjs 单文件恢复, 仓库级脚本 -> 中档全量测试已另行在跑], raised_by_agent: false }
feature: 046
requirements: [INFRA-2]
rollback: git rm scripts/probe-dll-string.cjs 即回到 59d9f07 状态
expected_files:
  - scripts/probe-dll-string.cjs
  - docs/ai/pilot/2026-11-05-probe-dll-restore/**
---

# 04 · Task — 2026-11-05-probe-dll-restore

## Allowed
- 恢复 scripts/probe-dll-string.cjs（59d9f07^ 原字节）与本工件目录。

## Forbidden
- 修改工具逻辑；恢复其他被删探针；改动 59d9f07 其余清理成果。

## 验证命令
`node --check scripts/probe-dll-string.cjs`；行为三态由 qa-ci 在系统 DLL 实测（FOUND/ABSENT/--expect-absent）。