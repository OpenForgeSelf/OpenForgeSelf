---
version: v2-target
name: OpenForgeSelf（铸己匣）· 工坊锻造 Workshop Forge · 器物美学目标规范
description: 本地优先的 AI 工具箱宿主。以高保真设计稿为唯一真源：东方器物美学 × 现代工具箱，熔金橙 + 墨黑铸铁 + 宣纸白 + 朱砂印章，深夜铸造的工坊气质。规范真源 = D:\Users\12504\Downloads\design_handoff_forgeself_home_plugin_system\design_files\（index.html / App.jsx / HomeScreen.jsx / OtherScreens.jsx / kit-styles.css / colors_and_type.css）。
colors:
  primary: "#F59E0B"
  primary-light: "#F9BC4E"
  primary-dark: "#D97706"
  success: "#5A7A5A"
  warning: "#E0A030"
  error: "#C0392B"
  info: "#4A7A9B"
  bg: "#0B0908"
  bg-page: "#14110F"
  fill: "#1B1815"
  text-primary: "#F4F0E7"
  text-regular: "#B5AB9F"
  text-secondary: "#8F8478"
  text-disabled: "#6B6058"
  border: "#322D28"
  border-light: "#24201C"
typography:
  title-md:
    fontFamily: "'Noto Serif SC', 'LXGW WenKai', ui-serif, serif"
    fontSize: 26px
    fontWeight: 700
    lineHeight: 1.15
    letterSpacing: 0.05em
  title-sm:
    fontFamily: "'Noto Serif SC', 'LXGW WenKai', ui-serif, serif"
    fontSize: 20px
    fontWeight: 600
    lineHeight: 1.35
    letterSpacing: 0.03em
  body-lg:
    fontFamily: "'Inter', 'PingFang SC', system-ui, sans-serif"
    fontSize: 16px
    fontWeight: 400
    lineHeight: 1.75
  body-md:
    fontFamily: "'Inter', 'PingFang SC', system-ui, sans-serif"
    fontSize: 14px
    fontWeight: 400
    lineHeight: 1.55
  label-sm:
    fontFamily: "'Inter', 'PingFang SC', system-ui, sans-serif"
    fontSize: 12px
    fontWeight: 500
    lineHeight: 1.35
  label-xs:
    fontFamily: "'JetBrains Mono', ui-monospace, monospace"
    fontSize: 10px
    fontWeight: 500
    lineHeight: 1.35
    letterSpacing: 0.14em
rounded:
  xs: 2px
  sm: 4px
  md: 6px
  lg: 10px
  xl: 14px
  full: 9999px
spacing:
  xs: 4px
  sm: 8px
  md: 12px
  lg: 16px
  xl: 20px
  xxl: 24px
  xxxl: 32px
components:
  topbar:
    height: 48px
    backgroundColor: "rgba(11, 9, 8, 0.35)"
    backdropFilter: "blur(24px) saturate(140%)"
    borderBottom: "1px solid rgba(245, 240, 231, 0.06)"
  card:
    backgroundColor: "rgba(20, 17, 15, 0.32)"
    backdropFilter: "blur(18px) saturate(140%)"
    rounded: 12px
    padding: 16px
    border: "1px solid rgba(245, 240, 231, 0.08)"
    boxShadow: "0 6px 24px rgba(0,0,0,0.28), inset 0 1px 0 rgba(255,255,255,0.04)"
  statusbar:
    height: 22px
    fontFamily: "'JetBrains Mono', ui-monospace, monospace"
    fontSize: 10px
    letterSpacing: 0.04em
  hero:
    padding: "20px 24px"
    rounded: 12px
    marginBottom: 16px
---

# OpenForgeSelf · UI 设计规范（DESIGN.md）

