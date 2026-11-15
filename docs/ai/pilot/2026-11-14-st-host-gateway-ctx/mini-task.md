# Mini-Task：智能终端助手/凭据恒 424 —— StHostGateway 拿错容器（子容器自解析，非 IContext）

> 日期：2026-11-14｜级别：轻量（≤3 业务文件缺陷修复，Intent/Spec/Plan/Task 合并单文件）
> **闸门1：待用户批准后开工**（本文件即待批方案；触发输入 = 用户粘贴的 :51888 宿主日志 `[smart-terminal] host_module_unavailable: 宿主对话模块不可用，会话不受影响`，2026-11-14 16:13:38）
> **适用问题类**：新问题类候选——「插件 DI 构造的服务注入 `IServiceProvider`（MS 自解析 = 插件子容器）后执行期解析宿主契约恒 miss」；与 PC-005（构造期 eager 解析）/ b-engineering-daily §B4（构造期禁解析）同族但**不同例**：本例解析发生在执行期、写法合规（CreateHostScope），错在**拿到的容器对象**不是宿主根。

## Intent（为什么/做什么/到什么程度）

- **Problem**：:51888 实例（smart-terminal v2.0.0 + ai-agent v1.10.0 均已启用，经只读 GET /api/plugin 核实）终端右侧助手发消息 100% 返回 424 `host_module_unavailable`（用户日志 + 代码链吻合）。
- **Why（根因链，Inferred——码链完整闭合，待红灯测试升级 Verified）**：
  1. `StHostGateway` 构造函数注入 `IServiceProvider`（`Plugins/SmartTerminal/Services/StHostGateway.cs:17`）。
  2. 它注册在 smart-terminal **插件子 ServiceCollection**（`SmartTerminalPlugin.cs:39`），由子 provider 构造 ⇒ MS DI 对 `IServiceProvider` 自解析 ⇒ 注入的是**子容器本体**（`PluginManager.cs:684-687` 每插件独立集合；`PluginManager.cs:781` 只向子容器注册 `IContext`，不注册 IServiceProvider 指向宿主）。
  3. 执行期 `TryChat()` 调 `_services.CreateHostScope()` ⇒ `RequireHostProvider` 分支①在真 MS 容器上返回**该容器自身**（仓内自证：`PluginContextDiExtensionsTests.cs:34-43` 注释「真容器的 GetService(typeof(IServiceProvider)) 返回根 scope」）⇒ 作用域 = 子容器。
  4. 子容器里**没有** `IChatCompletion`（该契约由 ai-agent 插件注册进自己的子集合 + 根 Context 共享表，`AIAgentChatCompletion.cs:186-187`）⇒ `GetService` 返回 null。
  5. ⇒ `TerminalSessionService.V2.cs:463` 抛 `HostUnavailable("宿主对话模块不可用，会话不受影响")` ⇒ 控制器记 `[smart-terminal] host_module_unavailable: …`（`SmartTerminalController.cs:355`）并回 424。
  6. 同根因波及：`TrySecrets()`（`StHostGateway.cs:19`）解析 `ISecretEncryptionService` 同样恒 null ⇒ 建凭据/带密钥导入恒 424「宿主凭据加密模块不可用」（`TerminalSessionService.V2.cs:439/523`）。
  7. **为何能活到今天**：2026-11-12 pilot 05-evidence 自认「没有做一次真实模型对话；助手作用域与工具 DI 由单测和源码守卫证明」（`docs/ai/pilot/2026-11-12-smart-terminal-open/05-evidence.md:19`）；单测/集成测试用显式 host 装配（rig 不含真宿主 DI 接线），`SmartTerminalV2Tests` 不触 SendChat。对照组：8 个 st_* 工具拿的是 **ctx 本体**（`SmartTerminalPlugin.cs:77-79` 传 `ctx`），`CreateHostScope()` 走 Context→宿主根 seed，所以工具好用、助手不好用。
- **Expected Outcome**：终端助手对话与凭据加密解析走 **IContext 父链**（Cordis 正道），在 ai-agent 启用的实例上恒可解析；ai-agent 缺席/禁用时仍按契约回 424（优雅降级语义不变，`03-架构与接口.md:23`）。
- **Constraints**：不改 424 契约与错误文案；不动 st_* 工具（好用）；不动运行实例（修复随下次插件发布生效）；不重启任何宿主进程。

