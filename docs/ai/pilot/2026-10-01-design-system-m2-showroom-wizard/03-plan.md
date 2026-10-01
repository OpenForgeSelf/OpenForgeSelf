# Plan

> 阶段：Stage 3｜具体到真实文件路径。「新增」= 仓库里目前不存在（已核实）；「改」= 已存在。
> Task ID：PILOT-ds-m2-showroom｜依赖：M1 闸门2 通过（M1 契约见 `2026-10-01-design-system-m1-agent-tools/03-plan.md` §D/§E/§H）

## Files To Change

**后端（`Plugins/DesignSystem/`）**

- file: `Services/ExportService.cs`（改）
  reason: 把 `Load` 里"图 → `Snap` 列表（解析有效值 + 颜色补 hex）"那段抽成共用方法 `BuildSnaps(TokenGraph)`，并新增 `Snapshot SnapshotFromGraph(TokenGraph graph, DesignProject project, String? themeCode)`（空的字体/资产/页面/组件/变体/图标）。`Load` 改为"读库建图 → 调同一方法"，**行为不变**（既有 `ExportProjectionTests` 不改且全绿）。
- file: `Services/PreviewCssService.cs`（新增，`public sealed`）
  reason: FR1。`Preview(GenerationRequest req, String? theme)`：`req.Themes=[theme ?? "light"]` → `DesignGenerator.Generate` → `Shared`/`Themed[theme]` 转 `TokenNode` → `TokenGraph` → `SnapshotFromGraph`（临时 `DesignProject{Code="preview",Version="0.0.0"}`，**不 Insert**）→ `ExportService.ToCss`。返回 `PreviewCssResult(theme, css, seed, industry, hue, notes)`。
- file: `Controllers/DesignSystemController.cs`（改）
  reason: 新增 `[HttpPost("generate/preview-css")]`（沿用 `Guard`、类级鉴权；与 `generate/preview` 同样的入参校验与错误文案）；`meta.capabilities` 追加 `preview-css`。
- file: `Services/DesignSystemConstants.cs`、`plugin.json`（改）
  reason: 版本三元组 + 清单版本 → `3.0.0`。
- file: `Plugins/DesignSystem/README.md`、`ROADMAP.md`、`docs/02-features/036-design-system.md`（改）
  reason: 事实同步（四模式、展厅、向导、交付页、`preview-css`、版本）。

**前端（`Plugins/DesignSystem/web/src/`）**

- `shell/nav.ts`（新增）：把 `DesignSystemView.vue` 里的 `NAV`、`SectionKey`、`NavItem` 原样搬出（**标签/分组/capability 一字不改**）并导出 `SECTION_KEYS`。
- `shell/mode.ts`（新增，纯函数）：`type Mode = 'start'|'showroom'|'workbench'|'delivery'`、`MODE_LABELS`（开始/展厅/工作台/交付与接入）、`resolveInitialMode({ hash, hasProjects, stored })`（FR2 的默认规则）、`readStoredMode()/storeMode()`（`localStorage['ds.mode']`，读写失败静默）。
- `shell/ModeBar.vue`（新增）：`role="tablist"`，方向键切换，`aria-selected`。
- `DesignSystemView.vue`（改）：顶部 `<ModeBar>`；原有 `<nav class="ds-nav">` + 主题条 + 错误条 + `<main class="ds-main">`（14 个 section 与两个预览页）整体包进 `v-if="mode==='workbench'"` 分支，**模板与类名不动**；新增 `start/showroom/delivery` 三个分支；`newProject()` 改为 `mode='start'` + 重置向导（删掉两次 `window.prompt`）；NAV 改为从 `shell/nav.ts` 导入。
- `start/wizard.ts`（新增，纯状态机）+ `start/StartMode.vue`（新增）+ `start/wizard.test.ts`（新增）。
- `showroom/Showroom.vue`、`Wardrobe.vue`、`Stage.vue`、`TunePanel.vue`、`OutfitScope.vue`、`DeviceFrame.vue`（新增）；`showroom/CompareStrip.vue`（B 片新增）。
- `showroom/outfits.ts`、`showroom/tune.ts`、`showroom/scenes.ts`（新增，纯逻辑）+ 同名 `*.test.ts`。
- `showroom/mannequins/*.vue`（A：`AdminDashboard / AdminList / AdminForm / AdminDetail / AdminSettings / StatusBoard`；B：`WorkbenchApp / LandingPage / MobileH5`）+ `showroom/mannequins/mannequin.css`（`mq-*` 类，全部只读 `--ds-*` 变量）。
- `delivery/DeliveryMode.vue`、`delivery/snippets.ts`（+ `snippets.test.ts`）（C 片新增）。
- `design/glossary.ts`（新增，+ `glossary.test.ts`）、`design/route.ts`（C 片新增，+ `route.test.ts`）、`design/latest.ts`（新增，序号守卫小工具 `createLatest()`：`const run = latest(); … if (!run.isCurrent()) return`，+ 测试）、`design/skin.ts`（改：`scopeCssToSkin(css, scope = '.ds-skin')`、新增 `pickVars`、`composeCss`；`skin.test.ts` 扩展，**旧用例不改**）。
- 守卫测试（新增，读盘风格同 `classes.test.ts`）：`design/dialogs.test.ts`（AC7）、`design/mannequins.test.ts`（AC12）、`design/glossary.test.ts` 内含词典使用守卫（AC9）。
- `api.ts`（改）：新增类型与方法：`StylePreset`/`PresetMatch`、`QuickCreateInput/Result`、`PreviewCssResult`、`AgentAccess`、`AgentTool`、`McpConfig`、`ReviewResult`；方法 `listPresets / recommendPresets / quickCreate / previewCss / getAgentAccess / putAgentAccess / listAgentTools / reviewCode / getMcpConfig`。**所有路径只在 `api.ts` 出现**（既有纪律）。

