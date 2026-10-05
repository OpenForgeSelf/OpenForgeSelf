# 036 - 设计系统（design-system 插件）

> 插件形态：仓库根 `Plugins/DesignSystem/`，运行时 id `design-system`，当前版本 **3.1.0**
> （2.0.0 = 库驱动重写；2.1.0 = 组件规格 + 变体矩阵 + 尺寸轴；2.2.0 = 资产/字体/页面三表补齐写入口与生成种子；2.3.0 = 版本快照与 diff 覆盖品牌三表和组件目录；2.4.0 = 组件规格进机器可读产物；2.5.0 = Element Plus 换肤接缝；2.6.x = 门禁盯手改之后/对比度全覆盖/顺序收口/尺度词表/导入回流前置；2.7.0 = DTCG 导入/回流；2.8.0 = **Agent 工具层**：8 个 `design_*` 工具 + REST 对等 + 写开关；3.0.0 = **展厅与向导**（M2，四模式外壳 + 模特穿衣服试穿 + 预览零写库同源）；3.1.0 = **风格轴与 UX 规范**（M3，7 个风格轴 + 预设 8→13 + 第 13 张表 `DesignGuideline` 与 14 条默认规范进全部交付物））。
> 自带界面（路由 `/design-system`，`plugin.json` 的 `frontend.route`），经宿主远程加载（`frontend.entry = web/dist/index.js` + 同目录 `style.css`）。
> 管理端点全部要求 `ApiKeyPolicy` 鉴权；数据落在插件自建库 `~/.forgeself/Plugins/design-system/DesignSystem.db`（ConnName=`DesignSystem`，13 张表）。

## 功能定位

给"本系统自己"当**设计语言底座**：把一个产品的设计系统落成**可持久化、可校验、可版本化、可标准交付**的库，而不是生成一张好看但没人能接手的设计稿。

覆盖的能力面：项目与多品牌层级、主题（mode 轴：色向 / 密度 / 品牌）、三层令牌（primitive → semantic → component + 别名图）、确定性生成引擎（oklch 色阶 + 对比度定向选 tone + 排版模块化 + 尺度/阴影/动效 + **7 个风格轴**）、WCAG 2.2 可达性审计与发布门禁、组件与变体矩阵、图标库（内置零许可证负担一套 + 用户可导入）、资产/页面/字体登记、**UX 规范（14 条确定性默认，可编辑、进快照与全部交付物）**、不可变版本快照与令牌级 diff、行业标准投影导出（格式清单由 `GET export/formats` 现报，界面不写死）。

## 为什么重写（v1 是玩具的具体证据）

| 面 | v1（≤1.2.1） | v2（2.0.0） |
|---|---|---|
| 数据 | 浏览器 `localStorage`，上限 10 条 | 后端 12 张 XCode 表，插件自建自举 |
| 生成 | 前端 `design/generate.ts` ≡ `presets.ts` 常量表 | 后端 `Oklch`/`ColorRampGenerator`/`SemanticResolver`/`TypographyGenerator`/`ScaleGenerators`，确定性（SHA256 seed + GeneratorVersion），同参数必得同结果 |
| 对比度 | `selfCheck` 文案自称 ≥4.5:1，**从未计算** | `ContrastMath` 实测 WCAG 2.2 比值；语义角色按"需要的对比度反查 tone"选定；填充/标签成对二次修正 |
| 主题 | 一套暗色常量 | mode 是**轴**不是分层：色向（light/dark/high-contrast）与密度（compact/comfortable）各自铺覆盖层，切档后同一令牌有效值真的不同（有测试钉） |
| 导出 | 前端拼字符串，`tokens.json` 非 DTCG 且 `$schema` 是编的 | 后端逐格式投影（**清单由 `GET export/formats` 现报**）：DTCG 2025.10 / CSS / Tailwind v4 `@theme` / SCSS / LESS / TS / Tokens Studio `$themes` / DESIGN.md / **Element Plus 换肤接缝** / shadcn registry / Stardust JSON + **逐表合法 data.sql** / 十类逻辑实体明细 / zip bundle |
| 版本 | 无 | `DesignRelease` 不可变快照（文件只增不改）+ SHA256 内容哈希 + 令牌级 diff + critical 未清即拒发 |
| 组件库 | `ComponentGallery.vue` 渲染写死的 SRE fixture | 读 `api.listComponents/listVariants`，样张样式一律取 `component.*` 令牌 |
| 界面预览 | 前端另算一套 CSS | 换肤容器复用后端 `export?format=css`（`:root` 收窄成 `.ds-skin` + 变量别名表）→ **预览与交付同源** |

## 数据模型（13 表）

`DesignProject`（含 `Kind`/`ParentProjectId` 支持多品牌派生，`SchemaVersion`/`GeneratorVersion`/`ProjectionVersion` 三元组）、`DesignTheme`（`ModeKind∈{color,density,brand}`）、`DesignToken`（**三层统一一张表**：Tier/Path/Type/Value/ValueJson/AliasPath/ThemeId + oklch 三分量拆列 + ContrastRatio/WcagLevel + Generator/Seed/Version + Lifecycle/ReplacedBy/Deprecated + Extensions JSON 袋；唯一键 `(ProjectId,ThemeId,Path)`）、`DesignShadowLayer`（复合阴影逐层展开，`(TokenId,Layer)` 唯一）、`DesignComponent`、`DesignComponentVariant`（`(ComponentId,VariantKey,State,ThemeId)` 唯一）、`DesignIcon`（`SvgBody` 真实路径数据 + Collection + License）、`DesignAsset`、`DesignScreen`、`DesignFontFace`、`DesignAudit`、`DesignRelease`、**`DesignGuideline`**（v3.1.0 新增，唯一键 `(ProjectId,Code)`；`Category/Title/Summary/Body/RulesJson/TokenRefsJson/AppliesToJson` + `Source(generated|manual)` + `Status(adopted|draft|archived)` + `GeneratorVersion/GeneratorSeed/SortOrder`；**正文与规则只存原文（含反引号令牌路径），数值一律不入库**）。

表结构由 `Plugins/DesignSystem/Data/Model.xml` 经 `xcode Model.xml` 生成（生成物不手改，业务码写进 `.Biz.cs`）；已验证二次生成**逐字节一致**且 `BindColumn` 无字段漂移。每张表都带 `Extensions` JSON 袋，长期演进不必每次迁表。

## 后端端点（`/api/design-system`，类级 `ApiKeyPolicy`）

