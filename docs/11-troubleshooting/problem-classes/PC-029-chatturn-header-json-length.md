---
id: PC-029
title: ChatTurn 整包请求头/响应头 JSON 被 DataObjectField 1000 字截断校验拒绝
tags: [xcode, chat-turn, header-json]
invariant: 持久化整包 HTTP 头 JSON 的列必须是不限长文本（DataObjectField 长度 -1）。不得把 Cookie/Authorization 这类会超过 1000 字的字段标成 1000。
status: guarded
first_seen: 2026-11-12
recurrences: []
cases: [2026-11-12-chatturn-requestheaders-length]
signature:
  - {id: S1, kind: test, path: "ForgeSelf.Api.Tests/Unit/ChatTurnServiceTests.cs"}
allowlist: []
guards:
  - {level: G1, path: "ForgeSelf.Api.Tests/Unit/ChatTurnServiceTests.cs", status: active}
lens:
  - 新字段如果保存的是整包 JSON（请求头、响应头、正文），长度是 -1 还是一个会在真实浏览器头上爆掉的上限？
  - 写入失败被 ChatTurnService 吞掉之后，调用方还会不会以为轮次已经落库？
---
# PC-029 ChatTurn 头 JSON 不限长

## 1 不变量与诱因

`RequestBody` / `ResponseBody` 已经是长度 -1。`RequestHeaders` / `ResponseHeaders` 仍是 1000。控制器把 `Request.Headers` 整包序列化进去，浏览器 Cookie 一长就在 `Insert` 时被 XCode 拒绝。服务把异常吞掉，只打「写入聊天轮次失败」。

## 2 特征签名与扫描方法

G1：`SaveTurnAsync_HeaderJsonOver1000Chars_PersistsFullText` 写入超过 1000 字的请求头和响应头，断言 Id > 0 且原文可读回。

盲区：别的实体上仍有长度为 1000 的短文本列（错误信息、描述）。那些列的写入方会截断，不在本类。

## 3 Canonical fix

`ChatTurn.RequestHeaders` 与 `ResponseHeaders` 的 `DataObjectField` 第四参改为 -1，与 `ChatMessage.Content` 相同。SQLite 不强制 VARCHAR 长度，不需要迁移。

被否：写入前截断到 1000。调试字段被切开会丢掉后半段头，和「保留原始请求头」的用途相反。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G1 | `ChatTurnServiceTests.SaveTurnAsync_HeaderJsonOver1000Chars_PersistsFullText` | `dotnet test` 过滤该用例 | 不覆盖 ErrorMessage 等仍为 1000 的短字段 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸 |
|------|------|------------|----------|
| 2026-11-12-chatturn-requestheaders-length | 2026-11-12 | 写入聊天轮次失败：RequestHeaders 长度限制 1000 字符 | 否（首次） |

## 6 现状补记（2026-11-12 问答）

列长度已经是 -1（`ForgeSelf.Api/Entities/ChatTurn.cs` 的 `RequestHeaders` / `ResponseHeaders`）。本类否决的是写入前截到 1000。

`LlmTurnAuditStore.SaveAsync` 曾在序列化后把请求头 JSON 切到 1000 字符。2026-11-13 已删掉这段切片，全文写入 `RequestHeaders`。同一次保存仍不给 `ResponseHeaders` 赋值。守卫：`LlmTurnAuditIsolationTests.SaveAsync_HeaderJsonOver1000Chars_PersistsFullText`。

整包头只为调试归档（`docs/chat-record-session-redesign.md` §1.2）。会话归并不读这列：`ChatSessionResolver.ResolveConversationKey` 在请求当时只取 `X-Session-Id`、`X-Conversation-Id`、`x-interaction-id`、`x-conversation-id`。聊天记录详情 `ChatRecordDetail.vue` 只展示 `requestBody`。网关回包不依赖这列。入站整包头含 `Authorization` / `Cookie`，这条存储路径没有脱敏。

因此要留就留全文；截成 1000 会得到非法 JSON，也切掉后半段头。本次问答不改代码。

2026-11-12 20:59:53 运行实例 `2.4.22.2611122022` 打出这行警告时，对应 `ChatTurn.Id=42343`：`ResponseStatus=200`，`RequestHeaders` 长度正好 1000，末尾停在半截 JSON（`iew_required\":false,`），响应正文以 `data: [DONE]` 结束，正文里没有 `error` / `exception`。21:44:41 的 `2.4.22.2611122107` 同样是 200 且头长 1000（Id 42345、42346）。这行日志不把该次 HTTP 响应变成失败。

2026-11-13 01:30:19 运行包 `2.4.22.2611130032` 再次打出同一行。对应 `ChatTurn.Id=42352`：`CreatedTime` 与日志同一毫秒，`ResponseStatus=200`，`RequestHeaders` 长度正好 1000，`ResponseBody` 长度 4762。同秒的 42350、42351 同样是 200 且头长 1000。切片在 `SaveAsync`，而 `LlmProxyHttp.ForwardAsync` 先 `FlushAsync` 再入队。这行只切落库的请求头，不改已经写出的回包。该包仍带这行，是因为当时源码还没删切片。

2026-11-13 源码已删切片。dev-stack `:7301` 用 OpenAI SDK 打默认模型 `default:qwen3_8_vision` 后，ChatTurn 13–18 的请求头长度为 1671–1777，不再停在 1000。
