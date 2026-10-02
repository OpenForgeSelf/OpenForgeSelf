# Review

> 阶段：Stage 8｜Reviewer 视角重查 Intent → Spec → Plan → Task → Code → Test → Evidence 全链。
> 结论只报事实；CHANGES_REQUIRED 时须指明回退到哪个阶段。

> **状态：已独立复验（2026-10-03，验收方复跑）——结论见文末 Final Decision。**
> 本文件的「验收清单（V0–V15）」与「Final Decision 判定规则」是**实现开始前**预注册的原文，本次复验**未改动**；以下复验记录与结论由验收方在独立复跑后填写。
> 可**片级验收**（T-A / T-B / T-C 各自完成即可交验）。

## 验收清单（预注册）— 规划/验收方独立复跑，不采信 05-evidence 自述

| 编号 | 动作                                                                                                                                                                                                                                                                                                | 通过判据                                                                                                                                                                              |
| ---- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| V0   | 前置：确认 04-task「闸门状态」两项均 ✅（M1 APPROVED + 用户批准）；读 05-evidence 列出声称 Verified 的条目；`git status` / `git diff --stat` 清点改动                                                                                                                                                | 每个 Verified 都附真实输出；改动文件集合 ⊆ 04-task「Allowed」；Forbidden 零改动                                                                                                       |
| V1   | 环境：`TMP/TEMP` 重定向、UTF-8、`--logger "console;verbosity=normal"`                                                                                                                                                                                                                               | 之后所有测试命令在此环境下跑                                                                                                                                                          |
| V2   | `dotnet build Plugins/DesignSystem/DesignSystem.csproj`；`cd Plugins/DesignSystem/web && pnpm run check && pnpm run build`                                                                                                                                                                          | 0 error；新增后端代码 0 warning；`dist/index.js` 与 `style.css` 产出（`dist` 已被 `.gitignore` 忽略）                                                                                 |
| V3   | 后端过滤集 `FullyQualifiedName~DesignSystem` + `--list-tests`                                                                                                                                                                                                                                       | 失败 0；**报告总数 == 发现数**；含 `PreviewCssTests`、`MannequinVariableContractTests`；与 05 报告数一致                                                                              |
| V4   | `cd Plugins/DesignSystem/web && pnpm run test`                                                                                                                                                                                                                                                      | 全绿；文件数/用例数与 05 一致；含 `wizard / outfits / tune / scenes / route / snippets / glossary / skin / latest / mode` 与四个守卫（`dialogs / mannequins / classes / vocabulary`） |
| V5   | e2e：`design-system-showroom.spec.ts` 与既有 `e2e/plugins/design-system`                                                                                                                                                                                                                            | 新 spec 的 A/B/C 三块全绿；既有无新增红；环境不可行 → Unknown + 替代证据并告知用户                                                                                                    |
| V6   | 范围：`git diff --stat -- Plugins/DesignSystem/Data/Model.xml ForgeSelf.Api Plugins/McpCenter Plugins/DesignSystem/web/src/sections`；csproj / package.json / 锁文件                                                                                                                                | **为空**；无新增依赖（`package.json` 除可能的 `version` 外无 diff）                                                                                                                   |
| V7   | e2e 既有 spec 的 diff 审查：`git diff -- ForgeSelf.Web/e2e/plugins/design-system/design-system.spec.ts`                                                                                                                                                                                             | 只出现 `enterWorkbench` 的新增与两处调用；**无任何断言被删/改/弱化**                                                                                                                  |
| V8   | **DOM 契约对表**（03 §U）：在 e2e 隔离实例里逐项核对模式条名称、`data-*` 与 `aria-label`、模特 `data-mq-wear` 数量下限（§M）                                                                                                                                                                        | 全部一致                                                                                                                                                                              |
| V9   | **反作弊与抽测**：不看 Evidence，独立复现 ≥6 条 AC（建议：AC2 同源、AC4 变量契约、AC7 prompt 守卫、AC11 并发/防抖、AC13 画布底色、AC18 令牌不明文）；做 5 个反向探针（见 05 表）并**亲眼看到变红**；检查测试质量：无永真断言、无被 `skip` 的用例、`it.each` 确实展开、新增用例清单覆盖 03 Test Plan | 反向探针全部变红；无作弊形状                                                                                                                                                          |
| V10  | **模特零字面量人工复核**：随机打开 3 个模特 `.vue`/`mannequin.css`，肉眼找字面色值、字体栈、`px` 圆角/阴影/内外边距；用 M1 的 `design_review`/`POST projects/{id}/review`（若 M1 已落地）把一个模特源码喂进去                                                                                       | 人工与工具均 0 error；人工无字面量漏网                                                                                                                                                |
| V11  | **视觉验收（我亲自读图）**：重跑 e2e 截图后逐张读图；至少覆盖 3 预设 × 5 场景 × 明/暗，加上并排对比、交付页、向导 4 步各 1 张；按 Level 3 清单核对（图标/间距/颜色/留白/对齐/遮挡/溢出/空态/错误态）                                                                                                | 无遮挡/溢出/错位/残留占位；缺陷清单与 05 一致；严重缺陷 = CHANGES_REQUIRED                                                                                                            |
| V12  | 体验走查（隔离实例，模拟"完全不懂设计的外行"）：从空库开始，只用向导 + 展厅完成"选风格 → 微调 → 创建 → 去交付页复制 agent-rules"；记录卡点与不友好处                                                                                                                                                | 无需任何专业术语即可走通（默认大白话）；卡点写入 Findings                                                                                                                             |
| V13  | 数据安全与写入面：展厅/试穿全程 12 表行数不变（手动 REST 核对）；界面无删除入口；写开关用例收尾已改回；e2e 项目只软归档                                                                                                                                                                             | 全部成立                                                                                                                                                                              |
| V14  | 文档与技能（AC28）：README / ROADMAP / 036 / `design-system-verify` / `design-system-consume` 事实同步                                                                                                                                                                                              | 逐项存在且与实现一致                                                                                                                                                                  |
| V15  | 五步闭环里规划方不可代做的项（打 tag 发布 / 用户页面更新 / 运行实例只读复验）是否被如实列为"待用户授权"                                                                                                                                                                                             | 如实列出                                                                                                                                                                              |

