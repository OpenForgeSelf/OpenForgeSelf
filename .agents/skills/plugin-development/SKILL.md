---
name: plugin-development
description: 新建 / 维护 OpenForgeSelf 插件的端到端指南（后端 + 自带界面 + 维护闭环）。用于「新建插件」「插件从宿主迁移成独立插件」「改完插件要怎么走完流程」。当用户要新建一个插件、或要改动/修复已有插件（尤其是带 web/ 界面的插件）时使用。
---

# 插件开发与维护（新建 · 迁移 · 改完就发）

本技能是插件工作的**总入口**。它把「新建插件」和「改完插件后的维护闭环」串起来，
细节分别委托给下面三个专项技能，**不要在这里重复它们的正文**：

| 专项技能 | 负责 |
|---|---|
| `plugin-frontend-scaffold` | 从 AIAgent 模板生成插件 `web/` 前端骨架 |
| `plugin-publish-verify` | 发布与验证：主路径 = 打 tag 自动发布 + 页面自动更新；禁止 agent 停宿主 |
| `e2e-testing` | 插件层 e2e（`e2e/plugins/<id>/<id>.spec.ts`）+ 截图读图 |

---

## 一、什么时候用

- 要新建一个新的后端插件（纯后端 / 带界面）
- 要把宿主内置的页面**迁移成独立插件**（如 QuickLinks、AIAgent 走过的路）
- 要改 / 修一个已有插件，需要知道「改完还要做什么」

---

## 二、铁律（先看这 20 条）

1. **先读技能再动手。** 涉及插件的任务，开工前先读本技能 + 上表对应的专项技能。
   历史上正是因为技能没被读取、也没登记进 `AGENTS.md`，导致改完插件后
   既没发插件、也没跑插件层 e2e，只写了临时脚本自验就宣称完成。
2. **两层命名**：目录 / 程序集是 PascalCase（`QuickLinks`），运行时 id 是 kebab-case（`quick-links`）。
   `plugin.json` 里 `Id` 用 kebab，发布脚本 `-Plugin` 传**目录名**。
3. **界面归插件，不归宿主。** 新插件一律自带 `web/`，宿主的 `src/views/` 不再新增插件页面；
   已有的宿主内置页面应逐步迁出（迁完删宿主实现 + 删 `dynamicPlugins.ts` 的 switch-case 分支）。
4. **插件前端的真实约束只有一条：不能 `import` 宿主模块。**
   - ✅ **能用** Tailwind、**能用** `<ElXxx>`、**能用** `vue-router` 等——前提是
     ① 在插件 `vite.config.ts` 的 `rollupOptions.external` 里声明为外部依赖，
     ② 构建产物经宿主 `index.html` 的 import map 解析到**宿主同一份实例**
     （已由 AIAgent / Home 两个插件实证；`<ElXxx>` 另需宿主 `exposeSharedDeps` 暴露相应组件）。
   - ❌ **不能用** `@/xxx` 别名（指向宿主 `src/`，插件是独立预编译产物，运行时解析不到）；
     不能 `import` 宿主的 store / service / 组件文件。
   - ⚠️ **漏声明 external 会造成静默失效**：插件会内联一份自己的 vue/vue-router 副本，
     与宿主非同一实例 → `inject` / `useRouter()` 返回 `undefined`，且**只在用户点击那一刻报错**，
     极难定位。构建后自检：`grep 'from "vue-router"' dist/index.js` 应有命中（证明是裸导入）。
5. **插件内跳转必须走导航桥，禁止自行 `router.push` 当主路。**
   全仓无 `router.afterEach`，tab 栏与 usage 统计**仅**由宿主 `useOpenPage().openPage()` 触发。
   宿主提供 `app.provide('forgeOpenPage')` + `window.__FORGE_OPEN_PAGE__`（同一函数实例）双通道。
   插件侧建议用四级降级链：`inject` → `window.__FORGE_OPEN_PAGE__` → 自带 `router` → `location.assign`，
   并把生效通道 `console.debug` 出来（避免静默降级成悬案）。
   **宿主侧桥体必须包 `app.runWithContext()`**——否则桥由插件组件调用时，
   内部 `useRouter()` 的 `inject` 会落到插件实例上而返回 `undefined`，抛
   `Cannot read properties of undefined (reading 'push')`（Home 插件实测踩坑，见 `specs/033-home/design.md` §9）。
6. **HTTP 方法以后端 `[Http*]` 特性为准**，不要照抄调用方。
   踩过：后端是 `[HttpPut("{id}")]`，移植时沿用 POST → 405。
7. **改完插件 = 代码改完 + 门禁通过 + 发布（打 tag 自动发布 / 本地目录更新源 + 页面自动更新）+ 隔离实例走查 + 运行实例只读复验**，五步缺一不算完成。
8. **插件根视图的滚动容器其子区块必须 `flex-shrink: 0`。**
   插件根组件常写成 `height:100%` + flex 列 + 内层滚动容器（如 `.home-content { flex:1; overflow-y:auto }`）。
   该滚动容器的**直接子区块默认 `flex-shrink:1`**：一旦内容总高超过容器（视口一矮就触发，
   Playwright 默认 1280×720 即中招，1440×900 不触发），区块被 flex 压扁，
   再叠加区块自身的 `overflow:hidden` 会直接裁掉内部文字/内容（典型症状：Hero 问候语「文字挡住半截」）。
   修法：`.scroll-container > * { flex-shrink: 0 }`（覆盖全部当前+未来区块，非魔法数字）；
   e2e 视觉用例加回归守卫「区块必须完整容纳其内部内容」。Home 插件实测踩坑，见 `specs/033-home/design.md` §13 行 5。
9. **【实体改动铁律】改实体一律先改 `Data/Model.xml`，再跑 `xcode Model.xml` 生成 —— 禁止手改生成件。**
   ```
   Plugins/<X>/Data/
   ├── Model.xml                    # ★ 列/索引/默认值的唯一真源，人工维护
   └── Entities/
       ├── <Name>.cs                # xcode 生成，**会被覆写** —— 不要手改！
       └── <Name>.Biz.cs            # 人工维护，**生成器绝不碰** —— 业务方法写这里
   ```
   - **动手前先 `ls Entities/`**：存在同名 `.Biz.cs` 即说明本项目早已启用分部类约定
     （所有插件实体都是成对的）。**别把自定义查询写进 `.cs`** —— 下次 `xcode` 就丢。
   - 每插件**必须有** `Data/Model.xml`；结构照抄 `Plugins/Scheduler/Data/Model.xml`。
     `ConnName` / `Namespace`（`ForgeSelf.Api.Plugins.<X>.Entities`）/ `Output=Entities` 三项务必对齐。
   - XML 的 `Column@DataType` / `@Nullable` / `@DefaultValue` 是列类型与默认值真源
     （如 `Vendor=custom`、`Status=Queued`、`Enabled=1`）。
   - 跑法：`cd Plugins/<X>/Data && xcode Model.xml`。工具在 `~/.dotnet/tools/xcode`；
     ⚠ `xcode --help` 无效（会把 `--help` 当文件名处理）。
   - 生成**幂等**（连跑两次产物 md5 字节一致），可放心纳入日常流程。
   - 生成附产物：`<X>.htm` 数据字典 **入库**（各插件都有）；`Config/XCode.config` 已被 `.gitignore` 忽略，不处理。
   - 生成出的 `<summary>{name}。…</summary>` 带**未替换的 `{name}` 占位符**是全项目既有样式
     （7 个插件无一例外），**不是缺陷，不要单独去修**（否则反而不一致）。
   - **改完必跑三件套**：① 编译 0 error；② 该插件测试全绿；
     ③ `diff <(grep -oE 'BindColumn\("[A-Za-z]+"' 备份.cs) <(同 新.cs)` 确认**无字段漂移**。
   - 历史教训（2026-09-21）：曾直接手改 `AgentDefinition.cs`（生成件）加自定义查询，
     被用户纠正「不要直接改实体，应该改 xml 然后执行 `xcode xx.xml` 更新实体」。
