# Specification — DesignSystem 插件 v2.0.0

> 阶段：Stage 2｜从 `00-repository-understanding.md` + `01-intent.md` 推导，全部内容对应真实文件/端点，不发明不存在的接口。
> Task ID：PILOT-ds-v2｜规则：不确定点记 `Unknown`，禁止猜测与美化。**闸门1 用户确认的就是 §Acceptance Criteria 这张清单。**

## Functional Requirements

| # | 需求 | 依据 |
|---|---|---|
| FR1 | **设计系统项目（系统 of record）**：创建 / 重命名 / 列出 / 归档设计系统项目；项目含 Code、Name、种子文本与生成参数；`Status` 三态 `draft|published|archived`（软删，永不物理删数据） | intent D1、design §2 表1、G14 |
| FR2 | **主题（模式轴）**：每项目多主题（`light`/`dark`/`high-contrast` + 自定义），主题分 `color`/`density` 两类；`ThemeId=0` 表示跨主题共享层；某主题有效值 = 共享层 + 覆盖层按 `Path` 合并 | B2、design §2、G4 |
| FR3 | **三层令牌库**：`primitive / semantic / component` 三层，`Path` 点分 kebab 唯一（`(ProjectId,ThemeId,Path)` 唯一索引）；类型封闭枚举与 DTCG `$type` 一一对应（color/dimension/fontFamily/fontWeight/duration/cubicBezier/number/string/shadow/border/gradient/typography/transition/strokeStyle）；复合值存 `ValueJson` | B1/B2、D2、G15 |
| FR4 | **别名图**：令牌可指向另一 `Path`；只允许 高层→低层或同层；解析深度 ≤16；**检出环 / 悬空别名 → 该批次生成失败并返回参与环或缺失的路径清单，且不落任何行**（整批事务） | B1、G11/G17 |
| FR5 | **确定性生成引擎**：由 brief 文本 + 种子色 + 参数（比例、密度、色阶档数、主题集合）产出全量令牌；**同输入必得同输出**；每行携带 `Generator/GeneratorSeed/GeneratorVersion`；重新生成默认跳过 `Generator=manual` 的人工覆写行并回报冲突数 | B implication 5、G8 |
| FR6 | **色彩科学替换启发式**：oklch 感知色阶（tone 目标 + hue-cycling 修正暗端发浑/亮端漂移 + 中调彩度峰值）；语义角色**按所需对比度反查 tone**（定向选取，不是事后审计）；派生态（hover/active/表面染色）在导出 CSS 里用 `color-mix(in oklab)` 表达；中性灰不是写死 slate 梯度而是随种子色相微染 | B3、替换 `colorScale.ts:29-49`、`generate.ts:23-61` |
| FR7 | **非色令牌必须真实变化**：排版（模块化比例 + 每档 size/line-height/tracking + fluid `clamp()` 最小/最大视口）、spacing（基准单位 × 密度）、radius、border、shadow（**逐层**）、motion（cubic-bezier + 时长 + **`prefers-reduced-motion` 派生变体**）——随项目参数变化，禁止逐项目字节相同 | v1 缺陷 `generate.ts:23-61 ≡ presets.ts:68-93`；B4 |
| FR8 | **可达性作为数据与门禁**：色对计算 WCAG 2.2 比率（正文 4.5:1 / 大字与非文本 3:1 / AAA 7:1）并落 `DesignAudit`；审计项含 contrast / focus-visible / reduced-motion / 跨层违规（component 直连 primitive）/ 孤儿与未用；APCA 只作参考读数不作门禁；**存在 Critical 未通过时禁止创建 Release** | B4、D5、D10 |
| FR9 | **组件清单与变体矩阵**：`DesignComponent` × `DesignComponentVariant(variant × state × theme)`，变体的背景/文字/边框**以令牌 `Path` 引用**表达；每组件可存 props 文档、do/don't、逐组件可达性标注；变体入库前规范化序列化以保幂等 upsert | B6、design G5 |
| FR10 | **图标库**：内置集合（真实 `SvgBody` 路径数据）+ 项目自定义图标；stroke-width / grid / sizes / tags 可查可导；内置库只读（显式拒改） | B7、A2、G7 |
| FR11 | **字体资产与页面清单**：`DesignFontFace`（family/weight/style/file/display/role/license）与 `DesignScreen`（页面→图标→组件引用）作为**非令牌**的交付清单可 CRUD 并进入导出包 | A2、B implication 11(e) |
| FR12 | **标准投影导出（≥8 种）**：DTCG `tokens.json`、CSS 自定义属性（保留别名与 `color-mix`）、Tailwind v4 `@theme` CSS、SCSS、LESS、TS 带类型、Tokens Studio `$themes` + Figma Variables JSON、DESIGN.md、shadcn 风格 `registry.json`、**Stardust 兼容 `<entity>.json` + `data.sql`**；单文件预览 + 多文件 zip + SHA256 | B1/B5/B6、A3、D8 |
| FR13 | **Stardust 兼容只读端点**：`GET /api/design-system/{entity}.json` 返回 `{entity, source, generated, total, data[]}` 且字段名与其 88 列逻辑实体名对齐（不暴露我们的物理表），附 `api/index.json` 清单，使参考物查看器可直连我们 | A3 |
| FR14 | **版本化与回归 diff**：创建不可变 Release 快照（版本、令牌哈希、审计摘要、说明），任意两个快照可 diff（新增/删除/改值/改别名/主题差异），同 brief 可复现 | B implication 10、G13 |
| FR15 | **库驱动界面**：12 个 section（见 design §4）全部读写后端；组件库按所选设计系统真实渲染其 component tier 令牌与变体矩阵；主题微调面板（theme/density/brand L-C-H + 任意令牌覆盖）可保存进后端 | 现状 5/6 section 是静态 fixture |
| FR16 | **令牌唯一来源不变量**：插件界面所有可视样式只消费 `--ds-*`；清除 `DsButton.vue:74/:113/:117`、`DesignSystemView.vue:102`、`DesignStudio.vue:498` 的硬编码色值；`BrandLogo.vue:53` 字标改用项目 `Name`；`plugin.json:7` 的 `IconUrl` 去掉 `example.com` 占位 | README 自订不变量 `:116` |
| FR17 | **交互设计统一要求**：点即保存 + 部分更新（`{value?}/{aliasPath?}/{description?}` 分传）；操作成败可见（失败留窗并打印原因）；轮询/刷新内容未变不赋值；空态分级（未选项目 / 无令牌 / 生成失败 / 审计全清 / 宿主未登录）；令牌树筛选 + 分页边界；破坏性操作二次确认且确认逻辑为可单测纯函数；根视图版本徽标 | `plugin-development` §3.4 / 铁律 13 |
| FR18 | **鉴权与自描述**：所有控制器类级 `[Authorize("ApiKeyPolicy")]`；`GET /api/design-system/meta` 返回 `ModelVersion / GeneratorVersion / ProjectionVersion / 能力面清单`，前端据能力面对不支持项显式降级 | 铁律 17、design §6 问2/问4 |