### Final Decision 判定规则（预注册）

- **APPROVED**：AC1–AC28 全部 Verified（e2e/读图因环境不可行的须 Unknown + 替代证据并告知用户）；V6/V7 零违规；V9–V11 无阻断发现；基线红与新增红已区分。
- **CHANGES_REQUIRED**：存在可修缺陷；必须指明回退阶段与具体条目。
- **BLOCKED**：M1 未通过 / 用户未批准 / 环境或并行会话冲突导致无法验证；写明阻塞原因与升级对象。

## 复验执行记录（2026-10-03，验收方独立复跑）

**快照声明（重要）**：本次复验在 worktree `D:\src\my-proj\OpenForgeSelf\OpenForgeSelf` 上执行。复验进行中，**并行会话在同一 worktree 落入了 M3 在制品**：`Plugins/DesignSystem/Services/StyleAxes.cs`（新增，mtime `2026-10-03 01:19:54`，文件头自注「M3 §A1」）与 `Services/TypographyGenerator.cs`（修改，mtime `01:21:12`，diff 自注「M3 字体搭配轴 `editorial`」）。因此：

- V2/V3/V4/V5 的结论**对「M3 落盘前」的 M2 快照有效**（命令与被测产物时间戳见下）；
- V0/V6 的「改动集合 ⊆ Allowed」结论**仅对该快照成立**；
- 复验后段 `dotnet publish`（e2e globalSetup）已因 M3 在制品失败（见 Findings）。

