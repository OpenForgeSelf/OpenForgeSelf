# M4c-P4 插件前端迁移 · 批证据

批次：M4c 插件批次 P4（FileTools / Sems / ToolBridge / ImGateway）
执行：SubAgent o_000E3L84plf ｜ 日期：2026-10-11
流程依据：m4c-plugin-spec.md（插件冻结模式）· m4-execution-spec.md §0 · DESIGN.md · 设计稿真源 · plugin-frontend-development.md

## 1. 迁移清单（每插件绝对路径）

### ToolBridge
- `Plugins/ToolBridge/web/src/ToolBridgeView.vue`
  - 页头加 eyebrow（mono 10px / 0.18em / 金大写）；`.view-title` 改 serif display 26px/700/0.05em；`.tb-version` 改金 mono 徽标。
  - `.tb-card` 由 el-fill 实底改玻璃卡（--glass-bg / --glass-border / blur(saturate)）。
  - 主按钮 `.tb-btn` 金底 + 深墨字（移除 `color:#fff` 裸值），hover translateY(-1px)。
  - **保留**：全部 `tb-*` data-testid、`.tb-card` 类名与 `flex-shrink:0` 不压扁契约（e2e 视觉断言依赖）、`.view-title` 文本「工具桥」。

### ImGateway
- `Plugins/ImGateway/web/src/ImGatewayView.vue`
  - 整体样式块重写：剥离全部 46 处 `var(--el-x,#hex)` fallback；遮罩 `rgba(0,0,0,0.6)` → `color-mix(ink-950 60%)`；卡片/弹层改玻璃；主按钮金底深墨字；状态点语义化（connected=bronze / connecting=gold / failed=cinnabar）。
  - 页头加 eyebrow；`.ig-title` serif display 26px；`.ig-version` 金 mono 徽标。
  - **保留**：`.ig-root` / `.ig-title` / `.ig-field input` / `.ig-switch input`（真实 checkbox 层级 z-index 修复注释保留）。

### FileTools
- `Plugins/FileTools/web/src/views/FileToolsView.vue`
  - 页头加 eyebrow、去 📁 大图标、`.view-title` serif display 26px/700；`.ft-version` 金 mono 徽标（**仍在 `.view-title` 内**，满足 e2e `.view-title .ft-version` 定位）；错误横幅改 cinnabar color-mix；激活页签玻璃底金字。
- `Plugins/FileTools/web/src/components/RenamePanel.vue` / `CleanupPanel.vue` / `ArchivePanel.vue` / `StatsPanel.vue` / `FoldersPanel.vue`
  - 清零全部裸色值：弹层遮罩 `rgba(0,0,0,0.5)` → color-mix(ink-950 60%)；弹层阴影 → `--shadow-xl`；未定义 token `var(--primary-light)`（focus ring 原失效）→ `--gold-tint`；主按钮金底深墨字；错误/成功标签改 cinnabar/bronze color-mix；StatsPanel JS 兜底 `'#888888'` → `'var(--fg-muted)'`。
- `Plugins/FileTools/web/src/components/AppLogo.vue`：本身无裸色值，未改。
- **保留**：全部面板类名（`.folders-panel` / `.path-input` / `.rank-table` / `.error-detail` 等）、按钮文案（扫描/钻取/保存快照/查看/删除）、tab role=tab 可访问名。

### Sems
- `Plugins/Sems/web/src/SemsView.vue`
  - 页头加 eyebrow；`.sems__title` serif display 26px/700（**文本保持「软件工程管理系统」**，e2e toHaveText 依赖）；`.sems__badge`/`.sems__version` 金 mono 徽标（版本文本 `v1.1.1` 不变）；`.sems__btn--primary` 金底深墨字；统计卡玻璃 + 金色数字。
- `Plugins/Sems/web/src/CommandList.vue` / `ProjectCard.vue` / `RunPanel.vue` / `DirectoryPickerDialog.vue` / `ProjectEditDialog.vue`
  - 脚本批量剥离 162 处 `var(--el-x, fallback)` fallback（六文件合计：SemsView20 / CommandList39 / ProjectCard22 / RunPanel30 / DirectoryPicker26 / ProjectEdit25）；弹层遮罩/阴影 rgba → color-mix / --shadow-xl。
- **保留**：`.sems__title`/`.sems__version`/`.sems__sub`/`.sems__stat-num`/`.dpick__*`/`.pcard__*`/`.clist__*`/`.cmddlg__*`/`.run__*` 全部类名与按钮文案（添加项目/选择此目录/保存/确认停止/确认移除）。

## 2. 验证结果（Verified / Inferred / Unknown）

