# Plan — 按高保真设计稿全面改造（分步骤实施计划）

> 阶段：Stage 3｜Task：DESIGN-OVERHAUL-2026-10-11｜日期：2026-10-11
> 规则：每里程碑为**一个独立 pilot（或轻量 mini-task）**，先过闸门1 再 Implement；本文件是总路线图（依赖 + 文件 + 验证 + 档位），各里程碑的 02-spec/04-task 在开工时细化并继承本文件验收标准。
> 设计稿唯一真源：design_files + DESIGN.md（v2-target）。所有路径均为实测。

---

## M0 资产与地基（前置 · 无 UI 改动面）

**目标**：背景图本地化（修复熔炉 404）、品牌资产（logo/纹理）落地宿主、字体栈方案定稿。

### Files To Change
- `ForgeSelf.Web/public/backgrounds/`（新增：6 张背景图本地化，含熔炉补图；来源=设计稿 bundle 或免费图源）
- `ForgeSelf.Web/public/brand/`（新增：logo-seal / logo-wordmark / logo-mark-mono / texture-forge / texture-paper 5 个 SVG，从 bundle assets/ 复制）
- `ForgeSelf.Web/src/stores/appearance.ts`（背景图来源改本地路径 + 12 纯色/6 图预设数据 + 遮罩字段，本步只做数据与路径）
- `ForgeSelf.Web/src/App.vue`（onerror 降级纯色兜底，最小改动）

### 验证
- 浏览器实测：6 图全部 200（Invoke-WebRequest 或 bu 页面检查）；无外链引用残留（grep images.unsplash.com 结果 = 0）
- 快档：`pnpm run check` + `pnpm run test`（改动涉及 stores/App.vue，必跑）

### 依赖/退出
- 无依赖；退出 = 背景资产就绪且熔炉 404 关闭。
- 涉及外观 store 逻辑变化 → 按轻量 mini-task 走闭环（≤3 文件）。

---

## M1 主题 token 器物化

**目标**：`workshop-forge.css` 全量迁移到 DESIGN.md「Tokens」（浅/暗两套），全项目引用 token 无裸色值新增。

### Files To Change
- `ForgeSelf.Web/src/styles/themes/workshop-forge.css`（全量重写：色板/功能色/文字/边框/玻璃参数 → 器物 token；`--forge-*` 扩展）
- `ForgeSelf.Web/src/styles/themes/bg-image-mode.css`（表面透明规则对齐新 token）
- `ForgeSelf.Web/src/styles/main.css` 或 tailwind 入口（如需挂 `--forge-*` 全局）
- `ForgeSelf.Web/scripts/check-style-tokens.mjs`（或同级守卫脚本：基线重建，硬编码色值清单刷新）
- 各 `.vue` 中裸色值（`bg-[#...]` / `text-[#...]`）→ token（按守卫报告逐文件替换，仅本批涉及者）

### 验证
- 快档：`pnpm run check` + `pnpm run test`
- token 守卫：`node scripts/check-style-tokens.mjs` 基线重建后 0 新增漂移（记录前后数字）
- 视觉：浅/暗两主题截图对比设计稿色板（e2e 视觉用例或走查截图）

### 依赖
- M0（资产就绪不影响本步，可并行）；退出 = 守卫绿 + 双主题视觉对齐。

---

## M2 宿主壳层（TopBar / StatusBar / 背景面板 / 助手气泡）

**目标**：App.vue 壳层全面对齐设计稿 App.jsx：48px 玻璃 TopBar、22px StatusBar、背景面板（12 纯色+6 图+遮罩 0-90%+localStorage）、助手气泡。