| 编号 | 复跑命令 / 动作                                                             | 结果          | 证据（真实输出）                                                                                                                                                                                                                                                                                                                                             |
| ---- | --------------------------------------------------------------------------- | ------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| V0   | 读 04「闸门状态」；读 05 声称 Verified 项；`git status --porcelain`、`git diff --stat` | ⚠️ 部分       | 04 两项均 ✅（M1 APPROVED + 2026-10-02 用户批准）。改动集合**超出 Allowed**：`ForgeSelf.Web/src/{main.ts, services/authInit.ts, services/__tests__/authInit.test.ts}`（D7，03-plan 已留档）、`ForgeSelf.Api.Tests/Plugins/DesignSystemTests/GenerateShapeTests.cs`、`Plugins/DesignSystem/DesignSystemPlugin.cs`（后两者 03-plan「Files To Change」与 04 Allowed 均未列，属必要连带改动但**未留档**）。Forbidden 零改动 ✅ |
| V1   | `$env:TMP/TEMP` 重定向到 `.temp/gate2/tmp`；`--logger "console;verbosity=normal"` | ✅            | 之后所有后端测试命令均在此环境执行（日志：`.temp/gate2/v3-test.log`）                                                                                                                                                                                                                                                                                        |
| V2   | `dotnet build Plugins/DesignSystem/DesignSystem.csproj`；`pnpm run check`；`pnpm run build` | ✅（快照时）  | `build-plugin EXIT=0`；`check` exit 0；产物 `dist/index.js = 408399 B`、`dist/style.css = 92107 B`（与 05 报告 `408.40 kB / 92.11 kB` **逐字节一致**）。⚠️ 复验后段同一 csproj 已变为 **2 error**（见 Findings M-C）                                                                          |
| V3   | `dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~DesignSystem"` + `--list-tests` | ✅            | `测试总数: 362 / 通过数: 362`、`总时间: 7.0608 分钟`；`--list-tests` 解析 `discovered=362`（**报告数 == 发现数**）；`PreviewCssTests=21`、`MannequinVariableContractTests=2`、`ExportProjectionTests=25`、`DesignSystemAuthTests=3`（均 >0，与 05 一致）                                                                                                    |
| V4   | `cd Plugins/DesignSystem/web && pnpm run test`                              | ✅            | `Test Files 18 passed (18)`、`Tests 238 passed (238)`；四个守卫（`dialogs / mannequins / classes / vocabulary`）与 `wizard/outfits/tune/scenes/route/snippets/glossary/skin/latest/mode` 均在列                                                                                                                                                                |
| V5   | `pnpm exec playwright test --config=playwright.config.ts e2e/plugins/design-system`（默认并行）＋ `--workers=1`（串行） | ❌ → ✅        | **默认并行：`18 passed` + `1 failed`（2.0m）**——失败条 `A1 AC13`，断言 `试穿/微调不得写入任何项目`，`Expected: 2 / Received: 3`；**串行 `--workers=1`：`19 passed (2.2m)`**。⇒ 默认并行下**不能稳定复现 05 声称的 19/19、0 flaky**（根因见 Findings M-A）                                                                                                   |
| V6   | `git diff --stat -- Model.xml ForgeSelf.Api McpCenter web/src/sections`；porcelain（含未跟踪）；依赖文件 diff | ✅            | 四处 `git diff --stat` **全空**；上述路径 porcelain **全空**；`Plugins/DesignSystem/web/package.json` 无 diff；`plugin.json` 仅 `Version: 2.8.0 → 3.0.0` ⇒ **无新增依赖**                                                                                                                     |
| V7   | `git diff -- ForgeSelf.Web/e2e/plugins/design-system/design-system.spec.ts` | ✅            | 仅新增 `enterWorkbench` helper、2 处调用与注释；`14 insertions(+), 0 deletions(-)`；**无断言被删/改/弱化**                                                                                                                                                                                                                                                      |
| V8   | 03 §U/§M 对表 ↔ e2e 断言常量                                                | ✅            | 模式条四名（开始/展厅/工作台/交付与接入）、`data-stage` 五属性（outfit/theme/device/scene/page）、`aria-label`（明暗/设备/场景/页面/衣柜）、九页 `§M` 下限（14/5、18/6、12/5、14/6、12/6、14/4、14/6、16/5、12/5）与 spec 内 `ADMIN_PAGES/BOARD_PAGES/B_PAGES` **逐项一致**                                                                             |
| V9   | 不看 05 独立复现 + 5 个反向探针 + 测试质量检查                              | ✅            | 自建临时探针（用完即删、已确认无残留）：`classes`（悬空 `ds-*` 类）、`dialogs`（`window.prompt`）、`glossary`（未知词条）、`mannequins`（`#fff` 字面量）、`latest`（单点改写 `isCurrent` ⇒ `latest`×2 + `wizard` 陈旧响应）**全部变红**（`7 failed / 234 passed` 于 241 例）；清理后复跑 **238 全绿**。质量：无 `.skip/.todo/.only`；4 处 `it.each` 真展开（探针文件被其逐文件用例自动纳入，证明非空转） |
| V10  | 人工读 3 个模特文件；M1 工具复核                                             | ⚠️ 人工 3/3 ✅，工具未跑 | `mannequin.css`、`MobileHome.vue` 肉眼 **0 字面量**（颜色仅 `var(--ds-*)`/`transparent`/`inherit`；尺寸仅百分比与 `var(--ds-*)`）；`design_review`/`POST review` 喂入模特源码**未做**（记局限）                                                                                                                                                        |
| V11  | e2e 重拍后逐张读图 + 全 30 张量化（1×1 缩放均值色）                         | ❌（1/30 格作废） | **30/30 已量化、22/30 已目视**。`qa-admin-calm-admin-dark.png` 均值 `(232,235,237)` ≈ 其浅色档 `(232,234,237)` → **该格未反映深色档**；其余深色档 mean 42–58（正常）。同一 `admin-dashboard` 页在另两预设下深色正确（`finance-trust` / `workbench-focus` 的 admin-dark 肉眼确为深色）⇒ **取证时序问题，非产品缺陷**。其余未见遮挡/溢出/错位/残留占位。D1 机器判据：`矩阵完成：共 30 张`、`标题对比度最小 13.75 / 低于 4.5:1 的档位=无`、桌面档 `框宽=1280 可见宽=648`（裁切已量化） |
| V12  | 隔离实例模拟外行走查                                                         | ⚠️ 未做       | 未做真人走查；可替代的间接依据仅 e2e A1（空库四步建成功）+ 术语词典实现，**不足以替代本项**（记局限）                                                                                                                                                                                                                                                          |
| V13  | 数据安全与写入面                                                             | ⚠️ 部分成立   | ① 12 表行数不变：由 `PreviewCssTests.AC1_八预设_明暗_非空且含surfaceBg_且零写库` 真断言（`DesignProject…DesignRelease` 共 12 个 `FindCount()` 调用前后 `Equal`）✅；② 交付页无删除入口：`delivery/**` grep `删除\|archive\|DELETE` **0 命中** ✅；③ 写开关收尾还原：spec C3 先 PUT 往返再还原原值 ✅；④ **「e2e 项目只软归档」不成立**——spec 头注释如此声称，但全文无 `afterAll`/归档调用，`ensureProject` 只建不归档 ❌                                                                              |
| V14  | `git diff` 五处文档/技能                                                     | ✅（1 Minor）  | README / ROADMAP / 036 / `design-system-verify` / `design-system-consume` 五处均更新且与实现一致；Minor：README v3.0.0 条目写「`test` 15 文件」，实测 **18 文件**                                                                                                                                                                                              |
| V15  | 07 §12 五步闭环完成度表                                                      | ✅            | ③ 发布 = `未做`「须用户授权，规划方与实现方均不得代做」；⑤ 运行实例只读复验 = `未做`「须用户在运行实例更新到新版本后触发」——如实列出                                                                                                                                                                                                                          |

