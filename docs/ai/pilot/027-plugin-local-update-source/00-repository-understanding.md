# Repository Understanding — 插件本地目录更新源

> 阶段：Stage 0｜来源：2026-09-28 真实仓库探查（Read/Grep，20+ 文件交叉核对；2026-09-28 经审查子代理二次核对）

## 项目结构

- `ForgeSelf.Api/` — ASP.NET Core API（.NET 10 + SQLite + NewLife.XCode），插件架构
- `ForgeSelf.Api/Plugins/<PascalCase>/` — 每个插件（plugin.json + csproj + Controllers/Services/Data + web/）
- `ForgeSelf.Api/Plugins/Services/` — 插件运行时服务：PluginManager / PluginVersionService / PluginInstallerService / PluginPackagerService
- `ForgeSelf.Api/Controllers/PluginController.cs` — 插件管理面 API（类级 `[Authorize("ApiKeyPolicy")]`）
- `ForgeSelf.Api/AppBuilder.cs` — 服务注册（宿主更新配置 UpdateSettingsService 在此注册，落盘 `{数据根}/Config/update-settings.json`）
- `ForgeSelf.Api/Plugins/ServiceCollectionExtensions.cs` — `AddPluginManager` 注册 PluginVersionService 等（DI 自动解析 ctor）
- `ForgeSelf.Web/src/services/pluginApi.ts` + `src/stores/plugin.ts` + `src/views/PluginStore.vue|PluginDetail.vue|PluginUpdates.vue` — 前端插件管理
- `ForgeSelf.Web/src/components/settings/UpdatePanel.vue` — 宿主「更新源配置」UI（输入22 重构为四选项）
- `scripts/publish-plugin.ps1` — 单插件构建 + stage 到 `_backups/<id>/<ver>/`（开发侧载链路）

## 技术栈

| 层 | 技术 | 依据（文件/配置） |
| --- | --- | --- |
| 后端 | ASP.NET Core（.NET 10）、SQLite、NewLife.XCode | ForgeSelf.Api.csproj / Program.cs |
| 前端 | Vue 3.5 + Vite 6 + TS 5.7 + Element Plus 2.14 + Tailwind 4 + Pinia + pnpm | ForgeSelf.Web/package.json |
| 测试 | xUnit + Moq + FluentAssertions（后端）；vitest（前端单测）；Playwright e2e | ForgeSelf.Api.Tests / vitest.config.ts / playwright.config.ts |
| 脚本 | PowerShell（PS 5.1，`pwsh` 残桩不可用） | scripts/*.ps1 |

## 插件更新现状（关键事实，全部 Read 验证）

| 能力 | 实现 | 位置 |
| --- | --- | --- |
| 版本化更新（不重启宿主） | `_backups/<id>/<ver>/` stage → `POST /api/plugin/update/{id}` → versions/<ver>/ + current 指针 + ALC 换载 | `PluginVersionService.UpdatePlugin`（138-224 行） |
| 检查更新 | 扫 `_backups/<id>/` 目录版本 vs 当前生效版本 | `CheckForUpdates`（35-75 行） |
| 回滚/版本历史 | `POST /api/plugin/rollback/{id}` / `GET /api/plugin/{id}/versions` | 226-284 / 77-137 行 |
| 插件包（.forgeself-plugin = zip） | `PluginPackagerService.PackagePlugin/ValidatePackage/ExtractPackage/ReadPackageMetadata` | PluginPackagerService.cs |
| 从包安装/更新 | `POST /api/plugin/install`（上传包）→ `InstallFromPackage`（新装）/ `UpdateFromPackage`（覆盖式更新） | PluginController 679-739 行；PluginInstallerService 30-126 行 |
| 打包下载 | `POST /api/plugin/package/{id}` → `<Name>-<Version>.forgeself-plugin` | PluginController 654-677 行 |
| 前端更新/回滚 UI | 插件市场页已有「检查更新/更新/回滚 + 版本历史」 | PluginStore.vue 193-226、PluginUpdates.vue |

## 包格式与布局（审查后修正）

- `.forgeself-plugin` = zip。**两种产出布局**（设计以脚本产物为真源）：
  1. `PluginPackagerService.PackagePlugin`（API 打包端点）递归打包整个插件目录 → 含 `versions/**` + `current` 嵌套（**不是**干净布局）；
  2. **`scripts/package-plugin.ps1`（本设计新增）产出干净布局：根 = `plugin.json` + 入口 DLL + 依赖 + `web/dist/`**——与 `_backups/<id>/<ver>/` 布局一致，`PluginPackagerService.ExtractPackage` 解包后可直接作为版本目录源。
- `ValidatePackage` 只校验 plugin.json 存在 + Id/Name/Version 非空，**不校验 EntryAssembly 文件存在**（P1-6 硬约束：本设计从包 stage 前须补入口 DLL 校验）。

## 缺口

- 插件**没有「更新源」配置**：新版本只能靠本机 `publish-plugin.ps1` stage 进 `_backups`，或随宿主 zip 整体打包；没有像宿主 `UpdateSettingsService` 那样「设置一个目录 → 页面检查更新自动发现新包」的能力。

## 可复用模式

- 宿主更新源配置：`UpdateSettingsService`（`ctor(initial, path)` + `Current` + `Update(Action)` + `LoadPersisted` + `Save` + `_gate` 锁；`{数据根}/Config/update-settings.json`，运行时可变、落盘、重启优先）→ 可完全同构复制为插件版本（AppBuilder:167-169 注册点可复用）。
- 版本化更新链路 `UpdatePlugin`：从 `_backups/<id>/<ver>/` 读取（StageVersion → 切 current → ALC 换载）→ 新来源只需「把包解到 `_backups/<id>/<ver>/`」即可复用全部逻辑（含回滚/保留 N 版本）。
- `StageVersion` 递归复制不关心源目录内部结构；`ResolveEntryAssemblyPath` 按 `versions/<current>/<EntryAssembly>` 命中。

## 测试方式

- 后端单测：`ForgeSelf.Api.Tests/Services/`（UpdateSettingsServiceTests / UpdateCheckerTests 为宿主更新配置既有测试范式）；插件服务测试在 `ForgeSelf.Api.Tests/Plugins/`（PluginVersionServiceTests 等——**本设计新增的 PluginVersionService 相关测试应放 Tests/Plugins/**，P2-7）
- 前端：`ForgeSelf.Web` 下 `pnpm run check`（vue-tsc + eslint）＋ `pnpm run test`（vitest）。⚠ **vitest 排除 `**/*.spec.ts`**（vitest.config.ts 注释「*.spec.ts 留给 Playwright e2e」）→ 组件测试必须命名 `.test.ts`（P1-8）
- 构建：后端 `dotnet build`（ForgeSelf.Api）；前端 `pnpm run build`
- 脚本守卫：`RepositoryScriptTests.cs` 自动检查含非 ASCII 的 `.ps1` 必须带 UTF-8 BOM（P2-11）

## 构建命令

```bash
cd ForgeSelf.Api && dotnet build
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~PluginUpdateSettingsServiceTests|FullyQualifiedName~PluginVersionUpdateSourceTests"
cd ForgeSelf.Web && pnpm run check && pnpm run test
# 注意：PowerShell 5.1 不支持 && 解析，实际执行时拆分
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| `ForgeSelf.Api/Models/Plugins/` | 插件面 DTO/模型集中地（PluginDetailDto.cs 等；UpdateConfig 在 Models/ 根属宿主面先例，P2-9） |
| `ForgeSelf.Api/Services/` | 宿主面服务（UpdateSettingsService 等） |
| `ForgeSelf.Api/Plugins/Services/` | 插件运行时服务 |
| `ForgeSelf.Api.Tests/Services/` | 宿主面服务测试 |
| `ForgeSelf.Api.Tests/Plugins/` | 插件服务测试（PluginVersionServiceTests 等） |
| `scripts/` | 构建/发布/运维脚本 |
| `docs/02-features/<NNN>-<功能>.md` | 功能文档（当前最高 035；**036 已被 specs/036-github-release-auto-update 语义占用**，新功能避开 → 用 038，P1-7） |
| `docs/ai/pilot/<task-id>/` | §11 闭环工件 |

