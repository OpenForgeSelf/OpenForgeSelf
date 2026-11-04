# Plan

> 阶段：Stage 3｜Task ID：PILOT-079（2026-11-03-im-gateway-task-dispatch）
> 具体到真实文件；dsh-superagent 模式：T1/T2 可并行由不同 teammate 持有，写范围互斥。

## Files To Change

**T1 契约与派发服务（写范围：ForgeSelf.Abstractions + Plugins/AIAgent + 对应测试）**

- file: `ForgeSelf.Abstractions/AgentRunDispatchContracts.cs`（新）
  reason: FR-1+FR-9 契约落点（IAgentRunDispatch：SubmitAsync/FindAsync/ResumeAsync + DTO + 先例同款注释）。
- file: `Plugins/AIAgent/Services/AgentRunDispatchService.cs`（新）
  reason: FR-2 派发服务（Submit 即回/后台驱动 RunAsync/AgentRef 解析/同会话并发闸/Find 回退）。
- file: `Plugins/AIAgent/AIAgentPlugin.cs`
  reason: Apply 里 eager 构造 + ctx.Register<IAgentRunDispatch>（提供即 effect）。
- file: `Plugins/AIAgent/plugin.json`
  reason: 1.9.8 → 1.10.0（新契约）。
- file: `ForgeSelf.Api.Tests/Plugins/AIAgent/AgentRunDispatchTests.cs`（新）
  reason: AC1/AC2（XCodeTestFixture 模式，orchestrator 用接口替身）。