## Input
- 文本 brief（沿用 v1 的输入习惯，但仅作参数线索）、种子色（hex/oklch）、色相/彩度、行业倾向、排版比例（如 1.25 major-third）、密度、主题集合、档数（50..950）。
- 令牌级编辑输入：`value` / `aliasPath` / `description` / `deprecated` / `extensions`。
- 组件/图标/字体/页面的 CRUD 载荷；导出参数（`format`、`theme`、`group`、`include`）。

## Output
- 有效令牌视图（解析别名后的值 + 原始引用 + 对比度读数），按 tier/group/主题分面。
- `DesignAudit` 结果集（含通过率与 Critical 明细）与可导出审计报告。
- 12 种导出工件（单文件 / zip + SHA256）；Stardust 兼容 `api/*.json` + `data.sql`。
- Release 快照与两个快照间的 diff 报告。
- 前端：12 个库驱动 section；实时预览换肤（不谎报落库）。

## Business Rules
1. `(ProjectId, ThemeId, Path)` 唯一；唯一性校验**直查 DB**，不读 `Meta.Cache`（铁律 11）。
2. 别名只可 高层→低层或同层，深度 ≤16；成环或悬空即整批失败（FR4）。
3. `Generator=manual` 的行不被重新生成覆盖，除非 `overwrite=true`。
4. 有 Critical 审计未通过 → 拒绝创建 Release（返回 409 + 明细）。
5. 内置图标库（常量 `BuiltinProjectId`）只读。
6. Release 快照不可变；一切修改只作用于草稿态。
7. 删除项目 = `Status=archived`（软删）；插件代码与测试**不得删除任何库文件/数据目录**（铁律 10）。
8. 复合 shadow 令牌以 `ValueJson` 为真源，`DesignShadowLayer` 为查询投影，两者必须一致（服务层单点写）。
9. 前端本地缓存仅作离线草稿，UI 必须显式标注"未落库"；后端不可用不得静默假装成功（G20）。

