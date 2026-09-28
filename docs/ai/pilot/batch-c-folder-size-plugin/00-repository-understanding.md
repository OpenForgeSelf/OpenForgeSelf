# Repository Understanding（批次C · 文件夹大小统计插件）

> 阶段：Stage 0｜规范：`docs/04-standards/ai-native-engineering-workflow.md` §2
> 原则：以下每条均有真实文件/行号依据，未做常识推测。task-id: `batch-c-folder-size-plugin`｜落稿：2026-09-27

## 项目结构

- 解决方案：`ForgeSelf.slnx`（新格式 XML 解决方案，插件项目以 `<Project Path="Plugins/X/X.csproj" />` 挂在顶层或 `/Backend/Plugins/` 文件夹下）
- 宿主后端：`ForgeSelf.Api/`（ASP.NET Core，TFM **`net10.0-windows`**，`ForgeSelf.Api.csproj`:4）
- 宿主框架内插件机制目录：`ForgeSelf.Api/Plugins/`（`PluginManager.cs`、`ExtensionPointManager.cs`、`PluginLoadContext.cs`、`Services/PluginFrontendFileMiddleware.cs` 等，**不放插件项目**）
- 插件项目实际落点：**仓库根 `Plugins/<PascalCase>/`**（18 个目录；注意 `.agents/skills/plugin-development/SKILL.md`:206/:250 写作 `ForgeSelf.Api/Plugins/<X>/`，与仓库实际不符 → 见 03-plan 偏差记录 D-0）
- 宿主前端：`ForgeSelf.Web/`（Vue 3 SPA + e2e 体系 `ForgeSelf.Web/e2e/**`）
- 后端测试：`ForgeSelf.Api.Tests/`（插件相关测试在 `ForgeSelf.Api.Tests/Plugins/`）、`ForgeSelf.Core.Tests/`、`ForgeSelf.Abstractions.Tests/`
- 规范与工件：`docs/04-standards/`、`docs/18-templates/ai-pilot/`、`docs/ai/pilot/<task-id>/`、`docs/07-decisions/not-taken-decisions.md`

## 技术栈

| 层 | 技术 | 依据（文件/配置） |
| --- | --- | --- |
| 后端宿主 | .NET 10（`net10.0-windows`）+ ASP.NET Core | `ForgeSelf.Api/ForgeSelf.Api.csproj`:4 |
| 插件项目 | `net10.0` + `FrameworkReference Microsoft.AspNetCore.App` + `NewLife.Core 11.17.2026.701` | `Plugins/QuickLinks/QuickLinks.csproj`:4-19 |
| ORM/数据 | NewLife.XCode 12.0.2026.701 + SQLite（**无实体的插件不引 XCode**） | `Plugins/QuickLinks/QuickLinks.csproj`:17；`Plugins/FileTools/FileTools.csproj` 无 XCode 引用 |
| 前端 | Vue 3.5.13 / Vite 6.2.4 / TS 5.7.2 / Element Plus 2.14.3 / Tailwind 4.3.3 / Pinia 3.0.0 / pnpm 10.13.1 | `ForgeSelf.Web/package.json`:12-57（`packageManager`/`engines` 同文件） |
| 单测 | vitest 3.2.4（jsdom） | `ForgeSelf.Web/package.json`:54、`ForgeSelf.Web/vitest.config.ts`:28 |
| e2e | Playwright 1.61.1 | `ForgeSelf.Web/package.json`:38 |
| 后端测试 | xUnit + Moq + FluentAssertions | `ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj` |

## 架构特点