10. **【数据安全铁律·禁止删除】测试与插件代码一律不许主动删除数据库文件 / 数据目录 —— 无例外。**
   - **硬约束：禁止任何形式的"清库/删库/删目录"自动化**。包括但不限于
     `File.Delete` / `Directory.Delete` / `rm` / `rm -rf` / `del /S` / `Remove-Item -Recurse`
     打在 **任何** 数据目录上；也包括挂在 `ProcessExit` / `Dispose` / `AppExit` 上的"退出时清理"。
     **测试库目录只创建、只使用，永不删除** —— 磁盘上多几个临时 db 文件是可接受的成本，
     远低于"路径算错一次就删掉真实数据"的风险。
   - **为什么禁止（三条理由，别再自己找补）**：
     1. **收益极低**：测试库落在 `bin/Debug/**/Data/` 这类构建产物目录里，本就是可抛弃物，
        `dotnet clean` / 删 `bin` 即可统一收拾，**不需要测试进程自己去删**。
     2. **风险不对称**：换来的是"少几个临时文件"，代价是"任何一处路径判断失误即删真实数据"。
     3. **不可靠**：`ProcessExit` 挂钩在强杀/崩溃时根本不执行 —— 为了一个**本来就不可靠的清理效果**
        去承担删错目录的风险，这笔买卖从一开始就不划算。
   - 测试夹具改连接串 / 建表**必须**指向带随机后缀的隔离目录
     （如 `TestDataDir/{类名}_{随机串}/{连接名}.db`），**禁止**指向：
     - 用户目录（`%USERPROFILE%` / `~` / `Desktop` / `Documents` / `Downloads`）
     - 运行态真实库：`ForgeSelf.Api/bin/**/Data/*.db`、`publish/**/Data/*.db`
     - 宿主配置的数据根（`AppContext.BaseDirectory/Data`）
   - `XCodeConfig.InitializeXCodeDatabase` 对 `env.IsEnvironment("Testing")` 会 early return ——
     **这就是"不在测试期碰真实库"的官方闸门**，测试里不要绕过它。
   - ⚠ **反面教材（务必记住）**：曾在 `XCodeTestFixture` 里挂 `ProcessExit` 去删测试库目录
     （当时的理由"保持干净"），经用户质询后**判定为错误做法并已移除**。
     另有一个连带教训：曾把删除挂在夹具 `Dispose` 上 —— `IClassFixture` 是"每类一份实例、跑完即 Dispose"，
     引用计数在第一个类结束时归零 → 目录被删 → 后续类全落空，**一次性炸 27 个测试**。
     两个教训指向同一条结论：**不要自动化删除任何数据目录。**
   - 若确实需要清理，**由人手动执行**（或走独立的、显式确认的清理脚本），
     绝不内嵌进测试/插件的运行路径里自动触发。
11. **【测试隔离铁律】每个测试类用独立数据库，业务唯一性校验一律直查 DB，禁读实体缓存。**
   - `Entity<T>.Meta.Session` 是 **`AsyncLocal`**（`Entity_Meta.cs:78`），故 `Meta.Cache` 是
     **每执行上下文一份**的进程内缓存。夹具构造函数 / `BeforeAfterTestAttribute.Before` /
     测试方法体**分属不同 AsyncLocal 上下文** —— 在前者里清缓存**清不到后者正在用的那份**。
     症状极迷惑：`FindAll()` 直查库得 0 行，缓存路径却读出**上一批次甚至另一个物理库**的幽灵行
     → 误报「厂商标识 xxx 已被占用」。加诊断反而"好了"，是因为诊断里的 `FindAll()` 顺手刷新了当前上下文。
   - → **普适推论**：**唯一性校验、存在性判断等正确性关键路径，一律直查数据库**
     （`FindAll(_.X == v)` / `FindCount(...)`），**绝不复用 `Meta.Cache` / `Find`**。
     缓存只可用于"展示用列表"这类允许陈旧的场景。这是**生产隐患**（多实例/切库时缓存快照可能来自别的库），
     不只是测试问题。
   - 测试隔离做法：夹具实例构造函数里把连接串指到**本类专属的随机目录**；
     `DAL.AddConnStr` 发现连接串变化会走内部 `Reset()`（清 `_Db`/`_Tables`/`_hasCheck`）→ 物理切库生效。
     ⚠ **切库后必须复位"建表已完成"标志**，否则第二个类跳过建表 → 空库 `no such table`。
     ⚠ 切库**不解决**缓存串扰，两者要分别修。
   - 排查手段：对同实体同时调 `FindAll()`（直查）与缓存路径，结果不一致即命中此坑。
12. **【建表铁律】插件必须自行建表 —— 宿主/框架的建表不会覆盖到插件实体。**
   - `EntityFactory.InitConnection` 与宿主 `XCodeConfig.EnsureTablesCreated` **只扫"当前已加载程序集"**，
     插件 DLL 加载晚于宿主建表 → 插件实体**不在其列**。症状：生产库 0 张表 → 全线 `no such table`。
     （MemorySystem 早已踩过并留注释；AgentHub 漏做 → 委派功能全废。）
   - 正确姿势（**不依赖程序集扫描**；本仓实采形状，参考 `Plugins/FileTools/Data/FileToolsTables.cs` / `AgentHubTables.cs`）：
     ```
     EntityFactory.InitConnection(ConnName)   // 全量建表：按实体 Meta 逐张 CreateTable（幂等）
     DAL.Create(ConnName).Db.ServerVersion    // 探活确认库可开（⚠ 禁用 dal.Session.Query("SELECT 1")，
                                              //   库文件尚未创建时它会抛 NullReferenceException）
     ```
     ⚠ **`TableItem.Create(typeof(X)).DataTable` + `dal.SetTables(IDataTable[])` 这一写法在本仓 0 命中**
     （旧版本文字，历史出处是 XCode 早期手工建表；照抄会编译不过 —— 批次C 实测）。
     时序坑仍在：首个实体在连接未就绪时只建空库不建表，故**必须先 InitConnection 再探活**。
   - 建表真源放 `Data/<PascalCase>Tables.cs`（`public const String ConnName = ...` +
     `EntityTypes` 数组 + `EnsureCreated()` 返回 bool），插件 `IPlugin` 启动时调用并 `XTrace.Log.Warn` 失败，别散落多份。
   - **建表异常绝不静默吞**：吞掉后症状是"部分表存在、某张表神秘缺失"，
     错误信息（`no such table`）与真因（建表抛异常）相距极远，极难定位。
   - 宿主侧还有一处必登记：`ForgeSelf.Api/Data/XCodeConfig.cs` 的 `PluginDbs` 加一行（连接名 → 插件 Id），
     库文件才会落到 `数据根/Plugins/{插件Id}/{连接名}.db`；**插件内禁止自注册 `DAL.AddConnStr`**。
12b. **【插件登记三处必齐铁律】新插件除 `PluginDbs` 外，还必须登记 `ForgeSelf.Api.csproj` 与（有单测时）`ForgeSelf.Api.Tests.csproj`，判据只看宿主产物**（2026-10-05 PILOT-033 A2→A3 实证）。
   - **三处登记**：① `ForgeSelf.Api/Data/XCodeConfig.cs` → `PluginDbs[<ConnName>] = <插件Id>`（建库建表，见铁律 12）；
     ② `ForgeSelf.Api/ForgeSelf.Api.csproj` 的插件 `ItemGroup` → `<ProjectReference Include="..\Plugins\<X>\<X>.csproj" ReferenceOutputAssembly="false" />`
     （**只建构建顺序**；缺它 ⇒ `dotnet build ForgeSelf.Api` 根本不编译该插件，`StageAllPlugins` 拷不到 DLL，
     `publish`/CI 包里 `Plugins/<X>/` **只有 plugin.json 没有 DLL** ⇒ 运行实例里插件永不出现。`:127` DesignSystem 注释记录的 MSB3030 同一病）；
     ③ 插件业务要写单测时，`ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj` → `<ProjectReference Include="..\Plugins\<X>\<X>.csproj" />`
     （缺它 ⇒ 测试编译报 **CS0234 命名空间…中不存在类型或命名空间名**；插件业务测试的目录约定 = `ForgeSelf.Api.Tests/Plugins/<X>Tests/`）。
   - **判据必须是宿主产物，不是插件目录自建**：`cd ForgeSelf.Api && dotnet build` 后核对
     `ForgeSelf.Api/bin/Debug/net10.0-windows/Plugins/<X>/<X>.dll` 存在，且与 `Plugins/<X>/bin/Debug/net10.0/<X>.dll` **md5 相同**
     （或按下方「查 DLL 字符串」条用 `scripts/probe-dll-string.cjs` 确认本次新增类型 FOUND）。
     ⚠ **`dotnet build Plugins/<X>/<X>.csproj` 单编插件不算数**——它证明的是插件自身可编译，证明不了它进了宿主构建图；
     A2 当时正是拿这条报了「骨架完成」，缺陷一直到 A3 才暴露。
   - **为何不能直接引用宿主**：插件 csproj 一律只 `ProjectReference` `ForgeSelf.Core` + `ForgeSelf.Abstractions`
     （实测 19/19），宿主 `ForgeSelf.Api` 对插件是 `ReferenceOutputAssembly="false"`——注释原文「避免插件类型进入默认 ALC
     造成与 PluginLoadContext 的双重加载」。⇒ 插件要读宿主实体（`ChatTurn`/`SessionEventEntity` 等，都在 `ForgeSelf.Api/Entities/`）
     **只能经 Abstractions 契约 + 宿主实现 + DI**（同构先例：`IUsageStatsService` @ `Abstractions` →
     `AppBuilder.cs` 注册 → `Plugins/DevTools`/`MemorySystem`/`ScriptRunner` 消费）。插件侧**零编译宿主类型**。
   - **net10.0 vs net10.0-windows**：插件是 `net10.0`、宿主与测试工程是 `net10.0-windows`，构建日志会出 **MSB3271** 兼容关系告警。
     这是既有形态、非阻断；判据看 `error` 计数与测试是否真执行，别为这条告警改 TFM。

13. **【版本展示铁律】每个插件根视图必须在标题旁展示自身当前版本号。**
   - 根视图 header 标题旁加版本徽标（如 `v2.0.0`），数据从宿主 `GET /api/plugin` 解包
     `.data` 后按 `id` 过滤本插件取得（注意响应是 `{data:[...], code, message, success}`
     包装，不是裸数组）。
   - 用途：用户/走查时一眼确认「当前跑的是哪版」，避免「改了代码但浏览器跑旧 bundle」
     的版本错位误判。
   - 样式：小字号圆角灰底徽标（`--el-fill-color-light` + `--el-text-color-secondary`），
     不抢标题视觉。
   - 参考实现：`Plugins/ImGateway/web/src/ImGatewayView.vue`（`.ig-version` 徽标 +
     `http.ts` 的 `fetchPluginVersion`）。

