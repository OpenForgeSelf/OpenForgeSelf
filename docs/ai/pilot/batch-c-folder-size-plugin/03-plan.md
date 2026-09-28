# Plan（批次C · 扩 FileTools + 快照持久化版）

> 阶段：Stage 3｜v2 重写（v1 按「新建插件」写，闸门1 否决后回炉）。
> Task ID：`PILOT-batch-c-folder-size-plugin`｜基线 = 02-spec v2（U-1A′/U-2A′/U-3A′/U-4A′ 已批）

## Files To Change

### A. 后端 · 插件 `Plugins/FileTools/`

- file: `Plugins/FileTools/FileTools.csproj`
  reason: 加 `<PackageReference Include="NewLife.XCode" Version="12.0.2026.701" />`（对齐 `Plugins/QuickLinks/QuickLinks.csproj`:16-17）。`StageAllPlugins` 会排除 `NewLife.*` 出插件目录（`ForgeSelf.Api.csproj`:103）→ 类型身份仍单份。
- file: `Plugins/FileTools/Data/Model.xml` ★**新建（列/索引/默认值唯一真源）**
  reason: 结构照抄 `Plugins/QuickLinks/Data/Model.xml`:1-71；`Namespace=ForgeSelf.Api.Plugins.FileTools.Entities`、`Output=Entities`、`ConnName=FileTools`、`Nullable=False`（Option 级）；两张表 `ScanSnapshot`/`ScanFolderEntry`（字段与索引见 02-spec FR-5）。**非主键列一律 `Nullable="True"`**（`agent-workflow.md`:551，否则 SQLite AUTOINCREMENT 报错）。
- file: `Plugins/FileTools/Data/Entities/ScanSnapshot.cs`、`ScanFolderEntry.cs`
  reason: **只能由 `cd Plugins/FileTools/Data && xcode Model.xml` 生成，禁止手写/手改**（铁律9）。附产物 `Data/FileTools.htm` 数据字典一并入库（仓内惯例）。生成后跑两次比对 md5 证幂等，再 `diff` BindColumn 证无字段漂移。
- file: `Plugins/FileTools/Data/Entities/ScanSnapshot.Biz.cs`、`ScanFolderEntry.Biz.cs`
  reason: 人工维护的分部类（生成器绝不碰）：`Valid(DataMethod)` 校验（`RootPath` 空则抛）、`MaxCacheCount`、自定义查询（`FindByIds`/`DeleteBySnapshot` 等）。范例 `Plugins/QuickLinks/Data/Entities/QuickLink.Biz.cs`:29-66。
- file: `Plugins/FileTools/Data/FileToolsTables.cs`
  reason: 建表唯一真源。**形状取 `Plugins/AgentHub/Data/AgentHubTables.cs`:23-66**：`ConnName` const + `EntityTypes` 数组 + `EnsureCreated()` = `EntityFactory.InitConnection(ConnName)` 全量建表 → `DAL.Create(ConnName).Db.ServerVersion` 探活 → 失败 `XTrace.Log.Error` 并 `return false`（不静默吞）。⛔ 不抄 `plugin-development` 铁律12 的 `TableItem.Create`/`dal.SetTables`（全仓 0 命中，编译必失败 → 偏差 D-1）。
- file: `Plugins/FileTools/Models/FolderScanModels.cs`
  reason: DTO：`FolderScanRequest{Directory,Recursive=true,MaxDepth=0,Top=50}`、`ScanAccepted`、`ScanView`、`FolderSizeRow`、`CompareRow`、`SaveSnapshotRequest`、`SnapshotSummary`、`SnapshotDetail`、`enum ScanState{Queued,Running,Completed,Failed,Cancelled}`。**不改既有 `FileStatsModels.cs` 字段**（AI 工具 `filetools.file_stats` 的返回契约要稳）。
- file: `Plugins/FileTools/Services/IFolderScanService.cs` + `FolderScanService.cs` ★核心
  reason: 流式遍历 + walk-up 归并 + 进度/取消 + 快照 CRUD 与 compare。**独立新服务，不改 `FileStatsService.cs`**（其全线 `Task.FromResult` 同步假异步，把真异步塞进去会连带动到 3 个既有端点的行为；范围控制）。`AddScoped` 注册。
