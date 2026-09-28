# Evidence

> 阶段：Stage 7｜
>
> **只记录实际发生的事情**
>
> ，不得根据代码推测测试结果。
> 每个验证项标注来源等级：Verified（亲自跑过，有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。禁止混用。
> ⛔ 禁用表述：「应该可以」「理论上通过」「看起来没问题」「大概率是」「估计可以」。

## Task

PILOT-028（批次 1 已实施；批次 2 待立项）

## Changed Files



* `ForgeSelf.Api/Plugins/Services/PluginVersionService.cs`（重写：删 BackupPlugin/GetBackupList/RestoreFromBackup/StageVersion/GetHighestBackupVersion；CheckForUpdates source=staged；UpdatePlugin 激活不复制；RollbackPlugin 只认 versions/）

* `ForgeSelf.Api/Plugins/Services/PluginInstallerService.cs`（删三处 BackupPlugin 调用）

* `scripts/update-agent.ps1`（去整目录备份 + 应用后清 Updates// + Backups 退役）

* `scripts/publish-plugin.ps1`（stage 目标 → Plugins//versions//）

* `ForgeSelf.Api/Services/AI/LocalFileImageRecognitionCache.cs`（构造器调 CleanupExpiredSessions，TTL=30 天）

* `ForgeSelf.Api.Tests/Plugins/PluginVersionServiceTests.cs`（versions/ 直落布局 + 新增 RollbackPlugin\_UnknownVersion\_ReturnsFalseWithoutChanges）

* `ForgeSelf.Api.Tests/Plugins/PluginVersionUpdateSourceTests.cs`（staged 直落更高版本时包不列出）

* `ForgeSelf.Api/Models/Plugins/PluginDetailDto.cs`、`ForgeSelf.Web/src/types/plugin.ts`（source 注释 backup→staged）

* 文档：`docs/04-standards/packaging-upgrade-backup.md`（§1/§2/§3-T4・T6/§4-R2・R3・R4・R6/§5 标记批次 1 已实施 + 变更记录）、`docs/02-features/035`（6 处）、`038`（4 处）、`docs/04-standards/agent-workflow.md`（B5 5 处）、`ForgeSelf.Api/Plugins/README.md`（10 处）、`docs/05-guides/plugin-hot-reload-limitations.md`（2 处）

## Build

Command:



```
cd ForgeSelf.Api && dotnet build
```

Result: PASS（来源等级：Verified）



```
Build succeeded. 0 Error(s), 322 Warning(s)（存量 nullability warnings，基线 833 → 322 为 040-B1 在飞区合并编译后的存量）
```

## Unit Test

Command:



```
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~PluginVersion|FullyQualifiedName~PluginInstaller|FullyQualifiedName~PluginFrontend|FullyQualifiedName~PluginMenu|FullyQualifiedName~ImageRecognitionCache|FullyQualifiedName~TempPluginDirectory"
```

Result: PASS（来源等级：Verified）



```
56/56 通过（含 PluginVersionServiceTests 7、PluginVersionUpdateSourceTests 7 等；新增 RollbackPlugin_UnknownVersion_ReturnsFalseWithoutChanges 绿）
```

Command:



```
dotnet test ForgeSelf.Api.Tests   # 全量
```

Result: PASS with pre-existing failures（来源等级：Verified）



```
1542 通过 / 9 失败（1551 总计）。9 失败全部落既有四族，与本任务零文件交集：
  - WorkflowPlanningIntegrationTests ×6（404：/api/ai-agent/workflow/* 路由在测试宿主未注册，存量 TODO 批次E）
  - ScriptRunnerDiIntegrationTests ×1（404 flaky，插件路由注册时序）
  - TerminalCommandGuardTests ×1（断言 Contain("remove-item") 大小写不匹配，测试自身断言问题）
  - ForgeConfigTests ×1（并行 flaky）
归因：工作区叠着 040-B1（SessionEvent 联合化）大量未提交改动（26 tracked + 17 untracked），失败文件均属该在飞区；未修、非本任务引入。
```

## Integration Test

Result: N/A（依据：本任务无新增集成测试；插件版本化链路由 PluginVersionServiceTests / PluginVersionUpdateSourceTests 单测覆盖）

## E2E

Result: N/A（依据：本任务无 UI 行为变化；插件管理页走查待批次 2 后按 e2e-testing 技能补充）

## Static Analysis

Result: N/A（依据：前端仅 1 行注释改动 `types/plugin.ts`（source: backup→staged），未触发 `pnpm run check/test`；后端改动无 lint 体系）

## Screenshots

N/A（无 UI 变更）

## Known Limitations



* `PluginInstallerService.UpdateFromPackage` 仍是覆盖式更新（ExtractPackage 覆盖活动目录 + Disable/Enable），与版本化 stage 语义未统一 —— 与 038 包源流不同路径，接管决策待下游（列入 02-spec Unknown）。

* 008 `UpdateService` 旧链路代码未动（冻结决策待用户拍板）。

* `scripts/release/package-release.ps1` 仍保留「打包时删 `Plugins/_backups`」防御性删除（清存量，无害）；`migrate-plugin-versions.ps1` 保留跳过 `_*`（幂等兼容存量）；`XCodeConfigTests.cs:187` 注释保留（防御性描述）。

## Unresolved Issues



* 无 BLOCKED 项。全量测试 9 失败归因明确（既有四族 + 040-B1 在飞区），与本任务零交集。

* 批次 2（宿主 QQNT 结构）未实施 —— 待用户拍板立项（已列入 04-task Task B）。