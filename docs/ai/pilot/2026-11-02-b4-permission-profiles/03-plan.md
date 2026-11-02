# Plan — B4 权限可配置化

> Task ID：`2026-11-02-b4-permission-profiles`｜TDD：新决策逻辑先红后绿；实体/接线为结构面

## 步骤分解（每步可独立验证）

| # | 步骤 | 文件 | 验证 |
|---|------|------|------|
| S1 | 契约：`Abstractions/PermissionProfiles.cs`（§3 全类型）+ `HighRiskToolPolicy` 补注释（默认策略等价语义说明） | Abstractions | dotnet build Abstractions 绿 |
| S2 | 端点骨架：`ForgeSelf.Api/Controllers/PermissionsController.cs`（DI 注入 IPermissionProfileService；400/404/409 语义先落） | Api/Controllers | build 绿；AC1 契约知会 taskcenter-dev |
| S3 | 实体：`Entities/PermissionProfile.cs+.Biz.cs`、`PermissionDecisionLog.cs+.Biz.cs`（照 ToolApproval 模式） | Api/Entities | EnsureCreated 单测（真 SQLite）绿 |
| S4 | 服务：`Services/PermissionProfileService.cs`（DecideAsync §4 全分支 + CRUD + QueryAudit + RememberSessionAgent）+ **TDD 红先**：`ForgeSelf.Api.Tests/Unit/PermissionProfileServiceTests.cs` | Api/Services + Tests | 红留档 → 绿；AC2/AC3/AC4 |
| S5 | 接线：AppBuilder 注册服务 + pre-execute 监听器（provider 构建后启动段 `OnSerial<ToolExecution, PreToolDecision?>("tools/pre-execute", …)`；监听内 try-catch，异常=fail-open 返回 null + XTrace.Warn）+ AIAgentService/RunOrchestratorService 两处 RememberSessionAgent | AppBuilder.cs、AIAgentService.cs、RunOrchestratorService.cs | 集成测试走 ExecuteAsync 真管线（AC5） |
| S6 | BrowserUse 上收：registry 层 url→域名判定（复用 BrowserGuard.NormalizeDomain 语义的宿主侧等价实现，不引插件依赖）+ 插件本地校验保留说明 | ToolRegistry.cs（监听器内）、Plugins/BrowserUse 仅注释 | AC6 单测 + 集成 |
| S7 | 防双问：MafTaskLoopAgent 包装条件加 `HasRuleForToolAsync` 让位判断（agentId 取当前 run/request agentId，经构造注入或 IContext） | MafTaskLoopAgent.cs | AC8 单测 + 既有 MafTaskLoopAgent 过滤集全绿 |
| S8 | 审计端点联调测试：`ForgeSelf.Api.Tests/Integration/PermissionsApiTests.cs`（WebApplicationFactory 或 TestServer 既有模式） | Tests | AC7 |
| S9 | 收口：中档全量 dotnet test（报 lead 开窗 build）+ 05/06/07 | 全部 | AC9 |

## 依赖与顺序

S1→S2 可并行 S3；S4 依赖 S1+S3；S5/S6/S7 依赖 S4；S8 依赖 S2+S4；S9 最后。测试文件随步骤同步写（S4 起先红）。

## 风险与对策

- **双问破坏零配置红线**（最高风险）→ S7 让位判断 + AC3/AC8 双守卫；任何既有测试红立即停。
- **pre-execute 监听器抛异常打断管线** → 监听器内全 catch（fail-open）+ 对抗性单测（服务抛异常时工具仍执行）。
- **审计写放大**（每工具调用 1 行）→ V1 接受 + 记 07 风险（清理策略后置）。
- **DLL 锁** → 代码+测试先写完，build/test 报 lead 统一开窗（B2 实证流程）。

## 回滚

`git checkout -- <touched files>`；新表（PermissionProfile/PermissionDecisionLog）留在库中无行为影响（无配置=零行为变化）；Run/审计数据只增不删。
