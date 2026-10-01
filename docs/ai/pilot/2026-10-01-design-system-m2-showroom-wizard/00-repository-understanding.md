# Repository Understanding

> 阶段：Stage 0｜规范：docs/04-standards/ai-native-engineering-workflow.md §2
> Task ID：PILOT-ds-m2-showroom｜日期：2026-10-01｜所属：设计插件升级 M2（v3.0.0；M1 见 `2026-10-01-design-system-m1-agent-tools`，M3 见 `2026-10-01-design-system-m3-style-guideline`）
> 所有条目来自真实仓库内容（读码/读文档/跑命令），来源标注在「依据」列；**凡引用 M1 产物的条目，依据是 M1 的 03-plan 契约（尚未实现），开工时必须对着 M1 真实代码复核**（见末尾「开工前复核清单」）。

## 项目结构

- 本任务对象：`Plugins/DesignSystem/web/`（插件自带界面，Vue 3 lib 产物，经宿主 import map 共享 `vue`）与一处后端新增端点（`Plugins/DesignSystem/Controllers/DesignSystemController.cs`）。
- 前端现状（Verified，读码）：`src/index.ts`（入口，导出 `DesignSystemView`）、`src/DesignSystemView.vue`（根视图，约 470 行）、`src/state.ts` + `state.test.ts`（模块单例状态 + 竞态守卫）、`src/api.ts`（类型化 API，经 `http.ts` 的 `get/post/put/withQuery/getExportText`）、`src/http.ts`、`src/components/PanelState.vue`（唯一共享组件）、`src/design/{skin,derive,pool}.ts` 与 `skin/derive/pool/classes/vocabulary` 五份 `*.test.ts`、`src/styles/{tokens,base}.css`、`src/sections/*.vue`（14 个专业 section）。`web/` 另有 `package.json`（`build/dev/check/test`）、`vite.config.ts`、`tsconfig.check.json`、`pnpm-lock.yaml`、`pnpm-workspace.yaml`；`web/dist/` 被 `.gitignore:30` 忽略（构建不产生 git 变更）。`state.ts` 注释里提到的 `design/draft.ts` **并不存在**（陈旧注释，不要照着找）。
- 后端相关（Verified）：控制器路由前缀 `api/design-system`，类级 `[Authorize("ApiKeyPolicy")]`；`POST generate/preview`（`DesignSystemController.cs:368`）**只回摘要（seed/industry/hue/各主题条数/前 12 个色样），不回 CSS**；`ExportService.ToCss(Snapshot)`（`ExportService.cs:368`）是**实例方法**，只读快照、无时钟、不触库；`TokenGraph` 有公开构造 `TokenGraph(IEnumerable<TokenNode> shared, IEnumerable<TokenNode>? themed, String? themeCode)`（`TokenGraph.cs:69`），`TokenNode(Path,Tier,Type,Value,AliasPath,Extensions,ValueJson,Lifecycle,Description)` 是纯记录；`DesignGenerator.Generate(req)` 返回 `GenerationResult{Shared, Themed[theme], Shadows, Notes…}` 的内存产物（`DesignGenerator.cs:98`）。
- 关联（Verified）：`Plugins/McpCenter/`（`GET api/mcp-center/config` 回 `listenUrl/hasToken/tokenMasked/isRunning`；MCP 端点 `{listenUrl}/mcp`，探活 `/health`——见 `e2e/plugins/mcp-center/mcp-center.spec.ts`）。
- M1 产物（**契约，未实现**）：`GET presets`、`POST presets/recommend`、`POST projects/quick-create`、`GET projects/{id}/brief`、`POST projects/{id}/review`、`GET/PUT agent-access`、`GET agent/tools`、导出格式 `brief`/`agent-rules`，`meta.agentTools`（见 M1 03-plan §D/§E/§H）。

## 技术栈

