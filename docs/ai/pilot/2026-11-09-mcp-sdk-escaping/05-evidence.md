# 05-evidence：MCP C# SDK 外层转义缝调研

> 分级：**Verified**（亲自取得输出）/ **Inferred**（凭证据推断）/ **Unknown**（未验证）。现场现象证据（`.temp/fs-call-response.json`，2026-11-09 17:49 输入10 抓包）为本批前置输入，引用不重测。

## E1 SDK 引用与版本盘点 — Verified

- `Plugins/McpCenter/McpCenter.csproj:38`：`<PackageReference Include="ModelContextProtocol.AspNetCore" Version="2.2.0" />`。
- NuGet 缓存盘点：ModelContextProtocol（Core）已有 **2.0.0、2.2.0**；ModelContextProtocol.AspNetCore **2.2.0**。无其他版本。

## E2 `McpJsonUtilities.DefaultOptions` 可变性（v2.2.0，在用版）— Verified

- 反射探针本机缓存 DLL（`ModelContextProtocol.Core.dll` 2.2.0）：`DefaultOptions` 为 **get-only 静态属性**，**无 `set_DefaultOptions`**；类型内存在 `MakeReadOnly` 调用路径。
- 官方源码 v2.2.0 `src/ModelContextProtocol.Core/McpJsonUtilities.cs`（真实路径经 git trees API 确认；先前猜测的 `Json/` 子目录 404）：
  - `public static JsonSerializerOptions DefaultOptions { get; } = CreateDefaultOptions();`
  - `CreateDefaultOptions` 内 `options.MakeReadOnly();` → **启动即冻结，运行期不可注入 Encoder**。

## E3 成功响应（主逃逸面）序列化缝 — Verified ✦决定性证据

v2.2.0 `src/ModelContextProtocol.Core/Server/McpSseEventWriterExtensions.cs`（快照 `.temp/tmp/ssewriterext-2.2.0.cs`，71 行）：

```csharp
[ThreadStatic] private static Utf8JsonWriter? _jsonWriter;   // L13-14，private

// L39-56 FormatJsonRpcMessage：
if (_jsonWriter is null) { _jsonWriter = new Utf8JsonWriter(writer); }   // ← 无 JsonWriterOptions
else                     { _jsonWriter.Reset(writer); }
JsonSerializer.Serialize(_jsonWriter, item.Data,
    McpJsonUtilities.JsonContext.Default.JsonRpcMessage!);
```

- writer 构造**不带任何 `JsonWriterOptions`** → `Encoder = null` → STJ 默认 `JavaScriptEncoder.Default` → 非 ASCII 全转 `\uXXXX`、引号转 `\u0022`——与现场抓包形态**逐一吻合**（E8）。
- `_jsonWriter` 为 `private [ThreadStatic]`，外部不可达；转义所有权在 **writer 自身**，不在序列化 options。

## E4 错误响应路径缝 — Verified

v2.2.0 `src/ModelContextProtocol.Core/Server/StreamableHttpHandler.cs`：

- L46-47：`s_messageTypeInfo` / `s_errorTypeInfo` 均取自 `McpJsonUtilities.DefaultOptions`（已冻结）。
- L644（及 L1427）：`Results.Json(jsonRpcError, s_errorTypeInfo, statusCode: statusCode)` —— 错误体经**序列化器自建 writer**，理论可投影 `typeInfo.Options.Encoder`；但该 options 即 DefaultOptions（E2 已证冻结）⇒ **错误路径同样无缝**。
- L647-650：`InitializeSseResponse` 设 `text/event-stream` 响应头（SSE 成功路径载体）。

## E5 传输层配置面全枚举 — Verified

v2.2.0 `HttpServerTransportOptions`（快照 `httptransportopts-2.2.0.cs`，15413B，全属性枚举）：`ConfigureSessionOptions`、`RunSessionHandler`、`SessionMode`、`EnableLegacySse`、`EventStreamStore`、`SessionMigrationHandler`、`PerSessionExecutionContext`、`IdleTimeout`、`MaxIdleSessionCount`、`TimeProvider`。**无任何序列化器 / Encoder / JsonSerializerOptions 配置点**。`StreamableHttpPostTransport`（`posttransport-2.2.0.cs` L212）与 SSE 写链（`ssetransport-2.2.0.cs` L64/L106 `_sseWriter.WriteAsync(SseItem…)`）最终都汇入 E3 同一 writer。

