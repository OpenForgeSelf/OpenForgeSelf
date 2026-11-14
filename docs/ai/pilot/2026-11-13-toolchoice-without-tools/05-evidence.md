# 05-evidence — tool_choice 无 tools 未丢弃 → 上游 400

任务：`2026-11-13-toolchoice-without-tools`｜日期：2026-11-13｜汇报人：任务协调人
验证环境：dotnet test（ForgeSelf.Api.Tests），`NO_PROXY=localhost,127.0.0.1,::1`，`TEMP/TMP=<repo>\.temp\tmp`（§5.0 前置已做）。

## 1. 缺陷证据（用户截图，输入6）
- 上游 400：`When using tool_choice, tools must be set. (parameter=tool_choice)`，type=`BadRequestError`，param=`tool_choice`，code=400（JSON 探查器：body[0].error）。
- 根因（代码级，修复前）：4 个出站 tool_choice 写点全部无 tools 守卫——
  `ChatResponsesBridge.MapToolChoiceToResponses`（原 L507/516）、`MapToolChoiceToChat`（原 L527/536）、`LlmProxyChatClient.BuildJson`（原 L176–177）、`OpenAIChatJson.FromUnified`（原 L157–158）；**第 5 处（首轮绿灯期暴露）**：`LlmProxyPipeline.ExecuteAsync` L42–43 **同协议直通**（Responses 入站 → Responses 上游时 body 原样转发，完全不过 bridge）——这正是用户 live 400 的实际路径，首次同类扫查漏判（逃逸分析见 07）。
- live 链路经 `OpenAIRawForwarder` 原样 POST（pipeline 路径无 SDK 守卫）。

## 2. 红灯复现（TDD Red — 全部 Verified）
### 红灯运行1（编译期，已被运行2 取代）
- 2×CS0117：`ChatToolMode` 不含 `CreateRequiredMode`（测试 L398/L417）→ 按仓内先例 `MafTaskLoopAgentTests.cs:306` 改用 `ChatToolMode.RequireAny`。日志：`ForgeSelf.Api.Tests/.temp/toolchoice-red-run1.log`。
### 红灯运行2（行为期，权威红灯；日志 `ForgeSelf.Api.Tests/.temp/toolchoice-red-run2.log`，后台任务 pwsh-fdab365945604a90）
- 汇总：`失败: 6，通过: 12，总计: 18`（exit code 0 是重定向假象，以日志正文为准）。
- 6 红（行号=日志）：
  | # | 测试 | 失败断言 | 消息 |
  |---|------|----------|------|
  | 1 | `LlmProxyPipelineTests.ResponsesInbound_ToolChoiceWithoutTools_NotForwarded` | L525（断言 L332） | `Expected sent["tool_choice"] to be <null>, but found auto.` |
  | 2 | `LlmProxyPipelineTests.ResponsesInbound_EmptyTools_ToolChoiceNotForwarded` | L566（断言 L348） | `… to be <null>, but found required.` |
  | 3 | `LlmProxyPipelineTests.ChatInbound_ToolChoiceWithoutTools_NotForwardedToResponses` | L538（断言 L364） | `… to be <null>, but found required.` |
  | 4 | `LlmProxyPipelineTests.ChatClient_ToolModeRequiredWithoutTools_OmitsToolChoice` | L553（断言 L404） | `… to be <null>, but found required.` |
  | 5 | `OpenAIChatJsonTests.ToolChoiceWithoutTools_IsOmitted` | L497 | tool_choice 在无 tools 时仍输出 |
  | 6 | `OpenAIChatJsonTests.ToolChoiceWithEmptyTools_IsOmitted` | L511 | 空数组 tools 时仍输出 |
- 阳性保持绿（守 G2 不被误伤，12 绿内）：`ResponsesInbound_WithTools_ToolChoicePreserved`（T4）、`ChatClient_ToolModeRequiredWithTools_KeepsRequired`（T6）、`OpenAIChatJsonTests.ToolChoiceWithTools_IsKept` 及既有 9 条过滤集测试。
- 红灯形态与预测完全一致：无守卫写点 6 处暴露，有 tools 阳性全绿。

## 3. 修复（Green 实施，5 处守卫全落）
1. `ForgeSelf.Api/Services/AI/Proxy/ChatResponsesBridge.cs`：`MapToolChoiceToResponses`/`MapToolChoiceToChat` 方法入口插入 `if (<源>["tools"] is not JsonArray { Count: > 0 }) return;`（两向对称，自包含不动调用点）。
2. `ForgeSelf.Api/Services/AI/Proxy/LlmProxyChatClient.cs` L176：`RequiredChatToolMode` 写点加 `&& options.Tools is { Count: > 0 }`。
3. `Plugins/AIAgent/MafLoop/OpenAIChatStreamReader.cs` L157：`FromUnified` 写点加 `&& request.Tools is { Count: > 0 }`。
4. `ForgeSelf.Api/Services/AI/Proxy/LlmProxyPipeline.cs`（首轮绿跑后补，覆盖 T1/T2 红）：`ExecuteAsync` 在 `LlmJson.Write(outboundNode)` 前插咽喉点守卫——`if (outboundNode["tool_choice"] is not null && outboundNode["tools"] is not JsonArray { Count: > 0 }) outboundNode.Remove("tool_choice");`。该点为**类级汇合点**：同协议直通与桥接产物在此合并出站，兜住直通路径。bridge 内两守卫保留为纵深防御（直接调用 bridge 的旁路）。
- 不改：`OpenAISdkAdapter`（已有守卫 L226–246/L552–573）、`ChatToResponsesDropped`（tool_choice 携 tools 时合法）、`MapToolsTo*` 的 presence-only 语义。

