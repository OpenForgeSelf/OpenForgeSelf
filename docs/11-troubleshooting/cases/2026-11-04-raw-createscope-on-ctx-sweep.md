---
id: 2026-11-04-raw-createscope-on-ctx-sweep
title: 全仓清扫插件侧 ctx 本体裸 CreateScope（plan_workflow 实证，20 处漏网一次改完）
pc: PC-005
severity: P1
evidence: Verified
---
# Case：2026-11-04 IServiceScopeFactory 解析失败全仓清扫（T13）
> 规则见 [PC-005](../problem-classes/PC-005-plugin-eager-provider-resolve.md)。同族前案：2026-10-17-gateway-list-todos-scope（list_todos）。
## 1 现象
:51888 实例（真实模型）20:54:53：`[AIAgentPlugin] plan_workflow 执行失败: No service for type 'IServiceScopeFactory' has been registered`——AI 规划工作流工具不可用。
## 2 复现红灯
源码级复刻：`PlanWorkflowToolFunction.ExecuteAsync` 在 Apply 传入的 `ctx` 本体（自有服务表，无 IServiceScopeFactory）上裸 `CreateScope()`。守卫红灯 = 新架构守卫 `PluginRawCreateScopeGuardTests`（变异探针实证可翻红：临时回退一处即命中）。
## 3 根因（5 Whys）
1. 为什么抛 IServiceScopeFactory 未注册？→ `CreateScope()` 被调在 ctx 本体上，它不是 MS 容器。
2. 为什么在 ctx 上调？→ 插件 Apply 把 `ctx` 以 `IServiceProvider` 形参传给工具类构造存 `_serviceProvider`，请求期当容器用。
3. 为什么逃过 2026-10-17 治理？→ 那批以三次实抓名单（TodoTracker/sems/AgentHub）逐案修，**未做全仓穷举**；plan_workflow、RecordUsageAsync 共享扩展、DevTools/ScriptRunner/SystemMonitor/MemorySystem/WorkflowEngine 同模式但不在实抓名单。
4. 为什么 RecordUsageAsync 失败没暴露？→ 它 try-catch 静默吞错（统计悄悄丢失，无日志）。
5. 为什么没有守卫兜住？→ 既有 G2 只拦「构造期解析」，不拦「请求期裸 CreateScope 于 ctx 形态字段」。
- **第 6 问：** 同一动作第二次出现就应穷举固化（问题类闭环），逐案修名单法必漏。
## 4 同类扫查命中表（清扫 20 处 / 11 文件）
| 位置 | 处置 |
|------|------|
| AIAgentPlugin.cs PlanWorkflowToolFunction:299（报错主案） | → CreateHostScope |
| ForgeSelf.Abstractions ToolFunctionUsageReportingExtensions:29（RecordUsageAsync，静默吞错型） | → CreateHostScope |
| DevToolsPlugin:279；ScriptRunnerPlugin:190/318/463；CodeSnippetToolFunctions:359/491；ScriptRunnerToolFunctions:75/194/303 | → CreateHostScope |
| SystemMonitorPlugin:251/335/427/576/674；MemorySystemPlugin:441 | → CreateHostScope |
| WorkflowExecutor:1042/1072（WorkflowEngine，RegisterServices(ctx) 实锤同源） | → CreateHostScope |
| Sems ToolExtensions:46-47（手工版 GetService(IServiceProvider) 回落） | 收敛为 CreateHostScope（语义等价，防再分叉） |
| ToolRegistry.cs:649（宿主 ForgeSelf.Api/Services，DI 注入真容器） | 不改（非 ctx 本体），偏差表登记 |
| AppBuilder×3 / PluginManager / pluginProvider 类（BuildServiceProvider 结果）/ 已用 CreateHostScope 点 | 不改（真容器或已治理） |
## 5 修复与守卫
- 统一入口：`PluginContextDiExtensions.CreateHostScope()`（请求期 ctx.GetService(typeof(IServiceProvider)) 命中宿主 seed 根 provider；未就绪抛指名错误）。
- 新守卫 G3：`ForgeSelf.Api.Tests/Architecture/PluginRawCreateScopeGuardTests.cs`（源码扫描 Plugins+Abstractions 非注释行禁 `(_serviceProvider|serviceProvider|provider?)\.CreateScope(`；双向阳性对照：扫描文件数>400 + 3 个已治理锚点必须仍存在，防路径漂移假绿）。
- 漏网原因一句话：上一轮「实抓名单逐案修」未穷举；本批改为「全仓 grep + 接收者形态甄别」并固化守卫，同法下次直接复用。
## 6 结论
清 20/20（含 1 处手工版收敛）；1 处宿主真容器豁免登记。plan_workflow 链路恢复（ctx seed 后 CreateHostScope 正常）。