# Specification（PILOT-074 v2 · 2026-10-27 · openai-responses-upstream）

> 由 00 真实读码 + 01 Intent 推导。Task ID：PILOT-074。
> **v2 变更（2026-10-27 输入4 用户拍板）**：①出站 Responses 采用**路线 A1（openai-dotnet SDK 适配）**；②**范围扩大**——存量手写 OpenAI chat 协议的**出站调用点**统一切换到 MAF 依赖传递携带的 SDK（`OpenAI/2.10.0` + `Microsoft.Extensions.AI(.OpenAI)/10.5.1` 均已在依赖树，零新增包）。
> 外部协议依据：[Azure OpenAI Responses API](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/responses)、[LiteLLM /responses](https://docs.litellm.ai/docs/response_api)、[openai-dotnet](https://github.com/openai/openai-dotnet)、[microsoft/agent-framework](https://github.com/microsoft/agent-framework)（DeepWiki 实查 2026-10-27）。

## 实现路线（闸门1 已拍板项）

| 决策 | 内容 |
|---|---|
| A1 = raw SDK 类型 | 适配器用 openai-dotnet 原生类型（`ChatClient`/`ChatCompletion`/`ChatCompletionUpdate`；`ResponsesClient`/`ResponseResult`/`StreamingResponseUpdate` 族），不用 M.E.AI `AsIChatClient()` 高层封装——理由：网关职责是**增量转发**，raw SDK 保留 delta 逐块语义；M.E.AI 连接器会合并增量（A2）破坏与现网关等价性 |
| 迁移范围 = 出站调用点 | 见下方「手写协议迁移清单」；入站 serving 层**不在范围**（openai-dotnet 是客户端 SDK，无服务端对应物；MAF Hosting 替代入站=架构级变更，登记 not-taken 另议） |

## 手写协议迁移清单（出站调用点盘点，2026-10-27 读码实证）

| 站点 | 现状 | 处置 |
|---|---|---|
| `OpenAICompatibleProvider`（OpenAI/Custom 类型路径） | 手写 `BuildOpenAIRequest`/DTO/SSE 解析（:32-183、:415-637、DTO 区 :640-932） | **迁移到 SDK**（chat 非流式/流式/models）；OpenAI DTO 区保留给 Anthropic 遗留路径 |
| `OpenAICompatibleProvider`（Anthropic 类型路径） | 同一套 OpenAI 格式体发往推导出的 `/v1/messages`（URL/鉴权头特判，体不转 Anthropic 原生） | **原样保留**（行为 parity；原生 Anthropic 迁移=独立 TODO，走 `Microsoft.Agents.AI.Anthropic`） |
| `AIService` 默认回退路径 | 手写 POST `_aiConfig.ApiEndpoint` + `AIChatResponse` 解析 + **`reasoning_content` 扩展字段回退**（AIService.cs:146-205；流式 :206+） | **条件迁移**：门=SDK 面能取到 `reasoning_content`（FakeHandler 回放 deepseek 形状实测）；取不到则保留手写并在 07 记 Known Limitation |
| `AIProviderService.TestConnectionAsync` | 手写探针 body（:199-264，OpenAI/Anthropic 两分支） | OpenAI/Custom→SDK chat 探针；新增 OpenAIResponses→SDK responses 探针；Anthropic 探针手写保留（x-api-key 头，SDK 不支持） |
| `MultimodalProcessor` | 只构造 `UnifiedChatRequest` 走 `IAIProvider`，无手写 wire | 不迁移（provider 切 SDK 后自动受益） |
| `ModelProxy` | `IWebProxy` 纯代理装配（ModelProxy.cs:19） | 不涉及 |
| 入站 serving（OpenAIChat/OpenAIResponses/AnthropicMessages/AgentChat/AgentStreamTranslator） | 手写 SSE serving + DTO（OpenAIChatController 自带一套重复 DTO :684-843） | 不迁移（见上表决策）；重复 DTO 清理不做（无关重构） |

## Functional Requirements

| # | 需求 | 依据/落点 |
|---|------|-----------|
| FR-1 | `AIProviderType` 新增 `OpenAIResponses`；复用实体 `ProviderType` 字符串列，`ToConfig` 的 `Enum.TryParse` 天然支持，零 DB 迁移 | `AIConfigModels.cs:27-32`、`AIProviderService.cs:458-466` |
| FR-2 | 新建 `OpenAIResponsesProvider : IAIProvider`（SDK 路线）：`UnifiedChatRequest`→`CreateResponseOptions`（`SystemPrompt→instructions`；user/assistant/system 消息→`input` items（`input_text`/`output_text`；图像块→`input_image`）；assistant.tool_calls→`function_call`（`call_id`+`arguments` **JSON 字符串**）；role=tool→`function_call_output`；`Tools`→扁平函数定义；`ToolChoice` 透传；`MaxTokens→max_output_tokens`；固定 `store:false`）；自定义 Endpoint（base 推导）+ 代理注入 `HttpClientPipelineTransport` | 00-F2/F3、`AgentChatController.cs:79-85` 样板 |
| FR-3 | FR-2 响应映射：`ResponseResult`（message/output_text→正文；function_call→`UnifiedToolCall`，`arguments` 字符串原样）+ `usage{input_tokens,output_tokens,total_tokens}`→`UnifiedUsage`；流式 `StreamingResponseUpdate`→`UnifiedStreamChunk`（文本 delta→`DeltaContent`；function_call 增量→`DeltaToolCall`；完成→usage+IsDone；`failed`/流中 `error` 事件→终止失败；未知事件跳过）——**空 `output`/失败态不得当成功** | PC-008/PC-013 lens |
| FR-4 | 模型列表：OpenAIResponses 走 OpenAI 同源 `/v1/models`（SDK models 客户端若面合适则用 SDK，否则保留既有 GET+解析，实施期定，兜底见 Unknown-4） | 00-F5 |
| FR-5 | `OpenAICompatibleProvider` 的 OpenAI/Custom 路径切 SDK：`ChatAsync`/`ChatStreamAsync` 经新 SDK 适配器（Unified→`ChatCompletionOptions`；`ChatCompletion`/`ChatCompletionUpdate`→Unified）；请求头 Bearer 由 SDK 承担；`GetModelsAsync`（OpenAI 分支）同 FR-4 策略；**Anthropic 分支逐字保留** | 迁移清单 |
| FR-6 | `AIProviderRegistry.ReloadAsync` 按 `ProviderType` 分派工厂（OpenAI/Custom/Anthropic→`OpenAICompatibleProvider`，OpenAIResponses→`OpenAIResponsesProvider`） | 00-F6 |
| FR-7 | 测试连接：三路探针——OpenAI/Custom→SDK chat 探针（URL 推导规则不变，预算语义对齐现值 128）；OpenAIResponses→SDK responses 探针（`input`+`max_output_tokens:128`，URL 推导补 `/v1/responses`，四形态与 001 文档归一化语义一致）；Anthropic→手写探针保留 | 00-F8、`AIProviderService.cs:171-264` |
| FR-8 | `AIService` 回退路径条件迁移（见迁移清单门）：SDK 调用 + 保持 10s 超时（:171）、非 2xx/异常→`GetFallbackResponse` 降级语义（:177/:199/:204）、`reasoning_content` 回退语义（:189-193）逐条等价 | 迁移清单 |
| FR-9 | 前端：类型下拉新增「OpenAI Responses」（value=`OpenAIResponses`）；`AIProviderType` 联合类型同步；`typeTagMap` 补键（TS Record 全键约束=编译期守卫）；tag 与编辑回显正确 | 00-F10 |

## Input

- 既有 `AIProvider` 实体 + 设置页表单；新合法值 `ProviderType="OpenAIResponses"`；Endpoint 三形态（裸域/`…/v1`/完整路径）均可推导。
- 既有依赖：`OpenAI/2.10.0`、`Microsoft.Extensions.AI(.OpenAI)/10.5.1`、`Microsoft.Agents.AI(.OpenAI)/1.6.1`（`obj/project.assets.json`）——**不新增 NuGet 包**。

## Output

- 出站 wire：OpenAI/Custom 走 SDK 形状（与手写版语义等价，AC-4 对拍证明）；OpenAIResponses 走 Responses 协议。
- 入站对客户端、录制（ChatSession/ChatTurn）、统一契约 `Unified*`/`IAIProvider`：**不变**。

## Business Rules

- BR-1 协议按 provider 显式声明；不做自动探测/桥接（LiteLLM 式桥接登记 not-taken）。
- BR-2 无状态：`store:false`、不透传 `previous_response_id`（出站每轮发完整 input）。
- BR-3 默认行为零变化：未配置新类型/未触达迁移点时行为不变；**已声明的行为修正仅 BC-1 一项**。
- BR-4 Responses-only 能力（reasoning/web_search/file_search 等工具与 item 类型）本期不透传不映射（入站既有忽略行为保持；响应侧跳过+日志）。
- BR-5 **修订（输入9 终版，2026-10-28 17:59 用户定标「不想手写，维护测试都麻烦，尽量用现成；但正确能用是底线」）**：Anthropic 聊天/流式/测试连接探针同样切 SDK——走 Anthropic 官方 OpenAI 兼容层（base 归一 …/v1 + /chat/completions，Bearer 鉴权；`max_completion_tokens`/`stream`/`tools`/system-hoist 官方支持，2026-10-28 文档实证）；**模型列表保留手写原生 `GET /v1/models`**（x-api-key + anthropic-version，兼容层不含该端点）。原「原生格式修正=独立任务」由兼容层路线一并解决。已知限制：Claude 4.7+ 仅收默认 temperature/top_p（其它值 400，探针先暴露）；非 workspace 域 key 需 `anthropic-workspace-id` 头（未做）。
  - 沿革：输入6（14:21）首次拍板并实施 → 输入7（15:1x）「维持手写」回滚 → 输入8（17:27）流程图澄清「手写≠原生协议」→ 输入9（17:59）定标现成优先+正确能用，恢复输入6 批次（本版为终态）。

## Boundary Conditions

- 上游不支持 `/v1/responses`（404/405）→ 走既有错误转译，不自动降级换协议（BR-1 反面）。
- **BC-1（唯一有意行为修正，声明）**：OpenAI/Custom 非流式 `ChatAsync` 由「直发 `_config.Endpoint` 原文」（`OpenAICompatibleProvider.cs:44`，配裸 `…/v1` 会 404 的既有不对称）改为与流式/测试连接同一套 URL 归一化（补 `/chat/completions`）——配完整 URL 的既有用户零感知，配 base URL 的从 404 变为可用。
- **BC-2**：SDK 客户端显式 `Retries=0`（或实施期确认默认值后等价配置），保持现「单次请求」语义，避免失败时延变化。
- **BC-3**：SDK 请求头（User-Agent 等）与手写版存在无害差异，AC 不逐字对头。

## Error Handling

- 上游 4xx/5xx：既有异常转译链路不变（错误轮次落 ChatTurn）。
- 流中 `error` 事件/`response.failed`：终止流、置失败态，不输出假完成（PC-013）。
- SDK `ClientResultException`（或等价）在适配器内转译为既有异常语义，调用方（控制器/测试连接）无感。

## Compatibility

- 枚举扩展向后兼容；`Enum.TryParse` 宽松解析保持。
- 契约层 `Unified*`/`IAIProvider` 不动：AIAgent 插件、AIService 路由分支、录制、别名路由均不感知。
- 前端联合类型新增值由 TS 强制同步（typeTagMap 缺键编译红）。

## Non-functional Requirements

- 不新增 NuGet 包（SDK 已在依赖树）。
- 适配器集中一处（新文件），`OpenAICompatibleProvider`/`OpenAIResponsesProvider` 只做编排；日志风格与现 provider 对齐。

## Failure Modes（PC lens 逐条）

| 命中 PC | 本任务如何满足 |
|---|---|
| PC-008（对外协议客户端合规） | wire 细节交官方 SDK；适配器只做类型映射；AC-1/2/4 抓包+对拍断言 |
| PC-012（设置页提供方表单） | 只加下拉项与 tag 映射；走查核对弹窗层级 |
| PC-013（模型解析/空响应当成功） | FR-3/FR-8 空输出/failed/error 一律失败或降级语义逐条等价；AC-3/7 覆盖 |

## Interaction Design（交互设计 · 必写节）

### 交互规格（点什么出现什么）

| 交互点 | 触发 | 结果 | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| 类型下拉 | 打开供应商编辑弹窗 | 下拉四项：OpenAI / Anthropic / Custom / OpenAI Responses | 单选，默认 OpenAI | 选中即回显 | 不涉及（必填有默认） | 编辑回显存储值；历史未知值回落 OpenAI（ToConfig 现行为） |
| 保存 | 选 OpenAI Responses + 填 Endpoint | 入库，列表 tag 显示 `OpenAIResponses` | 校验通过才可保存 | 成功刷新列表（既有链路） | Endpoint 空=不生效（既有规则） | ApiKey 留空=保留原密钥（文案不变） |
| 测试连接 | 点「测试」（新类型） | 按 `/v1/responses` 推导 URL 探活 | loading→成功/失败 | 成功 toast 含耗时；失败文案含上游错误 | 不涉及 | 上游无该端点明确报 404/405，不静默降级（BR-1） |

### 交互验收标准（并入 AC）

- AC-UI-1：下拉有且仅有四项，新项 label=「OpenAI Responses」、value=`OpenAIResponses`。
- AC-UI-2：保存后列表 tag=`OpenAIResponses`；重新编辑回显正确。
- AC-UI-3：mock 上游（200+output_text）测试连接成功；无该端点（404）失败文案可见，无「假成功」。

### 走查符合性（DoD 绑定）

- dev-stack 起源码前后端，按 `ui-ux-design` 走查清单核对（PC-012 弹窗层级、tag 语义色、长名不破版）。

## Acceptance Criteria（闸门1 确认清单）

- [ ] AC-1 xUnit：Responses 请求映射——FakeHandler 抓包断言 wire：`instructions`/`input` items（含 `function_call` 的 `arguments` 为 JSON **字符串**、`function_call_output`、`input_image`）/扁平 `tools`/`max_output_tokens`/`store:false`。
- [ ] AC-2 xUnit：Responses 流式映射——SDK 事件流→UnifiedStreamChunk：文本 delta 聚合、工具参数增量聚合、usage 回填、`failed`/`error` 终止失败、未知事件跳过不抛。
- [ ] AC-3 xUnit：Responses 非流式映射——message/function_call→Unified；`status!=completed` 或空 `output` → 失败/异常，不当成功。
- [ ] AC-4 xUnit：chat SDK 迁移**对拍（golden master）**——同一组 fake chat 响应（纯文本/工具调用/多模态 content 数组/usage 哨兵分片）喂旧手写解析与新 SDK 适配器，断言 Unified 输出等价；BC-1 抓包断言（完整 URL 配置行为不变）。
- [ ] AC-5 xUnit：工厂分派（新类型→`OpenAIResponsesProvider`；其余类型实例不变）+ URL 推导 Theory（Responses 四形态 + chat base 推导）。
- [ ] AC-6 xUnit：测试连接三路——SDK chat 探针、SDK responses 探针、Anthropic 手写探针（含 x-api-key 断言）；成功/失败/解析失败文案语义与现一致。
- [ ] AC-7 xUnit：AIService 回退路径（若迁移门通过）——deepseek 形状（含 `reasoning_content`）回放→结果取到 reasoning 文本；10s 超时与降级 fallback 语义保持；**迁移门不通过则本条改为「保留手写」并在 07 记录**。
- [ ] AC-8 vitest：下拉四项/保存 payload/typeTagMap 新键/编辑回显。
- [ ] AC-9 回归：既有 OpenAICompatibleProvider、网关三风格、别名路由、代理录制相关测试全绿（BR-3）。
- [ ] AC-10 走查：dev-stack 现场按交互规格逐项核对（AC-UI-1~3 + PC-012）。

## Unknown

| 不确定点 | 影响 | 处理方式 |
|---|---|---|
| U-1 `ChatCompletionOptions` 的输出上限序列化字段（`max_completion_tokens` vs 旧 `max_tokens`）——部分兼容上游只认旧字段 | 出站请求被忽略上限或 400 | 实施第一步 FakeHandler 抓包实证；若仅发新字段且目标上游不认 → 评估 Pipeline 策略补字段或该维度保守回退手写（记录于 03 偏差表） |
| U-2 AIService 回退的 `reasoning_content`（deepseek 系扩展，非 OpenAI 官方字段）SDK 是否可达 | FR-8 迁移门 | FakeHandler 回放实测；不可达则保留手写（AC-7 改记） |
| U-3 流式 usage：raw SDK 是否自动置 `include_usage` | usage 回填 | FakeHandler 实证；必要时在 options 层显式开启 |
| U-4 models 列表 SDK 面（`GetModelsClient` 等）与第三方 /models 兼容性 | FR-4 兜底 | 实施期定；不合适则保留既有 GET+解析 |
| U-5 `ResponsesClient` 在 2.10.0 的 `OPENAI001` Experimental 状态 | 编译警告/特性开关 | 实施期确认；按需包 `#pragma` 或等价处理 |
| U-6 OpenRouter/Groq/DeepSeek 等第三方 `/v1/responses` 支持面 | 不影响设计（BR-1 按配置声明） | 搁置；文档标注已确认支持方（Azure/vLLM） |
