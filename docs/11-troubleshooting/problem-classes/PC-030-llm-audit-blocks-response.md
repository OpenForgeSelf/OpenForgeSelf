---
id: PC-030
title: LLM 网关旁路落库被 await，SQLite 重建或记录失败挡住客户端响应
tags: [llm-gateway, chat-turn, xcode, side-effect]
invariant: 公开 LLM 网关把上游字节写回之后，不得再 await 旁路落库。记录只能入队；入队失败、队列满、后台保存失败都不得改变已经写好的状态码和正文。
status: guarded
first_seen: 2026-11-12
recurrences: []
cases: [2026-11-12-llm-audit-blocks-response]
signature:
  - {id: S1, kind: test, path: "ForgeSelf.Api.Tests/Unit/LlmTurnAuditIsolationTests.cs"}
  - {id: S2, kind: grep, glob: "ForgeSelf.Api/Services/AI/Proxy/LlmProxyHttp.cs", regex: "SaveTurnAsync|UpsertSessionAsync|UpsertTurnAsync|BeginAsync", expect: no-match}
allowlist: []
guards:
  - {level: G1, path: "ForgeSelf.Api.Tests/Integration/UnifiedAIGatewayIntegrationTests.cs", status: active}
  - {level: G2, path: "ForgeSelf.Api.Tests/Unit/LlmTurnAuditIsolationTests.cs", status: active}
lens:
  - 这段落库如果改成 await，客户端要不要等它结束才算收到响应？
  - 保存抛错或 SQLite 重建表时，状态码和正文还会不会被换掉？
---
# PC-030 网关旁路记录不得挡住响应

## 1 不变量与诱因

旁路记录的注释写了「不影响接口响应」，但 `ForwardAsync` 在刷新正文之后仍 `await` 会话和轮次写入，动作要等这次写入返回才结束。客户端要等响应结束。XCode 对 `ChatTurn.RequestHeaders` / `ResponseHeaders` 做不了 `ALTER COLUMN` 时会同步重建表，重建就发生在这次等待里。服务层把异常吞掉，吞掉之前线程已经堵住了。

## 2 特征签名与扫描方法

S2 扫 `LlmProxyHttp.cs`，热路径上不得再出现轮次/会话保存调用。G1 用慢保存和入队抛错两条行为锁响应时间与正文。

盲区：应用内聊天和 AIAgent 的投影仍在返回前 await。那是读模型对齐，不是这条网关旁路。签名不覆盖它们。应用内聊天未走进出站管道是另一件事，见 `docs/02-features/004-provider-models-integration.md` §2.5，不由本类裁定。

## 3 Canonical fix

热路径只组快照并 `Enqueue`。后台单读者用新的 DI scope 做原来的保存。`TryWrite` 失败或保存抛错只记日志。

被否：在 `catch` 里再包一层。异常本来就被吃掉，卡住的是重建表本身，不抛异常。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G1 | `UnifiedAIGatewayIntegrationTests` 慢保存 / 入队抛错 | `dotnet test` 过滤该类 | 不覆盖应用内投影 |
| G2 | `LlmTurnAuditIsolationTests.GatewayHotPath_DoesNotCallTurnPersistence` | 同上 | 只扫三个公开入口和 `LlmProxyHttp` |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|------|------|------------|----------|
| 2026-11-12-llm-audit-blocks-response | 2026-11-12 | SQLite 重建 ChatTurn 请求头列时网关没有响应 | 否（首次） |
