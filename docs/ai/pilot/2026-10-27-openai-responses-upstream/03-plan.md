# Plan（PILOT-074 · 2026-10-27 · openai-responses-upstream）

> 阶段：Stage 3｜必须具体到真实文件路径。Task ID：PILOT-074。规格依据：同目录 02-spec v2（路线 A1 + 存量手写出站点切 SDK）。

## Files To Change

- file: `ForgeSelf.Abstractions/AIConfigModels.cs`
  reason: `AIProviderType` 枚举新增 `OpenAIResponses`（FR-1；零 DB 迁移，F7）
- file: `ForgeSelf.Api/Services/AI/Providers/OpenAISdkAdapter.cs`（新建）
  reason: 唯一 SDK 适配层：`AIProviderConfig`→`OpenAIClient`（Endpoint base 推导 + `HttpClientPipelineTransport(IHttpClientFactory client)` 代理继承 + `Retries=0`，BC-2）；`UnifiedChatRequest`→`ChatCompletionOptions`/`CreateResponseOptions`；`ChatCompletion`/`ChatCompletionUpdate`/`ResponseResult`/`StreamingResponseUpdate`→`UnifiedChatResponse`/`UnifiedStreamChunk`
- file: `ForgeSelf.Api/Services/AI/Providers/OpenAIResponsesProvider.cs`（新建）
  reason: 新协议类型编排（FR-2/3/4），薄层调用 adapter
- file: `ForgeSelf.Api/Services/AI/Providers/OpenAICompatibleProvider.cs`
  reason: OpenAI/Custom 路径切 adapter（FR-5）；非流式 URL 归一化（BC-1，修正 :44 直发 Endpoint 的既有不对称）；**Anthropic 分支与 OpenAI DTO 区逐字保留**（BR-5）
- file: `ForgeSelf.Api/Services/AI/AIProviderRegistry.cs`
  reason: `ReloadAsync` 按 ProviderType 分派工厂（FR-6，:46-52 单点）
- file: `ForgeSelf.Api/Services/AIProviderService.cs`
  reason: 测试连接三路探针（FR-7；OpenAI/Custom→SDK chat 探针、OpenAIResponses→SDK responses 探针、Anthropic 手写保留 :199-264）
- file: `ForgeSelf.Api/Services/AIService.cs`
  reason: 回退路径条件迁移（FR-8，门=U-2；保持 10s 超时 :171 与降级语义 :177/:199/:204）
- file: `ForgeSelf.Web/src/types/aiProvider.ts` + `ForgeSelf.Web/src/components/settings/AiProvidersPanel.vue`
  reason: 联合类型 + 下拉项 + `typeTagMap` 补键（FR-9；:1、:37、:597-601）
- file: `ForgeSelf.Api.Tests/Unit/OpenAISdkAdapterTests.cs`、`ForgeSelf.Api.Tests/Unit/OpenAIResponsesProviderTests.cs`（新建）；`AIProviderTestConnectionUrlTests.cs`（扩展 Responses 形态 Theory）；`AIProviderTestConnectionUrlTests`/新增 AIService 回退对拍用例
  reason: AC-1~7 的载体；FakeHandler + `HttpClientPipelineTransport` 注入（复用 `AIModelFeatureTests.cs:22-87` 桩模式）
- file: `ForgeSelf.Web/src/__tests__/AiProvidersPanel.test.ts`
  reason: AC-8
- file: `ForgeSelf.Web/e2e/settings/ai-providers.spec.ts`（或既有同域 spec，实施时按实际路径）
  reason: 冒烟 1 条：创建 OpenAIResponses 类型 provider→列表 tag→编辑回显（AC-UI-1~2）
- file: `docs/02-features/001-ai-provider-config.md`、`docs/02-features/004-provider-models-integration.md`、`docs/16-reference/api.md`
  reason: 闸门3 归档时回写真源（供应商类型清单 + URL 归一化规则扩展 + 出站协议说明）

## Implementation Steps

