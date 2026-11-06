---
id: 2026-11-06-dbadmin-dark-connection-contrast
title: 暗色下 DbAdmin 选中连接名不可见（light-8 亮金底 + 浅色文字 1.10:1）
pc: PC-025
severity: P1
evidence: Verified
---
# Case：2026-11-06 DbAdmin 暗色连接名不可见

> Case 只讲「这一次发生了什么」；规则/修法/扫描方法引用 PC-025，不复述。

## 1 现象

用户在暗色主题下截图（附件 `72916e40-…_image.png`）：DbAdmin 左栏「我的连接」中选中连接 `IOC_TEST` 行背景为亮金，文字几乎看不见；未选中的 `walkthrough-fixture` 正常。肉眼判断为「连接名在暗色模式下不可见」。

## 2 复现红灯

新增类级守卫 `ForgeSelf.Web/src/styles/themes/workshop-forge.contrast.test.ts`，在修复前跑：

```bash
cd ForgeSelf.Web && pnpm exec vitest run src/styles/themes/workshop-forge.contrast.test.ts
```

真实输出（2026-11-06 09:19，`Tests 3 failed | 2 passed`）：

- `--el-color-primary-light-3=#F9BC4E 必须比 --el-color-primary 更暗（当前 0.5666 >= 0.4389）`
- `dark light-8 #FDE68A vs text-primary #F4F0E7 = 1.10:1`
- `dark light-9 #FEF3C7 vs primary #F59E0B = 1.93:1`

修复后同命令 `5 passed`（2026-11-06 09:20）。

## 3 根因（5 Whys + 第 6 问）

1. 为什么连接名看不见？→ `.node.on` 背景 `--el-color-primary-light-8=#FDE68A`，文字继承 `--el-text-color-primary=#F4F0E7`，对比度 1.10:1（`DbAdminView.vue:1000,1025`）。
2. 为什么 light-8 是亮金？→ `html.dark` 段把 `--el-color-primary-light-3..9` 定义成比主色更亮的浅色（`workshop-forge.css:188-195` 修复前）。
3. 为什么会写成亮色？→ 2026-10-11 design overhaul（`a99f5e2`）重写主题时，primary ramp 沿用了浅色模式的亮金思路；设计稿真源 `forgeself-design/themes/workshop-forge.css:112-116` 的暗色 ramp 本应逐级变暗，实现层漂移未被发现。
4. 为什么走查没发现？→ 2026-11-05 db-admin 收口走查按 8 个功能点执行，均在默认主题（`system` → 浅色）；02-spec 没有为「选中行」写视觉规格与亮/暗矩阵；走查清单的 D/E 条款存在但未点名 primary-light-8/9 表面。
5. 为什么 e2e 没发现？→ `ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts` 只断言元素可见/文本，没有 computedStyle 对比度断言；默认主题浅色，暗色从未进入自动化。

- **第 6 问：为什么现有流程/守卫没让我一开始就写对？** → 归因「有原则无机器判据 + 走查粒度不足」：`DESIGN.md` 有 4.5:1 原则、`ui-ux-design` 有 D/D-1 清单，但没有「暗色 light-N 必须逐级更暗」的 token 不变量，也没有机器扫；`design:check` 只拦裸色值/自定义 token 族，不校验 token 语义；e2e 默认浅色，导致暗色表面永远不在证据面。

## 4 同类扫查命中表

| 位置（文件:行） | 同根因？ | 处置（已修 / 已守卫 / TODO） |
|-----------------|----------|-------------------------------|
| `Plugins/DbAdmin/web/src/DbAdminView.vue:1025` `.node.on` light-8 底 | 是（用户指认点） | 已修（token ramp 修复，dark 7.98:1） |
| `Plugins/DbAdmin/web/src/DbAdminView.vue:1015` `.banner-info` light-9 底 + primary 字 | 是（1.93:1） | 已修（dark 6.97:1） |
| `Plugins/AIAgent/web/src/components/RunRecordPanel.vue:1363` `color: light-7` | 是（把 light-N 当文字色） | 已修（改 `--el-color-primary`） |
| `Plugins/AgentHub/web/src/AgentHubEventStream.vue:196` `color: light-3` | 是（同模式，但改后深棕 #D97706 对深底 5.55:1） | 已守卫（token 修复后达标；保留） |
| `ForgeSelf.Web/src/components/MessageItem.vue:68` `.message-user` light-9 底 + 继承正文 | 是（修复前亮底浅字） | 已修（token 级，dark 16+:1） |
| `Plugins/TodoTracker` / `QuickLinks` / `McpCenter` / `FileTools` 等 `light-9` 底 + primary 字 | 是（约 30 处标签/选中底） | 已修（token 级，dark 6.97:1；vitest 覆盖 token 语义） |
| `ForgeSelf.Web/src/components/MessageInput.vue:150` / `Plugins/Home ... :1141` 等 light-3 hover + 白字 | 是（底色变暗后白字仍 3.5:1，未达 AA 4.5） | TODO P3（按钮前景配对属另一根因，不扩大本批） |
| `Plugins/QuickLinks` light-8 用于 `box-shadow` / `TaskDetail` `light-9` 背景 | 否（非文字/不参与文字对比） | 不改 |
| `Plugins/AIAgent/docs/workbench-prototype.html` light-7 文字 | 否（历史原型文档，不在运行时 UI） | 不改 |

