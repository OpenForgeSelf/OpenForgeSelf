# Repository Understanding

> 阶段：Stage 0｜规范：docs/04-standards/ai-native-engineering-workflow.md §2
> Task ID：PILOT-ds-m3-style-guideline｜日期：2026-10-01｜所属：设计插件升级 M3（v3.1.0；M1 见 `2026-10-01-design-system-m1-agent-tools`，M2 见 `2026-10-01-design-system-m2-showroom-wizard`）
> 所有条目来自真实仓库内容（读码/读文档/跑命令），来源标注在「依据」列；**凡引用 M1/M2 产物的条目，依据是它们的 03-plan 契约（尚未实现），开工时必须对着真实代码复核**（见末尾「开工前复核清单」）。

## 项目结构

- 本任务对象：`Plugins/DesignSystem/` 后端生成器与数据层（`Services/{DesignGenerator,ScaleGenerators,TypographyGenerator,ColorRampGenerator,SemanticResolver,ExportService,ReleaseService,…}.cs`、`Data/Model.xml`、`Data/Entities/*`）+ 少量前端（工作台新增一个 section、微调面板扩展）+ M1 工具的**增量**扩展。
- `Data/`（Verified）：`Model.xml`（12 张表）、`Entities/`（每表两个文件：`X.cs` 为 **xcode 生成物**、`X.Biz.cs` 为业务 partial，二者都在库里）、`DesignSystemTables.cs`（`EntityTypes` 登记 + `EnsureCreated`）、`DesignSystem.htm`（xcode 生成的模型文档页，随 `Model.xml` 重生成）。
- 工具链（Verified，`Get-Command`/`dotnet tool list -g`）：本机已装 `xcodetool 11.25.2026.901`，命令 `xcode`（位于 `C:\Users\Administrator\.dotnet\tools\xcode.exe`）。技能 `design-system-verify` §三 规定：只改 `Model.xml` 再 `xcode Model.xml` 生成，**生成物不手改**，业务码进 `.Biz.cs`；生成后须"二次生成逐字节一致 + `BindColumn` 集合不漂移"。

## 技术栈

| 层   | 技术                                                                                                                                                                                                            | 依据                                                             |
| ---- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------- |
| 后端 | .NET 10 / C# / NewLife.XCode 12.0.2026.701 / SQLite（插件自建库 `DesignSystem.db`，连接名 `DesignSystem`）                                                                                                      | `DesignSystem.csproj`、`DesignSystemTables.ConnName`             |
| 建表 | `DesignSystemTables.EnsureCreated()` → `EntityFactory.InitConnection("DesignSystem")` **全量建表**：新增的实体类型在已有库上会被自动补建（只加表、不动旧表），这是本里程碑"新增 `DesignGuideline` 表"的升级路径 | `DesignSystemTables.EnsureCreated`                               |
| 测试 | xUnit + FluentAssertions；`[Collection("XCode")]` + 每类独立 SQLite 目录；同源/反例/契约反射风格见 `ReleaseSnapshotTests`、`ColorGenerationTests`、`ExportProjectionTests`                                      | `ForgeSelf.Api.Tests/Plugins/DesignSystemTests/*`（基线 208 例） |
| 前端 | Vue 3 lib、原生标签、模块单例状态（同 M2 00）                                                                                                                                                                   | `web/vite.config.ts`                                             |

## 生成器现状：哪些参数会改变产物，哪些写死（Verified，读码）