### Files To Change
- `ForgeSelf.Web/src/App.vue`（壳结构：玻璃遮罩层 + TopBar + StatusBar + main + 气泡 + 背景面板 Teleport）
- `ForgeSelf.Web/src/components/TopNavbar.vue`（40px→48px 玻璃化、金线 Tab 指示 + 光晕、右侧加背景面板/助手入口、移除实底 `--el-bg-color`）
- `ForgeSelf.Web/src/components/StatusBar.vue`（新增：22px mono 10px，端口/版本/状态；端口取真实服务端口，禁止硬编码）
- `ForgeSelf.Web/src/components/BackgroundPanel.vue`（新增：12 纯色 + 6 图 + 遮罩滑杆，选择即预览+落盘）
- `ForgeSelf.Web/src/components/AssistantBubble.vue`（新增：悬浮气泡 + 消息输入流，复用 MessageInput/MessageList）
- `ForgeSelf.Web/src/stores/appearance.ts`（12+6 预设、遮罩、localStorage forgeself:bg 持久化）
- `ForgeSelf.Web/src/components/Sidebar.vue`（删除：已弃用且引用未定义变量；同步移除引用/测试）

### 验证
- 快档：`pnpm run check` + `pnpm run test`；新增 store 逻辑补 vitest（持久化/遮罩端点）
- 浏览器走查：背景面板选色/选图/拖遮罩 → 刷新后保持（localStorage）；StatusBar 显示真实端口；Tab 金线指示
- e2e：`ForgeSelf.Web/e2e/plugins/home` 回归 + 新增壳层视觉用例（截图对照设计稿）
- 档位：本步仅前端 → 快档 + e2e 定向（不触发后端中档）

### 依赖
- M0（背景图/品牌资产）；M1（token 先就位，玻璃参数/色板才可用）——顺序 M1→M2。
- 退出 = 壳层三件套 + 背景体系 + 气泡可用，Sidebar 删除无残留引用。

---

## M3 首页迁移（Home 插件按 HomeScreen.jsx）

**目标**：`Plugins/Home/web/src/HomeView.vue` 按 HomeScreen.jsx 重构：Hero（时段问候「傍晚好，锻造师」）+ 常用功能 + 5 列核心看板 + 待办 + 最近活动空态；widget 契约落地（home.hero-composer / home.function-grid / home.dashboard-boards / home.todo / home.activity-feed）。

### Files To Change
- `Plugins/Home/web/src/HomeView.vue`（布局重构：Hero/功能网格/5 列看板/待办/活动，玻璃卡片 + 铸铁阴影 + 空态）
- `Plugins/Home/web/src/homeStore.ts` / `features.ts` / `homeEntries.ts`（看板数据、功能网格数据、时段问候逻辑）
- `Plugins/Home/web/src/styles/tailwind.css`（器物 token 引入或对齐宿主 token）
- `Plugins/Home/web/src/index.ts`（widget surface 声明：home.* 五个）
- `ForgeSelf.Web/src/router/pluginRoutes.ts` / `dynamicPlugins.ts`（宿主消费 widget 契约——若 M5 后置，本步先保证布局对齐，widget 消费放 M5）

### 验证
- 快档：`cd Plugins/Home/web && pnpm run build`；宿主 `pnpm run check` + `pnpm run test`
- 插件 e2e：`ForgeSelf.Web/e2e/plugins/home/`（现有 4 例回归 + 新增视觉用例：Hero 完整容纳、5 列看板、空态文案）
- 走查：隔离实例按用户视角截图对照 design_files 首页（preview-01-home.png 基准）
- 档位：涉及插件 → 快档 + 插件 e2e；不触发中/深档

### 依赖
- M1（token）、M2（壳层视觉上下文）。**插件门禁**：改动 `Plugins/Home/web/` → 实施完成走 plugin-development 五步（门禁→e2e→发布→走查→运行实例只读复验；发布方式：打 tag 或本地目录源，用户拍板）。
- 退出 = HomeView 与设计稿布局对齐（截图对照）+ widget 声明就位 + e2e 绿。

---

## M4 功能屏迁移（宿主视图 + 插件前端统一）

