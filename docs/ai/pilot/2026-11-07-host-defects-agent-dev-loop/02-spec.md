# Spec

> Task ID: PILOT-079｜缺陷批修复规格（7 项）。根因全部经两路只读调查子代理定位 + 关键现场亲核（file:line 见各节「根因」）。不确定点显式标 `Unknown`。

## 1. 背景
:51888 实例插件开发演示暴露 7 缺陷（输入7 分级，输入8 拍板全修）。P1②/P3② 命中 PC-013（第 3 族例/第 2 次复发，须逃逸分析）；P1① 命中 PC-005 同族（同错误串，canonical fix 已存在）；其余为新类候选。

## 2. 目标
七项缺陷全部带红灯测试修复；命中 PC 的按 problem-class-closure 闭环（守卫补强+逃逸分析+归档）；修复后本地实例上 Agent 能自主完成插件开发全流程（含自跑构建）。

## 3. 范围
**Allowed**：`Plugins/SmartTerminal/**`、`Plugins/McpCenter/Services/PublicTools/**`、`Plugins/AIAgent/**`（MafTaskLoopAgent/Controllers/Services）、`Plugins/Scheduler/**`、`ForgeSelf.Api.Tests/**`（含 Architecture 守卫）、`docs/11-troubleshooting/**`、pilot 工件。
**Forbidden**：`ForgeSelf.Web/**` 与插件 `web/**`（本批零 UI 改动）；`:51888` 实例及其进程；`ForgeSetting`/鉴权核心/DB 结构；git 提交/发布。

## 4. 功能需求（逐项规格）

### P1① SmartTerminal DI 全灭（PC-005 同族）
- 根因【V】：`SmartTerminalPlugin.RegisterTools`(:60-64) 把 Apply 传入的 `IContext` 当 `IServiceProvider` 注入 8 个 st_* 工具，各工具请求期 `_sp.CreateScope()`（SmartTerminalToolFunctions.cs:35/76/116/155/177/209/250/289）→ `No service for type 'IServiceScopeFactory'`。
- 规格：8 处改 `_sp.CreateHostScope()` + `using ForgeSelf.Abstractions;`；8 个 st_* 工具全链路恢复正常（st_list_shells 可列、st_run_command 可执行）。
- 守卫补强（G2）：`PluginRawCreateScopeGuardTests.cs:28-30` 正则扩为 `(_sp|_serviceProvider|_provider|_ctx|_context|serviceProvider|provider\?)\s*\.\s*CreateScope\s*\(`，新增 `_sp` 形态阳性对照锚点（SmartTerminal 修复后即锚点）；同类扫查结论（FileToolsPlugin.cs:51 传入但未消费，潜在地雷）记入归档非本批改动。

### P2① run_terminal_command 30s 硬上限 + 禁重定向 + 超时丢输出（mcp-center）
- 根因【V】：`TerminalCommandGuard.MaxTimeoutSeconds=30`(:40)；`TrySplitSegments`(:193-195) 引号外 `>` `<` 反引号整串拒、`&`(:206-209) 除 `&&` 外整串拒（**连带拦掉 `2>&1`**）；`RunTerminalCommandTool`(:136-137) 钳制 [1,30]；超时分支(:205-212)丢弃全部已捕获输出。
- 规格：
  1. 上限放宽：`DefaultTimeoutSeconds=300`、`MaxTimeoutSeconds=1800`（守卫常量，工具钳制与 schema/描述同步）；显式可配置延后（记 TODO，本批不做——McpCenter 网关配置面 DTO+Controller+Vue 改动超范围）。
  2. 重定向放行（V1 面）：引号外 `>` `>>` 允许（支持前置 fd `1>`/`2>`；`2>&1` 合并形态允许）；`<` 与反引号**仍拒绝**；裸 `&`（非 `&&`/`>&1`）仍拒绝；分段校验语义不变（每段过白名单+破坏性红线，重定向原文参与扫描）。
  3. 执行路由：含重定向的命令（单段或多段）一律经 `pwsh -NoProfile -NonInteractive -Command` 执行（pwsh 原生处理重定向；ArgumentList 转义不变）；无重定向多段/单段行为零变化。
  4. 超时/取消响应**带回部分输出**：Kill 后等捕获任务收尾（宽限 5s，失败/故障侧按空处理），超时信封新增 `stdout`/`stderr`/`stdoutTruncated`/`stderrTruncated` 字段（取消路径同样带回）；解码仍走 `ProcessOutputText`（PC-011 不变量不动）。
  5. 既有测试更新：ChainExecutionTests 中「重定向拒绝」断言改为「重定向放行且走 pwsh」；补钳制边界、`2>&1`、超时带部分输出用例。

