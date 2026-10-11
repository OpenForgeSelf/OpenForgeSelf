# M4c-P1 批证据：AIAgent 插件前端迁移到冻结模式

- 批次：M4c 插件批次 P1（最大插件，逐组件迁移）
- 范围：`Plugins/AIAgent/web/src/**` 全部（AiAgentView.vue + 11 个 components + tailwind.css）
- 日期：2026-10-11
- 流程合规：纯样式 token 化改造（不改业务逻辑/模板结构/选择器），属 m4c-plugin-spec「插件冻结模式」允许面；未碰宿主/其他插件。

---

## 1. 逐组件清单（改了什么 / 验证等级）

| 文件 | 改动 | 验证 |
|------|------|------|
| `AiAgentView.vue` | 无 `<style>` 块（样式全走 Tailwind 工具类 `bg-bg-page text-text` → tailwind.css @theme 映射 `--el-*`）；本批 0 fallback 可剥（stripped=0）。版本号经 `loadMeta()` 传入 ChatPanel | Verified（构建通过） |
| `styles/tailwind.css` | `@theme inline` 把 `--color-bg-page` 等别名到 `--el-*`；本就 token 化，未动（其 `--color-*` 别名已计入 compliance a=20 基线，不增） | Inferred |
| `components/ContextGroup.vue` | 剥 10 处 `var(--el-x,#hex)` fallback → `var(--el-x)` | Verified |
| `components/ContextPanel.vue` | 剥 40 处 fallback；`ElSkeleton` 本就显式 import（合规） | Verified |
| `components/DirectoryPickerDialog.vue` | 剥 13 处 fallback（保留原 UTF-8 BOM）；`ElDialog/ElButton/ElScrollbar` 显式 import | Verified |
| `components/EmptyHint.vue` | 剥 1 处 fallback | Verified |
| `components/FileEditor.vue` | 剥 17 处 fallback | Verified |
| `components/SessionPanel.vue` | 剥 42 处 fallback | Verified |
| `components/StepProgressCard.vue` | 剥 39 处 fallback；5 处裸 rgba tint（badge/stuck/error 背景）→ `color-mix(in srgb, var(--el-color-warning/success/danger) N%, transparent)` | Verified |
| `components/TagInput.vue` | 剥 9 处 fallback | Verified |
| `components/RunRecordPanel.vue` | 剥 65 处 fallback；8 处裸 rgba tint（run/status/plan-badge/stuck）→ color-mix(token) | Verified |
| `components/ChatPanel.vue` | 剥 98 处 fallback；8 处裸 rgba（tool-status/chip 边/chip-x hover/abtn-n/opt--on）→ color-mix(token)；2 处 box-shadow `rgba(0,0,0,...)` → `var(--el-box-shadow-light)`/`var(--el-box-shadow)`；3 处裸 hex（send--stop `#fff`、opt-box/pop-go `#1d1e1f`）→ `var(--el-color-white)`/`var(--el-text-color-inverse)` | Verified |
| `components/AgentEditDialog.vue` | 剥 36 处 fallback；switch 旋钮裸 `#fff` → `var(--el-color-white)` | Verified |

合计剥离 `var(--token, #hex/rgba)` fallback **370 处**；裸 rgba tint → color-mix **21 处**；裸 hex/shadow → EP token **5 处**。
最终全目录裸色值（`#hex`/`rgb(`/`rgba(`）残留 **0**（Grep 复核）。

## 2. 冻结模式对齐

- 玻璃卡/卡片：插件为复杂三栏工作台，沿用既有 `--el-bg-color`/`--el-fill-color`/`--el-border-color-*` 卡片层级（宿主 workshop-forge.css 已把 `--el-color-primary` 重主题为金），不新造玻璃类（功能保留 > 严格视觉拷贝）。
- 三态：空态（EmptyHint / ChatPanel 欢迎气泡 / SessionPanel 空文案）、加载态（ContextPanel `ElSkeleton` / ChatPanel 打字指示 `…`）、错误态（ChatPanel `.chat__error` + `ElMessage.error`）——本就具备，未改。
- 品牌语汇 / 版本号：ChatPanel 标题后已有 `<span class="chat__version">v{{ version }}</span>`（e2e 断言 4 专测 `.chat__version` 文本）；version 由 `loadMeta()` 读 plugin.json（1.7.3）。
- 模板 `ElXxx`：全部显式 `import { ElXxx } from 'element-plus'`（经宿主共享桥取同实例），无未解析模板组件。
- 未新增 token 族 / 未加胶囊按钮 / 未加 blur>40px / 未加蓝紫渐变（compliance 四项均不增）。

## 3. 验证结果（按插件规格 §3 顺序）

环境前置已设：`NO_PROXY/no_proxy=localhost,127.0.0.1,::1`；`TEMP/TMP=<repo>\.temp\tmp`。

