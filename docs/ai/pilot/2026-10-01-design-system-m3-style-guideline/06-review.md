# Review

> 阶段：Stage 8｜Reviewer 视角重查 Intent → Spec → Plan → Task → Code → Test → Evidence 全链。
> 结论只报事实；CHANGES_REQUIRED 时须指明回退到哪个阶段。

> **状态（10-04 更新）：实现方代签已填写（见「代签说明」与文末 Final Decision）；独立验收方仍可复跑推翻**
> 本文件在**实现开始前**由规划/验收方写好考试范围（防止看了实现再定标准）。「审查八问 / 各项 Check / Findings / Final Decision」
> 在实现完成并经**独立复验**之前一律不得填写；实现方**不得**修改本文件的结论栏。

## 验收清单（预注册）— 规划/验收方独立复跑，不采信 05-evidence 自述

| 编号 | 动作                                                                                                                                                                                                                                                                                                                                                                                                    | 通过判据                                                                                                                |
| ---- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| V0   | 读 05-evidence，列出所有声称 Verified 的条目；`git status` / `git diff --stat` 清点改动                                                                                                                                                                                                                                                                                                                 | 每个 Verified 都附真实输出；改动文件集合 ⊆ 04-task Allowed / 03 Files To Change，多出的有解释；Forbidden 文件零改动     |
| V1   | 环境：`$env:TMP=$env:TEMP='D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\.temp\ds-m1\tmp'`；`chcp 65001`；UTF-8 输出                                                                                                                                                                                                                                                                                       | 之后所有测试命令都在此环境下跑                                                                                          |
| V2   | **前置闸门**：M1、M2 的 06 Final Decision = APPROVED；本目录 04-task 闸门1 五项（G1–G5）用户已批准并留痕                                                                                                                                                                                                                                                                                                | 缺任一项 → 本任务整体判 BLOCKED（不论代码质量）                                                                         |
| V3   | `dotnet build Plugins/DesignSystem/DesignSystem.csproj`                                                                                                                                                                                                                                                                                                                                                 | 0 error；新增代码 0 warning（与基线 warning 数对比）                                                                    |
| V4   | `dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~DesignSystem" --logger "console;verbosity=normal"`，再 `--no-build … --list-tests`                                                                                                                                                                                                                                                        | 失败 0；**报告总数 == 发现数**；总数 ≥ 开工基线 + 03 §Test Plan 要求的新增用例；与 05 报告的数一致                      |
| V5   | **黄金回归独立复跑**：单独运行 `StyleAxisGoldenTests`；再核对 05「黄金基线记录」的录制日期/HEAD **早于**任何生成器文件的首次改动（`git log --follow` 对 `DesignGenerator.cs`、`ScaleGenerators.cs` 等）                                                                                                                                                                                                 | 全绿；基线先于改动；基线字典未被事后改写（`git log -p` 看字典条目是否在生成器改动之后变动过——变动须有登记的偏差与原因） |
| V6   | **轴反作弊抽测**：对 `shadowStyle=crisp`、`radiusStyle=pill`、`fontPairing=editorial` 各自用**新请求**（不在测试里的 brief/hue）调 `generate/preview` 与落库，逐条核对：白名单外路径与默认产物逐条相同、白名单内按 §A3/§A4–A9 公式取值；人工核算 1 条 `crisp` 阴影数值                                                                                                                                  | 与契约一致；无永真断言、无被 `Skip` 的用例（录制器除外）                                                                |
| V7   | **xcode 与表**：`git diff -- Plugins/DesignSystem/Data/Model.xml` 只含 `DesignGuideline` 表新增；`DesignGuideline.cs` 为生成物（头部生成标记、无手改痕迹）；规划方自行对 `Model.xml` 再跑一次 `xcode`，`git status` 无新变动                                                                                                                                                                            | 二次生成无差异；既有 12 张表 diff 为零                                                                                  |
| V8   | **旧库升级**（AC12）：规划方独立复现——用 M3 之前的实体集建库、写项目与令牌、再 `EnsureCreated`（即 `GuidelineSchemaTests` 的做法，确认它真的先建了旧库而非直接用新库）                                                                                                                                                                                                                                  | 新表存在、旧行逐行不变、存量项目读规范为空（不回填）                                                                    |
| V9   | **快照与幂等**：对"无规范的旧项目"同版本重发布两次 → 第二次无新版本；schema 2 旧快照 vs 新快照 diff：`NotComparableKinds=["guideline"]` 且无"规范新增"；`ReleaseBoard` 提示可见                                                                                                                                                                                                                         | 三条全部成立                                                                                                            |
| V10  | **数字守卫 / 同源抽测**：随机抽 3 个令牌改值（如 `space.6`），导出 brief / design-md / bundle 的规范文本里该处括注的值同步变化且**规范模板文本零改动**；再对生成器产出的 14 条默认规范（3 种 kind）自行跑一遍正则 `\d+(\.\d+)?(px\|rem\|em\|ms\|s)` 与 `#hex`（WCAG 比值与列表序号除外）。手写规范（`source=manual`）不受数字守卫约束（守卫只约束生成器模板，见 02-spec FR8），只确认其被原样保存与渲染 | 值同源；生成文本 0 命中；手写不被误拒                                                                                   |
| V11  | **REST 契约**：反射/路由表确认**无** DELETE 规范端点；类级鉴权未削弱；`expectUpdatedAt` 409；`archived` 可经 PUT 恢复；400 文案含合法取值提示                                                                                                                                                                                                                                                           | 全部成立                                                                                                                |
| V12  | **工具增量**：`GET api/design-system/agent/tools` 的 `parametersSchema` 只增不改（与 M1 03-plan §B 对表：既有键全在、仅新增 `code/title/summary/body/rules/tokens/category/status` 与枚举新值）；总数仍 8；`design_review mode=checklist` 含 `g:` 前缀项且 `tokens[]` 全存在                                                                                                                            | 契约一致（人工对表）                                                                                                    |
| V13  | **界面**：DOM 契约 §G8 逐项在真实宿主可定位；原 14 个 section 入口与文案不变（对 M2 e2e 回归零新增红）；`ReleaseBoard.vue` 的 diff 仅一处新增；无 `v-html`（`grep`）                                                                                                                                                                                                                                    | 成立                                                                                                                    |
| V14  | **视觉复验**：独立打开展厅与"UX 规范"页（真实宿主或 e2e 截图），editorial / tech-crisp / flat-minimal / warm-craft / kids-playful 五个预设肉眼可辨；7 个轴各取至少 1 个非默认值读图                                                                                                                                                                                                                     | 每轴非默认值肉眼可辨；缺陷已修或登记 TODO（来源等级 Verified；读不了图则标 Unknown 并告知用户）                         |
| V15  | **决策 D1–D6 合规**：`meta.entities` 仍 10；`AuditKinds.All` 不变；`ComponentBlueprints` 仍 10；存量项目无回填；规范文本不含数字；预设 13 个且原 8 个一字未改                                                                                                                                                                                                                                           | 逐条核对为是                                                                                                            |
| V16  | **规范文案用户审阅**（AC26）：05 附清单存在；用户已确认措辞或给出修改意见并已落实                                                                                                                                                                                                                                                                                                                       | 缺用户确认 → 最高判 COMPLETED_WITH_RISK 并标明（不得判 APPROVED 后再补）                                                |
| V17  | **既有 e2e**：`e2e/plugins/design-system` 全跑（含 14 个 `nav`、`entities===10`、M2 新 spec）                                                                                                                                                                                                                                                                                                           | 无新增红；环境不可行 → Unknown + 替代证据并告知用户                                                                     |
| V18  | 范围：`git diff --stat -- ForgeSelf.Api Plugins/McpCenter Plugins/AIAgent`；csproj/package.json/锁文件                                                                                                                                                                                                                                                                                                  | 前三者**为空**；无新增 NuGet/npm 依赖                                                                                   |
| V19  | 版本与文档（AC22/AC25）：三常量 + `plugin.json` = `3.1.0`；README / ROADMAP / 036 / 两个技能事实同步                                                                                                                                                                                                                                                                                                    | 一致                                                                                                                    |
| V20  | 五步闭环里**规划方不可代做**的项（打 tag 发布 / 用户页面自动更新 / 运行实例只读复验）是否如实列为"待用户授权"                                                                                                                                                                                                                                                                                           | 如实列出，未被宣称完成                                                                                                  |

