# 设计插件 DesignSystem · 设计语言底座（v2.6.5）

> 铸己匣（ForgeSelf）的**设计系统**插件：把设计系统落成**可持久化、可校验、可版本化、可标准交付**的库。
> 插件 ID `design-system`，挂载路由 `/design-system`，界面由插件自带（`web/dist`），宿主运行时远程加载。
>
> ⚠️ **本文 v1 段落已作废**：v1 把"生成/导出"写在浏览器里（`web/src/design/generate.ts`、`exporters.ts`）、数据落 `localStorage`、`selfCheck` 自称对比度 ≥4.5:1 却从不计算。
> v2.0.0 起**设计值只由后端 C# 产生一份**：界面读 `tokens/effective`，预览复用 `export?format=css`（预览与交付同源）。
> 权威细节见 **`docs/02-features/036-design-system.md`**（表结构 / 端点 / 生成与门禁口径 / 验证入口）与 `docs/ai/pilot/design-system-v2/`（立项工件链）；
> 下方 §三 结构树、§五 生成引擎、§六 产出清单、§七 自评、§八 下一步里的 v1 描述**保留作历史对照**，读到就当历史读。

> 本质（v1 的说法，已升级）：**输入一段需求描述，输出一套像 Stardust 一样完整的设计系统**。
> v2 的说法：输入种子色/参数（或一句需求线索），**建出一套能被工程消费、能被审计拦下、能版本化比对的设计系统**。
> Stardust 只是调研参照物，不是插件身份。

---

## 一、三角色定位（解释「产出机器」）

本插件同时扮演三个角色，缺一个就不是完整产品：

| 角色 | 含义 | 在本插件中的体现 |
|------|------|------------------|
| **设计系统（System）** | 定义一套可被工程消费的视觉语言 | 通用 token 模型（颜色 / 类型 / 间距 / 组件 / 品牌）+ 展示页（令牌 / 组件库 / 控制台 / 官网） |
| **设计工作台（Studio）** | 接收需求，以专业设计师视角产出完整设计 | 需求输入 → 行业画像识别 → 生成 DesignSystem → 7 个结果分区浏览 |
| **产出机器（Artifacts）** | 把设计落成下游可直接带走、可直接用的文件 | 6 类产出文件：Markdown 规格、自包含 HTML 预览、结构化 JSON、tokens(JSON/CSS/Tailwind)，全部可下载/复制/新窗口预览 |

**「产出机器」具体指什么？** 不只是「看看就好」，而是：
- **清单**：明确告诉用户这次生成了哪些文件、各是什么、给谁用。
- **预览**：不用下载也能立刻看到效果（工作台内「实时预览」+ 独立 `preview.html`）。
- **导出**：逐文件或全部下载，拿到手里就能继续用。

---

## 二、本质需求目标

用户（产品/设计/前端）想解决的核心问题：

> "我要做一个 XX 产品，需要一套统一的设计系统来规范它的样子。"

本插件的目标响应：

1. **零门槛启动**：输入一句话需求即可得到完整设计系统。
2. **通用而不空洞**：不是给一套固定色卡，而是据行业/颜色词推导品牌与组件集合。
3. **对标 Stardust 完备度**：输出的设计系统覆盖品牌、颜色、类型、间距、组件、应用示例、自检七大维度。
4. **可消费**：产出文件覆盖「人读 / 机读 / 工程可用」三层。
5. **可预览**：工作台内实时换肤预览 + 可离线打开的 preview.html。

### 非目标（明确不做）

- **不做像素级视觉稿**：输出的是设计规格与线框，不是 Figma 替代品。
- **不做设计资产管理（后端）**：当前产物落在浏览器本地，不跨设备共享。
- **不接 LLM**：生成引擎是确定性的，保证可复现、可回归、可离线。

---

## 三、快速上手（为后来者准备）

### 3.1 30 秒看懂结构

```
Plugins/DesignSystem/
├── plugin.json                 # 清单：Version 与后端版本三元组同步（2.6.5）/ route=/design-system / entry=web/dist/index.js
├── DesignSystem.csproj         # 引用 NewLife.Core + NewLife.XCode（自带持久化）
├── DesignSystemPlugin.cs       # 建表 + 内置图标幂等首植 + DI 注册
├── Data/
│   ├── Model.xml               # 12 张表的结构真源（xcode 生成实体，生成物不手改）
│   └── Entities/               # 生成实体 + *.Biz.cs 手写业务成员
├── Services/                   # 设计系统的"后端真相"（色彩数学只有一份，在这里）
│   ├── Oklch.cs / ContrastMath.cs
│   ├── TokenGraph.cs / TokenRepository.cs        # 别名图/环检测、批量事务、手改保护、乐观并发
│   ├── ColorRampGenerator.cs / SemanticResolver.cs
│   ├── TypographyGenerator.cs / ScaleGenerators.cs
│   ├── DesignGenerator.cs / AuditEngine.cs
│   ├── ExportService.cs / ReleaseService.cs      # 11 种投影 + 不可变快照与 diff
│   └── CatalogRepository.cs / BuiltinIcons.cs / DesignMapper.cs / DesignSystemConstants.cs
├── Controllers/DesignSystemController.cs         # 类级 [Authorize("ApiKeyPolicy")]
└── web/                        # 自带界面（Vite lib 模式；vue/element-plus 全 external，模板只用原生标签）
    ├── vite.config.ts / tsconfig.check.json      # check 借宿主 vue-tsc，插件不装工具链
    └── src/
        ├── index.ts             # 入口：导出名必须等于 plugin.json views[0] = DesignSystemView
        ├── DesignSystemView.vue # 根视图：14 section 导航（按 capabilities 灰化）+ 项目/主题选择器 + .ds-skin 换肤容器
        ├── http.ts / api.ts / state.ts           # 鉴权封装（只读可对 Busy 重试）/ 后端 DTO 的 TS 镜像 / 模块单例状态
        ├── design/derive.ts / design/skin.ts     # 展示层纯函数 / 后端 CSS 收窄 + 变量别名（不算值）
        ├── design/classes.test.ts                # 守卫：模板里的 ds-* 词汇表类必须有定义（build 不查这个）
        ├── components/PanelState.vue / BrandLogo.vue
        ├── styles/tokens.css（手工维护的外壳中性主题）+ base.css
        └── sections/                             # 项目与生成 / 令牌工作台 / 色彩实验室 / 排版标度 /
                                                  # 尺度与密度 / 阴影与动效 / 主题实验室 / 图标库 /
                                                  # 审计与门禁 / 导出交付 / 版本与对比 / 组件库 /
                                                  # 品牌展示页 / 品牌资产（资产·字体·页面三表的读写面）
```

### 3.2 本地构建与门禁

```bash
cd Plugins/DesignSystem/web
pnpm install
pnpm run build      # → dist/index.js + dist/style.css（宿主按「入口同目录 style.css」注入）
pnpm run check      # vue-tsc 类型检查（借宿主的 vue-tsc；见 package.json）
pnpm run test       # vitest 单测（宿主 include 已含 Plugins/*/web/src/**/*.test.ts）

dotnet build Plugins/DesignSystem/DesignSystem.csproj
# 必须带 verbose logger，并核对"测试总数 == --list-tests 发现数"（quiet 模式在测试主机崩溃时会报假绿）
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~DesignSystem" --logger "console;verbosity=normal"
```

