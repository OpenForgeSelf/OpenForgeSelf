# 00 Repository Understanding — 统一主题修复批（el-token 全项目收敛 + 弹窗标准化）

## 项目用途与结构
- 铸己匣 OpenForgeSelf：个人可扩展工具平台。宿主 SPA（`ForgeSelf.Web/`，Vue 3.5 + Vite 6 + TS 5.7 + Element Plus 2.14.3 + Tailwind 4 + Pinia）+ ASP.NET Core API（`ForgeSelf.Api/`，.NET 10 + SQLite + NewLife.XCode）+ 插件架构（`Plugins/<X>/` + `plugin.json`，插件 web 产物经宿主 import map 远程加载、与宿主同 DOM 共享 `:root` 上的 `--el-*` 变量）。
- 测试体系（唯一）：宿主 `pnpm run check`（vue-tsc+eslint）/ `pnpm run test`（vitest）；插件前端 check/test/build 借宿主工具链（`cd Plugins/<X>/web && pnpm run check/test/build`）；后端 `dotnet build`/`dotnet test`；e2e = 单 Playwright 配置 + globalSetup（构建宿主→publish 宿主→前端 dev→真实 token），应用层 `e2e/*.spec.ts` + 插件层 `e2e/plugins/<id>/`。
- 主题体系真源：`ForgeSelf.Web/src/styles/themes/*.css`（检查器忽略清单=值所有者）；桥接层 `tailwind.css` 的 `@theme` 块把 `--color-*` 等本地命名引到 `--el-*`；插件侧自建体系：DesignSystem `--ds-*`（web/src/styles/tokens.css）、AIAgent `--aiw-*`（agent-core/styles/widget.css）、SmartTerminal `styles.css` 内嵌刻度。

## 本批依赖的前批事实（已核实，2026-11-23）
- 审计批 `docs/ai/pilot/2026-11-20-el-token-unify/`（00-05 齐，未提交，在主检出工作区）已交付检查器五件套：`scripts/check-theme-tokens.mjs`（R1 白名单 / R2 未知 el / R3 硬编码色）、`element-tokens.json`（EP 2.14.3，568 token）、`custom-tokens-allow.json`（当前空）、`gen-element-tokens.mjs`、`fixtures/theme-tokens-anti-patterns.vue`。
- 迁移器两件仅存灾前备份 `..\OpenForgeSelf-backup`：`scripts/token-migration-map.json`（338 token 映射决策：phantomEl 15 + rename 大表 + allowPrefixes/allowTokens 豁免册 + deleteDef + functionalColorExemptions）与 `scripts/migrate-theme-tokens.mjs`（--dry-run/--apply/--scope/--rules/--prune-defs）。本批拷入并入库。
- 基线实测（本 worktree bb59154，2026-11-23 15:00）：`node scripts/check-theme-tokens.mjs` = **6021 处违反 / 16 域**：自定义 4392、硬编码 1522、未知 el 107；分布 DesignSystem 1813、宿主 components 1455 + views 1107 + styles 26 + App.vue 10 + data 4、AIAgent 839、Sems 193、ImGateway 166、SmartTerminal 138、Home 107、FileTools 65、ToolBridge 33、McpCenter 28、TodoTracker 24、AgentHub 13（日志 `.temp/theme-baseline.log`）。
- dry-run 实测：迁移器按现映射可机械替换 **111 文件 / 2971 处**，未映射仅 2 处（均为注释内 `var(--ds-…)` 示意串，检查器正则不命中，无实害）。
- themes 供给现状：`workshop-forge.css` 提交版已含浅/暗两块 `--el-*` 重定义且值与旧自造 token 等值对齐（--gold-400→--el-color-primary 暗值同为 #F59E0B；--fg-primary→--el-text-color-primary、--ink-850→--el-bg-color-overlay 暗值同为 #1B1815 等）；仍供给 4 行幻影 token（浅 138-139 / 暗 230-231 的 `--el-color-accent`、`--el-color-accent-teal`）待删。
- 备份 diff 核实：备份版 workshop-forge.css 仅多出 `--canvas-*` 12 档调色板块（属另一在途批 theme-canvas-unify，不归本批，不搬）。
- 已知弹窗缺陷：SkillsView 自造弹窗浅色文字对比度 1.06:1（输入10 实测，PC-025 同类）；TODO 在册「EP 状态横幅暗色 <4.5:1」为另一主题级欠账，不归本批。

## 测试/门禁形态
- pre-commit：触前端 src 必跑 design:check 棘轮（本批净减方向）；触 pilot 目录必跑工件链（-EnforceHeader -EnforceAcCoverage -EnforceInteraction）+ feature 回写 + 问题类（defect 类）。
- `check-problem-classes.ps1` 台账命中：PC-025（对比度）与本批 SkillsView 弹窗修复同域——修复动作即 canonical fix（迁 el-dialog 后由 EP 规范对比度承载），在 02 Failure Modes 预填。

## 候选任务与选择理由
本批即任务本身（横切样式规范治理修复批），无另选低风险任务；风险源与缓解见 03-plan §风险。
