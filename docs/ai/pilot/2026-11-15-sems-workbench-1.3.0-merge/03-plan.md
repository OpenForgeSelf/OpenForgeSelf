# Plan

> 阶段：Stage 3 — Plan（必须具体到真实文件路径 `file:` + `reason:`）｜规范：`docs/04-standards/ai-native-engineering-workflow.md` §2
> 任务 ID：`2026-11-09-sems-workbench-1.3.0` ｜ 日期：2026-11-09
> 上游依据：`00/01/02` 工件；`scripts/check-design-compliance.mjs` + `scripts/design-compliance-baseline.json`（实测 `a:0`）；`Plugins/Sems/web/src/workbench/workbench.css`（实测含 `.wb{}` 原型 token 层）；`plugin-development` §四 维护闭环六步；`AGENTS.md` §5.6 门禁分档 / §8 设计门禁三步 / §11；4 份评审基线（D1–D5 / G1–G9 / P1–P6 / 主题·T-01~T-10 / 视觉优化 7 类 / B1·B2）；`.forgeself/memory/2026-11-09.md` 决策记录

---

## 整改批次与门禁顺序（核心）

> **顺序铁律**：批次 0 为**开工前必改项**，不先关闭则 `pnpm run design:check` 持续 FAIL、pre-commit 拦截，后续任何批次都无法合规落地。每个批次结束跑一次「快档门禁」（`AGENTS.md` §5.6），**全部批次完成后**才进入总验收（G1–G9）。
> **设计守卫失败不得用 `--update` 绕过**：`scripts/design-compliance-baseline.json` 是棘轮基线，`Plugins/Sems/web/src` 当前 `a:0`；若 `design:check` 因新增 token/胶囊/紫渐变报 FAIL，唯一合法动作是**改实现消除违规**，把计数压回 0。**严禁**执行 `node scripts/check-design-compliance.mjs --update`（或任何等价「抹平基线」写法）把 46 写成新基线——该动作会把不合规状态烤进仓库、pre-commit 形同虚设，属主题评审 T-10 / G9 明令禁止，也违反 AGENTS.md §10「禁伪造验证」。此约束对本任务所有批次、所有 agent 生效，须写入 04-task 的 Forbidden。

```
[批次 0] 开工前必改：删 workbench.css 的 .wb{} 自定义 token 层 → design:check 从 FAIL(a=46) 回 PASS(a=0)
   │  快档门禁：design:check + style-tokens + 插件前端 build
   ▼
[批次 1] 主题化（主题·T-01~T-10）：宿主 token 直引 / 明暗跟随 / 器物切角 / 清紫 / 徽标换色 / 修 B1·B2
   │  快档门禁：design:check(a=0) + style-tokens + build + Chromium 明暗双模渲染冒烟
   ▼
[批次 2] 假能力清除（D1/D2/D3 + X1–X4 + 视觉优化 §5）：2 步登记向导 / S07·S08 改名删死按钮加说明条 / S09 项目设置 / S06 真实信号
   │  快档门禁：build + vitest（confirmOps/specOps 等纯逻辑）+ G7 逐屏静态核对
   ▼
[批次 3] 视图与状态（D4/G4 + 视觉优化 §6·§7）：默认工作台 / 顶栏+侧栏双入口 / 不丢状态 / 明暗切换 / 列表·卡片双模式
   │  快档门禁：build + vitest + G4 切换不丢状态留痕
   ▼
[批次 4] 交互缺陷（v0.2 已点名）：实时日志增量轮询 / 运行中记录不消失 / 命令交互反馈（FR-5）
   │  快档门禁：vitest（增量/记录态单测）+ build + RunPanel 渲染冒烟
   ▼
[批次 5] 自动化资产与口径（P2/P3/P5 + RK-02/03/06/11 + 研发·T-02/03/05/11）：e2e 视图前置切换 / 13→22 / 21→25 / 版本三处 1.3.0
   │  快档门禁：pnpm check/test（宿主前端，静态适配留痕；完整 e2e 标 Unknown）
   ▼
[批次 6] 文档与工件（P1/P4/P6 + 规范 Stage 7）：ui-design-brief / 功能文档 / 预览标注 / 00–07 工件 / 日志与 TODO 回写
   │  总验收：G1–G9 全表 + 三道闸门裁剪留痕 + 05–07 由验证阶段产出
   ▼
[完工后汇报] 不执行发布动作（git 提交/推送/打 tag/运行实例复验）
```

