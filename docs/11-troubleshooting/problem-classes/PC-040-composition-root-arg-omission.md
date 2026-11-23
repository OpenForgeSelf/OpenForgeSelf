---
id: PC-040
title: 组合根构造可选参数省参漏装配（静默 null 依赖）
tags: [aiagent, composition-root, di, silent-degradation]
invariant: 组合根构造调用必须全显式命名实参（含可选参）；新增可选参时调用点与守卫同步
status: open
first_seen: 2026-11-21
recurrences: []
cases: [2026-11-21-composition-root-arg-omission]
signature: []
allowlist: []
guards:
  - {level: G1, path: none, status: planned, todo: "PC-040 守卫重建（real-Apply 装配图断言 + 显式 arity 枚举）随代码级还原批落地", expires: 2026-12-23}
lens:
  - 新增构造可选参数后，全部调用点是否已显式命名？
  - 可选参为 null 的降级路径是否有日志（静默降级禁令）？
---
# PC-040
Canonical: AIAgentPlugin 组合根装配 AgentFacadeRunExecutor 漏传 planGenerator（可选参省参零警告，仅真实装配路径暴露）。

灾后注（2026-11-23）：修复代码与守卫随未推送提交丢失（v1.10.2 批）；组合根现状回到漏传形态（本仓 9ad7c48 基线核验）；本档案按会话记录重建，PC-040 守卫重建绑 TODO 输入13 A 方案。