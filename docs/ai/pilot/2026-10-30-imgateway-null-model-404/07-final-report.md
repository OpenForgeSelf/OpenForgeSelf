# 07 Final Report：IM 网关收消息后 AI 补全 404（imgateway-null-model-404，2026-10-30）

## 验证结果（Validation）与审查（Review）

- **红灯（复现）**：`ChatCompletionWritePathTests` 新增 2 条（`CompleteAsync_NoModelBound_UsesEnabledCatalogModel_NotStaleConfiguredName` / `…_EmptyCatalog_FailsExplicitlyWithoutUpstreamCall`）修复前双红（`Assert.Equal` 期望 `qwen3_8_vision` 实发陈旧名 / `Assert.False` 空目录仍"成功"调上游），`.temp/tmp/red-run2.log`（2 失败/2 通过）——与生产 404 逐点同构。
- **绿灯（修复）**：同集 **4/4 绿**（含既有 2 条写路径门禁）。
- **回归**：AIAgent 域过滤集 **105/120**（15 红逐条对表 = 他会话 68df669 批 `ProjectWorkspaceDefaultRootTests` 基线红，0 条归本批）；相邻面（Orchestrator/GoalPursuit/ChatControllerInvariant/ChatModelResolver）**29/29**。
- **冒烟 e2e**：`e2e/plugins/ai-agent/ai-agent.spec.ts` **6/7**；唯一红「计划驱动执行（029）」判责为 **2026-10-25 批基线漂移**（e2e 宿主日志 21:44:32 `[RunOrchestrator] 无可用对话模型，拒绝创建执行`——全新宿主无模型目录 + 10-25 拒绝创建门；挂点路径 POST /runs → RunOrchestratorService 不经过本批 3 文件；已登记 TODO 移交）。
- **起源码走查（插件门禁③）**：dev-stack 重建实例（含修复）：A=UI 显式模型回复正常；B=`POST /api/ai-agent/chat` **不带 chatModelId**（企微同构）→ **200「收到」**，后端日志三行链（`宿主默认模型 qwythos-9b-v2 不在启用模型目录中` → `使用模型目录首个启用模型：default:qwen3_8_vision` → task_complete 收束）——**修复在真实宿主+真实上游生效**。截图 `ForgeSelf.Web/screenshots/dev-stack/2026-10-30-imgateway-null-model-404/`。
- **机器校验（问题类闭环门禁）**：`check-problem-classes.ps1` **OK（18 PC）**；`ProblemClassRegistryTests` **5/5**。
- **未跑**：中/深档（未触宿主源码树、`scripts/**`、e2e 基建，§5.6 触发表不要求）；前端三件套（零前端变更面）。
- **Review**：八问 + Final Decision=**APPROVED** 见 [06-review.md](06-review.md)；闸门2 用户验收通过后 git 提交方为闸门3 产物。

## 交付状态

- **状态**：✅ 实现与验证完成（**闸门2 待用户验收**；验收通过前不提交 git）
- **目标达成**：用户指令三段全闭环——**测试复现**（真实边界证据链 + 可重复红灯）→ **修复**（三类入口接解析链）→ **再测**（红→绿 + 域回归 + e2e + 真实走查）。
- **运行实例现状**：:51888（2.4.22.2610301421）仍带缺陷行为，**修复随下次发布生效**（本批未发布，按 §8.5 等用户点头）；dev-stack :7301/:7399 已跑上修复代码。

## 主要变更

| 文件 | 变更 |
|------|------|
| Plugins/AIAgent/Services/AIAgentService.cs | `RunAgentLoopAsync`/`RunAdHocCoreAsync` 入口接 `IChatModelResolver` 有序链；解析不到 error 事件明确收束（+惰性 resolver 属性，DI 优先） |
| Plugins/AIAgent/Controllers/AIChatController.cs | 两聊天端点先解析后建 options；解析不到 503/SSE error（显式请求透传语义不变） |
| ForgeSelf.Api.Tests/Plugins/AIAgent/ChatCompletionWritePathTests.cs | CompletionFixture 扩配置/目录 mock；新增 2 条入口级守卫（PC-013 G2） |
| docs/11-troubleshooting/ | PC-013（复发#1 + guard S3 + lens 增补）+ case 2026-10-30-im-gateway-unbound-model-404（新）+ 三处索引 |
| docs/ai/pilot/2026-10-30-imgateway-null-model-404/ | mini-task（AC 全勾）+ 05 + 06 + 07 |

## 问题类归档（技能 problem-class-closure）

- **PC-013 二次复发**（2026-10-30）：入口级逃逸——IM 网关路径 null 模型直发陈旧 `AiConfig.ModelName`。守卫补位：**G2 入口级** `ChatCompletionWritePathTests`（IChatCompletion 路径：null→目录解析 / 空目录零上游请求）；lens 增补「null 透传形态」问句；消费方清单补 `RunAgentLoopAsync`/`RunAdHocCoreAsync`/`AIChatController`×2。case：`2026-10-30-im-gateway-unbound-model-404`。
- 机器校验：Registry 5/5；check-problem-classes.ps1 18 PC 全绿。

## 重构提案

- **R0（已执行）**：入口接链为守卫式最小修复，不重构。
- **R1（入 PC-013 §6 待批，须另立任务）**：`CreateAgent` 的 `AgentOptions.ModelId` 为空改为编译期不可表达（required + 入口解析），把「新增入口忘接链」整类消灭；涉及 `IAgentRegistry`/`CreateAgent` 签名链，走 R2 评审。

## 悬空教训清单

**空**。三项后续（R1 提案、e2e 029 基线用例同步、:51888 运行实例复验）均已落 TODO.md（⬜/🔄 含来源与优先级），无未登记悬空项。

## 遗留与风险

- 上游对 `qwythos-9b-v2` 的原始 404 响应体未取得（宿主密钥为 v2 密文，不宿主外解密）——404 判定由运行日志（请求模型名+SDK 状态）+ 目录缺席双证，**Unknown 如实登记**。
- 企微真实消息级复验须 :51888 更新后进行（运行实例只读复验=发布后步骤⑤）；当前以同链路（IChatCompletion 入口）单测 + dev-stack 同构请求走查佐证。
- dev-stack 本轮为解 DLL 锁停过一次（21:38–21:52，已在协作区登记与他方回帖确认），现已重建恢复并跑上修复代码。