## Files To Change

### 批次 0 · 开工前必改项

- file: `Plugins/Sems/web/src/workbench/workbench.css`
  reason: 删除 `.wb{}` 内约 54 个原型自定义 token 定义层（`--bg-header:#c9e9db` / `--accent:#6a45c0` / `--radius-pill:999px` / `--sp-*` / `--fs-*` / `--lh-*` / `--motion-*` / `--header-height` 等），并把全部 `var(--xxx)` 引用逐个替换为宿主 token 或 px 字面量。**这是 `design:check` 从 `a=46` 回到 `a=0` 的唯一合法路径**（合入功能扩展代码后已实测 FAIL，根因在此）。与批次 1 同文件、分两步：批次 0 先删定义层让守卫转绿，批次 1 再做完整主题映射与视觉重做。
- file: `scripts/design-compliance-baseline.json`
  reason: **只读核对，禁止修改**。确认 `"Plugins/Sems/web/src": {"a":0,"b":0,"c":0,"d":0}` 未被改动；任何 diff 出现即视为「`--update` 抹平基线」违规。

### 批次 1 · 主题化（主题·T-01~T-10）

- file: `Plugins/Sems/web/src/workbench/workbench.css`
  reason: 承接批次 0，按《主题口径变更评审意见》§二 +《视觉优化评审意见》§七重做：颜色 `var(--el-*)`/`--gold-*`/`--ink-*`/`--paper-*`/`--cinnabar-400`/`--bronze-500`；强调色一律 `--el-color-primary`（浅色对比度 4.70:1）；圆角 `--radius-md` 禁 999px 胶囊；阴影/动效/玻璃/字体走 `--shadow-*`/`--dur-*`/`--ease-*`/`--glass-*`/`--font-*`；间距写 px 字面量（修 B2）；状态栏明暗分别取 token + `--fg-secondary` 字（修 B1）；顶栏 56px 单行左对齐；`INFO` 用 `--el-color-info`、DEMO 用 `--gold-400`、全站清紫（规则 D）；`.wb` 前缀不变。
- file: `Plugins/Sems/web/src/workbench/WorkbenchView.vue`、`Plugins/Sems/web/src/workbench/views/*.vue`（9 个）
  reason: 视觉结构重做（页头三段式 / 列表行 1px 分隔 + hover 金染 / 卡片 1px 边框 / 仪表盘 4 磁贴 + 双栏 / 空态=图标+serif 引言+副说明 / 字阶 eyebrow→display→sans→mono）；补 `role=tablist/tab/tabpanel`、侧栏 `aria-current`（X10）；确保模板内无裸 hex、无胶囊类。
- file: `Plugins/Sems/web/src/workbench/icons.ts`
  reason: 图标 stroke 用 `currentColor` 跟随主题，不引紫色/薄荷绿。

### 批次 2 · 假能力清除（D1/D2/D3）

- file: `Plugins/Sems/web/src/workbench/views/ManagerWizard.vue`
  reason: 4 步向导砍成 2 步「登记」（① 选目录 ② 名称/类型/标签/描述）；删包管理器/Git 初始化/预设/功能/覆盖目录区域；按钮「创建项目」→「登记项目」；确认页加「不会在你的目录中创建或修改任何文件」；`POST` 成功后 `PUT` 写回 `type/tags/description`，写回失败可独立重试（BR-1）。
- file: `Plugins/Sems/web/src/workbench/views/ProjectPluginsView.vue`
  reason: 改名「Sems 工具（22）」；条目改工具名 + 用途；删「添加插件/安装开发工具/下载/官方徽标/最新版本」；屏首行加语义说明条「非插件市场，不提供安装/下载」。
- file: `Plugins/Sems/web/src/workbench/views/ProjectDependenciesView.vue`
  reason: 改名「运行命令」；字段改 命令名/脚本/最近运行/最近更新；删「安装依赖」；加说明条「非依赖解析，只做查看」；新增命令默认只读（RK-12）。
