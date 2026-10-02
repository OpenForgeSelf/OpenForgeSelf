# Review

> 阶段：Stage 8｜Reviewer 视角重查 Intent → Spec → Plan → Task → Code → Test → Evidence 全链。
> 结论只报事实；CHANGES_REQUIRED 时须指明回退到哪个阶段。

> **状态：APPROVED（第二轮复验通过 · 2026-10-01）**
> 预注册原则不变：清单与判定规则在实现开始前写好（防看实现定标准），实现方不得修改本文件结论栏。
> 第一轮复验 = CHANGES_REQUIRED（3 项必改）→ 实现方修复 → **第二轮定向复验通过**（见「第二轮复验结果」）。
> 结论：**APPROVED**；另留 3 项文书引用小瑕（不阻断，见 Findings「Minor（第二轮）」）。

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

### 第一轮复验结果（2026-10-01，规划方独立执行；不采信 05 自述）

| 编号 | 结果             | 证据 / 备注                                                                                                                                                                                                                                                                                                         |
| ---- | ---------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| V0   | ✅                | 05 全部 AC 行附命令/日志；`git diff HEAD --name-only` 共 47 项 = 实现文件 + 工件（已被他人 `git add` 入 index；本会话不 commit）。计划外文件 2 处：`ForgeSelf.Api.Tests.csproj`（→ Major-3）、`DesignScanner.cs`（DesignReviewer 拆分件，认可）。                                                                   |
| V1   | ✅                | `TMP/TEMP=.temp/ds-m1/tmp` 存在；`chcp 65001`；后续命令全部在此环境执行。                                                                                                                                                                                                                                           |
| V2   | ✅                | `DesignSystem.csproj` / `AIAgent.csproj` 复跑构建：**0 警告 0 错误**。                                                                                                                                                                                                                                              |
| V3   | ✅                | 过滤集 **335/335**（6.17 分钟，`acc-full.log`）；`--list-tests` 发现数 **335** == 报告总数；与 05 数字一致。                                                                                                                                                                                                        |
| V4   | ✅                | `AIAgent\|McpCenter\|Sems` 回归 **215/215**（EXIT=0）；无红，无需对存量红表。                                                                                                                                                                                                                                       |
| V5   | ✅                | check EXIT=0 / vitest **75 passed（6 files）** / build 297.56kB+58.38kB；`Plugins/DesignSystem/web` git **零 diff**。                                                                                                                                                                                               |
| V6   | ✅（含环境修复）  | 首跑被「宿主签名」阻塞：Node 语境 `powershell.exe`（5.1）无 `Cert:`（实测 `drive=False certs=0`），`pwsh` 正常（`drive=True certs=1`）→ 经用户指示改 `e2e/global-setup.ts` 用 `pwsh` 并同步规范；重跑 **7 passed（3.4m）**（既有 UI 大用例 + 新 agent 6 用例）。                                                    |
| V7   | ⚠️                | `Data/`、`web/`、`ForgeSelf.Api`、`McpCenter` 零 diff ✅；AIAgent 恰 2 文件 ✅；无 `UniversalTool` 注册 ✅；唯一计划外 = `ForgeSelf.Api.Tests.csproj`（经核实：`System.Data.SQLite 2.0.2` 原即传递依赖、同版本，本次仅 `ExcludeAssets="runtime"` 资产过滤并对准宿主 → 不构成"新增外部依赖"，但**未登记** → Major-3）。 |
| V8   | ✅（附 1 处订正） | 静态对表：8 工具 `properties` 键集与 §B 逐项一致（`DesignToolIndex.cs`）；§D 8 预设 id/名称/顺序与 `StylePresets.cs` 一致；§E5 关键子串全部命中（`DesignBriefBuilder.cs:367-386`）；`meta.capabilities` 含新增五项、`agentTools` == 8（**实测 22 项 ≠ 05 所述 24 项** → Major-2）。                                 |
| V9   | ⚠️                | 测试质量：无 `Assert.True(true)`、无 Skip、无空测试 ✅；检查 reviewer 语料——`rgb()` 空格语法、Vue `<style scoped lang="scss">`、TSX `styled` 模板、tsx 行号偏移均已有正式用例，无需另做手喂探针；**但 05 两处数字/表述不实（AC16 软归档、AC24 计数）→ Major-2**。                                                    |
| V10  | ✅                | `design-system-consume` 已建并登记 AGENTS §2.4:111；`design-system-verify` 增补 34–40；README v2.8.0；036 含 Agent 工具层章节；ROADMAP P1.21。                                                                                                                                                                      |
| V11  | ✅                | 控制器**无任何 `HttpDelete`**；写工具动作仅 set_token/regenerate/publish（无删除）；agent-access 只写不删；e2e 全程在隔离实例、无物理删除。                                                                                                                                                                         |
| V12  | ⚠️ 未做           | 未实测内置 agent 扩容后（+8 `design_*`，合计自身/记忆/设计三插件）的 prompt 预算，无降级预案 → 记入 07 风险 + TODO。                                                                                                                                                                                                |
| V13  | ✅                | 打 tag 发布 / 页面自动更新 / 运行实例只读复验均如实列为"待用户授权"；HEAD 无本批提交（改动停在 index/工作区）。                                                                                                                                                                                                     |