> **唯一真源声明（2026-10-10 用户指令）**：以高保真设计参考稿
> `D:\Users\12504\Downloads\design_handoff_forgeself_home_plugin_system\design_files\`
> （入口 `index.html`；主壳 `App.jsx`；首页 `HomeScreen.jsx`；功能屏 `OtherScreens.jsx`；
> 布局 `kit-styles.css`；tokens `colors_and_type.css`；设计说明 `../README.md`）为**唯一真源**。
> 本文件的目标 = **把现有代码对齐到该设计稿**。设计稿与代码不一致时，以设计稿为准，
> 未对齐处在本文件对应章节记为「**待对齐**」（代码对齐后移除）。
> 原「代码是唯一事实源」的表述废止。
>
> 实现约束：参考稿为 React + Babel 浏览器直跑的高保真原型，**不是生产代码**；
> 生产实现转写为 Vue 3 SFC + Composition API + TypeScript + Vite（宿主 `ForgeSelf.Web`、
> 插件 `Plugins/*/web`），tokens 层（`colors_and_type.css`）可直接复用/转译。

---

## Overview

**品牌与风格**：铸己匣是本地优先的个人 AI 工具箱——瑞士军刀级应用。
视觉定位「**东方器物美学 × 现代工具箱**」，母题四个：**器物、锻造、沉淀、工具箱**。
深夜铸造的工坊气质：熔金橙为唯一品牌强调，墨黑铸铁打底，宣纸白作浅色底，朱砂红作印章点缀；
玻璃质感克制、信息密度中等偏高，面向长时间使用的工具型界面。

**设计原则（三系统 + 三禁）**：
1. 三套系统，不设第四套：Element Plus（组件与暗色）+ Tailwind CSS v4（布局与排版）+ 主题文件（色值 token）。
2. 只覆盖官方 `--el-*` 变量与设计稿 token（`--ink-*` / `--gold-*` / `--paper-*` / `--cinnabar-*` / `--bronze-*` 及语义变量）定义色值；不得新增自定义 token 类（如 `--surface-*`、`--forge-*`、`--app-*`）。
3. 页面以组件和工具类拼装，不写手写组件类；样式优先复用既有组件与 token。
4. **三禁**（器物美学红线）：禁止赛博紫/蓝紫渐变、禁止玻璃拟态过头（`backdrop-filter` 数值不得调高）、禁止可爱风插画与 emoji 装饰。

**使用比例**：中性（墨黑/宣纸/灰阶）90% · 熔金 8% · 朱砂 2%。

**规则缺失时的兜底判断**：先问「这个界面是工具型（高频、信息密集）还是展示型（少量、留白）」。工具型优先紧凑与对齐；展示型优先留白与层级。

---

## 唯一真源与文件映射

| 主题 | 真源文件 | 说明 |
|------|----------|------|
| 全部设计 token（色/字/距/圆角/动效） | `design_files/colors_and_type.css` | 唯一 token 出处，实现层转译为主题文件 |
| 布局与组件样式 | `design_files/kit-styles.css` | 玻璃卡片/顶栏/状态栏/首页各模块/背景面板 |
| 主壳（TabBar/StatusBar/背景系统） | `design_files/App.jsx` | 宿主拥有，非插件 |
| 首页（Hero/常用/看板/待办/活动） | `design_files/HomeScreen.jsx` | 目标态布局与数据形态 |
| 功能屏（Agent/技能/MCP/监控/设置） | `design_files/OtherScreens.jsx` | PageHead 与各屏卡片规格 |
| 品牌基调/交互/插件架构 | `../README.md` | 设计说明：Plugin-First、屏幕清单、动效、Don'ts |
| 资产（logo/纹理） | `assets/*.svg` | logo-seal / logo-wordmark / logo-mark-mono / texture-forge / texture-paper |

---

## Colors

**主题切换机制**：默认**暗色**（`data-theme="dark"`，`color-scheme: dark`），另有浅色 `data-theme="light"`（宣纸质感「白日工坊」）。主题切换写根元素 `data-theme`；实现层沿用既有 `forgeself.theme.mode`（light | dark | system）持久化机制。

**色板（暗色，默认）**：

| 角色 | Token | 值 | 用途 |
|------|-------|----|------|
| 熔金橙（主色） | `--gold-400` | `#F59E0B` | 主 CTA / 强调 / 选中 / 品牌徽标 |
| 熔金悬浮 | `--gold-300` | `#F9BC4E` | hover、active 文字 |
| 熔金按下 | `--gold-500` | `#D97706` | 按下态、渐变终点 |
| 朱砂红 | `--cinnabar-400` | `#C0392B` | 印章 / 错误 / 警示 |
| 青铜绿 | `--bronze-500` | `#5A7A5A` | 成功 / 状态绿点 |
| 墨黑（canvas） | `--ink-950` | `#0B0908` | 应用底色「熔炉深处」 |
| 墨黑（surface） | `--ink-900` | `#14110F` | 卡片 / 面板（玻璃底色） |
| 墨黑（elevated） | `--ink-850` | `#1B1815` | 弹层 / 浮层 |
| 墨黑（border strong） | `--ink-700` | `#322D28` | 强边框 |
| 宣纸白 | `--paper-white` | `#FBF7EE` | 浅色底（浅色模式主 canvas） |
| 宣纸暖 | `--paper-warm` | `#F4EEDF` | 浅色模式页面底 |

**前景 / 边框（暗色）**：主文字 `#F4F0E7`（`--fg-primary`）、次级 `#B5AB9F`（`--fg-secondary`）、三级 `#8F8478`（`--fg-tertiary`）、禁用 `#6B6058`（`--fg-muted`）；边框阶梯 hairline `rgba(245,240,231,.06)` → subtle `.10` → default `.16` → strong `.28`；品牌边框 `rgba(245,158,11,.48)`。

**语义色（暗色）**：success `#6B8E4E`（浅底文字 `#A8C08A`）、warning `#E0A030`（`#F5C572`）、danger `#C0392B`（`#E86A5B`）、info `#4A7A9B`（`#7FA8C8`）；语义底色用对应 `rgba(…, .14-.16)`。

**浅色主题（`data-theme="light"`）**：canvas `#F4EEDF`、surface `#FBF7EE`、elevated `#FFFFFF`；主文字 `#14110F`；主色降为 `#B45309`（gold-700），边框与阴影换浅色系。除主色收敛外，规则与暗色一致。

**背景系统（宿主拥有，非插件）**：
- **纯色 12 档**：墨黑 `#0B0908` / 铁灰 `#1B1815` / 深蓝 `#0F172A` / 深绿 `#0F1F1A` / 深紫 `#1A0F1F` / 朱砂 `#3A1512` / 青铜 `#1F2A1F` / 宣纸 `#F4EEDF` / 瓷白 `#FBF7EE` / 天青 `#1E3A4C` / 玄铁 `#141518` / 曜黑 `#000000`（`App.jsx:58-71`）。
- **图片预设 6 张**：熔炉 / 山雾 / 墨色 / 星野 / 海岸 / 竹林（`App.jsx:73-80`；⚠️ 熔炉预设的 Unsplash 外链已 404 失效，需本地化修复，见「待对齐」）。
- **本地上传**：FileReader 转 DataURL 应用。
- **遮罩浓度**：滑竿 0–90%，写 `--overlay-opacity`；默认 55%。
- **机制**：`--bg-color` / `--bg-image` CSS 变量注入 `body`，`body::before` 固定遮罩层（`linear-gradient(180deg, rgba(11,9,8,overlay-0.05) 0%, rgba(11,9,8,overlay) 100%)`），背景 `cover/center/fixed`，切换过渡 400ms `ease-out`。持久化 `localStorage['forgeself:bg']`。
- **背景图使用规则**：背景属宿主底盘，与玻璃卡片共存；遮罩保证前景可读（对比度规则见 Do's）。⚠️ 设计稿方案为 CSS 变量 + 遮罩（非固定定位 `<img>`），以设计稿为准。

**使用规则**：
- 熔金橙仅用于主操作、选中态、品牌标识；同一视图内主色强调不超过一处。
- 颜色语义必须对应功能（成功/警告/错误），不得用于装饰。
- 文字对比度不低于 4.5:1（正文）、3:1（大号文字与图标）；背景图模式下靠遮罩与 `fg-*` 分级文字保证可读（`kit-styles.css:570-573`）。
- Tailwind 不定义独立色值，所有 `--color-*` 映射到设计稿 token / `--el-*`。
- 需要透明度派生时用 `color-mix(in srgb, var(--gold-400) 12%, transparent)`，不写硬编码 `rgba(245,158,11,…)` 进组件。

**已对齐（2026-10-11）**：
- ✅ 背景图资产本地化：6 张预设已落到宿主 `ForgeSelf.Web/public/backgrounds/`（含熔炉补图 `forge.jpg`，修复 `App.jsx:74` 外链 404），引用为本地 URL（`src/stores/appearance.ts` `BG_IMAGE_PRESETS`）；`App.vue` 图片加载失败自动降级纯色（`onBackgroundError`）。
- ✅ 主题 token 迁移：`ForgeSelf.Web/src/styles/themes/workshop-forge.css` 已按本节色板重写（浅/暗两套，含设计稿 token `--ink-*`/`--gold-*`/`--paper-*`/`--cinnabar-*`/`--bronze-*`/`--fg-*`/`--dur-*`/`--ease-*`/`--shadow-*`/玻璃参数；浅色主色收敛 `#B45309`）。
- ✅ 色值守卫基线重建：`scripts/check-style-tokens.mjs` 豁免 token 定义层（`**/styles/themes/`）后重建基线 `scripts/style-token-baseline.json`（合计 2501，PASS）。

---

## Typography

**字体族**（`colors_and_type.css:175-180`）：

| 角色 | Token | 字体栈 | 用途 |
|------|-------|--------|------|
| 展示（大标题） | `--font-display` | `"Noto Serif SC", "LXGW WenKai", "Songti SC", ui-serif, serif` | 页标题、hero、印章 |
| 中文书卷 | `--font-serif-cn` | `"LXGW WenKai", "Noto Serif SC", "Songti SC", "SimSun", ui-serif, serif` | 小标题、引言、卡片正文 |
| 正文 | `--font-sans-cn` | `"Inter", "PingFang SC", "Microsoft YaHei", -apple-system, sans-serif` | 默认正文、表单、按钮 |
| 等宽 | `--font-mono` | `"JetBrains Mono", "SF Mono", Menlo, Consolas, monospace` | 代码、数据、时间、eyebrow、状态 |

**字号阶梯**（1.25 比例，CJK 微调）：

| 层级 | 字号 | 字重 | 行高 | 字距 | 用途 |
|------|------|------|------|------|------|
| display | 64px | 900 | 1.15 | 0.05em | 品牌级展示（仅特殊场景） |
| h1 / 页标题 | 26px | 700 | 1.15 | 0.05em | 功能屏大标题（PageHead） |
| h2 | 20px | 600 | 1.35 | 0.03em | 区块标题 |
| h3 | 18px | 500 | 1.35 | — | 卡片内标题 |
| 正文 body | 14px | 400 | 1.55 | 0 | 默认正文 |
| body-lg | 16px | 400 | 1.75 | — | 重要说明段落 |
| label-sm | 12px | 500 | 1.35 | — | 按钮、标签、次级信息 |
| mono-small | 11px | — | 1.55 | 0.02em | 代码、日志 |
| eyebrow | 10px | 500 | 1.35 | 0.14em（大写） | 金色小标签（PageHead 眉题） |

**规则**：
- 中文标题用 `--tracking-cjk: 0.05em`（印章感）；英文/数字标题用 `--tracking-wide: 0.04em`。
- 同一区块内字号不超过三级；标题与正文靠字号和字重区分，不靠颜色堆叠。
- 禁止正文 `font-size` 低于 12px（mono 数据类可到 10-11px，如状态栏、eyebrow、日志）。
- 中英混排靠字体回落保证，不为中文单独写字体；正文行高 1.55、标题 1.15-1.35。

---

## Layout

**应用壳**（`kit-styles.css:2-29, 104-126`，body 为 flex 纵向三明治）：
- 顶部 `TopBar`：高 **48px**，玻璃条（`rgba(11,9,8,.35)` + `blur(24px) saturate(140%)` + 下边框 hairline），`-webkit-app-region: drag`（品牌/导航/操作区 `no-drag`）。
- 主内容 `<main class="main">`：`flex:1; overflow-y:auto`，内边距 **20px 28px 36px**，`position:relative`。
- 底部 `StatusBar`：高 **22px**，mono 10px，`rgba(11,9,8,.4)` + `blur(20px)` + 上边框 hairline；左「匠人 Lv.1 · 已装载 N 器 · AI 离线 · REST+WS :7788」，右端时钟。
- 页面切换：`.page-enter` 淡入上移 `280ms ease-out`。

**首页布局**（1440 宽，居中留呼吸空间，自上而下）：
1. **Hero 玻璃卡**：`padding 20px 24px`；左「匠」字印章 glyph（28px 金渐变底）+ 问候标题；副行「今日 N 次工具调用 · N 个技能已启用」（数字用 mono + 金色）；composer 输入框 + 金色发送钮；建议芯片横排。
2. **常用功能**：`section-title` + `.func-row` 横排 `func-tile`（min-width 108px，图标金、文字 13px），点击导航对应 Tab。
3. **核心看板**：`.board-row` **5 列 grid**，`board-card`（min-height 88px）：标题 + 状态点（金脉动 `on` / 灰 `off` / 青铜绿 `green`）+ 状态 mono + body serif。
4. **底部两列**：`.bottom-row` grid **1.5fr : 1fr**——待办（复选框 14px、done 划线 0.5 透明、dashed 分隔、右下「查看全部 N 条 →」金）+ 最近活动（图标 26px 金底、空态 serif 居中）。

**间距刻度**（4pt 网格，`colors_and_type.css:222-235`）：2 / 4 / 8 / 12 / 16 / 20 / 24 / 32 / 40 / 48 / 64 / 96。

| 场景 | 值 |
|------|----|
| 紧凑 / 图标与文字 | 4px |
| 行内元素 / 芯片间距 | 8px |
| 卡片间距 / 网格 | 12px |
| 标题与内容 | 16px |
| 卡片内边距 | 14–20px（玻璃卡 16px 基准） |
| 区块间距 | 16–18px |
| 主内容区内边距 | 20 / 28 / 36px |

**响应式**：设计稿以 1440 桌面为基准，未定义统一断点；实现层需在 `ForgeSelf.Web/src/styles` 确定断点后补充（记入 TODO，规范随后补）。

---

## Elevation & Depth

**层级规则**：
- 页面层：canvas 底色（暗 `#0B0908` / 浅 `#F4EEDF`），可被背景系统覆盖（背景图 + 遮罩）。
- 卡片与面板：**玻璃卡片** `.glass`（`kit-styles.css:129-155`）：
  - `background: rgba(20,17,15,.32)`（浅色主题下换浅色系）
  - `backdrop-filter: blur(18px) saturate(140%)`（**参数不得调高**）
  - `border: 1px solid rgba(245,240,231,.08)`，圆角 12px
  - `box-shadow: 0 6px 24px rgba(0,0,0,.28), inset 0 1px 0 rgba(255,255,255,.04)`
