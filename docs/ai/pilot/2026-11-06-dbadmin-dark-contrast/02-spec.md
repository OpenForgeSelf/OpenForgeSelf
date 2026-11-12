# Spec — 暗色连接名不可见（主题 token 倒置）修复 + 规范/走查/守卫补强

> Task ID：`2026-11-06-dbadmin-dark-contrast`｜AC 编号无连字符（机器查 AC token）
> 真源：`DESIGN.md`（UI 规范）+ `ForgeSelf.Web/src/styles/themes/workshop-forge.css`（token 实现）+ `forgeself-design/themes/workshop-forge.css`（设计稿 token）+ `ui-ux-design` 走查清单。

## 1 背景与目标

暗色下 DbAdmin 选中连接行背景 `--el-color-primary-light-8 = #FDE68A`、文字 `--el-text-color-primary = #F4F0E7`，WCAG 对比度 1.10:1（用户截图）。根因是 `html.dark` 中 `--el-color-primary-light-3..9` 被定义成比主色更亮的浅色，与 Element Plus 暗色语义（light-N = 主色与黑混合的背景级深色）及设计稿 token（`forgeself-design/themes/workshop-forge.css:112-116`：`light-3:#D97706 … light-8:#78350F light-9:#451A03`）相反。本批同时补规范、走查清单、Spec 模板与机器守卫，防止同类回归。

## 2 范围（In / Out）

**In**：
- `ForgeSelf.Web/src/styles/themes/workshop-forge.css` 暗色 primary light-3/5/7/8/9 五值。
- 同根因扫描命中的「把 light-N 当文字色」用法（`Plugins/AIAgent/web/src/components/RunRecordPanel.vue:1363`）。
- `AGENTS.md`（§8 设计门禁第 3 步）、`DESIGN.md`、`.agents/skills/ui-ux-design/SKILL.md`、`docs/04-standards/agent-workflow/b-engineering-daily.md`、`docs/18-templates/ai-pilot/02-spec.tpl.md`。
- 新守卫 `ForgeSelf.Web/src/styles/themes/workshop-forge.contrast.test.ts`。
- `ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts` 冒烟内新增亮暗对比度断言。
- `Plugins/DbAdmin/docs/ui-design-brief.md` 补选中态视觉说明。
- PC-025 + case + 三处索引 + 本 pilot 工件。

**Out**：
- 不改后端代码/接口/数据库；不改动其它语义色（success/warning/error/info）的 dark ramp（其对比度问题属另一根因，记 TODO）。
- 不改 `.node.on` 的结构与交互语义；不改宿主布局/组件；不发布、不提交。

## 3 功能需求

- **FR1** 暗色 primary ramp 恢复「逐级更暗」：`light-3:#D97706`、`light-5:#B45309`、`light-7:#92400E`、`light-8:#78350F`、`light-9:#451A03`（与设计稿真源一致）。
- **FR2** 选中连接行（`.node.on`）在暗色下背景 `#78350F`、文字 `#F4F0E7`，实测对比度 ≥4.5:1（目标 7.98:1）；浅色下保持现状（`#F0B83B` + `#14110F`）且 ≥4.5:1。
- **FR3** 扫描全仓 `--el-color-primary-light-3/5/7/8/9` 作为 `color:` 的用法；同根因命中项改为 `--el-color-primary` 或其它可读前景 token。
- **FR4** 新增类级守卫：解析主题 CSS，断言暗色 light-N 亮度严格小于主色、light-8/9 与默认前景/主色对比度 ≥4.5、浅色选中行 ≥4.5。
- **FR5** 走查规范补强：新增「主题 token 明暗语义」不变量与「选中/激活/标签/横幅底 × light/dark」检查项；明确 e2e/走查必须显式切暗色。
- **FR6** 问题类归档 PC-025（含 signature + G2/G1 守卫 + lens），case 记录复发/逃逸分析。

## 4 Input / Output