> 附带核查：`e2e/global-setup.ts` 的 `powershell.exe→pwsh` 修改为**验收期间按用户指示**执行的基础设施修复（不属实现方范围）；规范同步见 `docs/04-standards/agent-workflow.md` B6/Part C 与 `e2e-testing` 技能。

### 第二轮复验结果（2026-10-01，修复批次定向复验）

> 预注册复验方式：`QuickCreateServiceTests` 定向 + 全过滤集（总数 == 发现数）+ 05/03 文本核对。以下全部为规划方独立复跑。

| 项                           | 结果 | 证据                                                                                                                                                                                                                                                                                                                                      |
| ---------------------------- | ---- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| ① 代码修复（Major-1）        | ✅    | 代码核对：`QuickCreateService.cs`——生成失败 try/catch → `_projects.Archive(project.Id)` + `error="生成失败，已将刚建的项目 {code} 归档（软删）：{原因}"`（§G 原文）；`AppliedResult.Warnings = BuildWarnings(outcome.Audit)`（`audit.Blocking` → 「审计存在 N 条 critical，发布前需处理」）；`request.Brief` 缺省 `description ?? name`。 |
| ② 测试                       | ✅    | `QuickCreateServiceTests` 定向复跑 **15/15**（`acc2-quick.log`；含 §J 注入用例 `Create_生成阶段失败_项目软归档且错误含code`——未知主题注入 → 归档 + Status=Archived + error 含 code；及 `Create_审计有critical_项目保留且带warnings`、`Create_brief缺省_取description或name`、`BuildWarnings_审计不阻断_空列表`）。                        |
| ③ 全过滤集                   | ✅    | **339/339**（7.73 分钟，`acc2-full.log`）；`--list-tests` 发现数 **339** == 报告总数（335 + 新增 4）。                                                                                                                                                                                                                                    |
| ④ 05 订正（Major-2）         | ✅    | AC24 与行 43 均「**22 项（17 旧 + 5 新）**」；AC16 行改为「失败路径有软归档（生成异常 → 项目归档 + 错误含 code + warnings，注入用例验证）」；Known Limitations ② 更新；新增「验收第一轮修复」记录表。                                                                                                                                     |
| ⑤ 03 补登（Major-3/Minor-1） | ✅    | 偏差记录追加第 10 条（csproj 资产过滤：原即传递闭包同版本、无新增依赖、与宿主同构）+ 第 11 条（AllocateCode 回退，改善型）。                                                                                                                                                                                                              |
| ⑥ 范围                       | ✅    | `Data/`、`web/`、`ForgeSelf.Api`、`McpCenter` 零 diff；AIAgent 仍恰 2 文件；修复批次仅动 `QuickCreateService.cs` + 其测试 + 05/03 文本。                                                                                                                                                                                                  |

**结论：三项必改全部落实且经独立复跑确认 → 通过（APPROVED）。**

遗留文书引用小瑕（**不阻断**；建议实现方在闸门3 提交前顺手订正，或经用户同意照现状封存）：
1. 05「Plan 偏差汇总」仍写「9 条」且仅列 8 项——现应为 11 条（补列第 10/11 条）。
2. 05 Known Limitations ② 引用「03-plan 偏差记录 09」——AllocateCode 实为第 11 条（行 463）。
3. 03-plan §H `PUT agent-access` 请求体写 `{allowWrite}`，实现/测试/e2e/`design-system-consume` 技能一致为 `{enabled}`（消费侧真源已一致）——建议补一条偏差登记或把 §H 描述改为 `enabled`。

> 判定记录：第 3 条为第二轮复验新发现；按「不影响功能、不影响消费契约、纯文书/登记级别」判定不阻断——与第一轮判 CHANGES_REQUIRED 的依据（伪 Verified 的 Spec 行为、共享基建未登记变更）性质不同。

