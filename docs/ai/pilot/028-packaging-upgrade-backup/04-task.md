# Agent Task

> 阶段：Stage 4｜把任务变成 
>
> **Agent 可以直接执行的工作单元**
>
> ，零自我决策空间。
> 前序工件：00-repository-understanding / 01-intent / 02-spec / 03-plan 齐备且经闸门 1 确认。

## Task ID

PILOT-028（Task A：批次 1 已完成；Task B：批次 2 待用户拍板立项）

## Task A — 批次 1：去插件备份 / 去宿主整目录备份 / 缓存 TTL

### Objective

做完后仓库达到的可验证状态：插件更新链路零 `_backups`（新版本直落 `versions/<ver>/`，更新 = 激活不复制，回滚只认 versions/）；update-agent 升级不再做整目录备份且应用后清理 `Updates/<tag>/`、退役 Backups 存量；ImageRecognitionCache 初始化执行 30 天 TTL 清理；`ForgeSelf.Api dotnet build` 0 error、插件相关过滤集测试全绿。

### Scope

#### Allowed



* 修改 `ForgeSelf.Api/Plugins/Services/PluginVersionService.cs`（去 `_backups` 重写，见 03-plan.md Files To Change）

* 修改 `ForgeSelf.Api/Plugins/Services/PluginInstallerService.cs`（删三处 BackupPlugin 调用）

* 修改 `scripts/update-agent.ps1`（去整目录备份 + 更新缓存清理 + Backups 退役）

* 修改 `scripts/publish-plugin.ps1`（stage 目标 `_backups/` → `versions/`）

* 修改 `ForgeSelf.Api/Services/AI/LocalFileImageRecognitionCache.cs`（TTL 清理）

* 修改 `ForgeSelf.Api.Tests/Plugins/PluginVersionServiceTests.cs`、`PluginVersionUpdateSourceTests.cs`（布局改造 + 新增用例）

* 修改 `ForgeSelf.Api/Models/Plugins/PluginDetailDto.cs`、`ForgeSelf.Web/src/types/plugin.ts`（source 注释同步）

* 同步文档：`docs/04-standards/packaging-upgrade-backup.md`、`docs/02-features/035`、`038`、`docs/04-standards/agent-workflow.md`、`ForgeSelf.Api/Plugins/README.md`、`docs/05-guides/plugin-hot-reload-limitations.md`

#### Forbidden



* ❌ 不提交 git（用户明确「只修改，不提交」；提交须用户明确指示）

* ❌ 不停 / 启 / 杀任何用户宿主进程（含 `D:\src\tools\ForgeSelf`、`:51888` 实例）

* ❌ 不动 `AppContext.BaseDirectory` 路径基准 / 数据根（那是批次 2 范围）

* ❌ 不碰 008 `UpdateService.cs`（冻结决策未定）

* ❌ 不改活动插件目录内容规则（只放插件自身 DLL）

* ❌ 不顺手修 040-B1 在飞区文件（工作区叠加的 dsh 改动与本任务隔离）

* ❌ 不引入新依赖、不改 DB 结构、不改 API 契约面（PluginController 无备份端点暴露）

### Acceptance Criteria



* [x] `ForgeSelf.Api dotnet build` 0 error

* [x] 插件相关过滤集测试全绿（56/56，含新增 `RollbackPlugin_UnknownVersion_ReturnsFalseWithoutChanges`）

* [x] update-agent.ps1 无整目录备份步骤；含 `Updates/<tag>/` 清理与 Backups 退役逻辑

* [x] LocalFileImageRecognitionCache 构造器调用 TTL 清理

* [x] publish-plugin.ps1 stage 目标 = `Plugins/<id>/versions/<ver>/`

* [x] 文档同步完成（真源 §5 标记批次 1 已实施；035/038/agent-workflow/Plugins README/guides 去 `_backups`）

* [x] grep 残留仅限历史冻结工件（027 pilot）/ 防御性兼容（package-release/migrate/XCodeConfigTests）

### Expected Files



* ForgeSelf.Api/Plugins/Services/PluginVersionService.cs（重写）

* ForgeSelf.Api/Plugins/Services/PluginInstallerService.cs

* scripts/update-agent.ps1

* scripts/publish-plugin.ps1

* ForgeSelf.Api/Services/AI/LocalFileImageRecognitionCache.cs

* ForgeSelf.Api.Tests/Plugins/PluginVersionServiceTests.cs / PluginVersionUpdateSourceTests.cs

* ForgeSelf.Api/Models/Plugins/PluginDetailDto.cs / ForgeSelf.Web/src/types/plugin.ts

* 文档 6 处（真源 / 035/038/agent-workflow/Plugins README/guides）

### Verification Commands



```
cd ForgeSelf.Api && dotnet build
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~PluginVersion|FullyQualifiedName~PluginInstaller|FullyQualifiedName~PluginFrontend|FullyQualifiedName~PluginMenu|FullyQualifiedName~ImageRecognitionCache|FullyQualifiedName~TempPluginDirectory"
dotnet test ForgeSelf.Api.Tests   # 全量（归因失败清单）
powershell -File scripts/verify-pilot-artifacts.ps1 -TaskId 028-packaging-upgrade-backup
```

## Task B — 批次 2：宿主 QQNT 式目录结构（**待用户拍板立项**）

### Objective

程序根目录公共外置（启动器 / 公共层），宿主每次更新的内容入 `versions/<ver>/`，`plugins/` 与 `versions/` 并排；发布脚本布局改造；端到端升级演练（打包 → 检查更新 → 下载 → 重启并更新 → 回退）。

### Scope

#### Allowed



* 用户拍板后：改 `scripts/release/release-local.ps1`、`scripts/release/package-release.ps1`（zip 内 versions/ 布局）

* 改 `ForgeSelf.Api/Services/StagedUpdateService.cs` + `scripts/update-agent.ps1`（按新布局应用 + current 指针 + 失败回退）

* 改 `ForgeSelf.Api/AppBuilder.cs`（路径基准：plugins/ 与 versions/ 并排）

#### Forbidden



* ❌ 未获用户拍板前不实施任何批次 2 改动

* ❌ 不停 / 启 / 杀宿主进程；不破坏打 tag 自动发布 / 页面自动更新主路径

### Acceptance Criteria



* [ ] 用户拍板立项（含 008 冻结决策）

* [ ] 发布产物为 QQNT 布局（公共层 + versions// + plugins/）

* [ ] 端到端升级演练通过（含版本回退）

### Expected Files

（立项后按 03-plan.md 批次 2 Files To Change 展开）

### Verification Commands



```
# 立项后补充：release-local.ps1 打包 + e2e 隔离实例升级演练（按 e2e-testing 技能）
```