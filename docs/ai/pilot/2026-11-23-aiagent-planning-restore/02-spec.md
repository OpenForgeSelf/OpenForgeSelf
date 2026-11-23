# 02 Spec（灾后按意图重实现 · 2026-11-23）

## 背景与目标
仓库灾难丢失 1.10.1/1.10.2 批；用户授权按意图重实现。目标=planned 模式 LLM 真规划闭环回归 + PC-040 组合根显式装配与守卫，成功判据见 01-intent 四条。

## 范围（In / Out）
- In：facade planned 路径规划回合、回退标注、组合根/DbAdmin 显式化、plan.note 透传渲染、契约测试。
- Out：①②④⑤ 四缺陷、PC-039 ChatModelId、真实模型端到端调优、插件六步 ②③⑤（移交清单见 07）。

## 功能规格
- `IAgentRunExecutor` 新增 `StartPlanningAsync(sessionId, runId, ct)`；facade `PostMessage` planned 分支在发布草稿卡后经 `SafeStart` 触发规划回合。
- 规划回合（executor）：取快照 → 解析规划器（ctor 显式注入优先，`_ctx.Get<IPlanGeneratorService>()` 兜底）→ `ResolveModelAsync` → 构造临时 `AgentRun`（不落库，Id 取负随机避免与编排器 scratch 会话 `plan:{runId}` 互撞）→ `GeneratePlanAsync`。
- 产出回写 `AgentFacadeService.UpdatePlannedPlan`：仅 `awaiting_plan` 且 run 匹配时生效（确认后到达的规划产出拒收，防覆盖进行中轮次）；回写即再发 `plan.proposed`。
- 失败语义（静默降级禁令）：规划器不可用（组合根漏装配）/未产出有效计划/超时/异常 → 保留草稿计划（无则单步）+ `Note=「规划失败，已用回退计划」` + `XTrace Warn [AgentFacade] 规划器不可用（组合根未装配 planGenerator）` / `[AgentFacade] 规划失败 session={0} run={1}: {2}`；用户取消不写回退。
- `AgentPlanDto` 新增 `Note` 字段，随 `plan.proposed` 载荷透传；前端 session.logic 归一化 `note`、AgentWorkspace 计划卡渲染。
- DbAdminPlugin 组合根同形态显式化（`systemConnNames: DbConnectionService.DefaultSystemConnNames`，PC-040 同类扫查命中）。
- Unknown（标明）：与丢失实现逐字节差异不可考，本批为意图级重实现；临时 Run 负 Id 方案为本批设计选择。

## 数据与契约
- 计划载荷契约：`plan.proposed` payload = AgentPlanDto（goal/steps/constraints + 新增 note，camelCase 序列化）；重复发布=以最新为准（UI 就地替换计划卡）。
- 无数据库 schema 变更、无破坏性 API 变更（DTO 新增可选字段向后兼容）；`Plugins/AIAgent/web/package.json` 新增 vitest devDep + test 脚本（clone 基线缺失的测试入口）。

## 交互设计（Interaction Design）
- 点什么出现什么：planned 会话输入消息发送 → 立即出现草稿计划卡（目标/步骤/约束，状态「待确认」）→ 规划回合完成后计划卡被真实计划替换（同一卡片就地更新，无额外弹窗）；若规划失败，卡片内容保留草稿并在目标区下方出现一行浅色说明「规划失败，已用回退计划」。
- 空态：无计划时计划面板显示「还没有计划…」引导文案（既有行为不变）。
- 反馈：规划回合后台执行不阻塞发送响应（202 即回）；确认/修改计划入口行为不变；用户确认后迟到的规划产出被拒收（界面不再变化）。
- 边界：同一会话连发消息受既有 409 忙碌约束；停止/删除会话后规划回写静默忽略。
- 五态核对（本批涉及面）：加载=后台规划期间显示草稿卡（无额外 loading 态）；空=无计划引导文案；出错=note 标注+Warn；无权限=不涉及；正常=真计划替换。

## 非功能与回滚
- 性能：规划回合 20s 无进展超时（PlanGeneratorService 既有），不阻塞发送响应；线程=后台 SafeStart。
- 回滚：单提交 git revert；无迁移、无破坏性契约。

## 验收标准
- AC1-AC4 定义见 04-task「AC」节；判定命令见 04「验证命令」节。

