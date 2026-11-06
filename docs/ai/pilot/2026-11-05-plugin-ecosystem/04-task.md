---
task_id: 2026-11-05-plugin-ecosystem
feature: none
risk: { level: L2, triggers: ["ForgeSelf.Api/Plugins/Services/PluginInstallerService.cs", "ForgeSelf.Web/src/views/PluginStore.vue", "scripts/package-plugin.ps1"], raised_by_agent: false }
gate1: { mode: user, at: 2026-11-05T21:40+08:00, ref: "计划 独立插件生态整改 Build 批准" }
gate2: { mode: reviewer, at: null, decision: null }
gate3: { mode: agent, at: null }
expected_files:
  - ForgeSelf.Api/Plugins/PluginVersionLayout.cs
  - ForgeSelf.Api/Plugins/Services/PluginInstallerService.cs
  - ForgeSelf.Api/Plugins/Services/PluginVersionService.cs
  - ForgeSelf.Api/Plugins/Services/PluginPackagerService.cs
  - ForgeSelf.Api/Controllers/PluginController.cs
  - ForgeSelf.Web/src/views/PluginStore.vue
  - scripts/package-plugin.ps1
  - docs/04-standards/packaging-upgrade-backup.md
rollback: "git checkout -- 上述业务文件（本任务不 commit）"
writeback: { feature_doc: pending, reason: "契约写入 packaging-upgrade-backup.md §1.9，不另建功能文档" }
---

# Agent Task

> 阶段：Stage 4｜闸门1 已由用户批准计划。

## Task ID

2026-11-05-plugin-ecosystem

## Objective

用户插件按数据根 `plugins/{id}` 安装 ZIP/DLL，指定版本可激活且失败回滚，宿主露出 `plugin.*` 工具，市场页能安装与激活，HelloWorld 骨架只落在隔离数据根。

## Scope

### Allowed

- 03-plan.md 列出的文件与同目录新增的校验器、提供器、工具、测试。
- 既有安装/更新源测试里把 `.forgeself-plugin` 改为 `.zip`，并改「更新即激活」断言为「只暂存」。
- 设置页/导入页/脚手架页上仍宣传 `.forgeself-plugin` 的一句文案。

### Forbidden

- 修改 `DataLocationService` 的默认分支与 `FORGESELF_DATA_ROOT` 优先级。
- 修改 `ForgeSelf.Api.csproj` 的 `VersionPrefix`、`.env`。
- 拆 `Plugins/*` 进独立仓库，或把 HelloWorld 放进本仓库 `Plugins/`。
- 进程沙箱、新数据库表、新 NuGet 依赖。
- git commit、push、tag、GitHub Release、nuget push。
- 停/启/杀 :51888 或任何已在跑的宿主（含已有 dev-stack）。
- 用 `powershell` 5.1，用一次性 `temp/*.cjs` 当结论。
- 改 `PluginScaffolderService` 的生成字节包行为。

## Acceptance Criteria

- [ ] AC1–AC13（见 02-spec）有测试或命令证据
- [ ] `.forgeself-plugin` 不再被扫描或生成
- [ ] 05/06/07 按真实命令结果填写

## Expected Files

见头部 `expected_files`。

## Verification Commands

```bash
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~PluginEcosystem|FullyQualifiedName~PluginInstaller|FullyQualifiedName~PluginVersionUpdateSource|FullyQualifiedName~PluginPackager|FullyQualifiedName~PluginRoots|FullyQualifiedName~PluginVersionService" --nologo
cd ForgeSelf.Web && pnpm run check && pnpm run test && pnpm run design:check
```
