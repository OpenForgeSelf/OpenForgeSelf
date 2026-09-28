# 036 · 文件工具 · 目录大小排行与快照（批次C）

- 插件：`file-tools`（`Plugins/FileTools/`，宿主内嵌 UI：文件工具箱 → 第 5 个 tab「目录排行」）
- 后端：`api/filetools/folders/*`，控制器 `Plugins/FileTools/Controllers/FileToolsController.cs`
- 状态：本批实现；证据见 `docs/ai/pilot/batch-c-folder-size-plugin/05-evidence.md`

## 1. 这个能力答什么问题

| 问法 | 由谁回答 |
| --- | --- |
| 「这个目录**总共**多大 / 有哪些类型 / 最大的文件是哪些」 | 既有 `api/filetools/stats/{directory,large-files,types}`（`IFileStatsService`，单根标量） |
| 「这个目录下**哪个子目录**最占地方、各占多少、比上周长了多少」 | **本能力**：按目录聚合的占用排行 + 快照 + 趋势 |
| 「C: 还剩多少空间」 | `system-monitor` 的卷级磁盘信息（粒度不同，勿混用） |

## 2. 契约

全部端点在 `api/filetools/` 前缀下，统一信封 `{code,message,success,data}`（前端 `parseResponse` 已解 `data`）。
**整个 `FileToolsController` 自批次C 起类级 `[Authorize("ApiKeyPolicy")]`**：这些端点按调用方给的任意绝对路径读写宿主文件系统，属管理面（`plugin-development` 铁律17）。既有 13 个端点因此一并需要 token —— 依据是全仓无任何 HTTP 调用方（`git grep "api/filetools"` 仅命中 `[Route]`），AI 工具走服务直调不经 HTTP。

| 方法 + 路径 | 入参 | 出参 `data` |
| --- | --- | --- |
| `POST folders/scan` | `{directory, top=50}` | `{scanId, rootPath, state}`（立即返回，扫描在后台跑） |
| `GET folders/scan/{scanId}` | — | `ScanView`（Running 期间是部分结果） |
| `POST folders/scan/{scanId}/cancel` | — | `true`；已结束的任务返 400 |
| `DELETE folders/scan/{scanId}` | — | `true`（只释放内存任务，不动快照/磁盘） |
| `POST folders/snapshots` | `{scanId, note}` | `SnapshotSummary`；进行中的任务返 400 |
| `GET folders/snapshots?take=50` | — | `SnapshotSummary[]`（按扫描时间降序） |
| `GET folders/snapshots/{id}` | — | `SnapshotDetail` |
| `GET folders/compare?from=&to=` | 两个快照 id | `CompareRow[]`（根路径不同 → 400） |
| `DELETE folders/snapshots/{id}` | — | `true`（**只删数据库行**） |

`ScanView` 关键字段：`state`、`rootTotalBytes`/`rootTotalFormatted`、`rootOwnBytes`、`items[]`（`relativePath,name,totalBytes,totalFormatted,directBytes,fileCount,dirCount,percentage`）、`otherRow`、`rootOwnRow`、`truncated`/`capNote`、`inaccessibleCount`、`skippedReparseCount`、`partial`、`childCount`。字节展示串一律由后端产出，前端不第二套口径。

## 3. 算法与口径（为什么排行只给「直接子目录」一层）

- 一趟流式遍历（`DirectoryInfo.EnumerateFileSystemInfos` + 显式栈，**不用** `GetFiles(...,AllDirectories)`）；文件字节取自枚举记录（`FileInfo.Length`，**零额外 stat 系统调用**）；每个文件的字节记到它的**直接父目录** `directBytes`，并**当场沿父链把 `totalBytes` 上卷**（O(depth) 次累加）。为什么是「当场」而不是扫完再归并一趟：扫描中的 `Running/Cancelled` 中间态也要能对外解释 —— 旧写法下根分母只有根级字节，界面实测出现过「总占用 29B / 某行 160KB / 占比 564965%」。
- **并行分片**（1.1.2 起）：根本级文件由主线程直接累计，根的直接子目录各为一个独立分区，`Parallel.ForEach` 并行遍历（`MaxDegreeOfParallelism = min(核数, 8)`），每个分区顺序 DFS；全部共享状态读写都在 `lock(job.Sync)` 内，逐文件上卷保证**任意时刻**（含并行中间态）分区不变式自洽。实测同棵 5.2 万文件树 22.7s → 0.7~2.2s（8 核），与 XCoder/码神工具（枚举自带大小 + 多线程）的差距由此消除。
- **分区不变式**：`Σ(直接子目录 totalBytes) + 根本级文件字节 == 根总量`。排行恒取「直接子目录」这一层切片 —— 同层互不重叠，才能保住这条不变式，`Σ行 + 其他 + 本级 == 根总量` 才可断言。多级展开会让父子重复计数，故**要看更深层用钻取**（以该子目录为新根重扫）。前端 `partitionOk()` 校验失败时会显式告警，不把截断结果冒充准确占用。
- 容错：符号链接/junction/挂载点**不跟随**（`skippedReparseCount`）；条目级 `UnauthorizedAccessException`/`IOException` 计入 `inaccessibleCount` 并继续；仅根路径不可用时任务转 `Failed`。
- 上限：单任务目录 ≤ 200,000、文件 ≤ 5,000,000，触顶即 `truncated=true` 并停止深入；内存任务表 ≤ 20 个（按完成时间淘汰，在跑的不淘汰）。

## 4. 数据落点（批次C 起 FileTools 首次有插件库）

- `Data/Model.xml` 是列/索引唯一真源 → `cd Plugins/FileTools/Data && xcode Model.xml` 生成 `Data/Entities/*.cs`（**禁手改**）；业务方法写 `*.Biz.cs`。
- 两张表：`ScanSnapshot`（一次扫描的头，含 `RootTotalBytes`/`RootOwnBytes`/`ChildCount`/`Top`/`Truncated`）、`ScanFolderEntry`（排行明细行，索引 `SnapshotId`、`SnapshotId+TotalBytes`）。
- 库文件：`{数据根}/Plugins/file-tools/FileTools.db`（连接名 `FileTools`）。连接串由宿主 `ForgeSelf.Api/Data/XCodeConfig.cs` 的 `PluginDbs["FileTools"]="file-tools"` 注册（**插件禁止自注册**）；建表由 `Data/FileToolsTables.EnsureCreated()` 在插件 `Apply` 内完成（宿主建表只扫启动期已加载程序集）。
- 快照不可变：保存只插新行，历史快照永不 UPDATE；趋势按 `relativePath` 跨快照现算。

## 5. 已知边界（不藏着）

1. **`filetools.folder_stats` 工具在 AI 聊天里看不到**：`AIAgentService.ResolveOwnToolDefinitions()` 的白名单只含 `ai-agent` + `memory-system`，FileTools 全部 6 个工具（含本批新增）只能经 `aiagent.universal_tool` 按名调用。扩白名单会撑大本地小模型 prompt，属独立决策（已登记 `TODO.md`）。
2. 内存任务表随宿主进程重启清空 → 要复看必须落快照；界面空态里明写了这一点。
3. FileTools 其余 4 个 tab 的数据仍是 mock（`services/fileToolsApi.ts`），**只有本 tab 走真接口**；mock 债单独登记，不在本批顺手还。
4. FileTools 的 4 个子菜单 Path（`/file-tools/stats` 等）历史上就悬空（宿主只注册 `/file-tools`），本批未动菜单面，故未修（`TODO.md`）。
