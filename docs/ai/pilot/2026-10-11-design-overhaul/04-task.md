---
task_id: 2026-10-11-design-overhaul
feature: none
risk: { level: L2, triggers: ["ForgeSelf.Web/**", "ForgeSelf.Api/**", "Plugins/*/web/**"], raised_by_agent: false }
gate1: { mode: auto, at: 2026-10-11T00:15, ref: "user /goal" }
gate2: { mode: reviewer, at: 2026-10-11T13:55, decision: APPROVED }
gate3: { mode: agent, at: 2026-10-11T20:30 }
expected_files:
  - ForgeSelf.Web/src/**/*.vue
  - ForgeSelf.Web/src/styles/themes/workshop-forge.css
  - Plugins/*/web/src/**/*.vue
  - scripts/check-*.mjs
rollback: "git revert HEAD"
---

# Task — 按高保真设计稿全面改造（任务单元清单）

> 阶段：Stage 4｜Task：DESIGN-OVERHAUL-2026-10-11｜日期：2026-10-11
> 说明：每个任务单元 = 一个可独立走 AI-Native 闭环（或轻量 mini-task）的工作包；闸门1 逐包批准后 Implement。本清单为总调度表，各包 04-task 细节在开工时按此展开。

---

## T1（M0）背景与品牌资产本地化

- **Objective**：6 张背景图（含熔炉补图）与 5 个品牌 SVG 落地 `ForgeSelf.Web/public/`，App.vue 增加 onerror 降级；appearance store 预设数据就位。
- **Allowed**：public 资产、appearance.ts 数据、App.vue 最小 onerror。
- **Forbidden**：不动主题 token、不改壳层布局、不引入网络字体依赖。
- **AC**：
  - [ ] `ForgeSelf.Web/public/backgrounds/` 6 图存在且本地位移（无 unsplash 外链残留，grep = 0）
  - [ ] `public/brand/` 5 SVG 存在
  - [ ] 图片加载失败降级纯色（onerror 命中测试）
  - [ ] `pnpm run check` 0 error、`pnpm run test` 全绿
- **Verification**：`cd ForgeSelf.Web && pnpm run check && pnpm run test`；`grep -r "images.unsplash.com" src public` 结果为空。

## T2（M1）主题 token 器物化

