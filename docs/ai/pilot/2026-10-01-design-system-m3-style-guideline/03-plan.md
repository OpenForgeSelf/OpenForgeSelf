# Plan

> 阶段：Stage 3｜具体到真实文件路径。「新增」= 仓库里目前不存在（已核实）；「改」= 已存在。
> Task ID：PILOT-ds-m3-style-guideline｜依赖：M1、M2 均已合入并验收通过（契约见各自 03-plan）

## 已定决策（闸门1 的默认推荐，用户可在批准时改）

| #   | 决策                                                           | 理由                                                                                          |
| --- | -------------------------------------------------------------- | --------------------------------------------------------------------------------------------- |
| D1  | `DesignGuideline` **不**进 Stardust 十类实体                   | 外部兼容契约与 e2e `entities.length===10` 不动；规范经 brief / design-md / bundle / REST 交付 |
| D2  | **不**新增审计类别（规范引用失效只给 `brokenRefs` + 界面标注） | 不动 `AuditKinds.All`、门禁口径文案与 e2e 的 kindmap 断言；候选记入 ROADMAP                   |
| D3  | **不**扩充组件蓝本（仍 10 个）                                 | 新组件会改变默认产物，破坏"默认轴逐字节兼容"黄金回归；M2 模特已够用                           |
| D4  | **不**回填存量项目的规范                                       | 避免静默改变已发布版本内容哈希；仅用户触发时写入                                              |
| D5  | 规范**只写令牌路径不写数字**，数字渲染时现查                   | 令牌改值规范不漂移（自查表 #22 同源）                                                         |
| D6  | 预设库 8 → 13（新增 5 个，原 8 个一字不改）                    | 覆盖每个轴的每个非默认取值；原 8 个保持黄金回归                                               |

## Files To Change

**后端（`Plugins/DesignSystem/`）**

- `Services/StyleAxes.cs`（新增，`public static`）：词表/默认值/校验/`seed 后缀`（§A1、§A10）。
- `Services/DesignGenerator.cs`（改）：`GenerationRequest` 增 7 个可空字段；`Generate` 起始处校验；把轴取值贯穿到 `ScaleOptions`（阴影/描边）、`TypeOptions`（字体）、`RampFor(Neutral/accent)`、`ComponentTokens(radiusMap)`；`BuildSeed` 仅在非默认时追加后缀；`SeedBrandCatalog` 对 `font.display` 增 `role=display` 登记；`SeedGuidelines` 调用（经 `GenerationService`）。**默认路径的计算顺序/取整/格式一字不改。**
- `Services/ScaleGenerators.cs`、`TypographyGenerator.cs`、`ColorRampGenerator.cs`（改，最小增量）：`ScaleOptions` 增 `ShadowStyle`/`BorderStrength`（默认值=现状）；`TypeOptions` 增 `FontDisplay`（默认 null）；`Shadows`/`Border` 按风格分支；`GenerateNeutral` 已有 `tint` 参数无需改。
- `Services/StylePresets.cs`（改，M1 产物）：追加 5 个预设（§P），不动原 8 个。
- `Services/GuidelineGenerator.cs`、`GuidelineRepository.cs`、`GuidelineService.cs`、`GuidelineRenderer.cs`、`GuidelineCategories.cs`（新增，`public sealed`/`public static`）。
- `Data/Model.xml`（改）：**仅新增** `DesignGuideline` 表（§G1）；`Data/Entities/DesignGuideline.cs`（xcode 生成物，不手改）、`DesignGuideline.Biz.cs`（新增，业务验证）；`Data/DesignSystem.htm`（xcode 随生成更新）；`Data/DesignSystemTables.cs`（改：`EntityTypes` 登记）。
- `Services/GenerationService.cs`（改，M1 产物）：在组件/品牌种子之后调 `SeedGuidelines`；响应增键 `guidelines`。
- `Services/ExportService.cs`、`DesignBriefBuilder.cs`、`AgentRulesBuilder.cs`（改）：规范进 brief / design-md / bundle / Manifest / agent-rules；`Snapshot` 新增**末位可选参数** `Guidelines`（默认空，免得波及既有构造点）。
- `Services/ReleaseService.cs`（改）：`CurrentSchema=3`；`BuildSpecs` 增 `guideline`；`DiffOf` kind 级可比性；`ReleaseDiff` 增 `NotComparableKinds`（末位参数）；`Hash` 无需改（规格已进哈希）。
- `Services/DesignMapper.cs`（改，若 diff 的 DTO 在此）：输出 `notComparableKinds`。
- `Controllers/DesignSystemController.cs`（改）：规范 5 个端点；`meta` 增 `styleAxes`/`guidelineCategories`/capability `guidelines`。
- `Agent/*`（改，M1 产物，**只增不改**）：`design_edit action=guideline`、`design_lookup kind=guideline`、`design_context` 章节名 `guidelines`、`design_review` checklist 派生、`design_guide` 文案；`DesignToolIndex` 描述同步；schema `properties` 只增。
- `Services/DesignSystemConstants.cs`、`plugin.json`、`README.md`、`ROADMAP.md`、`docs/02-features/036-design-system.md`（改）：3.1.0 与事实同步。

