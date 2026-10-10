# Review

> 阶段：Stage 8｜Task ID：2026-10-10-aiagent-tools-features-fix

## 审查八问（逐项回答）
1. **意图理解对吗**：对——五项均按字面实现，第 4 项经用户确认「常驻标签并入标签栏」。
2. **Spec 漏了吗**：无；Interaction Design 已列四 UI 面。
3. **Plan 可执行吗**：可——文件路径与实际一致，全部落地。
4. **代码对吗**：默认挂载作用域未变（ToolScope 3 项，回归绿）；显式勾选才放开，语义向后兼容。
5. **测试够吗**：新增 rankFeatures 4 例 + 契约注册回归 1 例；既有 765 宿主用例全绿。
6. **Evidence 真实吗**：全部 Verified 标注，失败项注明基线复现方式（stash 对照）。
7. **范围越界吗**：无——13 文件均在本任务边界内。
8. **遗留风险**：031 场景 2-5 待 51888 复验（外部依赖，非本次改动引入）。

## Requirement Check
五项 FR 全部有对应 Evidence（截图/evaluate 实测值/单测）。

## Scope Check
通过。features.ts、WS 词表、主题 token 未触碰。

## Test Check
通过（见 05-evidence）。

## Architecture Check
跨插件契约统一走 `ctx.Register` 共享表（与 IWorkflowAIAdvisor/IAgentRegistry 同范式），未新增私有接缝。

## Risk
- Low：`universal_tool` 进默认挂载作用域后模型可见工具数 +1（描述较长，本地小模型 prompt 增量约 1 工具定义；031 设计本意即模型侧可见）。
- Low：显式勾选放开到全注册表——仅用户/Agent 显式意图触发，默认行为不变。

## Findings
### Critical
无。
### Major
无。
### Minor
- `WorkflowEnginePlugin.RegisterToolExtensions` 自建 WorkflowService 每次 new（存量，未在本任务范围，记 TODO）。
- `WorkflowPlanningIntegrationTests` 6 例基线红（测试宿主不加载插件控制器），与本任务无关，已记 TODO。

## Final Decision
**APPROVED**（附 2 条 Minor 遗留，均已入 TODO.md 待办区）。
