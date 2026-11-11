# 05 Evidence — 2026-11-07-json-chinese-raw-encoder

> 阶段：Stage 7｜只记录实际发生的事情。所有验证项均为 **Verified**（亲自跑过，引用真实日志）。
> 日志文件位于 `.temp/`（gitignored），关键输出原文已逐条摘录于下文，日志清理后仍可对账。

## Task

PILOT：JSON 序列化中文保持原文（去除 `\uXXXX` 转义）——宿主 + 22 插件全量扫点。
执行方式：宿主 ~35 文件由 Lead 亲自清扫；22 插件目录按分区交 3 个并行子代理（组A/组B/组C）清扫，终态由 G2 守卫机器判定；子代理豁免遗留的 10 处只读初始化器由 Lead 按统一不变量补齐。

## Acceptance Criteria 对照（02-spec AC1–AC6）

| AC | 内容 | 状态 | 证据来源 |
| --- | --- | --- | --- |
| AC1 | G1 单测：`ForgeJson.Default`/`Indented` 输出中文原文且不含 `\u`，命名策略为 null | ✅ Verified | `.temp/guard-green.log`（5/5 通过，含 4 个 G1） |
| AC2 | G2 守卫 RED→GREEN；阳性对照通过 | ✅ Verified | `.temp/guard-red2.log`（488 违例，1 failed）→ `.temp/guard-green.log`（0 failed） |
| AC3 | `dotnet build` 0 error | ✅ Verified | `.temp/host-build2.log`（371 警告 / 0 错误） |
| AC4 | 全量 `dotnet test` 新增红 = 0（对表基线红清单） | ✅ Verified | `.temp/full-test1.log`（29 红逐组归因见下：本批引入 1 红已修 + 本批行为变更暴露夹具缺陷 2 红已修，其余 26 红均为存量/并行会话/环境瞬时/负载 flake，非本批） |
| AC5 | AC1–AC4 全部留 Verified 日志于 05 | ✅（随 AC4 回填即齐） | 本文件 |
| AC6 | PC-027 + case 归档、台账索引更新、`check-problem-classes.ps1` 绿 | ✅ Verified | `.temp/check-pc1.log`：`check-problem-classes OK：27 个 PC 签名/allowlist/guards 全部通过`（PC-027 status=guarded） |

## Changed Files

**新建（2）**

- `ForgeSelf.Abstractions/ForgeJson.cs` — 共享选项：`Default`（Encoder=UnsafeRelaxedJsonEscaping）、`Indented`（Default + WriteIndented）
- `ForgeSelf.Api.Tests/Architecture/JsonNonAsciiEscapingGuardTests.cs` — G2 源码枚举守卫 ×1 + G1 行为单测 ×4（共 5 测试）

**宿主生产代码扫点（Lead 亲自，~35 文件）**

UnifiedAI 4 控制器 + StreamEvents + AgentChatController（StreamSerializerOptions 已有 Encoder）、ChatController、ChatSessionResolver（2 处 + using）、ToolRegistry（9 处）、SessionEventJsonConverter、PermissionProfileService、UnifiedUpstreamErrors、LocalFileImageRecognitionCache、LocalModelGate、WebSocketBroadcaster、PersistentApprovalService、UsageStatsService、WorkflowUsageService（4 处）、UpdateSettingsService + PluginUpdateSettingsService（→ForgeJson.Indented + using ForgeSelf.Abstractions）、PluginArtifactValidator（2 处）、PluginSrcScaffold、PluginJobStore、PluginScaffolderService（L338 位于 `$$$"""` 模板内，同步修模板产出 + using）、PluginSourceResolver、PluginHostTools（3 处）、PluginPackagerService（3 处 deserialize-only）、PluginManager（deserialize-only）、OpenAICompatibleProvider（2 处 deserialize-only）、OpenAIResponsesProvider（deserialize-only）、AppBuilder（AddJsonOptions + using）。

**测试夹具修复（1，本批行为变更暴露的伴生缺陷）**

- `ForgeSelf.Api.Tests/Plugins/McpCenterTests/Fixtures/mock-mcp-server.js` — mock `runStdio` 的 LSP 帧读取从「`setEncoding('utf8')` 后按 JS 字符串长度切片」改为 Buffer 字节级切帧（`Content-Length` 按字节、帧体按字节切再 UTF-8 解码）；ASCII 载荷下旧实现碰巧正确，中文原文载荷下帧被误判不完整 → 客户端 300s 超时。归因与验证见「Integration Test」实验 4。

