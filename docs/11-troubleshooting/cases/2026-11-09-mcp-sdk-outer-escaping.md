---
id: 2026-11-09-mcp-sdk-outer-escaping
title: MCP 网关 JSON-RPC/SSE 外层（第三方 SDK 传输层）中文仍转义 \uFF08——SDK 无配置缝，记录不修
pc: PC-027
severity: P3
evidence: Verified
---
# Case：2026-11-09 MCP C# SDK 传输层外层转义调研（无缝，停止修复）

> Case 只讲「这一次发生了什么」；规则/修法/扫描方法一律引用 [PC-027](../problem-classes/PC-027-json-nonascii-default-escaping.md)，不复述。调研证据链 → `docs/ai/pilot/2026-11-09-mcp-sdk-escaping/05-evidence.md`（下称 05-E#）。

## 1 现象

2026-11-09 17:49（输入10 发布验证）实测 `POST http://127.0.0.1:18890/mcp` → `tools/call universal_tool` → `read_file`：**内层工具信封**（`ForgeJson.Default`）中文原样（`# TODO（当前状态）`），**外层 JSON-RPC/SSE 帧**由 ModelContextProtocol C# SDK 2.2.0 自身序列化——非 ASCII 全转 `\uFF08` 形态、引号转 `\u0022` 形态。同一响应两形态并存（原始证据 `.temp/fs-call-response.json`）。用户 18:30 直接指令修复（输入11）。

## 2 复现与闭环形态

- 现象复现 = 已归档抓包（只读重放即可复现：任意含中文参数/结果的 tools/call）。
- **TDD RED→GREEN 不适用**：修复面在第三方依赖内部且无注入缝（05-E3/E5/E7）；仓内无可承载「外层中文原样」断言并转绿的代码——强行落测试即永久红。本 case 按「记录不修」（AGENTS §7.5 ⏸ 阻塞态）闭环：验收 = 05 证据链（全 Verified），而非红灯转绿。
- 守卫落位形态：无 G1-G4 新增（第三方内部不可达，见 §6 逃逸分析）；替代守卫 = PC-027 lens 第 4 问 + index 速查行 + 本档。

## 3 根因（缝定位链）

外层成功响应（主逃逸面）：`McpSseEventWriterExtensions.FormatJsonRpcMessage`（v2.2.0 L39-56）`new Utf8JsonWriter(writer)` **不带 JsonWriterOptions** → `Encoder=null` → `JavaScriptEncoder.Default` → 转义；writer 为 `private [ThreadStatic]` 不可达。两处 `JsonSerializerOptions`（`DefaultOptions` v2.2.0；v2.4.0 再加 `ProtocolOptions`）均启动 `MakeReadOnly()` 冻结；`HttpServerTransportOptions` 全配置面无序列化项（05-E5）；STJ writer 语义排除「传 typeInfo 改写已构 writer」旁路（05-E7）。错误路径（`Results.Json(…, s_errorTypeInfo)`）理论上可投影 options.Encoder，但该 options 即冻结的 DefaultOptions ⇒ 同无缝（05-E4）。**最新版 v2.4.0 逐字同形**（05-E6）。

## 4 同类扫查命中表（外层面全清单）

| 范围 | 同根因？ | 处置 |
|------|----------|------|
| SSE 成功路径（tools/call、tools/list 全部主响应） | 是（主逃逸面） | 无缝可修 → 记录不修 + 选项呈报 |
| 错误响应路径（HTTP 状态码 + error JSON） | 是（同冻结 options） | 同上 |
| Streamable HTTP POST 响应流 | 是（汇入同一 writer 链） | 同上 |
| `/health`（McpGatewayServer.cs L217-223 原始字符串） | 否 | 无 STJ，无关面 |
| 网关日志/落盘序列化 | 否 | 宿主自身面，已在 G2 覆盖内 |
| SDK stdio 客户端传输（连外部 MCP） | 否 | 解析/接收侧，非本现象写出面 |

## 5 处置与被否方案（呈报选项）

- **O1 接受+文档化**（本批已完成）：`\uXXXX` 为 JSON 规范合法、语义无损（任何合规解析器还原原样），残留仅「直接读线路原文」可读性。
- **O2 上游 issue/PR**（推荐并行）：附精确指针（`FormatJsonRpcMessage` L46-55，请求 writer 构造接入 Encoder 缝，如从 `McpJsonUtilities` 暴露 Encoder/JsonWriterOptions 配置）。
- **O3 宿主侧 `/mcp` 响应改写中间件**（可备选，需独立 mini-task + 行为守卫）：缓冲 POST 响应体、按 `data:` 行解析重序列化；牺牲流式、双倍内存。
- **被否**：① fork/patch NuGet 包（用户红线，任何结局禁止）；② 反射 hack（仅达错误路径 + AOT/trimming 脆性）；③ 仅升级 SDK（v2.4.0 已证同形无缝）。

## 6 逃逸分析

**非守卫逃逸**：G2 守卫设计范围 = 仓内生产 .cs 源码字面量；第三方 NuGet 依赖内部序列化面从一开始不可达（PC-027 已登记 ForgeJson 守卫不可达）。定性 = **新暴露面**（内层修复后同一现象在外层面继续可见）。机制上浮：PC-027 lens 新增第 4 问（第三方库自带 JSON 面的转义行为与配置缝核查）+ `index.md` 现象速查行——下个遇到「MCP/第三方响应中文变 \uFF08」的人 30 秒定位到本档。

## 7 守卫落地

无 G1-G4 新增（不可达）。缓解三件：PC-027 §6 调研结论节 + lens 第 4 问；index 速查行；若 O3 立项则由其 mini-task 配行为守卫（断言 SSE 帧中文原样）。

## 8 基建改进

① PC-027 增「§6 外层第三方 SDK 缝调研（2026-11-09）」与 lens 第 4 问；② `docs/11-troubleshooting/index.md` 增速查行；③ `problem-classes/README.md` 台账 PC-027 行复发数更新并标注「非逃逸新面」；④ TODO PC-027 观察项⑤ 转 ⏸ 阻塞态（待上游/宿主侧决策）。
