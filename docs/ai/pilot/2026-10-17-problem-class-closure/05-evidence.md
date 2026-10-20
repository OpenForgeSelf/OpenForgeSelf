# Evidence

> Stage 7｜只记录实际发生的事情。来源等级：Verified（亲跑拿到真实输出）/ Inferred / Unknown。

## Task

PILOT 2026-10-17-problem-class-closure（task_kind: infra，全量级）

## Changed Files（本批净变更，git status 实测核对）

**新增（未跟踪）**
- `.agents/skills/problem-class-closure/`：SKILL.md（86 行）+ references/01~08 + scripts/new-problem-case.ps1 —— ⚠️ `.gitignore:89` 的 `.*/` 规则将其忽略（`git check-ignore` 实测 `!!`），既有技能均为 `git add -f` 入库（`git ls-files .agents` 16 项），闸门2 提交时同样必须 `-f`
- `docs/18-templates/postmortem/`：problem-class.tpl.md（front-matter schema 单一来源）+ case.tpl.md（11 节）
- `docs/11-troubleshooting/problem-classes/`：README.md 台账 + PC-001~007 七档
- `docs/11-troubleshooting/cases/`：README.md 索引 + 4 case（2026-09-24 裸fetch / 2026-10-09 PluginDbs / 2026-10-10 契约未注册 / 2026-10-17 网关scope）
- `scripts/check-problem-classes.ps1`（G3 执行器 + 缺陷类 pilot 07 三节门禁；UTF-8 BOM 实测 True）
- `ForgeSelf.Api.Tests/ProblemClassRegistryTests.cs`（守卫的守卫，5 Fact）
- `ForgeSelf.Api.Tests/ProblemClassEnumerationTests.cs`（G2 类级枚举，3 Fact）
- `docs/ai/pilot/2026-10-17-problem-class-closure/00~07`

**修改**
- `AGENTS.md`：§0第2条同根因例外 / §0第3条 v1.2.0 / §0第6条问题类闭环门禁 / §2.4 技能登记
- `docs/04-standards/ai-native-engineering-workflow.md`：v1.2.0 七处（§1.5 无关重构澄清、Stage 0 适用问题类、Stage 2 Failure Modes、Stage 3 Test Plan 守卫、Stage 8 九问、§5 十三节）
- `docs/18-templates/ai-pilot/07-final-report.tpl.md`：§10/§11 → 问题类归档/重构提案/悬空教训清单（必须为空）
- `docs/README.md`：§一速查表 +「设计/修复前自查已知问题类」行、报错行改知识库入口表述
- `docs/11-troubleshooting/index.md`：重写为知识库入口（三层分工，每行指向 case/PC）
- `scripts/hooks/pre-commit`：DIRS 循环追加 `-PilotTask` 门禁；新增暂存触碰 `docs/11-troubleshooting/` 或扫描器自身时全量台账扫描
- `TODO.md`（gitignore，本地）：新增 P1「修复：Scheduler 卸载不停止调度器」

## Build

Command: `dotnet build ForgeSelf.slnx -c Release -v q --nologo`

Result: **PASS（Verified）** EXIT=0

```text
SLN-BUILD-EXIT=0（无 error 行）
```

**偏差披露**：默认 Debug 配置终验时被阻断——`ForgeSelf.Api\bin\Debug\...\ForgeSelf.dll` 被 12:35:48 新起的 dev 宿主（PID 130972，`--console --plugins-dir`，不在本会话 `.temp/dev-stack.json` 记录内，推断为并行会话/用户所起）锁定；按红线「禁止 agent 停/启/杀用户运行中的宿主」未处置。本会话在锁出现前已实测 Debug 配置 `dotnet build ForgeSelf.Api.Tests` 成功（BUILD-OK），最终测试验证走 Release 配置。

## Unit Test

Command: `dotnet test ForgeSelf.Api.Tests -c Release --no-build --filter "FullyQualifiedName~ProblemClass" --nologo`

Result: **PASS 8/8（Verified）** EXIT=0（Registry 5 + Enumeration 3 全绿）

Command: `dotnet test ... --filter "FullyQualifiedName~RepositoryScript"`

Result: **1 红（既有基线，非本批引入）**：`backfill-feature-ids.ps1` 缺 BOM（TODO.md 在案 P2）；本批两新脚本 `[IO.File]::ReadAllBytes` 实测 BOM=True。

## 守卫试跑（红绿往返）

- `pwsh scripts/check-problem-classes.ps1` → 7 PC 签名/allowlist/guards 全通过 EXIT=0（Verified）
- `-PilotTask 2026-10-17-problem-class-closure` → `task_kind≠defect，免三节` EXIT=0（infra 正确跳过，Verified）
- 红绿往返（临时 defect 目录，验证后已删）：缺三节 → `[pilot-gate] FAIL` EXIT=1；补三节 → OK EXIT=0（Verified）
- `bash -n scripts/hooks/pre-commit` → 语法 OK（Verified，Git bash）

## 首跑战果（本机制的价值证明，Verified）

