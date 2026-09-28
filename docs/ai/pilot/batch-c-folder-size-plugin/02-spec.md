# Specification（批次C · 文件夹大小统计 —— 扩 FileTools 版）

> 阶段：Stage 2｜v2 重写（v1 推荐「新建插件 + 无持久化」被闸门1 否决）。
> Task ID：`PILOT-batch-c-folder-size-plugin`｜v1 2026-09-27 → **v2 2026-09-27（按批复回炉）**
> 闸门1 已批复（四项均非推荐项）：**U-1A′ 扩 FileTools ｜ U-2A′ 复用现有命名 ｜ U-3A′ P0 带快照持久化 ｜ U-4A′ P0 声明 scan 工具**；U-5（契约上移 Abstractions）= 搁置、U-6 作废（不再有首装问题）。
> **批复 U-3A′ 即 `AGENTS.md` §3「高风险（DB 结构 / 依赖升级）须升级给人确认」的人工批准**，风险等级降档执行。

## 0. 已定基线（原推荐项作废，判定过程留档作「被推翻的决策」）

| # | v1 推荐 | **批复基线（本文按此）** | 连带后果 |
| --- | --- | --- | --- |
| U-1 | 新建插件 | **扩 `Plugins/FileTools`** | 界面走宿主包（存量路径，见 C-3′）；FileTools 首次获得 `Data/` 持久化层 |
| U-2 | `StorageAnalyzer` | **沿用 `file-tools` / `api/filetools` / `filetools.*`** | 无新菜单、无新 route、无新 API 前缀 → 不触碰 `menu-route-consistency.spec.ts` ③ 的顶层 Path 断言 |
| U-3 | 内存态 | **快照持久化 + 趋势对比** | 新增 2 张表 + `xcode` 生成 + csproj 加 `NewLife.XCode` + **改宿主 `XCodeConfig.PluginDbs`**（插件禁止自注册连接串） |
| U-4 | 不出工具 | **声明 `filetools.folder_stats` 工具** | 只注册进 FileTools；**不越界改 AIAgent 白名单**（详见 FR-11 与 D-5） |

### 0.1 三个必须承认的结构性事实（不是否决项，是本轮实现的前提）

1. **界面归插件 vs 存量宿主 UI**：`plugin-development` 铁律3 要求「宿主的 `src/views/` 不再新增插件页面」。FileTools 的 UI 本就长在宿主（`src/views/FileToolsView.vue` + `components/filetools/*Panel.vue`，静态路由 `router/index.ts`:148）。批复选扩 FileTools ⇒ 本能力继续落宿主，属**存量延续**而非新开违规面；但**必须作为新 tab 加进现有 4 tab 结构**，不得新增顶层菜单项。
2. **FileTools 前端整体是 mock**（实证：`ForgeSelf.Web/src/services/fileToolsApi.ts` 无 HTTP 客户端 import，`getDirectoryStats` :349-376 `setTimeout(600)` + 硬编码 727MB；`git grep "api/filetools"` 全仓仅命中 `[Route]`）。新功能**绝不允许写进这个 mock 文件**，也绝不允许复用其 mock 函数 → 新真实接口单独成文（决策 3）。
3. **FileTools 的 4 个子菜单 Path 是悬空的**：`FileToolsPlugin.cs`:49-88 声明 `/file-tools/{rename,cleanup,archive,stats}`，而 `router/index.ts`:148 只注册父级 `/file-tools`；`e2e/menu-route-consistency.spec.ts` ③ 只断言**顶层** Path（其头注释 :14-16 明确把 children 排除在断言外）→ 悬空至今不被拦。本批**不顺手修**（另记 TODO），也**不得**把这些 children 提升为顶层项（一提就红）。

## Functional Requirements

