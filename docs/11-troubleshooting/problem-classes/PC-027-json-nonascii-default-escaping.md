---
id: PC-027
title: JSON 写出路径未声明非 ASCII Encoder，System.Text.Json 默认把中文转义成 \uXXXX
tags: [json,serialization,encoding,non-ascii,chinese]
invariant: 所有生产代码 JSON 写出路径（JsonSerializer.Serialize / ToJsonString / WriteAsJsonAsync / JsonWriterOptions / MVC AddJsonOptions）必须显式声明 Encoder=UnsafeRelaxedJsonEscaping 或复用 ForgeJson.Default/Indented；中文落盘/出网后必须是原文，不得是 \uXXXX 转义序列。
status: guarded
first_seen: 2026-11-07
recurrences: []
cases: [2026-11-07-json-chinese-raw-encoder, 2026-11-09-mcp-sdk-outer-escaping]
signature:
  - {id: S1, kind: test, path: ForgeSelf.Api.Tests/Architecture/JsonNonAsciiEscapingGuardTests.cs}
allowlist: []
guards:
  - {level: G2, path: ForgeSelf.Api.Tests/Architecture/JsonNonAsciiEscapingGuardTests.cs, status: active}
lens:
  - 新增/改动任何 JSON 写出调用（Serialize/ToJsonString/WriteAsJsonAsync/Utf8JsonWriter）后，是否跑了 JsonNonAsciiEscapingGuardTests（G2，构建绿 ≠ 中文不被转义）？
  - 新增 JsonSerializerOptions/JsonWriterOptions 初始化器（含 target-typed new()）是否声明了 Encoder？只读（Deserialize）初始化器也必须声明——守卫无法静态区分读/写，Encoder 反序列化时被忽略，零读侧风险。
  - 代码在 `$$$"""` 原始字符串模板内生成（脚手架类）时，模板里的 Serialize 是否也用了 ForgeJson？（源码扫描 Strip 掉字符串字面量，模板内违例守卫不可见——已知盲区，见 §2。）
  - 引入/升级**第三方库自带 JSON 序列化面**（如 MCP C# SDK 的 JSON-RPC/SSE 传输层）时，是否核查过其传输层转义行为与可配置缝？（G2 只扫仓内源码字面量，第三方依赖内部结构性不可达——实证见 §6；2026-11-09 立。）
---
# PC-027 JSON 写出路径默认 Encoder 把中文转义成 \uXXXX

> 事件经过见 `../cases/2026-11-07-json-chinese-raw-encoder.md`。

## 1 不变量与诱因

**不变量**：中文内容（工具提示、错误消息、记忆正文、配置值……）经任何 JSON 序列化面（HTTP 响应、SSE 流、文件落盘、DB JSON 列、插件间载荷）后必须保持原文。`System.Text.Json` 默认 Encoder（JavaScriptEncoder.Default）把所有非 ASCII 转成 `\uXXXX`：内容不丢（反序列化可还原），但**所有直接读 JSON 的人**（用户看落盘文件/DB、grep 排障、其他语言消费者、日志）看到的是 `\u76EE\u5F55\u4E0A\u6EAF` 这种不可读形态。

**诱因**：① STJ 默认行为是转义非 ASCII，且**没有全局开关**（没有 AppContext 开关、没有程序集 attribute）——每一处 `Serialize` / 每一个 options 初始化器都要各自声明 Encoder，漏一处即违例；② options 初始化器形态多样（`new JsonSerializerOptions{...}`、target-typed `new()`、共享静态字段、调用点内联、`JsonWriterOptions` 结构体属性赋值），扫查极易漏；③ 只读（Deserialize）初始化器与写路径共用同一语法形态，人眼扫查无法区分，容易误判「这里不写出 JSON 不用管」。

## 2 特征签名与扫描方法

- **S1（test，G2）**：`JsonNonAsciiEscapingGuardTests`——源码级枚举宿主 + 全部插件的生产 .cs（排除 Tests/obj/bin/web/dist 等），注释/字符串剥离后扫描三类违例：单参 `JsonSerializer.Serialize(...)`、无参 `.ToJsonString()`、不含 `Encoder` 的 `JsonSerializerOptions/JsonWriterOptions` 初始化器（含 target-typed `new()`）。阳性对照：要求扫到 >50 个 Serialize 调用（守卫环境自证非空转）；另有对照用例证明「无选项默认行为确实产生 `\uXXXX`」。
- **G1（行为锁）**：同文件内 4 个用例锁 ForgeJson 行为——`Default` 输出含「目录上溯」原文且不含 `\u`；`Indented` 带缩进且同样保原文；命名策略保持 null（不改属性名）；对照组证明默认 Encoder 仍转义。
- **已知盲区**：① 原始字符串模板（`$$$"""`）内生成的代码（脚手架类，如 PluginScaffolderService）被 Strip 剔除，守卫不可见——本批已把仓内模板手工改为 ForgeJson 并登记 lens 第 3 问；② 字符串内插值洞 `{expression}` 中的表达式不做扫描（Strip 保长占位，无法安全展开）；③ `SerializeToUtf8Bytes/SerializeToElement/JsonContent.Create` 等冷门重载未纳入扫描形态（仓内 grep 确认 0 命中，出现时扩展守卫）。

## 3 Canonical fix