PC-001 G2 枚举测试建立当天逮到**真实存量违例**：`SchedulerPlugin.StopScheduler` 经 `ctx.Get<ISchedulerHost>()` 停调度器，但该契约无任何 ctx.Register/seed 路径（`HostProvidedServiceContracts` 不含、插件只 `AddSingleton` 进自身 DI、`Context.Get` 无 DI 回落——源码实证 `Plugins/Scheduler/SchedulerPlugin.cs:183` + `ForgeSelf.Api/Plugins/PluginManager.cs:241-243`）→ 恒 null，卸载静默不停。处置：TODO P1 + PC-001 allowlist 文件级豁免（expires 2026-11-15），修复属插件六步闭环另批。

## Integration Test / E2E

Result: N/A（本批为流程基建 + 文档 + 守卫测试，无宿主运行时行为变更；Scheduler 缺陷修复批再补 e2e 证据）

## Static Analysis

Result: N/A（无前端源码改动）

## Screenshots

N/A（无 UI）

## AC 覆盖对照（02 §8 ↔ 实测证据；补齐于 2026-10-20 提交日，证据均为已真跑项的编号索引）

| AC | 判据 | 证据与结果 |
| --- | --- | --- |
| AC1 | 扫描器 exit 0 且逐 PC 计数非空转 | `pwsh scripts/check-problem-classes.ps1` 输出 10 行 `[scan] PC-0xx status=… 完成` + `EXIT=0`（建立日 2026-10-17 与提交日 2026-10-20 各实跑一次均绿） |
| AC2 | 阳性对照：植入临时违规变红、移除回绿 | 临时 defect pilot 目录 + 植入违规 PC 的红绿往返实测（本文扫描器职能节；验后即删无残留） |
| AC3 | build 0 错误 + `--filter ProblemClass` 全绿 | Release 全解 build 0 error；ProblemClassRegistryTests + ProblemClassEnumerationTests 共 8/8 PASS |
| AC4 | 首批 PC front-matter 完整、lens 非空、guards 有 active（或 none+TODO+expires） | PC-001~007 七档全部由扫描器逐字段校验通过（AC1 同源）；PC-004 none+TODO+expires 2026-12-31 在台账 |
| AC5 | cases 引用与两处 README 索引双向一致 | 扫描器对 `cases` 键验实体文件存在；`problem-classes/README.md` 与 `cases/README.md` 人工比对一致（提交日复核） |
| AC6 | AGENTS.md 恰三处改动 | `git diff AGENTS.md` 实测：§0 第 2 条例外句、§0 第 6 条门禁行、§2.4 技能表行（含选型顺序行自然附属），无其他膨胀（提交日 2026-10-20 复核） |
| AC7 | 规范 v1.2.0 七处落笔 | `docs/04-standards/ai-native-engineering-workflow.md` 版本头 v1.2.0 + §1 澄清/Stage 0/2/3 增补/九问第 9 问/§5 节名，`git diff` 14 行变更可对照 |
| AC8 | 07 模板三节替换且 verify 关键节兼容 | `docs/18-templates/ai-pilot/07-final-report.tpl.md` diff 实测 §10/§11 换三节；本任务自身 07 过 verify（Validation/Review 节仍在） |
| AC9 | pre-commit 台账扫描 + 缺陷类三节拒缺，红绿两路 | `bash -n` 语法绿；`-PilotTask` 直调红绿往返已实测；真实 hook 首过 = 本次提交（触碰 docs/11-troubleshooting/ 即触发全量扫描，2026-10-20 通过即终验） |
| AC10 | 零业务代码变更，变更集与 00 触点一致 | git status 实对：新增仅 docs/scripts/测试工程两文件/技能；`Plugins/**` 与 `ForgeSelf.Api/**` 生产源码零改动（TodoTracker 变更属并行 PILOT-062 批，非本批） |

## Known Limitations

- 修复期共 6 处首跑缺陷（`r"..."` Python 风格前缀、非 verbatim `"\b"`=退格符、相对路径按 bin CWD 解析、Moq `Match` 二义性、`[Globalization.DateTimeParseOptions]` 短命名空间不存在、pre-commit 误插空语句行）——全部由**真跑**暴露并修复，无一处靠推断放行；教训见 06-review。
- Debug 配置终验受阻（并行宿主锁），以 Release 等价验证 + Debug 阶段性成功记录。
- PC-001 枚举判据对「消费点全部位于 allowlist 文件」的契约直接豁免（文件级粒度），同文件新增其他未注册契约消费也会被豁免——靠 lens 问题 3 在 Review 兜底（档案 §2 盲区已披露）。

## Unresolved Issues

- 阶段四（结构性重构提案 `RegisterContract` 统一入口、大技能瘦身）按方案拍板缓行，PC-001 台账 G4（提案）行在案。
- Scheduler 修复（TODO P1）待下批 defect 任务走 problem-class-closure 全流程 + plugin 六步闭环，届时补正式 case。
- 闸门2 待用户：git 提交（技能目录需 `git add -f`）。
