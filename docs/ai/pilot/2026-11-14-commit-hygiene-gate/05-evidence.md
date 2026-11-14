# Evidence

> 阶段：Stage 7｜**只记录实际发生的事情**，不得根据代码推测测试结果。
> 每个验证项标注来源等级：Verified（亲自跑过，有真实输出）/ Inferred（凭代码推断）/ Unknown（未验证）。禁止混用。
> ⛔ 禁用表述：「应该可以」「理论上通过」「看起来没问题」「大概率是」「估计可以」。

## Task

2026-11-14-commit-hygiene-gate（提交内容卫生门禁；日志代号 PILOT-081）。
本批实现 §B13 提交内容卫生门禁：L1 四类绝对地址规则 + L2 本地拒绝列表，落为 `scripts/check-commit-hygiene.ps1`（-Staged/-All/-Files/-DiffFile 四口径 + -Json + -DenylistPath）+ pre-commit 第 4 道门 + 测试 6 例 + 登记五处 + 拒绝列表（本地私有，不入库）。

## Changed Files

按提交批记录（本批多批原子提交）：

**B1（已提交 `605d3bc`，pilot 工件先行）**
- `docs/ai/pilot/2026-11-14-commit-hygiene-gate/00-repository-understanding.md` ~ `04-task.md`（5 件，+243 行）

**B3（已提交 `88a583b`，文档批）**
- `docs/06-research/005-loopforge-feature-analysis.md`（+237，L3 已脱敏）
- `docs/06-research/006-loopforge-absorption-map.md`（+178）
- `docs/07-decisions/not-taken-decisions.md`（+11，059 LoopForge 条目）
- `docs/README.md`（索引 4 处）

**B2（本批主体，提交中）**
- `scripts/check-commit-hygiene.ps1`（新增，260 行，UTF-8 BOM）
- `scripts/hooks/pre-commit`（第 4 道卫生门）
- `ForgeSelf.Api.Tests/CommitHygieneGuardTests.cs`（新增，6 个 [Fact]）
- `docs/04-standards/agent-workflow/b-engineering-platform.md`（§B13 真源节）
- `docs/04-standards/agent-workflow/b-engineering-daily.md`（§B2 PILOT-076 同因第四面补种）
- `docs/04-standards/agent-workflow/README.md`
- `AGENTS.md`、`DESIGN.md`（绝对地址脱敏清理）
- `docs/07-decisions/not-taken-decisions.md`（060 三条不做）
- `.agents/skills/commit-hygiene-audit/SKILL.md`（新增）
- `docs/ai/pilot/2026-11-14-commit-hygiene-gate/05-evidence.md` ~ `07-final-report.md`（本三件）

**B4a（收尾批）**
- `.gitignore`（补 `.grok/` 子目录白名单：放行 agents/workflows/rules/personas/roles 五个子目录入库——原先 `.*/` 通配会把整个 `.grok` 吞进忽略、发现阶段跳过被忽略的规则；其余 `.grok` 内容仍忽略）
- `.grok/agents/ofs-explore.md`、`.grok/agents/ofs-review.md`、`.grok/rules/harness.md`、`.grok/workflows/ofs-review.rhai`（新增 4 件）

**不入库（如实记录）**
- `.forgeself/commit-hygiene.denylist.txt`（12 行，本地私有；按 §B13 设计即不提交，只以路径+行数引用）
- 本 05 所引原始日志均在 `.temp/tmp/`（`pilot-hygiene-*.log` / `pilot081-*.log`；`.temp` 已 gitignore 不入库，供现场复核）。根目录 `gate-logs/` 为另一会话的门禁日志目录，与本次无关，保持未跟踪

## Build

Command（脚本自身可解析 + BOM 检查，03-plan Build 关卡）：

```bash
pwsh -NoProfile -Command "$e=$null; [System.Management.Automation.Language.Parser]::ParseFile('scripts/check-commit-hygiene.ps1', [ref]$null, [ref]$e) | Out-Null; 'parse-errors=' + $e.Count"
```

Result:（来源等级：Verified）

```text
parse-errors=0
bom-bytes=239,187,191  （UTF-8 BOM 在位）
```

Hook 安装（幂等重装，保证第 4 道门在位）：

```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/install-git-hooks.ps1
```

Result: PASS（来源等级：Verified）——日志 `.temp/tmp/pilot-hygiene-installhooks.log`，「校验通过」，exit 0。

## Unit Test

**快档·过滤集（AC6 前半）**：

Command:

```bash
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~CommitHygieneGuard"
```

Result: PASS（来源等级：Verified）——日志 `.temp/tmp/pilot-hygiene-filteredtest.log`：

```text
已通过! - 失败的测试: 0，通过的测试: 6，总计: 6，持续时间: ~8 秒；exit 0
覆盖：AC1 四类绝对地址阳性集 / AC2 阴性集 0 命中 / AC3 DiffFile 行号=新侧行号 / AC4 拒绝列表子串+regex 命中与缺失跳过 / 模式互斥用法错 exit 2
```

