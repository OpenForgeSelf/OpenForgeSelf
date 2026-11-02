# Evidence — B5 旁路收敛

> 阶段：Stage 7｜只记录实际发生的事情。来源等级：Verified（亲自跑过，有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。
> 任务：`2026-11-02-b5-legacy-cleanup`｜执行者：verify-dev｜门禁档位：中档全量 dotnet test + 深档全量 e2e（02-spec §9 AC9）。

## Task

B5 旁路收敛：① 删 ReactLoopAgent（唯一运行时 = MafTaskLoopAgent）② UseMafRuntime/TaskLoopMode 开关收敛默认常开 ③ AgentHub 启发式旁路裁决后删除（lead seq8 拍板 A=删除）+ UI 死代码。IM 审批推送后置不动。

## Changed Files

**删除（生产行为面）**：
- Plugins/AIAgent/Services/ReactLoopAgent.cs（949 行）
- Plugins/AIAgent/Services/AgentCoordinatorService.cs（505 行）
- Plugins/AIAgent/Services/AgentExecutorService.cs（303 行）
- ForgeSelf.Api.Tests/Plugins/AIAgent/ReactLoopAgentTests.cs（512 行）/ ReactLoopTaskLoopTests.cs（99）/ ReactLoopInboxGateTests.cs（138）/ ReactLoopExitAndSuspendTests.cs（212）

**新增**：
- Plugins/AIAgent/Services/AgentTurnRuntime.cs（共享契约纯迁移：AgentTurnRuntime + PreStep*/AgentRequest*/TurnStopping）
- ForgeSelf.Api.Tests/Plugins/AIAgent/TestAgentFixtures.cs（共享测试脚手架）
- ForgeSelf.Api.Tests/Plugins/AIAgent/MafEngineGateTests.cs（AC2/AC5/AC8 禁用名单机器守卫 + 引擎门禁）
- ForgeSelf.Api.Tests/Plugins/AIAgent/MafInboxGateTests.cs（AC3 inbox R2/转向/中止）
- ForgeSelf.Api.Tests/Plugins/AIAgent/MafExitAndSuspendTests.cs（AC3 exit/suspend/取消）
- ForgeSelf.Api.Tests/Unit/ForgeSettingLegacyKeyCompatTests.cs（AC6 兼容读）
- docs/ai/campaigns/2026-11-02-maf-runtime-hardening/b4-qa-audit.md（qa-gate 产出，非本批）

**修改**：41 文件（git diff --stat：+187/−3217）——AIAgentPlugin.cs（DI 删 2）、AgentsController.cs（删 6 端点 + CoordinateRequest）、AgentModels.cs（87 行收敛）、Agents.cs（删 UseMafRuntime/TaskLoopMode）、MafTaskLoopAgent.cs（taskLoop 衍生 + 注释）、AIAgentService.cs（工厂恒 MAF）、AgentRuntimeRegistry.cs（模式匹配）、6 编排器系测试换装、注释级引用 13 文件、ForgeSelf.Web 三文件（死代码删除）、not-taken-decisions.md（055）、problem-classes/PC-013 + case 2026-10-25（守卫路径迁移）。

## Build

Command（可重复）：

```powershell
dotnet build ForgeSelf.Api.Tests -o "$PWD\.temp\bin-b5s8" --nologo -v q
```

Result: PASS（来源等级：Verified）

```text
build exit: 0（0 error；日志 .temp/build-b5s8.log）
前端：pnpm run check → exit 0（0 error / 166 warnings 存量风格项；组件守卫 PASS）
```

## Unit / Integration Test（AC 映射表，AC token 与 02-spec §9 逐字一致）

总门禁：`dotnet test "$PWD\.temp\bin-b5s8\ForgeSelf.Api.Tests.dll" --no-build`（中档全量，OutDir 动线）

Result: PASS（判责后零新增红；详见「全量对表」节）（来源等级：Verified）

```text
失败: 73，通过: 3354，已跳过: 0，总计: 3427，持续时间: 15 m 9 s - ForgeSelf.Api.Tests.dll (net10.0)
```

