# Specification

> 阶段：Stage 2｜必须从真实 Repository Understanding 与 Intent 推导。
> 规则：① 所有内容与实际项目一致；② 不得发明不存在的接口、类、模块；③ 不确定点显式记录为 `Unknown`，不得自行假定。
> Task ID：PILOT-028 ｜ 2026-09-28

## Functional Requirements

**F1（真源统一，输入30 已完成）**：新建 `docs/04-standards/packaging-upgrade-backup.md` 为打包/升级备份/缓存/安装目录结构唯一真源（§0 真源声明/§1 现状/§2 浪费点6项/§3 QQNT 目标 T1-T6/§4 生命周期规则 R1-R8/§5 实施清单/§6 引用关系）；AGENTS.md §2.3 引用该真源，重复规则从 AGENTS.md / agent-workflow.md / 035/038 / 两个插件技能收敛。

**F2（插件去 `_backups`，批次1 已实施）**：
- F2.1 `PluginVersionService`：删除 `_backupsDirectory` 字段、`BackupPlugin()`、`GetBackupList()`、`RestoreFromBackup()`、`StageVersion()`（从 `_backups` 复制那条路径）、`GetHighestBackupVersion()`；
- F2.2 新版本 stage 目标 = `versions/<ver>/`（包源 `EnsureStagedFromPackageSource` 直落、侧载脚本直落）；
- F2.3 `CheckForUpdates` 版本基准统一 = `versions/` 内已直落未生效最高版本（source=`staged`）与包目录最高版本（source=`package`）取更高者；
- F2.4 `UpdatePlugin` = 激活已直落版本（停旧 → ForceCollect → 切 current → 同步活动清单 → RefreshMetadataFromDisk+Enable → PruneVersions），**不复制文件**；
- F2.5 `RollbackPlugin` 只认 `versions/` 内存在的版本（保留当前+上一版，无备份可恢复）；
- F2.6 `PruneVersions`（保留当前+上一版，更旧 TryDeleteDirectory 延迟删除）等既有能力保留；
- F2.7 `PluginInstallerService` 安装/更新/卸载三处 `BackupPlugin` 调用删除；
- F2.8 `scripts/publish-plugin.ps1` staged 目标 `_backups/<id>/<ver>/` → `Plugins/<id>/versions/<ver>/`；
- F2.9 `PluginDetailDto.cs` / `ForgeSelf.Web/src/types/plugin.ts` 的 `source` 字段注释 `backup` → `staged`。

**F3（宿主升级去整目录备份，批次1 已实施）**：
- F3.1 `scripts/update-agent.ps1` 删除「整目录备份到 `%LOCALAPPDATA%\ForgeSelf\Backups\<ts>`」步骤；
- F3.2 应用成功后删除 `Updates/<tag>/` 暂存目录（保留 agent-*.log）；
- F3.3 退役并清理存量 `%LOCALAPPDATA%\ForgeSelf\Backups`。

**F4（图片缓存 TTL，批次1 已实施）**：`LocalFileImageRecognitionCache` 构造器调用 `CleanupExpiredSessions()`，删除 `LastWriteTimeUtc` 超 `CacheMaxAge`（30 天）的会话目录。

**F5（批次2 待立项，用户拍板后实施）**：宿主 QQNT 式目录结构——程序根目录公共外置（启动器/公共 DLL），宿主每次更新的内容入 `versions/<ver>/`，`plugins/` 与 `versions/` 并排；发布脚本（release-local.ps1/package-release.ps1）与更新链路（StagedUpdateService/update-agent.ps1）按新布局改造；端到端升级演练。

**F6（008 收口，待用户拍板）**：`UpdateService`（008 Windows 服务旧链路）与 036 双链路并存；建议冻结 008，只保留页面自动更新（036）。

## Input

- 用户输入30/31（优化意图、QQNT 式方向、改代码落地的拍板）。
- 真实仓库代码：PluginVersionService.cs、PluginInstallerService.cs、PluginVersionLayout.cs、PluginController.cs、AppBuilder.cs、update-agent.ps1、publish-plugin.ps1、package-release.ps1、LocalFileImageRecognitionCache.cs、PluginVersionServiceTests.cs、PluginVersionUpdateSourceTests.cs。

## Output

- 唯一真源文档（已存在）+ AGENTS.md 引用（已存在）。
- 批次1 代码/脚本/测试/文档改动（已实施，见 05-evidence）。
- 批次2：待立项，产出方案后实施。

## Business Rules