- 浮层（弹窗/面板/菜单）：`.glass-strong`（`rgba(20,17,15,.55)` + `blur(24px) saturate(160%)`）；下拉可 `blur(32px)`（背景面板）。
- 玻璃弱化：`.glass-subtle`（`.18` + `blur(14px)`）用于次级衬底。

**阴影阶梯**（铸铁光影，`colors_and_type.css:111-119`，暗色）：

| 档位 | 值 | 用途 |
|------|----|------|
| xs | `0 1px 0 rgba(0,0,0,.35)` | 极浅 |
| sm | `0 1px 2px rgba(0,0,0,.4), 0 0 0 1px rgba(0,0,0,.2)` | 静止态 |
| md | `0 4px 12px rgba(0,0,0,.45), 0 1px 2px rgba(0,0,0,.35)` | 悬浮卡片 |
| lg | `0 12px 32px rgba(0,0,0,.55), 0 2px 6px rgba(0,0,0,.4)` | 下拉、菜单 |
| xl | `0 24px 60px rgba(0,0,0,.65), 0 4px 12px rgba(0,0,0,.45)` | 模态 |
| brand | `0 0 0 1px rgba(245,158,11,.4), 0 8px 24px rgba(245,158,11,.18)` | 品牌强调（选中态） |
| forge | `0 0 40px -8px rgba(245,158,11,.35)` | 熔炉光晕（logo、主按钮） |
| inset | `inset 0 1px 0 rgba(255,255,255,.03), inset 0 -1px 0 rgba(0,0,0,.4)` | 玻璃内高光 |