**测试（`ForgeSelf.Api.Tests/Plugins/DesignSystemTests/`）**

- `PreviewCssTests.cs`（新增）：AC1/AC2/AC3。
- `MannequinVariableContractTests.cs`（新增）：AC4（从测试进程目录向上找到含 `ForgeSelf.slnx` 的仓库根，读 `Plugins/DesignSystem/web/src/showroom/**/*.{vue,css}`，正则提取 `var\((--ds-[\w-]+)`；对 8 个预设 × `light`/`dark` 用 `PreviewCssService` 取 CSS，扫 `(--ds-[\w-]+)\s*:` 得定义集；断言差集为空，失败信息列出缺失变量与首个出处文件:行）。
- `DesignSystemAuthTests.cs`（既有，随之更新版本断言与 `preview-css` 能力断言）。

**e2e / 文档 / 技能**

- `ForgeSelf.Web/e2e/plugins/design-system/design-system.spec.ts`（改，**仅**新增 `enterWorkbench()` 助手与两处调用；`nav()` 不变）；`design-system-showroom.spec.ts`（新增，三个 `test.describe`：A/B/C）；`design-system-helpers.ts`（新增，新 spec 用的 `apiData/apiPost/mcpCall/shot`，**不去重构旧 spec**）。
- `.agents/skills/design-system-verify/SKILL.md`（改，增补自查项）、`.agents/skills/design-system-consume/SKILL.md`（改，增"交付与接入页"）。
- `docs/ai/pilot/2026-10-01-design-system-m2-showroom-wizard/*`、`.forgeself/memory/<日期>.md`、`TODO.md`。

## 实现契约（零自决详表）

### §U DOM 与交互契约（e2e 与验收依赖，名称即判据）