- **FR-1 递归目录聚合扫描**：**新建独立服务** `Plugins/FileTools/Services/FolderScanService.cs`（`IFolderScanService`）提供目录排行扫描，返回**根总量 + 按 `totalBytes` 降序的子目录排行**。**不改 `FileStatsService.cs` 既有方法**（其全线 `Task.FromResult` 同步假异步，掺真异步会连带动到 3 个既有端点行为 → 03-plan 偏差 D-3）。遍历必须 `Directory.EnumerateFileSystemEntries` 手工递归展开，**禁止** `Directory.GetFiles(dir,"*",SearchOption.AllDirectories)`（该文件 :26/:27 现有写法即内存/延迟悬崖，属存量、本批不顺手改；AC-4 只卡新代码路径）。
- **FR-2 walk-up 归并（非 O(n²)）**：文件长度记入其**直接父目录** `directBytes`，并**当场沿父链把 `totalBytes` 上卷**（每个文件 O(depth) 次累加），一趟遍历即得每目录含全部后代的总量。禁止「每个目录各自递归扫一遍」。
  - **修订（2026-09-28，e2e 截图实测驱动）**：原设计是「遍历结束后按深度降序再做一趟归并」，实测**错** —— `Running/Cancelled` 的中间态里根 `Total` 只含根级字节，界面出现「总占用 29B / 某行 160KB / 占比 564965%」这种假数字（`screenshots/e2e/file-tools/folders-cancelled.png`）。改成逐文件上卷后，**任意时刻**分区不变式都成立；归并趟已删除。防回归：单测 `PartialViews_DuringScan_AlwaysSatisfyPartitionInvariant_AndPercentagesWithin100` + e2e 取消用例回读接口断言。
