# Agent Task（批次C · 扩 FileTools + 快照持久化版）

> 阶段：Stage 4｜v2 重写（基线 = 02-spec v2 + 03-plan v2，闸门1 已批）。零自我决策空间。

## Task ID

`PILOT-batch-c-folder-size-plugin`（子单元 **T1 后端 / T2 前端 / T3 证据链**，按序执行，每单元结束立即跑其验证命令，失败不进下一个）

## Objective

`Plugins/FileTools` 获得「按目录聚合的大小排行」能力：流式遍历 + walk-up 归并 + 可取消带进度的后台任务 + 8 个 `api/filetools/folders/*` 端点（类级 `ApiKeyPolicy`）+ 2 张 XCode 表（`ScanSnapshot`/`ScanFolderEntry`）支撑快照与趋势对比 + `filetools.folder_stats` AI 工具 + 宿主 `FileToolsView` 第 5 个 tab「目录排行」真实接线（不走 `fileToolsApi.ts` 的 mock），并有 xUnit / vitest / Playwright 三层可重复证据。

## Scope

### Allowed（= 03-plan「Files To Change」全集）

- **新建** `Plugins/FileTools/`：`Data/Model.xml`、`Data/FileToolsTables.cs`、`Data/Entities/{ScanSnapshot,ScanFolderEntry}.cs`（**仅由 xcode 生成**）、`Data/Entities/{ScanSnapshot,ScanFolderEntry}.Biz.cs`、`Data/FileTools.htm`（生成附产物）、`Models/FolderScanModels.cs`、`Services/{IFolderScanService,FolderScanService,IFolderScanJobStore,FolderScanJobStore}.cs`
- **修改** `Plugins/FileTools/`：`FileTools.csproj`（+NewLife.XCode）、`FileToolsController.cs`（+8 action、+类级 `[Authorize]`、`GetOverview` 补 `folders`）、`FileToolsPlugin.cs`（Apply 注册 + `EnsureTablesCreated` + `ctx.Effect` 取消 + `FolderStatsToolFunction`）
- **宿主 2 行**：`ForgeSelf.Api/Data/XCodeConfig.cs`（`PluginDbs` +`["FileTools"]="file-tools"`）、`ForgeSelf.Api.Tests/XCodeTestFixture.cs`（`_connNames` +`"FileTools"`）
- **新建前端**：`ForgeSelf.Web/src/services/fileToolsFoldersApi.ts`、`src/stores/fileToolFolders.ts`、`src/components/filetools/{foldersModel.ts,FoldersPanel.vue,foldersModel.test.ts}`
- **修改前端**：`src/types/fileTools.ts`（`FileToolTab` +`"folders"` + 新镜像类型）、`src/views/FileToolsView.vue`（第 5 tab + 渲染分支）；（条件）`src/data/features.ts` 仅在 `check-features` 要求时 +1 行（偏差 D-5）
- **新建测试**：`ForgeSelf.Api.Tests/Unit/FolderScanServiceTests.cs`、`ForgeSelf.Api.Tests/Unit/FileToolsFoldersAuthAndToolTests.cs`、`ForgeSelf.Api.Tests/Integration/FolderSnapshotPersistenceTests.cs`、`ForgeSelf.Web/e2e/plugins/file-tools/file-tools.spec.ts`
- **文档/沉淀**：`docs/02-features/<NNN>-文件工具-目录排行.md`、`docs/07-decisions/not-taken-decisions.md`（追加）、本目录 `05-evidence.md`/`06-review.md`、`TODO.md`、`.forgeself/memory/2026-09-27.md`、`.agents/skills/plugin-development/SKILL.md`（**仅**回写铁律12 建表形状与宿主 UI 型插件发布路径两段，偏差 D-1）

### Forbidden

