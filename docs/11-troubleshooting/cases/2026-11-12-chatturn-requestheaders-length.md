---
id: 2026-11-12-chatturn-requestheaders-length
title: 写入聊天轮次失败，RequestHeaders 长度限制 1000 字符
pc: PC-029
severity: P1
evidence: Verified
---
# Case：2026-11-12 ChatTurn 请求头超长

## 1 现象

与 404 同时出现：`写入聊天轮次失败（不影响接口响应）: [RequestHeaders/请求头JSON@ChatTurn]长度限制1000字符`。14:07 那次头没超长，轮次 42330 保存成功，所以两条故障互不依赖。

## 2 复现红灯

修复前 `SaveTurnAsync_HeaderJsonOver1000Chars_PersistsFullText`：`Expected turn.Id to be greater than 0L, but found 0L`（Insert 被 XCode 拒绝，服务吞掉异常）。日志：`.temp/tmp/alias-header-red.log`。

## 3 根因（5 Whys + 第 6 问）

1. 轮次没插入 → Insert 抛长度异常。
2. 为什么抛 → `RequestHeaders` 的 `DataObjectField` 长度是 1000。
3. 为什么值会超过 → 控制器把全部请求头 JSON 写进去。
4. 为什么正文可以超长、头不行 → `RequestBody` 已是 -1，头列漏改。
- **第 6 问：** 没有「整包 JSON 列必须 -1」的测试，超长只在真实浏览器头上出现。

## 4 同类扫查命中表

| 位置 | 同根因？ | 处置 |
|------|----------|------|
| `ChatTurn.RequestHeaders` 长度 1000 | 同根因 | 改为 -1 |
| `ChatTurn.ResponseHeaders` 长度 1000 | 同根因（整包头 JSON） | 改为 -1 |
| `ChatTurn.ErrorMessage` 长度 1000 | 异根因，写入前已截断 | 不改 |
| 其他实体的 1000 字描述字段 | 异根因 | 不改 |

## 5 修复与被否方案

见 PC-029。否决截断到 1000。

## 6 重构提案与结论

R0。两列改成与正文相同的不限长文本即可，不必改表。

## 7 守卫落地

G1：超过 1000 字的请求头和响应头可以插入并按原文读回。修复后该用例通过。