### P1② 上游空流静默 Suspended（PC-013 复发批 · 逃逸=聊天流式路径）
- 根因【V】：`MafTaskLoopAgent` TimedOut 分支(:416-423)先 break，空响应守卫(:449)不可达 → 零字节超时落 `Suspended` → SSE done 帧（turnEndReason=Suspended, usage=null）→ 前端当正常完成。规格偏离 2026-10-29 02-spec:24（空流→Failed）。
- 规格（对齐 PC-013 不变量 2）：**零字节空流 + 无工具调用 + 超时 ⇒ 走 Failed 路径**（TurnFailed 帧 → SSE error 帧 → 持久化失败态）；已有部分产出（HasText||HasToolCall）的超时仍走 Suspended 可续跑。守卫测试拆两例：`零字节超时→TurnFailed`、`部分产出超时→Suspended 可续`；同步改固化旧行为的 `RequestStall_TurnSuspended_KeepsScene`。
- 逃逸分析（强制，PC-013 第 2 次复发）：10-25 修任务环路径、10-30 修 IM 入口路径，聊天流式（MafTaskLoopAgent+AIChatController）是第三条路径；本批落 G2 守卫测试于该路径后，评估是否需抽象共享判定（见 07 提案节）。

### P2② 审批 Decide 不唤醒
- 根因【V】：`ApprovalsController.Decide`(:46-59) 仅写状态；续跑消费依赖下次回合启动（`ConsumeContinuationsAsync`），唤醒门槛(:171-178)无输入即 `yield break`。
- 规格：Decide 成功后（Approved/Denied 均算）：会话回合未运行 → `Inbox.Followup(sessionId, 审批结果文案)` 唤醒 + 后台消费 `RunAsync`（不阻塞 HTTP、异常落日志）；回合运行中 → 不投喂，响应体注明「回合运行中，将随下一回合消费」。防双跑复用 AIChatController 并发闸门同款判定。幂等：非 Pending 决策不唤醒。

### P2③ 调度器 Once 四缺陷
- 根因【V】（纠正输入7 前提——扫描本身支持过期补触发，无新鲜度下界）：
  1. runAt 缺省/不可解析 → `NextRunTime=MinValue` → 永久排除且无报错（僵尸）；update 未带 runAt 同样抹成 MinValue。
  2. runAt 无 UTC 归一，按 UtcNow 比较 → 本地墙钟填法晚约 8h。
  3. Skip（inflight/会话已有未结束 Run）仍推进 LastRunTime/RunCount/Once→Disabled —— Once 被空耗。
  4. 工具面仅 create/list/pause，`DeleteTaskAsync` 无暴露。
- 规格：
  1. create/update：`scheduleType==Once ⇒ runAt 必填、可解析、> now`，否则工具返回 success=false + 中文原因（含时区提示）。
  2. 归一：能解析出 offset 的按 `DateTimeOffset→UtcDateTime`；无 offset 的按 UTC 处理并在工具描述中明示「runAt 传 UTC 或带时区偏移」。
  3. 僵尸守卫：创建/更新后 `Enabled && NextRunTime==MinValue` ⇒ 拒绝（不留静默僵尸）。
  4. Skip 不消耗：trigger 返回 Skip ⇒ 跳过推进块（Once 不禁用、不记 LastRunTime），打显式日志（含跳过原因），下轮重试。
  5. 新增 `scheduler.tool.delete_scheduled_task` 工具（仿 pause 模板，调既有 `DeleteTaskAsync`，级联日志删除语义不变）。

### P3① 长会话上下文溢出无自愈
- 根因【V】：每步全量重放 `DeriveMessages`（:269/:287/:297）无预算；上游 400 → TurnFailed → SSE error 直透原始报文；历史不变 ⇒ 永久 400。
- 规格（请求级视图裁剪，不动存储）：每步组消息前按预算裁剪（预算=可配上限默认 200k tokens − 余量；估算=最近 Usage.PromptTokens + 新增字符/4，无 Usage 用字符/4 兜底）；从旧到新丢**中间**历史，保系统指令、最近 N 条、工具调用/审批成对消息不拆散；每次裁剪记显式 turn 事件（禁静默）。上游 400 且报文含 context length/maximum context 类关键字 ⇒ 裁剪后自动重试一次（同样显式记事件）。仍溢出则 TurnFailed，错误文案改写为用户可读指引（不再是原始英文报错裸透）。

