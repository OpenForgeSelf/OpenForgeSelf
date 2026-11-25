# mini-task：顶部栏页签左右间距调小 → 页签 logo/关闭图标同位互换重设计

- date: 2026-11-25
- task-id: `topbar-tab-gap`
- feature: 壳层（ForgeSelf.Web TopNavbar，无 02-features 单页映射，宿主壳层批）
- type: 轻量 UI 改版（间距收紧 + 图标交互模型重设计）｜裁剪依据：规范 §4 轻量（2 文件：TopNavbar.vue + 守卫测试），Intent/Spec/Plan/Task 合并本文件，05/06 单独产出
- 闸门1 处理：用户直接指令两轮（输入28 原文 + 追加轮「效果不行，优化下，删除图标放到右侧，页签默认展示插件对应logo，悬浮时，页签对应插件或者功能的logo变成关闭图标」即需求与验收口径）+ AGENTS §8.5 自动迭代。无接口/契约/依赖变更，git 可回滚。

## 零、需求变更记录（v1 → v2 → v3）

- v1（10:13）：页签左右边距调小。已实施：padding 14→9、下划线 inset 12→7；量测 30→20px（无槽边界）/ 52→42px（带槽边界）。
- v2（10:31 用户反馈「效果不行」）：改交互模型——页签默认在**右侧图标槽**显示功能/插件 logo，悬浮时 logo 同位变为关闭/取消钉住图标；删除图标置于右侧。v1 的 padding 收紧保留（辅助）。守卫测试随不变量载体改造（槽从 `.close-x` 自身变为 `.tab-icon-slot` 盒，不变量语义不变：常驻占位 + 显隐语义切换 + 零位移）。
- v3（14:10 用户反馈「图标在左，而不是右」）：图标槽（logo⇄动作图标互换面）从标签**右侧**移到**左侧**（[logo][label] 浏览器页签式布局）。仅模板调序；互换机制、槽规格、守卫断言、零位移性质均不变。

## 一、Intent（为什么 / 做什么 / 到什么程度）

- **Problem**：用户反馈「页签之间左右边距太大了」。实测：`.tab-item` 水平 padding 14px + `.tab-nav` gap 2px ⇒ 相邻页签文字间距 30px；hover 胶囊两侧留白偏空。
- **Why**：输入28 直接要求调小。
- **Expected Outcome**：页签左右 padding 收紧，相邻页签文字间距明显变小；hover/active 视觉（胶囊、下划线）保持既有语汇，无换行/截断/溢出。
- **Constraints**：只动 TopNavbar.vue 间距数值；不动 gap（2px 已很紧）、不动图标槽宽（输入22 批守卫面）、不动颜色/主题、不动交互逻辑。
- **Success Criteria**：AC-1 padding 收紧后守卫测试仍绿（图标槽常驻预留不被破坏）；AC-2 走查量测：相邻页签文字间距 ≤22px（现 30px）；AC-3 快档全绿；AC-4 hover/active 视觉正常（截图读图）。

## 二、Spec

### Functional Requirements

**v1（已实施，保留）**
- FR-1 `.tab-item` 水平 padding 14px → 9px（垂直 6px 不动）：相邻页签文字间距 30px → 20px（无槽边界），hover 胶囊保留 9px 侧留白。
- FR-2 `.tab-item.active::after` 左右 inset 12px → 7px：维持「下划线比文字宽 2px」的既有关系（14−12 = 9−7）。

**v2（logo/关闭图标同位互换；v3 修订位置）**
- FR-3 非首页页签在**标签左侧**渲染常驻 16px 图标槽（`.tab-icon-slot`，flex:none；v2 曾置于右侧，v3 按用户指令移左）——布局 `[槽][标签]`：默认显示该页签对应功能/插件的 logo——按 `tab.path` 在 `mergeFeatureList(manifestStore.menus)` 中匹配 `FeatureItem`，经 `lucideIconSvg(icon, 14)` 渲染内联 SVG（数据单一源 = `src/data/featureIcons.ts`；插件菜单图标缺省 `package`，未知图标名渲染空占位不报错）。首页签（`/`）不渲染槽（不可关闭，无 logo）。
- FR-4 悬浮（`:hover`）或键盘聚焦（`:focus-within`）时，槽内 logo 隐、动作图标现——**同位互换**：动作图标绝对定位于同一 16px 盒内（钉住项 = 📌 取消钉住，动态项 = × 关闭，语义沿用），宽度恒定 ⇒ 零位移。显隐一律 `visibility + opacity`（160ms `--dur-fast`/`--ease-out`），禁止 `display` 切换（PC-042 不变量）。
- FR-5 无匹配功能/插件的页签（如 `/all-features` 等工具页）：槽仍渲染（空闲时留空），悬浮仍显示 ×（保证可关闭且零位移）。

### Interaction Design（ui-ux-design 交互规格模板）

