# M4b-2 批证据：迁移四个中优宿主屏

- 批次：M4b-2（设计稿全面重构）
- 日期：2026-10-11
- 范围（专属）：
  - `ForgeSelf.Web/src/views/CodeSnippetsView.vue`
  - `ForgeSelf.Web/src/views/ScriptLibrary.vue`
  - `ForgeSelf.Web/src/views/WorkflowLibrary.vue`
  - `ForgeSelf.Web/src/views/DevToolsView.vue`
- 模式：冻结模式（PageHead + ForgeCard + EmptyState + scoped 全 token 化 + Motion 按 DESIGN.md）
- 样板/组件：SystemMonitorView.vue（只读）、components/{PageHead,ForgeCard,EmptyState}.vue（只读复用）

---

## 一、执行清单

| # | 文件 | 改动 | 保留的功能/交互 |
|---|------|------|-----------------|
| 1 | CodeSnippetsView.vue | 页头换 `<PageHead eyebrow="Code Snippets · 代码片段库" title="片段 · 列表 · 详情">`，#actions 放计数徽标「N 个片段」；detail 未选中态换 `<EmptyState>`；移除 `.view-header`/`.empty-detail` 裸色样式；scoped 全 token 化（--fg-*/--paper-tint/--border-*/--font-mono/--radius-md） | 取数（loadSnippets/languages/categories）、事件（select/create/toggle-favorite/search/filter/page-change/save/cancel/edit/delete）、子组件 CodeSnippetList/Viewer/Editor 全部 props/events、路由无关、`.code-snippet-editor` 钩子类不动 |
| 2 | ScriptLibrary.vue | 根改列布局：顶部 PageHead（eyebrow「Script Runner · 脚本运行器」title「脚本库 · 编辑器 · 输出」），#actions 放「新建」按钮（自侧栏头移入）；下方 `.workspace` 行（sidebar 260px + main）；welcome 空态换 `<EmptyState>`；lang-badge/stop/exit-code/modal-overlay 全部裸色值 → color-mix token | 脚本列表筛选、代码编辑器（textarea 行号联动）、运行/停止/AI生成/保存按钮逻辑、输出控制台（行数/退出码/耗时）、ParameterForm/ExecutionDetail 模态、scriptHub.connect 生命周期、所有 store action |
| 3 | WorkflowLibrary.vue | 页头 PageHead（eyebrow「Workflow Engine · 工作流引擎」title「工作流 · 执行 · 调度」），#actions 放搜索框（placeholder「搜索工作流...」）+ 金色「创建工作流」按钮；列表卡片换 `<ForgeCard interactive>`；空态换 `<EmptyState>`；加载态保留 spinner；内联 `:style` 中 rgba/hex → color-mix / token；`var(--text-*)`/`--font-family-mono` 未定义引用清理 | 筛选 tabs、卡片状态图标/步骤 pills/进度条/meta/操作按钮、创建/编辑/执行/暂停恢复/AI生成 全部事件、WorkflowEditor/ExecutionDetail/AIWorkflowGenerator 模态 |
| 4 | DevToolsView.vue | 页头 PageHead（eyebrow「Dev Tools · 开发者工具」title「工具箱 · 快捷入口」），#actions 放「全部展开/收起」按钮；tabs 导航保留；错误横幅 token 化（cinnabar）；工具网格保留；移除未用导入（AppLogo/useRoute/computeRouteMeta） | 工具分类 tabs、卡片展开/收起、API 拉取 `/api/DevTools/list`、错误横幅关闭 |

---

## 二、验证结果（分级：Verified / Inferred / Unknown）

