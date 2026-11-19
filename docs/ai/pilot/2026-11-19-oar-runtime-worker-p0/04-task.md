---
task_id: 2026-11-19-oar-runtime-worker-p0
feature: 032
risk: { level: L2, triggers: ["Plugins/AgentHub/RuntimeWorker"], raised_by_agent: true }
gate1: { mode: user, at: "2026-11-19T17:47+08:00", ref: "seq6 搞定上面的问题后提交一把，然后开始实现" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - Plugins/AgentHub/RuntimeWorker/
  - docs/ai/pilot/2026-11-19-oar-runtime-worker-p0/
  - docs/02-features/032-agent-hub.md
rollback: "git revert <commit>"
writeback: { feature_doc: 032-agent-hub, reason: "OAR RuntimeWorker P0 落地" }
---

# Agent Task

## Objective

落地 AgentHub RuntimeWorker（OAR 0.109.0）+ 12 步探针，作为 P1 .NET 客户端前提。

## Scope

### Allowed

- `Plugins/AgentHub/RuntimeWorker/**`
- 本 pilot 目录与 `032-agent-hub.md` 进度回写

### Forbidden

- 任何 `.cs` / 插件前端 / 自动 install / 停宿主

## Acceptance Criteria

- [ ] smoke 12/12
- [ ] build 绿
- [ ] 锁版本 0.109.0
- [ ] 032 有 P0 进度句

## Verification Commands

```bash
export PATH="/path/to/node24/bin:$PATH"   # 本机须 Node≥24
cd Plugins/AgentHub/RuntimeWorker
pnpm install && pnpm run smoke && pnpm run build
```
