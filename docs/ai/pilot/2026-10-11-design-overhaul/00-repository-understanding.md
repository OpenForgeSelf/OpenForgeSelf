# Repository Understanding — 按高保真设计稿全面改造（设计对齐总规划）

> 阶段：Stage 0｜Task：DESIGN-OVERHAUL-2026-10-11｜日期：2026-10-11
> 唯一真源：`D:\Users\12504\Downloads\design_handoff_forgeself_home_plugin_system`（入口 `design_files/index.html`；核心 `design_files/{App.jsx, HomeScreen.jsx, OtherScreens.jsx, kit-styles.css, colors_and_type.css}`；说明 `../README.md`；资产 `assets/{logo-seal, logo-wordmark, logo-mark-mono, texture-forge, texture-paper}.svg`）
> 规范映射：`DESIGN.md`（v2-target，2026-10-10 已按设计稿重写，本方案以其为目标规格，不重复承载）

## 1. 项目用途与技术栈（实测）

- OpenForgeSelf（铸己匣）：个人 AI 工具箱宿主。Vue 3.5 + Vite 6 + TS + Element Plus 2.14 + Tailwind 4 + Pinia（`ForgeSelf.Web/`，pnpm）；.NET 10 + SQLite + NewLife.XCode 后端（`ForgeSelf.Api/`）；插件架构（**顶层 `Plugins/`** 20 个插件：AgentHub / AIAgent / CostScope / DesignSystem / DevTools / FileTools / Home / ImGateway / McpCenter / MemorySystem / QuickLinks / SamplePlugin / Scheduler / ScriptRunner / Sems / SystemMonitor / TextTools / TodoTracker / ToolBridge / WorkflowEngine；宿主侧 `ForgeSelf.Api/Plugins/` 仅 Abstractions / Dev / Services）。
- e2e 在 `ForgeSelf.Web/e2e/plugins/<id>/`（含 home、ai-agent、design-system 等 12 个）；地址真源 `e2e/helpers/e2e-env.ts`（默认 7102/7002，禁止硬编码）。

## 2. 宿主前端现状（实测文件）

| 区域 | 现状文件 | 与设计稿差距 |
| --- | --- | --- |
| 壳层 | `ForgeSelf.Web/src/App.vue` | 只有背景图 `<img>`（fixed）+ 透明度滑块 + `TopNavbar` + main>RouterView；**无 StatusBar、无玻璃面板体系、无助手气泡**；背景无"12 纯色 + 6 图 + 遮罩 0–90%"体系（`localStorage forgeself:bg` 未落地） |
| 顶栏 | `components/TopNavbar.vue` | 40px（h-10）实底 `--el-bg-color`；Element Plus 风格 Tab（下划线圆角指示条）；**非 48px 玻璃、非设计稿金线+光晕 Tab、无背景面板/遮罩入口** |
| 主题 | `styles/themes/workshop-forge.css` | 仅覆盖 `--el-*`；主色已 `#F59E0B` 但**功能色/背景/文字仍为旧值**（success #3FB950、bg #FFFFFF、text 暗色系），未按 DESIGN.md 器物 token（墨黑 #0B0908 / 宣纸 #FBF7EE / 朱砂 #C0392B / 青铜 #5A7A5A）迁移；`bg-image-mode.css` 表面透明规则存在 |
| 视图 | `src/views/` 23 个（AgentsManage / AllFeatures / Chat / ChatRecords / CodeSnippets / DevTools / Memory / PersonalLibrary / PluginDetail / PluginImportExport / PluginPage / PluginScaffolder / PluginStore / PluginUpdates / Profile / Prompts / ScriptLibrary / Settings / Skills / SystemMonitor / TextTools / WorkflowLibrary） | 均为 Element Plus 常规表单/表格/卡片风格，**未按 PageHead + 器物卡片 + 玻璃层级规格**改造；无统一空态/反馈体系 |
| 组件 | `components/`：AppLogo / MessageInput / MessageItem / MessageList / PluginCard / PluginDetailModal / Sidebar（**已弃用但存在**）/ TopNavbar | AppLogo 为宿主内实现（`src/assets` 为空，需核实其 SVG 来源）；Sidebar 应删除 |
| 路由 | `router/{index.ts, dynamicPlugins.ts, pluginRoutes.ts}` | 插件动态路由已存在；首页由 Home 插件经 `ForgeSetting.HomePluginId` 重定向（033 已实现） |
| stores | 17 个（tabs / theme / appearance / plugin / pluginManifest / agent / chat / …） | `appearanceStore` 已有 backgroundImage / backgroundOpacity，**缺遮罩度、12 纯色 + 6 图预设、forgeself:bg 持久化** |

