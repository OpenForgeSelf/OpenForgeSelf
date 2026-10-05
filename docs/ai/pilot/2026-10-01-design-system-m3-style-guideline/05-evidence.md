# Evidence

> 阶段：Stage 7｜**只记录实际发生的事情**，不得根据代码推测测试结果。
> 每个验证项标注来源等级：Verified（亲自跑过，有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。禁止混用。
> ⛔ 禁用表述：「应该可以」「理论上通过」「看起来没问题」「大概率是」「估计可以」。

> **状态：NOT_STARTED（交接骨架）**——实现尚未开始。除「基线（规划会话）」一节是规划会话的真实实测外，其余各节**全部待实现方填写**；
> 空栏 = 未做，不代表通过。填写规则见 04-task.md「交接说明」；规划方事后按 06-review.md 独立复验，不采信本文自述。

## Task

PILOT-ds-m3-style-guideline（设计插件 v3.1.0：风格轴 + 预设库 + UX 规范）

## 基线（规划会话 2026-10-01 实测，Verified）

| 项                      | 命令 / 条件                                                                                                | 结果                                                                       | 来源等级 |
| ----------------------- | ---------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------- | -------- |
| 发现用例数（M1 开工前） | `dotnet test ForgeSelf.Api.Tests --no-build --filter "FullyQualifiedName~DesignSystem" --list-tests`       | 208（M1/M2 合入后会变大，开工时重取）                                      | Verified |
| 重定向 Temp 后          | 先 `$env:TMP=$env:TEMP='D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\.temp\ds-m1\tmp'` 再跑同过滤集          | `测试总数: 208 通过数: 208 总时间: 4.6885 分钟`                            | Verified |
| 版本现状（M1 开工前）   | `DesignSystemConstants` 三常量 / `plugin.json`                                                             | 均为 `2.7.1`（git HEAD `61b327d`）；M1 → 2.8.0、M2 → 3.0.0、本任务 → 3.1.0 | Verified |
| `xcode` 工具            | `xcode` CLI（xcodetool 11.25.2026.901）已安装；既有实体含 `X.cs` + `X.Biz.cs`，`DesignSystem.htm` 为生成物 | 可用                                                                       | Verified |
| 新增实体类型的升级路径  | `DesignSystemTables.EnsureCreated()` → `EntityFactory.InitConnection("DesignSystem")`                      | 读码：对已有库自动补建新表（**升级测试 AC12 须实测，此处仅为读码**）       | Inferred |

## 开工复核（实现方填，对应 00「开工前复核清单」6 项）

| #   | 复核项                                                                                                                   | 命令 / 动作                   | 结果 | 来源等级 |
| --- | ------------------------------------------------------------------------------------------------------------------------ | ----------------------------- | ---- | -------- |
| 1   | M1、M2 均已合入且各自 06 Final Decision = APPROVED | 读两个 pilot 的 06 + TODO.md | **部分成立**：M1 = APPROVED 且已提交 `5fa914c`；**M2 的 06 仍是 PENDING**（用户 2026-10-03 输入1 原话「已经完成第二阶段开发，正在验收」）。即 06 V2 的「M2 APPROVED」前置未满足，本实现按用户明示指令先行；M2 验收若回改生成器契约，须回来复跑黄金回归与本过滤集。**（收口时更新：该前置现已满足——M2 的 06 已回填 `APPROVED`（原判 CHANGES_REQUIRED 的 5 条已闭环）并提交 `897d10d`；本格保留的是开工时点的事实，不改写历史）** | Verified（读原文 + 收口复核） |
| 2   | 基线重取：过滤集总数/通过/失败；总数 == 发现数；web 三件；既有 e2e；存量红对表 | `dotnet test --filter ~DesignSystem` | **诚实交代两处**：① 未单独跑「M3 改动前」的过滤集——同一工作树含 M2 在途未提交改动，不 stash 别人在制品就复现不出改动前状态；改前数引用 M2 的 05-evidence（过滤集 **362/362**、web 18 文件 238 用例、e2e **19 passed**）。② 第一次跑满过滤集是在轴实现之后：**429 例 / 428 通过 / 1 失败**，唯一失败是我自己写的 §P 彩度断言未跟上 0.05→0.08 的修正（已改并复跑 17/17 绿），**M1/M2 基线零回归红**。收口时（步骤 15）再按「总数 == 发现数」取一次并贴 `--list-tests` | Verified（`.temp/ds-m3/filter-run-1.log`） |
| 3   | **黄金基线已录制**（先于任何生成器改动） | `StyleAxisGoldenTests` 录制器 | 已录并贴入字典；此后每个落地步骤复跑该测试均绿（见下一节） | Verified |
| 4   | `xcode` 对**未改动** `Model.xml` 跑一次：生成物与库内逐字节一致 | `xcode Model.xml`（改表前）+ 改表后再跑一次比对 | **已做（收口时更正本格：此前写"未做/待 T-B"，实际切片 B 已执行并记在 AC12）**：改表前二次生成**零漂移**；加 `DesignGuideline` 表后再跑，生成物对未改动 Model 仍逐字一致，唯一差异是 `DesignSystem.htm` 的 CRLF（`--ignore-cr-with-eol` 下内容零差异），`BindColumn` 集合不漂移 | Verified（见 AC12 行） |
| 5   | `font.display` 可行性探针 | 读码 + 实测 | **确有"只有 sans/mono"的隐含假设**：`DesignGenerator.SeedBrandCatalog` 的字体登记写死两项 `("sans", Value("font.sans")) / ("mono", Value("font.mono"))`。已改为三项、`display` 缺失时 `Value()` 回空串被循环跳过（不造假登记）。`ExportService` 侧的字体投影留待步骤 10 一并核 | Verified |
| 6   | `git status` 清点并行会话改动，与 `Plugins/DesignSystem/**` 无重叠 | `git status` | **有重叠但同族**：`Plugins/DesignSystem/**` 的在途改动就是 M2（同 pilot 家族、未提交），不是第三方会话。M3 只做增量；与 M2 交付面重叠的文件（`PreviewCssService.cs`、`showroom/*`、`start/*`、`design/glossary.ts`）按 04-task Allowed 授权改，收口必须复跑 M2 的 e2e 全目录（AC23/AC19）。另检测到 `testhost` 瞬时锁住 `ForgeSelf.Api.Tests` 输出目录（有会话在跑测），构建重试后通过，**未停/启/杀任何进程** | Verified |

## 黄金基线记录（AC1/AC2；**必须在改任何生成器代码之前填写**）

| 项                          | 内容                                                                                                   |
| --------------------------- | ------------------------------------------------------------------------------------------------------ |
| 录制日期 / git HEAD         | **2026-10-03 01:16（+0800）**，HEAD `d69b0b5`（工作树含 M2 未提交在途改动，见「开工复核」#6）。录制动作**早于**本会话对 `DesignGenerator.cs`/`ScaleGenerators.cs`/`TypographyGenerator.cs`/`ColorRampGenerator.cs` 的任何一次写入（06 V5 可核：这些文件在 `d69b0b5` 之后才被我改） |
| 基线集合                    | 原 8 个预设 × (`shared` + 各主题层) + 3 个非预设请求（`后台管理系统` / `#7c3aed` / `hue=200,compact`） |
| 键数（`Dictionary` 条目数） | **50**（8 预设 × 5 层 = 40 + `request-brief` 3 + `request-seed-color` 3 + `request-hue-compact` 4）；默认路径下录制器还断言"字典条目数 == 用例层数"，漏录/多录当场红 |
| 录制方式                    | `StyleAxisGoldenTests.录制器_仅DS_RECORD_GOLDEN为1时重录基线`（偏差：用环境变量开关代替 `[Fact(Skip)]`），输出 `.temp/ds-m3/golden.txt`（1037 行逐层原文）+ `golden.cs.txt`（字典片段），人工贴入 `Baseline`。**这是测试资产的录制，不是临时脚本作验证结论**（AGENTS §0 红线） |
| 每个轴落地后重跑结果        | 阴影：**绿**（4/4）｜描边：**绿**｜中性色：**绿**｜字体：**绿**｜圆角：**绿**｜强调色：**绿**（六轴 + 强度一次性全部接线后跑 `StyleAxisGoldenTests` 4/4，见 01:27 那次输出）|
| 反向探针（黄金回归必响）    | **已做（收口时更正本格：此前写"未做/Unknown 待补"，实际 05:06 已实跑）**：临时把 `ScaleGenerators.SoftLayers` 的 `blur` 系数改 `* 0.1` → `~StyleAxisGoldenTests` 报 `失败: 1，通过: 3，总计: 4` 并**点名四个漂移档**（`admin-calm\|light` 期望 `5eb53582…` 实得 `4c72881d…` 等）；当场还原后复跑 `失败: 0，通过: 4`。原文与两份日志见「反向探针记录」节（`golden-probe-red.log` / `golden-probe-green.log`）。**来源等级：Verified**（不再是"待补"） |

## Changed Files

<!-- 实现方列出全部改动/新增文件（以 git status 为准）；与 04-task Allowed / 03 Files To Change 逐项对账，多出的要解释 -->

**收口实测（终态 2026-10-04 16:3x 重测，HEAD 仍是 `2ceae29`，M3 未提交）：`git status --porcelain` **112 行**，本批 **106 个展开路径**（**80 已跟踪修改 + 26 本批新增未跟踪**）**——口径写死，两个数不是一回事：**106 = 展开路径数**（`git diff HEAD --name-only` 80 + `git ls-files --others` 里本批的 26），**112 = porcelain 行数**（未跟踪目录折叠：外来 23 个路径只占 6 行）。逐区域项数只在 `07-final-report.md` §3 列**一份**（那里已按 106 机器加总核对）；本文件「接手与收尾」A 段记 10-04 的两跳来源与逐跳文件名。**这里曾经记 84/90 行（10-03 23:15），是"同一份数据抄两遍"必漂的现场**——05/07 各留一份分区表时两处已漂过一轮（05 记 `Tests 21 / skills 3`、07 记 `20 / 2`），故 05 的副本撤掉、只留计数与口径。

**总项数的账要一直摊到最新一轮**（16:56 的 83 → 18:31 的 88 → 20:23 的 89 → 23:15 的 90 行/84 路径 → **10-04 14:50 的 110 行/104 路径 → 16:3x 的 112 行/106 路径**（23:15→14:50 多 1 项 = `e2e-testing` 技能那条并行跑法约束；14:50→16:3x 多 2 项 = 代签把 `06-review.md` 改成已修改 + `agent-workflow.md` 补并发锁一条；81→83 多的两项 = `AGENTS.md` 与 `StyleAxisPerformanceTests.cs`）：并行会话的 LLM 可观测性立项落了 3 项（`docs/06-research/004-llm-observability-forgeself-design.md`、`docs/ai/pilot/2026-10-03-llm-observability/`、`llm-observability-research-2026.md`）；环境又冒出 1 项 `.wbapp_OdgtS39w1X6Yw1qy3Ub0q1.genie`；其余 2 项（根 `README.md`、`dsh-ui-bundle/`）mtime 实测为 **10-02 16:16 / 10-02 15:07**，早于本任务开工，属并行会话/既有在制品，一律**不并入本批、不 `git add`**。**我自己造的临时目录一律当场删**：20:23 删掉本轮 e2e 的两个输出目录 `.pw-out-ds5/`、`.pw-out-ds6/`（`.pw-out-*` 不在 `.gitignore` 里，留着就是脏文件），删前它们是 91 项里的 2 项；此前的 `.pw-out-ds/`、`.pw-out-ds2/3/4` 与仓库外解包目录 `%TEMP%/dsz-m3`、`%TEMP%/dsz-m3b` 同样已清。

**与 04-task Allowed 对账后"多出的"三处，逐处交代**（详见 03-plan 偏差表 02:26 / 04:15 / **16:45** / **18:20–18:24** 各行）：
① `Agent/DesignToolIndex.cs`、`Agent/DesignTools.cs` 未在 Files To Change 列出，但不改就让 agent schema 与后端词表分家（第二份真相）；
② `design-system.spec.ts` 的 `navLabels` 是写死的 14 项清单，第 15 个入口出现后必须纯追加同步，否则 AC19"原 14 入口无新增红"不成立；
③ 新建 `sections/ReleaseBoard.test.ts`（Allowed 只枚举了新增 `sections/Guidelines.vue`）——为把 AC17 的 UI 可见性从 Unknown 补成实测；**加测试那一轮 `ReleaseBoard.vue` 未再改动**，但该文件本体在 M3 实现期确有 `+4/−1` 两处改动（见 03-plan 18:24 那条与 V13 实测形状），此前写成"一字未改"是歧义表述，已更正。零新依赖、产物字节数不变。
**Forbidden 逐路径 diff 为空**：`ForgeSelf.Api/**`、`ForgeSelf.Web/src/**`、两个 `.csproj`、`package.json`（AC24；18:30 重测 `git diff --stat -- ForgeSelf.Api Plugins/McpCenter Plugins/AIAgent` 仍为**空**）。

## AC → 证据矩阵（26 行必须全填；e2e / 读图不可行写 Unknown + 原因 + 替代证据）

> **表结构的一条名实交代（21:45 结构自查实测，不是排版洁癖）**：表头按模板有 6 列（最后一列「结论摘要（关键原文，不许推断）」），但 **AC10–AC18、AC20–AC26 共 16 行只填到第 5 列**（`grep` + 逐行数未转义竖线实测：**6 格的是 AC1–AC9 与 AC19 共 10 行，5 格的是 AC10–AC18 与 AC20–AC26 共 16 行**）。原因不是"摘要没写"，而是**填写时把关键原文就地写进了「结果」列**（例如 AC18 里的 `实测 **9/9**（ac18-dirty-level-before.log…）`、AC23 里的 `529 报告 == 529 发现`），第 6 列因此留空 ⇒ 渲染上那 16 行的最后一格是空的，**读成"该行没做摘要"是误读**。我不动表头（模板要求它在场），也不为了对齐把同一句话抄两遍（那才是第二份真相）。**这一形态不是本轮新发现**：`design-system-verify` 技能 19:33 的「实测噪声白名单」第①条早已登记过（GFM 按格序左填，不会错位），本轮只是把这句话补给读表的人。

| AC   | 判据（摘自 02-spec）                                                                                                                         | 验证命令 / 用例全名 | 结果 | 来源等级 | 输出摘要（贴关键原文，勿贴推断） |
| ---- | -------------------------------------------------------------------------------------------------------------------------------------------- | ------------------- | ---- | -------- | -------------------------------- |
| AC1  | 黄金回归：原 8 预设 × 各层 SHA-256 与基线逐一相等；`GeneratorSeed` 逐字不变 | `StyleAxisGoldenTests.AC1_基线集合_默认轴产物哈希逐一相等` + `AC1_种子串_默认路径逐字不变` | 50 条键全部相等；种子 16 位 hex 无后缀 | Verified | 终态轮原文（`.temp/ds-m1/logs/filter-run-perf-b.log:813–814`）：`已通过 StyleAxisGoldenTests.AC1_种子串_默认路径逐字不变 [14 ms]`／`已通过 StyleAxisGoldenTests.AC1_基线集合_默认轴产物哈希逐一相等 [17 ms]`（该类 4/4 在 519 与 529 两轮都在；19:07 那轮的同一对是 25 ms / 29 ms） |
| AC2  | 缺省请求逐字节同基线（含 3 个非预设请求） | `StyleAxisGoldenTests.AC2_同请求两次_逐字节相同` | 3 个非预设请求（brief / seedColor / hue+compact）全部在 50 键内逐一相等 | Verified | 同上（4/4 绿） |
| AC3  | 每轴每个非默认取值：白名单内 ≥1 条变化、白名单外逐条不变 | `StyleAxisTests`（12 个按轴参数化的用例 + 3 条公式核算 + editorial 的 `font.display`/角色定向 + **`V6_轴改动的路径集合不随输入改变` 4 组参数化** + **`V6_阴影公式的结构不随输入改变` 3 组参数化**） | 全绿：白名单外**零**条越界，各轴"必须改动"条数达标。**V6 反作弊补的是"输入维度"**：本文件其余 AC3 用例共用同一个 `Base()`（hue 220 / 无 brief / general），轴若偷偷依赖那组参数照样绿；新用例对 `shadowStyle=crisp/flat`、`radiusStyle=pill`、`fontPairing=editorial` 各跑**三组新输入**（带 brief 的电商 / hue30+finance+大字阶 / 显式 seedColor+compact+大圆角基），断言"改动**路径集合**与基准完全相同且非空"（多了＝改到白名单外，少了＝换输入不生效），**不另立第二份白名单定义**。**V6 第二族（18:00 补）判的是"结构"而不是"集合"**：`shadowStyle=crisp/flat/layered` 各在三组新输入下阴影**层的形状**必须恒定（crisp＝浅色单层、深色双层且含 `#ffffff` inset + `#000000`；flat＝单层且 `offsetY=0 blur=0`、深色不 inset；layered＝与 `soft` 同前缀且 level≥2 多一层 ambient），三组必须都真跑到（`ran.Should().Be(3, …)`）⇒ 防"只在一条输入上判结构"与"循环空转"（自查表 #56） | Verified | `StyleAxisTests` 单类 **53 条全绿**；全过滤集 **529/529**（20:12 终态，`filter-run-perf-b.log`）。反例：把基准集合换成另一条轴 → **实红 4/4** 并打出实际路径集（`v6-probe-A-wrong-reference.log`） |
| AC4  | 审计矩阵：7 轴 × 取值 × 3 基础请求 × 4 默认主题，0 critical | `StyleAxisAuditTests.AC4_轴取值矩阵_三基础请求四主题_审计零critical` + `AC4_轴矩阵落库后_别名目标仍然存在` | **60 个组合（17 个枚举取值 + 强度 3 点）× 3 基础请求 = 全部 0 critical**；用例数由词表算出并被断言钉住 | Verified | 终态轮原文（`.temp/ds-m1/logs/filter-run-perf-b.log:742`）：`已通过 StyleAxisAuditTests.AC4_轴取值矩阵_三基础请求四主题_审计零critical [1 m 1 s]`——**矩阵本体**（不再靠「无失败记录」的间接说法）在 529 轮内逐条报出并通过；同轮 `:740` 是新增的 `Boundary_editorial才登记display字族_默认档不造假登记` |
| AC5  | `meta.styleAxes` 与 `StyleAxes` 一致；非法取值 400；强度夹取+`Notes`；`accentStrategy` 优先 | `StyleAxisTests.AC5_词表_七条轴且首项是默认`、`AC5_词表的field必须是GenerationRequest的真实可空属性`（反射 + NullabilityInfoContext）、6 条非法取值、2 条越界夹取、`AC3_强调色策略优先于偏移数值` | 全绿。`meta` 出参侧已加 `styleAxes = StyleAxes.Vocabulary()`；**端到端一致性由 AC10/AC11 的界面与 e2e 验** | Verified（单测 + HTTP 层：`meta.styleAxes` 经真实宿主 e2e 消费，见 AC10/S1 —— 收口时把此前留的"Unknown（HTTP 层，待 e2e）"关掉） | 同上（`StyleAxisTests` 现 **53/53**，19:07 全过滤集内） |
| AC6  | 复现：同请求两次逐字节相同；非默认 `Seed` 带后缀、默认无后缀 | `StyleAxisTests.AC6_非默认取值才带种子后缀`、`AC6_种子后缀顺序按轴序且总长不超列宽`、`AC6_同请求两次_种子与产物逐字节相同` | 全绿；后缀实测 `;sh=layered;bo=bold;ne=cool;fo=editorial;ra=round;ac=analogous;st=1.75`，总长 68 ≤ 100 | Verified | 同上（`StyleAxisTests` 现 **53/53**，19:07 全过滤集内） |
| AC7  | 预设恰 13 个；原 8 个 `request` 与基线逐字段相等；新 5 个与 §P 一致；每个"生成→审计"0 critical；覆盖矩阵；M1 回归全绿 | `StylePresetAxisTests`（17 项）+ `StyleAxisAuditTests.AC7_十三个预设_生成落库后审计零critical` + `StylePresetsTests`/`PresetRecommenderTests`（增量） | 全绿。**一处 §P 数值偏差**：`flat-minimal` 彩度 0.05→0.08（0.05 时徽标对比 3.72:1 过不了 4.5:1，由审计测试抓到；§P 允许 ±0.03），已登记 03-plan 偏差表 | Verified | 终态轮原文（`.temp/ds-m1/logs/filter-run-terral.log:1220`）：`已通过 StyleAxisAuditTests.AC7_十三个预设_生成落库后审计零critical [12 s]`（早期轮次同一用例是 23 s，此处以终态为准）；`StylePresetAxisTests 17/17` |
| AC8  | 13 预设 × light/dark：`preview-css` == 落库后导出；M2 变量契约全过 | `PreviewCssTests.AC2_同源_落库导出_等于_内存预览_全预设_明暗`（计数断言 8→13）+ `MannequinVariableContractTests.AC4_模特引用的每个变量_八预设明暗导出都必须有定义`（8→13） | 在**过滤集全跑**中通过（429 例仅 1 红，且那 1 红是我自己的 §P 断言，非本项） | Verified | 终态轮原文（`.temp/ds-m1/logs/filter-run-terral.log:1179,1161`）：`已通过 PreviewCssTests.AC2_同源_落库导出_等于_内存预览_全预设_明暗 [36 s]`、`已通过 MannequinVariableContractTests.AC4_模特引用的每个变量_八预设明暗导出都必须有定义 [367 ms]`。早期轮 `.temp/ds-m3/filter-run-1.log`（429 例 / 失败 1，失败项名见「开工复核」#2，非本项）留作历史 |
| AC9  | 工具/REST 增量：`design_create` 新字段落库后令牌差异成立；schema 键集更新并登记 | `StyleAxisTests.AC9_工具schema的轴属性与StyleAxes同源`（解析 schema 逐轴逐值核 enum/default/min/max）+ `AC9_agent读到的预设request带出轴取值` + `QuickCreateServiceTests.M3_风格轴必须穿过quick_create到生成参数` + 反射守卫 `M3_GenerationRequest的每个可空字段都得是ApplyOverrides的候选`；REST 侧 `generate`/`generate/preview`/`preview-css` 入参为完整 `GenerationRequest`，e2e S2 已实测请求体带轴且交付 CSS 随轴变 | **绿（轴部分）**：当时 46/46（该类现为 53/53，多出的是 V4/V6 两期新增）；schema JSON 由 `DesignAgentToolContractTests` 当场判过一次非法并修好 | Verified | 终态轮原文（`.temp/ds-m1/logs/filter-run-terral.log:1358–1360`）：`AC9_工具schema的轴属性与StyleAxes同源(toolName: "design_create")`、`(toolName: "design_edit")`、`AC9_agent读到的预设request带出轴取值` 三条逐条报出并通过。`guideline` 类工具增量已由 AC18 的 `GuidelineToolTests` 8 条补齐 |
| AC10 | 前端：「更多风格选项」全由 `meta.styleAxes` 渲染；`vocabulary` 守卫反向探针变红；`tune.ts` 映射                                              | `design-system-style.spec.ts` S1（控件条数与取值文案两处判据）+ `design/vocabulary.test.ts`（界面不许再存一份后端词表）+ `showroom/tune.ts` 的轴映射 | 全绿：S1 断言 `[data-axis]` **条数 == `meta.styleAxes` 条数**、且第一个枚举轴的芯片文案**逐字等于** `meta.valueLabels[value]`（界面没抄词表也没自译）；`tune.ts` 的轴控件写回按 `field` 走，实测请求体 `shadowStyle="crisp"`（S2）；守卫反向探针（往 `tune.ts` 追加三值字面量）**确实变红**并给出「应改读 GET /meta」的原文（见「反向探针记录」第 4 行），还原后 246/246 绿 | Verified |
| AC11 | e2e：editorial vs tech-crisp 标题 `font-family`/卡片 `box-shadow` 不同；向导创建后含 `font.display` | `design-system-style.spec.ts` S1/S2/S3 | **部分成立**：注入到页面的交付 CSS 层面成立（editorial 定义 `--ds-font-display` 衬线栈、tech-crisp 不定义；卡片 `box-shadow` 两套不同且非 `none`）；**「标题 computed `font-family` 不同」这条不成立**，根因是既有导出把复合排版令牌的 `fontFamily` 丢了（见 Known Limitations #1，非 M3 引入）。S2 另抓到并修好了 quick-create 丢轴缺陷 | Verified（e2e 日志 `.temp/ds-m3/e2e-style-a*.log`，S1/S2 真实红过并已回填） |
| AC12 | 表与升级：`Model.xml` 仅新增一表；xcode 二次生成一致；`EntityTypes` 登记；旧库升级测试 | `GuidelineSchemaTests`（5）+ `GuidelineUpgradeTests`（3）；`dotnet test --filter ~GuidelineSchemaTests\|~GuidelineUpgradeTests`；`git diff --stat Model.xml` | 全绿：EntityTypes 13 张且 `Model.xml` 13 个 `<Table>`（既有 12 张逐张在列）；生成物三条 `BindIndex` 逐条对 Model；**全控制器无 `HttpDeleteAttribute`（反射）**；类级 `[Authorize("ApiKeyPolicy")]` 在位；13 张表由建表路径产出到物理库；重复 `EnsureCreated()` 后旧令牌行逐行不变、规范零回填、显式生成能落 14 条；同 (ProjectId,Code) 第二条 `Save()` 被拒而换 code 写得进（证明唯一索引真落到库里）。`Model.xml` diff = 27 增 0 删；xcode 二次生成对未改动 Model 零漂移（仅 `DesignSystem.htm` 的 CRLF，`--ignore-cr-with-eol` 内容零差异）。旧库冷进程形态单独 Verified 一次，跑序无关化改造见 03-plan 偏差表首行 | Verified（`dotnet test` 输出；`GuidelineUpgradeTests.AC12_*` 三条） |
| AC13 | `GuidelineGenerator`：确定性、14 个 codes、MUST≥1、引用存在、§G3 敏感性、数字守卫（+反向探针） | `GuidelineGeneratorTests`（21） | 全绿：codes 集合恰等 §G2 的 14 个；每条 3–6 规则且 MUST≥1；规则 id 小写 kebab；同输入两次 JSON 逐字相同；引用只含传入的真路径（无路径时为空）；300+ 串过数字守卫（`\d+(px\|rem\|em\|ms\|s)` 与 `#hex`，先剥 WCAG 比值）；反向探针（塞一句含 `16px` 的模板）确实变红；kind/density/industry 三轴逐行有 §G3 约定差异。**对号入座（18:20 补）**：这一条就是 06 预注册 **V10 第二段**（"对生成器产出的 14 条默认规范（3 种 kind）自行跑一遍正则 `\d+(\.\d+)?(px\|rem\|em\|ms\|s)` 与 `#hex`，WCAG 比值与列表序号除外"）的答案——扫描面比 V10 要求的更宽（3 kind × 3 density × 7 industry、>300 条文本、`texts.Count>300` 防空转），此前 05 只把它记在 AC13 名下、没映射到 V10，属登记缺失而非能力缺失 | Verified |
| AC14 | 仓储/服务：只补空；manual 受保护；`overwrite` 覆盖；`generate` 响应含 `guidelines`；存量不回填 | `GuidelineServiceTests`（10）+ `GenerateShapeTests`（2）+ `GuidelineUpgradeTests` | 全绿：首次 created=14 codes、二次 created 空 / skipped 14 / `Count` 不翻倍；手改行 `SkippedProtected=[buttons]` 且标题/正文/**UpdatedAt 一字不变**；`overwrite=true` 点名 14 条含 buttons；引用不存在令牌 → `ArgumentException` 逐条列出且**不列出存在的那条**、零行写入；非法分类/状态/超限规则/不合形 code 四条文案各给可用值且库里 0 行；归档行参与判重、默认清单读不到、`all` 读得到、PUT 恢复；`Run` 第二次带回 14（不是 0）；播种失败不判死生成但写进 `Notes`。存量项目不回填＝升级零回填那条。顺带修掉两处真实缺陷：`space.4` 反推密度串档、`SeedGuidelines` 回新增数 | Verified（10/10 + 2/2 通过） |
| AC15 | REST：CRUD/过滤/`brokenRefs`/校验/鉴权/**无 DELETE**/409/软归档恢复 | `GuidelineRestTests`（8）+ `GuidelineSchemaTests` 反射两条 | 全绿：清单 14 条带 `categoryLabel`（读后端词表）；分类/状态过滤（未知分类回空不回全部）；坏引用 `brokenRefs` 逐条点名 + 正文标「已不存在」而 `bodyRaw` 不脏；四类校验回 400 且文案含非法值与可用值；code 不存在 404、`expectUpdatedAt` 过期 409（409 后库里仍是第一版）、项目不存在沿用控制器既有异常口径（已记 TODO）；软归档可 PUT 恢复；`generate` 端点回传 created/skipped/skippedProtected/overwritten/total 真计数；`meta` 的 `guidelineCategories/levels/styleAxes/capabilities(guidelines)` 与后端词表同源；每条规范 `tokenRefs` 全能在默认主题取到值、`brokenRefs` 为空、`tokenValues` 与 `tokens/effective` 逐字相等 | Verified |
| AC16 | 导出：brief 章/design-md 章/bundle 两文件/Manifest/agent-rules；空不开章；`contentHash` 随之变化；渲染值==`tokens/effective`；Stardust 仍 10 | `GuidelineExportTests`（**12**，18:20 由 10 增到 12：新增的两条都是 V10） | 全绿：brief 出 `guidelines` 章、排在 `checklist` 前、勾选项条数恰等 MUST 规则数（SHOULD/MAY 不进）；预算不足时整章进 `omitted` 不留半截；design-md 出完整章（含 SHOULD/MAY 与「引用令牌：」行）；bundle 两份文件真在 zip 里且 Manifest 列出（无规范时既不开章也不写文件也不列）；agent-rules 指向 `guidelines/GUIDELINES.md` 与 `design_context sections=["guidelines"]`；`contentHash` 随规范改标题而变、同内容重算幂等；导出 `tokenValues` 逐条 == 控制器 `tokens/effective`；`compact` 主题不抛且无假断链；Stardust 实体清单仍 10 且不含 guideline。过程中修掉真实缺陷 #5：规范章取值改为固定参考主题（`GuidelineView`） | Verified |
| AC17 | 快照 schema 3：不凭空新增；v3↔v3 报变更；hash 含规范；无规范项目重发幂等；旧文件可读 | `GuidelineReleaseTests`（6）+ `ReleaseSnapshotTests`（17，零回归） | 全绿：`CurrentSchema=3` 且快照含 14 条 `guideline` 规格（归档行不进）；schema2↔3 双向 `NotComparableKinds=["guideline"]`、`SpecsAdded/Removed/Changed` 均不含 guideline、`IsEmpty=true`；v3↔v3 报出 title/body 字段级变更，新增→SpecsAdded、归档→SpecsRemoved；只改规范正文 → 同版本重发被 `DesignConflictException` 拒（证明规范真进了 hash）；无规范项目重发幂等返回同一行、跨 schema 比对 `Total=0`；把快照文件改回 schema 2 形态后仍可读。**V9 的"ReleaseBoard 提示可见"这一档已从 Unknown 补成实测**（2026-10-03 16:38）：`ReleaseBoard.vue` 的 `[data-diff-not-comparable]` 只在**两份快照的 diff** 里出现，而 e2e 侧确实造不出这个前提——**已核实的原因**（不再是推测）：`ReleaseService.Create` 永远按 `CurrentSchema` 写快照，且 `DesignMapper.cs:102-104` 的 `ReleaseDto` 只回 `snapshotAvailable` 布尔、**不回快照文件路径**，连"把旧文件改成 schema 2 形态"都没有入口。改用组件级实测：新增 `Plugins/DesignSystem/web/src/sections/ReleaseBoard.test.ts`（`@vue/test-utils` + `jsdom`，宿主早已装好、**零新依赖**，插件侧先例是 `showroom/Showroom.test.ts`），把后端三种真实出参各喂一次并**真点一次「对比」**：① `notComparableKinds=['guideline']` → 提示必须出现、文本必须点名 `guideline` 与"无法比较"，且同屏不得出现"两版内容一致"；② `notComparableKinds=[]` → 提示必须**不**出现（证明是条件渲染，不是常驻装饰）；③ `specsComparable=false`（schema 1 旧快照）→ 走另一条"整节不可比"文案，不与 M3 这条混用。实测 **3 passed（270ms）**，插件 web 全量 `test` 由 247 → **250 passed（19 文件）**；`pnpm run check` **0 error**（`tsconfig.check.json` 的 `include` 是 `src/**/*.ts`，新文件真在射程内，且 `noUnusedLocals/noUnusedParameters` 开着）；`pnpm run build` 产物 `dist/index.js` **442,971 B** + `style.css` **96,481 B**，与门禁②/zip 内字节数一致（加测试文件没动到交付物）。 | Verified（后端六条 + 组件级三条；`19 files / 250 tests`，日志 `.temp/ds-m1/logs/plugin-web-test-releaseboard.log`） |
| AC18 | 工具：`design_edit guideline` 写读回读+写开关；`design_lookup guideline`；`design_context guidelines`；checklist 派生条目（`g:` 前缀、`tokens[]` 全存在）；总数仍 8 | `GuidelineToolTests`（**9**，18:48 收口重测 V3 警告归因时补一条脏数据守卫）+ `DesignAgentTool*`（25，零回归）+ **e2e G7**（21:16 起常驻：经 MCP 网关证明"注册表里实际注册的 design_*"与"插件自述的 `meta.agentTools`"相等，并真调到 `design_guide`） | 全绿：工具总数 8、`design_edit.action` 含 guideline、`design_lookup.kind` 含 guideline、context 章节描述含 guidelines，且 edit schema 的分类/状态/级别枚举逐字等于后端词表（`GuidelineCategories.SchemaProperties()`）；`apply=false` 干跑零写入并回读 `before`，`apply=true` 写入后由 `design_lookup` 读回同文（`source=manual`）、同 code 第二次是更新不是新增；写开关关闭时写动作被拒且库里零行、只读类别照常、干跑仍可；空 `tokens[]`/`rules[]` 不清空既有内容；checklist 派生条目数=库里规则总数、id 全部 `g:<code>:<ruleId>`、MUST→error/SHOULD→warning/MAY→info、`tokens[]` 只含取值视图里存在的路径、静态基线条目未缩水、id 全局唯一。**另钉一条脏数据下限（18:48）**：`AC18_脏规则行_级别为null_checklist不抛且回落SHOULD` —— 直接把库里一行的 `RulesJson` 换成 `level: null` 的脏行（绕过写入校验的历史行形态），断 ① `ReadRules` 把缺失级别**规一成 SHOULD**（这条掉了下面的调用就 NRE）② checklist 照常出 `g:<code>:dirty-1` 且严重度==`warning`（与正常行同一条读法，不留两套口径）。实测 **9/9**（`ac18-dirty-level-before.log`，修 `SeverityOf` 签名**之前**跑的基线），并随 19:07 终态轮再跑一次：`filter-run-terral.log:1138` `已通过 …AC18_脏规则行_级别为null_checklist不抛且回落SHOULD [1 s]` | Verified |
| AC19 | 界面：第 15 个 section、能力置灰；e2e 写一条规范 REST 回读一致；chip 值同源；原 14 入口无新增红                                              | `design-system-guidelines.spec.ts` G1/G2/G3 **+ G4（§G8 DOM 契约逐项走查）+ G5（零规范项目的空态引导）+ G6（820px 窄屏纵向堆叠）** + `shell/nav.ts`（第 15 项带 `capability: 'guidelines'`） | 全绿 6/6（20:05 单 spec 串行 1.0m，`e2e-g5g6-b.log`；首跑 20:03 曾红一条 G6，红因见「规格反向覆盖审计」第 1 条）：入口按 `meta.capabilities` 可用性给灰化（用例断言含 `guidelines` 且按钮 `toBeEnabled`）；界面清单与 REST **逐 code 相等**；chip **逐个** == 后端 `tokenValues` == `tokens/effective?theme=light`（三方同源）；编辑器绑 `bodyRaw`、原文无硬编码数值；改一条 → REST 回读同文且 `source=manual`；重新生成报 `手改受保护 1` 且用户文本未被覆盖；条数不翻倍；新建草稿不落库、保存才进库且徽标 `manual`。**G4 把 §G8 契约逐项跑过**（此前只有我读代码说"都在"）：列表条数 == REST、来源徽标取值 ∈ `generated|manual`、`规范标题/摘要/正文` 三控件 `toBeEditable()`、正文 `tagName === 'TEXTAREA'`（钉住 Forbidden"不许 v-html 渲染规范正文"）、分类与级别下拉候选 == `meta` 词表、规则行三件套、「添加规则」点一次真多一行、chip 文本形状 `path = 值` 且值 == 后端；并声明**走查全程零写入**（同一收集器先证明看到 N 条 GET，才允许宣布 `writes == []`）。实测：`列表 15 条；来源 manual 2 / generated 13`、`添加规则：1 → 2（未保存）`、`chip 8 个，首个「space.4 = 8px」`。**过程中逼出两条真实界面缺陷（#6 新建草稿面板不出现、#7 归档成功零反馈），已修**（详见 E2E·B 块）；原 14 入口的既有 e2e 无新增红在步骤 15 全目录回归复验 | Verified（日志 `.temp/ds-m1/logs/e2e-guidelines-b1.log`、`-b2.log`、`-b3.log`（三个都在，实测 `ls`）、`e2e-g4-walkthrough-2.log`、`e2e-regress-style-guidelines-7.log`） |
| AC20 | 数据安全：无删除路径；归档软删；e2e 只软归档                                                                                                 | `GuidelineSchemaTests`（反射）+ `GuidelineUpgradeTests` + G2 | 全绿：反射断言 `DesignSystemController` **零 `HttpDelete`**（连宿主/仓储一起扫，无 DELETE 出口）；归档=改 `Status=archived`，`status=all` 读得到、默认清单读不到、恢复=PUT `status=adopted`；e2e 只做软归档并且收尾不硬删数据（唯一项目码，隔离库）；归档后项目自身 `status` 必须仍非 archived（不误伤上级） | Verified |
| AC21 | 视觉 QA 逐张读图；每个轴值肉眼可辨；缺陷修复或登记 TODO                                                                                      | `design-system-style.spec.ts` V 片（V1 26 张 / V2 10 张 / V3 20 张 / **V4 3 张（插件自己的控制面：轴面板 + 衣柜）** = **59 张**）+ 逐张读图（结论见「Screenshots」节） | 59 张**全部逐张看过**并写下结论；三条机器判据同时成立：① 每张的页标题/卡标题对最近底色对比度 ≥4.5:1（`low` 数组恒空）；② 每张对应的**舞台注入 == 该件衣服交付 CSS**（声明行逐条相等）；③ V3 每条轴的每个非默认取值都有该轴负责前缀下的变量变化。**逼出并修好真实缺陷 #8**（`shadowStrength=0` 投影成不透明实心色）。诚实边界（不假装"全都一眼可辨"）：`shadowStyle=crisp/layered`、`borderStrength=bold` 在 2200×1000 取景下**可辨度弱**（要靠 computed 数字），`fontPairing=editorial` 这张与对照件**看不出差别**（= Known Limitations #1 的精确边界），三条均如实标注而非改判据。**V4 补的是"控制面"那一半**：轴面板与衣柜此前一张图都没进过证据（`ls screenshots \| grep -icE 'tune\|axis'` 实测 0），现按"选中态看得见 + 选中档==这件衣服真值 + 滑块区间==meta + 三档宽度不横向裁切"四条判据常驻，并跑过反例（换期望源必红，见「反向探针记录」） | Verified（终态 `11 passed (2.6m)`，日志 `.temp/ds-m1/logs/e2e-regress-batchC-rerun.log`；首跑 `e2e-regress-style-guidelines-11.log`）。**对 06 的 V14 逐条清点（18:42 实测 `ls screenshots/e2e/design-system/m2`，不是回忆）**：V14 点名的五个预设`editorial-serif / tech-crisp / flat-minimal / warm-craft / kids-playful` **各有 4 张**（`m3v1-*-admin-light` + `m3v1-*-admin-dark` + `m3v2-*-landing-home-light` + `m3v2-*-mobile-home-light`，13 件预设的 admin 明暗 26 张全在）；V14 要求的「7 条轴各取至少 1 个非默认值」**全部有实拍**：`shadowStyle` crisp/flat/layered(3)、`shadowStrength` 0/2(2)、`borderStrength` bold(1)、`neutralTemp` cool/pure/warm(3)、`fontPairing` editorial/humanist/system(3)、`radiusStyle` pill/round/sharp(3)、`accentStrategy` analogous/mono/split/triadic(4)，另加 `baseline` 对照 = `m3v3-*` 20 张。⇒ V14 的**取证面**无缺口；「肉眼可辨」那一半的诚实边界仍按上行标注（三条可辨度弱 + `fontPairing` 与对照看不出差别＝G16） |
| AC22 | README / ROADMAP / 036 / `design-system-verify` / `design-system-consume` 更新                                                               | 五处文件（`git status` 可见均为 M） | 全部更新：① `Plugins/DesignSystem/README.md` 标题与首部升到 **v3.1.0**、第四节改为"13 张表"并画出 `DesignGuideline` 树、变更记录把**六处真缺陷**逐条点名（quick-create 丢轴 / 密度串档 / SeedGuidelines 回新增数 / 取值主题走错视图 / 界面两处 / `shadowStrength=0` 投影成实心色）；② `ROADMAP.md` 2026-10-01 计划小节标题与 M1/M2/M3 三行状态改为实跑事实（M1 `5fa914c`、M2 `897d10d` 已 APPROVED+提交，M3 实现完成待闸门2，附 529/529 等真实数字）；③ `docs/02-features/036-design-system.md` 版本阶梯补 3.0.0/3.1.0、能力面/数据模型（13 表 + `DesignGuideline` 字段与"正文不存数字"）/端点表（规范 5 条 + 无 DELETE）/`meta` 面/工具表五行/**新增「风格轴与 UX 规范（v3.1.0 · M3）」整节**/工作台 14→15 与真实 nav 顺序/快照 schema 3；④⑤⑥ **三个**技能（10-04 由输入12 追加第四个：`plugin-publish-verify`，见下方「接手与收尾」H 段）：verify 加自查表 **#44–#71（共 28 条，全部 v3.1.0/M3；20:41 由 #44–#64 补两条、#67 出在 20:52–21:00 那轮（产物等价性测量）、#68 出在 21:50 前后（网关链 G7）、#69 出在 22:20 前后（表格结构自查）；编号连续性现查：全表 **71 条、1–71 无断号**（#70「未复现的缺陷不得写成已证根因，注释不是行为证据」、#71「静态扫描类守卫：锚点用仓库根独有的文件，0 违规必须自带阳性对照」为 10-04 两条新追加）**（#49「落库判据要断整族」、#50「长批 e2e 钉端口」、#51「走查必须做成常驻用例 + "没发生 X"型断言要自带反证」、#52「写成功后立刻读可能读到旧视图 ⇒ 判据必须挂在不吃写读链的权威源上」均为批 C 收口时补、**#53「四层门禁绿 ≠ 插件已交付：收口必须交五步对账表，点名哪步被谁 gate 住；不做事决策必须入台账编号」**为 V4/门禁④ 收口时补）、四层门禁 ③ 的 spec 清单更新为五个 spec、第四节加 5 条 e2e 约束（改 .vue 必重建 dist、别硬点"选为工作项目"、结果反馈必须在 section 级、**读界面派生态必须等于权威源**、**并行会话下显式钉 `E2E_FRONTEND_PORT/E2E_BACKEND_PORT`**）、第五节加 3 条已知未做；consume 更新工具表四行 + REST 对等三行 + 常见坑 **#8–#11**（数值是现查的 / 空数组=不改 / 没有删除 / 写后立刻读要轮询；**23:31 由输入47 复测改正**——本格原写 `#8–#10`，而 `git diff HEAD -- .agents/skills/design-system-consume/SKILL.md` 实测本批新增的是 **#8/#9/#10/#11 四条**（#11＝写成功后立刻读可能读到旧视图、集成方必须轮询），全表 1–11 连续无断号）（数值是现查的 / 空数组=不改 / 没有删除）。**22:11 由 G7 的实测读数逼出一条我自己的文档漏改（本格此前不完整）**：`docs/02-features/036-design-system.md` 的 `design_guide` 行仍写「**三条**工作流」，而 M3 给它加的是**第四条**（`guideline`）——G7 经网关拿到的原文就是 `workflows=consume/create/maintain/guideline`，是这条判据把漂移暴露出来的。已改为「四条」并点名第四条的来源与版本号，同节补一句「外部客户端经网关调 design_* 这条链有常驻判据」（不写会漂的数）。⇒ **AC22 此前那句"五处已同步"对 036 这一行并不成立**，本轮补齐；教训：改工具出参的**结构**（多一条工作流）和改数值一样会漂文档，只扫数字不够，要扫" enumerate 型描述"（三条/四个/两类这种话）。 | Verified |
| AC23 | 后端过滤集全绿且总数==发现数；web 三件；既有 e2e 全绿（14 个 nav、`entities===10`）                                                          | 见「Unit Test / Static Analysis / E2E · 全目录回归」 | 后端 **529 报告 == 529 发现 == 529 通过 == 0 失败**（verbose logger，终态 20:12；过程九轮真数：506、508、509、513、514、517、518、519、**529**）；插件 web `check` 0 error / `test` **250（19 文件）** / `build` 442.97 kB JS + 96.48 kB CSS（终态 16:38–16:39，含 V4 与 `ReleaseBoard.test.ts` 之后各重跑一次，产物字节数与 zip 内一致）；宿主 `check` **0 error / 81 warning**（判据改造与 S2/G4/V4 每轮改动后各复跑：05:57、06:26、06:38、07:13、07:37、16:29、16:34、16:36、**21:14（加 G7 后）**、21:55（终态文件复验），**十次**都与基线逐字同数）、`test` 741/741（宿主 `src/**` 本批零改动，此后未再改宿主代码）；`design-system.spec.ts` 的 navLabels 已含 15 项、`meta.entities` 仍 10。**e2e 全目录回归的权威终态是 21:26 那一轮：串行整目录 `33 passed / 0 失败（4.8m）`，跑在终态源码与终态 DLL 上**（20:22 的 `32 passed`、19:21 的 `30 passed` 与 05:37→06:06 的三批 + 16:41 的批 C 复跑一并降为历史轮次；19:11 首跑曾红一条 G1，20:03 加 G5/G6 后又红过一条 G6，21:17 为 G7 的相等判据**故意**红过一次（反向探针），三次红因与加固见「E2E · 全目录回归」「规格反向覆盖审计」「E2E · 网关链 G7」三节）（含 M3 新增 14 条与 M1/M2 既有 19 条同库同宿主）。**四层门禁的第四层（本地 zip 发布产物走查）在终态源码上重跑过**：`release-local.ps1 -Version 2.3.0` → **终态包 `2.3.0.2610031922`（19:22）**，包内 `plugin.json=3.1.0` + DLL/前端实现指纹全 FOUND + `SHA256SUMS` 逐字 MATCH（详见「发布与产物走查」节；未打 tag、未推远程、未碰 `-UpdateDir`；15:57 那轮 `2.3.0.2610031557` 降为历史） | Verified（`.temp/ds-m1/logs/filter-run-perf-b.log` + `list-tests-perf.log`（终态 529）；历史轮次含 `filter-run-terral.log`/`list-tests-terral.log`（519）、`filter-run-final-518.log`/`list-tests-final-518.log`（518）、 `filter-run-v6.log`/`list-tests-v6.log`（513）、`filter-run-v10.log`/`list-tests-v10.log`（514）、`filter-run-final-517.log`/`list-tests-final-517.log`（517）、`filter-run-final.log`/`list-tests-final.log`（509）；批 C 四条归因（覆盖六轮红跑）逐条见「E2E · 全目录回归」） |
| AC24 | 范围：`Model.xml` 仅新增表；宿主/McpCenter 零 diff；无新依赖；组件蓝本仍 10；`AuditKinds.All` 不变                                           | `git status` + `git diff` 实测（04:42） | 改动集合 ⊆ `Plugins/DesignSystem/**` + `ForgeSelf.Api.Tests/Plugins/DesignSystemTests/**` + `ForgeSelf.Web/e2e/plugins/design-system/**` + 本 pilot 目录 + `docs/02-features/036` + `docs/07-decisions/not-taken-decisions.md`（024–027）+ **三个** `.agents/skills/**`（`design-system-verify` / `design-system-consume` / `e2e-testing`）+ `AGENTS.md`（§2.4 技能表里去掉会漂的「33 条」计数）+ `.forgeself`/`TODO.md`（**23:30 补枚举**：原枚举只列了「两个 design-system 技能」，漏掉本批实改的 `AGENTS.md`、`.agents/skills/e2e-testing/SKILL.md`、`docs/07-decisions/not-taken-decisions.md` 三处——`git status` 实测这三项均为 ` M` 且归本批；判据未放宽，仍是 ⊆，逐路径清单以 07 §3 区域表为准）；**`ForgeSelf.Api`、`ForgeSelf.Web/src`、两个 `.csproj`、`package.json` 逐路径 diff 为空**（宿主与 McpCenter/AIAgent 零改动、无新依赖）；`git diff -U0 Model.xml` 的表级增删只有 `+ <Table Name="DesignGuideline" Description="UX 规范">` 与 `</Table>` 两行（既有 12 张表定义零改动）；`AuditKinds.cs`/`AuditEngine.cs`/组件蓝本文件零 diff（`ComponentBlueprints` 实测仍 **10** 条、`git diff` 里没有任何含 `lueprint` 的增删行）；**`v-html` 精确交代**：`grep -rn v-html Plugins/DesignSystem/web/src` 命中 6 处，全部是 M1/M2 既有 section 注入自己库里的 `svgBody`（`BrandAssets.vue`、`IconLibrary.vue`×2、`TokenShowcase.vue`×2 及其注释），**`Guidelines.vue` 零命中**——Forbidden 说的是"v-html 渲染规范正文"，正文是 `<textarea aria-label="规范正文">`，e2e G4 直接把这条钉成断言（`tagName === 'TEXTAREA'`）。**16:45 复验（V4 与 `ReleaseBoard.test.ts` 之后）**：两处新增都落在上述集合内（一个是 Allowed 名单里的既有 spec 文件，一个是插件 `web/src` 下的**新测试文件**，超出 04-task 对"新增文件"的枚举 → 已记 03-plan 偏差表 16:45 行）；Forbidden 逐路径 diff 再次为空（宿主 `ForgeSelf.Api`、`ForgeSelf.Web/src`、两个 `.csproj`、`package.json` 零改动），**未新增任何依赖**——`@vue/test-utils` 与 `jsdom` 在宿主 `ForgeSelf.Web/package.json:44,48` 早已在列，插件侧先例是 `showroom/Showroom.test.ts`；`ReleaseBoard.vue` 本身**未被改动**（只新增它的测试） | Verified |
| AC25 | 版本三常量与 `plugin.json` 均 `3.1.0`                                                                                                        | `grep` 四处同读 | **3.1.0 × 4**：`plugin.json.Version`、`ModelVersion`、`GeneratorVersion`、`ProjectionVersion` 全部同步；`Description` 补「UX 规范」。升前查过两件事：黄金基线只哈希 `TokenPatch` 层文本（不含版本串，不会因升版而红）、全仓测试**没有任何写死 `"3.0.0"` 的断言**（`DesignSystemAuthTests`/`ExportProjectionTests`/`DesignAgentToolTests` 都引用常量）。Generator 这次**必须**递增：生成链路现在会落 14 行规范（生成落库内容变了） | Verified |
| AC26 | 规范全文清单（14 条 × 3 种 kind）已写入下节并交用户审阅                                                                                      | `DS_DUMP_GUIDELINES=1` 产出 + 本文件下节 | **已产出并整篇嵌入**：42 个条目标题（14 条 × `console`/`marketing`/`product`），每条给 `code / 分类 / 标题 / 摘要 / 规则（级别+文本）/ 引用令牌 / 适用用途`，行业差异条目另列每份末尾；产出者是测试资产（`GuidelineGeneratorTests.清单产出器_仅DS_DUMP_GUIDELINES为1时写盘`，**默认路径直接 `return`、不含断言**——它只是产出器，不是守卫；清单的实质内容由 AC13 的 21 条参数化用例钉住），**不是手抄也不是临时脚本**。原件 39,165 字节落在 `.temp/ds-m3/guidelines-catalogue.md`（不入库），故全文嵌入本文件才算耐久副本。**措辞尚未交用户确认** → 本批只宣称"已产出、可改、改的是 `GuidelineGenerator` 模板与 `GeneratorVersion`"，不宣称"文案已通过"（V16 判定权在验收方/用户）。**20:39 复验「嵌入副本 == 当前生成器产出」**（这一条此前只写过"已嵌入"，没人验证过副本会不会漂）：`DS_DUMP_GUIDELINES=1 dotnet test --filter ~清单产出器`（1/1 通过，`guideline-dump-recheck.log`）重出原件 39,165 字节，与本节下方嵌入块**逐行比对：403 行 vs 403 行、忽略标题降级（`##`→`###`）后差异 0**。**给复验者的一个坑**：嵌入副本为了挂在本节下**整体降一级标题**，直接逐行 diff 会报出 389 处"差异"——那是我的排版，不是文案漂移，比对时必须先剥掉行首 `#+`。 | Verified（产出与嵌入 + **20:39 副本对账 0 差异**）/ **待用户审阅**（措辞） |