| 区域       | 契约                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| ---------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 模式条     | `role="tab"`，可访问名**恰为** `开始`、`展厅`、`工作台`、`交付与接入`；`aria-selected`；容器 `role="tablist" aria-label="模式"`。                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| 开始       | 根 `[data-start]` 带 `data-step="1".."4"`；场景选项 `[data-scene="admin                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                    | workbench | board | landing | mobile | other"]`；预设卡 `[data-preset="<预设id>"]`；名称输入 `aria-label="设计系统名称"`；创建按钮可访问名 `创建我的设计系统`；高级折叠开关 `aria-label="高级设置"`，code 输入 `aria-label="项目代码"`；成功区 `[data-wizard-done]`（含三个去向按钮 `去展厅看看` `去交付与接入` `继续在工作台微调`）。 |
| 展厅       | 衣柜 `role="listbox" aria-label="衣柜"`，选项 `role="option" [data-outfit-id]`（`preset:<id>` / `project:<code>` / `tuned:<n>`）；舞台 `[data-stage]` 带 `data-outfit / data-theme / data-device / data-scene / data-page`；场景页签 `role="tab"` 名：`后台/中台` `工具/工作台` `状态板` `官网/落地页` `移动端 H5`；后台页面页签名：`仪表盘` `列表` `表单` `详情` `设置`；控件 `aria-label="明暗"`（选项 `浅色/深色`）、`"疏密"`、`"设备"`；微调面板 `[data-tune]`，控件 `aria-label="品牌色" / "圆润度" / "疏密" / "动效"`，按钮 `还原`、`保存为新设计`；对比：衣柜项按钮 `加入对比`，对比区 `[data-compare]` 内两个 `[data-stage-frame]`，差异条 `[data-compare-diff]`。 |
| 模特标记   | 每个模特页根 `div.mq-page[data-mq-page="<id>"]`；每处使用蓝本组件的元素带 `data-mq-wear="button\|card\|input\|badge\|nav\|table\|dialog\|tooltip\|tabs\|select"`（AC14 数元素用）。                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| 交付与接入 | 各卡 `[data-dv="mcp\|rules\|brief\|files\|tools\|review"]`；文本区 `<pre data-dv-text="agent-rules\|brief">`；开关 `role="switch" aria-label="允许 AI 修改设计"`；审查输入 `aria-label="待审查代码"`，语言 `aria-label="代码语言"`，按钮 `开始审查`；复制按钮可访问名 `复制`（回退时按钮旁出现 `[data-copy-fallback]` 提示）。                                                                                                                                                                                                                                                                                                                                             |

### §M 模特页面内容规格（"关键元素下限"= AC14 判据；内容为通用占位，不绑定真实业务）

| 页面 id（`data-mq-page`） | 场景 / 标签        | 必含内容                                                                                                                                                                                  | `data-mq-wear` 元素数 ≥ / 种类 ≥ |
| ------------------------- | ------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------- |
| `admin-dashboard`         | admin / 仪表盘     | 顶栏 + 侧栏（nav，含当前项指示条）；4 张 KPI 卡（card + badge 涨跌）；一块 CSS 柱状图（4 根柱分别用 `--ds-chart-series-1..4`）；"最近订单"表格（table，≥5 行，状态 badge）；2 个按钮      | 14 / 5                           |
| `admin-list`              | admin / 列表       | 筛选栏（input + select×2 + 查询/重置按钮）；状态页签（tabs，4 个）；表格（≥6 行，含 badge 与操作按钮）；分页（按钮组）                                                                    | 18 / 6                           |
| `admin-form`              | admin / 表单       | 分组卡片（card×2）；input×4（含 1 个**校验失败态**：错误文案用 `--ds-semantic-danger`）、select×2、textarea（用 input 令牌）；提交/取消按钮；一个成功提示条（用 `--ds-semantic-success`） | 12 / 5                           |
| `admin-detail`            | admin / 详情       | 面包屑式 nav；头部（标题 + badge + 按钮×2）；tabs（3 个）；描述列表卡；操作记录表（≥4 行）；一个**静态展示**的 tooltip                                                                    | 14 / 6                           |
| `admin-settings`          | admin / 设置       | tabs；设置卡（input/select/按钮）；"危险操作"区 + 一个**静态展示打开态**的 dialog（遮罩用 `--ds-component-dialog-scrim`）                                                                 | 12 / 6                           |
| `status-board`            | board / 概览       | 6 张状态卡（card），每张带 badge（success/warning/danger/info 各至少 1）与迷你进度条；"事件"表格（≥4 行）；一个 tooltip                                                                   | 14 / 4                           |
| `workbench-editor`（B）   | workbench / 编辑器 | 左侧文件树（nav）；编辑区 tabs（3 个）+ 代码区（`--ds-font-mono`）；右侧属性面板（card + input + select）；底部状态栏（badge×3）                                                          | 14 / 6                           |
| `landing-home`（B）       | landing / 首页     | 顶部导航（nav）；Hero（大标题用 `--ds-size-display`，主/次按钮）；3 张特性卡；3 张定价卡（其一高亮）；订阅输入 + 按钮；页脚                                                               | 16 / 5                           |
| `mobile-home`（B）        | mobile / 首页      | 顶栏；搜索 input；3 张列表卡（card + badge）；主按钮；底部标签栏（nav，4 项，当前项指示）                                                                                                 | 12 / 5                           |