## 审查八问（逐项回答）

1. **是否真正满足 Intent**：是（就 M2 范围）。四模式外壳、向导真落库、展厅"试穿"同源、交付与接入页、术语词典、哈希深链均落地并有断言钉住；AC13 同源判据（画布 computed 底色 == 后端 `semantic.surface-bg`）为硬证据。
2. **是否符合 Spec**：基本符合。偏差（D7 宿主 3 文件、`GenerateShapeTests.cs`、`DesignSystemPlugin.cs`）已由 03-plan D7 或 05-evidence 部分留档；后两者未进 Allowed 清单（见 Minor）。
3. **是否超出 Scope**：**是**。除 D7 外的 2 个文件未在 Allowed/03-plan 内（必要连带改动）；另有**并行 M3 在制品**进入同一工作树（非 M2 授权范围）。
4. **是否改了不该改的文件**：Forbidden 零改动；但 Allowed 外有 2 个未留档文件 + 1 组已留档文件（D7）。
5. **测试是否覆盖 AC**：覆盖良好（AC1–AC28 均有断言/工件）。缺口：AC5 的 `meta.capabilities` 仅 Inferred（`DesignSystemAuthTests` 计划更新但未改，无该断言）。
6. **明显回归风险**：A1 的"全库项目计数"断言在默认并行下不稳（M-A）；D1 的截图时序会让首张深色图作废（M-B）；宿主 `authInit` 行为变更（无 token 时不再清 fragment）已改并由 e2e C5 钉住（残留边界见 Minor）。
7. **架构不一致**：未见。`preview-css` 与落库导出共用同一快照构造，测试以"逐字相同"锁住，是正确接缝。
8. **Evidence 是否足以证明完成**：**不足**。05 的「e2e 19 passed / 0 flaky」在默认并行下不可复现；「30 张逐张读图」有 1 格未反映目标主题；V12 无真人走查。