1. **Unknown 实证先行**（U-1/U-2/U-3/U-5）：临时探针级 xUnit 用 FakeHandler 抓 SDK 实际出站请求（max_tokens 字段名、reasoning_content 可达性、流式 usage、ResponsesClient Experimental 标记）→ 结论回填本文件偏差表；U-2 不过则 FR-8 降级为「保留手写」并改 AC-7。
2. 契约：`AIConfigModels.cs` 枚举 + `types/aiProvider.ts` 联合类型（typeTagMap 缺键即编译红，TS 守卫先红）。
3. adapter TDD：先写 AC-4 对拍测试（同一 fake chat 响应喂旧手写解析路径与新 adapter，断言 Unified 等价）→ 实现 `OpenAISdkAdapter` → 绿。
4. `OpenAIResponsesProvider` TDD：AC-1（请求 wire 抓包）→ AC-2（流式映射）→ AC-3（非流式/失败态）。
5. Registry 分派 + 测试连接三路：AC-5、AC-6（含 URL 推导 Theory 扩展）。
6. `OpenAICompatibleProvider` 切换：OpenAI/Custom→adapter（含 BC-1 归一化）；Anthropic 分支不动；跑 AC-9 回归（既有 provider/网关/录制/别名测试）。
7. `AIService` 回退路径：按步骤 1 的 U-2 结论执行迁移或保留（AC-7）。
8. 前端：下拉项 + tag 映射 + 编辑回显（AC-8）。
9. e2e 冒烟 1 条 + dev-stack 起源码现场走查（AC-10，按 02 交互规格逐项 + PC-012 弹窗层级核对；截图存 `ForgeSelf.Web/screenshots/e2e/`）。
10. 门禁收口 + docs 真源回写（Files To Change 最后一组）。

## Test Plan

1. 新增 xUnit：adapter 对拍（AC-4）、Responses 三件套（AC-1/2/3）、工厂与 URL（AC-5）、测试连接三路（AC-6）、AIService 回退（AC-7，视门）。
2. 扩展 xUnit：`AIProviderTestConnectionUrlTests` 增 OpenAIResponses 形态 Theory。
3. vitest：`AiProvidersPanel.test.ts` 增新类型四项断言（AC-8）。
4. e2e：ai-providers 冒烟 1 条（零 mock，真实后端；§5.0 环境前置：NO_PROXY/TEMP）。
5. 回归：既有 provider/网关三风格/别名/录制相关测试（AC-9）。

## Verification

### Build

```bash
cd ForgeSelf.Api && dotnet build
```

### Unit Test（新增过滤集 + 全量=中档，因触碰宿主源码 ForgeSelf.Api/**）

```bash
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~OpenAISdkAdapter|FullyQualifiedName~OpenAIResponses|FullyQualifiedName~AIProviderTestConnectionUrl"
cd ForgeSelf.Api.Tests && dotnet test
```

### Integration Test

```bash
# FakeHandler 集成用例已并入上述 Unit 过滤集（SDK Transport 可注入），无独立集成套件
```

### E2E

```bash
cd ForgeSelf.Web && pnpm run test:e2e -- --grep ai-providers
```

### Other Checks

```bash
cd ForgeSelf.Web && pnpm run check && pnpm run test && pnpm run design:check
```

## Plan 偏差记录