### 3.3 运行 / 预览

- 正规入口是 **e2e 隔离实例**（自动 publish 宿主 + 起前端 + 注入真实 token，数据落在 `.temp/e2e/<ts>/publish/Data`，不碰用户运行中的宿主）：
  `cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/design-system`
- 首次使用：进「项目与生成」新建项目 → 填种子色/参数 → ① 预览（不落库）→ ② 确认写入；之后各 section 都读同一份库。
- 需要看运行态宿主（例如用户正在用的实例）时，用仓内工具拿 token：`node ForgeSelf.Web/scripts/get-forge-token.cjs`，**不要**为了验证写一次性脚本，也不要由 agent 停/启用户宿主。

### 3.4 关键约定

- **界面不写死设计值**：样式一律 `--ds-*` 变量；用户系统的色/尺/阴影一律 `:style` 绑定后端返回值。
- **界面不算设计值**：需要数字就问后端（`tokens/effective` 已带解析值 + hex + 对比度判级；需要试算就调 `generate/preview`，它不落库）。
- **外壳中性、用户系统进 `.ds-skin`**：`styles/tokens.css` 是插件自己的工具外观（手工维护）；预览容器把后端 CSS 的 `:root` 收窄到 `.ds-skin` 并用别名表把外壳变量重键到用户令牌，二者互不污染。
- **写操作必须自证**：改完调 `invalidate()` 回读，并把后端错误原文显示出来（401/400/409 分级），不弹个"成功"了事。
- **能力面驱动**：入口是否可点看 `meta.capabilities`，后端没声明的能力不假装可用。
- **只读请求可对 `SQLITE_BUSY` 退避重试，写请求绝不重试**（`web/src/http.ts`）：宿主 DAL 并发下偶发 500，
  重复落库比红屏更糟，所以重试只给 GET；根因见缺口 G8。
- **换肤别名只引用后端 CSS 里真存在的变量**：指向未定义变量的 `var()` 会让整条声明在 computed-value 阶段失效，
  表现是"预览突然全透明"（`design/skin.ts` 的 `definedVars` 就是为堵这个）。

---

## 四、设计体系模型（v2：库里的 12 张表）

真源是 `Data/Model.xml`（结构）+ 后端服务（语义），不再是前端的一个 TS 类型：

```
DesignProject ─┬─ DesignTheme（ModeKind: color | density | brand；mode 是轴不是分层）
               ├─ DesignToken   ← 三层统一一张表：Tier(primitive|semantic|component) + Path + Type
               │                  + Value / ValueJson(复合) / AliasPath(别名链)
               │                  + ColorHex + OklchL/C/H + ContrastRatio/WcagLevel
               │                  + Generator/GeneratorSeed/GeneratorVersion + Lifecycle/ReplacedBy + Extensions
               │   唯一键 (ProjectId,ThemeId,Path)；removed 的行不进发布快照
               ├─ DesignShadowLayer（复合阴影逐层展开，与 ValueJson 成对）
               ├─ DesignComponent / DesignComponentVariant（变体×状态×主题矩阵）
               ├─ DesignIcon（SvgBody 真实路径数据 + Collection + License）
               ├─ DesignAsset / DesignScreen / DesignFontFace
               ├─ DesignAudit（审计结论，唯一键含 ReleaseId/Kind/Target）
               └─ DesignRelease（不可变快照：Version + TokensHash + SnapshotFile + 审计摘要）
```

要点：
- **三层不是三张表**：同一张 `DesignToken` 用 `Tier` 区分，别名把 component→semantic→primitive 串成图，解析与环检测在 `TokenGraph`。
- **每张表都有 `Extensions` JSON 袋**：长期演进（新指标、新派生）不必每次迁表。
- **颜色推导规则不变但搬到后端并实测**：显式给色 > 色相 > 行业默认色相；辅助色相带偏移；中性/语义/尺度随参数计算而非固定表；可达性由 `ContrastMath` 判，不靠文案承诺。

---

## 五、生成引擎

**v2 真源在 `Plugins/DesignSystem/Services/`**（`Oklch` / `ColorRampGenerator` / `SemanticResolver` / `TypographyGenerator` / `ScaleGenerators` / `DesignGenerator`）；下文提到的 `web/src/design/generate.ts` 已随 v1 下线。

**设计取向：确定性 + 可解释**，不接 LLM：
- 同一参数永远产出同一套令牌（SHA256 seed 写进每行 `GeneratorSeed`，可复现、可 diff、可回归）。
- **种子色逐位进色阶**（按种子明度就近锚档），换品牌不换色相。
- **语义角色按"需要的对比度反查 tone"**（正文 4.5 / 大字与非文本 3.0），不是固定"500 阶"；填充/标签成对校验，达不到就在扩展里记"未满足"。
- **mode 是轴不是分层**：色向主题铺语义色，密度主题铺自己的尺度覆盖（切 compact 后 `space.*` 有效值真的变）。
- 行业倾向只用于**取默认参数**（键与代码 `DesignGenerator.Industries` 一一对应：
  `devtools / finance / healthcare / education / commerce / media / general`）；界面显式给的值优先，不被行业覆盖吃掉。

---

## 六、产出（v2：`GET /api/design-system/projects/{id}/export?format=&theme=`）

格式清单由后端 `ExportFormats` 提供，界面从 `export/formats` 读，不写死：

| 格式 | 给谁用 | 关键点 |
|---|---|---|
| `dtcg` | 任何支持 W3C DTCG 2025.10 的工具 | 嵌套树 + `$value/$type/$description/$extensions`，别名保留 `{path}` 不烤死 |
| `css` | Web 项目 / 本插件预览换肤 | `--ds-*` 变量；别名保留 `var()`；tint 用 `color-mix(in oklab,…)`；带 `prefers-reduced-motion` 与 `:focus-visible` 块；`@font-face` **只给登记了文件的字体** |
| `tailwind` | Tailwind v4 项目 | CSS-first `@theme`（不是 v3 的 `tailwind.config.js`） |
| `scss` / `less` / `ts` | 各构建管线 | `$`/`@` 变量；TS 为 `as const` + `TokenPath` 联合类型 |
| `tokens-studio` | Figma Tokens Studio | `$themes` + 每主题一个 set（只列真有覆盖的主题） |
| `design-md` | 人与 AI 读的规范 | YAML 前置（colors/typography/rounded/spacing/components）+ Do/Don't + 「组件规格」逐蓝本列解剖/状态/轴/a11y 约束 + 「品牌与资产」段（资产 / 字体许可证 / 起手屏），可被 lint |
| `element-plus` | 用 Element Plus 写的界面（含本宿主） | `--el-*` 换肤接缝：**右侧只出现 `var(--ds-…)` 与它的 `color-mix` 派生**，零字面色值；库里没有的档位如实列"未映射"，不发明值 |
| `registry` | shadcn 风格消费方 | `registry.json`：条目 = 组件目录 ∪ 有 `component.*` 令牌的分组，`meta` 带 `anatomy` / `a11yNotes` / `states` / `variants[]`（每格轴值与令牌清单）/ `unresolvedTokenRefs`（引用不到的如实列出，不静默丢） |
| `stardust-json` / `stardust-sql` | 与调研参照物互通 | `index.json`（每类实体的 `total` 与明细 url **同源同一份数据**）+ **每表独立 DELETE 的合法 data.sql** + 反向生成的 `design-system.xml`；明细走 `GET api/design-system/{id}/{entity}.json`（裸 `{entity,source,generated,total,data[]}`） |
| `bundle` | 一次带走全部 | zip + README 清单（含品牌工件计数）+ `brand/`：图形资产逐文件 `.svg`、`fonts.json`、`screens.json` |

