# 016 · 开发工具箱（Dev Tools）

> 状态：已实现（代码中已落地）
> 最后更新：2026-08-13

## 概述

面向开发者的纯计算型工具集，无数据持久化。覆盖 JSON/YAML/XML 格式化与校验、多种编解码（Base64/URL/Unicode/HTML/Hex）、哈希（MD5/SHA1/SHA256/SHA512/HMAC）、JWT 编解码与生成、UUID/雪花 ID、二维码生成与解码、时间戳转换与时区、颜色转换、正则表达式测试。

## 代码落点

| 层 | 文件 |
|----|------|
| 控制器 | `Plugins/DevTools/Controllers/DevToolsController.cs`（`[Route("api/devtools")]`） |
| 前端视图 | `src/views/DevToolsView.vue` |
| 前端服务 | `src/services/devToolsApi.ts` |

## 核心 API（`api/devtools`，均为 POST 除非注明）

| 分组 | 端点示例 |
|------|----------|
| JSON | `json/format`、`json/minify`、`json/validate`、`json/jsonpath`、`json/to-yaml` |
| YAML | `yaml/to-json`、`yaml/format`、`yaml/validate` |
| XML | `xml/format`、`xml/minify`、`xml/validate` |
| 编码 | `encode/base64`、`decode/base64`、`encode/url`、`decode/url`、`encode/unicode`、`decode/unicode`、`encode/html`、`decode/html`、`encode/hex`、`decode/hex` |
| 哈希 | `hash/md5`、`hash/sha1`、`hash/sha256`、`hash/sha512`、`hash/all`、`hash/hmac` |
| JWT | `jwt/decode`、`jwt/validate`、`jwt/generate` |
| UUID | `uuid/generate`（GET/POST）、`uuid/snowflake`、`uuid/convert` |
| 二维码 | `qrcode/generate`、`qrcode/custom`、`qrcode/decode` |
| 时间戳 | `timestamp/current`（GET）、`timestamp/to-datetime`、`timestamp/from-datetime`、`timestamp/format`、`timestamp/timezones`（GET）、`timestamp/convert-timezone` |
| 颜色 | `color/convert`、`color/palette`、`color/contrast` |
| 正则 | `regex/test`、`regex/match-groups`、`regex/replace`、`regex/split`、`regex/patterns`（GET） |

## 使用要点

- 全部为无状态计算，结果即时返回，不落库。
- 与 TextTools（018）在编解码/哈希上有功能重叠，但 DevTools 偏交互工具箱、TextTools 偏文本文件处理。
