# mini-task：LLM 网关旁路记录不得挡住响应

闸门1 依据：2026-11-12 用户直接指令（输入1）。成功判据：上游字节已经写回之后，会话/轮次落库再慢、再抛，动作也在 800ms 内返回，状态码和正文保持上游原样。

## 目标

公开网关（`/v1/chat/completions`、`/v1/responses`、`/v1/anthropic/messages`）先结束 HTTP 响应，再在后台记 ChatTurn。SQLite 重建表或记录失败只记日志。上游 LLM 自己的失败仍原样返回。

## 无界面交互

无界面交互。三个入口仍是原来的 HTTP 转发。客户端看到的状态码和正文不因记录成败改变。没有新页面。

## 改动

- `LlmProxyHttp.ForwardAsync`：写完并刷新响应后只调用 `ILlmTurnAudit.Enqueue`，不再 await 会话或轮次。
- `LlmTurnAuditQueue`：有界通道，`TryWrite` 立即返回；单读者后台循环，异常只打日志。队列满则丢弃该条。
- `LlmTurnAuditStore`：原旁路落库逻辑，跑在独立 DI scope 里。
- 三个统一入口控制器改为注入 `ILlmTurnAudit`。`AppBuilder` 注册单例队列，并把它同时挂成 HostedService。

## 不改

- 上游转发、状态码、流式字节拷贝。
- `ChatTurn` 列长度（PC-029 已是 -1）。请求头仍按原逻辑超过 1000 字符截断。
- 应用内 `ChatController` / AIAgent 的投影 await。那是读模型对齐，不是网关旁路。
- XCode `Migration=Full`（另一条进行中的待办）。

## 验证

快档：`dotnet test --filter LlmTurnAuditIsolationTests|UnifiedAIGatewayIntegrationTests`。
中档：宿主源码改动，全量 `dotnet test`，对基线。

## Allowed

只改网关旁路记录的线程归属和失败隔离，加对应测试与 PC-030。

## Forbidden

不改上游报文。不在请求线程上等待 SQLite。不停用户宿主。不提交 git。
