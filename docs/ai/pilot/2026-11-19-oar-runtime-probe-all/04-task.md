---
task_id: 2026-11-19-oar-runtime-probe-all
feature: 032
risk: { level: L1, triggers: ["Plugins/AgentHub/web/"], raised_by_agent: true }
gate1: { mode: authorized-by-user, at: "2026-11-19T15:37Z", ref: "user:加一下这个功能…提交全部…合并到主分支" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - Plugins/AgentHub/web/src/AgentHubView.vue
  - Plugins/AgentHub/docs/ui-design-brief.md
  - docs/02-features/032-agent-hub.md
  - docs/ai/pilot/2026-11-19-oar-runtime-probe-all/
rollback: "git revert <commit>"
writeback: { feature_doc: 032-agent-hub, reason: "one-click probe all runtimes in inventory" }
---

# Agent Task（轻量 · 细节见 mini-task.md）

## Objective

Runtime 库存「一键探测」：对当前产品面 Runtime 列表并发调用既有 `POST …/runtimes/{id}/probe`。

## Scope

### Allowed

`expected_files` 所列。

### Forbidden

新增 batch API（除非明确必要）；停杀用户宿主；改 Agent 卡探测；改 csproj 版本行。

## Acceptance Criteria

- [x] 库存区可见「一键探测」且与「刷新」分离
- [x] 运行中禁用；每卡进度；聚合成功/失败
- [x] 032 §6.1 + ui-design-brief 已回写
- [x] 插件 web build PASS；真机截图证据

## Verification Commands

```bash
cd Plugins/AgentHub/web && pnpm run build
```
