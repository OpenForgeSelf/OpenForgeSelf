# 05 · Evidence（验证证据）

> 任务：`sems-workbench-1.3.0` ｜ 日期：2026-11-09 ｜ 落点：`仓库根目录`（完整仓库，纯源码树无 `.git`）
> 原则：**只记录验证环境实际执行过的命令与实测输出**；未执行项一律标 `Unknown / 环境受限`，不得以历史数据、外部报告或推断代替。
> 关联工件：`00-repository-understanding.md` / `01-intent.md` / `02-spec.md` / `03-plan.md` / `04-task.md` / `06-review.md` / `07-final-report.md`

---

## 1. 验证环境（实测）

| 项 | 实测值 | 影响 |
| --- | --- | --- |
| Node.js | `v22.13.1` | 前端构建 / 单测 / 守卫可用 |
| pnpm | `10.28.2` | 可用 |
| Chromium | `Google Chrome 144.0.7559.132` | 无头逐屏渲染验证可用 |
| Python | `3.11.1` | 辅助脚本可用 |
| `.NET SDK` | `6.0.301` | **不匹配**仓库目标 `net10.0-windows` → `dotnet build` / `dotnet test` 不可执行 |
| `pwsh` | `MISSING` | `scripts/dev-stack.ps1`、发布链不可执行 |
| 宿主运行实例 | 无 | 宿主 e2e（`sems.spec.ts`）无法完整执行；`/api/*` 由验证页按设计降级为 DEMO |

---

## 2. 门禁与构建（全部实跑，命令可复现）

工作目录 `Plugins/Sems/web`（构建/单测）与仓库根（守卫）。

| # | 命令 | 实测输出 | 结论 |
| --- | --- | --- | --- |
| E1 | `pnpm build` | `vite v6.4.3`；`✓ 54 modules transformed`；`dist/index.js 175.69 kB │ gzip 42.73 kB`；`dist/style.css 50.69 kB │ gzip 7.72 kB`；`✓ built in 441ms` | **PASS** |
| E2 | `pnpm test` | `Test Files 4 passed (4)`；`Tests 32 passed (32)`；`confirmOps 8` / `runDuration 2` / `specOps 13` / `workbench/taskState 9` | **PASS** |
| E3 | `node scripts/check-design-compliance.mjs` | `a(token族)=209 b(胶囊)=0 c(玻璃过头)=0 d(蓝紫渐变)=0（基线 209/0/0/0）`；`PASS：无新增设计规范违反` | **PASS** |
| E4 | `node scripts/check-style-tokens.mjs` | `硬编码色值合计 1759（基线 2501）`；`PASS：无新增硬编码色值，较基线减少 742 处` | **PASS** |
| E5 | `stat` 产物 | `dist/index.js 175694 bytes` / `dist/style.css 50694 bytes` | 与 E1 一致 |

> E3/E4 **未使用 `--update`**，棘轮基线未被抹平。`Plugins/Sems/web/src` 在设计守卫中的分项基线 `a=0,b=0,c=0,d=0` 保持不变；E3 打印的 `a=209` 是全仓合计，与基线相等故 PASS。

---

## 3. 静态一致性核对

### 3.1 工具名三方一致（评审 RK-03 / P3「13 → 22」）

复跑守卫：`node docs/ai/pilot/2026-11-09-sems-workbench-1.3.0/evidence/check-tool-parity.mjs`

| 来源 | 数量 | 说明 |
| --- | ---: | --- |
| `ForgeSelf.Web/e2e/plugins/sems/sems.spec.ts` 的 `SEMS_TOOLS` | **22** | 已补齐 9 个 spec 工具 |
| `ToolExtensions.cs` + `SpecTools.cs` 的 `base(pluginId, "sems_*")` | **22** | 13 基础 + 9 规格 |
| `SemsPlugin.cs` 的 `ToolExtensions.Add(...)` 调用 | **22** | 注册入口 |
| 仅 e2e 有 / 仅后端有 | `[]` / `[]` | 无差异 |
| 名字与顺序完全一致 | `true` | — |

结论：**PASS 三方一致（22 / 22 / 22）**。第 517 行 `toHaveLength(SEMS_TOOLS.length)` 随数组自动变为 22；文件头注释、数组注释、test 标题中的「13 个」已改为「22 个」（数组注释中「13 个基础工具 + 9 个规格工具」为刻意保留的构成说明）。

### 3.2 端点数（评审 RK-06 / P5「21 → 25」）

`grep -cE '\[Http(Get|Post|Put|Delete)' Plugins/Sems/Controllers/*.cs`：

| Controller | 端点数 |
| --- | ---: |
| `ProjectCommandsController.cs` | 7 |
| `ProjectsController.cs` | 6 |
| `RunsController.cs` | 3 |
| `SpecsController.cs` | 9 |
| **合计** | **25** |

与评审实测一致，无需纠正。

### 3.3 版本号（评审 RK-11）

`Plugins/Sems/plugin.json:5` `Version = "1.3.0"`；`Plugins/Sems/web/package.json:3` `version = "1.3.0"`。宿主 e2e 的 `PLUGIN_VERSION` 为运行时动态读取清单，徽标断言 `v1.3.0` 不写死字面量。