> **AC14–AC26 这张表是 10-03 收口时写的，10-04 的四类新事实不要拿它当终态**（终态一律以「接手与收尾」A–H 段为准）：① **AC23 的 `529 == 529`** 已被 **539 == 539 == 539 通过 / 0 失败**（16:0x，`logs/clean-filter-run.log`，= 529 + Biz 直查守卫 10）取代；② **AC23 的"e2e 全绿 33 passed"** 之后又跑过两轮（10-04 12:54 **33 passed**、16:16 **32 passed / 1 failed** + 单条复跑 **1 passed**），红因是 `SQLITE_BUSY`（G19 在串行轮现身 ⇒ README G19 已升级口径）；③ **AC24 的范围声明必须补枚举**——10-04 本会话又改了 `scripts/package-plugin.ps1`、`scripts/release/make-release-notes.ps1`、`scripts/verify-pilot-artifacts.ps1`（`.EXAMPLE` 注释）、`scripts/install-git-hooks.ps1`（同上）、`ForgeSelf.Api.Tests/RepositoryScriptTests.cs`（新守卫）、`ForgeSelf.Api.Tests/Plugins/DesignSystemTests/BizDirectQueryGuardTests.cs`（新增）、`docs/04-standards/agent-workflow.md`、`docs/04-standards/packaging-upgrade-backup.md`、第四个技能 `.agents/skills/plugin-publish-verify/SKILL.md`、`AGENTS.md`、`docs/07-decisions/not-taken-decisions.md`（028）；**判据本身没放宽**：Forbidden 名单里的 `ForgeSelf.Api/**`（宿主本体）、`ForgeSelf.Web/src/**`、两个 `.csproj`、`package.json` 到 18:0x 仍是**零 diff**（本轮实测见下方 ④），范围扩大的是"文档/脚本注释/测试"这三类，已同步记入 `03-plan.md` 偏差表；④ **AC24 那句"零改动"的复验口径**：本会话跑的是 `git status --porcelain` + 逐路径 diff（18:0x 重测读数见 07 §3），**不是**全量构建验证；宿主全量 `dotnet test`、宿主 `pnpm run check`、深档全量 e2e 按用户指令**仍未复跑**。

## 规范全文清单（AC26；交用户在闸门2 审阅措辞）

> 由实现方在步骤 7 之后导出：对 `console`、`marketing`、`product` 三种 kind（density 取 `default`、industry 取 `general`）各生成一份，逐条贴出 `code / 分类 / 标题 / 摘要 / 规则（级别+文本）/ 引用令牌`；再附 §G3 各 industry/density 的差异条目。**未经用户确认的措辞不得宣称"文案已通过"。**

| 项                        | 内容 |
| ------------------------- | ---- |
| 清单文件路径              | 本目录 `05-evidence.md` 下方「清单全文」（已整篇嵌入，随 git 入库）；原件由测试资产产出：`.temp/ds-m3/guidelines-catalogue.md`（39,165 字节，`DS_DUMP_GUIDELINES=1` 跑 `GuidelineGeneratorTests.清单产出器` 生成，**不是手抄**） |
| 交付用户的时间 / 回复位置 | 2026-10-03 04:40 随本批完成汇报交付（闸门2）；请审阅下方三种用途全文与 §G3 差异条目 |
| 用户反馈与处理            | **待用户回填**（闸门2）。未经用户确认，本批不宣称"文案已通过"——只宣称"文案已产出、措辞可改、改动只影响 `GuidelineGenerator` 模板与 `GeneratorVersion`" |

### 清单全文（14 条 × 3 种用途 kind；生成器 v1，density=default、industry=general 为正文，行业差异条目另列每份末尾）

> 由 `GuidelineGeneratorTests.清单产出器` 直接调生成器产出（不是手抄）。三种用途各一份全文，
> 行业专项条目（finance/healthcare/devtools/media/commerce/education）另列在每份末尾。
> 文本里只有**令牌路径**，具体数值由渲染端现查当前令牌（决策 D5）。

### 用途 kind = console（density=default）

#### 栅格与页面边距（`layout-grid` · 布局与栅格）

> 页面水平边距、列间距、断点全部取自已有的间距与断点令牌，密度改了它们就跟着改。

- **MUST** 页面水平边距取 `space.*` 档之一，本项目默认 `space.6`（随密度变化，具体值由令牌给）。
- **SHOULD** 栅格按十二列划分，列间距取 `space.4`，不自己算百分比。
- **MUST** 断点只用 `breakpoint.2` 到 `breakpoint.5` 这档已有的四档，不新增自定义断点值。
- **MAY** 控制台类以填满工作区为常态，不额外约束内容最大宽。

布局层只允许引用尺度令牌。任何一处把边距写成具体尺寸，都会在下一个密度档上失效。

- 引用令牌：`space.4`、`space.6`、`space.5`、`space.8`、`breakpoint.2`、`breakpoint.3`、`breakpoint.4`、`breakpoint.5`
- 适用用途：console/system/product

#### 层级与阴影（`elevation` · 布局与栅格）

> 卡片、浮层、对话框分别取固定的阴影层级，层叠顺序只用 z-index 令牌。

- **MUST** 卡片底用 `shadow.elevation-2`，不在组件里自写阴影值。
- **MUST** 菜单与浮层用 `shadow.elevation-3`，对话框用 `shadow.elevation-5`。
- **MUST** 层叠顺序只取 `z-index.*`，禁止写具体数值。
- **SHOULD** 同一种容器在整站只用一个阴影层级；要改就改令牌。

层级是「谁盖住谁」的约定，不是装饰：同一页出现两种卡片阴影，说明有人在自造层级。

- 引用令牌：`shadow.elevation-2`、`shadow.elevation-3`、`shadow.elevation-5`、`z-index.1`、`z-index.2`、`z-index.3`、`z-index.4`、`z-index.5`、`z-index.6`
- 适用用途：product/console/brand/marketing/system

#### 密度（`density` · 布局与栅格）

> 同一页面不混用多种密度；数据密集型页面取紧凑，阅读型取宽松。

- **MUST** 同一页面只用一种密度，不在局部把间距改回更松或更紧。
- **MUST** 所有间距取 `space.*` 档位；不接受组件内自报的尺寸值。
- **SHOULD** 数据密集页可整体切到紧凑主题，但必须整页切，不能只切一块。
- **SHOULD** 阅读型页面切到宽松档时，标题与正文的层级关系保持不变。

本项目的默认密度档是适中。间距只取 `space.*` 档位，不写绝对值。

- 引用令牌：`space.1`、`space.2`、`space.3`、`space.4`、`space.5`、`space.6`、`space.8`
- 适用用途：product/console/brand/marketing/system

#### 响应式（`responsive` · 布局与栅格）

> 控制台以桌面为主，窄屏下不得出现横向滚动。

- **MUST** 任一断点下页面不出现横向滚动条。
- **SHOULD** 断点取 `breakpoint.*`，与栅格条用同一组档。
- **MUST** 可点区域不小于 `component.button.sm.min-height`，触屏场景取 `component.button.lg.min-height`。
- **MUST** 控制台桌面优先：窄屏下次要信息可折叠，主任务必须仍可完成。

响应式的判据是「能不能看完」，不是「有没有断点」。

- 引用令牌：`breakpoint.2`、`breakpoint.3`、`breakpoint.4`、`breakpoint.5`、`component.button.sm.min-height`、`component.button.lg.min-height`
- 适用用途：console/system/product

#### 页面模式（`page-patterns` · 页面模式）

> 列表 / 详情 / 表单 / 仪表盘四类页面的结构固定

- **MUST** 新页面必须归入列表、详情、表单、仪表盘四类之一，说不归类的先别画。
- **MUST** 列表页顺序：标题区 → 筛选区 → 表格 → 批量操作，不自造新顺序。
- **SHOULD** 详情页顺序：标题与主操作 → 摘要信息 → 明细区块 → 关联记录。
- **SHOULD** 表单按区块分组，区块标题用 `type.h3`，字段间距见表单条。
- **MAY** 区块容器用 `component.card.*`，不自己拼底色与圆角。

控制台页面按四类模式套结构，区块顺序固定，换页面不等于换布局习惯。

- 引用令牌：`component.card.background`、`component.card.radius`、`component.card.padding`、`type.h3`
- 适用用途：console/system/product

#### 导航（`navigation` · 导航）

> 一级入口限量、当前项必须有非颜色指示、层级不超过三层。

- **MUST** 一级入口数量设上限，超出就归类而不是继续加项。
- **MUST** 当前项必须有指示条或字重差（`component.nav.indicator`），不得只靠颜色区分。
- **MUST** 层级不超过三层；到第三层还在加深时，改成一个列表页加详情页。
- **SHOULD** 控制台深页面给面包屑，用户能从当前位置退回上一级。
- **SHOULD** 移动端底部标签栏数量设上限，超出的收进「更多」。

导航解决的是「用户在不在」的问题：找不到入口的页面等于没有。

- 引用令牌：`component.nav.indicator`、`component.nav.item.foreground`、`component.nav.item.foreground-active`、`component.nav.item.background-active`
- 适用用途：console/system/product

#### 按钮层级（`buttons` · 表单与输入）

> 每屏一个主按钮；危险操作二次确认；禁用态不得只靠透明度。

- **MUST** 每屏至多一个主按钮（`component.button.primary.*`），其余用次要或文字按钮。
- **MUST** 不可逆操作只用危险按钮（`component.button.danger.*`），且必须二次确认并在确认框里写清后果。
- **SHOULD** 默认尺寸取 md 档；触屏为主的场景升一档，数据密集表格内降一档。
- **MUST** 禁用态用 `component.button.primary.background-disabled` 与 `component.button.primary.foreground-disabled` 两条真令牌，不得只调透明度。
- **SHOULD** 按钮文字用「动词 + 宾语」，不用「确定/提交」这类脱离上下文的词。

按钮层级是页面的语法：主按钮一多，用户就不知道该点哪个。

- 引用令牌：`component.button.primary.background`、`component.button.danger.background`、`component.button.sm.min-height`、`component.button.md.min-height`、`component.button.lg.min-height`、`component.button.primary.background-disabled`、`component.button.primary.foreground-disabled`
- 适用用途：product/console/brand/marketing/system

#### 表单（`forms` · 表单与输入）

> 标签在输入上方，失焦即校验，错误文案写「原因 + 怎么改」。

- **MUST** 标签置于输入框上方，不靠占位符当标签（输入后占位符消失，用户就忘了这一栏填什么）。
- **MUST** 必填项有非颜色标记（星号或文字），不只靠标签颜色区分。
- **MUST** 失焦即校验单字段；提交时汇总全部错误并把焦点移到第一个错误字段。
- **MUST** 错误文案含「问题 + 怎么改」；聚焦态用 `component.input.border-focus`，不只靠变色。
- **SHOULD** 字段垂直间距取 `space.3`（随密度），同一表单内保持一致。

表单是流失率最高的地方：校验与错误文案的质量直接决定用户能不能提交成功。

- 引用令牌：`component.input.border`、`component.input.border-focus`、`component.input.border-active`、`component.input.placeholder`、`component.input.background`、`space.3`、`semantic.danger`
- 适用用途：product/console/brand/marketing/system

#### 反馈与提示（`feedback` · 反馈与提示）

> 四类提示各有语义色，且一律配图标与文字；错误不自动消失。

- **MUST** 成功/警告/错误/信息各用对应语义色令牌，且必须同时给图标与文字。
- **MUST** 瞬时提示可自动消失；错误与需要用户处置的提示必须手动关闭。
- **SHOULD** 关键结果用页内反馈（就地显示在相关区块），不要只用一条浮层提示打发。
- **MUST** 任何失败都必须可见；请求失败却没有提示等于把错误吞掉。

反馈的判据是「用户看完知道下一步做什么」，不是「有没有弹提示」。

- 引用令牌：`semantic.success`、`semantic.warning`、`semantic.danger`、`semantic.info`、`component.badge.foreground`、`component.badge.tint`
- 适用用途：product/console/brand/marketing/system

#### 空 / 加载 / 错误态（`states` · 状态（空/加载/错误））

> 加载较慢给骨架，空态给下一步行动，错误态给原因与重试；禁止空白页。

- **MUST** 预计较慢时用骨架屏（取 `semantic.surface-2` 打底），不用全屏遮罩转圈。
- **MUST** 空态必须给出下一步行动入口；新用户的第一次进入就是空态。
- **MUST** 错误态含原因与重试入口，并区分「没数据」与「取失败」。
- **MUST** 任何路径都不得留下无提示的空白页。
- **SHOULD** 部分成功要显示部分结果与失败清单，不假装全量成功。

这三种状态最容易被漏掉：漏掉就是把「还没数据」显示成「坏了」。

- 引用令牌：`semantic.text-3`、`semantic.surface-2`、`semantic.surface-1`、`component.card.background`
- 适用用途：product/console/brand/marketing/system

#### 表格与图表（`data-display` · 数据展示）

> 表格用组件令牌，数字右对齐；图表系列色按序取且不只靠颜色区分。

- **MUST** 表头与行 hover 底色取 `component.table.*`，不自选灰色。
- **MUST** 数字列右对齐，位数不齐时用等宽字体（`font.mono`）。
- **MUST** 图表系列色按 `chart.series-1` 起的顺序取，且不只靠颜色区分系列（配图标或文字标注）。
- **SHOULD** 百分比与金额由数据层格式化，视图层不自己算占比。

数据展示的目标是能被核对：对齐、单位、来源三样都要看得见。

- 引用令牌：`component.table.border`、`font.mono`、`chart.series-1`、`chart.series-2`、`chart.series-3`、`chart.series-4`、`chart.series-5`、`chart.series-6`、`chart.series-7`、`chart.series-8`
- 适用用途：product/console/brand/marketing/system

#### 文案与术语（`content` · 文案与术语）

> 语气随用途；按钮用动词短语；术语全站一致；不留占位文案。

- **MUST** 语气平实、面向操作：先给结论，再给细节；不用营销词。
- **MUST** 按钮与链接用「动词 + 宾语」，同一动作全站同一个词。
- **MUST** 同一概念全站用同一个词（术语在库里，改名要一起改）。
- **MUST** 不得留「待补充/TBD」这类占位文案上线。

文案是界面的一部分，不是最后贴上去的皮：写不清通常说明功能还没想清。

- 引用令牌：`type.h1`、`type.h2`、`type.h3`、`type.h4`、`type.body`、`breakpoint.4`
- 适用用途：console/system/product