> 实现中发现 Plan 与仓库实际不符时，先在此记录偏差，再修正 Plan，不得直接绕过。

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 2026-10-28 | U-3 实证（探针 wire 抓包） | 假设流式需手发 stream_options.include_usage | **SDK 流式自动发送 `include_usage:true`**，usage 哨兵分片照常到达 |
| 2026-10-28 | U-5 实证（探针） | —— | ResponsesClient 需 `#pragma warning disable OPENAI001`；`CreateResponseStreamingAsync` 必须显式 `StreamingEnabled=true`；请求落 `POST {base}/v1/responses`，instructions 独立于 input items |
| 2026-10-28 | U-6a 编译实证 | `options.Retries = 0` | 属性名实为 **`RetryPolicy`**：`options.RetryPolicy = new ClientRetryPolicy(maxRetries: 0)` |
| 2026-10-28 | U-6b 编译实证 | 流分片 id 透传 upd.Id | **`StreamingChatCompletionUpdate` 与 `StreamingResponseOutputTextDeltaUpdate` 均无 `Id` 成员** → 流分片复用本地生成会话 id |
| 2026-10-28 | U-6c 编译实证 | base64 图像三参 CreateInputImagePart | 无 mediaType 重载 → **mediaType 并入 data URI** |
| 2026-10-28 | U-6d 编译实证 | Error 按 string 兜底 | `Response.Error` 为 **ResponseError 类型**（含 `.Message`） |
| 2026-10-28 | U-6e 编译实证（ForgeSelf.Api 0 错误通过） | —— | MessageResponseItem/FunctionCallResponseItem/ReasoningResponseItem、ResponseToolChoice 四工厂、ResponseItem 五工厂、StreamingResponseCompletedUpdate.Response.Usage、ChatToolCallUpdate 三成员、ClientResultException.Status 全部一次命中 |
| （待填：U-1 字段名 / U-2 reasoning_content 可达性 / U-5b 流式事件序列——测试工程被并行会话 ADR-004 改名暂时阻塞，其修复后跑探针回填） |  |  |  |
| 2026-10-28 01:05 | U-1 实证（U1_MaxTokens 探针钉板） | —— | SDK 对 MaxTokens 实际发送 **`max_completion_tokens`**（BC-3 风险落地：旧兼容上游若只认 `max_tokens` 会在测试连接阶段即暴露，探针与真实链路同 wire）；不引入 body 改写策略（那等于回到手写协议） |
| 2026-10-28 01:05 | U-2 实证（探针通过） | U-2 不确定→FR-8 可能保留手写 | **ModelReaderWriter 往返保留 `reasoning_content`**（SDK 类型面无此成员，往返 JSON 可达）→ FR-8 门放行：AIService 非流/流式回退均迁 SDK（`reasoningAsContentFallback` 开关，仅回退路径付往返成本） |
| 2026-10-28 01:05 | U-5b 实证（探针） | —— | `response.completed` 事件映射 `StreamingResponseCompletedUpdate` 正常；**FakeHandler 通路下 EOF 前最后一事件滞留不 flush**（垫一个后续事件即送达；真实上游分块流不受影响，官方样例均直接依赖 completed） |
| 2026-10-28 01:05 | AC-7 迁移实证（AIServiceTests 15/15） | —— | 旧测试假响应为 PascalCase 宽松形状（手写解析器大小写不敏感），SDK 严格要求 camelCase wire → 假响应改 wire 忠实形状；「无效 JSON 行跳过」韧性由 SDK 内部 SSE 解析保留（逐行 Warn 回调消失）；空 choices 形状 SDK 拒收→异常分支（Error 日志），降级语义不变 |
| 2026-10-28 01:35 | AC-7 收尾实证（RealLLMIntegrationTests 4/4） | —— | U-6f 偏差：SDK 要求非空 ApiKeyCredential（空串构造期抛异常，手写版发「Bearer 」空参）→ 适配器空 key 用无害占位符 `EMPTY`（键免鉴权上游忽略该头）；SDK 传输层响应完成后即释放请求体（MessageBodyAdapter disposed）→ RealLLM 测试改为发送现场读体（handler 回调内 ReadAsStringAsync） |
| 2026-10-28 01:35 | 中档全量（2333 例） | —— | 16 红 = 13 登记基线（TODO:101/102：UnifiedAIGateway 6 + WorkflowPlanning 6 + ScriptRunnerDi 1，`no such table: AIModel`/插件装配，与本批无关）+ 2 并行会话在改文件（ExtensionPointManagerTests，ToolExtensions 扫描）+ 1 本批（RealLLM 格式测试，已修绿）→ 修后复跑预期 15 红 = 13 基线 + 2 并行 |
| 2026-10-28 17:59 | **输入9：定标「现成优先+正确能用」，恢复输入6 批次（终态）** | BR-5 修订（同输入6） | 输入8 流程图澄清「手写≠Anthropic 原生协议」后，用户定标：不想手写（维护/测试都麻烦）、尽量用现成、正确能用是底线。恢复输入6 全部实现（adapter DeriveAnthropicCompatBaseUrl + provider 去守卫 + 探针 SDK 化 + ExtractContent 删 + 测试/文档同步）；重建期间 provider 文件曾被空写事故清空，已从 git HEAD 一次性确定性重建（全部边界模式定位+写前守卫）。验证：过滤集 79/79（与输入6 首验一致）；全量对表见 05 追记 |
