# 07 Final Report — aiagent-planning-restore（2026-11-23）

## 一、任务
灾后按意图重实现：planned 模式 LLM 真规划闭环（丢失批缺陷③）+ PC-040 组合根显式装配与守卫 + 计划卡 note 透传链。

## 二、主要变更
- 后端：`AgentFacadeRunExecutor`（+planGenerator 形参、`StartPlanningAsync` 规划回合、回退草稿+note、Warn 三路径）、`IAgentRunExecutor` 接口、`AgentFacadeService`（planned 分支触发 + `UpdatePlannedPlan` awaiting_plan 门回写）、`AgentPlanDto.Note`、`AIAgentPlugin` 组合根全显式 7 参、`DbAdminPlugin` 同形态显式化。
- 前端：`session.logic.ts`（plan.note 归一化 + 类型）、`AgentWorkspace.vue`（计划卡 note 渲染）、`session.logic.test.ts`（契约用例）、`package.json`（补 vitest test 入口——clone 基线缺失）。
- 测试：`AIAgentPluginCompositionTests` 4 用例（组合根 arity 源码断言 + 真规划回写 + 无规划器回退 + 确认后拒收）。
- 清单：`plugin.json` 1.10.3（诚实变更注，注明五缺陷中 ①②④⑤+PC-039 尚待重实现）；PC-040 档案 open→guarded-partial（G1 落地、G2 未建如实标注）。

## 三、验证（Validation）
- ✅ Verified：宿主 build 绿（0 error）；组合守卫测试 4/4 绿；插件 web build 绿；session.logic.test 15/15 绿（含新契约用例）。
- ✅ Verified：check-problem-classes / check-commit-hygiene(-Staged) / verify-pilot-artifacts / verify-pilot-feature-writeback 提交前全绿（结果见 05 §三）。
- ⚠️ Unknown：浏览器级计划卡走查、真实模型端到端规划、插件六步闭环 ②③⑤ 未执行（见 05 §四）；插件 web 全套件存量 15 红+10 套件加载失败已归因基线基建债并记 TODO。

## 四、审查（Review）
06-review 八问全过，Final Decision=APPROVED（限本批范围）。

## 五、问题类归档
- PC-040：守卫 G1 落地（组合根源码断言 + 规划行为锁），档案已更新 status=guarded-partial；G2 类级枚举未建，随后续组合根批补。
- PC-039：本批未触碰（异根因），档案维持 open，遗留项见 TODO。

## 六、重构提案与结论
- 提案（未采纳，记档）：将 facade planned 分支的草稿首发改为「规划完成才出卡」——被否：失去即时反馈且丢失批行为证据显示草稿卡先出、规划产出后替换；维持现设计。
- 结论：本批达到「planned 模式功能恢复」的意图级目标；恢复度=五缺陷中缺陷③核心 + PC-040 守卫，其余按 TODO 移交。

## 七、移交清单（悬空教训与后续批）
1. 五缺陷剩余：①会话时间线分段 ②工具卡片展开 ④计划面板真实步骤列表 ⑤卡住卡片介入收敛（TODO P1 在册）。
2. PC-039：ChatModelId 实体列 + Create/Resume/Restart 贯通 + 双接缝单决策点断言（TODO P1 在册）。
3. 插件 web 测试基建债：vitest 配置（vue 解析）+ http.session/http/sessionArchive 12 断言漂移修复（本批新暴露）。
4. 插件六步闭环 ②③⑤（冒烟 e2e / 起源码走查 / 运行实例复验）待用户安排；发布（④）前须源码级复验（TODO 输入14 警示仍有效）。