**品牌三表也进交付物（v2.2.0）**：库里登记了字体却不出 `@font-face`、有 logo 却不进包，就是"库里有了、交付物里没有"的半套交付。三条规矩：

- `@font-face` 只给**真登记了文件**（`FileName`/`FileRef` 非空）的行 —— 系统字体栈成员没有可分发文件，硬编 `src` 等于让浏览器去 404；
  它们改为在 CSS 末尾以注释列出（族名 / 字重 / 许可证 + "不随本产物分发"），清单不隐身但也不假声明。
- 图形资产逐文件落成 `brand/<code>.svg`：库里只存图形本体，导出时补 `<svg xmlns viewBox="0 0 24 24">` 外壳；
  **品牌图形统一 24×24 网格**（与内置图标库同一规矩，外壳与图形本体不同网格就会裁图），颜色一律 `currentColor`。
- `brand/fonts.json` / `brand/screens.json` 用**不转义非 ASCII** 的序列化：这两个文件是给人和 CI 答合规的，
  把中文许可证变成一串 `\uXXXX` 等于把答案锁在编码里（这条是跑测试时才暴露的）。

**组件规格也进交付物（v2.4.0）**：库里有 10 个蓝本与几十个变体格子，产物里却只有一串 `component.*` 令牌路径 —— 那"组件"两个字对下游等于没写。三条规矩：

- registry 条目的键集 = **组件目录 ∪ 有 `component.*` 令牌的分组**（`meta.inCatalog` 说清是哪一侧），
  `meta` 带 `anatomy` / `a11yNotes` / `states` / `variants[]`（每格轴值 + 该格令牌清单）。
- **引用不许凭空列**：每条令牌引用都要能在同一份令牌工件里查到；查得到的进 `registryDependencies`，查不到的**如实**进 `meta.unresolvedTokenRefs`，
  静默丢掉就等于把"规格与令牌是两份真相"藏起来。
- Stardust 兼容投影的十类逻辑实体：清单 `total` 与明细 `data[]` **同出 `EntityRows` 一处**，
  `GET api/design-system/{id}/{entity}.json` 返回裸 `{entity, source, generated, total, data[]}`（不走 success/data 信封，参照物的查看器直连这个形状）。
  `design-icon` 计的是"内置 `forge` 集 + 项目自登记"（随插件版本走，与版本快照里"内置库不进快照"是两回事：清单说的是消费方拿得到什么）。

**顺序也是声明，而且曾经有三份真相（v2.6.4）**：产物里的 `size = lg / md / sm`、`状态 = active、default、disabled、hover` 都是**字母序凑的** ——
值没错，但设计师据以检查的那条"从最小档往上、从默认态往后"被打乱了。更要命的是三处各排各的：投影按字母序、读路径按 `State` 字母序、前端又 `.sort()` 一次。
现在的口径：

- 档位序与词表只有一份 —— `DesignSystemConstants.VariantAxes`（`size` = xs→xl、`role`、`state` = default→hover→active→focus-visible→focus→disabled→pressed）；
  生成器认轴、建矩阵的 `SortOrder`、三份投影排序**全部读它**，表外的值退回字母序（没证据的顺序不编造）。
- 词表随 `GET /meta` 出给前端（`variantAxes`（轴 → 档位序）/ `scaleOrders` / `tiers` / `colorFamilies` / `auditKinds` / `entities`），
  界面按它排 —— 前端**不另列词表**，也不自己 `.sort()`。v2.6.5 起尺度档序走同一条路；v2.6.6 清掉最后三份手抄的（层级序、色族序、审计类别序）：
  色族序不是新抄一份，而是**生成器逐族产阶用的那张表**（`ColorFamilies.All` → `RampFor`）；审计类别序与 `AuditEngine` 的产出**双向核对**
  （表里有却造不出触发场景 = 空声明，产出了表外的类 = 界面筛不到）。v2.6.7 起三条并列的轴字段合并成一份 `variantAxes` 清单，
  并且**变体的分组序也归它管**（`ListVariants` 不再按 JSON 文本的字典序分组）。
- **补格子时状态只能选、不能敲**：下拉读 `meta.variantAxes` 的 state 轴，词表外的取值要显式选「自定义」再填（也随时能选回词表）——
  自由文本让 `hove` 这种拼错的状态名进得了库、却进不了任何投影口径。变体轴同理（v2.6.7）：先选轴、再选档位，JSON 由界面拼好且只读。
- 矩阵的**读序**规则写进 `ListVariants`：先按变体分组（**按 `VariantAxes` 的轴序 + 档位序**，v2.6.7；原先是 JSON 文本的字典序），
  组内按落库 `SortOrder`（生成时即"变体序→状态档位序"编号）。
  只按 `SortOrder` 排是不行的：用户自己补的格子 `SortOrder` 缺省 0，会把整张矩阵的顺序顶翻。
- 这条改动是 e2e 逼出来的：断言"界面状态序 == DESIGN.md 状态序"当场红了（界面 `default、disabled、hover、active` vs 产物 `default、hover、active、disabled`），
  说明只改投影不够 —— 读路径与界面也得回到同一张表。"再抄一次"由守卫 `web/src/design/vocabulary.test.ts` 堵：
  界面源码里出现"同一行 ≥3 个互不相同的已知成员"字面量清单即红（临时塞一份镜像已验证它真会红）。

**Element Plus 换肤接缝（v2.5.0 · spec U3）**：宿主界面本身就是 EP 写的，"为系统进行设计"要能真的落到它身上。四条规矩：

- 产物是 `--el-*: var(--ds-*)` 的**接缝**，不是第二份值：先引 `tokens.<theme>.css` 再引它，切主题只换文件。
- **只引用同主题 `tokens.css` 里真定义了的变量**；库里没有的档位不发明值，改在文末如实列"未映射 N 项"并指名缺哪条令牌。
- EP 的 `light-N` / `dark-2` 原义是"与白/黑混合"，这里**照比例但把目标换成本页底色 / 正文墨色** ——
  写死 white/black 在深色主题下会把悬停态洗成灰白、把按下态压成死黑。
- 有意**不映射** `--el-color-white/black`（EP 当固定前景用，映射到表面色会把文字压成同色；前景对比度由 `component.*.foreground` + 审计门禁负责）、
  `--el-index-*`（层级不是设计值）、`--el-transition-all/-fade*`（EP 由 duration + function 组合，已给原料）。