## E6 最新版 v2.4.0 同形态核验 — Verified

官方源码 v2.4.0（latest tag）逐文件比对：

- `McpJsonUtilities.cs`：`DefaultOptions { get; }` 仍 get-only + `MakeReadOnly()`；新增第二个同样冻结的 `ProtocolOptions { get; }`（`propertyNameCaseInsensitive: false`）。**两处 options 均冻结**。
- `McpSseEventWriterExtensions.cs`：与 v2.2.0 **逐字同逻辑**（L48 `new Utf8JsonWriter(writer)` 无 options，L55 同 JsonContext 序列化）。
- `StreamableHttpHandler`：L775 `GetRequiredJsonTypeInfo<T>()` 改从 `ProtocolOptions` 取，错误路径同形（L687/L1479）。
- `HttpServerTransportOptions`：配置面与 v2.2.0 一致（快照 15413B 同形），仍无序列化配置点。

⇒ **Inferred（高置信）**：v2.4.0 已是最新发行版且无缝；修复不会在已发行版本中「又消失」⇒ 全部已发行版本（含 v2.3.x）均无此缝。

## E7 STJ 语义锚（排除「序列化器可改写已构 writer」想象）— Verified（.NET 10 源码语义）

`Utf8JsonWriter` 的 `JsonWriterOptions`（含 Encoder）**构造期一次性固定**（`Options` 为 get-only）；`JsonSerializer.Serialize(writer, value, typeInfo)` 对调用方传入的既有 writer **不能**替换其 Encoder（SDK 在生产正常工作 ⇒ 也不存在 options 不匹配抛错路径）。⇒ E3 的 writer 形态即最终转义行为，**不存在「通过传不同 typeInfo 改写转义」的旁路**。

## E8 现场证据对照 — Verified（引用输入10 抓包）

- 外层（SDK 序列化）：`\uFF08\u5F19\u5F49…` 形态、`"` → `\u0022` —— `JavaScriptEncoder.Default` 特征形态。
- 内层（工具信封，`ForgeJson.Default` = UnsafeRelaxedJsonEscaping）：中文原样 `# TODO（当前状态）`。
- 同一响应两形态并存 ⇒ 归因于两层序列化器 Encoder 差异，非数据/传输损坏。
- 原始证据：`.temp/fs-call-response.json`（2026-11-09 17:49 归档）。

## E9 DeepWiki 交叉验证 — Verified（问答）/ Unknown（上游立场）

- 问 `modelcontextprotocol/csharp-sdk`：答复确认 `DefaultOptions` 不可设置、SSE writer 无公开缝、索引上下文中**无维护者承认此问题/承诺修复**的记录（检索 URL：deepwiki.com/search/in-modelcontextprotocol-csharp-sdk_ecbfe30e…）。
- **Unknown**：上游 issue tracker 实际是否有同类 issue / 路线图计划（未逐一检索 GitHub Issues）。

## 结论

SDK 2.2.0→v2.4.0（最新）**均无外层 JSON 转义配置缝**：主逃逸面（SSE 成功路径）由硬编码 `new Utf8JsonWriter(writer)` 决定，两处 JsonSerializerOptions 均启动冻结，传输配置面无序列化项，STJ 语义排除旁路。触发任务预设停止条件：**不 fork、停止、呈报选项**（选项与推荐见 06-review）。

## 探针材料说明

`.temp/tmp/` 下 SDK 源码快照（`ssewriterext-2.2.0.cs` / `ssewriterext-2.4.0.cs` / `httptransportopts-2.2.0.cs` / `httptransportopts-2.4.0.cs` / `streamablehttphandler-2.2.0.cs` / `httphandler-2.4.0.cs` / `ssetransport-2.2.0.cs` / `posttransport-2.2.0.cs` / `mcpjsonutilities-2.4.0.cs`）为**探索期一次性取证材料**，不进版本控制；本文件已内联决定性源码行，结论不依赖快照文件（原始 URL = `https://raw.githubusercontent.com/modelcontextprotocol/csharp-sdk/<tag>/src/ModelContextProtocol.Core/…`，树索引 = `…/git/trees/<tag>?recursive=1`）。
