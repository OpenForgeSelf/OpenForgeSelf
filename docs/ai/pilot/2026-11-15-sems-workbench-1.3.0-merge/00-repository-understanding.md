# Repository Understanding

> 阶段：Stage 0 — Repository Understanding（动手写代码之前必须完成）｜规范：`docs/04-standards/ai-native-engineering-workflow.md` §2（v1.2.0，强制）
> 任务 ID：`2026-11-09-sems-workbench-1.3.0` ｜ 日期：2026-11-09
> 上游依据：`AGENTS.md` §0 预飞铁律 / §5 验证门禁 / §11 九阶段闭环；`docs/04-standards/ai-native-engineering-workflow.md`；`.agents/skills/plugin-development/SKILL.md`（总纲 + §四 维护闭环六步）；完整仓库源码树；`.forgeself/memory/2026-11-09.md`；仓库根 `TODO.md`；4 份评审基线报告
> 原则：所有条目来自**真实仓库内容**，禁止凭常识推测；未执行的验证一律不写成 PASS。

---

## 项目结构

- 开发落点：完整仓库（纯源码树，无 `.git`）。
- 根级组成：`ForgeSelf.slnx`、后端宿主 `ForgeSelf.Api/`、前端 `ForgeSelf.Web/`、后端测试 `ForgeSelf.Api.Tests/`、`ForgeSelf.Abstractions/`、`ForgeSelf.Core/`、`ForgeSelf.Bootstrapper/`、插件源码 `Plugins/`（含本任务目标 `Plugins/Sems/`）、构建脚本 `scripts/`、文档 `docs/`、流程工件 `docs/ai/pilot/`、模板 `docs/18-templates/ai-pilot/`、技能 `.agents/skills/`。
- 关键守卫文件（本任务强相关，已实测存在）：`scripts/check-design-compliance.mjs`、`scripts/check-style-tokens.mjs`、`scripts/design-compliance-baseline.json`、`ForgeSelf.Web/e2e/plugins/sems/sems.spec.ts`。
- 本任务工件目录：`docs/ai/pilot/2026-11-09-sems-workbench-1.3.0/`（00–07 工件齐备）。

## 技术栈（从真实仓库确认）

| 层 | 技术 | 依据（文件/配置） |
| --- | --- | --- |
| 宿主后端 | .NET 10 / ASP.NET Core / SQLite / NewLife.XCode | `ForgeSelf.Api/ForgeSelf.Api.csproj`（目标 `net10.0-windows`）、`AGENTS.md:83` |
| 宿主前端 | Vue 3.5 + Vite 6 + TS 5.7 + Element Plus 2.14 + Tailwind 4 + Pinia + pnpm | `ForgeSelf.Web/vite.config.ts`、`AGENTS.md:82` |
| 插件前端 | 独立预编译产物 `web/dist/index.js` + `style.css`；`vite.config.ts` 声明 external + import map 共享依赖 | `Plugins/Sems/web/vite.config.ts`、`plugin.json` 的 `frontend.entry` |
| 主题 | Workshop Forge · 器物美学，唯一真源 `ForgeSelf.Web/src/styles/themes/workshop-forge.css`（`html.dark` 切明暗） | 《主题口径变更评审意见》§1.1、`DESIGN.md`（v2-target） |
| 插件前端测试 | vitest（`*.spec.ts` 与组件同目录） | `Plugins/Sems/web/src/*.spec.ts` |
| 宿主 e2e | Playwright `e2e/plugins/sems/sems.spec.ts`（零 mock，globalSetup 起真实宿主） | `AGENTS.md:225`、`e2e-testing` 技能 |
| 设计守卫 | Node 脚本 + 棘轮基线 JSON；入口 `pnpm run design:check` | `scripts/check-design-compliance.mjs`、`scripts/design-compliance-baseline.json` |

## 架构特点（与 Sems 直接相关）