## Boundary Conditions
- 空项目 / 只有 primitive / 无主题 / 主题无覆盖；令牌数 0 与 >5000（分页、导出体积，见 Unknown U1）。
- 别名自指（`a→a`）、互指（`a→b→a`）、链长 17、指向 deprecated、指向另一 tier 之上（component→semantic 合法，semantic→component 非法）。
- 色值边界：`L=0`、`C=0`（纯灰，hue 未定义）、gamut 外 `oklch` 值需 clamp 回 sRGB 并在审计里记 info；alpha 0 / 1。
- 文本边界：brief 为空（v1 是"至少 10 字符"，改为可空 = 纯参数模式）；brief >2000 字符截断并提示；中文/emoji/全角；项目 `Code` 冲突。
- 并发：两窗口编辑同一令牌 → `UpdatedAt` 乐观并发 409。
- 窄屏（1280×720 默认视口）不破版；插件根滚动容器子区块 `flex-shrink:0`（铁律 8）。

## Error Handling
| 情形 | 行为 |
|---|---|
| 别名环 / 悬空 | 400 + `paths[]`；事务回滚，零行落库；前端逐行标红 |
| 生成后存在 Critical 对比度失败 | 生成**成功返回**（值可用），但审计标 Critical、禁止 Release；UI 给"哪些角色对不达标、建议换哪个 tone"的可执行提示 |
| 唯一性冲突 | 409 + 冲突 `Path`；不静默改键 |
| 宿主未登录 / token 过期 | 前端分级空态 + 重试入口，不用 localStorage 假装成功 |
| 建表失败 | `Apply()` 抛出并记日志（绝不吞），宿主日志可见根因 |
| 导出格式不支持 | 400 + 能力清单（来自 `meta`），不返回半成品 |

## Compatibility
- `plugin.json` `frontend` 契约保持（`views[0]=DesignSystemView`、`route=/design-system`、`entry=web/dist/index.js`）→ 宿主路由/菜单 e2e 不需改（`menu-route-consistency.spec.ts` 仍通过）。
- 版本 `1.2.1 → 2.0.0`；`ForgeSelf.Web/e2e/plugins/design-system/design-system.spec.ts:141` 的 `EXPECTED_VERSION` 必须同步（同 PR 内）。
- 新增：`Data/Model.xml`、`DesignSystemTables.cs`、`Services/`、`Controllers/DesignSystemController.cs`、`web/src/http.ts`、新 sections；`web/package.json` 新增 `check`/`test` 脚本（当前缺失）。
- 保留可读兼容：v1 的 localStorage 工作区在首次进入时**一次性迁移**为草稿项目并提示，随后不再作为真源。
- 不引入新 NuGet / npm 依赖（D4）；不改宿主 `ForgeSelf.Web/src` 渲染链路与 `--el-*` 主题。
- 数据库：**纯增量**（新连接名 `DesignSystem`，新库文件），不改宿主/其它插件表，不需要回滚脚本。

## Non-functional Requirements
- **确定性**：生成器无随机、无时钟依赖（`generatedAt` 只写元数据不进值），可在 xUnit 里做黄金值断言。
- **性能**：5000 令牌的项目，`GET tokens`（分页 200）与 `POST generate` 均 <2s（实测记录，不达标即优化）；导出不超请求体上限。
- **可测性**：所有色彩/对比度/排版/投影逻辑是纯函数（无 IO），双端（C#/TS）同一组黄金值。
- **零 mock**：插件层 e2e 走真实宿主 + 真实库。
- **无警告**：`dotnet build` 0 warning 0 error；`pnpm run check` 0 error（插件 web 现无该脚本，属交付物）。
- **可达性自证**：插件自身 UI 遵循它生成的语义令牌，且自身界面通过 contrast 抽查（吃自己的狗粮，可被 e2e 断言）。

## Acceptance Criteria（闸门1 确认清单 · 逐条可测）

