# 03 Plan — 全项目 token 规范统一（修复批执行方案，本批只审计）

> 修复批开工前置：用户看过 05-evidence 违反清单并拍板 §5 三个待拍板项。

## 1. 分域收敛顺序（按「违反密度 × 回归风险」排序）
| 域 | 违反量 | 策略 |
|----|--------|------|
| ① 宿主桥接层 | tailwind.css 26 + App.vue 10 | 最先修：修 2 处幻影 token（`--el-color-accent`/`--el-color-accent-teal` 不存在），@theme 块按 R6 登记；建立**映射表真源**（下表 §2） |
| ② 宿主 themes 补齐 | workshop-forge.css | 把自造 token 的值等值搬进 `--el-*` 重定义（浅/暗两块），形成全量 el 侧供给 |
| ③ 宿主 views/components | 2588 处/96 文件 | 按映射表机械替换（top 文件 ProfileView 107 / MemoryView 96 / SkillsView 90 / WorkflowLibrary 90 / ScriptLibrary 84 / ChatView 74 / PluginImportExport 77）；每替换一域跑亮/暗走查截图 |
| ④ 插件（非 DesignSystem） | AIAgent 839 / Sems 193 / ImGateway 166 / SmartTerminal 138 / Home 107 / FileTools 62 等 | AIAgent widget.css(454)+blocks.css(189)+assistant.css(109) 为独立浮窗样式，优先迁 el token；各插件改完按 plugin-development 跑门禁+1 冒烟 e2e |
| ⑤ DesignSystem 插件 | 1813 处 | **待拍板④**：其 `--ds-*` 是插件产品本体（对外展示 token 体系）——推荐豁免登记（白名单 `--ds-` 前缀），不走 el 迁移 |
| ⑥ 弹窗迁移（R5） | 9 个自造 modal 文件 | 换 `<el-dialog>`（保留现有视觉 token 引用），SkillsView 弹窗随之解决输入10 的 1.06:1 缺陷 |
| ⑦ 门禁接线 | scripts | 违反清零后：`check:theme` 接入根 `check` 链 + pre-commit + CI；反模式 fixture 纳入回归（check:theme:demo） |

## 2. 映射表（初始版，修复批在 themes 补齐时定稿）
| 自造 token（使用量） | 目标 |
|---|---|
| `--gold-400`(228) / `--gold-300` / `--gold-tint*` | `--el-color-primary`（值 #F59E0B 已是主题主色）+ `--el-color-primary-light-*`/`color-mix` 派生 |
| `--fg-primary`(151) / `--fg-secondary`(143) / `--fg-tertiary`(102) / `--fg-muted` | `--el-text-color-primary` / `-regular` / `-secondary` / `-placeholder` |
| `--ink-850` / `--ink-950` | `--el-bg-color-overlay`（暗值恰同为 #1B1815）/ `--el-bg-color-page`（暗）或场景化映射 |
| `--radius-md`(106) / `--radius-sm`(80) | `--el-border-radius-base` / `-small`（tailwind.css 已如此映射） |
| `--ease-out`(121) / `--dur-fast`(109) 等 | **待拍板③**：el 无动效时长 token → R6 登记 `--dur-*`/`--ease-*`（非颜色、结构稳定）或内联 |
| `--font-mono`(139) | `--el-font-family` 不可承载等宽 → R6 登记 `--font-mono`（字体栈非颜色） |
| `--cinnabar-400`(95) / `--bronze-500`(54) / `--paper-tint`(71) | 破坏性→`--el-color-danger`；品牌辅助→`--el-color-*` 派生或 R6 登记 |
| `--aiw-*`(AIAgent) / `--ds-*`(DesignSystem) | 域④迁移 / 域⑤豁免 |
| 边界情况：语义对不上 el 模型的 | themes 内 `--el-` 命名空间扩展段承载（仍满足「值只在主题文件」），逐条过闸门 |

## 3. 验证分档（修复批每域）
- 快档（每批必跑）：`pnpm run check` + `pnpm run test` + `node scripts/check-theme-tokens.mjs`（违反数只许降不许升）+ 本插件 1 冒烟 e2e（涉插件时）。
- 亮/暗双态走查：每域收敛后 `dev-stack` 截图对比（输入10 教训：暗色正常≠浅色正常）。
- 全部收敛后：中档（`scripts/**` 已动 → 全量 `dotnet test`）+ 深档（全量 e2e）。

## 4. 回滚点
- 每域一 commit（闸门3 需用户批准）；token 替换纯机械可 `git revert` 单域回滚。
- themes 补齐先于使用点替换：任一时刻页面引用的 token 在新旧两套同时可用。

## 5. 待拍板（≤3 项，均带推荐）
1. **DesignSystem `--ds-*` 体系**：推荐「豁免登记」（白名单 `--ds-` 前缀）——它是设计系统插件的产品本体，迁 el 反而破坏其展示语义。
2. **动效/字体等非颜色结构 token（`--dur-*`/`--ease-*`/`--font-mono`，约 400 处使用）**：推荐「R6 白名单登记」——el 无对应 token，内联会丢主题一致性。
3. **弹窗迁移范围**：推荐 9 个自造 modal 全迁 `el-dialog`（一次灭掉输入10 类缺陷 + 行为统一）；若担心视觉回归可先迁宿主 5 个。
