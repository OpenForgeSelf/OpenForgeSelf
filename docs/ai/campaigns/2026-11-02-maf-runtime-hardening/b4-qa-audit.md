# B4 中档门禁审计报告（b4-qa-audit）

> 审计人：qa-gate｜时间：2026-11-02 23:42–23:58 +08:00｜性质：**审计 verify-dev 的门禁证据，非重跑**
> 基准：[qa-baseline.md](qa-baseline.md)（2026-11-02 21:39 采集，HEAD 07d314b，89 红）
> 被审计批：B4 全批（后端 3c4d7ce + UI 943bb0c）；被审计证据：verify-dev 全量 r3 OutDir + 标准 bin 隔离复跑
> 判责口径（lead 22:4x 批准）：三判归属（B4 引入 / B2-B3 携带 / 环境漂移）+ 隔离复跑定性 + 写面与 git log 定位

## 0. 审计结论（总览）

| 项 | 结论 |
|---|---|
| 全量汇总行 | ✅ 与自述一致（日志正文核对：**3435 总 / 3348 过 / 87 败 / 0 跳 / 17m7s**） |
| 87 = 43 基线同族 + 44 OutDir 伪红 | ✅ 差集逐名闭合（本报告 §2）；「零新增红」**成立** |
| 89 基线 → 87 差集 | ✅ 89 = 43 仍红 + 44 本轮未显现（环境漂移）+ **2 registry 红确已修复**（cf01aa5） |
| 44 伪红排除（81 全绿） | ⚠️ **结论方向成立、证据链有一格缺口**：隔离复跑打在 **pre-B4 标准 bin**（Tests.dll 21:52:09 < B4 源码 22:52+）——绿证的是 pre-B4 代码；本审计以 44 条错误正文逐机制归类 + B4 AppBuilder 纯增量 diff 补强（§3），残差项见 §5-1 |
| 只读冒烟 | ✅ 全绿且**更正一处预期**：`/api/health` 存在且 200（lead 预期 404 无该路由，实测 `{"status":"healthy"}`） |
| Value 修复生效 | ✅ 旧 Run 实证（run 7：旧式仅 Text 判据 → 回退 Text 断言 passed + 收据冻结；run 4 无判据 → verifyStatus=0 无假 Verify） |

## 1. 证据清单（被审计物）

| 证据 | 路径 | 关键内容 |
|---|---|---|
| 全量 r3 OutDir | `.temp/tmp/b4-fulltest-final.log`（270KB，23:20） | 汇总行 + 87 红名单 + 每红错误正文（1690 行） |
| 隔离复跑 #1/#2 | `.temp/tmp/b4-isolate1.log` / `b4-isolate2.log`（23:22） | **54/54 绿 + 27/27 绿 = 81 全绿**；⚠️ 未记录 filter 参数 |
| B4 过滤集/UI 门禁 | `b4-unit-test*.log`、`b4-maf-test*.log`、`b4-ui-check/test/design*.log` | 23 新用例 + UI 三面门禁（本审计未逐条复核，属 B4 快档已过钩子范围） |
| git 证据 | 3c4d7ce / 943bb0c / cf01aa5 diff | AppBuilder 纯增量；cases/README.md + case front-matter 修复 |
| 本审计产出 | `.temp/tmp/b4-final-redlist.txt`（87 名单）、`b4-audit-stdbuild.log`（锁证据）、`b4-smoke-*.json` | 差集与冒烟原始数据 |

## 2. 差集核对（89 基线 ↔ 87 本次）——逐名闭合

- **仍在红（43，与基线同族）**：ProjectWorkspaceDefaultRootTests 15 + WorkflowPlanning 6 + ScriptRunnerDi 1（404 同因）+ ImGatewayTests 18（中文孪生，基线清单为编码伪影、本次清单干净，同一批用例）+ TodoTracker 内 2 条盘符断言未现（归环境漂移）+ CancelAsync 1 + MemGuard 3 + ForgeConfig 1（已知偶发族 TODO 97）等，合计 43。
- **基线有、本轮无（44）**：TodoTracker 32 + ImGateway.DbConfig 4 + TodosControllerTests 7 + CancelAsync/部分盘符 1 —— 全部为基线已定性的**环境红/flake**，本轮未显现 = 环境漂移（PC-014 族类序/数据根敏感），非修复。
- **基线红已修复（2）**：ProblemClassRegistryTests 两条不在 87 清单 ✅。修复证据：`cases/README.md` 索引行已含 `2026-11-02-memory-timestamp-minvalue | PC-022`；case front-matter `id: 2026-11-02-memory-timestamp-minvalue` 与文件名一致；均由 **cf01aa5**（B2 问题类闭环）落库。TODO 该 ⬜ 条可销。
- **本次新红（62 名义 → 真新 44）**：62 − 18（ImGateway 中文孪生）= **44**，全部不属于 B2/B3/B4 写面（git 交叉：B2 触 SessionEventProjection/BuiltInAgentExecutionService/MafLoop；B3 新文件；B4 见 §3 diff——44 条所属 12 类无一被触）。

## 3. 44 伪红机制归类（读 OutDir 日志错误正文，非推断）

