# Review

> 阶段：Stage 8｜Reviewer 视角重查 Intent → Spec → Plan → Task → Code → Test → Evidence 全链。
> 结论只报事实；CHANGES_REQUIRED 时须指明回退到哪个阶段。

> **状态：PENDING（预注册验收清单；尚未验收）**
> 本文件在**实现开始前**由规划/验收方写好考试范围。「审查八问 / 各项 Check / Findings / Final Decision」在实现完成并经**独立复验**之前一律不得填写；实现方**不得**修改本文件的结论栏。可**片级验收**（T-A / T-B / T-C 各自完成即可交验）。

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
