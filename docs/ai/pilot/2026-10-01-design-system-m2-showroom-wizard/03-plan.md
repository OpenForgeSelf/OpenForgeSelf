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

| 时间       | 偏差点 | 原 Plan | 修正后 |
| ---------- | ------ | ------- | ------ |
| 2026-10-02 | D1：AC3 的「非法参数 400」不可复现。生成器对任意用户数值输入不抛 `ArgumentException`（未知 industry 回落 general、未知 theme 走浅色角色、色阶族恒全），`generate/preview` 本身亦无 400 路径 | AC3 断言 `preview-css` 对非法数值参数返回 400 | **已处置（用户拍板「先解决」）**：`PreviewCssService` 入口新增 `GuardInput` 数值域校验（Hue∈[0,360) / Chroma>0 / TypeRatio>0 / TypeBasePx>0 / RadiusBase≥0 / MotionScale>0），越界抛 `ArgumentException` → 控制器 `Guard` 映射 400；**刻意只属于 preview-css**，既有 generate/preview 语义不动。新增 12 条 Theory + 1 条边界放行用例，全绿 |
| 2026-10-02 | D2（步骤4）：`refreshAll` 无条件 `loadSkin()` 与 spec 语义「投影只在预览页取」冲突——有历史项目残留时，goto 后 `loadProjects → selectProject(项目0)` 即取到 light 投影，非预览页切档主题条变 `pending`（待重取），而 e2e 断言（v2.6.8 起的语义）要求从未进过预览页应为 `unloaded`（未取）。M2 外壳改造后此脆弱性被稳定触发（既有 e2e 巨型用例回归红） | 步骤4 仅「外壳四模式 + 既有 e2e 零回归」，未定义 loadSkin 调用点改动 | **已处置**：`refreshAll` 内 `loadSkin()` 加 `if (currentNav.value?.skin)` 条件（非预览页不取投影，预览页刷新/切档仍取）；`watch(active)`/`watch([themeCode, projectId])` 两处皮肤页入口不变。修复后既有巨型用例 7 passed（2.1m）全绿，切 dark 状态回 `unloaded`，语义与 e2e 断言一致。判定：v2 既有行为修正（投影只在预览页取），不属于 M2 新引入缺陷 |
| 2026-10-02 | D3（步骤7）：`GET presets`（`StylePreset`）**不返回 `request`**，只有 `POST presets/recommend`（`PresetMatch`）才带。§C 却要求 `buildOutfits({projects, presets})` 的 presets 用 "M1 `GET presets` 全部（保持后端顺序）"，而预设衣服预览必须有 `request` 才能调 `preview-css` | `buildOutfits` 直接吃 `GET presets` 的 `StylePreset[]`（隐含其自带 `request`） | **已处置（实现侧）**：新增 `PresetInput = StylePreset & { request?: GenerateRequest \| null }`；`Showroom` 侧合并 `listPresets()`（保序）与 `recommendPresets({limit: 大值})`（取 `request`），无法取得 `request` 的预设预览显示错误态（不静默渲染空白）。`buildOutfits` 契约字段（`mine/presets/hiddenCount/ungenerated`）与 id 前缀不变；测试覆盖顺序保持与 `request` 透传 |
| 2026-10-02 | D4（步骤7）：AC4 变量契约的**射程**。02-spec/03-plan 原文写扫描 `web/src/showroom/**` 全部源码，但展厅**外壳组件**（`Showroom/Wardrobe/Stage/TunePanel/DeviceFrame/OutfitScope.vue`）是插件 UI 外壳，合法使用**外壳 tokens**（`styles/tokens.css` 生成的 `--ds-fs-*`/`--ds-fg-*`/`--ds-surface-*`/`--ds-color-primary` 等），它们**不在后端 8 预设导出定义集**里；按原文射程必红，且红的不是缺陷 | AC4 断言 `showroom/**` 全部 `var(--ds-*)` ⊆ 每个预设 × light/dark 导出定义集 | **已处置（实现侧收窄）**：射程收窄为 `showroom/mannequins/**` 的 `.vue` + `.css`——**模特页才是"样板间"契约面**（它读后端导出变量）；外壳组件用自己的外壳 tokens，不属该契约。`MannequinVariableContractTests` 同步收紧目录并在类注释标注「偏差 D4」。AC4 判据的**实质不变**（模特引用的每个后端变量都必须真实存在） |
| 2026-10-02 | D5（步骤9）：§U 场景页签名**书写顺序**为「后台/中台、工具/工作台、状态板、官网/落地页、移动端 H5」，但 `scenes.ts` 自身注释与 AC15 的 id 枚举为 `admin/board/workbench/landing/mobile`（`board` 在 `workbench` 前）；两处顺序不一致 | 场景顺序唯一定义在 `scenes.ts`，未规定书写顺序 | **已处置（追加式 + 顺序不构成判据）**：A 片已交付 `admin/board` 保持原位，B 片三场景**追加其后**（`admin, board, workbench, landing, mobile`）。e2e（B1）改为**按可访问名点击 + 集合等价**断言，不依赖顺序；vitest `scenes.test.ts` 锁定该追加顺序 |
| 2026-10-02 | D6（步骤9）：§U 要求「衣柜项按钮 `加入对比`」，但衣柜选项本身是 `<button role="option" [data-outfit-id]>`，HTML 不允许 button 嵌套 button（嵌套会导致点击语义错乱/无障碍树损坏） | 衣柜每项是一个可点击 option 按钮，旁边挂「加入对比」按钮 | **已处置（行容器 + 旁挂）**：每项包进 `.ds-wardrobe__row` 容器，`[data-outfit-id]`/`role="option"`/`aria-selected` 仍在 option 按钮上（A1 既有定位器语义不变）；对比按钮旁挂，带 `data-compare-toggle="<id>"` + `aria-pressed`，可访问名 `加入对比`/`移出对比`。§U 判据（名称与 `[data-compare]`/`[data-compare-diff]`）不弱化 |
| 2026-10-02 | D7（切片C/步骤12）：**FR14 深链与宿主既有行为直接冲突**。插件自路由走 URL fragment（`#/showroom/<page>?theme=dark`），但宿主两处会抹掉整段 fragment：① `main.ts:100 consumeTokenFromHash()` 无条件 `replaceState(pathname+search)`（不区分有无 token）；② `main.ts:94 router.beforeEach` 的 `return { path: to.fullPath, replace: true }` 未带 hash（把 `#...` 并进 path，实测还触发 `No match found for location with path "/design-system#/..."` 警告）。两者叠加 → 插件 setup 期 `parseHash(location.hash)` 得 null，深链失效（e2e C5 实测 `data-device` 期望 mobile 实得 desktop、`data-theme` 期望 dark 实得 light，恰落默认态） | Plan/02-spec 假定 fragment 深链天然可用，未规划宿主侧改动 | **已处置（宿主最小改动 + 越层记录）**：① `authInit.ts` 新增纯函数 `stripTokenFromHash(hash)`——只摘 `token=` 那一项，保留路由与其余查询项；`consumeTokenFromHash` 仅在确有 token 时才写回，其余情况**不动 fragment**（宿主原有安全意图不变：token 仍被清除）。同步改宿主单测 2 处断言 + 新增 `#/route?token=` 唯一查询项归一为 `#/route` 一条。② `main.ts` beforeEach 形参改 `return { path: to.path, query: to.query, hash: to.hash, replace: true }`，显式带回 hash。改后 C5 导航序列第 4 条恢复为 `replace:http://localhost:7002/design-system#/showroom/admin-dashboard?outfit=preset:admin-calm&theme=dark&device=mobile`，C 片 6/6 绿 |
| 2026-10-02 | D8（切片C/步骤11）：e2e 初稿（测试自身缺陷，非产品缺陷）把 `POST projects/{id}/review` 的 `summary`/`findings` 当 `string`/`string[]` 断言，实测抛 `api.summary.trim is not a function`。真源：后端 `DesignReviewer.ReviewSummary` + `ReviewFinding` 是**结构体**（record），控制器原样透传；前端 `{{ reviewResult.summary }}`（Vue `toDisplayString`）以 JSON 文本渲染 | e2e 按「后端返回字符串」比对 | **已处置（改测试，不动后端）**：`ReviewResultView` 接口改为结构体类型（`ReviewSummaryView`/`ReviewFindingView`）；断言改为「取 UI 渲染文本 → `JSON.parse` → 与 REST 对象 `toEqual` 深比对」，判据（界面与 REST 同源）不弱化。另修两处测试自身竞态/就绪等待：`selectedProjectId` 改 `expect.poll(...).toBeGreaterThan(0)`（原在自动选中前读到 0 → 打 `projects/0` 得 500）；`gotoDelivery` 增加等 `[data-dv="tools"] tbody tr` 首行可见；C 片 `test.describe.configure({ timeout: 120_000 })`（长链路超出默认 30s） |
| 2026-10-02 | D10（切片C/步骤14 收口）：**不属于计划偏差，属 M2 已交付代码中的真实缺陷**（读图外的第二处，由 e2e C5 复跑暴露）。深链 `theme=dark` 在"深链直达 + 项目列表晚一步到达"时被重置回首档 `light`。根因：`Showroom.vue` 的两处状态重置 watcher（换衣服重置 `theme/density`、重置微调面板）挂在 `selectedOutfit`（**衣服对象引用**）上，而 `buildOutfits` 每次重算（项目/预设晚一步到达均触发）都会**按 id 重建**衣服对象——引用变了但"选的还是那一件"，被误判成一次换衣服 | 未规划；`watch(selectedOutfit)` 在 A 片即如此写，浅色档看不出来（重置回 `light` 与初值相同，无差异） | **已处置（修复 + 断言化）**：两处 watcher 改盯 `selectedId`（**选择的身份**），回调内再取 `selectedOutfit.value` 读当前衣服——同 id 重建不再触发重置，真正换衣服行为不变。新增组件回归测试 `showroom/Showroom.test.ts`（2 条）：① 深链 theme/device/scene/page 全落舞台；② 衣柜随后刷新（对象重建）不得重置深链主题。判据落在 `[data-stage]` 的 `data-theme`（真实出口）。修复前 ② 红（`expected 'light' to be 'dark'`），修复后 ② 绿；e2e C5 由红转绿、showroom spec 12/12 绿 |
| 2026-10-03 | D11（**闸门2 复验 M-A 处置**）：`design-system-showroom.spec.ts` 的 A1「试穿/微调不得写入任何项目」用**全库项目总数**（`listProjects(page).length`）判定，而 `playwright.config.ts` 是 `fullyParallel: true` + 本地 `workers` 不限 + `retries: 0`，同目录多 spec 共用**同一个后端实例**并发建项目 → 计数被无关用例改写。实测（2026-10-03 独立复验）：默认并行 `18 passed / 1 failed`（`Expected 2 / Received 3`），`--workers=1` 串行 `19 passed`；05-evidence 原记的「全目录 19 passed、0 flaky」在默认并行下**不可复现** | A1 用全库项目数比对"微调不写库"、用计数 +1 判"保存为新设计"落库 | **已处置（改为与并发无关的两条判据）**：① 微调/试穿期间挂 `page.on('request')` 断言页面**不发任何 POST/PUT/PATCH/DELETE**（等待 400ms 覆盖 300ms 防抖窗口）；② 用**项目 id 集合**（而非计数）做前后比对，"保存为新设计"改为轮询「出现一个 id 不在 `idsBefore` 里、名字以 `微调` 结尾的项目」（`quick-create` 的落库口径，A1 是唯一会这么建的用例）。判据不弱化（写请求 = 0 比"计数不变"更强） |
| 2026-10-03 | D12（**闸门2 复验 M-B 处置**）：D 片（AC24）截图前只断言 `data-theme` **属性** + 画布"非透明"，而属性在切换瞬间即变、主题重绘在其后的帧里 → 会拍到上一档旧色。实测 `qa-admin-calm-admin-dark.png` 均值 `(232,235,237)` 与它同档浅色截图 `(232,234,237)` 几乎相同（**该格未反映深色档**；每预设的**首张**深色图最易中招） | 截图前判据 = `data-theme` 属性正确 + 画布非透明 | **已处置（补"主题已生效"门 + 两帧）**：改为轮询「画布不透明 **且** 底色亮度方向 == 所选明暗」（`relLuminance(c) < 0.5 ? 'dark' : 'light'` 必须等于 `theme.code`），再 `requestAnimationFrame` 连放两帧后才 `shotOf`。判据落点是**像素来源本身**（画布 computed 底色），不依赖属性 |
| 2026-10-03 | D13（**闸门2 复验 M-C 处置：Allowed 清单外必要连带改动留档**）：`ForgeSelf.Api.Tests/Plugins/DesignSystemTests/GenerateShapeTests.cs`（`NewController` 补传 `PreviewCssService`——控制器构造依赖变更的连带修改）与 `Plugins/DesignSystem/DesignSystemPlugin.cs`（装配并注册 `PreviewCssService`）**不在 04-task「Allowed」清单**，但属步骤 2 的必要连带改动 | 04-task Allowed 只列了 `DesignSystemController.cs` / `ExportService.cs` / `PreviewCssService.cs` / `DesignSystemConstants.cs` 与三个后端测试文件 | **已处置（按 D7 同款留档，不改 00–04）**：按 04-task「交接说明」第 1 条「不要重写 00–04；偏差记入 03-plan」，在此留档而非回改 04-task。两文件均为「构造依赖变更的连带修改/装配注册」，无行为扩张。D7 的宿主 3 文件（`authInit.ts` / `main.ts` / `authInit.test.ts`）同此口径，已由 D7 行留档 |
| 2026-10-03 | D14（**闸门2 复验读出的真实产品缺陷**，D10 同族）：「深链指定衣服还没到达就被判不存在」。`Showroom.vue` 的 `restored` 闩锁在**衣柜首个非空快照**上消费深链：若那一刻深链指定的衣服尚未到达，`applyInitial` 找不到目标 → 回落 `list[0]`，且闩锁从此不再复原 → 该衣服**永不选中**（`aria-selected` 恒 false）。触发条件：项目走宿主 shell 的 `projects`（先到）、预设要等 `GET presets` + `presets/recommend` 合并后一次性落值（后到）。串行/快网下两者同批到达、看不出；**并行下 C5 稳定红**（`[data-outfit-id="preset:admin-calm"]` 期望 `aria-selected=true` 实得 `false`）。注：D11 修好 A1 的假红后，这条才在默认并行下暴露出来 | 未规划；原实现语义为「衣柜首次非空即应用深链」 | **已处置（修复 + 断言化）**：新增 `sourceSettled(outfitId)`——只等与目标**同类**的那一路（`preset:*` 看 `presetsLoading`；`project:*` 看宿主 `projectsState` 的 `ready/error`；其它视为已落定）；来源未读完且目标未出现时**不消费深链**（保持空选择、等它出现），读完才允许回落首件。`loadPresets` 加 `finally` 落定——预设目录读取失败时也必须能回落，不能永久等待。新增组件回归 `Showroom.test.ts` 第 3 条（项目先到 + 深链指定预设 → 预设到达后 `[data-stage]` 的 `data-outfit` 必须为 `preset:admin-calm`）：**修复前红**（`expected 'project:late-p1' to be 'preset:admin-calm'`）修复后绿；e2e C5 在默认并行下转绿 |
