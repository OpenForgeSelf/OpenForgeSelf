---
id: PC-039
title: Mock 提供方静默改道 / 会话模型快照缺失致 resume 被默认模型接管
tags: [aiagent, mock, llm, model-snapshot]
invariant: Mock 提供方必须直连不经转发管道改道；会话模型身份必须随 AgentRun 快照贯通 Create/Resume/Restart
status: open
first_seen: 2026-11-20
recurrences: []
cases: [2026-11-20-mock-provider-silent-divert]
signature: []
allowlist: []
guards:
  - {level: G1, path: none, status: planned, todo: "代码级还原批（TODO 输入13 A 方案）落地时重建双接缝单决策点断言与模型快照贯通测试", expires: 2026-12-23}
lens:
  - Mock 配置下请求是否被转发管道吃掉（400 无感）？
  - resume/restart 后模型身份是否被宿主默认模型静默接管？
---
# PC-039
Canonical: ForgeChatClientAdapter.Create 单一决策点（Mock 直连 / 非 Mock 走管道）+ AgentRun.ChatModelId 快照三处贯通。

灾后注（2026-11-23）：修复代码与守卫随未推送提交丢失（v1.10.1 批，37 files），守卫失守；本档案按会话记录重建，代码级还原绑 TODO 输入13 A 方案。