- file: `Plugins/FileTools/Services/FolderScanJobStore.cs`（+ 接口）
  reason: 单例内存任务表（`ConcurrentDictionary<Guid,ScanJob>` + CTS + ≤20 淘汰）。`AddSingleton`。
- file: `Plugins/FileTools/Controllers/FileToolsController.cs`
  reason: 追加 8 个 action（`folders/scan` 系列 + `folders/snapshots` 系列 + `compare`）+ 在 `GetOverview`(:30-41) 自描述里补 `folders` 小节；**类级** `[Authorize("ApiKeyPolicy")]`（铁律17 原文要求类级；本控制器现有 13 端点**零消费者**（`git grep "api/filetools"` 仅命中 `[Route]`）→ 一并受保不破坏任何调用方，见偏差 D-2）。
- file: `Plugins/FileTools/FileToolsPlugin.cs`
  reason: `Apply`(:17-32) 追加：`AddScoped<IFolderScanService>` + `AddSingleton<IFolderScanJobStore>`、`EnsureTablesCreated()`（返 false 只 Warn 不抛）、`ctx.Effect(Disposable.Create(...))` 取消在跑任务；`ToolExtensions`(:100-104) 追加 `FolderStatsToolFunction`（`Id="filetools.folder_stats"`、`Name` 全局唯一、`ExecuteAsync` 内 `RecordUsageAsync`）。**不动 `RegisterMenuExtensions`(:38-90)**（不加新菜单，见 AC-9）。

### B. 宿主 · 仅 2 处最小触点

- file: `ForgeSelf.Api/Data/XCodeConfig.cs`
  reason: `PluginDbs`(:24-33) 追加 `["FileTools"] = "file-tools"`。缺这一步 → 连接串没人注册、`ServerVersion` 探到错误库或插件自注册（违反 `agent-workflow.md`:541/:570）。value 必须等于 `Plugins/FileTools/plugin.json`:2 的 `Id`。
- file: `ForgeSelf.Api.Tests/XCodeTestFixture.cs`
  reason: `_connNames`(:14-26) 追加 `"FileTools"`，否则夹具不建本插件表 → `no such table`。

### C. 宿主前端（FileTools 无 `web/`，界面按批复继续落宿主）

- file: `ForgeSelf.Web/src/types/fileTools.ts`
  reason: `FileToolTab`(:1) 联合类型追加 `"folders"`；新增本能力的 TS 镜像类型（与 A 组 DTO 一一对应，字段名逐字对齐，避免前端自算第二套占比口径）。
- file: `ForgeSelf.Web/src/services/fileToolsFoldersApi.ts` ★新建
  reason: **不进 `fileToolsApi.ts`（那是 mock，见 02-spec §0.1-2）**。用 `authFetch`（控制器已类级鉴权；形状照 `pluginApi.ts`:122-128）+ 本文件自带 `parseResponse`（**已解包 `json.data`，禁止再 `.data`**；204 短路必给；照 `todoApi.ts`:21-41/:33-35）。
- file: `ForgeSelf.Web/src/stores/fileToolFolders.ts` ★新建
  reason: 扫描任务/进度轮询/快照列表状态，与 mock 驱动的 `stores/fileTools.ts` 隔离；`stores/fileTools.ts` 仅因 `FileToolTab` 类型扩展而被动兼容，**不改其逻辑**。
- file: `ForgeSelf.Web/src/components/filetools/foldersModel.ts` ★新建（纯函数）
  reason: 无 Vue/EP 依赖的可单测编排：`buildRows(items, top, rootTotal)`（含「其他」合成行 + 字节守恒）、`emptyStateOf(...)`（未扫描/扫描中/目录为空/无权限/任务失效 分级）、`sameProgress(a,b)`（同值不赋值防闪）、`confirmAction({ask})`（二次确认注入）。范例形状 `AIAgent/web/src/sessionArchive.ts`。
- file: `ForgeSelf.Web/src/components/filetools/FoldersPanel.vue` ★新建
  reason: 第 5 个 tab 的面板（Element Plus 全局引入可用；`ElMessageBox.confirm` 样式宿主已带）。滚动容器子区块 `flex-shrink:0`（铁律8）；长路径名 `title` 提示 + 不破版。