- file: `Plugins/Sems/web/src/workbench/views/ProjectConfigurationView.vue`
  reason: 「项目构建配置」空壳改「项目设置」=项目档案（`ProjectUpdate` 四字段）+ 工作台显示偏好；复用 `ProjectEditDialog` 语义，点即保存。
- file: `Plugins/Sems/web/src/workbench/views/ProjectDashboard.vue`
  reason: 删「端口占用 8080 / 结束占用进程」，换运行中会话数/已登记命令数/可用工具数(22)/规格覆盖率/最近运行结果；磁贴 + 1.5:1 双栏布局。
- file: `Plugins/Sems/web/src/workbench/workbenchStore.ts`
  reason: 清理 `wizardMeta` 中预设/功能/Git 等假字段相关 DEMO 元数据；保留 22 工具清单（已实测 `TOOLS` 22 项）与真实路由态；`DEMO` 态禁写 + 「重试连接」入口（RK-08）。
- file: `Plugins/Sems/web/src/workbench/views/ProjectSpecsView.vue`
  reason: 5 张静态能力卡 → 挂载 `SpecPanel.vue` 真实规格列表 + 空态；标「Sems 扩展」。

### 批次 3 · 视图与状态（D4/G4）

- file: `Plugins/Sems/web/src/workbench/WorkbenchView.vue`
  reason: 顶栏 + 侧栏（或「更多」）双入口「切换经典视图」；默认 `viewMode:'workbench'`；切走再切回保留筛选/展开/滚动位置；明暗切换（月亮图标，写 `localStorage`）。
- file: `Plugins/Sems/web/src/workbench/views/ManagerProjects.vue`
  reason: 列表/卡片双模式切换（FR-1）+ 偏好持久化；搜索/收藏/行内操作在两模式一致；四态（空/加载/失败可重试/搜索无结果）。
- file: `Plugins/Sems/web/src/workbench/workbenchStore.ts`
  reason: `viewMode` / 双模式 / 明暗偏好 / 收藏 的 state 与持久化；`localStorage['sems-workbench-v1']` 损坏回落默认。
- file: `Plugins/Sems/web/src/SemsView.vue`、`Plugins/Sems/web/src/LegacySemsView.vue`
  reason: 经典视图入口与工作台入口的挂接；**7 个经典 `.vue` + `SpecPanel.vue` 的 DOM 选择器零改动**（兼容性硬约束）。

### 批次 4 · 交互缺陷（FR-5）

- file: `Plugins/Sems/web/src/RunPanel.vue`、`Plugins/Sems/web/src/workbench/views/ProjectTasksView.vue`
  reason: 运行后 `logs?after=n` 增量轮询追加实时日志；运行中命令「运行记录」在会话存活期持续可见、停止后转终态不消失；命令运行/停止/失败给可见反馈与禁用态原因。（用户已说明「后台服务日志运行台没问题」→ 不重写既有日志运行台，仅修这 3 个交互点。）
- file: `Plugins/Sems/web/src/runDuration.ts`（及必要的新增纯逻辑 `.spec.ts`）
  reason: 把「增量追加 / 运行中记录不消失 / 终态转换」抽为可测纯函数并配 vitest 用例，避免只在界面回显上打补丁。

### 批次 5 · 自动化资产与口径

- file: `ForgeSelf.Web/e2e/plugins/sems/sems.spec.ts`
  reason: `SEMS_TOOLS` 由 13 补到 22（补 9 个 spec 工具名）、`toHaveLength` 同步；增加视图前置切换（进工作台前先切到目标视图）或补工作台专用用例；经典视图断言保持。完整 e2e 验证环境标 `Unknown / 环境受限`，仅静态适配留痕。
- file: `Plugins/Sems/plugin.json`、`Plugins/Sems/web/package.json`
  reason: 核对保持 **1.3.0**（已实测一致，本批不改值，仅作为守卫对照项）。
- file: `Plugins/Sems/docs/`（功能/交付说明）、`Plugins/Sems/README` 类文档
  reason: 端点数 21→25（7/6/3/9）、版本号 1.3.0、主题口径「与 `workshop-forge.css` 一致，明暗自动跟随」（替换「视觉 token 与交接包一致」作废表述）、预览标注「静态复刻非实况」（P4/P5 / 研发·T-04/05/11）。

