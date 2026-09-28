# Plan

> 阶段：Stage 3｜
>
> **必须具体到真实文件路径**
>
> ，禁止只写「修改 Service、增加测试」。
> Task ID：PILOT-028 ｜ 2026-09-28

## Files To Change

### 批次 1（已实施，输入 31 用户拍板后落地）



* file: `ForgeSelf.Api/Plugins/Services/PluginVersionService.cs`

  reason: 去 `_backups` 核心 —— 删 BackupPlugin/GetBackupList/RestoreFromBackup/StageVersion/GetHighestBackupVersion；包源 / 侧载直落 versions/；CheckForUpdates source=backup→staged；UpdatePlugin 激活不复制；RollbackPlugin 只认 versions/；统一 GetHighestStagedVersion 基准

* file: `ForgeSelf.Api/Plugins/Services/PluginInstallerService.cs`

  reason: 删安装 / 更新 / 卸载三处 `_versionService.BackupPlugin(...)` 调用（各留注释说明）

* file: `scripts/update-agent.ps1`

  reason: 删整目录备份步骤 → 应用成功后删 `Updates/<tag>/`（保留 agent-\*.log）+ 退役清理 `%LOCALAPPDATA%\ForgeSelf\Backups` → 重启宿主

* file: `scripts/publish-plugin.ps1`

  reason: staged 目标 `_backups/<id>/<ver>/` → `Plugins/<id>/versions/<ver>/`（头注释 / 用法 / 幂等检查同步）

* file: `ForgeSelf.Api/Services/AI/LocalFileImageRecognitionCache.cs`

  reason: 构造器调 `CleanupExpiredSessions()`—— 删 LastWriteTimeUtc 超 30 天（CacheMaxAge）会话目录

* file: `ForgeSelf.Api.Tests/Plugins/PluginVersionServiceTests.cs`

  reason: 改为 versions/ 直落布局；新增 `RollbackPlugin_UnknownVersion_ReturnsFalseWithoutChanges`

* file: `ForgeSelf.Api.Tests/Plugins/PluginVersionUpdateSourceTests.cs`

  reason: versions/ 已直落 staged 更高版本时包不列出

* file: `ForgeSelf.Api/Models/Plugins/PluginDetailDto.cs`

  reason: source 字段注释 backup→staged

* file: `ForgeSelf.Web/src/types/plugin.ts`

  reason: source 字段注释 backup→staged

* file: `docs/04-standards/packaging-upgrade-backup.md`、`docs/02-features/035-*.md`、`docs/02-features/038-*.md`、`docs/04-standards/agent-workflow.md`、`ForgeSelf.Api/Plugins/README.md`、`docs/05-guides/plugin-hot-reload-limitations.md`

  reason: 真源标记批次 1 已实施；去 `_backups` 语义同步（035/038/agent-workflow B5/Plugins README 9 处 /guides 哈希路径）

### 批次 2（待立项，用户拍板后实施）



* file: `scripts/release/release-local.ps1`（打包布局改造：公共层 + versions//）

* file: `scripts/release/package-release.ps1`（zip 内目录布局）

* file: `ForgeSelf.Api/Services/StagedUpdateService.cs` + `scripts/update-agent.ps1`（按 versions/ 布局应用更新）

* file: `ForgeSelf.Api/AppBuilder.cs`（路径基准调整：plugins/ 与 versions/ 并排）——**高风险，须用户拍板 + 端到端升级演练**

## Implementation Steps

**批次 1（已完成）**：



1. PluginVersionService 全量重写（去 `_backups`，直落 versions/ 单轨）；

2. PluginInstallerService 三处删 BackupPlugin 调用；

3. update-agent.ps1 去整目录备份 + 更新缓存清理 + Backups 退役；

4. publish-plugin.ps1 stage 目标改 versions/；

5. LocalFileImageRecognitionCache TTL 清理；

6. 测试同步（versions/ 直落布局 + staged/package 双源 + 新增回滚未知版本用例）；

7. 文档同步（真源 / 035/038/agent-workflow/Plugins README/guides）。

**批次 2（待立项）**：



1. 用户拍板 QQNT 布局与 008 冻结；

2. 设计发布产物布局（公共层清单 + 每次更新内容清单）；

3. 改 release-local.ps1/package-release.ps1 出 versions/ 布局 zip；

4. 改 StagedUpdateService/update-agent.ps1 按新布局应用 + current 指针切换 + 失败回退；

5. 端到端演练：打包 → 页面检查更新 → 下载 → 重启并更新 → 版本回退。

## Test Plan



1. `PluginVersionServiceTests`：versions/ 直落布局全用例绿；新增未知版本回滚返回 false 且无变更。

2. `PluginVersionUpdateSourceTests`：staged 直落更高版本时包源不重复列出。

3. 过滤集回归：PluginVersion|PluginInstaller|PluginFrontend|PluginMenu|ImageRecognitionCache|TempPluginDirectory。

4. 全量 dotnet test：与本任务零文件交集（040-B1 在飞区失败项不计入本任务）。

## Verification

### Build



```
cd ForgeSelf.Api && dotnet build
```

### Unit Test



```
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~PluginVersion|FullyQualifiedName~PluginInstaller|FullyQualifiedName~PluginFrontend|FullyQualifiedName~PluginMenu|FullyQualifiedName~ImageRecognitionCache|FullyQualifiedName~TempPluginDirectory"
dotnet test ForgeSelf.Api.Tests   # 全量（记录失败清单与归因）
```

### Integration Test



```
N/A —— 本任务无新增集成测试（插件版本化链路由现有 PluginVersionServiceTests/PluginVersionUpdateSourceTests 覆盖）
```

### E2E



```
N/A —— 本任务无 UI 行为变化（source 注释 + 后端逻辑；插件管理页走查待批次2 后按 e2e-testing 技能补充）
```

### Other Checks



* `powershell -File scripts/verify-pilot-artifacts.ps1 -TaskId 028-packaging-upgrade-backup`（PILOT 工件链门禁）

* grep 全仓 `_backups|BackupPlugin|整目录备份` 残留分类核对（历史冻结 / 防御性兼容白名单）

## Plan 偏差记录

> 实现中发现 Plan 与仓库实际不符时，先在此记录偏差，再修正 Plan，不得直接绕过。



| 时间         | 偏差点                       | 原 Plan              | 修正后                                                                                                                               |
| ---------- | ------------------------- | ------------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| 2026-09-28 | 工件链时序                     | 按九阶段先产工件、闸门 1 确认后开工 | 输入 31 用户拍板「直接改代码落地」视为闸门 1 授权，批次 1 先行实施；工件链（00-07）于实施后补齐供用户审核（输入 33），批次 2 仍须拍板后实施                                                  |
| 2026-09-28 | PluginVersionService 改动形态 | 局部删备份方法             | `_backups` 贯穿发现 / 执行 / 回滚多路径，改为全量重写（删 5 个方法 + 语义统一），测试同步重写                                                                        |
| 2026-09-28 | 全量测试基线                    | 预期零失败               | 9 失败全落既有四族（WorkflowPlanning 404×6、ScriptRunnerDi 404×1、TerminalCommandGuard 大小写 ×1、ForgeConfig flaky×1），与本次改动零文件交集（040-B1 在飞区），未修 |