- **职责边界（实测写清，不靠推断）**：e2e 在真实宿主里量到 —— 注入后 `--el-color-primary` 从宿主自带的 `#F59E0B` 变成我们的 `#6d28d9`，
  EP 组件层 `--el-button-bg-color` 同样从 `#F59E0B` 变成 `#6d28d9`（**变量层确实赢了宿主自己的主题文件**）；
  但探针按钮最终 `background-color` 仍是琥珀色 —— 宿主有直接钉按钮底色的规则，**任何主题（含宿主自己那份）都盖不过它**。
  所以这份产物的承诺是"把变量喂进 EP 的组件层"，不是"覆盖宿主里所有硬写背景的选择器"；要后者得改宿主样式表（属宿主侧，已记 TODO）。

**门禁必须对"用户手改之后"仍然成立（v2.6.0）**：颜色对比度、别名、分层这些早就在审，但它们查的都是"生成器算出来的东西对不对" ——
而库里的值谁都能改。四类新维度专门盯现值（都报 **warning**，不拦发布）：

| kind | 在查什么 | 为什么值得拦一下 |
|---|---|---|
| `target-size` | `*.min-height` 是否 ≥24px（WCAG 2.2 2.5.8） | 该条本身有例外（内联、浏览器控制、本质性小目标），报 critical 会误伤并让人无视门禁 |
| `ramp-monotonic` | `space.` / `radius.` / `duration.` 按**声明序**必须递增 | 手改出一个逆序档，档位号就失去含义；档序取自 `ScaleGenerators` 的同一张表，审计不另列一份 |
| `naming` | path 必须是点分 kebab（每段以字母数字开头，段内可带连字符） | 写入端只做 trim + 小写；`space.4 x` 会一路长成 `--ds-space.4 x` 这种没人能用的键，投影不报错 |
| `lifecycle-ref` | 仍被别名引用的 `deprecated` / `removed` 令牌 | 软删不物理删，行还在、也还能解析 —— "没人报错"恰恰是最危险的状态 |

**对比度这一维查的是"库里所有的前景/背景对"，并按 WCAG 原文豁免不该拦的（v2.6.2）**：
过去它查一张写死的八对清单（button / card / …），于是**生成器真实产出的 `dialog` / `tooltip` / `select` 从来没被查过，用户自己新增的组件更是直接绕过门禁**。
现在按命名约定从库里推导：每个 `component.<ns>.foreground[-状态]` 找同命名空间的 `.background[-状态]` → `.background` → `.tint[-状态]` → `.tint`，
再补上推不出来的显式对。多一个组件就多一道判定，不需要有人记得回来加清单。

- **禁用态按 1.4.3 豁免，只报读数、不拦发布**：WCAG 1.4.3 原文豁免"非活动界面构件"。生成器的按钮禁用态实测 3.41:1，
  按 4.5 判 critical 会一次冒出 9 条假警报 —— 用户第一次看到"发布被一个禁用按钮挡住"就开始绕过门禁，哭狼的孩子式门禁等于没有门禁。
  但豁免不是跳过：这些对仍带 `rule=wcag22-1.4.3-exempt` 落库，判级退回 1.4.11 的 **3.0** 兜底线（≥3.0 = info、<3.0 = warning），永不为 critical。
- **一次 Run 的产出就是该 scope 的全部结论**：`AuditRepository.Record` 会清掉本轮没再产出的旧行。
  一条对不再被判定（对象解析不出颜色了、对不复存在了），库里却留着上一轮的 `passed` 行，就是**用旧绿灯冒充今天查过** ——
  而发布门禁 `HasBlocking` 读的正是这些行。
- **（v2.6.3）"清掉旧行"只解决了一半**：判不成的对象必须自己报一条 **`rule=wcag22-1.4.3-unresolved`**（声明为 color 却解析不出颜色 = warning，不拦发布）。
  否则它从审计里静默消失，界面上既不见红也不见黄 —— 与"查过且没问题"一模一样。别名本身就失败的（成环/悬空）仍由 `alias` 报 critical，这里不重复记账。

另有**版本快照**：`POST projects/{id}/releases` 产出不可变 JSON 快照（文件只增不改）+ SHA256 内容哈希 + 两版之间的差异。
差异**不只令牌**（v2.3.0 起）：快照里另有 `specs` 一节，形状是 `kind + key + 字段表`，覆盖
`component`（组件目录）/ `variant`（变体格子）/ `asset`（图形资产）/ `screen`（起手屏）/ `font`（字体登记），
diff 因此能报到"哪一条的哪个字段"（例：`asset/logo 的 svgBody` 变了）。两条硬规矩：

- **规格进哈希**：否则"只换了 logo"会被判定为与上一版内容一致，同版本号重发直接幂等放行 —— 版本化就成了摆设。
- **旧快照不许凭空补一节**：schema 1 的快照文件没有 `specs`，比对时后端回 `specsComparable=false`，
  界面显示"不可比"而不是"新增 N 条"（把结构差异报成内容变更，是假警报的一种）。
  内置图标库（`ProjectId=0`）不进快照：它随插件版本走，不是某个项目的一次发布内容。

> 历史（v1 的 6 个文件：`design-system.md` / `preview.html` / `design-system.json` / `tokens.json` / `tokens.css` / `tailwind.tokens.js`）随前端导出器一并下线。

---

## 七、当前实现是否符合需求（v2.0.0 重审）

| 需求 | 状态 | 说明 |
|------|------|------|
| 输入需求/参数 → 得到一套完整设计系统 | ✅ | 后端确定性生成三层令牌 + 多主题；同参数可复现 |
| 插件通用，不绑死 Stardust | ✅ | 外壳中性主题，Stardust 只作调研参照物；内置图标是自绘 `forge` 集 |
| 产出可被工程消费 | ✅ | 11 种标准投影 + bundle；DTCG 真兼容（v1 的 `$schema` 是编的，已废） |
| 预览与交付同源 | ✅ | 预览容器复用 `export?format=css`，不再前端另算一套 |
| 持久化 / 健壮 / a11y | ✅ | 12 表自建库 + 鉴权 + 批量事务回滚 + WCAG 2.2 审计门禁（v1 这行是假的：当时只有 localStorage 与文案自称的自检） |
| e2e 验证 | ✅ | `e2e/plugins/design-system/design-system.spec.ts` v2 全链路 17 段断言，2026-09-29 连跑两跑全绿（含"预览底色 == 后端令牌值"的逐位比对与 11 张读图） |
| 组件规格可浏览 | ✅ | `generate` 除写 `component.*` 令牌外，同步落地组件目录（10 个蓝本：按钮/卡片/输入框/徽标/导航/数据表/对话框/提示浮层/选项卡/选择器），清单只取本次真实生成的令牌 |
| 声明的能力面供给得出 | ✅ | 资产 / 字体 / 页面清单从"只有 GET、永远空表"补成"生成有种子 + 界面写得进"（v2.2.0）；`meta.capabilities` 每一项都要能读能写，否则撤下声明 |

### 已知缺口（诚实）