## 3.1 首轮绿跑失败 → 迭代（Iterate 第 1 轮）
- 首轮（日志 `ForgeSelf.Api.Tests/.temp/toolchoice-green-run.log`，job pwsh-3d4fece237b545f8）：`失败: 2，通过: 16，总计: 18`——T1（L600 `…but found auto.`）、T2（L613 `…but found required.`）仍红。
- 诊断：T1/T2 走 Responses 入站 → Responses 上游**同协议直通**（pipeline L42–44 `request.Protocol == outboundProtocol ? body : ToOutbound(...)`），不经过 `ChatResponsesBridge`，bridge 守卫根本不在路径上；T3（chat 入站，桥接路径）首轮即绿印证此判。
- 对策：补第 5 处咽喉点守卫（见 §3.4）——放在 `LlmJson.Write(outboundNode)` 前，两路径（直通 body / 桥接产物）必经此汇合。

## 4. 绿灯与回归（Verified）
- 快档过滤集（`--filter "FullyQualifiedName~LlmProxyPipelineTests|FullyQualifiedName~OpenAIChatJsonTests"`）：见 §4.1。
- 中档全量 `dotnet test`（碰 `ForgeSelf.Api/**` 触发）：见 §4.2（基线红逐族对表，零新增红）。

### §4.1 快档绿跑
- 运行2（第 5 处守卫后，权威绿；日志 `ForgeSelf.Api.Tests/.temp/toolchoice-green-run2.log`，job pwsh-39f883b333c24d7c）：**`失败: 0，通过: 18，总计: 18`（99ms）** ✅
- 覆盖：T1–T6（LlmProxyPipelineTests，含直通路径 T1/T2、桥接路径 T3、M.E.AI 路径 T5、阳性 T4/T6）+ OpenAIChatJsonTests 3 例 + 既有过滤集 9 条（无回归）。

### §4.2 中档全量（2026-11-14 收口；日志 `ForgeSelf.Api.Tests/.temp/toolchoice-fullsuite.log`，后台 job pwsh-e1680ef8728e43fc）
- 汇总：**失败: 25，通过: 3750，总计: 3775**（持续时间 15 m 19 s；§5.0 前置已做）。
- 本批测试面（T1–T6 + OpenAIChatJsonTests 3 例）**全绿**：全量输出只打印失败用例，本批 9 例均不在失败清单。
- 25 红逐族对表基线登记（全部存量，非本批引入）：
  | 族 | 数 | 基线登记 |
  |----|----|----------|
  | `ProjectWorkspaceDefaultRootTests`（AIAgent 工作区读写断言漂移） | 15 | TODO L526（PILOT-079 回归发现，stash 对照实证非引入批；2026-11-11 基线为 14，同类） |
  | `WorkflowPlanningIntegrationTests`（测试宿主未装配插件控制器 404） | 6 | TODO L247（2026-10-10 存量） |
  | `ScriptRunnerDiIntegrationTests.GetRuntimes…` + `ScriptRunnerTests.CancelAsync…` | 2 | TODO L338（2026-10-25 存量，同 404 族） |
  | `MafApprovalLoopTests.ConfiguredRule_YieldsMafWrapper_…` | 1 | TODO L548/L29④（已知瞬时态，并行构建中间态家族） |
  | `JsonNonAsciiEscapingGuardTests`（守卫盲区：智能终端在途文件） | 1 | PC-027 已知家族（TODO L29） |
- 判责：**0 新增红**。对表 2026-11-11 全量基线（`失败 32/通过 3741`，TODO L548）：25/25 逐族命中存量登记；旧基线的 `DesignAgentToolContractTests` ×1 本轮绿；前基线记录的 `McpStdioWindowsShimTests.StdioTransport_…` 与 `OpenAIResponsesOutboundSchemaTests.Production_OutputTextDelta_…`（TODO L17 已修绿）本轮均未红。总计 3775 > 3741 属批间正常增长。

## 5. 证据等级汇总
| 结论 | 等级 | 依据 |
|------|------|------|
| 6 处无守卫写点为根因 | Verified | 修复前源码 + 6 红逐条断言消息 |
| 修复后快档绿（18/18） | Verified | toolchoice-green-run2.log 汇总行 `失败: 0，通过: 18` |
| 全量无新增红（25 红全为存量基线，§4.2 逐族对表） | Verified | toolchoice-fullsuite.log 汇总行 `失败: 25，通过: 3750，总计: 3775`（job pwsh-e1680ef8728e43fc） |
| 有 tools 阳性行为不变 | Verified | T4/T6/WithTools_IsKept 红灯期已绿 |