**前端（`Plugins/DesignSystem/web/src/`）**

- `shell/nav.ts`（改，M2 产物）：增 `guidelines` 项（标签 `UX 规范`，分组「建系统」，`capability:'guidelines'`）。
- `sections/Guidelines.vue`（新增，第 15 个 section）+ `DesignSystemView.vue`（改：主区 `v-else-if="active==='guidelines'"` 增一支）。
- `sections/ReleaseBoard.vue`（改，**仅**新增一处 `notComparableKinds` 提示渲染，不改既有 DOM/类名/文案）。
- `api.ts`（改）：`listGuidelines/getGuideline/putGuideline/generateGuidelines/archiveGuideline` + 类型；`MetaInfo` 增 `styleAxes/guidelineCategories`。
- `showroom/TunePanel.vue`、`start/StartMode.vue`（改，M2 产物）：折叠区「更多风格选项」；`showroom/tune.ts`（改：映射扩展）；`design/glossary.ts`（改：新词条）；`design/vocabulary.test.ts`（改：`VOCABULARIES` 增 `styleAxes`，并保留既有项）。
- 不改：既有 14 个 `sections/*.vue`（`ReleaseBoard.vue` 的上述一处除外）。

**测试 / e2e / 文档技能**

- `ForgeSelf.Api.Tests/Plugins/DesignSystemTests/`：`StyleAxisGoldenTests.cs`、`StyleAxisTests.cs`、`GuidelineSchemaTests.cs`、`GuidelineGeneratorTests.cs`、`GuidelineServiceTests.cs`、`GuidelineRestTests.cs`（含反射：无 DELETE 路由）；改 `ExportProjectionTests`、`DesignBriefTests`、`ReleaseSnapshotTests`、`PresetTests`、`PreviewCssTests`、`MannequinVariableContractTests`、`DesignAgentToolTests`、`GenerateShapeTests`、`DesignSystemAuthTests`（均为**增量**，每处改动登记偏差记录）。
- `ForgeSelf.Web/e2e/plugins/design-system/design-system-style.spec.ts`（新增：A 块风格轴 / B 块规范）；既有 spec 不改。
- `.agents/skills/design-system-verify/SKILL.md`（改）、`.agents/skills/design-system-consume/SKILL.md`（改）。
- `docs/ai/pilot/2026-10-01-design-system-m3-style-guideline/*`、日记、`TODO.md`。

## 实现契约（零自决详表）

### §A1 风格轴词表（`StyleAxes`，唯一真源；`GET meta.styleAxes` 原样输出 `[{axis,field,kind,values?,default,min?,max?}]`）

| 轴         | 请求字段         | 取值（**首项 = 默认**；`null` 等价默认）                                                                                                         |
| ---------- | ---------------- | ------------------------------------------------------------------------------------------------------------------------------------------------ |
| 阴影风格   | `shadowStyle`    | `soft`、`crisp`、`flat`、`layered`                                                                                                               |
| 阴影强度   | `shadowStrength` | 数值，范围 0–2，默认 1（越界夹取并写 `Notes`）                                                                                                   |
| 描边强度   | `borderStrength` | `regular`、`bold`                                                                                                                                |
| 中性色温   | `neutralTemp`    | `brand`、`cool`、`warm`、`pure`                                                                                                                  |
| 字体搭配   | `fontPairing`    | `modern`、`system`、`humanist`、`editorial`                                                                                                      |
| 圆角风格   | `radiusStyle`    | `soft`、`sharp`、`round`、`pill`                                                                                                                 |
| 强调色策略 | `accentStrategy` | `complement`（≡既有默认偏移 168°）、`analogous`（30°）、`split`（150°）、`triadic`（120°）、`mono`（0°）；**非 null 时优先于 `accentHueOffset`** |

### §A2 白名单：每个轴的非默认取值只允许改这些路径（比较字段 = `Path|Tier|Type|Value|AliasPath|ValueJson`，**不含 `GeneratorSeed` 与 `Extensions` 里的 seed 字样**）