- file: `ForgeSelf.Web/src/views/FileToolsView.vue`
  reason: tab 栏(:18-23) 加「目录排行」+ 渲染分支(:67-72) 加 `<FoldersPanel v-else-if="store.currentTab === 'folders'" />`。
- file: `ForgeSelf.Web/src/components/filetools/foldersModel.test.ts` ★新建
  reason: vitest（**后缀 `.test.ts`**，`agent-workflow.md`:503）。

### D. e2e

- file: `ForgeSelf.Web/e2e/plugins/file-tools/file-tools.spec.ts` ★新建
  reason: AC-8。零 mock、真实临时树 `%TEMP%/ForgeSelfE2E_FileTools_{Guid:N}/`（只建不删）；断言「面板发出 `POST /api/filetools/folders/scan` 且表格数字来自响应体」（AC-7 用 `page.on('response')`/`route` 观测，不做网络 mock）。

### E. 文档与技能回写

- file: `docs/02-features/<NNN>-文件工具-目录排行.md`（`<NNN>` 由 `ls docs/02-features` 实测最大号 +1）
- file: `docs/07-decisions/not-taken-decisions.md`（追加）
- file: `.agents/skills/plugin-development/SKILL.md`
  reason: 回写两条实证：① 铁律12 的 `TableItem.Create/dal.SetTables` 与仓库真实形状不符（应指 `AgentHubTables.cs` 的 `EntityFactory.InitConnection`）；② 「宿主 UI 型插件（无 `web/`）改后端 = 必须 `build.ps1` 全量发布 + 重启宿主」的发布路径说明。**只改这两段正文，不动 §2.4 技能清单结构**（`AGENTS.md` §2.4 登记规则不变）。
- file: `docs/ai/pilot/batch-c-folder-size-plugin/05-evidence.md`、`06-review.md`
- file: `TODO.md`、`.forgeself/memory/2026-09-27.md`

## Implementation Steps

> 每步结束即可跑对应验证，失败不进下一步（3 次失败回滚升级）。

1. **依赖 + 清单登记先行**：`FileTools.csproj` 加 XCode；`XCodeConfig.PluginDbs` 加 `["FileTools"]="file-tools"` → `dotnet build` 绿（证明包与宿主共享不打架）。
2. **Model.xml → xcode**：写 `Data/Model.xml` → `cd Plugins/FileTools/Data && xcode Model.xml` → 读生成件核对 `BindTable(ConnName="FileTools")` 与列 → 连跑两次 md5 比对（幂等）→ 补 `.Biz.cs`（`Valid` + 自定义查询）→ `dotnet build` 绿。（AC-1）
3. **扫描算法 TDD（Red→Green）**：在 `ForgeSelf.Api.Tests/Unit/FileToolsPluginTests.cs` **同风格新增 region**（或新文件 `Unit/FolderScanServiceTests.cs`，沿用其 `_testDir` 随机临时目录 + `CreateTestFile(name,size)` 私有 helper）：先写「3 层已知字节树 → 期望排行与 DirectBytes 守恒」红灯 → 实现 `FolderScanService`（`EnumerateFileSystemEntries` 手工递归、walk-up、reparse 跳过、逐条目容错、深度/条目上限）→ 绿。**先不接 DB**。
4. **任务化 + 取消**：`FolderScanJobStore`（单例）+ `Task.Run` + CTS；`FileToolsPlugin.Apply` 注册 + `ctx.Effect` 取消 → 补取消/并发用例 → 绿。
5. **建表 + 快照 CRUD**：`FileToolsTables.EnsureCreated()`（AgentHub 形状）+ `Apply` 调用 → `XCodeTestFixture._connNames` 加 `"FileTools"` → save/list/detail/compare/delete 用例（含快照不可变、compare `Missing`）→ 绿。（AC-3）
6. **控制器**：8 个新 action + `GetOverview` 补 `folders` + 类级 `[Authorize("ApiKeyPolicy")]` → 反射断言用例绿。
7. **AI 工具**：`FolderStatsToolFunction`（复用步骤 3 的服务，不复制算法）+ `RecordUsageAsync` → 用例断言工具能在 `IToolRegistry` 里按 `filetools.folder_stats` 命中并返回可解析 JSON。
8. **后端全量回归**：`dotnet build` + `dotnet test ForgeSelf.Api.Tests`（既有用例零删改；与批次A 记的 9 项存量红名单逐条比对**零新增**）。
9. **前端纯函数 + store + 面板**：`foldersModel.ts` 与其 `.test.ts` 先红后绿 → `fileToolsFoldersApi.ts`（authFetch+parseResponse）→ `fileToolFolders.ts` store → `FoldersPanel.vue` → `FileToolsView.vue` 挂 tab + `types/fileTools.ts` 扩联合类型。
10. **前端门禁**：`cd ForgeSelf.Web && pnpm run check && pnpm run test`；另跑 `pnpm run check:features`（`scripts/check-features.mjs` 是否存在 features 一致性约束，S5 实测；不通过则按提示补 `data/features.ts` → 偏差 D-5）。
11. **e2e**：写 `e2e/plugins/file-tools/file-tools.spec.ts`（真实临时树 + AC-7 接线证明）→ `bash node_modules/.bin/playwright test e2e/plugins/file-tools` → 再跑 `e2e/menu-route-consistency.spec.ts` 复证断言面未动（AC-9）→ 截图读图核对。
12. **发布 + 走查**：`git status` 确认无非本会话在制品 → 停运行宿主 → `build.ps1`（全量，含 `pnpm run build` → wwwroot）→ 起 publish 宿主（51888）→ 浏览器按 §3.4 七项走查 + 清临时数据。（AC-10）
13. **文档 + 沉淀 + 出口清单**：`docs/02-features/`、`not-taken-decisions.md`、`05-evidence`/`06-review`、技能回写、`TODO.md` 收口、当天日记补全 → 按 §10 汇报。

