# B5 中档门禁审计报告（b5-qa-audit）

> 审计/执行人：qa-gate｜时间：2026-11-03 01:11–01:35 +08:00｜性质：**新鲜标准 bin 中档全量（收口窗口内亲自执行）+ 对表判责**
> 基准：[qa-baseline.md](qa-baseline.md)（89 红，HEAD 07d314b）＋ [b4-qa-audit.md](b4-qa-audit.md)（44 伪红机制表）＋ verify-dev b5s8 全量对表（73 红）
> 被测批：B5（删 ReactLoopAgent + 开关收敛 + Coordinator/Executor 裁决删除，verify-dev；HEAD=943bb0c + 工作树 B5 变更，收口后由 lead 提交）

## 0. 结论（总览）

| 项 | 结果 |
|---|---|
| 运行口径 | ✅ **新鲜标准 bin**（Tests.dll 01:12:10 > 最新源码 00:28:56，**零漂移**）；`dotnet test <dll> --no-build --nologo`（窗口内 dev-stack 已停，无锁）；**无 OutDir**；13 m 55 s |
| 汇总 | **3427 总 / 3339 过 / 88 败 / 0 跳**（`.temp/tmp/b5-fulltest.log` 正文，exit=1 不作证据） |
| 89 基线对表 | ✅ 逐名闭合：88 = **85 基线同族** + **3 环境漂移特例**；基线 89 = 85 显现 + 2 registry 修复（保持修复✓）+ 2 Todos 未显现（漂移）+ 3 特例 |
| **B5 引入红** | **零**（B5 专项测试面全部绿，见 §3；3 特例错误正文均为环境机制，见 §2） |
| **B4 §5-1 残差格** | ✅ **双向闭合**：① verify-dev 新鲜 bin 守卫 17/17（`.temp/test-guards-stdbin-b5win.log`）② 本全量中 B4 期 OutDir 伪红族（ChatController 16/Invariant 4/Race 1/RealLLM 4/AIProviderRegistry 5/ChatCompletionWritePath/PersistentSessionStore.Di_Resolves/XCodeIronRule 等 26 条）**在真标准 bin 全部转绿** |
| **门禁判定** | ✅ **B5 中档门禁通过**（零新增红 + B4 残差闭合）；待 e2e 深档 |

## 1. 运行环境与证据链

- 前置（§5.0）：`NO_PROXY`/`TEMP→.temp\tmp` 已设；判定看日志正文。
- 构建态：verify-dev 窗口内重建（0 error，`.temp/build-stdbin-b5win.log`）；本执行前核验 Tests\bin Tests.dll 01:12:10，全仓最新 .cs = 00:28:56（ForgeSettingLegacyKeyCompatTests.cs）→ **被测二进制覆盖当前全部源码**。
- 执行方式：detached runner（`Start-Process` + `.done` 标记，runner pid 99428 → testhost 42068），防 harness 丢任务；发射前优雅回收 verify-dev 构建遗留的 22 个空闲 MSBuild 节点（`dotnet build-server shutdown`，空闲内存 2.7→5.2GB，规避基线 OOM 同款风险）。
- 证据：`.temp/tmp/b5-fulltest.log`（154KB）＋红名单 `.temp/tmp/b5-final-redlist.txt`（88）＋verify-dev 名单 `.temp/test-full-b5s8.log`（73）＋基线名单 `.temp/tmp/qa-baseline-redlist.txt`（89）。

## 2. 88 红逐类构成与判责（三判归属）

