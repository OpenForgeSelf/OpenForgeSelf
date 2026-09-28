# Intent（批次C · 文件夹大小统计插件）

> 阶段：Stage 1｜只描述「为什么做 / 做什么 / 做到什么程度」，不提前决定具体代码实现。
> Task ID：`PILOT-batch-c-folder-size-plugin` ｜ 日期：2026-09-27
> 需求来源：用户直接指令（`AGENTS.md` §1.1 最高优先级）——「做一个统计文件夹大小的插件」

## Problem

仓库里「目录大小」这件事散在 4 处，**没有一处能回答「这个盘里哪个文件夹最占地方、各占多少」**：

| 现有实现 | 它答的问题 | 它答不了的 |
| --- | --- | --- |
| `FileStatsService.GetDirectoryStatsAsync`（`Plugins/FileTools/Services/FileStatsService.cs`:8-90） | 「这一个根目录总共多大」 | 按子目录分组、排行、钻取；且 `GetFiles(...,AllDirectories)`(:26) 一次性物化全部路径，大树上是内存+延迟悬崖（全线 `Task.FromResult` 同步假异步，无取消） |
| `ScriptRunner` 模板 `file-dir-size-ps`（`Plugins/ScriptRunner/Services/ScriptTemplateService.cs`:122-138） | 命令行里排一次 Top-N | 每次 spawn PowerShell；无结构化输出、无界面、无进度、无取消 |
| `PluginVersionService.GetDirectorySize`（`ForgeSelf.Api/Plugins/Services/PluginVersionService.cs`:471-477） | 备份目录各自多大 | `private`、只一层，别人用不了 |
| `DiskMonitorService.GetDiskDrivesAsync`（`Plugins/SystemMonitor/Services/DiskMonitorService.cs`:188-230） | 「C: 还剩多少」 | 卷粒度 ≠ 目录粒度；无路径参数 |

同时存在一个必须点破的结构性事实：**FileTools 的界面整体是 mock，13 个真实端点零消费者**
（`ForgeSelf.Web/src/services/fileToolsApi.ts`:349-372 用 `setTimeout(600)` + 硬编码 727MB 造假数据；`git grep "api/filetools"` 全仓仅命中 `[Route]` 特性自身）。它也没有 `plugin.json.frontend` 块、没有 `web/`，UI 长在宿主 `src/views/FileToolsView.vue` —— 与 `plugin-development` 铁律3「界面归插件，宿主的 `src/views/` 不再新增插件页面」相反。

## Why

1. 用户要的是一件**能反复用的能力**（空间治理的第一步），不是一次性脚本；现状要么拿不到排行、要么拿不到结构化结果。
2. 「在 FileTools 上再加一个 tab」这条路会把新界面写进宿主包，等于**继续违反铁律3**，并把 mock 债继承进新功能里（在假数据服务层上写新功能 = 交付一个绿色但从不落盘的界面）。
3. 排行类能力的价值在**可取消 + 可钻取 + 进度可见**，这与 FileTools「一次性请求-响应」的服务形态根本冲突（`FileStatsService.cs`:8/:92/:148/:218 全是 `Task.FromResult` 包装的同步遍历）；把它们塞进同一服务只会把悬崖放大。
4. 本仓插件架构成熟（18 个插件、清单驱动菜单/路由、远程加载 `web/dist`、版本化发布脚本齐备），做一个自带界面的插件的边际成本已被既有骨架摊薄——`plugin-development` §3.1/§3.2 + `plugin-frontend-scaffold` + `run-plugin-publish-verify.ps1` 是现成通路。

## Expected Outcome

用户在宿主导航里看到「存储分析」入口，输入或浏览选定任意目录 → 点扫描 → 看到进度（已遍历文件/目录数、累计字节）→ 得到该目录**各子目录按占用降序的排行**（大小、占比、文件数、子目录数），可点任一子目录**钻取**再扫一层；扫描期间可**取消**；结果含「其他（未列入 Top-N）」行，占比合计≈100%，不留「看起来完整其实是截断」的假象。

后端侧同时得到一个新的可复用契约：`api/storage-analyzer` 的扫描任务（创建/查询/取消）+ 目录浏览，全部经 API Key 鉴权；并有可重复的测试证据（后端 xUnit + 插件 web vitest + 插件层 Playwright e2e 真实落盘目录树）。

## Constraints

逐条对照规范 §1 硬性约束与 `plugin-development` 铁律：