- 不改 `Plugins/FileTools/Services/FileStatsService.cs`、`FileSizeFormatter.cs`、`Models/FileStatsModels.cs`、`ICleanupService/CleanupService/IArchiveService/ArchiveService/IRenameService/RenameService` 的任何既有行为（AC-4 的 grep 归属靠这条守住）
- 不改 `FileToolsPlugin.cs`:38-94 的 `RegisterMenuExtensions`（不加/不动菜单，AC-9 依据）；不改 `plugin.json`
- **不进** `ForgeSelf.Web/src/services/fileToolsApi.ts`（mock 债文件）、不改其 mock 函数、不改 `stores/fileTools.ts` 的 `loadStats` 等动作逻辑；不在其中留任何「顺手改成真接口」的痕迹
- 不改 `Plugins/AIAgent/**`（含 `AIAgentService.ResolveOwnToolDefinitions` 白名单、`AiAgentView.vue` picker）、不改 `ForgeSelf.Api/Services/ToolRegistry.cs`、不改宿主鉴权注册（`AppBuilder.cs`:212-214）与 `ApiKeyAuthenticationHandler`
- 不改 `ForgeSelf.Api.csproj` 的 `StageAllPlugins`/`StagePluginsToPublish`；不改 `ForgeSelf.slnx`（FileTools 已在解决方案内）
- 不改 `menu-route-consistency.spec.ts`（本批零 route 变更；若实跑红先分析根因，禁止改判据凑绿）
- 不手写/手改 xcode 生成的 `Data/Entities/*.cs`；不在 `.cs` 里写业务方法（一律 `.Biz.cs`）
- **不删任何目录/文件**（铁律10）：不 `File.Delete`/`Directory.Delete` 打数据目录或测试临时树；快照 `delete` 端点只删 DB 行
- 不新增 NuGet/npm 依赖（仅 `NewLife.XCode`，版本钉 `12.0.2026.701`）；不引 `ElTable` 之外的第二套 UI 体系
- 不用一次性 `temp/*.cjs` 作验证（`AGENTS.md` §0 红线）；手工不拷发布产物
- 不 `git commit`/merge/push（闸门2 前不提交，规范 §1.1:17）；不在未通报情况下停/起共享 51888 实例覆盖他人 in-flight 工作（02-spec U-2d）
- 不「顺手」修：FileTools 悬空子菜单（D-7）、既有假异步（`TODO.md` 已登记）、ScriptRunner `file-dir-size-ps` 模板、其他 23 个插件控制器的鉴权

## Acceptance Criteria

见 02-spec **AC-1 ~ AC-12**（逐条不改判据）。子单元归属：T1=AC-1~5 + 后端全量零新增红；T2=AC-6；T3=AC-7~12。

## Expected Files

```
# 后端
Plugins/FileTools/FileTools.csproj                                     (改)
Plugins/FileTools/Data/Model.xml  Data/FileToolsTables.cs  Data/FileTools.htm   (新)
Plugins/FileTools/Data/Entities/{ScanSnapshot,ScanFolderEntry}.cs(.Biz.cs)      (新生成+新手写)
Plugins/FileTools/Models/FolderScanModels.cs                                         (新)
Plugins/FileTools/Services/{IFolderScanService,FolderScanService,IFolderScanJobStore,FolderScanJobStore}.cs (新)
Plugins/FileTools/Controllers/FileToolsController.cs                   (改)
Plugins/FileTools/FileToolsPlugin.cs                                   (改)
ForgeSelf.Api/Data/XCodeConfig.cs                                      (改 +1 行)
ForgeSelf.Api.Tests/XCodeTestFixture.cs                                (改 +1 行)
ForgeSelf.Api.Tests/Unit/{FolderScanServiceTests,FileToolsFoldersAuthAndToolTests}.cs   (新)
ForgeSelf.Api.Tests/Integration/FolderSnapshotPersistenceTests.cs      (新)
# 前端
ForgeSelf.Web/src/types/fileTools.ts  src/views/FileToolsView.vue      (改)
ForgeSelf.Web/src/services/fileToolsFoldersApi.ts                      (新)
ForgeSelf.Web/src/stores/fileToolFolders.ts                            (新)
ForgeSelf.Web/src/components/filetools/{foldersModel.ts,FoldersPanel.vue,foldersModel.test.ts} (新)
ForgeSelf.Web/e2e/plugins/file-tools/file-tools.spec.ts                (新)
# 文档
docs/02-features/<NNN>-文件工具-目录排行.md  docs/07-decisions/not-taken-decisions.md
docs/ai/pilot/batch-c-folder-size-plugin/{05-evidence.md,06-review.md}
.agents/skills/plugin-development/SKILL.md（两段回写）  TODO.md  .forgeself/memory/2026-09-27.md
```

## Verification Commands

```bash
# T1
cd ForgeSelf.Api && dotnet build
cd ../Plugins/FileTools/Data && xcode Model.xml            # 连跑两次比 md5（幂等）
cd ../../.. && dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~FileTools"
dotnet test ForgeSelf.Api.Tests                            # 全量：与批次A 9 项存量红名单逐条比对，零新增
git grep -n "SearchOption.AllDirectories" -- Plugins/FileTools/Services
git grep -nE "Directory\.Delete|File\.Delete" -- Plugins/FileTools
# T2
cd ForgeSelf.Web && pnpm run check && pnpm run test && node scripts/check-features.mjs
# T3
cd ForgeSelf.Web && bash node_modules/.bin/playwright test e2e/plugins/file-tools
cd ForgeSelf.Web && bash node_modules/.bin/playwright test e2e/menu-route-consistency.spec.ts
pwsh build.ps1                                             # AC-10（先停运行宿主，再起 51888 走查）
```
