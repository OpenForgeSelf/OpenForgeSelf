# Agent Task

> 阶段：Stage 4｜Agent 可直接执行的工作单元，零自我决策空间。
> 前序工件：00–03 齐备。**闸门1 依据**：用户于 2026-09-30 计划轮选定「推进顺序 A / 内置 agent A / DesignGuideline 新表 A（M3）/ 展厅五类场景」，并于 2026-10-01 在审阅计划后下达「Start implementation」；本任务书即该计划的 M1 切片。若用户对本工件有异议，回炉到 Stage 1–3 修订。

## 交接说明（实现方必读）

> 本里程碑由「规划/验收方」与「实现方」分工完成：规划方（本目录 00–04 的作者）**不写业务代码**；实现方独立实现并自证；规划方事后**独立复验**并签 06/07。目录内工件已齐备到可直接开工，**不要重写 00–04**。

1. **闸门状态**：闸门1 ✅（用户 2026-10-01 输入56「Start implementation」，计划已批；2026-10-01 输入58 要求规划细化后交他人实现）。实现中发现与仓库现状不符 → 先记入 03-plan 末尾「Plan 偏差记录」再改，不要静默偏离。
2. **读序**：`AGENTS.md`（§0 / §5.6 / §10 / §11）→ `docs/04-standards/ai-native-engineering-workflow.md` → 本目录 01 → 02 → **03（重点 §A–§J，是唯一设计依据）** → `.agents/skills/design-system-verify/SKILL.md`（自查表 #16 #22 #24 #27）→ `.agents/skills/plugin-development/SKILL.md` → `.agents/skills/e2e-testing/SKILL.md` → 范例 `Plugins/Sems/ToolExtensions.cs`、`ForgeSelf.Api.Tests/Plugins/Sems/SemsToolExtensionTests.cs`。
3. **环境约束（实测，必守）**
   - 跑任何 `dotnet test` 前先 `$env:TMP='D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\.temp\ds-m1\tmp'; $env:TEMP=$env:TMP`：系统 Temp 对测试主机拒绝访问，208 例里 101 例会假红（`UnauthorizedAccessException`）。`.temp/` 已 gitignore、目录已存在。**不得**为此改测试夹具。
   - 终端先 `chcp 65001; [Console]::OutputEncoding=[System.Text.Encoding]::UTF8`；必须带 `--logger "console;verbosity=normal"`（quiet 模式测试主机崩溃会假绿）；**报告总数必须 == `--list-tests` 发现数**（基线 208，随新增用例增长）。
   - `--no-build` 的测试运行期间不要重新编译；单个 `dotnet test` 全跑 DesignSystem 过滤集约 5 分钟。
   - **并行会话**：仓库里同时有另一个 AI 会话在改宿主（plugin-dev-experience：`ForgeSelf.Api/**`、`scripts/`、`TODO.md`、日记）。共享文件（`AGENTS.md`、`TODO.md`、`.forgeself/memory/*.md`）只做**局部精确替换**且改前重读最新内容；`git status` 里不属于本任务的改动一律不碰、不 stash、不 checkout、不 reset；构建失败先判断是否对方改动引入（记入 Evidence，**不要顺手修宿主**）。
   - 禁止停/启/杀用户运行中的宿主进程；未获授权不 `git commit / push / tag`、不打包发布。