| # | 缺口 | 影响 | 优先级 |
|---|------|------|--------|
| G1 | 行业推断仍靠关键词打分，不理解语义 | 奇异/跨品类需求只能得较通用的骨架（可用显式参数覆盖） | P2 |
| G2 | ~~导出体积未实测~~ **已闭合（2026-09-29）**：后端用例打印 `[T302]`（整包体积/构件数/构建耗时），e2e 打印 CSS / data.sql / DTCG 三个 KiB | 极端规模仍未做分页/流式 | ✅ |
| G3 | 无 Figma **双向**同步（只出 Tokens Studio 兼容文件） | 设计侧改回 Figma 仍要手工搬运 | P3 |
| G4 | 插件 src 无 eslint 入口（宿主 flat config 覆盖不到 `../Plugins/**`） | 规范只靠 vue-tsc + 人工 | P2 |
| G5 | `plugin.json` entry 无 content-hash 缓存键 | 同版本号改前端需硬刷（属宿主加载器，见 ROADMAP P1.3） | P2 |
| G6 | 产出是规格+令牌，不是像素稿 | 不能直接"照着切" | 明确不做 |
| G7 | ~~组件变体 × 状态矩阵为空~~ **已闭合（2026-09-29）**：格子由 `component.<code>.<variant>.<part>[-<state>]` 真实路径推导 | 无令牌支撑的组合不落格（不发明状态） | ✅ |
| G8 | 宿主 SQLite 并发读写偶发 `SQLITE_BUSY` → 本插件接口 500 | 已做四件：连接串 `Busy Timeout=5000`、审计/目录**整批一个事务**、发布按项目**串行化**、只读请求退避重试。跨请求写锁的根治仍在宿主 DAL | 🟡 已缓解 |
| G9 | ~~品牌三表没进导出投影~~ **已闭合（2026-09-29）**：CSS 出 `@font-face`（仅登记文件的行）、bundle 落 `brand/*.svg` + `fonts.json` + `screens.json`、DESIGN.md 增「品牌与资产」段 | 后端用例 3 条钉住（170/170） | ✅ |
| G11 | **版本快照与 diff 覆盖品牌三表与组件目录**（v2.3.0）：快照 schema 2 带 `specs`，规格进哈希，旧快照报"不可比" | 后端 5 条用例 + e2e 界面可见（175/175） | ✅ |
| G12 | ~~组件规格没进机器可读产物~~ **已闭合（2026-09-29，v2.4.0）**：registry 条目带 `anatomy/a11yNotes/states/variants[]`、DESIGN.md 逐蓝本列约束；Stardust 清单的 `total` 与明细端点 `EntityRows` 同源，`GET {id}/{entity}.json` 真存在（FR13 补齐） | 后端 4 条用例（178/178）+ e2e 逐实体比对真实行数 | ✅ |
| G13 | ~~设计系统落不到"被它服务的系统"身上~~ **已闭合（2026-09-30，v2.5.0 · spec U3）**：新增 `element-plus` 投影（`--el-*` → `--ds-*`，零字面色值、缺档如实未映射）+ bundle 内逐主题一份 | 后端 3 条用例（181/181）+ e2e 核对每个引用都能在同主题 tokens.css 里定义 | ✅ |
| G14 | ~~审计只盯生成器、不盯手改~~ **已闭合（2026-09-30，v2.6.0）**：新增 `target-size` / `ramp-monotonic` / `naming` / `lifecycle-ref` 四类维度（档序取自 `ScaleGenerators` 同一张表，不另列第二份） | 后端 4 条用例（185/185）+ e2e 现场改值跑审计（18px 被抓、改回 28px 消失） | ✅ |
| G10 | 整包 `bundle` 在**浏览器侧**取不到 zip：`page.evaluate(fetch)` → `Failed to fetch`；点 `<a download>` → 事件到了但 `download.path: canceled`。已排除"太慢"（后端构建 605ms） | 产物本身是好的（后端用例直接量过体积/构件数），但**用户点下载这条路径没有证据**；根因待查（见 TODO） | P1 |

> v1 的 G2「产物不落后端」与 G4「无自动对比度校验」已在 v2.0.0 关闭；历史描述留在 `ROADMAP.md` 正文里作决策记录。

---

## 八、为后来者准备的下一步

如果你接手这个插件，下面是**最有价值、按优先级排序**的下一步（详细方案与触发条件见 `ROADMAP.md`）：

### P1 · 建议优先做

1. **修好"用户点下载"这条路径（缺口 G10）**
   - 现状：整包在服务层是好的（后端用例量过构件数与体积），但浏览器侧两种取法都拿不到 zip（`Failed to fetch` / `download.path: canceled`）。
   - 做什么：先绕开 vite 代理直连宿主端口比对，确认是代理链还是宿主写响应的方式；若是 `ExportedFile` 一次性 `byte[]` 的问题就改流式返回，并在 e2e 里用 `download` 事件把这条路径钉住。
   - 判据：e2e 能拿到 zip 且字节数 > 包内任一单工件；根因写成事实而不是猜测。

2. ~~**把组件规格（含 a11y 约束与矩阵）供给进机器可读产物**~~ **已闭合（v2.4.0，缺口 G12）**
   - 做法与判据都落地了：registry 条目带 `a11yNotes` + `anatomy` + `states` + `variants[]`（每格轴值与令牌清单）+ `unresolvedTokenRefs`，
     DESIGN.md 「组件规格」逐蓝本列解剖/状态/变体轴/令牌条数/可达性要求；内容全部从 `DesignComponent` / `DesignComponentVariant` 读，投影里不另算一份。
   - 同族另一半也补了：spec FR13 的 `GET api/design-system/{id}/{entity}.json` 过去**只有清单没有明细**（且清单里 `design-icon/screen/font-face` 写死 0、
     `design-component` 数的是令牌条数）。现在清单 `total` 与明细 `data[]` 同出 `EntityRows`，两边不可能再各说一套。
   - 还没做的一件（下一位可从这里接）：界面「导出台」没有把这十类实体 url 列出来给用户直连——目前是纯后端契约。

3. **修复宿主体质性插件版本/更新 API**（宿主侧，见 `ROADMAP.md §P1.2`）
   - 为什么：`PluginVersionService.Initialize(pluginsPath)` 从未调用，导致 `/api/plugin/updates|update|versions|rollback` 死代码；新插件 install 把文件写到 exe 根而非 `publish/plugins/<id>/`。
   - 做什么：在宿主启动时 Initialize PluginVersionService；install 路径改为 `AppContext.BaseDirectory/plugins`。

4. **宿主 SQLite 并发锁治理（缺口 G8，属宿主）**
   - 为什么：并发读写会冒 `SQLITE_BUSY` → 接口 500；本插件侧的重试只是止痛。
   - 做什么：宿主级"写串行化/统一重试策略"或逐库 PRAGMA 方案，需人拍板后动（见 TODO.md P1）。

5. **pluginViewLoader 缓存键改为 version + content-hash**（宿主侧，见 `ROADMAP.md §P1.3`）
   - 为什么：现在同版本号的前端改动用户必须硬刷才能看到。
   - 做什么：构建时计算 dist/index.js 的 hash，清单 URL 用 `?v=<ver>&h=<hash>`。

