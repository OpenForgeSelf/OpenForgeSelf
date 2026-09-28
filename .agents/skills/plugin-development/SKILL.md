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

## 二、铁律（先看这 19 条）

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

---

## 三、新建 / 迁移插件：步骤

### 3.1 后端骨架

```
ForgeSelf.Api/Plugins/<PascalCase>/
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

```powershell
pwsh .agents/skills/plugin-frontend-scaffold/scripts/scaffold-plugin-frontend.ps1 -Plugin <PascalCase>
cd ForgeSelf.Api/Plugins/<PascalCase>/web && pnpm i && pnpm run build
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
读技能 → 改代码（实体改动走 Model.xml→xcode）→ 门禁 → 插件层 e2e → 发布（打 tag 自动发布 / 本地目录更新源 + 页面自动更新）→ 走查（e2e 隔离实例）→ **运行实例只读复验（用户启用新版本后；见 `plugin-publish-verify`「运行实例只读复验」）** → 更新插件文档 → 记日志
```

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

- 后端默认端口 `7102`，本环境长期运行的 publish 实例用 `51888`
- 插件目录 = `AppContext.BaseDirectory/Plugins` → publish 实例即 `publish/Plugins`
- **插件数据目录** = `ctx.EnsurePluginDataDirectory()` → `{数据根}/Plugins/{插件Id}`（生产即 `~/.forgeself/Plugins/{id}`）。
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
  const b = fs.readFileSync('publish/Plugins/X/X.dll')
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
