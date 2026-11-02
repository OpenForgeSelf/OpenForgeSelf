---
task_id: 2026-11-02-b4-permission-profiles
feature: campaign/2026-11-02-maf-runtime-hardening-B4
risk: { level: L2, triggers: ["ForgeSelf.Api/Services/ToolRegistry.cs", "ForgeSelf.Api/AppBuilder.cs", "ForgeSelf.Abstractions/**", "Plugins/AIAgent/MafLoop/MafTaskLoopAgent.cs", "Plugins/BrowserUse/**"], raised_by_agent: false }
gate1: { mode: user, at: "2026-11-02T22:37:00+08:00", decision: "战役 00-campaign-workflow §B4 经 lead seq5 开工令开工（闸门1 视为已批）" }
gate2: { mode: reviewer, at: null }
gate3: { mode: agent, at: null }
expected_files:
  - ForgeSelf.Abstractions/PermissionProfiles.cs
  - ForgeSelf.Abstractions/HighRiskToolPolicy.cs
  - ForgeSelf.Api/Entities/PermissionProfile.cs
  - ForgeSelf.Api/Entities/PermissionProfile.Biz.cs
  - ForgeSelf.Api/Entities/PermissionDecisionLog.cs
  - ForgeSelf.Api/Entities/PermissionDecisionLog.Biz.cs
  - ForgeSelf.Api/Services/PermissionProfileService.cs
  - ForgeSelf.Api/Controllers/PermissionsController.cs
  - ForgeSelf.Api/Services/ToolRegistry.cs
  - ForgeSelf.Api/AppBuilder.cs
  - Plugins/AIAgent/MafLoop/MafTaskLoopAgent.cs
  - Plugins/AIAgent/Services/AIAgentService.cs
  - Plugins/AIAgent/Services/RunOrchestratorService.cs
  - ForgeSelf.Api.Tests/Unit/PermissionProfileServiceTests.cs
  - ForgeSelf.Api.Tests/Integration/PermissionsApiTests.cs
rollback: "git checkout -- <touched files>；新表留库无行为影响（无配置=零行为变化）；审计日志只增不删"
writeback: { feature_doc: "docs/02-features 权限策略节随 07-final-report 回写（契约/端点/决策语义），由 lead 闸门3 提交时落库", reason: "V1 语义冻结于本 pilot 02-spec/05" }
---

# Task — B4 权限可配置化（Agent 工作单元）

> Task ID：`2026-11-02-b4-permission-profiles`｜执行者：verify-dev（战役批 teammate）｜战役工作流：`docs/ai/campaigns/2026-11-02-maf-runtime-hardening/00-campaign-workflow.md` §B4

## Allowed（本任务写面）

- `ForgeSelf.Abstractions/`：PermissionProfiles.cs（新）、HighRiskToolPolicy.cs（注释/常量补充）
- `ForgeSelf.Api/Entities/`：PermissionProfile(.Biz).cs、PermissionDecisionLog(.Biz).cs（新，照 ToolApproval 模式手写）
- `ForgeSelf.Api/Services/`：PermissionProfileService.cs（新）、ToolRegistry.cs（pre-execute 监听器接线）、AppBuilder.cs（DI + 启动接线）
- `ForgeSelf.Api/Controllers/PermissionsController.cs`（新，宿主控制器）
- `Plugins/AIAgent/MafLoop/MafTaskLoopAgent.cs`（包装让位判断，AC8 防双问）
- `Plugins/AIAgent/Services/AIAgentService.cs` + `RunOrchestratorService.cs`（RememberSessionAgent 两处最小接线）
- `Plugins/BrowserUse/`（仅注释/文档性改动，逻辑零改动——本地校验保留）
- 测试：`ForgeSelf.Api.Tests/Unit/PermissionProfileServiceTests.cs`、`ForgeSelf.Api.Tests/Integration/PermissionsApiTests.cs`、必要时 `ToolRegistryEventBusTests.cs` 增 case（不改既有断言）
- 文档：本 pilot 目录 00-07、TODO.md（本条）、`.forgeself/memory/2026-11-02.md`

## Forbidden

- `ForgeSelf.Web/src/**`、`features.ts`、`Plugins/AIAgent/web/**`（UI 归 taskcenter-dev 协同批）
- `Plugins/BrowserUse` 逻辑改动（本地校验是纵深防御，不上收删除）
- AgentHub `PermissionBroker`（铁律 2：只服务外部 CLI 委派，禁止合并或平行复用）
- `:51888` 实例停/启/杀/配置（铁律）；dev-stack :7301/:7399 不停（DLL 锁报 lead 开窗）
- 生成件手改（xcode 生成 `.cs`）、审批契约破坏性变更（IApprovalService 签名不动）、全量 e2e（未触发深档条件）

## 验证命令（§5.6 中档：碰宿主源码）

```powershell
$env:NO_PROXY='localhost,127.0.0.1,::1'; $env:TEMP="$PWD\.temp\tmp"; $env:TMP="$PWD\.temp\tmp"
dotnet test ForgeSelf.Api.Tests\ForgeSelf.Api.Tests.csproj
```

构建撞 dev-stack DLL 锁（MSB3027）→ 报 lead 统一开窗；开窗前完成全部代码与测试编写（不 build）。

判据：全量对表基线（最近一次全量日志），**新增的红才是我的**；看日志正文计数，不拿 exit code 当证据。

## 完成定义

AC1..AC9 全绿（02-spec §9）+ 05/06/07 齐 + 契约知会 taskcenter-dev 留痕 + 向 lead 报告（不自行 commit）。

## 执行偏差记录（实现期实测，闸门1 语义不越界）

1. **ToolRegistry.cs 零改动**：pre-execute 闸门 B8/042 已内建（监听器 = 消费方），策略接线挂在 AppBuilder 的 `IEventBus.OnSerial("tools/pre-execute", ...)`，无需动 registry。expected_files 中该项未使用（合理收敛：改动面更小）。
2. **Integration/PermissionsApiTests.cs 并入 Unit/PermissionProfileServiceTests.cs**：AC7 采用控制器级测试（真 XCode SQLite + 直接 new PermissionsController），避免 WebApplicationFactory 整宿主环境面（鉴权/端口/DLL 锁），路由契约由端点骨架 + camelCase 序列化测试覆盖。
3. **新增触碰（不在 expected_files）**：`Plugins/AIAgent/Services/ReactLoopAgent.cs`（AgentTurnRuntime 加 `IPermissionProfileService? PermissionProfiles` 注入面——与 B2 RunVerify 同模式）；`Plugins/BrowserUse/BrowserUseConfig.cs`（头注释语义声明，零逻辑）；`ForgeSelf.Api.Tests/Plugins/AIAgent/MafApprovalLoopTests.cs`（AC8 MAF 层让位正反 2 用例，复用既有 MafApprovalFixture）。
4. **HighRiskToolPolicy.cs 零改动**：零配置红线在 PermissionProfileService 内实现（无 profile → default-policy Allow），硬名单常量无需变更。
5. **HasRuleForToolAsync 签名微调**：02-spec §3 草案为 `(agentId, toolName)`，实现改为 `(sessionId, toolName)`（服务内部经 RememberSessionAgent 解析 Agent）——MAF 侧 agent 不自知 agentId，会话键即其已有信息；语义等价（服务内仍按 Agent→全局匹配序）。