14. **【长连接自恢复铁律】插件自带的长连接/后台任务必须自管生命周期，禁止依赖宿主级 HostedService。**
   - 宿主级 `IHostedService` 的 `StartAsync` 只在宿主进程启动时跑一次——插件热重载（销毁→重建）时
     新插件实例注册的 HostedService **不会重启**，旧实例的 `_onMessage`/`CancellationToken`
     不随新实例重置 → 新 Manager 永远建不了连（实测 im-gateway v2.0.0 踩坑：热重载后
     状态恒 disconnected，手动重连也无效，只能靠重启宿主恢复）。
   - 正确姿势：插件自己的管理器在**构造函数**里自初始化（new CancellationTokenSource +
     注册回调 + 拉起连接），`Apply()` 幂等校正；插件销毁时 `StopAll()` 停连接 + 取消 CTS。
     不依赖宿主级 HostedService 的 StartAsync 来"踢一脚"。
   - 反例（禁止）：把长连接启动挂在宿主级 `IHostedService.StartAsync`，插件热重载后新实例
     等不到第二次 StartAsync，连接永远起不来。
   - 参考实现：`Plugins/ImGateway/Services/ImGatewayConnectionManager.cs`（构造函数调 EnsureStarted）。

15. **【宿主前端 API 层铁律】宿主 service 的 `parseResponse` 已解包 `json.data`**：新增 fetch 函数直接按目标类型 T 收（`parseResponse<T>(resp)` 返回 T 本身），**禁止再取 `.data`/`.stats`**——会得到 undefined、页面无错但功能静默失效（fetchPluginVersion / fetchTraffic 各踩一次）。宿主内嵌组件型插件（无独立 web/）的 service 层同样适用。
16. **【发布铁律】发布 = 打 tag 自动发布 + 页面自动更新（2026-09-27 新规范）；禁止手工 Copy-Item 发布产物；禁止 agent 停/启/杀用户宿主进程。**
   - 改完插件/宿主 = 代码 + 门禁 + 插件层 e2e + **发布（打 tag → CI 自动打包 GitHub Release，或本地 `release-local.ps1 -UpdateDir` + 页面本地目录更新源）** + 隔离实例走查 + **运行实例只读复验（用户启用新版本后）**，五步缺一不算完成。
   - **宿主由 update-agent 自更新（spec 036）**：用户/页面点「检查更新 → 下载 → 重启并更新」，全程无人停宿主。
     任何情况下 **agent 不得 Stop-Process 用户运行中的 ForgeSelf**（旧规范 run-plugin-publish-verify.ps1 会杀掉非 publish 实例，2026-09-27 已废除该行为，见 plugin-publish-verify）。
   - 宿主后端 Controller 改完**必须先 `dotnet build` 门禁**（直接复制旧 Release DLL → 新端点 404，流量统计接口实测）；发布产物由 CI/打包脚本全量重建，**不要手工复制 DLL**。
   - 插件含独立引擎等额外产物时，纳入打包脚本参数（如 `-Engine MyPlugin.Engine`）或写入插件文档的发布说明；**一律禁止手工 Copy-Item 发布产物**。
   - 开发期验证走 e2e 隔离实例（globalSetup 自动构建，不碰用户运行实例）；确需在运行实例上版本化侧载插件（`POST /api/plugin/update/{id}`，宿主不重启）必须先获用户同意。

17. **【管理面鉴权铁律】插件暴露的管理/CRUD/配置 HTTP 控制器必须类级 `[Authorize("ApiKeyPolicy")]`。**
   - 宿主**没有全局鉴权中间件**，鉴权是**逐控制器显式**的（对照 `Controllers/AIProviderController.cs:21`）。
     插件控制器经插件子 provider 注册进宿主 Kestrel，**不会自动被保护**——裸 curl 无 token 也能 200。
   - 反例（mcp-center v2.1.0 踩坑）：`api/mcp-center/servers`、`api/mcp-center/config`、`api/mcp` 三个
     管理控制器未加特性，无 token 直接可读外部服务器清单与网关配置；已加类级
     `[Authorize("ApiKeyPolicy")]` 修复（回归测试：`McpAdminAuthTests` 反射断言每个管理控制器都带该策略）。
   - 对外服务端口（如 MCP/网关端口）的令牌是**另一层**安全，不替代管理面鉴权；两层都要有。
   - 验收时必查：插件全部 `Controllers/` 类是否带 `[Authorize("ApiKeyPolicy")]`（见 §五 验收标准 3.2）。

18. **【网关/工具类插件铁律】对外暴露「万能工具/统一网关」时，必须让外部调用方「能发现工具」。**
   - 至少提供**工具枚举能力**（如 `list_tools`：返回全部已注册工具的名称/说明/参数 schema，支持关键字过滤），
     并写进万能工具 description，引导先枚举再按名调用。
   - 万能工具 description 必须包含：**入参格式示例**、**常规能力分类**（读写文件/执行命令/搜索/计算/系统监控等
     具体工具名）、**如何发现更多工具**（list_tools 入口）、外部工具命名空间（如 `mcp.<服务器id>.<工具名>`）。
   - 外部调用方依据说明就能知道有哪些能力、怎么传参，而不是黑盒试探。
   - 参考实现：mcp-center `Services/ListToolsToolFunction.cs` + `UniversalToolForwarder.ToolDefinitionJson`。

19. **【菜单/路由贡献一致性铁律】**
    ① 插件面向用户的界面入口**只许在 plugin.json frontend（menu/route/entry）声明一处**，宿主机械派生菜单与路由；
    ② 后端 IMenuExtension 仅用于无自带界面插件的后端菜单贡献，其 Path 必须等于真实可导航目标，且与 manifest 声明不得并存冲突；
    ③ 任何插件 route 改名/增删必须同步更新 e2e/menu-route-consistency.spec.ts（§四门禁清单加一条）；
    ④ 纯后端无界面插件不得声明界面菜单（撤销或待界面立项后恢复）。
20. **【新建带界面插件·lockfile 铁律】`Plugins/<X>/web/` 必须带 `pnpm-lock.yaml` + `pnpm-workspace.yaml`，否则发布链在 CI 红。**
    `scripts/release/build-frontend.ps1` 对每个 `Plugins/*/web`（glob 自动发现，无需登记）跑
    `pnpm install --frozen-lockfile && pnpm build`（`:39/:41`）——**没有 lockfile 时 `--frozen-lockfile` 直接失败**，
    红在 frontend 段（本地出树构建能过 ⇒ 极易漏检，2026-10-06 ToolBridge 实证）。
    最小可用组合（照抄 `Plugins/QuickLinks/web/`）：`package.json` devDependencies 只放
    `@vitejs/plugin-vue` + `vite`；`pnpm-workspace.yaml` 写 `allowBuilds.esbuild: true` 与
    `onlyBuiltDependencies: [esbuild]`（缺前者 esbuild postinstall 被跳过，vite 起不来）。
    本机装 lock：`cd Plugins/<X>/web && TEMP=<repo>\.temp\tmp pnpm install --offline`
    （store 已有同版本包时离线即可生成 lock；直连 registry 会被本机代理 TLS 证书拦）。
    **交付前按 CI 同参数复现一次**：`pnpm install --frozen-lockfile && pnpm build`。

21. **【输入形状矩阵铁律】凡"解析用户/外部文本"的功能，用例必须由**真实外部产物**驱动，且每种形状 × 「有无代码围栏」× 「裸发（无围栏）」各一条。**
    实证（2026-10-06 ToolBridge，被用户当场抓住）：单测 **140/140 绿**，用户把网页 AI 的回复粘进界面点「解析」却报
    "这段里没认出工具调用"。真因＝`CallParser` 只在 ``` 围栏**内部**试 JSON（`ParseFence`），正文区只走标签式/key=value；
    而**从网页聊天复制代码块常常只带内容不带围栏**——这是该功能最常见的输入形态。
    我写的用例全部自带围栏 ⇒ 覆盖的是我设想过形状，不是真实形状；"绿"因此毫无意义。
    **机制化三条**（写这类功能时逐条落地，不许凭感觉）：
    ① **拿到真实样例才收口**：spec 里标 `Unknown` 的输入样例（本例 U-1）属**阻塞项**，不是"备注"——
       没有真样例就先别报"解析已完善"，要么向用户要一段原文，要么自己按最可能的形态补形状；
    ② **围栏双向成对**：每种结构格式必须同时有"带 ``` 围栏"和"**裸发**（前后带散文）"两条用例；
       裸发还要补一条**反向护栏**（散文中像结构却不是调用的内容，如工具目录 JSON，不得被硬掰成调用）；
    ③ **界面空态文案要能自证成因**：报"没认出"时必须把解析器给的 `reason` 原文显示出来
       （本例已具备，正因如此才定位到"未解析"而非"识别失败"）。
    同场加一条环境教训（AGENTS §5.0 的又一实例）：`dotnet test` 的 TEMP 重定向**必须在 pwsh 内部赋值**
    （`pwsh -Command "$env:TEMP='<repo>\.temp\tmp'; dotnet test …"`），在 Git-Bash 里 `export TEMP` **不会**传进
    testhost ⇒ `ExecutorTests` 7 条 `UnauthorizedAccessException: …AppData\Local\Temp…is denied` 假红。
    红先怀疑环境，但**必须用"同命令重跑 + 改赋值姿势"对照**才能定责，别拿"环境问题"当结论。