#### 可达性（`a11y` · 可达性）

> 对比度、焦点可见、目标尺寸、键盘可达、不只靠颜色——五条都由审计与界面共同保证。

- **MUST** 正文对比度不低于 4.5:1、大字与非文本不低于 3:1，由可达性审计强制，不靠人眼判断。
- **MUST** 键盘焦点必须可见，焦点环取 `component.focus.*`（宽度与偏移都来自令牌）。
- **MUST** 交互目标不小于 `component.button.sm.min-height`。
- **MUST** 全部核心路径键盘可达，模态不得锁死键盘（不得有键盘陷阱）。
- **MUST** 状态区分不得只依赖颜色：另有图标、文字或形状。

可达性不是加分项。WCAG 2.2 是这些规则的出处，本插件的审计门禁负责把它变成数据。

- 引用令牌：`component.focus.outline-width`、`component.focus.outline-offset`、`component.focus.outline-color`、`component.button.sm.min-height`、`type.body`、`semantic.text-1`、`semantic.surface-1`
- 适用用途：product/console/brand/marketing/system

#### 动效（`motion` · 动效）

> 时长只用 duration 档、缓动只用 ease 档；减少动效偏好下用 reduced 档；禁无限循环抢注意力。

- **MUST** 时长只取 `duration.micro`、`duration.base`、`duration.macro`、`duration.emphasized` 四档，不写毫秒字面量。
- **MUST** 缓动只取 `ease.*` 令牌；进入用 `ease.decelerate`，退出用 `ease.accelerate`。
- **MUST** 尊重系统偏好：减少动效时用 `duration.base-reduced` 等 reduced 档替代。
- **SHOULD** 无限循环动画不得抢占注意力；提示性动效播完即停。
- **MAY** 动效要表达方向或因果（从哪来、到哪去），纯装饰性动效优先删掉。

动效的判据是「能不能帮用户理解变化」，帮不上的动效就是延迟。

- 引用令牌：`duration.micro`、`duration.base`、`duration.macro`、`duration.emphasized`、`duration.micro-reduced`、`duration.base-reduced`、`duration.macro-reduced`、`duration.emphasized-reduced`、`ease.standard`、`ease.decelerate`、`ease.accelerate`、`ease.emphasized`
- 适用用途：product/console/brand/marketing/system

### 用途 kind = marketing（density=default）

#### 栅格与页面边距（`layout-grid` · 布局与栅格）

> 页面水平边距、列间距、断点全部取自已有的间距与断点令牌，密度改了它们就跟着改。

- **MUST** 页面水平边距取 `space.*` 档之一，本项目默认 `space.6`（随密度变化，具体值由令牌给）。
- **SHOULD** 栅格按十二列划分，列间距取 `space.4`，不自己算百分比。
- **MUST** 断点只用 `breakpoint.2` 到 `breakpoint.5` 这档已有的四档，不新增自定义断点值。
- **MAY** 内容型长文页的最大宽度取 `breakpoint.4`，超宽屏靠两侧留白而不是拉宽行。
- **SHOULD** 窄屏优先：先排最小断点下的顺序，再逐级放宽。

布局层只允许引用尺度令牌。任何一处把边距写成具体尺寸，都会在下一个密度档上失效。

- 引用令牌：`space.4`、`space.6`、`space.5`、`space.8`、`breakpoint.2`、`breakpoint.3`、`breakpoint.4`、`breakpoint.5`
- 适用用途：marketing/brand

#### 层级与阴影（`elevation` · 布局与栅格）

> 卡片、浮层、对话框分别取固定的阴影层级，层叠顺序只用 z-index 令牌。

- **MUST** 卡片底用 `shadow.elevation-2`，不在组件里自写阴影值。
- **MUST** 菜单与浮层用 `shadow.elevation-3`，对话框用 `shadow.elevation-5`。
- **MUST** 层叠顺序只取 `z-index.*`，禁止写具体数值。
- **SHOULD** 同一种容器在整站只用一个阴影层级；要改就改令牌。

层级是「谁盖住谁」的约定，不是装饰：同一页出现两种卡片阴影，说明有人在自造层级。

- 引用令牌：`shadow.elevation-2`、`shadow.elevation-3`、`shadow.elevation-5`、`z-index.1`、`z-index.2`、`z-index.3`、`z-index.4`、`z-index.5`、`z-index.6`
- 适用用途：product/console/brand/marketing/system

#### 密度（`density` · 布局与栅格）

> 同一页面不混用多种密度；数据密集型页面取紧凑，阅读型取宽松。

- **MUST** 同一页面只用一种密度，不在局部把间距改回更松或更紧。
- **MUST** 所有间距取 `space.*` 档位；不接受组件内自报的尺寸值。
- **SHOULD** 数据密集页可整体切到紧凑主题，但必须整页切，不能只切一块。
- **SHOULD** 阅读型页面切到宽松档时，标题与正文的层级关系保持不变。

本项目的默认密度档是适中。间距只取 `space.*` 档位，不写绝对值。

- 引用令牌：`space.1`、`space.2`、`space.3`、`space.4`、`space.5`、`space.6`、`space.8`
- 适用用途：product/console/brand/marketing/system

#### 响应式（`responsive` · 布局与栅格）

> 营销与品牌页移动优先；控制台桌面优先但必须能在窄屏看完关键信息。

- **MUST** 任一断点下页面不出现横向滚动条。
- **SHOULD** 断点取 `breakpoint.*`，与栅格条用同一组档。
- **MUST** 可点区域不小于 `component.button.sm.min-height`，触屏场景取 `component.button.lg.min-height`。
- **MUST** 移动优先：先排窄屏顺序，再逐级放宽。

响应式的判据是「能不能看完」，不是「有没有断点」。

- 引用令牌：`breakpoint.2`、`breakpoint.3`、`breakpoint.4`、`breakpoint.5`、`component.button.sm.min-height`、`component.button.lg.min-height`
- 适用用途：marketing/brand

#### 页面模式（`page-patterns` · 页面模式）

> 首屏 / 特性 / 定价 / 页脚四段式

- **MUST** 首屏只放一个主张与一个主行动按钮。
- **SHOULD** 顺序：首屏 → 特性 → 佐证 → 定价 → 页脚，缺段的按空段跳过而不是硬凑。
- **MUST** 页脚必须有可达的联系方式与法务入口，不放装饰性链接。
- **SHOULD** 正文段落宽度受 `breakpoint.4` 约束，超宽屏用两侧留白。
- **MAY** 内容型站点另设文章与详情页模板，不复用落地页结构。

品牌与营销页按阅读节奏分段，每段只承担一个主张。

- 引用令牌：`component.card.background`、`component.card.radius`、`component.card.padding`、`type.h3`
- 适用用途：marketing/brand

#### 导航（`navigation` · 导航）

> 一级入口限量、当前项必须有非颜色指示、层级不超过三层。

- **MUST** 一级入口数量设上限，超出就归类而不是继续加项。
- **MUST** 当前项必须有指示条或字重差（`component.nav.indicator`），不得只靠颜色区分。
- **MUST** 层级不超过三层；到第三层还在加深时，改成一个列表页加详情页。
- **SHOULD** 站点层级较深时给面包屑或分类入口。
- **SHOULD** 移动端顶栏收纳为抽屉或底部标签，标签数不超过五个。

导航解决的是「用户在不在」的问题：找不到入口的页面等于没有。

- 引用令牌：`component.nav.indicator`、`component.nav.item.foreground`、`component.nav.item.foreground-active`、`component.nav.item.background-active`
- 适用用途：marketing/brand

#### 按钮层级（`buttons` · 表单与输入）

> 每屏一个主按钮；危险操作二次确认；禁用态不得只靠透明度。

- **MUST** 每屏至多一个主按钮（`component.button.primary.*`），其余用次要或文字按钮。
- **MUST** 不可逆操作只用危险按钮（`component.button.danger.*`），且必须二次确认并在确认框里写清后果。
- **SHOULD** 默认尺寸取 md 档；触屏为主的场景升一档，数据密集表格内降一档。
- **MUST** 禁用态用 `component.button.primary.background-disabled` 与 `component.button.primary.foreground-disabled` 两条真令牌，不得只调透明度。
- **SHOULD** 按钮文字用「动词 + 宾语」，不用「确定/提交」这类脱离上下文的词。

按钮层级是页面的语法：主按钮一多，用户就不知道该点哪个。

- 引用令牌：`component.button.primary.background`、`component.button.danger.background`、`component.button.sm.min-height`、`component.button.md.min-height`、`component.button.lg.min-height`、`component.button.primary.background-disabled`、`component.button.primary.foreground-disabled`
- 适用用途：product/console/brand/marketing/system

#### 表单（`forms` · 表单与输入）

> 标签在输入上方，失焦即校验，错误文案写「原因 + 怎么改」。

- **MUST** 标签置于输入框上方，不靠占位符当标签（输入后占位符消失，用户就忘了这一栏填什么）。
- **MUST** 必填项有非颜色标记（星号或文字），不只靠标签颜色区分。
- **MUST** 失焦即校验单字段；提交时汇总全部错误并把焦点移到第一个错误字段。
- **MUST** 错误文案含「问题 + 怎么改」；聚焦态用 `component.input.border-focus`，不只靠变色。
- **SHOULD** 字段垂直间距取 `space.3`（随密度），同一表单内保持一致。

表单是流失率最高的地方：校验与错误文案的质量直接决定用户能不能提交成功。

- 引用令牌：`component.input.border`、`component.input.border-focus`、`component.input.border-active`、`component.input.placeholder`、`component.input.background`、`space.3`、`semantic.danger`
- 适用用途：product/console/brand/marketing/system

#### 反馈与提示（`feedback` · 反馈与提示）

> 四类提示各有语义色，且一律配图标与文字；错误不自动消失。

- **MUST** 成功/警告/错误/信息各用对应语义色令牌，且必须同时给图标与文字。
- **MUST** 瞬时提示可自动消失；错误与需要用户处置的提示必须手动关闭。
- **SHOULD** 关键结果用页内反馈（就地显示在相关区块），不要只用一条浮层提示打发。
- **MUST** 任何失败都必须可见；请求失败却没有提示等于把错误吞掉。

反馈的判据是「用户看完知道下一步做什么」，不是「有没有弹提示」。

- 引用令牌：`semantic.success`、`semantic.warning`、`semantic.danger`、`semantic.info`、`component.badge.foreground`、`component.badge.tint`
- 适用用途：product/console/brand/marketing/system

#### 空 / 加载 / 错误态（`states` · 状态（空/加载/错误））

> 加载较慢给骨架，空态给下一步行动，错误态给原因与重试；禁止空白页。

- **MUST** 预计较慢时用骨架屏（取 `semantic.surface-2` 打底），不用全屏遮罩转圈。
- **MUST** 空态必须给出下一步行动入口；新用户的第一次进入就是空态。
- **MUST** 错误态含原因与重试入口，并区分「没数据」与「取失败」。
- **MUST** 任何路径都不得留下无提示的空白页。
- **SHOULD** 部分成功要显示部分结果与失败清单，不假装全量成功。

这三种状态最容易被漏掉：漏掉就是把「还没数据」显示成「坏了」。

- 引用令牌：`semantic.text-3`、`semantic.surface-2`、`semantic.surface-1`、`component.card.background`
- 适用用途：product/console/brand/marketing/system

#### 表格与图表（`data-display` · 数据展示）

> 表格用组件令牌，数字右对齐；图表系列色按序取且不只靠颜色区分。

- **MUST** 表头与行 hover 底色取 `component.table.*`，不自选灰色。
- **MUST** 数字列右对齐，位数不齐时用等宽字体（`font.mono`）。
- **MUST** 图表系列色按 `chart.series-1` 起的顺序取，且不只靠颜色区分系列（配图标或文字标注）。
- **SHOULD** 百分比与金额由数据层格式化，视图层不自己算占比。

数据展示的目标是能被核对：对齐、单位、来源三样都要看得见。

- 引用令牌：`component.table.border`、`font.mono`、`chart.series-1`、`chart.series-2`、`chart.series-3`、`chart.series-4`、`chart.series-5`、`chart.series-6`、`chart.series-7`、`chart.series-8`
- 适用用途：product/console/brand/marketing/system

#### 文案与术语（`content` · 文案与术语）

> 语气随用途；按钮用动词短语；术语全站一致；不留占位文案。

- **MUST** 语气有感染力但不夸大；一个段落只说一件事。
- **MUST** 按钮与链接用「动词 + 宾语」，同一动作全站同一个词。
- **MUST** 同一概念全站用同一个词（术语在库里，改名要一起改）。
- **MUST** 不得留「待补充/TBD」这类占位文案上线。

文案是界面的一部分，不是最后贴上去的皮：写不清通常说明功能还没想清。

- 引用令牌：`type.h1`、`type.h2`、`type.h3`、`type.h4`、`type.body`、`breakpoint.4`
- 适用用途：marketing/brand

#### 可达性（`a11y` · 可达性）

> 对比度、焦点可见、目标尺寸、键盘可达、不只靠颜色——五条都由审计与界面共同保证。

- **MUST** 正文对比度不低于 4.5:1、大字与非文本不低于 3:1，由可达性审计强制，不靠人眼判断。
- **MUST** 键盘焦点必须可见，焦点环取 `component.focus.*`（宽度与偏移都来自令牌）。
- **MUST** 交互目标不小于 `component.button.sm.min-height`。
- **MUST** 全部核心路径键盘可达，模态不得锁死键盘（不得有键盘陷阱）。
- **MUST** 状态区分不得只依赖颜色：另有图标、文字或形状。

可达性不是加分项。WCAG 2.2 是这些规则的出处，本插件的审计门禁负责把它变成数据。

- 引用令牌：`component.focus.outline-width`、`component.focus.outline-offset`、`component.focus.outline-color`、`component.button.sm.min-height`、`type.body`、`semantic.text-1`、`semantic.surface-1`
- 适用用途：product/console/brand/marketing/system

#### 动效（`motion` · 动效）

> 时长只用 duration 档、缓动只用 ease 档；减少动效偏好下用 reduced 档；禁无限循环抢注意力。

- **MUST** 时长只取 `duration.micro`、`duration.base`、`duration.macro`、`duration.emphasized` 四档，不写毫秒字面量。
- **MUST** 缓动只取 `ease.*` 令牌；进入用 `ease.decelerate`，退出用 `ease.accelerate`。
- **MUST** 尊重系统偏好：减少动效时用 `duration.base-reduced` 等 reduced 档替代。
- **SHOULD** 无限循环动画不得抢占注意力；提示性动效播完即停。
- **MAY** 动效要表达方向或因果（从哪来、到哪去），纯装饰性动效优先删掉。

动效的判据是「能不能帮用户理解变化」，帮不上的动效就是延迟。

- 引用令牌：`duration.micro`、`duration.base`、`duration.macro`、`duration.emphasized`、`duration.micro-reduced`、`duration.base-reduced`、`duration.macro-reduced`、`duration.emphasized-reduced`、`ease.standard`、`ease.decelerate`、`ease.accelerate`、`ease.emphasized`
- 适用用途：product/console/brand/marketing/system

### 用途 kind = product（density=default）

#### 栅格与页面边距（`layout-grid` · 布局与栅格）

> 页面水平边距、列间距、断点全部取自已有的间距与断点令牌，密度改了它们就跟着改。

- **MUST** 页面水平边距取 `space.*` 档之一，本项目默认 `space.6`（随密度变化，具体值由令牌给）。
- **SHOULD** 栅格按十二列划分，列间距取 `space.4`，不自己算百分比。
- **MUST** 断点只用 `breakpoint.2` 到 `breakpoint.5` 这档已有的四档，不新增自定义断点值。
- **MAY** 控制台类以填满工作区为常态，不额外约束内容最大宽。
- **SHOULD** 窄屏优先：先排最小断点下的顺序，再逐级放宽。

布局层只允许引用尺度令牌。任何一处把边距写成具体尺寸，都会在下一个密度档上失效。

- 引用令牌：`space.4`、`space.6`、`space.5`、`space.8`、`breakpoint.2`、`breakpoint.3`、`breakpoint.4`、`breakpoint.5`
- 适用用途：product/console/system

#### 层级与阴影（`elevation` · 布局与栅格）

> 卡片、浮层、对话框分别取固定的阴影层级，层叠顺序只用 z-index 令牌。

- **MUST** 卡片底用 `shadow.elevation-2`，不在组件里自写阴影值。
- **MUST** 菜单与浮层用 `shadow.elevation-3`，对话框用 `shadow.elevation-5`。
- **MUST** 层叠顺序只取 `z-index.*`，禁止写具体数值。
- **SHOULD** 同一种容器在整站只用一个阴影层级；要改就改令牌。

层级是「谁盖住谁」的约定，不是装饰：同一页出现两种卡片阴影，说明有人在自造层级。

- 引用令牌：`shadow.elevation-2`、`shadow.elevation-3`、`shadow.elevation-5`、`z-index.1`、`z-index.2`、`z-index.3`、`z-index.4`、`z-index.5`、`z-index.6`
- 适用用途：product/console/brand/marketing/system

#### 密度（`density` · 布局与栅格）

> 同一页面不混用多种密度；数据密集型页面取紧凑，阅读型取宽松。

- **MUST** 同一页面只用一种密度，不在局部把间距改回更松或更紧。
- **MUST** 所有间距取 `space.*` 档位；不接受组件内自报的尺寸值。
- **SHOULD** 数据密集页可整体切到紧凑主题，但必须整页切，不能只切一块。
- **SHOULD** 阅读型页面切到宽松档时，标题与正文的层级关系保持不变。

本项目的默认密度档是适中。间距只取 `space.*` 档位，不写绝对值。

- 引用令牌：`space.1`、`space.2`、`space.3`、`space.4`、`space.5`、`space.6`、`space.8`
- 适用用途：product/console/brand/marketing/system

#### 响应式（`responsive` · 布局与栅格）

> 产品页移动优先；任何断点都不出现横向滚动。

- **MUST** 任一断点下页面不出现横向滚动条。
- **SHOULD** 断点取 `breakpoint.*`，与栅格条用同一组档。
- **MUST** 可点区域不小于 `component.button.sm.min-height`，触屏场景取 `component.button.lg.min-height`。
- **MUST** 移动优先：先排窄屏顺序，再逐级放宽。

响应式的判据是「能不能看完」，不是「有没有断点」。

- 引用令牌：`breakpoint.2`、`breakpoint.3`、`breakpoint.4`、`breakpoint.5`、`component.button.sm.min-height`、`component.button.lg.min-height`
- 适用用途：product/console/system

#### 页面模式（`page-patterns` · 页面模式）

> 标题区 → 主体 → 操作区

- **MUST** 页面自上而下是标题区、主体、操作区，操作区不藏进滚动深处。
- **MUST** 每屏只有一个主行动，其余降为次要按钮（按钮层级条已约束）。
- **SHOULD** 新用户的空态就是第一屏，按状态条给行动而不是留白。
- **MAY** 移动优先时主操作落到底部可达区域。

产品页按三段式组织：先说清这是什么，再给内容，最后给动作。

- 引用令牌：`component.card.background`、`component.card.radius`、`component.card.padding`、`type.h3`
- 适用用途：product/console/system

#### 导航（`navigation` · 导航）

> 一级入口限量、当前项必须有非颜色指示、层级不超过三层。

- **MUST** 一级入口数量设上限，超出就归类而不是继续加项。
- **MUST** 当前项必须有指示条或字重差（`component.nav.indicator`），不得只靠颜色区分。
- **MUST** 层级不超过三层；到第三层还在加深时，改成一个列表页加详情页。
- **SHOULD** 站点层级较深时给面包屑或分类入口。
- **SHOULD** 移动端底部标签栏数量设上限，超出的收进「更多」。

导航解决的是「用户在不在」的问题：找不到入口的页面等于没有。

- 引用令牌：`component.nav.indicator`、`component.nav.item.foreground`、`component.nav.item.foreground-active`、`component.nav.item.background-active`
- 适用用途：product/console/system

#### 按钮层级（`buttons` · 表单与输入）

> 每屏一个主按钮；危险操作二次确认；禁用态不得只靠透明度。

- **MUST** 每屏至多一个主按钮（`component.button.primary.*`），其余用次要或文字按钮。
- **MUST** 不可逆操作只用危险按钮（`component.button.danger.*`），且必须二次确认并在确认框里写清后果。
- **SHOULD** 默认尺寸取 md 档；触屏为主的场景升一档，数据密集表格内降一档。
- **MUST** 禁用态用 `component.button.primary.background-disabled` 与 `component.button.primary.foreground-disabled` 两条真令牌，不得只调透明度。
- **SHOULD** 按钮文字用「动词 + 宾语」，不用「确定/提交」这类脱离上下文的词。

按钮层级是页面的语法：主按钮一多，用户就不知道该点哪个。

- 引用令牌：`component.button.primary.background`、`component.button.danger.background`、`component.button.sm.min-height`、`component.button.md.min-height`、`component.button.lg.min-height`、`component.button.primary.background-disabled`、`component.button.primary.foreground-disabled`
- 适用用途：product/console/brand/marketing/system

#### 表单（`forms` · 表单与输入）

> 标签在输入上方，失焦即校验，错误文案写「原因 + 怎么改」。

- **MUST** 标签置于输入框上方，不靠占位符当标签（输入后占位符消失，用户就忘了这一栏填什么）。
- **MUST** 必填项有非颜色标记（星号或文字），不只靠标签颜色区分。
- **MUST** 失焦即校验单字段；提交时汇总全部错误并把焦点移到第一个错误字段。
- **MUST** 错误文案含「问题 + 怎么改」；聚焦态用 `component.input.border-focus`，不只靠变色。
- **SHOULD** 字段垂直间距取 `space.3`（随密度），同一表单内保持一致。

表单是流失率最高的地方：校验与错误文案的质量直接决定用户能不能提交成功。

- 引用令牌：`component.input.border`、`component.input.border-focus`、`component.input.border-active`、`component.input.placeholder`、`component.input.background`、`space.3`、`semantic.danger`
- 适用用途：product/console/brand/marketing/system

#### 反馈与提示（`feedback` · 反馈与提示）

> 四类提示各有语义色，且一律配图标与文字；错误不自动消失。

- **MUST** 成功/警告/错误/信息各用对应语义色令牌，且必须同时给图标与文字。
- **MUST** 瞬时提示可自动消失；错误与需要用户处置的提示必须手动关闭。
- **SHOULD** 关键结果用页内反馈（就地显示在相关区块），不要只用一条浮层提示打发。
- **MUST** 任何失败都必须可见；请求失败却没有提示等于把错误吞掉。

反馈的判据是「用户看完知道下一步做什么」，不是「有没有弹提示」。

- 引用令牌：`semantic.success`、`semantic.warning`、`semantic.danger`、`semantic.info`、`component.badge.foreground`、`component.badge.tint`
- 适用用途：product/console/brand/marketing/system

#### 空 / 加载 / 错误态（`states` · 状态（空/加载/错误））

> 加载较慢给骨架，空态给下一步行动，错误态给原因与重试；禁止空白页。

- **MUST** 预计较慢时用骨架屏（取 `semantic.surface-2` 打底），不用全屏遮罩转圈。
- **MUST** 空态必须给出下一步行动入口；新用户的第一次进入就是空态。
- **MUST** 错误态含原因与重试入口，并区分「没数据」与「取失败」。
- **MUST** 任何路径都不得留下无提示的空白页。
- **SHOULD** 部分成功要显示部分结果与失败清单，不假装全量成功。

这三种状态最容易被漏掉：漏掉就是把「还没数据」显示成「坏了」。

- 引用令牌：`semantic.text-3`、`semantic.surface-2`、`semantic.surface-1`、`component.card.background`
- 适用用途：product/console/brand/marketing/system

#### 表格与图表（`data-display` · 数据展示）

> 表格用组件令牌，数字右对齐；图表系列色按序取且不只靠颜色区分。

- **MUST** 表头与行 hover 底色取 `component.table.*`，不自选灰色。
- **MUST** 数字列右对齐，位数不齐时用等宽字体（`font.mono`）。
- **MUST** 图表系列色按 `chart.series-1` 起的顺序取，且不只靠颜色区分系列（配图标或文字标注）。
- **SHOULD** 百分比与金额由数据层格式化，视图层不自己算占比。

数据展示的目标是能被核对：对齐、单位、来源三样都要看得见。

- 引用令牌：`component.table.border`、`font.mono`、`chart.series-1`、`chart.series-2`、`chart.series-3`、`chart.series-4`、`chart.series-5`、`chart.series-6`、`chart.series-7`、`chart.series-8`
- 适用用途：product/console/brand/marketing/system

#### 文案与术语（`content` · 文案与术语）

> 语气随用途；按钮用动词短语；术语全站一致；不留占位文案。

- **MUST** 语气面向使用者：说清能做什么，不堆术语。
- **MUST** 按钮与链接用「动词 + 宾语」，同一动作全站同一个词。
- **MUST** 同一概念全站用同一个词（术语在库里，改名要一起改）。
- **MUST** 不得留「待补充/TBD」这类占位文案上线。

文案是界面的一部分，不是最后贴上去的皮：写不清通常说明功能还没想清。

- 引用令牌：`type.h1`、`type.h2`、`type.h3`、`type.h4`、`type.body`、`breakpoint.4`
- 适用用途：product/console/system

#### 可达性（`a11y` · 可达性）

> 对比度、焦点可见、目标尺寸、键盘可达、不只靠颜色——五条都由审计与界面共同保证。

- **MUST** 正文对比度不低于 4.5:1、大字与非文本不低于 3:1，由可达性审计强制，不靠人眼判断。
- **MUST** 键盘焦点必须可见，焦点环取 `component.focus.*`（宽度与偏移都来自令牌）。
- **MUST** 交互目标不小于 `component.button.sm.min-height`。
- **MUST** 全部核心路径键盘可达，模态不得锁死键盘（不得有键盘陷阱）。
- **MUST** 状态区分不得只依赖颜色：另有图标、文字或形状。

可达性不是加分项。WCAG 2.2 是这些规则的出处，本插件的审计门禁负责把它变成数据。

- 引用令牌：`component.focus.outline-width`、`component.focus.outline-offset`、`component.focus.outline-color`、`component.button.sm.min-height`、`type.body`、`semantic.text-1`、`semantic.surface-1`
- 适用用途：product/console/brand/marketing/system

#### 动效（`motion` · 动效）

> 时长只用 duration 档、缓动只用 ease 档；减少动效偏好下用 reduced 档；禁无限循环抢注意力。

- **MUST** 时长只取 `duration.micro`、`duration.base`、`duration.macro`、`duration.emphasized` 四档，不写毫秒字面量。
- **MUST** 缓动只取 `ease.*` 令牌；进入用 `ease.decelerate`，退出用 `ease.accelerate`。
- **MUST** 尊重系统偏好：减少动效时用 `duration.base-reduced` 等 reduced 档替代。
- **SHOULD** 无限循环动画不得抢占注意力；提示性动效播完即停。
- **MAY** 动效要表达方向或因果（从哪来、到哪去），纯装饰性动效优先删掉。

动效的判据是「能不能帮用户理解变化」，帮不上的动效就是延迟。

- 引用令牌：`duration.micro`、`duration.base`、`duration.macro`、`duration.emphasized`、`duration.micro-reduced`、`duration.base-reduced`、`duration.macro-reduced`、`duration.emphasized-reduced`、`ease.standard`、`ease.decelerate`、`ease.accelerate`、`ease.emphasized`
- 适用用途：product/console/brand/marketing/system

## 公式调整记录（03 §A3「系数可微调」；无调整写「无」）

| 轴 / 风格 | 原系数 | 新系数 | 原因（审计 critical 摘要 / 读图结论） |
| --------- | ------ | ------ | ------------------------------------- |
| 预设 `flat-minimal` 的品牌彩度（`chroma`，非轴取值名） | 0.05 | **0.08** | 0.05 时"生成→审计"出 **1 条 critical**：`component.badge.foreground`（semantic.success）对它自己的 `badge.tint` 底只有 **3.72:1**，达不到 TextNormal 的 4.5:1——极低彩度的绿太浅，垫 15% 自己后与白底几乎分不开。由 `StyleAxisAuditTests.AC7_十三个预设_生成落库后审计零critical` 抓到（Verified）。轴的**取值名与性格词一字未动**，属 03 §P/§D 允许的 ±0.03 数值微调；同时它是 `flat` 阴影 + 极低彩度的组合，改彩度不影响"扁平"这一性格 |
| 七条轴（`shadowStyle/shadowStrength/borderStrength/neutralTemp/fontPairing/radiusStyle/accentStrategy`）的 §A3–A9 系数 | — | **无调整** | 全部按 03 原系数实现；默认值逐字节兼容由 50 项黄金基线守到（任何系数漂移当场变红），无需也不允许"看图微调" |

## Build

Command:

```bash
dotnet build Plugins/DesignSystem/DesignSystem.csproj --no-incremental   # 必须全量重建：源码未变时增量构建是 no-op，直接报「0 个警告」
```

Result: **PASS**（来源等级 **Verified**，2026-10-03 04:54）

```text
DesignSystem -> Plugins\DesignSystem\bin\Debug\net10.0\DesignSystem.dll
已成功生成。
    0 个警告
    0 个错误
已用时间 00:00:06.26
```

## Unit Test

Command:

```bash
# 环境：TMP/TEMP 固定到仓内目录（宿主 XCode/SQLite 写盘口径）
# V1 诚实边界：chcp 65001 我**没有单独执行**（本轮全部命令跑在 Git Bash 里）。V1 的实际目的——UTF-8 输出——
#   由落盘日志逐条可读承担（下面贴的原文含中文用例名与报错），复验方若在 cmd/PowerShell 下复跑请按 V1 显式 chcp。
TMP=.temp/ds-m1/tmp TEMP=.temp/ds-m1/tmp dotnet test ForgeSelf.Api.Tests \
  --filter "FullyQualifiedName~DesignSystem" --logger "console;verbosity=normal"
TMP=.temp/ds-m1/tmp TEMP=.temp/ds-m1/tmp dotnet test ForgeSelf.Api.Tests \
  --filter "FullyQualifiedName~DesignSystem" --no-build --list-tests
```

Result: **PASS**（来源等级 **Verified**，**权威跑 2026-10-03 20:12**）；总数 / 发现数 / 失败数：**529 / 529 / 0**（通过数 529，跳过 0）。上一轮权威跑是 19:07 的 **519 / 519 / 0**，被 20:12 取代；**增量账**（每一步都是当场真跑，不是推算）：513 →（+1 `GuidelineExportTests.V10_改令牌值…` 单令牌版）→ **514**（17:59）→（+3 `StyleAxisTests.V6_阴影公式的结构不随输入改变`）→ **517**（18:15）→（−1 单令牌版 + 2 三令牌版与手写版）→ **518**（18:32）→（+1 `AC18_脏规则行_级别为null…` 守卫，并因两处生产码净改动整体重跑）→ **519**（19:07）→（**+10** 反向覆盖审计补的三格：`StyleAxisPerformanceTests` 9 条 = 1 条生成器阈值 + 8 条轴耗时比值；`StyleAxisAuditTests.Boundary_editorial才登记display字族_默认档不造假登记` 1 条）→ **529**（20:12，终态）。

```text
# .temp/ds-m1/logs/filter-run-perf-b.log（verbose logger，7.0638 min，20:12 —— 终态权威轮）
测试总数: 529
     通过数: 529
# .temp/ds-m1/logs/list-tests-perf.log → grep -c "DesignSystemTests\." = 529（发现数 == 报告数）
# 交叉核对：grep -c '^  已通过 ' = 529；grep -c '^  失败 ' = 0
# 按类核对（list-tests-perf.log）：StyleAxisTests 53、StyleAxisPerformanceTests 9、StyleAxisAuditTests 4（原 3 + 新 1）、
#   GuidelineExportTests 12、GuidelineToolTests 9、StyleAxisGoldenTests 4
# 本轮**没有生产码改动**（只新增/修改测试码）⇒ 插件 DLL 与门禁④ 走查过的 `2.3.0.2610031922` 内二进制同源
# 上一轮（19:07 / 519，filter-run-terral.log）降为历史：那一轮的构建实测（--no-incremental）是 0 个错误 / 370 个警告

# .temp/ds-m1/logs/filter-run-final-518.log（7.5378 min，18:32 —— 上一轮，留作历史）
测试总数: 518
     通过数: 518

# .temp/ds-m1/logs/filter-run-v6.log（verbose logger，7.6545 min，17:21 —— 上一轮，保留作历史）
     通过数: 513
# .temp/ds-m1/logs/list-tests-v6.log → grep -c "DesignSystemTests\." = 513

# .temp/ds-m1/logs/filter-run-final.log（verbose logger，7.1062 min，06:20 —— 更早一轮，保留作历史）
测试总数: 509
     通过数: 509
# .temp/ds-m1/logs/list-tests-final.log → grep -c "DesignSystemTests\." = 509
```

本轮新增/改动的用例数对账（与 03 §Test Plan 对齐，全部实际跑过）：`GuidelineSchemaTests` 5、`GuidelineUpgradeTests` 3、
`GuidelineServiceTests` 10、`GuidelineRestTests` 8、`GuidelineExportTests` **12**（18:20 由 10 增：两条 V10）、`GuidelineReleaseTests` 6、`GuidelineToolTests` 8、
`GuidelineGeneratorTests` 21（含常驻反向探针与 `DS_DUMP_GUIDELINES` 清单产出器）、`StyleAxis*Tests` / `StylePresetAxisTests`（A 片）。
开工基线 362（M2 交付时）→ 现 **529**（终态 20:12；`StyleAxisTests` 单类 **53** 条，含 V6 两族反作弊：路径集合 4 组 + 结构 3 组）；**没有任何用例被删除或 `Skip`**（收口时按 V6 的字面要求实测：全目录 `grep -rn "Skip *=\|Skip\]\|\[Ignore\|Ignore\]"` **零命中**）。两条"开关式"用例（V6 明文豁免的录制器一类）**默认路径并不等价，此处逐条更正**：① `StyleAxisGoldenTests.录制器_仅DS_RECORD_GOLDEN为1时重录基线` 默认路径**有真断言** —— `Baseline.Count == Cases().Sum(层数)`，防"用例加了、基线没录"（`StyleAxisGoldenTests.cs:165-170`）；② `GuidelineGeneratorTests.清单产出器_仅DS_DUMP_GUIDELINES为1时写盘` 默认路径是**裸 `return;`，没有任何断言**（`GuidelineGeneratorTests.cs:265`）—— 此前本行把两者一起写成"默认路径仍执行'不该写盘'的断言"，**对②是夸大**，现按代码实况更正。②的实质内容（14 codes × 3 kind、MUST≥1、引用令牌存在）由 AC13 的参数化用例钉住，不靠这个产出器。

