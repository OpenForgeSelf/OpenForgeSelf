# Evidence

> 阶段：Stage 7｜只记录实际发生的事情。来源等级：Verified（亲自跑过）/ Inferred / Unknown。

## Task

PILOT-074（`docs/ai/pilot/2026-10-27-openai-responses-upstream/`）— OpenAI Responses API 上游支持 + 出站单格式漏斗收敛为 openai-dotnet SDK。

## Changed Files

- `ForgeSelf.Abstractions/AIConfigModels.cs` — enum `AIProviderType` 增 `OpenAIResponses`
- `ForgeSelf.Api/Services/AI/Providers/OpenAISdkAdapter.cs` — **新增**（~770 行）：chat/Responses 双协议出站适配（BC-1/2、U-6a~f、reasoningAsContentFallback）
- `ForgeSelf.Api/Services/AI/Providers/OpenAIResponsesProvider.cs` — **新增**：IAIProvider 实现，委派 adapter
- `ForgeSelf.Api/Services/AI/Providers/OpenAICompatibleProvider.cs` — chat 分支切 SDK（Anthropic 保留手写 BR-5）；BC-4 Custom 归一化
- `ForgeSelf.Api/Services/AI/AIProviderRegistry.cs` — 按 ProviderType 分派工厂（FR-6）
- `ForgeSelf.Api/Services/AIProviderService.cs` — 测试连接三路探针（SDK chat / SDK responses / Anthropic 手写）；status 0 →「无法连接」文案保留
- `ForgeSelf.Api/Services/AIService.cs` — FR-8：非流/流式回退路径出站切 SDK（10s CTS、GetFallbackResponse、log+静默停语义保留）
- `ForgeSelf.Web/src/types/aiProvider.ts` — `'OpenAIResponses'` 类型联合
- `ForgeSelf.Web/src/components/settings/AiProvidersPanel.vue` — typeTagMap 全键 + 下拉第四项
- 测试：`ForgeSelf.Api.Tests/Unit/OpenAIWireProbeTests.cs`（5 探针）、`Unit/OpenAISdkAdapterTests.cs`（**新增** ~350 行 16 测试）、`Unit/AIServiceTests.cs`（期望更新）、`Integration/RealLLMIntegrationTests.cs`（期望更新）、`AIModelFeatureTests.cs`（FakeHandler 复用）
- 前端测试：`src/__tests__/AiProvidersPanel.test.ts`（AC-8 四类型断言）、`e2e/ai-providers.spec.ts`（AC-UI-1 + U-1 字段断言更新）
- 文档：`docs/ai/pilot/2026-10-27-openai-responses-upstream/00-07`、`docs/02-features/001+004+api.md` 回写

## Build

Command: `dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj`

Result: PASS（来源等级：Verified）

```text
0 error / 0 warning（本批新增文件；宿主既有 CS8601/CS8602 等警告为存量基线，与本批无关）
```

## Unit Test（含集成 FakeHandler 套件）

Command: `dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~OpenAISdkAdapter|FullyQualifiedName~OpenAIWireProbe|FullyQualifiedName~AIServiceTests|FullyQualifiedName~RealLLMIntegrationTests|FullyQualifiedName~AIProviderTestConnection"`

Result: PASS（来源等级：Verified）

```text
已通过! - 失败: 0，通过: 76，总计: 76（filtered-final.log）
其中：OpenAISdkAdapterTests 16（含 AC-4 golden master / AC-5 URL Theory / U-1 钉板 max_completion_tokens /
AC-7 deepseek reasoning 回放 flag=true 提取 + flag=false 零成本）/ OpenAIWireProbeTests 5（U-2 roundtrip 放行）
/ AIServiceTests 15 / RealLLMIntegrationTests 4 / AIProviderTestConnection* ~36
```

## Integration Test（全量=中档门禁）

Command: `dotnet test ForgeSelf.Api.Tests`（全量，独跑）

Result: PASS-with-baseline（来源等级：Verified）

```text
已通过! - 失败: 10，通过: 3236，总计: 3246，持续时间: 12 m 1 s（fulltest-final2.log）
10 红对表：WorkflowPlanningIntegrationTests 6（TODO:102 登记）+ ScriptRunnerDi 1（TODO:101 ⑥）+
RepositoryScriptTests BOM 1（TODO:101 ⑤）+ ExtensionPointManagerTests 2（并行会话 ADR-004 在改文件）
= 登记基线 + 并行会话文件；本批新增红 = 0
注：第一跑（pwsh-a8927575a678466c）被我并发过滤跑杀 testhost 干废（日志只剩 4 例）→ 教训：
全量期间禁止并行跑测试/杀 testhost；已独跑重验
```