### 3.4 e2e 语法解析（验证环境能做到的最深静态检查）

`esbuild@0.25.0` 对修改后的 `sems.spec.ts` 做 `transformSync(loader:'ts', target:'es2022')`：`OK 解析成功，输出 28669 字节，warnings = 0`。

---

## 4. Chromium 逐屏渲染验证（明暗双模，22 张截图）

脚本：`evidence/verify.cjs`（复现方式见 §7）。产物：`evidence/verify-report.json` + `evidence/shots/*.png`（22 张）。

### 4.1 一次必须记录的验证缺陷与修复

**旧脚本是假验证**：初版用 `location.hash = '/dashboard'`，但 `workbenchStore.ts` 默认走内存路由，只有 URL 带 `?semsHash=1` 才启用 `#sems<route>` 命名空间 hash（`USE_HASH` / `HASH_PREFIX='#sems'`，`WorkbenchView.vue` 的 `onMounted` 注册 `hashchange → syncFromHash`）。旧脚本 URL 无该参数、hash 前缀也不对，导致 **10 张截图其实是同一个首页**，报告里 10 屏指标完全一致却未被告警。

本轮修正为 `?semsHash=1` + `#sems<route>`，并加入三重「真切屏」断言：

1. 每次切屏后校验 `location.hash === '#sems' + route`；
2. 记录**排除 `.statusbar` 实时时钟后**的主内容文本 SHA1，断言 10 屏两两互异（不排除时钟会因秒数变化产生假互异）；
3. 项目内 6 屏 + 向导屏断言 `h2` / 主文本命中该屏唯一标题。

同时把「非 `/api/*` 的 404」与 `PAGEERROR` 一律判为失败，避免把资源缺失当成可忽略噪声。

### 4.2 实测结果

| 指标 | light（`html.dark=false`） | dark（`html.dark=true`） |
| --- | --- | --- |
| 逐屏互异内容数 | **10 / 10** | **10 / 10** |
| 强调色 `--el-color-primary` | `#B45309` | `#F59E0B` |
| 状态栏 `color` / `bg` | `rgb(20,17,15)` / `rgb(255,255,255)` | `rgb(244,240,231)` / `rgb(27,24,21)` |
| 顶栏背景 | `rgb(251,247,238)` | `rgb(20,17,15)` |
| 自定义 `--sp-2` 仍被定义 | `false` | `false` |
| 经典视图入口数（首屏） | `2` | `2` |
| 胶囊按钮（`999px`）计数 | `0` | `0` |
| 假能力词命中 | `[]` | `[]` |
| 横向溢出 | `false` | `false` |
| 状态栏可读（`color !== bg`） | `true` | `true` |
| 按钮圆角 | `4px`（`--radius-sm`） | `4px` |
| 375px 窄屏 `scrollWidth` / `clientWidth` | `375 / 375`，溢出元素 `[]` | `375 / 375`，溢出元素 `[]` |
| `PAGEERROR` | `0` | `0` |
| 问题数 | **0** | **0** |

**切屏真实性旁证**：修复后验证页捕获的 404 由 2 个增加到 5 个，且全部为 `/api/*`：`/api/projects`、`/api/plugin`、`/api/projects/9001/commands`、`/api/runs`、`/api/projects/9001/specs`。说明 commands / runs / specs 屏确实发起了各自的数据请求，而非停在同一页。这 5 个 404 是「验证页不提供后端、插件按设计降级 DEMO」的预期结果，单列统计不计入实现缺陷。

各屏 `h2` 实测（证明逐屏命中）：

| 屏 | hash | h2 |
| --- | --- | --- |
| 01-projects | `#sems/projects` | `[]`（管理器屏，靠文本哈希互异校验） |
| 02-create | `#sems/create` | `[]` |
| 03-wizard | `#sems/create/wizard` | `[]`（主文本命中「登记项目到 Sems」） |
| 04-import | `#sems/import` | `[]` |
| 05-dashboard | `#sems/dashboard` | `["项目仪表盘"]` |
| 06-tools | `#sems/plugins` | `["Sems 工具（22 / 22）"]` |
| 07-commands | `#sems/dependencies` | `["运行命令"]` |
| 08-settings | `#sems/configuration` | `["项目设置"]` |
| 09-tasks | `#sems/tasks` | `["项目任务","运行面板 0"]` |
| 10-specs | `#sems/specs` | `["项目规格"]` |

### 4.3 读图复核（人工判读，非自动断言）

- `light-01-projects.png`：修复前右上角「经典视图」按钮被压成**竖排 4 个字**并与帮助图标叠在一起；修复后为横排带边框按钮 + 独立帮助图标，无重叠。
- `light-11-narrow-375.png`：修复前该按钮与居中标题「Sems 项目工作台」文字重叠；修复后窄屏下缩为图标钮（`aria-label="切换经典视图"` 保留，`svg.ic` 固定 px 尺寸不受 `font-size:0` 影响），标题加 `max-width` 防溢出，重叠消失。
- `dark-06-tools.png`：暗色主题下「不是插件市场」语义说明条、`Sems 工具（22 / 22）` 标题、22 条工具行可读，对比度正常。