## 审查八问（逐项回答）

### 八问回答（第一轮）

1. **实现是否真正满足 Intent？** 大体满足：8 工具 / REST 对等 / 写开关 / 审查 / 预设 / brief+agent-rules / 白名单全部落地并经真实宿主验证；**缺口 = "快速创建失败软归档 + 审计 blocking 警告"（FR12/AC16 明文，未实现）**，修复后即全满足。
2. **实现是否符合 Spec？** 除上述 FR12/AC16 缺口外一致；两条计划级小偏差（§G「brief 缺省」、「AllocateCode 全占用报错→hash 回退」）须补登记。
3. **是否超出了 Scope？** 计划外 2 处：`ForgeSelf.Api.Tests.csproj`（资产过滤修复；判定合理，须登记 → Major-3）+ `DesignScanner.cs`（审查器拆分件，认可）。
4. **是否修改了不应该修改的文件？** 否——`Model.xml`/`web/**`/`ForgeSelf.Api`/`McpCenter` 零 diff；AIAgent 恰白名单 2 文件；宿主未被触碰。
5. **测试是否覆盖 Acceptance Criteria？** 覆盖充分（335 含新增 127、AMS 215、web 75、e2e 7）；**缺口 = §J「生成失败注入→软归档」用例未写**（随 Major-1 补）。
6. **是否存在明显回归风险？** 低：`Generate` 形状锁死、存量 0 回归、web 零 diff；剩余观察项 = 内置 agent 工具面扩容的 prompt 预算（未实测，入 07 风险）。
7. **是否存在架构不一致？** 无：服务单例装配、`DesignToolKit` 单点、`meta.agentTools` 与 `DesignToolIndex` 同源、写开关 fail-closed，均符合既有架构与技能约束。
8. **Evidence 是否足以证明任务完成？** 绝大多数 Verified 且有日志；**两处不实（AC16 软归档、AC24 计数）**必须订正（→ Major-2）。

## Requirement Check

- **已达成**：工具层（8 个）/ REST（7 端点+meta）/ 写开关 / 审查引擎（15 规则）/ 预设与推荐 / brief+agent-rules 导出 / AIAgent 白名单 / 版本 2.8.0 / 文档技能——全部实测通过。
- **未达成（必改）**：FR12 / 数据安全规则 6 / AC16「生成失败时软归档已建项目」；连带 `warnings`（audit.blocking 提示）恒空；§J 对应注入用例缺失。
- **未达成（登记或一行小修）**：§G「brief 缺省取 `description ?? name`」未实现；§G「AllocateCode 全占用报错」被 hash 回退替代（改善型，登记即可）。

## Scope Check

- 合规：Forbidden 清单内文件零改动（Model.xml / web / 宿主 / McpCenter）；AIAgent 恰 2 文件；无新增 npm/NuGet 实体依赖（SQLite 包本在闭包、同版本）。
- 不合规（登记类）：`ForgeSelf.Api.Tests.csproj` 计划外变更未登记（技术判定：保留；补登 + 提交说明注明）。
- 会话外说明：`e2e/global-setup.ts` 的 pwsh 修复为**验收会话按用户指示**执行，不计入实现方范围（规范已同步）。

## Test Check

- 后端过滤集 335/335 且总数==发现数；AMS 回归 215/215；web 三件全绿；e2e 7/7（隔离实例、真实网关）。
- 测试质量：无永真断言 / 无 Skip / 无空测试；反例探针（reviewer 反例、写开关、干跑零写库）齐。
- 缺口：QuickCreate 失败注入用例（§J）→ 随 Major-1 补。

## Architecture Check

- 装配（`Apply` 手工 new + `AddSingleton(instance)`）与 03-plan §G 一致；工具/控制器共享同一批服务与 `DesignToolKit`。
- 同源：`meta.agentTools` ← `DesignToolIndex`；brief/review/导出共用 `ExportService.Load` + `TokenIndex`；审查建议与 `nearest` 共用 finder。
- 安全：`AgentAccess` 现读不缓存、损坏 fail-closed、原子替换；工具无读盘/执行/删除能力；`Guard` 统一 400。
- 无架构级发现。

## Risk

