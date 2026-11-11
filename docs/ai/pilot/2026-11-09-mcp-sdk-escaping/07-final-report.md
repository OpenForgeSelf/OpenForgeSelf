# AI-Native Pilot Result（最终汇报）— MCP C# SDK 外层转义缝调研

## 1. Repository Understanding

- 在用 SDK：`Plugins/McpCenter/McpCenter.csproj:38` → `ModelContextProtocol.AspNetCore 2.2.0`；网关装配在 `ForgeSelf.Api/Plugins/McpCenter/McpGatewayServer.cs`（`MapMcp("/mcp")`）。
- 外层转义行为归属 ModelContextProtocol C# SDK 自带序列化面（G2 源码扫描的结构性盲区），非宿主/守卫逃逸。
- PC-027 守卫（`ForgeJson.cs` + `JsonNonAsciiEscapingGuardTests.cs`）已封死仓内序列化字面量面；SDK 内部 writer 形态不受其覆盖。

## 2. Selected Task

用户 2026-11-09 18:30 直接指令（输入11，即闸门1）：修 SDK 外层转义，使 MCP 网关响应中文原样。mini-task 预设停止条件：**验证缝 → 无缝即停止（不 fork、零业务代码变更）、呈报选项**。

## 3. Changed Files

零业务代码变更（`git status` 可证）。文档/归档工件 10 处：本 pilot `mini-task.md` / `05-evidence.md` / `06-review.md` / 本件；`docs/11-troubleshooting/problem-classes/PC-027-json-nonascii-default-escaping.md`（§6 外层调研节 + lens 第 4 问 + 复发数 0→1）；新 case `docs/11-troubleshooting/cases/2026-11-09-mcp-sdk-outer-escaping.md`；`cases/README.md`、`problem-classes/README.md`、`index.md` 三索引；TODO.md ⑤ 转 ⏸；日记 `.forgeself/memory/2026-11-09.md`。

## 4. Validation

- Build: 不触发（零代码变更；基线零漂移）。
- Unit Test: 不触发（同上）。
- E2E: 不触发（同上）。现场现象证据沿用 `.temp/fs-call-response.json`（2026-11-09 17:49 抓包，Verified 引用）。

## 5. Evidence

`05-evidence.md` E1-E9 全链 Verified：① DLL 反射探针证 `McpJsonUtilities.DefaultOptions` get-only+`MakeReadOnly` 启动冻结；② 官方源码行号级比对（v2.2.0 在用版 + v2.4.0 最新版）证主逃逸面 = `McpSseEventWriterExtensions.FormatJsonRpcMessage` L46-55 硬编码 `new Utf8JsonWriter(writer)`（无 `JsonWriterOptions` → Encoder=Default）；③ STJ 语义锚（`Utf8JsonWriter.Options` 构造期固定）排除「传 typeInfo 改写转义」旁路；④ 传输配置面全枚举无序列化项；⑤ 抓包内外两形态对照归因成立；⑥ DeepWiki 交叉验证。**结论：SDK 2.2.0→v2.4.0 均无外层转义配置缝，不 fork 不可修。**

## 6. Review

`06-review.md` Final Decision = **APPROVED（调查与归档闭环）**；修复项 **⏸ BLOCKED**（外部依赖无缝，处置待拍板）。选项：O1 接受+文档化（✅ 已完成，语义无损）/ O2 上游 issue（✅ 推荐并行）/ O3 宿主侧响应改写中间件（⏸ 暂不，需再立项+守卫）；反射 hack、仅升级 SDK 已被证据否。

## 7. Risk

L2（低）：`\uXXXX`/`\u0022` 为 JSON 合法转义、解析器语义无损，残留仅「直接读线路原文」可读性；未知项 = 上游 issue tracker 实况（05-E9 标 Unknown，O2 发起时补查）。

## 8. Problems Found

- PC-027 暴露新面（第三方依赖自带 JSON 序列化面），已归档 + lens 扩问，非新 PC。
- 修复本身 ⏸ BLOCKED 待拍板（O2/O3 归属用户决策）。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PASS（零代码为论证结论，非跳步） |
| Code → Test | PASS（不适用已论证） |
| Test → Evidence | PASS |
| Evidence → Review | PASS |

### 9.5 本批自复盘（必填 · 2026-10-31 用户复盘要求立）

- **经验提取**：① 「预设停止条件」先行（mini-task ④）避免了为交差而 fork/反射 hack 的错误路径；② DLL 反射探针 + 上游源码行号级比对 + STJ 语义锚三件套，一次会话内把「缝在不在」钉死到可拍板粒度；⑥ 深浅两层证据并存（v2.2.0 在用 + v2.4.0 最新）排除「升级即好」侥幸。
- **待改进**：调研类任务无 E2E/构建门禁可跑时，Evidence 章节应显式写「不触发 + 理由」（本批已按此执行，后续延续）；未逐条检索上游 GitHub Issues（标 Unknown 而非脑补）。

## 10. 问题类归档

归入 **PC-027（json-nonascii-default-escaping，本批为其外层面复发，复发数 0→1）**；case 文件：`docs/11-troubleshooting/cases/2026-11-09-mcp-sdk-outer-escaping.md`；机制上浮：PC-027 lens 新增第 4 问（第三方库自带 JSON 面核查）+ `index.md` 速查行。

## 11. 重构提案

**R0（不重构）+ 理由**：无仓内代码可重构（零业务代码变更）。潜在重构为 O3 宿主侧 `/mcp` 响应改写中间件——跨模块、牺牲流式与双倍内存，按 problem-class-closure 属 R2 档，**须独立 mini-task + 行为守卫 + 用户立项拍板**，本批不预启动（06-review §8/选项表）。

## 12. 悬空教训清单（必须为空）

- O2（上游 issue 是否发）与 O3（中间件是否立项）为**待用户拍板项**，非悬空教训：已分别登记于 `06-review.md` §8/选项表、TODO.md ⑤（⏸ 阻塞态）、日记「下一步」——逐字可检索。
- 本清单为空：无未落成项。
