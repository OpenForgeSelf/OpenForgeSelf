# Plan

> 阶段：Stage 3｜Task ID：2026-11-12-llm-proxy-thin-forward

## Files To Change

- file: `ForgeSelf.Abstractions/LlmProxyContracts.cs`
  reason: 入站请求、结果、管道端口
- file: `ForgeSelf.Api/Services/AI/Proxy/*`
  reason: 管道、原始转发、Chat/Responses/Anthropic 桥、流翻译
- file: `ForgeSelf.Api/Controllers/UnifiedAI/OpenAIChatController.cs` 及 Responses、Anthropic、AgentChat
  reason: 控制器改薄壳
- file: `Plugins/AIAgent/Services/AIAgentService.cs`、`MafLoop/ForgeChatClientAdapter.cs`、`GoalPursuitService.cs`
  reason: 模型回合进管道，保留原始模型 id
- file: `ForgeSelf.Api/Controllers/AIProviderController.cs`、`Repositories` 的 Update、`ForgeSelf.Web` 设置页与类型
  reason: API 与 UI 去掉多模态三字段；Update 不覆盖库列
- file: 删除 `MultimodalProcessor` 与图片缓存类型及其测试
  reason: 请求路径不再识图

## Implementation Steps

1. 契约、DI、`GetWireUri` / `ResolveApiKey`。
2. 原始转发与两条协议桥、流翻译、管道。
3. 四个控制器与宿主 `LlmProxyChatClient`、插件适配器接上管道。
4. 删除识图运行时；API、仓储 Update、设置页去掉三字段。
5. 快档测试与前端三门禁。

## Test Plan

1. `LlmProxyPipelineTests`：同协议字段、空模型、流字节、429、Chat→Responses 映射、Responses 入站流的稳定 `item_id`、Anthropic 图片。
2. `UnifiedAIGatewayIntegrationTests`：前缀模型改写、空模型、流 usage 字节、429。
3. `AIModelFeatureTests` 源码守卫：别名调用只在管道。
4. 前端设置页单测不再断言多模态徽标。

## Verification

### Build

```bash
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj
```

### Unit Test

```bash
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~LlmProxy|FullyQualifiedName~OpenAISdkAdapter|FullyQualifiedName~AIModelFeature|FullyQualifiedName~UnifiedAI"
```

### Integration Test

同上过滤集已包含 `UnifiedAIGatewayIntegrationTests`。不另跑全量。

### E2E

N/A。本批不改 e2e 基建，不新起宿主。设置页夹具只删字段，由 vitest 覆盖。

### Other Checks

```bash
cd ForgeSelf.Web
pnpm run check
pnpm run test
pnpm run design:check
```

## Plan 偏差记录

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 2026-11-12 | 同协议不能用 `CompleteChatAsync` | 用户曾说用 MAF 自带 OpenAI 客户端 | 复用该客户端的传输规则，POST 原始 JSON |
| 2026-11-12 | 宿主不能 new 插件里的适配器 | 实验 agent 直接用 `ForgeChatClientAdapter` | 宿主侧 `LlmProxyChatClient` |
| 2026-11-12 | 流测试把 Chat SSE 喂给 Responses 上游，再在客户端找 Responses 事件 | 断言挂在错误方向 | 改为 Responses 入站、Chat 上游，客户端收到 `TextDeltaEvents` |