**目标**：23 个宿主视图 + 其余插件前端按 OtherScreens.jsx 统一 PageHead / 器物卡片 / 空态 / 反馈 / 边界。**分批（每批一个 pilot）**：

- M4a 高优先级宿主屏：`AgentsManageView / SettingsView / SystemMonitorView / SkillsView / PluginStore / PluginDetail`
- M4b 中优先级：`AllFeaturesView / ChatView / MemoryView / PromptsView / CodeSnippetsView / ScriptLibrary / WorkflowLibrary / DevToolsView / TextToolsView / PersonalLibraryView`
- M4c 其余 + 插件前端（DesignSystem / AIAgent / McpCenter / MemorySystem / Scheduler / TodoTracker / QuickLinks / TextTools / FileTools / DevTools / SystemMonitor / ScriptRunner / WorkflowEngine / CostScope / ToolBridge / AgentHub / ImGateway / Sems 的 web/ 前端统一规格）

### Files To Change（每批内）
- 对应 `ForgeSelf.Web/src/views/<X>.vue`（PageHead 引入 + 卡片/空态/反馈统一，禁裸色值）
- `ForgeSelf.Web/src/components/PageHead.vue`（新增：标题+副题+操作区通用组件）
- `ForgeSelf.Web/src/components/ForgeCard.vue`（新增：器物玻璃卡片通用组件）
- `ForgeSelf.Web/src/components/EmptyState.vue`（新增：空态分级组件：文案+引导+重试）
- 各插件 `Plugins/<X>/web/src/**`（同规格；DesignSystem 插件 token 同步——涉及 `Plugins/DesignSystem` 用 design-system-verify 收口）

### 验证
- 每批：快档 `pnpm run check` + `pnpm run test`（+ 对应插件 `pnpm run build`）；走查清单抽查；e2e 定向回归（受影响 spec）
- 档位：宿主视图为主 → 快档；插件前端批次 → 插件五步闭环（发布方式随批拍板）
- 退出 = PageHead/ForgeCard/EmptyState 覆盖抽查 ≥5 屏走查 0 命中；无新增裸色值（守卫绿）

### 依赖
- M1（token）、M2（壳层）、M3（首页基准风格定型）。退出 = 三组件就位 + 抽查绿。

---

## M5 Plugin-First 架构规范落地

**目标**：manifest 字段与 surface 类型规范化为宿主校验/文档基准；首页 widget 契约消费（若 M3 已声明，本步补宿主消费）。

### Files To Change
- `ForgeSelf.Api/Abstractions`（manifest 类型定义补 surfaces 字段校验，若缺失）
- `ForgeSelf.Web/src/router/dynamicPlugins.ts` / `pluginRoutes.ts`（surface 路由：tab / home-widget / quick-action / status-badge 消费）
- `ForgeSelf.Web/src/stores/pluginManifest.ts`（manifest 规范化读取）
- `docs/02-features/`（新功能文档：Plugin-First 规范，若新写）

### 验证
- 快档 + 后端 `dotnet build` + `dotnet test`（过滤器：plugin manifest 相关）；e2e 定向（plugin-store / home）
- 档位：碰宿主后端 → **中档**（后端全量 dotnet test）

### 依赖
- M3（widget 声明）；退出 = surfaces 四类可被宿主消费，manifest 校验测试绿。

---

## M6 动效与 UX 体系统一

**目标**：160/220/360ms + 锤落回弹动效；焦点/悬停/加载态统一；交互规格成文（并入 M7 规范章节）。

### Files To Change
- `ForgeSelf.Web/src/styles/themes/workshop-forge.css`（动效 token：`--forge-dur-fast/med/slow` + 回弹 cubic-bezier）
- `ForgeSelf.Web/src/components/ForgeCard.vue` / `PageHead.vue`（动效挂载：进入 160ms、展开 220ms、面板 360ms + 锤落回弹）
- 各视图动效替换（随 M4 批次顺带，不单独铺开）
- `.agents/skills/ui-ux-design` 或 `docs/04-standards/`（动效规范条目沉淀）

