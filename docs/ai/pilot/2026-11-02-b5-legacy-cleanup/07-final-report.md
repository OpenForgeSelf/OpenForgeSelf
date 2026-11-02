# Final Report — B5 旁路收敛（2026-11-02-b5-legacy-cleanup）

> 战役：2026-11-02-maf-runtime-hardening §B5｜执行者：verify-dev（战役批 teammate）｜2026-11-03
> 状态：**COMPLETED（待 lead gate2 验收 + gate3 由 lead 提交；verify-dev 不自行 commit）**

## 1 任务一句话

三处旁路收敛：① 删 ReactLoopAgent（运行时唯一 = MafTaskLoopAgent）② 删 UseMafRuntime/TaskLoopMode 开关（默认常开语义）③ AgentHub 启发式旁路经拍板全链删除 + UI 死代码清理；IM 审批推送后置不动。

## 2 完成内容（对应 04-task S1..S10）

| 步 | 内容 | 结果 |
|----|------|------|
| S1 | 共享契约迁移 AgentTurnRuntime.cs（纯移动） | ✅ |
| S2 | 删 ReactLoopAgent.cs（949 行）+ 工厂/注册表收敛 | ✅ |
| S3 | 测试迁移：ReactLoop* 4 文件 → MafEngineGateTests/MafInboxGateTests/MafExitAndSuspendTests + 共享 TestAgentFixtures | ✅ 24/24 + 合批 106/106 |
| S4 | 开关删除（Agents.cs 两属性 + AIAgentService 工厂恒 MAF + 注释引用 13 文件） | ✅ grep 零残留 |
| S5 | TaskLoopMode ≡ (ExitToolNames 为空) 衍生映射落码落档 | ✅ |
| S6 | ForgeSetting 兼容读测试（残留键不崩不吸收 + 回写清洗） | ✅ 2/2 |
| S7①② | （并入 S2/S4，无独立代码面） | ✅ |
| S7③ | AgentCoordinator 裁决（lead seq8 A=删除）→ 服务×2/端点×6/DI/待删模型/UI 三文件死代码删除 + not-taken 055 | ✅ check 0 error / test 1073 绿 |
| S8 | 中档全量（OutDir bin-b5s8） | ✅ 3427 总/3354 过/73 败逐族归属**零未归属红**（唯一 B5 同根因新红 PC-013 台账路径当批修复并隔离 5/5 绿） |
| S9 | 深档全量 e2e（4 worker + 空输出目录） | ✅ 151 过/79 败四族判责**零 B5 归属红**（LM Studio 挂 13 + 陈旧断言 34 + 可见性超时 32） |
| S10 | 05/06/07 工件 + 偏差留痕 + 日记/TODO 收敛 | ✅（本件） |

## 3 主要变更

- **删除 −3217 行**：ReactLoopAgent.cs / AgentCoordinatorService.cs / AgentExecutorService.cs / ReactLoop* 测试 4 文件 / AgentsController 6 端点 / AgentModels 待删模型 / UI agentApi 六方法 + store 编排面 + types 六类型。
- **新增**：AgentTurnRuntime.cs（共享契约）+ 测试脚手架/守卫 5 文件（含 MafEngineGateTests 禁用名单 = AC2/AC5/AC8 机器守卫）+ ForgeSettingLegacyKeyCompatTests。
- **文档**：agenthub-adjudication.md（AC7 拍板留痕）、not-taken-decisions.md 055、PC-013 台账守卫路径迁移（MafTaskLoopAgentTests.cs）、04-task 偏差 §1-10（10 项实现期偏差逐条留痕）。
- git diff --stat：41 文件修改 + 4 新增 + 8 删除，+187/−3217（生产行为面净删除）。

## 4 Validation（验证结果，全部 Verified）

> 独立验证记录：快档过滤集 24/24 + 106/106；中档全量 3427/3354/73（零未归属红）；深档 e2e 244/151/79 四族判责 B5 归属 0；收口窗口新鲜 bin 补验见 §8。

## 4.1 Review（审查记录）

> 六审 06-review.md 八问全答 Final Decision APPROVED（gate2 待 lead 复核）；本节为工件链校验所需的字面节名锚点，实体内容见 06-review.md。