## 代码组织方式

- 后端：Controllers/Services/Entities 分层；插件经 `Plugins/<X>/` + plugin.json 注册；插件服务经 `AddPluginManager` 注册（DI 自动解析 ctor）。
- 前端：services（authFetch 带 token）/ stores / views / components 分层；插件面 UI 在 `PluginsPanel.vue`（设置-插件管理 tab，本设计 UI 落点，P2-8）。

## 现有工程规范

- AGENTS.md §0 预飞铁律 / §11 AI-Native 闭环（闸门1 用户确认后开工，本工件已提交确认）
- 插件铁律 10/11/17：禁止删除数据目录；测试隔离（独立随机目录、直查 DB）；管理面鉴权类级 `[Authorize("ApiKeyPolicy")]`
- 发布规范（2026-09-27）：禁止 agent 停/启/杀宿主；宿主自更新；插件发布走版本化侧载
- 打包流程：宿主唯一入口 `release-local.ps1`；本任务新增的是**插件级**打包脚本（打出 .forgeself-plugin 到插件更新源目录），并列不冲突
- 前端：禁止显式 `import { ElXxx }`；新代码 TS 类型不用 any
- 脚本：含中文 `.ps1` 必须 UTF-8 BOM（B6 铁律，RepositoryScriptTests 自动守卫）

## 候选低风险任务

本任务（插件本地目录更新源）即候选：不涉及 DB 迁移、不涉及宿主核心架构、不动生产数据；新增配置服务同构既有模式、复用版本化链路；改/新文件 11 个（含测试与文档），超 mini-task 线走全量链。

## 选择该任务的原因

用户已确认推荐方案（输入27 闸门1 前置说明）：宿主已支持本地 zip 更新源，插件需获得同构的「本地插件包目录更新源」能力，使插件更新独立于宿主发版、不重启宿主，并复用既有版本化/回滚体系。