## E2E

Command: `cd ForgeSelf.Web && pnpm run test:e2e -- --grep "ai-providers|AI 提供方"`

Result: PASS（来源等级：Verified）

```text
12 passed (1.1m)（e2e-providers2.log）
第一跑 10 passed / 2 failed，两红均本批行为变化：
① max_tokens → max_completion_tokens（U-1 钉板落地，断言更新）
② 不可达上游失败文案（SDK status 0 未映射旧「无法连接」→ AIProviderService 补映射，文案契约保留）
含新增 AC-UI-1（类型下拉四项）断言
```

## Static Analysis

- `pnpm run check`：PASS（Verified）— 0 errors / 151 warnings（=既有基线，本批 0 新增）
- `pnpm run test`（vitest）：PASS（Verified）— 1035/1035（含 AC-8 新测试，较基线 +1）
- `pnpm run design:check`：PASS（Verified）— style-tokens 无新增裸色值 + compliance 无新增违反

## 走查（AC-10 · dev-stack 现场 Verified）

`scripts/dev-stack.ps1`（backend :7301 / frontend :7399，token 注入）逐项核对：

| 项 | 结果 | 证据 |
|----|------|------|
| AC-UI-1 类型下拉四项 | ✅ OpenAI / OpenAI Responses / Anthropic / Custom 全可见 | 快照 YAML + e2e 断言 |
| 新建 OpenAIResponses 提供方保存 | ✅ 卡片出现、类型标签「OpenAIResponses」渲染 | screenshots/e2e/walkthrough-responses-provider-card.png（走查过程截图，MCP 根限制未入库，快照为证） |
| 无登记模型 → 测试连接禁用 | ✅ 按钮 disabled（T018 规则） | 快照 |
| 不可达上游错误反馈 | ✅ toast「请求失败(504): 上游请求失败：由于目标计算机积极拒绝…」 | 快照 + walkthrough-unreachable-upstream-feedback.png（同上） |
| 终态（走查数据已清理） | ✅ 面板回到基线状态 | ForgeSelf.Web/screenshots/e2e/walkthrough-providers-panel-final.png |

## Screenshots

- `ForgeSelf.Web/screenshots/e2e/walkthrough-providers-panel-final.png`（走查终态）

## Known Limitations

- BC-3：SDK 对 `MaxTokens` 固定发 `max_completion_tokens`（U-1 实证）；仅认 `max_tokens` 的旧兼容上游会在测试连接阶段暴露（同 wire 探针先行）。不做 body 改写策略（= 回到手写协议）。
- U-6b：SDK 流式更新无 `.Id`，分片 id 用本地生成的 `chatcmpl-<guid>`（下游只做透传聚合，无影响）。
- U-6f：空 ApiKey 时适配器发占位符 `EMPTY`（SDK 要求非空 credential；键免鉴权上游忽略该头）。
- U-5b：FakeHandler 单体 StringContent 通路下 SDK SSE 读取器在 EOF 前滞留最后一个事件（测试垫一个后续事件解决；真实分块上游不受影响）。
- 手写版多模态「单消息多 content block」在 chat 侧由 SDK 等价表达；Responses 侧图片走 data URI（SDK 无 3 参重载）。
- assistant 消息同时带文本与 ToolCalls 时 SDK 无法表达为单条 → 拆两条（text message + tool-call message）；主流上游不受影响（对拍测试覆盖）。

## Unresolved Issues

无（本批验证全绿；基线红与并行会话红已在上方对表归位）。

## 验收编号索引

门禁按无连字符编号检索。等级仍以上文为准。

- AC1 AC2 AC3：`OpenAISdkAdapterTests.cs` 文件头写明覆盖 AC-1..AC-5，过滤集 16 测全绿（Unit Test 节）。
- AC4 AC5：同上，Unit Test 节已点名 golden master 与 URL Theory。
- AC6：过滤集中的 `AIProviderTestConnection*` 约 36 测全绿（测试连接三路）。
- AC7：Unit Test 节已点名 deepseek reasoning 回放。
- AC8：Static Analysis 节 vitest 1035/1035。
- AC9：Integration Test 全量对表，既有兼容路径无新增红。
- AC10：走查节 dev-stack 现场。

