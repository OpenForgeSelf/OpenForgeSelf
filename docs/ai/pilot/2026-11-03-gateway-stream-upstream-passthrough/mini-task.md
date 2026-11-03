# mini-task · 统一 AI 网关上游 HTTP 错误全量透传

## Intent
凡经 `openai-dotnet` / `HttpClient` 返回的上游 HTTP 错误（**不限 429**：4xx/5xx 全状态），网关对客户端的行为与上游语义一致：**非流式 = 同 HTTP 状态 + error 体；流式 = SSE error + 正常终止**，禁止再统一压成 500 或「流半路断无 `[DONE]`」。

## Spec
| 项 | 要求 |
|----|------|
| 异常来源 | `ClientResultException`（`Status` + Message）、`HttpRequestException`；**InnerException 链**向上解包 |
| 状态码 | 原样 `StatusCode(status)`；`Status==0` 时落 502 |
| error.type | Message 内 JSON 的 `error.type` 优先；否则按状态映射（401/403→authentication，429→rate_limit，4xx→invalid_request，5xx→upstream） |
| error 体 | Message 内嵌 `{ "error": … }` **优先原样回传**；否则网关构造 OpenAI 形 `{ message, type, code }` |
| 流式 Chat/Responses | `data: {"error":…}` + `data: [DONE]` |
| 流式 Anthropic | `event: error` + snake_case |
| 覆盖控制器 | `OpenAIChatController`、`OpenAIResponsesController`、`AnthropicMessagesController`、**`AgentChatController`**（实验 Agent 路由） |

## Plan
1. `UnifiedUpstreamErrors.cs`：Resolve 解包 + ClientResult 字段 + JSON 透传 + SSE 写回  
2. 四控制器 catch 统一走 Resolve / ToErrorBody / Write*StreamError  
3. `UnifiedUpstreamErrorsTests` Theory（400–503）+ 网关 429 流式/非流式集成测  
4. `docs/02-features/004-provider-models-integration.md` §2.6

## Task
- **Allowed**：`ForgeSelf.Api/Services/AI/UnifiedUpstreamErrors.cs`、`Controllers/UnifiedAI/*.cs`（上述四件）、`ForgeSelf.Api.Tests/**`、004 §2.6、本 mini-task / 05-evidence  
- **Forbidden**：改 SDK 适配器出站逻辑、发布、停/启宿主

## Interaction Design
无界面交互（纯 API 网关）。

## 验证（快档）
```powershell
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj `
  -p:OutputPath=bin/upstream-passthrough/ `
  -p:IntermediateOutputPath=obj/upstream-passthrough/ `
  --filter "FullyQualifiedName~UnifiedAIGatewayIntegrationTests|FullyQualifiedName~UnifiedUpstreamErrorsTests"
```
判定：**全绿**；Theory 至少覆盖 400/401/403/404/408/422/429/500/502/503。

## 待你拍板
无，默认按 Spec 全量透传执行。
