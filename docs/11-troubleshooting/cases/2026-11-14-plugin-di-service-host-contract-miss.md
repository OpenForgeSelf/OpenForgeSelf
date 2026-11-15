---
id: 2026-11-14-plugin-di-service-host-contract-miss
title: 插件 DI 构造服务经子容器解析宿主/跨插件契约恒 null
pc: PC-034
severity: P1
evidence: Verified         # 本 case 主证据等级：Verified | Inferred | Unknown
---
# Case：2026-11-14 智能终端助手/凭据恒 424（StHostGateway 拿错容器）

> Case 只讲「这一次发生了什么」；规则/修法/扫描方法一律引用 PC，不复述。

## 1 现象

用户贴出运行实例日志一行：`16:13:38.327 [smart-terminal] host_module_unavailable: 宿主对话模块不可用，会话不受影响 智能终端插件`——智能终端右侧助手每次「发送」都 424（`Plugins/SmartTerminal/Services/TerminalSessionService.V2.cs:463`），建凭据/带密钥导入同病（`:439`、`:523`「宿主凭据加密模块不可用」）。会话本身不受影响（按设计降级），但降级是**永久性**的，不是偶发。实例侧 ai-agent v1.10.0 / smart-terminal v2.0.0 均 enabled（只读 GET /api/plugin 实测，:51888），排除「提供方未启用」。

## 2 复现红灯

接线级（真实双容器装配，`ForgeSelf.Api.Tests/Plugins/SmartTerminalTests/StHostGatewayWiringTests.cs`，2026-11-14 23:0x 实跑）：

```
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~StHostGatewayWiringTests"
失败 TryChat_Resolves_Host_Contract_Through_Real_Plugin_Wiring:
  Expected gateway.TryChat() to refer to ...FakeChatCompletion because StHostGateway 经 IContext 通路…, but found <null>.
失败 TrySecrets_Resolves_Host_Contract_Through_Real_Plugin_Wiring: Expected gateway.TrySecrets() not to be <null>
失败! - 失败: 2，通过: 1，总计: 3
```

阳性对照（同测试内 `ctx.Get<IChatCompletion>()`）通过——注册侧通、消费侧拿错容器，缺陷被精确隔离在 StHostGateway 的容器选择。修复后同测试 3/3 绿。

## 3 根因（5 Whys + 第 6 问）

1. 为什么助手恒 424？→ `StHostGateway.TryChat()` 恒 null。
2. 为什么恒 null？→ gateway 对注入的 `IServiceProvider` 调 `CreateHostScope()` 解析，而它注册在插件子容器（`SmartTerminalPlugin.cs:39`），MS DI 把构造参数 `IServiceProvider` 自解析为**子容器本体**——子容器没有 `IChatCompletion`（提供方注册进根 Context 共享表/宿主 seed，只对 IContext 通路可见）。
3. 为什么写成了注入 IServiceProvider？→ 宿主侧惯用法（构造存 provider、用时 CreateHostScope）被照搬进插件；同文件工具函数拿 ctx 好用，形成「有时好有时坏」的误导对照。
4. 为什么单元测试没拦住？→ 2026-11-12 批次单测用显式 host rig 装配 gateway，绕开了真实插件接线；该批次自认「没有做一次真实模型对话」（`docs/ai/pilot/2026-11-12-smart-terminal-open/05-evidence.md:19`）。
5. 为什么 PC-005 守卫（工具构造函数禁解析）没拦？→ 它扫的是**构造期解析 token**，本例是「构造存 provider、**执行期**解析」——规则文本（B4）没覆盖这个形态，守卫签名自然也没有。
- **第 6 问：为什么流程没让我一开始就写对？**——插件 DI 服务的「正确姿势」（构造注入 IContext）只有零散文与实践对照（ImGateway/AgentHub），没有**可执行的不变量**：`architecture-design` 铁律 4 说了子容器只有插件服务+IContext，但没有任何测试在「有人往插件 DI 服务构造函数里塞 IServiceProvider」时报警。本次补齐（G2 枚举守卫）。

## 4 同类扫查命中表

