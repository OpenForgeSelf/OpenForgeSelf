# Repository Understanding

> 阶段：Stage 0（动手写代码之前必须完成）｜规范：docs/04-standards/ai-native-engineering-workflow.md §2
> 原则：所有条目必须来自**真实仓库内容**，禁止凭常识推测。
> Task ID：PILOT-053 ｜ 目录：`docs/ai/pilot/2026-10-06-tool-bridge/` ｜ 日期：2026-10-06
> 来源等级标注：**Verified**=本次实读文件/命令输出；**Inferred**=据读到的代码推断，未实跑。

## 项目结构

- 仓库根 `d:/src/my-proj/OpenForgeSelf/OpenForgeSelf`（Verified：`ls Plugins/` + AGENTS.md §2.1）
- `ForgeSelf.Api/` — .NET 10 ASP.NET Core 宿主（含 `Plugins/` 子目录=**插件装载器运行时代码**，非插件源码）
- `Plugins/<PascalCase>/` — **插件源码**（本次要新增的落点）。现有 19 个（Verified）：AIAgent / AgentHub / CostScope / DesignSystem / DevTools / FileTools / Home / ImGateway / McpCenter / MemorySystem / QuickLinks / SamplePlugin / Scheduler / ScriptRunner / Sems / SystemMonitor / TextTools / TodoTracker / WorkflowEngine
- `ForgeSelf.Web/` — Vue 3.5 + Vite 6 宿主前端；插件自带前端在 `Plugins/<X>/web/`
- `ForgeSelf.Api.Tests/` `ForgeSelf.Abstractions.Tests/` — 后端 xUnit
- `docs/ai/pilot/` — 任务工件链（pre-commit 硬门禁校验 00-07 八件）
- `docs/02-features/` — 功能档案；现有编号最高 **038**（`038-plugin-local-update-source.md`），039 空闲（Verified：`ls docs/02-features/`）

## 技术栈

| 层 | 技术 | 依据（文件/配置） |
| --- | --- | --- |
| 后端宿主 | .NET 10 / ASP.NET Core / SQLite / NewLife.XCode | `ForgeSelf.Api/ForgeSelf.Api.csproj`、`ForgeSelf.Api/Data/XCodeConfig.cs:24-42`（Verified） |
| 插件 TFM | `net10.0`（宿主与测试工程是 `net10.0-windows`，构建日志出 MSB3271 告警属既有形态） | plugin-development 铁律 12b（Inferred→已在 CostScope 构建日志实证过） |
| 插件依赖边界 | 插件 csproj **只** `ProjectReference` `ForgeSelf.Core` + `ForgeSelf.Abstractions` | 调研实测 19/19 插件；`ForgeSelf.Api.csproj:104-105` 注释原文「避免插件类型进入默认 ALC 造成与 PluginLoadContext 的双重加载」（Verified） |
| 插件前端 | Vue 3.5 lib 模式 ES 产物 + external（vue/vue-router/pinia/element-plus 由宿主 import map 解析） | `Plugins/QuickLinks/web/vite.config.ts:24-30`（Verified） |
| 测试 | xUnit + Moq + FluentAssertions + Coverlet；Vitest；Playwright | `ForgeSelf.Api.Tests/*.csproj`、`ForgeSelf.Web/playwright.config.ts:42-52`（Verified） |

## 架构特点

