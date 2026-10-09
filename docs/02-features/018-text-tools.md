---
feature_key: F018
feature_no: 018
status: implemented
last_updated: 2026-10-06
aliases: ["018-text-tools"]
---

# 018 · 文本工具（Text Tools）

> 状态：已实现（代码中已落地）
> 最后更新：2026-08-13

## 概述

文本处理能力：JSON/XML/HTML 格式化与压缩、多种编解码（Base64/URL/Unicode）、哈希计算（MD5/SHA1/SHA256/SHA512）、文本统计（字数/行数/字符分布）。偏文本级处理，与 DevTools（016）的计算工具箱互补。

## 代码落点

| 层 | 文件 |
|----|------|
| 控制器 | `Plugins/TextTools/Controllers/TextToolsController.cs`（`[Route("api/texttools")]`） |
| 服务 | `Plugins/TextTools/Services/`：`TextFormatterService`(ITextFormatterService)、`TextStatsService`(ITextStatsService)、`HashService`(IHashService)、`EncodingService`(IEncodingService) |
| 模型 | `Plugins/TextTools/Models/TextToolModels.cs` |
| 前端视图 | `src/views/TextToolsView.vue` |
| 前端服务 | `src/services/textToolsApi.ts` |

## 核心 API（`api/texttools`）

| 方法 | 路由 | 说明 |
|------|------|------|
| POST | `format/json`、`minify/json` | JSON 格式化/压缩 |
| POST | `format/xml`、`minify/xml` | XML 格式化/压缩 |
| POST | `format/html`、`minify/html` | HTML 格式化/压缩 |
| POST | `encode/base64`、`decode/base64`、`encode/url`、`decode/url`、`encode/unicode`、`decode/unicode` | 编解码 |
| POST | `hash/md5`、`hash/sha1`、`hash/sha256`、`hash/sha512` | 哈希 |
| POST | `stats` | 文本统计 |

## 使用要点

- 与 DevTools 的 `json/*`、`hash/*` 端点功能重叠，但 TextTools 聚焦文本粘贴处理场景。