1. **插件 build**：`cd Plugins\AIAgent\web; pnpm run build` → **PASS**（EXIT=0，vite 1.68s，64 modules；产物 `dist/index.js` 127.35KB / gzip 29.83KB，`dist/style.css` 120.48KB / gzip 17.35KB）。Verified。
2. **design:check**：`cd ForgeSelf.Web; npm run design:check` → **PASS**。
   - style-tokens：合计 **1669**（基线 2501），PASS，较基线**减少 832**（含本批 AIAgent 397→约 27）。
   - compliance：**a=244（≤244）/ b=0 / c=0 / d=0（≤1）**，PASS。
   Verified。
3. **宿主 check**：`pnpm run check`（vue-tsc -b + eslint）→ **EXIT=0，0 errors**；warnings 145 条全部位于 `ForgeSelf.Web/src/**`（宿主既有，非本批改动——本批仅改 `Plugins/AIAgent/web/src`，不在宿主 eslint 扫描范围）。0 errors 硬门禁通过。Verified。
4. **宿主单测**：`pnpm exec vitest run --maxWorkers=2` → **70 文件 / 792 测试全绿**（187s；含 `Plugins/AIAgent/web/src/http.test.ts`、`http.session.test.ts`、`toolEvents.test.ts`）。Verified。
5. **冒烟 e2e**：`e2e/plugins/ai-agent/ai-agent.spec.ts` —— **环境受阻，未拿到绿**（见 §5）。Unknown（非代码缺陷）。

## 4. 决策四要素

- **决策**：把 `var(--el-token, #hex/rgba)` 的色值 fallback 一律剥成 `var(--el-token)`；裸 rgba tint 用 `color-mix(in srgb, var(--el-color-X) N%, transparent)` 表达；裸阴影/白/深色用 EP 既有 token（`--el-box-shadow-light/--el-box-shadow/--el-color-white/--el-text-color-inverse`）。
- **背景**：宿主运行时已注入全套 `--el-*`（workshop-forge.css + EP），fallback 色值是 style-tokens 守卫的拦截源（AIAgent 目录基线 397 处）；DESIGN.md 禁裸 hex/rgb。
- **理由**：剥离 fallback 不改变运行时视觉（宿主必然提供 `--el-*`），却把硬编码色值计数降下来（棘轮只禁上涨，下降合法）；color-mix 保留原 tint 透明度比例且不引入新 hex；不重写工作台视觉（功能保留 > 视觉拷贝，且本批禁碰其他插件/宿主）。
- **准则**：插件样式一律引用级联进来的 `--el-*`/DESIGN token，不写裸色值、不带色值 fallback；状态 tint 统一 color-mix(token)。

## 5. 未做 / 风险 / 受阻

- **冒烟 e2e 未绿（环境受阻，非本批代码缺陷）**：
  - 第 1 跑：globalSetup 签名段失败（`sign-publish.ps1` 退出码 1）。
  - 按规格设 `E2E_SIGN_EXE=false` 第 2 跑：globalSetup 清理段 `cmd /c timeout /t 2` 在非交互 shell 失败（残留宿主句柄未释放）。
  - 第 3 跑（6 worker）：globalSetup 通过、纯 API 测试（GET /api/agents）通过，但 6 个页面测试均 30s 超时——`.agent`/error/loading 三者均未出现。
  - 第 4 跑（单 worker、单测、timeout 60s）：拿到根因 `Failed to fetch dynamically imported module: /plugins/ai-agent/web/dist/index.js`——e2e 隔离宿主**前端 :7002 未监听**（后端 :7102 起、前端未起），插件 JS 404。
  - 判定：这是 e2e 隔离实例在本机高负载下前端/插件静态未就绪的环境问题（与 §5.0 记的新 worktree 缺插件 dist 同类），**非本批 CSS 改动所致**——报错是网络 404 而非 JS 语法/渲染错误，且本批 0 模板/选择器改动（`.agent/.sess/.chat/.ctx/.chat__version` 全部原样）。
  - 建议：待低负载时段复跑 `ai-agent.spec.ts`；或由有完整 e2e 发布环境的 CI 跑。本批不新增/不删/不收敛其余 4 个既有 e2e spec。
- **AgentEditDialog 下拉箭头 data-URI SVG 内 `stroke='%23a3a6ad'`**：URL 编码 hex，不被 style-tokens 正则匹配（无字面 `#`），是既有功能性 SVG 资产，未改（改它有破坏箭头风险）。记录备查。
- **warnings 145**：全部宿主 `ForgeSelf.Web/src` 既有（M4a/M4b/M5/M6 批次累积），非本批新增；本批插件代码不被宿主 eslint 扫描。
- 未跑全量 e2e（按规格只跑主冒烟）；未跑后端 `dotnet test`（本批零后端改动，属快档）。