**插件扫点（3 子代理分区，按目录不相交）**

| 分组 | 目录 | 子代理回报 | 终态验证 |
| --- | --- | --- | --- |
| 组A | WorkflowEngine / MemorySystem / TodoTracker / QuickLinks / Scheduler / ScriptRunner / Sems / Home | 18 文件 ~150 处 | G2 GREEN 背书 |
| 组B | McpCenter / DevTools / SystemMonitor / FileTools / AIAgent / AgentHub | 51 文件 ~208 处（含 DevTools JsonFormatterService 3 处 JsonWriterOptions、YamlFormatterService L85） | G2 GREEN 背书；自检残留仅 ToolCallFileExtractor.cs L14 文档注释（非代码） |
| 组C | DesignSystem / ToolBridge / CostScope / BrowserUse / SmartTerminal / DbAdmin / ImGateway / SamplePlugin | 21 文件（含 DesignSystemController JsonOpts L29、ResultFormatter ToJsonString×3） | G2 GREEN 背书 |

**统一不变量补齐（Lead 亲自，10 处只读/反序列化初始化器）**

子代理曾按旧规则「仅 Deserialize 使用的 options 不动」豁免，Lead 裁定统一不变量（所有生产 `new JsonSerializerOptions/JsonWriterOptions` 初始化器——含 target-typed `new()` 与 deserialize-only——一律声明 Encoder；Encoder 在反序列化时被忽略，零读侧风险）后补齐：BrowserUseConfig.ReadOptions、BuiltInAgentExecutionService、PlanGeneratorService、ImGatewayDbStores、McpGatewayConfig、PermissionClassifier、CallParser.LooseJson、TurnLedger.ReadOptions、ProfileLoader、RunTerminalCommandTool（9 处无 using 的文件用全限定 `System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping` + PC-027 注释）。

**文档/归档（3）**

- `docs/11-troubleshooting/problem-classes/PC-027-json-nonascii-default-escaping.md`（新建）
- `docs/11-troubleshooting/cases/2026-11-07-json-chinese-raw-encoder.md`（新建）
- `docs/11-troubleshooting/problem-classes/README.md`（tags 表 + 总表各加 PC-027 一行）

## Build（AC3）

Command:

```bash
cd ForgeSelf.Api && dotnet build
```

Result: PASS（来源等级：Verified）

```text
# .temp/host-build2.log 尾部原文
    371 个警告
    0 个错误
已用时间 00:00:08.76
```

（371 警告为基线存量 nullable/xUnit 提示，与本次改动无关；`AppBuilder.AddJsonOptions` 首版 CS1061 已修正后重跑。）

## Unit Test（AC1 + AC2）

Command（守卫过滤集）:

```bash
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~JsonNonAsciiEscapingGuard"
```

**RED（修复前，TDD 红灯留证）** — Result: FAIL（预期红灯，来源等级：Verified）

```text
# .temp/guard-red2.log 断言消息原文
Expected offenders to be empty because PC-027 违例 488 处（JSON 写出未用 ForgeJson，中文会转义成 \uXXXX）：
# 测试尾部
失败!  - 失败:     1，通过:     4，已跳过:     0，总计:     5，持续时间: 8 s - ForgeSelf.Api.Tests.dll (net10.0)
```

失败测试 = G2 `生产代码_JSON写出路径_必须中文保持原文_禁止默认转义`（JsonNonAsciiEscapingGuardTests.cs:line 46）；G1 4 测试此刻已绿。

**GREEN（清扫完成后）** — Result: PASS（来源等级：Verified）

```text
# .temp/guard-green.log 尾部原文
已通过! - 失败:     0，通过:     5，已跳过:     0，总计:     5，持续时间: 4 s - ForgeSelf.Api.Tests.dll (net10.0)
```

- 488 → 0 违例；阳性对照（G2 扫描命中计数下限，防正则失效假绿）随 G2 通过一并成立。
- AC1 的 4 个 G1：`ForgeJson.Default` 序列化含「目录上溯」对象输出中文原文且不含 `\u`、`Indented` 同且带缩进、命名策略 null、对照组默认 options 仍转义——全绿（同一日志）。

## Integration Test（AC4）

