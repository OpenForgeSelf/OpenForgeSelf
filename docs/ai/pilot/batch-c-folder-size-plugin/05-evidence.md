# Evidence（批次C · 目录大小排行 —— 扩 FileTools + 快照）

> 阶段：Stage 7｜只记录**实际发生**的检查与其真实输出。分级：Verified（亲自跑过拿到输出）/ Inferred（读代码推断）/ Unknown（未验证）。
> Task ID：`PILOT-batch-c-folder-size-plugin`｜日期：2026-09-27｜闸门1 已批（U-1A′/U-2A′/U-3A′/U-4A′）

## Task

T1 后端 + T2 前端（本文覆盖范围）；T3（插件层 e2e 实跑、发布到运行宿主、浏览器走查）**尚未完成**，见 §Unresolved。

## Changed Files

**新增 · 后端**
- `Plugins/FileTools/Data/Model.xml`（2 表真源：`ScanSnapshot`、`ScanFolderEntry`）
- `Plugins/FileTools/Data/Entities/{ScanSnapshot,ScanFolderEntry}.cs`（**xcode 生成**，未手改）+ 同名 `.Biz.cs`
- `Plugins/FileTools/Data/FileToolsTables.cs`、`Plugins/FileTools/Data/FileTools.htm`（生成附产物）
- `Plugins/FileTools/Models/FolderScanModels.cs`
- `Plugins/FileTools/Services/{FolderScanJobStore,FolderScanService,FolderSnapshotService}.cs`（接口与实现同文件）
- `Plugins/FileTools/FolderStatsToolFunction.cs`

**修改 · 后端**
- `Plugins/FileTools/FileTools.csproj`（+`NewLife.XCode 12.0.2026.701`）
- `Plugins/FileTools/FileToolsController.cs`（+9 个 `folders/*` action、类级 `[Authorize("ApiKeyPolicy")]`、`GetOverview` 补 `folders`、构造注入 2 个新服务）
- `Plugins/FileTools/FileToolsPlugin.cs`（Apply：DI 注册 + `EnsureCreated` + `ctx.Effect` 取消在跑任务 + 注册 `FolderStatsToolFunction`）
- `ForgeSelf.Api/Data/XCodeConfig.cs`（`PluginDbs` += `["FileTools"]="file-tools"`，1 行 + 注释）

**新增/修改 · 前端（宿主包内）**
- 新：`services/fileToolsFoldersApi.ts`、`stores/fileToolFolders.ts`、`components/filetools/{foldersModel.ts,foldersModel.test.ts,FoldersPanel.vue}`、`e2e/plugins/file-tools/file-tools.spec.ts`
- 改：`types/fileTools.ts`（`FileToolTab` += `'folders'` + 镜像类型）、`views/FileToolsView.vue`（第 5 tab）、`components.d.ts`（unplugin 自动生成 1 行 `FoldersPanel`）

**文档**
- 新：`docs/02-features/036-filetools-folder-ranking.md`、本工件链目录
- 追加：`docs/07-decisions/not-taken-decisions.md` §009

## Build

| 检查 | 结果 | 来源 |
| --- | --- | --- |
| `dotnet build Plugins/FileTools/FileTools.csproj` | ✅ **BUILD_OK**（0 error；输出 `2 个警告/7 个错误` → 修完后 `1 个错误` → 最终 grep 无 `: error`，退出码 0） | **Verified** |
| `xcode Model.xml` 生成产物 | ✅ `Data/Entities/` 出现 4 个文件（2 生成 + 2 Biz），`[BindTable(... ConnName = "FileTools")]` + 3 条 `BindIndex` 正确；新增列 `RootOwnBytes`/`ChildCount` 重生成后 `BindColumn` 在位（:71） | **Verified** |
| 生成幂等 | ✅ 连跑两次 `cmp` 4 个文件全部 **SAME**（首轮报 FAILED 是我拿「第一次生成之前」的 md5 清单去比，非产物不稳定） | **Verified** |
| `pnpm run check`（vue-tsc + eslint） | ✅ exit 0，`✖ 82 problems (0 errors, 82 warnings)` —— 82 条与批次A 记录的存量基线**逐条同数**，本批新增文件贡献 0 error / 0 warning（单独 eslint 目标目录输出为空） | **Verified** |
| `tsc -p tsconfig.e2e.json --noEmit` | ✅ exit 0（新 e2e spec 类型正确） | **Verified** |

