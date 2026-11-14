# AI-Native Pilot Result（最终汇报）— PILOT-081

> 任务：v1/models 别名模型额外返回原模型｜任务日：2026-11-14｜状态：✅ COMPLETED（闸门2 待用户验收，闸门3 未提交）

## 1. Repository Understanding

我确认了（详见 `00-repository-understanding.md`）：

- 技术栈：ForgeSelf.Api = .NET 10 + ASP.NET Core + NewLife.XCode；测试 xUnit + FluentAssertions + XCodeTestFixture 临时 SQLite（`[Collection("XCode")]`）。
- `/v1/models` 链路：`ModelsController.ListModels`（`[Route("v1")]` + ApiKeyPolicy）的列表源 = 上游 `/models` 拉取结果（`OpenAICompatibleProvider.GetModelsAsync` → `ModelInfo`），经 `AIModel.GetGatewayModelListId(providerName, upstreamModelId)` 投影——原实现别名遮蔽原名，是本批要改的点。
- 适用问题类：PC-028（别名路由/出站替换，本批命中为互补面——返回原名条目的前提是其可路由，`ResolveUpstreamModel` 已支持 `provider:model` 形态）+ PC-014（测试类隔离纪律的视角约束）；其余未命中。
- 真源：`docs/02-features/004-provider-models-integration.md` §2.6 承载 `/v1/models` id 语义，本批回写。

## 2. Selected Task

用户 2026-11-14 直接指令：「需求调整：v1/models 获取模型接口，模型设置了别名，增加返回原本应该返回的模型，即设置了别名的额外返回，而不是仅返回别名，原本的也要有」→ PILOT-081，闸门1 以用户直接指令预批（PILOT-080 先例，`04-task.md` YAML `gate1: { mode: pre-approved }`）。

## 3. Changed Files

| 文件 | 动作 |
| --- | --- |
| `ForgeSelf.Api/Entities/AIModel.Biz.cs` | `GetGatewayModelListId` 委托化取集合首项（单值语义不变）+ 新增 `GetGatewayModelListIds`（别名在前 + 原名，Ordinal 去重） |
| `ForgeSelf.Api/Controllers/UnifiedAI/ModelsController.cs` | `ListModels` 改 `SelectMany` 集合展开；`GetModel` 单端点不动 |
| `ForgeSelf.Api.Tests/AIModelFeatureTests.cs` | +3 用例：集合语义（别名/无别名）、控制器两条目、退化去重 |
| `docs/02-features/004-provider-models-integration.md` | §2.6 回写 2026-11-14 修订（两条目语义 + 路由说明） |

工作区其余改动（DbAdmin / commit-hygiene 批）属并行会话，本批未触碰。

## 4. Validation

Build: ✅ PASS（Verified）——`dotnet test` 含宿主工程构建 0 error（pilot081-green.log，EXIT=0；存量告警 CS8618/xUnit1031 无新增）。

Unit Test: ✅ PASS 快档（Verified）——TDD 红（CS0117 ×3 = 3 条新用例）→ 绿 `失败: 0，通过: 25，总计: 25`（22 既有含 PC-028 守卫全绿）。中档全量（Verified）——`失败: 26，通过: 3752，总计: 3778，14m14s`，26 红逐条对表基线（ProjectWorkspace ×15 / WorkflowPlanning ×6 / JsonNonAscii ×1 / ForgeSetting 归一 ×1 / ScriptRunnerDi ×1 / CancelAsync ×1 / Maf ×1），**归 PILOT-081 的新增红 = 0**；总数 3778 = 当日早间基线 3775 + 3（即本批新用例）。

E2E: N/A——`e2e/api-gateway.spec.ts:63-71` 对 `/v1/models` 只断言 200 + `object=="list"` 结构不锁 id 内容；本批未触碰 e2e 基建，不触发深档（未运行，如实声明）。

## 5. Evidence

关键结论与全量对表明细：`05-evidence.md`（分级 Verified）。日志：`.temp/tmp/pilot081-red.log` / `pilot081-green.log` / `pilot081-fulltest2.log`。本目录 00-07 八件齐备，工件链门禁（pre-commit `verify-pilot-artifacts.ps1`）在用户闸门3 提交时生效。

