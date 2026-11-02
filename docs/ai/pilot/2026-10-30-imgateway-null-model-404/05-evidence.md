# 05 Evidence：IM 网关收消息后 AI 补全 404（PC-013 复发修复）

> 时间窗：2026-10-30 21:10–22:00｜验证档位：**快档 + AIAgent 域过滤集 + 1 冒烟 e2e + dev-stack 起源码走查**（未触宿主源码树 `ForgeSelf.Api/**` 与 `scripts/**`，不触发中档；全量不对表理由见 §偏差）。

## 1 现象与真实边界证据（Verified）

| # | 结论 | 证据 |
|---|------|------|
| E1 | :51888 实例企微消息 → `MAF Run 失败 … 404 (Not Found)` → `补全失败` → `IM网关 AI 补全失败`，21:16:45 / 21:17:18 两条 | `C:\Users\12504\.forgeself\log\2026_10_30.log` L2476-2511（只读） |
| E2 | 上游请求模型 = `qwythos-9b-v2`：`[gpustack] 发送流式 Chat 请求（SDK），模型: qwythos-9b-v2` | 同日志 21:16:36 |
| E3 | 企微通道/会话映射模型均为 null | 直查 `ImGateway.db`（ReadOnly）：`ImGatewayChannel` Id=1 `BoundChatModelId=null`；`ImGatewaySession` Id=1 `ModelId=null`，SessionKey=`wecom:HeBingHong:HeBingHong` |
| E4 | 实例 appsettings 残留 `AI:ModelName=qwythos-9b-v2` | `D:\src\tools\OpenForgeSelf\versions\2.4.22.2610301421\appsettings.json` AI 节 |
| E5 | `qwythos-9b-v2` 不在启用模型目录 | 直查宿主 `ForgeSelf.db` AIModel 表：28 条（id 10-37），无此名；启用含 `gpustack:deepseek-v4-flash`(12)、`gpustack:qwen3_8_vision`(16) |
| E6 | gpustack 为默认 provider | 直查宿主 `ForgeSelf.db` AIProvider：gpustack `IsDefault=1`，Endpoint=`http://gpustack.luxvisions-inno.com/v1/chat/completions` |
| E7 | DB `ApiKey` 为 v2 密文，不可宿主外直用 | 实例与 dev 两库 keylen=111、前缀 `v2:`；直连探测 2 次（GET /models、POST 1-token）均 401 → 放弃宿主外解密（敏感边界），404 判定以 E2 运行日志为准 |

## 2 红灯（可重复复现，Verified）

- 测试：`ForgeSelf.Api.Tests/Plugins/AIAgent/ChatCompletionWritePathTests.cs` 新增 2 条：
  - `CompleteAsync_NoModelBound_UsesEnabledCatalogModel_NotStaleConfiguredName`——fixture：`IConfigurationService.GetAIConfig().ModelName="qwythos-9b-v2"`（复刻 E4）+ `ITurnTelemetryQuery` 目录只含 `fake:qwen3_8_vision`（复刻 E5 形态）+ 真实 `ChatModelResolver` 入 DI（同生产装配）。
  - `CompleteAsync_NoModelBound_EmptyCatalog_FailsExplicitlyWithoutUpstreamCall`——目录空（复刻 e2e 全新宿主形态）。
- 红灯输出（21:33，`--no-build` 重跑存 `.temp/tmp/red-run2.log`）：
  - `Assert.Equal() Failure: Strings differ  Expected: "qwen3_8_vision"`（实际发的是陈旧配置名 → 与生产 404 同构）
  - `Assert.False() Failure`（目录空仍「成功」调上游 → 静默降级复现）
  - 汇总：`失败: 2，通过: 2，总计: 4`

## 3 修复（差异面）

