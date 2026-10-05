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
| 2026-10-03 01:16 | §A5 录制器形态 | `[Fact(Skip="…")]`，重录时手工取消 Skip | 改为 **`DS_RECORD_GOLDEN=1` 环境变量开关**（默认路径只核对"基线条目数 == 用例层数"）。理由：每次重录都要改测试代码容易留下"改了忘了还原"的口子；开关式录制器无需编辑文件，也不会多出被 `Skip` 的用例（06 V6 要求"无被 Skip 的用例，录制器除外"仍成立）。产物同两份：`.temp/ds-m3/golden.txt`（逐层原文）+ `golden.cs.txt`（可直接贴的字典片段） |
| 2026-10-03 01:17 | §A10 种子后缀键名 | 用轴名（`;shadow=crisp;border=bold;…`） | 改**两字符短键** `;sh=crisp;bo=bold;ne=pure;fo=editorial;ra=round;ac=analogous;st=1.4`。**背景**：七轴同时非默认时按轴名的后缀长 105 字符，加上 16 位种子 = 121，超出 `DesignToken.GeneratorSeed` 的 100 列宽，而**改既有列属 04-task Forbidden**。**准则**：取值名一律保持契约原词（`crisp/layered/editorial…`），轴序不变，完整轴名与中文说法进 `Notes`；新增守卫用例 `AC6_种子后缀顺序按轴序且总长不超列宽` 钉住"任意组合下 seed ≤ 100" |
| 2026-10-03 01:19 | Files To Change 漏列 `Services/PreviewCssService.cs` | 未列 | **必须改**：`PreviewCssInput` 是 `GenerationRequest` 的平铺镜像，展厅「微调」（FR6）经它传轴。不加这 7 个字段 = 界面能选轴但预览 CSS 不变，AC8「preview-css 与落库导出对 13 预设逐字一致」当场不成立。已加 7 字段 + `ToRequest()` 透传 |
| 2026-10-03 01:19 | Files To Change 漏列 `Services/PresetRecommender.cs` | 未列 | **必须改两处**：① `Copy()` 手写逐字段拷贝，不加 7 个轴字段就会在"预设 → 推荐 → 深链"链路上静默丢轴（新增守卫用例逐字段行为核对）；② `limit = Math.Clamp(limit, 1, 8)` 的 8 是 M1 时代的目录数，目录 13 个后会**永远推不出新 5 件衣服**，改为跟随 `StylePresets.All.Count` |
| 2026-10-03 01:20 | `TypeOptions` 字族默认值写法 | 在记录体内放 `const DefaultSans/DefaultMono` 供参数默认值引用 | 编译不过（CS0103：主构造函数参数看不到记录体内的常量）。抽出为 **`FontStacks`** 静态类（`Sans`/`Mono` 两个 const），`modern` 搭配与 `TypeOptions` 默认值都引用它——"默认 = 现状"从此有一处可核对的字面量，而不是两处各抄一遍 |
| 2026-10-03 01:46 | §P 预设 `flat-minimal` 彩度 0.05 | chroma=0.05 | **0.05 → 0.08**（§P/§D 允许 ±0.03 内的数值微调，取值名与性格词不动）。**实测背景**：0.05 时生成→审计出 1 条 critical——`component.badge.foreground`（semantic.success）对它自己的 `badge.tint` 底只有 **3.72:1**，达不到 TextNormal 的 4.5:1；极低彩度的绿太浅，垫 15% 自己后与白底几乎分不开。由 `StyleAxisAuditTests.AC7_十三个预设_生成落库后审计零critical` 抓到（Verified） |
| 2026-10-03 01:55 | M1/M2 测试的「8 个预设」断言 | 03 只写"增量更新并登记" | 逐条登记：`StylePresetsTests`（目录数 8→13）、`PreviewCssTests`（AC1/AC2 标题"八预设"→"全预设"，计数 8→13；循环本就遍历 `StylePresets.All`，写死数字只会遮住"新预设没被扫到"）、`MannequinVariableContractTests`（8→13，AC8 变量契约对 13 预设全过）、`DesignAgentToolTests`（`design_presets` 出参 8→13）、`PresetRecommenderTests`（`limit_钳制到1到8`→`limit_钳制到1到目录数`，99 → 13 条）。既有断言零删除、零弱化 |
| 2026-10-03 02:08 | Files To Change 漏列 `web/src/showroom/outfits.ts` | 未列（03 前端只列了 `TunePanel.vue`/`StartMode.vue`/`tune.ts`/`nav.ts`/`api.ts`/`glossary.ts`/`vocabulary.test.ts`） | **必须改**：`toPreviewInput()` 把衣服 `request` 拍平成 `PreviewCssInput` 时逐字段手写。不加这 7 个字段，衣柜里选了 `editorial-serif`/`tech-crisp`，**舞台拿到的仍是去掉轴的请求**——AC11「两件衣服标题字族与卡片阴影肉眼不同」当场不成立。已补 7 字段透传 |
| 2026-10-03 02:08 | Files To Change 漏列 `web/src/start/wizard.ts` 与 `web/src/showroom/tune.test.ts` | 未列 | `Wizard` 需要 ①`axisFields`（由界面从 `meta.styleAxes` 注入，状态机自己不列轴名）②`setAxis(field, value)`，否则向导第③步无法把轴写回 `request`；`tune.test.ts` 因 `TuneState` 加了 `axes` 必须同步（并补 7 条轴的判据用例）。两处都属 04-task Allowed 的"界面/测试增量"，此处显式登记 |
| 2026-10-03 02:18 | Files To Change 漏列 `Services/QuickCreateService.cs` | 未列 | **必须改**（缺陷驱动）：`ApplyOverrides` 手写逐字段透传，M3 的 7 个轴字段一个都没在里面 → 界面把 `shadowStyle=flat` 发到 `quick-create`，服务把它丢掉，创建出来的项目仍是默认软阴影。**由 e2e S2 实测抓到**（断言"非默认阴影档必须改变导出 CSS 的阴影形状"红：实得 `0px 3px 17.4px -1.5px`）。补 7 行透传 + 两条守卫用例（种子后缀逐项核对 + 反射扫"可空字段是否都在 ApplyOverrides 里出现"，防 M4 再犯） |
| 2026-10-03 02:26 | Files To Change 未列 `Agent/DesignToolIndex.cs` / `Agent/DesignTools.cs` / `Agent/DesignToolBase.cs` 的**轴**改动 | 03 只在 T-B 步骤 12 提到"工具增量（guideline）" | AC9/FR5 要求 `design_create`/`design_edit(regenerate)` 接受并带出 7 个轴字段。三处必改：① `DesignToolIndex` 的 create/edit schema **由 `StyleAxes.SchemaProperties()` 生成轴属性**（不在 schema 里手抄 enum——那是第三份词表，自查表 #27），`design_presets` 的 `limit.maximum` 与摘要里的"8 个"改为跟随 `StylePresets.All.Count`（M1 写死 8，目录 13 个后 agent 永远只能拿 8 件）；② `DesignTools` 的两个 `new GenerationRequest{…}` 构造点各补 7 个 `GetStr/GetDouble`，两处 `RequestDto` 补 7 个出参；③ `GetDouble` 原为 `DesignCreateTool` 私有静态，`Regenerate` 用不到 → 上移到 `DesignToolBase`（与既有 `GetStr/GetBool/GetInt/GetStrArray` 同类同处）|
| 2026-10-03 02:08 | 「更多风格选项」控件不新建组件 | 03 未规定 | 控件在 `TunePanel.vue` 与 `StartMode.vue` **各写一段模板**，没有抽 `StyleAxesFields.vue`。理由：04-task Allowed 只允许新增 `sections/Guidelines.vue` 一个前端文件，不该为省 20 行模板去扩面；**逻辑与词表仍是单点**（取值一律来自 `meta.styleAxes`，`axisFields/axesFromPreset/setAxis` 只在 `tune.ts`/`wizard.ts` 各一处），`vocabulary.test.ts` 的反向探针已证明"抄词表会红" |
| 2026-10-03 02:44 | §AC12 旧库升级测试形态 | 「先用**不含该表的实体集**建库并写入项目/令牌，再 `EnsureCreated` → 新表存在且旧行不变」 | 该形态**在本项目测试基建里不可稳定复现**：XCode 的按需建表只在实体 Meta 首次初始化时决策，同进程里前面的 DesignSystem 测试类已把 13 个实体的 Meta 预热到别的库文件；实测（`--filter` 同跑 `GenerateShapeTests + GuidelineUpgradeTests`）报 `no such table: DesignProject`，把 `DAL.Tables` 置 null 也不会重新建表 → 跑序变了就红。**处置**：① 该形态在"本类是进程内第一个 XCode 类"的条件下**单独 Verified 一次**（EnsureCreated 补建新表、旧令牌行逐行不变、规范为空），事实记入 05；② 常态跑序改由 `GuidelineUpgradeTests` 三条**跑序无关**判据承担同等风险：13 张表全在表清单里 / 同 (ProjectId,Code) 第二条 `Save()` 必被拒（且换 code 必写得进，排除"别的原因"）/ 重复 `EnsureCreated()` 不动旧行且回填零条数 |
| 2026-10-03 03:16 | 规范取值口径统一（超出 04-task 字面） | §G4 只要求"渲染端共用一个取值函数"；REST 出参未列 `tokenValues` | 实现过程撞出两件事：① `ExportService.ValueOf(snapshot)` 与控制器自写的 graph 解析是**两份实现**，`design-md@compact` 因此在规范章里满屏「令牌已不存在」（AC16 用例抓到，Verified）→ 定死为**规范章一律走项目默认色彩主题（参考主题）**，导出/REST/界面共用 `ExportService.ValueOf`；② 界面 chip 若自己再查 `tokens/effective` 就是第三份真相 → REST 出参加 `tokenValues`（后端同一个取值函数算的）+ `valueTheme`（写明取值用的是哪个主题），`Guidelines.vue` 只读这两项。新增 `AC15_引用的令牌全部真实存在…` 与 `AC16_compact主题不抛_且不出现假断链` 两条钉住；`meta`/DTO 键位增量已同步 `api.ts` |
| 2026-10-03 03:28 | 步骤 11 出参落点 | 「`DesignMapper` 输出 `notComparableKinds`」 | diff 出参不在 `DesignMapper`（它只做实体→DTO），而是在 `DesignSystemController.DiffReleases` 里就地匿名对象组装 → 字段加在控制器（前端 `api.ts` 的 `ReleaseDiff` 同步）。同类：步骤 10 计划写的 `Services/AgentRulesBuilder.cs` 仓库里不存在，agent-rules 文本由 `DesignBriefBuilder.BuildAgentRules` 产出 |
| 2026-10-03 03:29 | 快照/规范服务的装配方式 | 未规定 | `ExportService.Snapshot` 用**末位可选参数** `Guidelines`、`ReleaseService` ctor 用**末位可选参数** `guidelineRepository`：两者都有 6~10 处既有构造点（含 M1/M2 测试），加必填参数会把"规范"变成所有构造点的隐式契约；未注入时语义明确＝"这一层没接上"（空清单 → 不开章、不写文件、不列 Manifest），不是空壳假交付 |
| 2026-10-03 03:41 | 步骤 12 工具入参的一处自决保护 | FR13 只列 `rules[]/tokens[]` | `design_edit action=guideline` 把 **`tokens: []` / `rules: []` 一律解释为"这次不改"**（转 null），不解释为"清成空"：**否则 agent 只想改标题就会把引用清单清空**（`GuidelineRepository.Upsert` 的既有语义是 null=不改、空数组=清空，工具层不收 destructive 默认）。用例 `AC18_edit_guideline_空tokens与rules不清空既有内容` 钉住；REST/界面仍按 null=不改、空数组=清空的显式语义 |
| 2026-10-03 04:15 | M1/M2 e2e 入口清单增量 | 「既有 spec 不改」 | `design-system.spec.ts` 的 `navLabels` 是 14 项写死清单，第 15 个入口「UX 规范」出现后它必须同步（否则 AC19「原 14 个入口无新增红」不成立）。改动是**纯追加**（+1 项、注释标 M3 增量），零删除零弱化；另修我自己引入的两处 lint 真错：e2e 里把 BOM 写成字面量（`no-irregular-whitespace`，M1 注释明确要求写 `\uFEFF` 转义）与 slice A 遗留未用的 `cssVarOnStage` |
| 2026-10-03 04:31 | 步骤 13 界面两处真实缺陷（B 块 e2e 抓到） | 04-task 只写"加第 15 个 section「UX 规范」"，没规定面板结构与提示语位置 | ① 详情列原写 `v-if="draft && selected"`：**新建草稿没有库内行**（`selected` 恒 null）→ 点「新建规范」填完标识后右侧什么都不出现，草稿流程在界面上是死的；改为 `draft && (selected \|\| draft.isNew)`，并把依赖 `selected` 的"引用令牌区"（`<template v-if="selected">` + 新草稿一句说明）与"归档/恢复按钮"（`v-if="selected && …"` / `v-else-if="selected"`）按可用性收口。② `note/err` 两段结果反馈原在 `gl__detail` **面板内部**，而归档动作本身会让选中行从默认清单消失 → `draft=null` → 面板关闭 → 「已归档 forms」跟着消失＝点完零反馈；提到 section 级（`err` 加 `state !== 'error'` 避免与 `PanelState` 重复同一句话）。两处都在 Allowed 文件 `sections/Guidelines.vue` 内，不扩面；G3 补"新建后 `[aria-label=规范标题]` 必须可见"、G2 保留"归档后必须看见「已归档 forms」"，让这两个缺陷有常驻守卫。**另**：`pickProject` 判据由"必须点『选为工作项目』"改为"这一行成为工作项目"（`state.loadProjects()` 会自动选首条 → 按钮是禁用的「当前」，b1 因此超时 300s） |
| 2026-10-03 04:38 | 测试夹具装配与产品装配不同形（假红） | 03/04 未要求改 `DesignBriefTests` 夹具 | `dotnet test --filter ~DesignSystem` 实测红一条：`DesignBriefTests.E1_章节序_identity恒含`——`Sections` 少了 `guidelines`。根因不是章节序，而是**该夹具构造 `ExportService`/`GenerationService` 时没挂 `Guidelines`**（控制器与 `GenerateShapeTests`/`GuidelineRestTests` 都挂了）→ `GuidelineRows` 空 → 规范章按"空不开章"规则整章消失 → 那句 `Sections.Should().Equal(SectionOrder)` 把装配缺口读成"章节序坏了"。处置＝夹具补 `DesignGuideline.Meta.Cache.Expire=0` + `GuidelineService`，并同时挂到 `_generation.Guidelines` 与 `_export.Guidelines`（与产品装配同形）。这条判据因此**变强**：一个刚生成过的项目，brief 必须真的带出 `guidelines` 章。教训入 `design-system-verify` 自查表 #45（"REST/夹具漏挂 Guidelines"） |
| 2026-10-03 05:56 | 步骤 14 · e2e 读"舞台注入 CSS"的到位判据（批 C 两轮假读逼出来的） | Plan 只写"读注入 CSS 断言轴到产物"，未规定怎么算"读到位" | ① 第一版直接读 → `Received string: ""`（`<style>` 先渲染、`loadCss()` 后填）；改成"长度>100" 仍被第二轮假读击穿——换装瞬间 `Stage.vue` 把**新衣服 id + 旧 css 文本**一起交给 `OutfitScope`（`Showroom.vue:loadCss()` 取数期不清空 `css`，Vue 原地复用同一个 div），DOM 上是"挂着新 id 的上一件皮肤"。② 现判据＝**注入文本的声明行逐条等于这件衣服自己的权威交付 CSS**：预设走 `presets/recommend{limit:999}` 拿 request → `generate/preview-css{…request, theme, density:'default'}`（与 `outfits.ts:toPreviewInput` 同一份字段表），项目衣服走 `export?format=css&theme=`。V3 `stageDecls` 的等式判据由此**推广到 S1/V1**（重复轮询实现合并），S1 实测 `editorial-serif 311 变量 / tech-crisp 310 变量`与交付逐条相等。③ 产品侧根因（`data-outfit` 与正文在取数窗口内不一致）**不改**：修复要动 `showroom/Showroom.vue`+`showroom/Stage.vue`，两文件不在本任务 Allowed 名单 → 记 TODO(P2) 交用户拍板 |
| 2026-10-03 16:45 | 步骤 15 收口后又新增了两个测试资产，其中一个超出 04-task 的"新增文件"枚举 | 04-task Allowed 只列了 `web/src/**` 里**新增 `sections/Guidelines.vue`** 与 `e2e/.../design-system-style.spec.ts`（新增） | ① `design-system-style.spec.ts` 内新增 **V4**（Allowed 文件内的追加，无需例外）；② 新建 `Plugins/DesignSystem/web/src/sections/ReleaseBoard.test.ts`（**Allowed 名单未列的新文件**）。为什么必须加：AC17 的"ReleaseBoard 提示 UI 可见性"此前只能标 Unknown，而**造不出前提是核实过的事实**——`ReleaseService.Create` 永远写 `CurrentSchema`、`DesignMapper.cs:102-104` 的 `ReleaseDto` 只回 `snapshotAvailable` 不回文件路径，e2e 连"改旧快照文件"都没有入口；组件级实测是唯一能把它变成 Verified 的路，且**零新依赖**（`@vue/test-utils` + `jsdom` 在宿主 `ForgeSelf.Web/package.json:44,48` 早已在列，插件先例 `showroom/Showroom.test.ts`）。边界核对：`ReleaseBoard.vue` 本体在**这一轮**没再动（它自己的两处改动是 M3 实现期就发生的，见 18:24 那条）、Forbidden"改既有 14 个 sections/*.vue"未新增触碰、`package.json`/`.csproj`/宿主逐路径 diff 为空、`pnpm run build` 产物字节数与门禁② 及 zip 内一致（442,971 / 96,481）⇒ 加测试不改交付物。判据侧同时补了反例（换期望源必红，见 05「反向探针记录」） |
| 2026-10-03 18:20 | 完成度自审：`V10` 此前**只做了预注册三段里的一段**，判据比考试范围窄 | 06 的 V10 原文有三段（①抽 **3** 个令牌改值 × **brief/design-md/bundle** 三种交付物；②对生成器 14 条 × 3 kind 自跑数字/十六进制正则；③手写规范不受数字守卫约束）。我在 17:38 只落了①的"一个令牌 + 只验 design-md"，②其实早由 `AC13_数字守卫_全部生成文本零命中`（3 kind × 3 density × 7 industry，>300 条文本，WCAG 比值排除）+ `反向探针_数字守卫必须响` 覆盖但**没映射到 V10**，③完全没做 | ① 把单令牌用例替换为 `V10_抽三个令牌改值_三种交付物里现查的数跟着变_规范文本一字不动`：候选口径写死（被规范引用 + 共享层 px 型 + **当前交付里就以这个共享值出**，第三条专防我自己造假红——主题层有覆盖时改共享值本就不该变交付）；brief 只渲染「标题+摘要+全部 MUST」，故强制"三个里至少一个进得了紧凑章"并用 `briefChecked>0` 防空转（自查表 #56）。② 新增 `V10_手写规范正文带数字_不被误拒_原样保存与渲染`（`8px`/`#0f172a`/`24px` 写进正文必须存得下、`Source=manual`、design-md 与 bundle 的 `bodyRaw`/`textRaw` 逐字相等）。③ 三条反向探针全部实跑：bundle 期望换旧值→实红；brief 期望换旧值 + **临时在 `GuidelineService.Save` 装一条越界守卫**→一轮双红；还原后 12/12。零新依赖、零实现净改动（临时守卫当场逐字还原，全仓 grep 残留 0） |
| 2026-10-03 18:24 | 预注册判据的**字面**与实测不符：V13 写「`ReleaseBoard.vue` 的 diff 仅一处新增」 | 实测 `git diff --numstat` = **`+4 / −1`**、`-U0` = **2 个 hunk**：① 新增 `[data-diff-not-comparable]` 提示块（3 行）；② `kind` 图例行补 `/ guideline`（改 1 行） | 按实测形状登记，**不改判据迁就实现、也不把图例那行说成"不算一处"**。理由：图例那行是"guideline 成了可 diff 的 kind"的必然同步，与提示块同属一个功能，所以"仅一处新增"想挡的"顺手大改界面"并没有发生（整文件 diff 就是 4 行）；但字面确实为二，复验方（V13）按实测核对即可。同时把 16:45 那条里"`ReleaseBoard.vue` 本体一字未改"的歧义表述更正为"这一轮未再改动"——原句会被读成"M3 没碰过该文件"，与 git diff 矛盾 |
| 2026-10-04 12:35 | **Biz 直查重构超出 04-task Allowed 与 03「Files To Change」**：12 个 `.Biz.cs` + 6 个仓储/服务被改，47 处实体查询改走高级查询方法 | 原计划里没有"读路径单一出口"这一层改造；它由我上一版的**错误归因**引出——我把 G15 写成"读侧走实体缓存"，用户 09:31 连问「**关不掉的，不要试图关闭缓存**」「**为什么要关缓存**」 | 方案 A（关缓存）当场**回滚删除**（`DesignSystemCachePolicy.cs` / `WriteReadConsistencyTests.cs` 不存在，插件内 `CachePolicy\|DisableEntityCache` 引用实测 0，注：此处竖线为文案分隔非表格符）；改按用户指定口径（参照 `ForgeSelf.Api/Entities/AIModel.Biz.cs` 的 `Find(exp)` 直查）落地：13 个 `.Biz.cs` 各带 `QueryAll/QueryFirst/QueryCount` + 分页重载。实测：编译 0 错误 / 370 警告（与基线同数）、后端过滤集 529 == 发现数 529（12:46）、design-system e2e 串行 33 passed（12:54）。**归因更正的账另记 05/06/README G15 与 not-taken 024** |
| 2026-10-04 16:4x | 本会话**再超两处枚举**：① 新增测试资产 `BizDirectQueryGuardTests.cs`（04-task 的"新增文件"里没有）；② 改**仓库级脚本** `scripts/package-plugin.ps1`（Allowed 完全不含 `scripts/**`） | 04-task 只允许插件层 + 测试增量；`scripts/**` 属全站共用发布工具链 | 触发原因＝用户 23:43「发布本地插件，我将在 51888 选择本地目录更新插件」，而该脚本**此前从来没可能产出它文档里的产物**（`:126` 把 `.forgeself-plugin` 交给只认 `.zip` 的 `Compress-Archive`，原文红在 `logs/ds-package-bizdirect.log`，属既有缺陷非本批引入）。修法＝临时 `.zip` → 改名；终态按**包内容**验真（8 条目 / `plugin.json` 3.1.0 / 只带 `DesignSystem.dll` / 前端与门禁② 逐字节同尺寸）。守卫 ① 为常驻判据（10 用例，读数自打 `扫描 57 / 高级查询 47 / 违规 0`，真插一行违规探针实红后还原）。**档位代价如实**：改 `scripts/**` 按 §5.6 该跑中档（宿主全量 ~15min），本批未跑——用户明令不跑全量，已写进 06 Final Decision 限制①。同轮附带产出：README §3.5、agent-workflow.md 并发锁一条、TODO P3（包内混宿主 `.pdb`、脚本扩展名无常驻守卫） |
| 2026-10-04 18:0x | **再扩两类**：① 又改了两个仓库级脚本的**注释**（`scripts/verify-pilot-artifacts.ps1`、`scripts/install-git-hooks.ps1` 的 `.EXAMPLE` 用法行）；② 把规范文本写进 `AGENTS.md` §2.3 与 `docs/04-standards/{agent-workflow.md,packaging-upgrade-backup.md}`、第四个技能 `plugin-publish-verify`、`design-system-verify` 的发布命令示例、`Plugins/DesignSystem/README.md` §3.5，并**为老宿主重出一版签名宿主包**（`release-local -Sign -UpdateDir`） | 用户两条指令：输入11「本地使用脚本发布本地宿主增加签名，流水线默认不传不用签名，在技能说明这一点」、输入12「AGENTS.md 增加规范说明，执行脚本统一使用 pwsh，禁止使用 powershell 5」；这两条都不在 04-task 的 Allowed 名单里（属规范/发布面，不是插件功能面） | 全部为**文本/注释与发布产物**，零插件行为改动：脚本改动只在 `.EXAMPLE` 注释（字节级替换、BOM 前后 True、替换前断言命中=1）；新包按**包内容**验真（806 条目 / `versions/current=2.7.2.0` / 包内 `plugins/DesignSystem/plugin.json=3.1.0` / 从包里抽出的两个 exe `FileVersion=2.7.2.0` 且 `Get-AuthenticodeSignature=Valid` / SHA256 三处逐字一致）。**同轮两件按实态追记**：① 修正 05 上一版那句"乱码 notes 被打进 zip 内部"——实测两版包内 `.md` 条目数都是 **0**，乱码从未进入交付包；② 修好真源 `packaging-upgrade-backup.md` 里一处**HEAD 就存在**的表格行损坏（行内换行 + `0x0B` 控制符，成因＝历史上用 PS 双引号字符串打补丁，反引号被当转义符），并全量扫本次改动路径的控制字符 → 0 残留。**档位**：改 `scripts/**` 按 §5.6 属中档触发，本轮只复跑定向守卫 `--filter ~RepositoryScriptTests` → **失败 0 / 通过 20 / 总计 20**，宿主全量仍未跑（用户明令） |