## Test Plan

1. **算法（无 DB）**：已知字节树 ⇒ 每行 `TotalBytes`/`DirectBytes`/排行序/`Percentage` 唯一期望；`Σ列出行 DirectBytes + Other.DirectBytes == RootTotalBytes`（±0.5%）；`maxDepth=0` vs `-1`；条目/文件上限 → `Truncated`；reparse 目录被跳过且计数；只读/无权限目录计入 `InaccessibleCount` 且任务不 `Failed`；根不存在 → 快速失败；取消后 `TotalBytes` 不再增长且状态 `Cancelled`；两个并发任务结果互不污染。
2. **持久化（`[Collection("XCode")]` + `XCodeTestFixture`）**：save→list→detail 逐字段；重复 save 生成新行且旧行 hash 不变（BR-6）；`delete` 只减行数、`grep` 证明无文件系统删除；compare 正常增减 + 消失路径 `Missing=true`；不同 `RootPath` compare → 400。
3. **鉴权**：反射断言 `FileToolsController` 带类级 `ApiKeyPolicy`；运行实例无 token 访问 `stats/directory` 与 `folders/scan` 均 401（如实记录「既有端点鉴权状态被本批一并收紧」及其零消费者依据）。
4. **工具**：`filetools.folder_stats` 注册可见、参数 schema 合法、执行返回可解析 JSON、`RecordUsageAsync` 被调。
5. **前端单测**：`foldersModel.test.ts` 四组（字节守恒 / 根 0 不出 NaN / 取消⇒零请求 / 同值不赋值）。
6. **e2e**：主链路 + 取消 + ≥2 空态 + **AC-7 真实接线证明**；`menu-route-consistency` 复跑。
7. **视觉**：截图读图（图标/间距/对齐/溢出/窄屏/版本徽标）。

## Verification

### Build

```bash
cd ForgeSelf.Api && dotnet build                       # AC-1
cd Plugins/FileTools/Data && xcode Model.xml           # 生成（连跑两次比 md5）
```

### Unit Test

```bash
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~FileTools"        # AC-2/AC-3/鉴权/工具
dotnet test ForgeSelf.Api.Tests                                                # 全量零新增红（与批次A 9 项基线逐条比对）
cd ForgeSelf.Web && pnpm run check && pnpm run test                            # AC-6
```

### Integration Test

```bash
# 不另起炉灶：本项目集成层 = ForgeSelf.Api.Tests/Integration/（真实 SQLite + 直接构造 controller，
# 先例 Integration/TodosControllerTests.cs:11-29）。快照端点用例并入本测试项目（步骤 5），命令同上。
```

### E2E