**中档·全量（AC6 后半，本批碰了 `scripts/**` + 共享测试基建，按 AGENTS §5.6 必跑全量）**：

Command:

```bash
cd ForgeSelf.Api.Tests && dotnet build-server shutdown && dotnet test
```

Result: PASS-with-baseline（来源等级：Verified）——日志 `.temp/tmp/pilot081-fulltest2.log`（12:10–12:25:04，14m14s；首次 11:26 一轮因环境卡死废弃，未采信，见 Known Limitations）：

```text
失败的测试: 26，通过的测试: 3752，总计: 3778
红线归属：25 个为既有基线红（TODO.md L17/L185 台账在案，本批零触及）；1 个 ForgeConfigTests 为已登记的偶发红（同 TODO 台账）。
本批新增的 CommitHygieneGuard 6 例全绿。
```

## Integration Test

**真提交演练（AC5 后半，活体证据——B3 首次提交被第 4 道门真实拦截）**：

Result: PASS（来源等级：Verified）——事件链（记录于工作日记与 03-plan 偏差表）：

```text
1) git commit（B3 首次）→ pre-commit 第 4 道门拒绝，exit 1：
   [pre-commit] 校验提交内容卫生 (check-commit-hygiene)
   [hygiene] FAIL hygiene.abs-drive docs/06-research/005-loopforge-feature-analysis.md:3
   check-commit-hygiene: FAIL (files=4 lines=428 hits=1)
   （按 §B13 铁律，拒绝页不回显违规摘录原文入档，此处仅记规则 ID + file:line）
2) 脱敏该行（去除盘符路径字面量，改述来源）→ 重新暂存
3) git commit 重试 → 四道门全过，exit 0 → 落为 88a583b
   check-commit-hygiene: PASS (files=4 lines=428 hits=0)
```

pre-commit 第 4 道门文本断言（AC5 前半）由 `CommitHygieneGuardTests` 覆盖，6/6 绿（见 Unit Test）。

**空暂存演练（§8.5 / Test Plan 7）**：

```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/check-commit-hygiene.ps1 -Staged
```

Result:（来源等级：Verified）空暂存区：`check-commit-hygiene: PASS (files=0 lines=0 hits=0)`，exit 0（在主检出跑，拒绝列表在位无跳过注记）。真实提交演练（上方 B3 链）为更强证据，二者互证。

## E2E

Result: N/A（依据：本批零 UI 改动——守卫为 CLI 脚本 + pre-commit hook + 后端测试，02-spec 已声明无用户可见界面）。

## Static Analysis

**1) 设计门禁（快档必跑项）**：

```bash
cd ForgeSelf.Web && pnpm run design:check
```

Result: PASS（来源等级：Verified）——日志 `.temp/tmp/pilot-hygiene-designcheck.log`，exit 0：