全量 `dotnet test`（中档门禁：碰宿主源码触发 §5.6 中档）。

Command:

```bash
cd ForgeSelf.Api.Tests && dotnet test
```

Result: 29 失败 / 3708 通过 / 3737 总计，25m1s（来源等级：Verified，`.temp/full-test1.log` 尾部原文）

```text
失败!  - 失败:    29，通过:  3708，已跳过:     0，总计:  3737，持续时间: 25 m 1 s - ForgeSelf.Api.Tests.dll (net10.0)
```

**逐组归因**（记录基线红 ForgeSetting ×1 + PluginFrontendFileMiddleware ×2 本轮反而通过，未新增其红；各组数量以 `full-test1.log` 失败清单逐行统计为准）：

| 组 | 数量 | 错误特征 | 归因（终判） |
| --- | --- | --- | --- |
| ProblemClassRegistryTests.Ledgers_ListEveryPc_And_EveryCase | 1 | `cases/README.md 索引缺 case 行: 2026-11-07-json-chinese-raw-encoder` | **本批引入，已修**（补 cases/README 索引行后复跑 5/5 绿，`.temp/registry-retest1.log`） |
| ProjectWorkspaceDefaultRootTests | 15 | `缺少必填参数 cwd：工作目录绝对路径`（FileToolOps.cs L42，已提交契约）；测试仍按 FR-5 旧语义写 | **存量**（HEAD worktree 同红，实验 1；FileToolOps.cs 未修改；FileTools 4 文件差异经 `git diff` 核验为纯 Encoder 编辑） |
| WorkflowPlanningIntegrationTests | 6 | `POST /api/ai-agent/workflow/plan` 404 | **存量**（HEAD worktree 同红，实验 1） |
| ScriptRunnerDiIntegrationTests | 1 | `GET /api/scripts/runtimes` 404 | **存量**（HEAD worktree 同红，实验 1） |
| McpClientIntegrationTests.StdioTransport_EchoAndAdd_RoundTrip + McpStdioFramingTests.Auto_Framing_LspServer_FallsBackAndHandshakes | 2 | StdioMcpTransport `tools/call` 300s 超时（5m/5m5s）；两测试均以中文载荷（`你好`/`回退帧`）调 echo，同会话 initialize/tools/list（ASCII）全过 | **本批行为变更暴露的测试夹具缺陷，已修**（实验 4：mock `runStdio` 用 `setEncoding('utf8')` 后按 JS 字符长度切 Content-Length 帧，ASCII 载荷碰巧正确、中文载荷帧被误判"不完整"→ 客户端超时；客户端字节级写帧本就合规。修复 mock 为 Buffer 字节级切帧 + 复跑绿 `.temp/mcp-fixture-retest1.log`；全仓 JS `Content-Length` 扫描仅此一处，无同类扩散） |
| DesignAgentToolContractTests.空参数_不抛 | 1 | `success` False | **负载 flake**（机器空闲单独复跑通过，`.temp/flake-retest2.log`） |
| MafApprovalLoopTests.ConfiguredRule | 1 | `tool/approval-request` 事件仍出现 | **编译快照瞬时中间态**（干净重建后整组 6/6 绿，`.temp/maf-retest3.log`；HEAD worktree 同绿——15:48 全量那次构建取到了并行会话编辑中的瞬时中间态；DB 污染假说已证伪：移走 `bin\Data\ForgeSelf.db` 后 `--no-build` 仍红 `.temp/maf-retest2.log`，重建即绿排除该假说） |
| DbAdminToolFunctionTests.Tools_SevenWithDbPrefix | 1 | Expected 7 tools, found 8 | **并行会话引入，非本批**（HEAD worktree 该测试绿；`git diff` 实锤并行会话 DbAdmin 多库任务新增 DbListDatabasesTool，本批对其文件仅 Encoder 一行） |
| ScriptRunnerTests.CancelAsync_ForRunningScript_ShouldCancelExecution | 1 | 真实 powershell.exe 进程取消，5s 轮询窗超时 | **负载 flake**（机器空闲单独复跑通过，`.temp/flake-retest3.log`） |

**归属实验（证据链）**：