## Unit Test

| 检查 | 结果 | 来源 |
| --- | --- | --- |
| `pnpm exec vitest run`（全量，含插件 web glob） | ✅ **Test Files 44 passed (44) / Tests 482 passed (482)**，53.7s。基线对照：批次A 记 43 files/473 tests → 本批 +1 file / +9 tests（`foldersModel.test.ts`），**零回归** | **Verified** |
| `foldersModel.test.ts` 9 例 | ✅ 覆盖：分区守恒（Σ行+其他+本级==根总量）、缺「其他」行时必须报不守恒、根为 0 不出 NaN、展示行顺序、6 级空态文案互不相同、轮询同值不赋值（仅 durationMs 变化判为无变化）、取消⇒`calls==0` | **Verified** |
| 后端 xUnit（`FolderScanServiceTests` / `FolderSnapshotPersistenceTests` / `FileToolsFoldersAuthAndToolTests`） | ⏳ 由子代理编写并实跑中（AC-2/3/5 的判定入口） | **Unknown**（本文不预测结果） |
| `dotnet test ForgeSelf.Api.Tests`（全量零新增红） | ⏳ 未跑 | **Unknown** |

## Integration Test

`ForgeSelf.Api.Tests/Integration/FolderSnapshotPersistenceTests.cs`（真实 SQLite，`[Collection("XCode")]` + `XCodeTestFixture`，夹具 `_connNames` 加 `"FileTools"`）—— 文件与执行结果见上表，**Unknown**。

## E2E

- 用例已写好并通过类型检查：`ForgeSelf.Web/e2e/plugins/file-tools/file-tools.spec.ts`（6 个 test：主链路含精确字节与占比断言、钻取、取消两条路、空态分级、快照存看删、零控制台错误）。
- **真实接线证明**：用例断言 `POST /api/filetools/folders/scan` 实际发出且 200，界面数字必须等于临时树写入的已知字节（11,816 / 9,216 / 100 / 2,500），因此不可能被 mock 蒙过。
- ⛔ **尚未运行**。本 worktree 无 `.playwright-browsers`（仓库内固定目录，被 gitignore）→ 需 `pnpm run browsers:install`（~180MB）；且 globalSetup 会另起宿主（本机 7102 当前未运行）。**禁止**用一次性脚本代替（§0 红线）。

## Static Analysis

| 检查 | 结果 |
| --- | --- |
| `git grep -n "SearchOption.AllDirectories" -- Plugins/FileTools/Services` | ✅ 新代码 0 命中（AC-4）。存量命中在 `FileStatsService.cs`:26/:27 —— 属既有方法，本批按 03-plan D-3 未动，逐行归属如实记录 |
| 目录/文件删除动作（铁律10） | ✅ `FolderSnapshotService` 的 `Delete` 只打 `ScanSnapshot`/`ScanFolderEntry` 实体行；无 `File.Delete`/`Directory.Delete` |
| 是否进了 mock 文件 | ✅ `git diff` 未包含 `services/fileToolsApi.ts`（02-spec §0.1-2 的约束成立） |
| 宿主触点面 | ✅ 仅 `XCodeConfig.cs` 1 行 + `components.d.ts` 自动生成行；`ForgeSelf.Api.csproj`/`ForgeSelf.slnx` 未改（FileTools 已在解决方案内，`StageAllPlugins` 靠 glob） |
| 未越界 | ✅ 未改 `Plugins/AIAgent/**`、未改 FileTools 既有 4 个服务与 `FileStatsModels.cs`、未改菜单注册、未改 `menu-route-consistency.spec.ts` |

