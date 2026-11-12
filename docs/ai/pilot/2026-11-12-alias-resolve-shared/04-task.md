---
task_kind: defect
risk: { level: L1, triggers: ["ForgeSelf.Api/Controllers/UnifiedAI", "ForgeSelf.Api/Services/AI/AIProviderRegistry.cs", "ForgeSelf.Api/Entities/ChatTurn.cs"], raised_by_agent: true }
expected_files:
  - ForgeSelf.Api/Services/AI/AIProviderRegistry.cs
  - ForgeSelf.Api/Controllers/UnifiedAI/OpenAIChatController.cs
  - ForgeSelf.Api/Controllers/UnifiedAI/OpenAIResponsesController.cs
  - ForgeSelf.Api/Controllers/UnifiedAI/AgentChatController.cs
  - ForgeSelf.Api/Controllers/UnifiedAI/AnthropicMessagesController.cs
  - ForgeSelf.Api/Entities/ChatTurn.cs
feature: alias-resolve-shared
rollback: 还原上述文件到改前版本；头列改回长度 1000 即恢复旧校验
---

# 04 Task

## Allowed

- 共用 `ResolveUpstreamModel`，四个网关入口改为调用它。
- `ChatTurn.RequestHeaders` / `ResponseHeaders` 长度改为 -1。
- 对应单测、问题类 PC-028 / PC-029、功能文档 004 的别名一句。

## Forbidden

- 不改出站 URL 拼接，不改 GPUStack 配置。
- 不重启、不停止用户正在运行的宿主。
- 不截断请求头来绕过长度校验。

## 验证命令

`dotnet test ForgeSelf.Api.Tests --filter FullyQualifiedName~ResolveUpstreamModel_PublicAlias_SubstitutesUpstreamId|FullyQualifiedName~GatewayModelEntries_CallSharedResolveUpstreamModel|FullyQualifiedName~SaveTurnAsync_HeaderJsonOver1000Chars_PersistsFullText`