| 轴                           | 允许改动                                                                                                                                     | 必须改动（AC3）                                                |
| ---------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------- |
| shadowStyle / shadowStrength | `shadow.*`（各色向主题层；含 `DesignShadowLayer` 展开）                                                                                      | `shadow.elevation-1..5` 中 ≥3 条的 `Value`/`ValueJson` 变化    |
| borderStrength               | `border.hairline`、`border.thick`                                                                                                            | 两条值都变（`bold`：`2px` / `3px`）                            |
| neutralTemp                  | `color.neutral.*`，以及语义层里**选中的 neutral tone**（别名目标路径可变）；其解析值随之变                                                   | `color.neutral.*` ≥6 条 hex 变                                 |
| fontPairing                  | `font.sans`、`font.mono`；`editorial` 另**新增** `font.display` 并把 `type.display/h1/h2/h3` 的 `ValueJson.fontFamily` 改为 `{font.display}` | 对应 `font.*` 值变（`modern` = 默认不变）                      |
| radiusStyle                  | `component.*.radius`（button/input/select/badge/card/dialog/tooltip 及 `button/input.{sm,md,lg}.radius`）的 `AliasPath`                      | ≥3 条别名目标变                                                |
| accentStrategy               | `color.accent.*`、`color.info.*`，以及选中它们的语义角色（`link`/`info` 等）的别名目标                                                       | `color.accent.*` ≥6 条 hex 变（`complement` 即默认，不要求变） |

### §A3 阴影公式（`ScaleGenerators.Shadows`；`soft` = 现有实现**一字不改**；`d`=密度系数，`S`=强度，`L`=1..5，数值一律 `Math.Round(x,1)`）

| 风格           | 亮色层                                                                                                                                  | 暗色层                                                                     | Note                             |
| -------------- | --------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------- | -------------------------------- |
| `soft`（默认） | 现状                                                                                                                                    | 现状                                                                       | 多层外投影 / inset 高光 + 外投影 |
| `crisp`        | 单层：`y=L·d`，`blur=L^1.35·1.8·d`，`spread=0`，`alpha=clamp(S·(0.07+0.05L),0,0.5)`，色 `#0f172a`                                       | 先插 1px 白 inset 高光（alpha 0.06，同现状），再同公式外层（色 `#000000`） | 锐利单层                         |
| `flat`         | 单层环线：`y=0`，`blur=0`，`spread=(L≤3?1:2)`，色 `#0f172a`，`alpha=clamp(S·(0.08+0.02L),0,0.3)`                                        | 单层环线：同形，色 `#ffffff`，同 alpha 公式；**不加** inset 高光           | 环形描边（无投影）               |
| `layered`      | `soft` 的全部层，再追加 `L≥2` 的环境层：`y=0.4·L·d`，`blur=L^1.6·1.5·d`，`spread=0`，色 `#0f172a`，`alpha=clamp(S·(0.03+0.02L),0,0.25)` | 同左（色 `#000000`），保留 inset 高光                                      | 多层 + 环境光                    |

系数可在**不改取值名**的前提下微调（若某主题审计出 critical 或读图不佳），调整记入 05「公式调整记录」。`shadowStrength` 对所有风格的 alpha 乘数；`ShadowStrength=0` 允许（阴影不可见但令牌仍存在）。

### §A4–§A9 其余轴落地

- **描边**：`regular` = `1px / 2px`（现状）；`bold` = `2px / 3px`。其余令牌不动（含 `component.focus.outline-width`）。
- **中性色温**（`RampFor(Neutral)`）：`brand` = `GenerateNeutral(hue)`（现状，tint 0.012）；`cool` = `GenerateNeutral(250, 0.014)`；`warm` = `GenerateNeutral(75, 0.012)`；`pure` = `GenerateNeutral(hue, 0)`。
- **字体搭配**（只用系统/通用字体栈，不分发字体文件）：
  - `modern`（默认）：sans `"Inter", "Noto Sans SC", system-ui, sans-serif`；mono `"JetBrains Mono", ui-monospace, monospace`（**与现状逐字相同**）。
  - `system`：sans `system-ui, -apple-system, "Segoe UI", "PingFang SC", "Microsoft YaHei", sans-serif`；mono `ui-monospace, "SF Mono", Menlo, Consolas, monospace`。
  - `humanist`：sans `"Source Sans 3", "Noto Sans SC", "PingFang SC", "Segoe UI", sans-serif`；mono `"Source Code Pro", ui-monospace, Menlo, Consolas, monospace`。
  - `editorial`：sans/mono 同 `modern`；**新增** `font.display` = `"Noto Serif SC", "Songti SC", "Source Han Serif SC", Georgia, serif`；`display/h1/h2/h3` 角色用 `{font.display}`，其余角色不变。
  - `TypeOptions` 增可空 `FontDisplay`（默认 null → 不产出 `font.display`、复合令牌不变，保证默认逐字节兼容）。
- **圆角映射**（`ComponentTokens(radiusStyle)`；`soft` = 现状）：

| 令牌（`component.` 前缀省略）  | soft     | sharp    | round    | pill           |
| ------------------------------ | -------- | -------- | -------- | -------------- |
| `button.radius`                | md       | sm       | lg       | pill           |
| `button.{sm,md,lg}.radius`     | sm/md/lg | xs/sm/md | md/lg/xl | pill/pill/pill |
| `input.radius`                 | md       | sm       | lg       | lg             |
| `input.{sm,md,lg}.radius`      | sm/md/lg | xs/sm/md | md/lg/xl | md/lg/xl       |
| `select.radius`                | md       | sm       | lg       | lg             |
| `badge.radius`                 | pill     | xs       | pill     | pill           |
| `card.radius`、`dialog.radius` | lg       | md       | xl       | xl             |
| `tooltip.radius`               | sm       | xs       | md       | md             |