| 门禁 | 命令 | 结果 | 等级 |
|------|------|------|------|
| design:check 样式令牌 | `npm run design:check` | style-tokens 2347（基线 2501，减 154，无新增）；compliance a=244 b=0 c=0 d=0（基线 d=1，无新增）PASS | Verified |
| 前端类型+lint | `pnpm run check` | 0 errors，76 warnings（= 基线，无新增） | Verified |
| 前端单测 | `pnpm run test`（vitest run --pool=forks --maxWorkers=2，NODE_OPTIONS=4096） | 67 files / 771 tests；12 failed 全部在 `SkillsView.test.ts`(9) 与 `pluginStore.test.ts`(3)，均非本批四屏；本批 4 文件 0 失败 | Verified |
| 路由 HTTP | GET :7399 四路由 | /code-snippets /script-runner /workflows /dev-tools 均 HTTP 200 | Verified |
| 鉴权 API | GET :7301/api/CodeSnippets（Bearer token） | HTTP 200（非 401），token 有效 | Verified |
| e2e code-snippets | playwright e2e/code-snippets.spec.ts | 3 passed | Verified |
| e2e workflows | playwright e2e/workflows.spec.ts | 3 passed | Verified |
| e2e 走查截图 | screenshots/e2e/code-snippets-load.png、workflows-load.png | 读图：PageHead eyebrow（金色字母间距）正常、#actions（计数徽标 / 搜索框+创建按钮）在位、无横向溢出、无白屏；code-snippets detail 空态 EmptyState 可见；workflows 中部为加载过渡态（三态均已接线） | Verified |

补充：
- vitest 首次跑 OOM（JS heap out of memory），加 `--max-old-space-size=4096` + `--pool=forks --maxWorkers=2` 后跑通。
- e2e 截图复跑期间 globalSetup 基建多次瞬时 flake（AuthentiCode 签名时间戳服务超时、残留目录清理 `cmd /c timeout` 退出码、后端就绪等待），清理残留 `.temp/e2e/wt-b26d4625` 后干净运行 6/6 通过。

---

## 三、设计决策（四要素：决策 → 背景 → 理由 → 准则）

1. **CodeSnippetsView #actions 用计数徽标，不放「新建」按钮**
   → e2e `getByRole('button',{name:'新建'})` 与搜索框在子组件 `CodeSnippetList` 内；若在页头再放一个「新建」会产生同名按钮，Playwright strict-mode 断言冲突。
   → 保持 e2e 选择器唯一、不破坏回归。
   → 准则：页头 #actions 只放与既有子组件按钮不重名的操作；CRUD 主按钮留在内容区子组件。

2. **ScriptLibrary 改列布局（PageHead 置顶 + workspace 行在下）**
   → 脚本运行器是全屏 IDE 式工作台（侧栏列表 + 代码编辑器 + 输出控制台），需要纵向高度。
   → 页头紧凑置顶，编辑器区 flex:1，全部既有编辑/运行/模态逻辑零改动。
   → 准则：全屏工作屏统一「PageHead 顶栏 + flex:1 工作区」结构，不强行套页面 hero。

3. **tinted 背景统一用 `color-mix(in srgb, var(--token) N%, transparent)` 替代 `rgba(...)`**
   → 样式令牌守卫 `COLOR_RE` 统计 `rgba(`/`#hex`；`color-mix` 不含裸色值，既满足 token 化又减少基线计数。
   → 避免引入新裸色值、不新增 token 族。
   → 准则：所有半透明底色（语言徽章、错误 hover、退出码、模态遮罩）一律 color-mix + 设计 token，不写 rgba()。

4. **DevToolsView 工具 emoji 图标保留，未换 SVG**
   → emoji 是工具数据标识（`tool.icon`），替换为内联 SVG 需逐图标设计、超出本批「冻结模式迁移」范围。
   → 功能保留 > 严格视觉拷贝（任务明示）。
   → 准则：记录为未做项，后续如需去 emoji 单独立项。

---

## 四、未做 / 风险

- **未做**：DevToolsView 12 个工具的 emoji 图标未替换为 SVG（功能数据标识，替换需独立设计）。
- **未做**：未新增插件级 e2e、未跑全量 e2e（按任务约束）。
- **风险（非本批引入）**：`pnpm run test` 12 红落在 SkillsView.test.ts / pluginStore.test.ts，属并发兄弟批次（M4b-3 等改 SkillsView/PluginView）与 M6（改共享组件 PageHead/ForgeCard/EmptyState）的在制品红；本批四屏无单测、无失败。按 AGENTS.md §5.6「基线红先对表再判责」，新增红才是我的——本批 0 新增。
- **风险**：e2e 截图为隔离实例首屏加载态；workflows 中部空态在数据加载完成后才呈现（逻辑已验证：isLoading=false 且 filteredWorkflows=[] 时渲染 EmptyState）。