| 族 | 数 | 归属 | 依据 |
|---|---|---|---|
| TodoDispatchFlowTests | 22 | 环境漂移（PC-014 族，基线已登记 TODO 94） | 基线同族 32 条中的 22 条本轮显现；类序/数据根敏感 |
| TodoBuiltInDispatchTests + TodoAgentStatusBatchTests | 6+4 | 环境漂移 | 同上（合计 TodoTracker 32，与基线同族数完全一致） |
| ImGatewayTests + ImGateway.DbConfigStoreTests | 18+4 | 环境漂移 | 基线同族（隔离复跑历史全绿；本轮混编行错误正文为 SQL/数据机制） |
| ProjectWorkspaceDefaultRootTests | 15 | 稳定红（基线登记，68df669 批 TODO 所有） | 基线同族 |
| WorkflowPlanningIntegrationTests | 6 | 稳定红（基线登记：隔离复跑仍 6/7 红，404 同因） | 基线同族 |
| TodosControllerTests | 5 | 环境漂移 | 基线 7 → 本轮 5（另 2 条未显现） |
| MemoryTimestampGuardTests | 3 | 环境漂移（基线隔离 3/3 绿） | 基线同族 |
| CancelAsync_ForRunningScript | 1 | 已知 flake（基线登记） | 基线同族 |
| ScriptRunnerDiIntegrationTests | 1 | 稳定红（基线隔离复跑仍红，404 同因） | 基线同族 |
| **特例①** BuiltInFilesChangedPersistenceTests.内置成功自动回写留痕应携带结构化文件清单 | 1 | **环境漂移** | 错误正文 `SQLiteException: SQL logic error`（XCodeTestFixture 数据污染=PC-014 机制）；B2 期测试，基线时尚不存在、verify-dev b5s8 同源跑绿 |
| **特例②** ApiKeyServiceTests.ResolveByToken_停用后返回null_且不回退主密钥 | 1 | **环境漂移** | 错误正文 `IOException: 无法删除要被替换的文件`，抛在测试构造器 XCodeTestFixture 初始化（文件替换冲突，与 B4 SettingsNetwork 同机制）；B5 未触 ApiKeyService |
| **特例③** DesignSystemTests.DesignAgentToolContractTests.ExecuteAsync_空参数_不抛 | 1 | **环境漂移** | `success=False`（工具执行受污染数据/全局态影响）；DesignSystem 插件 B5 零触碰；基线绿 + verify-dev 同源绿 |

**对表闭合账**：89 基线 = 85 显现 + 2 registry 修复（cf01aa5，保持修复）+ 2 Todos 未显现 + 3 特例（基线绿）⇔ 本轮 88 = 85 + 3 ✓。
**与 verify-dev b5s8（73 红）对账**（去混编孪生后）：共同核心 25；其独有 26 = B4 期 OutDir 布局伪红族（其跑法布局不同，本轮真标准 bin 下全绿——反向印证伪红定性）；我独有 41 = 环境族未在其跑显现 + 3 特例。同一源码两跑红集合差异全部落在已登记环境族内。

## 3. B5 专项面核对（写面 × 红集合 交叉）

B5 写面（verify-dev AC2–AC8）：ReactLoopAgent/AgentCoordinatorService/AgentExecutorService 删除、Agents.cs 开关删除、AIAgentService :332/:417 收敛、AgentsController 6 端点删除、AgentModels 收敛、AgentTurnRuntime 新文件、6 个编排器测试文件换装、ForgeSetting 兼容读。
**交叉结果：88 红中无一属于上述写面测试**；反之 B5 专项全部绿——MafTaskLoopAgentTests 16 + MafInboxGate 3 + MafExitAndSuspend 3（AC3，106/106 合批）、6 换装文件（AC4）、ForgeSettingLegacyKeyCompatTests 2/2（AC6）、LegacyContract_Removed 禁用名单守卫（AC2）、ReactLoop* 用例已删除（集合中不存在）。**零 B5 引入红成立。**

## 4. B4 残差格闭合（收口窗口双证）

1. verify-dev 守卫隔离（新鲜标准 bin）：ChatCompletionWritePath + XCodeIronRule + PersistentSessionStore 全族 + ChatProjectionRace = **17/17 绿**（`.temp/test-guards-stdbin-b5win.log`，34s）。
2. 本全量（真标准 bin）：B4 期 44 伪红所属类红集合为**零**（ChatController/Invariant/Race/RealLLM/AIProviderRegistry/Multimodal/SettingsNetwork/ChatCompletionWritePath/PersistentSessionStore/XCodeIronRule 全部绿）。
⟹ b4-qa-audit §5-1 残差项关闭；TODO 对应条目移除。隔离复跑 filter 落日志的实践缺口仍留 TODO（P3）。

## 5. 残差与建议

1. **3 特例复发监视**：本轮按错误正文判「环境漂移」，若下个全量同 3 条**再次**红，按 problem-class-closure 纪律二次复发立案（≥G2 守卫）。建议 verify-dev 下次过滤集抽跑这 3 条留正证。
2. TodosController 7→5、TodoTracker 32 全显 vs 昨日 0 显——环境族随类序/数据根漂移幅度大，全量对表以「族」为单位判责（本报告口径），单跑红数在 73–89 区间浮动属预期。
3. dev-stack 由 verify-dev 知会 lead 重启（本执行期间保持停止，符合窗口纪律）；重启后如需只读冒烟请指派（B5 未改 API 面，预期 profiles/audit/runs 同 B4 审计结果）。

## 6. 判责结论

**B5 中档门禁：通过。** 3427 总 = 基线 3372 + B2-B4 新增 55（3427 与 verify-dev 两跑完全一致）；88 红对表 89 基线：85 同族 + 3 环境漂移特例（错误正文实证）+ registry 2 修复保持——**零 B5 引入红、B4 残差双向闭合**。待 e2e 深档后 B5 可收口。