- **强调色**：偏移表见 §A1；`accentStrategy` 非 null 时覆盖 `AccentHueOffset`；`Notes` 记"强调色策略 x → 偏移 y°"。

### §A5 黄金基线（**必须在改任何生成器代码之前录制，并单独成一步提交工件**）

- 规范化文本：对一次 `GenerationResult`，对 `shared` 与每个 `Themed[theme]` 分别取该层全部 `TokenPatch`，按 `Path` 序数排序，逐行 `Path|Tier|Type|Value|AliasPath|ValueJson|Extensions|GeneratorSeed`，以 `\n` 连接，SHA-256 小写 hex。
- 基线集合：`StylePresets.All` 的**原 8 个**（用其 `Request` 原样生成，主题取其 `themes`）× (`shared` + 各主题)，再加 3 个非预设请求：`{brief:"后台管理系统"}`、`{seedColor:"#7c3aed"}`、`{hue:200,density:"compact",themes:["light","dark","compact"]}`。
- 落点：`StyleAxisGoldenTests.cs` 内 `static readonly Dictionary<String,String> Baseline`（键 `"<预设id或请求名>|<层>"`）。录制方式：同文件里一个**默认跳过**的 `[Fact(Skip="仅用于重录基线；DS_RECORD_GOLDEN=1 时手动取消")]` 录制器，把当前输出打印到 `.temp/ds-m3/golden.txt`，人工贴入字典。**这是测试资产的录制，不是用一次性脚本做验证结论**；录制日期与 HEAD 写入 05。
- 之后每个轴实现完都要重跑本测试：默认值必须逐字节不变。

### §A10 校验、种子与 `Notes`

- `StyleAxes.Validate(req)` 在 `DesignGenerator.Generate` 起始调用：非法取值抛 `ArgumentException($"风格轴 {axis} 的取值 {v} 不在 [{string.Join(", ", values)}] 内")`（REST 经 `Guard` 回 400）；`shadowStrength` 夹取到 [0,2]。
- 种子后缀：`BuildSeed` 在**仅当存在非默认取值**时，于原 seed 串末尾按轴序（`shadow,border,neutral,font,radius,accent,shadowStrength`）追加 `;shadow=crisp;…`；全默认时 **seed 串与现状逐字相同**。
- `Notes`：对每个非默认取值追加一行中文说明（如"风格轴：阴影=锐利（crisp）"）。

### §P 新增预设（追加到 `StylePresets.All` 末尾；原 8 个不动；ids/名称/轴取值为契约，数值微调规则同 M1 §D）

| id                | 名称      | 一句话                                     | 性格词                       | kind                     | industry                     | hue | chroma | density     | ratio | basePx | radius | motion | request.industry | 轴取值                                                                                                                                  | 关键词                             |
| ----------------- | --------- | ------------------------------------------ | ---------------------------- | ------------------------ | ---------------------------- | --- | ------ | ----------- | ----- | ------ | ------ | ------ | ---------------- | --------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------- |
| `editorial-serif` | 杂志·衬线 | 阅读与出版：衬线标题、环线阴影、暖灰       | elegant, editorial, calm     | brand, marketing         | media                        | 25  | 0.12   | default     | 1.33  | 16     | 4      | 0.9    | media            | fontPairing=editorial、shadowStyle=flat、radiusStyle=sharp、neutralTemp=warm、accentStrategy=split                                      | 杂志, 阅读, 出版, 博客, 文章, 专栏 |
| `flat-minimal`    | 极简·扁平 | 工具与文档的极简：低彩度、粗描边、系统字体 | minimal, precise, neutral    | console, system, product | general, devtools            | 265 | 0.05   | compact     | 1.15  | 14     | 3      | 0.8    | devtools         | shadowStyle=flat、shadowStrength=0.9、borderStrength=bold、radiusStyle=sharp、neutralTemp=pure、fontPairing=system、accentStrategy=mono | 极简, 扁平, 文档, 知识库, 笔记     |
| `warm-craft`      | 手作·温暖 | 生活/社区/手作的暖琥珀与圆润               | warm, handcrafted, friendly  | product, marketing       | commerce, education, general | 55  | 0.14   | comfortable | 1.25  | 16     | 10     | 1.1    | commerce         | shadowStyle=layered、radiusStyle=round、neutralTemp=warm、fontPairing=humanist、accentStrategy=analogous                                | 手作, 生活, 社区, 咖啡, 家居, 烘焙 |
| `tech-crisp`      | 科技·锐利 | 科技/云/安全产品的紫蓝与锐利阴影           | technical, sharp, futuristic | product, console, brand  | devtools                     | 285 | 0.2    | default     | 1.2   | 16     | 4      | 0.85   | devtools         | shadowStyle=crisp、shadowStrength=1.15、radiusStyle=sharp、neutralTemp=cool                                                             | 科技, AI, 云, 数据, 安全, 区块链   |
| `kids-playful`    | 童趣·圆润 | 儿童/游戏/亲子的明亮粉紫与胶囊圆角         | playful, bright, round       | product, marketing       | education                    | 330 | 0.2    | comfortable | 1.3   | 16     | 12     | 1.2    | education        | radiusStyle=pill、shadowStyle=layered、fontPairing=humanist、accentStrategy=triadic                                                     | 儿童, 游戏, 童趣, 亲子, 幼教       |