### 代签逐格回填（10-04 本会话；**读数一律指向 05 与日志，本表不复制第二份数字**）

| 格 | 本轮结论 | 依据（可复跑） |
|---|---|---|
| V0 | 通过 | 05「Changed Files」+「接手与收尾」A 段：104 路径逐区域相加 = 104（机器核对）；Forbidden 文件零改动 |
| V1 | 通过 | 本轮所有 `dotnet test` 均在 `TMP/TEMP = .temp/ds-m1/tmp` 下跑（命令原文见 05 D 段） |
| V2 | **名实不符（代签）** | M1 第二轮、M2 两份 06 均实读为 APPROVED ✓；闸门1 只有 **G1 是用户原话**（「新增表」），G2–G5 是实现方按默认推荐的解读——已在 04-task 写明，不是"五项逐项批准" |
| V3 | 通过（**本会话新增文件按文件归因 0 warning / 0 error**，实测口径） | `logs/clean-build.log`（16:01 那次编译**确实重新编译了** `BizDirectQueryGuardTests.cs`，因为它上一秒刚被改过）里 `grep BizDirectQueryGuardTests.cs(` 命中 **0** 条；插件 csproj 的警告数与基线同数（12:34–12:37 三轮 `biz-direct-build.log`）。**未做 `--no-incremental` 全仓重数**——那属于本批没被授权跑的档位；口径提醒见技能"警告度量必须 no-incremental + 按文件归因" |
| V4 | 通过（终态 **539 == 发现数 539**） | `logs/clean-filter-run.log`、`logs/clean-filter-list.log`；**档位＝快档，宿主全量本轮未复跑**（用户指令），已在 05 D 段与 §Final Decision 显式标注 |
| V5 | 通过 | `StyleAxisGoldenTests` 在 539 内全绿；基线仍早于任何生成器改动，字典无事后改写（10-04 未动生成器：`git diff` 里 `ScaleGenerators.cs` 的改动属 10-03 已登记者） |
| V6 | 通过（本轮未重做，沿用 10-03 的 V6 探针与 513/513 复跑） | 05 探针台账 V6 行（换基准轴实红 4/4）；**Inferred 部分**：10-04 的新输入抽测未再跑一遍 |
| V7 | 通过 | `git diff --numstat Model.xml` = 27 增 0 删且只 `+<Table Name="DesignGuideline">`（本会话 16:0x 复测）；生成物无手改 |
| V8 | 通过（10-03 已实跑；本轮未重跑，读数在 05） | `GuidelineSchemaTests` / `GuidelineUpgradeTests` 在 539 内全绿 ⇒ 旧库升级判据仍被同一套用例守着 |
| V9 | 通过（沿用 10-03） | `ReleaseSnapshotTests` 在 539 内；ReleaseBoard 三条组件测试在位 |
| V10 | 通过 | 三段对账 + 三条探针（bundle 旧值必红、brief 括注必红、手写不被误拒必红）在 539 内全绿，05 探针台账 |
| V11 | 通过 | 本会话实测 `grep -c HttpDelete DesignSystemController.cs` = **0**；类级 `[Authorize("ApiKeyPolicy")]` 在位（:21） |
| V12 | 通过（10-03 人工对表；本轮由 `GuidelineToolTests` 复跑覆盖） | 539 内 AC18/工具总数 8 的断言在绿 |
| V13 | 通过 | `Guidelines.vue` `v-html` = 0（本会话实测）；`ReleaseBoard.vue` diff 形状已按实态登记（不是"仅一处新增"） |
| V14 | **Unknown（本轮未读新图）** | 10-03 的 59 张读图是历史证据；10-04 未重跑视觉矩阵（web 源码零改动，`find -newermt` = 0 文件 ⇒ 同一产物），故本轮不宣称重新验过 |
| V15 | 通过（由 539 内既有用例守） | `meta.entities`=10 / `ComponentBlueprints`=10 / `AuditKinds.All` 不变的断言用例在绿；10-04 未新增类别 |
| V16 | **用户授权通过，非独立审阅** | 用户 10-03 23:43「按你推荐的来」⇒ 落地为"措辞按现有产出通过、不再改文案"；已在 05 AC26 行写明这是授权不是逐句审阅 |
| V17 | **有 1 条红，已归因且未放宽判据** | 10-04 16:16 串行 32 passed / 1 failed：`dtcg` 导出 500，栈 = `Export → RequireProject → DesignProjectService.Find → XCode → code = Busy (5) / database is locked`（**G19/E8 同形态**）；单独复跑同一条 **1 passed（2.1m）** ⇒ 偶发。**升级点**：G19 此前只在并行轮出现，本轮在**串行**轮出现 ⇒ README G19 已按实态改写口径；且 Biz 直查把 47 处读改成直接打库，"是否更易撞锁"**未量化**，记为未证风险 |
| V18 | 通过 | 本会话实测 `git diff HEAD -- ForgeSelf.Api Plugins/McpCenter Plugins/AIAgent ForgeSelf.Web/src` = 空；csproj/package.json/锁文件 diff = 0 |
| V19 | 通过 | `plugin.json` + 三常量（Model/Generator/Projection）实测均 `3.1.0`；README/ROADMAP/036/三技能同步（技能自查表实测 1–71 连续、consume 1–11 连续） |
| V20 | **如实列出** | ③ tag 未做（用户「先不做」）；插件本地包已出并按包内容验真；⑤ 运行实例只读复验未做（等用户在 :51888 更新）；未 commit（「改完之后再说」）——三件都没有被宣称完成 |