**顶栏挤压根因**：`.wb .topbar .corner button{width:26px;...}` 无差别作用于所有 `.corner button`，把新增的 `.wb-legacy-top` 宽按钮压成 26px。修复为 `button:not(.wb-legacy-top)`，并给 `.wb-legacy-top` 补 `width:auto;flex:none;white-space:nowrap`；窄屏 `≤640px` 再缩为图标钮。

---

## 5. 评审整改项落地证据

| 评审项 | 整改动作 | 证据 |
| --- | --- | --- |
| 主题口径：零自定义 token | 删除 `workbench.css` 的 `.wb{}` 原型 54 个自定义 token，全部改用宿主 `workshop-forge.css` 的 `--el-*` / `--gold-*` / `--ink-*`，间距字号写字面量 px | `--sp-2` 解析值为空、明暗强调色随 `html.dark` 自动切换、E3 PASS |
| 假能力 UI（D1–D3） | 向导改「登记项目到 Sems」+ 不可忽略说明条（不创建/修改文件、不跑 npm/git）；「依赖」屏改「运行命令」并删「安装依赖」；「插件」屏改「Sems 工具（N / 22）」并删添加/下载/升级死按钮；「配置」屏改「项目设置」并删「保存配置」死按钮；仪表盘「端口占用 / 结束占用进程」改「命令端口（只读）」 | 22 屏 `fakeWords = []`；`Sems 工具（22 / 22）` h2 实测 |
| 经典视图双入口（D4） | 顶栏 `wb-legacy-top` + 侧栏 `wb-legacy-side`，`SemsView` 模式条另有一处 | 每主题首屏 `legacyEntries = 2`（工作台内双入口） |
| 窄屏 375px（X5） | `.project-actions` 换行、状态栏隐藏版本串与 INFO、顶栏按钮缩图标 | `375/375`，`wide=[]` |
| e2e 工具数 13→22、默认工作台导致旧选择器全落空（RK-02/03） | 补 9 个 spec 工具名；新增 `injectSemsClassicView(page)`，在页面脚本执行前预置 `localStorage['sems-workbench-v1'].viewMode='legacy'`（3 处 `goto` 前均调用，`addInitScript` 对 `reload` 亦生效）；补双模式模式条可见性断言 | §3.1 三方一致；§3.4 语法解析 0 warnings |
| 端点数 21→25、版本 1.3.0 | 文档口径统一 | §3.2 / §3.3 |

---

## 6. 未执行项（`Unknown / 环境受限`，不得冒充通过）

| 项 | 阻塞原因 | 需要的环境 |
| --- | --- | --- |
| `dotnet build` / `dotnet test` | 验证环境 `dotnet 6.0.301` ≠ 目标 `net10.0-windows` | .NET 10 SDK + Windows 目标 |
| `pwsh scripts/dev-stack.ps1` 源码起栈走查 | 无 `pwsh` | PowerShell 7 |
| 宿主 e2e `sems.spec.ts` 完整执行 | 需宿主运行实例 + 真实 token（`injectRealApiKey`） | 完整 dev-stack |
| 发布链（`release-local.ps1 -UpdateDir` / 打 tag / 运行实例只读复验） | 环境 + `AGENTS.md §8.5` 列为**必须请示项**，发布动作未纳入本次范围 | 发布授权 + Windows/PowerShell |
| `?semsHash=1` 在宿主的前进/后退行为（RK-13） | 需宿主路由环境 | 宿主侧确认 |

> 验证环境对 e2e 只做到「静态适配 + 语法解析 + 三方工具名核对 + 逻辑推演」，**未声称 e2e 通过**。

---

## 7. 证据文件清单与复现

| 文件 | 说明 |
| --- | --- |
| `evidence/verify-report.json` | 完整逐屏验证报告（22 屏 × 明暗，含 hash / SHA1 / h2 / 计算样式） |
| `evidence/shots/*.png` | 22 张截图：`{light,dark}-{01..10}-*.png` + `{light,dark}-11-narrow-375.png` |
| `evidence/verify.cjs` | 验证脚本；依赖 `playwright-core`（由工程 `node_modules` 提供）与本机 Chromium（`CHROMIUM_PATH` 可指定）。脚本内 `ROOT = __dirname`，复现时把 `Plugins/Sems/web/dist/{index.js,style.css}`、`ForgeSelf.Web/src/styles/themes/workshop-forge.css`、Vue ESM 浏览器版、`element-plus` 最小 stub 与 `index.html` 放到**脚本所在目录**（`dist/` 子目录放构建产物），再 `node verify.cjs` |
| `evidence/check-tool-parity.mjs` | 零依赖守卫，直接在仓库根 `node <path>` 复跑 |

**最小复现命令**：

```bash
cd 仓库根目录
pnpm --dir Plugins/Sems/web build
pnpm --dir Plugins/Sems/web test
node scripts/check-design-compliance.mjs
node scripts/check-style-tokens.mjs
node docs/ai/pilot/2026-11-09-sems-workbench-1.3.0/evidence/check-tool-parity.mjs
```