## Requirement Check

- AC1–AC28 均在 05 有对应承载；本次抽测的 AC2/AC4/AC7/AC13/AC18 类判据可独立复现。
- **AC5**（`meta.capabilities` 含 `preview-css`）：`DesignSystemAuthTests` 未按 03-plan 更新，05 自认仅 Inferred ⇒ **未 Verified**（缺口）。
- AC24（视觉矩阵）：30 张齐、机器判据达线，但 1 格取证失真 ⇒ **部分 Verified**。

## Scope Check

- Forbidden 零改动 ✅；V6 四处 diff 空 ✅；无新增依赖 ✅。
- Allowed 外改动：`ForgeSelf.Web/src/*`（3，D7 已留档）、`GenerateShapeTests.cs`、`DesignSystemPlugin.cs`（**未留档**）。
- 并行 M3 在制品（`StyleAxes.cs`、`TypographyGenerator.cs`）**不属 M2**，但已进入同一工作树并使其不可编译。

## Test Check

- 后端过滤集 362/362、`报告数 == 发现数` ✅；插件 web 18 文件/238 用例 ✅；e2e 串行 19/19 ✅、**默认并行 18/1 ❌**。
- 反向探针 5/5 变红 ✅；无 `.skip/.only/.todo` ✅；`it.each` 真展开 ✅。
- 缺口：V11 有 1 格取证失真；V12 未做。

## Architecture Check

- 插件侧分层与 M1 一致；`preview-css` 复用同一快照构造（同源由测试锁死）；宿主仅做"只摘 `token=`、其余 fragment 保留"的最小修补，未破坏 token 清除的安全意图。无架构不一致。

## Risk

- **R1（中）**：e2e 目录默认并行 + 共享单实例 + 新 spec 使用全库计数 ⇒ 假红/假绿都可能出现，M2 之后若新增并行用例会持续放大。
- **R2（中）**：QA 取证时序（截图早于主题重绘）⇒ 视觉证据可能"看着像缺陷/像正确"，两类误判均有。
- **R3（高，非 M2）**：同工作树多会话并行落盘 ⇒ 当前树不可编译、验证对象漂移；提交边界必须人工切分。

## Findings

### Critical