1. **插件自动发现**：`Extensions` 反射 + `plugin.json` 清单；工具类扩展点（`List<IToolFunctionExtension> ToolExtensions`）由 `ForgeSelf.Api/Plugins/ExtensionPointManager.cs:173-177` 自动注册进全局 `ToolRegistry`（Verified）。**本任务不注册工具扩展点**（见 03-plan 决策 D3）。
2. **鉴权是逐控制器显式的，宿主无全局中间件**：策略声明 `ForgeSelf.Api/AppBuilder.cs:296-301`（`options.AddPolicy("ApiKeyPolicy", ...)`，Verified 实读）；插件管理面必须类级 `[Authorize("ApiKeyPolicy")]`（plugin-development 铁律 17；参考 `Plugins/FileTools/Controllers/FileToolsController.cs:10-16`）。
3. **插件数据目录**：`ctx.EnsurePluginDataDirectory()`（`ForgeSelf.Abstractions/ContextExtensions.cs:23-28` → `{数据根}/plugins/{插件Id}`，Verified）。随数据走的文件放这里，不放发布目录（会被升级覆盖）。
4. **命令执行安全门已存在但是插件私有**：`Plugins/AIAgent/Services/TerminalCommandGuard.cs:32-263`（Verified 实读 1-100 行 + 调研覆盖全文）——三道门（换行/管道/分号即拒 → 可执行名必须裸名且命中白名单 `dotnet/pnpm/node/git/ssh/pwsh` → 约 50 个破坏性 token 词边界红线 + `-EncodedCommand` base64→UTF-16LE 解码后扫描），`MaxOutputBytes=50*1024`、`MaxTimeoutSeconds=30`、`:25` 注释自述「V1 写死常量 + 预留配置扩展位」⇒ **无可配置入口**。命名空间 `ForgeSelf.Api.Plugins.AIAgent.Services` ⇒ 跨插件不可引用。
5. **文件沙箱范式**：`Plugins/AIAgent/Services/ProjectWorkspaceService.cs:98-122` `ResolveSafePath`（归一分隔符 → `Path.GetFullPath` → `StartsWith(root + sep)` 否则拒）。宿主/其它插件里**没有**可复用的通用沙箱函数（调研 grep：其余同类实现全为 private）。
6. **MCP 中心无重叠能力**：`docs/06-research/004-llm-observability-forgeself-design.md:82` 明确裁决「Playground / 重放：本仓 ❌ 无」（Verified）；`Plugins/McpCenter/Services/McpService.cs:207-241` 的 `TestToolAsync` 是 `Thread.Sleep(Random.Shared.Next(50,300))` + 恒 `Success=true` 的**假测试端点**，不含参数入参与结果负载。

## 测试方式

| 层 | 正规入口 | 依据 |
| --- | --- | --- |
| 后端 | `cd ForgeSelf.Api && dotnet build`；`cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~<X>"` | AGENTS.md §5.1/§5.2 |
| 插件单测归属 | `ForgeSelf.Api.Tests/Plugins/<X>Tests/`，并在 `ForgeSelf.Api.Tests.csproj:47-64` 加 `ProjectReference`（缺它 ⇒ CS0234） | 铁律 12b（Verified 读到 csproj 现列 12 个插件） |
| 宿主前端 | `cd ForgeSelf.Web && pnpm run check` + `pnpm run test` | AGENTS.md §5.1 |
| 插件前端 | `cd Plugins/<X>/web && pnpm run build`（沙箱内改用出树构建兜底） | plugin-development §3.2/§四.1 |
| 插件层 e2e | `ForgeSelf.Web/e2e/plugins/<kebab-id>/<kebab-id>.spec.ts`，零 mock、走 `e2e/fixtures/e2e` | `ForgeSelf.Web/playwright.config.ts:42-52`（testDir `./e2e` glob 自动发现，**新增 spec 无需登记**，Verified）；现成最短样例 `e2e/plugins/home/home.spec.ts` |
| 鉴权守卫写法 | 反射扫程序集断言每个控制器带 `ApiKeyPolicy` | `ForgeSelf.Api.Tests/Plugins/DesignSystemTests/DesignSystemAuthTests.cs:18-35`（Verified） |

## 构建命令