**规则**：
- 浅色主题阴影换浅色系（`colors_and_type.css:162-169`），不用纯白高光模拟层级。
- 不使用渐变作为层级手段；hero 与 send 按钮的琥珀渐变是品牌背景，不是层级。
- 边框优先于阴影：同级并排元素用 hairline/subtle 边框区分。

---

## Shapes

**圆角刻度**（器物切角，不过分圆润；`colors_and_type.css:237-245`）：

| 层级 | 值 | 用途 |
|------|----|------|
| xs | 2px | 印章类容器、极小标签 |
| sm | 4px | 默认小元素、按钮 |
| md | 6px | 输入框、chip、图标底 |
| lg | 10px | 卡片（玻璃卡实现取 12px，见注） |
| xl | 14px | 模态、弹层 |
| 2xl | 20px | 大弹层（极少用） |
| full | 999px | **仅**极小 tag / 状态点 / 开关圆钮 |

**规则**：
- **禁止胶囊按钮**：`border-radius: 9999px` 除极小 tag、状态点、开关滑块外一律不用；按钮/卡片不用 full。
- 建议芯片（suggest-chip）为半圆胶囊（20px）属 chip 特例，按钮与卡片除外。
- 同一区块内圆角统一，不混用直角与圆角；嵌套元素圆角比外层小一级。
- 图标按钮默认圆形或 md，不做异形。
- 注：玻璃卡 `.glass` 实测 12px（`kit-styles.css:134`），README token 表 lg=10px——玻璃卡统一取 10–12px 区间内一致即可，全站保持一致优先。