- **FR-3 后台任务化**：扫描不占用请求线程 —— `FolderScanJobStore`（单例，`ConcurrentDictionary<Guid, Job>`）+ `Task.Run` + 自持 `CancellationTokenSource`；`IPlugin.Apply` 内经 `ctx.Effect(Disposable.Create(...))` 取消在跑任务（铁律14，禁止宿主级 `IHostedService`）。
- **FR-4 端点**（全部挂在既有前缀 `api/filetools`，`FileToolsController.cs`:10；沿用既有「POST + `[FromBody]` 请求 DTO」同构，与 :261/:282/:303 一致）：
  | 端点 | 语义 |
  | --- | --- |
  | `POST api/filetools/folders/scan` | 创建扫描任务 → 立即回 `ScanAccepted{ScanId,State}` |
  | `GET  api/filetools/folders/scan/{id}` | 查询状态/进度/**部分结果**（Running 期间即返回截至当前排行） |
  | `POST api/filetools/folders/scan/{id}/cancel` | 取消 |
  | `POST api/filetools/folders/save` | 把已完成任务落快照（FR-6） |
  | `GET  api/filetools/folders/snapshots` | 快照列表（按 `ScannedAt` 降序） |
  | `GET  api/filetools/folders/snapshots/{id}` | 快照明细 |
  | `GET  api/filetools/folders/compare?rootPath=&from=&to=` | 同一 `relativePath` 在两个快照间的增减（趋势） |
  | `DELETE api/filetools/folders/snapshots/{id}` | 删快照（**只删数据库行，绝不删文件系统**，铁律10） |
  同时 `GET api/filetools`（`GetOverview` :30-41 的自描述）补 `folders` 小节 —— 它是该控制器的能力清单，不补等于清单撒谎。
- **FR-5 持久化 2 张表**（FileTools 首次有 `Data/`）：
  - `ScanSnapshot`：`Id(Int64 identity PK)` / `RootPath(String 500, Master, Nullable=False)` / `RootTotalBytes(Int64)` / `DirectoryCount(Int64)` / `FileCount(Int64)` / `DurationMs(Int64)` / `ScannedAt(DateTime)` / `Top(Int32)` / `Note(String 200)`。
  - `ScanFolderEntry`：`Id(Int64 identity PK)` / `SnapshotId(Int64)` / `RelativePath(String 1000)` / `Name(String 260)` / `TotalBytes(Int64)` / `DirectBytes(Int64)` / `FileCount(Int64)` / `DirCount(Int64)` / `Percentage(Decimal)`；索引 `IX(SnapshotId)`、`IX(SnapshotId, TotalBytes)`。
  - 生成链：**只改 `Data/Model.xml` → `cd Plugins/FileTools/Data && xcode Model.xml`** → 生成 `Data/Entities/*.cs`（**禁止手改**）+ 人工写 `*.Biz.cs`（铁律9）。`~/.dotnet/tools/xcode.exe` 已实测存在。
  - **非主键列一律 `Nullable="True"`**（`docs/04-standards/agent-workflow.md`:551：否则 XCode 对非 INTEGER 主键发 `AUTOINCREMENT`，SQLite 直接报错）。
- **FR-6 建表真源** `Plugins/FileTools/Data/FileToolsTables.cs`：`ConnName` const + `EntityTypes` + `EnsureCreated()`。
  **实现形状取仓库真实先例 `Plugins/AgentHub/Data/AgentHubTables.cs`:23-66（`EntityFactory.InitConnection(ConnName)` 全量建表 + `DAL.Create(ConnName).Db.ServerVersion` 探活），不取 `plugin-development` 铁律12 文字里的 `TableItem.Create/dal.SetTables` —— 后者全仓 0 命中（本轮实证），照抄必编译失败。**
  建表异常**不得静默吞**（返 bool + `XTrace.Log.Error`）。
- **FR-7 宿主登记连接串（改宿主 1 处）**：`ForgeSelf.Api/Data/XCodeConfig.cs`:24-33 `PluginDbs` 追加 `["FileTools"] = "file-tools"`（value 必须等于 `Plugins/FileTools/plugin.json`:2 的 `Id`）→ 由 :46-55 推出库文件 `{数据根}/Plugins/file-tools/FileTools.db`，:57-92 建父目录并 `DAL.AddConnStr`。**插件侧禁止自注册 `DAL.AddConnStr`**（`agent-workflow.md`:541/:570；QuickLinks 曾因自己注册出「同名两份库」，见 `QuickLinksPlugin.cs`:155-157 注释）。
- **FR-8 宿主界面（新 tab，零新增路由）**：
  - `ForgeSelf.Web/src/types/fileTools.ts`:1 `FileToolTab` 联合类型追加 `"folders"`；`FileToolsView.vue`:18-23/:67-72 加第 5 个 tab 与 `<FoldersPanel v-else-if>`；`stores/fileTools.ts`:19/:51-54 的 `currentTab`/`setTab` 自动兼容（**其 mock 的 `loadStats` 等一律不动**）。
  - 新组件 `src/components/filetools/FoldersPanel.vue`：路径输入 + 扫描 / 取消（`ElMessageBox.confirm`）+ 进度（轮询 1s，**同值不赋值防闪**）+ 排行表（大小/占比/文件数/子目录数/钻取）+「其他（N 个目录）」行 + Top-N + 快照区（保存 / 列表 / 明细 / 趋势对比 / 删除二次确认）+ 空态分级 + 失败留痕。
  - 新 store `src/stores/fileToolFolders.ts`（扫描任务与快照状态，与 mock 驱动的 `stores/fileTools.ts` 隔离，仅共用 `currentTab`）。
- **FR-9 前端 HTTP 接线（决策 3）**：新建 `ForgeSelf.Web/src/services/fileToolsFoldersApi.ts` —— 采用仓内既有「service 自带 `parseResponse`」惯例（`todoApi.ts`:21-41，**已解包 `json.data`，禁止再取 `.data`**，`agent-workflow.md`:494-495；**204 短路必给**，`todoApi.ts`:33-35）。控制器若带鉴权则改走 `authFetch`（`pluginApi.ts`:122-128 形状）。
- **FR-10 管理面鉴权（类级）**：`FileToolsController` 加**类级** `[Authorize("ApiKeyPolicy")]`（铁律17 原文即「类级」，实证依据见 03-plan 偏差 D-2：该控制器现有 13 端点**零 HTTP 消费者**、AI 工具走服务直调不经 HTTP → 类级不破坏任何调用方）。**代价如实承担**：既有 13 端点一并改为需 token，AC-5 必须实测并报告这一变化，不得当作「只保护了新端点」蒙混。
- **FR-11 AI 工具**：`FileToolsPlugin.cs` 新增 `FolderStatsToolFunction`（`Id="filetools.folder_stats"`、`Name` 全局唯一，`ToolRegistry.cs`:41-44 撞名即丢），`ExecuteAsync` 内**必须** `this.RecordUsageAsync(...)`（`ToolFunctionUsageReportingExtensions.cs`:18，既有 4 个工具 :256/:501/:683/:867 均如此）。
  **不越界改 AIAgent**：`AIAgentService.ResolveOwnToolDefinitions()`:303-321 的 `allowedPlugins` 白名单只含 `ai-agent` + `memory-system` → 实证 **FileTools 现有 5 个工具在聊天路径里本就全部不可见**（不是本批造成的回归）。新工具经已白名单化的 `aiagent.universal_tool`（`UniversalTool.cs`:17-53，按名派发任意工具）即可被调用。把 `"file-tools"` 加进白名单会撑大小模型 prompt（:136-138 与 `UniversalTool.cs`:11 的「77 工具爆 prompt」历史教训）→ **不在本批做**，登记 TODO 让「FileTools 工具整体可见性」单独决策。
- **FR-12 测试矩阵**：见 §Acceptance Criteria（后端 xUnit 三类 + 前端 vitest + 插件层 e2e）。

## Input

`{ directory: string(绝对路径), recursive: bool=true, maxDepth: int(0=仅直接子目录；-1=不限，受条目上限兜底), top: int(默认 50，夹紧 [1,500]) }`；快照入参 `{ scanId }` 或 `{ snapshotId }`；compare 入参 `{ rootPath, from, to }`。前端 token 由 `authFetch` 从 `localStorage['forge_api_token']` 注入。

## Output

信封 `ApiResponse<T>{Code,Message,Success,Data}`（`ForgeSelf.Abstractions/ApiResponse.cs`:3-58，`Ok(data,msg)` 置 `Code=0/Success=true`）。
`ScanView{ScanId,State,RootPath,RootTotalBytes,RootTotalFormatted,DirectoryCount,FileCount,DurationMs,InaccessibleCount,SkippedReparseCount,Truncated,Error?,Items:FolderSizeRow[],OtherRow?}`；`FolderSizeRow{RelativePath,Name,TotalBytes,TotalFormatted,DirectBytes,FileCount,DirCount,Percentage}`；快照/对比同构（`CompareRow{RelativePath,Name,FromBytes,ToBytes,DeltaBytes,DeltaPercent}`）。`Percentage` 与 `RootTotalBytes` 恒为后端算好后回传（前端不自算第二套口径 = 不留第二份真相）。

## Business Rules

- **BR-1 不跟随重解析点**（符号链接/junction/挂载点，`FileAttributes.ReparsePoint`）→ 计入 `SkippedReparseCount`，防环、防重复计数、防扫出根之外。
- **BR-2 逐条目容错**：单条目 `UnauthorizedAccessException`/`IOException` 吞并计入 `InaccessibleCount`，扫描继续；**仅根路径不可用**才转 `Failed`（对齐 `DirectoryBrowseService.cs`:78-81、`CleanupService.cs`:59-62 既有模式）。
- **BR-3 占比口径**：`Percentage = round(TotalBytes/RootTotalBytes*100, 2)`；根为 0 时全置 0，禁 NaN/∞。断言不变式：`Σ(列出行 DirectBytes) + OtherRow.DirectBytes == RootTotalBytes`（容差 ±0.5%）。
- **BR-4 大小格式化**：复用插件既有 `FileSizeFormatter.FormatSize`（1024 基 / `N2`），**不再造第二份格式化器**（这是本批唯一允许的复用，因它就在同一插件内）。
- **BR-5 资源上限**：内存任务表 ≤20（按完成时间淘汰最旧，但**已落快照的**不受淘汰影响）；单任务目录条目 ≤200,000、文件 ≤5,000,000，触顶即 `Truncated=true` 并停止深入。
- **BR-6 快照是不可变事实**：`save` 只写新行，永不 UPDATE 历史快照；趋势 = 两快照行间算。快照删除只删 DB 行。
- **BR-7 同一 directory 重复扫描各自独立**，不做结果复用（避免陈旧排行冒充新结果）。
- **BR-8 趋势对比的匹配键是 `RelativePath`**（大小写不敏感、`\`→`/` 归一），路径消失 → `ToBytes=0` 并显式标 `Missing=true`，不得静默丢行。

## Boundary Conditions

根是文件 / 不存在 / 已拔盘 → 400 或 `Failed`（带原因）；UNC → 允许，慢由取消兜底；空目录 → 排行空 + 空态「目录为空」；整盘无可读权限 → `InaccessibleCount>0` + 空态「无读取权限」（两种空态文案必须不同）；`maxDepth=0`/`-1`；长路径 >260（宿主 TFM `net10.0-windows` 仍须有真实用例证明）；`top` 越界夹紧并回显生效值；快照 `RootPath` 已不存在 → 明细仍出、界面标注「原路径已不存在」；compare 两个快照 `RootPath` 不同 → 400。

## Error Handling

| 场景 | HTTP | 处理 |
| --- | --- | --- |
| 根路径非法/不存在/是文件 | 400 | `ApiResponse.Error(msg,400)`，msg 带路径与原因（同 `FileToolsController.cs`:272-279 的 catch 形状） |
| 未带 token 访问新增 folders 端点 | 401 | `ApiKeyPolicy` 默认 |
| `scanId`/`snapshotId` 未知 | 404 | 前端引导重新扫描 |
| 后台遍历内异常 | 任务态 | 转 `Failed` + `Error` 文本，HTTP 200 回该状态（**禁**让轮询拿到 500） |
| 条目级异常 | — | BR-2 计数继续 |
| 建表失败 | 启动期 | `FileToolsTables.EnsureCreated()` 返 false → `XTrace.Log.Error`，`Apply` **不抛**（`SamplePlugin.cs`:33-34） |

前端失败必须**留痕可查**（面板内打印原因 + 处理建议），不是一闪而过的 toast（`plugin-development` §3.4-2）。

## Compatibility

- 既有 `api/filetools` 13 端点**签名与鉴权状态不变**；`DirectoryStatsResult`/`FileTypeItem` 等既有 DTO 不改字段（新 DTO 另建，避免动到 AI 工具 `filetools.file_stats` 的返回契约 :846-861）。
- 无新菜单/新 route/新 API 前缀 → `menu-route-consistency.spec.ts` 四条断言不受影响（改前仍须实跑复证，见 AC-9）。
- FileTools 与宿主同进程共享 `NewLife.XCode`：`StageAllPlugins` 故意排除 `NewLife.*`（`ForgeSelf.Api.csproj`:103，注释 :93「避免类型身份分裂」）→ 加包不会双份。
- 宿主触点仅 2 个文件：`ForgeSelf.Api/Data/XCodeConfig.cs`（`PluginDbs` +1 行）、`ForgeSelf.Api.Tests/XCodeTestFixture.cs`（`_connNames` +1 行）。测试项目已引用 FileTools（`ForgeSelf.Api.Tests.csproj`:41）。
- 新增 SQLite 库文件 `~/.forgeself/Plugins/file-tools/FileTools.db`（生产）；**只建不删**，且绝不指向 `AppContext.BaseDirectory/Data`。
- 前端：新文件 `fileToolsFoldersApi.ts` + `stores/fileToolFolders.ts`；`fileToolsApi.ts`（mock）与 `stores/fileTools.ts` 的 mock 动作零改动。
- ScriptRunner 的 `file-dir-size-ps` 模板本批不动（TODO 已登记收口议题）。

## Non-functional Requirements

- 扫描不占请求线程；大树可取消；1s 轮询在 `Running` 期间回部分结果（首屏不必等全扫完）。
- 单任务驻留 ≤~50MB（BR-5 是实现手段）；快照表按行存，单快照 ~Top+1 行（不放大）。
- 10 万量级文件扫描耗时**实测记录为基线，不设门禁阈值**（禁止埋历史阈值型断言）。
- 遵守 `plugin-development` §3.4 全部 7 项交互要求。

## Acceptance Criteria

> 闸门1 已批复本清单；实现期不得改判据。

- [ ] **AC-1** `cd ForgeSelf.Api && dotnet build` 0 error；`xcode Model.xml` 连跑两次产物 md5 一致（生成幂等实证），`Data/Entities/` 出现成对 `.cs` + `.Biz.cs`，`diff <(grep -oE 'BindColumn\("[A-Za-z]+"' 旧) <(...新)` 无字段漂移。
- [ ] **AC-2** `dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~FileTools"` 全绿：**既有 `Unit/FileToolsPluginTests.cs` 一条不许删/改判据**（零回归），新增用例覆盖：已知字节树逐行 `TotalBytes`/排行序/`Percentage` 相等、DirectBytes 字节守恒（BR-3）、`maxDepth` 0 vs -1、条目上限→`Truncated`、reparse 跳过计数、无权限计入且不 `Failed`、取消后字节不再增长且状态 `Cancelled`、并发两任务互不影响。
- [ ] **AC-3** 持久化用例（`[Collection("XCode")] : IClassFixture<XCodeTestFixture>`，`_connNames` 已含 `"FileTools"`）：save→list→detail 逐字段相等；快照不可变（重复 save 出新行、历史行 hash 不变）；`delete` 只减 DB 行且 `git grep` 证明无 `File.Delete/Directory.Delete` 打在该路径；compare 命中 `Missing=true` 分支。
- [ ] **AC-4** 禁全量物化：`git grep -n "SearchOption.AllDirectories" -- Plugins/FileTools/Services/FileStatsService.cs` 中**新代码路径** 0 命中（既有 :26/:27 属 `GetDirectoryStatsAsync`，本批不改 → 该 grep 结果须逐行归属说明，不得为凑 0 而顺手改旧方法）。
- [ ] **AC-5** 鉴权：新增 folders action 带 `[Authorize("ApiKeyPolicy")]`（反射断言用例，形状照 `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpAdminAuthTests.cs`）；运行实例无 token `POST api/filetools/folders/scan` 实测 **401**；同时既有 `stats/directory` 的鉴权状态**不变**并如实报告。
- [ ] **AC-6** `cd ForgeSelf.Web && pnpm run check` exit 0、`pnpm run test` 全绿；新增 `src/**/fileToolFolders*.test.ts`（**后缀必须 `.test.ts`**，`.spec.ts` 被 vitest 排除、留给 Playwright —— `agent-workflow.md`:503）锁死：「其他」行字节守恒、根为 0 不出 NaN、取消确认注入下「用户取消 ⇒ 零请求」、进度同值 ⇒ 不赋值。
- [ ] **AC-7** 界面真接线证明：e2e 拦截网络，断言面板**发出** `POST /api/filetools/folders/scan` 且表格数字来自响应体（非 `fileToolsApi.ts` 假数据）；`grep -c "setTimeout(600)" ` 类 mock 不出现在新链路。
- [ ] **AC-8** 新 e2e `ForgeSelf.Web/e2e/plugins/file-tools/file-tools.spec.ts`：真实临时目录树（落 `%TEMP%/ForgeSelfE2E_FileTools_{Guid:N}/`，**只建不删**，铁律10）；主链路（扫描→排行→钻取→保存快照→趋势对比）+ 取消 + ≥2 类空态；零 mock；截图落 `screenshots/e2e/file-tools/` 并读图核对。
- [ ] **AC-9** `e2e/menu-route-consistency.spec.ts` 实跑绿（4 passed）——证明「加 tab 不加顶层菜单」确实没碰断言面。
- [ ] **AC-10** 发布与走查：FileTools 前端在宿主包内 ⇒ 宿主产物须经 `build.ps1`（**先停运行宿主再覆盖**，铁律16 的 robocopy 静默跳过坑）；后端有改动 ⇒ 必须重启宿主生效；随后按 §3.4 七项逐项走查（进度无闪 / 取消两路 / 空态三种 / 「其他」行 / 版本徽标 / 长名不溢出 / 窄屏不破版）+ 清走查临时数据。
- [ ] **AC-11** 文档：`docs/02-features/<NNN>-文件工具-目录排行.md`（端点契约 + 表结构 + 与既有 `stats/*` 的分界 + 「FileTools 工具在聊天里不可见」的已知边界）；`not-taken-decisions.md` 追加被否决项（新建插件 / 不持久化 / 越界改 AIAgent 白名单 / 顶层新菜单）；`TODO.md` 本待办移除 + 新增本轮发现的悬空子菜单、mock 债范围扩大、铁律12 文字与仓库不符（技能回写）。
- [ ] **AC-12** `05-evidence.md`（Verified/Inferred/Unknown 分级，只列实际发生）+ `06-review.md`（八问 + Final Decision）+ 按 `07-final-report.tpl.md` 出最终汇报。

## Unknown（v2 残留）

| # | 不确定点 | 影响 | 处理方式 |
| --- | --- | --- | --- |
| U-2a | `Percentage` 列 `DataType` 用 `Decimal` 还是 `Int64`(×100) | xcode 生成列类型；SQLite Decimal 精度 | **保守假设 Decimal** 并在 Model.xml 生成后读产物核对，不符即记偏差 |
| U-2b | 快照保留策略（是否自动裁剪到最近 N 个） | 长期跑表会涨 | **搁置**：本批不自动裁剪（裁剪=自动删除，铁律10 精神要求人工执行），列表分页展示 |
| U-2c | 宿主 `XCodeConfig.EnsureTablesCreated()`（:159-191 反射已加载程序集）是否会与插件自建表冲突 | 重复建表 / 类型身份 | **S5 实测**：观察启动日志；冲突即记 03-plan 偏差再修，不预先猜 |
| U-2d | AC-10 的宿主发布是否会让并行会话的在制品被覆盖 | 用户是多会话共仓（见 auto-memory） | **执行前 `git status` + 停/起实例前通报**，不静默覆盖；发现非本会话在制品即停手升级 → **实测命中：运行中的 51888 宿主属于另一检出 `D:\src\tools\ForgeSelf\ForgeSelf.exe`（PID 6560），不是本 worktree 的产物，发布目标待定** |

## 11. 追加裁决（输入 2 · 覆盖 §0.1 的界面口径，2026-09-27 夜）

用户指令：**「文件工具应该前后端都迁移到独立文件夹，自身更新不依赖宿主，不重启宿主」**。判定：原 §0.1-1 承认的代价（界面留宿主）不接受 → U-1A′ 的**落点**（扩 FileTools）保留，**界面口径撤销**。整改条款：

- **FR-13 前后端全量归插件**：`FileToolsView` + 4 个既有面板 + `fileToolsApi` + `stores/fileTools` + `types/fileTools` + `AppLogo` 副本整体迁入 `Plugins/FileTools/web/`；本批新写的 4 个文件直接落在插件内。宿主 `src/` 内不再保留任何 FileTools 业务文件（原文件移 `.trash/2026-09-28-filetools-host-ui/`，不永久删除；`router/index.ts` 静态路由与 import、`components.d.ts` 声明、`data/features.ts` 注释同步）。
- **FR-14 菜单真源单点**：`plugin.json` 加 `frontend{views:["FileToolsView"],menu:"文件工具",route:"/file-tools",icon:"fa-solid fa-folder-open",entry:"web/dist/index.js"}`；**删除** `FileToolsPlugin` 的 `MenuExtensions` 属性 + `RegisterMenuExtensions` + `FileToolsMenuExtension`（铁律19②；顺带消掉 4 个悬空子菜单，即 §0.1-3 那条负债）。
- **FR-15 自带产物**：vite lib（ESM + `style.css` + external vue/vue-router/pinia/element-plus + 自备 Tailwind 入口 + `publicDir:false`）；沙箱内经**本批新增的可复用脚本** `scripts/build-plugin-web.ps1 -Plugin FileTools` 出树构建（把 §3.2 兜底固化，不再手写一次性命令）。
- **FR-16 更新不重启宿主 = 本条即验收目标**：FileTools 后续迭代只走 `POST /api/plugin/update/file-tools`（`versions/<ver>/` + current 指针 + ALC 换载 + 控制器动作刷新 + 静态资源从 `versions/<current>/web/dist/` 读），宿主进程不重启。
- **AC-13**：发布前后 `Get-Process ForgeSelf` PID 不变，且 `GET /api/plugin` 里 `file-tools` 版本 1.0.0→1.1.0、`GET /plugins/file-tools/frontend/index.js` 返回 200。
- **必须如实声明的边界（不许粉饰）**：本批含 1 处**宿主代码**改动 —— `ForgeSelf.Api/Data/XCodeConfig.cs` 的 `PluginDbs` 登记 `["FileTools"]="file-tools"`（插件库连接名唯一登记处，`agent-workflow.md`:540；插件禁止自注册）。所以**这一版仍需重建并重启宿主一次**；FR-16 的「不重启」成立于此后所有 FileTools 自身迭代。此边界须在最终汇报单列，禁止写成「已完全无需重启」。
- **实现期发现的发布通道 bug（已修）**：`run-plugin-publish-verify.ps1`:45/:180 同样按 `ForgeSelf.Api/Plugins/$Plugin/...` 拼路径 → 对仓库根 `Plugins/` 必然 `plugin.json not found`（与偏差 D-0 同源）。已改为 `Plugins/$Plugin/...`。
- 范围覆盖：03-plan/04-task v2 的 Forbidden「不改宿主前端业务面」「不动 FileTools 既有面板与服务」两条**作废**；其余 Forbidden（不碰 AIAgent、不改构建目标、不删目录、不引新依赖、不手拷发布产物）继续有效。
