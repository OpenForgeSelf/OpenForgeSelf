# Repository Understanding

> 阶段：Stage 0｜规范：docs/04-standards/ai-native-engineering-workflow.md §2
> Task ID：PILOT-ds-m1-agent-tools｜日期：2026-10-01｜所属：设计插件升级 M1（M2/M3 各自另立目录）
> 所有条目来自真实仓库内容（读码/读文档/跑命令），来源标注在「依据」列。

## 项目结构

- 仓库根：`ForgeSelf.slnx`、`ForgeSelf.Abstractions/`（契约层）、`ForgeSelf.Core/`（内核 IContext/EventBus/Fiber）、`ForgeSelf.Api/`（宿主，含插件加载）、`ForgeSelf.Api.Tests/`（后端测试）、`ForgeSelf.Web/`（宿主前端 + Playwright e2e）、`Plugins/<X>/`（18 个插件，每个自带 `plugin.json` 与可选 `web/`）、`docs/`、`scripts/`、`.agents/skills/`。
- 本任务对象：`Plugins/DesignSystem/`（v2.7.1，HEAD=61b327d「库驱动设计系统底座 v2.7.1」）——`Controllers/DesignSystemController.cs`（类级 `[Authorize("ApiKeyPolicy")]`，路由 `api/design-system`，39 个端点）、`Services/`（19 个文件：`DesignGenerator`/`ExportService`/`AuditEngine`/`ReleaseService`/`TokenRepository`/`CatalogRepository`…）、`Data/Model.xml`（12 表）、`web/`（Vue 3 + Vite lib，14 个 section）。
- 关联插件：`Plugins/McpCenter/`（对外 MCP 网关）、`Plugins/AIAgent/`（内置 agent）、`Plugins/Sems/`（工具接入范例）。

## 技术栈

| 层       | 技术                                                                              | 依据（文件/配置）                                                                                                 |
| -------- | --------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| 后端     | .NET 10 / C# / NewLife.XCode 12.0.2026.701 + NewLife.Core 11.17.2026.701 / SQLite | `Plugins/DesignSystem/DesignSystem.csproj`（net10.0、Nullable+ImplicitUsings 开）                                 |
| 插件前端 | Vue 3.5 + Vite lib（vue/element-plus 全 external，模板只用原生标签）              | `Plugins/DesignSystem/web/vite.config.ts`                                                                         |
| 后端测试 | xUnit + FluentAssertions（`.Should()`）+ Moq                                      | `ForgeSelf.Api.Tests/Plugins/DesignSystemTests/*.cs`（11 个文件，`[Collection("XCode")]` + 每类独立 SQLite 目录） |
| 前端测试 | vitest（借宿主入口，include 含 `Plugins/*/web/src/**/*.test.ts`）                 | `Plugins/DesignSystem/web/package.json`、`design-system-verify` 技能 §一                                          |
| e2e      | Playwright，真实宿主零 mock                                                       | `ForgeSelf.Web/e2e/plugins/design-system/design-system.spec.ts`（单个巨型用例，强依赖 14 个导航文案）             |

## 架构特点

- **插件向宿主 agent/MCP 暴露工具的既有路径**（Verified，读码）：插件类公开 `List<IToolFunctionExtension> ToolExtensions` → `ForgeSelf.Api/Plugins/ExtensionPointManager.cs` `DiscoverExtensionsFromPlugin` 反射收集并 `RegisterTool` 进全局 `ToolRegistry`（按 `Name` 唯一；`RemovePluginExtensions` 按 Id 注销）。范例：`Plugins/Sems/ToolExtensions.cs`（13 个 `sems_*`，基类统一封套/异常兜底/用量上报）+ `SemsToolExtensionTests`。
- **McpCenter 网关**（Verified）：`tools/list` 恒只有 1 个 `universal_tool`（`{tool,parameters}` 转发到宿主 `IToolRegistry`），发现靠同插件注册的 `list_tools`（`keyword` 过滤）；e2e 用 `FORGESELF_MCP_GATEWAY_PORT`（默认 18889）直连。
- **内置 AIAgent 工具白名单**（Verified）：`Plugins/AIAgent/Services/AIAgentService.cs` `ResolveOwnToolDefinitions` 只挂 `ai-agent` 自己 + `memory-system` 两个插件的工具（注释：全量 ~77 个会撑爆本地小模型 prompt）；`UniversalTool` 类存在但**未被 AIAgentPlugin 注册**。AIAgent.csproj 已 `InternalsVisibleTo ForgeSelf.Api.Tests`。
- **DI/服务解析陷阱**（Verified）：插件 `Apply(IContext)` 里的 ctx 不是 MS DI 的 `IServiceProvider`（TODO 输入19：`CreateScope()` 抛 IServiceScopeFactory 缺失）；宿主根 provider 由 `PluginManager.ProvideHostServices` 在 Apply 之后 seed 进根 Context，工具需在**调用时**才 `ctx.Get<IServiceProvider>()`。
- 设计插件现状（Verified）：`DesignSystemPlugin.Apply` 建表 → 首植内置图标 → 以 `services.AddSingleton<T>()` 注册 7 个服务；**没有 `ToolExtensions`**；生成入口 `DesignGenerator.ApplyToProject`+`SeedComponentCatalog`+`SeedBrandCatalog`+`AuditEngine.Run` 由控制器 `Generate` 内联编排；`ExportService.Snapshot`/`TokenGraph` 是纯记录，可脱离 DB 构造。