```bash
cd ForgeSelf.Web
bash node_modules/.bin/playwright test e2e/plugins/file-tools                 # AC-7/AC-8
bash node_modules/.bin/playwright test e2e/menu-route-consistency.spec.ts     # AC-9
```

### Other Checks

```bash
git grep -n "SearchOption.AllDirectories" -- Plugins/FileTools/Services      # AC-4（须逐行归属：新代码 0 命中）
git grep -nE "File\.(Delete|Delete)|Directory\.Delete" -- Plugins/FileTools  # 铁律10：新增代码 0 命中
node ForgeSelf.Web/scripts/check-features.mjs                               # 步骤 10（若该脚本要求 features 登记）
pwsh build.ps1                                                              # AC-10 宿主发布（先停宿主）
```

## Plan 偏差记录

| 时间 | 偏差点 | 原 Plan/文档 | 修正后 |
| --- | --- | --- | --- |
| 2026-09-27 | **D-0** 文档路径与仓库不符（`plugin-development` SKILL.md:206/:250 写 `ForgeSelf.Api/Plugins/<X>/`；`scaffold-plugin-frontend.ps1`:10-11 同错） | 按文档路径 | 一律仓库根 `Plugins/<X>/`；脚本 bug 入 `TODO.md`，本批不顺手修（且本批不新建插件前端，不触发该脚本） |
| 2026-09-27 | **D-1** 铁律12 的建表形状 `TableItem.Create(...).DataTable` + `dal.SetTables(...)` **全仓 0 命中**（仅出现在 `XCodeTestFixture.cs`:50 注释里） | 照铁律12 写 `FileToolsTables.cs` | 取实采先例 `AgentHub/Data/AgentHubTables.cs`:23-66（`EntityFactory.InitConnection` + `ServerVersion` 探活）；**回写技能**（步骤 13） |
| 2026-09-27 | **D-2** 铁律17 要求「类级」`[Authorize]`，但 FR-10 原文写「只给新增 action 加」以免动既有 13 端点 | action 级 | 改**类级**：实证 `git grep "api/filetools"` 全仓仅命中 `[Route]`（零 HTTP 消费者），AI 工具走服务直调不经 HTTP → 类级不破坏任何调用方且更贴铁律；代价（既有端点一并需 token）写进 AC-5 与最终汇报，不隐藏 |
| 2026-09-27 | **D-3** 新服务落点：v1 FR-1 写「在 `FileStatsService.cs` 新增方法」 | 改既有服务 | 独立 `FolderScanService`（`FileStatsService` 全线同步假异步，掺真异步会连带动 3 个既有端点行为）；FR-1 文案已同步修正 |
| 2026-09-27 | **D-4** 快照明细是否单独成表 | 单表 + Clob JSON | 两表（`ScanSnapshot` + `ScanFolderEntry`）：趋势对比需按 `RelativePath` 跨快照查行，Clob 方案查不动 = 假需求满足 |
| 2026-09-27 | **D-5**（待实测）`pnpm run check:features` / `scripts/check-features.mjs` 是否要求新能力登记进 `src/data/features.ts` | 未列入 Files | 步骤 10 实测；要求则补一行（只增不改），并在本表补记依据 |
| 2026-09-27 | **D-6**（待实测）宿主 `XCodeConfig.EnsureTablesCreated()`(:159-191) 与插件自建表的先后/重叠（02-spec U-2c） | 未考虑 | 步骤 5 观察启动日志实证；重叠或冲突则调整 `Apply` 内调用时机并补记 |
| 2026-09-27 | **D-7** FileTools 4 个子菜单 Path 悬空（`FileToolsPlugin.cs`:49-88 vs `router/index.ts`:148；e2e ③ 只断言顶层故未拦） | v1 未涉及 | 本批**不修**（新增 tab 而非菜单，AC-9 复证不受影响）；已入 `TODO.md` 独立项 |
| 2026-09-27 | **D-8** 「让工具在聊天里可见」需改 AIAgent 白名单 `ResolveOwnToolDefinitions()`:311-315 + 前端 picker filter（`AiAgentView.vue` `loadAgentTools`） | v1 Forbidden（不改 AIAgent） | 维持不越界：新工具经已白名单的 `aiagent.universal_tool` 可调用；「FileTools 全部工具在聊天不可见」是既有边界，登记 TODO 单独决策（FR-11 已写明依据） |