- **共享选项**：`ForgeSelf.Abstractions/ForgeJson.cs` 提供 `ForgeJson.Default`（仅 `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping`，零命名/缩进漂移）与 `ForgeJson.Indented`（Default + `WriteIndented = true`，落盘人工审阅面用）。
- **改法映射**：单参 `Serialize(x)` → `Serialize(x, ForgeJson.Default)`；`new JsonSerializerOptions { WriteIndented = true }` → `ForgeJson.Indented`；带其他属性（命名策略/转换器）的初始化器 → 保留原属性 + 补 `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,` 行；无参 `.ToJsonString()` → `.ToJsonString(ForgeJson.Default)`；MVC 管道 → `AddJsonOptions(o => o.JsonSerializerOptions.Encoder = ...)`。
- **统一不变量（本批裁定）**：只读（Deserialize-only）初始化器同样补 Encoder——守卫无法静态区分读/写路径，保持零 allowlist；Encoder 在反序列化时被忽略，零读侧风险。
- **被否方案**：① 「STJ 全局默认改掉」——不存在该机制，AppContext/反射黑科技脆且非公开契约；② 序列化后 Regex 反转义——破坏字符串内合法 `\u` 字面量与控制字符语义；③ 只改落盘不改 HTTP 面——用户可见中文同样出现在接口响应/DB 行里，必须同类同修。

## 4 守卫台账

| 等级 | 载体 | 何时触发 | 覆盖盲区 |
|------|------|----------|----------|
| G2 | `ForgeSelf.Api.Tests/Architecture/JsonNonAsciiEscapingGuardTests.cs` | `dotnet test --filter JsonNonAsciiEscapingGuardTests`（碰宿主源码=中档全量必然带上；插件批快档） | 原始字符串模板内生成代码；插值洞表达式；冷门序列化重载（见 §2） |
| G1 | 同文件 4 行为用例 | 随 G2 同跑 | 锁 ForgeJson 自身行为，防共享选项被人改坏 |

## 5 案例列表

| case | 日期 | 现象一句话 | 是否逃逸（守卫已在但没拦住） |
|------|------|------------|------------------------------|
| 2026-11-07-json-chinese-raw-encoder | 2026-11-07 | 用户点名「目录上溯」被序列化成 `\u76EE\u5F55\u4E0A\u6EAF`，要求全项目 JSON 中文保持原文 | 否（首见，此前无守卫；RED 实证 488 处违例） |
| 2026-11-09-mcp-sdk-outer-escaping | 2026-11-09 | MCP 网关 JSON-RPC/SSE 外层（第三方 SDK 2.2.0 传输层）中文仍转义 `\uFF08`——调研实证 SDK 无配置缝，记录不修呈选项 | 否（新暴露面非逃逸：G2 设计范围=仓内源码，第三方内部不可达，见 §6） |

## 6 外层第三方 SDK 缝调研（2026-11-09，无缝结论）

**调研结论（全 Verified，证据链 `docs/ai/pilot/2026-11-09-mcp-sdk-escaping/05-evidence.md`）**：ModelContextProtocol C# SDK **2.2.0（在用）→ v2.4.0（最新）均无外层转义配置点**——

1. **主逃逸面 = SSE 成功路径**：`McpSseEventWriterExtensions.FormatJsonRpcMessage`（v2.2.0 L39-56）`new Utf8JsonWriter(writer)` **不带 JsonWriterOptions** → `Encoder=null` → `JavaScriptEncoder.Default`（`\uFF08`/`\u0022` 与抓包形态逐一吻合）；writer 为 `private [ThreadStatic]` 不可达；转义所有权在 writer 自身。
2. **options 全冻结**：`McpJsonUtilities.DefaultOptions`（v2.2.0，get-only + `MakeReadOnly()`，DLL 探针无 set）与 v2.4.0 新增 `ProtocolOptions` 同冻结。
3. **错误路径同缝**：`Results.Json(…, s_errorTypeInfo)` 的 typeInfo 取自冻结 DefaultOptions（v2.2.0 StreamableHttpHandler L46-47/L644）。
4. **传输配置面无序列化项**：`HttpServerTransportOptions` 全属性枚举（v2.2.0=v2.4.0）无任何 Encoder/serializer 配置点。
5. **STJ 语义锚**：`Utf8JsonWriter` 的 `JsonWriterOptions` 构造期固定，`JsonSerializer.Serialize(writer,…)` 不能改写已构 writer 的 Encoder——不存在「传不同 typeInfo 改转义」旁路。
6. **DeepWiki 交叉验证**：无公开缝、DefaultOptions 不可设；上游立场 Unknown（GitHub Issues 未逐一检索）。

**语义影响**：`\uXXXX`/`\u0022` 为 JSON 规范合法转义、语义无损（任何合规解析器还原原样中文）；残留仅「直接读线路原文」可读性。

**处置（呈报待拍板，勿 fork）**：O1 接受+文档化（本节即产物，✅ 默认）；O2 上游 issue/PR（推荐并行，指针=`FormatJsonRpcMessage` L46-55 请求 writer 构造接入 Encoder 缝）；O3 宿主侧 `/mcp` 响应改写中间件（可备选：缓冲 POST 体按 `data:` 行重序列化，牺牲流式/双倍内存，需独立 mini-task+行为守卫）。**被否**：反射 hack（仅达错误路径 + AOT 脆性）、仅升级 SDK（v2.4.0 已证同形无缝）、fork NuGet 包（用户红线）。同类扫查与逃逸分析 → case `2026-11-09-mcp-sdk-outer-escaping` §4/§6。
