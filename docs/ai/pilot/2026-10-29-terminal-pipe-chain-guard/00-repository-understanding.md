# Repository Understanding

> 阶段：Stage 0｜规范：docs/04-standards/ai-native-engineering-workflow.md §2
> Task ID：PILOT-075｜日期：2026-10-29｜任务：执行命令工具放开管道/链式，每段走同一拦截校验

## 项目结构

- 宿主 `ForgeSelf.Api/`（.NET 10 + NewLife.XCode），前端 `ForgeSelf.Web/`（Vue3 + Vite），插件在仓库根 `Plugins/<PascalCase>/`（不是 `ForgeSelf.Api/Plugins/`——那是宿主装载器）。
- 本任务全部落点在两个插件的后端 + 测试工程 `ForgeSelf.Api.Tests/`，不触宿主源码、不触前端。

## 技术栈

| 层 | 技术 | 依据 |
| --- | --- | --- |
| 插件 mcp-center | .NET 10 类库，公共工具 `run_terminal_command` | `Plugins/McpCenter/Services/PublicTools/RunTerminalCommandTool.cs` |
| 插件 tool-bridge | .NET 10 类库，外部 AI 回合台 `run_command` | `Plugins/ToolBridge/Services/CommandExecutor.cs`、`ToolSpec.cs:77` |
| 测试 | xUnit + FluentAssertions + Moq（`ForgeSelf.Api.Tests`） | `ForgeSelf.Api.Tests/Plugins/TerminalCommandGuardTests.cs` 等 |

## 架构特点（本任务触碰面）

1. **守卫、两份实现、对账钉死**：`TerminalCommandGuard`（mcp-center）与 `CommandGuard`（tool-bridge）逐条同值；
   不能合并——守卫带 `XTrace.Log`，而共享层 Core/Abstractions 不引 NewLife（`ToolBridgeGuardParityTests.cs:11-14`）。
   漂移由金样表对账拦截（`ForgeSelf.Api.Tests/Plugins/ToolBridgeTests/ToolBridgeGuardParityTests.cs`）。
2. **三道门 + 破坏性红线**（`TerminalCommandGuard.cs:19-31`）：第 1 门可执行名白名单
   （dotnet/pnpm/node/git/ssh/pwsh，`TerminalCommandGuard.cs:41`）；第 2 门**整串字符级拒绝** `| ; & < > 反引号 换行`
   （`TerminalCommandGuard.cs:106-119`——本任务要改的就是这道门）；第 3 门 cwd 必填（工具内直检，
   `RunTerminalCommandTool.cs:107-120`，2026-10-29 拍板）；破坏性红线 = 位置参数精确匹配 + 内联代码词边界扫描 +
   `-EncodedCommand` base64 载荷解码扫描（`TerminalCommandGuard.cs:141-238`）。
3. **执行姿势**：守卫先于进程，拒绝路径零子进程；`Process`（UseShellExecute=false，不经 shell 解释）直启首 token，
   `Arguments = rest` 原样透传（`RunTerminalCommandTool.cs:122-138`）；stdout/stderr 抓原始字节交
   `ProcessOutputText` 裁决（PC-011，不固定码页）。tool-bridge `CommandExecutor` 同构（`CommandExecutor.cs:39-53`）。
4. **消费方**：`run_terminal_command` 注册进宿主 ToolRegistry（`Plugins/McpCenter/McpCenterPlugin.cs:57`），
   AIAgent 双引擎（React/MAF）经工具注册表调用；tool-bridge `run_command` 经 `ToolDispatcher.cs:51-53`。
   改两份守卫即覆盖全部命令执行入口。

## 测试方式

- 守卫纯逻辑单测（零子进程）：`TerminalCommandGuardTests`（250 行，含第 2 道门整组断言）。
- 对账常驻判据：`ToolBridgeGuardParityTests`（金样表两实现互比判定 + 拒绝原因原文）。
- 真实子进程测试先例：`RunTerminalCommandEncodingTests`（真 pwsh）、`ToolBridge/ExecutorTests.cs:317-333`（真起 pwsh/node）——本任务的链式真实执行测试有既有姿势可循。
- e2e：`ForgeSelf.Web/e2e/plugins/mcp-center/`（插件冒烟）、`e2e/plugins/ai-agent/tool-gateway-031.spec.ts` 场景 4（run_terminal_command 白名单放行，真实 LLM 链路）。

## 构建命令

```bash
cd ForgeSelf.Api && dotnet build
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~TerminalCommandGuard|FullyQualifiedName~ToolBridge|FullyQualifiedName~RunTerminalCommand|FullyQualifiedName~McpCenter"
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| `Plugins/McpCenter/Services/PublicTools/` | 平台公共工具（run_terminal_command 及其守卫/DTO） |
| `Plugins/ToolBridge/Services/` | 工具桥（初始提示词 PromptBuilder、解析 CallParser、执行 Executor、守卫副本） |
| `ForgeSelf.Api.Tests/Plugins/` | 插件后端测试（TerminalCommandGuardTests 在根、McpCenterTests/、ToolBridgeTests/） |
| `ForgeSelf.Web/e2e/plugins/` | 插件冒烟 e2e（每插件 1 条，铁律 24） |

## 代码组织方式

工具实现 IToolFunctionExtension（Id/Name/Description/ParametersJsonSchema/ExecuteAsync）；守卫为纯静态类
（Check/SplitExecutable/Truncate），描述文案（Description/Schema/提示词）由守卫常量派生
（`PromptBuilder.cs:64-68`——「手抄会漂移」的反向探针先例）。

## 现有工程规范（对本任务有约束力）

- AGENTS.md §2.4 plugin-development（已读）：改完插件 = 门禁 + 冒烟 e2e + 起源码走查 + 发布（发布另行征得同意）；描述与实现必须同源。
- AGENTS.md §5.6 门禁分档：本任务触 `Plugins/**`（非宿主源码/scripts/共享夹具）→ **快档**。
- 插件 DI 时序（铁律 26）：本任务不改构造注入面。
- AI-Native 规范 §1：不新增依赖、不无关重构、验证须真实跑。

## 适用问题类（Stage 0 增补节）

| PC | 是否命中 | 结论 |
| --- | --- | --- |
| PC-011（起子进程消费 stdout/stderr / 码页裁决） | **命中（lens）** | 触碰区域 = 子进程执行姿势。lens：新增链式执行路径**必须**沿用原始字节捕获 + `ProcessOutputText` 裁决，禁止在 pwsh 路径设置 StandardOutput/ErrorEncoding；码页对策进 Spec Failure Modes 预填 |
| PC-010（stdio 外部进程 shim/帧协商） | 否 | 那是 MCP stdio 客户端域，与命令执行无关 |
| PC-006（PS 5.1 编码/BOM） | 否 | 不写 *.1 脚本；链式执行显式用 `pwsh`（7）并经 ArgumentList 传参 |
| PC-014/015 | 否 | 不触测试夹具连接串 / 流式分片 |

## 候选低风险任务

用户直接指令（输入5）：执行命令工具的拦截「禁止管道和链式」不合理，放开支持；每个管道/链式段落走同样的拦截校验。

## 选择该任务的原因

用户最高优先级指令；触碰面收敛（2 份守卫 + 2 个执行器 + 测试 + 文案）；有对账测试与既有真实子进程测试姿势兜底；不涉 DB/鉴权/生产。
