# Final Report — B2 结果硬化（filesChanged 落库 / Verify 闸门 / 结果收据）

> 战役：`2026-11-02-maf-runtime-hardening` 批 B2 · 执行：verify-dev（teammate）· 构建/发布/起停：lead 统一调度 · 本报告不含 git 提交（lead 执行）。

## 1 结论

**⚠️ COMPLETED_WITH_RISK（风险面已收敛至低/信息级）**——三件事代码闭环，AC-1~8 全部 Verified（后端构建 0 错误、过滤集 r2 旁路 124/124、插件前端 build 绿、问题类闭环机器校验绿、AC-UI 两态走查截图齐）。已知余项全部为低级：cancel 冻结专测（P3）、AgentHub 回写语义（PC 盲区登记）、Value 语义修复待下次重启生效（源码+测试齐，向后兼容）。

## 2 交付摘要

| 项 | 内容 | 验证 |
|----|------|------|
| B2① filesChanged 落库 | ToolCallFileExtractor（结构化优先+正则兜底）、AppendSystemAsync +filesChanged（接口对齐）、AppendAdvanceRecordAsync 透传 | TDD 红（2/0）→ 绿（2/2），日志 b2-red-evidence.log / b2-green-fileschanged.log |
| B2② Verify 闸门 | ExitCriteria 落 AgentRun.ExitCriteria；三路收束共用 FinalizeRunAsync→Verify→Completed/Stuck；RunVerifyService 确定性断言 V1 + fail-safe；开放任务回合级 run/verify；SessionEvent 三步走 | RunVerifyGateTests 10 例 + RunVerifyService 单测，过滤集含 AC-3/4/6 |
| B2③ 结果收据 | 计划任务 ResultReceipt write-once 冻结 / 开放任务 run/receipt append-only；DTO 透出；RunRecordPanel.vue 收据区块（徽标+判据 ✓/✗+文件 chips+计数/冻结时间），空收据不渲染 | RunReceiptTests 5 例 + RunVerifyGate 冻结断言 + 插件 build 绿 |

完整变更清单/AC 映射/红灯原文 → `05-evidence.md`；八问审查 → `06-review.md`（Final Decision: APPROVED 有条件）。

## 3 验证记录（只列实跑）

- `dotnet build ForgeSelf.Api.Tests` → 0 错误（b2-build-green.log）
- 过滤集 15 子 → **121/121 通过 0 失败**（b2-filter-full.log），对照 qa 基线 89 红：零新增
- 核心新测试 23/23（b2-filter-core.log）
- 插件前端 `pnpm run build` → 绿（99 modules，dist/index.js 290.04KB，b2-web-build.log）
- `ProblemClassRegistryTests` 5/5（b2-registry.log）+ `pwsh scripts/check-problem-classes.ps1` → **OK：23 个 PC 全部通过**（b2-checkpc.log）

## 4 问题类归档（AC-8 · 缺陷类必填节）

- **case**：`docs/11-troubleshooting/cases/2026-11-02-fileschanged-persistence-loss.md`（front-matter id 与文件名一致、pc: PC-023、severity P2、evidence Verified；双断点根因 + 红灯原文 + 19 调用点/全库 Regex 同类扫查记录）。
- **PC**：`docs/11-troubleshooting/problem-classes/PC-023-todo-fileschanged-persistence-loss.md`（不变量、S1 真库测试签名 + S2 `new Regex` no-match 机器签名、G1 守卫台账、盲区登记）。
- **三处索引**：problem-classes/README.md 台账总表 + tags 检索表（PC-023 行，并补齐 PC-022 行）、cases/README.md 时间线索引、docs/11-troubleshooting/index.md 现象速查表。
- **守卫**：`BuiltInFilesChangedPersistenceTests.cs`（[Collection("XCode")] 真库，快照提取 + 自动回写落库两用例，进快档过滤集）；B2②③ 侧 RunVerifyGateTests/RunReceiptTests 同时守住提取器与收据冻结。
- **机器校验**：Registry 测试 + check-problem-classes.ps1 双绿（见 §3）。

## 5 重构提案