| 层       | 技术                                                                                                                                                                                                                                                                                      | 依据                                              |
| -------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------- |
| 插件前端 | Vue 3.5，Vite 6 lib 模式（`formats:['es']`，入口固定 `dist/index.js` + `style.css`），`vue / vue-router / pinia / element-plus` 全 external；**不使用 Element Plus 组件**（插件是预编译独立产物，宿主的自动解析不处理它，模板里写 `<ElXxx>` 运行时会失败）→ 一律原生标签 + CSS + 令牌变量 | `web/vite.config.ts` 注释                         |
| 状态     | 模块级单例 + Vue `ref/shallowRef`，**不用 pinia**（插件独立 ESM，pinia 实例须由宿主注入；单视图树用模块单例即可，且 vitest 可直接 import 断言）                                                                                                                                           | `state.ts` 头注释                                 |
| 前端测试 | vitest，**借宿主入口运行**：`pnpm -C ../../../ForgeSelf.Web exec vitest run ../Plugins/DesignSystem/web/src`；类型检查借宿主 `vue-tsc`（`tsconfig.check.json` 的 `paths` 指向 `ForgeSelf.Web/node_modules`）                                                                              | `web/package.json`                                |
| 后端     | .NET 10 / NewLife.XCode / SQLite（同 M1）                                                                                                                                                                                                                                                 | `DesignSystem.csproj`                             |
| e2e      | Playwright，真实宿主零 mock；设计系统用例是**一个巨型用例**（`test.describe.configure({timeout:420_000})`），17 个编号步骤                                                                                                                                                                | `e2e/plugins/design-system/design-system.spec.ts` |

## 架构特点（Verified，读码）

1. **外壳 / 画布两层**：外壳读中性变量（`styles/tokens.css`），用户设计系统只注入进 `.ds-skin` 容器；于是外壳永不被用户品牌污染。预览页（`NAV.skin===true` 的「组件库」「品牌展示页」）用 `<div class="ds-skin" :data-skin-theme>` 包住两段 `<style>`（`skinStyles.scoped` 与 `skinStyles.alias`）与 `.ds-skin__meta` 角标（`DesignSystemView.vue:270-283`）。
2. **换肤 = 后端 CSS 的选择器收窄**：`skin.ts` 的 `scopeCssToSkin` 把 `:root{` 与焦点环 `:where(...)` 收窄到 **硬编码的 `.ds-skin`**（单实例假设）；`SKIN_ALIASES` 把外壳变量（`--ds-fg-1` 等）重键到令牌变量；`definedVars` 只为**CSS 里真定义了**的变量写别名（否则 `var()` 指向未定义变量会让整条声明在 computed-value 阶段失效，表现是"页面突然全透明"）。前端**一个设计值都不算**。
3. **取数序号守卫**：`state.ts` 的 `effectiveSeq/skinSeq`——任何"await 完直接写共享 ref"的地方，后到的旧响应不得覆盖新状态（历史上 e2e 一红一绿的根因；`state.test.ts` 用受控 promise 钉住）。画布身份是状态：`skinTheme` / `skinApplied` / 主题条四态 `applied|pending|unloaded|unavailable`。
4. **导航**：`DesignSystemView.vue` 内常量 `NAV`（14 项，3 组「建系统 / 把质量 / 看效果」），`active = ref<SectionKey>('projects')`，主区 `v-if/v-else-if` 链；入口按 `meta.capabilities` 置灰而非隐藏；顶栏有项目下拉、「新建」「刷新」、版本徽标 `.ds-badge`（`模型 x · 生成器 y · 投影 z`）、主题条（`.ds-chip`）与页脚 `.ds-footer`（含当前主题码，e2e 据此断言）。
5. **新建项目现状**：顶栏「新建」用 `window.prompt` 两次（`DesignSystemView.vue:159,161`）；「项目与生成」页另有一张表单（`code` 须匹配 `^[a-z0-9][a-z0-9-]{0,39}$`、`name`、`kind`、`seedText`）+ 「生成向导」（brief / 种子色 / hue / chroma / density / typeRatio / typeBasePx / radiusBase / motionScale / brandName / industry / themes / overwrite），`generate/preview` → 确认 → `generate` 两步、`paramsStale` 守卫。`KINDS`、`DENSITIES`、`INDUSTRIES` 三个数组是**手抄**的后端取值（`Projects.vue:37-41`，不在 `vocabulary.test.ts` 的守卫词表内）。
6. **原生对话框**：`window.confirm` 3 处（`Projects.vue:156` 归档、`:324` overwrite、`TokenStudio.vue:105` 退役）；无 `alert`。e2e 用 `page.once('dialog', …)` 依赖归档的那一处。
7. **共享词汇表样式**：`ds-*` 类（`ds-btn / ds-input / ds-surface / ds-mini / ds-chip / ds-h3 / ds-small / ds-micro / ds-mono / ds-row / ds-stack …`）定义在 `styles/*.css` 或各 section 的 `<style scoped>`；`classes.test.ts` 守"模板里用的 `ds-*` 类必须有定义"。
8. **词表守卫**：`vocabulary.test.ts` 禁止界面源码里出现后端词表（tiers / colorFamilies / auditKinds / state / size / scale 档名）的**多成员字面量清单**（同一行 ≥3 个互不相同成员即红）。