**覆盖矩阵（测试钉）**：`shadowStyle` crisp/flat/layered、`borderStrength` bold、`neutralTemp` cool/warm/pure、`fontPairing` system/humanist/editorial、`radiusStyle` sharp/round/pill、`accentStrategy` split/mono/analogous/triadic、`shadowStrength≠1` ——每个非默认取值至少被 1 个预设使用。

### §G1 `DesignGuideline` 表（加到 `Model.xml` 的 `<Tables>` 末尾；风格与既有表一致）

```xml
<Table Name="DesignGuideline" Description="UX 规范">
  <Columns>
    <Column Name="Id" DataType="Int64" Identity="True" PrimaryKey="True" Description="规范ID" />
    <Column Name="ProjectId" DataType="Int64" Description="项目ID" />
    <Column Name="Code" DataType="String" Length="100" Nullable="False" Description="规范标识（项目内唯一，kebab）" />
    <Column Name="Category" DataType="String" Length="30" DefaultValue="layout" Description="分类：layout|page|navigation|form|feedback|state|content|a11y|motion|data" />
    <Column Name="Title" DataType="String" Master="True" Length="100" Description="标题" />
    <Column Name="Summary" DataType="String" Length="500" Description="一句话摘要" />
    <Column Name="Body" DataType="String" Length="4000" Description="正文（Markdown 文本；只写令牌路径不写数字）" />
    <Column Name="RulesJson" DataType="String" Length="4000" Description="规则清单（JSON：[{id,level,text}]，level=MUST|SHOULD|MAY）" />
    <Column Name="TokenRefsJson" DataType="String" Length="2000" Description="引用的令牌路径（JSON 字符串数组）" />
    <Column Name="AppliesToJson" DataType="String" Length="500" Description="适用用途类型（JSON 字符串数组，对应 DesignProject.Kind）" />
    <Column Name="Source" DataType="String" Length="20" DefaultValue="generated" Description="来源：generated|manual（manual 受重新生成保护）" />
    <Column Name="Status" DataType="String" Length="20" DefaultValue="adopted" Description="状态：adopted|draft|archived（归档为软删除）" />
    <Column Name="GeneratorVersion" DataType="String" Length="20" Description="生成器版本" />
    <Column Name="GeneratorSeed" DataType="String" Length="100" Description="生成种子（kind/industry/density）" />
    <Column Name="SortOrder" DataType="Int32" Description="排序" />
    <Column Name="Extensions" DataType="String" Length="2000" Description="扩展元数据袋（JSON）" />
    <Column Name="CreatedAt" DataType="DateTime" Description="创建时间" />
    <Column Name="UpdatedAt" DataType="DateTime" Description="更新时间，兼作乐观并发依据" />
  </Columns>
  <Indexes>
    <Index Columns="ProjectId,Code" Unique="True" />
    <Index Columns="ProjectId,Category" />
    <Index Columns="ProjectId,Status" />
  </Indexes>
</Table>
```

流程：先对**未改动**的 `Model.xml` 跑一次 `xcode` 并确认生成物与库内逐字节一致 → 加表 → `xcode` 生成 → **再跑一次比对逐字节一致** + `BindColumn` 集合不漂移；`DesignGuideline.Biz.cs` 的 `Valid` 校验 `Code` 非空/kebab、`Category`/`Status`/`Source` 在词表内、`UpdatedAt` 维护（参照 `DesignScreen.Biz.cs`）。

### §G2 默认规范目录（`GuidelineGenerator` 输出恰 14 条；codes/分类为契约，文案由实现方按约束撰写）

