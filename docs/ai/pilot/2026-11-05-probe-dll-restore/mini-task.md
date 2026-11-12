# mini-task — 恢复被误删的正规工具 scripts/probe-dll-string.cjs

## Intent（为什么）
59d9f07「清理冲刺草稿与探针残留」把 **正规工具** scripts/probe-dll-string.cjs 一并删除（该清理针对一次性探针，此文件是被 AGENTS §5.3、plugin-publish-verify、e2e-testing 引用的仓内正规入口）。今晚 T-G 发行验证需要它探 DLL 内字符串（插件装配/版本核验）。

## Spec（改什么、到什么程度）
- Interaction Design：**无界面交互**（CLI 工具恢复，无 UI）。
- 恢复 59d9f07^ 的原字节（git checkout 59d9f07^ -- scripts/probe-dll-string.cjs），不改内容。
- AC1 文件入库且语法可执行（node --check）。
- AC2 三态行为符合约定（qa-ci 已在系统 DLL 实测：FOUND exit 0 / ABSENT exit 1 / --expect-absent exit 0 —— Verified，来源 qa-ci 2026-11-05 报告）。

## Plan
单文件恢复 + 轻量工件；不改动其他清理成果（真正的一次性探针保持已删状态）。

## Task（Allowed/Forbidden + 验证）
Allowed：scripts/probe-dll-string.cjs（原字节恢复）+ 本轻量工件目录。
Forbidden：不改工具逻辑；不恢复其他被删文件；不动 59d9f07 的其余清理。
验证：`node --check scripts/probe-dll-string.cjs` + qa-ci 三态实测记录（05）。