> 历史项「后端持久化设计产物」（原 P1.1）已在 v2.0.0 完成：12 张表 + 批量事务 + 快照文件，localStorage 不再是真相源。

### P2 · 体验增强

6. **LLM 增强模式（可选开关）**
   - 触发：用户明确要求「更聪明 / 读懂复杂需求」。
   - 做法：保留确定性引擎作为 fallback 与回归基线，复杂需求走 LLM，简单/重复需求走确定性引擎。
   - 见 `ROADMAP.md §P2.1`。

7. **自动对比度 / 截图回归校验**
   - 触发：自检第 2 条（对比度）需要自动化；展示页换肤后需防止回归。
   - 做法：Playwright 截图基线 + WCAG 对比度脚本。
   - 见 `ROADMAP.md §P2.2`。

> 已完成项（不再列作"下一步"）：把「门禁 + 真宿主 e2e + 发布 + 读图走查」沉淀为可复用 skill —— 见 `.agents/skills/design-system-verify/SKILL.md`（四层门禁 + "假能力自查表"，本插件专用）。

### P3 · 生态

8. **Figma / Tokens Studio 插件**
   - 触发：设计团队真正开始消费 token。
   - 做法：由 `tokens.json` 生成 Design Tokens Format 兼容格式。
   - 见 `ROADMAP.md §P3.1`。

9. **社区预设市场**
   - 触发：用户开始贡献自己的行业画像 / 预设。
   - 做法：把 `presets.ts` 拆成按文件加载的 presets 目录，支持导入/导出预设。
   - 见 `ROADMAP.md §P3.2`。

---

## 九、历史与关键决策

- **2026-09-01**：v1.0.0 是静态 Stardust 展示柜（token/组件/页面），无生成能力。
- **2026-09-01**：v1.1.0 补齐设计工作台（ProductDesign 模型），引入生成、导出、持久化、StageDesignSystemPlugin 目标。
- **2026-09-02**：v1.2.0 重构为通用设计系统生成器：`DesignSystem` 通用模型、Stardust 降参考示例、外壳中性、展示页换肤、产出文件 6 类。
- **2026-09-29**：**v2.0.0** 库驱动重写（12 表 / 三层令牌 + 别名图 / 多主题与密度轴 / WCAG 2.2 审计门禁 / 11 投影 / 不可变快照与 diff / 13 个 section 界面 / 内置 40 枚零许可证图标）。
- **2026-09-29**：**v2.1.0** 组件规格从"只有令牌"变成"能浏览的规格 + 变体矩阵"：
  生成时落 10 个组件目录（含对话框/提示浮层/选项卡/选择器），矩阵格子由 `component.<code>.<variant>.<part>[-<state>]` 真实路径反推；
  组件层补尺寸轴（sm/md/lg）与 focus/disabled 态令牌。
  同版本产出变了就必须升版本号 —— 2.0.0 的 zip 已经存在，若继续叫 2.0.0，"同版本同输出"这条复现契约就是假的。
- **2026-09-29**：**v2.2.0** 把 `meta.capabilities` 里声明却"拿不到"的三项补齐：资产 / 字体 / 页面清单。
  此前它们**只有 GET、也没有任何地方种数据**，界面只能显示"无…"——声明了能力却供给不出，与假能力同罪。
  现在：`generate` 落三类种子（字体从 `font.sans`/`font.mono` 逐成员登记并写明许可证；页面按行业倾向给起手屏并在描述里写明"建议不是产品事实"；
  logo + 母题两枚原创 SVG，只用 `currentColor`）；新增三个 POST 写入口与「品牌资产」section，写完立即回读自证。
  两个跑起来才暴露的缺陷一并修掉：① 重跑生成原先会**覆盖用户改过的品牌行**（= 删用户数据）→ 改为按自然键只补空；
  ② 界面模板引用的 `ds-btn` / `ds-btn--ghost` / `ds-input` 在别处定义、本组件没有（scoped 不泄漏），build 全绿却渲染成浏览器默认控件
  → 新增 `design/classes.test.ts` 守 `ds-*` 词汇表类，并补上手改保护回执（`skippedProtected`/`conflicts` 此前从未显示）。
  同日补 M8：**品牌三表进导出投影**（CSS 的 `@font-face` 规矩、bundle 的 `brand/` 逐文件工件、DESIGN.md 的「品牌与资产」段）——
  写入口与库里数据齐了之后，"交付物里仍然没有它"是同一家族的另一半假能力。
- **2026-09-30**：**v2.3.0** 版本化补齐（M9）：快照 schema 1 → **2**，新增 `specs` 一节覆盖组件目录 / 变体格子 / 资产 / 起手屏 / 字体登记，
  形状 `kind + key + 字段表`，diff 报到字段级；**规格进哈希**（否则"只换了 logo"会被当成与上一版一致而幂等放行）；
  旧快照缺这一节时后端回 `specsComparable=false`、界面显示"不可比"而不是凭空"新增 N 条"。
  必须换版本号的理由：v2.2.0 的 zip（2.3.2）里发布的快照与 diff 形状就是旧的，同号换形状会砸消费方。
- **2026-09-30**：**v2.4.0** 组件规格供给进机器可读产物（M10，缺口 G12）：registry 条目从"一串令牌路径"变成带 `anatomy/a11yNotes/states/variants[]` 的规格，
  DESIGN.md 增「组件规格」节逐蓝本列约束；同时补齐 spec FR13 欠的那一半 ——
  `GET api/design-system/{id}/{entity}.json` 十类逻辑实体明细**过去根本不存在**，且清单里 `design-icon/screen/font-face` 写死 0、`design-component` 数的是令牌条数。
  现在清单 `total` 与明细 `data[]` 同出 `EntityRows`（结构上不可能再各说一套），三元组与 `plugin.json.Version` 同步 2.4.0。
  后端 4 条用例（178/178）+ e2e 在真实宿主里逐实体比对行数、比对 `design-component` 与组件目录条数相等。
- **2026-09-30**：**v2.5.0** Element Plus 换肤接缝（M11，收 spec U3）：新增导出格式 `element-plus`（+ bundle 内 `element-plus/theme.<theme>.css`），
  把 `--el-*` 接到我们自己的 `--ds-*` 上 —— 宿主界面就是 EP 写的，这套设计系统从此能落到"被它服务的系统"身上，而不只是交付给别人。
  接缝里**每个引用都必须由同主题 `tokens.css` 真定义**，缺的档位如实列"未映射"；EP 的 `light-N/dark-2` 按比例但改为向本页底色/墨色混合，深色主题才不会洗成灰白。
  本轮由测试抓到的真缺陷：`color-mix` 的第二个颜色写成了裸的 `--ds-…`（不是颜色值）→ 整条声明在 computed-value 阶段失效；已修并加断言。
  后端 3 条用例（181/181）+ e2e 在真实宿主里核对引用可解析与 EP 变量覆盖数。