---

## Motion

**时长 / 缓动**（`colors_and_type.css:247-258`）：

| Token | 值 | 用途 |
|-------|----|------|
| `--dur-instant` | 90ms | 按下反馈 |
| `--dur-fast` | 160ms | hover、开关、小过渡 |
| `--dur-base` | 220ms | Tab 切换、普通过渡 |
| `--dur-slow` | 360ms | 背景切换、面板、较大过渡 |
| `--dur-slower` | 540ms | 长过渡 |
| `--ease-out` | `cubic-bezier(0.22, 1, 0.36, 1)` | **默认**缓出 |
| `--ease-forge` | `cubic-bezier(0.34, 1.56, 0.64, 1)` | 「锤落」回弹，仅关键落地时刻（logo、重点出现） |

**规则**：
- Hover：`translateY(-1px)` 或 `opacity 0.85`（**禁止放大缩小**）。
- Active：`translateY(1px)` + 阴影收紧。
- Tab 切换：当前 Tab 金色下划线（2px，`--gold-400` + 8px 光晕）+ 文字 `--gold-300`；主视图淡入 220ms。
- 页面进入：`.page-enter` 淡入上移 `280ms ease-out`。
- 状态点（看板 on）：`pulse-dot` 2s 呼吸（`kit-styles.css:348-351`）；off/灰点无动画。
- 背景面板：`slide-in .24s ease-out`（淡入 + 上移 8px）。
- 动效不得持续高频空转；无意义装饰动画一律不做。