| 交互点 | 触发 | 结果（点什么出现什么） | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| 页签 logo | 无交互（静态） | 非首页签**左侧** 16px 槽常驻显示功能/插件 lucide 图标（14px，`currentColor` 跟随文字色）；首页签无图标 | 静态布局 | 不涉及 | 无匹配功能 → 槽留空 | 文字不换行不截断 |
| logo → 关闭图标 | 悬浮页签 / 键盘聚焦 | 右槽 logo 160ms 淡出、动作图标（×/📌）同位 160ms 淡入；页签及后续页签零位移 | 同位互换（两图标同一 16px 盒，visibility/opacity 切换） | × 悬浮自身有底色反馈（沿用既有） | 不涉及 | 点 × = 关闭页签（钉住项点 📌 = 取消钉住）；160ms 内快速进出不残留（visibility 延迟翻转） |
| 页签间距 | 无交互（静态收紧） | padding 9px：相邻页签间距收紧 | 静态样式 | 不涉及 | 不涉及 | 溢出走 tab-nav 横向滚动 |

- 五态：静态布局 + hover 互换面；加载/空/出错/无权限态不涉及（纯壳层样式）。键盘可达（focus-within 等效 hover）保留。
- 走查符合性：量测（页签宽 hover 前后零变化、logo 渲染、间距数字）+ 亮暗两态截图读图（图标颜色走 currentColor，无独立色值；按 §8.3 涉主题改动双态走查）。

### Boundary / Compatibility / Failure Modes
- 兼容：仅 TopNavbar.vue + 守卫测试；`lucideIconSvg` 为既有单一源（AllFeaturesView/PluginStore 已用）；`v-html` 内容全部来自仓内静态 SVG 表（无用户输入，无 XSS 面）；插件菜单 icon 缺省 `package`。
- Failure Modes：PC-042（悬浮宽度跳变）——结构上根除：两图标同一盒且盒宽恒定，任何状态布局零变化；PC-019（flex 压缩）——槽 `flex:none`。其余 PC 无命中。
- Unknown：无。

## 三、Plan（真实文件）

| 文件 | 动作 | 理由 |
| --- | --- | --- |
| `ForgeSelf.Web/src/components/TopNavbar.vue` | ① v1：padding 14→9、下划线 inset 12→7（已实施）；② v2：模板改「label + `.tab-icon-slot`(logo⇄×同位互换)」；script 增 `tabLogo(path)`（mergeFeatureList + lucideIconSvg）；样式增槽/logo 规则、`.close-x` 改绝对定位覆盖 | 用户两轮指令 |
| `ForgeSelf.Web/src/__tests__/TopNavbar.test.ts` | 守卫不变量载体改造：`.tab-icon-slot` 常驻 16px/flex:none + `.close-x` 绝对定位/visibility 显隐/无 display 切换 + logo hover 隐（visibility 语义）+ DOM 槽/logo/× 计数 | 不变量语义不变，载体类名/结构变 |

- Test Plan：① 守卫回归（改造后守卫套件红→绿）；② 快档：`pnpm run check` + `pnpm exec vitest run` + `pnpm run design:check`；③ 走查：dev:web + 量测（页签宽 hover 前后、logo 渲染、相邻间距）+ 亮/暗截图读图。
- 守卫等级：**G1 组件守卫改造**（随不变量载体更新；PC-042 不变量语义保持：常驻占位 + 显隐语义切换 + 零位移）。

## 四、Task

- **Scope Allowed**：上表 2 文件 + pilot 工件 + 日记/TODO 归档。
- **Scope Forbidden**：tabs store 数据结构、featureIcons.ts、其他组件、全局 tokens、e2e 基建、后端。
- **Acceptance Criteria**：
  - [x] AC-1 守卫套件（改造后）绿：槽常驻/同位互换/无 display 切换/零位移不变量全断言（9/9，见 05 V5）
  - [x] AC-2 走查量测：非首页签右侧 logo 渲染（智能体=bot 等 4/4）；hover 前后页签宽度零变化；相邻页签间距 30→20px（见 05 V7/V8/V3）
  - [x] AC-3 快档全绿：check / vitest 1278 全绿 / design:check（见 05 V6）
  - [x] AC-4 亮/暗两态截图读图：logo、hover 互换、active 下划线视觉正常（见 05 V9）
- **Verification Commands**：
  - `pnpm exec vitest run src/__tests__/TopNavbar.test.ts`（ForgeSelf.Web/）
  - `pnpm run check`；`pnpm exec vitest run`；`pnpm run design:check`
  - 走查：根 `pnpm run dev:web` + 浏览器量测 + 截图

## 五、适用问题类（v1.2.0 轻量任务栏位）

- 2026-11-25 按 tags 检索 `docs/11-troubleshooting/problem-classes/README.md`：无命中（本批为视觉微调非缺陷，不触发问题类闭环；PC-042 槽位不变量经守卫回归覆盖）。