```text
[style-tokens] 硬编码色值合计 1761（基线 2501）
[style-tokens] PASS：无新增硬编码色值，较基线减少 740 处
[design-compliance] a(token族)=209 b(胶囊)=0 c(玻璃过头)=0 d(蓝紫渐变)=0（与基线持平）
[design-compliance] PASS
``**2)DiffFile 本新增行扫描（AC9 棘轮口径，B2 前置自检）**：

Result: PASS（来源等级：Verified）——日志 `.temp/tmp/pilot-hygiene-diffscan.log`：

```text
check-commit-hygiene: PASS (files=8 lines=56 hits=0)；exit 0
```

**3) -Files 全批 23 件自扫（新文件全内容口径）**：

Result: PASS-with-stock-note（来源等级：Verified）——日志 `.temp/tmp/pilot-hygiene-selfscan.log`：

```text
check-commit-hygiene: FAIL (files=23 lines=4239 hits=10)
命中构成：全部落在 3 个含存量内容的改动文件之「既有行」（platform.md:118；daily.md:103,104,105,376；not-taken-decisions.md:287,383,384），其中 1 处为 L2 拒绝列表命中，均为入库多年的存量文本，不属本批新增；
17 件新增文件（守卫脚本、hook、测试、技能、pilot 00–04、研究文档 005/006、.grok 4 件等）全内容 0 命中。
与 -DiffFile 结果（新增行 0 命中）互证：本批新增内容零违规（AC9）。
```

**4) -All 全仓盘点（AC7，非阻断口径）**：

Result: 完成（预期 FAIL，盘点不阻断）（来源等级：Verified）——日志 `.temp/tmp/pilot-hygiene-allscan-main.log`：

```text
check-commit-hygiene: FAIL (files=3310 lines=535550 hits=717)；exit 1
按规则：fileurl 1 / drive 520 / posix 24 / unc 107 / denylist 65（合计 717）
精确 file 字段匹配：717 处中落在本批 3 个改动文件上的恰为上述存量 10 处（与 3) 逐一对应）；本批 20 件新增文件 0 命中；其余 707 处全在未触及的存量文件。
结论：本批新增/修改文件零新增违规（AC7）；存量底数 717 由棘轮口径隔离，不阻断提交（清理为 060② 不做项）。
```

**5) -All 在另一检出（worktree）上的对照观察**：

Result: 完成（来源等级：Verified）——日志 `.temp/tmp/pilot-hygiene-allscan.log`：`files=3304 lines=534741 hits=656`（fileurl 1 / drive 524 / posix 24 / unc 107 / denylist 0）。denylist=0 且带「拒绝列表文件不存在，L2 已跳过」注记——因该检出无本地拒绝列表文件。**双价值**：既是守卫按当前目录解析仓库根的运维注意点（见 Known Limitations），也是 AC4「缺失文件时 L2 跳过且有注明」的活体证据。

**6) 工件链机器校验**：

```bash
pwsh scripts/verify-pilot-artifacts.ps1 -TaskId 2026-11-14-commit-hygiene-gate -EnforceHeader -EnforceAcCoverage -EnforceInteraction
```

Result: PASS（来源等级：Verified）——见 07-final-report 收尾节记录的输出（00–07 八件 + 关键节齐备）。

## Screenshots

N/A（无 UI 改动）。

## Acceptance Criteria Mapping

| AC | 结论 | 证据（来源等级：除注明外均 Verified） |
| --- | --- | --- |
| AC1 -Files 阳性集四类各命中对应规则，exit 1 | 满足 | CommitHygieneGuard 测试① + B3 真拦截（abs-drive 命中） |
| AC2 -Files 阴性集 0 命中，exit 0 | 满足 | 测试② |
| AC3 -DiffFile 命中行号=diff 新侧行号 | 满足 | 测试③ |
| AC4 拒绝列表 re:+子串命中；缺失文件 L2 跳过且有注明 | 满足 | 测试④⑤ + 5) 中 worktree 对照运行的跳过注记（活体） |
| AC5 pre-commit 第 4 道门 FAIL 聚合拒绝（文本+真提交演练） | 满足 | 测试链 + Integration Test 三步真实事件链（拒绝→脱敏→重试通过） |
| AC6 过滤集全绿；全量中档仅基线红 | 满足 | 过滤 6/6 绿；全量 26/3778 全为台账在案的基线/偶发红，本批零触及 |
| AC7 -All 盘点本批文件零新增违规 | 满足 | 4) 精确匹配：本批文件命中恰为存量 10 处，新增行 0（-DiffFile 互证） |
| AC8 登记五处齐备 | 满足 | §B13（platform）/ AGENTS §2.3+§2.4+§5.6 / 技能 SKILL.md / not-taken 060 / AGENTS+DESIGN 清理行——均在本批 B2 变更清单内，落库后由 pre-commit 工件门禁校验 |
| AC9 本批入库文件自身零违规（-Files 自扫） | 满足 | 新增 17 件全内容 0 命中；改动 3 件新增 0 命中（-DiffFile）；存量 10 处属既有行，棘轮不拦 |

## Known Limitations

1. **棘轮只拦新增行**：全仓存量 717 处命中（4) 数字）本批不清理——按 060②「存量清理不做」记录，理由与重审触发条件见 060 原文。
2. **拒绝列表为本地私有文件**：不入库、不在 CI 生效——按 060①「CI 接入不做」记录。
3. **称谓/人名/其他项目业务信息无机械信号**：靠拒绝列表人工补词 + review 兜底——按 060③ 记录。
4. **-All 口径为非阻断盘点**：exit 1 属预期（有存量即报），不可用作 CI 判绿口径。
5. **守卫按「当前目录」解析仓库根**：在另一检出（worktree）里运行时扫的是那个检出（5) 对照实测）；跨检出使用须先 `Set-Location` 到目标检出根。此为运行注意点非缺陷（repo-root-from-CWD 为常规约定）。
6. **全量测试首轮 11:26 卡死废弃**：与并行会话共享构建节点相关（同时段有并行 dotnet 进程在跑）；改用 `dotnet build-server shutdown` 后重跑（12:10–12:25:04）取得有效读数。废弃轮未采信为任何结论依据。

## Unresolved Issues

1. **并行会话在途批未收口**：模型别名批（`AIModelFeatureTests.cs`/`ModelsController.cs`/`AIModel.Biz.cs`/文档 004 及其 pilot 目录）由并行会话推进中，本批全程未触碰、未提交。
2. **db-admin 批未收口**：等闸门 1 批准，相关文件保持未提交未改动。
3. **证据日志不入库**：本批原始日志在 `.temp/tmp/`（gitignored）；根目录 `gate-logs/` 为另一会话产物，未跟踪、不属本批，均不提交。
4. **全量测试 26 红待治理**：25 基线红 + 1 ForgeConfigTests 偶发红，台账 TODO.md L17/L185，不属本批。
5. **CI 接入 / 存量清理 / 机械检测**：060 三条不做，重审触发条件见原文。
