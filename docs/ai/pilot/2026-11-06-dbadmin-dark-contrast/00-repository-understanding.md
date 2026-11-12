# Repository Understanding

> 阶段：Stage 0（动手写代码之前必须完成）｜规范：docs/04-standards/ai-native-engineering-workflow.md §2
> 原则：所有条目来自真实仓库内容；本批只触碰 UI token、规范文档、前端测试与 DbAdmin 走查文档。

## 项目结构

- 仓库根：`ForgeSelf.slnx`、`AGENTS.md`、`DESIGN.md`、`TODO.md`、`docs/`、`scripts/`。
- 前端宿主：`ForgeSelf.Web/`（Vue 3 SPA + 主题 CSS + Playwright e2e）。
- 后端宿主：`ForgeSelf.Api/`（ASP.NET Core + XCode + 插件装载）。
- 插件：`Plugins/<PascalCase>/`（本任务相关 `Plugins/DbAdmin/`、`Plugins/AIAgent/`）。
- 开发工件：`docs/ai/pilot/YYYY-MM-DD-<task-id>/`；问题类台账：`docs/11-troubleshooting/`。

## 技术栈

| 层 | 技术 | 依据（文件/配置） |
| --- | --- | --- |
| 宿主前端 | Vue 3.5 + Vite 6 + TS 5.7 + Element Plus + Tailwind 4 + Pinia + pnpm | `ForgeSelf.Web/package.json` |
| 插件前端 | 独立 Vite lib 构建，`web/dist/index.js` 远程加载 | `Plugins/DbAdmin/web/package.json`、`plugin.json` |
| 测试 | vitest（单测）+ Playwright e2e（真实 publish 宿主） | `ForgeSelf.Web/package.json`、`ForgeSelf.Web/e2e/**` |
| 主题 | 单文件 token 层 `workshop-forge.css`（`--el-*` 与设计 token） | `ForgeSelf.Web/src/styles/themes/workshop-forge.css` |

## 架构特点

- 主题切换：`stores/theme.ts` 写 `html.dark` + `data-theme`；默认模式 `system`（`theme.ts:71-74`），CI/Playwright 默认解析为 **light**。
- 插件 UI 与宿主共用 `--el-*` 变量；`workshop-forge.css` 是唯一的 `--el-*` 覆盖层（`DESIGN.md:130-140`）。
- 设计真源：`DESIGN.md`（项目级 UI/UX 规范）+ 高保真参考稿；设计稿 token 文件 `forgeself-design/themes/workshop-forge.css` 在暗色下定义 primary light-3..9 为**逐级更暗**（115-116 行 `light-8:#78350F`、`light-9:#451A03`）。
- 现状实现与真源漂移：`ForgeSelf.Web/src/styles/themes/workshop-forge.css:188-194` 暗色 primary light-3..9 被写成**逐级更亮**（`light-8:#FDE68A`、`light-9:#FEF3C7`），与设计真源及 Element Plus 暗色语义（light-N = 背景级深色）相反。

## 测试方式

- 宿主前端：`cd ForgeSelf.Web && pnpm run check && pnpm run test && pnpm run design:check`。
- 插件前端：`cd Plugins/<X>/web && pnpm run build`（沙箱内可用 `scripts/build-plugin-web.ps1`）。
- 插件 e2e：`pnpm exec playwright test --config=playwright.config.ts e2e/plugins/<id>`（零 mock，真实宿主 + token）。
- 后端：`dotnet build` / `dotnet test`（本批不改后端代码，仍按插件闭环跑构建与插件过滤集）。

## 构建命令

```bash
cd ForgeSelf.Web && pnpm run check
cd ForgeSelf.Web && pnpm run test
cd ForgeSelf.Web && pnpm run design:check
cd Plugins/DbAdmin/web && pnpm run build
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/db-admin
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| `ForgeSelf.Web/src/styles/themes/` | 主题 token 唯一实现层（`--el-*` 覆盖、设计 token） |
| `ForgeSelf.Web/src/stores/theme.ts` | 主题模式持久化与 `html.dark` 应用 |
| `ForgeSelf.Web/e2e/plugins/db-admin/` | DbAdmin 插件冒烟 e2e |
| `Plugins/DbAdmin/web/src/DbAdminView.vue` | DbAdmin 单文件视图（含连接树与选中态样式） |
| `.agents/skills/ui-ux-design/SKILL.md` | 走查 UI 符合性清单（对比度/主题专项） |
| `docs/04-standards/agent-workflow/b-engineering-daily.md` | UI 批（含缺陷修复）的工程规则 Part B |
| `docs/18-templates/ai-pilot/02-spec.tpl.md` | Spec 的 Interaction Design / 视觉规格模板 |

## 代码组织方式

- DbAdmin 视图为单文件 SFC，`<style scoped>` 在 `DbAdminView.vue:991-1142`；连接节点 `.node`/`.node.on` 定义在 `1023-1025`。
- `run.on` 选中背景直接使用 `--el-color-primary-light-8`（`DbAdminView.vue:1025`），文字继承 `.dba-root` 的 `--el-text-color-primary`（`1000`）。
- AIAgent 运行记录的人类行使用 `color: var(--el-color-primary-light-7)`（`RunRecordPanel.vue:1363`），属同根因的「把 light-N 当文字色」写法。

## 现有工程规范

- `AGENTS.md` §8：设计门禁三步（静态守卫 / Spec 交互节 / 走查符合性）；§11：AI-Native 九阶段 + 闸门。
- `DESIGN.md:181,389`：文字对比度 ≥4.5:1（正文）、3:1（大号文字/图标）；`DESIGN.md:428-431` 门禁三步。
- `docs/04-standards/agent-workflow/b-engineering-daily.md:209-212`：UI 批（含纯缺陷修复）必须「目标态设计先行 + 全项走查留痕 + 对比度实测（亮/暗）」。
- `.agents/skills/ui-ux-design/SKILL.md:160-176`：D/D-1 对比度与文字不可见专项；`:192-196` E 插件前端主题专项。
- `docs/18-templates/ai-pilot/02-spec.tpl.md:37-44`：视觉规格节要求「逐表面 × light/dark 矩阵」，`02-spec.tpl.md:55`：对比度数值进 05-evidence。
- 规范**已有**对比度条款，但缺少「主题 token 明暗语义」不变量与「选中行/标签/横幅底」的具体走查项；e2e 默认 light 主题，暗色未被自动化覆盖。

## 候选低风险任务

- （已选）修复暗色 `--el-color-primary-light-N` 倒置 + 同类 `light-N` 文字用法 + 补规范/走查/守卫。
- 备选：仅修 `.node.on` 单点（否决：同类 token 仍影响标签/横幅/按钮，且守卫无法覆盖）。

## 选择该任务的原因

- 用户输入1 明确要求「先检查规范与走查验收标准 → 完善规范流程 → 修复问题」。
- 根因可复现、可用 token 不变量与 e2e 对比度断言做机器守卫；影响面虽涉及宿主主题，但只改 token 语义，无后端/契约/数据变更。

## 适用问题类（Stage 0 必填）

- 检索 `docs/11-troubleshooting/problem-classes/README.md` tags 表：无「UI 主题/对比度」行；台账中 PC-019（布局）与 PC-024（轮询）不覆盖本类。
- 结论：**未命中已有 PC**；本批按 problem-class-closure P2 新建 **PC-025**（暗色 light-N token 倒置 / 文字不可见）。历史同日 TODO 曾记录 IM 网关暗色对比度修复（2026-10-15，未立 PC），本批按「二次复发类」执行逃逸分析并强制 ≥G2 守卫。