## Screenshots

尚未产生（e2e 未跑，AC-7/8/9 依赖它）。

## Known Limitations

1. **排行只给「直接子目录」一层**（深一层靠钻取）：为了让 `Σ行 + 其他 + 本级 == 根总量` 可断言；多级展开必然父子重复计数。
2. **快照明细不含被 Top 挤掉的行**：只存 header 的 `ChildCount`/`RootOwnBytes`/`RootTotalBytes`，「其他」行在读侧按差值还原（不留第二份真相）。
3. `filetools.folder_stats` 在 AI 聊天里不可见（AIAgent 白名单，03-plan D-8 + TODO）。
4. 类级鉴权把 FileTools 既有 13 个端点一并收紧为需 token（依据：零 HTTP 消费者）。若未来有外部脚本直连 `api/filetools`，需带 key。
5. 未做插件版本徽标（`plugin-development` 铁律13）：FileTools 视图在宿主包内、非插件根视图，本批按宿主页面处理；迁出宿主 UI 时一并补。
6. 内存任务表随宿主重启清空（快照不受影响）。

## 追加 · 输入 2 的整改（迁成自带界面的独立插件）

| 检查 | 结果 | 来源 |
| --- | --- | --- |
| 宿主 `src/` 是否仍留 FileTools 业务文件 | ✅ `grep -rn "stores/fileTools\|services/fileToolsApi\|types/fileTools\|views/FileToolsView\|components/filetools" src e2e` = 0 命中（仅我 e2e 注释里的一处路径文字，已改指插件新路径） | **Verified** |
| 原文件处置 | ✅ 移入 `.trash/2026-09-28-filetools-host-ui/`（未永久删除，§4.1） | **Verified** |
| 菜单真源单点 | ✅ `FileToolsPlugin` 已无 `MenuExtensions`/`RegisterMenuExtensions`/`FileToolsMenuExtension`（grep 仅剩 `ToolExtensions`）；`plugin.json` 有 `frontend.entry` | **Verified** |
| `dotnet build ForgeSelf.slnx` | ✅ SLN_BUILD_OK | **Verified** |
| `dotnet test --filter ~FileTools\|~FolderScan\|~FolderSnapshot` | ✅ **失败 0 / 通过 45 / 总计 45**（含上一轮那个 schema 红用例；既有 18 例 FileToolsPluginTests 零删改） | **Verified** |
| 插件前端出树构建 | ✅ `scripts/build-plugin-web.ps1 -Plugin FileTools` → `dist/` 仅 `index.js`(101,723B) + `style.css`(63,671B)；30 modules transformed | **Verified** |
| external 裸导入自检 | ✅ `from "vue"`=1、`from "pinia"`=1、`from "element-plus"`=1；内联 Vue 标记=0（未出现第二份 Vue 实例） | **Verified** |
| 宿主前端门禁 | ✅ `pnpm run check` exit 0（0 errors / 82 存量 warnings，与本批变更前同数）；`vitest run` **44 files / 482 tests 全绿**，其中 `../Plugins/FileTools/web/src/components/foldersModel.test.ts (9 tests)` 已由宿主 glob 接管 | **Verified** |
| 迁移踩到的实证点 | ① 静态 `src="/logo/logo-128.png"` 会被 Vite 当资源解析 → 构建失败，改 `:src="logoUrl"`（§3.2 迁移表的再现）；② Tailwind 工具类必须插件自备（宿主只编译宿主的类） | **Verified** |

### 我先前一次错误陈述的更正（不留错误结论）

