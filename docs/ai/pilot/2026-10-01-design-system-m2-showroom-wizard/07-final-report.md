# AI-Native Pilot Result（最终汇报）

> 任务结束强制格式｜载体：markdown 正文直发群消息（规范 §5；仅超单消息上限或用户明确要求时用附件，且附正文摘要）
> 状态只报事实，禁止模糊表述（对齐 AGENTS.md §10.4/§10.5）。

> **状态：COMPLETED** —— M2（设计插件 v3.0.0 · 展厅与向导）全链闭环：切片 A/B/C 的 AC1–AC28 全填并 Verified；闸门1（2026-10-02 用户批准）、**闸门2 独立复验已执行（预注册 V0–V15）**，复验首判 CHANGES_REQUIRED 的 **5 条必办项已逐条闭环**，复验过程中暴露的 **1 项真实产品缺陷（D14）** 与 **2 项测试侧缺陷（M-A/B2）** 已修复并复验；`06-review.md` 结论已回填为 **APPROVED**。
> 未做项（发布 / 运行实例只读复验 / 真人外行走查）**如实列在 §12 与 §7**，不冒充完成。

## 1. Repository Understanding

见 `00-repository-understanding.md`。技术栈/架构/测试方式均从真实仓库内容确认，未用常识推测；本文件不重复其内容（本里程碑未改 00）。

## 2. Selected Task

设计插件 v3.0.0 · **M2 = 向导 + 展厅 + 交付与接入**（`PILOT-ds-m2-showroom`），对应 `01-intent.md` / `02-spec.md` / `03-plan.md` / `04-task.md`。里程碑标识 = `M2 completed acceptance`。

## 3. Changed Files（M2 所属；并行 M3 在制品一律排除）

**后端（插件自包含）**
- `Plugins/DesignSystem/Services/PreviewCssService.cs`（新增）：内存预览 CSS（不落库），与落库导出共用同一快照构造
- `Plugins/DesignSystem/Services/ExportService.cs`：抽出公开静态 `BuildSnaps(TokenGraph)`（排序/解析/补 hex 单一真源）+ 新增 `SnapshotFromGraph`
- `Plugins/DesignSystem/Controllers/DesignSystemController.cs`：注入 `PreviewCssService` + `POST generate/preview-css` + `meta.capabilities` 追加 `preview-css`
- `Plugins/DesignSystem/DesignSystemPlugin.cs`：装配并注册 `PreviewCssService`
- `Plugins/DesignSystem/Services/DesignSystemConstants.cs`、`Plugins/DesignSystem/plugin.json`：版本三元组 `2.8.0 → 3.0.0`

**后端测试**
- `ForgeSelf.Api.Tests/Plugins/DesignSystemTests/PreviewCssTests.cs`（新增，AC1/AC2/AC3）
- `ForgeSelf.Api.Tests/Plugins/DesignSystemTests/MannequinVariableContractTests.cs`（新增，AC4）
- `ForgeSelf.Api.Tests/Plugins/DesignSystemTests/GenerateShapeTests.cs`（构造签名连带，见 03-plan D13）

**插件前端（`Plugins/DesignSystem/web/src/`）**
- 新增：`shell/`（四模式 + 模式条）、`start/`（4 步向导）、`showroom/`（衣柜/舞台/微调/并排对比 + 9 个模特页 + `mannequin.css`）、`delivery/`（六卡）、`design/{glossary,latest,route}.ts` 及各自测试
- 修改：`DesignSystemView.vue`（外壳分支）、`api.ts`、`index.ts`、`styles/base.css`、`design/skin.ts`（参数化作用域）、`showroom/Showroom.vue`（**D10 修复**：状态重置 watcher 改盯 `selectedId`；**D14 修复**：新增 `sourceSettled()` 深链守卫）

**宿主（D7 越层最小改动，已在 03-plan 留档）**
- `ForgeSelf.Web/src/services/authInit.ts`（`stripTokenFromHash`：只摘 `token=`，无 token 不动 fragment）、`ForgeSelf.Web/src/main.ts`（`beforeEach` 带回 `path/query/hash`）、`ForgeSelf.Web/src/services/__tests__/authInit.test.ts`

**e2e**
- `ForgeSelf.Web/e2e/plugins/design-system/design-system-showroom.spec.ts`（新增，A/B/C/D 四片 12 用例）
- `ForgeSelf.Web/e2e/plugins/design-system/design-system-helpers.ts`（新增）
- `ForgeSelf.Web/e2e/plugins/design-system/design-system.spec.ts`（仅 `enterWorkbench` 助手 + 2 处调用，14 行新增、**无断言删改**）