| code            | 分类       | 标题               | 规则要点（每条 3–6 个规则，≥1 个 MUST）                                                                                                                                        | 引用令牌（存在才保留）                                                                              |
| --------------- | ---------- | ------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------- |
| `layout-grid`   | layout     | 栅格与页面边距     | 页面水平边距取 `space.*` 之一（随密度，见 §G3）；12 列栅格、列间距 `space.4`；断点只用 `breakpoint.2–5`；内容最大宽取 `breakpoint.4`（营销类）                                 | `space.4/5/6/8`、`breakpoint.2..5`                                                                  |
| `page-patterns` | page       | 页面模式           | 控制台：列表/详情/表单/仪表盘四类结构与区块顺序；营销：Hero/特性/定价/页脚；通用：标题区→主体→操作区                                                                           | `component.card.*`、`component.table.*`                                                             |
| `navigation`    | navigation | 导航               | 一级入口数量上限、当前项必须有指示条（不只靠颜色）、层级不超过三层、面包屑；移动端底部标签栏上限                                                                               | `component.nav.indicator`、`component.nav.item.*`                                                   |
| `buttons`       | form       | 按钮层级           | 每屏至多 1 个主按钮；危险按钮仅用于不可逆操作且需二次确认；尺寸选用场景；不得只靠透明度表达禁用                                                                                | `component.button.primary.*`、`component.button.danger.*`、`component.button.{sm,md,lg}.min-height` |
| `forms`         | form       | 表单               | 标签在输入上方；必填标记；失焦校验、提交汇总；错误文案"原因+怎么改"；聚焦态用 `component.input.border-focus`                                                                   | `component.input.*`、`semantic.danger`                                                              |
| `feedback`      | feedback   | 反馈与提示         | 成功/警告/错误/信息各用对应语义色且**配图标与文字**；瞬时提示自动消失、错误需手动关闭；关键结果用页内反馈而非仅 toast                                                          | `semantic.success/warning/danger/info`                                                              |
| `states`        | state      | 空 / 加载 / 错误态 | 加载超过阈值用骨架；空态给下一步行动；错误态含原因与重试；禁止空白页                                                                                                           | `semantic.text-3`、`semantic.surface-2`                                                             |
| `data-display`  | data       | 表格与图表         | 表头与行 hover 用组件令牌；数字右对齐；图表系列色按 `chart.series-1..8` 顺序且不只靠颜色区分；（行业附加见 §G3）                                                               | `component.table.*`、`chart.series-1..8`、`font.mono`                                               |
| `content`       | content    | 文案               | 语气随用途（控制台平实、营销有感染力）；按钮用"动词+宾语"；术语全站一致；（行业附加见 §G3）                                                                                    | —                                                                                                   |
| `a11y`          | a11y       | 可达性             | 对比度满足 WCAG 2.2（正文 `4.5:1`、大字与非文本 `3:1`，由审计门禁强制）；焦点可见用 `component.focus.*`；交互目标不小于 `component.button.sm.min-height`；键盘可达；不只靠颜色 | `component.focus.outline-width/offset/outline-color`、`component.button.sm.min-height`              |
| `motion`        | motion     | 动效               | 时长只用 `duration.*`、缓动只用 `ease.*`；`prefers-reduced-motion` 下用 `duration.*-reduced`；进入用 `ease.decelerate`、退出用 `ease.accelerate`；禁无限循环动画抢注意力       | `duration.micro/base/macro/emphasized`、`duration.*-reduced`、`ease.*`                              |
| `elevation`     | layout     | 层级与阴影         | 卡片 `shadow.elevation-2`、浮层/菜单 `shadow.elevation-3`、对话框 `shadow.elevation-5`；层叠只用 `z-index.*`                                                                   | `shadow.elevation-2/3/5`、`z-index.*`                                                               |
| `density`       | layout     | 密度               | 本项目密度的使用场景；同一页面不混用多种密度；数据密集页用紧凑、内容型页面用舒适；间距只取 `space.*`                                                                           | `space.*`                                                                                           |
| `responsive`    | layout     | 响应式             | 控制台桌面优先、营销/移动优先；不出现横向滚动；触控场景的目标取 `component.button.lg.min-height`                                                                               | `breakpoint.*`、`component.button.lg.min-height`                                                    |

**撰写约束（`GuidelineGeneratorTests` 机器核对）**：规则/正文**不得出现数字+单位**（`px/rem/em/ms/s`）与 `#hex`；允许的数字仅限 WCAG 比值（`4.5:1`/`3:1`/`7:1`）与 §G2 里的列表序号；令牌一律写成反引号路径；每条规则 `id` 在条内唯一、kebab。

### §G3 对输入的敏感性（每一行都要有断言）

