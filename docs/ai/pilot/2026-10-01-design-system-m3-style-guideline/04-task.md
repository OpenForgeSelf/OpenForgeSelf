# Agent Task

> 阶段：Stage 4｜Agent 可直接执行的工作单元，零自我决策空间。
> 前序工件：00–03 齐备。本任务书分三片（T-A / T-B / T-C），每片可独立交付、独立验收。

## 闸门状态（开工前置，必须全部 ✅ 才可 Implement）

| 闸门                                                               | 状态             | 依据                                                                                  |
| ------------------------------------------------------------------ | ---------------- | ------------------------------------------------------------------------------------- |
| M2 闸门2（`2026-10-01-design-system-m2-showroom-wizard` 验收通过） | ⬜ 待 M2 验收     | 其 06-review.md 的 Final Decision = APPROVED                                          |
| M3 闸门1（用户批准本里程碑开工，**并逐项批准下表**）               | ⬜ **待用户批准** | 批准后由用户或验收方在此处改为 ✅，写明日期与原话；**未 ✅ 时实现方不得改任何业务文件** |

### 闸门1 待批事项（AGENTS 要求：新增表等须显式批；每项给出默认推荐）

| #   | 事项                                                                                                                                      | 默认推荐 | 用户决定 |
| --- | ----------------------------------------------------------------------------------------------------------------------------------------- | -------- | -------- |
| G1  | **新增数据库表 `DesignGuideline`**（加法、自动建表、旧表旧数据不动；`Model.xml` 对既有 12 张表零 diff）——计划轮已选 A，此处作正式闸门确认 | 批准     | ⬜        |
| G2  | 发布快照 schema 2 → 3（新增 `guideline` 规格；旧快照 kind 级"不可比"，无规范项目 hash 与升级前相同）                                      | 批准     | ⬜        |
| G3  | 预设库 8 → 13（原 8 个不改；新增 5 个，见 03 §P）；风格轴 7 个及其取值词表（03 §A1）                                                      | 批准     | ⬜        |
| G4  | 决策 D1–D6（不加 Stardust 第 11 类 / 不加审计类别 / 不扩组件蓝本 / 不回填存量 / 规范不写数字 / 预设 13 个）                               | 按默认   | ⬜        |
| G5  | M1 工具 schema 的**只增**扩展（`design_edit action=guideline`、`design_lookup kind=guideline`、`design_context sections=guidelines`）     | 批准     | ⬜        |

## 交接说明（实现方必读）

> 分工同 M1/M2：规划/验收方不写业务代码；实现方独立实现并自证；规划方事后独立复验并签 06/07。**不要重写 00–04**；偏差记入 03-plan「Plan 偏差记录」。

1. **读序**：`AGENTS.md`（§0/§5.6/§10/§11）→ AI-Native 规范 → 本目录 01 → 02 → **03（§A1–§A10、§P、§G1–§G8）** → `.agents/skills/design-system-verify/SKILL.md`（尤其 §三 结构变更前必做、自查表 #16 #18 #21 #22 #24 #26 #27 #33）→ `.agents/skills/e2e-testing/SKILL.md` → M1/M2 的 03-plan 契约（本任务在其上做**增量**）。
2. **环境约束**（同 M1/M2，节选）：跑 `dotnet test` 前 `$env:TMP=$env:TEMP='D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\.temp\ds-m1\tmp'`；UTF-8；`--logger "console;verbosity=normal"`；报告总数 == 发现数；`--no-build` 期间不要重新编译；并行会话文件与共享文件只做局部精确替换；禁止停/启/杀宿主进程；未获授权不 `git commit/push/tag`。
3. **两个不可颠倒的顺序**：① **先录黄金基线，再改任何生成器代码**（03 §A5；录制日期与 HEAD 写入 05）；② **先对未改动的 `Model.xml` 跑一次 `xcode` 并确认生成物逐字节一致，再加表**（03 §G1）。违反任一条 = 验收直接 CHANGES_REQUIRED。
4. **检查点**：CP-A（步骤 1–5）、CP-B（6–13）、CP-C（14–15）。建议每片完成即交验收。**规范全文清单**（14 条 × console/marketing/product 三种 kind 的实际文本）在步骤 7 写完生成器后即可导出，**最迟在步骤 14 写入 05 并在回复里给路径**——文案由用户在闸门2 审阅，你不要自行宣布文案"已通过"。
5. **读图责任**：视觉 QA 要求逐张读图；无法读图须标 Unknown 并把截图路径交规划方（同 M2）。
6. **证据纪律 / 回报格式 / 范围纪律 / 失败处理**：同 M1 04-task「交接说明」第 5–8 条；06/07 的结论栏由规划方填写。
7. **规划方如何验收**：见 06-review.md「验收清单（预注册）」。

## Task ID

PILOT-ds-m3-style-guideline

## Objective

设计插件 v3.1.0：7 个风格轴（默认逐字节兼容）+ 预设库 13 个 + `DesignGuideline` 表与 14 条确定性默认 UX 规范（可编辑、进快照与全部交付物、工具与审查可读写）+ 第 15 个 section「UX 规范」+ 视觉 QA；DesignSystem 后端过滤集全绿且总数 == 发现数，插件 web 三件全绿，新 e2e 在真实宿主通过，既有设计系统 e2e 无新增红。

## Scope

### Allowed

