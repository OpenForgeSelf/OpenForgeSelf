---
task_id: 2026-11-23-aiagent-planning-restore
feature: 021
task_kind: defect
risk: { level: L2, reason: 公共接口扩展（IAgentRunExecutor 加方法 + DTO 加可选字段），无破坏性变更、无数据迁移 }
gate1: 用户已拍板「按照意图重新实现功能也可」（2026-11-23 13:15 会话原文），本批按意图重实现 planned 真规划竖切
gate2: 05-evidence + 06-review 齐备，验证门禁（快档）全绿
gate3: 提交归档（提交后推送/发布仍须用户指令）
expected_files:
  - Plugins/AIAgent/Services/AgentFacadeRunExecutor.cs
  - Plugins/AIAgent/Services/IAgentFacadeService.cs
  - Plugins/AIAgent/Services/AgentFacadeService.cs
  - Plugins/AIAgent/Models/AgentFacadeModels.cs
  - Plugins/AIAgent/AIAgentPlugin.cs
  - Plugins/DbAdmin/DbAdminPlugin.cs
  - Plugins/AIAgent/plugin.json
  - Plugins/AIAgent/web/package.json
  - Plugins/AIAgent/web/pnpm-lock.yaml
  - Plugins/AIAgent/web/src/assistant/session.logic.ts
  - Plugins/AIAgent/web/src/assistant/AgentWorkspace.vue
  - Plugins/AIAgent/web/src/assistant/session.logic.test.ts
  - ForgeSelf.Api.Tests/Plugins/AIAgentTests/AIAgentPluginCompositionTests.cs
rollback: 单提交 git revert 即回滚（无数据迁移、无破坏性契约变更）
writeback: docs/02-features/021-ai-agent.md（提交后核对是否需同步计划说明字段，不在本批强改）
---

# 04 Task（灾后按意图重实现 · 2026-11-23）

## Allowed / Forbidden
- Allowed：上表 13 文件 + 本目录工件 + docs/11-troubleshooting（PC-040 归档更新）+ TODO.md + 工作日记。
- Forbidden：其他插件/宿主源码；备份树（用户明令禁触）；推送/发布（未授权）。

## 验证命令
- `dotnet build`（宿主）+ `dotnet test --filter AIAgentPluginCompositionTests`
- `cd Plugins/AIAgent/web && pnpm install && pnpm run test && pnpm run build`
- `pwsh scripts/check-problem-classes.ps1` + `pwsh scripts/check-commit-hygiene.ps1 -Staged`
- `pwsh scripts/verify-pilot-feature-writeback.ps1 -TaskId 2026-11-23-aiagent-planning-restore`

## AC（验收标准）
- AC1：planned 会话发消息触发 LLM 规划回合，真实计划经 plan.proposed 回写替换草稿（测试锁）。
- AC2：规划器不可用/失败/超时 → 草稿计划 + note「规划失败，已用回退计划」+ Warn 日志；确认后迟到产出拒收（测试锁）。
- AC3：组合根 executor 构造全显式命名实参（含 planGenerator:）+ DbAdmin 同形态显式化（源码断言 + 读码核对）。
- AC4：plan.note 经 DTO→事件→session.logic→计划卡渲染贯通（vitest 契约锁）。