22. **【交付动线成本铁律】每次"发布/更新/新界面"交付，必须走一遍"用户从收到我的消息到真正用上"的动线，并把步骤数与不可达入口写进证据；说"入口在 X"必须有真实页面截图为证。**
    实证（2026-10-07 ToolBridge）：我把"更新插件"讲成"设置·插件管理 → 检查更新 → 更新"，用户回
    「没有看见你说的插件管理哪里有更新操作哦 / 你访问宿主看看」——真访问宿主才知：更新页确实存在（`/plugins/updates`，
    实测显示「发现 1 个可更新插件 · 工具桥 v1.0.2 → v1.0.3」），**但没有任何导航通向它**，只能手输地址；
    而插件市场 `onMounted` 不调 `checkForUpdates()` ⇒ 「可更新」角标恒空。我先前那句路径是**照后端能力/路由表反推 UI** 得来的，
    与"假能力"同一类错误（有端点 ≠ 用户可用），只是这次假的是入口而不是数据。
    **三条机械动作**：
    ① **动线走查归到 §四 第④步「走查」里做**（不新增第五步）：从"产物落位"开始，按用户视角一路点到生效，
       逐步记录（点哪、看到什么、是否需要手输 URL/是否需要重启）；**出现"必须手输 URL""找不到入口"即记 P1 UX 债**，
       不许以"功能已可用"交付。
    ② **UI 断言必须有截图证据**：入口路径、按钮文案、角标数字，一律以 `screenshots/live-<端口>/` 或 e2e 截图为准；
       没走到过就不要写"在 X 处点 Y"。（本条与 `design-system-verify` 的"假能力自查表"同源。）
    ③ **动线成本写进 05-evidence**：新增一行「用户动线成本 = N 步 / 不可达入口 M 个 / 需重启?」，
       与"门禁档位"并列；M>0 时任务状态不得写 COMPLETED（最多 PARTIALLY_COMPLETED）。

23. **【真源优先问答铁律】插件的事实型问题（谁生效 / 放在哪 / 从哪进 / 会不会重启 / 这字段什么含义）一律先走 `AGENTS.md` §2.5 五步**：
    查 `docs/README.md` §一「30 秒定位速查」→ 判精度 → 读码或走现场 → **回写同一份真源** → 带出处回答。
    插件域最容易触发这条：两路插件根、`versions/<current>`、顶层扁平清单、热切换口子这些事实散在宿主代码里，
    而 `packaging-upgrade-backup.md` §1.6 当年只有一句"同 Id 由版本号裁决"——精度不足、又没条文要求读完必须回写，
    结果同一个问题 2026-10-07 被问了三次、我读了三遍码。
    **两条判据**：① 文档里有句子 ≠ 够答——答不出"比的是哪个字段 / 哪段代码 / 界面上点哪"就是**精度不足**，按无真源处理；
    ② **读完代码只回答、不回写 = 该问题仍未解决**（回写属免闸门1 的文档动作，写进被问的那份真源，别在第二处复制一份）。

20. **【一键起环境铁律·禁止手敲运行命令】本地起前后端**只能**用 `scripts/dev-stack.ps1`，
    **禁止**手工敲 `dotnet <宿主dll> ...` / `node .../vite ...` / `npx vite` 之类的运行命令。
    - **为什么**：手工启动有两个已实证的**静默陷阱**，都不报错、都让人误判为「环境没起来」：
      ① **dev 宿主首参必须是 `--console`** —— `Program.Main` 只看 `args[0]` 分支（`Program.cs:86`），
      首参是别的（如 `--instance-id=`）会落到 `Program.cs:146` 兜底 `new WindowsService().Main(args)`，
      NewLife.Agent 把它当命令解析，日志只留 `ProcessCommand` / `ProcessFinished` 就 exit 0，
      **无 error、无端口**，表现为「探活一直 000 而日志干干净净」；
      ② **vite dev 的 optimizeDeps 会让 esbuild（Go 二进制）写 `node_modules/.vite/deps_temp_*`**，
      在沙箱里报 `Failed to write to output file: ... Access is denied` 并崩掉 dev server ——
      与目录权限无关（实测 `os.tmpdir()` 同样失败），但**同目录用 Node fs 写是成功的**。
    - **脚本已内置规避**：`--console` 恒在 `args[0]`；起前端前先跑 `scripts/probe-esbuild-write.mjs`
      探测 esbuild 写盘能力，不可写时生成的配置里关掉预打包（`optimizeDeps.noDiscovery`）——
      实测裸导入 `vue` / `pinia` / `element-plus` 仍被正确重写到 `.pnpm` 下的 ESM 文件，功能不受影响。
    - **端口被占用自动顺延**（+1 重试，最多 50 次），实际端口与令牌写入 `.temp/dev-stack.json`；
      宿主以 `FORGESelf_INSTANCE_ID=dev-stack` 隔离，**不影响用户正在运行的宿主实例**。
    - 用法：`pwsh scripts/dev-stack.ps1`（默认后端 7301 / 前端 7399）、`-SkipBuild` 跳过 `dotnet build`、
      `-BackendPort` / `-FrontendPort` / `-PluginsDir` / `-DataRoot` 覆盖、`-Stop` 停止并清理。
    - 同一铁律适用于插件前端：要真 HMR 用 `scripts/dev-plugin-web.ps1`，同样不许手敲 vite。

---

## 三、新建 / 迁移插件：步骤

### 3.1 后端骨架

> ⚠ **插件源码在仓库根 `Plugins/<PascalCase>/`**（不是 `ForgeSelf.Api/Plugins/`——那是宿主的插件**装载器运行时代码**目录，两者易混淆）。

```
Plugins/<PascalCase>/
├── plugin.json                        # 清单（Id/Version/EntryAssembly/EntryType/frontend）
├── <PascalCase>Plugin.cs              # 实现 IPlugin
├── Controllers/                       # [Route("api/<小写控制器名>")] 或自定义前缀
├── Services/
├── Data/                              # ★ 数据层（有实体的插件必备）
│   ├── Model.xml                      #   列/索引/默认值真源 → xcode Model.xml 生成实体
│   ├── <PascalCase>Tables.cs          #   建表唯一真源（插件必须自行建表，见铁律 12）
│   └── Entities/
│       ├── <Name>.cs                  #   xcode 生成（会被覆写）
│       └── <Name>.Biz.cs              #   人工维护（Valid/自定义查询/业务方法）
└── web/                               # 自带界面（可选，见 3.2）
```

> ⚠ **插件实体所在目录是 `Data/Entities/`（不是 `Entities/`）**，且**实体必须走
> `Data/Model.xml` → `xcode Model.xml` 生成**，业务逻辑写 `.Biz.cs`（见铁律 9）。
> 新建插件时先把 `Model.xml` 建好再生成，不要手写实体 `.cs`。

`plugin.json` 关键字段（以 AIAgent 为准）：

```json
{
  "Id": "quick-links",
  "Name": "快捷链接插件",
  "Version": "1.0.1",
  "EntryAssembly": "QuickLinks.dll",
  "EntryType": "ForgeSelf.Api.Plugins.QuickLinks.QuickLinksPlugin",
  "frontend": {
    "views": ["QuickLinksView"],
    "menu": "快捷链接",
    "route": "/quick-links",
    "icon": "fa-link",
    "entry": "web/dist/index.js"
  }
}
```

- 有 `entry` → 宿主**远程加载**插件产物（推荐，插件自治）
- 无 `entry` → 回退 `dynamicPlugins.ts` 里硬编码的主包组件映射（**存量兼容，新插件不要用**）

### 3.2 自带界面 `web/`

> ⚠ **别手写 `web/`**（2026-10-06 实测）：手写 vite/pnpm 配置会同时踩「pnpm 11 构建白名单」与
> 「vite 必须 lib 模式」两个坑，构建直接失败。**先跑下面的脚手架**，再改组件。
> 两个坑的症状与修法见 `plugin-frontend-scaffold`「为什么不许手写 web/」。

```powershell
pwsh .agents/skills/plugin-frontend-scaffold/scripts/scaffold-plugin-frontend.ps1 -Plugin <PascalCase>
cd Plugins/<PascalCase>/web && pnpm i && pnpm run build
```

> **沙箱内构建兜底（2026-09-22 实证）**：本环境插件目录内 `pnpm i && pnpm run build` 跑不通——
> 插件 `web/node_modules` 是残缺副本（`vite` 包缺 package.json/bin），而 `pnpm install` 重建 node_modules
> 会被沙箱 safe-delete 拦截器拦下（`SAFE_DELETE_BULK_CONFIRM_REQUIRED`）。**改用宿主树内出树构建**：
> 把插件 `web/src` + `vite.config.ts` 复制到 `ForgeSelf.Web/.plugin-build-<id>/`，以包装配置
> （`{ ...base, publicDir: false }`）驱动宿主 vite：
> `node ForgeSelf.Web/node_modules/vite/bin/vite.js build --config <临时目录>/vite.wrapper.config.ts --outDir <插件>/web/dist --emptyOutDir`
> **`publicDir: false` 必给**（否则宿主 `public/` 的 favicon/logo/shared 会被误拷进插件产物），
> `--outDir` 必须显式绝对路径（`outDir` 相对 cwd 而非 config 位置）。产物只应是 `index.js` + `style.css`。