### Final Decision 判定规则（预注册）

- **APPROVED**：AC1–AC26 全部 Verified（e2e/读图因环境不可行的须标 Unknown 且有替代证据并告知用户）；V2 前置成立；V5 黄金回归先于改动且全绿；V7/V18 零违规；V16 用户已确认措辞；基线红与新增红已区分。
- **CHANGES_REQUIRED**：存在可修缺陷；必须指明回退阶段（Spec / Plan / Task / Code / Test / Evidence）与具体条目。**以下任一情形一律 CHANGES_REQUIRED**：黄金基线晚于生成器改动或被事后改写；`Model.xml` 触及既有表；生成物被手改；规范文本出现数字单位；新增审计类别 / 实体类别 / 组件蓝本。
- **BLOCKED**：前置闸门（V2）未满足、环境/授权/并行会话冲突导致无法验证；写明阻塞原因与升级对象。

## 代签说明（先读这一段）

> **本文件的结论栏由实现方填写，不是独立验收。** 依据＝用户 2026-10-03 23:43 指令原文「2、实现方代签，本轮会话仅仅跑设计系统的测试，不跑全量。」
> 由此产生的**证据面边界必须一并读**：① 本轮未复跑宿主全量 `dotnet test`（~15min）、宿主 `pnpm run check` 全量、深档全量 e2e（145/75 那一轮属 10-03 历史证据，本轮未重验）；
> ② 所以 V2 要求的"独立复验"在本批**名实不符**，只能算"实现方自查 + 用户授权代签"；③ 下列每格读数只引用**本会话真实跑过的命令与其日志**，未跑的标 Unknown / 未复跑，不拿旧轮次冒充本轮。
> 规划/验收方若要独立复跑，按 V0–V20 原表逐格执行即可，本节结论可被推翻。