**写法约束**（`design/mannequins.test.ts` 机器核对）：模特 `.vue` 与 `mannequin.css` 中，颜色类属性（`color/background(-color)/border(-*)-color/outline-color/fill/stroke`）的值只能是 `var(--ds-*)`、`currentColor`、`transparent`、`inherit`、`none`；`font-family`、`box-shadow` 的值必须含 `var(--ds-`；`padding* / margin* / gap / border-radius / border-width / font-size / transition-duration` 的值只能由 `var(--ds-*)`、`calc(var(--ds-*) …)`、`0`、`auto`、`inherit`、`100%` 构成；布局性 `width/height/min-*/max-*/grid-template-*/flex*` 不限。**不得**出现 `#hex`、`rgb(`/`hsl(`/`oklch(`、17 个基础命名色（作为颜色值）。不使用 `v-html`。

### §W 向导状态机契约（`start/wizard.ts`；纯模块，建议 reducer 形态，判据以行为为准）

- 场景 → `kind` 与默认舞台场景：`admin→console / admin`、`workbench→console / workbench`、`board→console / board`、`landing→marketing / landing`、`mobile→product / mobile`、`other→product / admin`。
- 步骤门禁：①→② 需已选场景；②→③ 需已选预设；③→④ 恒可（微调可跳过）；④ 提交需 `name.trim()` 非空（≤60 字）且（若展开了高级并填了 code）`^[a-z0-9][a-z0-9-]{0,39}$`。回退保留全部输入。
- 推荐：进入②时用 `{kind, brief: 描述}` 调 `recommendPresets(limit=3)`；以 `requestId` 做序号守卫，陈旧响应丢弃；失败显示原文并回落展示全部预设。
- 创建：单飞（`phase==='creating'` 时再次提交被忽略）；成功 → `phase='done'` 并回读（`getProject`）核对令牌数；失败 → 停在④，保留输入，展示后端原文（409 时展开「高级」并聚焦 code）。
- 微调映射（唯一定义在 `showroom/tune.ts`，向导与展厅共用）：品牌色 → `seedColor`；疏密 `舒展/适中/紧凑` → `comfortable/default/compact`；圆润度 2–16（步长 1）→ `radiusBase`；动效 `克制/适中/活泼` → `motionScale` `0.8/1/1.2`；其余沿用预设 `request`。

### §S 皮肤与作用域契约（`design/skin.ts`、`showroom/OutfitScope.vue`）

- `scopeCssToSkin(css, scope='.ds-skin')`：把 `:root{` 换成 `{scope} {`，把 `:where(…):focus-visible` 换成 `{scope} :where(…):focus-visible`；`scope` 里允许 `[data-outfit="…"]` 属性选择器。
- `pickVars(css, wanted)`：解析 `:root{…}` 内的 `--ds-*: value;` 声明，保留 `name ∈ wanted` 的**逐字声明**；丢弃 `@media` 与 `@font-face` 及注释；若保留项的值引用了 `var(--ds-x)` 而 `x` 未保留，则把 `x` 也沿引用链补进来（防"指向未定义变量"）；纯文本处理，不解析颜色。
- `composeCss(parts)`：按序拼接（后者覆盖前者），用于"配色主题 CSS + `compact` 密度 CSS"。
- `OutfitScope`：props `{ outfitId, css, wanted? }`；`outfitId` 必须匹配 `^[a-z0-9:_-]+$` 否则不渲染并 `console.warn`（一次）；输出 `<div class="ds-outfit" :data-outfit="outfitId"><style>…</style><slot/></div>`；`<style>` 用文本插值（同现有 `<component :is="'style'">{{ … }}</component>` 写法），**不用 `v-html`**。
- `.ds-outfit` 样式（`display:block` 等）定义在 `styles/base.css`，保证 `classes.test.ts` 通过。

### §C 展厅数据层契约（`showroom/outfits.ts`、`tune.ts`）