**文档与技能**
- `Plugins/DesignSystem/{README.md,ROADMAP.md}`、`docs/02-features/036-design-system.md`、`.agents/skills/design-system-{verify,consume}/SKILL.md`
- 本任务工件 `docs/ai/pilot/2026-10-01-design-system-m2-showroom-wizard/{03-plan,04-task,05-evidence,06-review,07-final-report}.md`

**明确排除（并行 M3 在制品，不随本次提交）**：`Services/StyleAxes.cs`、`Services/TypographyGenerator.cs`、`Services/DesignGenerator.cs`、`Services/ScaleGenerators.cs`、`Services/StylePresets.cs`、`Services/PresetRecommender.cs`、`ForgeSelf.Api.Tests/**/{StyleAxis*Tests,StylePresetAxisTests,DesignAgentToolTests,PresetRecommenderTests,StylePresetsTests}.cs`；**另**：`Controllers/DesignSystemController.cs` 内 M3 追加的 `meta.styleAxes = StyleAxes.Vocabulary()` 2 行留在工作树（若一并提交会引入对未提交文件 `StyleAxes.cs` 的依赖而不可编译）。

## 4. Validation

| 环节 | 命令 | 结果（来源等级） |
| --- | --- | --- |
| 插件后端构建 | `dotnet build Plugins/DesignSystem/DesignSystem.csproj` | ✅ `0 个警告 / 0 个错误`（6.20s）（Verified） |
| 插件前端静态检查 | `cd Plugins/DesignSystem/web && pnpm run check` | ✅ EXIT=0，0 error（vue-tsc -p tsconfig.check.json + eslint）（Verified） |
| 插件前端单测 | `pnpm run test` | ✅ `Test Files 18 passed｜Tests 239 passed`（238 + 1 条 D14 回归）（Verified） |
| 插件前端构建 | `pnpm run build` | ✅ EXIT=0；`dist/style.css 92.11 kB`、`dist/index.js 408.82 kB`（116 modules, 3.91s）（Verified） |
| 后端过滤集 | `dotnet test ForgeSelf.Api.Tests --no-build --filter "FullyQualifiedName~DesignSystem"` | ✅ **362/362**、`--list-tests` 发现 **362**（总数==发现数）（Verified） |
| e2e（**权威**） | `pnpm exec playwright test --config=playwright.config.ts e2e/plugins/design-system --workers=1` | ✅ **19 passed**（EXIT=0）（Verified） |
| e2e（默认并行） | 同上去掉 `--workers=1` | ⚠️ 加固后 5 轮 = **3 绿（par4/par5/par8）/ 2 红（par6 SQLite 锁 M-G / par7 写开关竞态 M-H）**；两红均非 M2 代码缺陷（Verified） |
| 视觉 QA 量化 | 对 `m2/qa-*.png` 30 张逐张 1×1 缩放取均值色 | ✅ **15 组明暗对照全 OK**（FAIL_COUNT=0）（Verified） |
| 反向探针 | 5 个（classes/dialogs/glossary/mannequins/latest） | ✅ **5/5 变红**，清理后复跑全绿（Verified，见 06-review V9） |
| 工件链门禁 | `scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-01-design-system-m2-showroom-wizard` | ✅ PASS（00–07 八件 + 关键节）（Verified） |
| 视觉走查（人工） | 30 张 QA + 并排/交付/向导截图逐张读图 | ⚠️ 机器量化 30/30 通过；人工目视覆盖其中 22 张（其余 8 张仅量化）——**如实登记**（Verified 部分 + Unknown 部分） |
| 真人外行走查 | — | ❌ 未做（见 §12 ④ 与 §7 L4）（Verified 未做） |

## 5. Evidence

- `05-evidence.md`：AC1–AC28 证据矩阵、Build/Unit/E2E/Static Analysis/Screenshots、Known Limitations、**「闸门2 复验回应（2026-10-03）」**（处置对照表 + 真实复跑输出 + 30 张量化表 + 仍未做项）。
- `06-review.md`：预注册 V0–V15 与判定规则原文（未改）、复验执行记录、审查八问、四类 Check、Risk R1–R3、Findings（C-1 / M-A~M-H / m-1~m-5）、**Final Decision = APPROVED**（含 5 条必办项闭环表）。
- `03-plan.md`：Plan 偏差 D1–D14（D9/D10/D14 为"实现/复验后发现的真实产品缺陷"；D11/D12/D13/D14 为闸门2 复验新增）。

## 6. Review

