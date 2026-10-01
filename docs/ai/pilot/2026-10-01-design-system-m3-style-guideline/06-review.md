# Review

> 阶段：Stage 8｜Reviewer 视角重查 Intent → Spec → Plan → Task → Code → Test → Evidence 全链。
> 结论只报事实；CHANGES_REQUIRED 时须指明回退到哪个阶段。

> **状态：PENDING（预注册验收清单；尚未验收）**
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

### Final Decision 判定规则（预注册）

- **APPROVED**：AC1–AC26 全部 Verified（e2e/读图因环境不可行的须标 Unknown 且有替代证据并告知用户）；V2 前置成立；V5 黄金回归先于改动且全绿；V7/V18 零违规；V16 用户已确认措辞；基线红与新增红已区分。
- **CHANGES_REQUIRED**：存在可修缺陷；必须指明回退阶段（Spec / Plan / Task / Code / Test / Evidence）与具体条目。**以下任一情形一律 CHANGES_REQUIRED**：黄金基线晚于生成器改动或被事后改写；`Model.xml` 触及既有表；生成物被手改；规范文本出现数字单位；新增审计类别 / 实体类别 / 组件蓝本。
- **BLOCKED**：前置闸门（V2）未满足、环境/授权/并行会话冲突导致无法验证；写明阻塞原因与升级对象。

## 审查八问（逐项回答）

<!-- 待验收后填写 -->

1. 实现是否真正满足 Intent？
2. 实现是否符合 Spec？
3. 是否超出了 Scope？
4. 是否修改了不应该修改的文件？
5. 测试是否覆盖 Acceptance Criteria？
6. 是否存在明显回归风险？
7. 是否存在架构不一致？
8. Evidence 是否足以证明任务完成？

## Requirement Check

待验收

## Scope Check

待验收

## Test Check

待验收

## Architecture Check

待验收

## Risk

待验收

## Findings

### Critical

### Major

### Minor

## Final Decision

PENDING（尚未验收；实现完成并经独立复验前，不得写 APPROVED / CHANGES_REQUIRED / BLOCKED）