我曾报告「发布脚本 exit 0 = 发布成功」。**该结论错误**：命令经 `| tail` 管道，退出码是 `tail` 的，脚本实际在 `run-plugin-publish-verify.ps1`:46 抛 `plugin.json not found: ...\ForgeSelf.Api\Plugins\FileTools\plugin.json`。复核证据：宿主 PID 仍 6560、`GET /api/plugin` 里 `file-tools` 仍 **1.0.0**、`GET /plugins/file-tools/frontend/index.js` = **404** → 什么都没发布。已修该脚本 :45/:180 的路径拼接（改为仓库根 `Plugins/$Plugin/...`，与偏差 D-0 同源 bug）。教训：**发布成功判据必须是脚本自身退出码，禁止经管道取 `$?`**。

### 尚未跨过的一步（如实标注）

- **AC-13（发布后 PID 不变 + 版本 1.0.0→1.1.0 + bundle 200）：未验证 → Unknown**。原因不是代码问题，而是**发布目标未定**：运行中的 51888 宿主来自另一检出 `D:\src\tools\ForgeSelf\ForgeSelf.exe`，把本 worktree 的插件推给它会污染其他会话正在验收的副本（`AGENTS.md` §4.1/并行会话避让）。可选目标：① 本 worktree `build.ps1` 出 `publish/` 并起独立端口 + 隔离数据根的宿主；② 用户指定的实例。**已就此征询，未得答复前不擅自停/起他人实例。**
- e2e 实跑仍未做（本 worktree 无 `.playwright-browsers`，需 `pnpm run browsers:install`）；全量 `dotnet test` 未跑（仅跑了 FileTools 相关过滤）。

## 真机端到端（本地包 + 页面自动更新 + MCP 走查）

### 发布与自更新（Verified）

| 步 | 实测 |
| --- | --- |
| `release-local.ps1 -Version v2.2.7 -UpdateDir D:\src\my-proj\OpenForgeSelf\updates` | `ALL DONE in 299s`；`OpenForgeSelf-2.2.7-win-x64.zip` 72.9MB + SHA256SUMS + RELEASE-NOTES |
| 包内取证（补 lockfile 后） | `Plugins/FileTools/web/dist/index.js` **101,723B** + `style.css`；`plugin.json` 794B；全包 `web/dist` 条目 **16 → 18** ✅ 上一版缺的东西这次在包里 |
| 页面（MCP）设置→版本更新 | 更新源=本地目录、LocalDir 已配好；「检查更新」→ **发现新版本 v2.2.7**，72.9MB，SHA256 校验启用；「下载」→ 已下载/更新已就绪 |
| UI 的「重启并更新」按钮 | ⚠ 连点两次页面状态不推进（仍显示「更新已就绪」）。改由 `POST /api/update/apply` 触发 → 返回 `{"status":"applying","tag":"v2.2.7","stagedDir":"%LOCALAPPDATA%\\ForgeSelf\\Updates\\v2.2.7"}` |
| 自更新结果 | `api/update/status.currentVersion = **2.2.7.0**`；宿主 PID `25668`(1:14:46) → **`74524`(1:37:50)** —— **由 update-agent 重启，本 Agent 未停/启/杀宿主进程** |
| 插件版本 | `GET /api/plugin` 里 `file-tools` **1.0.0 → 1.1.0**；`frontend-manifest` 有 `entry:"web/dist/index.js"` + `webVersion:"626492d0f0a1da298470ade5"` |
| 插件资源 | `GET /plugins/file-tools/web/dist/index.js` → **200 / 101,723B**（与磁盘产物同字节）；`style.css` 200/44,944B。（我先前那条 404 是自己探测路径写错成 `/frontend/`，非产品缺陷） |
| 新端点鉴权 | 无 token `POST api/filetools/folders/scan` → **401**；带 token → **200** `{scanId, rootPath, state:0}`「扫描任务已受理」 |

### 界面走查（真实磁盘已知字节树 `%TEMP%/ftcheck_ui`：根 11,816B / A 9,216B / B 100B / 本级 2,500B）