| 输入                         | 可观察差异                                                                                                     |
| ---------------------------- | -------------------------------------------------------------------------------------------------------------- |
| `kind=console/system`        | `page-patterns` 含四类控制台模式；`layout-grid` 不含"内容最大宽"规则；`navigation` 含侧栏规则                  |
| `kind=marketing/brand`       | `page-patterns` 含 Hero/特性/定价/页脚；`layout-grid` 含"内容最大宽取 `breakpoint.4`"；`navigation` 含顶栏规则 |
| `kind=product`（及未知回落） | `page-patterns` 通用模式；`responsive` 含"移动优先"                                                            |
| `density=compact`            | `layout-grid` 页边距引用 `space.5`；`forms` 垂直间距引用 `space.2`；`density` 条描述数据密集场景               |
| `density=comfortable`        | 页边距 `space.8`；`forms` 间距 `space.4`；`buttons` 默认尺寸建议 `lg`                                          |
| `density=default`            | 页边距 `space.6`                                                                                               |
| `industry=finance`           | `data-display` 增金额千分位/负数标记/等宽对齐规则；`content` 增货币与小数位规则                                |
| `industry=healthcare`        | `a11y` 增"临床状态不只靠颜色、字号不小于正文"；`feedback` 增"关键告警需确认"                                   |
| `industry=devtools`          | `data-display` 增"代码/ID 用 `font.mono`"；`content` 增"简洁、可复制"                                          |
| `industry=media`             | `content` 增阅读宽度与行高规则；`page-patterns` 增内容页                                                       |
| `industry=commerce`          | `content` 增价格与促销标注；`feedback` 增加购成功反馈                                                          |
| `industry=education`         | `feedback` 增鼓励性反馈；`content` 增易懂语言规则                                                              |

### §G4 渲染（`GuidelineRenderer`，brief / design-md / bundle / REST 共用**同一个**函数）

`Render(guideline, valueOf)`：规则与正文里的反引号令牌路径后面追加 `（<值>）`，`<值>` 来自当前导出主题的有效值（缺主题回落共享层）；令牌不存在 → 追加 `（令牌已不存在）` 且该路径进入 `brokenRefs`。brief 的 `guidelines` 章为紧凑形态（标题 + MUST 规则）；design-md 与 `GUIDELINES.md` 为完整形态；`guidelines.json` 为结构化形态（含每个令牌的当前值）。`GET guidelines` 每条附 `brokenRefs[]`（对当前默认主题计算）。

### §G5–§G7 REST / 导出·快照 / 工具（响应形状）

- REST 响应：规范对象 `{code,category,title,summary,body,rules:[{id,level,text}],tokenRefs[],brokenRefs[],appliesTo[],source,status,updatedAt}`；`generate` → `{created[],skipped[],skippedProtected[],overwritten[]}`；`PUT` 返回新对象；400/409 文案见 02-spec Error Handling。
- 导出：`brief` 章节名 `guidelines`（`DesignBriefBuilder` 的章节清单增一项，顺序 `… brand → guidelines → checklist`）；`design-md` 在"组件规格/品牌与资产"之后增「UX 规范」；`bundle` 增 `guidelines/GUIDELINES.md`、`guidelines/guidelines.json`；`agent-rules` 增一行。
- 快照 / diff：`BuildSpecs` 对**全部**规范（含 archived，带 `status`）产出 `guideline` 规格；`DiffOf` 先做 kind 过滤：`a.SchemaVersion<3 || b.SchemaVersion<3` → 两侧都剔除 `guideline` 规格并 `NotComparableKinds=["guideline"]`。
- 工具（只增）：`design_edit` 增属性 `code/title/summary/body/rules/tokens/category/status`（`action` 枚举增 `guideline`）；`design_lookup` 的 `kind` 枚举增 `guideline`；`design_context.sections` 枚举增 `guidelines`；写开关判定 `IsWrite` 增 `action=guideline`；checklist 派生项 `id="g:<code>:<ruleId>"`，`severity` 取 M1 的三档（`error/warning/info`）：MUST→error、SHOULD→warning、MAY→info。

### §G8 界面 DOM 契约（e2e 依赖）

导航按钮可访问名 `UX 规范`；根 `[data-guidelines]`；列表项 `[data-guideline-code]`；详情控件 `aria-label="规范标题" / "规范摘要" / "规范正文"`；规则行 `[data-rule-level]`（含级别下拉与文本框，按钮 `添加规则`）；令牌 chip `[data-token-chip]`，文本形如 `space.6 = 24px`（值取 `tokens/effective`）；按钮 `保存规范`、`重新生成默认规范`、`归档`/`恢复`；来源徽标 `[data-source="generated|manual"]`。

## Implementation Steps

**切片 A（风格轴 + 预设）**

1. **基线与开工复核**（00 末尾清单）；**录黄金基线（§A5，先于任何生成器改动）**并单独成一步写入 05。
2. `StyleAxes` + 请求字段 + 校验 + seed 后缀 + `meta.styleAxes`（TDD：AC5/AC6 先红）；跑黄金回归确认仍绿。
3. 逐轴落地（每轴：先写 AC3/AC4 的该轴用例 → 实现 → 黄金回归仍绿）：阴影 → 描边 → 中性色 → 字体（含 `font.display` 与 `SeedBrandCatalog` 登记）→ 圆角映射 → 强调色。
4. 预设扩充（5 个）+ 覆盖矩阵 + 同源/变量契约对 13 预设；M1/M2 对应测试增量更新（登记偏差）。
5. 前端「更多风格选项」+ `vocabulary` 守卫（含反向探针）+ `tune.ts`；A 片 e2e（AC11）+ 截图。→ **检查点 A**

