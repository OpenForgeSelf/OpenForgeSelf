---
task_id: 2026-10-27-openai-responses-upstream
feature: 004-provider-models-integration
risk: { level: L2, triggers: ["ForgeSelf.Abstractions/AIConfigModels.cs", "ForgeSelf.Api/Services/AI/**", "ForgeSelf.Api/Services/AIService.cs", "ForgeSelf.Api/Services/AIProviderService.cs", "ForgeSelf.Web/src/components/settings/AiProvidersPanel.vue"], raised_by_agent: false }
gate1: { mode: user, at: null, decision: null }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - ForgeSelf.Abstractions/AIConfigModels.cs
  - ForgeSelf.Api/Services/AI/Providers/OpenAISdkAdapter.cs
  - ForgeSelf.Api/Services/AI/Providers/OpenAIResponsesProvider.cs
  - ForgeSelf.Api/Services/AI/Providers/OpenAICompatibleProvider.cs
  - ForgeSelf.Api/Services/AI/AIProviderRegistry.cs
  - ForgeSelf.Api/Services/AIProviderService.cs
  - ForgeSelf.Api/Services/AIService.cs
  - ForgeSelf.Web/src/types/aiProvider.ts
  - ForgeSelf.Web/src/components/settings/AiProvidersPanel.vue
  - ForgeSelf.Api.Tests/Unit/OpenAISdkAdapterTests.cs
  - ForgeSelf.Api.Tests/Unit/OpenAIResponsesProviderTests.cs
  - ForgeSelf.Api.Tests/Unit/AIProviderTestConnectionUrlTests.cs
  - ForgeSelf.Web/src/__tests__/AiProvidersPanel.test.ts
  - ForgeSelf.Web/e2e/ (ai-providers 冒烟 1 条)
  - docs/02-features/001-ai-provider-config.md
  - docs/02-features/004-provider-models-integration.md
rollback: "git revert 本批 commit（新类型未被配置时零行为影响；BC-1 有意修正随同 revert 回退）"
writeback: { feature_doc: pending, reason: "闸门3 归档时回写 001/004/api.md 真源" }
---

# Task（PILOT-074 · 2026-10-27 · openai-responses-upstream）

> 阶段：Stage 4｜前序工件 00/01/02/03 齐备，**待闸门1（用户）批准后进入 Implement**。

## Task ID

PILOT-074-openai-responses-upstream（工件目录 `docs/ai/pilot/2026-10-27-openai-responses-upstream/`）

## Objective

一句话：供应商可声明「OpenAI Responses」出站协议（SDK 适配，零新增包），且存量手写 OpenAI chat 协议的**出站调用点**统一切到 MAF 传递依赖携带的 openai-dotnet SDK；默认行为除已声明的 BC-1 外零变化，后端全量测试与前端门禁全绿。

## Scope

### Allowed

- 03-plan.md「Files To Change」所列文件；新增 xUnit/vitest/e2e 用例；闸门3 时回写 001/004/api.md 真源。
- `OpenAISdkAdapter`（新建）承载全部 Unified↔SDK 映射；`OpenAICompatibleProvider` 的 OpenAI/Custom 分支改造。
- BC-1（非流式 URL 归一化）、BC-2（Retries=0）两处已声明行为修正。

### Forbidden

- 生产环境 / DB 结构（实体列复用，无迁移）/ 鉴权安全逻辑。
- 新增 NuGet 包（SDK 已在依赖树；MAF.Anthropic 等留待独立任务）。
- 入站 serving 层（OpenAIChat/OpenAIResponses/AnthropicMessages/AgentChat/AgentStreamTranslator）与其重复 DTO 的清理。
- Anthropic 类型遗留路径的行为变更；`MultimodalProcessor`/`ModelProxy` 的改动。
- 无关重构（OpenAIChatController 内重复 DTO 不清理；命名/格式顺手改不做）。

## Acceptance Criteria

- [ ] AC-1~AC-10 按 02-spec「Acceptance Criteria」逐条（Responses 请求 wire 抓包 / 流式映射 / 非流式失败态 / chat 对拍 golden master / 工厂+URL / 测试连接三路 / AIService 回退（视 U-2 门）/ vitest / 回归 / 走查）。
- [ ] AC-UI-1~3 按 02-spec「交互验收标准」。
- [ ] 全量后端 `dotnet test`（中档）绿；前端 `check`+`test`+`design:check` 绿。
- [ ] 07 报告含 Unknown U-1~U-6 的实证结论（偏差表回填）。

## Expected Files

见 frontmatter `expected_files`（与 03-plan Files To Change 一致）。

## Verification Commands

```bash
cd ForgeSelf.Api && dotnet build
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~OpenAISdkAdapter|FullyQualifiedName~OpenAIResponses|FullyQualifiedName~AIProviderTestConnectionUrl"
cd ForgeSelf.Api.Tests && dotnet test
cd ForgeSelf.Web && pnpm run check && pnpm run test && pnpm run design:check
cd ForgeSelf.Web && pnpm run test:e2e -- --grep ai-providers
```

（判据看日志正文 PASS/FAIL 计数，不拿 exit code 当证据；e2e 前置见 AGENTS §5.0：NO_PROXY/TEMP。）