- **Objective**：workshop-forge.css 浅/暗两套全量迁移；守卫基线重建。
- **Allowed**：themes/*.css、main.css、check-style-tokens.mjs、本批涉及的 .vue 裸色值替换。
- **Forbidden**：不改布局/组件结构；不新增颜色定义（全部走 token）。
- **AC**：
  - [ ] 浅/暗两套 token 与 DESIGN.md「Tokens」一致（抽查 ≥10 个 key 值相同）
  - [ ] `node scripts/check-style-tokens.mjs` 基线重建后 0 新增漂移（记录前后数字）
  - [ ] 双主题截图对比设计稿色板（走查截图）
  - [ ] `pnpm run check` + `pnpm run test` 全绿
- **Verification**：如上命令 + 走查截图读图。

## T3（M2）宿主壳层

- **Objective**：48px 玻璃 TopBar、22px StatusBar、背景面板（12 纯色 + 6 图 + 遮罩 0-90% + localStorage forgeself:bg）、助手气泡；删除 Sidebar。
- **Allowed**：App.vue、TopNavbar.vue、新增 StatusBar/BackgroundPanel/AssistantBubble、appearance.ts、Sidebar 删除及引用清理、相关单测。
- **Forbidden**：不改首页插件内容；不动路由结构。
- **AC**：
  - [ ] TopBar 高度 48px（e2e 断言或走查截图量测）；StatusBar 22px 且显示真实端口/版本
  - [ ] 背景面板 12 纯色 + 6 图 + 遮罩滑杆 0-90% 可用；刷新后保持（localStorage）
  - [ ] 助手气泡展开/折叠可用
  - [ ] Sidebar.vue 已删除且无残留引用（grep = 0）
  - [ ] `pnpm run check` + `pnpm run test` 全绿；e2e home 回归绿
- **Verification**：快档 + 浏览器走查 + e2e 定向。

## T4（M3）首页迁移（Home 插件）

- **Objective**：HomeView 按 HomeScreen.jsx 重构；widget surface 声明。
- **Allowed**：`Plugins/Home/web/src/**`（HomeView/homeStore/features/homeEntries/tailwind/index）、e2e home spec 增补、必要时宿主 widget 消费最小接线。
- **Forbidden**：不动其余插件；不改宿主壳层。
- **AC**：
  - [ ] Hero（时段问候）/常用功能/5 列看板/待办/最近活动空态与 HomeScreen.jsx 布局对齐（截图对照 preview-01-home.png）
  - [ ] widget 声明 home.hero-composer/function-grid/dashboard-boards/todo/activity-feed 存在
  - [ ] 插件 `pnpm run build` 通过；e2e home 现有 4 例 + 新增视觉用例绿
  - [ ] plugin-development 五步闭环执行（含发布 + 走查 + 运行实例只读复验，发布方式用户拍板）
- **Verification**：`cd Plugins/Home/web && pnpm run build`；`cd ForgeSelf.Web && pnpm run check && pnpm run test`；`npx playwright test e2e/plugins/home`。

## T5a/b/c（M4）功能屏迁移（三批）

- **Objective**：PageHead/ForgeCard/EmptyState 三组件 + 各视图统一。
- **Allowed**：三组件新增、对应 views 改造、受影响插件 web 前端（DesignSystem 走 design-system-verify 收口）。
- **Forbidden**：不改后端 API；不新增依赖。
- **AC**（每批）：
  - [ ] 该批视图全部接入 PageHead；卡片/空态/反馈统一（走查清单 0 命中）
  - [ ] 无新增裸色值（守卫绿）
  - [ ] 快档全绿（+ 插件批 build）
  - [ ] 插件批走 plugin-development 五步
- **Verification**：快档命令 + 走查 + 守卫。

## T6（M5）Plugin-First 架构规范

- **Objective**：manifest surfaces 字段校验 + 宿主消费 tab/home-widget/quick-action/status-badge。
- **Allowed**：Abstractions manifest 类型、dynamicPlugins/pluginRoutes/pluginManifest、相关测试、docs/02-features 规范文档。
- **Forbidden**：不改鉴权/权限核心。
- **AC**：
  - [ ] surfaces 四类可被宿主消费（e2e 定向 plugin-store/home 绿）
  - [ ] manifest 校验单测绿
  - [ ] 后端 `dotnet build` + `dotnet test`（过滤 manifest 相关）绿；碰 Api 源码 → 中档全量
- **Verification**：`dotnet build` + `dotnet test`（按档位）+ e2e 定向。

## T7（M6）动效与 UX 体系

- **Objective**：动效 token（160/220/360ms + 锤落回弹）+ 组件挂载 + 规范条目。
- **Allowed**：workshop-forge.css 动效 token、ForgeCard/PageHead 挂载、随 M4 顺带替换、规范沉淀。
- **Forbidden**：不整页 blur 堆叠。
- **AC**：
  - [ ] transition-duration 断言与设计稿一致（走查/单测）
  - [ ] 低配无卡顿走查
  - [ ] 规范条目沉淀（ui-ux-design 或 docs/04-standards）
- **Verification**：快档 + 走查。

## T8（M7）文档规范与守卫生效

- **Objective**：DESIGN.md 登记真源；新建页面/查询规范章节；守卫基线入库 + 接入 check/CI；e2e 视觉基线首批。
- **Allowed**：docs/README.md §一、AGENTS.md §8（及 §2.4 登记）、DESIGN.md 待对齐清单勾销、check-style-tokens 接入、e2e 视觉用例。
- **Forbidden**：不改业务代码逻辑（纯文档/守卫/e2e）。
- **AC**：
  - [ ] docs/README.md §一有 DESIGN.md 行；AGENTS.md §8 指向新真源
  - [ ] 新建页面/新建查询规范章节存在（PageHead/ForgeCard/EmptyState 使用规范 + token 引用 + 交互规格模板 + 品牌语汇 + 三禁复查清单）
  - [ ] 守卫接入 `pnpm run check`（或 CI），违规即红
  - [ ] e2e 视觉基线用例绿（首页/壳层/≥3 功能屏，截图对照设计稿基准）
- **Verification**：grep 命中 + 守卫跑通 + e2e 定向（深档触发条件评估）。

## T9（M8）全量验证与发布

- **Objective**：收口验证 + 发布 + 最终汇报。
- **Allowed**：按改动面分档验证；插件发布（用户拍板方式）；运行实例只读复验。
- **Forbidden**：不启停宿主；不越权提交（无明确许可不 git commit/push）。
- **AC**：
  - [ ] 全部门禁按档如实跑完并记录数字
  - [ ] 发布完成（插件五步闭环）或明确标注未发布项
  - [ ] 运行实例只读复验通过（如用户已更新实例）
  - [ ] DESIGN.md 待对齐清单全勾销
  - [ ] 07-final-report 汇总产出
- **Verification**：各档命令日志正文 + 复验截图读图。

---

## 任务依赖

```text
T1 --> T2 --> T3 --> T4 --> T5a/b/c（T6 可并行于 T4 后；T7 可随 T2/T3 早启动）
T8 依赖 T1-T4/T6 产物；T9 依赖全部。
```

## 用户拍板清单（闸门1 前置）

1. 宿主屏「宿主内改版 vs 转插件」：默认宿主内改版（仅 Home 保持插件）。
2. 发布方式：插件改动逐批发布（打 tag 自动发布 / 本地目录源）——默认打 tag 自动发布（CI 打包 GitHub Release）。
3. StatusBar 端口口径：默认显示实际服务端口。
4. 熔炉背景图图源：默认从设计稿 bundle/免费图源补同一意境图并本地化。