产物契约（错一个就加载不出来）：

- 出口固定 `web/dist/index.js` + `web/dist/style.css`（`assetFileNames: 'style[extname]'`）
- **导出的名字必须等于 `plugin.json` 的 `views[0]`**：`export { QuickLinksView }`（另带 default 兜底）
- `vue` / `vue-router` / `pinia` / `element-plus` 全部 external，由宿主 import map 解析到同一份实例

界面交互约定（2026-09-22 用户反馈沉淀）：

- 凡「点一下就改变用户可见状态」的操作（归档 / 删除 / 取消 / 发布 / 批量改…）**必须二次确认**，
  用 `ElMessageBox.confirm`（宿主已全局引入 `el-message-box.css`，插件无需另引样式）。
  软标记类动作（如归档）文案必须写明「数据保留 + 在哪可撤销」，别让用户误以为是删除。
- 确认逻辑写成**可单测的纯编排函数**：确认动作由调用方注入（范例 `AIAgent/web/src/sessionArchive.ts`，
  确认在 `AiAgentView.confirmArchive`），这样「用户取消 → 一个请求都不发」这条关键语义能被 vitest
  直接锁死，且测试不依赖 element-plus 弹窗；e2e 侧按 `e2e-testing`「交互确认类用例」覆盖取消 + 确认两条路径。

迁移宿主页面到插件时的固定改写（照做，别想当然）：

| 宿主写法 | 插件里改成 |
|---|---|
| `@/types/x`、`@/stores/x`、`@/components/x` | 相对路径 `./types`、`../store`，或 vendoring 一份纯逻辑层 |
| `<AppLogo />`（宿主专有组件） | 内联 `<span class="app-logo"><img :src="logoUrl" /></span>` |
| `class="flex items-center gap-2"`（Tailwind） | **可直接用**（插件自备 `tailwind.css` 入口，构建进 `style.css`） |
| `<ElButton>` 等 EP 组件 | **可直接用**（须 external + 宿主 `exposeSharedDeps` 暴露；否则退回原生 HTML + `--el-*`） |
| `useRouter()` / 直接 `router.push` | 走导航桥（见铁律 5），或 `inject('forgeOpenPage')` |
| `<img src="/logo/logo-128.png">` | `:src="logoUrl"`（**静态绝对路径会被 Vite 当资源解析导致构建失败**） |
| 用宿主的 `@/services/request` | 用插件自带的 `src/http.ts`（从 `localStorage['forge_api_token']` 取 token，直连后端） |

需要用 Pinia 时：宿主已装好 Pinia，插件直接 `defineStore` 即可；
只有 `index.ts` 里的**独立预览挂载**才需要自己 `createPinia()`。

### 3.3 宿主侧收尾（迁移场景）

1. 删宿主实现：`src/views/<X>View.vue`、`src/stores/`、`src/services/`、`src/types/`、`src/components/<x>/`
   （按项目规矩移到 `.trash/`，别 `rm`）
2. `src/router/dynamicPlugins.ts`：删掉对应的 `case '<X>View':`
3. `src/router/index.ts`：删静态路由 + 顶层 `import`
4. `components.d.ts`：删掉自动生成的对应声明行（否则 `vue-tsc` 指向已删文件报错）
5. `src/data/features.ts`：保留条目，补一句「视图已迁移到插件」注释
6. 单测 fixture 里若还写着该 view 且**没有 entry**，补上 `entry: 'web/dist/index.js'`
   （否则会走进已删除的 switch 分支 → 路由注册不上 → 测试红）

---

### 3.4 交互设计统一要求（设计 → 验证闭环，2026-09-23 用户要求）

**设计阶段（改 UI/交互前必须做交互设计，并写入功能设计文档，不只"功能跑通"）：**
1. **状态与持久化一致**：点即保存（添加/删除/编辑自动落盘），各存各的部分更新（接口按传参部分更新，如 `{proxy?} / {rules?}` 分传，谁变了传谁）——界面状态与持久化不一致 = 交互缺陷（曾因「添加规则只改内存表单、需再点保存配置」被用户点名批评）。
2. **操作成败可见**：启动/停止/保存等操作必须有明确反馈——成功提示 / 失败留窗打印原因并提示处理方式（如「需管理员权限，请以管理员重启」）。禁止"点一下闪一下无感知"（成功与否都不知道）。
3. **防闪**：轮询/自动刷新时内容未变不赋值（先比较再赋值，如 `JSON.stringify` 相同则跳过），避免整块重渲染闪动。
4. **空态分级**：区分不同空态并给引导文案（「引擎未运行：启动后开始记录」/「筛选无匹配」/「暂无数据（触发条件）」），不能一律"暂无数据"。
5. **边界设计**：筛选 + 分页（筛选变化回第 1 页、数据更新后越界自动回退最后一页、计数「筛选 N/总数」、每页条数可调）；数据上限说明（如「内存态上限 1000 条」）；长文本溢出（title 提示）；窄屏不破版。
6. **破坏性/状态变更操作二次确认**：见 §3.2（`ElMessageBox.confirm` + 可单测的确认编排函数）。
7. **版本展示**：根视图标题旁版本徽标（铁律 13）。

**验证阶段（走查必须按设计验证，不只看"能显示"）：**
1. 走查前先读功能设计文档中的交互设计清单，逐项核对。
2. 交互验证项：点即保存是否落盘（刷新/重进仍在）？操作失败是否留痕可查？轮询 12s+ 观察无闪动？空态分级各态文案正确？筛选/分页边界（筛选回第 1 页、越界回退、计数）？长进程名溢出？
3. e2e 按 `e2e-testing` 技能覆盖交互路径（确认取消两条路、空态、分页边界）；纯视觉用截图读图对照设计。

---

## 四、维护闭环（改完插件必走）

```
读技能 → 改代码（实体改动走 Model.xml→xcode）→ 门禁 → 插件层 e2e →（可选：本地预览给用户先体验，见下 👀；用户表露意图时必做）→ 发布（打 tag 自动发布 / 本地目录更新源 + 页面自动更新）→ 走查（e2e 隔离实例）→ **运行实例只读复验（用户启用新版本后；见 `plugin-publish-verify`「运行实例只读复验」）** → 更新插件文档 → 记日志
```

> ⚡ **开发态快速回路（2026-10-01 新增，仅 dev 宿主）**：本地迭代阶段不必走完整发布链——
> 以 `FORGESELF_DEV_MODE=1 --plugins-dir <repo>/Plugins` 起 dev 宿主后：
> - 改插件 C#：`dotnet build Plugins/<X>` → `pwsh scripts/dev-plugin.ps1 -Plugin <X>`
>   （shadow-copy 同版本热重载，秒级生效、无需 bump plugin.json 版本；`-All` 全量）；
> - 改插件 UI：`pnpm run build`（或 watch 构建）→ 刷新页面即生效（内容指纹破缓存 + dev no-store）；
> - 诊断：`GET /api/dev/diagnostics`（插件状态/最近错误/shadow 统计/日志尾，日志带 `[plugin:<id>]` 前缀）。
> 完整五步闭环仍是**交付门禁**，快速回路只替代其中的"本地看效果"环节。