- 输入：CSS token 文件、组件样式、e2e 页面主题态。
- 输出：修复后的 token 值；vitest/e2e 的 PASS/FAIL；05-evidence 中的对比度数值与截图路径。
- 不涉及 HTTP/数据契约。

## 5 Business Rules

1. `--el-color-primary-light-N` 在暗色下是**背景级 token**：`N` 越大越暗，禁止比 `--el-color-primary` 更亮。
2. 任何 primary 浅底（light-8/9）作背景的表面，正文/标签文字必须使用 `--el-text-color-primary` 或 `--el-color-primary`（或按 AA 计算后的可读 token），不得沿用无对比度保证的默认继承色。
3. 不把 `light-N` 当文字色使用（暗色下会变成深棕，浅色下会变成亮金——跨主题不可控）；文字强调统一用 `--el-color-primary`。
4. 主题验证必须亮/暗两态各一次；默认 `system` 在 CI/Playwright 下解析为 light，**不算暗色验证**。

## 6 Boundary Conditions

- 浅色主题 primary ramp 不变（`#F0B83B/#F9E4B8` 与深色文字搭配已达标）。
- 只改 primary 族；success/warning/error/info 的 dark ramp 保持现状（其标签底色对比度 2.8–3.3 属另类，登记 TODO）。
- 宿主中把 light-3 当按钮 hover 底且配白字的场景，改后对比度从 ~1.7 提升到 ~3.5，仍低于 AA 4.5；属文字色与底色配对问题（另一根因），本批只记录，不扩大改色范围。
- 插件远程加载共享宿主 CSS 变量；改宿主 token 即对插件生效，无需改插件构建 externals。

## 7 Error Handling

- 守卫失败信息必须包含 token 名、实测对比度与阈值，便于定位（例：`dark primary-light-8 vs text-primary = 1.10 < 4.5`）。
- e2e 断言失败时输出实际 background/color 值与计算出的 ratio。
- 若主题文件解析不到 `html.dark` 块，测试以「解析失败」红灯退出，不得静默跳过。

## 8 Compatibility

- 与 Element Plus 暗色语义对齐；EP 内置 tag/alert 在暗色下的 light-9 背景会由亮色变为深色，文字对比度提升。
- 与 `design:check`/`check-style-tokens` 无冲突（themes 目录豁免）。
- 与 `dark` class 切换机制兼容（`stores/theme.ts:76-87`）。
- 不改变任何 API、插件清单或数据。

## 9 Non-functional Requirements

- 无运行时性能影响（仅 CSS 变量值 + 测试解析）。
- 守卫纯静态解析，毫秒级，可在 `pnpm run test` 快档反复跑。
- 对比度计算使用 WCAG 2.x 相对亮度公式，结果可复算。

## 10 Interaction Design（有 UI · 点什么出现什么 · 五态 · 验收）

本批不改交互逻辑，只改视觉目标态；涉及以下交互点：

| 交互点 | 触发 | 结果（点什么出现什么） | 状态模型 | 反馈 | 空态 | 边界 |
| --- | --- | --- | --- | --- | --- | --- |
| 选择连接 | 点击左栏连接节点 | 节点进入选中态（`.node.on`），右区连接条切换；选中行文字保持可读 | 未选/选中/hover | 选中背景 + 文字颜色变化 | 无自建连接时显示空引导 | 暗色/浅色两态均 ≥4.5:1 |
| 主题切换 | 设置页切 light/dark/system | 全站 token 重算，插件同步 | light/dark/system | 即时切换 | — | 暗色必须实测，不靠 system 默认 |
| 标签/横幅 | 任务状态/错误提示出现 | primary-light-9 浅底 + 主色/正文 | 正常/警告/错误 | 颜色 + 文案 | — | 暗色下对比度 ≥4.5 |

### 视觉规格（目标态 · 逐表面 × light/dark）

