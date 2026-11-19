# AI-Native Pilot Result（最终汇报）

> Task ID：`2026-11-19-oar-remaining-close`｜状态：✅ COMPLETED（本批 F/V/W；缓做仍 ⏸）

## 1. Repository Understanding

OAR P0–P4 + gap-close 主路径已在；剩余为 agents/tasks 鉴权、Session/Runtime 管理面接线、安装确认、能力禁用与证据/走查。

## 2. Selected Task

按 `oar-remaining-gaps.md` 闭合 F1–F5，落盘 V* 证据与 W* 走查；缓做 D*/V9/V10/W9/W10 标 ⏸。

## 3. Changed Files

Agents/Tasks/Runtimes 控制器、OarWorkerClient、RuntimeWorker handlers、AgentHub web `http.ts`/`AgentHubView.vue`、Auth/Provider/Ask/EventStore 测试、032/架构、pilot 00–07。

## 4. Validation

Build: AgentHub + RuntimeWorker + plugin web ✅（Verified）  
Unit Test: 相关过滤集 **17 Passed**；AgentHub 宽过滤 106 Passed / 4 基线红（Linux ProbeSettings/powershell）（Verified）  
E2E: 正式 ForgeSelf.exe globalSetup 未跑（V9 ⏸）；本批用 Playwright walkthrough + 既有 Session 冒烟用例入库状态保留

## 5. Evidence

见 `05-evidence.md`；Cursor artifacts `oar-todo-verify/`；`oar-todo-walkthrough/INDEX.md`。

**追加（真 Grok）**：Cursor artifacts `oar-grok-acceptance/REPORT.md` — Worker 真会话 `GROK_ACCEPTANCE_OK`；UI G0–G44 **48/48**；诚实报告 `oar-screenshot-provenance.md`（原 walkthrough = scripted，非 vendor）。

## 6. Review

`06-review.md` → **APPROVED**（相对本批范围）；真 Grok 验收不改变 F1–F5 代码结论，只补证据等级（scripted → vendor）。

## 7. Risk

L1 — Linux `dev-stack` WindowStyle（D11 ⏸）改手工 net10.0；scripted installPlan=not_supported；会话短时 Closed。  
L2 — Host prompt 缓冲过短时 EventStore 可能缺 vendor 正文；Steer UI 依赖 agent F1/F2 facets。

## 8. Problems Found

无新增产品缺陷；环境侧 D11/V9 已入 TODO ⏸。  
发现：新登记 Oar agent 默认 facets 空 → caps 仅 `abort,oar` → Steer 误禁用（配置面，非协议缺陷）。

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

### 9.5 本批自复盘

- **经验提取**：安装确认用 `confirmed=true` 硬闸门；走查按钮文案含空白时用 hasText 宽松匹配；Linux 用 `/etc/machine-id` 作 `FORGE_MACHINE_GUID` 解密 token。  
- **待改进**：`dev-stack` WindowStyle 仍堵正式入口（D11）；正式 e2e exe 仍 V9。

## 10. 问题类归档

无（功能补齐，非缺陷复发）。

## 11. 重构提案

R0 — 管理面最小接线已满足；全量 SSE / vendor install UX 另开。

## 12. 悬空教训清单

（空）