### P3② 模型路由无故障转移
- 根因【V】：`ChatModelResolver` 解析只选一次零健康检查；上游异常/空流直透（与 P1② 同故障两观察面）。
- 规格（对齐 `RunOrchestratorService:194` 禁静默约束）：仅当**零 chunk 失败**（尚未产出任何 delta）⇒ 按目录取下一启用模型重试一次；每次降级 XTrace.Warn + turn 事件「模型 {A} 上游失败({原因})，本次降级使用 {B}」；单请求最多降级一次；已吐过 delta 不换（防撕裂工具调用连续性）；目录仅 1 模型时行为不变；usage 按实际成功模型计。

## 5. 非功能需求
- 零新增 NuGet 依赖；警告不超基线 474；新代码 PC-027（ForgeJson Encoder）与 PC-011（输出解码单点）不变量不破坏。
- 审批自动放行仅限本地 dev-stack 实例配置，不进本批代码改动（运行时配置）。

## 6. 交互设计（无界面交互——本批零 UI 改动，按模板必写节）
全部行为变化经工具/HTTP 观测：
- `st_*` 8 工具：入参示例 `{pluginId:"smart-terminal"}` 各工具原 schema 不变；修复前必返 `Fail("No service for type 'IServiceScopeFactory'...")`，修复后正常返回（st_list_shells 返回 shell 列表 JSON；st_run_command 返回 {exitCode, stdout, stderr}）。错误提示不再出现 DI 异常串。
- `mcp-center.run_terminal_command`：schema 变更点——timeoutSeconds 描述改「1-1800，默认300」；command 描述移除「不得含重定向>」，新增「支持 > >> 2>&1（经 pwsh 执行）；不支持 < 与反引号」；返回体新增超时/取消场景的 stdout/stderr 字段。错误提示示例：`Error: ... (contains '<')`（保持既有拒绝文案格式）。
- 审批链：POST `/api/ai-agent/approvals/{id}/decide` 响应新增/沿用 DTO + `resumed`（bool，是否已触发续跑）与 `resumeNote`（回合运行中说明）字段（**具体字段名以实现为准，记 05-evidence 时回填**）。SSE 侧：审批唤醒后用户会看到回合继续产出（无需手动再发消息）。
- 调度工具：`create_scheduled_task`/`update_scheduled_task` 对非法 runAt 返回 `success=false, error="runAt 必填且须晚于当前时间（UTC 或带时区偏移），如 2026-11-08T02:00:00+08:00"`；新工具 `delete_scheduled_task` 入参 `{id}`，成功返回 `{success:true}`，失败报不存在。
- 上下文溢出：SSE error 帧文案变为「会话历史已超出模型上下文上限，已自动裁剪重试；若仍失败请开启新会话」（裁剪重试成功则正常 done 帧且事件流含裁剪事件）。
- 模型降级：用户无感路径不变；事件流可见降级事件；上游全灭时 error 帧含末次真实原因。

## 7. 边界与异常
- P2①：重定向目标路径不做额外限制（终端工具本可写文件；破坏性红线扫描覆盖重定向段原文）；`>>` 追加、`>` 截断语义由 pwsh 保证。
- P1②：真实网络中途 stall（已有部分产出）不得误判 Failed。
- P2②：Decide 与用户同时发消息的并发——以「回合未运行」为准，运行中即不投喂。
- P2③：不给扫描加新鲜度窗口（保留 catch-up 语义，除非显式决策）；Cron/Interval 行为零变化。
- P3①：成对消息不可拆散否则上游再报错；裁剪是请求视图非存储变更。

## 8. 验收标准
- AC1 每项缺陷红灯→绿灯证据（05-evidence Verified，测试名可追溯）。
- AC2 `dotnet build` 0 错误；全量 `dotnet test` 对基线新增红=0；`check-problem-classes.ps1` 绿。
- AC3 P1①/P2① 合入后：宿主 Agent 经 run_terminal_command 跑通 `dotnet build`（>30s 命令不再假失败）与重定向构建日志落盘。
- AC4 本地实例（dev-stack）审批自动放行下，Agent 自主开发新插件全流程闭环（含 Agent 自跑构建）——本批修复的集成验证。
- AC5 PC-005/PC-013 归档更新含逃逸分析；新问题类（P2②/P2③/P3①）按需定级归档。

## 9. Unknown
- U1 审批唤醒的 `IAgentRegistry`/RunAsync 消费面在 Controller 层的可注入性（实现时定，影响 P2② 具体落点——Controller 级 or 服务级）。
- U2 模型目录「下一启用模型」的稳定排序来源（GetModelCatalogAsync 顺序即目录顺序，沿用）。
- U3 P3① 预算可配上限的配置落点（先常量，配置化记 TODO）。
