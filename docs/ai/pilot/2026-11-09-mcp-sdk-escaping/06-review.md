# 06-review：MCP C# SDK 外层转义缝调研

## 八问

**1. 需求是什么？完成到什么程度？**
用户 2026-11-09 18:30 直接指令（= 闸门1）：修 SDK 外层转义使 MCP 网关响应中文原样。**结果**：任务预设「先验证缝、无缝即停止」的条件被实证触发——SDK 无配置缝，修复在不 fork NuGet 的前提下不可达。按预设停止并呈报选项；**修复判定 ⏸️ BLOCKED（待 Lead/用户拍板）**。

**2. 变更了什么？**
零业务代码变更（`git status` 可证：仅新增/修改文档）。工件 10 处：本 pilot 3 件（mini-task / 05-evidence / 06-review）、[PC-027](../../../11-troubleshooting/problem-classes/PC-027-json-nonascii-default-escaping.md) 补「外层 SDK 缝调研」节 + lens 第 4 问、新 case `2026-11-09-mcp-sdk-outer-escaping`、三索引点（problem-classes/cases README、index.md 速查行）、TODO ⑤ 与日记状态。

**3. 如何验证的？**
05-evidence 证据链全 Verified 级：DLL 元数据探针（get-only、无 setter）＋官方源码逐文件行号引用（v2.2.0 在用版 + v2.4.0 最新版）＋STJ writer 语义锚＋现场抓包两形态对照＋DeepWiki 交叉问答。无代码变更 ⇒ 构建/测试门禁不触发（基线零漂移）。

**4. 有没有绕过测试/守卫？**
无。未写任何生产代码；G1-G4 守卫与冻结区（`ForgeJson.cs`、`JsonNonAsciiEscapingGuardTests.cs`）零触碰。TDD RED→GREEN 不适用已论证（mini-task ④：修复面在第三方依赖内部且无注入缝，仓内无可承载断言的代码）。

**5. 同类扫查做了吗？（PC-027 外层面全清单）**

| 面 | 序列化载体 | 结论 |
|----|-----------|------|
| SSE 成功路径（`tools/call`、`tools/list` 等全部主响应） | `McpSseEventWriterExtensions` 硬编码 writer（E3） | ✦ 主逃逸面，无缝 |
| 错误响应路径（HTTP 状态码 + error JSON） | `Results.Json(…, s_errorTypeInfo)` ← DefaultOptions（E4） | 同冻结 options，无缝 |
| Streamable HTTP POST 响应流 | 汇入同一 SSE writer 链（E5） | 同缝 |
| `/health`（McpGatewayServer.cs L217-223） | 原始字符串 WriteAsync，无 STJ | 无关面 |
| 网关日志/落盘 | 宿主自身序列化，已在 G2 覆盖内 | 无关面 |

**6. 范围控制与被否方案？**
- 未改业务文件；探针快照仅存 `.temp/tmp/`（不入库，结论可凭 URL 复得，05 已内联决定性行）。
- **被否① fork/patch NuGet 包**：用户预设红线（任何结局下禁止）。
- **被否② 反射 hack 置换 DefaultOptions**：仅可达错误路径（E4/E7：成功路径 writer 绕开 options），且 AOT/trimming 脆性——不彻底，否。
- **被否③ 仅升级 SDK**：v2.4.0 最新已证同形无缝（E6），否。

**7. 遗留风险？**
- `\uXXXX`/`\u0022` 为 JSON 规范合法、语义无损——任何合规解析器还原原样中文；实际影响仅「直接读线路原文」的可观测性（与 PC-027 §1 定性一致）。故 O1（接受+文档化）在语义上是安全的。
- 若未来 SDK 引入缝或网关改用其他传输（stdio 服务端等），需重查本面——已入 PC-027 lens 第 4 问。

**8. 教训沉淀？**
第三方依赖自带 JSON 序列化面 = G2 源码扫描的结构性盲区（仓内源码字面量之外）。机制上浮：PC-027 lens 增第 4 问「引入/升级第三方库自带 JSON 面（如 MCP C# SDK）时，是否核查其传输层转义行为与可配置缝？」＋ index.md 速查行。若 Lead 拍板 O3（宿主侧响应改写中间件），另立 mini-task 并配行为守卫（G1）。

## Final Decision

**APPROVED（调查与归档闭环）**；修复项 **⏸️ BLOCKED**（外部依赖：上游无缝，处置待拍板）。选项摘要（完整分析在 case §5）：

| 选项 | 内容 | 代价 | 建议 |
|------|------|------|------|
| **O1 接受+文档化** | 语义无损（解析器还原），残留仅原文可读性；PC-027 已归档结论 | 零代码 | ✅ 默认立即可做（本批已完成文档化） |
| **O2 上游 issue/PR** | 附精确指针（`McpSseEventWriterExtensions.FormatJsonRpcMessage` L46-55：writer 构造接入 Encoder 缝）请求上游加配置点 | 时间线不受控 | ✅ 与 O1 并行，推荐 |
| **O3 宿主侧响应改写中间件** | 仅 `/mcp` 路由：缓冲 POST 响应体、按 `data:` 行解析重序列化（UnsafeRelaxed）；牺牲流式、双倍内存、需独立 mini-task + 行为守卫 | 中高，真子系统 | ⏸ 若用户近期就要线路中文可读再立项 |
| ~~反射 hack~~ / ~~升级 SDK~~ | E4/E6 证据否 | — | ❌ |

**待拍板（≤2 问）**：① O2 上游 issue 是否现在发（需 GitHub 账号与措辞审定）？② O3 是否立项（默认：暂不，先 O1+O2）。