- `GenerationRequest`（`DesignGenerator.cs` 文件顶部）：`Brief / SeedColor / Hue / AccentHueOffset(默认168) / Chroma / Density / TypeRatio / TypeBasePx / RadiusBase / MotionScale / BrandName / Themes(默认 light+dark) / Industry`。行业参数包 7 个（`Industries` 字典：devtools/finance/healthcare/education/commerce/media/general → ratio/chroma/radius/density/motion）。
- **写死（风格轴的缺口）**：
  - 字体栈：`TypeOptions.FontSans = "\"Inter\", \"Noto Sans SC\", system-ui, sans-serif"`、`FontMono = "\"JetBrains Mono\", ui-monospace, monospace"`，`DesignGenerator` 只传 `(basePx, ratio, 320, 1280, 0.35)` → **字体栈不随请求变**；字重 400/500/600/700 固定；行高/字距按 px 阈值固定（`TypographyGenerator`）。
  - 阴影：5 级 `elevation-1..5`，`y=level×d`、`blur=level^1.6×3×d`、`spread=-level×0.5`、`alpha=clamp(S×(0.05+level×0.045),0,0.45)`；亮色阴影色 `#0f172a`，`level≥3` 加第二层；暗色用 1px 白色 inset 高光（alpha 0.06）+ 外投影 `#000000`。**`ScaleOptions.ShadowStrength` 已存在，但生成器固定传 1，`GenerationRequest` 未暴露**（`ScaleGenerators.cs` 顶部的 `ScaleOptions`、`DesignGenerator.Generate` 里的 `new ScaleOptions(density, radius, 1, motion)`）。
  - 描边：`border.hairline=1px`、`border.thick=2px` 固定。
  - 圆角：档系数 `xs.35 sm.65 md1 lg1.65 xl2.3 2xl3.3`（×`RadiusBase`×密度系数）+ `pill=999px`、`full=9999px`；**组件到圆角档的映射写死**：`button.radius=md`、`button.{sm,md,lg}.radius=sm/md/lg`、`input.radius=md`、`select.radius=md`、`badge.radius=pill`、`card.radius=lg`、`dialog.radius=lg`、`tooltip.radius=sm`（`DesignGenerator.ComponentTokens()`）。
  - 中性色：`ColorRampGenerator.GenerateNeutral(hue, tint=0.012)`——带**品牌色相**的极低彩度中性阶；`tint` 与色相取向不可调。
  - 强调色：`AccentHueOffset`（数值）可调；`info` 色族也跟着 `hue+accentOffset`（`RampFor`）。状态色色相固定（success 152 / warning 85 / danger 27）。
  - 语义角色表（`SemanticResolver.LightRoles/DarkRoles`，18 个角色，按 `PreferL` + 对比度反查 tone）、10 个组件蓝本（button/card/input/badge/nav/table/dialog/tooltip/tabs/select）与其令牌清单（`ComponentTokens()`）、缓动曲线 4 条、时长 `120/200/320/480ms`、断点 `640/768/1024/1280/1536`、`z-index 0/10/20/30/40/1000` 全部固定。
- 同参数同输出：种子参数写进每行 `GeneratorSeed`（`BuildSeed(...)`），由 `ColorGenerationTests` 钉"同参数两次生成逐字节一致"。

## 持久化 / 版本 / 导出现状（Verified，读码）

- **发布快照**：`ReleaseSnapshot.CurrentSchema = 2`（令牌 + `Specs`：component/variant/asset/screen/font，`Kind+Key+字段表`）；`DiffOf` 里 `comparable = a.Specs is not null && b.Specs is not null`——**只区分"有没有 Specs 节"，不区分 Specs 里有哪几种 kind**。若直接往 Specs 里加 `guideline` kind，schema 2 快照对 schema 3 快照比对时，整节 guideline 会被报成"新增 N 条"（正是本仓反复防的"把结构差异报成内容变更"）→ **M3 必须做 kind 级的可比性判断**。规格进哈希（`Hash`），同版本重发走幂等：内容一致返回已有、不一致拒绝（`CreateCore`）。
- **导出**：`ExportFormats.All` 13 项（M1 后 15 项：+`brief`、`agent-rules`）；`ToDesignMd` / `ToRegistry` / `Bundle` / `Manifest` 都读 `ExportService.Snapshot`（含 Project/Themes/Fonts/Assets/Screens/Components/Variants/Icons）；Stardust 兼容实体清单 `ExportService.StardustEntities` 恒为十类，e2e 断言 `entities.length === 10`。
- **审计**：11 类 `AuditKinds`；`ComponentPairsOf` 从库里按命名约定推导对比度对（新组件自动进门禁）；"生成产物自己必须先过"是既有纪律（自查表 #24）。
- **`SeedBrandCatalog`**（`DesignGenerator.cs:447` 起）的纪律是 M3 `SeedGuidelines` 的蓝本：整批一个事务、**只补空不覆盖**（按自然键跳过用户已有行）、"声明了就必须有种子 + 写入口 + e2e"（自查表 #16）。

## 测试方式

- 后端：`dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~DesignSystem" --logger "console;verbosity=normal"`（总数 == 发现数；`TMP/TEMP` 重定向见 M1 04-task）。
- **黄金回归**（本里程碑新增手段）：风格轴默认值必须**逐字节复现**开工时 HEAD 的产物。做法见 03-plan §A5（先录基线哈希，再改生成器）。
- 前端：`cd Plugins/DesignSystem/web && pnpm run check && pnpm run test && pnpm run build`；既有守卫 `vocabulary.test.ts`（界面不得抄后端词表）、`classes.test.ts`、M2 新增的 `dialogs / mannequins` 守卫。
- e2e：`cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/design-system`；既有 spec 里 14 个 `nav` 文案精确匹配（M3 新增第 15 个入口不影响）；`entities.length === 10` 断言（**M3 不改 Stardust 十类**）。

## 构建命令

