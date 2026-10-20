---
id: 2026-10-10-workflow-engine-contract-not-registered
title: AIAgent 工作流工具报「工作流服务没激活」——WorkflowEngine 三契约只进 IServiceCollection
pc: PC-001
severity: P1
evidence: Verified
---
# Case：2026-10-10 WorkflowEngine 跨插件契约未进共享表

> 规则/修法/扫描方法见 `../problem-classes/PC-001-cross-plugin-contract-shared-table.md`，本档只讲这一次发生了什么。

## 1 现象

AIAgent 内调用工作流类工具（`ctx.Get<IWorkflowService>()` 链路）得到「工作流服务没激活」/裸异常，而 WorkflowEngine 插件本身加载正常、宿主 DI 里服务也在。出处：`docs/ai/pilot/2026-10-10-aiagent-tools-features-fix/07-final-report.md` §10（Verified，当次任务实证）。

## 2 复现红灯

当次为缺陷修复任务（先定位后修）：AIAgent 工具解析 `ctx.Get<IWorkflowService>()` 返回 null（IContext 缺席语义=不抛错）即现象复现路径；修复后回归测试 `WorkflowEnginePluginRegistrationTests`（Apply 后 `WorkflowServiceResolver.Resolve(ctx)` 三契约断言）1/1 绿。出处：同 pilot `05-evidence.md` AC5（Verified）。

## 3 根因（5 Whys + 第 6 问）

WorkflowEngine 把 `IWorkflowService/IWorkflowExecutor/IWorkflowScheduler` 只 `AddScoped` 进自身 IServiceCollection → 兄弟插件子容器与 `ctx.Get` 均不可见。
- Why1 报错？消费方解析到 null。Why2 为 null？契约不在共享表。Why3 不在？提供方只注册进 DI 容器。Why4 只注册 DI？ASP.NET Core `AddScoped` 肌肉记忆，插件 `IContext.Register` 语义（root 共享表 vs 本地上下文）不在任何强制检查里。Why5 为什么没人拦？当时无枚举守卫、plugin-development 技能无此铁律。
- **第 6 问（现有流程为什么没让我一开始写对）**：Spec/Plan 没有任何 Stage 问「跨插件供给是否走共享表」——architecture-design 铁律 4 有文字但无索引无强制 → 归因「散文读不懂用不上 + 无守卫」。

## 4 同类扫查命中表

| 位置 | 同根因？ | 处置 |
|------|----------|------|
| WorkflowEngine 三契约 Apply 只 AddScoped（本 case） | 是 | 已修：Apply 内 `ctx.Register` 三契约（AC5） |
| AIAgent 工作流工具消费侧抛裸异常 | 连带 | 已修：软解析 + 可读错误 JSON（AC5） |
| 其余插件 ctx.Get 消费点 | 当时未扫 | 2026-10-17 建 PC-001 + G2 枚举测试补账 |

## 5 修复与被否方案

修法 = PC-001 canonical fix（提供方 Apply 期 eager 构造 + `ctx.Register`；消费方每次 `ctx.Get`，null 降级可读错误）。被否：兄弟插件直接引用对方程序集（违反插件隔离）。

## 6 重构提案与结论

R2 提案「`RegisterContract` 统一入口消灭 AddScoped 忘 Register 的表达面」登记于 PC-001 守卫台账 G4（提案），阶段四另批评估——本批不做。

## 7 守卫落地

G1 `ForgeSelf.Api.Tests/Plugins/WorkflowEngineTests/WorkflowEnginePluginRegistrationTests.cs`（active，当次落地）；G2 `ProblemClassEnumerationTests.ContractConsumedViaCtxGet_MustBeRegisteredSomewhere`（2026-10-17 本批补）。

## 8 逃逸分析

N/A 首次（当时无问题类体系）。注：本教训后续在 todo 侧疑似复发（用户 2026-10-17 反馈），候选归属 PC-001/PC-004/PC-005，未定论——见 PC-004 候选说明。

## 9 基建改进

促成 architecture-design 铁律 4 的「检查项化」讨论与 PC-001 建档；07 报告 §10/§11 的「同类隐患值得固化」从散文建议升级为带守卫的问题类（悬空教训补账）。

## 10 复用赋能

任何新增跨插件契约（Abstractions 接口）必须回答 PC-001 lens 三问；参照 `AgentDelegationContracts.cs` 头注释三段式模板。

## 11 证据

| 结论 | 等级 | 出处 |
|------|------|------|
| 根因与修法 | Verified | `docs/ai/pilot/2026-10-10-aiagent-tools-features-fix/` 02-spec AC5 / 05-evidence AC5 / 07 §10 |
| G1 守卫当前为绿 | Verified | `dotnet test --filter WorkflowEnginePluginRegistrationTests`（2026-10-17 本批 Verify 环节复跑） |
