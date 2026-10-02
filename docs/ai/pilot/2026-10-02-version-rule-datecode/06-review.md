# Review

> 阶段：Stage 8｜Reviewer 视角重查 Intent → Spec → Plan → Task → Code → Test → Evidence 全链。
> 结论只报事实；CHANGES_REQUIRED 时须指明回退到哪个阶段。

## 审查八问（逐项回答）

1. **实现是否真正满足 Intent？** 是。发行串改为「`<major>.<minor>.<patch>.<yyMMddHHmm>`」后，tag / `versions/<ver>/` / `versions/current` / zip 名 / `RELEASE-NOTES` / 两个 exe 的文件版本**实测同一串**（`2.3.0.2610021711`，见 05-evidence 同串核对表）；旧形态（`2.2.2026.x`、`2.2.11`）仍能收到新规则版本的更新（世代兜底比较 + 7 条单测 + 6 条旧形态回归用例）。
2. **实现是否符合 Spec？** 是。发行串形态、生成点唯一（`release-local.ps1`）、比较规则（第 4 段 > 65535 ⇒ 新规则世代）、注入条件（`^\d+\.\d+\.\d+(\.\d+)?$` 才注入）、文档同步（真源 §1.1 + 新增 §4-R10 + AGENTS §2.3）逐条落位。**通道实现偏离一处并已记录**：Spec 原定 `-p:Version` 注入，实测不可行（NU1105 / NETSDK1018，见 05「Rejected Approaches」），改为环境变量 `FORGESELF_RELEASE_VERSION`，**语义不变（同串、不外溢）**，已在 csproj 注释与本文件留痕。
3. **是否超出了 Scope？** 基本没有。Minor：`AGENTS.md` 新增条目同段落的两处 `<VT>ersions` 文本损坏顺手修正（同段必要修正，非越界改动）；`04-task.md` 两处由本任务回写脚本造成的文本损坏（「判为」/「判判定」）已在本任务内修复。
4. **是否修改了不应该修改的文件？** 否。未触碰前端源码与 `e2e/**`、未碰 `update-agent.ps1` / `sign-publish.ps1` / `package-release.ps1` / `make-release-notes.ps1`、未碰 `Plugins/DesignSystem/**`（并行 M2 任务在飞文件）、未碰任何运行实例目录。
5. **测试是否覆盖 Acceptance Criteria？** 覆盖（逐条见 05-evidence）：AC4/AC5/AC6 = 单测（50/50 绿，含 7 条新用例）；AC2/AC3/AC7/AC9/AC10 = 完整发布链实测 + 三处 exe 版本核对 + `new-version.ps1` 五种输入；AC1 = 真源文档与 AGENTS 实证；AC8 = 全仓扫描 113 命中并分级（新文本 / 历史记录 / 活队列）。
6. **是否存在明显回归风险？** 剩余风险受控但存在（L2）：① 旧规则安装实例（`2.2.2026.x`、`2.2.11`）的**真机升级链路**尚未验证——运行实例 `D:\src\tools\ForgeSelf`（`:51888`）仍停在 `versions/current = 2.2.11`，AGENTS 铁律禁止 agent 停/启/杀宿主，需用户升级后再做只读复验；② PE 文件版本数值字段被截断（已知、有意识，字符串字段完整）；③ 深档全量 e2e 未跑（未触碰 e2e 共享基建，按 §5.6 触发条件不满足）。
7. **是否存在架构不一致？** 无。版本规则属仓库级发行规则，落真源 `docs/04-standards/packaging-upgrade-backup.md §4-R10`（未落技能），与既有「打包/升级/备份真源唯一」的架构一致；csproj 兜底 + 环境变量注入的边界等于「宿主自身 = 两个 exe 项目」，不外溢到库项目。
8. **Evidence 是否足以证明任务完成？** 是。中档门禁（后端全量）实跑并完成归属：修复后 **16 红 / 2137 通过 / 总计 2153**，16 红**全部**为本任务外（存量基线家族 7 = `WorkflowPlanningIntegrationTests`×6 + `ScriptRunnerDiIntegrationTests`×1；环境类 8 = `UpdateServiceTests.ApplyUpdateAsync_*`，已用「旧规则版本串重跑同样红」证明无因果；并行在飞任务 1 = `DesignSystemTests.DesignAgentToolContractTests.ExecuteAsync_空参数_不抛`）；本任务引入的 1 条（`new-version.ps1` 缺 UTF-8 BOM）已修复并在复跑中消失；前端单测 688/688 绿、`pnpm run check` 唯一红项归属并行任务（文件交集为空）。

## Requirement Check

PASS

## Scope Check

PASS（Minor 备注见八问第 3 条）

## Test Check

PASS（中档后端全量 + 单测定点 + 发布链端到端；深档 e2e 未触发，理由已记录）

## Architecture Check

PASS

## Risk

L2

（理由：改动面 = 版本字符串与发布脚本，属公共发行链；已实测同串与外溢边界；剩余风险集中在「旧规则实例真机升级」与 PE 数值截断，均已在 05 显式列为 Known Limitations）

## Findings

### Critical

无

### Major

1. **旧规则安装实例的升级链路未做端到端验证**：`versions/current = 2.2.11` 的运行实例需要用户先升级（页面「自动更新」），之后才能只读复验「页面显示新串 / 自动更新到新规则版本」；在此之前该路径为 **Unknown**。
2. **前端 `pnpm run check` 红 1 项（非本任务）**：`e2e/plugins/design-system/design-system-agent.spec.ts(177,5) TS2322`，属并行在飞的 DesignSystem M2 任务；本任务零前端改动，但门禁因此不能报「前端全绿」。
3. **8 条 `UpdateServiceTests.ApplyUpdateAsync_*` 在隔离复跑中仍红**（非本任务引入，已用「旧规则版本串重跑同样红」实验证明无因果），但归属未定（环境类，既有测试套件红项）→ 应记 TODO 由测试 owner 跟进。

### Minor

1. `AGENTS.md` 同段落两处 `<VT>ersions` 文本损坏顺手修正（同段必要修正）。
2. `04-task.md` 两处回写脚本造成的文本损坏（「均为」→「判为」、「均判定」→「判判定」）已修。
3. 首次全量带出 1 条本任务引入的红（新建 `new-version.ps1` 缺 UTF-8 BOM，仓库守卫 `RepositoryScriptTests.ScriptsWithNonAscii_MustHaveUtf8Bom` 抓到）——**正是 AGENTS 预警过的历史坑**，已修并定点复验绿。
4. 本地 Release 构建偶发 `CS2012/MSB3021`（火绒锁定刚写出的 DLL），环境瞬时问题，重跑即过。

## Final Decision

APPROVED

<!-- 条件：Major 1（旧规则实例真机升级复验）需在用户升级运行实例后补做只读复验；本任务代码与工件按闸门2 交用户验收，验收通过前不提交 git。 -->