- **C-1 闸门**：S1~S4（Intent/Spec/Plan/Task）完成后须经**闸门1 用户确认**才可进入 Implement（规范 §1.1:16）。本工件状态=待批。
- **C-2 立项先于编码**：命名必须在功能定稿之后，且过「名实相符三问」；立项结论（新建 vs 扩 FileTools、做多大、叫什么）须用户拍板（`plugin-feasibility-study` §二 步骤 4/5）。
- **C-3 界面归插件**（铁律3）：新插件必须自带 `web/`，宿主 `src/views/` 不新增页面；菜单/路由只在 `plugin.json.frontend` 声明一处（铁律19）。
- **C-4 插件前端硬约束**（铁律4/5）：禁止 import 宿主模块与 `@/` 别名；`vue/vue-router/pinia/element-plus` 必须 external 由宿主 import map 解析到同一实例；界面跳转走导航桥（`inject('forgeOpenPage')` 四级降级）；`<ElXxx>` 只能用宿主 `exposeSharedDeps.ts` 已暴露清单内的组件（**无 `ElTable`/`ElTooltip`/`ElRadio`**，实证见 00 §架构特点 6）。
- **C-5 不动无关面**：不修改 `FileTools` 的任何服务/控制器/前端；不删除 `ScriptRunner` 的 `file-dir-size-ps` 模板；不顺手修 `fileToolsApi.ts` 的 mock 债 —— 各自独立记 `TODO.md`（`AGENTS.md` §1.3 范围控制）。
- **C-6 数据安全**（铁律10）：测试只创建、只使用带随机后缀的临时目录，**永不自动删除**；不得指向用户目录（`%USERPROFILE%`/Desktop/Documents/Downloads）与任何真实数据根。
- **C-7 生命周期自管**（铁律14）：后台扫描由插件自身管理器持有 `CancellationTokenSource`，经 `ctx.Effect(Disposable.Create(...))` 在插件卸载时取消；**禁止**挂宿主级 `IHostedService.StartAsync`。
- **C-8 鉴权**（铁律17）：端点接收任意绝对路径并读取文件系统元数据 → 属管理面，须类级 `[Authorize("ApiKeyPolicy")]`（策略声明于 `AppBuilder.cs`:214；现存 24 个插件控制器均无该特性，本插件不得复制这个洞）。
- **C-9 验证走正规测试体系**（`AGENTS.md` §0 红线 / §5.3）：结论只能来自 `pnpm run check`/`pnpm run test`/`dotnet build`/`dotnet test`/Playwright e2e；禁止一次性 `temp/*.cjs` 作验证。
- **C-10 发布唯一入口**（铁律16）：发布只走 `run-plugin-publish-verify.ps1`，禁止手工 Copy-Item 进 `publish/`。⚠ 新插件首次生效需 `POST /api/plugin/install`（`.forgeself-plugin` 包）或冷启动宿主（宿主无热重载）——脚本是否覆盖「首次安装」路径待 S5 实测确认（见 02-spec U-6）。
- **C-11 不新增依赖、不改构建配置**：`net10.0` + `NewLife.Core` + `FrameworkReference Microsoft.AspNetCore.App`（对齐 `Plugins/QuickLinks/QuickLinks.csproj`:4-22）；暂存靠 `ForgeSelf.Api.csproj`:100-120 既有 glob，仅追加 `ProjectReference` 与 `ForgeSelf.slnx` 条目。
- **C-12 风险分级**（`AGENTS.md` §3）：P0 无数据库实体 → 不触发「高风险（DB 迁移须升级给人）」。一旦要做快照持久化，即升为高风险并须单独闸门。

## Success Criteria

以下条件全部由实际命令输出判定，不采用主观描述：

- **SC-1** `dotnet build ForgeSelf.Api` 0 error，且构建输出目录出现 `Plugins/StorageAnalyzer/StorageAnalyzer.dll` + `plugin.json` + `web/dist/index.js`（由 :100-120 `StageAllPlugins` glob 保证）。
- **SC-2** `dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~StorageAnalyzer"` 全绿；用例须对**真实创建的临时目录树**断言，判据自洽（已知字节数 ⇒ 期望大小/顺序/占比唯一确定），不设历史阈值型断言。
- **SC-3** 鉴权回归：反射断言本插件全部控制器带 `[Authorize("ApiKeyPolicy")]`（先例 `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpAdminAuthTests.cs`）通过；无 token 直连 `POST api/storage-analyzer/scans` 得 401。
- **SC-4** `cd ForgeSelf.Web && pnpm run check` exit 0、`pnpm run test` 全绿，其中含本插件 `Plugins/StorageAnalyzer/web/src/*.test.ts`（宿主 `vitest.config.ts`:33 glob 自动纳入）。
- **SC-5** 插件前端产物存在且合规：`grep 'from "vue-router"' Plugins/StorageAnalyzer/web/dist/index.js` 有命中（证明 external 生效、未内联第二份 Vue）；`dist` 内只有 `index.js` + `style.css`。
- **SC-6** `pnpm run test:e2e e2e/plugins/storage-analyzer` 全绿（globalSetup 全新宿主、零 mock），走通「选目录 → 扫描 → 排行 → 钻取 → 取消」；截图落 `ForgeSelf.Web/screenshots/e2e/storage-analyzer/` 并读图核对。
- **SC-7** 四步闭环齐备（`plugin-development` §四）：门禁 → 插件层 e2e → 发布到运行宿主（脚本 `exit 0`）→ 浏览器走查（按 §3.4 交互清单逐项核对：进度防闪 / 空态分级 / 取消二次确认 / 版本徽标 / 截断「其他」行），并清掉走查造的测试数据。
- **SC-8** 工件链齐备：00~06 七件（插件任务属全量级，规范 §4:135）+ `07-final-report` 结构出最终汇报；`TODO.md` 该待办移除，发现的额外问题（FileTools mock 债、4 处重复求和、scaffold 脚本路径 bug、假异步）已入 `TODO.md` 待办区。