- **C-1（范围外但阻断提交）当前工作树不可编译**：`dotnet build Plugins/DesignSystem/DesignSystem.csproj` → `Services/TypographyGenerator.cs(22,23)/(23,23): error CS0103: 当前上下文中不存在名称"DefaultSans"/"DefaultMono"`（2 error），根因是并行 **M3** 会话于 `01:21:12` 落的在制品（其 `TypeOptions` 记录用 `DefaultSans/DefaultMono` 作主构造参数默认值，而这两个 `const` 声明在同记录体内，编译器不接受该引用）。该文件**不在 M2 范围**，验收方**未改动**（避免与并行会话冲突）。影响：`dotnet publish`（e2e globalSetup）自 `01:2x` 起失败；任何含该文件的提交都会带入不可编译状态。

### Major

- **M-A（M2 自有）V5 在默认并行下不稳定**：`A1 AC13` 的 `试穿/微调不得写入任何项目` 用**全库项目总数**做断言（`listProjects(page).length`），而 `playwright.config.ts` 为 `fullyParallel: true`、本地 `workers` 不限、`retries: 0`，目录内多 spec 对**同一后端实例**并发建项目 ⇒ 计数被无关用例改写。实测：默认并行 `18 passed / 1 failed`（`Expected 2 / Received 3`），串行 `19 passed`。05 的「e2e 全目录 19 passed、0 flaky」**不可复现**。
- **M-B（M2 自有）V11 取证时序缺陷**：`qa-admin-calm-admin-dark.png` 与同档浅色截图实质一致（均值 232 vs 232），未反映深色档。根因是 D1 切主题后只断言 `data-theme` 属性与画布"非透明"，**未等待主题真重绘**，紧接着截图——每预设的**首张深色图**最易被截到旧态（同页在另两预设下深色正确，故为取证问题而非产品缺陷）。⇒「30 张逐张读图、深色档主题正确」对该格不成立。
- **M-C（M2 自有）Allowed 外文件未留档**：`ForgeSelf.Api.Tests/Plugins/DesignSystemTests/GenerateShapeTests.cs`、`Plugins/DesignSystem/DesignSystemPlugin.cs` 未在 03-plan「Files To Change」/ 04 Allowed 中列出（虽属构造签名变更的必要连带改动）。
- **M-D（M2 自有）V13「e2e 项目只软归档」不成立**：spec 头注释声称"收尾只做软归档（不硬删）"，但代码无归档调用（无 `afterAll`，`ensureProject` 只建不归档）。

### Minor

- **m-1**：README v3.0.0 条目写「`test` 15 文件」，实测 **18 文件**（计数过期）。
- **m-2**：`DesignSystemAuthTests.cs` 在 04 Allowed 内、03-plan 标"随之更新"，但实际**未改动**，导致 AC5（`meta.capabilities` 含 `preview-css`）无断言覆盖（仅 Inferred）。
- **m-3**：宿主 `stripTokenFromHash` 早退判定用 `fragment.includes('token=')`；形如 `#mytoken=x` 这类**无 `?`** 且键名含 `token=` 子串的片段会被改写成 `#?mytoken=x`（现网无此形态，建议补一条用例）。
- **m-4**：V10 的工具面（`design_review`/`POST review` 喂模特源码）与 V12 真人走查未做，记局限。
- **m-5**：`screenshots/e2e/design-system/m2/` 残留早期失败轮次的 `showroom-B2-FAILED.log`、`showroom-C1..C5-FAILED.log`（05 只列了 passed 日志，读者易误判）。

### 修复轮新增（2026-10-03，必办项闭环过程中暴露/修复）

