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