```bash
dotnet build Plugins/DesignSystem/DesignSystem.csproj
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~DesignSystem" --logger "console;verbosity=normal"
cd Plugins/DesignSystem && xcode Model.xml     # 仅在改 Model.xml 之后；生成后二次生成比对
cd Plugins/DesignSystem/web && pnpm run check && pnpm run test && pnpm run build
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/design-system
```

> `xcode Model.xml` 的确切调用目录/参数以开工时 `xcode --help` 与既有生成记录（`docs/ai/pilot/design-system-v2/` 的 05-evidence）为准——**本条为 Unknown，开工复核时确认**。

## 主要目录职责

| 目录                                             | 职责                                                    |
| ------------------------------------------------ | ------------------------------------------------------- |
| `Services/DesignGenerator.cs` 等生成器           | 风格轴落点（M3-A）                                      |
| `Data/Model.xml` + `Data/Entities/`              | `DesignGuideline` 表（M3-B；唯一允许的结构变更）        |
| `Services/ExportService.cs`、`ReleaseService.cs` | 规范进导出与版本化（M3-B）                              |
| `Agent/`（M1 产物）                              | 工具增量扩展（M3-B，**只加不改既有字段**）              |
| `web/src/sections/`                              | 新增第 15 个 section「UX 规范」（M3-B；既有 14 个不改） |

## 代码组织方式与既有规范（对本任务有约束力）

- 数据安全铁律 10（只写不删，归档 = 软删）；唯一性判断直查库（铁律 11）；写路径整批一个事务；只读可对 BUSY 重试、写绝不重试。
- 自查表（`design-system-verify`）：#16 声明了就要有种子 + 写入口 + e2e；#18/#21 落库的东西必须同时进"交付物"和"版本"；#22 产物里的数字/引用/url 必须能当场核对；#24 新判据必须造反例；#26/#27 词表唯一真源且要被消费；#33 导入回流四问（对规范同样适用：round-trip、不丢条目、以库为准）。
- 版本三元组与 `plugin.json.Version` 同步（`DesignSystemAuthTests` 断言）；同版本产物变了必须升版。

## 候选低风险任务

1. 只做风格轴（M3-A）——不动库结构，风险最低，但"UX 规范作为唯一真源"缺口仍在。
2. **完整 M3（用户已定方向，切 A/B/C 三片）**：A 风格轴 + 预设扩充；B `DesignGuideline` 表 + 生成器 + REST + 导出/快照/工具/界面；C 视觉 QA 与文档技能。
3. 扩充组件蓝本（avatar/progress/switch…）——**本里程碑明确不做**：它会改变默认产物，破坏"默认轴逐字节兼容"这条最强回归判据；M2 模特只用现有 10 个蓝本组件，已够用。需要时另立 M4。

## 选择该任务的原因

用户（2026-09-30 原话）：「都能产出令人满意的设计，输出完整的设计系统。后续项目开发，关于前端 ui ux 的一切都依据该插件产出的设计系统，唯一真源，开发完页面审查也可作为依据」。计划轮已选「UX 规范 = A 新增 DesignGuideline 表（可编辑，进快照与导出）」。当前缺口：① 风格几乎只能换色（字体栈/阴影/描边/圆角映射/中性色全写死），"令人满意且彼此明显不同的设计"做不到；② 设计系统只有"视觉令牌"，没有 UX 规范（布局栅格、页面模式、导航、表单、反馈、状态、文案、可达性、动效），"UI/UX 唯一真源"只覆盖了 UI 一半。

## 开工前复核清单（实现方第一步，结果写入 05-evidence「开工复核」）

1. M1、M2 均已合入且各自 AC 全 Verified；M2 的 `preview-css` / 展厅 / 向导 / 交付页在真实宿主可用。
2. 重新取基线：后端过滤集（总数 == 发现数）、web `check/test/build`、既有 `e2e/plugins/design-system`；记录存量红。
3. **录黄金基线**（03 §A5，**必须在改任何生成器代码之前**）：`StylePresets.All` 的**原 8 个预设**（M3 开工时它就是这 8 个）× `shared` 与各主题层，外加 3 个非预设请求，规范化令牌文本的 SHA-256，贴入 `StyleAxisGoldenTests`。
4. 确认 `xcode` 调用方式（`xcode --help`；参考 `design-system-v2` 的生成记录），并先对**未改动**的 `Model.xml` 跑一次，确认生成物与库里**逐字节一致**（否则先查清环境差异，不要带着漂移改表）。
5. `font.display` 可行性小探针（用完即删）：确认 `ExportService`/`DesignGenerator.SeedBrandCatalog` 对新增 `font.*` 令牌不假设"只有 sans/mono 两条"；若有，登记偏差。
6. `git status` 清点并行会话改动，确认不与 `Plugins/DesignSystem/**` 重叠。
