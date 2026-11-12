# 046 · probe-dll-string 仓内工具（probe-dll-string-tool）

> 状态：随 2026-11-05-probe-dll-restore 轻量任务恢复入库（59d9f07 误删修复）。

## 功能点登记（与 pilot mini-task/04 一致）
- INFRA-2 仓内 DLL 探针工具：`scripts/probe-dll-string.cjs` 作为发布/验证链的正规入口存在（AGENTS §5.3、plugin-publish-verify、e2e-testing 引用）；行为约定：命中目标字符串 exit 0（FOUND）、未命中 exit 1（ABSENT）、`--expect-absent` 断言缺席时 exit 0。清理类提交不得移除被技能文档引用的正规工具。

## 用户动线
无用户界面；供 agent/CI 在发行验证中探测插件 DLL 是否含预期类型/字符串。

## 验收基准
pilot 2026-11-05-probe-dll-restore AC1/AC2（node --check + qa-ci 三态实测 Verified）。