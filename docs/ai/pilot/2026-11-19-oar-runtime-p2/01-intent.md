# Intent

> Task ID：`2026-11-19-oar-runtime-p2`

## Problem

P1 客户端已通，但 Delegation 无法按 AccessPoint 选 Oar；无 RuntimeId 字段；无 Session 持久化。

## Why

ADR-007 / concurrency-plan P2：Registry 选路 + SessionKey + Cli 回归。

## Expected Outcome

1. `IAgentTransportRegistry` 按 Kind 选 Cli|Oar。  
2. AccessPoint.RuntimeId；AgentHubSession 表 + SessionKey。  
3. OarTransport 经 OarWorkerClient；Cli/`IAgentDelegation` 不破。

## Constraints

不 Register `IAgentRuntime`（P3）；不改 UI/API；不自动 install；不停宿主。

## Success Criteria

过滤测试绿；032 §6 写 P2；pilot 05–07 齐。