| 组 | 端点 |
|---|---|
| 自描述 | `GET meta`（版本三元组 + tiers/tokenTypes/lifecycles + `capabilities`（v3.1.0 起含 `guidelines`）+ exportFormats + `styleAxes`（7 个风格轴的封闭取值表，v3.1.0）+ `guidelineCategories` / `guidelineLevels`（规范词表，前端不另抄一份）） |
| 项目/主题 | `GET/POST projects`、`GET/PUT projects/{id}`、`POST projects/{id}/archive`（软删）、`GET/POST projects/{id}/themes` |
| 令牌 | `GET projects/{id}/tokens`（分页原行）、`GET tokens/effective?theme=`（解析别名 + 真算 hex/对比度/判级 + description/valueJson/extensions）、`POST tokens`、`POST tokens/batch`（校验失败整批回滚）、`POST tokens/retire`、`POST tokens/{path}/shadow-layers` |
| 生成/审计 | `POST projects/{id}/generate?overwrite=`（除令牌外，还把 `component.*` 反推成组件目录 + 变体矩阵，并落**字体/页面清单/资产**三类种子）、`POST generate/preview`（不落库）、`POST projects/{id}/audit`（跑并落库）、`GET projects/{id}/audit` |
| 导出 | `GET projects/{id}/export?format=&theme=`（文件原文）、`GET export/formats`、`GET {id}/{entity}.json`（Stardust 十类逻辑实体明细，裸 `{entity,source,generated,total,data[]}`，`?theme=` 可切主题） |
| 版本 | `POST projects/{id}/releases`、`GET releases`、`GET releases/{id}[?format=dtcg]`、`GET releases/diff?from=&to=` |
| 目录 | `GET/POST components`、`GET/POST components/{code}/variants`、`GET icons`、`POST projects/{id}/icons`、`GET/POST projects/{id}/assets`、`GET/POST projects/{id}/screens`、`GET/POST projects/{id}/fonts` |
| Agent（v2.8.0） | `GET meta` 增 `agentTools`（8 工具名数组）；`GET agent/tools`（工具枚举，与 `list_tools` 同源）；`POST projects/quick-create`（`QuickCreateRequest`，`DryRun=true` 干跑不落库——**与工具 `apply=false` 同语义，REST 默认 `DryRun=false` 落库**）；`GET projects/{id}/brief`（md/json 说明书）；`POST projects/{id}/review`（审查）；`GET presets` / `POST presets/recommend`；`GET|PUT agent-access`（写开关） |
| 展厅（v3.0.0） | `POST generate/preview-css`（`PreviewCssRequest` → 内存构图产 CSS，**零写库**；入口数值域校验 → 400。供展厅"试穿"与并排对比取数，与落库 `export?format=css` 同源：去注释、规整空白后逐字相同） |
| UX 规范（v3.1.0） | `GET projects/{id}/guidelines[?status=&category=&theme=]`（默认**不含 archived**，每条带 `tokenRefs/tokenValues/brokenRefs/valueTheme/categoryLabel`）、`GET guidelines/{code}`、`PUT guidelines/{code}`（upsert；写入即 `Source=manual`；带 `expectUpdatedAt` 做乐观并发，不一致 409 不写；引用不存在的令牌 → 400 并逐条列出且零行写入）、`POST guidelines/generate?overwrite=`（只补空 + 保护手改，回 `created/skipped/skippedProtected/overwritten/total`）、`POST guidelines/{code}/archive`（**软删**）。本插件**没有任何 DELETE 端点**（恢复 = PUT 回来带 `status=adopted`） |

## Agent 工具层（v2.8.0 · 缺口 G15）

设计系统向 Agent（外部经 McpCenter 网关 `universal_tool`、内置 AIAgent 白名单 `ToolScopePluginIds` 含 `design-system`）暴露 **8 个 `design_*` 工具**，读 6 写 2：

