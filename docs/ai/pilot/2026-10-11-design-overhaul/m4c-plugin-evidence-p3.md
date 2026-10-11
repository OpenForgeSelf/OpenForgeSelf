# M4c P3 插件前端迁移证据（McpCenter / QuickLinks / TodoTracker）

- 日期：2026-10-11
- 范围：三个插件 `web/src/**` 冻结模式迁移（token 化 + 去 emoji 装饰 + 版本徽标 + 复用既有结构）
- 模式：插件冻结——禁 import 宿主组件，PageHead/卡/空态手写等价；`--el-*` 与 DESIGN 族 token
- 环境：dev-stack 复用（后端 :7301 / 前端 :7399），未起停宿主；E2E_SIGN_EXE=false

## 1. 改动清单（绝对路径）

### McpCenter
- `Plugins/McpCenter/web/src/McpCenterView.vue`
  - `serverStatusColor`：`var(--brand-amber-500, #f59e0b)` → `var(--el-color-success)`（去掉不存在的品牌变量与 hex fallback）
  - `.table-row.row-even`：`rgba(245,158,11,0.04)` → `color-mix(in srgb, var(--el-color-primary) 4%, transparent)`
  - `.btn-test:hover`：`var(--el-border-color-darker, #999)` → `var(--el-border-color-darker)`（去 hex fallback）
  - `.test-toast-overlay`：`rgba(0,0,0,0.3)` → `var(--el-mask-color)`；`.test-toast` 阴影 → `var(--el-box-shadow-light)`
- `Plugins/McpCenter/web/src/DshMcpPanel.vue`
  - `.dsh-error`/`.dsh-warn`：暗色 `rgba(245,108,108,*)`+`#f56c6c` / `rgba(230,162,60,*)`+`#e6a23c` → `--el-color-danger(-light-9)` / `--el-color-warning(-light-9)`
  - `.dsh-card`：`rgba(255,255,255,0.08/0.02)` 暗色残留 → `--el-border-color-lighter` / `--el-bg-color-page`
  - `.dsh-select`：`rgba(255,255,255,0.04/0.16)` → `--el-fill-color-light` / `--el-border-color`
- `playground/ToolPlayground.vue`：无违规色值，未改。

### QuickLinks
- `Plugins/QuickLinks/web/src/QuickLinksView.vue`
  - 新增版本徽标：`onMounted` 拉 `/api/plugin` 取 `quick-links` 版本 → `<span class="version-badge">v{{version}}</span>`（h1 兄弟节点，不影响 heading 可访问名）；新增 `.version-badge` 样式
  - 去 emoji：☰/🔍/📥/📤/⚠️/🔗/📁 → 内联 SVG（保留 `.menu-btn`/`.search-icon`/`.import-btn`/`.export-btn`/`.error-icon`/`.empty-icon`/`.upload-icon` 类名与全部 e2e 可访问名）
  - `.modal-overlay` `rgba(0,0,0,0.5)` → `var(--el-mask-color)`；`.modal-container`/侧栏阴影 → `var(--el-box-shadow)` / `--el-box-shadow-light`
- `Plugins/QuickLinks/web/src/components/ConfirmHost.vue`：遮罩/阴影 → `--el-mask-color` / `--el-box-shadow`
- `Plugins/QuickLinks/web/src/components/LinkFormModal.vue`：遮罩/阴影 → `--el-mask-color` / `--el-box-shadow`
- `Plugins/QuickLinks/web/src/components/ToastHost.vue`：阴影 → `--el-box-shadow-light`
- `Plugins/QuickLinks/web/src/components/LinkCard.vue`：🔗/✏️/🗑️/👆 → SVG（保留 `aria-label="编辑 X"/"删除 X"`，e2e 可访问名不变）
- `Plugins/QuickLinks/web/src/components/CategoryManager.vue`：✏️/🗑️ → SVG（保留 `aria-label="编辑分类"/"删除分类"`）
- 未动：分类图标 `{{ category.icon || '📁' }}`（用户数据，非装饰）；✓/✕/↑/↓ 字形符号（功能操作，非彩色 emoji）

