# 02 Spec — 统一主题修复批（el-token 全项目收敛 + 弹窗标准化）

> 承接审计批 02-spec R1–R6 规则集（不变）；本批 = 修复执行 + 门禁接线。映射真源 = `scripts/token-migration-map.json`（338 决策）+ 本批增量（--ds-* 全迁改写、未知 el 增量、hex→el 语义映射、R6 豁免册定稿）。

## 1. Functional Requirements
- FR1 幻影 token 修复：删除 `--el-color-accent`/`--el-color-accent-teal` 的一切引用与主题内定义；使用点按映射表改 `--el-color-primary`/`--el-color-success`。
- FR2 全项目 token 收敛：按映射表把非 `--el-*` 自造 token 引用改 `--el-*`（颜色语义）/ 字面量（EP 无档位的非颜色结构值）；`--ds-*` 按用户拍板**全部迁移不走豁免**（刻度/字体内联字面量、颜色/渐变改 el 组合、迁移后删孤儿定义）。
  - **执行变更注记（2026-11-23 W7，2026-11-24 独立审查核实补记）**：`--ds-*` 执行时实证 669 处全部位于 DesignSystem 插件内部（tokens.css/showroom/mannequin 密度皮肤契约体系），迁移=自毁契约域；改判为**插件内 12 文件 fileExemptions 精确路径登记**（宿主侧 0 处、检查器同样规则复算豁免外 0），理由入 `custom-tokens-allow.json` 与 not-taken 071。原「全部迁移不走豁免」的字面要求由该实证改判收敛，属需求范围变更留痕。
- FR3 硬编码色收敛：能映射 el token 的映射；展示型开发工具功能色（ColorTool/QrCodeTool/RegexTester/TimestampTool/JwtTool）按 DESIGN.md「展示型除外须评审说明」走**文件级登记**（检查器需支持 fileExemptions）；其余零散 hex 逐个映射或登记。
- FR4 R6 豁免册定稿：`custom-tokens-allow.json` 按映射表 allowPrefixes/allowTokens 登记（--dur-*/--ease-*/--font-mono 等非颜色结构 token、EP 无对应档位），逐条带理由。
- FR5 弹窗迁移：9 个自造 modal 全迁 `<el-dialog>`（保留视觉 token 引用、行为对齐 EP：ESC/遮罩关闭、焦点管理、append-to-body 与层级）；SkillsView 弹窗对比度 ≥4.5:1。
- FR6 门禁接线：`check:theme` 入根 `check` 链（check 尾部）+ pre-commit（触前端 src 时跑，替代审计期「只记录」）+ CI；`check:theme:demo` 32 处全捕获入回归。

## 2. Input / Output
- 输入：审计批 05-evidence 173 文件矩阵 + 迁移映射表 + 各域源码现状。
- 输出：迁移后源码（预计 115+ 文件）、映射表增量、豁免册、检查器 fileExemptions 小扩展、package.json 别名 + check 链接线、pre-commit 钩子增量、CI workflow 增量、pilot 工件 00-07。

## 3. Business Rules（沿用审计批 R1–R6，摘要）
R1 只用 --el-*；R2 --el-* 必须真实存在；R3 主题文件外禁硬编码色；R4 themes/*.css 唯一值定义点；R5 浮层用 EP 组件；R6 例外登记制。桥接层 `@theme` 豁免定义、值必须只引用 --el-*/字体栈/非颜色结构值。

## 4. Boundary Conditions
- 插件独立浮窗（AIAgent agent-core）迁移后必须亮/暗走查（独立视觉基准）。
- 展示型工具的取色器/色板数据是功能数据，不属主题色。
- `--overlay-opacity`/`--aiw-off-*` 等运行时注入参数非设计 token，登记保留。
- worktree 缺件：SQLite provider 与插件 dist 按 §5.0 在 e2e 前补齐。

## 5. Error Handling
- 迁移器映射冲突/重复 → 拒绝执行（脚本内置）；--apply 前必 --dry-run 留档。
- 检查器对不可解析输入（缺 element-tokens.json）→ 报错退出，不静默通过。

