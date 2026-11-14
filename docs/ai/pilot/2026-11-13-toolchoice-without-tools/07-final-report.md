# AI-Native Pilot Result（最终汇报）

> 任务结束强制格式｜载体：markdown 正文直发群消息（规范 §5；仅超单消息上限或用户明确要求时用附件，且附正文摘要）
> 状态只报事实，禁止模糊表述（对齐 AGENTS.md §10.4/§10.5）。

## 1. Repository Understanding

我确认了（已验证：源码读 + 既有 pilot 工件）：
- 宿主 `ForgeSelf.Api`（.NET 10 + NewLife.XCode）的 LLM 代理出站链：`LlmProxyPipeline.ExecuteAsync` 为直通/桥接两路的**类级汇合点**（`LlmJson.Write(outboundNode)` 前）；桥接经 `ChatResponsesBridge`（两向协议映射）；M.E.AI 路径经 `LlmProxyChatClient.BuildJson`；插件侧经 `Plugins/AIAgent/MafLoop/OpenAIChatStreamReader.FromUnified`。
- 出站守卫先例：`OpenAISdkAdapter` L226–246/L552–573 已有同族守卫（本次不重复动）。
- 测试体系：xUnit（`ForgeSelf.Api.Tests`），门禁分档按 AGENTS.md §5.6；本批缺陷任务触发「中档全量 + 基线红对表」。

## 2. Selected Task

缺陷修复（mini-task 裁剪档，≤3 源文件规则内实际 4 文件含偏差补充）：openai 响应式接口 400「tool_choice 发出但 tools 未带」（`When using tool_choice, tools must be set.`，param=tool_choice；来源 输入6 用户截图，P1）。任务目录 `docs/ai/pilot/2026-11-13-toolchoice-without-tools/`。

## 3. Changed Files

- `ForgeSelf.Api/Services/AI/Proxy/ChatResponsesBridge.cs` — `MapToolChoiceToResponses`/`MapToolChoiceToChat` 方法入口 tools 计数守卫（两向对称，自包含不动调用点）
- `ForgeSelf.Api/Services/AI/Proxy/LlmProxyChatClient.cs` — BuildJson 写点加 `&& options.Tools is { Count: > 0 }`
- `ForgeSelf.Api/Services/AI/Proxy/LlmProxyPipeline.cs` — `ExecuteAsync` 咽喉点守卫（`LlmJson.Write` 前；覆盖同协议直通路径，即用户 live 400 实际路径）
- `Plugins/AIAgent/MafLoop/OpenAIChatStreamReader.cs` — `FromUnified` 写点加 `&& request.Tools is { Count: > 0 }`
- `ForgeSelf.Api.Tests/Unit/LlmProxyPipelineTests.cs` — 新增 T1–T6（含两条阳性守 G2）
- `ForgeSelf.Api.Tests/Plugins/AIAgent/OpenAIChatJsonTests.cs` — 新建 3 例
- 归档：`docs/11-troubleshooting/problem-classes/PC-033-tool-choice-without-tools.md` + `docs/11-troubleshooting/cases/2026-11-13-toolchoice-without-tools.md`
- 工件：本目录 mini-task.md / 05 / 06 / 07

（代码与归档已入提交 `e50b9b9` / `8eba29e`；本批提交 = 工件补全 + TODO 收口。）

## 4. Validation

Build: ✅ Verified — `dotnet build` 通过（红 run2 前置）。

Unit Test: ✅ Verified —
- 快档过滤集：**18/18 绿（99ms）**（红 6 → 绿 18，日志 `ForgeSelf.Api.Tests/.temp/toolchoice-green-run2.log`）。
- 中档全量 `dotnet test`：**失败 25 / 通过 3750 / 总计 3775**（15m19s，日志 `ForgeSelf.Api.Tests/.temp/toolchoice-fullsuite.log`，job pwsh-e1680ef8728e43fc）；25 红逐族对表 TODO L172/L246/L338/L526/L548 登记的存量基线，**0 新增红**；本批 T1–T6 + OpenAIChatJson 3 例全绿（不在失败清单——全量输出只打印失败用例）。

E2E: 未跑——深档触发条件未满足（未碰 e2e 基建、非发版/tag）；本缺陷守卫面由全量单测覆盖，记为档位边界（非悬空项）。

