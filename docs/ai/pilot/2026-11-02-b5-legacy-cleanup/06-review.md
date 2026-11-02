# Review — B5 旁路收敛

> 阶段：Stage 8｜Reviewer 视角重查 Intent → Spec → Plan → Task → Code → Test → Evidence 全链。
> 结论只报事实。

## 审查八问（逐项回答）

1. **实现是否真正满足 Intent？** 是。三处旁路按架构 §7 授权收敛完成：ReactLoopAgent 删除（运行时唯一 = MafTaskLoopAgent，共享契约先行迁移 AgentTurnRuntime.cs）；UseMafRuntime/TaskLoopMode 开关删除（TaskLoopMode ≡ ExitToolNames 为空的衍生映射已注明代码与 05）；AgentCoordinator 启发式旁路经 lead seq8 拍板 A=删除后全链删除（服务×2 + 端点×6 + DI + 待删模型 + UI 死代码），能力归计划任务主路径（not-taken 055）。IM 审批推送保持后置未动。
2. **实现是否符合 Spec？** 是。02-spec §2 改/不改清单逐项对得上：删除面全落；不改面（ToolRegistry 六闸门、IApprovalService、IInbox 契约、AgentTurnRuntime 属性面、IChatCompletion 旧面、人设注册表、路由 /ai-agent、插件 Id）零触碰（git diff 佐证）；ForgeSetting 兼容读按 §3 补测试锁行为；③ 删除面契约（4 端点下线零消费者）成立。
3. **是否超出了 Scope？** 两处边界内延伸 + 一处明确不越权：① types/agent.ts 连带删除 AIChatMessage（唯一引用方 AgentInstance 同批删除，不删则留孤儿类型）；② docs/11-troubleshooting PC-013 台账守卫路径迁移（删除 ReactLoopAgentTests.cs 的同根因必然后果——问题类闭环门禁要求台账路径有效，5/5 隔离绿验证）；③ 超清单死代码（store getter 面/agentApi getters/types 4 项）**未删**，报 lead 裁决 →「不删另立批」已记 TODO P3。
4. **是否修改了不应该修改的文件？** 否。QA 对抗文件 InboxR2QaAdversarialTests 的改动全部为「实现方不得改写本文件」纪律下的机械适配例外（取消取景点 + 事件序/转义断言），逐条留痕 04-task 偏差 §7 并经 lead S8 验收确认。
5. **测试是否覆盖 Acceptance Criteria？** 是。AC1..AC9 逐条映射见 05（AC2/AC5/AC8 另有 MafEngineGateTests 禁用名单机器守卫防复发；AC6 有专用兼容读测试；AC9 双门禁判责闭合）。
6. **是否存在明显回归风险？** 低。删代码 −3217 行、无新增生产行为；用户可见面零变化（02-spec §7）；对外 HTTP/存储契约不变（被删端点零消费者经 00 F14/F15 实证）。残差：4 条 OutDir 布局敏感守卫的标准 bin 结论承 B4 §5-1（排进收口窗口，非 B5 新增）。
7. **是否存在架构不一致？** 否。运行时唯一引擎 = MafTaskLoopAgent（架构 §3.2 目标态）；编排/规划能力归计划任务主路径（RunOrchestrator + PlanGenerator）；人设注册表与执行引擎职责分离清晰。TaskLoopMode ≡ ExitToolNames 为空的衍生是唯一新增语义注释（非行为变更）。
8. **Evidence 是否足以证明任务完成？** 是。全部关键验证 Verified（构建 0 error、中档全量 3427/3354/73 逐族归属、深档 e2e 151 过/79 败四族判责、前端双门禁、隔离复跑三组、PC-013 修复后 5/5）；无 Inferred/Unknown 充当结论。

## Requirement Check

PASS（AC1..AC9 全绿；门禁 = 中档全量判责零未归属红 + 深档 e2e 判责零 B5 归属 + 构建 0 错误）

## Scope Check

PASS（删面/改面/不改面与 02-spec §2 一致；两处边界内延伸已声明；超清单发现上报未越权）

## Test Check

PASS（MAF 过滤集 24/24 + 编排器系合批 106/106 + 兼容读 2/2 + 中档全量对表 + 深档 e2e 对表 + 前端 check 0 error/test 1073 绿）

## Architecture Check

PASS（运行时唯一 MAF；统一循环 029 主路径承载编排；禁用名单守卫防旁路复活）

## Risk

L3（删除面大，触 Plugins/AIAgent/**——与 04 front-matter 声明一致；判责后残余风险低）

## Findings

### Critical

无。

### Major

无。

### Minor

1. 34 条陈旧断言 e2e（chat.spec 28 + ai-agent 宿主 3 + ai-agent 插件 2 + plugin-remote-view 1）钉 029 重设计前的页面结构/文案——既有技术债，建议另立清理批（已记 TODO）。
2. e2e 基线仅汇总数字留档（145/75/4/1，无逐名清单），逐名判责只能以错误正文为据——建议后续深档基线落逐名名单文件。
3. 编排器系测试对 runFlowTools/ToolDefinitions 的显式装配要求（MAF 显式声明通道）已成模式，TestAgentFixtures 已固化，后续新测试照抄即可。

## Final Decision

APPROVED（待 gate2：lead 验收；残差项已列 Known Limitations，不阻断收口）
