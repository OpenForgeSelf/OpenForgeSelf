# Repository Understanding — DesignSystem 插件 v2

> 阶段：Stage 0｜Task ID：PILOT-ds-v2｜日期：2026-09-28
> 依据：真实读码（42 个源文件全量审阅 + 宿主侧交叉核对），非推测。

## 1. 技术栈（本仓实证，不用常识替代）

| 层 | 事实 |
|---|---|
| 宿主后端 | .NET 10 ASP.NET Core，SQLite + **NewLife.XCode 唯一 ORM**；实体一律 `Data/Model.xml` → `xcode Model.xml` 生成（`plugin-development` 铁律 9） |
| 插件形态 | 仓库根 `Plugins/<PascalCase>/`（2026-09 由 `ForgeSelf.Api/Plugins` 迁出，commit `c7941a0`），`plugin.json` 声明 `frontend.views/menu/route/icon/entry` |
| 插件前端 | Vue 3 组合式 + TS，Vite **lib mode**（`formats:['es']`）→ 固定产物 `web/dist/index.js` + `style.css`；`vue/vue-router/pinia/element-plus` 全 external，由宿主 import map 解析到同一实例（`Plugins/DesignSystem/web/vite.config.ts:22-36`） |
| 令牌生成脚本 | `web/scripts/gen-tokens-css.ts` 由 `pnpm run build` 前置执行，写出受版本控制的 `web/src/styles/tokens.css`（CI 需 Node ≥22） |
| 测试体系 | 前端 vitest（宿主 `ForgeSelf.Web/vitest.config.ts:33` 已 include `../Plugins/*/web/src/**/*.test.{ts,tsx}`）+ Playwright e2e（`ForgeSelf.Web/e2e/plugins/<id>/`，零 mock）；后端 xUnit（`ForgeSelf.Api.Tests`） |

## 2. DesignSystem 插件现状（关键事实，逐条带出处）

### 2.1 后端：**空壳**
- 全插件只有 **1 个 `.cs`**：`DesignSystemPlugin.cs`（20 行），`Apply(IContext)` 是纯注释 no-op（`:15-19`）。
- **无实体、无 `Data/Model.xml`、无 `Controllers/`、无 `Services/`** —— 与宿主其它 17 个插件的形态不一致（对照 `Plugins/Scheduler/Data/Model.xml`）。
- `plugin.json` 的 `Permissions: []`，无 `Provides/Consumes`（`:10-11`），仅注册 1 个前端视图（`:12-18`）。
- `DesignSystem.csproj:12-19` 引用 `Microsoft.AspNetCore.App` 框架与 `Using Microsoft.AspNetCore.Http`，但代码里无人使用（死引用）。
- 唯一"持久化"是浏览器 `localStorage`：`web/src/design/storage.ts:15-19`，键 `forgeself.design-system.workspace.v1` / `.history.v1`，历史上限 **10 条**。

### 2.2 前端：一个真功能 + 四个演示页
| 文件 | 实/虚 |
|---|---|
| `web/src/sections/DesignStudio.vue`（726 行） | **真实**：brief → `generateDesignSystem` → 7 个结果 tab → 实时换肤 → 历史读写 → 6 格式导出/下载 |
| `web/src/sections/TokenShowcase.vue` | 半真：色块读 `var(--ds-brand-*)`（会随激活系统变），但标签与阶次写死（`:6-8`、`:93`、`:102`、`:181`），且**不接受 props** |
| `web/src/sections/ComponentGallery.vue` | **静态演示**：metrics/nav/rows 全硬编码（`:38-58`，写死 `api-gateway/auth-service/billing-svc`），完全忽略 `ds.components` —— 生成"金融"设计系统仍渲染 SRE 控制台 |
| `web/src/sections/console/*.vue`（5 个） | **静态、按钮无处理器**：`ConsoleKit.vue:24` 只有本地 tab 切换；`ConsoleServices.vue:22` 搜索框无 `v-model`；`ConsoleConfig.vue:25-26`、`ConsoleDeployments.vue:19` 的"保存配置/重置/新建部署"无实现 |
| `web/src/sections/marketing/MarketingSite.vue` | **静态**：文案写死（`:10-21`），4 个 CTA 全无实现（`:45-46`、`:86`） |

### 2.3 `web/src/design/` 核心：模型有、算法薄
- 模型（`schema.ts:52-91`）：`meta/brand/tokens{color,typography,spacing,radius,elevation,border,motion}/components/applications/selfCheck`。
- 真实算法**只有色彩**：HSL→hex（`colorScale.ts:15-26`）、**一条固定的 10 阶 L/S 曲线**（`:29-49`，按 Stardust 紫校准）、`accentHueOf=(hue+282)%360`（`:100-102`）、surface 仅给 `surface-bg` 上色（`:79-86`）。
- **写死的常量**（`generate.ts:23-61` 与 `presets.ts:68-93` 两处重复，逐字节相同 → 任何生成结果的非色部分完全一致）：spacing 4/8/12/16/24/32/48/64/96、radius、elevation（3 条硬编码 `rgba(15,23,42,…)`）、border、motion、排版 `scale`（display 64→micro 12，**无模块化比例、无 fluid clamp**）、`weights`、`lineHeights`；语义色是固定 8 键表（`colorScale.ts:67-76`）；neutral 是写死 slate 梯度（`:52-64`）；`selfCheck` 是 9 条固定散文（`generate.ts:356-366`），其中"对比度可达性 ≥4.5:1"（`:358`）**从未被计算**。
- 生成入口是 **关键词打分**：7 个行业各一张关键词表（`generate.ts:90-218, 261-273`）+ 颜色词覆盖（`:245-254`）+ 正则取品牌名（`:285-291`）。
- 导出 6 格式（`exporters.ts:324-369`）：md / preview.html（字符串拼）/ design-system.json / tokens.json / tokens.css / tailwind.tokens.js。
  - **`tokens.json` 不是 DTCG**：裸字符串表 + 虚构 `$schema: https://forgeself.dev/schema/design-system/v1`（`:36`）。
  - tailwind 导出是字符串拼 JS（`:111-131`），`duration` 的 `ms` 靠文本替换去掉（`:104`）；且 **Tailwind v4 已是 CSS-first `@theme`**，`tailwind.config.js` 形态过时。
  - 无 SCSS、无 Tokens Studio / Figma Variables、无 DESIGN.md、无 registry。