1. **插件发现/加载**：`PluginManager.DiscoverPlugins()`（`ForgeSelf.Api/Plugins/PluginManager.cs`:245）扫 `AppContext.BaseDirectory/Plugins` 下每个含 `plugin.json` 的目录（:257/:262-267）；清单反序列化大小写不敏感（:299-302）；`LoadPlugin`（:324）建独立 `PluginLoadContext`。
2. **扩展点靠鸭子类型收集**：`ExtensionPointManager.DiscoverExtensionsFromPlugin`（:138）反射插件实例上 `IEnumerable<IExtensionPoint>` 属性 → `IMenuExtension` 注册进菜单、`IToolFunctionExtension`（:174-177）注册进全局工具表。`ForgeSelf.Abstractions/IPlugin.cs`:6-9 契约体**只有** `void Apply(IContext ctx)`。
3. **菜单双源合并**：`ForgeSelf.Api/Controllers/PluginController.cs`:294-309 —— 有 `IMenuExtension` 的插件跳过 manifest 派生项（**IMenuExtension 优先**）；`plugin.json.frontend.{menu,route}` 仅在无 IMenuExtension 时派生。
4. **前端远程加载**：`PluginFrontendFileMiddleware.cs`:30/:33 暴露 `/plugins/{id}/web/dist/**`；`ForgeSelf.Web/src/router/dynamicPlugins.ts`:51-78 按 `frontend.entry` 远程取 `views[0]` 同名导出，无 entry 才回退硬编码 switch（:70-77，仅剩 MemoryView/TodoView）。
5. **构建暂存靠 glob**：`ForgeSelf.Api.csproj`:100-120 `StageAllPlugins`（AfterTargets=Build）把 `..\Plugins\*\bin\$(Configuration)\net10.0\**`、`..\Plugins\*\plugin.json`、`..\Plugins\*\web\dist\**` 拷进 `$(OutDir)Plugins\`；:134-141 `StagePluginsToPublish` 再进发布目录。**新插件无需改这两个目标**，但必须被 `ProjectReference`（:64-88 段）引用才会参与编译。
6. **宿主共享依赖桥**：`ForgeSelf.Web/src/shared/exposeSharedDeps.ts` 把 vue/vue-router/pinia + **有限清单**的 Element Plus 组件挂到 `window.__FORGE_SHARED__`，插件产物 external 后经 import map 复用同一实例。已暴露组件实采清单：`ElMessage ElMessageBox ElNotification ElLoading ElButton ElScrollbar ElTag ElProgress ElEmpty ElSkeleton ElSkeletonItem ElDialog ElTabs ElTabPane ElSwitch ElInputNumber ElInput ElCheckbox ElSelect ElOption` + `@element-plus/icons-vue`。**未暴露 `ElTable` / `ElTooltip` / `ElRadio`**。
7. **鉴权是逐控制器显式的**：策略 `ApiKeyPolicy` 声明于 `ForgeSelf.Api/AppBuilder.cs`:214（scheme `BearerApiKey`，`Security/ApiKeyAuthenticationHandler.cs`:25）。宿主侧仅 `PluginController.cs`:16 带该特性；**24 个插件控制器全部无 `[Authorize]`**（含 `api/filetools/stats/directory` 这种接收任意绝对路径的端点）。
8. **无热重载**：`PluginHotReloadWatcher` 已删（2026-09-24 一刀切）。新插件生效仅两条路：`POST /api/plugin/install`（`.forgeself-plugin` 包 → 触发 `DiscoverPlugins()` 重扫）或冷启动宿主；运行中更新走 `POST /api/plugin/update/{id}` 版本化侧载。
9. **端口**：宿主 Development backend `7102` / frontend `7002`；本环境长期 publish 实例 `51888`（`playwright.live.config.ts` baseURL 实证）。

## 「文件夹大小统计」能力的仓库现状（本任务的核心 Context）

| 位置 | 实际做了什么 | 缺什么 |
| --- | --- | --- |
| `Plugins/FileTools/Services/FileStatsService.cs`:8-90 `GetDirectoryStatsAsync` | 单个根目录的**总量**标量（`Directory.GetFiles(dir,"*",AllDirectories)` :26 + `foreach` 累加 `Length`）+ 扩展名分布 + 最新/最旧时间 | **无按子目录分组、无目录排行、无 walk-up 聚合、无进度/取消**；`DirectoryCount`(:29) 只是个数不是大小 |
| `Plugins/FileTools/Controllers/FileToolsController.cs`:261-322 | `POST api/filetools/stats/{directory,large-files,types}` | 无 `stats/folders`；`IFileStatsService.SortFilesAsync`(:10) 无 HTTP 端点 |
| `Plugins/FileTools/FileToolsPlugin.cs`:769-893 `FileStatsToolFunction` | AI 工具 `filetools.file_stats`，`action ∈ {directory,large_files,types,sort}` | `directory` 分支(:846-861) 同样只回单个总量 |
| `Plugins/ScriptRunner/Services/ScriptTemplateService.cs`:122-138 | **模板 `file-dir-size-ps`「目录大小统计（PowerShell）」**：递归求子目录大小 + `Sort-Object Size -Descending` + `Top` 参数 | 每次靠 spawn PowerShell 进程；无结构化输出、无 UI、无缓存、无进度 |
| `ForgeSelf.Api/Plugins/Services/PluginVersionService.cs`:471-477 | `private long GetDirectorySize(path)`，:459 对每个备份目录逐个求大小 | `private`、只一层、与插件无关，不可复用 |
| `Plugins/SystemMonitor/Services/DiskMonitorService.cs`:188-230 | `DriveInfo.GetDrives()` → **卷级** Total/Free/Used/UsagePercent | **卷粒度不是目录粒度**；无路径参数；答的是「C: 还剩多少」不是「哪个文件夹最肥」 |
| `Plugins/AIAgent/Services/DirectoryBrowseService.cs`:25-85 | 目录浏览器（null→列盘符根、逐级 `GetDirectories`、`UnauthorizedAccessException` 吞为空、`Path.GetFullPath` 规范化） | 属 **AIAgent 插件私有**，跨插件引用需上移 `Abstractions` 并出 ADR |

**⚠ 决定性发现：FileTools 的前端整体是 mock，真实端点零消费者。**
`ForgeSelf.Web/src/services/fileToolsApi.ts`（405 行）**没有 import 任何 HTTP 客户端**（仅 `import type` :1-11），每个函数造假数据（如 `getDirectoryStats` :349-372：`setTimeout(600)` 后返回硬编码 `fileCount:412 / totalSize:727MB` + 6 个假扩展名 + 5 个假大文件）。`git grep "api/filetools"` 全仓命中数 = 1，且就是 `[Route]` 特性本身。调用链：`StatsPanel.vue`:49 → `stores/fileTools.ts`:324 → 上述 mock。
FileTools 的 UI 也在宿主包里（`ForgeSelf.Web/src/views/FileToolsView.vue` 四 tab + `router/index.ts`:149-151 静态路由），`plugin.json` **无 `frontend` 块**、无 `web/` 目录 —— 与 `plugin-development` 铁律3「界面归插件，宿主的 `src/views/` 不再新增插件页面」相反。

## 测试方式

| 需求 | 正规入口 |
| --- | --- |
| 宿主前端类型/lint | `cd ForgeSelf.Web && pnpm run check`（`package.json`:24 `vue-tsc -b && eslint`） |
| 前端 + 插件前端单测 | `cd ForgeSelf.Web && pnpm run test`；**vitest 已把插件 web 纳入**：`vitest.config.ts`:33 include `'../Plugins/*/web/src/**/*.test.{ts,tsx}'`，:66-70 把 `../Plugins` 加进 `server.fs.allow` → 宿主 vitest 可直接跑 `Plugins/X/web/src/*.test.ts` |
| 后端构建 | `cd ForgeSelf.Api && dotnet build` |
| 后端测试 | `cd ForgeSelf.Api.Tests && dotnet test`（插件专项 `--filter "FullyQualifiedName~<X>"`） |
| 插件层 e2e | `ForgeSelf.Web/e2e/plugins/<id>/<id>.spec.ts`，`pnpm run test:e2e`（globalSetup 起全新宿主、零 mock）；实跑 51888 走查用 `pnpm exec playwright test --config=playwright.live.config.ts e2e/plugins/<id>`（需 `E2E_API_TOKEN` 或 `FORGE_SETTING_CONFIG`） |

## 构建命令

```bash
# 宿主后端（含插件暂存到 OutDir/Plugins）
cd ForgeSelf.Api && dotnet build
# 后端测试
dotnet test ForgeSelf.Api.Tests
# 宿主前端门禁
cd ForgeSelf.Web && pnpm run check && pnpm run test
# 插件前端（沙箱内 node_modules 残缺，须走 SKILL.md:253-260 出树构建兜底）
node ForgeSelf.Web/node_modules/vite/bin/vite.js build --config ForgeSelf.Web/.plugin-build-<id>/vite.wrapper.config.ts --outDir <绝对路径>/Plugins/<X>/web/dist --emptyOutDir
# 发布（唯一入口，自动 patch+1）
pwsh .agents/skills/plugin-publish-verify/scripts/run-plugin-publish-verify.ps1 -Plugin <PascalCase>
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| `Plugins/<PascalCase>/` | 插件项目：`plugin.json` + `IPlugin` 入口 + `Controllers/` + `Services/` + `Models/`（无实体时无 `Data/`）+ 可选 `web/` |
| `ForgeSelf.Api/Plugins/` | 宿主侧插件框架（加载、扩展点、版本化布局、前端静态中间件），**不放插件源码** |
| `ForgeSelf.Web/src/` | 宿主 SPA；按铁律3 不再新增插件页面 |
| `ForgeSelf.Web/e2e/plugins/<id>/` | 插件层 e2e（现有 8 个：agent-hub/ai-agent/design-system/home/im-gateway/mcp-center/quick-links/sems） |
| `docs/ai/pilot/<task-id>/` | §11 九阶段工件（已有 `batch-a-menu-route-consistency`、`batch-b-provider-catalog`） |

## 代码组织方式

- 两层命名：目录/程序集 PascalCase，运行时 `Id` kebab-case；`plugin.json` 里 `EntryType = ForgeSelf.Api.Plugins.<PascalCase>.<PascalCase>Plugin`。
- 控制器路由前缀约定：扁平小写单前缀、无版本段（实采 `api/filetools`、`api/monitor`、`api/quicklinks`、`api/mcp-center/servers`…）；宿主自身为单数 `api/plugin`。
- 工具 Id 必须插件前缀（`filetools.file_stats`，`SamplePlugin.cs`:134-135「全局工具注册表按 Id 唯一」）。
- `Apply(ctx)` 五件事（`SamplePlugin.cs`:18-31 权威注释）：取 `PluginMetadata.Id` 不硬编码 → `AddScoped` 注册服务 → 推 `MenuExtensions`/`ToolExtensions` → `ctx.EnsurePluginDataDirectory()` 取私有数据目录（禁写 `AppContext.BaseDirectory`）→ `ctx.Effect(...)` 登记可逆副作用；**`Apply` 内绝不抛异常**（:33-34，抛出即整插件注册失败且只留一句「注册插件服务失败」）。
- 插件私有数据目录：`{数据根}/Plugins/{插件Id}`（生产 `~/.forgeself/Plugins/{id}`）。

## 现有工程规范（对本任务有约束力）

1. `AGENTS.md` §11 → `docs/04-standards/ai-native-engineering-workflow.md`：九阶段 + 三道闸门（§1.1 自含）；**插件任务属「全量」级别，00~06 七件齐备**（规范 §4，:135）。
2. `plugin-development` 铁律：②两层命名 / ③界面归插件 / ④插件前端不可 import 宿主模块、external 必声明 / ⑤插件内跳转走导航桥 `inject('forgeOpenPage')` / ⑬根视图须显示版本徽标 / ⑰管理面控制器须 `[Authorize("ApiKeyPolicy")]` / ⑲菜单路由真源单一（`plugin.json.frontend` 声明一处）/ §四 维护闭环四步（门禁→插件 e2e→发布→浏览器走查）缺一即未完成。
3. `plugin-development` 铁律⑩/⑪/⑫：不自动删除任何数据目录；测试隔离用随机后缀专属目录；有实体必须 `Data/Model.xml → xcode` 且插件自行建表。**本任务 P0 无实体**，故 ⑨/⑫ 暂不触发。
4. `plugin-feasibility-study`：命名在功能定稿之后，须过「名实相符三问」；立项结论交用户拍板后才写实现代码。
5. `AGENTS.md` §0 红线：验证/截图/浏览器驱动禁止手写一次性 `temp/*.cjs`；结论必须沉淀为 §5.0 可重复测试。
6. `§3.4 交互设计统一要求`（plugin-development，2026-09-23 用户要求）：点即保存、操作成败可见、轮询防闪、空态分级、筛选分页边界、破坏性操作二次确认、版本徽标 —— 走查须逐项核对。

## 候选低风险任务

本任务**不属低风险**（新插件 + 新对外契约 + 任意路径文件系统读取）。同批排查出的低风险项（各自独立立项，不并入本任务）：

- `fileToolsApi.ts` mock → 真实 `api/filetools/*` 接线（纯缺陷债，无新契约）
- 移除 `ScriptRunner` 与能力重叠的 `file-dir-size-ps` 模板（重复建设收口）
- `GetDirectoryStatsAsync` 改 `EnumerateFiles` 流式 + 取消（`FileStatsService.cs` 全线 `Task.FromResult` 同步假异步）
- `scaffold-plugin-frontend.ps1`:10-11 路径 bug（`ForgeSelf.Api/Plugins/$Template/web` 不存在）

## 选择该任务的原因

用户直接指令（`AGENTS.md` §1.1 最高优先级）：「做一个统计文件夹大小的插件」。
Context 显示该能力**已在 4 处零散存在但没有一处给出「目录占用排行 + 真接口的界面」**：FileTools 只回单根总量且前端是假的；ScriptRunner 靠 PowerShell 进程模板；`PluginVersionService` 是 private 工具方法；SystemMonitor 只到卷级。因此本任务的真实缺口是「**结构化、可取消、可钻取的目录大小聚合排行 + 自带界面的插件**」，而非「从零发明一个能力」。