---

## Components

**组件来源**：优先 Element Plus（宿主）；插件模板禁止 `<ElXxx>`，用原生 HTML + token 变量或显式 import（沿用 `docs/05-guides/plugin-frontend-development.md`）。所有组件规格以设计稿为准。

### 宿主壳
| 组件 | 规格 |
|------|------|
| `TopBar` | 高 48px 玻璃条；左「logo-seal.svg 22px（金色 drop-shadow 辉光）+ 铸己匣（display 15px/700/0.06em）」；中 `tab-item`（13px，hover 白字+淡底，active 金字+金下划线 2px+光晕）；右操作按钮 30px（hover 淡底、active 金底 12%）；「背景」按钮开合背景面板 |
| `StatusBar` | 高 22px；mono 10px；左金「● 匠人 Lv.1」+ 分隔项；最右时钟（每秒更新） |
| `assistant-bubble` | 右下角固定 40px 圆钮（`right:24px; bottom:44px`），玻璃 + 金边 + forge 光晕，sparkles 图标；hover 微放大（scale 1.06，唯一允许的缩放例外） |
| `bg-panel` | 右上 300px `glass-strong` 浮层；纯色 12 档（6 列网格）、背景图 6 预设（3 列 60px 缩略图）、上传（虚线边框按钮）、遮罩滑竿（金 accent-color + mono 数值）、恢复默认；`localStorage['forgeself:bg']` |

### 首页（ForgeHome）
| 模块 | 规格 |
|------|------|
| `hero-card` | 玻璃卡；`glyph` 28px 金渐变「匠」字印章（shadow 金晕）；问候按小时分档（凌晨/上午/中午/下午/傍晚/深夜，`HomeScreen.jsx:5`）；sub 12px 带 mono 金数字；`composer` 输入框（focus-within 金描边 3px 晕）、send 34px 金渐变钮；`suggest-chip` 横排（点击填入输入框不提交） |
| `func-tile` | 常用功能横排；min-width 108px，金图标 16px + 13px 文字；点击导航 Tab |
| `board-card` | 核心看板 5 列；`dot` 6px 金（on 呼吸）/灰（off）/青铜绿（green）；status mono 10px（on 金色）；body serif 12.5px + strong 15px |
| `todo-list` | 玻璃卡；复选框 14px（hover 金边，done 金底 + 划线 + 0.5 透明）；dashed 分隔；右上 add/更多按钮；右下「查看全部 N 条 →」金 11px |
| `activity-list` | 玻璃卡；空态 serif 居中「暂无最近活动」；条目图标 26px 金底 12% + meta |

### 功能屏（PageHead + 各屏）
| 组件 | 规格 |
|------|------|
| `PageHead` | eyebrow（mono 10px 大写 0.18em 金）+ 标题（display 26px/700/0.05em）+ 右侧 actions（金底 chip 主操作） |
| Provider 卡（Agent） | 2 列 grid；图标 40px 金底 10%；名称 14px + 模型 mono 11px；状态 chip（已接入=青铜绿 / 未接入=灰 / 离线）；底部 [配置][API Key][测试] chip 按钮；下方 Agent 记忆空态（brain 图标 + 「匣中沉淀·尚未开始」+ mono 0 KB） |
| 技能卡（Skills） | 4 列 grid；图标 30px（启用金底 12% / 未启用灰 5%）；名称 13px；底部 mono「分类 · N 次」+ 金色小 toggle（28×15，圆钮 11px）；未启用整卡 opacity 0.6 |
| MCP 卡 | 2 列 grid；puzzle 金图标 18px；名称 mono 13px；状态点 6px + 文字（运行中青铜绿/已停止灰/错误朱砂）；底部 mono「暴露 N 个工具 · npx …」 |
| 指标卡（Monitor） | 4 列 grid；名称 serif 12px；数值 mono 26px + 单位 12px；进度条 4px（渐变 `#F59E0B→#D97706`，transition .6s）；hint mono 10px；下方实时日志 `pre`（`rgba(0,0,0,.3)`、mono 11px、max-height 240px 滚动） |
| 设置（Settings） | 左 180px 分类栏（图标 + 文字，active 金底 10% 金字）+ 右内容卡；每项 label 13px + desc 11px + 值/开关/更改按钮；dashed 分隔 |

**状态变体**：hover / active / disabled 用 token 派生（金底 6%/10%/14%、文字金、透明 0.6），不手写覆盖 EP 内部结构。

**空态三件套**：图标 + 一句 serif 引言 + 可选主操作（Agent 记忆、最近活动均如此）；不留白屏。