| 机制 | 数量 | 错误正文证据 | 判定 |
|---|---|---|---|
| 测试宿主引导失败→DI 容器空 | 21 | `No service for type 'ISessionStore'/'IConfigurationService'`（Invariant 4 + RealLLM 4 + ChatController 13）+ NRE（ChatController 3） | OutDir 布局伪红（AppBuilder 对这些注册零改动，纯增量 diff 佐证） |
| XCode 数据根未建表 | 11 | `XSqlException: SQL logic error / no such table: AIModel`（AIProviderRegistry 5 + Multimodal 6） | PC-014 族数据根伪红 |
| 源码扫描根漂移 | 2 | ChatCompletionWritePath `Assert.NotEmpty() Failure: Collection was empty`（扫描根随程序集位置漂移扫到空集）；XCodeIronRule `Assert.NotNull() Failure`（扫描根定位失败） | OutDir 布局伪红；⚠️ 但守卫本体未对 B4 源码出过标准 bin 结论（§5-1 残差） |
| 文件替换冲突 | 1 | SettingsNetwork `IOException: 无法删除要被替换的文件` | OutDir/临时文件伪红 |
| 已知偶发族 | 1 | ForgeConfig 断言路径 `.temp\tmp\qa-b9\...`（TODO 97 已登记 ConfigUnifier 全局态污染偶发假红） | 环境偶发，非 B4 |
| DI 解析 NotNull | 1 | PersistentSessionStore.Di_Resolves `Assert.NotNull() Failure`（OutDir 下装配失效形态） | 布局伪红；B4 AppBuilder 为**纯增量**（只加 IPermissionProfileService 单例 + fail-open 监听器，不动既有注册——diff 已核），残差 §5-1 |
| 其余 | 7 | 与上述同类（ChatController/RealLLM 同模式） | 同上 |

## 4. 只读冒烟（:7301，带 B4 bin，dev-stack.json pid 123988 @23:32:08）

| 探测 | 结果 | 判读 |
|---|---|---|
| GET /api/health（带鉴权） | **200** `{"status":"healthy","timestamp":"2026-11-02T23:51:37+08:00"}` | ⚠️ 更正 lead 预期（404 无路由）——路由存在且 healthy，更利于收口 |
| GET /api/permissions/profiles | 200 `[]` | B4 端点通；空数组 = 零配置默认策略红线一致（无策略=内置默认） |
| GET /api/permissions/audit?limit=3 | 200，真实联合审计数据（`kind:"tool-approval", status:"Approved"`，camelCase ✓） | 审计链路真实可见 |
| GET /api/ai-agent/runs + /runs/7 + /runs/4 | 200 | 见下 |

**Value 修复生效实证（只读）**：
- **run 7**（B2 开发期旧 Run，收据冻结于 22:18:31）：exitCriteria 为**旧式仅 Text**（`[{"Text":"验收通过","Type":"output_contains"}]`，无 Value 字段）→ 收据 `verifications[0] = {text:"验收通过", type:"output_contains", passed:true, detail:"产出包含「验收通过」"}` —— **旧判据经「Value 缺省回退 Text」路径正确断言**，run status=3、verifyStatus=2、收据含 summary/filesChanged/verifications/evidence/toolCallCount/frozenAt 全套结构 ✅。
- **run 4**（无 exitCriteria）：verifyStatus=0、无收据、无假 Verify 阶段 ✅（闸门对无判据旧 Run 零侵入）。
- **枚举观察（U2 相关）**：实体枚举数值（`status=3`、`verifyStatus=2`）与收据内字符串（`"status":"Completed"`、`"type":"output_contains"`）并存——与 U2 裁决「枚举保持数值语义 + 收据已字符串序列化」一致，实测吻合。

## 5. 残差与建议（按优先级）

1. **【下个锁窗口补一格】「B4 二进制 × 标准 bin」未验证**：隔离复跑（23:22）打在 21:52 pre-B4 标准 bin 上（Tests.dll 时间戳 < B4 源码 22:52+；本审计 23:48 尝试重建标准 bin 被 `.NET Host (120544)` 锁定拒——MSB3026/3027，证据 `b4-audit-stdbuild.log`）。风险低（§3 机制归类 + AppBuilder 纯增量），但按门禁纪律建议：下个 dev-stack 停启窗口，重建标准 bin 后跑 `dotnet test --filter "FullyQualifiedName~PersistentSessionStoreTests|FullyQualifiedName~XCodeTestFixtureIronRuleTests|FullyQualifiedName~ChatCompletionWritePathTests"`（3 守卫类，分钟级）＋44 伪红所属类过滤集，闭合 2×2 矩阵。
2. **【证据实践】隔离复跑未记录 filter 参数**：54/27 两份日志只有汇总行，覆盖范围只能靠类规模反推（54 与 27 的组成无法从证据直接闭合）。建议后续约定：filter 写入日志首行或文件名（复跑证据可自证）。
3. **【记录澄清】dev-stack 进程号不一致**：lead 通报 pid 131352，`dev-stack.json` 为 backendPid **123988**（startedAt 23:32:08）；标准 bin 锁定者为 .NET Host **120544**（非 123988，疑似 dev-stack 子进程/另一宿主）。请 lead 确认当前栈进程树归属，避免下次锁窗口误判。
4. **【留痕】** `ForgeSelf.Api\bin\Debug\net10.0-windows\Api.dll` 至今缺失（21:10 失败构建损伤，宿主运行不受影响），下个成功构建自愈，无需动作。
5. **【U2 登记】** 枚举数值语义保持（实测与裁决一致）；「实体枚举改字符串序列化」→ 已记 TODO ⬜ P3 候选。

## 6. 判责结论

**B4 全批（3c4d7ce + 943bb0c）门禁：通过（附 §5-1 残差项，不阻断收口）**。87 红对表基线 89 红：43 同族 + 2 已修复 − 44 环境漂移未显现 + 44 OutDir 伪红（错误正文逐机制实证、无一属 B2/B3/B4 写面）= **零新增红**；B4 自有 23 新用例全绿（含 OutDir 全量内通过）。B5（删 ReactLoopAgent + 开关收敛）收口 = 中档全量 + 深档全量 e2e，qa-gate 已待命。
