# Evidence — Responses incomplete（规划入库，代码未改）

## 范围说明

本目录当前只登记缺陷规划（`mini-task.md`）。**未改** `OpenAIResponsesStreamEvents.cs` / `OpenAIResponsesController.cs` / 相关测试。

## 现状对照（Verified · 源码读码，2026-11-03）

- 流式结束仍走「完成」语义：Accumulator 不持久化 `FinishReason` 用于 incomplete 分派（与 mini-task Intent 一致）。
- 非流式 `status` 映射仍以 completed 为主路径（mini-task Plan 所述 `_ => "completed"` 类行为未改）。
- 本回合**未**跑 `dotnet test --filter OpenAIResponsesOutboundSchemaTests`，也**未**在 dev-stack 复现 length 截断。

## AC 覆盖

| AC（来自 mini-task） | 状态 | 等级 |
|----------------------|------|------|
| length → response.incomplete | 未实现 | Unknown（无测试绿证） |
| stop → 仍 completed | 未实现（无新断言） | Unknown |
| 现场小预算流式对拍 | 未做 | Unknown |

## 为何仍入库

便于队列保留可执行规划；避免悬空口头 TODO。实现须另开回合：红灯 → 改映射 → 快档 → 补本文件为 Verified。