---

## Plugin-First 架构约定（本次转型核心）

> 目标态：所有功能统一被「**插件**」抽象接管，首页是动态面板而非导航目录。详见设计稿 `README.md`「核心概念」。

- **首页也是官方插件 `@forge/home`**（ForgeHome）：Hero / 常用功能 / 核心看板 / 待办 / 最近活动均为 `home-widget` surface 上的 slot，可由其他插件贡献、被替换/卸载。
- **Manifest 必含**：`id`（`@命名空间/名字`）、`name`、`version`、`author`、`category`（agent|tool|monitor|workflow|mcp|custom）、`icon`（Lucide 名或 SVG 路径）、`displayName`（zh-CN/en）、`entry`、`surfaces`（tab / home-widget / quick-action / status-badge）、`permissions`、`provides.commands`、`replaces`。
- **宿主拥有**（非插件）：背景系统、TabBar、StatusBar、全局遮罩、助手气泡；插件经 API 上报数据。
- **插件 UI 约束**：插件自带 Tailwind 时用 `@theme inline` 内联 token，不向 `:root` 发射 `--color-*`；插件 UI 必须展示版本号。

---

## Do's and Don'ts

**Do**
- 先查设计稿（`design_files/`）有没有对应原型：有则对齐布局、间距、组件选型与 token；没有则按本文件生成并回写设计稿。
- 颜色、字号、圆角、间距只从 token 与刻度中取值；新代码禁止硬编码 hex/rgb。
- 玻璃卡统一走 `.glass` / `.glass-strong` 参数（blur 18-24px / saturate 140-160%），不得调高。
- 文字对比度 ≥4.5:1（正文）、3:1（大号文字与图标）；背景图下用遮罩 + `fg-*` 分级保证可读。
- 列表/面板提供空态、加载态、错误态；空态 = 图标 + 一句 serif 引言（+ 主操作）。
- 破坏性操作（删除、覆盖、停用）前必须二次确认，失败要留痕可见。
- 滚动区域内固定高度卡片设 `flex-shrink-0`；日志区限高滚动。
- 主操作按钮每屏仅一个；破坏性用朱砂 `--cinnabar-400`。
- hover 用 `translateY(-1px)` / opacity .85；active 用 `translateY(1px)` + 阴影收紧。

**Don't**
- 不要新增 `--surface-*`、`--forge-*`、`--app-*`、`--sidebar-*` 等自定义 token 类。
- 不要在 `.vue` 文件新增 hex / rgb 硬编码色值（展示型开发工具除外，评审中说明）。
- 不要用 `import { ElXxx } from 'element-plus'` 在宿主页面做组件导入（类型导入除外）。
- 不要 `:deep` / 全局样式打补丁式覆盖 EP 内部结构，优先改 `--el-*` 与 token。
- **不要玻璃拟态过头**（blur 数值不调高）、**不要 emoji 装饰**、**不要胶囊按钮**（极小 tag / chip 例外）、**不要蓝紫渐变**、**不要可爱风插画**。
- 不要用纯颜色区分唯一语义（同时配图标或文字）；不要把正文写成 12px 以下。
- 不要 `canvas` 绘制时硬编码色值，取色用 `getComputedStyle`。

---

## 新建页面与查询执行规范（2026-10-11 立 · 硬性）

> 目的：保证以后**所有新建页面、新建查询**与既有 UI/UX/品牌一致，避免漂移（用户 2026-10-11 指令）。违反本节的改动在闸门1 不通过。

**新建页面**（宿主 `ForgeSelf.Web/src/views/**` 或插件 `Plugins/<X>/web/src/**`）：
1. 页头必须用 `PageHead` 三件套：eyebrow（mono 10px 大写 0.18em 金）+ 标题（display 26px/700/0.05em）+ 右侧主操作 chip（金底）。
2. 布局骨架 = `.glass` / `.glass-strong` 卡片 + `--el-*` / 设计稿 token + Tailwind 布局原语；**禁止裸 hex/rgb**（含 `style` 内联与 `tailwind.config`，展示型开发工具除外须评审说明）。
3. 状态三件套齐全：加载态 / 空态（图标 + 一句 serif 引言 + 可选主操作）/ 错误态（留痕可见）；不留白屏。
4. 品牌语汇：主操作每屏仅一个（金底/金描边），破坏性用朱砂 `--cinnabar-400`；无 emoji 装饰、无胶囊按钮（极小 tag/chip 例外）、无蓝紫渐变、无可爱风插画。
5. 响应式以 1440 桌面为基准；固定高度卡片 `flex-shrink-0`，日志/列表限高滚动。
6. 交互反馈：hover = `translateY(-1px)` / opacity .85；active = `translateY(1px)` + 阴影收紧；动效取 `--dur-*`/`--ease-*`；焦点态金描边。