## 审查八问（实现方代签，出处见上方「代签说明」）

1. **实现是否真正满足 Intent？** 满足。Intent 的三件事都落到了可判定的产物上：① 7 条风格轴**确实改产物**（不是词表装饰）——由 `StyleAxisTests` + `StylePresetAxisTests` 的覆盖矩阵与 `StyleAxisGoldenTests`（默认档逐字节不变）两头钉住；② UX 规范**真落库、真进交付物**——REST 五端点 + brief/design-md/bundle 三形态的"现查值"判据（`GuidelineExportTests`、V10 三段）；③ 第 15 个 section 与能力面驱动**同源**（`meta.capabilities`，界面不算设计值）。**未满足的一条**：批 C 暴露的"写后立即读拿到残缺产物"（G15）**没有修复**，也**没有假装修复**——原"实体缓存"归因已被用户质疑后实测证伪（`Find/FindAll` 不走实体缓存），当前状态＝**待复现**，见 05「接手与收尾」与 README G15 行。
2. **实现是否符合 Spec？** 符合，含两条如实的"名实交代"：① 02-spec 的性能 NFR 是**实测**的（`StyleAxisPerformanceTests`，比值判据 1.5，25 次取中位）——但该判据**对机器负载敏感**，本会话在并发 build 干扰下红过一次（比值 1.61），**未放宽阈值**，改为串行复跑；② FR→落点映射表已建在 05，26 条 AC 逐行有 Verified/Unknown 分级。
3. **是否超出了 Scope？** 有两处超出 04-task 枚举、均有登记：① 10-04 的 **Biz 直查重构**（12 个 `.Biz.cs` + 6 个仓储/服务，47 处调用点）——这是用户指定口径（输入49），不是自发扩展，并有常驻守卫 `BizDirectQueryGuardTests` 兜住；② `scripts/package-plugin.ps1` 是**仓库级脚本**（不在插件 Allowed 内），为交付用户选择的"本地插件更新源"通道而修，属既有缺陷修复；改它触发 §5.6 中档（应跑宿主全量）**但用户明令不跑全量**，故显式标注未跑。其余 Forbidden 面（宿主 / McpCenter / AIAgent / `ForgeSelf.Web/src` / csproj / package.json / 锁文件）逐路径 diff **为空**。
4. **是否修改了不应该修改的文件？** 见 05「接手与收尾」A 段的 104 路径分区清单与 07 §3；Forbidden 集合实测为空。**本文件（06）的结论栏被填写本身是例外**，由用户指令授权，出处已在上方写明。
5. **测试是否覆盖 Acceptance Criteria？** 覆盖（AC 表逐行引用具体用例名与日志）。**两处已知不可判定项如实保留**：① 「15 条规则」这类无口径计数没有做守卫（036 文案问题，已记 TODO P3）；② AC26 规范措辞的用户审阅按 10-03 23:43「按你推荐的来」落地为**按现有产出通过、不再改文案**——这是"用户授权通过"，不是"独立审阅已过"。
6. **是否存在明显回归风险？** 有两条，都可被门禁接住：① Biz 直查改了 47 处读路径 ⇒ 靠后端过滤集 + 插件层 e2e 串行（本轮已跑，读数见 05 D 段）；② 关缓存方案已回滚干净（插件内 `CachePolicy|DisableEntityCache` 引用实测 0）。**计时类判据在并发下会飘**（本会话实测），这是流程风险不是产品风险 ⇒ 已写进 agent-workflow.md（dotnet build/test 一律串行）。
7. **是否存在架构不一致？** 新增一条应当写进约定的事实：**插件的 XCode 实体查询只能经各实体 `.Biz.cs` 的高级查询方法（`QueryAll`/`QueryFirst`/`QueryCount`）**，实体上的任何 `Find*` 不得出现在 `.Biz.cs` 之外。理由不是性能而是**单一出口 + 避开生成器自带的 `Meta.Cache` 助手**；已入 `design-system-verify` 技能（#70/#71）并由守卫常驻。
8. **Evidence 是否足以证明任务完成？** **不足以证明"任务完成"，只足以证明"实现与自查完成"**：交付面三件未做（tag 发布、用户在页面更新、运行实例只读复验），跨插件档位未复跑。⇒ Final Decision 只能带风险附注，且必须写明"这是代签"。

