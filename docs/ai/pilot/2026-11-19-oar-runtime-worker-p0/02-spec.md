# Specification

> Task ID：`2026-11-19-oar-runtime-worker-p0`｜feature: 032

## Functional Requirements

| ID | 需求 |
| --- | --- |
| FR-1 | Node Worker 包：`Plugins/AgentHub/RuntimeWorker`，依赖 OAR 0.109.0 |
| FR-2 | JSON-RPC/JSONL stdio：stdout=协议，stderr=日志 |
| FR-3 | Registry = `defaultRuntimes` + `scripted` |
| FR-4 | 方法：`worker.hello`、`runtime.list|installation|auth|models|usage|installPlan`、`session.open|resume|prompt|deliver|control|records|status|dispose|list` |
| FR-5 | 通知 `session.event` |
| FR-6 | smoke 12 步（AC-R1） |
| FR-7 | `runtime.list` 不携带 brand SVG（体积） |

## Interaction Design

无界面。经 stdio / `pnpm run smoke` 操作；错误以 JSON-RPC `error.code/message` 返回。

## Business Rules

- 不暴露自动 `runtime.install` 执行入口（本批）；`installPlan` 只读可有
- Session 对外 `sessionKey`（GUID），与 OAR `session.id` 分离

## Boundary / Error

- 未知 runtime → -32002；未安装 → -32003；未知 method → -32601
- scripted 无 auth/models → `not_supported`

## Compatibility

- 不破坏现有 CliTransport / IAgentDelegation
- Node 22 无法跑本包（engines）

## Acceptance Criteria

- [ ] AC1 smoke 12/12
- [ ] AC2 `pnpm run build` 成功
- [ ] AC3 stdio hello + list 含 scripted/claude/codex/opencode
- [ ] AC4 无 `.cs` 业务改动