- **插件即远程视图**：`plugin.json` 声明 `frontend.views:["SemsView"]` / `route:"/sems"` / `entry:"web/dist/index.js"`；宿主 `utils/pluginViewLoader.ts` 运行期 `import(entryUrl)` 加载插件前端，`injectPluginStyles` 幂等注入同目录 `style.css`（`cssCodeSplit:false` 与之严丝合缝 → 评审 A-11「需显式引入 style.css」警告**不成立**）。
- **插件前端硬约束**：不能 `import` 宿主模块（`@/xxx` 别名不可用）；共享包须在 `vite.config.ts` 的 `rollupOptions.external` 声明，否则会内联第二份 Vue 实例导致 `inject/useRouter` 静默失效。`plugin-development` 铁律 4/5。
- **后端能力零改动**：22 个 `sems_*` 工具、4 个 Controller、`grep -cE '\[Http(Get|Post|Put|Delete)'` 实测 **25 个端点**（ProjectCommands 7 / Projects 6 / Runs 3 / Specs 9）；`ForgeSelf.Abstractions/IProjectRegistry.cs` 的 `ProjectUpdate` 支持 `Name/Type/Description/Tags`。重构不触碰 `.cs`。
- **工作台作用域**：新增 `web/src/workbench/`（`WorkbenchView.vue` / `workbenchStore.ts` / `workbench.css` / `views/*.vue` / `icons.ts`），样式挂 `.wb` 前缀避免污染宿主；原 7 个经典组件（`SemsView.vue` / `SpecPanel.vue` / `ProjectCard.vue` / `CommandList.vue` / `RunPanel.vue` / `DirectoryPickerDialog.vue` / `ProjectEditDialog.vue`）保留为「经典视图」，另落 `LegacySemsView.vue`。
- **视图切换**：`workbenchStore.ts` 的 `viewMode` + `localStorage['sems-workbench-v1']` 记忆；评审 D4 要求默认工作台且经典视图切换在**顶栏 + 侧栏双入口**可见。
- **路由**：内存路由为主，`WbRoute` 联合类型实测 10 条：`/projects` `/create` `/create/wizard` `/import` `/dashboard` `/plugins` `/dependencies` `/configuration` `/tasks` `/specs`；S02 空态、S11「更多」菜单非独立路由 → 合计 **11 屏 + 规格扩展项**。

## 测试方式（正规入口）

| 目的 | 命令 | 验证环境可执行性 |
| --- | --- | --- |
| 插件前端构建 | `cd Plugins/Sems/web && pnpm run build` | ✅ 可执行 |
| 插件前端单测 | `cd Plugins/Sems/web && pnpm run test` | ✅ 可执行 |
| 设计合规守卫 | `node scripts/check-design-compliance.mjs` / `pnpm run design:check` | ✅ 可执行 |
| 样式 token 守卫 | `node scripts/check-style-tokens.mjs` | ✅ 可执行 |
| 宿主前端门禁 | `cd ForgeSelf.Web && pnpm run check && pnpm run test` | ✅ 可执行 |
| 后端构建/测试 | `dotnet build` / `dotnet test` | ❌ 环境受限（见「环境事实」） |
| 插件层 e2e | `pnpm exec playwright test e2e/plugins/sems` | ❌ 环境受限，仅静态适配与留痕 |
| 源码本地前后端走查 | `pwsh scripts/dev-stack.ps1` | ❌ 无 `pwsh` |
| 离线预览/构建产物渲染冒烟 | Chromium 无头对离线 HTML / `dist` 渲染读图 | ✅ 可执行 |

> 判据一律看日志正文（PASS/通过数），不拿 exit code 当证据；`AGENTS.md:199/251`。

## 构建命令

```bash
# 可执行（验证环境）
cd Plugins/Sems/web && pnpm run build && pnpm run test
node scripts/check-design-compliance.mjs
node scripts/check-style-tokens.mjs
cd ForgeSelf.Web && pnpm run check && pnpm run test
# 环境受限，仅留痕不判绿（见 05-evidence，由验证阶段写）
dotnet build / dotnet test
pwsh scripts/dev-stack.ps1
pnpm exec playwright test e2e/plugins/sems/sems.spec.ts
```

## 主要目录职责

| 目录 | 职责 |
| --- | --- |
| `Plugins/Sems/` | 插件本体：`plugin.json` + `Sems.csproj` + `SemsPlugin.cs` + `ToolExtensions.cs` / `SpecTools.cs` + `Controllers/` `Services/` + `web/` |
| `Plugins/Sems/web/src/workbench/` | 本任务重构新增的工作台壳、store、样式、9 个屏组件 |
| `Plugins/Sems/web/src/*.vue`（7 个） | 经典视图组件，须原样保留 |
| `ForgeSelf.Web/src/styles/themes/` | 主题唯一真源 `workshop-forge.css` |
| `ForgeSelf.Web/e2e/plugins/sems/` | Sems 宿主 e2e（断言经典视图选择器，工具数 13） |
| `scripts/` | 设计守卫/发布/门禁脚本（本任务用前两者） |
| `docs/ai/pilot/`、`docs/18-templates/ai-pilot/` | 工件链产物与七份模板 |