> 👀 **给用户先体验一遍（本地预览，不发布）** —— **触发条件**：用户表露"我看看效果 / 先别发布 / 本地跑一下 / 传统起前后端"
> 这类意图时，**直接按下面走**，不要继续往下发布、也不要拿 e2e 截图代替真人体验、更不要反问用户"要不要跑 e2e"。
> 预览只是收反馈的环节，**不替代 §四 的任何一步**（预览完仍要 ③发布 ④走查 ⑤运行实例只读复验，且发布需授权）。
>
> 1. **先看端口，绝不碰用户实例**：`netstat -ano | grep -E "7102|7002"`。被占就换备用端口起；
>    `:51888` 与 `D:\src\tools\ForgeSelf` 是**用户的运行实例，不停不杀不动配置**。
> 2. **起后端**：`cd ForgeSelf.Api && dotnet run`（端口真源与"勿用 `--urls`"见
>    `docs/04-standards/agent-workflow.md` §分层/鉴权/配置；改端口三选一：`FORGESELF_PORT` > `--server-port` > 改 config）。
>    要边改边看就用上面 ⚡ 的 `FORGESELF_DEV_MODE=1 --plugins-dir <repo>/Plugins` + `scripts/dev-plugin.ps1`。
> 3. **起前端**：`cd ForgeSelf.Web && pnpm run dev`（7002）。**后端换了端口就必须同时设**
>    `VITE_APP_BASE_API=http://localhost:<后端端口>` —— dev 代理的 `/api` 与 `/plugins/**/web/**`
>    默认打 7102（`ForgeSelf.Web/vite.config.ts:37-54`），不改会出现"页面开得了、接口全 404"。
> 4. **交地址 + 交动线**：给用户 `http://localhost:7002<plugin.json.frontend.route>`，并说清**点几下、入口在哪**
>    （侧栏菜单有没有项 / 只能直连 URL）、以及"这一步会真的写库"。动线说不清就是交付缺陷，不是小事。
>
> **四条必知的坑（2026-10-07 todo-tracker 预览实测）**
> - **插件 UI 是远程加载的** ⇒ 预览前必须已 `dotnet build`（把 `plugin.json` + `web/dist/*` 落进
>   `ForgeSelf.Api/bin/Debug/net10.0-windows/Plugins/<Id>/`）且插件前端 `pnpm build` 过。缺产物时页面落在
>   `.plugin-view-state--error`；全新 worktree 里 `Plugins/*/web/dist` **一个都没有**（gitignored，见 `e2e-testing` §失败排查）。
> - **鉴权**：插件管理面 API 带 `[Authorize("ApiKeyPolicy")]` ⇒ 浏览器要已有 `forge_api_token`。
>   前端 `services/authInit.ts` 首启会自动打 `GET /api/init-token` 落 localStorage，正常不用管；
>   若页面空态且 network 里是 401，先直接开 `http://localhost:<后端端口>/api/init-token` 或清 localStorage 刷新。
> - **数据不互通**：dev 数据根 = 程序目录 `Data/`，发布/服务态 = `~/.forgeself`（真源 agent-workflow §运行时数据落盘铁律）
>   ⇒ 预览实例是**空台账**，用户看不到自己现有数据。**起预览前就要一句话说明**，否则会被当成"数据丢了"。
>   要让用户看真实数据只有一条路：发布 + 用户自己在真实实例启用新版本。
> - **宿主启动会写回 `ForgeSetting.config`**（端口事故在册）⇒ 起完核对端口/配置未被改写；并行会话共用机器时
>    优先 `FORGESELF_PORT` 起备用端口，别抢默认 7102。
>
> **2026-10-07 现踩现记的三条（预览专属，代价最高的一类）**
> - **必须隔离数据根**：起预览就带 `FORGESELF_DATA_ROOT=<repo>\.temp\preview-data`。
>   `dotnet run` 的 Development 判定来自 launchSettings，一旦绕开它（`--no-launch-profile`）或环境不匹配，
>   dev 实例会把数据根解析成 **`~/.forgeself` = 与用户长期实例共用一份库和配置**。
>   实测事故：预览实例对真实 `TodoTracker.db` 做了建表加列，并把 `ForgeSetting.config` 的 `PortNumber` 改成 7102
>   （用户实例重启就会跑到 7102）。恢复三步：备份到 `.trash/` → 只改回目标值 → `Compare-Object` 逐行 diff 核对
>   "只有那一行变了、行数与 CRLF 未变"。
> - **同机已有实例 ⇒ 用环境变量换实例标识，别用命令行参数**：全局 Mutex 会拒启动
>   （`错误：另一个实例已在运行，请勿重复启动。`）。正确写法是 env `FORGESelf_INSTANCE_ID=preview`
>   （**大小写就是这样**）；`--instance-id=preview` 当 CLI 参数传会被 NewLife.Agent 当成服务命令
>   （日志 `ProcessCommand cmd=--instance-id=preview` → `ProcessFinished`）然后**进程直接退出**——
>   与"`--urls` 被吞"是同一类坑。
> - **别替用户点 `init-token`**：`GET /api/api-server/init-token` 只在**首次**有效，调用即把 `IsFirstInit` 置 false，
>   之后用户浏览器再拿不到 token（页面 401 空列表）。预览时只验 `200` 的静态资源与 `/api/health`，
>   token 交给前端 `authInit.ts` 自己去取。
> - **起完必查三件**：① 日志里 `运行时数据根目录` 落在隔离目录；② `~/.forgeself/config/*` 的 mtime 没被本次改动；
>   ③ 用户实例端口仍在监听（`netstat`）。收尾时**只停自己起的**进程。
>
> **收尾**：预览进程是 agent 起的 ⇒ 记下 PID，用完**只停自己起的**；用户的实例一律不碰。

> 🚶 **走查（④）不是发布产物的专属仪式，双绿不能替代它**（2026-10-07 todo-tracker 实证）：
> 单测 226/226 + 插件层 e2e 8/8 的同时，用户明确要求的那条"一致的路径认为是同一个项目"在数据层是**坏的**
> （点「关联项目」界面显示成功、路径也归一了，但 `ProjectId=0` ⇒ 项目过滤筛不到、`ListProjects` 计数恒 0、
> 详情面板仍写「未关联项目」）。两道门禁都抓不到的原因是结构性的：
> - **单测绕过了真实入口**：用例直接调 `Resolve(registerIfMissing: true)`，而坏的是服务层 `ApplyProjectFields` 的传参；
>   且断言 `Distinct().HaveCount(1)` 缺阳性对照——**四个 `0` 也算"只有一个不同值"**。
> - **e2e 断言了"回显"而不是"落库的身份"**：只查 `projectRoot` 有显示，没查 `projectId > 0` 与详情面板的关联态。
>
> 所以：**门禁绿之后、发布之前，先在 dev 预览实例用真浏览器走一遍主链路**（按 §3.4 交互清单逐项核对 +
> 截图读图 + 只清自己造的测试数据），这一遍标注为"dev 态预走查"，**不冒充 §四④ 的发布产物版走查**
> （发布后仍要用 `e2e` 隔离实例点一遍，⑤ 运行实例只读复验另有 `plugin-publish-verify`）。
> 写判据时的两条硬规矩：**断言锚在真实服务入口的返回**（不是绕过它直调内层），**"计数/唯一性"断言必配阳性对照**
> （先证"正确值确实可能出现"，再证"只出现一次"）。

0. **改实体时（先做这一步，再改代码）**：按铁律 9 走
   `改 Data/Model.xml → cd Data && xcode Model.xml → 确认无字段漂移`；
   业务方法写进 `.Biz.cs`，**不要碰 `.cs`**。
1. **门禁**
   - 前端：`cd ForgeSelf.Web && pnpm run check && pnpm run test`
   - 插件前端：`cd Plugins/<X>/web && pnpm run build`（沙箱内改用 §3.2 的出树构建兜底）
   - 后端：`cd ForgeSelf.Api && dotnet build`
   - 插件后端测试：`dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~<X>"`
     （涉及插件实体的改动**必须**跑该插件测试，并确认跨插件串扰已排除，见铁律 10/11）
2. **插件层 e2e**：`e2e/plugins/<id>/<id>.spec.ts`，零 mock，按 `e2e-testing` 技能写。
   **不要**写一次性临时脚本代替它。
   菜单/路由一致性回归（铁律 19③）：任何插件 route 改名/增删必须同步 `ForgeSelf.Web/e2e/menu-route-consistency.spec.ts` 并实跑通过。
3. **发布（2026-09-27 新规范：打 tag 自动发布 + 页面自动更新；禁止手动 Copy-Item；禁止停宿主）**
   - **主路径**：向用户请示 git 提交（审批后才 commit）→ 打 tag `v<X.Y.Z>` → `git push github v<X.Y.Z>` →
     CI 自动构建打包并创建 GitHub Release → 通知用户在「设置-版本更新」页点「检查更新 → 下载 → 重启并更新」完成升级
     （宿主 update-agent 自更新，**全程无人停宿主**）。
   - **本地离线**：`pwsh scripts/release/release-local.ps1 -Version v<X.Y.Z> -UpdateDir <目录>` →
     设置页「更新源 = 本地目录」填该目录 → 页面点「检查更新 → 下载 → 重启并更新」。
   - **禁止**：手动 `Copy-Item` / `dotnet publish` 散拷产物进 `publish/`（含 DLL、web/dist、引擎 exe）；
     **禁止 `Stop-Process` 用户运行中的 ForgeSelf**（旧 run-plugin-publish-verify.ps1 的「杀非 publish 实例」行为已废除）。
   - **可选侧载（仅插件 DLL，且须用户同意）**：`run-plugin-publish-verify.ps1 -Plugin <PascalCase目录>` 版本化侧载
     （`POST /api/plugin/update/{id}`，宿主不重启；见 plugin-publish-verify）。
   - 宿主内嵌组件型插件（前端在宿主 `src/`，无独立 web/）的前端产物 = 宿主前端构建（`pnpm run build` → wwwroot），
     随 tag 发布由 CI `build-frontend.ps1` 全量重建，不散拷文件。
   - 详见 `plugin-publish-verify`
   - **发布成功判据（铁律）**：GitHub Release 资产可下载且与 `SHA256SUMS.txt` 一致（或本地目录 zip 可被页面
     检查出新版本）；CI 失败 / 资产缺失 = 发布未完成，禁止宣称成功。
4. **浏览器走查**：控制浏览器访问宿主，按用户视角点一遍（含截图读图看图标/间距/对齐/溢出），
   并清掉造的测试数据。**UI/交互有改动时，必须按 §3.4 交互设计统一要求的验证清单逐项核对**
   （点即保存是否落盘 / 操作成败是否可见 / 轮询无闪动 / 空态分级 / 筛选分页边界 / 二次确认）。
5. **验证通过后更新插件文档**：走查确认插件没问题后，**必须**同步文档（代码是事实源）：
   - 功能文档 `docs/02-features/<NNN>-<功能>.md`：新能力/新端点/契约变化（如 status = int 枚举）补齐；
   - 契约变化同步到 `specs/<当前 spec>/contracts/`；
   - 插件**功能行为/契约**有实质变化时，升级插件版本并在文档标注（如 `plugin.json` Version + 文档版本号）。