跑过七次、每次都记真数（缺陷 #8 修复前后 + 批 C 收口 + V6 反作弊两族 + V10 补段各一次）：
① 05:24 `506 / 506 / 0`（`.temp/ds-m1/logs/ds-filter-m3-gate.log`）；同一次跑之前还有一轮 **505 通过 / 1 失败**——
红的正是 `DesignBriefTests.E1`（夹具漏挂 `Guidelines`，见 03-plan 04:38 偏差行），修好后转绿；
② 05:36 `508 / 508 / 0`（新增 `PreviewCssTests.AC3_阴影强度0_投影必须是transparent_而不是实心色` 与
`QuickCreateServiceTests.Create_只带轴的半份请求_轴必须落到库里令牌` 之后）。
③ 06:20 `509 / 509 / 0`：再加一条 `QuickCreateServiceTests.Create_带轴项目_导出CSS整族齐全且形状随轴变`
（批 C 一次不可复现红逼出来的"整族齐全"判据，见「E2E · 全目录回归」红账 4 与 Unresolved Issues #3）。
④ **17:21 `513 / 513 / 0`**（此后被 ⑤⑥⑦ 取代，留作历史轮次）：在 ③ 之上只多一类 —— `StyleAxisTests.V6_轴改动的路径集合不随输入改变`（4 组参数化，见下条 V6 反作弊），
`--list-tests` 独立发现数 **513**（`list-tests-v6.log` 计数），报告数 == 发现数 == 通过数；日志 `filter-run-v6.log`（`通过数: 513`，`总时间: 7.6545 分钟`）。
⑤ **17:59 `514 / 514 / 0`**（`filter-run-v10.log` + `list-tests-v10.log`，`总时间: 19.4413 分钟`）：在 ④ 之上加 `GuidelineExportTests.V10_改令牌值_规范渲染的数跟着变…`（单令牌版）。
**耗时 19.44 分钟不是用例变慢**：同期 `tasklist` 实测有多个并行 `dotnet` 进程在场（另一会话在跑它自己的门禁），我**没杀任何进程**，只是等。下一轮 ⑦ 无争抢时回到 7.54 分钟，可作对照。
⑥ **18:15 `517 / 517 / 0`**（`filter-run-final-517.log` + `list-tests-final-517.log`，`总时间: 10.4044 分钟`）：在 ⑤ 之上加 `StyleAxisTests.V6_阴影公式的结构不随输入改变`（crisp/flat/layered 三组参数化，见 AC3/AC5/AC6 行的"结构不随输入改变"）。
⑦ **18:32 `518 / 518 / 0`**（此后被 ⑧ 取代，留作历史轮次）：在 ⑥ 之上把 V10 的单令牌版**换成**三令牌 × 三交付物版，并**新增**手写规范不被误拒那条（−1 + 2 ⇒ 518）；
`--list-tests` 独立发现数 **518**（`list-tests-final-518.log`），按类核对 `StyleAxisTests` 53 / `GuidelineExportTests` 12 / `StyleAxisGoldenTests` 4；日志 `filter-run-final-518.log`（`通过数: 518`，`总时间: 7.5378 分钟`）。
⑧ **19:07 `519 / 519 / 0`**（此后被 ⑨ 取代，留作历史轮次）：在 ⑦ 之上加 `GuidelineToolTests.AC18_脏规则行_级别为null_checklist不抛且回落SHOULD`（1 条），
且这一轮是**在删掉 `DesignGuideline.Biz.cs` 的死字段 `MaxCacheCount` 之后重建的二进制**上跑的（`--no-incremental`：0 错误 / 370 警告）；
发现数 **519**（`list-tests-terral.log`），按类核对 `StyleAxisTests` 53 / `GuidelineExportTests` 12 / `GuidelineToolTests` 9 / `StyleAxisGoldenTests` 4；
`filter-run-terral.log`（`通过数: 519`，`总时间: 7.6199 分钟`）。**注**：19:05 我还改过一次 `DesignGuideline.Biz.cs` 的**注释措辞**（把「没有缓存分支」更正为「阈值被内联成字面量 1000」），
注释不改 IL ⇒ 不构成新的净改动，但重新打包时会一并带上，产物按最终源码构建。
⑨ **20:12 `529 / 529 / 0`（终态，权威）**：来源不是新功能，而是**02-spec 的反向覆盖审计**（见「规格反向覆盖审计」节）——
补 `StyleAxisPerformanceTests` 9 条（NFR 性能实测）+ `StyleAxisAuditTests.Boundary_editorial才登记display字族_默认档不造假登记` 1 条；
发现数 **529**（`list-tests-perf.log`），按类核对 `StyleAxisTests` 53 / `StyleAxisPerformanceTests` 9 / `StyleAxisAuditTests` 4 / `GuidelineExportTests` 12 / `GuidelineToolTests` 9 / `StyleAxisGoldenTests` 4；
`filter-run-perf-b.log`（`通过数: 529`，`总时间: 7.0638 分钟`）。**本轮零生产码改动**（只动测试码）⇒ 插件 DLL 与门禁④ 走查过的包内二进制同源。
**核对方法记一笔**：终态日志 `grep -c '^  已通过 '` = **529**、`grep -c '^  失败 '` = **0**；另有一类假信号要认得——全日志 `grep -c 错误` = **8**，逐条看**全部是 `已通过` 行里的用例名**（如 `NearestTokenFinderTests.空输入_给明确错误`、`DesignSystemStoreTests.悬空别名被拒_且错误文案指出缺哪个路径`），`grep 错误 | grep -v 已通过` 结果为**空**，所以既不能当成失败、也不该当成通过依据（判成败只数 `^  已通过 ` / `^  失败 ` 两种行首）。
定向复跑（缺陷 #8 的影响面）：`--filter ~PreviewCssTests|~ExportProjectionTests|~StyleAxis` **100/100**（`shadow-alpha-fix.log`）；
新用例单类复跑 `--filter ~QuickCreateServiceTests` **19/19**（`quickcreate-css-test.log`，18.38s）；
V10 补段期间单类复跑 `--filter ~GuidelineExportTests` **12/12**（`v10-three-forms.log` 18:19 / 探针还原后 `v10-three-forms-restored.log` 18:23），中途两轮探针轮分别 **11 通过 / 1 失败**（`v10-probe-bundle-old.log`）与 **10 通过 / 2 失败**（`v10-probe-brief-and-manualguard.log`）——**红是探针，不是回归**。

### 规格反向覆盖审计（20:00→20:15 · 「AC 全绿」不等于「spec 全文覆盖」）

**为什么单独做这一节**：AC1–AC26 是我自己写的判据，逐行 Verified 只证明「写了编号的东西做到了」。02-spec 的 **Business Rules / Boundary Conditions / Error Handling / Non-functional Requirements** 四节**没有 AC 编号**，正是整节漏测最容易发生的地方——本批实测就从里面捞出四条无人测过的判据（其中 NFR 那条是规格自己写着「实测记 Evidence」却一次没测）。

**做法**：把那四节逐条抄成清单，对每一条只问一句「05 里有没有一个能真打开的落点（用例名 / e2e 名 / 日志 / 代码行）」，没有就当场补，补不了就登记。

| spec 条目 | 审计前状态 | 现在 | 证据落点 |
|---|---|---|---|
| NFR·性能：非默认轴 ≤ 默认 1.5 倍、`GuidelineGenerator` < 50ms，**实测记 Evidence** | **0 处证据**（只有实现） | 常驻 9 条 + 真实数字 | `StyleAxisPerformanceTests`（1 条生成器阈值 + 8 条轴耗时比值），实测见下 |
| Boundary·`font.display` 存在才登记 `role=display` | 只有令牌侧/产物侧（AC3/AC4），品牌库那一格无人测 | 已补 | `StyleAxisAuditTests.Boundary_editorial才登记display字族_默认档不造假登记`（含"默认档确实没有该令牌"的前提自证 + sans 行在场作反证） |
| Boundary·存量项目无规范 → 界面空态引导 | 只有实现（`Guidelines.vue:347`） | 已补 e2e | `design-system-guidelines.spec.ts` **G5**（空态出现 → 点生成 → 空态消失且界面数 == REST 数） |
| Boundary·窄屏第 15 区列表/详情纵向堆叠 | **0 处证据** | 已补 e2e | **G6**（820px：`.gl__cols` 轨道数 == 1、详情顶 ≥ 列表底、本区横向溢出 0px） |
| Error Handling·`SQLITE BUSY` 读重试 / 写绝不重试 | **未按规格实现** | **本批不改，登记** | 全仓 `grep BUSY` 只有两处注释记录过它把请求打成 500（`AuditRepository.cs:48`、`ReleaseService.cs:107`），实际缓解是**发布串行化**；"读重试/写不重试"属全站读路径语义 → README **G19** + TODO P2 |
| Unknown·Stardust 第 11 类 / 审计类别 `guideline-ref` | 裁决"不加" | 不变，且有机判据钉住 | AC24（`ComponentBlueprints` 实测 10、`entities===10`、`AuditKinds.All` 零 diff） |

**逐条清点（全 29 条，不是抽样）**：上面那张表只列了"本轮有动作的格子"，容易被读成"审计=那六行"。这里把 02-spec 四节的**每一条**都过一遍，落点全部取**真实用例名**（`grep` 自 `ForgeSelf.Api.Tests/Plugins/DesignSystemTests/`，不是回忆）：

| # | spec 条目 | 落点（用例 / e2e / 工件） | 状态 |
|---|---|---|---|
| BR1 | 默认轴逐字节兼容（黄金哈希钉） | `StyleAxisGoldenTests.AC1_基线集合_默认轴产物哈希逐一相等`、`AC1_种子串_默认路径逐字不变` | 已有（AC1/AC2 行） |
| BR2 | 每轴真改产物 + 白名单外不变 + 各主题 0 critical | `StyleAxisTests`（53 条，含 V6 两族反作弊）+ `StyleAxisAuditTests.AC4_轴取值矩阵_三基础请求四主题_审计零critical` | 已有 |
| BR3 | 规范只写路径、数字由**同一个函数**现查；令牌失效可见不静默 | `GuidelineGeneratorTests.AC13_引用的令牌全部真存在_不存在的被剔掉而不是留着骗人` + `GuidelineRestTests.AC15_坏引用逐条点名并标注_不静默` + `GuidelineExportTests.V10_抽三个令牌改值_三种交付物里现查的数跟着变…` | 已有 |
| BR4 | 手改保护（`overwrite` 才覆盖且点名） | `GuidelineServiceTests.AC14_手改行受重新生成保护_内容一字不变`、`AC14_overwrite_逐条点名被覆盖的code` | 已有 |
| BR5 | 只写不删 | `GuidelineSchemaTests.AC12_控制器没有删除路由` + e2e G2（归档可恢复、不硬删） | 已有 |
| BR6 | 升级安全（旧库只加表、hash 不变、不回填） | `GuidelineUpgradeTests.AC12_重复初始化不动旧行也不回填规范` + `GuidelineReleaseTests.AC17_无规范项目_重发幂等且hash不受规范层影响` | 已有 |
| BR7 | 同源与模特契约对 13 预设 | `PreviewCssTests.AC2_同源_落库导出_等于_内存预览_全预设_明暗` + `MannequinVariableContractTests.AC4_模特引用的每个变量…`（含"悬空引用必须被判缺"反向探针） | 已有 |
| BR8 | 规范文案是产品内容，交用户审 | AC26 行 + 本节下方全文（**20:39 副本对账 0 差异**） | 已有，待用户 |
| BR9 | 工具仍 ≤8；写开关对 guideline 生效 | `GuidelineToolTests.AC18_工具总数仍是8_动作与类别枚举含规范`、`AC18_edit_guideline_过写开关_关闭时零写入` | 已有 |
| B1 | 存量项目无规范：不开章 / 界面空态 / checklist 回落静态基线 | `GuidelineToolTests.AC18_context_sections_guidelines_回紧凑章_空项目不开章`、`AC18_checklist_派生条目_id与级别映射_tokens只含真存在的路径`（静态基线未缩水）+ **e2e G5**（本轮补） | 两条已有，**界面那档本轮才补** |
| B2 | `kind` 未知回落 `product`；`industry` 不可得回落 `general` | `GuidelineGeneratorTests.AC13_未知kind与industry有回落不抛`（`GuidelineGeneratorTests.cs:246`）+ `PreviewCssTests.AC3_未知industry_回落general_不抛` | **用例早就有，但 05 此前从未引用**（`grep 未知kind与industry` 在 05 命中 0）⇒ 本轮登记在此，属**引用缺失不是能力缺失** |
| B3 | `tokenPaths` 缺某引用 → 引用被剔、规则保留 | `AC13_引用的令牌全部真存在_不存在的被剔掉而不是留着骗人` | 已有 |
| B4 | `font.display` 存在才登记 `role=display` | **本轮补**：`StyleAxisAuditTests.Boundary_editorial才登记display字族_默认档不造假登记` | 已补 |
| B5 | 并发：`expectUpdatedAt` 不一致 → 409 且不写 | `AC14_乐观并发_旧updatedAt写入被拒` + `AC15_code不存在回404_并发冲突回409_项目不存在沿用控制器既有异常口径` | 已有 |
| B6 | 规模上限（200 条 / Body 4000 / rules 30） | `AC14_非法词表与超限_文案给出可用值` + `AC15_校验失败回400并把可用值写进文案` | 已有 |
| B7 | 窄屏第 15 区纵向堆叠 | **本轮补**：e2e **G6**（computed 轨道数 / 几何 / 溢出） | 已补 |
| E1 | 风格轴取值非法 → 400 带词表 | `StyleAxisTests.AC5_*`（词表与校验）+ `PreviewCssTests.AC3_非法数值参数_入口拒绝_抛ArgumentException映射400` | 已有 |
| E2 | `tokenRefs` 含不存在路径 → 400 逐条列出、零写入 | `AC14_引用不存在的令牌_逐条列出且零行写入` | 已有 |
| E3 | `level`/`category`/`code` 非法 → 400 给合法值 | `AC15_校验失败回400并把可用值写进文案` + `AC18_edit_guideline_不合形入参_错误文案指出具体值` | 已有 |
| E4 | `expectUpdatedAt` 过期 → 409 + 最新值 | 同 B5 | 已有 |
| E5 | 超限 → 400 + 上限 | 同 B6 | 已有 |
| E6 | `generate` 遇手改 → 跳过并在响应列出 | 同 BR4（`SkippedProtected` 三处引用） | 已有 |
| E7 | 旧快照 diff 缺 guideline → `NotComparableKinds`，界面说"不可比" | `AC17_schema2与schema3互比_报不可比而不报新增`、`AC17_diff出参带notComparableKinds_界面才有话可说` + `ReleaseBoard.test.ts` 三条 | 已有 |
| E8 | `SQLITE BUSY` 读重试 / 写绝不重试 | **未按规格实现**（只有两处注释 + 发布串行化） | **登记未做**：README G19 / TODO P2 |
| N1 | 确定性（无随机无时钟） | `AC13_确定性_同输入两次逐字相同` + `PreviewCssTests.AC3_确定性_同请求两次_CSS相同` + `StyleAxisTests.AC6_*` | 已有 |
| N2 | 性能（≤1.5×、<50ms，实测记 Evidence） | **本轮补**：`StyleAxisPerformanceTests` 9 条 + 实测数字（0.86–1.15×；p95 0.010ms） | 已补 |
| N3 | 安全：正文文本节点、类级鉴权 + 写开关、不接受服务器路径输入 | `AC12_类级鉴权未削弱` + `AC18_edit_guideline_过写开关_关闭时零写入` + e2e G4 的 `tagName === 'TEXTAREA'`；**"路径输入"这一档**：规范入参只有 `GuidelinePatch` 的文本字段（无路径类参数），所以这条**没有可测面**，如实标注为"结构上不成立"而非"已测" | 已有 + 一条按实态说明 |
| N4 | 可维护：词表单点、取值函数单点 | `vocabulary.test.ts`（界面不许再存一份）+ `AC15_meta供给规范词表与能力位_前端不抄第二份` + V10 三段对账（brief/design-md/bundle 同一个函数） | 已有 |
| N5 | 新增代码 0 warning | 「Static Analysis」节：`--no-incremental` + **按文件归因**（372→371→370，其中 368 条在生成物） | 已有（口径见自查表 #59） |

**清点结论**：29 条里 **24 条早已有落点**（其中 1 条只是没被引用：B2）、**4 条本轮补成常驻判据**（B1 界面档、B4、B7、N2）、**1 条测不出来只能登记**（E8）、**1 条按实态标注为"无可测面"**（N3 的路径输入档）。**没有一条被"改松判据"处理掉**。

**29 条之外还漏了一面（X1）：spec 自己没写、但「产物文本」向外部客户端作出的承诺**。`DesignTools.cs` 里 `design_guide` 的 `discovery` 字段原文是「外部 MCP 客户端请先 `universal_tool {"tool":"list_tools","parameters":{"keyword":"design"}}` 枚举，再按名调用 design_* 工具」，`design-system-consume` 技能也把 MCP 网关列为三大消费入口之一。审计这面的过程（每步都是 `grep`/读码实测，不是回忆）：

| 这条链的跳 | 审计前有没有断言 | 实据 |
|---|---|---|
| ① 工具对象本身的行为（schema、封套、写开关） | **有**，12 条 `GuidelineToolTests` + `DesignAgentToolTests` 若干 | 但它们全部是 `new DesignGuideTool(kit)` **直构对象**（`DesignAgentToolTests.cs:21-28`、`GuidelineToolTests.cs:107-108`），一次都没经过宿主注册表 |
| ② 界面工具表 == REST `agent/tools` == `meta.agentTools` | **有**（不是缺口） | `design-system-showroom.spec.ts:726` **C3** 三条 `toEqual` 已经在比这三者 ⇒ 我一度想写"此前零证据"，`grep agent/tools` 命中 C3 后按实态收窄成"缺注册表那一跳" |
| ③ 插件把 8 件交给宿主 `IToolRegistry` | **只有日志** | 机制是 `DesignSystemPlugin.cs:86-93` 填 `ToolExtensions` → 宿主 `ExtensionPointManager.cs:174/238` 收集；全仓 `grep` 只有 `ForgeSelf.Api.Tests/Plugins/DesignSystemTests/` 里出现 `design_` 前缀（`grep -rn design_ ForgeSelf.Api.Tests` 去掉该目录后 **0 命中**；McpCenterTests 的 **13 个** `.cs` 文件 `grep -l design_` **0 个**）。运行时证据是 e2e 隔离实例日志 20:18:24「设计系统插件已注册 8 个工具函数」 |
| ④ 网关按名把 `design_guide` 真执行出来 | **没有** | `UniversalToolForwarderTests` 用的是 `Mock<IToolRegistry>`，证明的是"按名分发"这件**通用**行为（含 unknown tool 预检），不证明 design_* 在里面 |

⇒ 补 **e2e G7**（`design-system-guidelines.spec.ts`）：`GET /api/mcp-center/config` 取**被测实例自己的** `listenUrl`（不硬编码端口，`playwright.config.ts:39` 已按 worktree hash 钉 `FORGESELF_MCP_GATEWAY_PORT`）→ `tools/list` 必须只有 `universal_tool` → `list_tools keyword=design` 按 `pluginId` 归因后的名单**必须等于** `meta.agentTools` → 经网关调 `design_guide` 的 `version/tools/workflows/discovery` 逐条核对 → 两条反向腿。全程只读、不建数据。读数与红账见「E2E · 网关链 G7」节。

**FR 段也要逐条回读（22:40 补，这是反向审计的第三遍，前两遍都没覆盖它）**：#65 那两遍扫的是 02-spec **没有 AC 编号的四节**（BR/B/E/N），Function Requirements 一节当时**只当作"AC 的来源"没有单独清点**。机械核对（脚本 `grep` 编号，不靠记忆）结果是：17 条 FR 里 **12 条在 03/04/05 三件里连编号都没出现过**（FR2/3/4/7/8/9/10/11/12/14/15/17）——不代表没做没测，而是**映射没有留痕**，验收方无法照 FR 复验。下表把每条 FR 钉到**真实存在的落点**（类名/方法名逐条回源码 `grep` 过，文件名回 `ls` 过）：

| FR | 要求要点 | 落点（真实用例 / 文件 / e2e） | AC |
|---|---|---|---|
| FR1 | 请求侧 7 个可空轴字段、强度越界**夹取并写 Note**、JSON camelCase | `StyleAxes`；`StyleAxisTests.AC5_阴影强度越界_夹取且写进Notes`（断 `Math.Clamp` 后的值 + `Notes` 含"已夹取"，不静默） | AC5 |
| FR2 | 每轴只改白名单内前缀且必须真改产物 | `StyleAxisTests`（12 条参数化 + 3 条公式 + `V6_轴改动的路径集合不随输入改变` 4 组 + `V6_阴影公式的结构不随输入改变` 3 组）、`StyleAxisAuditTests.AC4_轴取值矩阵_三基础请求四主题_审计零critical` | AC3/AC4 |
| FR3 | 不传新字段时产物逐字节兼容（含 `GeneratorSeed`） | `StyleAxisGoldenTests.AC1_基线集合_默认轴产物哈希逐一相等`、`AC2_同请求两次_逐字节相同`（黄金基线 50 键常驻比对） | AC1/AC2 |
| FR4 | 预设 8 → 13 且原 8 一字不变、每件新预设"真生效" | `StylePresetsTests`、`StylePresetAxisTests.AC7_预设目录里新预设的轴取值_必须真改产物而非摆设`、`AC7_覆盖矩阵_每个非默认轴取值至少被一个预设用到` | AC7/AC8 |
| FR5 | M1 面只增不改（`design_create`/`regenerate`/`quick-create` 参数、`design_presets` 出参 `request`） | `DesignAgentToolContractTests` 与 `DesignAgentToolBehaviorTests`（同文件 25 条 Fact/Theory）、`QuickCreateServiceTests`、`GenerateShapeTests` | AC9 |
| FR6 | 「更多风格选项」全部由 `meta.styleAxes` 渲染、取值不在 TS 抄第二份 | `web/src/design/vocabulary.test.ts`（字面量守卫，实测会红）、`web/src/showroom/tune.ts`（映射）、`web/src/sections/Guidelines.vue`；界面档 e2e **V4**（轴面板 + 衣柜） | AC10/AC21 |
| FR7 | `DesignGuideline` 表（唯一结构变更）、生成物不手改、`EntityTypes` 登记 | `GuidelineSchemaTests`（`:32-34` 断 `EntityTypes` 含 `typeof(DesignGuideline)` 且 `HaveCount(13)`；另有反射断"无 DELETE 路由"）、`GuidelineUpgradeTests`、xcode 二次生成零漂移（本节上方「开工复核」第 4 行） | AC12 |
| FR8 | `GuidelineGenerator` 纯函数：14 条、确定性、MUST≥1、引用真存在、数字守卫 | `GuidelineGeneratorTests`（21 条，含 `AC13_确定性_同输入两次逐字相同`、`AC13_引用的令牌全部真存在_不存在的被剔掉而不是留着骗人`、`反向探针_数字守卫必须响`） | AC13 |
| FR9 | 仓储/服务：只补空、`manual` 受保护、`overwrite` 点名、归档参与判重 | `GuidelineServiceTests`（10 条）、`GenerateShapeTests`（`Run` 第二次带回 14 而非 0） | AC14 |
| FR10 | REST 五端点 + `status`/`category` 过滤 + `brokenRefs` + 校验文案给可用值 | `GuidelineRestTests`（8 条）、`GuidelineSchemaTests`（meta 词表同源） | AC15 |
| FR11 | 导出：brief 章、design-md 章、bundle 两文件 + Manifest、值现查 | `GuidelineExportTests`（12 条，含 V10 三段：brief 行内括注 / design-md / bundle `tokenValues`） | AC16 |
| FR12 | 快照 `CurrentSchema=3`、`Kind=guideline` 进 hash、旧快照可读、互比报"不可比" | `GuidelineReleaseTests`（6 条）、`ReleaseSnapshotTests`（17 条零回归）、`web/src/sections/ReleaseBoard.test.ts`（3 条：该出现/不该出现/schema 1 走另一条） | AC17 |
| FR13 | 工具只增：`design_edit action=guideline`、`design_lookup kind=guideline`、`design_context sections=guidelines`、checklist 派生、**`design_guide` 的 guideline 工作流与 discovery 文案** | `GuidelineToolTests`（9 条，含脏规则行回落）、`DesignAgentTool*`；**e2e G7**（经网关断到 `workflows=consume/create/maintain/guideline` 与 `discovery` 指向 `universal_tool`） | AC18 |
| FR14 | 界面第 15 个 section：列表/过滤/编辑/生成/归档、正文不走 `v-html`、chip 值同源 | `web/src/shell/nav.ts`（第 15 项带 `capability: 'guidelines'`）、`sections/Guidelines.vue`；e2e **G1–G6**（G4 断 `tagName === 'TEXTAREA'` 与全程零写入、G5 空态引导、G6 窄屏堆叠） | AC19/AC20 |
| FR15 | 生成链路：`SeedGuidelines` 只补空、`generate` 响应增 `guidelines` 键、`quick-create` 带轴 | `GuidelineServiceTests`（第二次 created 空 / skipped 14 / 条数不翻倍）、`GenerateShapeTests`、`QuickCreateServiceTests`（轴参数进库；缺陷 #1 quick-create 丢轴即由它 + e2e 逼出） | AC14 |
| FR16 | 视觉 QA：预设 × 场景 × 明暗逐张读图，每轴非默认取值肉眼可辨 | e2e `design-system-style.spec.ts` V 片 **59 张**（V1 26 / V2 10 / V3 20 / V4 3）+ 「Screenshots」节逐张结论；三条机器判据（对比度、注入==交付 CSS、变量变化） | AC21 |
| FR17 | 版本三元组 + `plugin.json` 升 3.1.0；README / ROADMAP / 036 / 两个技能同步 | `grep` 四处同读（AC25）；文档面 AC22 行（含 22:11 那条"三条→四条工作流"的枚举漂移更正） | AC22/AC25 |

**这一遍的结论**：17 条 FR **全部有真实落点，没有一条空转**；缺的是**映射留痕**而不是能力——与 BR/B/E/N 那遍"存在 ≠ 被指到"是同一族（#65 的口径），只是这次漏的是**有编号的那一节**。⇒ 立为技能口径：**反向审计要按"spec 的每一节"做，不能因为某节有 AC 编号就假定它已被覆盖**（AC 是判据列表，FR 是需求列表，两者不是一对一）。

**性能实测（20:12 轮，产出文件 `.temp/ds-m1/logs/perf-m3-measured.txt`，`DS_DUMP_PERF=1`）**：默认档生成 p50 **1.19–1.57ms**；八条非默认取值相对默认档的耗时比值 **0.86×–1.15×**，全部 ≤ 规格阈值 1.5×（`shadowStrength=2` 0.86×、`shadowStyle=crisp` 1.15×、`layered` 0.94×、`radiusStyle=pill` 1.02×、`neutralTemp=warm` 0.89×、`accentStrategy=triadic` 1.05×、`borderStrength=bold` 1.00×、`fontPairing=editorial` 1.02×）；`GuidelineGenerator`（35 条路径 → 14 条规范）p50 **0.009ms** / p95 **0.010ms**，阈值 50ms。取样纪律：warm-up 3 轮 + 25 轮**交替**取样取中位数——交替是这条紧阈值能常驻不假红的前提（本机常有并行会话，见自查表 #54）。

**这三条新判据能不能红（不是自夸，是跑出来的）**：19:51 首跑 `filter-run-perf.log:642` 就是红的——`NFR_非默认轴生成耗时…(axis: "fontPairing", value: "editorial")` 失败，原因**在我写错的守卫**：我断言两侧令牌条数必须相等，而规格 FR/AC3 自己规定 `editorial` 会**多产一条** `font.display`（报错原文给了数：`388 → 389`）。改成 `|Δ条数| ≤ 2` 并把这条合法增量的出处写进注释后转绿（`filter-run-perf-b.log` 529/529）。⇒ 守卫对"产物工作量真的变了"是有反应的，且这条反应被真跑证明过一次。

**顺带抓到的三条（都不是产品缺陷，但都会咬人、咬测试、咬验收）**：
1. **e2e 侧**：`pickProject` 原来用裸前缀 `new RegExp(code)` 找行。G5 建了 `<code>-bare` 之后同一前缀**命中两行** ⇒ `row.isVisible()` 在严格模式下抛错、被 `catch` 吞成 `false` ⇒ 轮询永远等不到（G6 首跑 1 failed 的真实原因，快照 `.pw-out-ds5/…/error-context.md` 里两行明摆在）。已改锚定匹配 `^code(?![\w-])`。同一形状的坑对用户也成立：**项目标识互为前缀时，任何"按前缀找行"的自动化都会踩**。
2. **界面侧**：G5 读图看到零令牌项目生成规范后立刻出现「断链 4 / 断链 3」红徽标。先核代码再下结论：`DesignSystemController.cs:519-522` 的 `brokenRefs` 除 `TokenRefsJson` 外，还会把**规则文本里反引号包住的令牌路径**（`GuidelineRenderer.ConcreteRefs`）计入 ⇒ 引用 `space.4` 而项目一个令牌都没有，标断链正是规格要的"不静默"，**不是缺陷**。但界面没告诉用户"这个项目还没生成过令牌"，一排红徽标会被读成"规范坏了" → 记 README **G20**（P3）。
3. **副本侧（同一批审计里顺手做的第三条）**：AC26 的规范全文是**嵌进 05 的耐久副本**，此前只宣称"已嵌入"，没人验证过副本会不会漂。20:39 用 `DS_DUMP_GUIDELINES=1` 重跑产出器（1/1，`guideline-dump-recheck.log`）后逐行对账：**403 行 vs 403 行、剥掉标题降级后差异 0** ⇒ 用户在闸门2 读到的文案就是当前生成器产出的文案。附带一条复验须知：嵌入副本整体降了一级标题，**直接 diff 会假报 389 处差异**（我第一次就撞了这个假报），比对必须先剥行首 `#+`。同一手法也该用在黄金基线那份副本上（它由 `DS_RECORD_GOLDEN=1` 录制、由 `StyleAxisGoldenTests` 每次跑比对，已经是常驻守卫，不需要人工对账——差别在于**有没有一条会自动响的判据**，副本类判据要往这个方向做）。

### V12 契约面：工具 `parametersSchema` 是否"只增不改"（17:53 源码级实测，命令可复跑）

06 的 V12 要"与 M1 对表：既有键全在、仅新增"。M3 未提交，所以**基线就是 `897d10d`（M2 提交）**，直接对表：

```bash
for f in DesignToolIndex.cs DesignTools.cs DesignToolBase.cs DesignToolKit.cs; do git show 897d10d:Plugins/DesignSystem/Agent/$f; done \
  | grep -o '\\"[A-Za-z]*\\":{' | sed 's/\\":{//;s/\\"//' | sort -u          # 旧：53 个键
grep -o '\\"[A-Za-z]*\\":{' Plugins/DesignSystem/Agent/*.cs | sed 's/\\":{//;s/\\"//' | sort -u   # 新：53 个键
comm -23 旧 新   # 消失的键 → 空
comm -13 旧 新   # 新增的键 → 空
diff <(git show 897d10d:…/DesignToolIndex.cs | grep -o '\\"required\\":\[[^]]*\]' | sort -u) \
     <(grep -o '\\"required\\":\[[^]]*\]' …/DesignToolIndex.cs | sort -u)     # → 空（逐字相同）
```

- **实测结论（Verified，源码级）**：手写 schema 串里的属性键 **53 → 53，既没消失也没新增**；四个工具的 `required` 数组**逐字相同**（没有给既有调用方加必填参数）。
- **M3 的增量到底长什么样**：① **枚举值就地扩**——`design_edit.action` 的 enum 从 `[set_token,regenerate,publish]` 变成 `[set_token,regenerate,publish,guideline]`（`DesignToolIndex.cs:50`）；② **键由运行时拼接**——`+ GuidelineCategories.SchemaProperties() + StyleAxes.SchemaProperties()`，所以**源码 grep 看不到这些键**。
- **因此本条的边界要说清**：上面那次对表只覆盖"**手写部分**不回归"；运行时拼出来的那部分（7 条轴 + 规范参数）的"既有键全在 + 枚举逐字等于后端词表"由 `StyleAxisTests.AC9_工具schema的轴属性与StyleAxes同源` 与 `GuidelineToolTests`（AC18，8 条，含"总数仍 8"）在**产出后的 schema** 上判。两层合起来才是 V12 的完整答案，不接受"grep 了一下就算契约没变"。

### V10 三段对账（18:20–18:24 · 完成度自审补做，不是新增功能）

06 的 V10 一格写了**三段**判据。18:15 我做完成度自审时逐条回读原文，发现自己只落了第一段的一半，于是当场补齐并把这段账留在这里——**证据宽度不足比缺证据更危险，因为它会让人以为已经测过**。

