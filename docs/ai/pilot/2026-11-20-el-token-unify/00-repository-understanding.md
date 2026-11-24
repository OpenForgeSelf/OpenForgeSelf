# 00 Repository Understanding — 全项目 token 规范统一（el-token-unify）

> 任务来源：2026-11-20 输入11 用户直接指令（=闸门1 依据）。本批先审计不改业务代码。
> 关联缺陷证据：输入9（UA focus outline 漏网）、输入10（SkillsView 弹窗浅色主题文字 1.06:1）——均暴露「自定义 token 固定使用/主题跟随不全」的结构问题。

## 1. 现状盘点（全部 Verified 实测/读码）

### 1.1 主题体系现状
- **主题值所有者**：`ForgeSelf.Web/src/styles/themes/workshop-forge.css`（唯一主题文件，浅色默认块 + `.dark` 暗色块；`bg-image-mode.css` 为背景图模式）。定义了两类变量：
  - `--el-*` 覆盖（Element Plus 官方变量重映射，如 `--el-border-color: #D8CFBC`）；
  - **项目自造 token 族**（`--gold-*` / `--ink-*` / `--fg-*` / `--paper-*` / `--cinnabar-*` / `--canvas-*` / `--dur-*` / `--ease-*` / `--radius-*` / `--glass-*` 等，浅/暗各一份）。
- **桥接层**：`ForgeSelf.Web/src/styles/tailwind.css` `@theme` 块定义 Tailwind 工具类所需的 `--color-*`/`--radius-*`/`--font-*`，值全部引用 `--el-*`（设计声明「颜色全部映射到 Element Plus 变量」）——但其中 `--color-accent → var(--el-color-accent)`、`--color-teal → var(--el-color-accent-teal)` 引用了 **element-plus 2.14.3 中不存在的 token**（详见 05 §3）。
- **插件侧**：15 个插件 web/src 各带独立样式；DesignSystem 插件有自建 token 体系（`Plugins/DesignSystem/web/src/styles/tokens.css`，75 个定义）；AIAgent 插件自建 `--aiw-*` 族（widget.css 55 个定义 + blocks.css 11 个）。
- **Element Plus**：2.14.3（`ForgeSelf.Web/node_modules/element-plus`），合法 `--el-*` token 共 **568 个**（`scripts/gen-element-tokens.mjs` 从 theme-chalk+dist 提取，生成 `scripts/element-tokens.json`）。

### 1.2 现有门禁与其盲区（输入9/输入10 实证 + 本次盘点）
| 门禁 | 覆盖 | 盲区 |
|------|------|------|
| `scripts/check-style-tokens.mjs` | 新增裸色值棘轮（themes/ 豁免） | 不查自定义 token 使用；存量裸色值已进基线 |
| `scripts/check-design-compliance.mjs` | token 族/胶囊/玻璃 blur/蓝紫渐变规则 A-D | 不查 token 是否存在、不查自造 token |
| PC-025 contrast 守卫（vitest） | 选中行/标签/横幅对比度 | 不查弹窗/浮层、不查 UA 样式 |
| **（本次新增）`scripts/check-theme-tokens.mjs`** | ①`--el-*` 真实性（拼写/档位错误）②非 `--el-` 自定义 token 白名单 ③硬编码颜色（hex/rgb/命名色，含 var() 兜底） | hsl() 按设计不查（上游同） |

### 1.3 自造弹窗现状（读码盘点）
- 自造 modal 实现（非 Element Plus 组件）**9 文件**：宿主 `PluginDetailModal.vue` / `MemoryView.vue` / `ScriptLibrary.vue` / `SkillsView.vue` / `WorkflowLibrary.vue`；插件 FileTools `CleanupPanel.vue` / `RenamePanel.vue`、QuickLinks `LinkFormModal.vue` / `QuickLinksView.vue`。
- 正确使用 `<el-dialog>` 的文件 9 个（两阵营并存，行为/主题一致性分叉——输入10 即其恶果实例）。

### 1.4 门禁套件引入（本批已落地）
- 自 `NewLife.Cube/NewLife.Cube.Vue/web/scripts` 复制 5 件 + fixture：`check-theme-tokens.mjs`（检查器，逻辑与上游一致）/ `gen-element-tokens.mjs`（生成 token 清单，适配本仓 pnpm workspace 搜索）/ `theme-check.mjs`（gen+check 入口）/ `custom-tokens-allow.json`（**审计期白名单为空**）/ `element-tokens.json`（生成物，568 token）/ `fixtures/theme-tokens-anti-patterns.vue` + 说明文档 `theme-tokens-check.md`。
- 灵敏度验证：对反模式 fixture 实跑 **32 处全捕获**（4 未知 el + 16 未授权自定义 + 12 硬编码），控制组零误报。
- 已加根 `package.json` 别名 `check:theme` / `check:theme:gen` / `check:theme:demo`；**未接入现有 `check`/pre-commit**（存量 6007 处违反，接入即全红——待修复批收敛后按档接入，见 03-plan）。
