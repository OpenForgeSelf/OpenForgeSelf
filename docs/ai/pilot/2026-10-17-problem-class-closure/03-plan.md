# Plan

> 阶段：Stage 3｜Task ID：2026-10-17-problem-class-closure
> 按方案「落地节奏」执行阶段一+二+三；阶段四（结构性重构提案、大技能瘦身）不在本批，理由：方案原文自标优先级另批处理。

## Files To Change

### 技能（新增）
- file: `.agents/skills/problem-class-closure/SKILL.md` — reason: 主技能 ≤120 行（按闸门1 方案正文草稿本地化）
- file: `.agents/skills/problem-class-closure/references/01-triage-and-root-cause.md` … `08-project-seeds.md`（8 份）— reason: 阶段细则；08 承载种子问题类与出处
- file: `.agents/skills/problem-class-closure/scripts/new-problem-case.ps1` — reason: 脚手架（UTF-8 BOM，pwsh）

### 知识库（新增/修改）
- file: `docs/11-troubleshooting/problem-classes/README.md` — reason: 台账（tags 检索表）
- file: `docs/11-troubleshooting/problem-classes/PC-001-cross-plugin-contract-shared-table.md` … `PC-006-*.md` — reason: 3 个种子完整 PC + 2 个范例归档 + 1 个候选（PC-004 状态回报≠进程终态，no-guard-yet + TODO + expires）
- file: `docs/11-troubleshooting/cases/README.md` + `cases/2026-10-10-workflow-engine-contract-not-registered.md`、`cases/2026-09-24-plugin-auth-frontend-bare-fetch.md`、`cases/2026-10-09-agenthub-db-unregistered-data-loss.md` — reason: PC-001/002/003 各配一条示范 case（从既有报告/troubleshooting 提炼，出处标 Verified/Inferred）
- file: `docs/11-troubleshooting/index.md` — reason: 升级为知识库入口：保留现象速查，每行加「归档」列指向 case/PC
- file: `docs/18-templates/postmortem/problem-class.tpl.md`、`case.tpl.md` — reason: FR3 模板

### 流程接入（修改）
- file: `AGENTS.md` — reason: §0 第 2 条例外句、§0 第 6 条出口行、§2.4 技能登记行（恰好三处，AC6）
- file: `docs/04-standards/ai-native-engineering-workflow.md` — reason: 升 v1.2.0（§1.5 澄清、Stage 0/2/3 增补、Stage 8 九问、§5 节名）
- file: `docs/18-templates/ai-pilot/07-final-report.tpl.md` — reason: §10/§11 → 问题类归档/重构提案/悬空教训清单
- file: `docs/README.md` — reason: §一 加「自查已知问题类」行、11/18 目录四问同步
- file: `scripts/hooks/pre-commit` — reason: 追加 `check-problem-classes.ps1` 调用（台账触碰 / pilot 暂存两类触发）

### 守卫（新增）
- file: `scripts/check-problem-classes.ps1` — reason: FR5 主脚本（signature 执行 + allowlist 到期 + -PilotTask 07 三节校验）
- file: `ForgeSelf.Api.Tests/ProblemClassRegistryTests.cs` — reason: 守卫的守卫
- file: `ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs` — reason: 三个类级枚举测试（PC-001/002/003）

**不改**：`scripts/verify-pilot-artifacts.ps1`（L4 宪法层）、`Plugins/**` 与 `ForgeSelf.Api/**` 生产源码、`ForgeSelf.Web/**`、任何用户运行实例。

## Implementation Steps

1. 本工件链 00-04（已完成即勾）。
2. 建 PC/case/模板/台账/索引全套文档（先定 schema，再写内容——脚本与测试按同一 schema 解析）。
3. 写技能 SKILL.md + 8 references + 脚手架脚本。
4. 写 `check-problem-classes.ps1` 并本机跑绿 + 阳性对照（AC2）。
5. 写 `ProblemClassRegistryTests.cs` + `ProblemClassEnumerationTests.cs`；枚举测试先跑一遍收集存量违规 → 合法存量进 baseline（带理由+到期日），非法存量记 TODO。
6. 接入 AGENTS.md / 规范 v1.2.0 / 07 模板 / docs/README / pre-commit。
7. Verify：`dotnet build` + `dotnet test --filter ProblemClass`（+全量回归不要求，基线红既有 2 条盘符大小写已记 TODO）；跑 pre-commit 脚本本体做暂存模拟。
8. 补 05-evidence / 06-review / 07-final-report；勾日记、清 TODO。

## Test Plan

（规范 v1.2.0 新要求：列出命中问题类对应的守卫——本任务自身命中 PC-001：新增枚举测试即其 G2 守卫落地动作）

1. `ProblemClassEnumerationTests.PluginContracts_ConsumedViaCtxGet_MustBeRegisteredSomewhere`（PC-001 G2）：扫 `Plugins/**/*.cs` 的 `ctx.Get<T>()` 消费点（T 属 Abstractions 接口），断言每个 T 在 Plugins/ForgeSelf.Api 出现过 `ctx.Register<T>/Register<T,U>/RegisterLocal<T>` 或宿主 seed 清单；阳性对照：消费集合非空。
2. `ProblemClassEnumerationTests.FrontendBareFetchToAuthedApi_MustUseAuthFetch`（PC-002 G3）：扫 `ForgeSelf.Web/src` + `Plugins/*/web/src` 的 `fetch('/api/…` 裸调用，baseline 外即红；阳性对照：authFetch.ts 自身与已改造 service 可被识别。
3. `ProblemClassEnumerationTests.PluginDalConnNames_MustBeRegisteredInPluginDbs`（PC-003 G2）：扫 `Plugins/**` 的 `DAL.Create("X")` 字面量与 `ConnName = "X"` 常量，断言 ⊆ `XCodeConfig.PluginDbs.Keys`；阳性对照：扫描命中 ≥8 个连接名。
4. `ProblemClassRegistryTests`：台账↔PC 文件双向一致；guard 路径存在或 none+TODO+expires；case 引用闭合；recurrences≥2 必须 G2+；docs/README 链接存在。
5. `check-problem-classes.ps1`：默认模式绿；植入违规红；`-PilotTask` 对本目录（无 defect 标）跳过、对模拟 defect 目录红/绿两路验证。

## Verification

### Build
```bash
dotnet build ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj
```
### Unit Test
```bash
dotnet test ForgeSelf.Api.Tests -f net10.0 --filter "FullyQualifiedName~ProblemClass"
```
### Integration Test
N/A——本任务不涉集成面（守卫测试为纯源码扫描）。

### E2E
N/A——无 UI/业务行为变更（方案阶段一~三不触前端运行态）。

### Other Checks
```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-problem-classes.ps1
pwsh -NoProfile -Command "& { $env:GIT_DIR=… }"  # pre-commit 模拟：sh scripts/hooks/pre-commit（带假暂存）红绿两路
```

## Plan 偏差记录

| 时间 | 偏差点 | 原 Plan（=闸门1 方案） | 修正后 |
| --- | --- | --- | --- |
| 2026-10-17 | 「pre-commit 工件门禁」实现载体 | 改 07 校验逻辑进工件门禁脚本 | `verify-pilot-artifacts.ps1` 头部自declare L4 宪法层 Agent 不得自改 → 07 三节校验移入新脚本 `check-problem-classes.ps1 -PilotTask`，pre-commit 追加调用；语义等价，载体分离 |
| 2026-10-17 | 阶段四 | — | 按方案原文缓行（大技能瘦身、结构性重构提案另批），记入 TODO 不阻塞 |