6. **记日志**：按 `AGENTS.md` §7.5 写当天工作日记 + 更新 `TODO.md`。

---

## 五、插件验收标准（按需加载）

验收插件（发布/走查前，或评审「插件是否完备可提交」）时，**按需读取**
[`references/plugin-acceptance.md`](references/plugin-acceptance.md)。
该文件是插件侧可勾选验收清单，与 AGENTS.md §0 出口清单 / §10 Verification-Centric Completion /
§四 维护闭环 / 本技能铁律 / MEMORY 铁律逐条呼应；管理面鉴权、工具可发现性等
硬性项在本技能铁律 17/18，验收清单里对应勾选项并给出验证动作。

---

## 六、关键事实速查

- **本地起前后端：一律 `pwsh scripts/dev-stack.ps1`（铁律 20），禁止手敲 dotnet / vite 运行命令。**
  默认后端 `7301` / 前端 `7399`，**被占用自动顺延**；实际端口、令牌、PID 在 `.temp/dev-stack.json`；
  停止用 `-Stop`。插件前端要真 HMR 另用 `scripts/dev-plugin-web.ps1`。
- **esbuild 写盘（沙箱）**：`vite build` 不受影响（产物由 rollup/Node 写）；`vite dev` 的 optimizeDeps
  由 esbuild Go 侧写盘 → `Access is denied`，dev-stack.ps1 会自动关预打包绕开（探测脚本
  `scripts/probe-esbuild-write.mjs`）。**别再用手敲 `npx vite` 去"手动修"**。
- 后端默认端口 `7102`，本环境长期运行的 publish 实例用 `51888`
- 插件目录 = `AppContext.BaseDirectory/plugins` → publish 实例即 `publish/plugins`
- **插件数据目录** = `ctx.EnsurePluginDataDirectory()` → `{数据根}/plugins/{插件Id}`（生产即 `~/.forgeself/plugins/{id}`）。
  **随数据走的文件放这里**（库文件/DB、CA 证书、业务配置），**不要放发布目录**（发布会被覆盖、配置会丢）。
  宿主插件在 `IPlugin.Apply(ctx)` 里 `SomeEngine.SetDataDirectory(ctx.EnsurePluginDataDirectory())` 取用；
  独立引擎等子进程由宿主启动时经命令行参数传路径（如 `--config <数据目录>/myplugin.json`），子进程不自行猜路径。
- 活动插件目录**只放插件自己的程序集**（`<Dir>.dll` + `plugin.json` [+ `web/dist`]），
  混入宿主共享 DLL 会让宿主启动即崩；版本化后实际生效在 `versions/<current>/`（根扁平为兼容回退）。目录结构与备份生命周期真源 = `docs/04-standards/packaging-upgrade-backup.md`（目标：去 `_backups`/插件备份）
- **发布/升级（2026-09-27 新规范）**：主路径 = 打 tag `v<X.Y.Z>` → push github → CI 自动打包 GitHub Release →
  页面「检查更新 → 下载 → 重启并更新」（宿主 update-agent 自更新）；本地离线 = `release-local.ps1 -UpdateDir <目录>` +
  设置页「更新源 = 本地目录」。**不再用 run-plugin-publish-verify.ps1 杀非 publish 实例重启宿主**；
  该脚本仅保留「版本化侧载插件（宿主不重启）」的可选用途（须用户同意）。
- ⚠ **宿主无自动热重载（2026-09-24 一刀切，PluginHotReloadWatcher 已删）：插件目录变更不会自动生效**（2026-09-22 mcp-gateway 实证：拷入后轮询 60s 插件列表无变化）。新增插件的两条生效路：① `POST /api/plugin/install` 上传 `.forgeself-plugin` 包触发 `DiscoverPlugins()` 重扫；② **冷启动宿主**（停 → 覆盖 → 起，启动扫描发现，最直接）。运行中更新已加载插件一律走版本化侧载 `POST /api/plugin/update/{id}`（side-by-side，天然绕开 DLL 锁）。
- 端点前缀是**单数** `api/plugin/...`（不是 `api/plugins/`）
- 宿主静态资源真实路径是 `/assets/...`；直接 `curl /index-xxx.js` 会 404，别误判成部署失败
- **查 .NET DLL 里有没有某字符串，别用 `strings`/`grep`**：.NET 元数据字符串是 **UTF-16LE**，
  `strings` 只扫 ASCII 单字节序列 → 必然漏检，会误判「发布产物是旧的」并触发无谓重建。
  正确姿势（Node）：
  ```js
  const b = fs.readFileSync('publish/plugins/X/X.dll')
  b.includes(Buffer.from('目标字符串','utf8')) || b.includes(Buffer.from('目标字符串','utf16le'))
  ```
- **Playwright CLI 在 MSYS/Git-Bash 下不能 `node node_modules/.bin/playwright`**：
  那是 shell 脚本（`basedir=$(dirname ...)`），node 直接解析会报
  `SyntaxError: missing ) after argument list`。正确姿势：`bash node_modules/.bin/playwright test ...`。
- **UI 走查前的环境前置：provider 的模型清单不会自动出现。**
  `/api/ai-models` 读的是 `AIModel` 表（独立实体），**不是** `AIProvider.SupportedModels`——
  即使 provider 配了 endpoint + key，前端仍显示「暂无可用模型」而卡住走查。
  需 `POST /api/ai-providers/{id}/fetch-models` 触发拉取（会真实请求上游 `/v1/models` 并 upsert）。
- **沙箱内起宿主进程跨工具调用会被回收**：`nohup ... &` 后下次工具调用 curl 返回 000。
  浏览器走查必须用 `run_in_background` 长驻宿主，用完 `TaskStop` 收掉；
  纯 API 验证则在同一 bash 调用内「启动 → curl → kill」完成。
- **插件前端出树构建有正规脚本，别手搓 wrapper**：`pwsh scripts/build-plugin-web.ps1 -Plugin <PascalCase>`
  已实现 §3.2 的兜底（复制到 `ForgeSelf.Web/.plugin-build-<id>/` + `publicDir:false` + 绝对 `--outDir` + 用完即删临时目录）。
  手搓的临时配置容易把 `outDir` 相对层级写错（`.plugin-build-*` 在 `ForgeSelf.Web/` 下，回仓库根要 `../../`）。
- **插件 csproj 用 global `Using` 收拢命名空间**：`<Using Include="ForgeSelf.Api.Plugins.<X>.Models" />` +
  `System.Text.Json` / `System.Text.Json.Nodes` / `System.ComponentModel`，省掉每个 Service 文件四行 using；
  漏 `System.ComponentModel` 会在 `Win32Exception`（进程启动失败分支）上编译断。
- **想跨插件共享一个纯函数，先读两份共享层 csproj 再决定**：`ForgeSelf.Core` **零 PackageReference**、
  `ForgeSelf.Abstractions` 只引 DI.Abstractions ⇒ 任何带 `XTrace`/NewLife 类型的代码都上移不了，
  硬上移＝给内核层加依赖＝依赖结构变更（高风险，须出 ADR）。替代方案＝本地实现 + **跨实现对账测试**
  （同一张金样表打两份实现，判定与原因原文逐条比），把"第二份真相"的漂移变成机器判据
  （实证：PILOT-053 的 `ToolBridgeGuardParityTests`）。
- **反向探针必须确认自己改了行为**：把探针代码放在 `return` 之后 = 不可达代码，跑出来照样全绿（2026-10-06 我自己中过一次）。
  探针的判定标准是「**改动前后测试结果变化**」，不是「代码写上了」。
- **`pnpm run check` 不等于"类型全绿"，发布链用的是 `vue-tsc -b`**：宿主 `check` 跑 `vue-tsc --noEmit`，
  而 `pnpm build`（= 发布链 `build-frontend.ps1` 的第一段）跑 **`vue-tsc -b`（build 模式，按 tsconfig references 把 `e2e/**` 也编进来）**。
  实证（2026-10-06 ToolBridge）：`check` 报 0 error、e2e 6 条运行时全绿，但发布链红在
  `e2e/plugins/tool-bridge/tool-bridge.spec.ts error TS2559`（我把字符串当 `toHaveText` 的 options 传，Playwright 运行时容忍、类型不容忍）。
  ⇒ **交付/发布前必须按同参数跑一次 `cd ForgeSelf.Web && npx vue-tsc -b`**；插件 e2e 用例里给断言写"说明文字"时，
  一律用注释而不是第二个参数。
- **本地发布给人装的包必须带 `-Sign`**（AGENTS.md §2.3 定稿）：
  `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts\release\release-local.ps1 -Version <三段号> -Sign -UpdateDir <上级目录>\updates`；
  脚本自己把 `TEMP/TMP` 指进仓库 `.temp`（勿再手工改）。跑完**必须验包内容**而不是看退出码：
  zip 存在 + `SHA256SUMS.txt` 逐字对 + L1/L2/L3 布局不变量 + 新插件真进 `versions/<ver>/plugins/<id>/`（DLL + plugin.json + web/dist）
  + 两个 exe 的 FileVersion 与包名同串 + `signtool verify` 为 Valid（时间戳间歇失败 ⇒ 半签不得交付）。

---

## 七、常见坑（2026-10-06 CostScope 插件全程实测；症状 → 根因 → 修法）

