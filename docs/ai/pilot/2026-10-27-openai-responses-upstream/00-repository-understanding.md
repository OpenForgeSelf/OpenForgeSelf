# 00-repository-understanding（出站上游支持 OpenAI Responses 新格式 · 2026-10-27 · PILOT-074）

> 阶段：Stage 0｜规范：docs/04-standards/ai-native-engineering-workflow.md §2｜所有条目来自真实仓库内容与实查外部文档，禁止常识推测。

## 任务域一句话

用户输入「本项目仅支持openai兼容格式，增加openai新格式」的**准确含义（读码实证）**：统一 AI 网关**入站**已支持三种风格（含 OpenAI Responses），但**出站上游**只有「OpenAI 兼容 chat/completions（+Anthropic messages 特例）」一条路——纯 Responses 上游（Azure `/openai/v1`、vLLM 等）接不进来。本任务 = 给出站补 OpenAI 新格式（Responses API）。

## 项目结构（与本任务相关部分）

- `ForgeSelf.Api/Controllers/UnifiedAI/`：入站统一网关（OpenAIChatController / OpenAIResponsesController / AnthropicMessagesController / ModelsController / AgentChatController）
- `ForgeSelf.Api/Services/AI/`：`AIProviderRegistry`（出站路由）+ `Providers/OpenAICompatibleProvider`（出站实现）
- `ForgeSelf.Abstractions/`：`IAIProvider` / `UnifiedChatModels` / `AIConfigModels`（跨边界契约层）
- `ForgeSelf.Api/Entities/AIProvider.cs`：供应商实体（ProviderType 为字符串列）
- `ForgeSelf.Web/src/components/settings/AiProvidersPanel.vue` + `src/types/aiProvider.ts`：供应商设置 UI

## 技术栈

| 层 | 技术 | 依据 |
| --- | --- | --- |
| 后端 | .NET 10 / ASP.NET Core 10 + NewLife.XCode(SQLite) | `docs/01-architecture/overview.md` §1 |
| AI 出站 | 手写 HttpClient + System.Text.Json（无 OpenAI SDK） | `OpenAICompatibleProvider.cs:1-66` |
| 前端 | Vue 3.5 + Element Plus + TS | `ForgeSelf.Web/package.json` |
| 测试 | xUnit(Moq/FluentAssertions) + vitest + Playwright e2e | AGENTS.md §5 |

## 架构特点（本任务域数据流）

```
入站三风格（v1/chat/completions、v1/responses、v1/anthropic/messages）
   → ConvertToUnifiedRequest() 归一为 UnifiedChatRequest（Abstractions 统一内部表示）
   → AIProviderRegistry.GetProviderByModel() 按 provider:upstream 前缀/别名路由
   → IAIProvider.ChatAsync / ChatStreamAsync  ←【出站接缝，当前仅 OpenAI 兼容一种实现】
   → 上游 HTTP（chat/completions SSE 或 anthropic /messages）
```
录制备链（ChatSession/ChatTurn）与多模态（MultimodalProcessor）挂接在控制器层，出站协议替换不影响其结构。

## 代码事实（file:line，实读）