**新建查询**（宿主/插件列表、看板、统计等取数 UI）：
1. 统一走既有 API 层与 loading/error/empty 三态，禁止页面内裸 `fetch` + 手写状态拼装。
2. 数据形态与设计稿一致：状态点（on 金 / off 灰 / 绿 青铜绿）、mono 数值、serif 名称、`status mono 10px` 等按「首页/功能屏」组件规格取值。
3. 查询交互点（排序/过滤/分页/刷新）有明确可见状态与提示；交互规格写「点什么出现什么」（见方案 `docs/ai/pilot/2026-10-11-design-overhaul/02-spec.md` 交互点表）。
4. 查询失败/为空必须有空态文案与重试入口，不得白屏或仅 console 报错。

**验收（门禁三步，2026-10-11 强化，缺一不可）**：
1. **静态守卫（自动）**：`pnpm run design:check`（= `scripts/check-style-tokens.mjs` 禁新增裸色值 + `scripts/check-design-compliance.mjs` 禁新增 token 族违规/胶囊按钮/玻璃过头/蓝紫渐变）PASS；pre-commit hook 在提交触及前端源码时自动拦截。
2. **Spec 硬性环节（闸门1）**：涉用户可见 UI 的 Spec 必含「Interaction Design」节（`ui-ux-design` 技能审查），写清「点什么出现什么」+ 空态/反馈/边界；缺节闸门1 不通过。
3. **走查符合性（验收）**：起源码本地前后端走查（`dev-stack.ps1`）+ 真实走查，按 `ui-ux-design` 走查清单逐项核对，并与设计稿（`design_files/`）截图对比。

---

## 门禁化说明（2026-10-11 立 · 强制，不靠自觉）

> 目标：框架变更、页面开发、新建查询**必须**按本文件执行。除 Spec 交互节与走查（人工环节）外，
> 本规范已做成**自动门禁**，违反即拦截（pre-commit + 交付门禁双保险）：

| 规范条款 | 机器判据（脚本） | 违规示例 |
|---|---|---|
| token 单一来源：禁新增自定义 token 族 | `check-design-compliance.mjs` 规则 A（棘轮基线） | 组件层新增 `--surface-*` / `--forge-*` / `--app-*` / 白名单外 `--xxx` |
| 禁裸色值（hex/rgb 硬编码） | `check-style-tokens.mjs`（棘轮基线） | `.vue` 里写 `#F59E0B` / `rgba(...)` |
| 禁胶囊按钮 | 规则 B（`border-radius: 9999px` / `rounded-full` 于按钮上下文） | 主操作按钮做成胶囊 |
| 禁玻璃拟态过头 | 规则 C（backdrop-filter/filter 的 blur > 40px） | blur 50px 的浮层 |
| 禁蓝紫渐变 | 规则 D（indigo/violet/purple 系色值入 linear-gradient） | `linear-gradient(...#6366f1, #a855f7)` |
| 可爱风 / 品牌语汇 / UX 一致性 | 人工：Spec 交互节（闸门1）+ ui-ux-design 走查 | emoji 装饰、插画风、对比度不足 |

- 基线文件：`scripts/style-token-baseline.json` + `scripts/design-compliance-baseline.json`（随代码入库，棘轮只禁增量上涨）。
- 存量违规已计入基线（逐步治理），**新提交不得让任何一项上涨**；确需上涨须先与维护者确认，禁止直接 `--update` 抹平。
- 接线：`ForgeSelf.Web/package.json` 的 `design:check` → `scripts/hooks/pre-commit`（触及前端源码即跑）→ AGENTS.md §5.6 快档门禁必跑。

---

## 维护说明

- **修改 token**：只改设计稿 `design_files/colors_and_type.css`（真源）→ 同步实现主题文件（宿主 `ForgeSelf.Web/src/styles/themes/`）→ 回写本文件与设计稿。
- **新增组件规格**：在「Components」补一行，并在对应原型页面验证；规格变化回写设计稿 `README.md` 屏幕清单。
- **背景图资产**：本地化到 `assets/backgrounds/`（当前熔炉外链 404，优先修复）。
- **色值守卫**：`scripts/check-style-tokens.mjs`（禁新增裸色值，基线随 token 迁移重建）+ `scripts/check-design-compliance.mjs`（禁新增设计规范违反，三禁红线机器判项），合并入口 `pnpm run design:check`。
- **文档校验**：可用 `@google/design.md` 的 `lint` 检查本文件 token 引用完整性（命令待仓库验证后写入）。
- **对齐节奏**：本文件是目标规范；实现层分批对齐（按页面/组件），每完成一块移除对应「待对齐」标注。