## 代码组织方式

- 后端：Controllers / Services / 实体分层，日志走静态 `XTrace.Log`，插件经 `Plugins/` + `plugin.json` 注册。本任务**后端零改动**。
- 前端：插件为独立预编译产物，`http.ts` 封 API、`types.ts` 定类型、`workbenchStore.ts` 管状态与降级 DEMO、`views/` 按路由分屏、`confirmOps.ts`/`specOps.ts`/`runDuration.ts` 抽可测纯逻辑并配同名 `.spec.ts`。
- 样式：`workbench.css` 为 `scoped=false` 全局样式，以 `.wb` 前缀作用域化（当前仍含原型自定义 token 定义层，见「代码现状」必改项）。

## 现有工程规范（对本任务有约束力）

| 条目 | 位置 | 约束 |
| --- | --- | --- |
| 九阶段工件链 + 三道闸门 | `ai-native-engineering-workflow.md` §1.1/§2 | 开发类任务唯一流程；00–07 工件，闸门1/2/3 自含 |
| 插件任务必写「Interaction Design」节 | 规范 §2 Stage 2（2026-10-30 用户立） | 02-spec 缺节 = 闸门1 不通过 |
| 插件维护闭环六步 | `AGENTS.md:33`、`plugin-development` §四 | 门禁 → 冒烟 e2e → dev-stack 走查 → 发布 → 运行实例只读复验 → 文档/日志；缺一不算完成 |
| 发布动作须请示用户 | `AGENTS.md:359` §8.5 | git 提交/推送、打 tag、本地目录发布、运行实例复验**本次不执行** |
| 设计门禁三步（含 `design:check`） | `AGENTS.md:335` §8 | 每次改完代码必跑；pre-commit 拦截 |
| 主题唯一真源、禁原型薄荷绿 | 《主题口径变更评审意见》 | 零自定义 token，直接引用 `--el-*` / `--gold-*` 等 |
| 版本号三处同步 | 设计研发评审 RK-11 / A-14 | `plugin.json` / `web/package.json` / 文档 = 1.3.0 |
| 基线红先对表、禁伪造验证 | `AGENTS.md:249`、`§10` | 验证结果标 Verified/Inferred/Unknown |
| 禁手写一次性 `temp/*.cjs` 作验证 | `AGENTS.md:37-41` | 验证走 vitest / playwright / 正规脚本 |

---

## 代码现状（本任务开工基线 · 已实测核对）

> 以下为本任务开工前的真实状态，写进工件以界定「从哪里改」。所有历史验证数字**仅作参照，合入后须重跑**。

| 线索 | 位置 | 事实 |
| --- | --- | --- |
| **本地功能扩展工作区** | 仅 `Plugins/Sems` 子树（无 `.git`、无规范载体） | v0.2/v0.3 功能扩展代码。`pnpm build` 得 `dist/index.js 175.33 kB` / `style.css 50.32 kB`，`pnpm test` **32/32 通过**——**历史实测，合入后需重跑**，不得当作当前仓库已通过 |
| **参考交付物 1.3.0** | 静态复刻/评审证据 | `156.58 kB` / `46.49 kB` / **23 passed**——同为历史/外部证据，非本仓库实测 |
| **当前落点** | 已把功能扩展代码合入 `Plugins/Sems` | `plugin.json` = **1.3.0**、`web/package.json` = **1.3.0`（本报告已 `cat` 核对一致）；合入前原始 `Plugins/Sems` 已留备份 |
| **开工前必改项** | `Plugins/Sems/web/src/workbench/workbench.css` | 合入后 `design:check` **FAIL**：`Plugins/Sems/web/src` 的 `a` 从基线 **0** 涨到 **46**。根因 = `.wb{}` 定义了约 54 个原型自定义 token（`--bg-header:#c9e9db` 薄荷绿、`--accent:#6a45c0` 紫、`--radius-pill:999px`、`--sp-*`/`--fs-*`/`--motion-*` 等，实测该定义层存在），违反规则 A 白名单；本报告已核对 `scripts/design-compliance-baseline.json` 中 `"Plugins/Sems/web/src": {"a":0,"b":0,"c":0,"d":0}` 属实 |
| **工具数断言欠账** | `ForgeSelf.Web/e2e/plugins/sems/sems.spec.ts:40-53,478` | `SEMS_TOOLS` 实测 **13** 项，`toHaveLength(SEMS_TOOLS.length)` = 13，与后端 22 工具失配（P3 / RK-03 / 研发·T-03） |
| **口径已同步** | `.forgeself/memory/2026-11-09.md`、根 `TODO.md` | 已写入任务拆解、闸门裁剪决策、环境事实；本工件与之保持一致 |

