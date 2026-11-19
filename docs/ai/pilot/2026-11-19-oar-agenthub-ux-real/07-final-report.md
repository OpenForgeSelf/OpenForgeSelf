# AI-Native Pilot Result（最终汇报）

> Task ID：`2026-11-19-oar-agenthub-ux-real`｜状态：✅ COMPLETED

## 1. Repository Understanding

AgentHub OAR 消费投影曾把 Worker `runtime.list`（含 scripted）一律 Available=true；UI Runtime/Session 混排、图标同质。本机仅 Grok 为 executable+登录。

## 2. Selected Task

真源审计 → 去产品模拟 → 品牌图标 → Runtime/Session UX 分区 → feature 032 回写。

## 3. Changed Files

`AgentDescriptor` 增 InstallationKind/Via；`AgentHubRuntimeProvider` 过滤 scripted + installation 诚实探测；AgentHub web 图标与分区布局；`032-agent-hub` §6.1；`dev-stack.ps1` Linux WindowStyle；pilot 00–07。

## 4. Validation

Build: AgentHub web `pnpm run build` ✅（Verified）  
Unit Test: `AgentHubRuntimeProvider` 含「不含 scripted」Pass（Verified）  
Worker smoke: 12/12（Verified，scripted 仅测试面）  
design:check: PASS（Verified）  
API: grok probe available/executable + logged_in；claude not_found；runtimes 无 scripted（Verified）  
E2E: 正式 `ForgeSelf.exe` globalSetup 在 Linux 不可用（ENOENT）— 以 dig-stack + Playwright 截图替代走查证据

## 5. Evidence

见 `05-evidence.md`；artifacts：`/opt/cursor/artifacts/oar-real-vs-simulated.md`、`/opt/cursor/artifacts/oar-ux-real/`。

## 6. Review

`06-review.md` → **APPROVED**（相对本批范围）。

## 7. Risk

Pi bundled 仍列出（诚实标注）；登记 Agent 无 RuntimeId 时仍靠 health；深档全量 e2e 未跑。

## 8. Problems Found

无新增产品缺陷。发现：dig-stack Linux `-WindowStyle` 拒启（本批已修）；Agent 目录曾残留 `remaining-scripted` 登记（已删 + UI 过滤）。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PASS |
| Code → Test | PASS |
| Test → Evidence | PASS |
| Evidence → Review | PASS |

## 10. 问题类归档

无（功能诚实性/UX，非缺陷复发）。

## 11. 重构提案

R0 — 可选：not_found Runtime 默认折叠分组，进一步降噪。

## 12. 悬空教训清单

（空）
