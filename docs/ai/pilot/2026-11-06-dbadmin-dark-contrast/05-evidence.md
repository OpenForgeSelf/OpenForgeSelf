# Evidence

> 阶段：Stage 7｜只记录实际发生的事情；来源等级 Verified / Inferred / Unknown 不得混用。
> 证据日志落 `.temp/*.log`（gitignored，随环境清理）；关键结论已摘录到本文件。

## 0. AC 覆盖映射

| AC | 证据 |
|---|---|
| AC1 | §1 红→绿：修复前 3 红（1.10/1.93/亮度倒置），修复后 5 绿 |
| AC2 | §4 e2e：`light=10.41:1 dark=7.98:1`，1 passed 59.1s |
| AC3 | §2：check 0 error / test 107 files 1159 tests / design:check PASS |
| AC4 | §3：DbAdmin `dist/index.js 68.65 kB` 构建成功 |
| AC5 | §5：dev-stack 走查 + 全新会话 computed 三值 + 截图 w3 |
| AC6 | §5/§3 对应文件：DESIGN.md / ui-ux-design / b-engineering-daily / 02-spec.tpl |
| AC7 | §7 同类扫查摘要 + TODO 逐字条目 |
| AC8 | §6：check-problem-classes OK 25 PCs + Registry 8/8；07 三节 |

## 1. 红灯 → 绿灯（G2 类级守卫）

| 项 | 命令 | 结果 | 等级 |
|---|---|---|---|
| 红灯（修复前） | `cd ForgeSelf.Web && pnpm exec vitest run src/styles/themes/workshop-forge.contrast.test.ts`（2026-11-06 09:19） | **FAIL 3/5**：`light-3=#F9BC4E 当前 0.5666 >= 0.4389`；`dark light-8 #FDE68A vs text-primary #F4F0E7 = 1.10:1`；`dark light-9 #FEF3C7 vs primary #F59E0B = 1.93:1` | Verified |
| 绿灯（修复后） | 同命令（09:20） | **PASS 5/5**，耗时 2ms | Verified |
| 绿灯（路径兼容改动后） | 同命令（10:15） | **PASS 5/5**，耗时 2ms | Verified |

日志：`.temp/theme-contrast-red.log`、`.temp/theme-contrast-green.log`、`.temp/theme-contrast-green2.log`。

## 2. 前端门禁

| 项 | 命令 | 结果 | 等级 |
|---|---|---|---|
| 类型 + lint（09:24） | `cd ForgeSelf.Web && pnpm run check` | exit 0；`✖ 166 problems (0 errors, 166 warnings)`；`[component-conventions] PASS（story 6 / ct.spec 6）` | Verified |
| 单测全量（09:28） | `cd ForgeSelf.Web && pnpm run test` | **107 files / 1159 tests passed**（含 `workshop-forge.contrast.test.ts` 5 passed） | Verified |
| 复跑 check（10:15，并行会话文件干扰） | `pnpm run check` | **FAIL**：仅 `src/styles/pluginTableClassGuard.test.ts`（未跟踪，属并行会话输入3/PC-026 守卫，TS1109/TS1005 语法错）报错；本批改动文件 `pnpm exec eslint …` **exit 0** | Verified（归因并行会话，非本批代码） |
| 本批文件 lint | `pnpm exec eslint src/styles/themes/workshop-forge.contrast.test.ts e2e/plugins/db-admin/db-admin.spec.ts` | **exit 0** | Verified |
| 设计守卫 | `cd ForgeSelf.Web && pnpm run design:check` | `[style-tokens] PASS：无新增硬编码色值，较基线减少 740 处`（1761/2501）；`[design-compliance] PASS`（a=209 b=0 c=0 d=0，与基线同值） | Verified |

日志：`.temp/frontend-check.log`、`.temp/frontend-test.log`、`.temp/design-check.log`。

## 3. 插件构建

| 插件 | 命令 | 结果 | 等级 |
|---|---|---|---|
| DbAdmin（修复主对象） | `cd Plugins/DbAdmin/web && pnpm run build` | PASS：`dist/style.css 12.43 kB`、`dist/index.js 68.65 kB`，376ms | Verified |
| AIAgent（同类 `light-7` 文字修正点） | `cd Plugins/AIAgent/web && pnpm run build` | PASS：`dist/style.css 182.75 kB`、`dist/index.js 298.54 kB`，1.09s | Verified |

日志：`.temp/dbadmin-web-build.log`、`.temp/aiagent-web-build.log`。

## 4. DbAdmin 冒烟 e2e（含 PC-025 实例级断言）

| 项 | 命令 | 结果 | 等级 |
|---|---|---|---|
| 首次（环境复用旧 Vite） | `pnpm exec playwright test --config=playwright.config.ts e2e/plugins/db-admin` | FAIL：`/db-admin` 停在 `.plugin-view-state--error`，资源 `/plugins/db-admin/web/dist/index.js` fetch 失败（旧 dev server 被 `reuseExistingServer` 复用，后端无对应实例） | Verified（环境问题，非本批代码） |
| 干净重跑 | 加 `CI=1 E2E_CLEAN=1`（09:34） | **1 passed (58.4s)** | Verified |
| 复跑取数值 | 加 `CI=1 E2E_CLEAN=1`（09:41） | **1 passed (59.1s)**；控制台输出：`[db-admin] 选中行对比度 light=10.41:1 dark=7.98:1` | Verified |