## 测试方式

- 后端：`dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~DesignSystem" --logger "console;verbosity=normal"`，核对报告总数 == `--list-tests` 发现数（技能 `design-system-verify` §一：quiet 模式测试主机崩溃会假绿）。
- 插件前端：`cd Plugins/DesignSystem/web && pnpm run check && pnpm run test && pnpm run build`。
- 插件层 e2e：`cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/design-system`。
- 验收自查：`.agents/skills/design-system-verify/SKILL.md` 第二节 33 条「假能力自查表」。

## 构建命令

```bash
dotnet build Plugins/DesignSystem/DesignSystem.csproj
dotnet build Plugins/AIAgent/AIAgent.csproj
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~DesignSystem" --logger "console;verbosity=normal"
```

## 主要目录职责

| 目录                                | 职责                                                             |
| ----------------------------------- | ---------------------------------------------------------------- |
| `Plugins/DesignSystem/Services/`    | 设计系统的后端真相：色彩数学、令牌图、生成、审计、导出、发布快照 |
| `Plugins/DesignSystem/Controllers/` | REST 面（新增端点一律进现有控制器，类级鉴权覆盖）                |
| `Plugins/DesignSystem/web/`         | 插件自带界面（M1 不改；M2 重构）                                 |
| `Plugins/Sems/`                     | 「插件对外暴露工具」范例                                         |
| `Plugins/McpCenter/`                | 对外 MCP 网关 + `list_tools`                                     |
| `ForgeSelf.Api/Plugins/`            | 插件管理、扩展点发现                                             |

## 代码组织方式

- 命名空间 `ForgeSelf.Api.Plugins.DesignSystem[.Services|.Controllers|.Data]`；C# 风格用 NewLife 类型别名（`String`/`Int64`/`Boolean`）；服务类多为 `sealed`；注释中文，讲"为什么"。
- 唯一性/存在性判断一律直查 DB（技能铁律 11）；写路径整批事务；只读请求可对 BUSY 重试、写请求绝不重试（`web/src/http.ts` 既有纪律）。
- 数据安全铁律 10：插件代码不得删除库文件/数据目录（归档=软删）。

## 现有工程规范（对本任务有约束力）

- AGENTS.md §0 预飞铁律 + §5.6 门禁分档 + §11 AI-Native 九阶段；`plugin-development` 铁律 9/10/11/12/13/17/18；`design-system-verify` 33 条自查（尤其 #16 声明能力必须有种子+写入口+e2e、#22 产物数字可当场核对、#24 新判据必须造反例证明会响、#27 词表要被消费）。
- 版本三元组（`DesignSystemConstants.ModelVersion/GeneratorVersion/ProjectionVersion`）必须与 `plugin.json.Version` 同步；`DesignSystemAuthTests` 断言这一点。

## 候选低风险任务

1. 仅为 `design-system` 补 `ToolExtensions`（只读工具 3 个）——最小闭环，但不满足「审查/创建/内置 agent」。
2. 完整 M1：8 个 `design_*` 工具 + 共用服务 + REST 对等端点 + AIAgent 白名单 + 测试 + 文档/技能（用户已选定）。
3. 直接做 M2 界面——依赖 M1 的预设/快速创建/brief，不宜先做。

## 选择该任务的原因

用户在计划轮明确选择「先 M1 Agent 工具层」（推进顺序 A）、「AIAgent 白名单加入 design-system，工具收敛到 ≤8 个」（内置 agent 接入 A），并在 2026-10-01 下达「Start implementation」。M1 以后端为主、不动插件前端、不改库结构，回归面小；其产出（brief / 预设 / 审查 / 快速创建服务）是 M2 向导与展厅、M3 规范的共同底座。