- 后端：构建 0 error；MAF 过滤集 24/24；编排器系+收据+MAF 合批 106/106；ForgeSetting 兼容读 2/2；中档全量 3427 总/3354 过/73 败逐族归属闭合（基线稳定/环境红 43 + B4 审计 OutDir 伪红 29 + B5 同根因已修 1）；PC-013 修复后隔离 5/5。
- 前端：pnpm run check 0 error（166 warnings 存量风格族）；pnpm run test 92 文件/1073 用例全绿。
- e2e 深档：244 总/151 过/79 败/4 跳/10 未跑（8.5m，4 worker，空输出目录）；四族判责（LM Studio 不可达 13【探针实证 localhost:1234 拒连】+ 陈旧断言 34【快照/源码双证页面健康、spec 钉旧世界】+ 可见性超时/共享后端 32 + **B5 归属 0**）。
- 判责纪律：一律日志正文 + 现场快照（error-context.md），不拿 exit code 当证据。

## 5 设计决策

- **TaskLoopMode ≡ (ExitToolNames 为空)**：开关删除的语义等价映射——开放任务恒任务循环（task_complete 自动挂载），计划路径（ExitToolNames 非空）无挂载。已注明 MafTaskLoopAgent.cs:196 与 05。
- **测试装配对齐生产 DI**：编排器系 mock 补 runFlowTools + 显式 ToolDefinitions（MAF 显式声明通道）；脚本工具 write_file→create_file（高风险名单会包审批挂起回合）。
- **ScriptedProvider 推进语义修复**：「无条件推进 + 入口回夹」，耗尽重复语义不变（RepeatLast 等价）——替身必须支撑目标用例模式。
- **QA 机械适配例外**：取消取景点（HangThenTextProvider + 显式 keepInbox:true，R2 不变量原样）+ MAF 事件序/转义断言，逐条留痕 04-task 偏差 §7。

## 6 代价与收益

- 收益：运行时唯一引擎（双轨维护成本归零）；审批泵统一覆盖（ReactLoop ad-hoc 无 MAF 审批泵的安全雷拆除）；AC2/AC5/AC8 机器守卫防旁路复活；净删 3217 行。
- 代价：一次性 10 项测试侧机械适配（已留痕）；34 条陈旧断言 e2e 技术债显形（029 重设计的遗留，记 TODO 另批）。

## 7 不做事决策（NOT-to-do，已入 docs/07-decisions/not-taken-decisions.md）

- **055 多智能体启发式编排不做，能力归计划任务主路径**（lead seq8 拍板 A=删除）。
- 超清单死代码（store getter 面/agentApi getters/types 4 项）**不删**（lead 裁决：另立批，随后端端点去留一并裁）——TODO P3 已登记。
- IM 审批推送（钉钉/企微）后置不动（B5 范围外）。

## 8 风险与残差

- 4 条 OutDir 布局敏感守卫（ChatCompletionWritePath/XCodeIronRule/PersistentSessionStore.Di/ChatProjectionRace）的标准 bin 结论承 B4 §5-1，已排进 lead 收口窗口（停 :7301 → 新鲜标准 bin → 补 4 守卫类隔离 → qa-gate 新鲜 bin 中档全量，一次窗口闭合 B4+B5 残差）。
- 34 条陈旧断言 e2e + e2e 基线无逐名清单留档（已记 TODO 建议）。
- 无 BLOCKED；无未验证冒充结论（全部来源等级 Verified，Unknown 面已在 Known Limitations 声明）。

## 9 问题类归档 / 重构提案 / 悬空教训（缺陷类门禁三节）

- **问题类归档**：本批无新缺陷类。同根因连带处置 1 项：PC-013 守卫路径因测试文件删除而悬空 → 台账迁 MafTaskLoopAgentTests.cs（docs/11-troubleshooting，5/5 隔离绿）。问题类机器校验 `pwsh scripts/check-problem-classes.ps1` 绿（ProblemClassRegistryTests 5/5 同判据）。
- **重构提案**：无（删除面即收敛本体）。
- **悬空教训**：删除测试文件时必须扫 `docs/11-troubleshooting/problem-classes/` 的 guards/signature 路径引用（ProblemClassRegistryTests 守卫已机器兜底——本次它抓到了，闭环有效）。

## 10 结论

任务完成（AC1..AC9 全绿 + 05/06/07 齐 + 裁决留痕 + 双门禁判责闭合），**可交付 lead 验收**；gate3 提交由 lead 执行。收口窗口残差闭合后战役 B5 即可关闭。
