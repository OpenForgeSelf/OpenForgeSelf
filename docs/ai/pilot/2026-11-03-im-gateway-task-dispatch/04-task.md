---
task_id: 2026-11-03-im-gateway-task-dispatch
feature: 031-im-gateway        # 关联 021-ai-agent；宿主侧无新页面
risk: { level: L2, triggers: ["ForgeSelf.Abstractions/AgentRunDispatchContracts.cs", "Plugins/AIAgent/Services/AgentRunDispatchService.cs", "Plugins/ImGateway/Services/ImTaskWatcher.cs"], raised_by_agent: true, note: "新公共契约=中风险（§5.6 触发中档全量）；插件自有库新表走 XCode 自动建表，非宿主迁移" }
gate1: { mode: user, at: 2026-11-03T23:33+08:00, decision: approved, note: "按默认拍板：①命令式触发 ②设置面板包含 ③IM审批开启；用户令组建产品/设计/前端/后端/QA 团队动工" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: lead, at: null }
expected_files:
  - ForgeSelf.Abstractions/AgentRunDispatchContracts.cs
  - Plugins/AIAgent/Services/AgentRunDispatchService.cs
  - Plugins/AIAgent/AIAgentPlugin.cs
  - Plugins/AIAgent/plugin.json
  - Plugins/ImGateway/Core/ImGatewayConfig.cs
  - Plugins/ImGateway/Data/Entities/ImGatewayTask.cs
  - Plugins/ImGateway/Data/ImGatewayTables.cs
  - Plugins/ImGateway/Services/ImTaskDispatcher.cs
  - Plugins/ImGateway/Services/ImTaskWatcher.cs
  - Plugins/ImGateway/Services/ImGatewayRouter.cs
  - Plugins/ImGateway/ImGatewayPlugin.cs
  - Plugins/ImGateway/plugin.json
  - Plugins/ImGateway/Controllers/ImGatewayController.cs
  - ForgeSelf.Api.Tests/Plugins/AIAgent/AgentRunDispatchTests.cs
  - ForgeSelf.Api.Tests/Plugins/ImGateway/ImTaskDispatchTests.cs
  - ForgeSelf.Web/e2e/plugins/im-gateway/im-gateway.spec.ts
rollback: "git revert <commit>；插件自有库新表留存无副作用（Enabled=false 即全部旁路）"
writeback: { feature_doc: 031-im-gateway 补任务网关节 + host-capability-seams 第三接缝登记, reason: "新能力接缝必须入台账（architecture-design 产出门禁 2）" }
---

# Agent Task

> 阶段：Stage 4｜前序工件 00-04 齐备且经闸门1 确认后方可 Implement。

## Task ID

PILOT-079（2026-11-03-im-gateway-task-dispatch）

## Objective

IM 网关收到「/task」命令消息后，经新契约 IAgentRunDispatch 在 AIAgent 创建并后台驱动 MAF Run（指派指定 Agent），任务自动出现在任务中心计划任务桶；终态后由网关经通道主动回复结果。Enabled=false 时一切行为与 v2.1.1 完全一致。

## Scope

### Allowed

- 03-plan.md「Files To Change」全部文件（T1 契约+AIAgent 侧；T2 ImGateway 侧；T3 e2e+docs）。
- 新增测试仅限列出的两个测试文件 + e2e 既有 spec 追加用例。
- FR-7 拍板含时：`Plugins/ImGateway/web/**` 设置面板小节。

### Forbidden

- 宿主 DB 结构迁移 / 鉴权权限支付逻辑（规范 §1.1-1.3）。
- 修改既有契约：`IChatCompletion` / `IScheduledAgentRunTrigger` / `IAgentDelegation`；修改 `TasksOverviewController` / `RunOrchestratorService` / `MafTaskLoopAgent` / `IApprovalService` / `PersistentApprovalService`（审批链本体）既有行为。
- 触碰其他插件目录（Sems/DesignSystem/…）；修改 `:51888` 运行实例与任何运行中宿主进程。
- 测试手搓 `DAL.AddConnStr`（PC-014）；`temp/` 一次性脚本充当验证（§5.0 红线）。
- 新增 NuGet/npm 依赖；git 提交（闸门3 前禁）。

## Acceptance Criteria

- [ ] AC1 契约单测绿（02-spec AC1 全项）
- [ ] AC2 派发服务单测绿（Submit 即回/终态快照/并发闸/RunId 回退）
- [ ] AC3 网关单测绿（分支/绑定/ack/watcher 幂等/重启恢复）
- [ ] AC4 dev-stack 集成实测过（tasks-overview planned 桶 + runs?sessionId=imtask:* 命中同一 runId + 回复证据）
- [ ] AC5 e2e 冒烟主链路绿 + 既有 im-gateway e2e 不红
- [ ] AC6 Enabled=false 回归绿
- [ ] AC7 中档全量 dotnet test 对表基线 0 归本批（+web 门禁若适用）
- [ ] AC9 simulate-inbound 端点（鉴权+注入）
- [ ] AC10 IM 审批闭环（卡片幂等/关键字决策/续跑触发/跨会话隔离/Expired 指引）
- [ ] AC8 工件 00-07 齐 + pre-commit 绿

- [ ] AC11 产品验收剧本（02-spec 旅程小节）随走查逐条执行留痕

## Expected Files

见 front-matter expected_files（15 文件 + 可选 web/**）。

## Verification Commands

```bash
cd ForgeSelf.Api && dotnet build
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~AgentRunDispatch|FullyQualifiedName~ImTask"
cd ForgeSelf.Api.Tests && dotnet test          # 中档全量（公共契约变更）
cd ForgeSelf.Web && npx playwright test e2e/plugins/im-gateway --project=chromium
```