- **2026-09-30**：**v2.6.0** 门禁补上"对用户手改也成立"的四类维度（M12，缺口 G14）：`target-size`（2.5.8 的 24px）/
  `ramp-monotonic`（space·radius·duration 按声明序必须递增）/ `naming`（点分 kebab，否则投影静默产出坏键）/
  `lifecycle-ref`（仍被引用的 deprecated·removed）。档序不另列一份 —— 直接读 `ScaleGenerators` 的 `SpaceSteps`/`RadiusSteps`/`DurationSteps`。
  两条新维度一上线就各自抓到我自己的错：正则把生成器自己的 `z-index.1` 判成不合形（段首连字符规则写错了），
  以及尺度消息只报档名不报完整路径。都按"先红后绿"改掉。
  后端 4 条用例（185/185）+ e2e 现场把 `component.button.sm.min-height` 压到 18px 跑审计必须抓到、改回 28px 必须消失。
- **2026-09-30**：**v2.6.1** 修掉一条"静默空跑"的门禁（自查时抓到，不是用户报的）：`ramp-monotonic` 的数值解析只认 `dimension + px`，
  而 `duration.*` 是 `duration` 类型、值带 `ms` —— 于是界面上门禁口径写着"space / radius / duration 三类"，实际只检了两类。
  改成 dimension/duration/number 都能取数（消息里显示各自的原文单位：`duration.base=200ms → duration.macro=100ms`），
  并补一条用例 `尺度单调性对时长族也必须真的生效_不能是静默空跑`（先红后绿）。
  同版本换内容不行（"同版本同输出"是复现契约），故 v2.6.0 的包作废、重打 **v2.6.1**。
- **2026-09-30**：**v2.6.2** 对比度这一维从"写死的八对清单"改为**按命名约定覆盖库里全部前景/背景对**（ROADMAP P1.14）：
  `component.<ns>.foreground[-状态]` → 同命名空间的 `.background[-状态]`/`.background`/`.tint[-状态]`/`.tint`。
  上线即抓到两条真缺陷：一是 `dialog`/`tooltip`/`select` 这些生成器自己产出的组件从来没被审计查过；
  二是禁用态按 4.5 判会一次冒出 9 条 critical（实测 3.41:1）—— 按 WCAG 1.4.3「非活动构件」豁免改成只报读数（退回 3.0 兜底线，info/warning，永不 critical），
  豁免项仍带 `rule=wcag22-1.4.3-exempt` 落库，不是跳过。写这套用例时又暴露第三个洞：`Record` 只覆盖本轮产出的键，
  某条对不再被判定后上一轮的 `passed` 行留在库里冒充今天查过（`HasBlocking` 就读它）→ 现在整批写入后清掉本轮没产出的旧行。
  后端 3 条用例（190/190，其中两条先红后绿）。
- **2026-09-30**：**v2.6.3** 补上 v2.6.2 自己留下的另一半：审计"清掉本轮未产出的旧行"之后，一个**还在库里但这轮判不成**的对象
  （声明为 color 却解析不出颜色，例如别名指到 `space.4`）会从审计里**静默消失** —— 界面上既不见红也不见黄，与"查过且没问题"一模一样。
  现在这类对象自己落一条 `rule=wcag22-1.4.3-unresolved`（warning，不拦发布；别名本身失败仍由 `alias` 报 critical，不重复记账），
  界面门禁口径同步写明。用例先红（`Expected row not to be <null>`）后绿；e2e 加"改坏→出现 unresolved→改回→消失"三段。
  后端 190/190。
- **2026-09-30**：**v2.6.4** 把"顺序"收成一份真相（ROADMAP P1.15）：新增 `DesignSystemConstants.VariantAxes`（size/role/state 三条档位序），
  生成器认轴与矩阵 `SortOrder`、DESIGN.md / registry / Stardust 实体三处排序全部读它；`GET /meta` 把词表出给前端，界面不再 `.sort()`。
  动因是 e2e 新断言当场抓到的三方不一致（界面 `default、disabled、hover、active` vs 产物 `default、hover、active、disabled`）——
  投影改了，读路径 `ListVariants` 还在按 `State` 字母序，前端又自己排一次。后端 2 条用例（档位序 + 矩阵读序）。
- **2026-09-30**：**v2.6.5** 把同一把尺子量到**尺度档位**（ROADMAP P1.16）：`ScaleGenerators` 新增 `SpaceOrder` / `RadiusOrder` / `DurationOrder`
  （`RadiusOrder` 必须含 `pill`/`full` 两个绝对值档 —— 它们不在倍率表里，词表漏了它们就等于漏了库里真实存在的档位），
  `GET /meta` 出 `scaleOrders`，`DensityScales` 删掉前端自己列的 `NAME_ORDER`，`ShadowMotion` 的阴影列表改走同一个比较器。
  顺带修掉潜伏的解析陷阱：`stepOf` 原先用 `parseInt`，于是 `radius.2xl` 的 "2" 被当成档位号，把 `2xl` 插到命名档中间（`pill`/`full` 反而排在 `2xl` 前）——
  现在只有**整段是数字**才算数值档。vitest 4 条新用例 + e2e 钉"界面上 `space.` / `radius.` 的顺序 == `meta.scaleOrders`"。
- **2026-09-30**：**v2.6.6** 词表族收尾（ROADMAP P1.17）——界面侧最后三份手抄词表清零，改为全部读 `GET /meta`：
  `derive.ts` 的 `TIER_ORDER`（→ `meta.tiers`，含"全部层级"下拉的选项）、`colorFamilies` 里的 `brand/accent/neutral/…`
  （→ `meta.colorFamilies`）、`AuditBoard` 的 `KINDS`（→ `meta.auditKinds`，门禁说明清单与分组排序都读它）。
  两张新表是**真表不是抄表**：`ColorFamilies.All` 由生成器逐族消费（`RampFor` 没有对应规则的族 = 抛，族集合与顺序由后端用例双向核对），
  `AuditKinds.All` 与 `AuditEngine` 的产出用一条用例双向核对（表里有却造不出触发场景 = 空声明；产出了表外的类 = 界面筛不出来）。
  补格子的 `state` 从自由文本改成下拉（词表 = `meta.stateOrder`），逃生口是**显式选中「自定义」**再填，e2e 顺带钉"逃生口能收回去"。
  新增守卫 `web/src/design/vocabulary.test.ts`：界面源码里再出现"同一行 ≥3 个互不相同的已知成员"字面量清单就红，
  反向探针（临时塞一份镜像）已验证它真会红。`/meta` 一并补出 `roleOrder`（此前只有后端消费，界面没法用）。
  **如实记**：`roleOrder` 这一轮仍**没有界面消费者** —— 组件库矩阵的行序还来自 `ListVariants` 的 `VariantKey` 字母序，
  且 `Variant` DTO 没把轴值出出来；已记 TODO，与"按轴取值的表单"同批做（不把它算作已交付）。