| # | 判据 | 验证方式 |
|---|---|---|
| AC1 | `Plugins/DesignSystem/Data/Model.xml` 存在且含 11 表；`xcode Model.xml` 生成后 `diff` 无字段漂移；实体只有 `Entities/*.cs`（生成）+ `*.Biz.cs`（人工） | 命令 `cd Plugins/DesignSystem/Data && xcode Model.xml` + `diff`（技能铁律 9 三件套） |
| AC2 | 冷启动后插件库 11 张表全部存在；建表异常不吞 | `dotnet test --filter DesignSystem` 中 `EnsureCreated` 用例断言 `TableItem` 全在；日志断言 |
| AC3 | 项目/主题/令牌 CRUD + 批量 upsert 端点可用，`(ProjectId,ThemeId,Path)` 唯一冲突返回 409 | xUnit（控制器/服务层）+ e2e |
| AC4 | 别名成环（`a→b→a`）与悬空别名：返回 400 + 路径清单，且**零行落库** | xUnit（事务回滚断言 count 不变） |
| AC5 | 确定性：同参数两次 `generate` 得逐字节相同的有效令牌集 | xUnit 黄金值（sha256 of sorted paths+values） |
| AC6 | oklch 色阶：给定种子色，50..950 各档 `L` 单调、彩度呈中调峰值、暗端不饱和成灰；与手算/黄金值一致 | xUnit `ColorRampTests`（含 L=0/C=0 边界） |
| AC7 | 对比度定向：语义角色（正文 on 背景 ≥4.5:1、非文本 ≥3:1）由生成器**选 tone 满足**，不依赖事后修 | xUnit 断言生成结果中所有角色对直接达标 |
| AC8 | 非色令牌随参数变化：两份不同排版比例/密度的项目，`typography.*`/`spacing.*`/`motion.*` 值集合**不相等**（破 v1 常量缺陷） | xUnit 差集断言 |
| AC9 | 排版含模块化比例与 fluid `clamp()`；motion 含 `prefers-reduced-motion` 派生；shadow 逐层落 `DesignShadowLayer` 且与 `ValueJson` 一致 | xUnit + 投影 golden-file |
| AC10 | WCAG 2.2 比率数学正确（对已知色对表：白/黑 21、`#767676`/白 4.54 等 ≥6 组黄金值），AAA/AA/非文本判级正确 | xUnit `ContrastMathTests` |
| AC11 | 审计落库且分类齐（contrast/focus/reduced-motion/tier-violation/orphan）；Critical 未清时 `POST releases` 返回 409 | xUnit + e2e |
| AC12 | 8+ 投影全部产出且过校验：DTCG 结构校验（`$value/$type/别名串`）、CSS 保留别名与 `color-mix`、Tailwind **v4 `@theme`**、Tokens Studio `$themes`、DESIGN.md、registry.json、SCSS/LESS/TS、Stardust `<entity>.json`+`data.sql`（**其 truncate 语句必须合法**，不照抄参考物缺陷） | xUnit golden-file 逐格式 |
| AC13 | `GET /api/design-system/{entity}.json` 与 `api/index.json` 形状与参考物一致（`{entity,source,generated,total,data[]}`），且**不暴露物理表名** | xUnit 形状断言 + 字段名白名单 |
| AC14 | Release 不可变快照 + 两快照 diff（新增/删除/改值/改别名/主题差异五类都覆盖） | xUnit |
| AC15 | 插件所有控制器带 `[Authorize("ApiKeyPolicy")]`；无 token 的 HTTP 请求返回 401 | 反射断言（仿 `McpAdminAuthTests`）+ 真 curl |
| AC16 | 前端 12 个 section 全部由后端数据驱动：`ComponentGallery` 不再含硬编码 `api-gateway/auth-service/billing-svc`；`console/*`、`marketing/*` 无"无处理器按钮" | grep 断言 + 走查截图 |
| AC17 | 主题微调面板改 theme/density/brand L-C-H 后，点保存到后端；**刷新页面（换会话）仍生效**；未保存前 UI 标注"未落库" | e2e（reload 持久化断言） |
| AC18 | 重新生成不覆盖 `Generator=manual` 行并回报冲突数；`overwrite=true` 才全量重来 | xUnit + e2e |
| AC19 | `grep -RInE "#[0-9a-fA-F]{3,6}\(|rgba?\\(" Plugins/DesignSystem/web/src --include=*.vue` 无本应走令牌的样式硬编码（白名单：tokens.css、生成脚本、中性遮罩常量）；`BrandLogo` 字标来自项目名；`plugin.json` 无 `example.com` | grep + 走查 |
| AC20 | `cd Plugins/DesignSystem/web && pnpm run build && pnpm run check && pnpm run test` 全绿（`check/test` 脚本为本交付物）；TS 侧色彩数学与 C# 黄金值同表通过 | 门禁命令 |
| AC21 | `dotnet build`（含插件）0 error；`dotnet test --filter DesignSystem` 全绿且跨插件无串扰（独立测试库目录、无删除动作） | 门禁命令 |
| AC22 | 插件层 e2e 全链路通过：建项目 → 生成 → 编辑令牌 → 审计 → 导出下载（读真实文件内容）→ 建 Release → reload 仍在 → 无 fatal console error | `ForgeSelf.Web/e2e/plugins/design-system/`（按 `e2e-testing`） |
| AC23 | 发布：`plugin.json` v2.0.0 + CI 产物或本地 `-UpdateDir` 包可被设置页检查到并升级；**agent 未停/启/杀任何用户宿主进程** | `plugin-publish-verify` 判据（Release 资产 + SHA256SUMS 一致） |
| AC24 | 文档回写：`Plugins/DesignSystem/README.md` 与 `ROADMAP.md` 状态与代码一致（自评矛盾项 `:191-192/:199`、行业键 `:166` 已修）；`docs/02-features/0XX-design-system.md` 记录新端点与契约；`design-system.spec.ts` 版本断言同步 | 人工核对 + grep |
| AC25 | 浏览器走查按 §3.4 交互清单逐项核对并截图读图（点即保存落盘 / 失败留痕 / 无闪动 / 空态分级 / 分页边界 / 二次确认），测试数据清理（软删，不删库） | 走查记录 + 截图目录 |

