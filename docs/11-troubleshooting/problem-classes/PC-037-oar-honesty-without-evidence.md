---
id: PC-037
title: OAR 无证据即 available / Succeeded
tags: [oar, agenthub, honesty]
invariant: OAR available/Succeeded 必须由真实 installation 或 turn 结算证据推出
status: guarded
first_seen: 2026-11-20
recurrences: []
cases: [2026-11-20-oar-early-exit-and-bundled-fake]
signature:
  - {id: S1, kind: test, path: "ForgeSelf.Api.Tests/Plugins/AgentHub/OarHandlersHonestyGuardTests.cs"}
  - {id: S2, kind: test, path: "ForgeSelf.Api.Tests/Plugins/AgentHub/OarPromptTurnGateTests.cs"}
  - {id: S3, kind: test, path: "ForgeSelf.Api.Tests/Plugins/AgentHub/OarTransportTurnSettleTests.cs"}
allowlist: []
guards:
  - {level: G1, path: "ForgeSelf.Api.Tests/Plugins/AgentHub/OarPromptTurnGateTests.cs", status: active}
  - {level: G1, path: "ForgeSelf.Api.Tests/Plugins/AgentHub/OarTransportTurnSettleTests.cs", status: active}
  - {level: G2, path: "ForgeSelf.Api.Tests/Plugins/AgentHub/OarHandlersHonestyGuardTests.cs", status: active}
lens:
  - prompt accept 后是否等 turn 结算？
  - 缺 installation 是否 not_found？
---
# PC-037
Canonical: OarPromptTurnGate + handlers not_found.
