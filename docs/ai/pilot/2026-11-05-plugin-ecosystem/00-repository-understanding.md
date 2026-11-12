# Repository Understanding

> 阶段：Stage 0｜任务 `2026-11-05-plugin-ecosystem`｜依据真实仓库，不凭常识补栈。

## 项目结构

- 解决方案 `ForgeSelf.slnx`。宿主 `ForgeSelf.Api/`（.NET 10 Web），前端 `ForgeSelf.Web/`（Vue 3 + Vite + pnpm），测试 `ForgeSelf.Api.Tests/`，插件源码 `Plugins/<Name>/`。
- 数据根：`ForgeSelf.Api/DataLocationService.cs`。`FORGESELF_DATA_ROOT` 优先；否则 Production → `~/.forgeself`，Development → `AppContext.BaseDirectory/data`。`scripts/dev-stack.ps1` 把该变量设为 `<repo>/.temp/dev-stack/data`。
- 插件两路扫描：`AppBuilder.cs` 内置根 `pluginsPath` + `AddPluginRoot({数据根}/plugins)`。版本布局 `ForgeSelf.Api/Plugins/PluginVersionLayout.cs`（`versions/<ver>/` + `current`）。
- 安装/版本/打包：`PluginInstallerService`、`PluginVersionService`、`PluginPackagerService`。`AppBuilder.cs` 只对版本服务 `Initialize(pluginsPath)`（内置根）。安装器 `Initialize` 在启动路径上没有调用。
- 工具面：`IToolFunctionExtension` + `ToolRegistry.RegisterTool`。插件工具经 `ExtensionPointManager` 注册；宿主侧目前没有 `plugin.*` 工具。
- 发行串比较：`UpdateChecker.ParseSemVer` / `CompareSemVer`（第 4 段 10 位时间码，`IsDateCoded = Core[3] > 65535`）。插件侧仍是 `VersionComparer`（`System.Version.TryParse`，第 4 段 > 65535 失败后走字符串比较）。
- 包扩展名现状：`*.forgeself-plugin`（`package-plugin.ps1`、`PluginVersionService` 扫描、`PluginController` 下载名）。

## 技术栈

| 层 | 技术 | 依据 |
| --- | --- | --- |
| 后端 | .NET 10、ASP.NET Core、SQLite、NewLife.XCode | `ForgeSelf.Api.csproj` |
| 前端 | Vue 3.5、Vite 6、TypeScript、Element Plus、Pinia、pnpm | `ForgeSelf.Web/package.json` |
| 测试 | xUnit + FluentAssertions；前端 vitest | `ForgeSelf.Api.Tests`、`pnpm run test` |
| 版本比较 | `UpdateChecker` 世代比较 | `ForgeSelf.Api/Services/UpdateChecker.cs` |

## 架构特点

用户插件目录的契约已经在 `IDataLocationService.GetPluginDataDirectory`：`{GetHostDataDirectory()}/plugins/{pluginId}`。内置插件仍从版本目录旁的 `plugins/` 扫描，不改落点。安装器目前把新包解到 `_pluginsDirectory`（未初始化时为空串）。

## 测试方式

- 后端相关过滤：`dotnet test ForgeSelf.Api.Tests --filter`（日志正文）。
- 改了 `ForgeSelf.Api` 与 `scripts/**` 时中档全量 `dotnet test`。本任务改仓库脚本 `scripts/package-plugin.ps1`，按 §5.6 应跑中档；若全量被既有基线红淹没，过滤集必须先绿，全量只对新增红判责。
- 前端：`pnpm run check`、`pnpm run test`、`pnpm run design:check`。
- 临时目录 `TEMP/TMP=<repo>\.temp\tmp`，`NO_PROXY` 含 localhost。

## 构建命令

```bash
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~Plugin"
cd ForgeSelf.Web && pnpm run check && pnpm run test && pnpm run design:check
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| `ForgeSelf.Api/Plugins/Services` | 安装、版本、打包 |
| `ForgeSelf.Api/Controllers/PluginController.cs` | 安装/更新/回滚 HTTP |
| `ForgeSelf.Web/src/views/PluginStore.vue` | 插件市场 |
| `docs/04-standards/packaging-upgrade-backup.md` | 打包/目录/版本唯一真源 |
| `scripts/package-plugin.ps1` | 单插件打包 |

## 代码组织方式

宿主服务构造注入，测试直接 `new`。新增依赖放在构造函数末尾可选参数，避免拆掉既有测试构造。插件工具不做成可卸载插件。

## 现有工程规范

- 唯一开发流程：`docs/04-standards/ai-native-engineering-workflow.md`。闸门1 由用户点 Build 批准计划视为已批。
- 脚本只用 `pwsh`。不改 `.env`，不改 `ForgeSelf.Api.csproj` 的 `VersionPrefix`。不 git commit。不停/启/杀 :51888 与用户正在跑的宿主。
- 不重写 `DataLocationService` 默认分支。不拆仓内 `Plugins/*`。不建进程沙箱。

## 适用问题类

无。本任务是新契约与接线，不是某一 PC 的复发修复。相邻事实：§1.8.1 已记录安装器未 `Initialize`、版本历史用内置根硬拼路径。本任务按已批准计划改这两处，不另开 PC。

## 候选低风险任务

数据根接线本身可单独测（假 `IDataLocationService`）。它被选为第一步，因为后面的安装落点都依赖它。

## 选择该任务的原因

用户已批准计划 `独立插件生态整改`，要求按 p-data-root → p5 做完，不停在第一阶段。