- `workbench/` 实测含：`WorkbenchView.vue`、`workbenchStore.ts`、`workbench.css`、`icons.ts`、`taskState.spec.ts`、`views/`（9 个屏组件：`ManagerProjects/ManagerBrowser/ManagerWizard/ProjectDashboard/ProjectPluginsView/ProjectDependenciesView/ProjectConfigurationView/ProjectTasksView/ProjectSpecsView`）。
- 经典视图相关：`LegacySemsView.vue` + 7 个原 `.vue` + `confirmOps/specOps/runDuration` 及其 `.spec.ts` + `http.ts`/`types.ts`/`specApi.ts`/`index.ts` 均在位。
- **本报告不对上述任何代码声称「构建/测试已通过」**；构建、单测、守卫的实际执行与结果由 05-evidence 在验证后记录。

## 环境事实（实测 · 必须如实带入 Evidence）

| 项 | 实测 | 后果 |
| --- | --- | --- |
| node / pnpm / chromium / python3 | `v22.13.1` / `10.28.2` / `144` / `3.11.1` 可用 | 前端构建、vitest、设计守卫、无头渲染冒烟可执行 |
| dotnet | **仅 6.0.301**，仓库目标 `.NET 10 / net10.0-windows` | `dotnet build` / `dotnet test` **无法在验证环境执行** |
| pwsh | **缺失** | PowerShell 发布脚本、`dev-stack.ps1` 起前后端走查**无法执行** |
| git | 源码树**无 `.git`** | 无法 `git status` / commit / 推送 / 打 tag；仓库以纯源码树形式提供 |
| 宿主 e2e | 需起 .NET 宿主 + 真实浏览器栈 | **无法完整执行**，只能做静态适配与留痕 |
| 结论 | — | Evidence 阶段上述受限项一律标 `Unknown / 环境受限`，**严禁写成 PASS**；可执行项见「测试方式」 |

## 适用问题类（规范 v1.2.0 增补栏位）

- 任务级别裁定：**全量**（插件重构 / 新功能，规范 §4），非轻量；不合并为 `mini-task.md`。
- v0.2 已点名 3 个交互缺陷（命令交互差、实际使用不返回实时日志、运行中命令运行记录突然不见）属本次范围（已明确纳入），归「缺陷/返工增强」处理：02-spec 设「Failure Modes」节逐条预填，03-plan 的 Test Plan 列对应守卫。
- `docs/11-troubleshooting/problem-classes/README.md` 台账存在；**本工件产出阶段未对本次 3 个缺陷做同类扫查与 PC 归档**（属 Implement/Test 阶段动作），此处如实记为「待按 `problem-class-closure` 技能在验证阶段补齐，当前无已命中 PC id，不得预填或虚构」。

## 候选低风险任务

本次 Stage 0 进入时任务已获指派与开工授权，且 4 份评审基线已给出必须关闭的 P0 阻断项与 D1–D5 决策，**不做低风险任务重选**；由 `2026-11-09-sems-workbench-1.3.0` 单任务全量承接，按 03-plan 的整改批次分片落地、每片独立可验证。

## 选择该任务的原因

本任务明确要求在本工区完成 sems 插件重构开发、按项目内流程规范走，且 4 份评审基线结论为「需求侧有条件开工 / 设计研发侧有条件不通过」，其 P1（代码入库 + pilot 00–07 工件）与 RK-01/RK-04 直接指向本工件链缺口；本批 00–04 即为关闭该阻断项、把重构纳入唯一开发流程的第一步。技术必要性另由实测支撑：合入后 `design:check` 已 FAIL（`a:0→46`），不先整改无法通过设计门禁与 pre-commit。