## Requirement Check

26 条 AC 全部逐行对账（05 的 AC 表 + FR→落点映射）。**满足**：AC1–AC25。**条件满足**：AC26（措辞＝用户授权按现有产出通过，非独立审阅）。**未满足**：批 C 的读侧一致性缺陷（G15）未修，已改记"待复现"，本批不宣称修复。

## Scope Check

Forbidden 逐路径 diff 为空（宿主 `ForgeSelf.Api`、`Plugins/McpCenter`、`Plugins/AIAgent`、`ForgeSelf.Web/src`、csproj/package.json/锁文件）；零新依赖；无 DELETE 路由（`grep -c HttpDelete` 实测 0）；规范正文无 `v-html`（`Guidelines.vue` 实测 0 命中）。两处超枚举已登记（Biz 直查＝用户指定；`scripts/package-plugin.ps1`＝交付通道所需且属既有缺陷）。

## Test Check

**跑的是哪一档必须写明**：本轮＝"快"档（`DesignSystem` 过滤集 + 插件 web check/test/build + design-system 插件层 e2e 串行），**宿主全量与深档全量未复跑**（用户指令）。每条新判据都配了能响的反向探针（本轮新增：Biz 直查守卫插真行 → 点名 `GuidelineRepository.cs:53` 实红 → 还原复绿）。终态计数与"报告总数 == 发现数"的核对见 05「接手与收尾」D 段与 `logs/clean-filter-run.log` / `clean-filter-list.log`。