**切片 B（UX 规范）**

6. `Model.xml` 加表 → `xcode` 生成 → 二次生成比对 → `EntityTypes` → 升级测试（AC12）。
7. `GuidelineGenerator` + §G2/§G3 + 数字守卫（TDD，含反向探针）。
8. `GuidelineRepository/Service/SeedGuidelines` + `GenerationService` 接线 + `generate` 响应增键 + `GenerateShapeTests` 更新（AC14）。
9. REST + `meta`（AC15，含"无 DELETE 路由"反射断言）。
10. `GuidelineRenderer` + 导出（brief/design-md/bundle/Manifest/agent-rules）（AC16）。
11. 快照 schema 3 + kind 级可比性 + `ReleaseBoard` 提示（TDD：先写"schema2↔3 不凭空新增"与"无规范项目重发幂等"）（AC17）。
12. 工具增量（AC18）；M1 测试增量更新。
13. 界面第 15 个 section + e2e（AC19/AC20）。→ **检查点 B**

**切片 C**

14. 视觉 QA（AC21）；**规范全文清单交用户审阅**（AC26，写入 05 并在回复里给路径）。
15. 文档 / 技能 / 版本 3.1.0；全部验证；写 05-evidence。→ **检查点 C**。**未获授权不提交 git、不推 tag、不停启用户宿主。**

## Test Plan

1. **黄金回归**（AC1/AC2）：每个轴落地后重跑；默认值逐字节不变。
2. **轴行为**（AC3–AC6）：轴 × 取值参数化；白名单内外比对；审计矩阵；校验与复现。
3. **预设 / 同源 / 变量契约**（AC7/AC8）：13 预设。
4. **规范**：表升级（旧库升级测试）、生成器（确定性/敏感性/数字守卫/引用存在）、服务（只补空/保护/覆盖）、REST（校验/并发/软删/无 DELETE）、渲染同源、导出、快照/diff/幂等、工具。
5. **前端 vitest**：`tune.ts` 扩展、`vocabulary.test.ts` 守卫（反向探针）、`glossary` 新词条；既有守卫仍绿。
6. **e2e（零 mock）**：A 块（editorial vs tech-crisp 可辨、`font.display` 入库）、B 块（规范读写回读、chip 值同源）；既有设计系统 e2e 回归。
7. **反向探针**（自查表 #24）：往规范模板塞 `16px`、把某预设的某轴改成默认、删掉一个规范引用的令牌、在 `vocabulary` 守卫里造三成员字面量——各自必须变红。

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
pnpm exec playwright test --config=playwright.config.ts e2e/plugins/design-system/design-system-style.spec.ts
pnpm exec playwright test --config=playwright.config.ts e2e/plugins/design-system   # 含既有巨型用例与 M2 新 spec，回归
```

### Other Checks

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-01-design-system-m3-style-guideline
git diff --stat -- Plugins/DesignSystem/Data/Model.xml Plugins/DesignSystem/Data/Entities ForgeSelf.Api Plugins/McpCenter
git diff -- Plugins/DesignSystem/Data/Model.xml   # 只应出现 DesignGuideline 表新增
```

## 风险与对策

| 风险                                          | 对策                                                                        |
| --------------------------------------------- | --------------------------------------------------------------------------- |
| 默认轴行为被改动（存量项目漂移）              | §A5 黄金基线在改代码前录制；每轴落地后重跑；`GeneratorSeed` 也进哈希        |
| 新阴影/圆角组合在某主题触发审计 critical      | AC4 矩阵（7 轴 × 取值 × 3 基础请求 × 4 主题）；公式系数可调但取值名不变     |
| `font.display` 破坏"只有 sans/mono"的隐含假设 | 开工探针；`SeedBrandCatalog`/brief/导出对缺失与存在都有用例                 |
| xcode 生成物漂移或手改                        | 先对未改动 Model.xml 跑一次比对；生成物不手改；二次生成逐字节一致           |
| 快照 schema 3 误报新增 / 破坏已发布版本幂等   | AC17：kind 级可比性 + "无规范项目 hash 不变"+"同版本重发幂等"三条先写后实现 |
| 规范文案质量                                  | 实现方初稿 → 用户闸门2 审阅（AC26）；机器只验结构/数字守卫/引用存在         |
| 与 M1/M2 测试的耦合                           | 所有对 M1/M2 测试的改动只做增量并登记偏差；既有断言不删不弱化               |

## Plan 偏差记录

> 实现中发现 Plan 与仓库实际不符时，先在此记录偏差，再修正 Plan。

| 时间     | 偏差点 | 原 Plan | 修正后 |
| -------- | ------ | ------- | ------ |
| （待填） |        |         |        |