## Spec（要点）

- FR-1 `StHostGateway` 改注入 `IContext`（`PluginManager.cs:781` 已保证子容器可注），`TrySecrets/TryChat` 改经 ctx 解析（`ctx.Get<T>()` 直取共享表，或 `ctx.CreateHostScope()`——以 red→green 实测取一，优先 `ctx.Get<T>()` 与 `ImGatewayRouter.cs:85` 同款）；解析不到仍返回 null（424 契约不变）。
- FR-2 补**接线级守卫测试**（G2）：按真实宿主装配（根 Context seed 宿主根 provider + 插件子 DI 容器）断言「从子容器解析 IStHostGateway 后 TryChat/TrySecrets 能拿到契约」——现装配下红，修复后绿，防 rig 再造假绿。
- FR-3 **同类扫查**（问题类闭环，本任务范围内完成）：排查其他插件 DI 构造服务中「注入 IServiceProvider + 执行期解析**宿主契约**」的同类写法（SystemMonitor:251/335/427/576/674、WorkflowExecutor:1042/1072、ScriptRunner:190/318/463、DevTools:279、MemorySystem:441 等已登记为扫查起点；解析**插件自有**服务者不算同类命中）。
- 非目标：HostProvidedServiceContracts 清单调整、AIAgent 注册方式、助手 UI、发布动作。
- **无界面交互**：本批纯后端修复，无 UI 变化；失败语义仍走既有 424 `{code,message}`。

## Plan

1. `Plugins/SmartTerminal/Services/StHostGateway.cs`：ctor 参数 `IServiceProvider` → `IContext`；TryGet 改 ctx 通路（同步更新接口注释）。
2. `ForgeSelf.Api.Tests/Plugins/SmartTerminalTests/`：新增接线守卫测试（真实双容器装配，先红后绿）。
3. 同类扫查 5+ 文件（只读甄别，命中且同根因 → 本批一并修或单列，异根因 → TODO）。
4. 文档回写：插件 03-架构与接口.md（§1 铁律 26 处补「必须经 IContext，禁止经插件子容器注入 IServiceProvider 解析宿主契约」+ 424 排查指引）；问题类归档 `docs/11-troubleshooting/`（新 PC 或并入现有类，按台账裁决）。

## Task

- **Allowed**：`Plugins/SmartTerminal/Services/StHostGateway.cs`、`ForgeSelf.Api.Tests/Plugins/SmartTerminalTests/**`、同类扫查命中文件（如需）、`Plugins/SmartTerminal/docs/03-架构与接口.md`、`docs/11-troubleshooting/**`、`docs/ai/pilot/2026-11-14-st-host-gateway-ctx/**`、TODO/日记。
- **Forbidden**：`TerminalSessionService.V2.cs` 的 424 语义与文案、st_* 工具文件、AIAgent/ImGateway 文件、启停宿主进程、:51888 实例写操作、发布/打 tag。
- **验收标准（AC）**：
  - [ ] 红：接线守卫测试在现装配下失败（子容器解析 IChatCompletion 为 null）。
  - [ ] 绿：改 ctx 通路后同测试通过；`TrySecrets` 同测通过。
  - [ ] 回归：SmartTerminal 全域过滤集 `dotnet test --filter` 绿；`pwsh scripts/check-commit-hygiene.ps1 -Staged` 绿。
  - [ ] 同类扫查有结论清单（命中/不命中各带文件:行与判据）。
  - [ ] 真实链路佐证：dev-stack 起源码前后端，终端助手发一条消息得到助手回复（不再 424）——「AI 离线」不能替代（2026-11-12 教训）。

## Test Plan（含 PC 守卫）

- 新增守卫（红→绿）：接线级测试（FR-2，命名随实现定，挂 SmartTerminalTests）。
- 复跑既有：SmartTerminalTests 全部（TerminalOpenParser/PermissionClassifier/ShellProfileDetector/V2/LaunchGuard）。
- 快档门禁：`pnpm run check`（未触前端则说明跳过理由）+ 后端过滤集 + 提交卫生。
- 插件硬性门禁六步中：①门禁 ②插件冒烟 e2e（smart-terminal 既有 1 条）③起源码走查（助手发消息真链路）随本批执行；④⑤发布/运行实例复验**待用户批准后另行进行**（本批不发布）。