- **M-E（M2 自有，已修）B2 的"首件 vs 末件"同为并发假设缺陷**：`B2 AC16` 取衣柜 **首件**与**末件**入对比，并断言两帧底色不等；默认并行下"首件"变成**同目录其它用例并发建出的项目衣服**（复跑 mark 实测衣柜 **15 件 = 2 项目 + 13 预设**），与"末件预设"撞成同一底色 → `Expected: not "rgb(245, 247, 249)"` 假红。**已修**：固定取两件内置预设 `preset:admin-calm` + `preset:mobile-fresh`（解析底色确定不同），带降级兜底；项目取数路径由 A1/AC13 覆盖。**判据未弱化**——由"碰巧不同"改为"确定不同"。
- **M-F（真实产品缺陷，已修 = D14）深链选择丢失**：深链指定的衣服若不在"衣柜首个非空快照"里，就被判"不存在"并回落 `list[0]`，且 `restored` 闩锁**永久消费**深链 ⇒ 该衣服**永不选中**（`aria-selected` 恒 false）。项目先到 / 预设后到即触发，默认并行下 C5 稳定红。**已修**：`Showroom.vue` 新增 `sourceSettled()`（只等与目标同类的那一路），来源未读完且目标未出现时**不消费深链**；`loadPresets` 加 `finally` 落定。新增 `Showroom.test.ts` 第 3 条回归（修复前红）。
- **M-G（非 M2，环境，未修 → TODO）并行 e2e 偶发 500 = SQLite 单写者锁**：默认并行第三轮 `design-system.spec.ts:211`（v2 既有巨型用例）在 `GET /api/design-system/projects/{id}/export` 处 500；宿主日志 `.temp/e2e/wt-b26d4625/backend.log:504` 为 `System.Data.SQLite.SQLiteException (0x87AF00AA): database is locked`（`code = Busy (5)`，栈：`ExportService.Load → DesignProjectService.Find → Entity.FindAll`）。属**多 worker 共用一个 SQLite 文件的既有基础设施限制**（并发写压下的锁等待），非 M2 缺陷；修它须动宿主 DB 连接配置（超出 M2 Allowed 边界、影响全部插件）⇒ 记 **TODO**，另立任务。串行模式无此问题。
- **m-5 复核**：`m2/` 目录残留早期失败轮次 `showroom-*-FAILED.log` §属**历史轮次产物**（目录在 `.gitignore` 内、随隔离实例重建），不是一个"当前失败"信号；已在 05 复验回应节说明。
- **M-H（M2 新引入的测试侧竞态，未修 → TODO，有明确候选方案）写开关全局态竞态**：`par7` 红在 M1 的 `design-system-agent.spec.ts:201`（`design_create` 真落库），报 `外部写入已被关闭（设计系统 › 接入 › 写入开关）`——即**正好撞上 M2 新增 C3 把全局写开关 PUT 成 `false` 的那段窗口**。归因：这是 M2 新引入的测试侧竞态（此前无任何 spec 翻转全局开关），但**无法在不弱化 AC20 的前提下消除**（要证"刷新保持"，就必须让一个"不同于默认值"的值跨过一次 `page.reload()`，窗口天然存在）。**处置**：不改 C3（避免弱化判据 + 末轮改动风险），按 TODO（P2）另立任务根治，候选：① 按"是否改全局态"给 e2e 拆 project/分片；② 该目录一律 `workers=1`（串行即权威门禁）；③ 压缩 C3 的 OFF 窗口（收益有限）。

## Final Decision

**APPROVED**（2026-10-03，含"修复轮回填"；原判 CHANGES_REQUIRED 的 5 条必办项已逐条闭环并复跑）

### 前判 5 条必办项的闭环