- `buildOutfits({projects, presets, limit=12})`：项目 = `status!=='archived' && tokenCount>0`，按 `updatedAt` 降序；预设 = M1 `GET presets` 全部（保持后端顺序）；返回 `{ mine, presets, hiddenCount, ungenerated }`。
- `createOutfitLoader({ loadProjectCss, previewCss, concurrency=3 })`：`load(outfit, {theme, density})` 返回 `Promise<string>`；键 `${outfit.id}|${theme}|${density}`；命中缓存不再取数；并发上限用 `design/pool.ts` 的 `mapLimit`；项目衣服：`loadProjectCss(projectId, theme)`，密度非 `default` 且项目有 `compact` 主题时 `composeCss([色向css, compactCss])`；预设/微调衣服：`previewCss({...request, theme, density})`。
- `tuneToRequest(presetRequest, tune)`、`createDebounced(fn, ms)`、`createLatest()` 见上；防抖 250ms；`flush/cancel` 可测。
- 舞台主题可用集：项目 = 该项目的色向主题；预设 = `light/dark`（若 `preview-css` 对 `dark` 失败则只给 `light` 并提示）。

### §E e2e 改动契约

- `design-system.spec.ts`：`nav(page, label)` **保持同步返回定位器（签名不变）**，调用处一条不改；新增 `async function enterWorkbench(page)`：若 `getByRole('tab',{name:'工作台',exact:true})` 的 `aria-selected` 不是 `true` 则点击（幂等）。在 `page.goto` 之后、第 3 步"14 入口全部可见"断言**之前**调用一次；`page.reload()` 之后再调一次（模式由 `localStorage['ds.mode']` 还原，通常已在工作台，调用只是幂等保险）。其余断言一条不动、不删、不弱化（AC26 以 diff 审查为准）。
- 新 spec 使用 `injectRealApiKey`；网关与 MCP 用 `process.env.FORGESELF_MCP_GATEWAY_PORT`；唯一项目码 `e2e-m2-<时间戳>`；收尾只做**软归档**；写开关等全局状态用例结束前**改回原值**；截图目录 `ForgeSelf.Web/screenshots/e2e/design-system/m2/`，截图前先断言数据到位（自查表 #28）。

## Implementation Steps

**切片 A（基座与骨架）**

1. **基线与开工复核**（00 §开工前复核清单）；先跑一遍既有 `e2e/plugins/design-system` 记存量红。
2. **后端 `preview-css`**（TDD）：先写 `PreviewCssTests`（AC1/2/3）与确认探针 → 抽 `ExportService.BuildSnaps/SnapshotFromGraph`（既有 `ExportProjectionTests` 须仍全绿）→ `PreviewCssService` → 控制器端点与 `meta.capabilities`。
3. **皮肤与纯模块**（TDD）：`skin.ts` 参数化 + `pickVars/composeCss`；`design/latest.ts`；`design/glossary.ts`；`shell/nav.ts`、`shell/mode.ts`；`design/dialogs.test.ts` 守卫（含反向探针）。
4. **外壳改造**：`ModeBar`、`DesignSystemView.vue` 分支化（工作台模板原样）、去 `window.prompt`；**此刻先不加新功能**，在 `design-system.spec.ts` 新增 `enterWorkbench()` 并调用后跑既有 e2e 确认零回归（若红，先修到与基线一致）。
5. **`api.ts` 增量**（类型 + 方法；路径只在此出现）。
6. **向导**（TDD）：`wizard.test.ts` 先红 → `wizard.ts` → `StartMode.vue`（4 步，大白话，`term()`）。
7. **展厅**（TDD）：`outfits/tune/scenes` 测试先红 → 实现 → `Wardrobe/Stage/TunePanel/OutfitScope/DeviceFrame` → A 片 6 个模特 + `mannequin.css` → `design/mannequins.test.ts` 守卫（含反向探针）→ `MannequinVariableContractTests`。
8. **A 片 e2e**（`design-system-showroom.spec.ts` 的 A 块）+ 截图读图；更新 05-evidence 的 CP-A 节。→ **检查点 A**

**切片 B**

9. **其余场景**：`scenes.ts` 补全；`WorkbenchApp / LandingPage / MobileH5` + 设备框；`CompareStrip`；守卫与变量契约自动覆盖新文件。
10. **B 片 e2e** + 截图读图。→ **检查点 B**

**切片 C**