## 5. Evidence

`05-evidence.md`（本目录）：§1 缺陷证据与 6 处根因写点（含首轮漏判的第 5 处直通路径，逃逸分析见 §3.1/07 §8）；§2 红灯 6/6 断言一致（权威红灯 run2）；§3 修复 5 处守卫落点；§4.1 快档绿 18/18；§4.2 全量对表（25 红逐族命中存量登记）。日志：`ForgeSelf.Api.Tests/.temp/toolchoice-{red-run2,green-run2,fullsuite}.log`。

## 6. Review

`06-review.md` Final Decision：**APPROVED**（八问全过、四 Check 全 PASS、Risk L1、Findings 无 Critical/Major、Minor 2 条已闭环处理）。

## 7. Risk

L1。已知风险：
1. 上游对 `tools:[]`（无 tool_choice）的接受度未实测——PC-033 盲区清单在案（守卫语义按 Spec 不改 `MapToolsTo*` presence-only）。
2. 守卫代码两向对称 + T3 桥接路径绿，但 live 实测仅覆盖 Responses 方向；Anthropic 方向同类形态依赖同一守卫（已列 PC-033 盲区）。

## 8. Problems Found

1. 首轮同类扫查漏「同协议直通」路径（首轮绿跑 T1/T2 暴露）→ 已补咽喉点守卫 + 逃逸分析归档 PC-033（扫查条款：必须含数据流汇合点）。
2. 全量基线红登记分散于 TODO 多行（L172/L246/L338/L526/L548），对表需跨行拼合 → 沿用 TODO L548 既有「基线治理」项，不新增悬空。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS（偏差先记录再修正：pipeline 咽喉点） |
| Plan → Code | PASS |
| Code → Test | PASS（TDD 红→绿 + 阳性对照） |
| Test → Evidence | PASS（三档日志全留痕） |
| Evidence → Review | PASS |

### 9.5 本批自复盘（必填 · 2026-10-31 用户复盘要求立）

- **经验提取（保留打法，2-4 条）**：
  1. TDD 红→绿与阳性对照（G2 守护）同步设计：红灯期同时锁 6 红与 12 绿，守卫落地后回归即证「修了该修的、没伤不该伤的」。
  2. 「类级咽喉点」定位法：协议转换类缺陷先画出站数据流汇合点，守卫放汇合点可同时覆盖直通/桥接两路（本轮 T1/T2 判责核心打法，已沉淀进 PC-033）。
  3. 长测（15m19s）走后台 job + 日志正文判读（exit code 不作证据），期间并行准备基线对表材料，等待零损耗。
- **待改进（1-3 条）**：
  1. 首轮同类扫查只查「协议转换写点」清单，漏「body 原样转发」路径——机制修正：同类扫查必须含「数据流汇合点」条款（已回写 PC-033 扫查清单）。
  2. 全量基线红登记分散多处，对表耗时——由 TODO L548「基线治理」项承载（已在案）。

## 10. 问题类归档

PC-033（新建，首见）：`docs/11-troubleshooting/problem-classes/PC-033-tool-choice-without-tools.md`（status: guarded，G2×4 + G1×2）；case：`docs/11-troubleshooting/cases/2026-11-13-toolchoice-without-tools.md`（5 Whys + 第 6 问 + 落选方案①②③）。已随提交 `8eba29e` 入库。

## 11. 重构提案

R0：不重构。理由：守卫已按「写点 + 咽喉点」纵深防御落位，形态与既有 `OpenAISdkAdapter` 出站守卫同族；`LlmProxyPipeline` 直通/桥接双路径结构是既有裁决（`e50b9b9` 薄转发批），本批不扩面。NOT-to-do（不动 `OpenAISdkAdapter`、不改 `MapToolsTo*` presence-only 语义、不新增 `ChatToResponsesDropped` 条目）已由 mini-task Forbidden 与 PC-033 承载，不再另记 `not-taken-decisions.md`。

## 12. 悬空教训清单（必须为空）

空。逐条去向：① `tools:[]` 上游接受度 → PC-033 盲区清单；② Anthropic 方向实测 → PC-033 盲区清单；③ 全量基线红治理 → TODO L548（逐字可检索）；④ E2E 未跑 → 深档触发未满足，属档位边界记录（07 §4），非遗留问题。