`06-review.md` **Final Decision = APPROVED**（2026-10-03）。原判 CHANGES_REQUIRED 的 5 条必办项闭环：① A1 去掉全库计数（改为页级写请求 0 条 + 已有项目指纹未变）→ ② D1 加"主题已生效"门并重拍 30 张 → ③ 05 的 e2e/读图表述更正（撤回"0 flaky"）→ ④ `GenerateShapeTests.cs`/`DesignSystemPlugin.cs` 按 D13 留档 → ⑤ 提交范围排除 M3 在制品且工作树可编译。复验另暴露 **1 项真实产品缺陷 D14**（深链选择丢失，已修 + 单测回归）与 **2 项测试侧缺陷**（A1 全家计数、B2 首/末件假设，均已改为确定性判据）。

## 7. Risk

- **L1（中）并行 e2e 的两类全局态竞态（M-G SQLite 单写者锁 / M-H 写开关全局翻转）**：默认并行下 M2 目录不能声称稳定全绿（实测 3/5）。**缓解**：以串行 `--workers=1` 为 M2 权威门禁（确定性 19/19）；根治需宿主 DB 连接配置或仓库级 e2e 分片策略（均超出 M2 边界）→ TODO 另立任务。**注意 M-H 是 M2 新引入的测试侧竞态**，已在 06-review 如实归因。
- **L2（低）D7 宿主越层改动**：`ForgeSelf.Web/src/{main.ts,services/authInit.ts}` 不在 04 Allowed 清单内。缓解：改动最小化（只摘 `token=`；无 token 不动 fragment），由 e2e C5 + 宿主单测钉住；待规划方/用户裁定是否另立任务。
- **L3（低）插件侧 `README.md` v3.0.0 条目写「`test` 15 文件」**（实测 18 文件，复验 m-1，Minor）：文档陈旧计数，记 TODO（P3）。未在本轮改，避免与并行 M3 会话改写 README 冲突。
- **L4（中）验证覆盖缺口**：真人外行走查（V12）与工具面 `design_review` 喂模特源码（V10）未做；人工目视只覆盖 30 张中的 22 张（其余靠量化）。**不得据本报告推断这两项已验证**。
- **L5（低）截图取景局限**：桌面设备框 1280px > 舞台可见宽 648px，右半在横向滚动区外（§FR9 既定行为），裁切区内容未逐张细读（Unknown）。

## 8. Problems Found

**真实产品缺陷（实现/复验中发现并修复）**
1. **D9** 深色档标题低对比：外壳 `.ds h1..h4{color:var(--ds-fg-1)}`（0-1-1 特异度）劫持模特页标题色 → 修复为 semantic 令牌 + 新增 WCAG 对比度断言（30 档全 ≥4.5:1）。
2. **D10** 深链 `theme=dark` 被重置为 `light`：状态重置 watcher 挂在衣服**对象引用**上，`buildOutfits` 按 id 重建对象被误判为"换衣服" → 改盯 `selectedId` + 2 条回归。
3. **D14**（复验新发现）**深链选择丢失**：`restored` 闩锁在"衣柜首个非空快照"消费深链，项目先到/预设后到即让目标预设被判"不存在"并永久回落 `list[0]` → 新增 `sourceSettled()`（只等同类来源）+ `loadPresets` `finally` 落定 + 回归用例。
4. **D3** `GET presets` 不返回 `request`（预览取数缺口）→ 合并 `listPresets()` 与 `recommendPresets()`；**D1** `preview-css` 无 400 路径 → 入口 `GuardInput` 数值域校验 + 控制器 `Guard` 映射。

**测试自身缺陷（复验暴露，已修）**
5. **M-A / A1**：用"全库项目总数"判定"微调不写库"，并行下被同目录其它用例改写 → 改为**页级写请求 0 条** + **已有项目指纹未变**。
6. **B2 / AC16**：用"衣柜首件 vs 末件"，并行下首件变成别的用例建的项目衣服、与末件预设撞色 → 改为固定两件内置预设（解析底色确定不同）。
7. **M-B / D1**：截图早于主题重绘（首张深色图拍到旧态）→ 加"主题已生效"门（亮度方向 + 两帧 rAF）。
8. **D8**：e2e 初稿把 `review.summary/findings` 当字符串（实为结构体）+ 三处竞态/超时 → 改测试，不改后端。