### 批次 6 · 文档与工件

- file: `Plugins/Sems/docs/ui-design-brief.md`
  reason: 规范 Stage 2 硬性（2026-10-30 用户立）：面向不读代码的设计师，写功能、旅程、界面交互行为，与 02-spec「Interaction Design」节一致。
- file: `docs/ai/pilot/2026-11-09-sems-workbench-1.3.0/00…04`（前期产出）、`05-evidence.md` / `06-review.md` / `07-final-report.md`（验证阶段产出）
  reason: 关闭 P1 / RK-01 / RK-04；05–07 不在规划阶段生成，但本 Plan 为其预留「验证命令 + 来源等级 + 环境受限清单」栏位。
- file: `.forgeself/memory/2026-11-09.md`、仓库根 `TODO.md`
  reason: 按 `AGENTS.md` §7.5 回写验证结果、决策记录、待办清除；已存在的两份文件口径与之保持一致（不另起炉灶）。

## Implementation Steps

1. **批次 0**：备份当前 `workbench.css`（合入前原始 `Plugins/Sems` 已留备份）→ 删 `.wb{}` 自定义 token 层 → 把引用改为宿主 token / px 字面量占位 → 跑 `node scripts/check-design-compliance.mjs` 必须 PASS 且 `a=0`；**禁止 `--update`**。
2. **批次 1**：完整主题映射 + 视觉重做 + 徽标/字阶/圆角/无障碍 → 明暗双模 Chromium 渲染冒烟（11 屏）→ `design:check` 复跑 `a=0`。
3. **批次 2**：按 D1/D2/D3 逐屏改假能力 → `confirmOps` / `specOps` 纯逻辑 vitest 补覆盖（登记写回重试、二次确认）→ G7 逐屏静态核对无死按钮。
4. **批次 3**：双入口 + 双模式 + 明暗切换 + 不丢状态 → vitest 覆盖 `viewMode` 持久化与回落。
5. **批次 4**：实时日志 / 运行记录 / 交互反馈 → 抽纯函数 + 单测（Red→Green）。
6. **批次 5**：e2e 断言与文档口径静态修正（不改后端）；宿主前端 `pnpm check/test` 对照基线。
7. **批次 6**：`ui-design-brief.md` + 工件链 + 日志/TODO 回写；总验收 G1–G9 全表。
8. **发现 Plan 与仓库实际不符** → 先在本文件「Plan 偏差记录」登记，再修正 Plan，**不得绕过**（规范 Stage 5 第 7 条）。

## Test Plan

| 层 | 内容 | 守卫 / 命令 |
| --- | --- | --- |
| 设计守卫（每批必跑） | token 族白名单 / 胶囊 / 玻璃 / 紫渐变 / 裸 hex | `node scripts/check-design-compliance.mjs`、`node scripts/check-style-tokens.mjs`、`pnpm run design:check`；断言 `a=0`，**禁 `--update`** |
| 插件前端单测 | 登记写回重试、二次确认编排（`confirmOps.spec.ts`）、spec 操作（`specOps.spec.ts`）、`taskState.spec.ts`、新增「日志增量/运行记录态」用例（`runDuration.spec.ts` 扩展） | `cd Plugins/Sems/web && pnpm run test` |
| 插件前端构建 | 产物 `dist/index.js` + `style.css` 可生成 | `cd Plugins/Sems/web && pnpm run build` |
| 宿主前端门禁 | 类型/lint + vitest，对照基线不劣化 | `cd ForgeSelf.Web && pnpm run check && pnpm run test` |
| 渲染冒烟 | Chromium 无头对离线预览（优化版/宿主主题版）与 `dist` 产物做 11 屏 × 明暗 + 375px + 空态/焦点读图 | 截图留痕，Console/pageerror = 0；**不作 e2e 结论**（AGENTS.md 红线：禁一次性 temp 脚本当验证） |
| 宿主 e2e（受限） | `sems.spec.ts` 视图前置切换 + 22 工具断言 | 验证环境**静态适配与留痕**，完整执行标 `Unknown / 环境受限` |
| 后端（受限） | `dotnet build` / `dotnet test` | 缺 .NET 10，标 `Unknown / 环境受限` |
| 走查/发布（受限或不执行） | `dev-stack.ps1` 走查、本地目录发布、运行实例只读复验 | 缺 `pwsh` / 无 `.git` / 用户授权不含发布 → 前两项 `Unknown / 环境受限`，发布**不执行** |
| 缺陷类守卫覆盖（v1.2.0） | 3 个交互缺陷 + B1/B2 的红灯→绿灯：既有守卫复跑（`design:check` 防 B1/B2 复发）+ 新增纯逻辑单测（增量/记录态） | 按 `problem-class-closure` 在 Implement/Test 阶段补 case/PC 归档；本 Plan 不预填 PC id |