| 表面 | light（现状保持） | dark（本批目标） | 对比度判据 |
| --- | --- | --- | --- |
| 左栏选中连接行 `.node.on` | bg `--el-color-primary-light-8 #F0B83B` / fg `--el-text-color-primary #14110F` | bg `--el-color-primary-light-8 #78350F` / fg `--el-text-color-primary #F4F0E7` | 两态 ≥4.5:1（dark 目标 7.98） |
| 状态标签 `.tag-primary` 等 | bg `light-9 #F9E4B8` / fg `--el-color-primary #B45309` | bg `light-9 #451A03` / fg `--el-color-primary #F59E0B` | dark 目标 6.97；light 现状 4.02 记 TODO |
| 信息横幅 `.banner-info` | bg `light-9` / fg `--el-color-primary` | bg `light-9 #451A03` / fg `--el-color-primary` | 同标签行 |
| 主色按钮 hover（AgentHub 等） | bg `light-3 #C2610A` / fg white | bg `light-3 #D97706` / fg white | 从 1.7 提升到 3.5；白字配对问题记 TODO |
| 运行记录 human 行 | fg `light-7 #E59A1F` on light bg | fg `--el-color-primary #F59E0B` on dark bg | ≥4.5 |

## 11 Failure Modes（Stage 0 命中 PC 的预填）

- **PC-025（新建）**：暗色 `--el-color-primary-light-N` 明暗倒置 → 选中行/标签/横幅文字不可见。
  - 本任务满足：FR1 修复 ramp；FR2 e2e 锁 DbAdmin；FR4 vitest 枚举 all light-N；FR5 规范补条目；FR6 归档。
  - 历史复发：2026-10-15 IM 网关暗色对比度修复（TODO 旧条目，未立 PC），按二次复发做逃逸分析（见 case §8）。
- **PC-019（flex 压缩/撑破）**：本批不改布局，lens 检查 `.node` 的 `white-space:nowrap` 与 `overflow:hidden` 已有省略链（`DbAdminView.vue:1023-1026`）→ 不适用。
- **PC-002/009**：不涉及 fetch/JSON。
- **PC-006/007**：脚本一律 `pwsh`、本地起服务只用 `dev-stack.ps1`。

## 12 Unknown

- 宿主其它使用 `--el-color-primary-light-9` 作底色的存量页面（PluginStore/PluginDetail/DevTools 等）在暗色下的观感：以 dev-stack 亮/暗截图抽查为准；若发现仍低于 4.5 的同根因项，按同类扫查范围修 token 或记 TODO。
- `AgentHub .ok-btn--primary:hover` 白字 3.5 的 AA 缺口：属按钮前景配对（非本类），本批记 TODO P3。

## 13 Acceptance Criteria（AC1..AC8，05 映射表逐字含同 token）

- **AC1** `workshop-forge.contrast.spec.ts` 修复前红、修复后绿；断言暗色 light-3/5/7/8/9 亮度逐级低于主色，light-8 vs text-primary ≥4.5，light-9 vs primary ≥4.5，浅色选中行 ≥4.5。
- **AC2** DbAdmin e2e 冒烟在选中连接后实测 light/dark 对比度均 ≥4.5（detail 输出实际数值）。
- **AC3** `pnpm run check`、`pnpm run test`、`pnpm run design:check` 全绿（本批过滤/全量见 03-plan）。
- **AC4** `Plugins/DbAdmin/web pnpm run build` 通过（产物 `dist/index.js` 存在）。
- **AC5** dev-stack 亮/暗两态走查 DbAdmin 选中行可读，截图存 `ForgeSelf.Web/screenshots/dev/db-admin/`，05-evidence 记数值。
- **AC6** 规范补强落地：`DESIGN.md` token 语义条目、`ui-ux-design` 三类表面清单、`b-engineering-daily` UI 批规则、`02-spec.tpl.md` 矩阵要求。
- **AC7** 同类扫查表齐全；同根因项已修，异根因项进 TODO（逐字可检索）。
- **AC8** PC-025 + case + `problem-classes/README.md`/`cases/README.md`/`index.md` 三处索引齐；`check-problem-classes.ps1` 绿；07 三节齐。