- **B1**：插件多版本共存（versions/ + current 指针）即回滚能力，任何场景不再产生插件备份目录。
- **B2**：更新版本比较基准唯一 = `GetHighestStagedVersion`（versions/ 内最高）+ 包目录最高取更高者；包版本必须 > 当前生效版本 且 > versions/ 现有最高 staged 版本，Id 匹配、版本号合法（`^\d+(\.\d+){0,3}$`）、包内存在入口 DLL，才列为可更新。
- **B3**：UpdatePlugin 在「已是最高版本早退」检查**之前**调用 `EnsureStagedFromPackageSource`（纯包源场景也能更新；P1-1 插入点语义保留）。
- **B4**：回滚 = 切 current 到 versions/ 内保留版本；`PruneVersions` 保留当前+上一版，更旧延迟删除（DLL 占用跳过下轮）。
- **B5**：宿主升级不再做任何整目录备份；更新暂存目录应用后即清；Backups 存量退役。
- **B6**：图片缓存会话目录超 30 天删除；失败占位结果不写缓存行为保留。

## Boundary Conditions

- 插件 `UpdateFromPackage`（PluginInstallerService 包更新）仍是覆盖式更新（ExtractPackage 覆盖活动目录 + Disable/Enable），与版本化 stage 语义未统一——已验证与 038 包源流不同路径，是否接管待下游定（Unknown）。
- 不修改宿主路径基准（`AppContext.BaseDirectory/Plugins`、数据根）——那是批次2 的范围。
- 不触碰 008 UpdateService 代码（冻结决策未定）。
- 不修改活动插件目录内容规则（只放插件自身 DLL）。

## Error Handling

- 版本切换失败：既有失败回退（ReloadPlugin 异常回退 current 到上一可用版本）。
- DLL 占用：TryDeleteDirectory 占用时跳过、下轮重试；ForceCollect 两轮 GC。
- 包源 stage 失败：全程内部捕获异常，仅记日志不传播 500（038 既有语义保留）。
- 缓存清理失败：不抛出、不阻断初始化（TryDeleteDirectory 语义）。

## Compatibility

- 对外 API 面零变化：`PluginController` 未暴露 backups/restore 端点（GetBackupList/RestoreFromBackup 无外部面），删除无影响。
- 兼容存量扁平插件目录：versions/ 不存在时 fallback 根扁平布局（PluginFrontendFileMiddleware 回退逻辑保留）。
- 发布主路径（打 tag 自动发布 + 页面自动更新）不变。

## Non-functional Requirements

- 空间：消除整目录备份、`_backups` 副本、更新缓存堆积、无 TTL 图片缓存。
- 性能：升级耗时不再包含 39s 级整目录备份。
- 可维护性：结构事实唯一真源，其余文档引用。

## Acceptance Criteria

- AC1：`ForgeSelf.Api dotnet build` 0 error。
- AC2：插件相关测试过滤集全绿；新增 `RollbackPlugin_UnknownVersion_ReturnsFalseWithoutChanges`。
- AC3：grep 全仓 `_backups|BackupPlugin|整目录备份` 残留仅限历史冻结工件（027 pilot）/防御性兼容（package-release 清旧目录、migrate 跳过 `_*`、XCodeConfigTests 注释）。
- AC4：update-agent.ps1 无整目录备份步骤；含 `Updates/<tag>/` 清理与 Backups 退役逻辑。
- AC5：LocalFileImageRecognitionCache 含 TTL 清理调用。
- AC6：文档同步：真源 §5 标记批次1 已实施；035/038/agent-workflow B5/Plugins README/plugin-hot-reload-limitations 去 `_backups` 语义同步。
- AC7：PILOT 工件链 00-07 八件齐备且通过 `scripts/verify-pilot-artifacts.ps1`。

## Unknown

| 不确定点 | 影响 | 处理方式（询问/搁置/保守假设并标注） |
| --- | --- | --- |
| 批次2 宿主 QQNT 式结构是否立项 | 更新链路形态变更、发布脚本布局改造 | **询问用户**（已多次提请拍板；实施前须端到端演练） |
| 008 UpdateService 是否冻结 | 双链路并存 vs 单链路 | **询问用户**（建议冻结 008；代码未动） |
| PluginInstallerService.UpdateFromPackage 覆盖式语义是否纳入版本化 stage | 插件安装链路部分路径仍覆盖式 | 搁置（与 038 包源流不同路径；待下游决定） |
| migrate-plugin-versions.ps1 ConvertFrom-Json 报错 | 存量迁移脚本健康度 | 搁置（TODO P3，本次未触碰；其跳过 `_*` 逻辑保持防御兼容） |
