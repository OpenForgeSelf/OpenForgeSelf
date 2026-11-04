# 07-final-report — PILOT-079 IM 网关任务派发（im-gateway 任务网关 + IAgentRunDispatch 接缝）

> 状态：⚠️ **COMPLETED_WITH_RISK**（完成；风险与附条件项见 §6/§7，均已显式列明）

## 1. 交付摘要

IM 网关新增「任务网关」：`/task` 前缀消息 → 跨插件契约 `IAgentRunDispatch`（Abstractions 新契约，第三接缝）派发 AIAgent 后台 Run → 即时 ack（编号+受派 Agent）→ Watcher 轮询终态主动推送（completed/failed/stuck 三模板）→ stuck 时 IM 审批卡片 + 关键字决策（DecideAsync 留痕 + ResumeAsync 续跑）。零 mock 全链验证（e2e 哨兵模型法 + REST 双查 + dev-stack 走查）。

## 2. 变更清单（本批 diff 面）

| 域 | 文件 | 说明 |
|---|---|---|
| Abstractions | `AgentRunDispatchContracts.cs`（新，142 行） | IAgentRunDispatch 契约 + Request/Outcome/Snapshot POCO |
| AIAgent 插件 | `Services/AgentRunDispatchService.cs`（新，468 行） | 提交/读回/续跑 + 后台 DriveAsync + 双层 inflight 闸 |
| AIAgent 插件 | `AIAgentPlugin.cs`（+13 行重构） | 缺陷#2 修复（Registry Singleton）+ 缺陷#3 修复（共享 pluginProvider + 5 契约实例单例注册） |
| ImGateway 插件 | `Core/ImGatewayConfig.cs`、`Data/Entities/ImGatewayTask.cs`+Model.xml、`Services/ImTaskDispatcher.cs`、`Services/ImTaskWatcher.cs`、Router 分支、`Controllers/ImGatewayController.cs`、plugin.json 2.2.0 | 任务网关全量（绑定行/派发/轮询/审批闭环/FR-8 simulate 端点） |
| 测试 | `AgentRunDispatchTests.cs`(16)、`ImTaskDispatchTests.cs`(28)、`AgentRegistryCaptiveTests.cs`(2，含跨 provider 守卫) | 新 46 例全绿 |
| e2e | `im-gateway.spec.ts`（+242） | 冒烟 ⓪-⑦ + AC6 回归，最终轮 5P/1S |
| 文档 | host-capability-seams §8、031-im-gateway 任务网关节、03-plan 偏差 6 行、TODO +2 | 回写完成 |

## 3. 验证汇总（全部实际执行，日志在 .temp/tmp/）

| 门禁 | 结果 | 证据 |
|---|---|---|
| 域回归（AIAgent+ImGateway 228 例） | 212 绿（15 基线红+1 假红已甄别） | p079-t2-final / p079-d3-std |
| 新测试三组 Red→Green | #1 Red 1/28→Green 60/60；#2 Red 1/1→Green；#3 Red 1/2→Green 2/2+4/4 | p079-t2-fix-*.log / p079-d2-*.log / p079-d3-*.log |
| 冒烟 e2e（三缺陷修复 build） | 5 passed/1 skipped/exit 0 | im-gateway-imtask-smoke.log |
| AC4 REST 双轮（前后 build 各一轮） | 双查同 runId + 消息链三连绿 | p079-ac4-*.log|json / p079-ac4f-* |
| dev-stack 走查 | 四图 + 配置回读一致 | screenshots/dev/p079-dev-01~04 |
| 中档全量 dotnet test（3491 例） | 3375 绿/116 红——**判读 0 归本批**（证伪见 §4） | p079-t4-fulltest2.log + 证伪双跑日志 |

## 4. 全量 116 红判读（0 归本批）

