---
id: 2026-10-17-gateway-list-todos-scope
title: 通用网关调 list_todos 报 IServiceScopeFactory 未注册
pc: PC-005
severity: P1
evidence: Verified
---
# Case：2026-10-17 通用网关 list_todos DI 解析失败

## 1 现象

McpCenter `universal_tool` 入参 `{parameters:"{}", tool:"list_todos"}` 返回 `{success:false, error:"No service for type 'Microsoft.Extensions.DependencyInjection.IServiceScopeFactory' has been registered."}`（用户原文，2026-10-17 输入下午；另附 ABA001 `BuildLocalAgent` 同型报错——该类型不在本仓，仅记同族佐证）。

## 2 复现红灯

复现批（本会话）：`UniversalGatewayListTodosScopeReproTests.cs` 真实 Context/ToolRegistry/UniversalToolForwarder 时序复刻，错误串逐字锚定，红灯复现成功（Verified，同日 05-evidence）。随后同日修复批（PILOT-062，`docs/ai/pilot/2026-10-17-plugin-di-helper-todo-fix/`）把该特征化测试**翻转为成功回归** `UniversalGatewayListTodosScopeRegressionTests.cs`（仍在 seed 之前构造工具，断言成功封套）。

## 3 根因（5 Whys + 第 6 问）

TodoTracker 工具类在 Apply 期 eager 解析宿主 provider → 早于 `ProvideHostServices` seed → 拿到的 provider 无 IServiceScopeFactory。
- Why1 报错？解析出的 provider 缺服务。Why2 缺？seed 未发生。Why3 未发生？解析发生在 Apply 期。Why4 Apply 期解析？工具构造时缓存了 provider 引用（构造期 eager）。Why5 为什么写成 eager？构造注入习惯 + 单插件 dev 装配看不出。
- **第 6 问**：2026-10-08 AgentHub 同族缺陷（`AgentHubDi.ResolveHost`，恒空）修复时**没做同类扫查、没建 PC**，规律只进了会话记忆（「禁止构造期缓存，统一执行期 scope」铁律），流程里没有任何 Stage 问「同类在哪」⇒ 本次为**逃逸**。

## 4 同类扫查命中表

| 位置 | 同根因？ | 处置 |
|------|----------|------|
| TodoTracker 工具 Apply 期 eager 解析（本 case） | 是 | 已修（PILOT-062 修复批同日落地：PluginContextDiExtensions + 工具改惰性解析） |
| AgentHub AgentDelegationProvider/工具基类（10-08，已修） | 是（同族，容器错误变体） | 已有 AgentHubDi.ResolveHost + 回归 2 用例；补记于 PC-005 §1 |
| 其余插件工具类 | 已枚举 | G2 架构守卫 `PluginToolConstructorDiGuardTests.cs`（active，拦构造期 eager 写法本身） |

## 5 修复与被否方案

本 case（复现批）只复现不修复（用户当时指令）；同日修复批 PILOT-062 已按 PC-005 canonical fix 落地（Abstractions 新增 PluginContextDiExtensions，TodoTracker 工具改持 IContext 惰性解析），修法与否决方案见 PC-005 §3。

## 6 重构提案与结论

R1 局部：工具改执行期 scope（修复批 ≤3 文件，不动公共契约面新增统一助手），已在 PILOT-062 批 07 披露。

## 7 守卫落地

G1 `UniversalGatewayListTodosScopeRegressionTests.cs`（active）；G2 `Architecture/PluginToolConstructorDiGuardTests.cs`（active）——两守卫均由同日修复批 PILOT-062 落地，PC-005 升 guarded。

## 8 逃逸分析

前案 = 10-08 AgentHub DI 修复 → 缺口归因：**散文/记忆有教训、无索引无守卫、没有 Stage 问同类** → 上浮动作：本 case + PC-005 建档、规范 v1.2.0 Stage 0/2/3 增补（适用问题类/Failure Modes/守卫进 Test Plan）。

## 9 基建改进

本 case 是问题类闭环机制的**首个在役缺陷样本**：直接促成 PC-005 与规范九问/三节接入（同批落地）。

## 10 复用赋能

任何插件工具/服务类的新增（plugin-development 流程）必须回答 PC-005 lens 三问；MCP 网关转发链改动 Review 时对照本 case。

## 11 证据

| 结论 | 等级 | 出处 |
|------|------|------|
| 错误复现（逐字锚定）→ 修复后翻转为成功回归 | Verified | `docs/ai/pilot/2026-10-17-gateway-list-todos-scope-repro/05-evidence.md`（复现）+ `docs/ai/pilot/2026-10-17-plugin-di-helper-todo-fix/`（修复）+ 测试文件 |
| 10-08 同族缺陷关联 | Inferred | `docs/ai/pilot/2026-10-08-todo-chain-close/05-evidence.md` 偏差记录④ |
