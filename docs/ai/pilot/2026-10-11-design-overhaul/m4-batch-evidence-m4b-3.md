# M4b-3 批次证据：迁移 TextToolsView + PersonalLibraryView

> 批次：M4b-3（中优宿主屏 ×2）
> 日期：2026-10-11
> 范围：`ForgeSelf.Web/src/views/TextToolsView.vue`、`ForgeSelf.Web/src/views/PersonalLibraryView.vue`

## 执行清单

| # | 里程碑 | 动作 | 文件 | 证据等级 |
|---|--------|------|------|----------|
| 1 | 读必读 | m4-execution-spec + DESIGN.md + SystemMonitorView 样板 + 三组件 PageHead/ForgeCard/EmptyState | — | Verified |
| 2 | TextToolsView 改造 | header→PageHead（eyebrow "Text Tools · 文本工具箱" / title "格式化 · 编解码 · 哈希 · 统计"）；删 emoji 🛠️；error-banner token 化（cinnabar）；全 scoped 样式 token 化 | `TextToolsView.vue` | Verified |
| 3 | PersonalLibraryView 改造 | view-header→PageHead（eyebrow "Personal Library · 个人工具库" / title "使用情况 · 能力成长"）；时间范围选择器+刷新按钮移入 #actions slot；6 统计卡→ForgeCard 网格；成长曲线/排名×3/节省时间→ForgeCard 包裹；error-state→EmptyState+重试；全 scoped 样式 token 化（移除全部 var(--x,#hex) fallback 与裸 hex）；canvas 取色改 getComputedStyle 读 --gold-400/--fg-tertiary；删除 6 个蓝紫渐变 icon 背景（违反三禁） | `PersonalLibraryView.vue` | Verified |

## 验证结果

### 1. 设计守卫（design:check）— PASS
- style-tokens：2418（基线 2501，本批减少 83 处裸色值）≤ 门禁 2484
- compliance：a=244 b=0 c=0 d=0（基线 a=244 b=0 c=0 d=1，本批修复了 1 处蓝紫渐变）
- 日志：`.temp/m4b3-design.log`

### 2. 类型+lint（pnpm run check）— 本批文件 0 error
- vue-tsc + eslint 输出中无 TextToolsView.vue / PersonalLibraryView.vue 相关 error
- 剩余 6 个 error 在 DevToolsView.vue（TS6133×3 + TS2307×1）和 WorkflowLibrary.vue（TS2322×1）——**并发 M4a/M4b 其他批次引入，非本批**
- 日志：`.temp/m4b3-check6.log`

### 3. 单测（pnpm run test）— 本批文件无新增失败
- 67 files / 771 tests：65 passed / 2 failed（SkillsView.test.ts 9 failed + pluginStore.test.ts 3 failed）
- 失败全部来自并发 M4a-2 批次（SkillsView/PluginStore 迁移中），非本批文件
- 本批两个视图无 vitest 单测，不引入新失败
- 日志：`.temp/m4b3-test.log`

### 4. dev-stack 起源码验收（③）— PASS
- 前端 `/text-tools` → HTTP 200
- 前端 `/profile` → HTTP 200（PersonalLibraryView 未在路由注册，仅 features.ts 信号引用；通过 ProfileView 间接可达）
- 后端 `GET /api/plugin/menu-items`（Bearer token）→ HTTP 200（token 有效）
- dev-stack 复用既有实例（:7301 / :7399），未起停

### 5. e2e 走查
- menu-route-consistency.spec.ts：2 passed / 2 failed（失败在 ② manifest 插件导航 + ④ mcp-center/design-system 回归——插件路由，非本批宿主屏；截图 `screenshots/e2e/m4b3-menu/`）
- app.spec.ts：global-setup 清理阶段 `cmd /c timeout` 失败（前次 e2e 临时目录锁，环境瞬态；与本批改动无关）

## 决策四要素

1. **决策**：PersonalLibraryView 的 6 个统计卡 icon 背景从蓝/紫/绿渐变改为统一金底 `--gold-tint` + 金色图标
   → 背景：原设计有 6 色渐变（蓝#3b82f6/紫#8b5cf6/绿#10b981/金#f59e0b/粉#ec4899/青#06b6d4），违反 DESIGN.md「无蓝紫渐变」三禁 + 品牌语汇（熔金仅 8%）
   → 理由：品牌一致性 > 原视觉；6 色渐变分散注意力，统一金底更符合「工具箱」克制气质
   → 准则：功能屏统计卡 icon 统一金底，不用多彩渐变

2. **决策**：canvas 成长曲线从硬编码 `#3b82f6`（蓝）改为 getComputedStyle 读取 `--gold-400`
   → 背景：原 canvas 用蓝色画线/面积渐变，与品牌熔金橙冲突
   → 理由：DESIGN.md 明确「canvas 绘制时硬编码色值，取色用 getComputedStyle」
   → 准则：所有 canvas 绘图必须从主题 token 读取颜色，不硬编码

3. **决策**：TextToolsView 删除 header 中的 emoji 🛠️
   → 背景：DESIGN.md 三禁「无 emoji 装饰」
   → 理由：PageHead 自带 eyebrow+title，emoji 多余
   → 准则：功能屏页头不用 emoji

## 未做 / 风险

- **PersonalLibraryView 未在路由注册**：该视图仅在 features.ts 信号引用，无独立路由（可能嵌入 ProfileView 或为孤儿视图）。迁移仍按冻结模式改造，但无法直接通过 URL 访问验证视觉效果
- **e2e app.spec.ts 未跑通**：global-setup 临时目录锁导致，环境问题非本批引入
- **并发批次干扰**：check/test 中存在其他批次（M4a-2 SkillsView/PluginStore、DevToolsView、WorkflowLibrary）的 error，非本批责任但影响全量门禁读数
- **FontAwesome 图标保留**：两屏仍用 `<i class="fa-solid ...">` 图标（PersonalLibraryView），未替换为 Lucide SVG——属既有功能保留范围，后续可统一治理