| AC | 判据（02-spec §9 原文 token） | 证据 | 等级 |
|----|------------------------------|------|------|
| **AC1** | 共享契约先行迁移——AgentTurnRuntime + PreStep*/AgentRequest*/TurnStopping 迁独立文件，编译绿且 MafTaskLoopAgent 既有过滤集全绿（零行为变化） | AgentTurnRuntime.cs 新文件（纯迁移 + `using ...MafLoop`）；MAF 过滤集 24/24（bin-b5s3，`.temp/test-maf-b5s3.log`）；全量构建 0 error | Verified |
| **AC2** | ReactLoopAgent 删除——类/工厂分支/注册表模式匹配/类型引用全仓 grep 零残留（文档历史记载除外），测试 ReactLoopTaskLoopTests 删除 | ReactLoopAgent.cs 已删（git D）；MafEngineGateTests.LegacyContract_Removed 禁用名单守卫绿（banned=["IAgentLoop","InMemoryAgentLoop","TurnEvent","ReactLoopAgent","UseMafRuntime","TaskLoopMode","AgentCoordinatorService","AgentExecutorService"]，扫描 Abstractions/Core/Api/Plugins，注释行排除）；git grep ReactLoopAgent 仅余历史注释与本文档 | Verified |
| **AC3** | 引擎语义门禁迁移——ReactLoopAgentTests/ReactLoopInboxGateTests/ReactLoopExitAndSuspendTests 核心判据由 MafTaskLoopAgent 驱动全绿（inbox R2/exit/suspend/cancel/状态机） | MafInboxGateTests 3 条 + MafExitAndSuspendTests 3 条 + MafTaskLoopAgentTests 既有 16 条全绿（106/106 合批 `.temp/test-harness-b5s3.log`）；判据逐条迁移（空响应 Failed/turn-stopping 转向/出口三件套/看门狗 Suspended/取消 Abort） | Verified |
| **AC4** | 编排器系测试换装——PlannedRunUnifiedLoopGates/RunVerifyGateTests/GoalPursuitTests/PlanGeneratorScratchSessionQaTests/GoalRunSseEvidenceTests/InboxR2QaAdversarialTests 换 MafTaskLoopAgent 装配全绿（A3 零事件/run verify/goal/规划 QA 断言不回退） | 6 文件换装完成（CreateAgent 返回 MafTaskLoopAgent + runFlowTools 装配 + 显式 ToolDefinitions）；合批 106/106（含 RunReceiptTests 6 + MafApprovalLoopTests 5 + AgentLoopAndInboxTests 等） | Verified |
| **AC5** | 开关删除——UseMafRuntime/TaskLoopMode 属性与全部赋值/消费点删除，CreateAgent 恒 MafTaskLoopAgent，全仓 grep 零残留 | Agents.cs 两属性删除（git diff）；AIAgentService.BuildAgentOptions 两行删除（:332 注释替代）；AIAgentService.cs:417 工厂恒 MAF；grep 仅余 MafTaskLoopAgent.cs 历史注释 + 守卫禁用名单 + 本文档 | Verified |
| **AC6** | ForgeSetting.config 兼容读——含 useMafRuntime/taskLoopMode 残留键的配置反序列化不崩不生效（测试锁行为） | ForgeSettingLegacyKeyCompatTests 2/2 绿（provider 级 Load 不抛 + 已知键绑定 + 反射复核属性不存在 + Save 回写清洗残留键） | Verified |
| **AC7** | AgentCoordinator 裁决留痕——推荐 + 理由 + lead 拍板原文归档 `agenthub-adjudication.md`；拍板前 AgentCoordinator 代码零改动（git diff 佐证） | agenthub-adjudication.md 已归档（§3 = lead seq8 原文 A=删除）；拍板前零触碰由 git 历史佐证（AgentCoordinatorService.cs拍板 diff 才出现 D） | Verified |
| **AC8** | ③ 执行（拍板=删除时）——Coordinator/Executor/4 端点/DI/待删模型 + UI 死代码删除，全仓 grep 零残留，not-taken 记录落盘 | 三服务文件删除 + AgentsController 删 coordinate/execute/handle/instances/instances/{id}/execute-task + AgentModels 收敛至 87 行（AgentType/AgentStatus/AgentPersonality/AgentDefinition/AgentWorkflowRef 保留）+ UI agentApi 六方法/store 编排面/types 六类型删除；not-taken-decisions.md 055 落盘 | Verified |
| **AC9** | 门禁——中档全量 dotnet test 对表零新增红；深档全量 e2e（e2e-testing SOP，4 worker，空输出目录）通过；构建 0 错误 | 中档：3427 总/3354 过/73 败逐族归属零未归属红（见下「全量对表」）；深档 e2e：151 过/79 败逐名四族判责零 B5 归属（见「E2E」节）；构建 0 error | Verified |

## 全量对表（AC9 中档，73 红逐族归属）

基线 = `docs/ai/campaigns/2026-11-02-maf-runtime-hardening/qa-baseline.md`（89 红）+ b4-qa-audit.md（44 OutDir 伪红机制表）。