| V10 的分段（06 原文口径） | 落点 | 实测 |
|---|---|---|
| ①「随机抽 **3** 个令牌改值……导出 **brief / design-md / bundle** 的规范文本里该处括注的值同步变化且规范模板文本零改动」 | `GuidelineExportTests.V10_抽三个令牌改值_三种交付物里现查的数跟着变_规范文本一字不动` | **12/12 通过**（`v10-three-forms.log` 18:19 / 还原后 `v10-three-forms-restored.log` 18:23）。抽样口径写死在代码里：被规范引用 + 共享层 px 型 + **当前交付里就以这个共享值出**（第三条专防我自己造假红：主题层有覆盖时改共享值本就不该变交付）；实际抽到的第一个是 `breakpoint.2`（768px → 2304px）。三条出口分别判：design-md 的 `引用令牌：path＝值` 行、bundle 的 `tokenValues[path]`、brief 紧凑章的行内 `（值）`；`briefChecked>0` 防"三个都没进紧凑章"的空转；末尾 `Fingerprint().Should().Equal(beforeText)` 判规范文本零改动（含 `UpdatedAt`） |
| ②「对生成器产出的 14 条默认规范（3 种 kind）自行跑一遍正则 `\d+(\.\d+)?(px\|rem\|em\|ms\|s)` 与 `#hex`（WCAG 比值与列表序号除外）」 | **早就存在**：`GuidelineGeneratorTests.AC13_数字守卫_全部生成文本零命中` + `反向探针_数字守卫必须响` | 扫描面比 V10 要求的宽：**3 kind × 3 density × 7 industry**、`texts.Count > 300` 显式防空转；反向探针四条正例（`16px`/`1.5em`/`200ms`/`#0f172a`）+ 一条排除例（`4.5:1`、`3:1` 不报）。**这一段的性质是"登记缺失"**：能力在 05 里记在 AC13 名下，没映射到 V10，所以自审时看起来像"没做"。已在 AC13 行补对号 |
| ③「手写规范（`source=manual`）不受数字守卫约束……只确认其被原样保存与渲染」 | `GuidelineExportTests.V10_手写规范正文带数字_不被误拒_原样保存与渲染` | 正文/规则里写死 `8px`、`#0f172a`、`24px`：**存得下**（`Save` 不抛、`Source=manual`）+ **原样出**（design-md 逐字包含、bundle `bodyRaw`/`textRaw` 逐字相等、`body` 渲染形态也不丢数字）。反向探针用的是**真装一条越界守卫进 `GuidelineService.Save`**（不是只改期望），一轮跑出双红后当场逐字还原 |

**这一格的可复跑命令**（复验方照抄即可，无需信我的转述）：

```bash
TMP=.temp/ds-m1/tmp TEMP=.temp/ds-m1/tmp dotnet test ForgeSelf.Api.Tests \
  --filter "FullyQualifiedName~GuidelineExportTests" --logger "console;verbosity=normal"
```

判定：`测试总数: 12 / 通过数: 12 / 失败 0`；两条 V10 用例名必须出现在 `已通过` 行里（`V10_抽三个令牌改值…`、`V10_手写规范正文带数字…`）。

## Integration Test

| 判据 | 用例全名 | 结果 |
|---|---|---|
| 13 张表全由建表路径产出、唯一索引真实落到物理库、重复初始化不动旧行也不回填规范 | `GuidelineUpgradeTests.AC12_13张表全部由建表路径产出到物理库` / `AC12_唯一索引真实落到库里` / `AC12_重复初始化不动旧行也不回填规范` | **PASS**（Verified；冷进程建表限制见 Known Limitations #4） |
| 生成链路把规范种进库并把现存条数带回响应（第二次不是 0） | `GuidelineServiceTests.AC14_生成链路把现存条数带回响应_第二次不是0` | **PASS** |
| 13 预设生成落库后审计零 critical（系数微调就是这条抓出来的） | `StyleAxisAuditTests.AC7_十三个预设_生成落库后审计零critical` | **PASS**（`flat-minimal` 彩度 0.05→0.08 已登记「公式调整记录」） |
| 默认产物逐字节兼容（50 项黄金基线） | `StyleAxisGoldenTests.AC1_基线集合_默认轴产物哈希逐一相等` + `AC1_种子串_默认路径逐字不变` + `AC2_同请求两次_逐字节相同` | **PASS** |
| 预览与交付同源（preview-css ↔ 落库导出） | `PreviewCssTests`（全预设遍历，计数 8→13） | **PASS** |
| 快照 schema 3 跨版本比对与幂等 | `GuidelineReleaseTests`（6）+ `ReleaseSnapshotTests`（17，零回归） | **PASS** |
| brief 章节序含 `guidelines` 且排在 `checklist` 前（夹具装配同形后这条才成立） | `DesignBriefTests.E1_章节序_identity恒含`、`GuidelineExportTests.AC16_brief_开规范章_只列MUST_且排在checklist之前` | **PASS**（E1 曾因夹具漏挂 `Guidelines` 红过一次，见 03-plan 04:38 偏差行） |

Result: **PASS**（依据如上；来源等级 **Verified**）

## E2E

### A 块（AC11）· `e2e/plugins/design-system/design-system-style.spec.ts` · 2026-10-03 02:21

Command: `cd ForgeSelf.Web && node node_modules/@playwright/test/cli.js test --config=playwright.config.ts e2e/plugins/design-system/design-system-style.spec.ts --output=../.pw-out-ds-m3 --reporter=list`

Result: **PASS 3/3**（真实宿主、零 mock；日志 `.temp/ds-m3/e2e-style-a7.log`；证据 `ForgeSelf.Web/screenshots/e2e/design-system/m2/style-S{1,2,3}-passed.log`；来源等级 **Verified**）

| 用例 | 判据（现场实测值） |
| --- | --- |
| S1 | `meta.styleAxes` 出 7 条轴；面板控件数 == 轴数；芯片文案 == `meta.valueLabels`（实测「柔和」= `soft` 的中文）；`editorial-serif` 注入的交付 CSS 定义 `--ds-font-display: "Noto Serif SC"…`，`tech-crisp` 的定义里没有它；两套卡片 `box-shadow` 不同且非 `none` |
| S2 | 点选 `shadowStyle=crisp` 后，`quick-create` 请求体 `shadowStyle="crisp"`；新建项目导出 CSS 实测 `--ds-shadow-elevation-3: 0px 3px 7.9px 0px color-mix(in oklab, #0f172a 22%, transparent)` —— 与 §A3 公式人工核算（`3^1.35×1.8=7.9`、`0.07+0.05×3=0.22`）逐字一致，单层（`color-mix(` 计数 1） |
| S3 | 向导第③步显示 editorial 已选中；创建后 `tokens/effective` 含 `font.display`（值含 `Noto Serif SC`）且 `font.sans` 仍是现状栈；`diagnostics` 为空（无悬空别名） |

过程中真实红过 4 次并逐次归因（见「Unresolved Issues」上方的 A 块红账）：1 次我的用例顺序、2 次 schema/断言自身写法、**1 次真缺陷（quick-create 丢轴，已修并补守卫）**、**1 次既有产物缺口（排版令牌 fontFamily 不进 CSS，不属本批修复范围）**。

### B 块（AC19/AC20）· `e2e/plugins/design-system/design-system-guidelines.spec.ts` · 2026-10-03 04:31

Command: `cd ForgeSelf.Web && node node_modules/@playwright/test/cli.js test --config=playwright.config.ts e2e/plugins/design-system/design-system-guidelines.spec.ts --output=../.pw-out-ds-m3 --reporter=list`

Result: **PASS 3/3（46.9s，`Running 3 tests` → `3 passed`，发现数==报告数）**；日志 `.temp/ds-m1/logs/e2e-guidelines-b3.log`；证据 `ForgeSelf.Web/screenshots/e2e/design-system/m2/guideline-G{1,2,3}-passed.log` + `guidelines-g{1,2,3}-*.png`；来源等级 **Verified**

| 用例 | 判据（现场实测值） |
| --- | --- |
| G1 | 第 15 个入口「UX 规范」可点且 `meta.capabilities` 含 `guidelines`；界面清单项 `data-guideline-code` 与 REST `/projects/1/guidelines` **逐 code 相等、条数 14**；引用令牌 chip **8 个逐个**比对：值 == 后端 `tokenValues` == `tokens/effective?theme=light`（三方同源，主题口径 `valueTheme=light` 显式交代）；正文输入框回填的是**库里原文 `bodyRaw`**（不是括注文本），且原文不含 `\d+(px\|rem\|ms)` |
| G2 | 界面改 `forms` 的标题/正文 → 保存 → **REST 回读同一份**（`title=E2E 改过的表单规范 murwacrt`、`bodyRaw` 逐字、`source=manual`）；「重新生成默认规范」跑完必须报 `手改受保护 1` 且回读标题仍是用户改的那条；归档**两条路径**：点「取消」→ REST `status` 仍 `adopted`（零写入），点「确认」→ `POST archive` 200、默认清单 **14→13**、`status=all` 口径里行还在（软删）、项目本身仍非 archived；勾「显示已归档」→ 选中 → 恢复 → REST `status=adopted` |
| G3 | 连点两次「重新生成默认规范」条数 **14 → 14**（不翻倍）；规则行 `[data-rule-level]` 与来源徽标都在，`[data-guideline-code] [data-source]` 计数 == 清单条数；「新建规范」只填标识 → **REST `status=all` 读不到**（草稿不落库），且此刻编辑器必须真的打开（`[aria-label="规范标题"]` 可见）；填标题+规则 → 保存 → REST 读得到、`rules[].textRaw` 含那句话、该行徽标 `data-source="manual"` |

红账（三轮，逐次归因，不藏着）：
- **b1（`.temp/ds-m1/logs/e2e-guidelines-b1.log`）G1 红、G2/G3 未跑**：`locator.click … waiting for getByRole('row', { name: /e2e-m3b-murvtrum/ }).getByRole('button', { name: '选为工作项目' })` 超时 300s。归因＝**我的用例写错**：`state.loadProjects()` 会把清单第一条自动选为工作项目，行内按钮此时渲染成禁用的「当前」（`Projects.vue:403`），硬点必等不到。`pickProject` 改判据为"这一行成为工作项目"（已是「当前」就过，否则点一次；隔离库里有陈旧项目时仍走点击路径）。
- **b2（`e2e-guidelines-b2.log`）G1 绿、G2 红在归档**：`getByText(/已归档 forms/)` 不可见，而同帧网络里 `POST projects/1/guidelines/forms/archive → 200`、随后 `GET guidelines` 已刷新——**后端做对了，界面上看不见**。归因＝**真实产品缺陷（本轮 #7）**：`note/err` 两段结果反馈写在详情面板 `gl__detail` 内部，而归档动作本身会让选中行从默认清单消失 → `draft=null` → 面板 `v-if` 关闭，提示语跟着一起没了；用户点完「确认归档」得到的是"条目凭空消失、零反馈"。修法＝把 `note`/`err` 提到 section 级，`err` 加 `state !== 'error'` 以免与 `PanelState` 重复同一句话。
- 同一轮自查顺带逼出**真实缺陷 #6**：详情列条件是 `draft && selected`，新建草稿没有库内行（`selected=null`）→ 点「新建规范」填完标识后右侧面板根本不出现，草稿流程在界面上死掉。修成 `draft && (selected || draft.isNew)`，并把依赖 `selected` 的引用令牌区与归档/恢复按钮按可用性收口；G3 加断言"新建后 `[aria-label="规范标题"]` 必须可见"，并去掉原先"manual 徽标 ≥1 条"对 G2 的跨用例依赖（改成由 G3 自己新建那条来验 manual）。
- **b3 三绿**。改 `.vue` 后先 `pnpm run build`（e2e 读 `web/dist`，`globalSetup` 不建插件前端）再跑，已把这条写进 `design-system-verify` 技能第 4 节。

### V 块（AC21/FR16 视觉 QA 矩阵）· `design-system-style.spec.ts` 的 V 片 · 2026-10-03 05:00→05:28

| 用例 | 张数 | 结果与实测判据 |
| --- | --- | --- |
| V1 13 预设 × 后台/中台 × 明暗 | 26 | **PASS（10.6s）**，日志 `.temp/ds-m1/logs/e2e-m3-v23.log`；预设清单取 `GET presets` 且断言恰 13 件、五件新预设都在；每张量两档对比度（页标题/hero 对画布底、卡标题对卡底），`low` 为空 ⇒ **13×2 全部 ≥4.5:1**；证据 `ForgeSelf.Web/screenshots/e2e/design-system/m2/style-V1-passed.log`（**该目录被 `.gitignore` 的 `screenshots/` 挡着，只在本机**，换机器需重跑 `design-system-style.spec.ts` 再生成）逐张记注入 CSS 字符数与卡片阴影计算值（`flat-minimal` = `0px 0px 0px 1px` 环线、`tech-crisp` 单层 `0px 2px 4.6px 0px`、`warm-craft`/`kids-playful` 双层） |
| V2 5 件新预设 × 落地页 / 移动端 × 明 | 10 | **PASS（6.1s）**；每张记 `wear` 数与画布底色，对比度同样全通过 |
| V3 7 条轴 × 19 个非默认取值 + 1 张默认档对照 | 20 | **PASS（1.0m）**，日志 `e2e-m3-v3g.log`。判据分两层：① **轴落到交付产物**——`export?format=css&theme=light` 与对照件逐变量比，要求变化数 >0 且至少一个落在该轴负责的令牌前缀上；② **舞台装的就是这份产物**——注入文本的声明行必须逐条等于交付 CSS（`expect.poll`）。实测变化变量数：`shadowStyle` 4–5、`borderStrength` 2、`neutralTemp` 9–11、`fontPairing` 1–2、`radiusStyle` 12–13、`accentStrategy` 各 22、`shadowStrength` 各 5 |

V3 的红账（三轮，全部归因清楚）：
- **红 1（用例）**：任何导航之前就调 `apiData` → `SecurityError: Failed to read the 'localStorage' property`。改成先 `openShowroom` 再取 meta。
- **红 2（后端契约在挡）**：`400 code 只能含小写字母/数字/中划线（^[a-z0-9][a-z0-9-]{0,39}$）：e2e-m3v-…-accentstrategy-analogous` —— 我拼的 code 41 字符超上限 40。改成序号命名，可读名只留在截图名与证据行。**这条红是有价值的**：它证明写入端的形状校验不是宽进宽出。
- **红 3（用例）**：`衣柜里应有 project:…-0 … element(s) not found`。真因＝`openShowroom` 已停在 `#/showroom`，再 `goto` 同一地址只是**同文档 hash 导航**，应用不重拉项目清单 → 衣柜 13 件全是预设（快照为证）。改 `page.reload()`；另加 `ensureOutfit()`（看不到就点「更多」，兜 `OUTFIT_LIMIT=12` 与异步加载两种情形）。
- **红 4 的教训（判据写法）**：`changed=0` 这一条同时可能是"轴没生效"与"舞台装了上一件"两种事——所以 V3 把两层判据拆开（REST 权威源 + 注入文本必须等于它）。拆开后一次就绿，说明轴一直是通的，红的是我的观测方式。

**V3 逼出的真实缺陷 #8（已修，`ExportService.ShadowCss`）**：`shadowStrength=0` 的卡片阴影计算值是 `rgb(15, 23, 42) 0px 2px 9.1px -1px`——**不透明实心投影**，与 03 §A3 / 02-spec 的"0 = 阴影不可见但令牌仍存在"正相反；根因是 `alpha > 0 && alpha < 1 ? color-mix(…) : color` 把 `alpha==0` 落进 else。修法：显式 `alpha<=0 → transparent`，无 `alpha` 字段仍按原样（不动手写令牌语义），`alpha>=1 → 纯色`。默认档 alpha 恒在 (0,1) ⇒ **存量产物一字不变**（`--filter ~PreviewCssTests|~ExportProjectionTests|~StyleAxis` **100/100** 复跑为证）。新增常驻用例 `PreviewCssTests.AC3_阴影强度0_投影必须是transparent_而不是实心色`（同时核默认 1 与拉满 2 两端，防"只在 0 那侧成立"）。

另补一条常驻后端用例 `QuickCreateServiceTests.Create_只带轴的半份请求_轴必须落到库里令牌`（V3 曾怀疑"只发 `{shadowStyle}` 的半份请求会被预设吃掉"→ 读回 `shadow.elevation-3` 的 Value + 逐层展开比对，18/18 绿）。


### E2E · 全目录回归（步骤 15 · AC23）· 2026-10-03 05:37→06:06 · **本节已被 19:21 那一节取代（见下一节），保留作 06:06 的历史终态**

跑法：`--config=playwright.config.ts` + `--workers=1`（M2 闸门2 已定"串行即权威"）+ `--output=../.pw-out-ds-m3`（空目录，不攒 trace）；分三批（每批一次 globalSetup：重建宿主 + 隔离库重置）。第 3 次起统一显式钉端口 `E2E_FRONTEND_PORT=7402 E2E_BACKEND_PORT=7502`——本机有并行会话时，config 的"认领端口 + `reuseExistingServer`"会让**别人的 teardown 打死我的 vite**（批 C 第三跑就是这条环境红，与判据无关）。

