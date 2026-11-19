# Intent

> Task ID：`2026-11-19-oar-runtime-p3`

## Problem

P2 选路已通，但跨插件无法 `ctx.Get<IAgentRuntime>()`；工具/UI 仍以 sessionRef 为主，无 Session 管理面。

## Expected Outcome

1. Provider 实现 + 同实例 Register/DI + Effect 清理 Worker。  
2. hub_ask_agent / delegate 支持 SessionKey；API runtimes/sessions；UI Session 页签。  
3. Cli 回归过滤不新增红。

## Constraints

不自动 install；不停宿主；OAR 类型不进 Abstractions；P4 消费者试点另开。
