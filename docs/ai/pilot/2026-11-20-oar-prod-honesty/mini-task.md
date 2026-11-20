# mini-task：OAR×AgentHub 生产诚实性（early Exit + bundled 假 available）

- **日期**：2026-11-20 ｜ **形态**：轻量（mini-task；缺陷修复 + 守卫）
- **闸门1 依据**：用户明文 Critical bugs FIX now + production-real（Gate1 already ordered）
- **适用问题类**：PC-037（新建）；相邻 PC-004
- **状态**：✅ 完成

## ① 意图（为什么）

1. `RunPromptBufferedAsync` 在 prompt accept 后 `Delay(50ms)` 即 Exit(0) → 宿主可在 vendor text_delta 入 EventStore 前标 Succeeded。
2. `handlers.ts` 在 `!rt.installation` 返回 `{available, via:bundled}` → 假 available。
3. 产品 ListAgents/UI 继续 omit `scripted`。

## ② 规格要求

1. Prompt accept 后等待 `session.status` idle+turn 证据或终端事件（`turn_ended`）；timeout + cancellation；禁止短 sleep 假成功。
2. 无 `rt.installation` → `not_found`。
3. TDD + PC-037 归档。

### Interaction Design（交互设计）

无新增控件。既有任务台/Session：Succeeded 仅在 OAR turn 结算后出现；Runtime 探测未装显示 not_found（非内置捆绑）。五态：加载=等待 settle；空=无事件；出错=timeout/cancel Error+Exit≠0；无权限=沿用既有；正常=Exit(0)+正文事件。

### Allowed / Forbidden

- **Allowed**：`docs/02-features/032-agent-hub.md`；`OarTransport`/`OarPromptTurnGate`/`OarWorkerClient`/`handlers.ts`；`OarPromptTurnGateTests`/`OarTransportTurnSettleTests`/`OarHandlersHonestyGuardTests`/`OarWorkerClientTests`；PC-037/case；本 pilot。
- **Forbidden**：停杀用户宿主；把 scripted 推入产品 ListAgents/UI；假 Succeeded；改 csproj 版本。

## ③ 验证

见 05-evidence（dotnet 17 绿；Worker smoke 12/12；AgentHub build 0 error）。
