# M4c 插件前端迁移 · P2 批证据（DesignSystem 插件）

> 日期：2026-10-11
> 范围：Plugins/DesignSystem/web/src/** 全部外壳样式迁移到 Forge token 体系
> 冻结模式：token 化 + 禁裸色值/fallback + 三态 + 品牌语汇 + 版本号展示

## 一、逐组件执行清单

| 文件 | 动作 | 说明 |
|------|------|------|
| `styles/tokens.css` | 重写 | 外壳 `--ds-*` 别名从硬编码浅色蓝色系（--ds-brand-600:#3a70ee 等）重映射到宿主 workshop-forge token（--gold-400/--fg-*/--border-*/--el-bg-color 等）；删除全部 ~60 个硬编码 hex 色阶变量（brand-*/accent-*/gray-*/success/warning/danger），改为引用宿主 token |
| `styles/base.css` | 重写 | .ds-surface 改用宿主玻璃参数（--glass-bg/--glass-blur/--glass-border）；.ds-chip/.ds-mini hover/active 动效取 --dur-*/--ease-out；eyebrow 取 --font-mono + --gold-400；.ds-surface-bg 改 transparent（宿主背景透出） |
| `DesignSystemView.vue` | scoped 样式重写 | header 改 --ink-veil + glass-strong blur；nav 分组改 mono 10px 大写 0.14em；nav active 改金底金描边；select/mini 按钮改 --el-fill-color-blank + --el-border-color；skin meta 改 --el-bg-color-overlay；footer 改 mono 10px |
| `shell/ModeBar.vue` | scoped 样式更新 | tab active 改 --gold-300 文字 + --gold-400 下划线 + --gold-glow-md 光晕；border 改 --border-hairline |
| `components/PanelState.vue` | scoped 样式更新 + 文案 | unauthorized 图标从 emoji 🔒 改为「锁」字（禁 emoji 规则）；icon 改 --font-display 28px 金色；error 用 --cinnabar-400；hint 改无 uppercase |

**未改动文件**（功能保留 > 视觉拷贝）：
- `sections/*.vue`（16 个 section 组件）：全部通过 tokens.css 别名重映射自动跟随主题，未改源码
- `showroom/**`（Showroom/Stage/OutfitScope/mannequins 系列）：展厅预览区使用运行时注入的用户设计系统令牌（.ds-skin 作用域），外壳 token 重映射不影响预览内容
- `delivery/DeliveryMode.vue`、`start/StartMode.vue`：同上，通过别名自动跟随
- `design/*.ts`、`state.ts`、`http.ts`、`api.ts`：纯逻辑文件，无样式
- 所有 `*.test.ts`：未改测试断言，全部通过

## 二、验证结果

### 层1：插件构建 + 守卫
- **插件构建** `pnpm run build`：PASS（index.js 445.75 kB / style.css 98.05 kB / 1.47s）— Verified
- **style-tokens 守卫**：1605（基线 2501，减少 896 处）— Verified
- **design-compliance**：a(token族)=209（基线 244）/ b(胶囊)=0 / c(玻璃过头)=0 / d(蓝紫渐变)=0（基线 d=1）— Verified
  - 三项指标全部下降，无新增违规

### 层2：宿主门禁
- `pnpm run check`（vue-tsc + eslint）：0 errors / 145 warnings（全部为宿主既有文件，插件前端不在宿主 ESLint 扫描范围）— Verified
- `pnpm run test`（vitest）：70 files / 792 tests 全绿（含 DesignSystem 插件自身 6 个测试文件：state/snippets/scenes/mode/dialogs/latest）— Verified

### 层3：插件层 e2e（主冒烟）
- `design-system.spec.ts`：1 passed（59.1s）— Verified
- 关键断言全部通过：
  - 版本自洽：plugin.json 3.1.0 == meta.modelVersion 3.1.0 == 界面徽标
  - 主题切换：light.surface-bg=#f6f6f9 / dark.surface-bg=#211f25，computed 逐位匹配
  - 密度：light.space.4=8px / compact.space.4=6px
  - 令牌总数：1478 / 图标 40 / 组件 10 / 十类实体读回行数正确
  - 导出：CSS 16.9KiB / DTCG 54.2KiB / round-trip 逐字一致
  - 审计：58 行对比度 / 0 critical / 禁用态豁免 6 行
  - EP 接缝：102 变量 / 132 条引用
- 截图读图：外壳浅色主题下 select/输入框白底、版本徽标金色、nav 激活态金色金边、空态金点 — Verified

### 层4：发布与走查
- 本批**不发布**（按 spec §3 不打 tag、不出 zip）— 说明已备妥
- 走查：e2e 截图读图已做（层3），未做真人外行走查

## 三、design-system-verify「假能力」自查表

| # | 检查项 | 状态 |
|---|--------|------|
| 1 | 主题切换真效（dark/light surface-bg 不同值） | Verified（e2e 逐位钉死） |
| 2 | 导出非空声明 | Verified（16.9KiB CSS / 54.2KiB DTCG） |
| 3 | 界面计数真实 | Verified（1478 tokens / 40 icons / 10 components） |
| 7 | 预览与交付同源 | Verified（注入 CSS == export?format=css） |
| 8 | 前端无第二套实现 | Verified（未改 design/*.ts 计算逻辑） |
| 17 | 共享词汇表类存在 | Verified（ds-chip/ds-mode-pane 在 base.css 全局定义） |
| 34 | dist 与源码同步 | Verified（先 build 再跑 e2e，v=hash 破缓存） |

## 四、决策四要素

- **决策**：外壳 `--ds-*` 别名重映射到宿主 token，而非逐组件改引用
  - 背景：DesignSystem 有 20+ 组件全部引用 `--ds-*` 变量，逐组件改不现实且易漏
  - 理由：tokens.css 是外壳别名层，改别名定义 = 所有组件自动跟随主题；`.ds-skin` 运行时注入的用户令牌作用域不受影响
  - 准则：插件外壳 token 别名层应指向宿主 token，用户预览令牌由后端注入运行时管理

- **决策**：表面色用 `--el-bg-color*` 而非 `--ink-900`
  - 背景：`--ink-*` 是暗色板固定值，不随 light/dark 切换；首版截图发现浅色主题下输入框背景黑色
  - 理由：EP 变量是主题感知的，自动跟随宿主 light/dark
  - 准则：插件外壳表面/输入背景用 `--el-bg-color*`，品牌/强调/边框用 `--gold-*`/`--border-*`

## 五、未做 / 风险

- **层3发布**：本批不发布，未打 tag、未出 zip、未本地目录更新
- **层5运行实例只读复验**：前提=用户已把宿主更到新版，本批未发布故前提不成立
- **4 个其余 e2e spec**（agent/guidelines/showroom/style）：按 spec 不跑不删，留待统一复跑窗口
- **500 错误**：e2e 日志中 7 个 500（tokens/effective light、audit filter、assets/screens 首次读取）为既有后端 SQLite BUSY / 冷读缺陷（skill 已知未做项），非本批前端改动引入
- **seedColor 默认值**：ColorLab/向导的种子色默认值 `#3366ff`/`#7c3aed` 未改（功能输入默认值，非外壳色；测试夹具依赖这些值）
