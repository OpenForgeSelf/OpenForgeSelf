---
task_id: 2026-11-09-sems-workbench-1.3.0
feature: none                      # 插件重构，不对应 docs/02-features 新编号；功能文档在 Plugins/Sems/docs/ 同步
risk: { level: L2, triggers: ["Plugins/Sems/web/src/workbench/", "ForgeSelf.Web/e2e/plugins/sems/sems.spec.ts"], raised_by_agent: false }
gate1: { mode: auto-granted, at: 2026-11-09, ref: "04-task 闸门裁剪说明" }
gate2: { mode: merged-to-final-report, at: null, decision: null }
gate3: { mode: not-executed, at: null, ref: "发布动作不执行；无 .git" }
expected_files:
  - Plugins/Sems/web/src/workbench/workbench.css
  - Plugins/Sems/web/src/workbench/views/*.vue
  - ForgeSelf.Web/e2e/plugins/sems/sems.spec.ts
  - Plugins/Sems/docs/ui-design-brief.md
  - docs/ai/pilot/2026-11-09-sems-workbench-1.3.0/00..07
rollback: "无 .git；回退用合入前原始 Plugins/Sems 备份目录"
writeback: { feature_doc: pending, reason: "批次 6 文档同步" }
---

# Agent Task

> 阶段：Stage 4 — Task（把任务变成 **Agent 可以直接执行的工作单元**）｜规范：`docs/04-standards/ai-native-engineering-workflow.md` §2 / §1.1 / §4
> 任务 ID：`2026-11-09-sems-workbench-1.3.0` ｜ 日期：2026-11-09
> 上游依据：`00-repository-understanding.md` / `01-intent.md` / `02-spec.md` / `03-plan.md`；`AGENTS.md` §0 预飞 / §8.5 自动迭代 / §10 完成判定 / §11；`ai-native-engineering-workflow.md` §1.1 三道闸门、§4 任务级别裁剪；`plugin-development` §四；`.forgeself/memory/2026-11-09.md` 决策记录；根 `TODO.md`
> 前序工件：00–03 齐备，闸门1 合并为「开工前一次性授权」（见「闸门裁剪说明」）。

## Task ID

`2026-11-09-sems-workbench-1.3.0`（全量任务级别：插件重构 / 新功能，规范 §4；不合并为 `mini-task.md`）

## Objective

做完后仓库达到的可验证状态：`Plugins/Sems` 1.3.0 工作台在完整仓库内合规落地——11 屏对齐原型且**最终功能 ≥ HTML**、sems 原有 22 工具/25 端点/经典视图/规格能力完整保留；列表/卡片双模式 + 经典视图顶栏/侧栏双入口可切且不丢状态；原型假能力全部清除（2 步登记向导、S07/S08 改名删死按钮加说明条、S09 项目设置、S06 真实信号）；主题零自定义 token 走 `workshop-forge.css`、明暗自动跟随、`pnpm run design:check` PASS 且 `Plugins/Sems/web/src` 的 `a` 保持 **0**（**禁 `--update` 抹平基线**）；3 个交互缺陷有可测修复；`docs/ai/pilot/2026-11-09-sems-workbench-1.3.0/` 00–07 工件齐全；发布动作零执行、环境受限项如实标 `Unknown / 环境受限`。

## Scope

### Allowed

- `Plugins/Sems/web/src/workbench/` 全部：`workbench.css`、`WorkbenchView.vue`、`workbenchStore.ts`、`icons.ts`、`views/*.vue`（9 个）及新增纯逻辑 `.spec.ts`。
- `Plugins/Sems/web/src/` 根下与交互缺陷/视图挂接相关文件：`RunPanel.vue`、`SemsView.vue`、`LegacySemsView.vue`、`runDuration.ts`（+对应 `.spec.ts`）；**7 个经典 `.vue` + `SpecPanel.vue` 仅挂接、DOM 选择器零改动**。
- `ForgeSelf.Web/e2e/plugins/sems/sems.spec.ts`：`SEMS_TOOLS` 13→22、视图前置切换/工作台用例（静态适配与留痕）。
- `Plugins/Sems/plugin.json` / `web/package.json`：值保持 1.3.0，仅作守卫核对（不改值）。
- `Plugins/Sems/docs/`（含新增 `ui-design-brief.md`）、插件功能/交付说明文档口径修正。
- `docs/ai/pilot/2026-11-09-sems-workbench-1.3.0/`：`00`–`04`（前期产出）、`05`–`07`（验证阶段产出）。
- `.forgeself/memory/2026-11-09.md`、根 `TODO.md`：按 §7.5 回写验证结果与待办清除。
- 只读核对：`scripts/design-compliance-baseline.json`、`scripts/check-design-compliance.mjs`、`scripts/check-style-tokens.mjs`（**不修改**）。

### Forbidden

- **生产环境 / DB 结构 / 鉴权·权限·支付·安全核心逻辑**（规范 §1 第 1–3 条）；`sems_add_command` 任意脚本 P1 安全债不在本期修（研发·T-12）。
- **后端 `.cs` / Controllers / Services / 实体 / DTO 任何改动**（22 工具 / 25 端点零改动，插件契约不变）；宿主 `ForgeSelf.Web/src/`、import map、`pluginViewLoader`、`workshop-forge.css` 不改。
- **新依赖**（npm / NuGet）；不改构建配置；不引入模块注册 / 插槽契约 / 定时任务 / 专家 / 架构层（保持实现简洁、避免过度设计）。
- **本期不做项零实现**：需求管理 / 开发规范 / 派发任务 / 门禁管理 / 宪法管理（仅预留）、真插件市场 / 依赖解析 / 脚手架生成、移动端精细适配、后台服务日志运行台重写。
- **假能力残留**：「安装依赖 / 添加插件 / 安装开发工具 / 下载 / 初始化 Git / 端口占用·结束占用进程 / 项目构建配置」等无后端支撑元素；紫色系、薄荷绿、999px 胶囊、`--sp-*` 等自定义 token 族。
- **设计守卫失败时严禁 `--update` 抹平基线**：不得执行 `node scripts/check-design-compliance.mjs --update` 或任何等价写法、不得改 `scripts/design-compliance-baseline.json` 的 `a` 值；唯一合法动作是改实现把 `a` 压回 0（主题·T-10 / G9 / `AGENTS.md` §10 禁伪造）。
- **禁手写一次性 `temp/*.cjs` 作验证**（`AGENTS.md:37-41`）：验证走 vitest / playwright / 正规守卫脚本 / Chromium 渲染冒烟留痕。
- **发布动作不执行**：git 提交 / 推送 / 打 tag / 本地目录发布 / 运行实例只读复验（`AGENTS.md` §8.5 须请示 + 无 `.git` 本就不能执行）；**禁止停/启/杀任何运行中的宿主进程**。
- **不改 `specs/`**（已弃用，§9）；不改其他插件源码；不顺手重构无关代码。

## 任务分解表

> 顺序与 `03-plan.md`「整改批次」一致；批次 0 为开工前必改项，未关闭不得进入后续批次。

| 批次 | 工作单元 | 主要文件（`file:` 见 03-plan） | 完成判据（可立即验证） | 门禁 |
| --- | --- | --- | --- | --- |
| B0 | 删 `workbench.css` 的 `.wb{}` 自定义 token 层，引用改宿主 token / px 字面量 | `workbench/workbench.css` | `design:check` 从 FAIL(`a=46`) 转 PASS 且 `a=0`；基线 JSON 未改 | 快档：design:check + style-tokens + build |
| B1 | 主题化 T-01~T-10：宿主 token 直引 / 明暗跟随 / 器物切角 / 清紫 / 徽标换色 / 修 B1·B2 / 视觉重做 | `workbench.css`、`WorkbenchView.vue`、`views/*.vue`、`icons.ts` | `a=0`、hex=0、规则 B/C/D=0；明暗双模 11 屏渲染冒烟无 console error | 快档 + 截图留痕 |
| B2 | 假能力清除 D1/D2/D3：2 步登记向导、S07/S08 改名删死按钮加说明条、S09 项目设置、S06 真实信号、规格真实列表 | `ManagerWizard.vue`、`ProjectPluginsView.vue`、`ProjectDependenciesView.vue`、`ProjectConfigurationView.vue`、`ProjectDashboard.vue`、`ProjectSpecsView.vue`、`workbenchStore.ts` | G7 逐屏无死按钮；登记=POST+PUT 且不建文件；`confirmOps`/`specOps` 单测绿 | 快档 + vitest |
| B3 | 视图与状态 D4/G4：默认工作台、顶栏+侧栏双入口、不丢状态、明暗切换、列表/卡片双模式、四态 | `WorkbenchView.vue`、`ManagerProjects.vue`、`workbenchStore.ts`、`SemsView.vue`、`LegacySemsView.vue` | G4 切走切回保留筛选/展开/滚动；双模式可切；四态齐全 | 快档 + vitest |
| B4 | 交互缺陷 FR-5：实时日志增量追加、运行中记录不消失、命令交互反馈 | `RunPanel.vue`、`ProjectTasksView.vue`、`runDuration.ts`(+`.spec.ts`) | 新增纯逻辑单测 Red→Green；渲染冒烟见日志追加 | 快档 + vitest |
| B5 | 自动化资产与口径 P2/P3/P5：e2e 13→22、视图前置切换、端点 21→25、版本三处 1.3.0 | `sems.spec.ts`、`plugin.json`、`web/package.json`、`Plugins/Sems/docs/*` | 静态断言可查；宿主前端 `check/test` 不劣于基线 | 快档（完整 e2e 标 Unknown） |
| B6 | 文档与工件：`ui-design-brief.md`、功能/交付说明、预览标注、00–07 工件、日志/TODO 回写 | `Plugins/Sems/docs/ui-design-brief.md`、`docs/ai/pilot/.../*`、日记、`TODO.md` | 00–07 齐全；G1 关闭；05–07 由验证阶段产出 | 总验收 G1–G9 |

## Acceptance Criteria（checkbox 清单）

**功能与产品**

- [ ] AC-1 11 屏（含 `/specs` 扩展项）对齐原型，最终真实功能 ≥ HTML；F01–F06 六流程覆盖。
- [ ] AC-2 列表/卡片双模式可切且偏好持久化（已确认需求）。
- [ ] AC-3 默认工作台；「切换经典视图」顶栏 + 侧栏双入口；切走再切回不丢状态（D4/G4）；7 个经典 `.vue` + `SpecPanel.vue` DOM 零改动。
- [ ] AC-4 D1：向导 2 步「登记」、按钮「登记项目」、确认页明示不创建/不修改任何文件、删 4 个假区域、写回失败可独立重试。
- [ ] AC-5 D2：S07「Sems 工具（22）」/ S08「运行命令」改名 + 删「安装依赖/添加插件/安装开发工具/下载」+ 屏首语义说明条 + 新增命令默认只读。
- [ ] AC-6 D3：S09「项目设置」= 项目档案 + 显示偏好；S06 换 Sems 真实信号（删端口占用/结束占用进程）。
- [ ] AC-7 G7 逐屏核对无假能力按钮残留；DEMO 态禁写 + 「重试连接」入口（RK-08）。
- [ ] AC-8 三个交互缺陷（v0.2 点名）有可测修复：实时日志增量可见、运行中记录存活期不消失、命令交互反馈完整。
- [ ] AC-9 本期不做项零实现；零新依赖；定时任务/专家/模块注册/插槽契约零引入。

**主题与视觉（G9 / 主题·T-01~T-10 / B1·B2）**

- [ ] AC-10 `workbench.css` 零自定义 token 定义、全文件 hex=0、间距写 px 字面量（不建 `--sp-*`）；颜色/圆角/阴影/动效/玻璃/字体全走宿主 token。
- [ ] AC-11 明暗自动跟随 `html.dark`，不监听 class、不写两套 CSS；顶栏/状态栏表面明暗分别取 token。
- [ ] AC-12 强调色 `--el-color-primary`、破坏性 `--cinnabar-400`、`INFO`=`--el-color-info`、DEMO=`--gold-400`；全站无紫色；圆角 `--radius-md` 禁 999px 胶囊。
- [ ] AC-13 B1（状态栏墨黑底墨黑字）/ B2（`--sp-*` 间距塌陷）不复发；明暗双模 11 屏截图逐屏比对留痕。
- [ ] AC-14 `pnpm run design:check` PASS 且 `Plugins/Sems/web/src` 的 `a` 保持 **0**；`scripts/design-compliance-baseline.json` 未被改动；**全程未用 `--update` 抹平**。

**门禁与资产（G1–G8 / P1–P6）**

- [ ] AC-15 `cd Plugins/Sems/web && pnpm run build` + `pnpm run test` 通过（**合入后重跑**；前期构建验证的 32/32 仅历史参照）。
- [ ] AC-16 `node scripts/check-design-compliance.mjs` / `node scripts/check-style-tokens.mjs` / `pnpm run design:check` PASS；`cd ForgeSelf.Web && pnpm run check && pnpm run test` 不劣于基线。
- [ ] AC-17 `sems.spec.ts` 工具数 13→22；宿主 e2e 视图前置切换/工作台用例静态适配留痕（完整执行 `Unknown / 环境受限`）；端点数 21→25；版本三处 1.3.0。
- [ ] AC-18 `.wb` 作用域不污染宿主（G8）；`ui-design-brief.md` 维护；预览标注静态复刻/非实况（P4）。
- [ ] AC-19 `docs/ai/pilot/2026-11-09-sems-workbench-1.3.0/` 00–07 八件齐全（00–04 前期产出、05–07 验证阶段产出），闸门裁剪依据在本文件留痕；按 §7.5 回写日记与 TODO。

**完成判定与诚实性**

- [ ] AC-20 环境受限项一律 `Unknown / 环境受限`，**严禁写成 PASS**；验证结果标 Verified / Inferred / Unknown 且不混用；最终状态按 `AGENTS.md` §10.3 取 ⚠️ `COMPLETED_WITH_RISK` 或 🟡 `PARTIALLY_COMPLETED`，不虚构完成。
- [ ] AC-21 发布动作零执行（git 提交/推送/打 tag/本地目录发布/运行实例复验）；未停/启/杀任何用户宿主进程。

## 闸门裁剪说明（必须写明 · 规范 §1.1 / §4）

**规范依据**：`ai-native-engineering-workflow.md` §1.1（授权表述只豁免中间过程汇报频率、不豁免三道闸门；裁剪须写入任务工件）；`AGENTS.md` §8.5（自动迭代、如无必要无需用户决策）；`plugin-development` §四（六步门禁）。

**裁剪性质**：本任务在授权范围内，把三道闸门的「用户确认环节」合并为「**开工前一次性授权 + 完工后汇报**」，不豁免九阶段与 00–07 工件，不减 Test→Evidence→Review 真实验证与审查（规范 §4「裁剪不等于跳过」）。裁剪留痕即本节与 frontmatter 的 gate 字段：

| 闸门 | 规范定义 | 本次合并/裁剪 | 留痕位置 |
| --- | --- | --- | --- |
| 闸门1（S1–S4 规格确认） | Intent/Spec/Plan/Task 完成后交用户确认，明确批准后方可 Implement | 开工前一次性授权，00–04 工件落盘即视为规格已确认，直接进入 Implement | 本文件 frontmatter `gate1.mode=auto-granted` + 本节 + `.forgeself/memory/2026-11-09.md` 决策记录 |
| 闸门2（Evidence + Review 后验收） | 05/06 齐备后按 §5 格式向用户交付最终汇报，验收通过前不提交代码 | 合并进**完工后汇报**：验证完成后产出 05/06/07，一次性按 `07-final-report.tpl.md` 十三节格式汇报，不再单独发起中途验收 | 本文件 `gate2.mode=merged-to-final-report`；05–07 由验证阶段补写 |
| 闸门3（验收后提交归档） | 闸门2 通过后执行 git 提交 + 文档同步 + 经验回写 | **不执行提交动作**（发布动作须请示 §8.5，且源码树无 `.git` 本就不能执行）；仅做文档同步/经验回写（批次 6）并如实标注 | 本文件 `gate3.mode=not-executed` + 「环境受限清单」 + 05-evidence |

**裁剪边界（不得扩大解释）**：

1. 本次裁剪**只合并「用户确认环节 / 过程汇报频率」**，不减、不跳九阶段、不减 00–07 工件、不减 Test→Evidence→Review 真实验证与审查（规范 §4「裁剪不等于跳过」）。
2. 发布动作（git 提交/推送、打 tag、本地目录发布、运行实例只读复验）不执行：`AGENTS.md` §8.5 明确列为「必须请示用户」项；且源码树无 `.git`，提交/推送/打 tag 客观上不能执行。
3. `plugin-development` §四 六步门禁中，③ dev-stack 走查 / ⑤ 运行实例只读复验 / ④ 发布 因环境或授权不执行，③④⑤ 与后端 `dotnet build/test` 一律标 `Unknown / 环境受限`，**不得据此宣称「六步全绿」或「任务完成」**。

## 环境受限清单（Evidence 一律 `Unknown / 环境受限`，严禁 PASS）

| 受限项 | 实测原因 | Evidence 标注 |
| --- | --- | --- |
| `dotnet build` / `dotnet test` | 验证环境 `dotnet 6.0.301`，仓库目标 `.NET 10 / net10.0-windows` | `Unknown / 环境受限` |
| `pwsh scripts/dev-stack.ps1` 起源码本地前后端走查 + 截图读图 | `pwsh` 缺失 | `Unknown / 环境受限` |
| 宿主 e2e 完整执行（`e2e/plugins/sems/sems.spec.ts`） | 需起 .NET 宿主 + 真实浏览器栈 + 真实 token | `Unknown / 环境受限`（仅静态适配与留痕） |
| PowerShell 发布脚本 / `release-local.ps1` | `pwsh` 缺失 + 发布须请示 | 不执行 |
| git status / commit / 推送 / 打 tag | 源码树**无 `.git`** | 不执行（客观上不能） |
| 运行实例只读复验 | 无运行实例 + 发布未执行 + 授权不含 | 不执行 |
| git 直连 github.com:443 | 超时，已改用仓库源码树 | 事实记录，非验证项 |

**验证环境可执行的验证（可标 Verified）**：`cd Plugins/Sems/web && pnpm run build` / `pnpm run test`、`node scripts/check-design-compliance.mjs`、`node scripts/check-style-tokens.mjs`、`pnpm run design:check`、`cd ForgeSelf.Web && pnpm run check && pnpm run test`、Chromium 无头对离线预览/构建产物做渲染冒烟（截图留痕）。环境可用项：`node v22.13.1` / `pnpm 10.28.2` / `chromium 144` / `python3 3.11.1`。

## Expected Files

见 `03-plan.md`「Files To Change」全表 + 本文件「任务分解表」。工件侧：`docs/ai/pilot/2026-11-09-sems-workbench-1.3.0/` 下 `00-repository-understanding.md`、`01-intent.md`、`02-spec.md`、`03-plan.md`、`04-task.md`（前期产出）、`05-evidence.md`、`06-review.md`、`07-final-report.md`（验证阶段产出）。

## Verification Commands

```bash
# ---- 可执行（合入后重跑；前期构建验证数字仅历史参照）----
cd Plugins/Sems/web && pnpm run build && pnpm run test
node scripts/check-design-compliance.mjs          # 必须 PASS，a=0；禁 --update
node scripts/check-style-tokens.mjs               # 必须 PASS
pnpm run design:check                            # 快档门禁
cd ForgeSelf.Web && pnpm run check && pnpm run test
grep -A4 '"Plugins/Sems/web/src"' scripts/design-compliance-baseline.json   # 只读核对 a=0 未被改
# Chromium 无头对离线预览/构建产物做 11 屏 × 明暗 + 375px + 空态/焦点渲染冒烟（截图留痕，不作 e2e 结论）

# ---- 环境受限 → 标 Unknown / 环境受限，不判绿 ----
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj -c Debug
dotnet test ForgeSelf.Api.Tests --logger "console;verbosity=detailed"
pwsh -NoProfile -File scripts/dev-stack.ps1
pnpm exec playwright test e2e/plugins/sems/sems.spec.ts

# ---- 不执行（发布/提交）----
# git commit / push / tag / release-local.ps1 / 运行实例只读复验
```

> 判据看日志正文（PASS/通过数），不拿 exit code 当证据；`design:check` 若 FAIL，改实现消除违规，**绝不 `--update` 抹平基线**。所有验证结果在 05-evidence 标 Verified / Inferred / Unknown，禁止混用或把 Inferred 冒充 Verified。
