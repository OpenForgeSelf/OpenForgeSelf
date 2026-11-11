# 02 Spec — 2026-11-07-json-chinese-raw-encoder

## Functional Requirements
- FR-1 新共享选项 `ForgeSelf.Abstractions.ForgeJson`：`Default`（=「无 options」默认行为 + `Encoder=UnsafeRelaxedJsonEscaping`）、`Indented`（再加 `WriteIndented=true`）。不设命名策略（保持既有 PascalCase 输出不变）。
- FR-2 生产代码全部 `JsonSerializer.Serialize*` 写出调用与 `ToJsonString()` 无参调用改走 `ForgeJson.Default`（或既有局部 options 补 `Encoder`）。
- FR-3 全部 `new JsonSerializerOptions { … }` / `new JsonWriterOptions { … }` 初始化器（含 target-typed `new()`）必须含 `Encoder`。
- FR-4 MVC：`AppBuilder.cs` `AddControllers()` 追加 `AddJsonOptions` 设同一 Encoder。
- FR-5 落盘缩进维持现状：原 `WriteIndented=true` 的落点改用 `ForgeJson.Indented`；原无缩进的落点不加缩进。

## Input
无新增外部输入；仅序列化配置变化。

## Output
- JSON 文本：非 ASCII 字符以 UTF-8 原文出现；结构/字段名/数值格式不变。
- G2 守卫测试 `JsonNonAsciiEscapingGuardTests`（源码枚举宿主+Plugins+Core+Abstractions）。

## Business Rules
- 只改写出路径；`Deserialize` 相关 options 仅当同时被写路径使用时补 `Encoder` 行，否则不动。
- 测试代码不在 G2 扫查面（守卫只枚举生产代码）；本批新增守卫测试自身同理。

## Boundary Conditions
- 含 `<`、`>`、`&`、`+` 的字符串同样不再被 HTML 安全转义（UnsafeRelaxedJsonEscaping 语义）；HTTP `application/json` 传输面无注入面（MVC 默认响应头非 text/html），风险记录于 Compatibility。
- 注释/字符串字面量中的 `JsonSerializer.Serialize(` 不计入守卫（StripStrings 规则）；插值字符串孔内调用属已知盲区，由 AC2 全量 grep 人工复核兜底。

## Error Handling
- 守卫违例输出 `相对路径:行号 → 违规类型` 明细；扫到 0 个 Serialize 命中 = 正则失效，阳性对照强制失败（照 ToolSchemaJsonGuardTests 规矩）。

## Compatibility
- JSON 语义等价：STJ/Newtonsoft/浏览器 JSON.parse 对 `\uXXXX` 与原文解析结果一致 ⇒ 存量 DB 列、配置文件、上游协议消费者全部兼容可读。
- 对外 API 响应转义形态变化：纯文本呈现层面，无结构变化。

## Non-functional Requirements
- 载荷体积：中文 3 字节 UTF-8 vs 6 字节 `\uXXXX`，变小；性能影响可忽略（encoder 分支更少）。

## Failure Modes（PC lens）
- PC-023（双编码 JSON/提取面）：本批不改嵌套结构与转义层数，提取正则不敏感于 `\u` vs 原文（两类均在字符串字面量内）⇒ 不适用，已复核。
- PC-027（本批新立）：默认转义非 ASCII——本任务以 G2 源码枚举守卫（结构性问题首次即 G2）+ G1 单测落地，满足「新代码再犯即红」。

## Interaction Design
**无界面交互**：本批无 UI 变化。外部可见面 = 文本内容（工具结果显示、MCP 客户端输出、落盘配置、API 响应体）中文由 `\uXXXX` 变原文；无新端点、无交互入口变化、无状态模型变化。

## Acceptance Criteria
- AC1 G1 单测：`ForgeJson.Default` 序列化含「目录上溯」对象输出中文原文且不含 `\u`；`Indented` 同且带缩进。
- AC2 G2 守卫：修复前 RED（输出命中明细），修复后 GREEN（生产代码零单参 Serialize、零缺 Encoder 的 options 初始化器），阳性对照（扫描计数下限）通过。
- AC3 `dotnet build`（ForgeSelf.Api，项目图含 22 插件）0 error。
- AC4 全量 `dotnet test` 新增红 = 0（对表既有基线红清单）。
- AC5 AC1–AC4 全部留 Verified 日志于 05。
- AC6 PC-027 + case 归档、台账三处索引更新、`check-problem-classes.ps1` 绿。