截图（e2e，已读图核对）：
- `ForgeSelf.Web/screenshots/e2e/db-admin/03a-selected-light.png`：亮色选中行 `#F0B83B` 底 + 深色字，清晰可读。
- `ForgeSelf.Web/screenshots/e2e/db-admin/03b-selected-dark.png`：暗色选中行深棕底 + 浅色字，清晰可读（不再是亮金底浅字）。

说明：中间一轮（09:38 左右）e2e 首测在 schema 列断言超时、重试时 Vite 退出（`ERR_CONNECTION_REFUSED`），属已知 dev server 环境抖动；最终以 09:41 的完整绿灯为准，不掩盖。

日志：`.temp/dbadmin-e2e.log`、`.temp/dbadmin-e2e-r2.log`、`.temp/dbadmin-e2e-r3.log`、`.temp/dbadmin-e2e-r4.log`。

## 5. 起源码本地前后端走查（plugin-development ③）

- `pwsh scripts/dev-stack.ps1`：后端 `:7301`（PID 120012）、前端 `:7399`（PID 61380），数据根 `.temp/dev-stack/data`，`dotnet build` **0 error / 352 warning**；已 `-Stop`。
- 浏览器（真实 dev-stack）新建 SQLite 连接 `WALKTHROUGH-CONTRAST`（文件 `.temp/walkthrough-contrast.db`），测连成功（服务器 3.50.4），保存并选中；亮/暗两态页面可读，无 error 级控制台消息。
- 浏览器会话首次截图 `w1/w2` 为**旧页面缓存**（选中行仍显示亮金），已作废；**全新浏览器会话**复验（10:08）实测：
  - `html.className = "dark"`
  - `getComputedStyle(.node.on).backgroundColor = rgb(120, 53, 15)`（= `#78350F`）
  - `getComputedStyle(.node.on).color = rgb(244, 240, 231)`（= `#F4F0E7`）
  - `--el-color-primary-light-8` computed = `#78350F`
  - 截图 `ForgeSelf.Web/screenshots/dev/db-admin/w3-selected-dark-fresh.png`（暗色整体可读；选中行计算样式已由上面的三值锁定）
- 清理：仅删除本批自建连接 `WALKTHROUGH-CONTRAST`（id=2，API DELETE）；未删除库文件（`.temp` 惰性清理）；未触碰其它连接。
- 走查截图：`screenshots/dev/db-admin/w3-selected-dark-fresh.png`；亮色版视觉证据用 e2e `03a-selected-light.png`（同一 `DbAdminView` 选中行，真实渲染）。

## 6. 问题类归档守卫

| 项 | 命令 | 结果 | 等级 |
|---|---|---|---|
| 台账/签名/守卫 | `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-problem-classes.ps1` | **OK：25 个 PC 签名/allowlist/guards 全部通过**（PC-025 完成） | Verified |
| Registry/Enumeration | `cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~ProblemClassRegistryTests|FullyQualifiedName~ProblemClassEnumerationTests"` | **8 passed, 0 failed** | Verified |
| 07 三节门禁 | `pwsh scripts/check-problem-classes.ps1 -PilotTask 2026-11-06-dbadmin-dark-contrast` | `[pilot-gate] OK：缺陷类 07 三节齐全` | Verified |
| Pilot 工件链 | `pwsh scripts/verify-pilot-artifacts.ps1 -TaskId 2026-11-06-dbadmin-dark-contrast -EnforceHeader -EnforceAcCoverage -EnforceInteraction` | `PASS: 全部 PILOT 工件链齐全（00-07 八件…含关键节）` | Verified |

## 7. 同类扫查结果（摘要）

- 同根因已修：`DbAdmin .node.on`、`.banner-info`（token 级）；`AIAgent RunRecordPanel` 的 `light-7` 文字色；各插件 `light-9` 标签/选中底（token 级）。
- 异根因/不修：`light-3` 按钮 hover 配白字（dark 3.5:1）、浅色 `light-9` 标签 4.02:1、success/error/info 深色 badge 2.88–3.29:1 → 已逐字进 TODO（见 07 §12）。
- 纯 border/box-shadow 用法（QuickLinks/CategoryManager 等）不参与文字对比度，不改。

## 8. 已知限制

- `pnpm run test` 全量在共享工作区运行，包含并行会话未提交的插件生态改动；本批新增测试 5 条，全量 1159 条通过，未出现新增红。
- e2e 首次失败为旧 Vite 复用环境问题；已用 `CI=1 E2E_CLEAN=1` 隔离复跑并连续两次全绿。
- 浏览器走查的 w1/w2 截图因旧页面缓存作废，不作为证据；以全新会话的 computed 三值与 e2e 双态截图为准。