**范围/流程问题（已留档）**
9. **D7** 宿主 `main.ts`/`authInit.ts` 抹掉插件 fragment（FR14 深链不可达）→ 最小改动 + 03-plan 留档；**D13** `GenerateShapeTests.cs`/`DesignSystemPlugin.cs` 属 Allowed 外必要连带 → 03-plan 留档。
10. **R3 多会话同工作树并行落盘**：复验期间 M3 在制品曾使工作树不可编译（C-1，M3 已自修）；提交边界必须人工切分。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | 好：技术栈/测试体系从真实仓库确认；未出现"常识推测"被推翻的情况。 |
| Intent → Spec | 好：AC1–AC28 可测、边界清晰；`Unknown` 项在实现中逐个收敛（D3/D4）。 |
| Spec → Plan | 好：Plan 落到真实文件；偏差 14 条（D1–D14）全部留档，未出现"改了没记"。 |
| Plan → Code | 好：分片 A/B/C 增量推进，每片收口即跑门禁。 |
| Code → Test | 中偏上：单测/后端过滤集/e2e 齐备；**但 A1、B2 两条 e2e 判据一开始就带"实例是安静的"隐含假设**，直到独立复验在默认并行下才暴露——说明"写完就跑一次串行"不足以证明断言抗并发。 |
| Test → Evidence | 中：首轮 Evidence 出现过度表述（"19 passed / 0 flaky"、"30 张逐张读图"），被独立复验推翻后已逐条更正；**教训 = Evidence 里的数字必须注明"在什么配置下跑出来的"**。 |
| Evidence → Review | 好：闸门2 预注册清单在实现前写好、复验未改判据，最终以 APPROVED + 必办项闭环收口。 |

## 10. 最重要的问题

**同一 worktree 上多会话并行开发 + e2e 目录默认并行共用单一后端实例**，这两件事叠加会把"环境/全局态问题"伪装成"用例问题"，双向都危险：

1. **假红**（M-A/B2/M-G/M-H）：断言依赖全局态（全库计数、衣柜首件、写开关、SQLite 锁）→ 并行下随机红，若不追根因就会误当 flake 放过；
2. **假绿**：本任务首轮 Evidence 的"19 passed / 0 flaky"就是在一次幸运的复跑上得出的结论；
3. **提交边界漂移**（R3）：M3 在制品一度让工作树不可编译，且 M3 的 2 行侵入 M2 所属文件（`DesignSystemController.cs`）——提交时必须按文件切分并逐行剥离，否则提交要么不可编译、要么把别人的在制品带走。

**已固化的对策**：a) 断言只依赖"本页/本用例"可观测量（页级请求、项目 id+指纹、固定预设 id）；b) 涉及全局态翻转的用例，以**串行**为权威门禁并在 Evidence 注明配置；c) 共享工作树的提交按文件清单切分，并在工件里写明排除项。

## 11. 下一步建议

1. **M3 闸门1**（用户批准后方可开工）：`docs/ai/pilot/2026-10-01-design-system-m3-style-guideline/`；M3 的 `meta.styleAxes` 等改动进入 `DesignSystemController.cs` 后，本文件 §3 的"排除项"随之失效，M3 自行提交即可。
2. **本任务遗留 TODO（P2）**：e2e 全局态竞态根治（M-H 拆 project/分片，或该目录一律 `workers=1`）；**（P2）** SQLite 单写者锁（M-G）——宿主侧加 `busy_timeout`/WAL 或 e2e 侧降低写并发。
3. **（P3）** 插件 `README.md` 陈旧计数（15 → 18/19 文件）；**（P2）** D7 宿主越层改动的归属裁定（是否另立任务）。
4. **发布链（须用户授权）**：`plugin-development` §四 第③步（打 tag 自动发布 / 本地目录更新源）与第⑤步（运行实例只读复验）本任务**未做**。

## 12. 五步闭环（plugin-development §四）完成度

| 步骤 | 状态 | 说明 |
| --- | --- | --- |
| ① 门禁（后端 / 插件前端） | **已做 ✅** | 插件 csproj `0 警告 / 0 错误`；插件 web `check` 0 error + `test` 18 文件/239 用例 + `build` 通过；后端过滤集 362/362。 |
| ② 插件层 e2e | **已做 ✅（权威=串行）** | 串行 `19 passed` EXIT=0；默认并行 3/5（两红均为全局态竞态 M-G/M-H，非 M2 代码缺陷，已归因 + TODO）。 |
| ③ 发布（打 tag 自动发布 / 本地目录更新源） | **未做** | 须用户授权；规划方与实现方均不得代做。 |
| ④ 隔离实例走查 | **已做（agent 走查 + 读图）⚠️** | 隔离实例按用户视角走 A/B/C/D 四片并落 30 张 QA + 各用例截图、逐张读图/量化；**真人外行走查（V12）未做**，如实登记。 |
| ⑤ 运行实例只读复验 | **未做** | 触发条件未满足：须用户在 `:51888` 等运行实例更新到本版本后，方可只读复验 token/版本徽标/主链路中间态。 |