## Verification

### Build

```bash
# 可执行（合入后重跑；前期构建的 175.33 kB/50.32 kB、32/32 仅为历史参照）
cd Plugins/Sems/web && pnpm run build
cd ForgeSelf.Web && pnpm run check
# 环境受限 → 标 Unknown / 环境受限，不判绿
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj -c Debug
```

### Unit Test

```bash
cd Plugins/Sems/web && pnpm run test            # vitest，含新增交互缺陷用例
cd ForgeSelf.Web && pnpm run test              # 宿主前端基线对照
# 环境受限
dotnet test ForgeSelf.Api.Tests --logger "console;verbosity=detailed"
```

### Integration Test

```bash
# 仅静态适配与留痕（验证环境无 .NET 10 / pwsh，无法起真实宿主 + 浏览器栈）
# dev-stack.ps1 起源码本地前后端走查 → Unknown / 环境受限
# 宿主 e2e plugins/sems → Unknown / 环境受限
```

### E2E

```bash
# 验证环境无法完整执行（需 .NET 宿主 + 真实浏览器栈 + 真实 token）
# pnpm exec playwright test e2e/plugins/sems/sems.spec.ts → Unknown / 环境受限
# 可执行替代：Chromium 无头对离线预览/构建产物渲染冒烟（截图留痕，不作 e2e 结论）
```

### Other Checks

```bash
node scripts/check-design-compliance.mjs         # 必须 PASS，Plugins/Sems/web/src 的 a 保持 0
node scripts/check-style-tokens.mjs              # 必须 PASS
pnpm run design:check                            # 快档门禁，禁 --update 抹平基线
# 核对基线未被改动（只读）
grep -A4 '"Plugins/Sems/web/src"' scripts/design-compliance-baseline.json
```

## 门禁分档与顺序（AGENTS.md §5.6）

- 每批改完跑**快档**：相关过滤单测 + `pnpm run check` + `pnpm run test` + `pnpm run design:check` + 本插件 1 冒烟 e2e（验证环境 e2e 降级为渲染冒烟留痕）。
- 本任务**不碰** `ForgeSelf.Api/**`、仓库级 `scripts/*.ps1`、共享测试基建 → 不触发「中档」全量 `dotnet test`；不碰 `e2e/global-setup.ts` / `playwright.*.config.ts` / `e2e/fixtures/**` → 不触发「深档」全量 e2e。
- 跨切面改动停在「快」即报绿 = 违规；本任务汇报须写明「跑的是快档 + 哪些受限项标 Unknown」。

## Plan 偏差记录

> 实现中发现 Plan 与仓库实际不符时，先在此登记偏差，再修正 Plan，不得直接绕过。

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 2026-11-09（规划时已确认） | 后端 `dotnet build/test`、`dev-stack` 走查、宿主 e2e、发布链 | 规范/技能要求六步全跑 | 验证环境 `dotnet 6.0.301`（需 .NET 10）、`pwsh` 缺失、源码无 `.git` → 上述项 `Unknown / 环境受限` 或**不执行**；据实标注，不以 Inferred 冒充 Verified（依据 `.forgeself/memory/2026-11-09.md` 决策记录 + 00 工件「环境事实」） |
| 2026-11-09（规划时已确认） | 合入功能扩展代码后 `design:check` 已 FAIL（`a=0→46`） | 无该必改项（功能扩展代码工作区无规范载体，未接守卫） | 新增**批次 0** 为开工前必改项，删 `.wb{}` 自定义 token 层使 `a` 回 0；基线 JSON 只读核对、禁 `--update` |
| 待 Implement 填写 | — | — | — |