| 批 | 覆盖 | 结果（原文） | 日志 |
| --- | --- | --- | --- |
| A | `design-system.spec.ts`（工作台全链路 mega 用例，含 15 个 nav 入口、`entities===10`） | `1 passed (42.9s)` | `e2e-regress-workbench.log` |
| B | `design-system-agent.spec.ts` + `design-system-showroom.spec.ts`（M1 工具面 + M2 A/B/C/D 四片，含 D1 的 30 张视觉矩阵、写开关、深链、并排对比） | `18 passed (1.4m)` | `e2e-regress-agent-showroom.log` |
| C | `design-system-style.spec.ts`（S1/S2/S3 + V1/V2/V3）+ `design-system-guidelines.spec.ts`（G1/G2/G3/**G4**） | **`10 passed (3.1m)`**（V1 26 张 / V2 10 张 / V3 20 张全部重出；G4 为 §G8 契约逐项走查） | `e2e-regress-style-guidelines-11.log` |
| **C 复跑**（16:41，V4 与"选中档==衣服真值"同源判据加入后） | 同批 C 覆盖面 **+ V4**（S1/S2/S3 + V1/**V4** + G1/G4） | **`11 passed (2.6m)`** —— V3 1.3m、V4 3.0s、G4 3.5s；**此前最不稳的 S2 在同批上下文里再一次一次通过**（不是单跑绿）；端口按技能 #50 显式钉 `E2E_FRONTEND_PORT=7442 / E2E_BACKEND_PORT=7542` | `e2e-regress-batchC-rerun.log` |

**合计 30 条用例、0 红**（**该轮**终态 16:41 = A 1 + B 18 + **C 复跑 11**；18:50 后生产码有净改动 ⇒ 本节的终态身份已由 19:21 那节接替）；M3 新增的 11 条（S1-S3 / **V1-V4** / G1-G4）与 M1/M2 既有 19 条同库同宿主跑通，无新增红。上面「A 块 / B 块 / V 块」三节是各片**首跑**的记录，终态以本节批 C 复跑为准（判据变严后重出）。

批 C 的红账：**四条归因，覆盖六轮红跑**（判据类 2 + 环境类 1 + S2 读侧 3；全部当场归因，不留"看起来是环境问题"）：
1. **判据红（我的用例）**：`S1 … Received string: ""` —— 读半加载态的 `<style>`。改 `expect.poll` 等到位。
2. **判据红（我的用例，更阴的一次）**：`expect(techCss).not.toMatch(/--ds-font-display\s*:/)` 收到的是**挂着 `preset:tech-crisp` 这个 id 的上一件皮肤**。根因在 `Showroom.vue:loadCss()` 取数期不清空 `css` + Vue 原地复用同一个 div（`Stage.vue` 把新 id 与旧文本一起交给 `OutfitScope`）。处置＝判据换成**"注入声明行逐条等于这件衣服自己的权威交付 CSS"**（预设走 `presets/recommend` + `generate/preview-css`，项目走 `export?format=css`），把 V3 已有的等式判据推广到 S1/V1；产品侧根因**不在本批改**（`Showroom.vue`/`Stage.vue` 不在 04-task Allowed）→ 记 TODO(P2)。见 03-plan 偏差表 05:56 行。
3. **环境红（非判据）**：`page.goto: net::ERR_CONNECTION_REFUSED at http://localhost:7002` → `2 failed / 7 did not run`。归因＝端口争抢（跑完 netstat 7002/7102 无监听；:51888 用户宿主全程未碰）。处置＝钉端口重跑，不改用例。教训入 `design-system-verify` §四。
4. **S2 的"产物缺族"红：从"不可复现"查到"可复现 + 归因读侧"**（run 4 / run 6 / run 9 三次红，判据升级后 run 11 绿）：
   - run 4、run 6 红在 `(导出里没有 elevation-3)`；加现场诊断后测到 **7628 字符、`--ds-shadow-*` 整族为 0**，而同一项目完整读时是 **17074 字符 / 310 个变量**。
   - run 9 换了个红法：`保存为新设计应且只应新增一个项目` Expected 1 / Received 0，而**同一轮宿主日志**写着 `15:18:34.404 [DesignSystem] 项目已创建 ds-0cdb81（默认主题已配齐）` ⇒ 写侧成功、`GET projects` 读不到。
   - 复现配方 `--grep "(G[1-4] |S2 )"` **2/2 稳定**（`e2e-s2-staleness-probe.log`、`e2e-s2-listlag.log`）。
   - **判据升级两次（第一次仍不够强，如实记）**：① 先改成"轮询到 shadow 五档齐全 + 同一瞬间读 `tokens/effective` 当分叉旁证"——实测发现 **`effective` 自己也会读到旧视图**（同一项目读到过 199 与 309 两个数），"导出 == effective"这种**同源自对**判据照样放过残缺；② 最终改成挂在**不吃这条写读链的权威源**上：`POST generate/preview-css`（零写库、当场跑生成器）的变量名集合必须被落库导出**一条不少**地包含；另加"`GET projects` 必须出现新增那条"的轮询，并抓 `quick-create` 的 **200 响应** ⇒ 写失败 / 写成功但读侧滞后 / 产物天生残缺，一次跑即可分辨。
   - 终态读数（run 11）：`响应 200 … "id":2,"code":"ds-0cdb81"`、`第 1 次读项目清单：共 2 条、相对保存前新增 1 条`、`第 1 次读导出：17074 字符、变量 310 个（同请求 preview-css = 310 个）`、`tokens/effective count=309`。
   - **产品侧结论**：写侧（生成/落库）是全的（服务层新用例 `Create_带轴项目_导出CSS整族齐全且形状随轴变` 19/19 同证），**滞后发生在读侧**（导出与项目列表两个端点都观察到）。机制候选＝跨执行上下文的 XCode 列表缓存幽灵读（`TokenRepository` 类注释自己写着这条铁律 11 风险）/ SQLite 连接快照可见性。修法属跨切面读路径 + 性能影响 → **本批不自作主张改**，TODO 立 P1（复现配方、现场配置、候选修法 A/B/C、"别重复踩"的判据写法都在里面），门禁侧留常驻读数（`attempts > 1` 即滞后一次）。

### E2E · 全目录回归（终态源码重跑 · 2026-10-03 19:07→20:22 · **本节 32 条已被下一节 21:26 的 33 条接替，保留作红账归因**）

为什么重跑：18:50 之后我有**两处生产码净改动**（`DesignReviewService.SeverityOf` 签名收紧、删 `DesignGuideline.Biz.cs` 的死字段 `MaxCacheCount`），而插件层 e2e 跑的就是真实宿主加载 `DesignSystem.dll` 的链路 ⇒ 16:41 那批结果不再代表当前源码（依据与教训见自查表 #60）。命令（串行、钉端口，并行会话在场）：

```bash
cd ForgeSelf.Web && E2E_FRONTEND_PORT=7412 E2E_BACKEND_PORT=7512 \
  node node_modules/@playwright/test/cli.js test --config=playwright.config.ts \
  e2e/plugins/design-system --workers=1 --reporter=list
```

| 轮次 | 结果 | 日志 |
|---|---|---|
| 19:07→19:11 首跑 | **26 passed / 1 failed / 3 did not run（4.4m）**，红的是我自己的 **G1** | `.temp/ds-m1/logs/e2e-terral-full.log` |
| 单独复跑 guidelines spec | **4 passed（52.1s）** ⇒ 顺序依赖，不是必然红 | `.temp/ds-m1/logs/e2e-g1-rerun-1.log` |
| 19:17→19:21 加固后复跑 | **30 passed / 0 失败（4.3m）**＝五份 spec 全绿（工作台 1、agent+展厅 18、style S1-S3+V1-V4、guidelines G1-G4） | `.temp/ds-m1/logs/e2e-terral-full2.log` |
| 20:03 加 G5/G6 后单 spec 首跑 | **5 passed / 1 failed** —— 红的正是新加的 **G6**，红因**不是产品**：`pickProject` 用裸前缀 `new RegExp(code)` 找行，G5 建的 `<code>-bare` 让同一前缀**命中两行** ⇒ 严格模式下 `row.isVisible()` 抛错被 `catch` 吞成 `false` ⇒ 轮询永远等不到（快照里两行明摆在）。改锚定匹配 `^code(?![\w-])` | `.temp/ds-m1/logs/e2e-g5g6.log` |
| 20:05 修好后单 spec 复跑 | **6 passed（1.0m）**＝G1-G6 全绿（G5 读数：`空态命中：REST 0 条、标题「这个项目还没有 UX 规范」` → `生成后：界面 14 条、REST 14 条、空态面板 0 个`；G6 读数：`820px：轨道 1 根，列表 y=473 h=1094，详情 y=1599 h=676；本区溢出 0px，整页 0px`） | `.temp/ds-m1/logs/e2e-g5g6-b.log` |
| **20:16→20:22 全目录复跑**（当时称"终态"，现被 21:26 的 33 条接替） | **32 passed / 0 失败（4.6m）**＝五份 spec 全绿（工作台 1、agent+展厅 18、style S1-S3+V1-V4、guidelines **G1-G6**）；钉 `E2E_FRONTEND_PORT=7414 / E2E_BACKEND_PORT=7514`、`--workers=1`、`--output` 指空目录（跑完即删） | `.temp/ds-m1/logs/e2e-full-after-g5g6.log` |

**这一节为什么又跑了一次**：19:21 那轮之后本批没有生产码改动，但**规格反向覆盖审计新增了 G5/G6 两条界面判据**（见上一节）⇒ 全目录必须重跑，否则"终态 30 条"这句话覆盖不了当前 spec 文件的内容。本轮**零生产码改动**（只动测试码），所以门禁④ 的包内容判据仍然成立（包内 DLL/前端产物与 19:22 那份逐字节同源）。

**G1 那条红的完整归因（三条实据 + 一条被排除的怀疑）**：

1. **失败快照原文**（`.pw-out-ds2/…G1…/error-context.md` 第 84–85 行，逐字抄录）：`- heading "项目清单" [level=4]` 与 `- text: 2 个`，而表内两行是 `e2e-musag9xe`、`e2e-src-musag7cj`（同批别的 spec 建的），**独缺本用例刚建的 `e2e-m3b-musagbw2`** ⇒ 既不是空表，也不是库被并行会话重建。该快照目录是 playwright 的 scratch 输出（不进版本库），本批跑完已删除，所以把关键两行抄进本节留证。
2. **后端同刻已写入**：隔离实例 `.temp/e2e/wt-b26d4625/backend.log` 有 `19:08:12.913 [DesignSystem] 项目已创建 e2e-m3b-musagbw2（默认色彩…）` ⇒ 写侧成功，红的不是 quick-create。
3. **机制在读码里**：`sections/Projects.vue` 的 `onMounted(() => { if (!projects.value.length) void loadProjects() })` —— **清单非空就不重读**；而 `DesignSystemView` 在 `page.goto()` 时已经加载过一次清单（那一次还没有新项目），之后 `pickProject` 只是 `expect(row).toBeVisible()` **反复查同一份旧 DOM**，20s 也等不来。**"单跑绿 / 全跑红"正是这类顺序依赖 UI 缓存的形状**（前面没有别的 spec 建过项目时，清单为空 ⇒ 挂载就读 ⇒ 通过）。
4. **排除的一条怀疑**：跑动中确有 `500 GET /api/design-system/projects/28/export?format=ts&theme=dark`（19:11:33）与 `socket hang up`，但后端日志显示那是 `EventLogLogger` **无权限写 Windows 事件日志**（`Cannot open log for source ".NET Runtime"`，Win32Exception 5）导致的 **1 次** Kestrel unhandled（该串文本共 11 处，unhandled 只 1 次，且发生在另一条已通过用例里）⇒ 环境权限问题，不是本批代码，也不是 G1 的成因（该用例通过）。

**处置：走用户的真实出口，不放宽判据。**`pickProject` 改成点表头「重新读取」（真人就是靠它刷新）之后再断言该行可见，并把"刷新第几次才出现"打进日志：实测 `G1 第 2 次`、`G2/G3/G4 各第 1 次`。**这条读数的局限也要说清**：`expect.poll` 的第一次迭代会在请求往返内立刻返回 false，所以 `attempts>1` 混着"正常网络延迟"与"界面根本不重读"两种解释，**它不是 G18 的定量指标**——G18 的实据是第 3 条的码与失败快照「2 个」。真要量化得改成"点一次后等请求落地再判定"，本批不做（不为读数再动已通过的用例）。

**新登记的真实产品缺口 README G18 + TODO(P2)**：项目清单在"已有条目"时切进「项目与生成」不会自动刷新 ⇒ 经 REST / 别的客户端新建的项目看不见，必须手点「重新读取」，**与该 section 自述「这里不存任何东西，刷新即回读库」名实不符**。修法（挂载即读 / 非空也静默刷新一次）动的是 M1/M2 既有交互与全站列表语义 ⇒ 另立批次，等用户点头。

### E2E · 网关链 G7（21:11→ · 补 X1 那一面：外部 MCP 客户端的出货承诺）

跑法与上面同一条命令，只把目标换成单条用例（钉端口避开并行会话）：

```bash
cd ForgeSelf.Web && E2E_FRONTEND_PORT=7416 E2E_BACKEND_PORT=7516 \
  pnpm exec playwright test e2e/plugins/design-system/design-system-guidelines.spec.ts \
  --grep "G7" --workers=1 --output=../.pw-out-ds3
```

| 轮次 | 结果 | 读数（原文取自日志，不是我复述） | 日志 |
|---|---|---|---|
| 21:15→21:16 首跑 | **1 passed（1.4m）** | `网关 http://127.0.0.1:19483（isRunning=true，hasToken=false）`；`list_tools 命中 8 件，其中 design-system 8 件：design_audit, design_context, design_create, design_edit, design_guide, design_lookup, design_presets, design_review`；`design_guide 经网关：version=3.1.0，tools=8，workflows=consume/create/maintain/guideline，allowWrite=true` | `.temp/ds-m1/logs/e2e-m3-g7-gateway.log`（证据副本 `ForgeSelf.Web/screenshots/e2e/design-system/m2/guideline-G7-passed.log`） |
| 21:17→21:19 **反向探针**（把期望源改错：往 `meta.agentTools` 里临时加一件不存在的 `design_temp_probe_zzz`） | **1 failed** —— 红的正是那一格：`- "design_temp_probe_zzz"` 出现在 Expected 侧、Received 侧没有 | 报错原文给了两侧计数：`注册表里的 design_*（8 件）与 meta.agentTools（8 件）不一致` ⇒ 这条相等判据**确实在读注册表返回的名单**，不是恒真 | `.temp/ds-m1/logs/e2e-m3-g7-probe.log` |
| 21:19 还原 | 探针串全文 `grep`（`design_temp_probe_zzz` + `临时探针`，范围 `ForgeSelf.Web/e2e` + `Plugins/DesignSystem`）**零命中**，净改动为一条新增用例 | — | — |
| **21:19→21:26 全目录终态复跑** | **33 passed / 0 失败（4.8m）**＝五份 spec 全绿（工作台 1、agent+展厅 18、style S1-S3+V1-V4、guidelines **G1-G7**）；钉 `E2E_FRONTEND_PORT=7418 / E2E_BACKEND_PORT=7518`、`--workers=1`、`--output=../.pw-out-ds4`（跑完即删）。**这一轮取代 20:22 的 32 条，成为 e2e 的权威终态** | 日志第 5 行 `Running 33 tests using 1 worker`、第 1749 行 `33 passed (4.8m)` ⇒ **发现数 == 报告数 == 33**；G7 在序列里排 `[13/33]`（第 273 行），不是被跳过的装饰 | `.temp/ds-m1/logs/e2e-full-after-g7.log` |

**本轮同样是"测试码净改动"**：生产码（`Plugins/DesignSystem/**/*.cs`、`web/src/**` 非 test）零改动 ⇒ 门禁 ①②④ 的既有终态数（529 / 250·19 / 包 `2610031922`）不被本轮作废，但 e2e 那一层的权威轮次按上表追到 **21:26 / 33 条**。

**G7 自己带两条反向腿**（不用另跑探针也成立的那部分）：不存在的工具名 `design_nope_e2e` 必须 `isError=true` 且文本含 `unknown tool`；把 `design_guide` 当**对外**工具名直接 `tools/call` 必须被 JSON-RPC 层拒（错误文案含 `universal_tool`）。这两腿钉的是"网关不是一台对什么都回一大坨的机器"，所以第 ① ② 格的绿不是假绿。

**这一条为什么值得常驻而不是当场测完就算**：它证明的是**跨插件接缝**（DesignSystem 交工具 ↔ McpCenter 分发），而这一接缝上两侧测试各自都"以为自己测过了"——DesignSystem 侧直构对象、McpCenter 侧用 mock 注册表。缝隙正好在中间。同时它不改 McpCenter / 宿主一行码（只**用**它的公开网关），符合本批 Forbidden。


### E2E · 深档全量（发版/tag 前跑的那一档 · 22:28→22:44 · **本批第一次仓内全量 e2e**）

§5.6 的深档触发条件写着「发版/tag 前」，而本批此前只跑到快档（`e2e/plugins/design-system` 目录，33 条）。M3 在 e2e 层新加的 **G7 是跨插件调用**（打 McpCenter 的网关与 `/api/mcp-center/config`），快档看不见它对同一宿主上其他套件的影响 ⇒ 必须补这一档。**先说清档位**：本节的绿/红**不覆盖**也不取代上一节的串行权威轮（那一轮才是 e2e 的判据终态）。

```bash
cd ForgeSelf.Web && E2E_FRONTEND_PORT=7420 E2E_BACKEND_PORT=7520 \
  pnpm exec playwright test --config=playwright.config.ts \
  --workers=4 --reporter=list --output=../.pw-out-full
```

| 项 | 实测（原文摘要） |
|---|---|
| 总数与结果 | `145 passed / 75 failed / 4 skipped / 1 did not run (15.2m)`，日志 `e2e-full-suite-tag-precheck.log`（判读只按这份日志，不看退出码——它是 1，因为按设计就有基线红） |
| 与上一份全量基线对比 | 2026-09-30「e2e 基线治理」批次记录的是 `103 passed / 83 failed / 4 skipped（总数 190）`；本次总数 **225**（M1/M2/M3 三个里程碑各自加了套件与用例），**failed 从 83 降到 75**、passed 从 103 涨到 145 ⇒ 方向为正，没有出现"红项变多" |
| 红项构成（逐个数出来，不是印象） | `chat.spec 28`、`quick-links 5`、`todo 5`、`chat-records 4`、`mcp-tools 4`、`agent-loop-autonomous 4`、`tool-gateway-031 4`、`ai-agent 3`、`agent-hub 3`、`agent-execute 3`、`ai-provider-chat 2`、`home 2`、`ai-providers 1`、`app 1` …… **design-system 只有 1 条**（合计 75） |
| 非我这一族的红归因 | 前三族与 09-30 基线的归类一致：**chat 整族（28，已知整族红）**、**真实 LLM/LM Studio 依赖族**（ai-agent / agent-loop / agent-execute / tool-gateway 等要真模型或真网关）、**51888 语义的 quick-links 族**、在册散红（`app.spec:35`、`todo`、`home`、`ai-providers`）。这些都不是 design-system 目录，也都不归本批动过；沿用基线批次同一口径逐族登记，不写"无关"两字带过 |

**design-system 那一条红的完整归因（做成了可复现实验，不是解释）**：红的是 M2 的 **A1**（`design-system-showroom.spec.ts:111`），报错原文：

```
Error: 试穿/微调不得改动任何已有项目
- Expected  - 1     + Received  + 3
- Array []          + Array [ "e2e-mushxak1" ]
```

1. **被改动的那条项目不是 A1 的**：`e2e-mushxak1` 匹配 `design-system.spec.ts:39` 的 `const RUN_CODE = \`e2e-${Date.now().toString(36)}\`.slice(0, 24)`（同一目录里**工作台全链路**那条用的码形），A1 自己的码是 `e2e-m2-<ts36>`（`design-system-showroom.spec.ts:47`）⇒ 4 worker 下另一条用例正在写它自己的项目，而 A1 的守卫是**全局范围**（"任何已有项目都不许变"）。
2. **同目录单独升到 4 worker 复现（22:47→22:52，`e2e-ds-workers4-repro.log`）：`27 passed / 2 failed / 4 did not run`**，但红的两条**换成了** `design-system-style.spec.ts:408`（S3 向导 `[data-wizard-done]` 90s 内没出现）与 `design-system.spec.ts:211`（`page.evaluate: Error: 500`，落在 `apiData` 的 `expect.poll` 里）。**红哪一条会随并发时序移动** ⇒ 这是并行互扰的形状，不是某条用例的确定性缺陷。
3. **串行权威轮是绿的**：同一份终态源码、同一套 DLL，`--workers=1` 整目录 **33 passed / 0 失败**（21:26，`e2e-full-after-g7.log`）⇒ M3 的产品码没有把 A1/S3/工作台跑坏；500 那一族正落在本批已登记的 **G19/E8（`SQLITE BUSY` 读侧未按规格重试）** 上（README G19 + TODO P2），并发写越多越容易撞。
4. **诚实边界**：本 worktree **没有 M3 之前的全量 e2e 日志可 diff**（`ls .temp/ds-m1/logs` 里那几份叫 full 的都是 design-system 目录单跑）⇒ 我不能说"这 75 条在 M3 之前也是这 75 条"，只能说：非 design-system 的红都在基线批次登记的族里、design-system 的红在串行下全部不复现。

**结论与处置**：深档这一档**跑了、读的是真实摘要、design-system 唯一的红已归因到并发并给出复现**；不因它在 4 worker 下红就改 A1 的判据（那条守卫是 M2 立的"试穿不写库"，收窄它=放宽判据）。**新登记 TODO(P2)**：插件层 e2e 的 `design-system` 目录在同一宿主库上**不可并行**，深档全量要么整跑 `--workers=1`，要么把该目录从并行批次里排除后单跑；同时把这条写进 `e2e-testing` 技能（跑法约束，不改任何用例）。



## 发布与产物走查（门禁 ④ · 本地 zip，**未打 tag、未推远程、未碰 `-UpdateDir`、未启停任何宿主**）

命令：`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release/release-local.ps1 -Version 2.3.0`
→ **终态发行串 `2.3.0 -> 2.3.0.2610031922`（19:22）**，产物 `artifacts/release/OpenForgeSelf-2.3.0.2610031922-win-x64.zip`（103.8 MB，`artifacts/` 已 gitignore），日志 `.temp/ds-m1/logs/release-local-m3-terral.log`。
**同一命令的上一轮 `2.3.0.2610031557`（15:57）降为历史**：它对应的源码里没有 18:50 之后那两处生产码改动（`SeverityOf` 签名收紧、删 `MaxCacheCount`），所以本批的包内容判据全部改在 19:22 的新包上重做（下表即新包实测）。

判据按 `design-system-verify` §一 的要求验**包内容**，不是"脚本跑成功"：

| 检查 | 实得 | 判定 |
|---|---|---|
| 包内插件三件套（**19:22 新包实测**） | `plugins\DesignSystem\DesignSystem.dll` 1,040,384 B、`plugin.json` 1,095 B、`web\dist\index.js` **442,971 B**、`web\dist\style.css` **96,481 B**（前端两个字节数与门禁② 逐项相等 ⇒ 加测试与改签名没动到交付物） | 前后端产物字节数与门禁② `pnpm run build` 报的 442.97 kB / 96.48 kB **逐一对上** ⇒ 包里就是刚验过的那份 |
| `plugin.json` 内容（从 zip 里解出来读） | `"Version": "3.1.0"`，Description 含「UX 规范（14 条确定性默认，可编辑、进交付物）」 | 3.1.0 真进了发布产物，不是工作树里改完没打进去 |
| DLL 实现指纹（`scripts/probe-dll-string.cjs`，**19:22 新包内的 DLL 实测**） | `GuidelineGenerator` / `StyleAxes` / `DesignGuideline` / `PreviewCssService` / `SeverityOf` / `3.1.0` **六个全部 FOUND** （`SeverityOf` 是 18:50 收紧签名那个私有方法，出现即证明包内程序集来自**改动后**的源码） | M3 的轴与规范代码真在程序集里，且是本批终态版本 |
| 前端 minified 产物指纹（探实现字面量，不探被压掉的函数名；**19:22 新包实测**） | `data-guideline-code` ×2、`data-style-axes` ×2、`重新生成默认规范` ×2、`更多风格选项` ×1、`UX 规范` ×4、`data-axis-value` ×4（V4 的同源判据属性）、`data-diff-not-comparable` ×1（AC17 提示）、`无法比较` ×1、`手改受保护` ×1 | 第 15 个 section、轴面板、发布对比提示都真在产物里 |
| 宿主 exe 版本戳（`Get-Item .VersionInfo`，**19:22 新包**） | `FileVersion 2.3.0.2610031922`、`ProductVersion 2.3.0.2610031922+897d10dc8fd2cfcfec9f6c5443d9952b3ce134b1` | 发行串规则（三段号 + 10 位时间码）成立；commit 尾巴是 M2 提交 `897d10d`（本批未提交，符合预期） |
| 包完整性（**19:22 新包**） | `SHA256SUMS.txt`（CRLF，先 `tr -d` 掉 CR）期望 `7c6164bfa8ed289cfc913cefa49f88ffb4c6862ea0fce61938b9bb0e146a7e3b` == `sha256sum` 实得同值 | **MATCH**（上一轮 15:57 包是 `1aa78001f6c0…f3584`，降为历史） |

**产物新鲜度与"包 == 当前源码"的等价性实测（20:53–20:59，把此前靠推理的两句话变成测量）**：

| 问的问题 | 实测 | 结论 |
|---|---|---|
| `web/dist` 是否落后于 `web/src`？ | `find Plugins/DesignSystem/web/src -type f -newer web/dist/index.js` ⇒ **0 个文件** | 界面产物对当前源码是新鲜的（此前我只说过"零改动"，没测过） |
| 插件 `.cs` 是否晚于 DLL / 晚于包？ | `find Plugins/DesignSystem -name '*.cs' -newer <Debug.dll>` ⇒ 0；`-newer <zip>` ⇒ 0 | 包与两个构建输出都没有"源码更新而产物没跟上"的窗口 |
| 包内的前端产物和门禁② 测的是不是**同一份**？ | zip 内 `plugins/DesignSystem/web/dist/index.js` **442,971 B / SHA 前缀 `e70d5898…`** == 盘上 `web/dist/index.js`；`style.css` 96,481 B / `82f26160…` == 盘上 | **逐字节 MATCH**（比 05 原先只记"字节数一致"强一档） |
| 包内 DLL 与本地重建为何不同？ | 同源码**两次构建逐字节相同**（19:50 与 20:53 的重建 `diff=0`）⇒ 本仓 .NET 构建是**确定性的**；而包内 DLL 与重建差 **384 字节 / 0.0369%**（57 段，从 PE 头 `0x88` 起）⇒ 差异来自**构建配置不同**（`release-local.ps1` 带版本/SourceRevisionId 等属性），不是源码不同 | **不能用哈希跨构建配置判"包是不是新的"**；正确的判据组合是：确定性自证 + `find -newer` 新鲜度 + 实现串探针 |
| 实现串在两边是否都在？ | UTF-16LE 探针（`scripts/probe-dll-string.cjs`）对**包内 DLL 与本地重建 DLL**各测 `GuidelineGenerator`/`StyleAxes`/`DesignGuideline`/`PreviewCssService`/`SeverityOf`/`3.1.0` ⇒ **六个全部 FOUND ×2** | 包内 DLL 确实含 18:50 之后的改动（`SeverityOf` 收紧） |

**这一节里我自己犯的两次测量错（记下来防重犯）**：
1. 先用 Python 按 **ASCII** 搜 `3.1.0`，两边都报 absent，差点写成"05 的门禁④ 记录不实"——.NET 元数据串是 **UTF-16LE**，仓内探针的注释里早就写着这条会误判（我这次是现成的工具摆在旁边没用）。改用 `probe-dll-string.cjs` 后两边都 FOUND。
2. 反向探针 `--expect-absent MaxCacheCount` 报 FOUND，看着像"死字段没删干净"。核源码：`MaxCacheCount` 是 **12 个同族实体 Biz 文件共有的真实字段**（`grep -rl` 实测 13 个 `.Biz.cs` 含它），而 `DesignGuideline.Biz.cs` 里只剩**第 33 行一条注释**提到这个名字（`grep -nE "Int32 MaxCacheCount"` ⇒ 0，字段声明确实已删）。⇒ **`--expect-absent` 只有对"全装配唯一的名字"才有证明力**，同族共用名做反向探针等于没测。

- 解包验证在**仓库外**临时目录做，做完即删：15:57 那轮的 `%TEMP%/dsz-m3` 与 19:22 这轮的 `%TEMP%/dsz-m3b` **均已删除并复核不存在**；playwright 的 scratch 输出目录 `.pw-out-ds2/3/4` 也在收口时删除（它们不在 gitignore 内，留在仓里会污染 `git status`）。仓库里只留 `artifacts/`（gitignore）内的产物与日志。
- **本层不做的事**：不打 tag、不推远程、不把包拷进任何"本地目录更新源"（`-UpdateDir` 未给），因此**用户运行中的宿主完全没被触碰**；页面「检查更新 → 下载 → 重启并更新」属用户动作，agent 只验产物与哈希（技能 §一 ④ 的分工）。

## 接手与收尾（2026-10-04：输入47–49 并行会话 + 本会话输入2）

> 本节记「实现之后又动了什么、依据是什么」。上方「发布与产物走查」等节的数字属于 2026-10-03 终态；10-04 的终态数在下方对应小节，两套都注明取数时刻与 HEAD（同一 HEAD `2ceae29`，M3 仍未提交）。

**A. 基线复测（17:4x 本会话再重测，HEAD `2ceae29`，M3 未提交）**：`git diff HEAD --name-only` = **83** 个已跟踪修改，本批新增未跟踪 = **26** ⇒ **展开 109 个路径**（`git status --porcelain` 112 行；外来 23 个未跟踪路径折叠成 6 行，两个口径不许混）。分区域（逐区域相加 = 109，已机器核对）：Services 25 / Data 17 / web/src 15 / DesignSystemTests 22 / e2e 5 / 控制器与插件根 5 / Agent 4 / **本 pilot 5**（`06-review.md` 的结论栏本会话填了）/ 三个技能 3 / AGENTS.md 1 / 036 1 / not-taken 1 / **agent-workflow.md 1**（并发锁那条）/ **scripts/package-plugin.ps1 1**。**同一栏在 14:50 记的是 104**，两处差额都指得出文件名（06-review.md、agent-workflow.md）——这就是"每次报前先重测"的用途，不是估算。2026-10-03 的 84 → 104 的来源：输入49 的 Biz 直查重构新动 **18** 个已跟踪文件（12 个 `.Biz.cs` + 6 个仓储/服务）+ 本会话 1 个测试文件 + 1 个脚本。

**B. Biz 直查的常驻守卫（本会话新增，用户批准的 P5）**：`ForgeSelf.Api.Tests/Plugins/DesignSystemTests/BizDirectQueryGuardTests.cs`，三条判据 + 参数化探针共 **10** 个用例。
- 判据：`.Biz.cs` 之外的插件生产码里，13 个实体名后跟**任何** `Find*` 调用即红。不只是裸 `FindAll`——生成器另造的 `FindByXxx` / `FindAllByXxx` 函数体首行才是 `Meta.Cache`，只禁 `FindAll` 会恰好放过最危险的那一类（我自己第一版就是这么漏的，加宽后 10/10 绿）。
- **读数常驻打印**（写进用例里，不是事后补的）：`扫描 57 个生产码文件；Biz 高级查询调用 47 处；裸实体查询违规 0 处` ⇒ 「47 处」由判据自己数出来，与输入49 自述独立对上。日志 `.temp/ds-m1/logs/bizguard3.trx`。
- 阳性对照两条：文件数 > 0、且 `.Query(All|First|Count)` 在这批文件里出现次数 > 0。**这两条当场抓到一个真陷阱**：仓库根锚点最初用 `Directory.Exists(<repo>/Plugins/DesignSystem)`，而 `ForgeSelf.Api.Tests/bin/Debug/net10.0-windows/Plugins/DesignSystem/` 同样存在（里面只有 `DesignSystem.dll`），上溯时先命中它 ⇒ 扫到 0 个源文件 ⇒「零违规」会永久假绿。锚点改为**仓库根独有的文件** `Plugins/DesignSystem/DesignSystem.csproj`（技能 #71）。
- **反向探针实红**：往真实生产文件 `GuidelineRepository.cs:53` 插一行 `DesignGuideline.FindAllByProjectId(projectId)`，用 `--no-build` 重跑该类（源码扫描型判据读磁盘上的源文件，探针行不参与编译也无妨）⇒ 红并点名该行；还原后 `grep -c 临时探针` = 0，复跑 10/10 绿。日志 `bizguard-probe.trx`（9 通过 / 1 失败，失败项就是判据本体）与 `bizguard-restored.trx`。
- **过程中的一次自我误判（记进台账）**：中间某轮编译因缺 `using Xunit.Abstractions;` 失败，而我随后用 `--no-build` 跑的是**上一个 DLL**，一度得出「加宽正则后仍有真违规」的错误结论；改用 TRX 读出真实消息（`Expected collection not to be empty because 插件源码目录必须被读到`）才定位到是空扫描。⇒ 判绿前必须确认这次编译真的成功（技能 #71 ④）。控制台码页会把中文日志搞成乱码，读中文结论一律走 TRX/UTF-8 文件，别在管道里赌。

**C. scripts/package-plugin.ps1 的产物扩展名缺陷（输入49 那轮的红，本会话修掉并实测）**：`:126` 把 `:56` 组出的 `<id>-<ver>.forgeself-plugin` 直接当 `Compress-Archive -DestinationPath`，而 PowerShell 只认 `.zip`，原文 `Compress-Archive : .forgeself-plugin 不是支持的存档文件格式` ⇒ **这个脚本从来没可能产出它自己文档里写的产物**（脚本此前未被任何批次改动，属既有缺陷，非本批引入；`git status -- scripts/` 在本轮之前为空）。修法：压成临时 `.zip` 再 `Move-Item` 改名。宿主侧口径不变（只扫 `*.forgeself-plugin`，`ForgeSelf.Api/Plugins/Services/PluginVersionService.cs:341`）。
- 判定不靠 exit code，靠**读包内内容**：产物 `D:/src/my-proj/OpenForgeSelf/updates/design-system-3.1.0.forgeself-plugin`，594,334 B，8 个条目；`plugin.json` = `Id design-system / Version 3.1.0 / EntryAssembly DesignSystem.dll`；DLL 只带 `DesignSystem.dll` 一个（宿主共享 DLL 零混入）；`web/dist/index.js` 442,971 B、`web/dist/style.css` 96,481 B **与门禁② 构建产物逐字节同尺寸** ⇒ 包里的前端确实是我们测过的那一份。SHA256 `C08832C899B5FDF401D8CBB35B5B2771C9CD62325BE6BB0AF43B8F6D2027F3E4`（日志 `pkg-fix-run.log`、`pkg-contents.md`）。
- 顺手抓到一条**非阻塞缺陷**（已入 `TODO.md` P3）：包内混有 `ForgeSelf.Abstractions.pdb` / `ForgeSelf.Core.pdb`——`$HostSharedAssemblies`（`:91-95`）的名单只匹配 `.dll` 不匹配 `.pdb`。无类型身份风险（.pdb 不参与加载），但违反「活动插件目录只放插件自身 DLL」的口径。
- **档位如实交代**：改仓库级脚本按 AGENTS §5.6 属**中档**触发（应跑后端全量 ~15min）。用户 2026-10-03 23:43 指令是「本轮会话仅仅跑设计系统的测试，不跑全量」⇒ 本批**未跑宿主全量**，替代证据 = 脚本手工复现 + 包内容核对 + `DesignSystem` 过滤集（下方 D）。06 的 V4 与 Final Decision 同样写明，**不拿过滤集冒充全量**。

**G. 更新说明中文乱码（用户实测报出，2026-10-04 17:3x 修复）**：
- **现象与定位**：设置页「更新说明」整片乱码（`旧预览` → `鏃ч瑙?`）。实测不是显示问题——**乱码烤在文件里**：`RELEASE-NOTES-2.7.2.0.md` 里那批提交标题按 UTF-8 解出来全是 GBK 误读形态。宿主端无罪：`UpdateChecker.cs:788-790` 只是 `File.ReadAllText` 读更新源目录里那个 .md。
- **根因**：`scripts/release/make-release-notes.ps1:49/54` 用 PowerShell 5.1 直接捕获 `& git log` 的 stdout，PS 5.1 按**控制台 OEM 码页（本机 GBK 936）**解码 git 的 UTF-8 字节 ⇒ 提交标题在捕获瞬间就坏，再以 `-Encoding utf8` 写出。同目录 `scripts/check-git-content.ps1` 早已自己切过码页（实测 1127 处早于它的 `& git log`），所以只有这条漏了——**这类缺陷依赖调用方环境，CI/别人的机器上可能看起来正常**，必须钉在脚本里。
- **修法**：新增 `Invoke-GitUtf8`（捕获期间 `[Console]::OutputEncoding = UTF8`，`try/finally` 复位），两处 `git log` 改走它；重跑 `make-release-notes.ps1` 重生成两版 notes ⇒ 实测 `RELEASE-NOTES-2.7.2.0.md` 2395 B、`RELEASE-NOTES-2.3.0.2610041706.md` 2404 B，中文逐字正确（`docs(pilot): 旧预览 v2.3.0.2610022049-preview 已按用户指令删除…`）。
- **常驻守卫（新增，防它回来）**：`ForgeSelf.Api.Tests/RepositoryScriptTests.cs.GitLogCapturingScripts_MustSwitchConsoleToUtf8First` —— 扫 `scripts/**.ps1`，凡出现 `& git log` 或 `Invoke-GitUtf8 log` 都必须**在第一次捕获之前**设过 UTF8 码页；带阳性对照（扫到 0 个文件即红）。**反向探针实红**：临时摘掉 `make-release-notes.ps1:47` 那行 → 红并点名 `scripts/release/make-release-notes.ps1（捕获位置 2165，切码页位置 -1）`；还原后 `grep -c 临时探针 = 0`、`RepositoryScriptTests` 复跑 **20/20 绿**（`logs/utf8guard-probe.log` / `utf8guard-run2.log`）。
- **档位如实**：这条守卫属 `ForgeSelf.Api.Tests` 全量档（用户指令不跑全量），本会话按**定向过滤集** `--filter ~RepositoryScriptTests` 跑过并留日志；宿主全量仍未复跑。
- **一处我写错了、现已按实测更正的事实（17:5x）**：上一版本节写「已经打进 `2.7.2.0` / `2.3.0.2610041706` **zip 内部**的那份 notes 仍是乱码版」——**包内根本没有 notes**。实测：`zipfile.namelist()` 在两版包里 `.md` 条目数都是 **0**（`2.7.2.0` = 806 条目 / 0 个 .md；连 10-03 的 `2.3.0.2610031922` 也是 806 / 0），`RELEASE-NOTES-<ver>.md` 只存在于**更新源目录**那份，页面读的也正是目录那份。⇒ 正确结论是：**乱码不曾进入任何交付包**，只需要目录里那份重生成（已做）；"下次发布起包内也随之干净"这句同样作废（包内从来没有那份文件）。

**F. 发布通道实测（用户输入10 选定 B「跳号发 2.7.2.0」）**：

| 产物 | 命令 | 按**包内容**的验真读数 | 等级 |
|---|---|---|---|
| 宿主整包 `2.3.0.2610041706` | `release-local.ps1 -Version 2.3.0 -UpdateDir D:\src\my-proj\OpenForgeSelf\updates`（全链五步不跳段，291s） | zip 103.8 MB；SHA256 `0faa2570…cbf6` 与 `SHA256SUMS.txt` **逐字一致**；`versions/2.3.0.2610041706/`；包内 `plugins/design-system/plugin.json = 3.1.0`（`logs/release-local-1004.log`） | Verified |
| 宿主整包 **`2.7.2.0`（跳号包，给老宿主用）** | `release-local.ps1 -Version 2.7.2.0 -UpdateDir …`（四段完整串幂等原样、**不补时间码**，249s） | zip `OpenForgeSelf-2.7.2.0-win-x64.zip` 108,881,764 B / 806 条目；`versions/current = 2.7.2.0`；**两个 exe 的 FileVersion 都是 `2.7.2.0`**、ProductVersion `2.7.2.0+2ceae29…`（commit sha 带在串里）；SHA256 `d4730983…5985b` 与台账一致；`RELEASE-NOTES-2.7.2.0.md` 在位；包内 `plugins/DesignSystem/plugin.json = 3.1.0`（`logs/release-local-2720.log`） | Verified |
| 插件单包 | `package-plugin.ps1 -Plugin DesignSystem -OutDir … -Force` | 见上方 C 段（8 条目 / `plugin.json` 3.1.0 / 只带插件 DLL / 前端与门禁② 同尺寸） | Verified |

**为什么必须跳号（不是我图省事）**：`:51888` 实测跑的是 `D:\src\tools\ForgeSelf\versions\2.2.11\versions\2.2.2026.0930\ForgeSelf.exe`，PE 戳 **`2.7.1.0`**（`GET /api/update/status` 的 `currentVersion` 就读它，`UpdateChecker.cs:288-300`；`versions/current` 指针改不动显示）。该构建**早于 §4-R10 ④ 的世代兜底** ⇒ 即便更新源目录里已有 `2.3.0.2610041706`，它仍回 `latestVersion=2.2.9 / hasUpdate=false`（认不出 10 位时间码串）。`2.7.2.0` 四段每段 ≤65535 且数值高于 `2.7.1.0`，老比较逻辑成立 ⇒ 页面才给得出"有更新"。这次跳号是**一次性例外**，已写进真源 §4-R10 ⑦ 与变更记录。
**升级后仍要说清的一条限制**：`2.7.2.0` 包里已经带 3.1.0 插件 ⇒ 升完宿主再拿插件单包"检查更新"会回**已是最新**（版本相等）——那只能证明"扫描 + 版本比较"链路在位，**证明不了"插件换文件"这个动作本身**；要证它得等下一次插件版本增量（M4 或 3.1.1），**本批不伪造高号包**。

**D. 10-04 终态门禁（跑的是"快"档 + 插件层 e2e 串行；宿主全量 `dotnet test`、宿主 `pnpm run check` 全量、深档全量 e2e **本轮均未复跑**——用户 10-03 23:43 指令「本轮会话仅仅跑设计系统的测试，不跑全量」）**：

| 层 | 命令 | 读数 | 来源等级 |
|---|---|---|---|
| ① 后端过滤集（终态） | `dotnet test ForgeSelf.Api.Tests --no-build --filter "FullyQualifiedName~DesignSystem" --logger "console;verbosity=normal"`（16:0x，串行，TMP/TEMP 固定 `.temp/ds-m1/tmp`） | **通过数 539 == `--list-tests` 发现数 539**、`测试运行成功。`、失败 0；539 = 原 529 + 守卫 10。日志 `logs/clean-filter-run.log` / `logs/clean-filter-list.log` | Verified |
| ① 前一轮那 1 条红的归因（写实） | 同一命令，15:44 轮：通过 538 / 失败 1 | 红在 `StyleAxisPerformanceTests.NFR_默认档生成耗时_与非默认档中位数的比值不超过上限1_5 (fontPairing=editorial)`，原文 `实际默认档中位 2.84ms、非默认档中位 4.58ms，比值 1.61（每种取值取 25 次）, but found 1.6103`。**责任在我**：那 12 分钟里我并发起了一次 `dotnet build --no-incremental`，抢同一输出树、产生 48 条 MSB3021/3027 重试，把计时抬高了。**判据宽度一字未放**，串行复跑后 539/539 全绿 ⇒ 这条计时判据对负载敏感，跑它时必须串行（已入 agent-workflow.md） | Verified（两轮日志都在） |
| ② 插件 web | `pnpm run check / test / build`（10-04 12:46–13:25 那轮） | check 通过、test **250 passed / 19 文件**、build `index.js 442,971 B` + `style.css 96,481 B`。**本轮不重跑的依据（实测不是推断）**：`find Plugins/DesignSystem/web/src Plugins/DesignSystem/web/package.json -newermt "2026-10-04 12:46"` 返回 **0 个文件** ⇒ 要被判的产物没有净变化 | Verified（`logs/ds-web-check-bizdirect.log` / `ds-web-test-bizdirect.log` / `ds-web-build-bizdirect.log` + `find` 依据） |
| ③ 插件层 e2e | `npx playwright test --config=playwright.config.ts e2e/plugins/design-system --workers=1 --output=%TEMP%/dsz-e2e-1004`（16:1x） | 见下一行 E | |
| ④ 包内容（插件本地包） | 读 zip 条目 | 上方 C 段（8 条目 / `plugin.json` 3.1.0 / 只带 `DesignSystem.dll` / 前端两文件与门禁② 逐字节同尺寸） | Verified |

**E. ③ 插件层 e2e（10-04 16:16 串行终态，`--workers=1`，输出目录在仓库外 `%TEMP%/dsz-e2e-1004`，跑完即删）**：
- 读数：**32 passed / 1 failed（6.2m）**，日志 `logs/e2e-1004-terminal.log`。这条判据不接受"差不多绿"，所以红的那条按原文入账：`design-system.spec.ts:636 expect(dtcg).toContain('$value')` 实得 `"500 "`（`GET .../export?format=dtcg&theme=light`）。
- **红因是实测出来的，不是猜的**：隔离宿主日志 `.temp/e2e/wt-b26d4625/backend.log` 里同刻的异常栈＝`DesignSystemController.Export → RequireProject → DesignProjectService.Find → XCode.EntitySession → DbSession.Execute → code = Busy (5), database is locked` ⇒ 正是已登记的 **G19/E8「只读路径撞 SQLITE_BUSY 直接 500」**，与断言宽度无关，也与本批规范功能无关。
- **偶发还是稳定，用重跑判定**：单独复跑同一条（`--grep "库驱动工作台全链路"`，`--workers=1`）→ **1 passed（2.1m）**，`logs/e2e-dtcg-retry.log` 内有 `design-system-v2-passed.log` 与 dtcg 200 回读 ⇒ **偶发撞锁**，不是 Biz 直查引入的稳定缺陷。
- **但有一条必须如实写的升级**：G19 此前只在**并行 4 worker**轮里出现过；这次是在**串行单轮**里出现的 ⇒ 撞锁概率并不只属于并发场景。且 10-04 的 Biz 直查把 47 处读路径改成直接打库（少了一层可复用结果），**"是否因此更容易撞锁"本批没有量化**（没有测同一路径的 DB 往返次数前后对比）⇒ 记为未证风险，README G19 已按实态升级。
- 对照上一轮：10-04 12:54（Biz 直查后、本会话改动前）同目录串行 **33 passed（6.0m）**（`logs/ds-e2e-bizdirect.log`）；10-03 21:26 也是 33 passed。⇒ 三轮里两轮全绿、一轮一条偶发红，与"新增红=我的"判责口径一起看：**本批没有新增稳定红，但接住 G19 的那条重试判据仍是缺的**。

### H. 签名重建 + 「执行脚本统一 pwsh」新规（输入11/12 · 2026-10-04 17:4x→18:0x）

**签名包实测（`release-local -Version 2.7.2.0 -UpdateDir … -Sign`，300s，`logs/release-local-signed.log`）**：

| 判据 | 读数 | 等级 |
|---|---|---|
| 签名段日志原文 | `复用已有自签证书: CN=OpenForgeSelf 铸己匣 (B7A5…CDC7)` → `签名完成：新增 2 / 跳过 1 / 失败 0`、`校验通过：3 / 3`（跳过那条 = `createdump.exe`，已是微软签名）。**该段在原始日志里是 GBK**，我按 GBK 解出 UTF-8 副本 `.temp/ds-m1/tmp/signed-block-utf8.txt` 才读得出来 | Verified |
| 包体 | `OpenForgeSelf-2.7.2.0-win-x64.zip` **108,893,574 B**（不签那版 108,881,764 B，差 11,810 B = 签名开销）；**806 条目**；`versions/current = 2.7.2.0`；`plugins/DesignSystem/plugin.json` 的 `Id=design-system / Version=3.1.0`（包里 18 份 manifest 全部逐份读出） | Verified |
| **从包里抽出的 exe**（不是从布局目录读） | 根 `ForgeSelf.exe` 401,000 B 与 `versions/2.7.2.0/ForgeSelf.exe` 16,473,448 B：`FileVersion` 均 **`2.7.2.0`**，`Get-AuthenticodeSignature` 均 **`Status=Valid`** | Verified |
| SHA256 | `1d12819b2c192aefd97c023cc39cd35e33688cbfad67d849ccb78dfe9b8058f6` —— `artifacts/release/SHA256SUMS.txt` 与 `updates/SHA256SUMS.txt` 与实测 `sha256sum` **三处逐字一致**（两份 zip 也是同一串） | Verified |
| 更新源目录世代混放（真源 §4-R10 ⑦ 的降级陷阱） | `updates/OpenForgeSelf-2.3.0.2610041706-win-x64.zip` 与其 notes **改名**为 `.zip.bak` / `.md.bak`（**不删除**）⇒ `*.zip` 扫描现在只看得见 `2.7.2.0` 一枚；插件单包 `design-system-3.1.0.forgeself-plugin` 留在原位 | Verified |

**我自己违反了新规一次，当场记下的证据**：这条发布命令用的正是被禁止的 `powershell -NoProfile … -File`。后果不是"风格问题"而是**上面那一格**——签名段的中文全被 5.1 按 GBK 写进了日志，我不解码就读就等于读不到；产物本身没被污染（notes 干净是因为脚本内部自己切了码页）。**后续同类命令一律 `pwsh`**，本会话末尾的工件门禁复跑已改走 `pwsh`。

**新规落点（八个文件，全为文本/注释，零行为改动）**：`AGENTS.md` **三处**（§2.3 新条目＝规则本体，含三条实测代价 + 唯一例外 + BOM 仍必需；§0 出口门禁的 `install-git-hooks.ps1` 写法补成 `pwsh -NoProfile … -File`；§5.6 末尾一条「本节所有 PowerShell 入口一律 pwsh，且判定看日志正文不看 exit code」）、`docs/04-standards/agent-workflow.md` §B6 头部两条铁律 + Part C 一行变更记录、`.agents/skills/plugin-publish-verify/SKILL.md`（发布命令口径）、`.agents/skills/e2e-testing/SKILL.md`（原"签名必须 pwsh"升为全局规则的局部体现）、`.agents/skills/design-system-verify/SKILL.md`（④ 发布示例换 `pwsh` 且补 `-Sign`）、`Plugins/DesignSystem/README.md` §3.5（出包命令）、`scripts/verify-pilot-artifacts.ps1` 与 `scripts/install-git-hooks.ps1` 的 `.EXAMPLE` 注释（**字节级替换、BOM 前后都是 True、替换前先断言命中数=1**）。CI 侧无需改：`.github/workflows/release.yml:40/46` 本来就是 `shell: pwsh`；仓内也早有同向守卫 `ForgeSelf.Api.Tests/Plugins/TerminalCommandGuardTests.cs:40`（插件终端白名单只放行 `pwsh`）。**未改的三处 `powershell.exe`**（`scripts/hooks/pre-commit:24` + 宿主 `RuntimeDetector.cs:108`、`StagedUpdateService.cs:252`）与理由 → TODO P2 + 台账 **028**。

**守卫复跑（改 `scripts/**` 触发，档位如实）**：`dotnet test --no-build --filter "FullyQualifiedName~RepositoryScriptTests"` → **失败 0 / 通过 20 / 总计 20**（`logs/repo-script-guard-after-pwsh-docs.log`）。**为什么用 `--no-build`**：`ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpCenterRuntimeTests.cs` 的 mtime 比现存测试 DLL（17:39）**新**，且有 5 个 `dotnet` 进程在飞 ⇒ 并行会话正在改测试源码；此时全量 build 会把**别人的在飞源码**建成我的红灯，也可能撞 DLL 锁。本次改动面是两个 `.ps1` 的**注释行**，该守卫在运行时读仓库文件、不需要重编译即可判定 ⇒ 定向过滤集是等价覆盖，**宿主全量与深档仍未复跑**（用户指令）。

**顺手抓出并修复的一处旧文档损坏（不是本批引入，但今天被我撞见）**：`docs/04-standards/packaging-upgrade-backup.md` 的 `Authenticode 签名` 行被**一个行内换行 + 一个 0x0B 控制符**切成两行（`TODO.md` 里同源一处 0x0B）。`git show HEAD:` 证实 **HEAD 里就坏了**（该文件 CR=1 / VT=1），成因是历史上用 PowerShell **双引号字符串**打补丁：反引号是 PS 的转义符，`` `r ``→回车、`` `v ``→垂直制表符、`` ` ``+空格→反引号被吞 ⇒ `` `release-local.ps1 -Sign` `` 落成了 `elease-local.ps1 -Sign`、`` `versions/<ver>/` `` 落成 `ersions/<ver>/`。已按**字节级**还原三处（替换前断言命中数=1），并全量扫描本次 128 个改动路径的控制字符 → **0 残留**；§1.1 那张表逐行核对列数（14 行 × 每行 4 个竖线 = 3 列一致）。教训一并入本节：**中文与反引号不得穿过 PS 双引号字符串写文件**（与记忆「中文串里的 ASCII 引号会断句」同族，新增"反引号=PS 转义符"这一条）。

**改动计数最新实测（18:3x，HEAD 已是并行会话的 `d88d709`）**：porcelain `-uall` = **137 行** = 本批 **112**（86 已跟踪改 + 26 新增未跟踪）+ 外来 **25**（全部未跟踪）。本轮 H 段自己的改动**没有新增文件**（都落在已计数文件里：`AGENTS.md`、`agent-workflow.md`、`plugin-publish-verify`、`e2e-testing`、`design-system-verify`、`Plugins/DesignSystem/README.md`、真源、台账、两个 `.ps1` 注释、05/07/03、`TODO.md`（不入库））。**113→112 的那一跳不是我删了东西**：并行会话 18:2x 的提交 `d88d709` 把本批范围内的 `ForgeSelf.Web/e2e/plugins/design-system/design-system-agent.spec.ts` 一起带走了 ⇒ 该文件已不在"未提交"集合里（内容没丢，现在对该文件 `git diff HEAD` 为空；但**那次提交没过本批闸门3**，属治理事实，已记 07 §3 头部与 §7 第 4 条）。

## 插件维护闭环五步对账（`plugin-development` §四 · 验收方按此表点收）

> 为什么要单列这张表：四层门禁是"测得对不对"，五步闭环是"这一版有没有真交付到用户手里"。两者口径不同，混着报就会出现"门禁全绿但插件从没发布/从没在真实实例上看过一眼"。本轮按步逐项交代**做到什么程度、被谁 gate 住**。

| 步 | 本批做到哪 | 状态 / 被谁 gate |
|---|---|---|
| ① 门禁（插件后端 build+test、插件前端 build/check/test） | 后端过滤集 **539 == `--list-tests` 发现数 == 539 通过 / 0 失败**（10-04 16:0x 终态，`logs/clean-filter-run.log` + `clean-filter-list.log`；= 10-03 20:12 的 **529** + Biz 直查守卫 10 条，529 降为历史轮次）；`pnpm run check` 0 error、`test` 250（19 文件）、`build` → `dist/index.js` 442,971 B + `style.css` 96,481 B | ✅ Verified |
| ② 插件层 e2e（`e2e/plugins/design-system` 全目录，隔离实例、零 mock；三轮都串行 `--workers=1`） | 10-03 21:26 **33 passed**（`e2e-full-after-g7.log`）；10-04 12:54（Biz 直查重构后）**33 passed（6.0m）**（`ds-e2e-bizdirect.log`）；10-04 16:16（本会话终态，含守卫与脚本改动）**32 passed / 1 failed（6.2m）**——红在 dtcg 导出回 500，实测栈为 `SQLITE_BUSY / database is locked`（G19/E8 同形态），单独复跑同一条 **1 passed（2.1m）** ⇒ 偶发。**判据未放宽、未重跑刷绿、串行轮出现 G19 这一事实已升级为 README 的更严口径**（详见上一节 E 段） | ✅ Verified（三轮日志都在） |
| ③ 发布（打 tag → CI GitHub Release，或本地目录更新源 + 页面自动更新） | **两条通道各自的状态分开记**：(a) 宿主整包——10-03 的 `2.3.0.2610031922` 只按包内容验真、未 `-UpdateDir`；**10-04 已按用户指令走本地目录更新源通道**（输入7/10/11）：`-UpdateDir D:\src\my-proj\OpenForgeSelf\updates` 出过 `2.3.0.2610041706`（未签，现已改名 `.zip.bak` 退出扫描）与**跳号签名包 `2.7.2.0`**（`-Sign`，两个 exe 从包里抽出实测 `Status=Valid`，SHA256 三处一致）；**仍未打 tag、未推远程**；(b) **插件级本地包已产出**（用户 2026-10-03 23:43 指令「发布本地插件，我将在 51888 选择本地目录更新插件」）：`D:/src/my-proj/OpenForgeSelf/updates/design-system-3.1.0.forgeself-plugin`（594,334 B，SHA256 `C08832C8…F3E4`），**判包内容**（`plugin.json` 3.1.0 / 只带 `DesignSystem.dll` / `web/dist` 两文件与门禁② 逐字节同尺寸）——这一步在 10-04 之前是**红的**（`Compress-Archive` 不认 `.forgeself-plugin`），修在 `scripts/package-plugin.ps1`，详见上方「接手与收尾」C 段与 F/H 段 | 🟡 本地包（宿主签名版 + 插件包）✅ 已交付到更新源目录；⬜ tag / push 待用户明确授权（agent 不擅自打 tag、不推远程）；⬜ ⑤ 待用户在页面点「检查更新 → 重启并更新」之后 |
| ④ 走查（e2e 隔离实例按用户视角点一遍 + 截图读图 + 清测试数据） | **已做成常驻用例而不是一次性手点**：G4「§G8 DOM 契约逐项走查」逐项核控件定位/可交互/词表与 `meta` 同源/规则行数与 REST 一致，并自带**全程零写入**反证（`writes` 数组必须为空）；**V4 把"插件自己的控制面"也做成常驻走查**（轴面板 7 条轴 + 衣柜 13 件：选中态看得见且==这件衣服真值、滑块区间==meta、三档视口不横向裁切，此前该界面**一张截图都没进过证据**）；视觉矩阵 **59 张逐张读图**（该目录当前共 155 张 png，含历史片）；测试数据落在本 worktree 的隔离实例目录 `<仓库根>/.temp/e2e/wt-<hash8>`（`global-setup.ts` 明文"绝不触碰用户实例 `:51888`"，每次运行先杀自家残留再重新发布） | ✅ Verified（**真人外行走查未做**，与 M2 V12 同口径如实登记） |
| ⑤ 运行实例只读复验（用户启用/更新到新版之后才成立） | 未做。前提不成立：用户尚未把 `D:\src\tools\ForgeSelf` / `:51888` 更到含 3.1.0 的版本，而 ③ 还没被授权；铁律禁止 agent 停/启/杀用户宿主 | ⬜ 待 ③ + 用户更新后（续做入口见 `TODO.md` M3 条目） |

- **16:41 之后为什么不必重跑 ②③（18:38 重测、18:52 复核；实测依据，不是"我想当然"。门禁④ 已在 18:50 因生产码净改动而作废重做，见本节第三条）**：`find Plugins/DesignSystem ForgeSelf.Api.Tests/Plugins ForgeSelf.Web -type f -newermt '2026-10-03 16:41' ! -path '*/obj/*' ! -path '*/bin/*' ! -path '*/dist/*' ! -path '*/node_modules/*'` 列出的**代码类**文件只有 —— `README.md`/`ROADMAP.md`（文档）、`ForgeSelf.Api.Tests/…/GuidelineExportTests.cs`、`StyleAxisTests.cs`（**纯测试文件**），外加 `Plugins/DesignSystem/Services/GuidelineService.cs`（**该件单独交代见下一行**）。
  **那件服务码单独交代**：18:21 为做"手写规范不被误拒"的反向探针，我往里临时加了一条越界守卫（"正文含数字单位即拒"），18:22 跑出双红后**当场逐字还原**，18:23 复跑该测试类 **12/12** 绿、全仓 `grep -rn 临时探针 --include=*.cs` **0** 命中 ⇒ **净改动为零**（该文件是 M3 新增、尚未入库，没有 `git diff` 可作对照，所以这里给的是"探针文本残留 0 + 还原后同码复跑"两条实测）。 **插件生产码（除上述已还原的探针）与前端源码（`web/src/**` 非 test）零改动** ⇒ e2e 跑的宿主产物、插件 web vitest 跑的源码都没变，16:41 的 **30 条 0 红** 与 16:38 的 **250/19** 仍是终态。**但这句话本身后来被三次重跑取代，按实态追记**：19:21 因生产码净改动重跑全目录（30 条 0 红）、20:22 因规格反向覆盖审计新增 G5/G6 再重跑（**32 条 0 红**）、21:26 因 X1 网关链审计新增 G7 再重跑（**33 条 0 红，`e2e-full-after-g7.log`**），全过滤集也从 519 走到 **529**（见 Unit Test 节 ⑧⑨ 与「E2E · 全目录回归」两轮账）。 **门禁④（本地 zip 产物走查）已重做**：18:50 之后实现码有净改动 ⇒ 15:57 那份包不再等于当前源码产物，按上面自己立下的规矩，19:22 **重新打包并把包内容判据逐条重跑**（新串 `2.3.0.2610031922`，实测见「发布与产物走查」节）。
- **18:50 出现了一条真正的生产码净改动 —— 上一段的前提就此失效，按规矩办**：重测 V3 的构建警告时发现 `DesignReviewService.cs:128` 的 CS8602（`SeverityOf(String? level)` 签名比契约宽），已把签名收成 `String level` 并补常驻守卫用例 `AC18_脏规则行_级别为null_checklist不抛且回落SHOULD`（修前先跑基线 9/9，见 AC18 行）。影响面交代清楚：① **行为零变化**（`ReadRules` 早已把缺失级别规一成 SHOULD，这条不可达崩溃；规一本身由新用例钉住）；② 插件构建警告 **372 → 371**、0 错误；③ 后端全过滤集**重跑**（那一轮 **519**，含新用例；20:12 的终态轮是 **529**）；④ 因为我改了插件 DLL 的源码，**15:57 那份本地 zip 不再等于当前源码的产物** ⇒ 按我自己写下的规矩（「若实现码发生净改动，必须重新打包并重走门禁④」）**重新 `release-local.ps1 -Version 2.3.0` 并按包内容重走门禁④**（不打 tag、不 `-UpdateDir`、不碰任何用户宿主）；⑤ **插件前端 e2e 需要重跑，这里一度写错已更正**：我 18:52 写的是"改动只在 `Services/*.cs`、前端源码零改动 ⇒ e2e 不必重跑"——**推理有洞**：`Plugins/DesignSystem/Services/*.cs` 编进 `DesignSystem.dll`，而 e2e 走的是**真实宿主加载这个 DLL** 的链路（`design_review mode=checklist` 恰好经过我改的 `SeverityOf`）。前端 `web/src/**` 零改动只能免除**门禁②（插件 web 三件）**，免除不了②'s e2e 层。⇒ 已排入重跑：`e2e/plugins/design-system` 全目录、`--workers=1`、钉 `E2E_FRONTEND_PORT=7412 / E2E_BACKEND_PORT=7512`（实测端口空闲；并行会话在场，按技能 #50）。门禁②（web check/test/build）确不需重跑，依据是 `find` 实测 16:41 后无 `web/src` 非测试文件改动。
- 本轮四条「不做事决策」已按 AGENTS §10.4 入台账：**`docs/07-decisions/not-taken-decisions.md` 024–027**（读侧滞后不在本批修 / 字体搭配轴的 font-family 投影缺口不在本批修 / 规范端点不单开 404 小灶 / 展厅换装窗口不在本批改），每条含被否方案、理由与重新审视的触发条件。

## Static Analysis

| 检查 | 命令 | 结果 | 来源等级 |
|---|---|---|---|
| 插件 csproj | `dotnet build Plugins/DesignSystem/DesignSystem.csproj --no-incremental`（18:50 终态重测，`plugin-build-noinc-final.log` / 修复后 `plugin-build-after-signaturefix.log`）| 终态（18:59，删掉 `MaxCacheCount` 死字段后重测）：**0 错误 / 370 警告**；轮次真数 **372 →（修 CS8602）371 →（删死字段）370**。**警告归因才是 V3 的判据**：**368 条落在 `Data/Entities/*.cs`**（xcode 生成物的 CS8618/8601/8603，宿主既有形态、Forbidden 改生成物），手写码只剩 **2 条** —— `Services/StylePresets.cs:67` 的死常量 `defaultThemes`（CS0219），`git log -S defaultThemes` 实测归属 **M1 提交 `5fa914c`**，非 M3 引入 ⇒ 记 TODO(P3) 不顺手修；另一条 `Data/Entities/DesignGuideline.Biz.cs:34` 的 `MaxCacheCount`（CS0414 赋值后从未使用）**是本批新增文件**，根因：本插件每个实体的 `.Biz.cs` 都按模板带这个字段，用的却是生成物 `.cs` 里的 `Meta.Session.Count < MaxCacheCount` 缓存查询分支——xcode 给 `DesignGuideline` 生成的那份**没有**缓存分支（本表按 `GuidelineRepository` 的"直查库判重"纪律走），字段剩个空壳 ⇒ 删字段并留一行说明，**删后实测 371 → 370 / 0 错误**，且终数轮全量已在删之后重跑（`filter-run-terral.log` + `list-tests-terral.log`），不让文档的数对不上源码。原先 `DesignReviewService.cs:128` 的 CS8602 已在收口时修掉（签名收紧，见 AC18 行） | Verified（原文见「Build」） |
| 插件前端类型 | `cd Plugins/DesignSystem/web && pnpm run check`（借宿主 `vue-tsc --noEmit -p tsconfig.check.json`） | **0 error**（只有一条 pnpm 配置的 WARN，与本批无关） | Verified |
| 宿主前端 | `cd ForgeSelf.Web && pnpm run check`（`vue-tsc -b && eslint`） | **0 errors / 81 warnings**，EXIT=0——81 条与开工基线逐字相同（都是既有 `.vue` 的格式 warning，`src/views/*`），本批**没有新增**一条；e2e 目录在同一套 eslint/tsconfig 射程内，**V4 新增后连跑三次（16:29 / 16:34 / 16:36）仍是 0 error / 81 warning**（含 V4 首版与加 ①b 后的版本），**加 G7 后又跑两次：21:14（新 spec 写完）与 21:55（反向探针还原后的终态文件复验），两次都是 0 error / 81 warning ⇒ 累计第 10 次同基线** | Verified（`.temp/ds-m1/logs/host-check-m3.log`、`e2e-m3-v4b-check.log`、`e2e-m3-v4c-check.log`、`web-check-g7.log`（21:14）、`web-check-g7-final.log`（21:55，终态）） |
| 宿主前端单测 | `cd ForgeSelf.Web && pnpm run test` | **741 passed / 741**（65 文件） | Verified（`.temp/ds-m1/logs/host-test-m3.log`） |
| 插件前端单测 | `cd Plugins/DesignSystem/web && pnpm run test` | **250 passed（19 文件）**（终态 16:38，含本轮新增 `sections/ReleaseBoard.test.ts` 3 条；此前 247/18 文件） | Verified（`.temp/ds-m1/logs/plugin-web-test-releaseboard.log`） |
| 已知盲区（不是本批引入） | 插件 `src/**` 无独立 eslint 入口（宿主 flat config 覆盖不到 `../Plugins/**`）→ 插件前端只有类型检查，没有 lint | 登记于 `design-system-verify` 技能第五节 | Unknown（未验证，非本批范围） |

## Screenshots

<!-- 视觉 QA 矩阵：轴 × 取值 × 场景；路径 ForgeSelf.Web/screenshots/e2e/design-system/m3-*.png；逐张读图结论；无法读图写 Unknown 并列路径 -->

**这批 PNG 与它们同目录的 `[evidence]` 日志不入库**（`.gitignore:33` 的 `screenshots/` 命中整棵树，实测 `git check-ignore -v` 打到 `ForgeSelf.Web/screenshots/e2e/design-system/m2/style-V3-passed.log`）⇒ 证据的耐久性分两层写：**用例级机器读数**（13×2 对比度全表、注入 CSS 声明数、逐轴变化变量数、缺陷 #8 的 `transparent` 修法）已经落在上面「V 块」表格正文里，**逐张的读图结论**落在本表每一行；而**逐张的像素级数字**只存在于同目录那些 `[evidence]` 日志（不入库），所以这里不宣称"每张的数字都留了档"——换机复核的正确做法是按上面「E2E · 全目录回归」小节的**跑法**（`--config=playwright.config.ts` + `--workers=1` + 显式钉 `E2E_FRONTEND_PORT/E2E_BACKEND_PORT` + `--output` 指空目录）单独重跑 `e2e/plugins/design-system/design-system-style.spec.ts`，重新生成图与 `[evidence]` 日志，再对着本表逐行比。**结论不依赖"图还在"，但依赖"能重跑"**；同理 AC26 的规范清单原件在 `.temp`（也不入库），所以整篇嵌进了本文件。

| 截图 | 对应轴 / 取值 | 读图结论 | 来源等级 |
| ---- | ------------- | -------- | -------- |
| **V1 · 13 预设 × 后台/中台 × 明暗（26 张，全部逐张看过）** | | | |
| `m3v1-admin-calm-admin-light` | 预设 `admin-calm`（默认轴：soft 阴影 / regular 描边 / brand 色温 / modern 字族 / soft 圆角 / complement） | 冷灰底 + 蓝主色 + 琥珀图表，卡片多层软投影，圆角中等；文字层级清楚，无重叠/溢出 | Verified |
| `m3v1-admin-calm-admin-dark` | 同上（深色档） | 深蓝黑底、白字、浅蓝/桃色图表；卡片靠 1px inset 高光勾边（不是黑块叠黑块）；对比度机器判据通过 | Verified |
| `m3v1-workbench-focus-admin-light` | 预设 `workbench-focus` | 冷灰 + 靛紫/橄榄图表，与 admin-calm 同族但色相更冷、更"工具台" | Verified |
| `m3v1-workbench-focus-admin-dark` | 同上（深色档） | 深底 + 长春花蓝/沙色；**与 admin-calm/finance-trust 的深色档差别较小**（色相接近，区分主要靠 chart/brand 色相）——不是缺陷，但"深色档一眼可辨"弱于浅色档，如实记下 | Verified |
| `m3v1-finance-trust-admin-light` | 预设 `finance-trust` | 冷灰底 + 深青/棕，稳重低彩；文字与卡片边界清楚 | Verified |
| `m3v1-finance-trust-admin-dark` | 同上（深色档） | 蓝底 + 中蓝/蜜桃色；同上"深色档区分度较小"的观察 | Verified |
| `m3v1-healthcare-gentle-admin-light` | 预设 `healthcare-gentle` | 薄荷白底 + 青绿/藕粉，圆角偏大、投影柔；视觉"轻"得出来 | Verified |
| `m3v1-healthcare-gentle-admin-dark` | 同上（深色档） | 绿调深底 + 青绿/粉；标题与卡底对比达标 | Verified |
| `m3v1-commerce-vivid-admin-light` | 预设 `commerce-vivid` | 暖灰底 + 高饱和橙红/青，促销感明显 | Verified |
| `m3v1-commerce-vivid-admin-dark` | 同上（深色档） | 暖褐底 + 珊瑚/青绿；深色下饱和度仍跳得出来 | Verified |
| `m3v1-media-bold-admin-light` | 预设 `media-bold` | 淡紫灰底 + 品红/深绿，**大字号数字最抢眼**（typeRatio 效果看得见） | Verified |
| `m3v1-media-bold-admin-dark` | 同上（深色档） | 近黑紫底 + 亮粉/绿；大数字在深底上仍清晰，未被"深底深字"坑到（M2 修过的那类缺陷没复发） | Verified |
| `m3v1-education-friendly-admin-light` | 预设 `education-friendly` | 鼠尾草绿底 + 绿/紫，数字呈衬线感（display 字族在 stat 值上取得到，见 Known Limitations #1 的收窄说明） | Verified |
| `m3v1-education-friendly-admin-dark` | 同上（深色档） | 绿调深底 + 亮绿/浅紫；圆角明显，亲和感在 | Verified |
| `m3v1-mobile-fresh-admin-light` | 预设 `mobile-fresh` | 淡青白底 + 青/藕粉，圆角与留白偏"移动" | Verified |
| `m3v1-mobile-fresh-admin-dark` | 同上（深色档） | 蓝黑底 + 青/粉；与浅色档同一性格，切换不串味 | Verified |
| `m3v1-editorial-serif-admin-light` | **新** `editorial-serif`（`fontPairing=editorial` + warm 色温） | 纸感暖底 + 砖红/墨绿；**大数字明确呈衬线**（stat 值取到 display 字族），页标题仍是正文字族 → 与 Known Limitations #1 完全一致 | Verified |
| `m3v1-editorial-serif-admin-dark` | 同上（深色档） | 暖黑底 + 粉/薄荷，衬线数字在深底上仍读得出；无低对比文字 | Verified |
| `m3v1-flat-minimal-admin-light` | **新** `flat-minimal`（`shadowStyle=flat` + 极低彩度） | **卡片没有投影，只有 1px 环线**（与其余 12 件一眼可分）；图表是去饱和的石蓝/灰紫 | Verified |
| `m3v1-flat-minimal-admin-dark` | 同上（深色档） | 近黑中性底 + 环线勾边；`pure` 中性色温在深色档最明显（不带蓝调） | Verified |
| `m3v1-warm-craft-admin-light` | **新** `warm-craft`（`neutralTemp=warm` + `radiusStyle=round`） | 米黄纸底 + 橙/芥末，圆角与软投影都偏"手作"；色温差异肉眼直接可辨 | Verified |
| `m3v1-warm-craft-admin-dark` | 同上（深色档） | 褐底 + 橙/卡其；暖色温在深色档同样成立（不是只在浅色档有效） | Verified |
| `m3v1-tech-crisp-admin-light` | **新** `tech-crisp`（`shadowStyle=crisp` + `radiusStyle=sharp`） | 单层紧投影（实测 `0px 2px 4.6px 0px`）+ 小圆角 + 紫/橄榄高对比 → "锐利"性格出来了 | Verified |
| `m3v1-tech-crisp-admin-dark` | 同上（深色档） | 深底 + 紫罗兰/芥末，勾边 + 单层投影；与 flat-minimal 的"纯环线无投影"能分开 | Verified |
| `m3v1-kids-playful-admin-light` | **新** `kids-playful`（`radiusStyle=pill` + 高彩） | **圆角明显最大**（卡片/按钮/导航项都近胶囊），品红 + 芥末，性格最外放 | Verified |
| `m3v1-kids-playful-admin-dark` | 同上（深色档） | 暗紫底 + 亮品红/芥末，圆角依旧；文字对比达标 | Verified |
| **V2 · 5 件新预设 × 落地页 / 移动端 × 明（10 张）** | | | |
| `m3v2-editorial-serif-landing-home-light` | editorial × 落地页 | 纸感底 + 砖红 CTA、薄荷徽标；衬线大标题（hero 取到 display） | Verified |
| `m3v2-editorial-serif-mobile-home-light` | editorial × 移动端 | 手机框内砖红 CTA + 藕色卡；390 框不溢出 | Verified |
| `m3v2-flat-minimal-landing-home-light` | flat-minimal × 落地页 | 全页无投影、只有环线；方形按钮 + 灰底 CTA，"扁平"在落地页同样成立 | Verified |
| `m3v2-flat-minimal-mobile-home-light` | flat-minimal × 移动端 | 环线卡片 + 石蓝 CTA；底部导航选中态与上方按钮间距偏紧（**两种皮肤一致 → 属模特页布局而非换肤**，非 M3 缺陷，记观察） | Verified |
| `m3v2-warm-craft-landing-home-light` | warm-craft × 落地页 | 米黄底 + 棕 CTA、圆角卡片；暖色温在落地页比后台更明显 | Verified |
| `m3v2-warm-craft-mobile-home-light` | warm-craft × 移动端 | 米黄手机框 + 棕 CTA；与 flat-minimal 同页对照差异一眼可辨 | Verified |
| `m3v2-tech-crisp-landing-home-light` | tech-crisp × 落地页 | 紫 CTA + 紧圆角 + 勾边卡片；这一张取景未裁右缘，整页布局无重叠 | Verified |
| `m3v2-tech-crisp-mobile-home-light` | tech-crisp × 移动端 | 紫 CTA、方形卡片；列表行高与标签位置正常 | Verified |
| `m3v2-kids-playful-landing-home-light` | kids-playful × 落地页 | 大圆角卡片 + 品红 CTA；定价卡与订阅框无溢出 | Verified |
| `m3v2-kids-playful-mobile-home-light` | kids-playful × 移动端 | 胶囊按钮 + 品红 CTA；与 flat-minimal 移动端同页型对照，圆角差最直观 | Verified |
| **V3 · 7 条轴 × 19 个非默认取值 + 默认档对照（20 张，缺陷 #8 修复后重拍，全部逐张看过）** | | | |
| `m3v3-baseline-admin-light` | **对照件**（admin-calm，七轴全默认） | 冷灰底 + 蓝主色 + 琥珀第二柱；卡片是多层软投影；表头/行分隔细；文字层级清楚。取景同 V1（顶部标题被裁半行、右缘横向滚动裁切 → 被裁区内容 Unknown，M2 已知限制 #5 同族） | Verified |
| `m3v3-shadowStyle-crisp-admin-light` | `shadowStyle=crisp` | 同色系下卡片投影收紧成单层贴边（computed `0px 2px 4.6px 0px`）；**与对照件的差别在 2200×1000 取景下肉眼可辨度弱**，成立证据是 5 个 `--ds-shadow-*` 变量差 + computed 值（不假装"一眼可辨"） | Verified |
| `m3v3-shadowStyle-flat-admin-light` | `shadowStyle=flat` | **一眼可分**：卡片完全没有投影，只剩 1px 环线勾边（computed `0px 0px 0px 1px`）；图表与文字不受影响（白名单外零改动） | Verified |
| `m3v3-shadowStyle-layered-admin-light` | `shadowStyle=layered` | 双层软投影，卡片"浮起"感比对照更明显（computed 两层：`…9.1px -1px, …0.8px 4.5px`）；可辨度中等 | Verified |
| `m3v3-shadowStrength-0-admin-light` | `shadowStrength=0`（缺陷 #8 的复验图） | **投影完全不可见**、卡片只留边界（computed `rgba(0, 0, 0, 0) 0px 2px 9.1px -1px`）——修复前这张是"不透明实心黑投影"（`rgb(15,23,42) …`），现在语义正确：0 = 看不见但令牌仍在 | Verified |
| `m3v3-shadowStrength-2-admin-light` | `shadowStrength=2` | 投影明显加重（alpha 0.14 → 0.28），卡片与底色的层次拉开；同方向另一端成立，说明公式整条接着 | Verified |
| `m3v3-borderStrength-bold-admin-light` | `borderStrength=bold` | 全站描边加重：表头分隔线、卡片边、表格行线都更黑更粗，"重描边"读得出来；**可辨度中等**（不是换色那种一眼差）。注：这张的元素截图区域随布局略有放大（`.ds-stage__viewport` 尺寸变了），不是取景错误 | Verified |
| `m3v3-neutralTemp-cool-admin-light` | `neutralTemp=cool` | 底/卡/表头整体偏冷蓝灰，与对照件同结构不同色温；9 个 `--ds-color-neutral-*` 变（可辨度中等，方向正确） | Verified |
| `m3v3-neutralTemp-warm-admin-light` | `neutralTemp=warm` | **一眼可辨**：米黄暖底（与 cool 是两个方向），文字仍清楚、对比达标 | Verified |
| `m3v3-neutralTemp-pure-admin-light` | `neutralTemp=pure` | 纯中性灰，既不带蓝也不带黄；三档色温可排开（cool ↔ pure ↔ warm），不是"只有一档生效" | Verified |
| `m3v3-fontPairing-system-admin-light` | `fontPairing=system` | **一眼可辨**：全站换 `system-ui`，数字与中文混排的字宽/标点位置都变了（computed 标题字族确实换成 `system-ui, -apple-system, …`） | Verified |
| `m3v3-fontPairing-humanist-admin-light` | `fontPairing=humanist` | 可辨：换成 "Source Sans 3"，字形更窄更人文，大数字 1,284 的笔画明显不同 | Verified |
| `m3v3-fontPairing-editorial-admin-light` | `fontPairing=editorial` | **与对照件看不出差别**（诚实记录）：这条只多出 `--ds-font-display` 变量，模特页标题/大数字都没取用它 → 正是 Known Limitations #1（导出丢 `fontFamily`）的精确边界；这条轴"肉眼可辨"在本批**不成立**，靠变量差留证 | Verified（不成立部分如实标注） |
| `m3v3-radiusStyle-sharp-admin-light` | `radiusStyle=sharp` | **一眼可辨**：方角——导航选中条、`+12%` 徽标、卡片、表格都成直角 | Verified |
| `m3v3-radiusStyle-round-admin-light` | `radiusStyle=round` | 可辨：圆角整体加大一档（介于对照与 pill 之间），13 个 `--ds-component-*-radius` 变 | Verified |
| `m3v3-radiusStyle-pill-admin-light` | `radiusStyle=pill` | **一眼可辨**：胶囊化（徽标与导航项全圆端），与 sharp 是同一轴的两端，方向正确 | Verified |
| `m3v3-accentStrategy-analogous-admin-light` | `accentStrategy=analogous` | 第二柱从琥珀换成**蓝紫**（邻近色策略），22 个 `--ds-color-accent-*` 变；主蓝不动 | Verified |
| `m3v3-accentStrategy-split-admin-light` | `accentStrategy=split` | 第二柱 = **砖橙/赤陶**，与 analogous、triadic 三张并排能明确排开 | Verified |
| `m3v3-accentStrategy-triadic-admin-light` | `accentStrategy=triadic` | 第二柱 = **玫红**（三角对位），文字对比仍达标 | Verified |
| `m3v3-accentStrategy-mono-admin-light` | `accentStrategy=mono` | 第二柱 = **中蓝**（与主色同族、只明度不同）——四档强调策略在图上真的分成四个方向，不是装饰 | Verified |
| **G4 · 「UX 规范」section 用户视角走查（插件闭环第④步，1 张）** | | | |
| `guidelines-g4-walkthrough` | 第 15 个 section 全貌（E2E 规范项目 v0.1.0） | 顶栏版本徽标「模型 3.1.0 · 生成器 3.1.0 · 投影 3.1.0」在位；左列规范卡片各带「N 条规则」+「生成器产出」徽标；右侧正文 textarea 显示原文（反引号路径**原样显示**，没有被当 markdown 渲染）、分类下拉 `layout`、规则 4 行每行 MUST/SHOULD/MAY 下拉 + 文本框 + 「删」、`添加规则` 整宽按钮、下方「引用令牌（当前值）」chips `space.4 = 8px / space.6 = 16px / space.5 = 12px / space.8 = 32px / breakpoint.2 = 768px`。排版无重叠、无截断、无空面板；唯一取景问题是列表首条标题被 section 内滚动推出一行（不是缺陷） | Verified |
| **V4 · 轴面板与衣柜＝插件自己的控制面（3 张，本轮新增）** | | | |
| `m3v4-axes-panel-1280` | 「更多风格选项」展开态（穿 `preset:admin-calm`，七轴全默认档） | 7 条轴全部在位且取值文案全部来自 `meta`：阴影风格（柔和/锐利/环线/多层）、描边强度（常规/加粗）、中性色温（品牌色温/冷调/暖调/纯中性）、字体搭配（现代无衬线/系统字体/人文无衬线/衬线标题）、圆角风格（柔和/锐利/圆润/胶囊）、强调色策略（互补/邻近/分裂互补/三角/单色）、阴影强度（1）+ 滑块。**选中档是蓝字 + 蓝描边**（实测 `rgb(58,112,238)` 字/边 vs 非选中 `rgb(51,65,85)` 字 + `rgba(15,23,42,0.08)` 边），一眼可分不是"看着差不多"；芯片换行整齐（色温第 4 档、强调策略第 4–5 档各另起一行），无重叠、无截断、无空面板；滑块停在 0~2 正中 | Verified |
| `m3v4-axes-panel-2200` | 同一面板在 2200 宽 | 与 1280 张**完全同形**（侧栏定宽 238px，不随视口拉伸）：证明宽屏下没有出现"控件被拉开/留白塌陷"；面板右缘 1688/2200 远在视口内 | Verified |
| `m3v4-wardrobe-1280` | 衣柜全貌（13 件预设） | 13 件全部列出且各带「预设」标 + 「加入对比」，**五件 M3 新预设的中文名都在**（杂志·衬线 / 极简·扁平 / 手作·温暖 / 科技·锐利 / 童趣·圆润）；选中件（后台·沉稳）蓝描边高亮，与非选中区分方式与轴面板一致；行高/间距均匀无遮挡；底部一行「试穿与微调都不会写入设计系统库。」——**这句在本用例里是可证的**：V4 全程网络收集里零写请求 | Verified |
| **取景与判据说明** | V4 拍的是**插件自己的界面**（不是被试穿的设计系统），所以不套"页标题对画布底 ≥4.5:1"那条机器判据；改用的三条是：选中态 `aria-checked` 与视觉类一致**且计算样式与非选中不同**、滑块区间与当前值逐字等于 `meta`/预设 `request`、面板 `scrollWidth<=clientWidth`（2200/1280/1024 三档实测 238/238、238/238、958/958） | 首版三条跑绿（`e2e-m3-v4-control-surface.log`，1 passed）；加 ①b 同源判据后我先自己写错一次（用 GET 打 `presets/recommend` 吃 **405**，`e2e-m3-v4b-tie.log` 实红），改 `apiPost` 后复绿（`e2e-m3-v4c-tie.log`，1 passed，`shadowStyle=soft` 来源=预设 request 字段而非默认回落） | Verified |

**读图时共同的取景说明（不是缺陷，但必须写清）**：V1 拍的是 `.ds-stage__viewport`，桌面框宽 1280 > 舞台可见宽 ⇒ **右缘被横向滚动裁掉**、顶部标题被滚动裁掉半行（M2 已知限制 #5 同族）；被裁区域的内容判 Unknown，不当成"没问题"。深色档截图顶部那条浅灰是插件外壳（不属于被试穿的设计系统），不计入皮肤判定。

## 反向探针记录（自查表 #24：新判据必须造反例证明会响）

| 探针             | 操作                                                         | 预期                                        | 实际（贴原文） |
| ---------------- | ------------------------------------------------------------ | ------------------------------------------- | -------------- |
| 数字守卫必响     | 往规范模板塞一句含 `16px` 的规则                             | `GuidelineGeneratorTests` 数字守卫变红      | **已做，做成常驻用例**（比"临时改源码当场还原"更硬）：`GuidelineGeneratorTests.反向探针_数字守卫必须响` = `NumberViolations(["正文字号不小于 16px"]).Should().NotBeEmpty()`，本轮 506/506 里实跑通过（Verified）。守卫抽成方法正是为了让探针能直接调它——AC13 的"全绿"只有在"探针会红"的前提下才有意义 |
| 预设覆盖矩阵必响 | 把某预设的某个轴改回默认值                                   | 覆盖矩阵用例变红                            | **已做，常驻形态**：`StylePresetAxisTests.AC7_预设目录里新预设的轴取值_必须真改产物而非摆设` 把每个新预设与"同参数但去掉全部轴"的版本逐条比 `shared\|path`/`theme\|path` 的 `Value\|AliasPath\|ValueJson`——某预设的轴一旦改回默认，两版逐字相同 → `NotEqual` 当场红。另有 `AC7_覆盖矩阵_每个非默认轴取值至少被一个预设用到` 保证 19 个非默认取值没有一个是"没人用过所以没测到"（两条本轮均绿，Verified） |
| 引用存在必响     | 删除一个规范引用的令牌                                       | `brokenRefs` 含该路径且界面标"令牌已不存在" | **已做，常驻用例**：`GuidelineServiceTests`（写入引用不存在的令牌 → `ArgumentException` **只列坏的那条**、库里零行写入）+ `GuidelineRestTests`（绕过服务直写仓储造坏引用 → 出参 `brokenRefs=[space.gone.9]`、括注正文写「已不存在」而 `bodyRaw` 不脏）。界面侧同口径由 e2e G1 的 chip 断言（值必须等于 `tokens/effective`）覆盖（Verified） |
| 词表守卫必响     | 在 `showroom/tune.ts` 末尾追加 `export const __probe = ['crisp', 'flat', 'layered']`（三条互不相同的轴取值字面量） | 守卫变红           | **已做，真实红**：`× 界面不许再存一份后端词表 > 风格轴取值 styleAxes：没有多成员字面量清单 → 风格轴取值 styleAxes 又被人抄成界面字面量了（应改读 GET /meta 的对应字段）： showroom	une.ts:124 → crisp,flat,layered…: expected [ Array(1) ] to deeply equal []`；还原后 `246 passed (246)` 复绿（探针是临时改源码、当场还原，未留文件） |
| 黄金回归必响     | 临时把 `soft` 阴影某系数改 0.1                               | 黄金回归变红（验证后还原并重跑全绿）        | **已做，真实红**（2026-10-03 05:06）：把 `ScaleGenerators.SoftLayers` 的 `blur = Pow(level,1.6) * 3 * d` 临时改成 `* 0.1` → `dotnet test --filter ~StyleAxisGoldenTests` 报 `失败: 1，通过: 3，总计: 4`，原文：`Expected drift to be empty because 默认轴产物发生变化 = 存量项目会被静默改值（M3 硬约束 4）。漂移项：admin-calm\|light 哈希漂移 期望 5eb53582…bb64 实得 4c72881d…1ba3`（`admin-calm\|dark`、`high-contrast`、`workbench-focus` 三档同时漂移）。**当场还原**（`grep -c "反向探针（临时" = 0`）后重跑同一过滤器 → `已通过! - 失败: 0，通过: 4，总计: 4`（`.temp/ds-m1/logs/golden-probe-red.log` / `golden-probe-green.log`）。结论：默认档产物的兼容判据不是摆设，任何一处系数漂移都会被点名列出（来源等级 Verified） |
| V4「选中档==这件衣服真值」必响（2026-10-03 16:37） | 只把**期望源**从 `admin-calm` 换成另一件 `tech-crisp`（面板上穿的仍是 admin-calm） | 同源判据变红，且红在"期望 crisp / 实得 soft" | **已做，真实红**（`.temp/ds-m1/logs/e2e-m3-v4-reverse-probe.log`）：`Error: 穿的是 admin-calm，轴 shadowStyle 的选中档必须等于它的真实值 crisp` → `Expected: "crisp"` / `Received: "soft"`，并把节点原文打出来：`14 × locator resolved to <button role="radio" type="button" aria-checked="true" data-axis-value="soft" class="ds-chip ds-chip--on">柔和</button>`。⇒ 这条判据**不是永真**：它真的在拿"穿的那件衣服的后端 `request`"对界面。探针改回后与绿跑（`e2e-m3-v4c-tie.log`，1 passed）逐字节同码，绿证据仍成立（Verified） |
| V4 首跑为什么先红一次（记进台账，防被当成"用例天生不稳"） | 我按 `apiData`（GET）打 `/api/design-system/presets/recommend` | — | **红在我自己**：`Error: 405`（该端点只吃 POST）。更该记的是——「`request` 只有 **POST** presets/recommend 才带，`GET presets` 不带」这句话**就写在我自己几行前的 `restCssForPreset` 注释里**，写新判据时没回看。改 `apiPost` 后复绿（Verified，`e2e-m3-v4b-tie.log` → `e2e-m3-v4c-tie.log`） |
| ReleaseBoard「无法比较」提示是条件渲染还是常驻装饰（AC17 补档） | 同一组件喂两种后端出参：`notComparableKinds=['guideline']` 与 `[]` | 前者必须出现、后者必须不出现 | **两条都实跑**（`sections/ReleaseBoard.test.ts`，3 passed / 270ms）：①存在并点名 `guideline`+「无法比较」，且同屏不出现"两版内容一致"；②`exists()===false`；③`specsComparable=false` 时走 schema 1 那条文案且**不**渲染 M3 这条。三条互斥成立 ⇒ 提示由数据驱动、不是常驻装饰，也不是"只要 diff 就报"（Verified）。**未做的探针如实写明**：没有临时删模板再证明测试会红——①③ 的"该有不该有"配对已经覆盖同一性质，删模板探针收益低于成本 |
| V6 轴反作弊：改动路径集合必须与输入无关（17:12） | 把 `V6_轴改动的路径集合不随输入改变` 的**基准集合**换成另一条轴（`accentStrategy=mono`），被测仍是 `shadowStyle/radiusStyle/fontPairing` | 4 组参数化必须全红 | **实红 4/4**（`.temp/ds-m1/logs/v6-probe-A-wrong-reference.log`），且报错把**实际路径集**整段打出来：`shadowStyle` → 15 条 `{light,dark,high-contrast}\|shadow.elevation-1..5`；`radiusStyle=pill` → 12 条 `shared\|component.*.radius`；`fontPairing=editorial` → 5 条 `shared\|{font.display, type.display, type.h1, type.h2, type.h3}`（⇒ 与基准的 24 条不等）。⇒ 这条判据**能区分轴**，不是"两边都非空就算过"。还原后随全过滤集复跑 **513/513** 绿（`filter-run-v6.log`）。**顺带产出的一条新事实**：editorial 在**令牌层**确实把 `type.display/h1/h2/h3` 指向 `font.display` —— 与 G16 完全自洽（角色重定向发生了，导出 CSS 丢 `fontFamily` 才导致肉眼看不见） |
| V10 现查值必响：改令牌值若不重新渲染就必须红（17:38） | 把用例里的 `afterMd` 换成改值**前**读到的 `beforeMd`（等价于"导出侧把规范章缓存住了"这一真实缺陷形状） | 单用例必须红 | **实红**（`v10-probe-no-rerender.log`：`测试总数: 1 / 失败数: 1`，报错头 `Expected afterMd "---…`）；还原后转绿 |
| V10 三交付物判据必响·bundle（18:20） | 只把 bundle 的期望从新值换成**旧值**（`BundleValue(…).Should().Be(c.Old)`），其余一字不动 | 必须红，且要把实际值打出来 | **实红 1 / 通过 11**（`v10-probe-bundle-old.log`）：`Expected BundleValue(c.Token.Path) to be "768px" with a length of 5 because breakpoint.2：bundle 的 tokenValues 没跟着改值走 ⇒ 它不是现查的, but "2304px"` —— 三个抽样里第一个是 `breakpoint.2`（768px→2304px），判据能指到具体令牌 |
| V10 另两段必响·brief 行内括注 + 手写不被误拒（18:22，一轮双红） | ① brief 行内期望换成旧值；② **临时在 `GuidelineService.Save` 里加一条"正文含数字单位即拒"**（把"数字守卫越界到手写"这个缺陷真实装进实现，而不是只在测试侧换个期望） | 两条用例各红一次 | **实红 2 / 通过 10**（`v10-probe-brief-and-manualguard.log`）：一条 `Expected brief "# 规范导出 exp（e-exp-…）设计说明书 · v0.1.0 …`、一条 `System.ArgumentException : 【临时探针】正文含数字单位`。临时守卫**当场逐字还原**（全仓 `grep -rn 临时探针 --include=*.cs` **0** 命中），还原后复跑 **12/12**（`v10-three-forms-restored.log`）⇒ ②的"不被误拒"是真的在守卫实现面前会红，不是名存实亡的断言 |
| NFR 耗时比值的"工作量守卫"必响（20:00，反向覆盖审计新增） | 首版判据我自己写成"默认档与非默认档**令牌条数必须相等**"，而 `fontPairing=editorial` 按规格**多产一条** `font.display` | 条数守卫必须红，且把两侧条数打出来 | **实红 1 / 通过 8**（`filter-run-perf.log:642`）：`Expected v.Total to be 388 because 非默认档的令牌条数从 388 变成 389：条数变化会让「耗时同量级」变成「工作量不同」，两条判据要分开, but found 389`。**这一条红的是我写错的守卫，不是产品缺陷**（规格 FR/AC3 自己规定 editorial 多一条），改成 `\|Δ条数\| ≤ 2` 并把合法增量的出处写进注释后 529/529 绿（`filter-run-perf-b.log`）。记进台账的理由：它同时证明这条守卫**对"测的不是同一件事"有反应**——否则耗时比值可以拿两份不同工作量自比（Verified） |
| G7「注册表实际注册 == 插件自述」必响（21:17，X1 网关链审计新增） | 只把**期望源**换错：`[...meta.agentTools]` 里临时多塞一件并不存在的 `design_temp_probe_zzz`（被测系统一字未动，插件也没少注册） | 相等判据必须红，并把两侧名单打出来 | **实红 1**（`e2e-m3-g7-probe.log`）：`Error: 注册表里的 design_*（8 件）与 meta.agentTools（8 件）不一致` + 深比较里 `- "design_temp_probe_zzz"` 只在 Expected 侧。⇒ 这一格真的在读**网关 `list_tools` 返回的注册表名单**，不是"两边都非空就算过"。还原后探针串在**代码与测试源码内 0 残留**（`Grep(design_temp_probe_zzz, glob=*.{cs,ts,vue})` 无命中；这句话的范围要写清——它现在只作为**记录**留在本台账与 `.temp` 日志里，"全仓 grep 0"是不成立的说法），单跑 1 passed、全目录 33 passed 复绿（Verified）。**另两条常驻反向腿**（不靠临时改码）：`design_nope_e2e` 必 `isError`+`unknown tool`、把 `design_guide` 当对外工具名直接 `tools/call` 必被拒 |
| Biz 直查守卫会不会是"空扫描假绿"（10-04 本会话，新常驻用例 `BizDirectQueryGuardTests`） | 往真实生产文件 `GuidelineRepository.cs` 插一行 `DesignGuideline.FindAllByProjectId(projectId)`，用 `--no-build` 重跑该类（源码扫描判据读磁盘源文件，探针行不参与编译） | 判据必须红，且要点名到那一行 | **实红**（`logs/bizguard-probe.trx`：9 通过 / 1 失败，失败项＝判据本体，消息原文 `发现 1 处裸调用：Plugins/DesignSystem/Services/GuidelineRepository.cs:53 var __probe = DesignGuideline.FindAllByProjectId(projectId);`）。还原后 `grep -c 临时探针` = 0、复跑 10/10 绿（`bizguard-restored.trx`）。**顺带逼出第二条探针**：首版把仓库根锚点写成 `Directory.Exists(<repo>/Plugins/DesignSystem)`，被测试输出目录 `bin/Debug/net10.0-windows/Plugins/DesignSystem/`（只放 DLL）截胡 ⇒ 扫到 0 文件 ⇒ "零违规"本会永久假绿；靠用例自带的阳性对照（文件数 > 0、`.Query*` 计数 > 0）当场红出来。锚点改用仓库根独有文件 `DesignSystem.csproj`（技能 #71） |

## Plan 偏差汇总

**27 条**（10-04 18:0x 追加第 27 条＝pwsh 新规的文档落点 + 签名宿主包 + 两个脚本注释），全部逐条落在 `03-plan.md` 的「Plan 偏差记录」表内（按时间序，最后五条为 05:56 / **16:45** / **18:20–18:24** / **10-04 12:35 与 16:4x** / **10-04 18:0x**；16:45 那条是收口后新增两个测试资产、其中一个超出 04-task "新增文件"枚举的交代；18:20–18:24 两条是**完成度自审把自己之前的判据宽度不足记进台账**：V10 只做了预注册三段里的一段、V13 的字面"仅一处新增"与实测 diff 形状不符）。归类（**下表是抽样归类，类别之间有重叠**——例如 quick-create 丢轴既是"Files To Change 漏列"也是"缺陷驱动"，所以条数列相加不等于 27，它不是分区清点）：

| 类别 | 条数 | 代表条目 |
|---|---|---|
| Files To Change 漏列（不改就当场假绿） | 5 | `PreviewCssService.cs`（轴进不了预览）、`PresetRecommender.cs`（`Copy()` 丢轴 + `limit` 写死 8 推不出新衣）、`QuickCreateService.cs`（`ApplyOverrides` 丢轴，被 e2e S2 抓到）、`showroom/outfits.ts`（`toPreviewInput` 丢轴）、`start/wizard.ts` + `tune.test.ts` |
| 契约/形态与计划不符（按真实落点做） | 5 | 录制器改环境变量开关、种子后缀改短键（列宽 100 硬约束）、`TypeOptions` 默认值抽 `FontStacks`、agent 轴属性由 `StyleAxes.SchemaProperties()` 生成、diff 出参落点在控制器而非 `DesignMapper` |
| 数值微调（允许范围内） | 1 | `flat-minimal` 彩度 0.05 → 0.08（审计 critical 3.72:1 逼出来的，同步登记「公式调整记录」） |
| 既有测试/断言增量登记 | 2 | M1/M2 的"8 预设"断言逐条改 13（零删除零弱化）、`design-system.spec.ts` 的 `navLabels` 追加第 15 项 |
| 判据落点改到可证层 / 不可复现形态的处置 | 4 | §AC12 冷进程建表改跑序无关三条 + 限制写明、规范取值口径统一（超出 04-task 字面，含 `tokenValues`/`valueTheme` 出参）、`vocabulary` 守卫不新建组件、**05:56 步 14：e2e 读"舞台注入 CSS"的到位判据（批 C 两轮假读逼出来，改判据不弱化）** |
| 缺陷驱动（e2e/测试抓出来才改的） | 3 | quick-create 丢轴（真缺陷 #1）、界面两处（新建草稿面板不出现 #6、归档零反馈 #7）、测试夹具漏挂 `Guidelines` 造成 E1 假红 |
| 收口后补证据（只加测试资产，不改实现） | 2 | ① 16:45 那条：V4「轴面板 + 衣柜」控制面走查（Allowed spec 内追加）+ 新建 `sections/ReleaseBoard.test.ts`（把 AC17 的 UI 可见性 Unknown 补成实测；那一轮只加测试文件，`ReleaseBoard.vue` 本体的两处改动是 **M3 实现期**就发生的，见下一行）；② 18:20 那条：**V10 此前只做了预注册三段里的一段**（一个令牌 + 只验 design-md），补齐成"三个令牌 × brief/design-md/bundle"+"手写不被误拒"两条用例，并顺手把早已存在但**没映射到 V10** 的 `AC13_数字守卫_全部生成文本零命中`（3 kind × 3 density × 7 industry）登记为 V10 第二段的答案 |
| 预注册判据字面与实测有出入（按实测登记，不改判据迁就实现） | 1 | 18:24 那条：06 的 V13 写「`ReleaseBoard.vue` 的 diff **仅一处新增**」，实测为 **`+4 / −1`、两个 hunk**——① 新增 `[data-diff-not-comparable]` 提示块（3 行），② `kind` 图例那行补 `/ guideline`（改 1 行）。图例那行是"guideline 成了可 diff 的 kind"的必然同步、与提示块同属一个功能，但**字面确实不是一处**，所以按实测形状登记，不写成"仅一处新增" |

## Known Limitations

1. **「字体搭配轴在展厅里看不到标题变衬线」——根因是既有的排版令牌投影缺口，不是 M3 引入**（Verified，`.temp/ds-m3/e2e-style-a3.log`）。
   `ExportService.TypographyCss()` 生成复合排版令牌的 CSS 值时**读了 `fontFamily` 却没写进输出**（只输出 `weight / size / line-height letterSpacing`），
   且模特页 `.mq-page__title` 只设 `font-size/line-height/font-weight`、不设 `font-family`。
   所以 `font.sans → font.display` 这条轴改了令牌（`type.display/h1/h2/h3` 的 `ValueJson.fontFamily`，单测已钉）但**交付 CSS 里没有对应声明**。
   **为什么本批没顺手修**：修它要动默认导出（新增/改变 `--ds-type-*` 声明或让模特页取字体族），
   直接违反 M3 硬约束 4「不传新参数时**产物（含 CSS）与开工时 HEAD 完全一致**」——存量项目的交付 CSS 会变，属需用户拍板的独立变更（已记 TODO）。
   本批采取的做法：把判据落在**可证且不破坏兼容的变量层**（注入的交付 CSS 里 `--ds-font-display` 的有/无），并在 e2e 里显式拍下标题字族仍相等这一事实，不假装它成立。
   **V3 把这条缺口的边界收窄了（Verified，`ForgeSelf.Web/screenshots/e2e/design-system/m2/style-V3-passed.log`，同 V1 行：该目录不入库）**：`fontPairing=system|humanist` 改的是 `--ds-font-sans/mono`，
   标题**继承**得到 → 计算值确实跟着变（实测 `system-ui, -apple-system, …` 与 `"Source Sans 3", "Noto Sans SC", …`）；
   只有 `editorial` 这一档改的是**只存在于 display 字族**的 `--ds-font-display`，而标题元素不引用它 → 看不出差别。
   所以"字体搭配轴整体无效"是错的，准确说法是**"展示字族没接到标题"**，修它的最小落点也就是模特页那一处 `font-family`（仍会改默认产物 → 保持待拍板）。
2. **展厅微调没有实时预览**（M2 既有缺口，M3 的轴因此也只能在"保存为新设计"后看到效果，Verified）：
   `outfits.ts` 有 `makeTunedOutfit()`（`tuned:<n>` 衣服）**但 `Showroom.vue` 从未调用**，`loadCss()` 只用 `outfit.request` 取 CSS，
   微调状态 `tune` 只在"保存为新设计"时用一次。自查表 #32（"声明了却没人调用"）同族。
   修它属改 M2 已交付交互（且与"不启动用户宿主"无关，但会牵动展厅换肤链路），本批不顺手做 → 已记 TODO。

3. **规范端点「项目不存在」的状态码沿用全站现状 500（不是 404）**（Verified 成因；非本批引入，已记 TODO）：`RequireProject(id)` 写在 `Guard(...)` **之外**是整个控制器的既有形态（`ListThemes` / `RunAudit` / M3 规范五端点同形），而宿主**没有全局异常中间件** → 异常逃出 action 就是 500，`Guard` 里的 404 映射轮不到。改它＝改全站状态码口径（含 M1/M2 已交付端点与既有断言）→ 需用户拍板；本批没给规范端点开小灶，`GuidelineRestTests` 按现状断言 `KeyNotFoundException` 并在注释里指向 TODO。

4. **XCode 的「冷进程按需建表」在共享测试进程里不可复现**（Verified 的是限制本身）：按需建表只在实体 Meta 首次初始化时决策，同进程里只要前面的用例热过 Meta，「旧库缺表 → 补建」就再也走不到（实测 `--filter` 同跑两个类时报 `XSqlException … no such table: DesignProject`，把 `DAL.Tables` 置 null 也不重建）。该形态**在本类是进程内第一个 XCode 类时单独 Verified 过一次**（补建新表、旧令牌行逐字不变、存量项目读规范为空），事实记在此；常态回归由 `GuidelineUpgradeTests` 三条**跑序无关**判据承担同等风险（13 张表全在表清单 / 同 `(ProjectId,Code)` 第二条必被拒且换 code 必写得进 / 重复 `EnsureCreated()` 不动旧行也不回填）。

## Unresolved Issues

无 FAIL 遗留：AC1–AC26 中，除下面四条**已登记、有替代证据或已加常驻判据**的项之外全部 Verified。

1. **AC11 的"标题字族肉眼不同"这一半不成立**（不是本批引入，用户已拍板本批不修）：根因是排版令牌投影缺口（Known Limitations #1）。
   替代证据＝变量层判据（注入 CSS 里 `--ds-font-display` 有/无，e2e S1）+ 令牌层判据（`tokens/effective` 里 `font.display` 真存在，e2e S3）。
   已尝试：① 直接改 `TypographyCss()` 输出 `font-family` → 会改默认导出，违反 M3 硬约束 4，**按用户指令停手**；② 把判据落到变量层（已做）；③ 记 TODO 待拍板（已记）。
2. **AC21 的 `fontPairing` 轴无法靠"看图"验收**：同上一条同因。V 片因此对该轴改拍"令牌/变量层变化 + 图片留证"，
   读图结论对这条轴标 **Unknown（视觉）**、Verified（变量层），并在 Screenshots 表逐张写明。
3. **「写后立即读拿到残缺交付物」已复现并归因到读侧（不再是 Unknown）**（P1，TODO 已立；修法属跨切面读路径，本批不改）：
   批 C 的 e2e S2 第一次读 `export?format=css&theme=light` = **7628 字符、`--ds-shadow-*` 整族为 0**，**同一瞬间** `tokens/effective?theme=light`
   已给出 `count=199 / shadow.*=5 条`，~400ms 后第二次读导出即 9086 字符五档齐全（单跑读到的是 17074 字符的完整产物）
   ⇒ **生成与落库是全的**（服务层新用例 `Create_带轴项目_导出CSS整族齐全且形状随轴变` 19/19 同证），残缺发生在**导出这条读路径读到旧视图**。
   复现配方 `--grep "(G[1-4] |S2 )"` **2/2 稳定**（日志 `e2e-s2-staleness-probe.log`）；现场条件：宿主 `XCode.config` = `DataCacheExpire 0 / EntityCacheExpire 10 / SingleCacheExpire 10`，
   写入侧全是同步 `entity.Save()`。机制候选（未定案，需读 XCode Session/Cache 语义）：跨执行上下文的缓存幽灵读
   （`TokenRepository` 类注释自己就写着"缓存是 AsyncLocal 每执行上下文一份…会读出幽灵行（铁律 11）"，而导出唯一读路径 `FindShared/FindThemed` 走的正是 `DesignToken.FindAll(exp)`）
   / SQLite 连接快照可见性。**本批处置**：S2 判据从"读一次"改成"**轮询到整族齐全才算过**，并把每次读到的长度与族名 + 同一瞬间的 `tokens/effective` 旁证逐次写进证据"
   —— 既不让偶发残缺蒙过门禁，也让这个缺陷每次跑都被量化（`attempts > 1` 就是它的读数）。
   **两条已排除的怀疑（免得后续重复查）**：① 不是"产物天生缺一半"——同一项目在另一跑里读到的是 `count=309 / 导出 310 个变量` 的完整集，
   说明 199/200 那一档是**过程态**而不是设计如此；② 单跑 17074 字符 vs 批跑 9086 字符的差不是"同一产物忽大忽小"以外的解释，而是
   **两个读路径都会读到旧视图**——`tokens/effective` 自己也不可信（同一项目读到过 199 与 309 两个数），
   所以"导出 == effective"这种**同源自对**判据会跟着一起被骗过去（我第一版就是这么写的，实测后被否掉）。
   ⇒ **S2 的最终判据换成挂在权威源上**：`POST generate/preview-css`（零写库、当场跑生成器，AC2/AC8 已证它与落库导出同源）
   的变量**名集合**必须被落库导出**一条不少**地包含；轮询到成立才放行，并把每次尝试的字符数/变量数与 preview-css 的条数逐次 mark 进证据
   （`attempts > 1` 就是"写后立读滞后"的读数）。另有一条独立判据同样按"轮询 + 记读数"写：`GET projects` 必须出现新增的那一条
   （实测出现过宿主日志已写『项目已创建 ds-0cdb81』而清单读 0 条），并同时抓 `quick-create` 的 **200 响应**，
   这样"写失败 / 写成功但读侧滞后 / 产物天生残缺"三种解释一次跑就能分辨。
   修法（关缓存 / 改直查 / 导出前失效）属跨切面读路径 + 性能影响 → 交用户拍板，TODO 已立 P1。
   **19:04 追加一条同族证据（读码所得，来源等级标清）**：为解释 `DesignGuideline.Biz.cs` 里那个没人用的 `MaxCacheCount` 才去读生成物，结果发现 **`DesignGuideline.cs` 的五条读路径全部写着 `if (Meta.Session.Count < 1000) return Meta.Cache.Find…`**（`:240/:258/:271/:286/:301`，Id / (ProjectId,Code) / ProjectId / Category / Status）——**xcode 把上限内联成了字面量 1000，而不是引用 Biz 里的字段**（同族的 `DesignToken.cs:402` 等写的才是 `< MaxCacheCount`），字段因此是死码。两件事要分开记账：① **Verified（代码级）**：规范的五条读路径在表行数 <1000 时走 `Meta.Cache`，与令牌侧同一套缓存语义；② **Inferred（产品级）**：因此「写后立读拿到旧视图」的 G15 风险**同样覆盖规范读路径**（`_repo.List/Find` 命中缓存时读到过期行），但**我没有在规范端点上实测到滞后**——本批所有规范用例的夹具都把 `DesignGuideline.Meta.Cache.Expire = 0` 关死了，测试环境天然看不见这个问题（这本身就是一条该登记的偏差：**夹具与生产缓存配置不同形**）。⇒ 对 P1 修法选型的影响：**方案 A（插件初始化处关掉 design-system 实体缓存）的覆盖面比原先想的更大**（不止令牌/导出，也含规范读），这条证据已并入 TODO 的 P1 拍板材料。
4. **展厅取数窗口内 `data-outfit` 与注入正文不一致（真实产品缺陷，本批不修 → TODO P2）**：`Showroom.vue:loadCss()` 换装时不清空旧 `css`，
   `Stage.vue` 把新 id 与旧文本一起交给 `OutfitScope` → 窗口内 DOM 是"挂着新衣服 id 的上一件皮肤"（用户侧＝点了新皮肤画布仍是旧样子；
   取证侧＝截图/自动化会拿到带错标签的证据）。修复要动 M2 的 `Showroom.vue` + `Stage.vue`，**两文件不在 04-task Allowed 名单** → 按 §1.3 记 TODO 交用户点头；
   本批把 e2e 判据改成"注入必须等于该件衣服自己的交付 CSS"，红线不再依赖这个窗口（03-plan 偏差表 05:56 行）。
5. **收口后又关掉的一档 + 仍开着的两档（16:38–16:45 复验）**：
   - **已关掉**：AC17 的"ReleaseBoard `[data-diff-not-comparable]` 提示 UI 可见性"此前标 **Unknown**，现由 `sections/ReleaseBoard.test.ts`（3 条，含"该出现/不该出现"配对）补成 **Verified**；e2e 造不出前提的原因也已核实成事实而不是推测（`ReleaseService` 永远写 `CurrentSchema`、`ReleaseDto` 不回文件路径）。
   - **仍开着（agent 做不了）**：③ 打 tag → CI 发布（需用户授权）、⑤ 运行实例只读复验（需用户先把宿主更到含 3.1.0 的版本）。
   - **仍开着（本批选择不做）**：**真人外行走查**——M3 的界面走查全部做成常驻用例（G4 + V4，含零写入反证），比手点更可重复，但"像用户一样在浏览器里点一遍"这件事本身没做；M2 的 V12 也是同一口径未做。要不要补，请验收方在闸门2 一并判。
6. **闸门1 的 G1–G5 逐项批准【没有留痕】，本批是按用户 standing 指令先行推进的**（18:57 完成度自审发现，**不是措辞问题，是前置条件未闭合**）：04-task 的五行「用户决定」实测全部是 ⬜，而 `ROADMAP.md` 的 M3 行此前写着「闸门1 ✅（2026-10-03 用户批准 G1–G5）」——**那句话没有出处，已按实态改成「闸门1 未闭合」**，04-task 状态行同步写明「按本行规则『未 ✅ 不得改业务文件』而业务文件已改 ⇒ 流程缺口，须用户在闸门2 逐条落字」。影响面按大小排：**G1 新增表 `DesignGuideline` 是唯一的高风险结构变更**（加法、自动建表、既有 12 张表零 diff、无 DELETE、旧行不回填，回退代价=删表；实测见 AC12/AC20），G2–G5 都落在既有契约的只增范围内（AC7/AC17/AC18 已逐条验"只增不改"）。⇒ **06 的 V2 若严格判，本任务应判 BLOCKED/CHANGES_REQUIRED 而不是 APPROVED**；我不自行把 ⬜ 改成 ✅，也不拿"用户说了继续开发"冒充逐项批准。

## 阻塞


**10-04 本会话追加（代签与交付通道）**：`06-review.md` 已按用户 10-03 23:43 指令由实现方**代签**（V0–V20 逐格回填 + Final Decision = APPROVED，风险级 `COMPLETED_WITH_RISK`，三条限制钉在结论上）；插件本地更新源已出包并按**包内容**验真（`updates/design-system-3.1.0.forgeself-plugin`），等用户在 `:51888` 设置页选该目录更新，之后才谈 ⑤ 只读复验；**仍等人**：闸门3 提交授权（终态 106 路径，逐路径 `git add`）、tag 发布（用户「先不做」）、G19 只读退避是否另立小批（本会话把它从"并发场景"升级为"串行也出现过"）、G16/G17 与两条 P2/P3 处置。
无。（闸门2 需要规划方独立复跑 `06-review.md` 的 V0–V20；V16 需要用户对规范文案表态；闸门3 提交与**发布 tag 需要用户明确授权**，本批未打 tag、未推送、未启停任何用户宿主。）

**闸门3 的前置已实测通过（22:03，不是"应该能过"）**：照 `scripts/hooks/pre-commit` 的真实调用方式跑
`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-01-design-system-m3-style-guideline`
→ 原文 `PASS: 全部 PILOT 工件链齐全（00-07 八件 + 关键节）`、退出码 0（日志 `.temp/ds-m1/logs/pilot-artifacts-m3-only.log`）。
**顺带更正我自己此前的一句推断**：仓库级整跑（不带 `-TaskId`）确实报 `FAIL: 共 3 项缺失/缺位`，但 3 项全部来自**另一会话的 `2026-10-03-llm-observability/`**（该目录按流程只到 00–04，缺件是它的正常在制状态）。我原先写"现在提交会被挡住、挡住的是别人"——**读了 hook 才发现不成立**：它是按暂存区实际触碰的目录逐个 `… -TaskId "$d"` 校验（`git diff --cached --name-only --diff-filter=ACMR | sed -n 's#^docs/ai/pilot/\([^/]*\)/.*#\1#p' | sort -u`），**那个目录的缺件不会挡本目录的提交**；整跑的 exit 1 只代表"全库清点结果"，不代表"我的提交被拦"。⇒ 结论按实态改：**本目录门禁 PASS，提交不被外目录阻塞**；处置不变——不替对方补工件、不改脚本绕过、也不把别人的目录算进本批账。