## 6. Compatibility
- Element Plus 2.14.3 不动；主题 CSS 变量对外名（--el-*）不变；插件构建产物仅样式引用变化，不改契约/接口/路由/实体。

## 7. Non-functional Requirements
- 亮/暗两态关键页面（宿主 5 视图 + AIAgent 浮窗 + DesignSystem 工作台 + SkillsView 弹窗）截图走查无回归；对比度 ≥4.5:1；`pnpm run check`/`test`、涉改插件 build、1 冒烟 e2e/插件、中档全量 dotnet test、深档全量 e2e 全绿（基线红对表后判责）。

## 8. Interaction Design（弹窗迁移交互规格）
| # | 触发 | 结果/状态/反馈 |
|---|------|----------------|
| I1 | 宿主 PluginDetailModal：插件列表行「详情」 | el-dialog 打开（title=插件名），ESC/遮罩/×关闭；内容区滚动，footer 保留操作按钮；关闭后焦点回触发行 |
| I2 | 宿主 MemoryView/ScriptLibrary/SkillsView/WorkflowLibrary 查看类弹窗 | 同 I1 模式：el-dialog + preserve 视觉 token；空态/加载态沿用既有 EmptyState/骨架；无自造遮罩层叠问题（EP zIndex 统一管理） |
| I3 | FileTools CleanupPanel/RenamePanel 确认/重命名弹窗 | el-dialog；确认按钮走 EP loading 态防重复提交；ESC 关闭等价取消 |
| I4 | QuickLinks LinkFormModal/QuickLinksView 编辑弹窗 | el-dialog + 表单校验不变；提交成功自动关闭并刷新列表 |
| I5 | 通用验收 | 弹窗内文字在浅/暗两态对比度 ≥4.5:1（SkillsView 1.06:1 消除）；弹窗打开时背景滚动锁定（EP 内建）；多弹窗不重叠错层 |
| 说明 | 本批无新增交互入口，全部为「同视觉、换实现」；上表为迁移后交互验收标准，走查逐项核对 |

## 9. Failure Modes（v1.2.0 缺陷类节）
- PC-025（暗色/浅色对比度）：本批 SkillsView 弹窗命中同根因（自造 modal 无对比度承载）→ canonical fix = 迁 el-dialog + 语义 token；同类扫查=9 弹窗全迁后逐弹窗读图对比度。
- 审计批 R2 记录的 92→107 未知 el 增量：迁移后重跑检查器逐条归零，不许留「未知 el」。
- 灾后双改风险：57 个双改文件不整文件搬，全部走「映射器重放 + 手工合并」姿势。

## 10. Unknown（显式记录）
- 浅色 --gold-400→--el-color-primary（#B45309）的品牌色观感收敛：走查确认（既定取舍，非缺陷）。
- DesignSystem tokens.css 定义删除后插件 UI 视觉等值性：e2e + 走查确认。
- 深档全量 e2e 基线红（82 failed 历史）：对表 `project-baseline-test-reds` 后判责，非本批红才归本批。

## 11. Acceptance Criteria
- AC1 `node scripts/check-theme-tokens.mjs` 全项目 0 违反（豁免册内项不计），退出码 0。
- AC2 `check:theme:demo` 对反模式 fixture 报 32 处全捕获（4 未知 el + 16 自定义 + 12 硬编码）。
- AC3 9 个自造 modal 文件 0 自造遮罩实现（读码盘点），均 `<el-dialog>` 实现。
- AC4 SkillsView 弹窗浅/暗两态对比度 ≥4.5:1（走查截图 + 读图）。
- AC5 `pnpm run check` + `pnpm run test` 全绿；涉改插件 `pnpm run build` 全绿。
- AC6 每涉改插件 1 条冒烟 e2e 绿；DesignSystem 四层门禁 ①②③ 过（④发布须用户授权，本批不执行并留痕）。
- AC7 中档全量 `dotnet test`：相对基线无新增红（对表判责）；深档全量 e2e：4 worker、--output 空目录、基线对表无新增红。
- AC8 `check:theme` 接入 check 链 + pre-commit + CI；亮/暗两态走查截图存档无回归。