- 红图：ImGatewayTests 46 + TodoTracker 33 + ProjectWorkspaceDefaultRootTests 15（TODO L432 既有基线漂移）+ TodosController 7 + WorkflowPlanningIntegrationTests 6（TODO L163 既有基线红）+ DbConfig 4 + 杂项 5。
- 证伪（同会话同代码）：ImGateway 域过滤 **50/50 绿**（p079-t4-falsify-imgw.log）；+TodoTracker 联跑 **199/199 绿**（p079-t4-falsify-imgw-todo.log）；AIAgent 域测试在风暴全量中**全绿**（不在红名单）。
- 机理：失败签名（no such table [ImGateway]/SQLiteException/NullReference）= PILOT-051 夹具文档载明的 I-6/D-10 手搓换库风暴；grep 实证 ~30 存量测试类仍手搓 `DAL.AddConnStr`（DesignSystem 20+/Chat/Cost/Budget/Telemetry/Sems/ProjectService 等）；附带发现 xunit.runner.json 未复制进 bin（双保险之一静默失效）。两项已记 TODO P2（测试基建治理）。
- 本批测试全部走合规路径（[Collection("XCode")] + XCodeTestFixture 进程级单次绑定），合规配置下全绿。

## 5. 问题类归档（缺陷类三节之一）

| 缺陷 | 类归属 | 类级动作（守卫等级） |
|---|---|---|
| #1 Watcher 不回填 RunId（注释声称回填实则缺失） | 「状态双写一致性」类：绑定行与快照两份状态源，写侧缺失致重启恢复失效 | G2 守卫：从 RunId=0 起步的新测试（p079-t2-fix Red→Green） |
| #2 IAgentRegistryService Scoped 被单例俘获 | DI 生命周期俘获类（多容器分裂的轻症形态） | G2 守卫：AgentRegistryCaptiveTests（真实 ServiceCollection，Red→Green） |
| #3 4 eager 块各自 BuildServiceProvider=5 并存单例 | **DI 多容器分裂类**（host-capability-seams §7 坑1 同族，AgentHub 曾发，AIAgent 逃逸） | G2+ 守卫：跨 provider 三同实例断言；逃逸分析与机理沉淀 §8；同类扫查=IChatCompletion（判定无害，03-plan 已记） |

同类扫查结论：ImGateway 侧无同根因（其消费为每次 ctx.Get，无缓存俘获）；Abstractions 无状态类单例。

## 6. 重构提案（缺陷类三节之二）

1. **测试基建治理（P2，已 TODO）**：~30 存量测试类手搓 AddConnStr 统一迁移 XCodeTestFixture（或分域 Collection）；xunit.runner 补 cs None+CopyOutputDirectory 并加守卫断言（配置存在≠生效）。
2. **AIAgent eager 接线**：本批内已重构完成（共享 provider + 实例单例注册），无遗留。
3. （提案级）`IChatCompletion` 内部自建 rootProvider 可并入共享 provider——判定无危害故不动码，留待下次触碰该文件时顺手收口。

## 7. 悬空教训（缺陷类三节之三）

1. **中档全量门禁长期未跑 → 债务以「116 假红」形态一次性爆发**。基线红对表的前提是基线新鲜；后续 PILOT 碰宿主源码时，全量判读应先跑「stash 对照」再判责（本批用同会话过滤证伪替代，等效但事后成本更高）。
2. **配置存在 ≠ 配置生效**：xunit.runner.json 不进 bin 就不生效；凡「双保险」机制须有生效性守卫（阳性行为断），不能只文件存在3. **宿主重定向日志为 GBK**：pwsh 7 默认 UTF-8 读必得 mojibake 假 0 匹配（本日实证两次）；读宿主日志一律显式 GetEncoding(936)。
4. **长中文内容工具参数重打必讹**（lead 本日三犯，7 处讹误自纠）：here-string 铁律 + 写后必回读验证，已入团队。
5. **send_message 对 inactive 成员可能不起转**：召回需观察+重发（product GO 曾需 2 ping）。

## 8. 附条件与遗留（不阻断本批）

