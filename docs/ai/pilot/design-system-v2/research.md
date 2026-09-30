# Research — Stardust 对标 + 设计系统行业通用方案

> 阶段：立项 Step 1（调研）+ Step 2（可行性素材）｜Task ID：PILOT-ds-v2｜日期：2026-09-28
> 两个来源：① 参考物 `D:\Downloads\Stardust`（用户指定）全量读码；② 2025–2026 行业一手规范（URL 内联，Verified）。

---

## A. Stardust 是什么（机制层面，不是"感觉"）

- **出身**：GenSpark AI 的 design handoff 导出包（`design_handoff_assets.md` 每个二进制都指向 `genspark.ai/api/files/...`；`*.html.srcmap.json` 是 GenSpark 的 DOM 元素映射 `om-html-1`，供宿主"编辑模式"叠加层使用；`tweaks_panel.jsx:5` 说的就是这套 `postMessage` 协议 `__activate_edit_mode` / `__edit_mode_set_keys`）。**不是** Figma/Anima/Locofy 导出。
- **内容**：「Stardust 星尘 · 分布式服务治理控制台」高保真设计，v1.4.0，React 18 UMD + Babel in-browser 编译，必须 HTTP 起服务（`Readme.txt:11`，`file://` 因 CORS 白屏）。
- **它自己声明的消费方式**（`design_handoff_stardust_console/README.md:18-26`，权威）：JSX **不是**生产代码 —— "用你自己的栈重新实现、复用你自己的组件库/路由/状态，**不要抄内联样式**，把 CSS 变量/令牌整体搬走"。
- **最近两个 commit 才是我们要的部分**：`tools/gen-design-data.py` 解析 `colors_and_type.css` + grep JSX 清单，产出三份同步工件 `design-system.data.sql` + `api/<entity>.json`×10 + `api/index.json`；`design-system.xml` 是手写对齐的 schema。

### A1. Stardust 的能力面矩阵（含它的缺口 —— 缺口就是我们的差异化）

| 关注点 | 覆盖 | 出处 |
|---|---|---|
| 色彩梯度 | ✅ gray 50–950(11) / brand 50–900(10) / cyan 300–600 / success·warning·danger 500+700，全部 `oklch()` | `colors_and_type.css:59-97` |
| 语义角色 | ✅ `--bg --surface-1/2/3 --overlay --fg-1..4 --border-1/2/strong --brand --brand-hover --brand-fg --link --elev-*` | `:133-158` |
| 明暗主题 | ✅ 默认 dark，`[data-theme='light']` 覆盖块（`:162-182`），切换在 `src/shell.jsx:14-25` | |
| 排版 | ✅ 2 字族 + 11 个 type role（size/weight/line-height/letter-spacing/uppercase/color-role）、`tabular-nums` | `:184-277` |
| 间距 / 圆角 / 阴影 / 动效 | ✅ 8pt 9 阶（带用途文案）/ 7 级 / **12 行按层分解的多层阴影**（dark 用 inset 高光替代 drop shadow）/ 2 缓动 + 4 时长 + 5 命名动画 | `:99-131` |
| 图标 | ✅ 71 个 Lucide 风格 stroke 1.5，**真实 `SvgBody` 路径存进库**，查看器能画真字形 | `src/icons.jsx`、`api/design-icon.json` |
| 组件清单 | ✅ 83 行，分类 primitives/shell/page/kit-chrome/merged-chrome，带 `DefinedIn`/`Track`/`Interactive` | `api/design-component.json` |
| 状态词表 | ✅ 21 键状态归一为 `{tone,label,sym}` + 兜底 | `src/primitives.jsx:8-40` |
| 运行时改主题 | ✅ dark/light + density + 星场强度 + **brand L/C/H 三滑杆实时重推导**（`src/app.jsx:36-47`） | |
| 文档/预览工件 | ✅ `index.html` 导航 + `design-system.html` 实体查看器 + **28 个 preview 令牌色卡** + 19 张评审截图 + 33 张 `_verify` 回归图 | `preview/`、`screenshots/` |
| 代码导出 | ✅ CSS 自定义属性 + SQL 种子 + JSON；`design-system.xml` → C#（`xcode design-system.xml`） | |
| **可访问性** | ❌ 几乎为零：全树无 `prefers-reduced-motion`、无 `:focus-visible`、无对比度令牌；`aria-*` 只出现在 `tweaks_panel.jsx` | |
| **响应式** | ❌ 只有 `ui_kits/console/login.html:32,48` 一条 `@media(max-width:820px)`，其余按 1280 定死 | |
| 布局栅格 / z-index / 断点 / 图表系列令牌 | ❌ 未令牌化（240px/68px 导轨硬写在 `app.css`） | |
| component tier 令牌 | ❌ **不存在**：JSX 里写死 `borderRadius:8`、`padding:'8px 14px'`、`fontSize:13.5`（`ui_kits/console/Primitives.jsx:5-27`）；Badge 的 `bg/bd` 直接内联 `oklch(…/0.15)` 字面量而不推导 | |
| 数据面瑕疵 | `design-system.data.sql:5` 的 truncate 语句从第 2 张表起是**非法 SQL**（`tools/gen-design-data.py:323`）；`design-system.xml` 手写、生成器不覆写 → schema 与数据会漂；A/B/C 三条组件轨并存、mock 数据互相矛盾（B 轨已声明冻结）；`api/` 是 11 个静态 JSON，**没有任何服务端代码** | |