```bash
# 后端（含插件构建顺序）
cd ForgeSelf.Api && dotnet build
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~ToolBridge"

# 环境前置（AGENTS.md §5.0，本机必做）
$env:TEMP = $env:TMP = '<repo>\.temp\tmp'
$env:NO_PROXY = 'localhost,127.0.0.1,::1'

# 插件前端（出树构建兜底见 plugin-development §3.2）
cd Plugins/ToolBridge/web && pnpm run build

# 宿主前端门禁
cd ForgeSelf.Web && pnpm run check && pnpm run test

# 插件层 e2e
cd ForgeSelf.Web && bash node_modules/.bin/playwright test e2e/plugins/tool-bridge

# PowerShell 一律 pwsh（禁止 powershell 5.1）
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts\verify-pilot-artifacts.ps1 -TaskId 2026-10-06-tool-bridge
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| `Plugins/<X>/` | 插件源码（`plugin.json` + `IPlugin` + `Controllers/` + `Services/` + `Models/` [+ `Data/`] [+ `web/`]） |
| `ForgeSelf.Api/Plugins/` | 宿主侧插件**装载器**运行时（`PluginManager` / `ExtensionPointManager` / `PluginLoadContext`），⚠️ 易与插件源码目录混淆 |
| `ForgeSelf.Core/` | 插件内核（`Context`/`IContext`/`EventBus`/`Fiber`/`Service`/`Disposable`，Verified：目录仅 7 个 .cs） |
| `ForgeSelf.Abstractions/` | 跨插件共享契约与 DTO（`PluginMetadata`、`IToolFunctionExtension`、`ContextExtensions`、`IDataLocationService`…） |
| `ForgeSelf.Web/e2e/plugins/` | 插件层 e2e（kebab-case 目录，现 9 个：agent-hub/ai-agent/design-system/file-tools/home/im-gateway/mcp-center/quick-links/sems，Verified） |
| `docs/02-features/` | 功能档案（代码是事实源，交付后同步） |

## 代码组织方式

- 后端分层 `Controllers/` → `Services/` → `Models/`；实体走 `Data/Model.xml` → `xcode Model.xml` 生成，业务写 `.Biz.cs`（铁律 9）。**本任务无实体**（台账落 JSON 文件，见 03-plan 决策 D2），故不涉及 Model.xml/建表/`PluginDbs`。
- 命名两层：目录/程序集 PascalCase（`ToolBridge`）、运行时 id kebab（`tool-bridge`）（铁律 2）。
- 插件前端入口导出名必须等于 `plugin.json` 的 `views[0]`（`Plugins/QuickLinks/web/src/index.ts:16-17`：`export { QuickLinksView }` + `export default`，Verified）。
- 纯逻辑（确认编排/文本拼装）必须抽成可单测函数，UI 只做调用（`Plugins/DesignSystem/web/src/delivery/snippets.ts:4`、`AIAgent/web/src/sessionArchive.ts` 范式）。

## 现有工程规范

对本任务有约束力的条目：
- `AGENTS.md` §0 预飞铁律与红线（禁手写一次性 `temp/*.cjs` 作验证；插件任务五步门禁）、§2.3（pwsh 统一）、§5.0（环境前置）、§5.6（门禁分档：本任务碰 `ForgeSelf.Api.csproj` ⇒ **中档**后端全量测试）。
- `docs/04-standards/ai-native-engineering-workflow.md` v1.1.0 §1 硬性约束（**不改 DB 结构**、不新增大依赖、不扩 scope）、§2 九阶段、§1.1 三道闸门。
- `.agents/skills/plugin-development/SKILL.md` 铁律 1-19（重点：2 两层命名、3 界面归插件、4 不能 import 宿主模块、7 改完插件=五步、10 禁止自动删除任何数据目录、13 根视图版本徽标、17 管理面类级鉴权、19 菜单/路由只在 plugin.json 声明一处）。
- 新建插件前置：`.agents/skills/plugin-feasibility-study/SKILL.md`（名实相符三问 + 流程图强制清单 + 扩展性四问）。
- 记忆/规范级既有约定：闸门批准要有出处；「跑通了」= 写明档位；不留第二份真相。

## 候选低风险任务

本轮需求本身即"新增一个独立插件"，可选的最小风险切分：

1. **纯前端粘贴解析 + 结果展示（不真执行）** — 风险最低，但用户明确要求"识别并执行指令 + 结果原样返回"，不满足需求 ⇒ **排除**。
2. **后端解析+执行、无自带界面（用 REST + curl 验证）** — 违反铁律 3「界面归插件」与用户"粘贴到插件里面"的交互描述 ⇒ **排除**。
3. **后端解析+执行 + 自带 Vue 界面 + JSON 文件台账 + 复用既有命令守卫策略（上移为共享纯函数）** ⇒ 采用。零 DB 结构变更、零新 NuGet 依赖、不动宿主业务逻辑；唯一跨插件动作是把 `TerminalCommandGuard` 从 AIAgent 私有移到 `ForgeSelf.Core`（待闸门1 拍板，见 03-plan 决策 D1）。

## 选择该任务的原因

- 需求直指"测试 AI 能否驱动工具"的场景，而本仓**确认不存在**任何"从 AI 自由文本解析工具调用"的代码（Verified：`tool_calls`/`function_call` 只出现在 OpenAI/Anthropic 协议网关的字段名与事件名归一化里；唯一的抠 JSON 函数 `AIAgentService.ExtractJson:1332-1346` 服务于工作流计划，不是工具调用解析）⇒ 是真缺口，非重复建设。
- 三类原子能力（读文件/写文件/执行命令）在仓内已有成熟实现范式与安全策略可对照（`ProjectFileToolFunctions.cs` / `RunTerminalCommandTool.cs` / `TerminalCommandGuard.cs`），新增部分是"解析层 + 编排层 + 回粘格式化"，风险集中在**安全边界**而非未知技术。
- 可完全落在插件目录内，不触宿主业务码与 DB（仅 `ForgeSelf.Api.csproj` +1 行构建顺序登记）。