| # | 事实 | 依据 |
|---|------|------|
| F1 | 契约：`AIProviderType { OpenAI, Anthropic, Custom }` | `ForgeSelf.Abstractions/AIConfigModels.cs:27-32` |
| F2 | 出站接口：`IAIProvider`（ChatAsync/ChatStreamAsync/GetModelsAsync） | `ForgeSelf.Abstractions/IAIProvider.cs:7` |
| F3 | 统一内部表示：`UnifiedChatRequest`（Messages/SystemPrompt/ContentBlocks/Tools/ToolChoice/Stream/MaxTokens…）、`UnifiedChatResponse`、`UnifiedStreamChunk`（DeltaContent/DeltaToolCall/Usage/IsDone） | `ForgeSelf.Abstractions/UnifiedChatModels.cs:69/113/124` |
| F4 | 出站唯一实现：`OpenAICompatibleProvider`——非流式**直发 `_config.Endpoint`**（:44）；流式经 `GetChatEndpointByProviderType` 推导 URL（:81）；SSE 解析 chat delta，`include_usage` 哨兵分片回填 usage（:133-155）；工具调用 delta 映射（:171-180） | `OpenAICompatibleProvider.cs` |
| F5 | URL 推导规则：OpenAI→补 `/v1/chat/completions`，Anthropic→补 `/v1/messages`，**其他类型 `default` 直接 throw `NotImplementedException`**（Custom 未覆盖，潜在既有缺陷，本批不修只记录） | `OpenAICompatibleProvider.cs:260-289` |
| F6 | 工厂单点：`ReloadAsync` 对**所有**配置一律 `new OpenAICompatibleProvider(cfg, httpClient)`——按类型分派只改这一处 | `AIProviderRegistry.cs:46-52` |
| F7 | 实体映射：`ToConfig` 用 `Enum.TryParse<AIProviderType>(entity.ProviderType, true)`（宽松，未知值回落 OpenAI）；保存时 `config.ProviderType.ToString()`——**实体 ProviderType 是普通字符串列（`AIProvider.cs:47-48`），新增枚举值 = 零 DB 迁移** | `AIProviderService.cs:458-466`、`:441` |
| F8 | 测试连接：用 `GetChatEndpointByProviderType` 推导完整 chat 接口后 POST（Anthropic 特判 body/鉴权；OpenAI 走 `messages`+`max_tokens`，`ExtractContent` 三字段回退） | `AIProviderService.cs:171-246`、`docs/02-features/001-ai-provider-config.md:64` |
| F9 | 入站 Responses 控制器已存在：`[Route("v1/responses")]`；input(string/message 数组)→Unified、instructions→SystemPrompt、function tools→Unified.Tools；**previous_response_id 已解析但未处理**；非流式回包把 `function_call.arguments` **序列化为 object（规范要求 JSON 字符串，协议偏差）**；流式仅发 `response.created/output_text.delta/completed`，**usage 恒 0、无 output_item.added/function_call_arguments.delta** | `OpenAIResponsesController.cs:15-18`、`:489-490`、`:415-423`、`:135-273` |
| F10 | 前端类型下拉仅三值 OpenAI/Anthropic/Custom；`AIProviderType = 'OpenAI' \| 'Anthropic' \| 'Custom'`；`typeTagMap` 按 TS Record 全键约束（加新值必同步，否则编译红） | `AiProvidersPanel.vue:597-601`、`:37`、`types/aiProvider.ts:1` |
| F11 | 既有同域测试桩可复用：`AIProviderTestConnectionUrlTests`（URL 推导 Theory）、`FakeHandler/HandlerHttpClientFactory`（`AIModelFeatureTests.cs:22-87` 同款） | `ForgeSelf.Api.Tests/Unit/` |

## 调研：标准做法（外部实查，均有出处）

**① OpenAI 新格式 = Responses API（`POST /v1/responses`），与 chat/completions 长期并存**（[Azure OpenAI Responses API 官方文档](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/responses)）：
- 请求：`input` = 字符串或 item 数组（message / function_call / function_call_output…）；系统提示用 `instructions`；`max_output_tokens`（对应 chat 的 max_tokens）；结构化输出在 `text.format`（chat 用 `response_format`）。
- 函数工具定义**扁平结构** `{"type":"function","name","description","parameters"}`（chat 是 `{type:"function",function:{...}}` 包裹层）；模型侧输出 `function_call` item（`call_id` + `arguments` 为 **JSON 字符串**，官方示例 `json.loads(item.arguments)`），工具结果以 `function_call_output` item 回填。
- 多轮：`previous_response_id`（服务端状态）**或**无状态手工拼接（把上一轮 `output` items 原样追加进下一轮 `input`）。
- 流式为类型化事件（`response.output_text.delta` 等）；**流中错误 HTTP 仍 200，以 `error` 事件下发**——解析器必须处理。
- 上游侧模型对 Responses 的覆盖（Azure 列表含 gpt-5.x/gpt-6.x/o 系列等）与 chat 不完全重合。