- 5 个 tab 齐（含「目录排行」），视图由插件自带 bundle 远程渲染，无 `.plugin-view-state--error`；
- 扫描结果：`总占用 11.54 KB · 目录 3 · 文件 6 · 耗时 3ms`；行 `A 9.00KB 78.00% (2文件/1子目录)`、`B 100.00B 0.85%`、`本级文件 2.44KB 21.16%`；**无守恒告警**（`warn-line` 不存在）；
- 钻取：根变 `...\ftcheck_ui\A`，`9.00KB / 目录1 / 文件3`，行 `Sub 1.00KB 11.11%` + `本级 8.00KB 88.89%` ✅；
- 快照：确认弹窗 → 列表出现该行（根路径/时间/9.00KB/50）→ **DB 写入成功，说明宿主 `PluginDbs["FileTools"]` 登记 + 插件自建表在真机生效**；删除（确认弹窗）→ 列表 1 → 0，且**只删数据库行**。

### 走查抓到的缺陷（已修，Verified by 门禁）

**`ScanView.state` 序列化为 int，前端按字符串比较**（仓内既有裁决 `not-taken-decisions §007`：DTO 保持 int、前端做映射，我这边没对齐）。后果分两级：
- 可见：完成态徽标条件 `state !== 'Completed'` 恒真 → 界面漏出一个 `2` 的 state-tag；
- **功能性（严重）**：`stopPolling` 条件 `state !== 'Queued' && state !== 'Running'` 对数字 1 恒真 → **大树扫描会在第一次轮询后就停止轮询，界面永久停在部分结果**；`isScanning`/`percentDone` 同样失真。小树 3ms 扫完所以走查时侥幸看不出来 —— 正是「小样本掩盖大缺陷」的典型。

修法（前端侧，遵 §007 不改后端 int）：`FolderScanState` 改 `0|1|2|3|4` + 新增 `SCAN_STATE_NAME` 映射；`pickEmptyState`/`isScanning`/`hasResult`/`percentDone`/`stopPolling` 全部改判数字；面板徽标显示语义名。修后门禁：`NO_STRING_STATE_LEFT`、e2e `tsc` 通过、`vitest` **44 files / 482 tests 全绿**、插件产物重建 `index.js 101,817B`。
顺带修：e2e 里我手算的占比 `77.99%` 是错的，实测 `9216/11816*100 = 78.00`（四舍五入到 2 位），已改断言。

**遗留（Unknown → 需再出一版）**：这个 state 修复**尚未落到运行中的 2.2.7**（2.2.7 里它是坏的）。要闭环须再打 `v2.2.8` 并重走一次页面更新；本回合未做，避免在无人复核下连续动实机。

- **T3 未走完**（AC-7/8/9/10 全部未验证）：e2e 实跑 → `build.ps1` 全量发布（须先停运行中的宿主，DLL 锁 + robocopy 静默跳过）→ 浏览器走查按 §3.4 逐项 → 清临时数据。
- 宿主发布要重启长期运行的 51888 实例，可能影响其他并行会话（02-spec U-2d）。
- 偏差 D-5/D-6/D-10 待补记（`check:features` 是否要求登记 features、宿主 `EnsureTablesCreated` 与插件自建表的时序、`maxDepth` 语义在实现期改为钻取切片）。

---

## 追加（2026-09-28 上午）：e2e 五跑 + 截图读图抓到并修掉第二条时序缺陷

> 本节所有结论都来自**落盘日志的真实摘要**（`$TEMP/e2e-filetools-{1..5}.log`、`$TEMP/dotnet-test-rollup{,2}.log`、`$TEMP/e2e-menu-1.log`、`$TEMP/web-check-rollup.log`）。
> 教训沿用上一节：**管道/后台通知的 `exit 0` 不是证据** —— 首跑通知写 exit 0，真实日志是 6 failed。

### 1. 五跑逐次实况（Verified）

