# Evidence（批次A 菜单/路由真源一致性 · 测试审查独立核验）

> 证据来源等级：**Verified**（AC-1~8 均为独立复跑真实输出；API 面 + DLL 探针 + 浏览器渲染反证三类独立证据交叉印证，篡改取证当场还原并 grep 确认零残留）。
> 岗位：测试审查（只读核验 + 门禁③发布，不改业务代码）。核验日期：2026-09-27。派单：seq105；基准：seq95 验收包 / seq99 任务书 / seq102 交付回报。

## 一、AC 逐项结果（全部为独立复跑真实输出）

| AC | 口径 | 结果 | 证据（实测输出摘要） |
| --- | --- | --- | --- |
| AC-1 | quicklinks 项 Path=/quick-links 且全表无 /quicklinks | **Pass** | 运行实例 `GET /api/plugin/menu-items` 顶层含 (quick-links,/quick-links)，全表（含 children）/quicklinks 出现次数=0；`git grep '"/quicklinks"'`（cs/ts/vue/json，排除 api/quicklinks）0 残留 |
| AC-2 | scheduler 悬空声明已撤销（决策①A） | **Pass** | menu-items scheduler 项=0；SchedulerPlugin.cs diff：RegisterMenuExtensions 删除、工具函数注册保留、恢复指引注释在位 |
| AC-3 | sample 同 AC-2 | **Pass** | menu-items sample 项=0；SamplePlugin.cs diff 同型 |
| AC-4 | mcp-center+design-system 补发且与 manifest 逐字段相等 | **Pass** | 实测两插件 menu=(MCP 中心,/mcp-center,connection) 与 (设计系统,/design-system,fa-palette) 均 == frontend-manifest 对应字段（逐字段相等=True） |
| AC-5 | 双源逐条对账 diff=0 | **Pass** | e2e ① 实跑绿；运行实例逐插件条数对账 10 个「启用+menu+route」插件全部恰 1 条（无缺发/双发） |
| AC-6 | e2e 全绿 + 篡改必 Fail | **Pass** | `menu-route-consistency.spec.ts` 实跑 **4 passed (50.8s)**（globalSetup 全新宿主零 mock）。篡改取证：QuickLinksPlugin.cs 顶层 Path 改 `/quick-links-TAMPER` 重跑 → **2 failed 2 passed**，③报「menu-items 顶层项 quicklinks.menu.main(plugin=quick-links) Path=/quick-links-TAMPER 无法被运行时路由解析（悬空菜单）」、①报「双源对账差异」；②④不受影响通过（语义正确）。篡改已当场还原（grep TAMPER=0，diff 恢复为任务书 T2 一行） |
| AC-7 | 前端 pnpm check/test + dotnet build/test + 既有 e2e 零回归 | **Pass** | dotnet build 0 错误；dotnet test 全量 Abstractions 13P｜Core 12P｜Api.Tests **1460P/9F**，失败名单逐条=批次E 9 项（WorkflowPlanning×6、ScriptRunnerDi、TerminalCommandGuard、ForgeConfig）**零新增**（总数 1469=旧基线 1466+本单新 3 例）。pnpm run check exit 0（0 errors/82 存量 warnings）；pnpm run test **43 files / 473 passed**。既有 4 红（app.spec:35/home.spec:35/mcp-center:158/quicklinks.spec:164）引用面排查：4 spec 与宿主 src 均无菜单 `/quicklinks` 引用（quicklinks.spec 用路由 /quick-links 与 API 前缀 /api/quicklinks，均与本单改动无关）；dev 基线 worktree 复证存量红的结论与我的引用面排查相互印证=与本批零关联 |
| AC-8 | GetMenuItems 合并单测 3 例绿 | **Pass** | 隔离复跑 `PluginMenuItemsMergeTests` **3/3 绿**（真实调用 controller，非 mock；断言含 Id=`<pluginId>.menu.manifest`、逐字段、不双发、禁用不发） |

## 二、门禁③（发布到运行实例）

- 路径：按 plugin-publish-verify 主路径全量发布——停旧实例（D:\src\tools\ForgeSelf，PID 68256）→ `build.ps1`（95.4s，exit 0，产物 publish/）→ 起 publish 宿主（Production，数据根 ~/.forgeself）→ health 200。
- migrate-plugin-versions.ps1 执行报 ConvertFrom-Json 错（某插件 manifest 解析失败，未逐插件迁移）；运行实例以扁平布局正常提供插件（menu-items/manifest/API 全符），如实记录待项管哥分诊。
- 产物探针（probe-dll-string.cjs）：`Plugins/QuickLinks/QuickLinks.dll` FOUND "quick-links" / ABSENT 旧串 `quicklinks"`；宿主 `ForgeSelf.dll` FOUND "menu.manifest" → **批次A 改动确认在运行实例二进制中生效**。
- 运行实例 API 面 AC-1~5 实测全符（见上表，即「发布实跑」证据）。
- **quick-links live 5 例（playwright.live.config.ts）= 5 failed，判非批次A回归（反证已做实）**：根因取证——①浏览器新 context 下 `GET /api/plugin/frontend-manifest` 匿名=**401**（quicklinks API 匿名 200），宿主前端 manifest 装载失败 → 插件视图路由未注册 → outlet 空渲染（错误快照 `- main` 空、仅顶栏），heading「快捷链接」永不现；②同一二进制 + token 注入环境（e2e globalSetup 轮）`/quick-links` 真实导航渲染已由新 spec ② 证实通过；③**决定性反证**：按 plugin-publish-verify「运行态宿主手工走查正规通道」（get-forge-token → localStorage 注入 → 导航，一次性探索探针、用完即删不入版本控制）在本批次A 运行实例实测：导航 `/quick-links` 后 HEADINGS=["快捷链接"] 正常渲染 → **5 例红与批次A 无关联，锁死在 live spec 无 token 注入入口**（live.config 无 globalSetup/storageState；spec 头注释「登录态已存在」假设人工浏览器，playwright fresh context 不成立）。解锁条件=live 基建补 token 注入正规通道（建议登记独立缺陷单）。批次A 发布实跑有效性由本报告门禁③三项证据（API 面 + DLL 探针 + 浏览器渲染反证）独立成立，非假绿。

## 三、端口真源事件（重要披露）

- 任务书/派单/AGENTS/live spec 均称运行实例 **:51888**；但宿主配置唯一真源 `~/.forgeself/Config/ForgeSetting.config` `PortNumber=7102`，且宿主代码 `ForgeSetting.Current.PortNumber` 仅认配置（ASPNETCORE_URLS 覆盖无效）。旧 tools 实例监听 51888 的来源无配置依据（疑历史遗留/启动覆盖），其被停后无法以 51888 复起。
- 处置：新批次A 宿主按真源运行于 **7102**；核验期间曾临时加机器级端口转发 `netsh portproxy 51888→7102` 供 live spec 触达（取证完毕**已删除**，`portproxy show all` 已确认为空，51888 现无监听）。
- 该矛盾（51888 惯例 vs 7102 配置真源 vs e2e 临时宿主同用 7102 的端口规划）超出测审职责边界，**请项管哥裁决/路由**（SOP-03 端口真源规则）。

## 四、红线自查

- 未修改任何业务代码（篡改取证当场还原，git diff 与交付清单一致）；未删/跳任何测试；本报告与全部群/私信产出无密钥明文；核验临时产物（.temp/tr-*、各 *.log）已清理且均被 git-ignore，工作区跟踪文件零污染。
