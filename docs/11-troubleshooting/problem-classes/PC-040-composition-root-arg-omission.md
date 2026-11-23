---
id: PC-040
title: 组合根构造可选参数省参漏装配（静默 null 依赖）
tags: [aiagent, composition-root, di, silent-degradation]
invariant: 组合根构造调用必须全显式命名实参（含可选参）；新增可选参时调用点与守卫同步
status: guarded-partial
first_seen: 2026-11-21
recurrences: []
cases: [2026-11-21-composition-root-arg-omission]
signature: []
allowlist: []
guards:
  - {level: G1, path: ForgeSelf.Api.Tests/Plugins/AIAgentTests/AIAgentPluginCompositionTests.cs, status: active}
lens:
  - 新增构造可选参数后，全部调用点是否已显式命名？
  - 可选参为 null 的降级路径是否有日志（静默降级禁令）？
---
# PC-040
Canonical: AIAgentPlugin 组合根装配 AgentFacadeRunExecutor 漏传 planGenerator（可选参省参零警告，仅真实装配路径暴露）。

灾后注（2026-11-23）：修复代码与守卫随未推送提交丢失（v1.10.2 批）；组合根现状回到漏传形态（本仓 9ad7c48 基线核验）；本档案按会话记录重建。
守卫重建（2026-11-23，任务 2026-11-23-aiagent-planning-restore）：按意图重实现 planned 真规划竖切时同步落地 G1 —— 组合根构造全显式命名实参（源码断言 registry:/planGenerator:）+ 规划行为锁（真规划回写 / 规划器不可用回退草稿+标注 / 确认后拒收）。G2（跨全部组合根构造点的类级 arity 枚举）未建，DbAdmin 构造点已同形态显式化但无机器守卫 → status=guarded-partial。