4. **实现顺序**：TDD 先红后绿，按 03 的 Implementation Steps 2→14。**建议三个检查点**（可选，便于规划方分段复验）：CP1 = 步骤 2–8（服务层全绿，含 `GenerateShapeTests` 与存量 208 例 0 回归）；CP2 = 步骤 9–11（工具 + REST + AIAgent 白名单）；CP3 = 步骤 12–14（e2e + 文档技能 + 证据）。到点更新 05-evidence 对应节并在回复里给路径。
5. **证据纪律**：05-evidence 只记**真实发生**的命令与输出摘要（来源等级 Verified / Inferred / Unknown 不得混用）；AC→证据矩阵每行都要填；e2e 因环境不可行时写 Unknown + 原因 + 替代证据，**禁止**用期望值或推断充数；**06-review.md、07-final-report.md 的结论栏由规划方填写，实现方不要碰**。
6. **回报格式**（回复里必须全含）：① 状态（AGENTS §10.3 五选一）② 05-evidence.md 路径 ③ 实际跑过的命令与总数（后端过滤集 / AIAgent·McpCenter·Sems 回归 / web 三件 / e2e）④ 偏差记录条数与位置 ⑤ 未完成或未验证项（诚实标 Unknown）⑥ 需要用户授权才能做的动作清单（tag 发布、页面自动更新、运行实例只读复验——**这些不是你的动作**，列出即可）。
7. **范围纪律**：发现无关问题 → 记 `TODO.md`（P1>P2>P3，来源:输入56），本次不顺手修；不得改 Forbidden 清单内的文件；收尾自查 `git diff --stat`（见下方 Verification Commands 末两行）。
8. **失败处理**：同一问题连续 3 次失败无新思路 → 回滚到最近绿态并升级给人（写入 05-evidence「阻塞」节）；出现非预期副作用立即回滚。
9. **规划方将如何验收**：见 `06-review.md`「验收清单（预注册）」——逐项独立复跑命令、抽查 AC、对表工具 schema、查"永真断言/被跳过用例/测试数 ≠ 发现数"等作弊形状、复算同源判据。开工前先读一遍，等于知道考试范围。

## Task ID

PILOT-ds-m1-agent-tools

## Objective

`design-system` 插件 v2.8.0：向宿主工具注册表暴露 8 个 `design_*` 工具（外部经 McpCenter 网关、内置 AIAgent 均可用），配套 brief/review/presets/quick-create REST 与 `brief`/`agent-rules` 导出格式、写开关、AIAgent 白名单；DesignSystem 后端过滤集全绿且报告总数 == 发现数，新 e2e 在真实宿主直连网关通过。

## Scope

### Allowed

- 03-plan.md「Files To Change」列出的全部文件（新增/修改）。
- `.temp/ds-m1/**`（日志、TMP 工作目录，已 gitignore）。
- 工件：`docs/ai/pilot/2026-10-01-design-system-m1-agent-tools/**`；日志 `.forgeself/memory/2026-10-01.md`；`TODO.md`。

### Forbidden

- 改数据库结构：`Plugins/DesignSystem/Data/Model.xml` 与 `Data/Entities/**`（`DesignGuideline` 属 M3，须 M3 闸门1 单独批）。
- 改 `Plugins/DesignSystem/web/**`（M1 前端零改动）、`ForgeSelf.Api/**`（宿主）、`Plugins/McpCenter/**`、其他插件（AIAgent 白名单与版本号除外）。
- 恢复/注册 `UniversalTool`；扩大内置 agent 可达的其他插件工具。
- 新 NuGet/npm 依赖；构建配置/CI workflow 变更；无关重构；改测试夹具去"适配"环境问题。
- 任何删除库文件/数据目录/用户数据的自动化；`git commit` / `git push` / 打 tag（未获授权）；停/启/杀用户运行中的宿主进程。
- 在工具里提供读服务器磁盘、执行传入代码、物理删除的能力。
- 用一次性临时脚本作验证结论（AGENTS §0 红线）。

## Acceptance Criteria

- [ ] AC1–AC4：工具集精确 8 个、契约/注册/网关往返/`design_guide`
- [ ] AC5–AC8：`design_context`（章节/预算/JSON/hash/同源）、`design_lookup`（含 nearest）
- [ ] AC9–AC13：审查引擎（零假警报、反例必响、建议正确、上限、清单）
- [ ] AC14–AC19：`design_audit`、预设与推荐、`design_create`/`design_edit`、写开关、异常与体积
- [ ] AC20–AC22：REST 对等与鉴权、导出格式与 bundle、`Generate` 形状不变且存量 0 回归
- [ ] AC23–AC25：AIAgent 白名单、版本与 meta、前端/库结构/宿主零 diff
- [ ] AC26：新 e2e 直连网关通过（或如实标注 Unknown 及原因）
- [ ] AC27–AC28：文档/技能登记；Build/过滤集（总数==发现数）/插件 web 三件/AIAgent 相关测试全绿