## 测试方式

- 前端：`cd Plugins/DesignSystem/web && pnpm run check && pnpm run test && pnpm run build`（check 借宿主 vue-tsc；`test` 借宿主 vitest）。**没有组件挂载测试**——现有 vitest 全是纯 TS（`state/skin/derive/pool`）与"扫源码的守卫"（`classes/vocabulary`）。新界面逻辑应拆成可纯函数/状态机测试的模块（见 03-plan）。
- 后端：`dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~DesignSystem" --logger "console;verbosity=normal"`，总数须 == `--list-tests` 发现数；本环境须先把 `TMP/TEMP` 指到 `.temp/ds-m1/tmp`（见 M1 04-task 交接说明）。
- e2e：`cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/design-system`；`injectRealApiKey(page)`、`page.evaluate(fetch + localStorage.forge_api_token)` 取后端事实；网关端口 `FORGESELF_MCP_GATEWAY_PORT`。截图读图走 `e2e-testing` 技能 Level 3。
- 既有 e2e 对界面的耦合面（Verified，读 `design-system.spec.ts`）：
  - `nav(page,label) = page.getByRole('button',{name:label,exact:true})`，14 个导航文案精确匹配：`项目与生成 令牌工作台 色彩实验室 排版标度 尺度与密度 阴影与动效 主题实验室 图标库 审计与门禁 导出交付 版本与对比 组件库 品牌展示页 品牌资产`；第 3 步在**页面刚加载**时就断言 14 个入口全部可见。
  - 大量选择器：`.ds-root .ds-badge .ds-chip .ds-skinstate[data-skin-state|data-skin-theme] .ds-skin[data-skin-theme] .ds-skin__meta .ds-footer .ts__table .ts__editor .cl__ramp .cl__family .ab__kind .ab__kindmap-row .cg__card .cg__vars li .cg__state-chip .cg__code .ec__table .ec__entities .ec__import .ec__card .rb__diff-pick select .rb__specs .ba__panel .ba__row`、`select[aria-label=…]`、`[data-import-*]`、占位符（`如 console-ui`、`如 铸己匣控制台`）、按钮名（`创建项目`、`① 预览（不落库）`、`② 确认写入（generate）`、`归档本项目`…）。
  - 因此 M2 的硬约束：**14 个 section 的组件、DOM、类名、文案原样搬进「工作台」模式**；e2e 只允许**新增** `enterWorkbench()` 助手并在「页面刚加载」与 `page.reload()` 之后各调用一次（`nav()` 本身不变）。

## 构建命令

```bash
cd Plugins/DesignSystem/web && pnpm run check && pnpm run test && pnpm run build
dotnet build Plugins/DesignSystem/DesignSystem.csproj
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~DesignSystem" --logger "console;verbosity=normal"
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/design-system
```

## 主要目录职责

