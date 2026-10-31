---
task_id: PILOT-077
task_kind: defect
feature: none
risk: { level: L2, triggers: ["Plugins/AIAgent/**", "ForgeSelf.Api.Tests/Plugins/AIAgent/**", "ForgeSelf.Web/e2e/plugins/ai-agent/**"], raised_by_agent: true }
gate1: { mode: reviewer, at: 2026-10-31T08:40, ref: "输入5 用户「开工」批准 03-plan（SSE 契约 + 评估输入行为变更面 → L2）" }
gate2: { mode: reviewer, at: 2026-10-31T10:50, decision: pending }
gate3: { mode: agent, at: null }
expected_files:
  - Plugins/AIAgent/Controllers/AgentRunsController.cs
  - Plugins/AIAgent/Services/RunOrchestratorService.cs
  - Plugins/AIAgent/MafLoop/GoalPursuitService.cs
  - Plugins/AIAgent/plugin.json
  - Plugins/AIAgent/web/src/agent-core/styles/blocks.css
  - Plugins/AIAgent/web/src/agent-core/styles/widget.css
  - Plugins/AIAgent/web/src/components/RunRecordPanel.vue
  - Plugins/AIAgent/web/src/agent-core/components/blocks/BlockRenderer.test.ts
  - ForgeSelf.Api.Tests/Plugins/AIAgent/GoalRunSseEvidenceTests.cs
  - ForgeSelf.Web/e2e/plugins/ai-agent/ai-agent.spec.ts
rollback: "git revert <commit>"
writeback: { feature_doc: none, reason: "插件级修复批无 02-features 文档；规范回流已落 b-engineering-daily B2/B3 与模板" }
---

# Task

> 阶段：Stage 4 ｜ Task ID：PILOT-077 ｜ 日期：2026-10-30

## Allowed

- `Plugins/AIAgent/Controllers/AgentRunsController.cs`（仅 WriteSseEventAsync switch 两个 case）
- `Plugins/AIAgent/Services/RunOrchestratorService.cs`（仅 ExecuteStepsCoreAsync 轨迹聚合；Mark* 序列化不动）
- `Plugins/AIAgent/MafLoop/GoalPursuitService.cs`（仅评估输入组装段）
- `Plugins/AIAgent/web/src/agent-core/styles/blocks.css`（`.aiw-tool__pre`/`.aiw-tool__body` 两规则）
- `Plugins/AIAgent/web/src/components/RunRecordPanel.vue`（`.rrp__step`/`.rrp__plan-body` 两规则）
- `Plugins/AIAgent/web/src/agent-core/styles/widget.css`（`.aiw-ask__item` 一规则）
- 测试：`ForgeSelf.Api.Tests/Plugins/AIAgent/GoalRunSseEvidenceTests.cs`（新）、`BlockRenderer.test.ts`（追加用例）、`e2e/plugins/ai-agent/ai-agent.spec.ts`（新）
- 工件与文档：本 pilot 目录 05-07、`docs/11-troubleshooting/problem-classes/`（PC-019 建档）、插件文档/日记/TODO 同步

## Forbidden

- 不改聊天链路行为与五类既有 SSE 事件输出（回归守卫锁）
- 不改评估判定语义（BR-4）、不改 payload 结构（`AgentRunModels.cs`）
- 不碰宿主源码（`ForgeSelf.Api/**` 业务面）、不碰 `:51888` 运行实例、不启停任何宿主进程
- 不改 `plugin.json` 版本号以外的清单字段；版本号升位随实现批按插件惯例处理并在 05 记录
- 不新增依赖、不新增动效、不新增裸色值（design:check 棘轮）
- 不做 FR 清单之外的「顺手改」（发现即记 TODO/日记）
- git 提交/发布动作等待闸门2/3，不自行执行

## 验证命令

```bash
# 后端
cd ForgeSelf.Api && dotnet build
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~AIAgent"
# 插件前端
cd Plugins/AIAgent/web && pnpm run build
# 设计守卫（宿主侧）
cd ForgeSelf.Web && pnpm run design:check
# e2e 冒烟
$env:NO_PROXY='localhost,127.0.0.1,::1'; pnpm exec playwright test e2e/plugins/ai-agent
```

判定：构建零错误；过滤集全绿（基线外新增红 = 本批责任，修复或回滚）；e2e 主链路绿；走查读图 AC-5/6/7 逐条核对。