## Expected Files

- 新增：`Plugins/DesignSystem/Agent/{DesignToolKit,DesignToolBase,DesignToolIndex,DesignReadTools,DesignReviewTool,DesignWriteTools}.cs`
- 新增：`Plugins/DesignSystem/Services/{GenerationService,AgentAccess,TokenIndex,NearestTokenFinder,DesignBriefBuilder,AgentRulesBuilder,DesignReviewer,DesignReviewService,StylePresets,QuickCreateService}.cs`
- 改：`Plugins/DesignSystem/{DesignSystemPlugin.cs,plugin.json,README.md,ROADMAP.md}`、`Controllers/DesignSystemController.cs`、`Services/{ExportService,DesignGenerator,DesignSystemConstants}.cs`
- 改：`Plugins/AIAgent/Services/AIAgentService.cs`、`Plugins/AIAgent/plugin.json`
- 新增测试：`ForgeSelf.Api.Tests/Plugins/DesignSystemTests/{DesignAgentToolTests,McpGatewayDesignToolsTests,DesignBriefTests,NearestTokenFinderTests,DesignReviewerTests,PresetTests,QuickCreateTests,AgentAccessTests}.cs`、`ForgeSelf.Api.Tests/Plugins/AIAgent/AIAgentToolScopeTests.cs`；改 `ExportProjectionTests.cs`
- 新增 e2e：`ForgeSelf.Web/e2e/plugins/design-system/design-system-agent.spec.ts`
- 文档/技能：`.agents/skills/design-system-consume/SKILL.md`（新）、`.agents/skills/design-system-verify/SKILL.md`、`AGENTS.md`、`docs/02-features/036-design-system.md`
- 工件：`docs/ai/pilot/2026-10-01-design-system-m1-agent-tools/00–07`

## 技能增补建议（`design-system-verify` 自查表续号，写"历史坑"式一句话 + 判据；实现方可据实际踩坑增删）

1. **Agent 通道必须与 REST/导出同源**：工具、REST、导出调用同一服务函数；判据 = 同一输入下工具返回与 REST 关键字段逐字段一致，说明书里的颜色/变量名 == `tokens/effective`。
2. **审查类判据用"自产语料零误报 + 每条规则反例必响"双向验证**：本插件自己导出的 CSS（全主题）必须 0 命中，每条规则至少一个反例必触发（只测其一都会骗人）。
3. **Agent 写能力必须有开关、默认值与降级**：关写后写工具被拒且文案指向开关路径；配置文件损坏→只读而不是可写；PUT 立即生效、新实例保持。
4. **干跑（`apply=false`）零写库**：前后项目数与 `DesignToken` 行数不变；不要只看返回值。
5. **工具数量与 prompt 预算是声明也是风险**：对内置 agent 的工具数量用真实 `ToolRegistry` 计数断言（≤ 8），并在文档写明降级预案。

## Verification Commands

```bash
# 环境：先把测试主机的 Temp 指到工作区（系统 Temp 被拒，见 03-plan 偏差记录）
$env:TMP='D:\src\my-proj\OpenForgeSelf\OpenForgeSelf\.temp\ds-m1\tmp'; $env:TEMP=$env:TMP

dotnet build Plugins/DesignSystem/DesignSystem.csproj
dotnet build Plugins/AIAgent/AIAgent.csproj
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~DesignSystem" --logger "console;verbosity=normal"
dotnet test ForgeSelf.Api.Tests --no-build --filter "FullyQualifiedName~DesignSystem" --list-tests
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~AIAgent|FullyQualifiedName~McpCenter|FullyQualifiedName~Sems" --logger "console;verbosity=normal"

cd Plugins/DesignSystem/web && pnpm run check && pnpm run test && pnpm run build
cd ForgeSelf.Web && pnpm exec playwright test --config=playwright.config.ts e2e/plugins/design-system/design-system-agent.spec.ts

powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-pilot-artifacts.ps1 -TaskId 2026-10-01-design-system-m1-agent-tools
git diff --stat -- Plugins/DesignSystem/web Plugins/DesignSystem/Data/Model.xml ForgeSelf.Api
```