11. **交付与接入**：`snippets.ts`（TDD）→ `DeliveryMode.vue`（六张卡）。
12. **深链与打磨**：`route.ts`（TDD）+ 哈希写回/还原；方向键/焦点/reduced-motion；空态与错误态文案。
13. **视觉 QA**：≥3 预设 × 5 场景 × 明/暗截图逐张读图，缺陷修或记 TODO。
14. **文档与技能 + 版本 3.0.0**；全部验证；写 05-evidence。→ **检查点 C**。**未获授权不提交 git、不推 tag、不停启用户宿主。**

## Test Plan

1. **后端**：`PreviewCssTests`（同源/零写库/确定性）、`MannequinVariableContractTests`（跨层契约）、既有 `ExportProjectionTests` 不改全绿、`DesignSystemAuthTests` 更新。
2. **前端纯逻辑 vitest**：`wizard / outfits / tune / scenes / route / snippets / glossary / skin / latest / mode` 各自 `*.test.ts`；守卫：`dialogs / mannequins / classes / vocabulary`（既有两个不改且须仍绿）。
3. **竞态与并发**：受控 promise 证明——旧响应后到不覆盖（推荐、预览、微调）、缩略图并发 ≤3、连点创建只发一次。
4. **e2e（零 mock）**：新 spec 的 A/B/C 三块 + 既有 spec 回归；直连真实宿主与 MCP 网关；视觉 QA 截图读图。
5. **反向探针**（自查表 #24）：`window.prompt(`、含 `#fff` 的模特片段、被删掉一个变量定义的 CSS（变量契约）、陈旧响应——各自必须让对应守卫/用例变红，记入 Evidence。

## Verification

### Build

```bash
dotnet build Plugins/DesignSystem/DesignSystem.csproj
cd Plugins/DesignSystem/web && pnpm run build
```

### Unit Test

```bash
$env:TMP='D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\.temp\ds-m1\tmp'; $env:TEMP=$env:TMP
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~DesignSystem" --logger "console;verbosity=normal"
dotnet test ForgeSelf.Api.Tests --no-build --filter "FullyQualifiedName~DesignSystem" --list-tests   # 总数须 == 报告总数
cd Plugins/DesignSystem/web && pnpm run check && pnpm run test
```

### Integration / E2E

```bash
cd ForgeSelf.Web
pnpm exec playwright test --config=playwright.config.ts e2e/plugins/design-system/design-system-showroom.spec.ts
pnpm exec playwright test --config=playwright.config.ts e2e/plugins/design-system   # 含既有巨型用例，回归
```

### Other Checks

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-01-design-system-m2-showroom-wizard
git diff --stat -- Plugins/DesignSystem/Data/Model.xml ForgeSelf.Api Plugins/McpCenter Plugins/DesignSystem/web/src/sections
git diff -- ForgeSelf.Web/e2e/plugins/design-system/design-system.spec.ts   # 只应出现 enterWorkbench 的新增与调用
```

## 风险与对策

| 风险                                   | 对策                                                                            |
| -------------------------------------- | ------------------------------------------------------------------------------- |
| `preview-css` 与落库导出出现非注释差异 | 步骤 2 先写 AC2 同源用例再实现；差异先登记偏差，再决定修 `ToCss` 还是修快照构造 |
| 缩略图过多拖慢首屏 / 打爆 SQLite       | 视口内才渲染 + `mapLimit ≤3` + 缓存 + `pickVars` 只注入模特用到的变量           |
| 外壳改造破坏既有 e2e                   | 步骤 4 先只改外壳并跑既有 e2e 到与基线一致，再加新功能                          |
| 模特引用了某预设里没有的变量           | AC4 变量契约测试（跨层）+ 每新增模特文件即被扫描                                |
| 作用域 `<style>` 之间互相污染          | `data-outfit` 属性作用域 + AC16 并排对比互不污染的 computed 断言                |
| 包体积膨胀                             | build 前后体积入 Evidence；9 个模特共用 `mannequin.css`，避免各自复制样式       |

## Plan 偏差记录

> 实现中发现 Plan 与仓库实际不符时，先在此记录偏差，再修正 Plan。

| 时间     | 偏差点 | 原 Plan | 修正后 |
| -------- | ------ | ------- | ------ |
| （待填） |        |         |        |