### A2. Stardust 的数据模型（这是我们要在 .NET 侧真正实现的东西）
`design-system.xml` 是 **NewLife.XCode `EntityModel`**（namespace `Stardust.Design`、`ConnName="StardustDesign"`），**10 表 88 列**，每表每列带 `DisplayName/Description`、Identity 主键、`DefaultValue`、命名唯一/复合索引：

| 表 | 行 | 列 | 唯一索引 |
|---|---|---|---|
| `DesignColor` | 64 | Code, Family, Step, Mode, Value, ColorSpace, **OklchL/C/H**, Alpha, AliasOf, IsPrimary, Usage | `(Code,Mode)` |
| `DesignSpacing` | 9 | Code, Step, Pixel, Usage | `Code`,`Step` |
| `DesignRadius` | 7 | Code, Level, Pixel, Usage | `Code` |
| `DesignShadow` | 12 | Code, Mode, **Layer**, IsInset, OffsetX/Y, Blur, Spread, Color, Alpha, Usage | `(Code,Mode,Layer)` |
| `DesignMotion` | 11 | Code, Kind, Ms, **CurveX1/Y1/X2/Y2**, Curve, Iteration, Usage | `Code` |
| `DesignTypeRole` | 11 | Code, Selector, FontFamily, Weight, SizePx, LineHeight, LetterSpacingEm, Uppercase, ColorRole, Usage | `Code` |
| `DesignFontFace` | 6 | Family, Weight, Style, File, Display, Role | `(Family,Weight)` |
| `DesignComponent` | 83 | Code, Category, DefinedIn, Track, Interactive, Description | `(Code,Track)` |
| `DesignScreen` | 16 | Code, Title, Icon, TabQuery, CountKey, AlertKey, Description | `(Code,TabQuery)` |
| `DesignIcon` | 71 | Code, Track, Api, StrokeWidth, Sizes, **SvgBody(2000)**, Usage | `(Code,Track)` |

值得照抄的决策：**oklch 拆成 3 个数值列**（可在 SQL 里插值/查询）、**阴影一行一层**、`AliasOf` 保留别名图、`Mode∈{both,dark,light}` 让主题成为行维度、`Icon.SvgBody` 存真路径。
它的命名法：kebab-case 无 `--color-` 前缀；primitive 是 `--{family}-{step}`，semantic 是 `--{role}-{n}`；**2.5 层**（component tier 缺失）。

