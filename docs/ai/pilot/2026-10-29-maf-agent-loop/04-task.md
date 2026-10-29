---
task_id: 2026-10-29-maf-agent-loop
feature: none                  # AIAgent 插件能力增强；无对应 docs/02-features 编号，收尾回写功能文档候选
risk: { level: L2, triggers: ["ForgeSelf.Abstractions/Agents.cs", "Plugins/AIAgent/**", "ForgeSelf.Api/Services（未触碰，仅全量对表）"], raised_by_agent: false }
gate1: { mode: user-preauthorized, at: 2026-10-29T01:03, ref: "用户输入原文「自行调研、自行决策开发，不用问我」→ 01-intent 闸门1 裁剪记录" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }   # 提交仍待闸门2 验收后执行
expected_files:
  - ForgeSelf.Abstractions/Agents.cs
  - ForgeSelf.Abstractions/AgentLoopEvent.cs
  - Plugins/AIAgent/AIAgent.csproj
  - Plugins/AIAgent/AIAgentPlugin.cs
  - Plugins/AIAgent/Models/AgentRunModels.cs
  - Plugins/AIAgent/Services/AIAgentService.cs
  - Plugins/AIAgent/Services/ReactLoopAgent.cs
  - Plugins/AIAgent/Services/RunOrchestratorService.cs
  - Plugins/AIAgent/Services/PlanGeneratorService.cs
  - Plugins/AIAgent/Services/GoalPursuitService.cs
  - Plugins/AIAgent/Services/ToolFunctions/TaskLoopToolFunctions.cs
  - Plugins/AIAgent/MafLoop/ForgeChatClientAdapter.cs
  - Plugins/AIAgent/MafLoop/MafTaskLoopAgent.cs
  - Plugins/AIAgent/MafLoop/RegistryToolFunction.cs
  - Plugins/AIAgent/web/src/types.ts
  - Plugins/AIAgent/web/src/components/RunRecordPanel.vue
  - ForgeSelf.Api.Tests/Plugins/AIAgent/MafTaskLoopAgentTests.cs
  - ForgeSelf.Api.Tests/Plugins/AIAgent/ReactLoopTaskLoopTests.cs
  - ForgeSelf.Api.Tests/Plugins/AIAgent/GoalPursuitTests.cs
rollback: "git checkout -- <改动文件>（闸门2 验收前未提交；每步实现后可逐文件回退）"
writeback: { feature_doc: pending, reason: "AIAgent 循环/目标模式功能文档待 07 阶段补" }
---

# Agent Task

> 阶段：Stage 4｜Agent 可直接执行的工作单元。

## Task ID

PILOT-051

## Objective

聊天 Agent 循环按行业标准改为「持续工作直到显式宣告完成」（MAF Agent 驱动，ReactLoopAgent 后备同语义），并新增目标模式（目标+验收标准+评估续轮），全部行为有测试证据。

## Scope

### Allowed

- 03-plan「Files To Change」列出的文件；新增测试文件；既有测试文件的必要断言适配（如构造点补新可选参数）。
- 复用既有测试基建（ScriptedProvider/InMemorySessionStore/FakeToolRegistry/InMemoryInbox/EventBus）。

### Forbidden

- 生产环境 / DB 结构（不新增实体、不新增列、不迁移）/ 鉴权权限支付逻辑。
- 新增 NuGet 依赖（MAF 1.6.1 与 M.E.AI 10.5.1 均已在宿主 NuGet 图内）。
- L3/L5 遗留循环（RunAdHocCoreAsync/AgentExecutorService）的行为改动。
- ForgeSelf.Web 宿主前端、e2e 基建文件（global-setup/playwright 配置）、无关重构。

## Acceptance Criteria

- [ ] AC1 MAF 引擎任务循环帧序/日志序（工具→纯文本→续跑→task_complete）
- [ ] AC2 纯问答零工具回合行为不变
- [ ] AC3 ReactLoopAgent 后备同语义；计划路径语义不变
- [ ] AC4 适配器翻译正确（消息/工具/usage/finish/ToolChoice/Instructions）
- [ ] AC5 目标模式三态（达标/补步骤续轮/预算耗尽 Stuck）+ 评估失败保守回退
- [ ] AC6 新字段空值 = 现状行为（既有 RunOrchestrator 测试全绿）
- [ ] AC7 dotnet build + 过滤集绿；中档全量对表无新增红
- [ ] AC8 插件前端 build 绿 + AC-UI1~3 走查/冒烟
- [ ] AC9 05-evidence/06-review 落盘且 Verified 证据齐

## Expected Files

见 front-matter expected_files（实现允许因仓库现状微调，偏差记入 03-plan 偏差记录表）。

## Verification Commands

```bash
cd ForgeSelf.Api && dotnet build
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~MafTaskLoopAgent|FullyQualifiedName~ReactLoopTaskLoop|FullyQualifiedName~GoalPursuit|FullyQualifiedName~ForgeChatClientAdapter"
cd ForgeSelf.Api.Tests && dotnet test
cd Plugins/AIAgent/web && pnpm run build
```