| 跑次 | 结果 | 失败点与根因（全部在**用例侧**，除标注的产品缺陷） |
| --- | --- | --- |
| ① | 6 failed | `getByRole('button', {name:/目录排行/})` 找不到元素 —— 页签是**自绘 tablist**（`role="tab"` 的 button），不是 `role=button`；顺带查出字节串（`11,816 B` 应为 `11.54 KB`）、`.rank-table` 与快照表同名、`.empty` 与 `.empty.small` 同名三处口径错 |
| ② | 5 passed / 1 failed (2.1m) | 弹窗内 `name:'取消'` 子串命中确认键「取消扫描」→ strict mode 2 个 |
| ③ | 5 passed / 1 failed (4.6m) | 150s 超时被**建树**吃光：同一份 12000 文件树首建实测 40s（②）↔ 150s（③）波动 |
| ④ | **6 passed (2.8m)** | 大树改成固定路径 + `.fixture.json` 哨兵的**复用夹具** `ensureBigTree()`（铁律10 只建不删，本就允许复用） |
| ⑤ | **6 passed (2.6m)** | 后端聚合修复后复跑，含新加的中间态闭合断言 |
| ⑥ | **6 passed (2.8m)** | 补铁律13 版本徽标 + 插件产物重建后的全量复跑（`$TEMP/e2e-filetools-6.log`） |

主链路证据（④⑤ 同）：`rootTotalBytes=11816`、`items=[9216,100]`、`rootOwnBytes=2500`、`directoryCount=3`、`fileCount=6`、`Σ行+其他+本级 == 根总量`、界面文本逐字等于接口给的 `totalFormatted` / `percentage.toFixed(2)`。

### 2. 截图读图抓到第二条产品缺陷：部分结果的根分母算错（**已修，Verified**）

- **现象**（④ 的 `screenshots/e2e/file-tools/folders-cancelled.png`）：`总占用 29.00 B · 目录 300 · 文件 4563 · Cancelled`，而每行 `160.00 KB`、占比 **564965.52%**。
- **根因**：`Walk()` 只把文件字节计入**本级** `Total`，自底向上的 `AggregateUpwards()` 在**扫完后**才跑一趟 → `Running/Cancelled` 中间态里 `root.Total` 只含根级字节（那 29B 正是夹具哨兵 `.fixture.json`），分母失真。
- **为什么 e2e ④ 是绿的**：当时取消用例只断言「有 Cancelled + 有行」，没断言数值自洽 —— 弱断言放过了真缺陷（与上一节 `state` 缺陷同一类：**中间态/大树才是照妖镜**）。
- **修法**：改为**逐文件沿父链上卷**（`for (var node = self; node != null; node = ParentOf(job, node)) node.Total += len;`，仍在既有 `lock (job.Sync)` 内），删除 `AggregateUpwards` 及其调用点 → 任意时刻不变式成立。
- **防回归两道**：① 单测 `PartialViews_DuringScan_AlwaysSatisfyPartitionInvariant_AndPercentagesWithin100`（把**每一个**中间轮询视图收下来断言闭合 + 占比 ≤100，与终态无关故不 flaky）；② e2e 取消用例在 Cancelled 后回读接口做同样断言，并断言界面不出现 `.warn-line`。
- **修后实况**（⑤ 的同名截图）：`总占用 7.82 MB · 目录 300 · 文件 2003 · 耗时 3606 ms · Cancelled · 结果已截断：已被取消…`，每行 `160.00 KB = 2.00%` ✅。

### 3. 门禁实况（Verified，均为真实命令输出）

| 门禁 | 命令 | 结果 |
| --- | --- | --- |
| 后端 | `dotnet test ForgeSelf.Api.Tests -p:UseAppHost=false --filter "FullyQualifiedName~FolderScan\|~FolderSnapshot\|~FileToolsFolders"` | **失败 0 / 通过 28 / 总计 28**（2m50s，含新增 1 例）。首轮编译错 `CS1503 double→decimal`（`Percentage` 是 `decimal`），改 `BeInRange(0m,100m)` 后绿 |
| 前端 | `pnpm run check`（`vue-tsc -b && eslint`，`vue-tsc -b` 含 `tsconfig.e2e.json` → e2e 用例被类型检查） | **exit 0**；eslint 0 errors / 81 warnings（既有排版类告警，§5.5 记为不阻断） |
| 插件 e2e | `playwright test --config=playwright.config.ts e2e/plugins/file-tools --output=../.pw-out-filetools` | **6 passed (2.6m)** |
| 应用层门禁 | 同上跑 `menu-route-consistency.spec.ts` + `plugin-remote-view.spec.ts` | **4 passed / 1 failed** —— 红在 `/sems`（见下）；FileTools 的菜单/路由/远程渲染项**通过** |

