---
id: 2026-11-07-json-chinese-raw-encoder
title: 全项目 JSON 中文被默认转义成 \uXXXX（用户点名「目录上溯」不可读）
pc: PC-027
severity: P2
evidence: Verified
---
# Case：2026-11-07 全项目 JSON 序列化中文保持原文改造

> Case 只讲「这一次发生了什么」；规则/修法/扫描方法一律引用 PC-027，不复述。

## 1 现象

用户指令（原文）：「将本项目的json格式化，关于中文部分，设置不要序列化成 **\u76EE\u5F55\u4E0A\u6EAF 这种，直接保持中文原文」。`\u76EE\u5F55\u4E0A\u6EAF` 即「目录上溯」——落盘文件、DB JSON 列、接口响应里的中文全是 `\uXXXX` 形态，直接读 JSON 排障完全不可读。

## 2 复现红灯

- **测试层（红灯先行）**：新增守卫 `ForgeSelf.Api.Tests/Architecture/JsonNonAsciiEscapingGuardTests.cs`（5 用例：4 个 G1 行为锁 + 1 个 G2 源码扫描）。首跑 RED：**失败 1（G2）/ 通过 4（G1 全绿）**——G2 报「PC-027 违例 488 处（JSON 写出未用 ForgeJson，中文会被转义成 \uXXXX）」。日志 `.temp/guard-red2.log`（首个 red 日志因编译错误无效，已废弃）。
- **行为对照（守卫环境自证）**：G1 对照用例证明默认 Encoder 输出确实含 `\u76EE\u5F55`，ForgeJson.Default 输出含「目录上溯」原文——证明守卫断言方向正确、非空转。

## 3 根因（5 Whys + 第 6 问）

1. 为什么中文变 `\uXXXX`？→ `System.Text.Json` 默认 Encoder（JavaScriptEncoder.Default）转义所有非 ASCII 字符。
2. 为什么各处都在用默认 Encoder？→ STJ **没有全局默认开关**（无 AppContext/attribute），每个序列化点必须各自声明 Encoder；仓内既有代码全部用无参/最小参数重载。
3. 为什么当初没发现？→ 功能正确性不受影响（反序列化可还原），只有「直接读 JSON」的排障/审阅场景暴露；且不同面的落盘分散在 ~150 个调用点，单点看都「没问题」。
4. 为什么扫查/修复容易漏？→ 初始化器形态多样（内联/target-typed `new()`/共享静态字段/`JsonWriterOptions` 结构体/模板字符串内生成代码），且只读（Deserialize）初始化器与写路径语法相同、人眼无法区分。
5. 为什么没有守卫？→ 此前无人把「中文原文」当序列化契约测过；本批按问题类闭环立 G2 源码扫描 + G1 行为锁。
- **第 6 问：为什么现有流程没让我一开始就写对？** 归因：项目没有共享序列化选项（各处手写 options），新增写出点无从「复用正确默认」。本批落 `ForgeSelf.Abstractions/ForgeJson.cs`（Default/Indented）作为唯一正确入口，并在 PC-027 lens 中登记「新增初始化器必含 Encoder」问题。

## 4 同类扫查命中表

| 范围 | 同根因？ | 处置 |
|------|----------|------|
| 宿主 ForgeSelf.Api（Controllers/Services/Plugins 服务/AppBuilder） | 是（本体） | 已修：~35 文件，统一 ForgeJson.Default/Indented 或补 Encoder 行；MVC 管道补 AddJsonOptions |
| ForgeSelf.Core / ForgeSelf.Bootstrapper | 否 | grep 0 命中（无序列化点） |
| 插件 22 个目录（3 个并行子代理分组清扫：组A 8 目录 / 组B 6 目录 / 组C 8 目录） | 是（同根因） | 已修：单参 Serialize 补 ForgeJson.Default、初始化器补 Encoder、`WriteIndented=true` → ForgeJson.Indented；只读初始化器按统一不变量同补 Encoder |
| PluginScaffolderService `$$$"""` 模板内生成的插件代码 | 是（同根因、守卫盲区） | 已手工改模板（生成代码用 ForgeJson.Default）+ PC-027 lens 登记盲区 |
| ForgeSelf.Web / e2e / 前端 | 否 | 前端 JSON.stringify 不转义非 ASCII（JS 默认行为），无需改动 |
| 测试夹具 `ForgeSelf.Api.Tests/Plugins/McpCenterTests/Fixtures/mock-mcp-server.js`（stdio LSP 帧读取） | **伴生暴露**（非同根因：本批「中文原文输出」触发其潜伏缺陷——帧读取按 JS 字符长度切，`Content-Length` 按字节，ASCII 载荷碰巧正确、中文载荷帧永远"不完整"→ 客户端 300s 超时；全量测试 2 红起初误判 flake，空闲复跑仍红才定性） | 已修：改 Buffer 字节级切帧；两个中文载荷 MCP 集成测试（`StdioTransport_EchoAndAdd_RoundTrip` 的 `你好` / `Auto_Framing_LspServer_FallsBackAndHandshakes` 的 `回退帧`）即常驻守卫，任一侧回归字符级切帧即 300s 超时红。修复后 2/2 绿 / 5s（pilot 05「Integration Test」实验 4） |
| DevTools JsonFormatterService/YamlFormatterService（「json 格式化」功能本体） | 形态相关 | JsonWriterOptions 按 G2 规则补 Encoder（格式化器写路径），由组B统一处置 |

## 5 修复与被否方案

修法 = PC-027 canonical fix（共享 ForgeJson + 逐点映射 + 只读初始化器同补 Encoder 的统一不变量）。被否方案见 PC-027 §3（全局默认不存在；Regex 反转义破坏语义；只改落盘不改 HTTP 面不彻底）。

## 6 重构提案与结论

**R0（不重构）**：逐点映射是机械改动，不引入新抽象。唯一结构化新增 = ForgeJson 共享选项类（Abstractions 层，宿主与插件均可引用）。附带：`UnsafeRelaxedJsonEscaping` 名字含「Unsafe」，其风险面是「HTML 敏感字符（`<` `>` `&` 等）不再转义」——本项目所有 JSON 消费方均为 API/文件/DB，无「内嵌 HTML」场景；若未来出现「JSON 直接内嵌进 HTML 模板」的用法，必须另用 `JavaScriptEncoder.Create(UnicodeRanges.All, ...)` 组合，已在 PC-027 §1 约束「消费方为 API/文件/DB」。

## 7 守卫落地

新增 G2 源码扫描守卫 + G1 行为锁 `ForgeSelf.Api.Tests/Architecture/JsonNonAsciiEscapingGuardTests.cs`（RED 488 处 → GREEN 0 处，见 pilot 05-evidence；`.temp/guard-red2.log` → `.temp/guard-green.log`）。首见无逃逸。

## 8 逃逸分析（二次复发必做，首见可写「N/A 首次」）

N/A 首次。

## 9 基建改进

① `ForgeJson` 共享选项入库（新增 JSON 写出点的正确默认入口）；② G2 守卫接入日常门禁（碰宿主源码的中档全量必带；插件批快档可 --filter）；③ TODO 登记：守卫盲区（原始字符串模板/插值洞/冷门重载）如出现真实逃逸再升级实例级扫描；④ 发布后 :51888 运行实例对 2-3 个中文密度高的插件面板做「中文原文显示」抽查（记 TODO，属升级实例动作）。