| 文件 | 变更 |
|------|------|
| `Plugins/AIAgent/Services/AIAgentService.cs` | +`_modelResolver` 字段 + 可选 ctor 参数 + `ModelResolver` 惰性属性（DI 注入优先，兜底 `new ChatModelResolver(_ctx)`）+ `ResolveChatModelOrFallbackAsync`；`RunAgentLoopAsync` / `RunAdHocCoreAsync` 入口：null/空 → 解析链 → 仍空 → `error` 事件收束（不建 agent、不发上游） |
| `Plugins/AIAgent/Controllers/AIChatController.cs` | ctor 可选 `IChatModelResolver?`（插件自有服务，生产 DI 可注）；`ResolveChatModelAsync` 助手；同步端点解析不到 → 503 明确文案；流式端点 → SSE `error` 事件；显式模型请求原样透传（语义不变） |
| `ForgeSelf.Api.Tests/.../ChatCompletionWritePathTests.cs` | CompletionFixture +可选 `configuredModelName`/`catalog` 参数（mock IConfigurationService/ITurnTelemetryQuery，DI 注册 ChatModelResolver）；新增 2 条守卫（G2 入口级） |

未触碰：`ChatModelResolver.cs`、`ResolveModelId`/`ResolveProvider` 语义、IM 网关插件、`RunOrchestratorService`、`BuiltInAgentExecutionService`（既有接链复跑确认）。

## 4 绿灯与回归（Verified）

| 档 | 命令/范围 | 结果 |
|----|-----------|------|
| 修复目标集 | `dotnet test --filter FullyQualifiedName~ChatCompletionWritePathTests` | **4/4 绿**（2 新守卫 + 2 既有写路径门禁），21:47 |
| PC-013 既有守卫 | 同上集含 `ChatModelResolverTests`（G1） | G1 单独再跑于 29 例集，绿 |
| 域过滤集 | `--filter FullyQualifiedName~ForgeSelf.Api.Tests.Plugins.AIAgent` | **105/120 绿**；15 红全部为 `ProjectWorkspaceDefaultRootTests`（他会话 68df669 批基线红，对表 `docs/ai/pilot/2026-10-30-release-version-from-csproj/05-evidence.md:68`，0 条归本批） |
| 相邻面 | `--filter Orchestrator|GoalPursuit|ChatControllerInvariant|ChatModelResolver` | **29/29 绿** |
| 构建 | `dotnet test` 编译宿主+测试工程 | 无 error（warning 均既有） |
| 冒烟 e2e | `pnpm run test:e2e -- e2e/plugins/ai-agent/ai-agent.spec.ts`（真实后端，globalSetup 全新宿主） | **6/7 过**；1 挂（「计划驱动执行（029）」期望发任务后出现 Run 行）→ **判责：基线红，非本批**——e2e 宿主日志 `.temp/e2e/wt-b26d4625/publish/Log/2026_10_30.log` 21:44:32 `[ChatModelResolver] 启用模型目录为空` + `[RunOrchestrator] 无可用对话模型，拒绝创建执行`：全新宿主无模型目录，2026-10-25 批（非本批，`RunOrchestratorService` 未触碰）的拒绝创建门使该用例必挂；用例断言未同步该行为。挂点路径（POST /api/ai-agent/runs → RunOrchestrator）不经过本批 3 个改动文件 |
| dev-stack 走查 | 起源码前后端（含重建=修复后代码），浏览器走 AIAgent 聊天 | 见 §5 |

## 5 起源码本地走查（插件门禁③，Verified）

- 环境：`scripts/dev-stack.ps1`（重建后启动；端口/令牌 `.temp/dev-stack.json`）；dev 库含 default provider（gpustack）+ 启用模型 `gpustack:qwen3_8_vision`（21:21 既有成功聊天佐证上游可用）。
- 场景 A（修复主路径，null 模型）：AIAgent 页面**不选模型**直接发消息 → 修复前行为 = `ResolveModelId(null)`→`qwythos-9b-v2`→上游 404；修复后期望 = 解析链命中 `gpustack:qwen3_8_vision` → 正常回复。
- 场景 B（显式模型回归）：下拉选 `qwen3_8_vision` 发消息 → 正常回复（显式透传语义未变）。
- 截图：`ForgeSelf.Web/screenshots/dev-stack/2026-10-30-imgateway-null-model-404/`（场景 A/B 各一张）。
- 结果与日志摘录见 §7（若走查受阻，受阻事实原样记录，不以「应能通过」充当结论）。

## 6 偏差与不确定性

