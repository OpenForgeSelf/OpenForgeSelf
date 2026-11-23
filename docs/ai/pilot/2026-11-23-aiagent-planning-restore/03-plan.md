# 03 Plan（灾后按意图重实现 · 2026-11-23）

| # | 动作 | 真实文件 | 预期结果 |
|---|------|----------|----------|
| 1 | 执行器加 planGenerator 形参/字段 + `StartPlanningAsync` + 回退/映射助手 | `Plugins/AIAgent/Services/AgentFacadeRunExecutor.cs` | 编译过；规划回合可独立调用 |
| 2 | 接口加方法 | `Plugins/AIAgent/Services/IAgentFacadeService.cs` | facade 可经接口触发规划回合 |
| 3 | facade planned 分支触发 + `UpdatePlannedPlan` 回写（awaiting_plan 门 + 事件重发） | `Plugins/AIAgent/Services/AgentFacadeService.cs` | 发消息即触发；确认后拒收 |
| 4 | DTO 加 Note | `Plugins/AIAgent/Models/AgentFacadeModels.cs` | note 随 plan.proposed 透传 |
| 5 | 组合根全显式装配（PC-040） | `Plugins/AIAgent/AIAgentPlugin.cs` | executor 构造含 registry:/planGenerator: 命名实参 |
| 6 | DbAdmin 同形态显式化 | `Plugins/DbAdmin/DbAdminPlugin.cs` | systemConnNames 显式传参 |
| 7 | 装配守卫 + 规划行为锁测试（先于实现写好，Red→Green） | `ForgeSelf.Api.Tests/Plugins/AIAgentTests/AIAgentPluginCompositionTests.cs` | 4 用例绿 |
| 8 | 前端 note 透传/渲染 + 契约测试 | `Plugins/AIAgent/web/src/assistant/session.logic.ts` `AgentWorkspace.vue` `session.logic.test.ts` | check+test 绿 |
| 9 | 全量门禁（§5.6 快档）+ 工件链 + 提交 | — | 门禁绿后提交 |
- 同类修复项（problem-class-closure）：PC-040 同根因扫查命中 DbAdmin 构造点，第 6 步一并收口（闸门1 视为已含）。
- 回滚点：单提交原子回滚（git revert）。