### A3. `api/` 的意图（直接决定我们的控制器形状）
`api/index.json:4` 自陈：*"静态文件代替后端接口…换成真实 API 时只需替换 url"*。每份是 `{entity, source, generated, total, data[]}`，字段名与 XCode 列名 1:1；`design-system.html:347-358` 用 `fetch(cache:'no-store')` + `?v=<generated>` 破缓存。
→ **这就是给我们插件的 ASP.NET 控制器预留的插座**。我们实现真后端时，除 REST CRUD 外还应提供这层"实体集合"只读投影（`GET /api/design-system/{entity}.json`），使 Stardust 那类查看器零改动即可接我们的库。

### A4. tweaks 面板 = 运行时主题编辑器（要吸收的交互概念）
`tweaks_panel.jsx`（627 行）是**通用可复用外壳**：`useTweaks(defaults)` + `TweakSection/Row/Slider/Toggle/Radio/Select/Text/Number/Color/Button/SuggestionBar`（`:560-562`），280px 可拖动玻璃面板、缩略图 deck、分段 radio 滑块、色板 chip。
真实控制项在 `src/app.jsx:23-68`（由 `/*EDITMODE-BEGIN*/…/*EDITMODE-END*/` 持久化 diff 区播种）：**6 个控件** —— theme(dark|light)、density(default|compact)、starIntensity、brandL(0.4–0.8)、brandC(0–0.28)、brandH(210–330)；效果写进 `data-theme`/`data-density`（`src/shell.jsx:14-31`，落 `localStorage['sd-*']`）并实时推导 `--brand-500/400(+0.06L)/600(−0.08L, C×0.95)`。
→ 只有 brand 色可调；spacing/radius/motion/shadow/typography **都不可调**。我们要把它升级成**全令牌可编辑**，且**落库而非 localStorage**。
注意 `app-13-light-theme.png` 暴露的未闭合缝：light 主题下星场画布仍是暗星云 → 装饰面需要显式 mode 归属。

---

## B. 行业通用方案（2025–2026 一手规范）