### 4. 与本批无关、已上报不顺手修

`menu-route-consistency` ② 号用例在 `/sems` 红：Sems 插件加载链正常（宿主日志 `发现插件: 软件工程管理 v1.0.3 - sems` → 启动完成 → 扩展点已发现），但路由渲染内容为 0；同一日志另有两条 `读取…子服务列表失败: SQL logic error`（疑为 Sems 自建表时序）。本批只碰 `Plugins/FileTools/**` + `XCodeConfig.PluginDbs` 一行 + e2e 配置 → 已入 `TODO.md`（P1，来源:批次C e2e 门禁复跑）。

### 5. 版本与发布的连带结论（需用户拍）

`plugin.json` **1.1.0 → 1.1.1**（`web/package.json` 同步）。理由：**已装上运行宿主的 2.2.8 里带的是有缺陷的 1.1.0**（聚合分母 + state 两条都中），不升版本号就没有可比对的更新目标。要闭环仍需一次发布动作：tag→CI、本地 `release-local.ps1 -Version v2.2.9`，或经用户同意的插件侧载 `POST /api/plugin/update/file-tools`。

### 6. 我自己违反铁律13 一处（用户输入10 追问后才查出，**已补 + 复验**）

用户问「插件页面还没显示版本，是不是没更新」。我第一次只把它记成 P2 待确认 TODO —— **定性错了**：`plugin-development` **铁律13** 早已登记「每个插件根视图必须在标题旁展示自身当前版本号」（参考实现 `Plugins/ImGateway/web/src/ImGatewayView.vue`），是本插件漏做，不是新需求。用 TODO 待确认掩盖违规，属流程缺陷。

- 补做：`Plugins/FileTools/web/src/services/pluginMeta.ts`（`fetchPluginVersion`，取**已加载**版本而非构建期常量 —— 侧载/回滚后常量会说谎）+ `FileToolsView.vue` 标题旁 `.ft-version` 徽标 + 副标题补「目录大小排行」。
- 产物：`pnpm run build` → `dist/index.js` **102,432B**（原 101,817B）、`style.css` 45,120B。
- 断言：e2e 主链路加 `toHaveText(/^v\d+\.\d+\.\d+$/)`；⑥ 跑截图实测徽标为 **`v1.1.1`**（Verified，`screenshots/e2e/file-tools/folders-ranking.png`）。
- 门禁复跑（⑥ 同批）：`pnpm run check` **exit 0**（0 errors / 81 warnings）、`vitest` **44 files / 482 tests 全绿**、插件 e2e **6 passed (2.8m)**。

### 7. 技能回写（Persist 完成项）

- `plugin-development` **铁律12** 改为仓库实采形状：`EntityFactory.InitConnection(ConnName)` + `DAL.Create(ConnName).Db.ServerVersion` 探活，并显式标注旧文字 `TableItem.Create`/`dal.SetTables` **本仓 0 命中**；补 `XCodeConfig.PluginDbs` 登记点与「插件禁止自注册 `DAL.AddConnStr`」。
- `e2e-testing` 新增「三条硬规矩」（跑完必读截图 + 为中间态写断言 / 大树夹具固定路径 + 哨兵复用 / 管道 `exit 0` 不是证据）与截图清单两项（**数值自洽**、**版本徽标**），并记两处选择器高频坑（自绘 tablist 是 `role=tab`；弹窗按钮名要 `exact: true`）。