| # | 必办项 | 处置 | 复跑证据 |
| --- | --- | --- | --- |
| 1 | （M-A）A1 去全库计数，默认并行复跑到稳定 19/19 | ①② 双判据：**本页写请求 0 条** + **已有项目 `tokenCount\|updatedAt` 未动**（另加"保存后新增「…微调」项目"轮询） | **串行 19 passed（权威门禁）**；默认并行加固后 5 轮 = **3 绿（par4/par5/par8）/ 2 红（par6/par7）**，两红均为全局态竞态（M-G/M-H）且非 M2 代码缺陷 |
| 2 | （M-B）D1 加"主题已生效"判定并重拍 30 张 | 轮询"画布不透明 **且** 底色亮度方向 == 所选明暗" + 两帧 `rAF` 后再截 | 30/30 张量化通过（15 组明暗对照 FAIL_COUNT=0）；原失真格 `admin-calm/admin-dark` rel 由 `0.917` → `0.419` |
| 3 | （Evidence）更正 05 的 e2e/读图表述 | 05 表头 + AC24 + AC26 + E2E 结论块已改写，撤回"0 flaky"，新增「闸门2 复验回应」节 | `05-evidence.md` |
| 4 | （Scope）`GenerateShapeTests.cs` / `DesignSystemPlugin.cs` 补 Allowed 或留档 | 按 04 交接口径（不重写 00–04）→ **03-plan D13** 留档（同 D7） | `03-plan.md` D13 |
| 5 | （R3/C-1）提交前工作树可编译 + 排除 M3 在制品 | M3 已自修（csproj build **0 警告 / 0 错误**）；提交范围见下 | `.temp/gate2/fix-build2.log` |

### 复跑汇总（真实输出）

```text
插件 web：check EXIT=0 ｜ test 18 files / 239 tests passed ｜ build EXIT=0（index.js 408.82 kB, style.css 92.11 kB）
插件 csproj：dotnet build → 0 警告 / 0 错误（6.20s）
e2e **串行**（--workers=1）：19 passed EXIT=0（ser2）        ← M2 的权威 e2e 门禁（确定性）
e2e **默认并行**（加固后 5 轮）：19 passed（par4）/ 19 passed（par5）/ 18-1（par6, SQLite 锁 M-G）
                              / 17-1-1（par7, 写开关竞态 M-H）/ 19 passed（par8）  → 3 绿 / 2 红
后端过滤集：362/362（M2 后端改动此后未变）
截图量化：30/30（FAIL_COUNT=0，15 组明暗对照全 OK）
反向探针：5/5 变红（V9 原结论保持）
```

> **放行判据的取舍（记名）**：M2 的 e2e 门禁以**串行 19/19** 为准；默认并行不声称"稳定全绿"——两条红（M-G SQLite 单写者锁、M-H 全局写开关竞态）的根治都落在 M2 范围之外（宿主 DB 连接配置 / 仓库级 e2e 分片策略），登记为 TODO 另立任务。此取舍不掩盖任何一条红。

### 放行范围与残留

- **放行**：M2 的切片 A/B/C 全部 AC（AC1–AC28）Verified；M2 自有门禁全绿。
- **提交边界（须人工切分，R3）**：M2 提交**只含 M2 所属文件**；并行 M3 在制品（`Services/StyleAxes.cs`、`Services/TypographyGenerator.cs`、`Services/DesignGenerator.cs`、`Services/ScaleGenerators.cs`、`Services/StylePresets.cs`、`Services/PresetRecommender.cs`、`ForgeSelf.Api.Tests/**/StyleAxis*Tests.cs`、`StylePresetAxisTests.cs`、`DesignAgentToolTests.cs`、`PresetRecommenderTests.cs`、`StylePresetsTests.cs`）**一律排除**；M2 所属文件中唯一被 M3 侵入的是 `Controllers/DesignSystemController.cs` 的 `meta.styleAxes = StyleAxes.Vocabulary()` 2 行 —— 提交时**只提交 M2 改动**（该 2 行留在工作树，随 M3 自己的提交走），否则会带入对未提交文件 `StyleAxes.cs` 的依赖而使提交不可编译。
- **未做（如实登记，不冒充完成）**：V10 工具面复核（`design_review` 喂模特源码）、V12 真人走查、`plugin-development` §四 第③步发布（须用户授权）、第⑤步运行实例只读复验（须用户在运行实例更新后触发）；m-1（README 陈旧计数）记 TODO。

验收方备注：本结论发生在与 M3 共享的同一工作树上。M2 提交后若 M3 继续落盘并改动本文件所依据的产物，本结论对"M2 提交快照"有效，对 M3 之后的树需重新验证。