**T2 网关侧（写范围：Plugins/ImGateway/** + 对应测试）**

- file: `Plugins/ImGateway/Core/ImGatewayConfig.cs`
  reason: FR-6 WeCom.TaskGateway 配置块。
- file: `Plugins/ImGateway/Data/Entities/ImGatewayTask.cs` + `ImGatewayTask.Biz.cs`（新）
  reason: FR-4 绑定表实体（XCode 自动建表）。
- file: `Plugins/ImGateway/Data/ImGatewayTables.cs`
  reason: 登记新实体。
- file: `Plugins/ImGateway/Services/ImTaskDispatcher.cs`（新）
  reason: FR-3/FR-4 命令解析 + Submit + ack + 绑定落库 + 「任务状态」查询。
- file: `Plugins/ImGateway/Services/ImTaskWatcher.cs`（新）
  reason: FR-4 HostedService 轮询 + 终态主动回复 + 重启恢复 + 超时收尾。
- file: `Plugins/ImGateway/Services/ImGatewayRouter.cs`
  reason: FR-3 落库后加任务分支（Enabled 短路，约 +15 行）。
- file: `Plugins/ImGateway/ImGatewayPlugin.cs`
  reason: 注册 ImTaskWatcher HostedService（先例 ImGatewayConnectionHostedService）。
- file: `Plugins/ImGateway/plugin.json`
  reason: 2.1.1 → 2.2.0。
- file: `Plugins/ImGateway/Controllers/ImGatewayController.cs`
  reason: FR-8 模拟入站端点（e2e/集成的消息触发入口，复用类级 ApiKeyPolicy）。
- file: `Plugins/ImGateway/web/**`（仅当 FR-7 拍板含）
  reason: 设置面板「任务网关」小节（DESIGN.md 新建查询规范 + design:check 棘轮）。
- file: `ForgeSelf.Api.Tests/Plugins/ImGateway/ImTaskDispatchTests.cs`（新）
  reason: AC3+AC10（契约/审批服务用接口替身，不依赖 AIAgent）。

**T3 验证面（写范围：e2e + docs）**

- file: `ForgeSelf.Web/e2e/plugins/im-gateway/im-gateway.spec.ts`
  reason: AC5 +1 冒烟主链路（真实宿主零 mock）。
- file: `docs/ai/pilot/2026-11-03-im-gateway-task-dispatch/**`
  reason: 05/06/07 随实施补齐。
- file: `docs/01-architecture/host-capability-seams.md`
  reason: 第三接缝登记（IAgentRunDispatch，接缝台账既有格式）。
- file: `docs/02-features/031-im-gateway.md`
  reason: 补「任务网关」节（writeback）。

## Implementation Steps

1. T1a：写契约文件 + 契约单测（红）→ 实现派发服务至绿 → AIAgentPlugin 注册 → plugin.json bump（agent-dev）。
2. T2a：配置块 + 实体 + ImGatewayTables（im-dev，与 T1a 并行——只依赖契约文件存在，可先按 FR-1 签名写替身接口防编译断）。
3. T2b：ImTaskDispatcher 单测红→绿 → ImTaskWatcher 单测红→绿 → Router 接线 + simulate-inbound 端点 + Plugin 注册 → plugin.json bump（im-dev；依赖 T1 契约入库编译）。
4. T3：e2e 冒烟用例 + dev-stack 集成实测（AC4）→ 走查（im-test + lead 验收）。
5. T4：门禁复跑（中档全量）→ 工件 05/06/07 + 接缝登记/feature 文档回写 → 汇报闸门2（lead）。

## Test Plan

1. AC1/AC2：AgentRunDispatchTests（契约校验矩阵、解析三态、驱动终态、并发闸、RunId 回退）。
2. AC3：ImTaskDispatchTests（分支、绑定、ack、watcher 幂等、恢复）。
3. AC5/AC6：e2e/plugins/im-gateway（+1 冒烟 + 既有不红）。
4. AC4：dev-stack Invoke-RestMethod 实测（tasks-overview + 消息表证据）。

## Verification

### Build

```bash
cd ForgeSelf.Api && dotnet build
```

### Unit Test

```bash
cd ForgeSelf.Api.Tests && dotnet test --filter "FullyQualifiedName~AgentRunDispatch|FullyQualifiedName~ImTask"
```

### Integration Test

```bash
# dev-stack.ps1 起源码前后端 → Invoke-RestMethod tasks-overview + ImGateway 消息表核查（AC4）
```

### E2E

```bash
cd ForgeSelf.Web && npx playwright test e2e/plugins/im-gateway --project=chromium
```

### Other Checks

- 中档全量：`dotnet test`（新 Abstractions 契约=公共接口变更，§5.6 触发中档；对表基线 0 归本批）。
- 若动 web：`cd Plugins/ImGateway/web && pnpm run build` + `pnpm run design:check`（宿主侧跑）零新增。
- pre-commit 工件门禁（-EnforceInteraction）。

## Plan 偏差记录

| 时间 | 偏差点 | 原 Plan | 修正后 |
| --- | --- | --- | --- |
| 2026-11-03 | plugin.json 版本基线 | 1.9.8→1.10.0 | 仓库实际基线已是 1.9.9（PILOT-077 已 bump）→ 按 1.9.9→1.10.0，目标版本不变（be-dev 发现，lead 代填） |
| 2026-11-04 | FR-8 端点路由 | 规格文本 `/api/imgateway/...` | 实现取控制器类级真实路由 `api/im-gateway/simulate-inbound`（`[Route("api/im-gateway")]` 已定，改规格写法为偏差记录）（be-dev） |
| 2026-11-04 | xcode 重生成模板漂移 | 仅新增 ImGatewayTask 表 | xcode 全量重生成带入模板级改动：实体缓存 `1000`→`MaxCacheCount`、ImGateway.htm +194 行（新表文档），已归一 LF 并逐文件核对（be-dev） |
| 2026-11-04 | Watcher 发送失败语义 | FR-4「RepliedAt 未置，不自动重发」 | 实现：发送失败 → RecordError + 进程内 `_sendAttempted` 去重（本进程不重发，RepliedAt 保持未置；宿主重启后按 FR 重启恢复语义可再试一次）。机制替代但保「幂等不重发」不变量（be-dev，lead 可裁决） |
| 2026-11-04 | DbConfigStore 解析容错 | Plan 未明确损坏 JSON 行为 | TaskGatewayJson 损坏/空 → 回默认配置（Enabled=false 兜底）+ XTrace.Warn，不抛（与插件配置解析既有风格一致）（be-dev） || 2026-11-04 | Watcher RunId 回填缺失（qa 走查发现，lead 裁决修复） | FR-4 明文「Watcher 拿到 RunId 后回填」 | ProcessRowAsync 状态回写处同步回填 row.RunId（snapshot.RunId>0 且与行不一致时），随既有保存点持久化；新增用例从 RunId=0 起步验证（be-dev） || 2026-11-04 | IAgentRegistryService 俘虏依赖（qa e2e 首跑发现，lead 裁决方案 A 根修） | 原 AddScoped 被 4 个 eager scope（advisor/builtin/schedule/dispatch）捕获为插件寿命实例 | 改 AddSingleton（L47；并发字典线程安全，写路径走注册表本体）；同类扫查完成：MemorySystem/WorkflowEngine 同模式但被捕获图为无状态或自有状态服务，无视图分裂危害，判定不改；新增真 DI 容器类级守卫测试（Scoped 下红已实证 p079-d2-red.log，单例后绿）（be-dev） || 2026-11-04 | 多 provider 实例分裂（缺陷单 #3，qa AC4 实测 + be-dev 轻查根因） | AIAgentPlugin 4 个 eager 块各自 BuildServiceProvider + 宿主主 provider = 5 个 per-provider 单例注册表（各 DB 加载、独立字典）；IAgentRegistry 工厂独立 provider 为第 6 份 | 中间路径根修：① 4 eager 块共享同一 pluginProvider+eagerScope（IAgentRegistry 工厂一并并入）；② eager 产出 5 契约以实例单例注册回 services（last-wins）→ 宿主主 provider 与 ctx.Get 同实例=全局单视图；③ 守卫升级：跨 provider 断言（eager==宿主==请求作用域）Red 实证（p079-d3-red.log）→ Green（p079-d3-std.log 4/4）；④ IChatCompletion 同款模式核实不动码：RegisterChatCompletion 本就实例注册，CompleteAsync 每请求 CreateAsyncScope 重读宿主契约/DB，无跨视图可变状态俘获，判定无危害（be-dev） || 2026-11-04 | T13 全仓清扫：插件侧 ctx 本体裸 CreateScope（PC-005 二次复发，plan_workflow 实证） | 工具/服务构造存 Apply 传入的 ctx，请求期裸 CreateScope → IServiceScopeFactory 未注册；2026-10-17 治理逐案修未穷举 | 20 处/11 文件统一迁 PluginContextDiExtensions.CreateHostScope（AIAgent plan_workflow/RecordUsageAsync 静默吞错扩展/DevTools/ScriptRunner×5/CodeSnippet×2/ScriptRunnerTools×3/SystemMonitor×5/MemorySystem/WorkflowExecutor×2/Sems 手工版收敛）；ToolRegistry（宿主真容器）豁免登记；新守卫 PluginRawCreateScopeGuardTests（源码扫描+双向阳性对照）+ case 归档 2026-11-04-raw-createscope-on-ctx-sweep（be-dev） |
| 2026-11-04 | T14 stuck 语义校正+阈值 | RunOrchestratorService：无进展阈值 45s→**300s 默认 + `AIAgent_StepTimeoutSeconds` 环境变量可配（10..7200 clamp）**；挂起原因三分类（**等待审批**≠无进展超时≠未声明完成，`ComposeSuspendReason` + `PendingApprovalProbe` 生产走 ctx 懒解析 IApprovalService）；TasksOverview stuck 行下发 **CancelUrl**（非终态可取消，CancelRun 端点 L247 已存在仅 DTO 未下发）；AgentOptions.StepTimeout 默认同步 300s。用户裁定（验收#7+#8）：45s 太短不够大部分任务执行 | be-dev |
| 2026-11-04 | T10-A 终态结果投影缺陷 | 根因三层：① `ProjectResultText` 优先 StuckReason（completed 态也中招）② `FinalizeRunAsync` 完成不清 `run.StuckReason` ③ `FreezeReceipt` write-once 挡住终局收据（stuck 期收据永久占位）。修复：completed 投影只取收据 summary（脏收据宁可空）；完成清 StuckReason；**中间态收据允许被 Completed 终局覆盖（终局→终局仍 write-once）**。现象：用户收到「✅ 任务 #12 完成\n步骤执行超过 45s 挂起…」坏内容 | be-dev |
| 2026-11-04 | T10-B 审批关键字「1」误放（判定：非缺陷） | 实例配置 `approveKeywords:["同意","批准","1"]`（用户自加数字快捷键），`StartsWith` 前缀匹配行为符合配置——「1」=同意属**知情配置行为**。处置：审批卡回显当前词表（同意/拒绝两组全量），防「不知为何被吞/没反应」；匹配逻辑不改（配置语义清晰） | be-dev |
| 2026-11-04 | T10 观测盲区补日志 | Watcher RunLoop 零日志（运行实例上无法判断 Watcher 是否在跑）：补「轮询已启动（间隔 Ns）」「本轮扫描未回执绑定 N 行（Debug）」「已推送任务回复 taskKey/RunId/状态（Info）」三点位 | be-dev |
| 2026-11-04 | T16 执行状态轮询节拍与进展推送（用户拍板验收#8） | 执行状态轮询节拍与进展推送 | PollIntervalSeconds 默认 60s（fallback 60）；快照变化即推进展（ProgressPushEnabled 子开关默认开、首轮只建基线防重启轰炸）；StalledMinutes 默认 5 分钟无变化才通知（对齐 T14 300s）+行级幂等去重；终态推送不受节拍影响；Snapshot 契约新增 CurrentStepIndex/StepCount（可空非破坏）+dispatch step 事件解析；ImGateway 补 InternalsVisibleTo | be-dev |
| 2026-11-04 | T12 回复超长分段发送（用户拍板验收#5） | 回复超长分段发送 | ComposeTerminalText 取消截断返回全文；MaxCharsPerMessage 默认 2000（工单建议放 WeComChannel 块，落在 TaskGatewayConfig——Watcher/Dispatcher 侧无通道配置访问点，通道差异分段待微信通道票再做）；MaxReplyChars 默认 1500→6000 改总保险丝（0=关）；分段中途失败语义=全量重发非续发（零断点状态，续发需持久化进度属后续票）；段间顺序保证（await 顺序不并发）；031 文档配置表同步 | be-dev |