1. product 4 项：completed 终态 / AC10 运行时 / 任务状态快照 / 并发+24h——真实模型环境补人工走查。
2. design 5 类截图补证：off 态 / 删空前缀 / 名册兜底 / 1024px / 配置视图位置上下文。
3. 测试基建治理 2 项（TODO P2）；ShortCode/RunId 双制拍板（P3）。
4. 闸门3：git 提交归档，待用户验收。
---

## §返工轮（2026-11-04 夜，闸门2 验收反馈不通过 → 9 票修复 → 重提交）

**触发**：用户在 :51888 真实模型环境逐项验收报 7 项不通过 + 补充第 8 项（45s 超时口径）。全部派发持久化 teammates 修复（be-dev×5 / fe-dev×4），lead 逐票亲核验收。

**修复对照**：
| 反馈 | 票 | 根因 | 修复 | 验证 |
|---|---|---|---|---|
| #1 倒序 | T8 | 前端跨桶拼接序（后端本就有序） | 组装后 startedAt 统一倒序 | REST 10→1+UI ✅ |
| #2 跳转无参 | T9 | 无深链 | ?runId=/​?session= 双参数直达执行视图 | 活体+截图 ✅ |
| #3 完成不推送 | T10 | **推了坏内容**（stuck 收据 write-once 占位+投影优先 StuckReason） | completed 只投影收据 summary+终局可覆盖+清 StuckReason | Run 13 真模型 ✅（手机送达=用户环境） |
| #4 不自动滚 | T11 | 滚动所有权时机脆弱 | 首灌无条件/增量近底/rAF×2 | DOM 实测 ✅ |
| #5 超长截断 | T12 | 模板层静默截断 | 分段发送 (i/n)+MaxReplyChars=6000 总保险丝 | 码+单测×4；行为面=用户环境 |
| #6 IServiceScopeFactory | T13 | 上一轮名单法漏网（ctx 本体裸 CreateScope） | 全仓 35 处甄别、20 处漏网迁 CreateHostScope()+G3 守卫 | 守卫 2/2+case 归档 ✅ |
| #7 stuck 无入口 | T14/T15 | 45s 看门狗把「等审批」误判「无进展」 | 语义三分类+300s 可配+cancelUrl 下发+前端橙徽标/继续/终止 | 单测×5+端点实证 ✅ |
| #8 轮询/超时口径 | T16 | 45s 过短 | 60s 节拍+变化即推+5min 无变化才卡+终态即推 | 9a/9b/9c 活体 ✅ |

**验证轮**：9 ✅ Verified / 2 ⚠️（行为面待用户环境：T12 分段触发、T10 手机送达、9a 极端值）/ 回归护栏 e2e 5P/1S 零回归 + 全量 3393 绿/125 红净真新增红=0 / T13 守卫实跑 2/2。详见 05 §8。

**规格同步**（product）：02-spec 四处拍板修订（FR-4/FR-5/FR-6/Business Rules 7，旧值保留标注废止）+ 03-plan 偏差表补 T12/T16 规范行 + 06-review 返工 Review（product APPROVED、design UI 三点全符合）。

**新登记（均不阻塞本批）**：① LLM 400=既有缺陷（工具结果无上限×模型踩中大目录，SessionEvent 铁证；修复三建议入 TODO）② dev-stack 探测脚本遗失致静默关预包（已重建，闸门3 随 git 入库）③ ImTaskDispatch 全量池隔离提案 ④ 微信通道阈值下放+断点续发（后续票候选）。

**问题类归档/重构提案/悬空教训**：PC-005 二次复发按逃逸处理（名单法→全仓穷举法+守卫固化，case：docs/11-troubleshooting/cases/2026-11-04-raw-createscope-on-ctx-sweep.md）；PC 台账已更新。

**Final Decision（返工轮，lead）**：✅ 闸门2 重提交——9 票全交付+验证轮闭环+规格同步完成；**条件**：用户环境复验三项（手机送达/T12 分段行为/9a 极端值）随发布更新后复验，不通过则返工重提。闸门3 继续 hold（零 git）直至验收通过。