- 03-plan「Files To Change」列出的全部文件（新增/修改）。
- `Plugins/DesignSystem/Data/Model.xml`（**仅新增** `DesignGuideline` 表）与 xcode 生成物（`Data/Entities/DesignGuideline.cs`、`Data/DesignSystem.htm` 更新）、`Data/Entities/DesignGuideline.Biz.cs`、`Data/DesignSystemTables.cs`（登记）。
- `Plugins/DesignSystem/web/src/**`：新增 `sections/Guidelines.vue`；允许改的既有文件限 03 列出者（`shell/nav.ts`、`DesignSystemView.vue`、`api.ts`、M2 的 `showroom/TunePanel.vue`/`start/StartMode.vue`/`showroom/tune.ts`、`design/glossary.ts`、`design/vocabulary.test.ts`）以及 `sections/ReleaseBoard.vue` 的**唯一一处**新增提示。
- M1 的 `Agent/*`、`Services/{StylePresets,GenerationService,DesignBriefBuilder,AgentRulesBuilder}.cs`（增量）；对 M1/M2 测试文件的**增量**更新（每处登记偏差）。
- `ForgeSelf.Web/e2e/plugins/design-system/design-system-style.spec.ts`（新增）；`.agents/skills/design-system-verify/SKILL.md`、`.agents/skills/design-system-consume/SKILL.md`；`.temp/ds-m3/**`；本目录工件、日记、`TODO.md`。

### Forbidden

- 改**既有 12 张表**的任何表/列/索引；删除或重命名任何列；手改 xcode 生成物（`X.cs`、`DesignSystem.htm`）。
- 回填存量项目的规范、改动已发布快照文件、任何物理删除能力（含规范的 DELETE 路由）。
- 扩充组件蓝本（`ComponentBlueprints` 仍 10 个）、新增审计类别、改 Stardust 十类实体与 `meta.entities`、改 `meta.auditKinds`。
- 改既有 14 个 `sections/*.vue`（`ReleaseBoard.vue` 的唯一提示行除外）、删减/弱化既有 e2e 断言。
- 改 `ForgeSelf.Api/**`、`Plugins/McpCenter/**`、`Plugins/AIAgent/**`、其他插件；新增 npm/NuGet 依赖；接 LLM；`v-html` 渲染规范正文。
- 规范/模特/展厅里写数字单位字面量或字面色值（见 03 §G2 撰写约束与 M2 §M 写法约束）。
- `git commit/push/tag`（未授权）；停/启/杀用户运行中的宿主进程；用一次性临时脚本作验证结论（AGENTS §0 红线；§A5 的基线录制器是**测试资产**，不在此列）。

## Acceptance Criteria

- [ ] **T-A**：AC1–AC11（黄金回归、轴行为/审计矩阵/词表校验/复现、13 预设、同源与变量契约、工具/REST 增量、前端、A 片 e2e）
- [ ] **T-B**：AC12–AC20（表与旧库升级、生成器、仓储服务、REST、导出、快照 schema 3、工具、界面、数据安全）
- [ ] **T-C**：AC21、AC22、AC26（视觉 QA、文档与技能、**规范文案交用户审阅**——AC26 规格归 B 片、交付在步骤 14）
- [ ] **全局**：AC23–AC25（后端过滤集 + web 三件 + e2e 回归、范围零违规、版本 3.1.0）

## Task Slices

| 片      | 步骤（03 Implementation Steps） | 完成定义（DoD）                                                                                                     | 片级验证                                                              |
| ------- | ------------------------------- | ------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------- |
| **T-A** | 1–5                             | AC1–AC11 全 Verified；黄金回归全程绿；CP-A 证据写入 05                                                              | 后端过滤集 + web 三件 + `design-system-style.spec.ts` A 块 + 既有 e2e |
| **T-B** | 6–13                            | AC12–AC20 全 Verified；A 片用例仍绿                                                                                 | 同上 + B 块                                                           |
| **T-C** | 14–15                           | AC21、AC22、AC26 与 AC23–AC25 全 Verified（e2e/读图不可行项 Unknown + 替代证据）；规范全文清单已交用户；05 全部填完 | 全套 + 视觉 QA                                                        |

## 技能增补建议（`design-system-verify` 自查表续号，写"历史坑"式一句话 + 判据）

1. **默认值逐字节兼容**：新增可选生成参数时，先在改代码前录制黄金哈希（含 `GeneratorSeed`），默认路径任何变化都必须是有意且升版的。
2. **新轴必须有白名单**：非默认取值只许改指定路径、且必须真改；其余路径逐条不变；再配"轴 × 取值 × 主题"的审计矩阵（"只断言产物通过"证明不了新轴在起作用）。
3. **文本型规范不写数字**：数字渲染时同一函数现查，令牌改值规范不漂移；写进文本的数字就是第二份真相。
4. **快照升 schema 要 kind 级可比**：只区分"有没有 Specs 节"会把新增规格种类报成内容新增；并钉"无新规格的项目 hash 不变 → 同版本重发仍幂等"。
5. **加表的升级路径**：旧库升级测试（先建旧表集、写数据、再 `EnsureCreated`）+ xcode 二次生成逐字节一致 + 不回填存量；"新表自动建出"不等于"存量数据没被动"。

## Verification Commands

```bash
# 环境
$env:TMP='D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\.temp\ds-m1\tmp'; $env:TEMP=$env:TMP

# 后端
dotnet build Plugins/DesignSystem/DesignSystem.csproj
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~DesignSystem" --logger "console;verbosity=normal"
dotnet test ForgeSelf.Api.Tests --no-build --filter "FullyQualifiedName~DesignSystem" --list-tests

# 插件前端
cd Plugins/DesignSystem/web && pnpm run check && pnpm run test && pnpm run build

# e2e（真实宿主）
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/design-system/design-system-style.spec.ts
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/design-system

# 工件与范围
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-01-design-system-m3-style-guideline
git diff --stat -- Plugins/DesignSystem/Data/Model.xml Plugins/DesignSystem/Data/Entities ForgeSelf.Api Plugins/McpCenter
git diff -- Plugins/DesignSystem/Data/Model.xml
```