- **2026-09-30**：**v2.6.7** 契约可见 + 变体表单收口（ROADMAP P1.18）：
  ① `/meta` 出 `entities`（= `ExportService.StardustEntities`），「导出交付」页新增**十类逻辑实体插座表**——
  每行是当前项目该实体的真实 url，且「行数」是页面**按当前主题真的去读** `GET api/design-system/{id}/{entity}.json` 拿回的 `total`
  （只摆链接不读就是"看起来能用"）；换主题会整表重读，不留旧数。
  ② 变体的**分组序**改读 `VariantAxes`：`ListVariants` 以前按 `VariantKey`（JSON 文本字典序）分组，
  于是 `{"role":"danger"}` 排在 `{"role":"primary"}` 前面 —— 矩阵是设计师核对档位的那张表，字母序凑它等于没有序。
  ③ `/meta` 的三条并列字段 `stateOrder`/`sizeOrder`/`roleOrder` 合并成**一份 `variantAxes` 清单**（轴 → 档位序，顺序 = 表的声明序）：
  三条字段就是同一张表在契约里抄三遍，加第四条轴必漏；现在加轴只改 `VariantAxes.Orders` 一处。
  ④ 有了这份清单，「新建变体」不再让用户敲 `{"size":"md"}`：**先选轴、再选档位**，JSON 由界面按后端 canonical 口径拼好（只读），
  词表外的轴/档位仍要显式选「自定义」，多轴组合走显式的「直接写 JSON」开关（前端不发明第二套序列化）。
  ⑤ `.ds-mono` 补上 `text-transform: none`：这行原先整条套在 `.ds-micro`（uppercase）里，图标 code 显示成 `DASHBOARD` 而库存 `dashboard` ——
  用户照屏幕抄回接口就 404，标识的可抄性比版式重要。
- **2026-09-30**：**v2.6.8** 画布投影身份收成状态 + 导出读路径去 N+1（ROADMAP P1.19）：
  ① 新增 `skinTheme`（**已注入投影属于哪一档**）与 `skinApplied`。以前状态里只有一段 CSS 文本，"画布现在是哪一档"没人记，
  于是**界面选中的主题**和**画布实际用的主题**可以是两回事（投影只在「看效果」页注入，在别的页切档不重取）——
  同一操作步骤的截图两次运行一暗一亮就是这么来的。现在主题条按四态说清：`applied` / `pending`（待重取 X → Y）/
  `unloaded`（本次会话还没取过，进预览页时按 Y 取）/ `unavailable`（没项目或没导出能力）；预览画布顶部另有角标显示档位与字面颜色值。
  ② `cssVar` 从"声明了却没人用、且正则把 `--` 前缀拼错"变成真被使用的取数口，并加 `resolveCssVar` 顺 `var(--x)` 链
  （语义层是别名、原语层才写字面色）：角标因此能显示 `surface-bg #211f25` 这种**与渲染像素逐位对得上**的真值，
  而不是 `var(--ds-color-neutral-950)` 这种用户还得自己解的中间态。
  ③ 导出与快照的变体读从"逐组件查一次"（N+1）改成一次批量 `VariantsByComponent`（行序与逐组件读**逐位一致**，有断言钉），
  导出页的读回加限流（同时在途 ≤3）：实测二十几个并发只读请求会把宿主的 SQLite 顶出 `database is locked`（500）。
  前端对只读请求本来就退避重试（最多 4 次尝试），所以**界面上看不出来**——但重试是有上限的，而且每次重试都在拖慢这一页。
  收敛后**并没有消除** 500：连跑三轮 e2e 的只读 500 次数分别是 2 / 0 / 3（同一现象随机出现、落在不同端点），
  全部被重试兜住所以用例是绿的。宿主跨请求写锁本身仍是待拍板的宿主议题（不是这里能根治的）。
  ④ 新增后端门禁「每种声明的格式 × 库里每个主题都真能导出」：以前"全格式"只跑浅色档，暗色档才是别名链最密的地方。
  ⑤ 证据可复现性：e2e 把"进入预览页后投影必须到货""角标值 == 后端 `semantic.surface-bg` == 画布 `background-color` 渲染值"
  钉成断言，截图不再靠肉眼看明暗猜档位。
- **2026-09-30**：**v2.7.0** DTCG 导入/回流（ROADMAP P1.20，`docs/ai/pilot/design-system-import/`）：
  插件第一次能"进"外部令牌而不只是"出"。`POST projects/{id}/import/preview` + `POST projects/{id}/import`
  （`format=dtcg`，能力与上限由 `/meta` 声明：`importFormats` / `importLimits`，界面据此启用入口）。
  解析器 `DtcgImporter` 三条纪律：①层级/类型只从证据推——`$type` 沿树继承（与导出侧互逆），推不出就逐条拒并回原因；
  **库里已有路径的层级以库为准**（文件里没有 tier，按首段猜会把 `chart.series-1` 这类语义层降级成 primitive，
  其别名"逆向指向上层"触发整批图校验拒绝——实测踩过）；②**不搬家**：文件路径若库里已有（共享层或目标主题），
  写回它原本所在的主题，只有新路径才落到所选主题——否则 round-trip 会把共享 primitive 复制进主题层；③provenance 只进列
  （`Generator=imported` + `GeneratorSeed=sha256(文件字节)`），不塞 `$extensions`（否则第一次 round-trip 就不逐字一致）。
  默认 `overwrite=false` 保护手改/既有导入行（冲突逐条列出，不静默覆盖）；同一文件内路径重复、成环/悬空别名**整批不写**并回逐条诊断；
  条目上限 5000 / 4MB 在解析器里判（控制器与单测同链）。导入的新令牌进**同一套**对比度门禁（低对比的 component 前景/背景会被审计抓出）。
  「导出交付」页新增导入回流块：选文件 → 预览（将写入 N 条 + 被拒清单 + 冲突路径，不落库）→ 确认写入 → 生效值可查
  （e2e 断言别名顺到 `#123456`、导出→导入→导出 **55517 字节逐字一致**、成环样本拒写）。
- **2026-09-30**：**v2.7.1** 插件头部重排（用户指令）：去掉 BrandLogo 三角标识（「测试」其实是项目名字标、跟着 logo 一起消失，
  项目上下文由右侧选择器承担），「设计系统」标题与版本徽标（模型/生成器/投影）收进同一行、副标题独立一行；
  `BrandLogo.vue` 随之成为零引用死组件，按「声明了没人用＝假能力」口径移入 `.trash/`
  （`classes.test.ts` 的逐文件守卫自动少一条 BrandLogo 用例，属预期）。vitest **75**、`index.js` 297.56 kB。

关键修复：
- 移除 CSS 顶部 `@import url(fonts.googleapis.com)`，避免渲染阻塞导致插件无样式。
- 补齐 `StageDesignSystemPlugin` MSBuild Target，否则 `dotnet publish` 不复制插件 DLL。
- token 单一事实源：`design/tokens.ts` → `scripts/gen-tokens-css.ts` → `styles/tokens.css`。

---

## 十、参考

- 插件前端运行时契约：见 `specs/010-plugin-frontend`（或宿主板对应的插件加载文档）。
- 发布/真宿主验证 SOP：`.agents/skills/plugin-publish-verify/SKILL.md`。
- 插件层 e2e SOP：`.agents/skills/e2e-testing/SKILL.md`。
- 路线图：`./ROADMAP.md`。