## 3. 插件现状（实测）

- **Home 插件**：源码在 **顶层 `Plugins/Home/web/src/`**（HomeView.vue / homeStore.ts / features.ts / homeEntries.ts / usageStats.ts / featureIcons.ts / http.ts / types.ts / index.ts / styles/tailwind.css）——033 已实现 Hero/常用功能/核心看板/待办/活动整页插件化（e2e `ForgeSelf.Web/e2e/plugins/home/` 4/4 绿）。**与设计稿差距**：当前为 Element Plus 常规卡片布局，未按 HomeScreen.jsx 的"傍晚好，锻造师"Hero、5 列看板、玻璃卡片、金线 Tab 等重构；未 widget 化（design 要求的 home.hero-composer / home.function-grid / home.dashboard-boards / home.todo / home.activity-feed 五个 surface widget 未落地）。
- 其余 19 插件为宿主功能域（Agent / MCP / Memory / Scheduler / 监控等），本次以"宿主屏 + 插件屏按设计稿统一改版"为范围，不新建插件。

## 4. 背景图现状（实测，待对齐清单第 1 条）

- 设计稿 `App.jsx:74` 熔炉图外链 `https://images.unsplash.com/photo-1519113155050-ce987da39100?w=800...` **404 失效**（浏览器实测 onerror）；其余 5 图（山雾 photo-1506905925346 / 墨色 photo-1509475826633 / 星野 photo-1502134249126 / 海岸 photo-1439405326854 / 竹林 photo-1519834785169）实测 200。
- 生产策略：**背景图一律本地化到宿主 `ForgeSelf.Web/public/backgrounds/`（或插件资产），禁依赖外链**（离线可用 + 无 404 风险）；熔炉图从设计稿 bundle 或免费图源补。

## 5. 设计稿关键规格（已映射进 DESIGN.md v2-target，此处只列方案级引用）

- 品牌：东方器物美学（熔金橙 #F59E0B / 墨黑铸铁 #0B0908 / 宣纸白 #FBF7EE / 朱砂 #C0392B / 青铜 #5A7A5A；中性 90%·金 8%·朱砂 2%）。
- 壳层：48px 玻璃 TopBar + 22px StatusBar（mono 10px）；背景面板（12 纯色 + 6 图 + 遮罩 0–90%，默认 0.55，`localStorage forgeself:bg`）；助手气泡悬浮入口。
- 首页 widget：home.hero-composer / home.function-grid / home.dashboard-boards / home.todo / home.activity-feed。
- 玻璃：`rgba(20,17,15,.32)` + blur(18px) saturate(140%) 圆角 12px；禁胶囊；铸铁阴影 8 档。
- 动效：160/220/360ms + 锤落回弹；字体：Noto Serif SC / LXGW WenKai / Inter / JetBrains Mono。
- 技术约束（README）：生产用 Vue3+TS+Vite 转写（React 参考非生产）；Plugin Manifest 字段 id/name/version/author/category/icon/displayName/entry/surfaces/permissions/provides/replaces；surfaces=tab|home-widget|quick-action|status-badge；宿主拥有背景/TabBar/StatusBar/遮罩/气泡。

## 6. 差距结论（方案输入）

1. 主题 token 未器物化（M1）。
2. 壳层无 StatusBar/玻璃/背景体系/气泡（M2）。
3. 首页已插件化但未按设计稿布局/widget 化（M3）。
4. 23 宿主视图 + 19 插件前端未统一 PageHead/卡片/空态/反馈（M4）。
5. Plugin-First manifest/surface 规范未落地为代码约束（M5）。
6. 动效/交互无统一规范（M6）。
7. 文档真源未登记（docs/README.md §一无 DESIGN.md 行）、AGENTS.md §8 仍指向 forgeself-design、新建页面/查询无强制规范、无设计守卫（check-style-tokens.mjs 基线陈旧 2612>2559）、无 e2e 视觉基线（M7）。
8. 全量验证与发布未执行（M8）。
