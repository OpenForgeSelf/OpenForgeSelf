# 006 设置背景图（背景图模式）— 功能需求与设计

> 功能编号：006
> 状态：已实现
> 关联：007 背景可见性与透明度；前端样式铁律（working memory）（历史 specs/006 已弃用）
> 最后更新：2026-08-12

## 1. 功能需求

### 1.1 背景
桌面应用希望支持自定义全屏背景图，提升观感，且不破坏 Element Plus 设计 token 体系。

### 1.2 目标
1. 用户在设置面板（`AppearancePanel`）选择本地图片作为全屏背景；
2. 背景图稳定显示（不依赖外链渲染）；
3. 弹层（dialog/dropdown 等）在背景图模式下正确透明化，不出现"白底黑框"。

## 2. 设计

### 2.1 渲染方案（铁律）
- **用固定定位 `<img>`**（`position:fixed; inset:0; object-fit:cover; z-index:1; pointer-events:none`），内容层 `z-index` 叠放；其下还有一层 `.app-bg-backing`（实心 `var(--el-bg-color)` 主题色兜底，`z-index:0`），保证背景图未加载或透明时始终有主题色垫底；
- **禁止** CSS `background-image: url(外链)`——本环境外链背景图不渲染（已验证：手动注入 `!important` 截图仍纯白）。

### 2.2 主题 token（铁律）
- 全程**零自定义 token**：只重写官方 `--el-*`，不定义 `--surface-*`/`--forge-*`/`--fs-*` 等；
- 弱化/半透明用官方 `--el-*` + `color-mix(in srgb, var(--el-*) N%, transparent)` 派生，不用 `rgba()`；
- 背景图模式唯一来源 `src/styles/themes/bg-image-mode.css`：主布局透明化由选择器 `.app-layout.has-bg-image` 驱动（`App.vue` 给布局层挂 `has-bg-image` class）；teleport 到 `<body>` 的弹层（dialog/dropdown/menu 等）透明化由 `body.app-bg-image-mode` 后代选择器覆盖（`.el-dialog` 半透/无边框/深色遮罩）；dropdown/menu 等小浮层保持实底；
- **canvas 取色**：canvas 2D 不解析 CSS 变量，JS 中用 `getComputedStyle(document.documentElement).getPropertyValue('--el-color-*')` 读计算值。

### 2.3 实现位置
| 文件 | 职责 |
|------|------|
| `src/styles/themes/bg-image-mode.css` | 背景图模式复合 token + 弹层透明化 |
| `src/styles/themes/workshop-forge.css` | 主体设计 token |
| `src/stores/appearance.ts` | 背景图/可见性状态 |
| `src/components/settings/AppearancePanel.vue` | 设置面板 |
| `src/App.vue` | 给 `<body>` 挂 `.app-bg-image-mode` |

## 3. 使用指南
设置 → 外观 → 选择背景图；切换"背景图模式"开关；调整可见性/透明度（007）。

## 4. 测试覆盖
| 测试 | 覆盖点 |
|------|--------|
| `appearance-store.test.ts` `AppearancePanel.test.ts` `App.test.ts` | 状态切换、body class 挂载、面板交互 |

## 5. 注意事项
- 背景图模式 teleport 弹层透明化依赖 `body.app-bg-image-mode` 后代选择器覆盖，普通规则直接覆盖工具类（`:deep` 等底层 hack 会被拒）。