| 位置（文件:行） | 同根因？ | 处置（已修 / 已守卫 / TODO#） |
|-----------------|----------|-------------------------------|
| Plugins/SmartTerminal/Services/StHostGateway.cs:17 | 是（本 PC 本体） | 已修（canonical fix：IContext 通路 + 接线守卫） |
| Plugins/WorkflowEngine/Services/WorkflowExecutor.cs:15,22-27,566-567,1040-1042,1070-1072 | 是（DI 注册 `AddScoped<IWorkflowExecutor,WorkflowExecutor>` + IServiceProvider 构造，半迁移态） | 已修（收敛为单 IContext 构造；L566-567 的脚本步骤恒失败另有 PC-001 提供侧缺口 → TODO#4） |
| Plugins/Scheduler/Services/TaskExecutor.cs:19,23-26,60 | 是（DI 注册 `AddSingleton<ITaskExecutor,TaskExecutor>` + IServiceProvider 构造） | 已修（IContext 构造；AgentRun 路径行为不变） |
| Plugins/Scheduler/Services/WorkflowTaskHandler.cs:11,13-16,46 | 是（DI 注册 `AddSingleton<WorkflowTaskHandler>` + IServiceProvider 构造；DI 子容器路径对 IWorkflowExecutor 会抛 InvalidOperationException） | 已修（IContext 构造，两路一致） |
| ForgeSelf.Api/Plugins/PluginManager.cs（PluginManager 本体） | 否（宿主基建，宿主 DI 生命周期，非插件子容器服务面） | 守卫扫描面有意收窄为 Plugins/ 树（PC-034 §2 已记录） |
| Plugins/{SystemMonitor,ScriptRunner,DevTools,MemorySystem,Scheduler,WorkflowEngine,AIAgent,QuickLinks,TodoTracker} 工具函数 + AgentHub AgentDelegationProvider | 否（对照组：持 ctx 本体或 ctx.Get&lt;IServiceProvider&gt;() 回落宿主根，正道） | 无需改（写进 PC-034 lens 作对照） |
| Plugins/SystemMonitor/SystemMonitorPlugin.cs:37,64-68 | 否（异根因：Apply 期经 ctx 直取插件自有监控服务恒 null → StartSampling effect 空转，PC-005 同族） | TODO#5（来源:输入19 扫查） |
| Plugins/WorkflowEngine/Services/WorkflowExecutor.cs:566-571 | 否（异根因：ScriptRunner 从未 ctx.Register&lt;IScriptExecutor&gt;/&lt;IScriptService&gt; 进共享表 → 工作流脚本步骤恒抛「脚本执行器未注册」，PC-001 同族） | TODO#4（来源:输入19 扫查） |

## 5 修复与被否方案

canonical fix（PC-034 §3）四处应用：StHostGateway / WorkflowExecutor / TaskExecutor / WorkflowTaskHandler 全部收敛为 IContext 构造，执行期解析改 `ctx.Get<T>()`（WorkflowExecutor 的 `_ctx` 字段本已存在，本次完成半迁移）。否决「把 IChatCompletion 加进 HostProvidedServiceContracts / 转发清单」：消费侧拿错容器的病，用注册面蔓延治，回归 PC-001 老路。否决「allowlist 豁存 Scheduler 两文件」：修的是机械构造签名，无行为风险，不值得挂带利息的债。

## 6 重构提案与结论

**R1（局部，本批已随修）**：WorkflowExecutor 半迁移收敛（去掉双构造参数）。**R2 提案（不在本批）**：给 `IContext` 提供 `CreateHostScope()` 语义的官方回落（如 `IHostGateway` 抽象），把「插件要宿主根 scope」的表达面收进内核一处——影响宿主与多插件公共接缝，按闸门1 另批；当前 `ctx.Get<IServiceProvider>()` 回落已够用，暂不动。

## 7 守卫落地

- G1：`StHostGatewayWiringTests.cs`（真实双容器接线 3 例：TryChat/TrySecrets 解析 + 契约缺席保持 null→424 降级契约），active。
- G2：`ProblemClassEnumerationTests.cs` Pc034 两例（检测器自证 + 全插件枚举，allowlist 空账），active。
- 逃逸说明：首见即建 PC，无「守卫已在但没拦住」的前案；2026-11-12 批次的 rig 假绿由 G1 接线级装配直接堵住。

## 8 逃逸分析（二次复发必做，首见可写「N/A 首次」）

N/A 首次。同族背景：PC-005（构造期 eager 解析，2026-10-08 同族未建 PC 已记逃逸一次）——本 PC 与其互补（构造期 vs 执行期），lens 已互指。

## 9 基建改进

- 问题类台账新增 PC-034 + 两级守卫（G1 接线装配范式可复用于其它「真实接线 vs 显式 rig」的争议场景）。
- `Plugins/SmartTerminal/docs/03-架构与接口.md` §1 补「宿主契约必须经 IContext 解析」精度条款 + 424 两条排查路（问题类在消费侧文档的落点）。
- 本批 G2 守卫首跑即真实命中 3 文件并全数修复（非 allowlist），证明守卫有牙。

## 10 复用赋能

新插件服务、插件控制器依赖、任何「进插件子容器」的类：写构造函数时按 PC-034 lens 自查（IContext only）；e2e/单测写插件服务测试时按 G1 范式用真实双容器装配；Review 插件 PR 时对照命中表对照组。Spec 模板如涉「插件消费宿主契约」，Failure Modes 引 PC-034。

## 11 证据

| 结论 | 等级 | 出处（文件:行 / 命令输出 / 截图） |
|------|------|-----------------------------------|
| 424 恒降级来自 TryChat 恒 null | Verified | `.temp/red-st-gateway.log`（接线红灯 2 例）+ TerminalSessionService.V2.cs:463 |
| MS DI 对插件子容器 IServiceProvider 自解析为本体 | Verified | `ForgeSelf.Api.Tests/PluginContextDiExtensionsTests.cs:34-43` 注释与断言 + PluginServiceRegistry.cs 头注释 |
| IChatCompletion 注册侧可用（阳性对照） | Verified | 接线测试同测内 `ctx.Get<IChatCompletion>()` BeSameAs 通过 |
| 修复后接线绿 | Verified | `.temp/green-st-gateway.log`（3/3）+ `.temp/g2-third-run.log`（5/5 含 G2） |
| G2 守卫咬合（首跑 3 真实命中） | Verified | `.temp/g2-second-run.log`（TaskExecutor 命中）+ pwsh 复刻全量清单（4 命中） |
| ai-agent/smart-terminal 实例均启用 | Verified | 只读 GET /api/plugin（:51888，2026-11-14 输入18） |
