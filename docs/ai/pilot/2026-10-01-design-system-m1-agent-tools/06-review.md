# Review

> 阶段：Stage 8｜Reviewer 视角重查 Intent → Spec → Plan → Task → Code → Test → Evidence 全链。
> 结论只报事实；CHANGES_REQUIRED 时须指明回退到哪个阶段。

> **状态：PENDING（预注册验收清单；尚未验收）**
> 本文件在**实现开始前**由规划/验收方写好考试范围（防止看了实现再定标准）。「审查八问 / 各项 Check / Findings / Final Decision」
> 在实现完成并经**独立复验**之前一律不得填写；实现方**不得**修改本文件的结论栏。

## 验收清单（预注册）— 规划/验收方独立复跑，不采信 05-evidence 自述

| 编号 | 动作                                                                                                                                                                                                                                                                                                            | 通过判据                                                                                                                                                               |
| ---- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| V0   | 读 05-evidence，列出所有声称 Verified 的条目；`git status` / `git diff --stat` 清点改动                                                                                                                                                                                                                         | 每个 Verified 都附真实输出；改动文件集合 ⊆ 04-task「Expected Files」，多出的有解释；Forbidden 文件零改动                                                               |
| V1   | 环境：`$env:TMP=$env:TEMP='D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\.temp\ds-m1\tmp'`；`chcp 65001`；UTF-8 输出                                                                                                                                                                                               | 目录存在；之后所有测试命令都在此环境下跑                                                                                                                               |
| V2   | `dotnet build Plugins/DesignSystem/DesignSystem.csproj`；`dotnet build Plugins/AIAgent/AIAgent.csproj`                                                                                                                                                                                                          | 0 error；新增代码 0 warning（与基线 warning 数对比）                                                                                                                   |
| V3   | `dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~DesignSystem" --logger "console;verbosity=normal"`，再 `--no-build … --list-tests`                                                                                                                                                                | 失败 0；**报告总数 == 发现数**；总数 ≥ 208 + §J 要求的新增用例；与 05 报告的数一致                                                                                     |
| V4   | `--filter "FullyQualifiedName~AIAgent\|FullyQualifiedName~McpCenter\|FullyQualifiedName~Sems"`                                                                                                                                                                                                                  | 红项逐条对表存量基线（TODO.md / 项目记忆里 mcp-center 的已知红）；**新增的红才算实现方的**                                                                             |
| V5   | `cd Plugins/DesignSystem/web && pnpm run check && pnpm run test && pnpm run build`；`git status --short Plugins/DesignSystem/web`                                                                                                                                                                               | 三件全绿；`Plugins/DesignSystem/web` 在 git 里**零 diff**（`web/dist/` 已被 `.gitignore:30` 忽略，构建不产生 git 变更）                                                |
| V6   | `cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/design-system/design-system-agent.spec.ts`，再跑 `e2e/plugins/design-system` 回归                                                                                                                                      | 新 spec 全绿且 6 个断言块都在；既有用例无新增红；环境不可行 → 标 Unknown，要求替代证据（进程内网关往返测试）并告知用户                                                 |
| V7   | 范围：`git diff --stat -- Plugins/DesignSystem/Data/Model.xml Plugins/DesignSystem/web ForgeSelf.Api Plugins/McpCenter`；`git diff --stat -- Plugins/AIAgent`；csproj / package.json / 锁文件                                                                                                                   | 前者**为空**；AIAgent 仅 `AIAgentService.cs` + `plugin.json`；无新增 NuGet/npm 依赖；未恢复 `UniversalTool` 注册                                                       |
| V8   | 契约对表：运行态取 `GET api/design-system/agent/tools` 的 `parametersSchema`，逐工具核对 `properties` 键集 == 03-plan §B；预设 id 集合 == §D；`agent-rules` 含 §E5 全部子串；`meta.capabilities` 含五项、`meta.agentTools` 恰 8 个                                                                              | 全部一致（记录比对结果；这是人工对表，不是一次性脚本结论）                                                                                                             |
| V9   | **抽测与反作弊**（至少 5 条 AC 不看 Evidence 独立复现，建议：AC6 同源、AC9 自产 CSS、AC10 任一规则反例、AC16 干跑零写库、AC18 写开关）；对 `DesignReviewer` 手工喂 3 个**未入测试**的输入（含 `rgb(124 58 237)` 空格语法、Vue SFC 的 scoped style + 模板内联、TSX `styled` 模板）；再加 3 条合法写法确认 0 报警 | 行号/规则/建议符合 §C；合法写法 0 报警；检查测试质量：无永真断言（`Assert.True(true)`）、无空测试、无被 `Skip` 的 `[Fact]`、新增用例清单覆盖 §J；05 里的数字与实际一致 |
| V10  | 文档与技能（AC27）：`.agents/skills/design-system-consume/SKILL.md` 存在且登记 AGENTS §2.4；`design-system-verify` 增补；README / ROADMAP / `docs/02-features/036-design-system.md` 事实同步（版本、工具、导出格式、REST、边界）                                                                                | 逐项存在且与实现一致                                                                                                                                                   |
| V11  | 数据安全：工具与 REST 无物理删除；`QuickCreate` 失败软归档；`agent-access.json` 无删除能力；e2e 收尾只软归档                                                                                                                                                                                                    | 全部成立                                                                                                                                                               |
| V12  | 风险项：内置 agent 一次挂 22 个工具（既有 14 + 新 8）对小模型 prompt 预算是 Unknown——看实现方是做了实测还是明确写了降级预案（默认只挂 4 个只读工具）                                                                                                                                                            | 二选一有证据；未做则列入 07 风险                                                                                                                                       |
| V13  | 五步闭环里**规划方不可代做**的项（打 tag 发布 / 用户页面自动更新 / 运行实例只读复验）是否被如实列为"待用户授权"，而非被宣称已完成                                                                                                                                                                               | 如实列出                                                                                                                                                               |

### Final Decision 判定规则（预注册）

- **APPROVED**：AC1–AC28 全部 Verified（e2e 因环境不可行的须标 Unknown 且有替代证据并告知用户）；V7 零违规；V9 无发现；基线红与新增红已区分。
- **CHANGES_REQUIRED**：存在可修缺陷；必须指明回退阶段（Spec / Plan / Task / Code / Test / Evidence）与具体条目。
- **BLOCKED**：环境、授权或并行会话冲突导致无法验证；写明阻塞原因与升级对象。

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
