# mini-task：tool_choice 无 tools 时未丢弃 → 上游 400（openai 响应式接口报错）

- 日期：2026-11-13｜类型：缺陷修复（≤3 源文件 → mini-task 裁剪）｜来源：用户输入6（截图）
- 现象：经 LlmProxyPipeline 的 OpenAI Responses 协议请求携带 `tool_choice` 但不带 `tools` 时原样透传 → 上游 400 `When using tool_choice, tools must be set. (parameter=tool_choice)`（type=BadRequestError, param=tool_choice）。

## Intent（为什么/做什么/到什么程度）
- 为什么：tool_choice 是 tools 的从属字段；出站写点未守卫从属关系，导致上游 400。这不是单点而是「类」——全部 tool_choice 出站写点同根因。
- 做什么：类级修复——「源请求 tools 缺失或空数组 ⇒ 丢弃 tool_choice」守卫语义在全部出站写点统一落地；有 tools 时行为完全不变（阳性保绿）。
- 程度：快档过滤集红→绿 + 中档全量对比基线无新增红 + 问题类归档（docs/11-troubleshooting）。

## Spec（守卫语义，含 Unknown）
- 判据：源请求 `tools` 缺失或 count==0 ⇒ 出站体不得含 `tool_choice`；`tools` 非空 ⇒ tool_choice 原样保留（"auto"/"required"/function 对象三形态）。
- 对称性：Chat→Responses 与 Responses→Chat 两方向同语义；M.E.AI ToolMode=RequireAny（无 Tools）与 UnifiedChatRequest.ToolChoice（无 Tools）同语义。
- 不新增 `ChatToResponsesDropped` 条目（tool_choice 携 tools 时合法，仅无 tools 时丢弃）。
- Unknown：上游对 `tools:[]`（无 tool_choice）的接受度——T2 断言只锁 tool_choice 缺失，`tools:[]` 保留（MapToolsTo* 为 presence-only 守卫，本次不改其语义）。
- 交互设计：无界面交互——纯后端 LLM 代理出站守卫，无 UI 变更；操作面 = 宿主 LLM 代理 HTTP 端点（经 `LlmProxyPipeline` 出站），入参 = 统一入站请求体的 `tools`/`tool_choice` 字段，错误提示 = 修复前上游 400 `BadRequestError`（形态见 05 §1），修复后无 tools 时 `tool_choice` 静默剔除、有 tools 行为不变。

## Plan（真实文件）
1. `ForgeSelf.Api/Services/AI/Proxy/ChatResponsesBridge.cs` — `MapToolChoiceToResponses`/`MapToolChoiceToChat` 方法入口加源 tools 计数守卫（自包含，不动调用点）。
2. `ForgeSelf.Api/Services/AI/Proxy/LlmProxyChatClient.cs` — BuildJson tool_choice 写点加 `&& options.Tools is { Count: > 0 }`。
3. `Plugins/AIAgent/MafLoop/OpenAIChatStreamReader.cs` — `OpenAIChatJson.FromUnified` tool_choice 写点加 `&& request.Tools is { Count: > 0 }`。
4. **【偏差记录，绿灯期补充】** `ForgeSelf.Api/Services/AI/Proxy/LlmProxyPipeline.cs` — `ExecuteAsync` 在 `LlmJson.Write(outboundNode)` 前加咽喉点守卫。背景：首轮绿跑 T1/T2 仍红（toolchoice-green-run.log），诊断出**同协议直通路径**（pipeline L42–44，Responses 入站→Responses 上游时 body 原样转发不过 bridge）——这正是用户 live 400 的实际路径，首轮同类扫查遗漏。守卫语义同 §Spec，放类级汇合点（直通/桥接两路必经）。
- 测试：`ForgeSelf.Api.Tests/Unit/LlmProxyPipelineTests.cs` 新增 T1–T6（含两条阳性守 G2）；`ForgeSelf.Api.Tests/Plugins/AIAgent/OpenAIChatJsonTests.cs` 新增 3 例（新建）。

## Task（Allowed/Forbidden + 验证）
- Allowed：上述 4 源文件（含偏差补充的 LlmProxyPipeline.cs，同根因同类修复）+ 2 测试文件 + 本目录工件 + `docs/11-troubleshooting/` 归档 + 日记/TODO。
- Forbidden：不动 OpenAISdkAdapter（L226–246/L552–573 已有守卫）、不动 ChatToResponsesDropped、不改 csproj 版本（守卫 RepositoryReleaseVersionTests）、不 git 提交（闸门3 需用户）。
- 验证：快档 `dotnet test --filter "FullyQualifiedName~LlmProxyPipelineTests|FullyQualifiedName~OpenAIChatJsonTests"` 红→绿；中档全量 `dotnet test`（碰 ForgeSelf.Api/**）基线对表（基线红见日记，不归属本批）。