- **中档未跑（偏差已登记）**：本批改动面 = 插件后端（`Plugins/AIAgent/**`）+ 插件测试，未触 `ForgeSelf.Api/**` 宿主源码、`scripts/**`、共享测试基建（仅向既有测试文件**追加**用例与 fixture 可选参数，`ChatCompletionWritePathTests` 全绿验证无破坏）→ 按 §5.6 触发表走快档；e2e globalSetup/配置未触碰（跑的是既有 spec）。
- **上游直连 404 探测未做成**：宿主密钥为 v2 密文，宿主外不解密（§1 E7）；404 判定依赖 E2 运行日志（请求确实以该模型名发出 + 上游确实回 404，SDK 异常透传 HTTP 状态）+ E5 目录缺席双证。**Unknown 项**：上游对 `qwythos-9b-v2` 的原始响应体未取得（401 无法以密钥复现）——不影响根因链成立。
- **IM 网关真实企微消息验证不可行**：WeCom 回调指向 :51888（用户实例），本批不动运行实例、不发企微消息；修复效果以单元红灯→绿灯 + dev-stack 真实聊天走查（同一条 IChatCompletion 入口链）佐证，**:51888 实测复验留待用户更新版本后**（运行实例只读复验属发布后步骤⑤）。
- **dev-stack 停/启**：为解 DLL 锁按当日惯例 `-Stop`（已在 TODO 协作交接登记，:51888 用户宿主全程未动），走查后已恢复（重建含修复）。

## 7 走查执行记录（22:01–22:03，Verified）

- 环境：dev-stack 重建后实例（:7301 后端 pid 109412 / :7399 前端 pid 26784，21:52:42 起，`.temp/dev-stack.json`；**运行代码含本批修复**）。dev 库 provider=default（gpustack 同上游），启用模型 `default:qwen3_8_vision`；dev 宿主 appsettings 同样残留 `AI:ModelName=qwythos-9b-v2`（与生产同构）。
- **发现（记录为走查事实）**：web UI 在模型目录非空时自动选中首个模型（`AiAgentView.vue:388-394`），故「web 端 null 模型」仅在目录为空时可达；真实 null 调用方 = IM 网关（单元红灯已覆盖）——走查场景据此调整为 A（UI 显式模型全链路）+ B（HTTP 层无模型请求，与企微同构）。
- **场景 A（显式模型回归）**：`/ai-agent` 页面（插件徽标 v1.9.8）新会话发送「你好，请用一句话介绍你自己。」→ 22:02 正常回复；后端日志 22:02:00 `[ChatModelResolver] 使用调用方指定模型：default:qwen3_8_vision`（显式透传语义未变）。截图 `ForgeSelf.Web/screenshots/dev-stack/2026-10-30-imgateway-null-model-404/scenario-A-explicit-model-reply.png`（读图：三栏正常、消息与回复成对渲染、无错误条）。
- **场景 B（修复主路径，WeCom 同构）**：`POST /api/ai-agent/chat`（Bearer dev 令牌）body **仅** `{"message":"IM网关同构测试：请只回复两个字：收到"}`（无 chatModelId）→ **HTTP 200**，回复「收到」，usage 4465 tokens。后端日志链（GBK 解码 `.temp/dev-stack-backend.log`）：
  ```
  22:02:38.092 [AIAgentPlugin] 收到聊天请求，SessionId: ffc2eb4a…, 消息长度: 19, 模型: (默认), Agent: (默认)
  22:02:38.100 [ChatModelResolver] 宿主默认模型 qwythos-9b-v2 不在启用模型目录中，改用目录首个启用模型
  22:02:38.100 [ChatModelResolver] 使用模型目录首个启用模型：default:qwen3_8_vision
  22:02:43.153 [MafTaskLoopAgent] 出口工具 task_complete 收束回合 SessionId=ffc2eb4a…, TurnId=39e3b6d7
  ```
  修复前同请求形态（:51888 21:16 实证）= 陈旧 `qwythos-9b-v2` 直发上游 → 404；现被解析链拦截并命中目录模型。**与生产缺陷逐点同构，修复在真实宿主 + 真实上游生效。**
- 测试数据清理：走查仅产生 1 条会话消息（dev 一次性数据目录，无需清理）；:51888 与企微未触碰。

## 8 机器校验（问题类闭环门禁，Verified 22:05）

- `pwsh scripts/check-problem-classes.ps1` → **OK：18 个 PC 签名/allowlist/guards 全部通过**（含 PC-013 新 guard S3/复发计数）。
- `dotnet test --filter ProblemClassRegistryTests` → **5/5 绿**（台账完整性/悬空引用校验，含新 case 归档）。