| 工具 | Kind | 作用 |
|---|---|---|
| `design_guide` | read | 使用指南：版本/工具清单/**四条**工作流（`consume` / `create` / `maintain` / **`guideline`（v3.1.0 加）**）/可选项目与预设/写开关/发现提示 |
| `design_context` | read | 设计说明书（唯一真源）：身份/规则/颜色/排版/尺度/组件/品牌/**UX 规范（v3.1.0 `sections=["guidelines"]`）**/交付清单，md 或 json |
| `design_lookup` | read | 令牌分页/组件/导出/图标/**规范（v3.1.0 `kind=guideline`）**；`nearest` 按值反查最近令牌（六类：color/length/duration/shadow/font-family/font-weight） |
| `design_review` | read | 审查代码与设计系统一致性（15 条规则：硬编码色/魔法数字/未知令牌/已移除引用…）；`checklist` 交付清单 = 静态基线 + **v3.1.0 起把库里的规范规则派生成条目**（MUST→error / SHOULD→warning / MAY→info，id=`g:<code>:<ruleId>`）。**hardcoded 默认 warning，`strict:true` 升 error** |
| `design_audit` | read | 读可达性审计结论；`run=true` 重跑并落库（写动作，受写开关约束） |
| `design_presets` | read | `list` 预设全字段（v3.1.0 起 **13** 个）/ `recommend` 按 brief/industry/kind/tone/density 打分推荐 |
| `design_create` | **write** | 从预设+显式参数快速创建设计系统项目（令牌/组件/审计/**规范**）；`apply=false` 干跑零写库 |
| `design_edit` | **write** | `set_token` 写单令牌 / `regenerate` 重生成 / `publish` 发布（critical 未清拒）/ **`guideline` 增改一条规范（v3.1.0）** |

- **唯一真源**：`Agent/DesignToolIndex.cs`（8 Entry；`meta.agentTools` = `All.Select(Name)`；`list_tools` 枚举同一张表）。
- **v3.1.0 增量仍是"加参数不是加工具"**：`design_lookup kind=guideline`、`design_edit action=guideline`、`design_context sections=["guidelines"]`——工具数保持 8，发现面（`agentTools`/`GET agent/tools`）不变形；参数枚举与长度上限由后端词表（`GuidelineCategories.SchemaProperties()` / `StyleAxes.SchemaProperties()`）生成，不在 C# 之外再抄一份。
- **封套**：工具结果 = `{success, data}`（camelCase 键）；`list_tools` 例外（直接是数据，无封套）。
- **写开关 `AgentAccess`**（fail-closed）：`{数据根}/plugins/design-system/agent-access.json`，默认 fail-open，文件损坏→只读；PUT 立即生效、重启保持（现读文件不缓存）。
- **契约与消费侧**：出参键、常见坑、REST 对等表见 `.agents/skills/design-system-consume/SKILL.md`；判据/偏差/证据见 `docs/ai/pilot/2026-10-01-design-system-m1-agent-tools/`。
- **"外部客户端经网关调 design_*"这句话是有常驻判据的**：插件层 e2e 有一条用例真走 `GET /api/mcp-center/config` 取被测实例自己的网关地址 → `tools/list`（对外只有 `universal_tool`）→ `list_tools` 枚举到的 design_* **必须等于** `meta.agentTools` → 经网关真调到 `design_guide`；并自带两条反向腿（未知工具名必须报错、不能绕过 `universal_tool` 直呼 `design_guide`）。以前这一跳只有宿主启动日志可看，现在每次 e2e 都会响。

## 展厅与向导（v3.0.0 · M2）

插件界面从"只有专业工作台"扩成**四模式外壳**（开始 / 展厅 / 工作台 / 交付与接入；工作台 14 个 section 原样不动）：

- **开始（向导）**：场景 → 预设 → 风格微调 → 命名四步，提交真落库（令牌 >100、审计无 critical）；失败保留输入并展示后端原文，创建期间单飞防重复，陈旧的预设推荐响应不覆盖新状态。
- **展厅（试穿）**：衣柜 = 13 个风格预设（v3.1.0 起，原 8 个 + 5 个轴驱动预设）+ 自己的项目；舞台上一个模特页被"穿上"任一件衣服。五类场景共 9 页模特：后台·中台 5 页（仪表盘/列表/表单/详情/设置）、状态板 1 页、工具·工作台 1 页、官网·落地页 1 页、移动端 H5 1 页。模特页只认 `--ds-*`（零字面量，有守卫），`OutfitScope` 把**后端文本投影**收窄到本件衣服上 → **画布上看到的 == 导出交付的**（e2e 断言画布 computed 底色 == 后端 `semantic.surface-bg`）。设备三档 1280/820/390 只表达"这段界面在多大屏上"；移动端场景强制手机框。支持两件并排对比（各帧作用域独立、互不污染，差异条列各帧字面值）。
  **预览取景（v3.1.0 输入22）**：舞台控制条有「视图」三档——**适应**（默认，整页按画布可用宽等比缩放 `k=min(1,可用宽/稿宽)`，永远不放大）、**1:1**（真实像素）、**最大化**（让开两侧栏，画布吃满整幅）；三档都可拖画布右下角改尺寸（缩放比随之重算），档位记在 `localStorage` 刷新不丢；控制条右端实时显示缩放比读数（`[data-stage-zoom]`），非适应档另给一行"画布外怎么到达"的提示。展厅栏宽单独放宽到 1872（不再与工作台共用 1240），故 1920 窗口下"适应"档就是 1:1 全幅。
- **交付与接入**：Agent 网关地址（页面**不含真实令牌**，只显示占位片段与含掩码都不渲染）、8 个 `design_*` 工具清单（== `GET agent/tools` == `meta.agentTools`）、写开关 PUT 往返、`brief`/`agent-rules` 原文（== REST 导出）、试审查（与 `POST review` 同源；>200KB 客户端拦截、不发请求）。
- **术语词典**：默认大白话、可切专业并向持久化开关写入。
- **深链与可达性**：`#/showroom/<page>?outfit=<id>&theme=<code>&device=<id>` 往返；衣柜/模式条方向键可选、键盘焦点有 `:focus-visible` 轮廓、页面横向溢出 ≤2px。
- **宿主侧配套（同批修复）**：插件自路由走 URL fragment，而宿主原先有两处会抹掉整段 fragment —— `authInit.consumeTokenFromHash()` 无条件 `replaceState(pathname+search)`（**无 token 也照抹**）、`main.ts` 的 `router.beforeEach` 只带 `fullPath`（把 `#...` 并进 path）。现改为"只摘 `token=` 一项、其余 fragment 原样保留；beforeEach 回填 `{path,query,hash}`"——token 仍被清除，深链不再失效。

判据/偏差/证据：`docs/ai/pilot/2026-10-01-design-system-m2-showroom-wizard/`。

## 风格轴与 UX 规范（v3.1.0 · M3）

M2 之前"换风格"只能换色：预设写死八套，阴影/描边/字族/圆角/强调色策略都不在参数面上。M3 把这些做成**轴**，并把 UX 规范落成库里的第 13 张表。

**A 片 · 7 个风格轴（默认值逐字节兼容）**

- 轴序与取值（真源 `Services/StyleAxes.cs`：`meta.styleAxes` 由它生成，界面「更多风格选项」与 agent JSON Schema 消费同一份，前端不另抄）：`shadowStyle`(soft|crisp|flat|layered) / `borderStrength`(regular|bold) / `neutralTemp`(brand|cool|warm|pure) / `fontPairing`(modern|system|humanist|editorial) / `radiusStyle`(soft|sharp|round|pill) / `accentStrategy`(complement|analogous|split|triadic|mono) 六个枚举轴 + 一个数值轴 `shadowStrength`(0–2，默认 1；越界**夹取并写 Notes**，滑杆多走一格不该报错)。每个轴**首项 = 默认**，默认值同时是 const 供生成参数默认值引用。
- **兼容性是机器判据不是口头承诺**：录制器（`DS_RECORD_GOLDEN=1`）把每个"生成输入 → 各主题各层令牌文本"的 SHA-256 钉成基线（50 条），任何改动只要让默认档的产物变一个字节就红。轴只在显式传非默认值时才起作用。
- 预设 **8 → 13**：新增 `editorial-serif` / `flat-minimal` / `warm-craft` / `tech-crisp` / `kids-playful`（各带自己的轴组合）。展厅微调面板按 `meta.styleAxes` 渲染轴 chip，选中值随"保存为新设计"进 `quick-create` 请求体 → 真的落到令牌上（e2e 在**后端单测同一条公式结果**上做双重门禁，防止"界面选了、后端没吃"）。
- 已知缺口（**记 TODO 未修，用户拍板本批不动**）：复合排版令牌导出时 `fontFamily` 被丢弃 → "标题在展厅里看起来是衬线"这条还不成立；e2e 如实拍下该事实而不是假装它成立。

**B 片 · UX 规范（`DesignGuideline` + 14 条确定性默认）**

- **一条规范 = 原文 + 规则 + 引用令牌**，正文与规则文本里只写反引号令牌路径，**数值一律不入库**：显示与导出的数字由 `GuidelineRenderer.Annotate` 现查。取值主题口径定死为**参考主题 = 项目默认色彩主题**（`ExportService.GuidelineView`），与导出请求主题无关 —— 规范引用的 `semantic.*` / `shadow.*` 只存在于主题层，用共享层或密度主题当视图会把整片引用误判成断链（M3 实测踩到，由 `GuidelineRestTests` / `GuidelineExportTests` 钉住）。出参用 `valueTheme` 交代实际取值主题，界面不许假装它是"当前主题"。
- 生成器 `GuidelineGenerator` 确定性产出 14 条（按钮 / 表单 / 布局栅格 / 状态 / 层级…），种子 `kind;industry;density` 写进每行 `GeneratorSeed`；**density 从产物反推**（代回 `ScaleGenerators.BaseUnit` 逐档试，读倍率为 1 的 `space.2`），不在服务里重列像素数字。
- **只补空、手改保护、归档=软删**：`Generate` 跳过已存在行，`Source=manual` 的行默认更不动（要覆盖必须显式 `overwrite=true` 并在响应里点名）；判重连 archived 一起看（否则"归档后重新生成"会撞唯一索引）；恢复 = PUT 回来带 `status=adopted`。播种挂在生成链路里（`SeedGuidelines` 返回"库里现存未归档条数"，不是"本次新增数"——第二次生成零新增，返回新增数会让界面上的规范凭空变 0）。
- **进全部交付物**（否则"库里有、产物没有"是另一半假能力）：DESIGN.md 增「## UX 规范」章；bundle 落 `guidelines/GUIDELINES.md` + `guidelines/guidelines.json`（**只有非空才写文件**，Manifest 也只列包里真有的路径）；`brief` 增 `guidelines` 章（紧凑形态只列 MUST，排在 `checklist` 前）；`agent-rules.md` 两行指路；`design_review mode=checklist` 把规则派生成条目（MUST→error / SHOULD→warning / MAY→info，id=`g:<code>:<ruleId>`）。规范原文进 `ContentHash` 与发布快照。
- 快照 schema **2 → 3**：`specs` 新增 `guideline` 类（11 字段，只存原文）；旧侧从未记过 → diff 报 `notComparableKinds=["guideline"]` 与"不可比"，**不许报成"新增 14 条"**。
- 工具面**仍是 8 个** `design_*`（不新增工具）：`design_lookup kind=guideline`（列表带 `total/archived/returned/truncated/omittedByQuery`，详情带 candidates）、`design_edit action=guideline`（`apply=false` 干跑回读 `before`；`tokens:[]`/`rules:[]` 一律当"这次不改"，否则 agent 只想改标题就会清空引用清单）、`design_context sections=["guidelines"]`。JSON Schema 的枚举与长度上限全部由 `GuidelineCategories.SchemaProperties()` 从后端词表生成。
- 界面第 15 个 section「UX 规范」：清单/筛选/关键词、编辑器（绑 `*Raw` 原文，括注只用于展示）、规则行增删、引用令牌 chip（值 == `tokens/effective`，不在前端再查一遍）、来源徽标（生成器产出 / 手改受保护）、页内二次确认的归档与恢复 —— 本插件没有删除能力，所以也没有"删除"按钮。

判据/偏差/证据：`docs/ai/pilot/2026-10-01-design-system-m3-style-guideline/`。

## 生成引擎的关键取舍

- **种子色逐位进色阶**：按种子明度就近锚到某一档，该档复现种子原值；"品牌色"不再是装饰性输入。
- **对比度定向选 tone**（Leonardo 思路）：语义角色不是固定"500 阶"，而是在色阶里挑满足该角色用途（正文 4.5 / 大字与非文本 3.0）的最贴近审美锚点的 tone；填充色与标签色成对校验，达不到就记录"未满足"而不是假装通过。
- **OKLCH 数学按 CSS Color 4 参考值钉死**（矩阵系数曾错一位导致纯灰带假彩度）；域外颜色用彩度收敛裁剪 + hue-cycling 补偿。
- **确定性与幂等**：SHA256 生成 seed 写进每行 `GeneratorSeed`，同参数重跑不产生新东西；`Generator=manual` 的行默认不被覆盖，跳过条数通过 `skippedProtected/conflicts` 回报到界面。
- **mode 是轴**：非 color 轴主题只铺尺度/品牌覆盖，语义色补丁被剔除；密度档（compact/comfortable/default）真的改变 `space./radius./duration.` 的有效值。

## 审计门禁口径（只报真账）

`contrast`（WCAG 2.2 实测，未达 = critical；禁用态按 1.4.3「非活动构件」豁免，只报读数）、`alias`（环/悬空/逆向 = critical）、`tier-violation`（component 直连 primitive = warning）、`focus`（缺 `component.focus.outline-*` = critical，对应 2.4.7/2.4.11）、`reduced-motion`（缺 `duration.*-reduced` = warning）、`orphan/unused`（info~warning）、`theme-empty`（色向主题无覆盖 = warning）。**critical 未清时发布端点直接 409**。

APCA 明确不作为门禁（见 `docs/07-decisions/not-taken-decisions.md` 009）；表里预留 `ApcaLc` 列但不参与判定。门禁宁少勿假：曾把"表头对卡片底"当 1.4.11 检查，产出 1.18:1 的假 critical——假警报一次就会让人无视整套门禁。

### 门禁必须盯"库里的现值"（v2.6.0 · 缺口 G14）

上面那些类查的共同前提是"值来自生成器"。但令牌值、组件尺寸、路径名谁都能改 —— 改完没人管，门禁就退化成生成器的自检。
四类新维度专门盯现值，**一律报 warning 不拦发布**（沿用上一条"宁少勿假"）：

| kind | 判据 | 为什么这么定 |
|---|---|---|
| `target-size` | 任何 `*.min-height` 解析后 ≥24px（WCAG 2.2 2.5.8） | 2.5.8 自带例外（内联元素、浏览器控制、本质性小目标），报 critical 会误伤 |
| `ramp-monotonic` | `space.` / `radius.` / `duration.` 按**声明序**必须严格递增 | 档序直接读 `ScaleGenerators.SpaceSteps/RadiusSteps/DurationSteps`，审计不另列一份；密度轴主题会覆盖尺度，故那些主题单独复查 |
| `naming` | path 必须形如 `^[a-z0-9][a-z0-9-]*(\.[a-z0-9][a-z0-9-]*)*$` | 写入端只做 trim + 小写、不校验形状；`space.4 x` 会一路长成 `--ds-space.4 x`，投影不报错但没人能用 |
| `lifecycle-ref` | 被别名引用的令牌若 `Lifecycle ∈ {deprecated, removed}` 就报出来 | 退役是软删：行还在、还能解析、不报任何错 —— 换肤或清理那天才会炸 |

两条新维度上线即抓到自己的错（都按先红后绿修掉）：正则把生成器自己的 `z-index.1` 判成不合形（段首连字符规则写错），尺度消息只报档名不报完整路径。
由此定下一条做法：**新加判据必须先断言"生成产物自己过"**，否则新维度第一次上线就是一片假红，之后没人再看它。
e2e 的钉法是现场把 `component.button.sm.min-height` 压到 18px → 跑审计必须出现 warning → 改回 28px → warning 必须消失。

**v2.6.1 补记**：`ramp-monotonic` 第一版只认 `dimension + px`，而 `duration.*` 是 Duration 类型带 `ms` →
时长族静默空跑（界面写着检三类、实际检两类）。补一条"故意把 `duration.macro` 压到 100ms 必须报"的用例才暴露 ——
由此定下做法：**新判据不能只断言"生成产物通过"，必须再造一个反例证明它真的会响**。

### 对比度这一维必须覆盖"库里的全部对"（v2.6.2）

`contrast` 原来查一张**写死的八对清单**（button/card/…）。清单只会覆盖当初想到的那几个：生成器真实产出的
`component.dialog.foreground` / `tooltip` / `select` 从来没被算过，用户自己新增的组件更是直接绕过门禁 ——
而界面上"可达性审计"一片绿，看起来像查过了。现在按**命名约定从库里推导**：

- 每个 `component.<ns>.foreground[-状态]` → 同命名空间找 `.background[-状态]`，逐级退回 `.background` → `.tint[-状态]` → `.tint`；
- 推不出来的显式对（placeholder 对输入底、nav/tabs 的 hover 底）单独列，且不与推导结果重复；
- 结论：多一个组件就多一道判定，不依赖有人记得回来加清单。

**禁用态按 WCAG 1.4.3 豁免，只报读数不拦发布**：1.4.3 原文豁免"非活动界面构件"。这套推导一上线就在自己的生成产物里报了
9 条 critical（按钮禁用态前景实测 3.41:1，出现在暗色/高对比主题）——那是假警报：拿豁免项拦发布，用户第一次看见
"发布被一个禁用按钮挡住"就开始绕过整套门禁。判级因此退回 1.4.11 的 **3.0** 兜底线（≥3.0 = info、<3.0 = warning），
`rule` 记 `wcag22-1.4.3-exempt`，消息写明依据。**豁免不是跳过**：读数必须留在库里，否则"没人报"和"没问题"长得一模一样。

**第三个洞（写这套用例时暴露）：审计行必须是"本轮全部结论"，不是增量覆盖**。`AuditRepository.Record` 原先只覆盖本轮产出的键，
某条对不再被判定（对象解析不出颜色、对不复存在）时，上一轮的 `Passed=true` 行留在库里，而发布门禁 `HasBlocking` 读的正是这些行 ——
等于用旧绿灯冒充今天查过。现在整批写入后清掉本轮没产出的旧行。
但"清掉"只解决了一半：**判不成的对象必须自己报一条"无法判定"**，否则它从审计里静默消失，界面上既不见红也不见黄，和"查过且没问题"一模一样。
所以 `contrast` 里新增 `rule=wcag22-1.4.3-unresolved`（声明为 color 却解析不出颜色 = warning，不拦发布；别名本身失败仍由 `alias` 报 critical，不重复记账）。
用例 `声明成color却解析不出颜色的令牌_必须自己报无法判定_也不许留旧通过行` 同时钉住这两半。

四条用例（190/190）：覆盖必须含 dialog/tooltip/select；用户新增的同色对必须 critical；禁用态退化到 1.0 必须升 warning 而不升 critical；
声明为 color 却解析不出颜色的令牌必须报 `unresolved` 且不得留着上一轮的通过行。

### 顺序也是一份声明，只能有一份真相（v2.6.4）

产物里的 `size = lg / md / sm`、`状态 = active、default、disabled、hover` 都是**字母序凑的**：值没错，但"从最小档往上、从默认态往后"这条设计师据以检查的序被打乱了。
更糟的是当时有**三处各排各的**：投影按字母序、`CatalogRepository.ListVariants` 按 `State` 字母序、前端 `ComponentGallery` 又 `.sort()` 一次。

- 唯一真源：`DesignSystemConstants.VariantAxes`（`size` = xs→xl、`role` = primary→link、`state` = default→hover→active→focus-visible→focus→disabled→pressed）。
  生成器认轴（`AxisOf`）、建矩阵用的后缀表（`StateSuffixes` 由它派生）、矩阵 `SortOrder`、三份投影的排序**全部读它**；表外的值退回字母序（没证据的顺序不编造）。
- 词表随 `GET /meta` 出给前端（`stateOrder` / `sizeOrder` / `roleOrder` / `scaleOrders` / `tiers` / `colorFamilies` / `auditKinds`），
  界面按它排 —— 前端不另列词表，也不自己 `.sort()`（那正是决策 010 灭过的"第二套实现"的顺序版）。
- 矩阵读序规则（`CatalogRepository.ListVariants`）：**先按变体分组（字母序），组内按落库 `SortOrder`**。
  只按 `SortOrder` 排不够 —— 用户自己补的格子 `SortOrder` 缺省 0，会把整张矩阵顶翻；用例 `矩阵读序按变体分组_组内按状态档位序_用户补的格子不许跳到最前` 钉住。
- 这条改动是 e2e 逼出来的：新断言"界面状态序 == DESIGN.md 状态序"第一次跑就红（界面 `default、disabled、hover、active` vs 产物 `default、hover、active、disabled`）。
  只改投影是不够的 —— 读路径和界面也必须回到同一张表。用例 `产物里的档位与状态必须按档位序_不是字母序凑的` 钉住三份产物同序 + 表外回退行为。

**v2.6.5 把同一把尺子量到尺度档位**：`ScaleGenerators` 新增 `SpaceOrder` / `RadiusOrder` / `DurationOrder`，`GET /meta` 出 `scaleOrders`，
`DensityScales` 删掉前端自己列的 `NAME_ORDER`（那份还含 `thin`/`pill` 等后端根本不产出的名字），`ShadowMotion` 的阴影列表改走同一个比较器。
两点值得记住：① 词表必须**覆盖库里真实存在的档位** —— `radius.pill` / `radius.full` 是绝对值档、不在倍率表里，只把倍率表当词表就会漏掉它们；
② `stepOf` 原先用 `parseInt` 取末段，于是 `radius.2xl` 的 "2" 被当成档位号，把 `2xl` 插到命名档中间（`pill`/`full` 反而排在它前面）——
现在只有**整段是数字**才算数值档。vitest 4 条用例钉住这两个陷阱，e2e 钉"界面上 `space.`/`radius.` 的顺序 == `meta.scaleOrders`"。

**v2.6.6 是这一族的收尾：界面不再存任何一份手抄词表，后端的表必须"被消费"**。清掉的三份镜像：
`derive.ts` 的 `TIER_ORDER`（改读 `meta.tiers`，顺带"全部层级"下拉的选项也从它来）、`colorFamilies()` 里的
`brand/accent/neutral/success/warning/danger/info`（改读 `meta.colorFamilies`）、`AuditBoard` 的 `KINDS`
（改读 `meta.auditKinds`，门禁说明清单与分组排序都读它）。两张新表不是"换个地方抄"，各自带了 producer：
`ColorFamilies.All` 就是 `DesignGenerator` 逐族产阶遍历的那张表（`RampFor` 缺规则 = 抛），用例反向核对"产出的族集合与顺序 == 表"；
`AuditKinds.All` 与 `AuditEngine` 真跑出来的 kind 双向核对（**表里有却造不出触发场景 = 空声明**、**产出了表外 = 界面筛不到**），
并用反射核对"const 与 `All` 一一对应"。补格子的 `state` 同时从自由文本改成下拉（词表 = `meta.stateOrder`，逃生口 = 显式选「自定义」）。
防复发：`web/src/design/vocabulary.test.ts` 扫界面源码，同一行出现 ≥3 个互不相同的已知成员字面量清单即红（已用临时镜像验过它真会红）；
e2e 步 8c 钉"色阶条带族序 / 层级下拉 / 门禁类别清单 / 状态下拉"四处顺序 == 后端四张表。

**v2.6.7 把"契约可见"做成"契约读得回真数"**（同族第 5 次收口）：
① `/meta` 出 `entities`（= `ExportService.StardustEntities`），「导出交付」页新增十类逻辑实体插座表——
每行是当前项目该实体的真实 url，「行数」是页面按当前主题**逐条请求** `GET api/design-system/{id}/{entity}.json` 拿回的 `total`
（实测 `design-color=159 … design-font-face=14`），换主题整表重读；只列链接不读 = 把后端契约抄到界面上当摆设。
② `/meta` 的 `stateOrder`/`sizeOrder`/`roleOrder` 三条并列字段合并成一份 **`variantAxes` 清单**（轴 → 档位序，顺序 = 表声明序）：
三条字段就是同一张表抄三遍，加第四条轴必漏一处；现在只改 `VariantAxes.Orders`。
③ 有了清单，「新建变体」改成**先选轴、再选档位**，`variantJson` 由界面按后端 canonical 口径拼好且只读，
词表外的轴/档位要显式选「自定义」，多轴组合走显式的「直接写 JSON」开关（前端不发明第二套序列化）。
④ **矩阵的分组序**也归词表管：`ListVariants` 原先按 `VariantKey`（JSON 文本字典序）分组，于是 `{"role":"danger"}` 排在
`{"role":"primary"}` 前 —— v2.6.4 只修了状态维，这轮补上变体维；老用例的判据同步从"字母序"**升级**为"词表序"。
⑤ `.ds-mono` 补 `text-transform: none`：`text-transform` 会继承，图标 code 曾被父级 `.ds-micro` 显示成 `DASHBOARD`（库里是小写），
用户照屏幕抄回接口就 404。

**v2.6.8 把"画布是哪一档"从肉眼看明暗收成状态**（同一件事的证据面与并发面各收一刀）：
① 状态里以前只有一段投影 CSS 文本，**没有"它属于哪个主题"这个字段**。而投影只在「看效果」页注入（外壳必须保持中性），
在别的页切档不会重取 —— 于是「界面选中的档」与「画布实际用的档」可以是两回事，同一操作步骤的截图两次运行一暗一亮就是这么来的。
现在 `skinTheme`/`skinApplied` 是状态，主题条按 `applied`/`pending`/`unloaded`/`unavailable` 四态说清差值
（e2e 当场抓到第三种：本次会话从没进过预览页时是「未取」而不是「待重取」，两种情况混成一句文案就是说谎）。
② 预览画布顶部角标显示档位 + `surface-bg`/`brand` 的**字面颜色值**：`cssVar` 原先是个零调用点、且正则把 `--` 前缀拼错的死函数，
修好后加 `resolveCssVar` 顺 `var(--x)` 链（语义层是别名、原语层才写字面色，链跟到底仍只读后端那一份文档）。
于是角标显示 `#211f25` 而不是 `var(--ds-color-neutral-950)`，e2e 得以把**角标值 == 后端 `semantic.surface-bg` == 画布渲染像素**三方逐位钉死。
③ 导出与快照的变体读从"逐组件查一次"（N+1）改一次批量（行序一致性有断言钉），导出页读回限流 ≤3：
二十几个并发只读请求实测会把宿主 SQLite 顶出 `database is locked`（500）。只读请求本就有退避重试兜着（所以界面看不出来），
但重试有上限且拖慢页面。收敛后**没有消除** 500（连跑三轮 e2e 的只读 500 是 2 / 0 / 3，落点每次不同）——
它降低的是争抢强度，不是并发模型；**宿主跨请求写锁仍是待拍板议题**（见 TODO）。
④ 新增后端门禁「每种声明的格式 × 库里每个主题都真能导出」：既有用例把全部格式只跑在浅色档上，
而暗色档才是别名链最密的地方 —— "格式都试过"不等于"每档都出得来"。

**v2.7.0 补上"进"的那半边：DTCG 导入/回流**（ROADMAP P1.20，工件链 `docs/ai/pilot/design-system-import/`）：
插件此前只能"出"（八种投影导出），Figma 侧改完令牌要人肉搬回库里 —— 这正是"不能回流的设计系统只算半个"的缺口。
① 两个端点：`POST projects/{id}/import/preview`（只算不写）+ `POST projects/{id}/import`（同一段解析，preview 与 import 共用一份 plan，不各算一遍）；
能力与上限由 `/meta` 声明（`importFormats=["dtcg"]` / `importLimits={maxEntries:5000,maxBytes:4MB}`），界面没声明就不亮入口。
② 解析器 `DtcgImporter` 是纯函数，三条纪律：**层级/类型只从证据推**（`$type` 沿树继承、与导出侧互逆；推不出就逐条拒并回原因；
库里已有路径的层级以库为准——按路径首段猜会把语义层 `chart.series-1` 降级成 primitive，其别名"逆向指向上层"触发整批图校验拒绝，实测踩过）；
**不搬家**（文件路径库里已有就写回原主题，只有新路径落到所选主题，否则 round-trip 会把共享 primitive 复制进主题层）；
**provenance 只进列不进 `$extensions`**（`Generator=imported` + `GeneratorSeed=sha256(上传字节)`，塞进逐令牌扩展第一次 round-trip 就不逐字一致了）。
③ 安全边界：默认 `overwrite=false`，手改（`Generator=manual`）与既有导入行受保护、冲突逐条列出；同一文件内路径重复、别名成环/悬空**整批不写**并回逐条诊断
（不许"好的进去了、坏的没人知道"）；上限在解析器里判，控制器与单测走同一条链。
④ 导入的新令牌进**同一套** WCAG 对比度门禁（用例导入一对低对比 component 前景/背景，审计当场抓出）——回流不豁免门禁。
⑤ 「导出交付」页导入回流块：选文件 → 预览（将写入/被拒/冲突清单，不落库）→ 确认写入 → 生效值可查；
e2e 钉三个判据：UI 写入后 `effective` 读回别名顺到字面值 `#123456`、导出→导入→导出 **55517 字节逐字一致**、成环样本拒写且诊断可读。

**v2.7.1 插件头部重排**（用户指令）：去掉顶部 BrandLogo（三角标识 + 项目名字标——项目上下文由右侧选择器承担），
「设计系统」标题与版本徽标（模型/生成器/投影，读 `/meta`）收进同一行、副标题独立一行；零引用的 `BrandLogo.vue` 移入 `.trash/`。

## 内置图标库 `forge`

40 枚本项目**原创绘制**（License=`Owned`，无第三方许可与归属负担），统一 24×24 网格、1.5px 描边、round 端点、`stroke="currentColor"`；每枚带中文名、检索标签与"什么时候用它"的用途约束。首植在插件 `Apply` 里幂等执行（已有则不重写），用户仍可导入自己的 Collection。

## 自带界面（四模式外壳 + 15 个库驱动 section）

外壳分四模式（v3.0.0）：**开始**（向导）/ **展厅**（试穿）/ **工作台**（下方 15 个 section）/ **交付与接入**（Agent 接入与文档原文）。工作台 15 个 section（v3.1.0 起第 15 个是「UX 规范」）：

项目与生成 / 令牌工作台 / 色彩实验室 / 排版标度 / 尺度与密度 / 阴影与动效 / 主题实验室 / 品牌资产 / 图标库 / 审计与门禁 / 导出交付 / 版本与对比 / **UX 规范** / 组件库 / 品牌展示页。导航入口按后端 `capabilities` 灰化（后端没声明的能力不假装可用）；空态分四层（未登录 401 / 后端错误 / 无项目 / 无令牌）。

## 品牌资产三表（资产 / 字体 / 页面清单）

这三项一直在 `meta.capabilities` 里声明着，但**只有 GET、也没有任何地方种数据** —— 界面就是一个永远显示"无…"的面板。
"声明了却拿不到"和假能力同罪，所以 v2.2.0 补齐三件：

- **生成即落种子**（`SeedBrandCatalog`，与组件种子同样整批一个事务）：
  字体 = 从 `font.sans` / `font.mono` 字体栈逐成员登记，许可证写明"字体栈成员（系统/浏览器提供），不随设计产物分发"；
  页面清单 = 按行业倾向给起手屏，描述里明确"生成器建议，不是产品事实"；
  资产 = logo + 母题两枚原创 SVG（`License=Owned`，只用 `currentColor`，颜色交给 `semantic.*`）。
- **写入口**：`POST projects/{id}/assets|screens|fonts`（同码即覆盖，幂等），界面「品牌资产」段三个面板可增可改，写完立即回读自证。
- **许可证与色值的硬约束放在界面上**：字体许可证留空直接拒绝提交（空许可证是合规雷）；
  SVG 里出现烤死的 `#xxx` 也拒绝提交——那样换肤对它无效，与设计系统的前提冲突。
- **交付物里也要有它**（否则"库里有、产物没有"是另一半假能力）：CSS 只对**登记了文件**的字体行出 `@font-face`，
  系统栈成员以注释列出族名/字重/许可证并写明"不随本产物分发"；bundle 落 `brand/<code>.svg`（补 `viewBox="0 0 24 24"` 外壳，
  品牌图形与图标库共用 24 网格）+ `brand/fonts.json` + `brand/screens.json`（**不转义非 ASCII**，中文许可证要能被人与 CI 直接读）；
  DESIGN.md 增「品牌与资产」段，三张表全空则不开这一节。

## 版本化覆盖什么（v2.3.0）

快照 schema 从 1 升到 **2**：除令牌外新增 `specs` 一节，形状 `kind + key + 字段表`，覆盖
`component`（组件目录）/ `variant`（变体格子）/ `asset`（图形资产）/ `screen`（起手屏）/ `font`（字体登记）。
两条规矩：**规格进哈希**（不然"只换了 logo"会被判为与上一版一致、同版本号幂等放行）；
**旧快照不许凭空补一节**（schema 1 文件没有 `specs` → 后端回 `specsComparable=false`，界面显示"不可比"而不是"新增 N 条"）。
内置图标库（`ProjectId=0`）不进快照：它随插件版本走，不是某个项目的一次发布内容。

**v3.1.0 升到 schema 3**：`specs` 再增 `guideline` 类（11 字段，只存规范原文与级别，不存括注后的数值——同一数字存两处就会漂）。同一条老规矩照样生效：规范改动**进哈希**（"只改了规范、令牌没动"必须能发出一版，否则同版本重发被幂等放行）；**旧快照不许凭空补一节**——对 schema≤2 的快照做 diff 时 `guideline` 类整体标不可比（出参 `notComparableKinds`，界面显示"不可比"并把原因说清），绝不报成"新增 14 条规范"。

种子的定位要说清楚：**建议值，不是产品事实**。重跑生成对这三张表**只补空不覆盖**——按自然键（资产 code / 页面 code / 字族+字重+样式）跳过已存在的行，所以用户换过的 logo、改过的起手屏、登记的可分发字体不会被静默写回（令牌侧靠 `Generator=manual` 保护，这里按同名键保护）。

## 组件规格：生成即落地

`generate` 除了写 `component.*` 令牌，还会把令牌**反推成组件目录**（`DesignComponent`）：

- 蓝本只声明"怎么从 `component.<code>.*` 推目录"，**令牌清单一律取本次真实生成的路径**——
  写死清单会让目录里出现库里没有的令牌，那就是又一个样子货。
- 这次没生成某组件的令牌就不建它的目录（宁缺不造空壳）；当前蓝本 10 项：按钮 / 卡片 / 输入框 / 徽标 / 导航 / 数据表 /
  **对话框 / 提示浮层 / 选项卡 / 选择器**（后四项 v2.1.0 补，覆盖参考物里我们有、原来没做的部分）。
- 每条目录都带 `a11yNotes`（可达性约束）与 `guidanceJson`（anatomy + 由令牌段推出的 variants）。
- **变体 × 状态矩阵由令牌反推**（`DeriveVariantCells`）：变体 = 路径第一段命名空间（primary/secondary/danger/item/header…），
  无命名空间的组件级令牌归 `base`；状态 = 叶子上的已知交互后缀（`-hover`/`-active`/`-focus-visible`/`-disabled`/`-pressed`），
  其余算 `default`。**凑不出令牌的组合不落格**，所以矩阵是令牌的投影，不是第二份要人维护的清单。
- **尺寸轴已落地**（v2.1.0）：`button/input.{sm,md,lg}.{padding-block,padding-inline,radius}` 别名到 `space.*`/`radius.*`，
  外加 `sm/md/lg.min-height`（28/34/42px，均不低于 WCAG 2.5.8 的 24px 目标尺寸下限）；
  focus 与 disabled 态也补了真令牌（`input.border-focus`、`button.<role>.background-disabled` 等），
  矩阵因此从"角色 × 状态"两层变成三轴。缺令牌的组合依旧如实不落格（如 `card` 只有 base/default 一格）。

### 规格必须出得来（v2.4.0 · 缺口 G12）

库里落成目录只算"存下了"；下游装组件规格时如果只拿到一串 `component.*` 路径，`a11yNotes`/`anatomy`/矩阵就还是**只在库里**的东西。三条口径：

- **registry**：条目键集 = 组件目录 ∪ 有 `component.*` 令牌的分组，`meta.inCatalog` 表明是哪一侧；
  `meta` 带 `anatomy` / `a11yNotes` / `states` / `variants[]`（每格 `axes` 轴值 + 该格令牌清单）/ `unresolvedTokenRefs`。
- **引用不许凭空列**：每条令牌引用都必须在同一份令牌工件里查得到——查得到的进 `registryDependencies`，
  查不到的**如实**进 `meta.unresolvedTokenRefs`。静默丢掉就是把"规格与令牌两份真相"藏进产物里。
- **DESIGN.md**：`## 组件` 下增 `### 组件规格`，逐蓝本列 `解剖 / 状态 / 变体轴（size = …）/ 令牌条数 / 可达性要求`；
  目录为空就不开这一节（空标题比没标题更像假交付）。
- **Stardust 逻辑实体（spec FR13 补齐）**：清单里每行的 `total` 与明细 `data[]` **同出 `ExportService.EntityRows` 一处**，
  于是"清单 12 行、明细空数组"在结构上不可能发生。此前 `CountFor` 是另一套计数：
  `design-component` 数的是组件层**令牌**条数（不是目录里的组件），`design-icon`/`design-screen`/`design-font-face` 直接写死 0；
  而且清单里的 url 指向的路由**根本不存在**。实测（真实宿主、e2e 逐实体比对）：
  `design-color=141 design-shadow=4 design-motion=13 design-type-role=17 design-spacing=10 design-radius=8 design-icon=40 design-component=10 design-screen=5 design-font-face=14`。
  注意 `design-icon` 计的是"内置 `forge` 集 + 项目自登记"（消费方拿得到什么），与版本快照"内置库不进快照"（某次发布改了什么）是两个问题。

## Element Plus 换肤接缝（`format=element-plus` · v2.5.0，收 spec U3）

宿主界面本身就是 Element Plus 写的，所以"为系统进行设计"的闭环必须能把设计系统落到 `--el-*` 上。产物是一份**接缝**（先引 `tokens.<theme>.css`，再引它）：

- 右侧**只允许** `var(--ds-…)` 或它的 `color-mix(in oklab, …)` 派生 —— 全文件零字面色值；
  每个引用都必须能在**同主题**的 `tokens.css` 里找到定义（e2e 在真实宿主里逐条比对），库里没有的档位如实列进文末"未映射 N 项"并指名缺哪条令牌。
- EP 的 `light-N` / `dark-2` 原义是"与白 / 与黑混合"；这里照它的比例，但把混合目标换成**本页底色 / 正文墨色**：
  写死 white/black 在深色主题下会把悬停态洗成灰白、把按下态压成死黑，换肤就成了半套。
- `--el-color-*-rgb` 由解析后的 hex 现算三元组（EP 拿它拼 `rgba()`），不另存一份颜色。
- `--el-component-size{,-small,-large}` 直接取 `component.button.{md,sm,lg}.min-height`（已过 WCAG 2.5.8 的 24px 下限），不再用 EP 写死的 32/24/40。
- **有意不映射**：`--el-color-white` / `--el-color-black`（EP 当固定前景用，映射到表面色会把文字压成同色；前景对比度由 `component.*.foreground` 与审计门禁负责）、
  `--el-index-*`（层级不是设计值）、`--el-transition-all/-fade*`（EP 由 duration + function 组合出的整串，已给原料）。
- 实测（真实宿主、e2e 打印）：**102 个 `--el-*` 变量、132 条令牌引用，全部可在 `tokens.css` 解析**；light 与 dark 两版**键集相同**（差别只在令牌解析到什么颜色）。
- **职责边界（实测，不是推断）**：e2e 在真实宿主里放一个 `.el-button--primary` 探针量了三件事 ——
  `--el-color-primary` `#F59E0B → #6d28d9`（宿主自带主题文件被我们盖过）、
  EP 组件层 `--el-button-bg-color` `#F59E0B → #6d28d9`（**接缝确实流到了 EP 用来上色的那条变量**）、
  而最终 `background-color` 仍是琥珀 `rgb(245,158,11)` —— 宿主存在直接钉按钮底色的规则，任何主题（含宿主自己那份）都盖不过它。
  所以这份产物的承诺是"把变量喂进 EP 组件层"，不是"覆盖宿主里所有硬写背景的选择器"；后者属宿主侧改动。

## 预览换肤的同源约束

预览容器 `.ds-skin` 的样式只有两个来源：后端 `export?format=css` 的产物（作用域从 `:root` 收窄到 `.ds-skin`）
+ 一张"外壳变量 → 令牌变量"的别名表。两条铁律：

1. 别名**只引用后端 CSS 里真定义的变量**（`definedVars` 扫描）；指向未定义变量的 `var()` 会让整条声明在
   computed-value 阶段失效，表现是"预览突然全透明"——这个坑是 e2e 真跑出来的。
2. 界面不做第二套设计值计算（v1 的玩具感正来自前端另写 generate/exporters）。

e2e 的判据因此是**逐位比对**：`.ds-skin` 的计算底色必须等于后端 `semantic.surface-bg` 的 hex→rgb，
且切主题时先确认界面主题码变了、再确认注入的 CSS 头部 `theme=` 变了。

## 验证入口（全部是仓内正规入口，无一次性脚本）

```bash
# 后端（12 表 + 生成 + 审计 + 导出 + 版本 + 图标 + 组件目录 + 品牌三表种子 + 十类逻辑实体明细）
# ⚠️ 必须带 verbose logger：quiet 模式在测试主机中途崩溃时会把"已跑条数"当总数并报"失败 0"（实测假绿）
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~DesignSystem" --logger "console;verbosity=normal"
#   核对：报告"测试总数" == `--list-tests` 发现数，不等就是事故而不是通过

# 插件前端：类型检查 / 单测（走宿主 vitest 入口，含样式类完整性守卫 classes.test.ts）/ 构建
cd Plugins/DesignSystem/web && pnpm run check && pnpm run test && pnpm run build

# 插件层 e2e（真实宿主 + 真实库，零 mock；三个 spec 文件：工作台 design-system.spec.ts / Agent design-system-agent.spec.ts / M2 展厅 design-system-showroom.spec.ts 的 A/B/C/D/E 片；每段都用 API 复核后端事实 + 截图读图）
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/design-system
#   视觉 QA 矩阵截图落 ForgeSelf.Web/screenshots/e2e/design-system/m2/qa-*.png（3 预设 × 5 场景 × 明/暗）
```

## 已知未做 / 风险

- **宿主 SQLite 并发锁会以 500 冒到用户**（缺口 G8，根因在宿主 DAL）：并发读写下实测出现过
  `code = Busy (5) / database is locked`，命中写路径（审计落库）与只读路径（项目查询、effective）。
  本插件侧止痛四件：宿主派生连接串补 `Busy Timeout=5000`、写路径整批一个事务 + 发布按项目串行化、
  界面只读请求退避重试（**写请求不重试**）、e2e 客户端同样只重试 Busy。
  因此"e2e 绿"是**带重试的绿**，不是"零锁冲突的绿"；治理方案需人拍板（见 TODO P1）。
- **整包导出（`bundle`）经 dev 代理取回会 `Failed to fetch`**，根因未定位（已排除"太慢被掐断"：后端实测构建 605ms）。
  体积数字改由后端用例给（`ExportProjectionTests` 打印 `[T302]`）；**用户在生产形态下的真实下载路径仍未核验**（TODO P2）。
- ~~组件变体矩阵为空~~（G7 已闭合）：`SeedComponentCatalog` 落 10 个蓝本 + 三轴格子，e2e 断言矩阵非空且每格挂真令牌清单。
- ~~组件规格没进机器可读产物~~（G12 已闭合，v2.4.0）：registry/DESIGN.md/十类实体明细都从库里读规格，见「规格必须出得来」。
  **剩一小口**：界面「导出台」没把这十类实体 url 列出来给用户直连，目前它是纯后端契约（P3）。
- ~~导出体积未实测~~（T302/U1 已给数字）：见上条与 `05-evidence.md`。
- **eslint 覆盖不到插件 src**：宿主 flat config 的 `files` 不能越出宿主 `src/**`，插件 lint 仍无正规入口（已进 TODO，需要产品级决定"每插件自带工具链"还是"宿主 config 增子目录"）。
- `plugin.json` 的 `entry` 无 content-hash 缓存键（宿主加载器职责，ROADMAP P1.3），升级后浏览器可能命中旧缓存。
