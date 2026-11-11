# 02 Spec — 2026-11-09-json-facade-unify

## Functional Requirements
- FR-1 门面 v2（`ForgeSelf.Abstractions/ForgeJson.cs` 扩展，保留既有 `Default`/`Indented`，92 文件在用不破坏）：
  - `Encoder`：共享 `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` 实例（AppBuilder 等处复用）。
  - `CreateOptions(Action<JsonSerializerOptions>? configure = null)`：以 `Default` 为基克隆并应用 configure；`Encoder` 强制 = `ForgeJson.Encoder`（防新 options 漏配编码器）。
  - `Serialize<T>(T value, JsonSerializerOptions? options = null)`（null → Default）、`SerializeIndented<T>(T value)`。
  - `Deserialize<T>(string json, JsonSerializerOptions? options = null)`、`Deserialize<T>(byte[] utf8, …)`、`Deserialize<T>(ref Utf8JsonReader reader, …)`、`Deserialize<T>(JsonElement element, …)`（均 null → Default）。
  - `Parse(string json)`、`Parse(string json, JsonDocumentOptions options)`。
  - `WriterOptions`（Encoder=Relaxed）、`WriterOptionsIndented`（再 +Indented）。
- FR-2 全部 `JsonSerializer.Serialize*` → `ForgeJson.Serialize*` / `SerializeIndented`。
- FR-3 全部 `JsonSerializer.Deserialize*` 与扩展形 `element.Deserialize<T>` → `ForgeJson.Deserialize*`。
- FR-4 全部 `JsonDocument.Parse` → `ForgeJson.Parse`。
- FR-5 全部 options 初始化器 → `ForgeJson.CreateOptions(...)` 或门面既有实例；热路径提 `static readonly` 字段。
- FR-6 `new Utf8JsonWriter` 的 options 实参 → `ForgeJson.WriterOptions` / `WriterOptionsIndented`。
- FR-7 `AppBuilder.cs:161` → `o.JsonSerializerOptions.Encoder = ForgeJson.Encoder`。
- FR-8 守卫 v2（`JsonNonAsciiEscapingGuardTests` 扩展）：生产代码（除 ForgeJson.cs）禁 `JsonSerializer\.`、禁 `new (JsonSerializerOptions|JsonWriterOptions|JsonDocumentOptions)`（含 target-typed `new()`）、禁 `JsonDocument\.Parse`、禁无参 `.ToJsonString()`、禁 `new Utf8JsonWriter(` 未带门面 WriterOptions；阳性对照 = `ForgeJson\.(Serialize|Deserialize|Parse)` 计数 > 600 且命中文件数 > 150。

## Input
无新增外部输入（内部重构，无接口/契约/存储变化）。

## Output
- `ForgeJson.cs` v2；155 个生产 .cs 调用点改写；守卫 v2 + G1 新增单测。
- 运行时行为：与改写前**字节级等价**（见 Compatibility）。

## Business Rules
- 读侧零漂移：STJ 反序列化忽略 `Encoder` 与命名策略 ⇒ `Deserialize(json, ForgeJson.Default)` ≡ 裸调用（输入8 已实证 STJ 无全局默认可改，等价性有据）。
- 写侧零漂移：批1 后所有写出口已带 Relaxed Encoder；`Default` 无命名策略、不缩进 = 原 inline `{Encoder=…}` 或无 options 形态 ⇒ 输出一致。**例外面**：各 inline options 的附加配置项（CaseInsensitive / SnakeCaseLower / WhenWritingNull 等）必须逐点原样搬入 `CreateOptions` configure，禁止丢失（03 普查表逐点核对）。
- 既有静态 options 字段（如 `PermissionProfileService.RuleJsonOptions`）改 `ForgeJson.CreateOptions(...)` 初始化，字段类型与用法不变。

## Boundary Conditions
- `Tests/**`（含守卫测试自身）不在扫查面；字符串字面量豁免（StripStrings 沿批1 规矩）；插值孔盲区由 AC2 全量 grep 人工复核兜底。
- `JsonNode.Parse`（21 处）不在本批：其唯一选项 `JsonNodeOptions` 仅属性名大小写一个旋钮，无编码器/深度语义，无可统一项；守卫不拦，将来有需要再入批。
- `ForgeSelf.Web` / e2e / 发布链不受影响；STJ 序列化器内部协议用法（custom converter 内 ref-reader）经门面包装后行为不变。

## Error Handling
- 守卫违例输出 `相对路径:行号 → 违规类型` 明细；扫描命中 0 = 正则失效，阳性对照强制失败（沿 ToolSchemaJsonGuardTests 规矩）。

## Compatibility
- 对外协议（Anthropic/OpenAI/MCP JSON-RPC、plugin.json 清单读取、落盘配置、DB 持久化列）输出/解析结果与改写前等价；存量数据无需迁移。

## Non-functional Requirements
- 不新增每调用分配：`CreateOptions` 仅限初始化期/冷路径；热路径（如 Anthropic SSE WriteSseAsync）提 `static readonly`。门面为一层 static 直通，性能与现状持平。

## Failure Modes（PC lens）
- PC-027 lens：本批不改转义行为，仅入口收敛。最大回归面 = 改写时漏搬 inline 配置项（如丢 `PropertyNameCaseInsensitive` 会改变反序列化容错）⇒ 03 普查表逐点核对 + AC4 全量测试兜底。
- PC-023（双 JSON/提取面）：不触碰，已复核不适用。

## Interaction Design
**无界面交互**：本批无 UI/端点/交互入口/状态模型变化；外部可见输出字节级等价。

## Acceptance Criteria
- AC1 G1 单测：`CreateOptions` 继承 Default 且强制 Encoder；`Deserialize`（Default）对 `\uXXXX` 与原文输入等价实证；`Parse` 包装可用；`WriterOptions`/`WriterOptionsIndented` 含 Relaxed Encoder；`Serialize` null-options ≡ Default。
- AC2 守卫 v2 RED→GREEN：改写前 RED 输出全量命中明细（预计 ~800 处），改写后 GREEN 且阳性对照（ForgeJson.Serialize/Deserialize/Parse > 600 处、文件 > 150）通过。
- AC3 `dotnet build`（ForgeSelf.Api，项目图含全部插件）0 error。
- AC4 全量 `dotnet test` 净新增红 = 0（对表既有基线红清单）。
- AC5 AC1–AC4 全部留 Verified 日志于 05。
- AC6 `pwsh scripts/verify-pilot-artifacts.ps1`（00-07 八件 + 关键节）PASS。