**② 官方 SDK 形态 = 按 API 形态各建客户端、共享传输**（[openai-dotnet](https://github.com/openai/openai-dotnet)，DeepWiki 实答）：`ChatClient` 与 `ResponsesClient`（`OpenAI.Responses`）并存；`CreateResponseOptions`/`ResponseResult`（output items: message/reasoning/function_call）；流式 typed updates（OutputTextDelta / FunctionCallArgumentsDelta·Done / ReasoningSummary*）；`arguments` 线上格式为 JSON 字符串。→ 印证「新增一个 provider 实现」而非改造现有类。

**③ 网关侧标杆 LiteLLM = 统一内部规范 + 每 provider 适配器 + 可选双向桥接 + 原生透传开关**（[LiteLLM /responses 文档](https://docs.litellm.ai/docs/response_api)）：上游无 Responses 能力时把 `/v1/responses` 入站桥接为 `/v1/chat/completions` 出站（反之亦然）；上游本身支持时配 `supported_endpoints: ["/v1/responses"]` **原样透传**；桥接方向由按模型/部署的 `mode` 配置决定。→ 印证「协议按配置声明」是业界标准，桥接是可选增强。

**④ 上游支持面（2026-10 实查）**：Azure OpenAI（`/openai/v1/`，官方文档）、vLLM（[官方文档含 OpenAI Responses Client 示例](https://docs.vllm.ai/en/latest/)：reasoning/tools/MCP 三例）已支持 `/v1/responses`；OpenRouter/Groq 支持情况未逐家实测（Unknown）；DeepSeek/Kimi/Qwen 等纯兼容端点预计仍只有 chat/completions（Inferred，未实测）。→ **结论：不能全局切协议，必须按 provider 声明。**

## 调研补充：Microsoft Agent Framework 路线（2026-10-27 输入3 用户问询，实查）

**仓内现状**：`Microsoft.Agents.AI.OpenAI 1.6.1` 已是宿主依赖（`ForgeSelf.Api.csproj:73`），传递依赖 `OpenAI/2.10.0`（openai-dotnet，含 `ResponsesClient`）+ `Microsoft.Extensions.AI/10.5.1`（`obj/project.assets.json:337/:132`）。`AgentChatController.cs:76-102` 已有「自定义 Endpoint + 代理 HttpClient（`HttpClientPipelineTransport`）→ `GetChatClient().AsIChatClient()` → `AsAIAgent()`」完整样板；其注释（:28-30）记录关键教训：**AIAgent 会在服务端自动执行工具，网关代理模式（工具由客户端执行、转发 tool_calls）必须停在 `IChatClient` 层**。

**MAF 能力面**（[microsoft/agent-framework](https://github.com/microsoft/agent-framework) DeepWiki 实答，2026-10-27）：
- 出站 Responses：`Microsoft.Agents.AI.OpenAI` 提供 `ResponsesClient` 的 `AsAIAgent()` / `AsIChatClient()` 扩展，支持自定义 base URL（Azure `/openai/v1`、vLLM 等 /v1/responses 兼容端点）→ **出站协议层可完全交给 SDK，无需手写 wire 格式**。
- 入站暴露：`Microsoft.Agents.AI.Hosting.OpenAI` 有 `MapOpenAIChatCompletions()` 与 `MapOpenAIResponses()`（默认 `/{agentName}/v1/*`）→ 可把单个 AIAgent 暴露为 OpenAI 兼容端点，但**不含**本项目网关语义（多 provider 注册表 + `provider:model` 前缀路由 + ChatSession/ChatTurn 录制 + 三风格统一 + ApiKeyPolicy），且 Anthropic 入站风格无 mapper → **入站仍保留自家三控制器**。
- Anthropic：MAF 有官方维护的 `Microsoft.Agents.AI.Anthropic` 连接器（基于 Anthropic NuGet 包）→ 未来补 Anthropic 原生出站也有现成件（需新增包，本期不做）。

**对实现路线的影响**：出站 Responses 有两条落法——A) SDK 适配（新 provider 内部用 `ResponsesClient`/`AsIChatClient()`，只写 Unified↔M.E.AI/SDK 薄映射，复用 `AgentChatController` 的映射样板；协议正确性归官方 SDK，PC-008 风险大降，零新增包）；B) 纯手写 DTO+SSE（原 02-spec 写法，零 SDK 依赖但协议自查）。**推荐 A**，待闸门1 拍板后修订 02/03。Unknown：1.6.1 实际 API 面与 `OPENAI001` Experimental 状态以实施期实测为准。

## 拍板与范围记录（2026-10-27 输入4，用户拍板）

- **路线 A1 采纳**：出站 Responses 走 openai-dotnet SDK 适配（raw SDK 类型保 delta 语义，见 02-spec「实现路线」）。
- **范围扩大**：存量手写 OpenAI chat 协议的**出站调用点**统一切 SDK。追加读码事实：`Microsoft.Extensions.AI.OpenAI/10.5.1` 确认在依赖树（project.assets.json:181）；迁移清单盘点完成——`OpenAICompatibleProvider` OpenAI/Custom 路径 + `AIService` 回退路径（:146-205 手写 POST/解析，含 `reasoning_content` 扩展字段回退与 10s 超时/降级语义）+ `AIProviderService` 测试连接探针（:199-264）；`MultimodalProcessor`（只构造 Unified 请求）与 `ModelProxy`（纯 IWebProxy 装配）无手写协议不涉及；**入站 serving 层无 SDK 对应物，不迁移**（MAF Hosting 替代=架构级，not-taken 另议）；`OpenAIChatController` 自带重复 DTO（:684-843）属既有债务，本批不清。
- 工件升级为 v2：02 重写（迁移清单/FR-5~8/AC-4 对拍/U-1~U-6）、03/04 新建。

## 适用问题类（按 tags 检索表）

| 触碰区域 | 命中 PC | 本任务如何满足 |
|---|---|---|
| 对外协议客户端（第三方 API 封装） | PC-008 | 新写 `OpenAIResponsesProvider` 属第三方 API 封装 → Spec 逐字段对官方协议（arguments 字符串、事件名、store/previous_response_id），AC 覆盖 |
| 设置页 AI 提供方编辑表单 | PC-012 | 只加下拉选项不动弹窗结构；走查确认编辑仍视口级弹窗 |
| 模型解析/上游空响应 | PC-013 | Responses 上游空 `output` 不当成功；测试连接 `ExtractContent` 适配 responses 结构；AC 覆盖 |

## 现有工程规范（对本任务有约束力）

- 遵循 Controllers/Services 分层、`async/await` 不 `.Result`（AGENTS §4.2）；契约入 Abstractions（architecture-design 铁律 2——`AIProviderType`/`Unified*` 均已在 Abstractions，沿用）。
- 中风险（公共接口/宿主源码）→ 执行后全量 `dotnet test`（AGENTS §3/§5.6）。
- 脚本一律 `pwsh`；验证判据看日志正文不拿 exit code 当证据（AGENTS §2.3/§5.0）。

## 构建命令

```bash
cd ForgeSelf.Api && dotnet build
cd ForgeSelf.Api.Tests && dotnet test
cd ForgeSelf.Web && pnpm run check && pnpm run test
```

## 候选低风险任务与选择原因

本任务即选定项：新功能、接缝清晰（出站工厂单点 F6）、零 DB 迁移（F7）、零新依赖（沿用 HttpClient）、默认行为零变化（不新增配置时走原路径）。风险=中（枚举契约扩展 + 宿主源码），按 §5.6 中档全量验证可兜住。
