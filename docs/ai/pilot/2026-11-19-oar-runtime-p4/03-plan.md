# Plan

> Task ID：`2026-11-19-oar-runtime-p4`

## Files To Change

| 路径 | 动作 |
| --- | --- |
| `ForgeSelf.Api.Tests/Plugins/AgentHub/AgentHubRuntimeConsumerPilotTests.cs` | 新增消费者试点 + Session 守卫 |
| `docs/01-architecture/agenthub-oar-runtime.md` | §7.1 映射注；§11 P4 ✅；§13 Node≥24 |
| `Plugins/AgentHub/RuntimeWorker/README.md` | 运维/CI 表 |
| `docs/02-features/032-agent-hub.md` | §6 P4 进度 |
| `docs/ai/pilot/2026-11-19-oar-agenthub-runtime/concurrency-plan.md` | P0–P4 ✅ |
| `docs/01-architecture/host-capability-seams.md` | 进度行 |
| `docs/ai/pilot/2026-11-19-oar-runtime-p4/` | 00–07 |

## Steps

1. 写 ConsumerPilot 测试（Get / 降级 / Cli / DTO）  
2. 写回架构 §13 + README + 阶段勾选  
3. 跑过滤测试 + Worker smoke  
4. 补 05–07；commit + push  

## Verification

见 `04-task.md` Verification Commands。

## Rollback

`git revert <commit>`