| 目录                                    | 职责                                                                   |
| --------------------------------------- | ---------------------------------------------------------------------- |
| `web/src/DesignSystemView.vue`          | 根视图：顶栏 + 导航 + 主题条 + 主区（M2 改造点：外壳升级为四模式）     |
| `web/src/sections/`                     | 14 个专业 section（M2 **原样**收进「工作台」，不改）                   |
| `web/src/state.ts`、`api.ts`、`http.ts` | 共享状态 / 类型化 API / 请求纪律（只读可对 BUSY 退避重试，写绝不重试） |
| `web/src/design/`                       | 展示层纯函数与换肤（M2 扩展 `skin.ts`，新增 `glossary.ts`/`route.ts`） |
| `Plugins/DesignSystem/Controllers/`     | REST 面（M2 新增 `generate/preview-css`）                              |

## 代码组织方式与既有规范（对本任务有约束力）

- Vue SFC `<script setup lang="ts">` + `<style scoped>`；注释中文、讲"为什么"；类型不用 `any`；模板里**不写 filter/map 回调**（vue-tsc 推不出参数类型，会隐式 any）——计算放进脚本。
- 样式类前缀：共享词汇表 `ds-*`，组件私有用 BEM（`pj__`、`ts__`、`cg__`…）。
- 自查表（`design-system-verify`）：#8 前端不得长回第二套设计值实现；#12 换肤判据 = `.ds-skin` 计算底色 == 后端 `semantic.surface-bg`；#13 控制台零可避免噪音；#17 `ds-*` 类必须真存在；#19 异步取数必须有序号守卫；#26/#27 词表唯一真源；#28 截图必须拍到"被断言的东西已就绪"；#30 预览显示的是哪一份必须是状态；#31 一个页面不要同时打二十个请求到 SQLite（限流 ≤3，`design/pool.ts` 的 `mapLimit`）。
- AGENTS §4.2：禁止显式 `import { ElXxx } from 'element-plus'`（本插件根本不用 EP）；新代码 TDD 优先。

## 候选低风险任务

1. 只做后端 `preview-css` + 展厅最小闭环（1 个场景）——能演示"换衣服"，但不解决"不友好的交互"与"接入"。
2. **完整 M2（用户已定方向，切 A/B/C 三片）**：A 基座（preview-css + 四模式外壳 + 向导 + 展厅骨架 + 后台/中台 5 页 + 状态板）；B 其余场景（工作台应用、官网/落地页、移动 H5 + 设备框）与并排对比；C「交付与接入」+ 文案/深链 + 视觉 QA。
3. 先做 M3 的风格轴——依赖展厅才能看出"明显不同"，不宜先做。

## 选择该任务的原因

用户（2026-09-30 原话）：效果展示要"类似卖衣服的模特，换衣服就是换设计系统"；"交互也很不友好"；使用者可能是"完全不懂设计和开发的外行"。计划轮已选推进顺序 A（M1→M2→M3）与展厅五类场景全做（后台/中台管理、工具/工作台、状态板、官网/落地页、移动端 H5）。M2 依赖 M1 的预设 / 快速创建 / brief / agent-rules / 写开关 / 工具索引——**因此 M2 必须在 M1 闸门2 通过之后开工**。

## 开工前复核清单（实现方第一步，结果写入 05-evidence「开工复核」）

1. M1 已合入且其 AC 全 Verified；`GET presets`、`POST presets/recommend`、`POST projects/quick-create`、`GET agent/tools`、`GET/PUT agent-access`、导出 `brief`/`agent-rules` 在真实宿主上可调用（不是只在文档里）。
2. 重新取基线：后端过滤集（总数 == 发现数）、插件 web `check/test/build`、既有 `e2e/plugins/design-system` 是否全绿（记录存量红）。
3. **`preview-css` 可行性小探针**（≤30 行，用完即删、不得作为验证结论）：确认可由 `GenerationResult` 在内存构造 `TokenGraph` → `ExportService` 实例的 `ToCss` 而不触库；若 `ToCss` 依赖了 `DesignProject` 实体的其他字段或 `DAL`，先在 03-plan「偏差记录」登记再改方案。
4. 确认 `GET api/mcp-center/config` 的响应字段名（`listenUrl/hasToken/tokenMasked/isRunning`）仍是现状（McpCenter 可能被别的会话改动）。
5. `git status` 清点并行会话的改动，确认不与 `Plugins/DesignSystem/web/**` 重叠。