### AC 口径修订（实现期决定，2026-09-29 记录，不改判定标准只澄清范围）

| AC | 修订 | 理由 |
|---|---|---|
| AC17 | 原写"主题微调面板改 L-C-H 后保存 + 未保存前标『未落库』"。实现改为：**微调=改生成参数重新生成并落库**（`generate/preview` 先看、`generate` 才写），ThemeLab 保持只读（主题增删 + 与 light 的逐条差异对比）。因 v2 无"前端草稿态"，"未落库"标注不再有对应物；持久化改由"reload 后仍从库里读得到"验证（e2e 步 16）。 | 前端不再持有任何设计值真相（not-taken-decisions 010）；造一个"改了但没保存"的中间态等于把 v1 的 localStorage 换个名字请回来 |
| AC20 | 原文含"TS 侧色彩数学与 C# 黄金值同表通过"。该项**取消**：前端不实现色彩数学，故无第二份实现可对齐。`check/test/build` 三项门禁不变，且新增 `derive.test.ts`/`skin.test.ts` 覆盖前端仅有的纯函数（变量命名规则与换肤别名）。 | 两份实现必然漂移，漂掉的正是用户据以决策的数字；见 03-plan 偏差 + not-taken-decisions 010 |
| AC22 | 增补两条必须断言的事实（都是本任务实测揪出来的）：项目列表"令牌数"与库内条数一致；`compact` 主题下 `space.*` 有效值与 `light` **不同**。 | 防止"缓存列当事实"与"密度轴是装饰"两类假绿复发 |

## Unknown
| 不确定点 | 影响 | 处理方式 |
|---|---|---|
| U1 大项目导出体积 / 响应上限（G10） | 超阈值需分页或流式 zip | 保守假设：先按 group 分文件 + zip；实测后若 <1MB 不额外处理；标注并实测记录 |
| U2 Radix Colors 内部生成数学（文档不可达，发的是预计算表） | 只影响"能否再接近它" | 搁置：采用 tone 目标 + hue-cycling（有公开一手依据 B3），并在 spec 记为参考而非依据 |
| U3 宿主 `--el-*` 与本插件 `--ds-*` 的映射是否要输出 | 若要"宿主真换肤"需额外投影 | 询问：属 M8/P3，本轮不实现，只在导出里留 `--ds-*`→`--el-*` 映射表接口位 |
| U4 组件真实渲染：变体矩阵用插件自研组件还是引用宿主组件 | 影响组件库 section 保真度 | 保守假设：先用插件自研 `Ds*` 组件消费 component tier 令牌（零 mock 且不破 external 约束） |
| U5 内置图标库选哪套（Lucide vs Tabler 子集）与许可证署名形式 | 影响资产与 License 列 | 询问（默认 Lucide ISC，取子集并保留 `License` 列） |
| U6 是否需要 LLM 解析 brief（M7） | 影响 FR5 输入丰度 | 搁置：先兑现确定性引擎 + 参数解析；LLM 只作参数解析器，见 D3/P2 |
