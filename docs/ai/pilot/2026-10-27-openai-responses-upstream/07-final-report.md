# AI-Native Pilot Result（最终汇报）

> 任务结束强制格式｜状态只报事实（AGENTS.md §10.4/§10.5）。

## 1. Repository Understanding

我确认了：宿主 ForgeSelf.Api（.NET 10 + NewLife.XCode/SQLite）已有 UnifiedAI 网关（入站三风格 → UnifiedChatRequest/Response/StreamChunk → 出站 IAIProvider），出站此前仅手写 OpenAI chat-completions wire（OpenAICompatibleProvider）+ Anthropic 手写；前端设置页类型下拉原两值；openai-dotnet 2.10.0 经 Microsoft.Agents.AI.OpenAI 1.6.1 已在依赖树（MAF 传递，零新增 NuGet）。

## 2. Selected Task

PILOT-074：OpenAI Responses API 上游支持（新 ProviderType → OpenAIResponsesProvider → SDK Responses 协议）+ 出站单格式漏斗收敛——所有手写 OpenAI chat 风格出站统一切 openai-dotnet SDK（AIService 回退路径 FR-8，U-2 实证放行）。用户输入5 拍板目标闭环：「三种类型配置 → 网关三风格都能用 → 用哪个接口走对应协议 → 按模型路由 → 对应转换并正确发请求」。

## 3. Changed Files

- 新增：`ForgeSelf.Api/Services/AI/Providers/OpenAISdkAdapter.cs`（双协议适配 ~770 行）、`OpenAIResponsesProvider.cs`
- 修改：`AIConfigModels.cs`（枚举+值）、`OpenAICompatibleProvider.cs`（chat 分支切 SDK/BC-4）、`AIProviderRegistry.cs`（FR-6 分派）、`AIProviderService.cs`（三路探针 + status0 文案）、`AIService.cs`（FR-8 回退迁移）、`types/aiProvider.ts`、`AiProvidersPanel.vue`
- 测试：`OpenAISdkAdapterTests.cs`（新增 16）、`OpenAIWireProbeTests.cs`（5）、`AIServiceTests.cs`、`RealLLMIntegrationTests.cs`（期望更新）、`AiProvidersPanel.test.ts`（AC-8）、`e2e/ai-providers.spec.ts`（AC-UI-1 + U-1 断言）

## 4. Validation

Build: PASS（0 错误；Verified）

Unit Test: PASS——过滤集 76/76（adapter/probe/AIService/RealLLM/TestConnection；Verified）；**全量 3246 例 = 3236 通过 / 10 红，10 红全部对表为登记基线（TODO:101/102 共 8）+ 并行会话在改文件（2），本批新增红 = 0（Verified，fulltest-final2.log）**

E2E: PASS——ai-providers 12/12（含新增 AC-UI-1；Verified，e2e-providers2.log）；前端 check 0err/151warn=基线、vitest 1035/1035、design:check PASS（Verified）

## 5. Evidence

`05-evidence.md`：五类验证全 Verified；关键实证——U-1 钉死 `max_completion_tokens`（BC-3 落地）、U-2 ModelReaderWriter 往返保留 `reasoning_content`（FR-8 门放行）、U-5b FakeHandler EOF 滞留（垫事件解决，真实上游无影响）；走查 5 项逐条核对（AC-UI-1 下拉四项 / 保存+标签 / 无模型禁测 / 不可达反馈 / 数据清理），截图 `ForgeSelf.Web/screenshots/e2e/walkthrough-providers-panel-final.png`。

## 6. Review

`06-review.md`：**APPROVED**（八问全过；Requirement/Scope/Test/Architecture 全 PASS；Risk L1）。

## 7. Risk

L1——BC-3：SDK 固定发 `max_completion_tokens`，仅认 `max_tokens` 的旧兼容上游会在测试连接阶段暴露（同 wire 探针先行，不做 body 改写=不回手写协议）；错误文案细节有 e2e 契约锚定。

## 8. Problems Found

- 旧测试假响应 PascalCase 不 wire 忠实（手写解析器宽松掩盖）→ 本批迁移暴露，已修（camelCase 化）
- 空 ApiKey 在 SDK 构造期抛异常（手写版发「Bearer 」空参）→ 适配器占位符「EMPTY」（U-6f）
- SDK 传输层事后释放请求体 → 测试须发送现场读体（RealLLM 修法）
- 全量 dotnet test 期间并发跑过滤测试/杀 testhost 会互杀（两会话同款症状）→ 教训入日记
- testhost 残留锁 DLL（MSB3027）反复出现 → 跑测前清 testhost 成固定前置

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS |
| Intent → Spec | PASS |
| Spec → Plan | PASS |
| Plan → Code | PASS（长文本写入两次产生残缺标识符，靠读回+小步 edit 修复） |
| Code → Test | PASS |
| Test → Evidence | PASS（教训：全量独跑，禁并发测试） |
| Evidence → Review | PASS |

## 10. 问题类归档

PC-013（空响应当成功，既有类）：本批 FR-3/FR-8 的 failed/空输出→失败或降级语义逐条等价，AC-3/AC-7 覆盖，无新增 case 文件（既有类在本次实施中的守卫延续，见 05-evidence Known Limitations）。本批测试期望修复（假响应形状/空 key/释放请求体）属「迁移引发的测试基建不 wire 忠实」一次性伴随面，随本批 golden master 断言固化，不另立 PC（理由：无独立缺陷逃逸路径，修复已由 AC-4 对拍回归锚定）。

## 11. 重构提案

R0——不重构。理由：本批本身即「手写协议 → SDK 统一」的目标性收敛；出站接缝已收敛为单一适配器，无新增跨模块债务。**输入9 终态（2026-10-28 17:59 用户定标「不想手写，尽量用现成，正确能用是底线」）**：Anthropic 出站亦统一 SDK（官方 OpenAI 兼容层，零新增 NuGet），R0 结论加强——出站接缝现为**唯一**适配器、**零**手写协议残留（模型列表除外，见 05 追记 Known Limitations）。

## 12. 悬空教训清单（必须为空）

（空——全部教训已落 05/06 Known Limitations、日记、TODO 或问题类归档）

## 14. 输入9 收口补记（2026-10-28 17:59 定标，终态）

- 标准（用户原话）：「不想手写，太麻烦，不管是维护还是测试，尽量用现成的。但最重要的是要正确能用」。
- 落地：恢复输入6 全部实现——① 适配器 `DeriveAnthropicCompatBaseUrl`（4 形态归一）+ `CreateChatClient` 分流；② provider 去守卫全走 SDK（324 行，手写 chat 体/死 DTO 清除，模型列表保留）；③ service 探针全类型 SDK 化（ExtractContent/ReadMessageText 删）；④ 测试 79/79（Anthropic 兼容层 URL 理论 4+5 例回归）。
- 过程事故与恢复：重建期间 provider 曾被空写（长写损坏连锁），已从 git HEAD 一次性确定性重建（全部边界模式定位 + 写前行数守卫）——「HEAD 机械恢复」策略二次实证。
- 已知限制（同输入6）：Claude 4.7+ 默认温度约束（上游模型行为，任何路线一致）；workspace-id 头未做；Anthropic 无 e2e 用例（既有）。
- 状态：**闸门2 验收通过（2026-10-28 19:0x 用户确认「按方案：全类型出站统一 SDK，Anthropic 走官方 OpenAI 兼容层来做」）**；闸门3 提交待用户明示。