- **中**：内置 agent 工具面扩容（+8 `design_*`，合计三插件）对本地小模型 prompt 预算未实测、无降级预案 → 07 必列 + 已记 TODO。
- **低**：`design_create` 失败路径无软归档（Major-1 修复后消除）。
- **低**：csproj 资产过滤属共享测试基建（其它会话测试同样受益/受影响）→ 提交说明注明（登记后消除）。
- 既有（非本批）：宿主 SQLite BUSY、生成器主题覆盖缺陷（TODO P2）、bundle 下载路径未核验。

## Findings

### Critical

（无）

### Major

1. **FR12/AC16「生成失败软归档 + warnings」未实现**（回退：Code/Test）——`QuickCreateService.Create` 的 apply 分支无失败保护（生成失败不软归档、无含 code 的错误文案）；`AppliedResult.Warnings` 恒空（audit.blocking 时无「审计存在 N 条 critical，发布前需处理」）；§J 要求的失败注入用例缺失。**05 AC16 行却标 PASS/Verified 并写「失败路径有软归档」，与其自身 Known Limitations ③ 矛盾。**
2. **05-evidence 两处不实**（回退：Evidence）——① AC24 行与 Changed Files 段写「capabilities 24 项」，实测 **22 项**（17 旧 + 5 新；controller:71 与 HEAD 对表）；② AC16 行「失败路径有软归档」与实现相反（随 Major-1 修正后更新）。
3. **`ForgeSelf.Api.Tests.csproj` 计划外变更未登记**（回退：Plan 登记；无代码返工）——`System.Data.SQLite 2.0.2 ExcludeAssets="runtime"` 资产过滤（无新增外部依赖、与宿主同构、修复 Node 语境驱动混入引发的 98 例环境红）。须补登 03-plan 偏差记录 + 提交说明注明。

### Minor

1. **AllocateCode 耗尽回退未登记**（回退：Plan 登记）——计划 §G 为"全部占用 → 错误"，实现为 `base-hash` 回退（确定性、更稳健；判定接受，须登记为改善型偏差）。
2. **§G「brief 缺省取 `description ?? name`」未实现**（回退：Code，一行）——随 Major-1 批次顺手实现。
3. **过程记录（非实现方缺陷）**：e2e 首跑被签名环境阻塞，修复为验收会话按用户指示执行；规范同步 `agent-workflow.md` B6/Part C + `e2e-testing` 技能。

**Minor（第二轮遗留，文书引用小瑕 · 不阻断 APPROVED）**：
1. 05「Plan 偏差汇总」仍写「9 条」且仅列 8 项——现应为 11 条（补列第 10/11 条）。
2. 05 Known Limitations ② 引用「03-plan 偏差记录 09」——AllocateCode 实为第 11 条（行 463）。
3. 03-plan §H `PUT agent-access` 请求体写 `{allowWrite}`，实现/测试/e2e/`design-system-consume` 技能一致为 `{enabled}`（消费侧真源已一致）——建议补一条偏差登记或把 §H 描述改为 `enabled`。

## Final Decision

**CHANGES_REQUIRED**（2026-10-01 · 第一轮）

修复批次（实现方执行，完成后由规划方定向复验）：

1. **Code/Test**：`QuickCreateService.cs` 补——① 失败软归档（`Archive` + 错误文案含 code，文案按 03-plan §G）；② `warnings`（audit.blocking → 「审计存在 N 条 critical，发布前需处理」）；③ brief 缺省 `description ?? name`。`QuickCreateServiceTests` 补对应用例（含 §J 注入用例）。
2. **Evidence**：05 订正——AC24「24 项」→「**22 项（17+5）**」（两处）；AC16 行与实现对齐（修复后为「有软归档」）。
3. **Plan**：03-plan 偏差记录补登 2 条——csproj 资产过滤（与宿主同构、无新增依赖）+ AllocateCode 回退（改善型）。

复验方式：`QuickCreateServiceTests` 定向 + 全过滤集（须仍 335+新增 == 发现数）+ 05/03 文本核对。通过后本决策更新为 **APPROVED**。

---

### 第二轮复验（2026-10-01 · 修复批次）→ Final Decision：APPROVED

- 三项必改全部落实且经独立复跑确认：**定向 15/15**、**全量 339/339（发现数 339 == 报告总数）**、05/03 文本订正齐全、范围零违规（详见「第二轮复验结果」）。
- 附 3 项文书引用小瑕（不阻断；见 Findings「Minor（第二轮）」）。
- 依据：AC1–AC28 维持 Verified（AC16 已随修复转真）；V7 范围内合规（csproj 已登记）；两轮发现项全部闭环或降为文书级。