- 无暗色模式、无对比度检查（`grep -i 'dark|prefers-color-scheme|contrast' web/src` 无相关命中）。

### 2.4 违反自身"组件只消费 `--ds-*`"不变量（`README.md:116` 自订规则）的硬编码
`DsButton.vue:74` `rgba(124,58,237,0.28)`（Stardust 紫光晕发给**所有**品牌）、`:113,117` `#dc2626/#b91c1c`；`DesignSystemView.vue:102` `rgba(255,255,255,.82)`；`DesignStudio.vue:498` `#991b1b`；`BrandLogo.vue:53` 字标写死字符串 `ForgeSelf`（生成的品牌名从不出现在自己的 logo 里）；`plugin.json:7` `IconUrl=https://example.com/...`。
文档与代码不一致：`README.md:166` 说行业是 `devops`，代码键是 `devtools`（`generate.ts:92`）。

### 2.5 测试与门禁现状
- e2e：**1 个** `ForgeSelf.Web/e2e/plugins/design-system/design-system.spec.ts`（246 行，含真实下载断言 `:187-199`、刷新持久化 `:203-211`、溢出 `:221-228`、fatal console `:231-244`）。**硬耦合版本断言** `EXPECTED_VERSION==='1.2.1'`（`:141` ↔ `plugin.json:4`）。
- 单元：插件内 `*.test.ts` **0 个** —— 唯一可纯测的逻辑（`colorScale/generate/exporters/storage`）零覆盖。
- 后端：**无** `Plugins/DesignSystem.Tests`；仅被 `ForgeSelf.Api.Tests/Plugins/PluginMenuItemsMergeTests.cs:72,76` 当 fixture 数据用。
- `web/package.json:7-11` 只有 `gen:tokens/build/dev`，**没有 `check`/`test`/`lint`**，而 `ROADMAP.md:46` 的验收标准要求跑 `pnpm run check`/`pnpm run test` → 现有验收命令本身不可执行。
- `web/dist` 被 `.gitignore:30` 忽略且当前不存在 → e2e 必须先构建。

### 2.6 ROADMAP 兑现度（`Plugins/DesignSystem/ROADMAP.md` 自订计划）
| 项 | 判定 |
|---|---|
| P1.1 后端持久化（`DesignRecord` + `api/design-records` + 前端拉取） | **未开工**（无实体无控制器） |
| P1.3 content-hash 缓存键 | 未开工（`plugin.json:17` 是裸 `web/dist/index.js`） |
| P2.1 LLM 增强模式 | 未开工（README 明确列为 non-goal，`README.md:46`） |
| P2.2 自动对比度 + 截图回归 | **部分**（e2e 截 10+ 图但无 baseline diff、零对比度断言；`ROADMAP.md:133` 点名的 `colorjs.io` 不在依赖里） |
| P2.3 `forge-design-system-verify` 技能 | 未开工（`.agents/skills/` 无此项） |
| P3.1 Figma / Tokens Studio | 未开工 |
| P3.2 社区预设市场 | 未开工（预设硬编码在 `presets.ts:244`） |
> `README.md:191-192` 自评把"e2e ✅"和"生产级（持久化/健壮/a11y）✅"打勾，与同文件 `:199` 自认的缺口 G2（无持久化）互相矛盾 —— 自评不可信，以本文件读码结论为准。

## 3. 必须尊重的既有约束（改动红线）
1. 产物契约：入口 `web/dist/index.js` + `style.css`，导出名 == `plugin.json views[0]`（`DesignSystemView`）。
2. 禁止显式 `import { ElXxx } from 'element-plus'`；插件内不得 `import` 宿主模块 / 不得用 `@/` 别名。
3. 插件内跳转走导航桥（`inject('forgeOpenPage')` → `window.__FORGE_OPEN_PAGE__` 降级链），禁止自行 `router.push` 当主路。
4. 新增管理/CRUD 控制器**必须**类级 `[Authorize("ApiKeyPolicy")]`（铁律 17）；插件**必须自行建表**（铁律 12）；数据文件落 `ctx.EnsurePluginDataDirectory()`（`~/.forgeself/Plugins/design-system`），不放发布目录。
5. 根视图标题旁必须显示版本徽标（铁律 13），且 e2e 的 `EXPECTED_VERSION` 必须同步升级。
6. 任何 `Plugins/<X>/web/` 改动 → `AGENTS.md:27` 四步门禁（构建+测试 / 插件层 e2e / 发布 / 走查）缺一不可。
7. 测试与插件代码**永不删除数据目录/库文件**（铁律 10）；唯一性校验直查 DB 不读缓存（铁律 11）。
8. 沙箱内插件 `pnpm i && pnpm run build` 跑不通 → 用 `plugin-development` §3.2 的出树构建兜底。