## 6. Review

`06-review.md`：八问逐项 PASS，Findings 仅 Minor 1 条（编辑工具丢 BOM，已当场还原），**Final Decision = APPROVED**。

## 7. Risk

L1——公共 API 响应内容新增条目（用户明确要求的行为变更）：OpenAI 兼容消费方按 id 取用，多一条可路由 id 无破坏面；PC-028 出站替换守卫快档+全量双绿。回滚点 = 还原 3 个代码/测试文件。

## 8. Problems Found

1. **设计偏差（已按 Spec 修正记录）**：AC-4 原拟「providerName 严格为空时别名=原名去重」不可达（严格空 providerName 时 DB 查找短路不读别名）；可达退化变体 = providerName 为空白串 `" "`。`Distinct(Ordinal)` 兜底 + 对应用例覆盖，偏差记 `03-plan.md` 偏差表。
2. **全量首跑挂死（环境类，已归因）**：未设 `NO_PROXY` 时全量在某集成用例后挂死 40 分钟（testhost CPU 0 增量 + 日志停写）；按进程归属精确清殓 26 个本跑进程后补前置复跑，14m14s 正常完成。教训已沉淀 `b-engineering-daily.md` §B2。
3. **编码回归（已修）**：编辑工具重写 `AIModel.Biz.cs` 丢 UTF-8 BOM，审查时还原。

## 9. Process Evaluation

| 环节 | 评价 |
| --- | --- |
| Repository Understanding | PASS（00 含适用问题类三问与证据行） |
| Intent → Spec | PASS（用户原话逐字入 Intent；AC-4 偏差当场改 Spec 并记偏差表） |
| Spec → Plan | PASS（Plan 落到真实文件；验证分档对齐 §5.6 触发面） |
| Plan → Code | PASS（TDD 先红后绿；单值语义兼容面委托化保住） |
| Code → Test | PASS（快档 25/25；中档 14m14s 对表 0 新增红） |
| Test → Evidence | PASS（三级读数 Verified，无 Inferred 混入） |
| Evidence → Review | PASS（八问全链重查；Findings 如实含 Minor） |

### 9.5 本批自复盘（必填）

- **经验提取（保留打法）**：① 需求调整类任务沿用「原语义函数委托新集合函数取首项」的兼容面打法——调用面零破坏、新旧语义同源；② TDD 三用例一批同红同绿，红名单逐行号核对即「红灯必须红在新用例上」；③ 挂死判据两件套（CPU 增量采样 + 日志 LastWriteTime）+ 杀进程前 CommandLine/CreationDate 窗口归属圈定，本轮零误杀并行会话进程。
- **待改进（1 条）**：中档全量首跑前未过 §5.0 环境前置清单（`NO_PROXY` 漏设），损失约 50 分钟挂死等待——机制修正已沉淀 B2（「后端全量同 checklist」），下批起把该前置并入全量跑测的启动命令模板（出处：AGENTS §5.0 / B2 2026-11-14 条）。

## 10. 问题类归档

非缺陷类任务（功能需求调整），无新增问题类：本批无缺陷复现/回归/返工场景；同类扫查（`AIModel.*` 调用面 + 别名相关用例 + 文档语义面）零命中同根因旧缺陷。关联既有类：PC-028（别名路由）守卫本批复验双绿、无逃逸。台账真源 `docs/11-troubleshooting/problem-classes/README.md` 本批零改动。

## 11. 重构提案

R0 不重构——改动面 2 个生产方法 + 1 处投影，无跨模块腐化；「列表 id 归一规则」已单点在 `AIModel` 实体层（`ChatModelId` 与列表 ids 语义一致）。若未来别名语义再扩（如列表排序/分组），再评估 R1 局部提案。

## 12. 悬空教训清单（必须为空）

（空——① 挂死教训已落 `b-engineering-daily.md` §B2 2026-11-14 条；② AC-4 偏差已落 `03-plan.md` 偏差表 + `02-spec.md`；③ GetModel 单值边界已落 `02-spec.md` Known Boundary + 真源 004 §2.6；④ BOM 还原动作已闭环于 06-review Minor，无遗留动作。）