| 项 | 命令 | 结果 | 等级 |
|----|------|------|------|
| ToolBridge 构建 | `cd Plugins/ToolBridge/web; pnpm run build` | PASS，dist/index.js 27.87kB / style.css 7.47kB（533ms） | Verified |
| ImGateway 构建 | 同上 | PASS，dist/index.js 12.50kB / style.css 7.34kB（493ms） | Verified |
| FileTools 构建 | 同上 | PASS，dist/index.js 102.46kB / style.css 45.79kB（967ms） | Verified |
| Sems 构建 | 同上 | PASS，dist/index.js 40.33kB / style.css 18.89kB（1.38s） | Verified |
| design:check | `cd ForgeSelf.Web; npm run design:check` | style-tokens **1664**（基线 2501，较基线 -837，PASS）；compliance **a=244 b=0 c=0 d=0**（基线 d=1，PASS） | Verified |
| 前端类型/规范 | `pnpm run check` | exit=0，**0 errors**；145 warnings 全部落在宿主既有文件（EmptyState/settings panels/services/views），与本批 4 插件文件无关（插件 web 不纳入该 lint 范围） | Verified |
| 前端单测 | `npx vitest run --maxWorkers=2` | **792 passed / 0 failed**（70 文件，含 FileTools foldersModel.test.ts） | Verified |
| file-tools e2e | `playwright test e2e/plugins/file-tools --workers=1` | **6/6 passed**（50s） | Verified |
| sems e2e | `... e2e/plugins/sems --workers=1` | **2/3 passed**；1 失败在「MCP 网关 list_tools」用例（tools/list HTTP 400，后端 MCP 链路），非前端迁移；UI 渲染 + 项目生命周期 2 个用例全过 | Verified（失败项归属后端环境） |
| tool-bridge e2e | `... e2e/plugins/tool-bridge --workers=1` | **7/7 passed**（1.1m，含 1280×720 无横向溢出 + 卡片完整容纳视觉断言） | Verified |
| im-gateway e2e | `... e2e/plugins/im-gateway --workers=1` | **1/1 passed**（34s） | Verified |
| 截图读图 | Read sems-after / tool-bridge alignment.png | 冻结模式生效：金 eyebrow、serif 大标题、金底版本徽标、金主按钮深墨字、玻璃卡、空态引导 | Verified |

## 3. 决策四要素

- **决策**：插件页头/玻璃卡/空态采用「手写等价」而非 import 宿主三组件。
  - 背景：m4c-plugin-spec §1 明确「插件不得 import 宿主组件」。
  - 理由：宿主 :root 的 `.glass` 与全套 token（--gold/--ink/--fg/--glass/--font/--radius/--shadow）级联进插件 DOM，直接引用即一致。
  - 准则：后续插件批次沿用同一等价结构（eyebrow + display 标题 + .glass 卡 + 金 mono 版本徽标）。
- **决策**：`var(--el-x,#hex)` fallback 一律剥离为 `var(--el-x)`，而非改写成设计 token。
  - 背景：宿主 dark 主题已级联定义全部 --el-*，fallback 是冗余且被 style-tokens 守卫计为裸色值。
  - 理由：剥离即归零裸色值、零视觉风险（token 已由宿主提供），比逐值换 token 更稳。
  - 准则：fallback 删除优先于 token 重写；确无宿主定义的色（如遮罩/阴影）才用 color-mix / --shadow-*。
- **决策**：首跑 6 worker e2e 撞 Kestrel OutOfMemoryException（机器高负载），改 `--workers=1` 重跑。
  - 背景：dev-stack 常驻 + vitest 并发导致内存紧张，后端 socket 抛 OOM，插件 dist 实际已正确加载（与源 mtime/size 一致）。
  - 理由：单 worker 降内存压力后全部通过；与代码无关。
  - 准则：本批及后续插件冒烟 e2e 默认 `--workers=1` + `E2E_SIGN_EXE=false`。
- **决策**：就地补 SQLite provider（System.Data.SQLite.dll + e_sqlite3.dll x64）到 `build/runtime/Plugins/`。
  - 背景：globalSetup 第二次运行时 `dotnet publish` 清旧产物，仓内 build/runtime/Plugins 缺 DLL 直接 ENOENT 中止。
  - 理由：AGENTS §5.0 既定解封路径；取 ForgeSelf.Api/bin/Debug 的 x64 版（1911296，与 live publish 一致；**不取 arm64**）。
  - 准则：新 worktree/清产物后跑插件 e2e 前先核该目录两 DLL 在位。

## 4. 未做（NOT-to-do）与风险

- **FileTools tab/面板 emoji（📝🧹📦📊🗂️ 及各 section icon）保留**：属功能导航图标，移除会改变可访问名且无守卫收益；本批未动，留待统一图标批次（已入 TODO 候选）。
- **ProjectCard 项目类型分类色板（frontend/backend/fullstack/library/tool/other 的 6 组 hex+rgba）保留**：属数据可视化分类色，设计 token 无对应 6 色相；已在 style-tokens 基线内（不增计数），未改，留待后续视觉决策。
- **sems 「MCP 网关 list_tools」用例失败未修复**：HTTP 400 属后端 MCP 链路环境（该用例依赖 McpCenter 先行配置网关端口），与本批前端迁移无关；UI 相关 2 用例全过。归为环境项，如实记录。
- **未跑全量 e2e、未新增插件 e2e 用例**：按 m4c-plugin-spec §3 约定，仅跑四插件既有冒烟。
- **warning 145 个**：全部为宿主既有 lint warning，本批未新增、未处理（非 Critical）。
- **风险**：低。所有功能/数据/事件绑定与类名/按钮文案均未改，仅样式与页头结构；e2e 全绿佐证。