## 5 修复与被否方案

**最终修法**：按设计稿真源修 `ForgeSelf.Web/src/styles/themes/workshop-forge.css` 暗色 primary ramp（`#D97706/#B45309/#92400E/#78350F/#451A03`），并把 `RunRecordPanel` 的 light-7 文字色改为主色。canonical fix 见 PC-025 §3。

**被否方案**：
- 只改 `.node.on` 背景为深色：单点补丁，标签/横幅/按钮 hover 同类问题仍在，用户要求的「下次不再发生」无法满足。
- 在组件里用 `color-mix`/`!important` 局部调：绕开 token 单一来源，新增漂移点，且 DESIGN.md 禁止打补丁式覆盖。

## 6 重构提案与结论

**R1（局部，已执行）**：主题 token 段 5 行 + 一个文字色用法，属同模块（UI token）局部修复，本批内完成并在 07 披露。无 R2/R3 提案：不改公共契约、不需要架构级改造。

## 7 守卫落地

| 等级 | 路径 | 状态 | 说明 |
|------|------|------|------|
| G2 | `ForgeSelf.Web/src/styles/themes/workshop-forge.contrast.test.ts` | active | 解析两套主题，枚举 light-3/5/7/8/9 亮度与 light-8/9 对比度；修复前 3 红，修复后 5 绿 |
| G1 | `ForgeSelf.Web/e2e/plugins/db-admin/db-admin.spec.ts` | active | 选中连接后实测 light/dark 选中行对比度 ≥4.5，并截 `03a/03b` 两图 |
| G3 | PC-025 signature S1/S2 + `check-problem-classes.ps1` | active | 主题文件重新出现倒置亮金值即红 |

**为什么上次没防住**：前案 2026-10-15 IM 网关暗色对比度修复只做了实例级 CSS 修正，没有立 PC、没有 token 语义守卫；本轮是同类第二次出现，按 problem-class-closure 强制 ≥G2，补了 token 枚举测试 + e2e 双态断言 + 规范条目。

## 8 逃逸分析（二次复发）

- **前案**：2026-10-15 IM 网关「浅色用户气泡白字压浅灰底 1.06:1」修复（TODO 旧条目，pilot 05 §E7 关闭），当时只验了浅色并修实例样式，**未归档 PC、未建 token 不变量守卫**。
- **守卫缺口归因**：① 流程层——UI 批 Spec 视觉规格（2026-10-31 已立）在 db-admin 收口批未写入选中的逐表面矩阵；② 工具层——`design:check` 不校验 token 语义；③ 自动化层——e2e 默认 light，暗色无断言；④ 文档层——走查清单有对比度原则但无「选中/标签/横幅」具体表面。
- **上浮动作（已落地）**：`DESIGN.md` 增 token 语义不变量与门禁表行；`ui-ux-design` D-1/E 增具体表面与「默认主题不算暗色验证」；`b-engineering-daily` UI 批规则增主题矩阵具体表面 + 类级处理；`02-spec.tpl.md` 主题矩阵条目增显式 dark 要求。

## 9 基建改进

- 新增可复跑的 token 对比度守卫（`pnpm run test` 快档即跑，无需浏览器）。
- DbAdmin 冒烟 e2e 从「元素可见」升级为「元素可读」，把主题双态纳入唯一冒烟用例（未新增第二条 e2e，遵守每插件 1 条冒烟铁律）。
- 规范/模板/走查清单补强，形成「原则 → 具体表面 → 机器判据」三层闭环。

## 10 复用赋能

- 所有含 `web/` 的插件新增/修改选中行、标签、横幅、按钮 hover 时必须引用 PC-025 lens（文字 token / 亮暗对比度 / 是否当文字色）。
- 新增页面/查询/组件走查必须显式切 dark 并实测三表面；`ui-ux-design` D-1/E 已固化。
- 主题 token 改动一律跑 `workshop-forge.contrast.test.ts`；新增主题形态需扩该测试。

## 11 证据

| 结论 | 等级 | 出处（文件:行 / 命令输出 / 截图） |
|------|------|-----------------------------------|
| 暗色选中行 1.10:1（修复前） | Verified | vitest 红灯输出 `Theme contrast: dark light-8 #FDE68A vs text-primary #F4F0E7 = 1.10:1`（`.temp/theme-contrast-red.log`，2026-11-06 09:19） |
| 暗色标签/横幅 1.93:1（修复前） | Verified | 同上红灯 `dark light-9 #FEF3C7 vs primary #F59E0B = 1.93:1` |
| 修复后 token 语义与对比度达标 | Verified | `workshop-forge.contrast.test.ts` 5 passed（2026-11-06 09:20） |
| `.node.on` 使用 light-8 + 默认前景 | Verified | `Plugins/DbAdmin/web/src/DbAdminView.vue:1000,1025` |
| 暗色 ramp 与设计稿一致 | Verified | `ForgeSelf.Web/src/styles/themes/workshop-forge.css:188-196` vs `forgeself-design/themes/workshop-forge.css:112-116` |
| 默认主题 system→light | Verified | `ForgeSelf.Web/src/stores/theme.ts:71-74,99-102` |
| 用户截图 | Verified | 附件 `c:\Users\12504\.trae-cn\attachments\6aed27d7a319e8e5020aea5a\72916e40-…_image.png` |