1. **HEAD worktree 对照**：在干净 worktree（commit d2d0d92，不含本批任何未提交改动）跑同一批 6 组失败测试 —— 结果：**22 失败 / 56 通过 / 78 总计，失败集与主树 29 红中的 22 个逐个完全一致**（ProjectWorkspace×15 + WorkflowPlanning×6 + ScriptRunnerDi×1），Design/Maf/DbAdmin/Registry 在 HEAD 全绿（`.temp/attrib-head1.log`，实验后已自 worktree 复制回主 checkout `.temp/` 留存）。HEAD 上同红 = 存量红。
2. **空闲复跑（第一轮，作废）**：全量结束后单独复跑 2×Mcp + CancelAsync —— 实验被我误杀 testhost 92832（该 job 的宿主）中止作废；教训：杀空闲期残留 testhost 前必须先查 job 归属。
3. **空闲复跑（第二轮）**：Design 空参数 + Maf ConfiguredRule 空闲复跑 —— Design 过（flake 实锤）、Maf 仍红（`.temp/flake-retest2.log`）；Maf 移库假说实验（`.temp/maf-retest2.log`）红；最终干净重建实验（`.temp/maf-retest3.log`）6/6 绿定性。CancelAsync 随实验 4 的 job 空闲复跑通过（`.temp/flake-retest3.log`）。
4. **Mcp 定性复跑 + 夹具修复验证**：空闲复跑 2×Mcp + CancelAsync（干净重建后）—— 2×Mcp 仍红且失败点精确落在中文载荷 `tools/call`（`.temp/flake-retest3.log`：5m/5m5s，两次均 `外部 MCP 请求超时（300s）：tools/call`）→ 非 flake；定位为 mock 夹具字符级切帧缺陷（详见上表）→ 修复 `mock-mcp-server.js`（Buffer 字节级切帧）→ 复跑两测试（`.temp/mcp-fixture-retest1.log`）—— 结果：**2/2 通过 / 5 秒**（修复前各 300s 超时），夹具修复生效。

**净新增红终判**：29 红中，本批引入且当时新增 = 1（Registry 索引行，已修复归零）；本批行为变更（中文原文输出）暴露的伴生夹具缺陷 = 2（Mcp×2，已修复归零）；其余 26 红 = 存量 22 + 并行会话 1 + 编译快照瞬时 1 + 负载 flake 2（Design/CancelAsync，本会话实证空闲即过）—— **修复后本批净新增红 = 0**。

**Mcp 夹具修复验证**（`.temp/mcp-fixture-retest1.log` 尾部原文）：

```text
已通过! - 失败:     0，通过:     2，已跳过:     0，总计:     2，持续时间: 5 s - ForgeSelf.Api.Tests.dll (net10.0)
```

（两个测试的中文 echo 载荷往返即字节级帧解析的常驻守卫：任一侧回归字符级切帧都会复现 300s 超时红。）

## E2E

Result: N/A（依据：未碰 e2e 基建（global-setup/playwright config/fixtures）、未发版未打 tag；03-plan「Verification」节已声明本批不触发 e2e，批次级裁剪理由见 07 §4 E2E）。

## Static Analysis（AC6）

1. **G2 源码枚举守卫**（结构性静态分析，常驻测试）：见「Unit Test」节 RED→GREEN，常驻防复发。
2. **`pwsh scripts/check-problem-classes.ps1`**（问题类台账机器校验，PC-027 注册后跑）— Result: PASS（来源等级：Verified）

```text
# .temp/check-pc1.log 尾部原文
[scan] PC-027 status=guarded 完成

check-problem-classes OK：27 个 PC 签名/allowlist/guards 全部通过
```

## Screenshots

N/A（本批无 UI 变化；02-spec「Interaction Design」节已声明无界面交互）。

## Known Limitations

- G2 守卫已知盲区（PC-027 §2 记录）：`$$$"""` 原始字符串模板内的模板代码（脚手架产物路径）、插值字符串孔内调用、异形重载（SerializeToUtf8Bytes 等，全仓 grep 0 命中）。
- 插件分组文件数为子代理回报值，终态正确性由 G2 守卫 GREEN 机器背书（分组内部不复核逐文件清单）。

## Unresolved Issues

无 BLOCKED / 无待回填项。AC1–AC6 全部 ✅ Verified；29 红全量归因闭环（本批引入 1 + 伴生暴露 2 均已修复归零，净新增红 = 0）；存量 22 红、并行会话 1 红、编译快照瞬时 1 红、负载 flake 2 红均已归属并记 TODO 观察项（见 07 §7 Risk / 日志）。