1. **W3C Design Tokens Format Module**：[DTCG 2025.10 Final Community Group Report](https://www.designtokens.org/TR/2025.10/format/) 是该组**首个稳定版**（注意：仍不是 W3C Standard）。语法 `$value`/`$type`（`$type` 沿树继承）、`$extensions` 放厂商元数据、别名是 **`{path.to.token}` 字符串**、`$description`/`$deprecated`；类型含 `color/dimension/fontFamily/fontWeight/duration/cubicBezier/number/string` 与复合 `shadow/border/gradient/typography/strokeStyle/transition`。**规范明确不管分层分类法** —— primitive/semantic/component 纯属业界约定。→ 对我们：DTCG 是**交换投影层**，不是存储模型；行存要自己合成点分路径、把别名串解析成 FK（并做环检测）、重发 `$type` 继承、复合行按 spec 校验。
2. **三层令牌架构 + mode 轴**：global/primitive → semantic → component，主题（mode）是**轴不是第四层**。证据：[Material 3 color roles](https://m3.material.io/styles/color/roles)（surface/on-surface/primary/secondary-container/outline）、[Tokens Studio `sd-transforms`](https://raw.githubusercontent.com/tokens-studio/sd-transforms/main/README.md) 的 `$themes` = Group > Theme > TokenSet，`permutateThemes()` 产出 `light_casual` 式 mode×brand 排列、[Figma Variables REST](https://developers.figma.com/docs/rest-api/variables-endpoints/)（collections/modes，上限 40 modes / 5000 variables，发布需 Enterprise）。命名主流：`category.role[-modifier][-state]` kebab。
3. **从一个种子生成色阶**：[material-color-utilities](https://github.com/material-foundation/material-color-utilities) 是真机制（CAM16 的 `hct`、tonal palette tone 0–100、static/dynamic `scheme`、`contrast`、`blend`、`score` 排种子色、`temperature`）；Radix Colors 发的是**预计算 12 阶表**不是生成器（内部数学文档不可达，Unknown）。"hue-cycling 保持感知彩度恒定"正是为了修 LCH 梯度在暗端发浑、亮端色相漂移。现代 CSS 改变分工：`oklch()/oklab()/color-mix(in oklab)` 与 `light-dark()` 已可用而 [`contrast-color()` 尚未 Baseline](https://piccalil.li/blog/some-css-only-contrast-options-until-contrast-color-is-baseline-widely-available/)；[Tailwind v4 默认调色板已是 oklch](https://tailwindcss.com/docs/theme)。→ **梯度数学留在服务端（确定性、以对比度为目标），只有相对派生（hover、表面染色）下沉到 CSS `color-mix`**。
4. **可达性**：[WCAG 2.2](https://www.w3.org/TR/WCAG22/)（2024-12 Recommendation）：1.4.3 AA 4.5:1、1.4.6 AAA 7:1、1.4.11 非文本 3:1、2.4.7 Focus Visible、2.4.11 Focus Not Obscured (Min)、2.4.13 Focus Appearance、2.5.8 Target Size。**APCA 已于 2023 从 WCAG 3 草案移除**，[WCAG 3.0 仍是 Working Draft](https://www.w3.org/TR/WCAG3/)、对比度度量未定，2.x 在 ~2030 前仍是规范 → **不做 APCA 门禁**（可作参考读数）。事实核查：**Adobe Leonardo 未被归档** —— 仓库改名 [adobe/leonardo](https://github.com/adobe/leonardo)，`@adobe/leonardo-contrast-colors@1.1.0` 最近发布 2026-07-08，另有 `@adobe/leonardo-mcp`（2026-02-21）。计算库选 [culori](https://github.com/culori/culori)（oklch + WCAG/APCA）/ chroma-js 语义；`wcag-contrast` 2019 后停更是事实，不用。
5. **构建/工具层**：[Style Dictionary 已到 v5.5.5](https://www.npmjs.com/package/style-dictionary)（v4 起一等 DTCG，v5 仍在完善；hooks: transforms/formats/actions/parsers/filters/preprocessors；`css/variables` 的 `outputReferences`）。2026 的 table-stakes 导出目标：DTCG JSON、CSS custom properties、**Tailwind v4 `@theme` CSS**、SCSS/LESS/JS/TS 带类型、Figma Variables / Tokens Studio JSON、[DESIGN.md](https://github.com/google-labs-code/design.md)。
6. **组件与文档层**：真实设计系统产品交付的是 component×state 变体矩阵、props/API 表、do/don't、**逐组件可达性标注**、usage/design/code 分栏（Carbon、[Polaris](https://shopify.dev/docs/api/polaris)、Material、Spectrum、Primer、Atlassian）、DS 自身版本化/changelog、多品牌 + 暗色 + 密度模式、采用度遥测。机器可消费的组件分发格式是 [shadcn registry](https://ui.shadcn.com/docs/registry)：`registry.json` 索引 items（`name`/`type: registry:component`/`title`/`description`/`files[{path,type}]`），item JSON 带 `dependencies`/`registryDependencies`/Tailwind·CSS 变量配置/`meta·icon·categories`。轻量对手是 DESIGN.md 的 `components:` 块（component → `{backgroundColor,textColor,rounded,padding,size}`，**值为令牌引用**，`-hover`/`-active` 变体作兄弟 id）。
7. **图标规模化**：canonical 是 **SVG 路径数据**（不是字体）：[Tabler 6,220 图标 / 24×24 栅格 / 2px stroke / `stroke="currentColor"`](https://raw.githubusercontent.com/tabler/tabler-icons/main/README.md)（并行发 raw SVG / sprite / webfont 三种包），[Lucide 1,600+](https://github.com/lucide-icons/lucide)。要令牌化的是：基准栅格(24)、`stroke-width`(1.5/2)、linecap/linejoin、光学尺寸阶、名称→语义槽映射。
8. **机器可读的"设计系统数据库"**：**没有找到任何先例或标准**做"每行存 oklch 三元组 / 每层一行阴影 / 每模式一行"的关系库。最接近的是 Figma Variables REST JSON（唯一真正的 mode-first API 形状）、Tokens Studio 文件集 + `$themes`、Style Dictionary 内存树、[design.md](https://github.com/google-labs-code/design.md)（单文件，带 `lint` + `diff`）。→ **定位是空白的**；代价是文件生态白送的东西（别名解析、类型继承、mode 排列、回归 diff）都得我们在 SQL 之上自己实现，且**绝不能让库表形状泄漏进导出工件**。
9. **AI 生成设计系统的天花板**：2026 的 SOTA 是 *brief → 一次性 UI + 一份持久 `DESIGN.md` 契约*，**不是令牌数据库**：[Google Labs design.md](https://github.com/google-labs-code/design.md)（约 28.1k star，`@google/design.md@0.4.0`，为 WCAG lint 把所有颜色转 sRGB，并 diff 两版做令牌/文案回归）、[stitch-sdk](https://github.com/google-labs-code/stitch-sdk)（prompt→screens）、[awesome-design-md](https://github.com/VoltAgent/awesome-design-md)、[design-md-chrome](https://github.com/bergside/design-md-chrome)（任意站点→DESIGN.md）；[v0 文档不含令牌持久化/导入](https://v0.app/docs/)，[Figma Make 是在既有 DS 上线框 codegen](https://www.figma.com/solutions/ai-wireframe-generator/)。天花板 = prompt → 调色+字体+间距+圆角+组件原子+do/don't 且校验对比度，但**没有分层、没有别名图、没有模式、没有库**。

---

## C. 可行性结论（Step 2）

**技术可行，且方向明确**：我们要做的不是"更强的 brief→codegen"，而是把 v1 的散文式一次性输出**升级为有库、有校验、有版本、有标准投影的设计系统底座**。三块风险与对策：

| 风险 | 等级 | 对策 |
|---|---|---|
| 新增 10+ 张表（插件自建库、`ConnName=DesignSystem`、落 `~/.forgeself/Plugins/design-system`） | **高（§3 风险分级：DB 结构变更须人确认）** | 纯增量、不碰宿主与既有库；建表走铁律 12 的 `dal.SetTables`；不写任何删除/清库动作（铁律 10）；`EnsureCreated` 幂等 |
| C# 与 TS 两侧都要色彩数学（后端权威生成 + 前端滑杆实时预览），漂移风险 | 中 | 后端是唯一真源；前端只做预览并**同一组黄金值**双端单测锁死；预览结果只在保存时回写后端 |
| 前端体量（10+ section）+ 四步门禁 + 发布 | 中（工量） | 分 M1–M5 里程碑推进，每个里程碑交付即可验证；`web/` 补 `check/test` 脚本（现在根本没有） |
| 依赖新增（`colorjs.io`/culori 类） | 中 | 优先**自实现 oklch/WCAG 数学**（纯函数、可单测、零新依赖），避免沙箱装包与体积；确需再议 |
| 让库表形状泄漏进导出 | 中 | 导出层只走"投影"接口（DTCG 树为唯一中间表示），加 golden-file 测试 |

**收益/成本/风险三栏**：收益 = 插件从"演示页 + localStorage"变成宿主的设计系统系统级底座（可被 AIAgent 工具面、其他插件的界面复用同一套令牌）；成本 = 一个中等规模后端 + 一次前端重写；风险 = DB 增量（可控）+ 前端回归面（用既有 e2e + 新增用例覆盖）。

**命名（Step 4，功能定稿后过"名实相符三问"）**：沿用目录 `DesignSystem` / id `design-system` / 菜单「设计系统」。① 覆盖职责：令牌库 + 生成 + 校验 + 版本 + 导出 + 组件/图标/页面清单，"设计系统"全囊括；② 不绑实现：未含 token/css/figma 等手段词；③ 不撞名：本仓无同名概念。**结论：不改名**（改名要连带动 `plugin.json`、宿主路由/菜单 e2e、已发布版本，收益为零）。