### 验证
- 快档；走查：动效时长断言（transition-duration）与设计稿一致；低配机器无卡顿（无整页 blur 堆叠）
- 退出 = 动效 token 就位 + 组件挂载 + 规范条目沉淀

### 依赖
- M1（token 承载）、M2（壳层动效上下文）；可与 M4 并行（动效 token 先行）。

---

## M7 文档规范与守卫生效（用户 /goal 核心：避免不一致）

**目标**：设计规范成为**可执行的强制依据**：登记真源、接入流程、守卫基线、e2e 视觉基线；新建页面/查询全部按规范（含交互规格、空态/反馈/边界、品牌语汇）。

### Files To Change
- `docs/README.md` §一「30 秒定位速查」：**新增 DESIGN.md 行**（唯一真源登记）
- `AGENTS.md` §8 设计稿工作流：指向新真源 design_files + DESIGN.md（替换 forgeself-design 引用或并列说明）；§2.4 技能清单按需补设计规范技能登记
- `docs/04-standards/` 或 DESIGN.md：**新建页面/新建查询规范章节**（PageHead/ForgeCard/EmptyState 使用规范、token 引用规范、交互规格模板、品牌语汇表、三禁红线复查清单）
- `ForgeSelf.Web/scripts/check-style-tokens.mjs`：基线重建入库（M1 产物），并确认接入 `pnpm run check` 或 CI
- `ForgeSelf.Web/e2e/`：视觉基线用例（每屏截图对照设计稿基准图，落 `screenshots/e2e/`；首页/壳层/≥3 功能屏首批）
- `DESIGN.md`：待对齐清单逐条勾销（熔炉 404 / workshop-forge.css 迁移 / 基线重建）

### 验证
- 文档：`docs/README.md` §一 grep DESIGN.md 命中；AGENTS.md §8 引用命中
- 守卫：`node scripts/check-style-tokens.mjs` 0 新增漂移；新建页面/查询规范章节存在且被后续 pilot 引用（抽查 ≥1 个新 pilot 的 02-spec 引用）
- e2e：视觉基线用例绿（对照基准截图）
- 档位：碰 `ForgeSelf.Web/e2e/**` → **深档**触发条件（globalSetup/playwright config 未碰则不强制全量，但视觉基线用例需在隔离实例跑通）

### 依赖
- M1（守卫基线）、M2-M4（截图基准）、M6（交互规范条目）；退出 = 文档三件套 + 守卫 + 视觉基线全绿。

---

## M8 全量验证与发布

**目标**：收口验证 + 发布（插件与宿主按发布规范），DESIGN.md 待对齐清单清零。

### 动作
- 快+中+深档按改动面执行：前端 `pnpm run check` + `pnpm run test`；后端 `dotnet build` + `dotnet test`（若 M5 动过 Api）；e2e 全量（若 M7 动过 e2e 基建）
- 插件发布：Home 等改动插件走 plugin-development 五步（tag/本地目录源，用户拍板）；宿主发布由用户/update-agent 自更新（agent 不启停）
- 运行实例只读复验（用户已在运行实例更新后）：token 校验 → 版本徽标 → 主链路中间态 → 截图读图
- 最终汇报：07-final-report（每里程碑 Evidence/Review 齐备后汇总）

### 退出
- 全部门禁绿（分档如实汇报）+ 发布/复验完成 + 工件链 00-07 齐备。

---

## 总依赖图（简）

```text
M0 资产 --> M1 token --> M2 壳层 --> M3 首页 --> M4 功能屏（3 批）
                                   |            |
                                   +-> M5 Plugin-First <--+（可并行）
M6 动效 token（可随 M1/M2 早启动）
M7 文档守卫（依赖 M1-M4 产物）--> M8 全量验证与发布
```

## Plan 偏差记录

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| （实施时填写） | | | |