| 族 | 本次红 | 归属 | 证据 |
|----|--------|------|------|
| ProjectWorkspaceDefaultRootTests | 15 | 基线稳定红（TODO P2 已登记，68df669 批） | 同名单同数 |
| WorkflowPlanningIntegrationTests | 6 | 基线稳定红 | 同名单同数 |
| ScriptRunnerDiIntegrationTests | 1 | 基线稳定红 | 同名 |
| ImGatewayTests | 18 | 基线环境红（PC-014 族，隔离绿先） | 名单同（中文用例名本次清单干净） |
| MemoryTimestampGuardTests | 3 | 基线环境红（隔离绿先例） | 同名单 |
| ChatControllerIntegrationTests | 16 | B4 审计 OutDir 伪红（测试宿主引导失败→DI 容器空；B4 同族 13，漂移 +3） | 错误正文同模式 |
| ChatControllerInvariantTests | 4 | B4 审计 OutDir 伪红（同上 Invariant 4） | 同族同数 |
| RealLLMIntegrationTests | 4 | B4 审计 OutDir 伪红（同上 RealLLM 4） | 同族同数 |
| AIProviderRegistryTests | 1 | OutDir 伪红（XCode 数据根；B4 同族 5 现 1） | **隔离复跑绿**（test-isolate-b5s8.log 34 过） |
| ChatProjectionRaceTests | 1 | OutDir 伪红（404=宿主引导缺路由，B4「其余 7 同模式」类） | 隔离仍红（OutDir 布局），与 B4 §5-1 残差同格 |
| ChatCompletionWritePathTests | 1 | OutDir 伪红（源码扫描根漂移，B4 §3 同款 Assert.NotEmpty） | 隔离仍红（OutDir 布局），同残差格 |
| XCodeTestFixtureIronRuleTests | 1 | OutDir 伪红（扫描根定位失败，B4 §3 同款 Assert.NotNull） | 隔离仍红（OutDir 布局），同残差格 |
| PersistentSessionStoreTests.Di_Resolves | 1 | OutDir 伪红（装配失效形态，B4 §3 同款；承 B4 §5-1 残差：标准 bin 结论待锁窗口） | 隔离仍红（OutDir 布局） |
| ProblemClassRegistryTests | 1 | **B5 同根因新红（本批唯一）→ 已修**：PC-013 守卫路径指向已删除的 ReactLoopAgentTests.cs → 台账迁 MafTaskLoopAgentTests.cs（docs/11-troubleshooting，纯文档）→ 隔离 5/5 绿 | 错误原文「PC-013 active 守卫路径不存在」；修复后 `.temp/test-pc13fix.log` 5/5 |

- 基线有、本轮未显现（环境漂移，同 B4 观察）：TodoTracker 32 + TodosControllerTests 7 + ImGateway.DbConfig 4 + CancelAsync flake 1 + ForgeConfig 1。
- **判责结论：零未归属红**。B5 同根因 1 条（PC-013 台账路径）已当批修复并隔离验证。
- 残差（承 B4 §5-1，不新增）：4 条源码扫描/DI 守卫类（ChatCompletionWritePath/XCodeIronRule/PersistentSessionStore.Di/ChatProjectionRace）的标准 bin 结论待 dev-stack 锁窗口（重建标准 bin 需停 :7301，属 lead 权限），与 B4 同格 carry over。

## E2E（AC9 深档）

Result: PASS（判责后零 B5 写面归属红；来源等级：Verified，`.temp/e2e-b5s9.log` + `.temp/pw-out-b5s9/` 现场）

```text
79 failed / 4 skipped / 10 did not run / 151 passed (8.5m)，4 worker，空输出目录 .temp/pw-out-b5s9
总用例 244（对照基线 145/75/4/1，总 225——基线仅有汇总数字留档，无逐名清单；
总用例漂移 +19 系 031 万能工具网关 / quick-links 插件等 spec 在该基线采集后新增）
```

79 败逐名分类（四族，判责依据 = 错误正文 + 现场快照 + 源码对照，`.temp/e2e-b5s9-failed.txt` 全名单）：