1. **轨迹落库形态 G4 化**：`AgentStepRun.ToolCallsJson`「序列化再套字符串」的双编码是本类缺陷的土壤——收敛为统一轨迹序列化入口（带 schema 版本），`ArgumentsSummary` 改存结构化 JSON，让反转义兜底退化为纯历史兼容。
2. **取证单点已立，下一步收敛调用方**：ToolCallFileExtractor 现服务快照（BuiltInAgentExecutionService）/收据（RunOrchestratorService）/开放任务回合（MafTaskLoopAgent）三处；建议后续把「写类工具判定 + 变更标记（A/M/D）」也统一进提取器，消灭三处各自的 ChangeOf 语义副本。
3. **成对 API 参数面巡检**：凡「手动路径有、系统路径没有」的成对留痕 API（AppendAsync/AppendSystemAsync 模式），纳入 plugin-development/工程规则检查项（PC-023 lens 第 3 条）。

## 6 悬空教训

- **「提取失败返回空」与「本来就没有」不可区分**：取证类代码必须二选一——结构化降级标记/告警日志，或真库守卫钉住非空形态；本批两者都做了（提取器软降级 + BuiltInFilesChangedPersistenceTests）。
- **接口演化静默面**：实现新增可选参数而接口未同步不会报编译错（反向才会）——「手动/系统成对 API」改任何一个必须同名扫另一个（已沉淀 PC-023 lens）。
- **高风险名单是语义而非障碍**：测试样本 write_file 被 HighRiskToolPolicy 挂审批导致回合不收束——正确行为教会我们用 create_file 做写类样本；任何绕过审批名单的「测试便利」都是倒退（未做，记录在案）。
- **PC-022 的 S2 签名是无操作**：其 `regex: "new Memory\\{"` 经 PowerShell Select-String 恒不命中（双反斜杠字面量），扫描脚本对它静默通过——建议 lead 顺手修正为有效的反模式签名（不在本批写面，仅提示）。

## 7 风险与遗留

| # | 项 | 等级 | 处置 |
|---|-----|------|------|
| 1 | ~~AC-UI 真实宿主走查未做~~ **✅ 已闭环（22:17-22:19 走查，两态截图 + 读图核对，05 §6）** | — | 截图 `ForgeSelf.Web/screenshots/dev/b2-receipt-20261102/` |
| 2 | cancel 冻结无专测 | 低 | TODO P3 已登记；write-once 与 Stuck/Completed 共路径 |
| 3 | AgentHub 委派回写 filesChanged 语义未覆盖 | 低 | PC-023 §4 盲区已登记 |
| 4 | TasksOverviewController.cs:295 一行修复在 taskcenter-dev 写面 | 信息 | 已报备 lead，请知会对方；build-green 指令优先 |
| 5 | FR-2.1 修订（ExitCriteria 独立列而非 PlanJson） | 信息 | 02-spec 已补修订记录；后续 resume 逻辑读列不读 Plan |
| 6 | 走查实证 Value 语义修复已入库（RunExitCriterion.Value，红→绿 3 用例，r2 旁路全过滤集 124/124），**运行实例仍载旧 bin**——语义随下次重启/发布生效；旧语义对裸词判据（Value 缺省）行为不变，向后兼容 | 低 | 源码+测试齐备；lead 下次重启即激活 |
| 7 | 走查测试数据：Run#5-#8 + run:{id} 会话日志留在库（runs 无 DELETE API；两 Run 零工具调用无文件副作用） | 信息 | 如需清净由 lead SQL 清 AgentRun 5-8 行 |

## 8 下一步（回流 lead）

1. ~~AC-UI 走查~~ 已闭环（见 §7#1）——B2 全部 AC（1-8）Verified。
2. 可提交：lead 统一 git 提交（本批全部变更未提交；工件目录已由 lead 拍平至 `docs/ai/pilot/2026-11-02-b2-fileschanged-verify/`）。
3. taskcenter-dev 第二构建窗口可开（B2 编译绿 + 过滤集基线已立）。
4. 可选：下次 dev-stack 重启后 Run#7 复跑即可实证 Value 语义生效（或随正常发布验证，非阻塞）。
