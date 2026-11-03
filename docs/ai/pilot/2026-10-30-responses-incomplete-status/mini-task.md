---
risk:
  level: L1
task_kind: defect
feature: responses-incomplete-status
expected_files:
  - ForgeSelf.Api/Controllers/UnifiedAI/OpenAIResponsesStreamEvents.cs
  - ForgeSelf.Api/Controllers/UnifiedAI/OpenAIResponsesController.cs
  - ForgeSelf.Api.Tests/Unit/OpenAIResponsesOutboundSchemaTests.cs
rollback: 还原上述三个代码文件即可恢复「恒报 completed」的现状行为
---

# 轻量任务：Responses 上游截断（finish_reason=length）应回 incomplete 而非 completed

## Intent（为什么）

实测（2026-10-30 输入1，dev-stack :7301）：思考型上游（gpustack/deepseek-v4-flash）把 `max_output_tokens` 预算吃满时（usage=预算值、终止块 `FinishReason="length"`），`POST /v1/responses` 流式回 `response.completed` 且正文为空、非流式回 `status:"completed"` 且 `output` 空。严格客户端（AI SDK / Open Responses）会把「空回复」当**正常完成**渲染，用户看到空气泡，无法得知是预算截断。

## Spec（改成什么样 · 协议保真）

官方 OpenAI Responses 语义：截断响应 = 终止事件 `response.incomplete`、`response.status="incomplete"`、`response.incomplete_details={"reason":"max_output_tokens"|"content_filter"}`。本任务把 finish_reason 映射补上：

- 流式：`OpenAIResponsesStreamEvents.Accumulator.Observe` 增记 `chunk.FinishReason`（终止块已携带，`OpenAISdkAdapter.cs:415-422` 透传 `length/content_filter/stop/tool_calls`，现状被丢弃）；`Completed` 按 finish 分派——`length` → `type="response.incomplete"` + `status="incomplete"` + `incomplete_details.reason="max_output_tokens"`；`content_filter` → 同型 `reason="content_filter"`；`stop/tool_calls/空` → 维持 `response.completed`/`completed` 原状。
- 非流式：`ConvertToResponsesResponse`（`OpenAIResponsesController.cs:393-398` 现为 `_ => "completed"`）同规则映射 `status` + `incomplete_details`。
- message item 内层 status 同步映射（截断时 `incomplete`）。Unknown：AI SDK zod 对 item 级 `incomplete` 的接受度以实施期对官方 schema 核对为准；若 item 级枚举不含 incomplete 则 item 保持 `completed`、仅 response 层改（降级方案，不阻断）。

## Plan（怎么改）

1. TDD 红灯：`OpenAIResponsesOutboundSchemaTests` 新增 2 断言组——① `Completed(FinishReason=length)` 序列化 = `response.incomplete` + `incomplete_details.reason=max_output_tokens`；② `stop` 维持 `response.completed`。先红。
2. 改 `OpenAIResponsesStreamEvents.cs`（Observe 记 FinishReason + Completed 分派）与 `OpenAIResponsesController.cs`（非流式映射）转绿。
3. 快档门禁 + dev-stack 现场：小预算流式请求应收到 `response.incomplete`（正文仍空但客户端可判「预算截断」），大预算请求仍 `response.completed` 且正文完整。

## Task（边界）

- **Allowed**：上述三文件 + 本目录工件 + 日记/TODO。
- **Forbidden**：不动 `OpenAISdkAdapter` 出站解析；不做 reasoning 增量透传（TODO P3 另行立项）；不动 chat/anthropic 两控制器；不发布、不停 :51888、不启停任何宿主。
- **验证命令**：`dotnet test --filter OpenAIResponsesOutboundSchemaTests`（旧 3 + 新 ≥4 全绿）；`ForgeSelf.Api` `dotnet build`；dev-stack 现场两组请求对拍。

## 代价与风险

风险 L1：事件形状仅在上游截断分支变化，正常完成路径字节级不变；对未知严格客户端，`response.incomplete` 是官方事件名，zod 校验应通过（AI SDK responseSchema 含 incomplete 枚举）。回滚点 = 三文件还原。

## 排程约束（2026-10-30）

并行会话正在打 2.4.22 包（14:16 已登记停 dev-stack、占 Api DLL 锁）——**Implement 须等该发布批结束**；本修复不随 2.4.22 携带，随下一批发布。
