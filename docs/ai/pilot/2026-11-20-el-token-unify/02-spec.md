# 02 Spec — 全项目 token 规范统一

> 交互设计节：本批为样式规范治理 + 审计，无新用户可见交互；修复批的验收含亮/暗双态视觉走查（见 §5 验收标准）。

## 1. 规则集（修复批目标态 · 机器可判）

| # | 规则 | 判定 | 依据 |
|---|------|------|------|
| R1 | 页面/组件样式只用 `--el-*` token | `check-theme-tokens.mjs` 规则②（白名单空）报 0 | 输入11 用户方案 |
| R2 | `--el-*` 必须真实存在（element-plus 2.14.3 的 568 个） | 规则①报 0（现存 92 处引用了不存在的 token） | 同上 |
| R3 | 主题文件外禁硬编码色值（hex/rgb(a)/命名色/`var(--x, 兜底色)`） | 规则③报 0（现存 1522 处） | 输入11 + DESIGN.md「禁裸 hex/rgb」升级为全量 |
| R4 | 主题文件（`src/styles/themes/*.css`）是唯一值定义点 | 忽略清单只含 themes/*.css | 输入11「主题文件负责控制具体的值」 |
| R5 | 浮层/弹窗用 Element Plus 组件（el-dialog/el-drawer），禁自造 modal | 读码盘点（现存 9 个自造弹窗文件） | 输入11「尽量使用已有组件避免自己造轮子」 |
| R6 | 例外登记制：确需保留的非颜色结构性 token（如动效 `--dur-*`/`--ease-*`）须登记 `custom-tokens-allow.json` 并经用户批准 | 白名单文件 diff | 上游门禁设计（未审批 token 即报错） |

## 2. 边界与豁免（谁可以不守 R1）
- `ForgeSelf.Web/src/styles/themes/*.css`：值的所有者（R4 豁免）。
- `ForgeSelf.Web/src/styles/tailwind.css` 与 `Plugins/*/web/src/styles/tailwind.css` 的 `@theme` 块：**Tailwind 工具类桥接层**（`--color-*` 等必须本地命名才能生成 utility class）——豁免**定义**、但值必须只引用 `--el-*`/字体栈/非颜色结构值；其中引用不存在 el token 的两行（`--el-color-accent`、`--el-color-accent-teal`）**必须修**（改映射到真实 token 或删除对应工具类）。
- 测试文件（`*.test.ts`/`*.spec.ts`）与 `.d.ts`：检查器忽略（非页面）。
- `ForgeSelf.Web/ct/` 组件画廊与 `forgeself-design/` 历史原型：不在扫描目标（画廊若引入新色值仍受 `check-style-tokens` 棘轮约束）。
- 展示型开发工具组件（devtools ColorTool/RegexTester/JwtTool/QrCodeTool/TimestampTool 等，取色器/色板类）：DESIGN.md 既有「展示型开发工具除外须评审说明」条款继续适用——修复批逐个评审：能映射 el token 的映射，确属功能数据（如取色器预置色板）的登记说明。

## 3. 映射原则（自造 token → el token，修复批执行）
- 颜色类一律等值映射进 themes/*.css 的 `--el-*` 重定义（如 `--gold-400 #F59E0B` → `--el-color-primary`；`--ink-850 #1B1815` → `--el-bg-color-overlay`（暗色）/弹层场景；`--fg-primary` → `--el-text-color-primary` 等），使用点改引用 `--el-*`；**语义对不上**的（如金/墨双色品牌语义与 el 单主色模型的差异）在 themes 内用 `--el-` 扩展段承载并在 03-plan 列映射表逐条过闸门。
- 尺寸/圆角/动效：优先 `--el-border-radius-*`/`--el-transition-*`；el 没有的档位（如 `--dur-fast`）→ R6 登记或内联标准值。
- 插件自建体系（DesignSystem `--ds-*` 75 定义、AIAgent `--aiw-*`）：DesignSystem 是设计系统插件、其 token.css 是**产品功能本体**（对外展示 token 体系）——单独拍板：豁免登记 or 迁移；AIAgent widget 独立浮窗样式随修复批迁移。

## 4. Unknown / 待拍板
- `--gold-*`/`--cinnabar-*` 品牌色映射到 `--el-color-primary` 后，暗色主题观感是否等值——修复批亮/暗走查确认（Unknown）。
- DesignSystem 插件 token 体系豁免还是迁移（用户拍板项）。
- `--dur-*/--ease-*` 动效 token 去留（R6 登记候选，用户拍板项）。

## 5. 验收标准（修复批）
1. `node scripts/check-theme-tokens.mjs` 退出 0（白名单内 0 处或仅 R6 登记项）。
2. `check:theme` 接入根 `check` 链 + pre-commit，全绿。
3. 亮/暗两态关键页面走查（含输入10 的 SkillsView 弹窗）截图对比无回归、对比度 ≥4.5:1。
4. `pnpm run check` + `pnpm run test` 全绿；涉及插件各跑 1 冒烟 e2e。

## 12. Interaction Design（无界面交互显式声明）
本批为纯审计与门禁套件批：无新增 UI/交互入口，不产出任何用户可见界面；产物 = 违规矩阵文档 + 检查器脚本 + fixtures 样本。门禁套件通过命令行入口（node scripts/...）操作，入参=文件路径/目录，输出=违反点清单与退出码；对不可解析输入报错退出不静默（见 §5）。符合 AGENTS §11.2「无界面交互」显式声明要求。
