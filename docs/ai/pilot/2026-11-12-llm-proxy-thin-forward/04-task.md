---
task_id: 2026-11-12-llm-proxy-thin-forward
feature: 004-provider-models-integration
risk: { level: L2, triggers: ["ForgeSelf.Api/Services/AI/Proxy/", "ForgeSelf.Api/Controllers/UnifiedAI/"], raised_by_agent: false }
gate1: { mode: user, at: 2026-11-12, ref: "session plan approved" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - ForgeSelf.Abstractions/LlmProxyContracts.cs
  - ForgeSelf.Api/Services/AI/Proxy/LlmProxyPipeline.cs
  - ForgeSelf.Api/Controllers/UnifiedAI/OpenAIChatController.cs
  - Plugins/AIAgent/MafLoop/ForgeChatClientAdapter.cs
  - ForgeSelf.Web/src/components/settings/AiProvidersPanel.vue
rollback: "git revert 本任务提交。库列未改，无需迁移回滚。未提交前用 git checkout 还原。"
writeback: { feature_doc: done, reason: "004 与 011 已回写" }
---

# Agent Task

> 阶段：Stage 4｜Task ID：2026-11-12-llm-proxy-thin-forward

## Task ID

2026-11-12-llm-proxy-thin-forward

## Objective

公开网关与内置 agent 的模型回合共用 `LlmProxyPipeline`。同协议原样转发。去掉识图与图片缓存。

## Scope

### Allowed

- `ForgeSelf.Abstractions/LlmProxyContracts.cs`
- `ForgeSelf.Api/Services/AI/Proxy/`
- `ForgeSelf.Api/Services/AI/Providers/OpenAISdkAdapter.cs` 的地址与密钥辅助
- `ForgeSelf.Api/Controllers/UnifiedAI/` 四个聊天控制器改为薄壳
- `ForgeSelf.Api/AppBuilder.cs`、`Plugins/PluginManager.cs` 登记管道
- `Plugins/AIAgent/` 里组模型请求并调用管道的路径
- 删除识图与缓存类型及对应测试
- `AIProviderController`、仓储 Update、设置页与其测试、e2e 夹具中的三字段
- 本目录 00–07 与 004/011/架构总览中已过时的当前事实

### Forbidden

- 改工具循环、技能、系统提示、收件箱、会话状态机的业务规则
- 删库列或写迁移
- 删 `OpenAISdkAdapter` 或给 `IAIProvider` 加发送方法
- 改 `GET /v1/models` 与 `ResolveUpstreamModel` 的匹配规则
- 停、启、杀 `:51888`，发布，提交，推送
- 新依赖、改版本号

## Acceptance Criteria

- [x] AC1–AC11，见 `02-spec.md`
- [x] 快档 `dotnet test` 过滤集通过数写入 `05-evidence.md`
- [x] 前端 check / test / design:check 的日志结论写入 `05-evidence.md`

## Expected Files

- `ForgeSelf.Api/Services/AI/Proxy/LlmProxyPipeline.cs`
- `ForgeSelf.Api/Services/AI/Proxy/OpenAIRawForwarder.cs`
- `ForgeSelf.Api/Services/AI/Proxy/ChatResponsesBridge.cs`
- `ForgeSelf.Api/Services/AI/Proxy/WireStreamTranslator.cs`
- `ForgeSelf.Api.Tests/Unit/LlmProxyPipelineTests.cs`

## Verification Commands

```bash
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~LlmProxy|FullyQualifiedName~OpenAISdkAdapter|FullyQualifiedName~AIModelFeature|FullyQualifiedName~UnifiedAI"
cd ForgeSelf.Web && pnpm run check && pnpm run test && pnpm run design:check
```