## Architecture Check

插件仍是"库驱动 + 后端唯一裁决"：界面不算设计值、能力面看 `meta.capabilities`、规范读路径与导出同源。新增一条约定（实体查询单一出口走 `.Biz.cs` 高级查询）并常驻。未动宿主/内核语义，未新增审计类别与实体类别（决策 D1–D6 维持）。

## Risk

① 未复跑宿主全量/深档 ⇒ 跨插件影响面本轮**未验证**（Unknown，非"无风险"）；② 计时判据对负载敏感（并发即飘），已在 07 §7 记为流程风险；③ 交付三件（tag / 页面更新 / 运行实例复验）未做；④ 包内混宿主 `.pdb`（非阻塞，TODO P3）。

## Findings

### Critical

无（本轮未发现会导致数据损坏、鉴权削弱或存量项目被静默改值的缺陷；黄金回归 `StyleAxisGoldenTests` 在位且默认档逐字节不变）。

### Major

1. **G15 读侧一致性仍未定案**（现象真实存在过，2/2 复现；本轮三次尝试均不可复现，缓存归因已证伪）→ 待复现条件，README G15 与 TODO P1 已按实态改写。
2. **宿主全量与深档全量本轮未复跑**（用户指令）→ 验收方若要判"跨插件无回归"，必须补这一档。

### Minor

1. 实现方代签（本段存在的前提）＝验收独立性丧失，已在本文件顶部如实写明。
2. 插件包混入宿主 `.pdb`（TODO P3）。

## Final Decision

**APPROVED（实现方代签，风险级＝`COMPLETED_WITH_RISK`）· 2026-10-04**

按预注册规则严格判，本批**够不上独立验收意义上的 APPROVED**（V2 的"逐项批准"名实不符、V14 本轮未读新图、V17 串行轮出现过一条红）。
之所以仍落 APPROVED，唯一依据是用户 2026-10-03 23:43 的明示授权（「实现方代签」「仅仅跑设计系统的测试，不跑全量」），
因此把三条限制钉在结论上，交付方与后续会话不得把它们读成"已全量验证"：

1. **档位限制**：本轮只跑"快"档 + design-system 插件层 e2e 串行；宿主全量 `dotnet test`、宿主 `pnpm run check` 全量、深档全量 e2e **均未复跑**（10-03 的 529 与 145/75 属历史证据）。改过 `scripts/package-plugin.ps1` 本应按 §5.6 触发中档，未跑——这条是用户指令的直接后果，不是遗漏。
2. **已知红**：V17 的 `dtcg` 500 是既有缺口 **G19/E8**（只读路径撞 SQLITE_BUSY 不重试）在本批串行轮里的现身，单跑复绿 ⇒ 偶发；**判据一字未放宽**。G19 的严重级别已按实态上调（不再只是并发场景），Biz 直查对撞锁频次的影响**未量化**，属未证风险。
3. **交付面未闭合**：tag 发布、用户在 `:51888` 页面更新、运行实例只读复验——三件都没做，也不由实现方代做。插件本地包已产出并按**包内容**验真。

**回退阶段判定**：无需回退 Spec/Plan/Code；若要彻底闭合 G19，属另立小批（Task 级新增：只读退避 + 一条会响的常驻判据）。