| 族 | 数量 | 判责 | 关键证据 |
|----|------|------|----------|
| LM Studio 不可达（真实 LLM 前置缺失） | 13 | 环境红 | 探针实证 `localhost:1234` 连接拒绝（本机实测 00:59）；agent-loop-autonomous 4 错误原文直指「由于目标计算机积极拒绝 (localhost:1234)」；agent-execute 3 + tool-gateway-031 4 = `/api/ai-agent/chat 应 200 实际 500`（上游不可用→错误路径）；ai-provider-chat 2（fetch-models 真拉取失败） |
| 陈旧断言（spec 钉旧版页面结构/文案） | 34 | 基线族红（陈旧 spec，非产品缺陷） | chat.spec 28（旧 Chat 页选择器 .input-textarea/.menu-button）；ai-agent 宿主 spec 3（等 `.panel-center`/旧 placeholder——error-context 快照实证页面已按 029 重设计且**功能完整渲染**：欢迎语/Agent 列表/聊天对话框/执行记录入口全在）；ai-agent 插件 spec 2（等 `.aed__mode-opt` 文案「计划驱动」，现文案「开放任务/计划任务」——源码 AgentEditDialog.vue:59-67 与快照双证）；plugin-remote-view 1（等旧 `.chat__textarea`，快照实证**插件远程加载链路本身正常**——插件页完整渲染）；session-management 1（跨 spec 测试数据污染：列表被 agent-loop/tool-gateway 残留 Agent 挤占） |
| 可见性超时/共享后端状态 | 32 | 环境红（4-worker 页面加载超时/共享后端污染族，与基线「多为环境依赖」一致） | app 6 + todo 5 + quick-links 插件 5 + update-panel 3 + chat-records 3 + design-system 3 + 单条 7（plugin-store/agent-hub/home/im-gateway/sems/quicklinks 宿主/quick-links 其余） |
| B5 写面归属 | **0** | — | 插件 web 零调用被删端点（grep coordinate/execute-task/instances/handle 仅命中流回调 handlers）；被删面唯一 UI 消费者（宿主 ForgeSelf.Web agentApi/store）已同批删除并 check/test 绿 |

- did-not-run 10（基线 1）：串行依赖链在先行用例失败后中断（LM Studio 挂放大了链式中断），非独立失败。
- 结论：**深档 e2e 零 B5 归属红**。删除面（AgentCoordinator 4 端点 + UI 死代码）用户可见零变化（02-spec §7）得到实锤：/ai-agent 页与 agents 列表页相关 spec 的红全部为「spec 钉旧世界」，页面本体渲染与主链路健康。

## Static Analysis

- pnpm run check（ForgeSelf.Web）：exit 0，0 error / 166 warnings（基线 153 条存量风格族，新增 warnings 均为既有文件既有行，无新文件引入）；组件规范守卫 PASS（story 4 / ct.spec 4）。（来源等级：Verified，`.temp/web-check-b5.log`）
- pnpm run test（vitest）：92 文件 / 1073 通过 / 0 失败（基线 90/1042 → 本批 +2 文件 +31 用例全绿）。（来源等级：Verified，`.temp/web-test-b5.log`）

## Screenshots

无新增用户可见界面（02-spec §7 无界面变更声明）；e2e 失败现场快照留存 `.temp/pw-out-b5s9/`（error-context.md 含页面 YAML 快照，判责依据），不作为 UI 交付证据。

## Known Limitations

- ~~4 条 OutDir 布局敏感守卫的标准 bin 结论待 dev-stack 锁窗口~~ → **已闭合（2026-11-03 01:12 收口窗口）**：lead 停 dev-stack 后重建新鲜标准 bin（Tests.dll 01:12:10，含全 B5 源码），4 守卫类隔离复跑 **17/17 全绿**（ChatCompletionWritePathTests + XCodeTestFixtureIronRuleTests + PersistentSessionStoreTests + ChatProjectionRaceTests，`.temp/test-guards-stdbin-b5win.log`）。OutDir 73 红中的 4 条布局伪红定性至此有标准 bin 正证；B4 §5-1 残差一并闭合。
- e2e 基线（145/75/4/1）仅汇总数字留档、无逐名清单，本次逐名判责以错误正文+现场快照为据；建议后续深档基线落逐名清单（复用 b4-qa-audit 的名单文件形态）。
- 34 条陈旧断言 e2e（chat.spec 28 + ai-agent 宿主 3 + ai-agent 插件 2 + plugin-remote-view 1）钉的是 029 重设计前的页面结构与文案，属既有技术债，记 TODO 待清理批（不在 B5 范围）。
- QA 对抗文件（InboxR2QaAdversarialTests）为「实现方不得改写」纪律下的机械适配例外：取消取景点（HangThenTextProvider + keepInbox:true）与事件序/转义断言已逐条留痕 04-task 偏差 §7。
- 基线外死代码（store 的 getTaskPriorityLabel 等、agentApi 的 getAgent 族、types 的 FindAgentRequest/BestMatchRequest/TaskPriority/AgentStatus）超出 ③ 批准清单，未删，报 lead 备案（04-task 偏差 §10）。

## Unresolved Issues

无 BLOCKED。AC1..AC9 全绿（AC9 中档判责闭合 + 深档 e2e 判责闭合 + 4 守卫类标准 bin 17/17 + 构建 0 错误）；余 34 条陈旧断言 e2e 技术债（记 TODO，非本批）。