> 这一节只收「**真踩过**」的坑，按主题归类。前端构建两坑详见 `plugin-frontend-scaffold`「为什么不许手写 web/」。

### A. 流程层：最贵的坑

- **手写插件 `web/` 而不走 `plugin-frontend-scaffold`** → 连带踩 pnpm 11 构建白名单 + vite lib 模式两个坑，
  构建直接失败。脚手架是整目录复制模板，模板里这些文件已经是对的。**先跑脚手架，再改组件。**
- **.NET 全绿 ≠ 构建没问题**：本例 `dotnet build`（根 solution）**0 错误**，失败只在另一条构建链（前端）。
  报「构建失败」时**先分清是哪条链**，别一头扎进 .NET。

### B. 测试与守卫

- **静态扫描守卫必须先剥注释再匹配**：文件里为了说明「**为什么**不能出现 X」必然会出现 X 的名字，
  直接 `text.Contains("X")` 必然误报自己。两次踩到（`IHostedService` 守卫、`api/usage` 端点边界守卫）。
  修法：扫前用 `StripComments` 去掉 `//`、`/* */`、`<!-- -->`。
- **目录上溯 `while` 循环必须推进指针**：`while (dir is not null) { if (命中) return …; }` 漏写
  `dir = dir.Parent;` ⇒ **死循环**，xUnit 表现为测试挂住不返回（实测挂 11 分钟才被察觉）。
  写完这类循环立刻检查指针是否推进；xUnit 建议给测试方法加超时。
- **探针判定标准是「改动前后测试结果变化」**，不是「代码写上了」——本技能第六章已强调，此处再确认一次：
  每次写守门测试都要用 MUTATION 探针实测它会变红。

### C. 后端 / 契约

- **「新增防误覆盖」与「改价」必须是两个通道**：一个 `Save` 既当新增又当更新，结果「重复即拒绝」会把改价也堵死
  （FR-3.4 改单价后历史重算直接不可实现）。拆成 `Save`（新增，重复且内容不同 ⇒ 拒绝）+ `Update`（要求已存在，
  **不做 upsert**，否则掩盖「改了个不存在的模型」）。
- **扩契约 DTO 必须同步契约测试里的字段清单**：「字段集一致」那条用例会立刻变红，而它变红是**正确的**
  （提醒你清单要一起改），别误判成回归。
- **服务层校验异常要映射 400，且刻意不捕获其它异常**：`ArgumentException`（含 `ArgumentOutOfRangeException`）
  ⇒ 400 + 明确 message；**其它异常应如实冒泡为 500**，不能伪装成「参数错」（否则数据访问故障会被误诊）。
- **查询类端点非法枚举值必须 400 + 列出可用值**，不得静默返回空表（02-spec Error Handling）。
  注意口径：`null`（未传）可给默认值，但**显式传空串是客户端错误 ⇒ 400**。

### D. XCode（本仓实体层）

- `FindByXxx` **可能不存在**：索引非唯一时 XCode 只生成 `FindAllByXxx`（如 `FindAllByModel`，无 `FindByModel`）。
  用前先 grep 生成的实体，别照抄名字。
- `[InlineData]` **不能写 decimal 常量算术**（CS0182，如 `InPrice * 2`）⇒ 常量算术放方法体外，
  或把测试方法改成普通方法 + 字面量。
- `DAL` 在 **`XCode.DataAccessLayer`** 命名空间。
- **record struct 的 `string` 字段默认是 `null`**（不是 `""`）：`acc.Key.Length` 会 NRE，
  聚合累加时用 `string.IsNullOrEmpty(acc.Key)` 判首次入桶。
- **插件库单测要自己 `DAL.AddConnStr` + 建表**（连不上宿主库）；注意 XCode 首次 `Insert` 会自动建表，
  所以「反射调 `Meta.CreateTable`」其实可能静默没生效——别把它当权威结论。
- **改 `Data/Model.xml` 后要重跑 `xcode`**，且 xcode 会**规范化 xml**（补默认值、去显式默认属性），
  手改的 xml 与生成物会有差异，别以为是别人改的。

### E. 宿主侧现状（不是本仓规范，是既成事实）

- **宿主 `ForgeSelf.Api/Entities/` 下 30 个实体只有生成物、没有 `Model.xml`**（均生成于 2026-09-25）⇒
  铁律 9/11「Model.xml 为真源、禁手改生成物」**在宿主侧从未成立**。
  实测**重建不可行**：按当前列定义重建后逐行 diff 达数百行，且索引名不一致
  （`..._Turn` vs 生成的 `..._TurnIndex`）会触发 XCode `CheckDeleteIndex` **删旧建新**，
  生成的类还会丢掉 `: IChatTurnModel, IEntity<IChatTurnModel>`（接口生成配置未复现）⇒ 重新生成会编译失败。
  ⇒ 要给宿主实体加列，**先请示用户**（备选：手改生成物 + 补模型 / 走近似关联不动表结构）。

### F. 提交与验证：**工作区全绿 ≠ 提交内容正确**（2026-10-06 实证，最隐蔽的一类坑）

- **症状**：收口时工作区跑测试 **227/227 全绿**，但在**干净检出**上跑同一套，立刻冒出 1 条自己范围内的失败。
- **根因**：宿主侧那几处「一行改动」（`AppBuilder.cs` 的 DI 注册、`*.csproj` 的插件引用）在并行会话共享文件里
  做「暂存后还原工作区」时**被当成别人的行留在了工作区**，从未入库。
  而源码守卫类测试（读 `AppBuilder.cs` 断言含某行）读的是**工作区文件** ⇒ 未提交的行照样能读到，测试假绿。
- **实证**：A3b 的 `builder.Services.AddScoped<ITurnTelemetryQuery, TurnTelemetryQueryService>()` 只在工作区、
  未随 `032caa3` 入库 ⇒ 干净检出时宿主不注入 `ITurnTelemetryQuery`，插件取宿主数据的唯一通道**处于未接线状态**；
  最终由守卫用例 `AppBuilder_注册了ITurnTelemetryQuery接缝` 在干净 worktree 全量回归中**实测转红**才暴露。
- **硬要求（收口前必做）**：在**只含已提交内容**的干净检出上跑**全量**回归，而不是在工作区：

  ```bash
  git worktree add /d/tmp/ofs-verify <你的 HEAD>
  cd /d/tmp/ofs-verify && dotnet test <TestProj>.csproj -v q --nologo   # 全量，不要 --filter
  git worktree remove /d/tmp/ofs-verify --force
  ```

  判据：与基线（`git worktree add ... <你的起始提交>`）对比 —— **通过数应恰好 += 你新增的用例数，失败数不增加**；
  若冒出的失败落在你的命名空间内 ⇒ 要么漏提交、要么真缺陷，二者都要修掉再收口。
- **逐文件确认，别只看 `git status`**：`git status` 显示某共享文件为 `M` 时，里面可能**同时**有你的行和别人的行。
  用 `git show <sha>:<file> | grep <你的唯一标记>` 逐个确认你的行**已在提交里**；
  最直接的办法是整份 `git diff -- <file>` 看一遍归属（本例中 `AppBuilder.cs` 的未提交差异 100% 是我自己的行，
  直接 `git add` 整个文件即可，反而不用走「取行」手法）。

### G. 插件自带界面的三类"看着对"的缺陷（2026-10-07 todo-tracker 1.1.0 全程实测；只有跑 e2e + 读图才暴露）

- **「点即保存」+「响应带整行快照」= 旧数据盖新数据**。连续失焦会连发多个 PUT，宿主日志证明**四个都落库了**，
  界面却仍显示「还缺：验收判据、验证命令」——因为响应**乱序回来**，最后一次落地的是较早那次写的旧快照。
  修法（普适，建议每个自带界面的插件都这么做）：前端 HTTP 封装把**非 GET 请求串成一条队列**（读不串），
  于是"最后到达的响应必然是最新状态"。参考 `Plugins/TodoTracker/web/src/http.ts` 的 `dispatch/writeChain`。
  写用例时**必须**同时断言两件事：界面翻对了 **且** REST 读回是真值（"界面翻对了不代表存对了"）。
- **空列表时不许替后端编原因**。`artifact-sets` 返回 `data: []` 时前端固定显示「该项目没有 docs/ai/pilot 目录」，
  而后端其实分得开"没目录"与"有目录但没匹配到 NN-*.md"，两种成因的下一步完全不同。
  修法：取封套时**连 `message` 一起返回**（`requestEnvelope`），空态优先显示后端给的那句，后端没说才用自己的兜底文案。
- **toast 会叠住你刚点过的控件**。四次失焦 = 四条「已保存」，实测正好压在「交给 AgentHub 执行」按钮上。
  修法：同文案同类型**去重并重置计时**、同时最多 3 条（`notify.ts`）。读图时专门看一眼"提示有没有挡住入口"。
- **禁用态的文案要分状态说**。委派按钮只用一个 fallback 文案（`先生成提示词`）时，"预览已生成但不可委派"
  会撒谎。要么按状态给四种文案，要么把后端 `delegationError` 原文端出来。
- **接口给了字段但界面没入口 = 假能力**。`preview.agents` 下发了 agent 候选却没有任何下拉，等于没做；
  补成 `data-test="delegate-agent"` 下拉才算闭环（对齐 `design-system-verify` 的"假能力自查"思路）。