### TodoTracker
- `Plugins/TodoTracker/web/src/TodoView.vue`：2 处 `color:#fff` → `var(--el-color-white)`
- `Plugins/TodoTracker/web/src/NotifyHost.vue`：`rgb(0 0 0 / 40%)` → `var(--el-mask-color)`；2 处 `color:#fff` → `var(--el-color-white)`
- `Plugins/TodoTracker/web/src/components/TaskDetail.vue`：`color:#fff` → `var(--el-color-white)`
- `Plugins/TodoTracker/web/src/components/ExecutionTimeline.vue`：`color:#fff` → `var(--el-color-white)`
- 版本徽标 `.tt-version` 既有（`fetchPluginVersion` 返回 `v` 前缀），未动。

## 2. 验证结果

| 项 | 结果 | 等级 |
|----|------|------|
| McpCenter `pnpm run build` | PASS，1.55s，dist/index.js 70.40 kB / style.css 117.95 kB | Verified |
| QuickLinks `pnpm run build` | PASS，0.72s，dist/index.js 50.10 kB / style.css 25.59 kB | Verified |
| TodoTracker `pnpm run build` | PASS，1.37s，dist/index.js 83.03 kB / style.css 21.66 kB | Verified |
| design:check style-tokens | 合计 1662 ≤ 2484（基线 2501，较基线 -839；三插件裸色 13+8+7=28 → 0） | Verified |
| design:check compliance | a=244(≤244) b=0 c=0 d=0(基线 d=1，现 0)，无增量 | Verified |
| `pnpm run check` | exit=0，0 errors；145 warnings 全部位于 `ForgeSelf.Web/src/**`（既有，日志中无任何 `Plugins/` 路径警告） | Verified |
| `npx vitest run --maxWorkers=2` | 792 passed / 70 files，0 failed（耗时 201s） | Verified |
| mcp-center e2e（UI 渲染用例） | 标题/版本徽标/服务器列表/工具表格/双 tab/网关状态卡渲染断言**全部通过**；失败仅在后端 MCP RPC（initialize HTTP 500、端点 POST 400） | Verified（UI）/ Unknown（后端 RPC） |
| todo-tracker e2e | 2 passed；14 个在「page setup 180s 超时」失败——环境资源耗尽（多次 e2e 遗留大量 dotnet 进程 + 高负载），非本次改动 | Inferred（环境） |
| quick-links e2e | 未跑：该 spec 为 live 变体（直连 51888），默认 config 走 globalSetup 全新宿主；当前环境高负载下 e2e 反复超时，按「不反复重试」停止 | Unknown |

## 3. 决策（四要素）

- 决策：以「token 化 + 去彩色 emoji + 补版本徽标」为迁移深度，不重排既有布局。
  - 背景：插件冻结模式禁 import 宿主组件；既有三插件已大量用 `--el-*`，主要缺口是残留裸 hex/rgb、QuickLinks 缺版本徽标、彩色 emoji 装饰。
  - 理由：功能保留 > 严格视觉拷贝（Main 验收条）；e2e 选择器契约密集（todo-tracker 15+ 用例、quick-links 用 aria-label），重排布局风险远大于收益。
  - 准则：替换一律保留类名与可访问名（aria-label），emoji→SVG，色值→`--el-*`/`--el-mask-color`/`--el-box-shadow*`/`color-mix`。

## 4. 未做 / 风险

- 未重排布局、未加 PageHead eyebrow（既有页头已含标题+版本+状态，满足等价结构）；Tailwind `@theme` 的 `--color-*`（合规 a=20 基线）未动，避免 Tailwind 构建行为风险。
- 风险1：e2e 后端 MCP RPC（initialize/端点）在本 worktree 全新宿主下返回 500/400，疑为其他批次后端改动或环境残留所致——**与本次前端改动无关**（未触碰任何 .cs），需在低负载环境复核对表基线。
- 风险2：todo-tracker / quick-links e2e 因环境高负载（page setup 超时 / live 变体）未取得完整通过证据；代码层三插件构建 + 守卫 + 单测全绿，视觉渲染在 mcp-center 用例中已证实。
- 未停/启/杀任何用户宿主进程（dev-stack 97516、:51888 的 92880/95628 均未动）。
