# OpenForgeSelf 向 DeepSeek Harness 架构对齐：改造方案

> 功能编号：dsh 架构对齐专项（040–042）· 总纲
> 状态：方案已定稿（2026-09-27）
> **⭐ 已于 2026-09-29 全部实施完毕（B1–B9 八批次收官），实施终态与验证数字见 [`03-plan.md`](03-plan.md)；本文保留为定稿方案存档。**
> 最后更新：2026-09-29
> 关联：施工总览 [`03-plan.md`](03-plan.md)；逐批分解 [`04-task.md`](04-task.md)；阶段一/二/三详细设计原稿位于 `specs/040|041|042`（仓库内已被 `.gitignore` 忽略、不入库、逐渐废弃，要点已摘要于本文 §4–§6）；对标调研 [`docs/06-research/003-deepseek-harness-运行全链路.md`](../../../06-research/003-deepseek-harness-运行全链路.md)

> 依据：仓库 `github.com/OpenForgeSelf/OpenForgeSelf`（main 分支）实读代码 + 你的 `docs/06-research/001` 调研、`07-decisions/001` ADR、`02-features/027-cordis-kernel` 档案。
> **提示**：以下文件行号为代码快照定位，实施前请在本地二次确认。

---

## 0. 结论先行

**你的插件层已经对齐 dsh 的 ~70%，而这恰恰是最难的部分（`ForgeSelf.Core` 内核、可逆副作用、能力接缝、独立程序集、热重载、动态端点）——这部分你已经啃下来了。**

剩下的差距**不在插件，在运行时**。压缩成一句话：

> **会话日志现在是个"旁路影子"，必须把它升格为唯一真相源。这一条不动，dsh 的所有特性（replay / resume / fork / trajectory / pre-step 拦截 / 不变量断言）都无法落地，因为你永远无法从日志重建"模型到底看到了什么"。**

现在的证据链：

| 现状 | 位置 | 问题 |
|---|---|---|
| `ChatController` 写 `ChatMessage` 表，**旁路** `_sessionStore?.Append(...)` | `ForgeSelf.Api/Controllers/ChatController.cs` | 事件日志不是真相源，是影子副本 |
| `InMemorySessionStore` 纯内存 `ConcurrentDictionary` | `ForgeSelf.Api/Services/InMemorySessionStore.cs` | 进程重启即失忆，replay 无从谈起 |
| `SessionEvent{Type: string, Payload: string}` | `ForgeSelf.Abstractions/ISessionStore.cs` | 裸字符串，无法编译期穷举，无法强制"可见即已记录" |
| 主循环是 `for (iteration = 0; iteration < 10; iteration++)` | `Plugins/AIAgent/Services/AIAgentService.cs` | 无 turn 对象、无取消原因、无 inbox 消费 |

一句话：**形似已经在（SessionEvent / Append / Replay / DeriveMessages 的名字全对），神不至（没人把它当权威）。**

---

## 1. 对齐度体检表

| dsh 层次 | dsh 实现 | OpenForgeSelf 现状 | 完成度 | 判词 |
|---|---|---|---|---|
| 插件内核 | Cordis（Context/Service/Fiber/effect） | `ForgeSelf.Core`：IContext/Fiber/Disposable/Service + 宿主 MS DI 桥 | **100%** | 已对齐，Scoped 语义近似（027 待办 1）已闭环 |
| 事件总线 | emit / waterfall / parallel / serial | `IEventBus.cs` 四种模式齐备 + 父子冒泡跨上下文 | **100%** | 027 待办 5（方案 B）已落地，跨上下文传播打通 |
| 能力接缝 | Definition / Provider / Consumer 三段式 | `ILlmRuntime`/`ISessionStore`/`IAgent`/`IInbox`/`IConfigurationService`… | **100%** | 契约 + 真实 Provider 齐备（`IAgentLoop` 已删，改 `IAgent`/`IAgentRegistry`；`IInbox` 已持久化） |
| 会话日志 | append-only SessionEvent + 事件 map | `ISessionStore.Append/Replay/DeriveMessages` + 全量重投影 | **100%** | 已落地为唯一真相源（B1–B4 收官） |
| Turn/Step 运行时 | turn/step 状态机 + 持久 inbox | `IAgent.RunAsync` + `ReactLoopAgent` + 持久 `IInbox` | **100%** | B5–B7 收官，`ReactLoopAgent` 驱动 turn/step |
| 工具流水线 | pre-execute→守卫→execute→post-execute→finalize→result | 六闸门全链路（`ToolRegistry` + `ToolGuardRegistry`） | **100%** | 三态 + waterfall 修正 + skipped + spill 全落地（B8） |
| 模型provider seams | 数十家适配器可换 | 已有 AIProvider / 多模型接入 | **100%** | 够用，持续演进 |
| 前端清单驱动 | 插件清单动态挂载 | `pluginManifest` + `dynamicPlugins.ts` | **100%** | 已落地，优于多数同类项目 |

> 已全部实施完毕（**2026-09-29，B1–B9 八批次收官**），总体 **100%**。原瓶颈「会话日志地位」「Turn/Step 运行时」已由 040/041 专项闭环，二者因果关系在改造中验证成立。

---

## 2. 三个真问题（按因果排序）

### 根因 1：日志是影子，不是真相（`①`必先解决）

`ChatMessage` 表才是写路径，`SessionEvent` 只是旁路。**这直接违背 dsh 第一不变量 Model-visible means logged。** 后果是连锁的：

- 无法 replay / resume / fork → 聊天记录详情页只能展示"当时存了什么"，不能展示"模型当时看到了什么"；
- 无法做运行时断言（"任何进模型的消息都能从日志重建"这条测试根本没法写，因为进模型的消息来自另一条表）；
- `pre-step` 拦截无处下手——拦截点要改写的是**即将发出的请求**，而请求根本不是从日志派生的。

### 根因 2：`Type`/`Payload` 是裸字符串

`string` 类型无法编译期穷举。新增一类事件时，编译器不会提醒你去更新 `DeriveMessages` 的 switch——**静默漏投影**就是这么发生的。dsh 用 TypeScript 的 `SessionEventMap`（类型级 map + 运行时断言）双保险；C# 里对应做法是**可辨识联合（record 层级）+ 注册表 + 运行时校验**。

### 根因 3：事件总线不跨上下文（027 待办 5，已从"待修"升级为"阻塞项"）

`Context.cs` 每个实例 `new EventBus()`，每个插件 Fiber 一张独立的 handler 表。**插件 A emit 的事件，插件 B 永远收不到。** 现在只有平台级单例总线接线了 `tools/*`。

这条原本在 027 里被判为"视需求再动"。**现在需求来了**：dsh 的 `agent/pre-step`、`agent/request`、`llm/stream` 全部是跨插件 waterfall——拦截点在 A 插件，主循环在 B 插件。总线不通，阶段二根本无法开工。

---

## 3. 改造总纲：一条主线 + 三个阶段

**主线**：`一切进模型的东西，都必须先落日志，再从日志派生出来。`

| 阶段 | 主题 | 产出 | 前置 |
|---|---|---|---|
| **一** | 地基校正：日志称王 | SessionEvent 联合化 + 写路径改序 + 持久化 + 事件冒泡 + 不变量测试 | 无（立即可做） |
| **二** | 运行时：Turn/Step 状态机 | IAgent 句柄 + turn/step 事件 + 持久 inbox + 取消传播 | 阶段一完成 |
| **三** | 工具管线与 seams 补全 | 三态决策 + 单调守卫 + waterfall 修正 + usage 回流 | 阶段二 Loop 成型 |

三个阶段**只能串行**（因果依赖），但每阶段内部可并行多个子任务，且每阶段都有独立可验证的门禁——符合你的"离散評価"习惯。

---

## 4. 阶段一：让会话日志成为唯一真相源

> 目标：把"`Append` 是顺手的事"变成"`Append` 是唯一的路"。

### 4.1 `SessionEvent` 联合化

把 `{Type: string, Payload: string}` 换成抽象 record 层级：

```csharp
// ForgeSelf.Abstractions/SessionEvents.cs
public abstract record SessionEvent(long Id, string SessionId, DateTimeOffset Timestamp);

public sealed record UserMessageEvent(long Id, string SessionId, DateTimeOffset Timestamp,
    string Content, MessageSource Source) : SessionEvent(Id, SessionId, Timestamp);

public sealed record AssistantMessageEvent(long Id, string SessionId, DateTimeOffset Timestamp,
    string Content, long? PromptTokens, long? CompletionTokens, string FinishReason) : SessionEvent(...);

public sealed record AssistantAttemptEvent(long Id, string SessionId, DateTimeOffset Timestamp,
    string Content, string ErrorKind) : SessionEvent(...);   // 失败/重试/取消

public sealed record ToolCallEvent(long Id, string SessionId, DateTimeOffset Timestamp,
    string CallId, string ToolName, string ArgsJson, ToolOutcome Outcome) : SessionEvent(...);

public sealed record RequestHeaderEvent(long Id, string SessionId, DateTimeOffset Timestamp,
    string ModelId, string RenderedSystemPrompt, IReadOnlyList<string> ToolSchemas) : SessionEvent(...);
```

配一张 **SessionEventMap + 运行时校验**：

```csharp
public static class SessionEventMap
{
    private static readonly Dictionary<string, Type> _known = new() {
        ["user/message"] = typeof(UserMessageEvent),
        ["assistant/message"] = typeof(AssistantMessageEvent),
        ["assistant/attempt"] = typeof(AssistantAttemptEvent),
        ["tool/call"] = typeof(ToolCallEvent),
        ["tool/result"] = typeof(ToolResultEvent),
        ["system/message"] = typeof(SystemMessageEvent),
        ["request/header"] = typeof(RequestHeaderEvent),
        ["turn/start"] = typeof(TurnStartEvent), ["turn/end"] = typeof(TurnEndEvent),
        ["step/start"] = typeof(StepStartEvent), ["step/end"] = typeof(StepEndEvent),
        ["agent/inbox/spliced"] = typeof(InboxSplicedEvent),
    };
    public static void EnsureKnown(string type) { if (!_known.ContainsKey(type)) throw new UnknownSessionEventException(type); }
}
```

`ISessionStore.Append` 第一行就 `SessionEventMap.EnsureKnown(evt.Type)`——**没注册的类型不允许进日志**。

> 为什么这步是根因级：类型一旦联合化，`DeriveMessages` 的 switch 就变成 `switch (evt) { case UserMessageEvent u: ... }` 加 `_ => throw`，编译器会逼你处理新类型；再叠运行时 map 校验，漏投影在开发期就会被炸出来。这正是 dsh 那个运行时断言的 C# 等价物。

### 4.2 写路径改序（这一步是全案的灵魂）

**现在**：

```
POST /api/chat/stream
  → SaveMessageAsync(user)                  ← 真相写入发生在这
  → provider.ChatStreamAsync(messages)      ← messages 来自内存 List<AIChatMessage>
  → _sessionStore?.Append(...)              ← 影子副本（可选依赖，问号那个 ? 就是证据）
  → SaveMessageAsync(assistant)
```

**改后**：

```
POST /api/chat/stream
  → inbox.Followup(sessionId, text)                  → Append(user/message)  【唯一写路径】
  → agent = await agents.GetOrCreateAsync(sessionId)
  → await foreach (frame in agent.RunAsync(ct))
        loop 内部：messages ← sessionStore.DeriveMessages(sessionId)   【模型输入只从日志来】
  → ChatMessage 表降级为只读投影（供列表/搜索/导出），不再承担真相
```

`AIChatController.cs` 末尾那句 `SaveMessageAsync(sessionId, "assistant", fullResponse...)` 要改成**投影同步器**：由 `ISessionStore` 的 append 触发更新，而不是业务代码双写。

> **判断标准**：改造完成的标志是——你在 `ISessionStore` 里塞一次坏数据，模型就一定看到坏数据。如果还存在"表里有、日志没有"的消息，就没改完。

### 4.3 持久化

`InMemorySessionStore` 必须有个落盘实现。你项目里已经是 Sqlite + XCode 生态：

- 一张 `SessionEvent` 表（`Id` 自增、`SessionId` 索引、`Ts`、`Type`、`PayloadJson`）；
- **append-only 语义靠仓储层强制**：只暴露 `Append`，不提供 Update/Delete；
- `Replay(sessionId)` = `WHERE SessionId=? ORDER BY Id`；
- `InMemorySessionStore` 保留作为测试替身/**退化 fallback**，二者共用契约测试（你已有 `SessionStoreContractTests`，正好扩抽象基类）。

### 4.4 解锁事件总线跨上下文（027 待办 5，方案 B）

你自己的 027 已经做了选型，推荐 B。确认执行，且**把它提到阶段一做**，理由是阶段二的 waterfall 依赖它：

- `EventBus` 加 `private readonly EventBus? _parent` + `EventBus(EventBus? parent)` 构造；
- `EmitAsync`/`ParallelAsync`/`SerialAsync`/`WaterfallAsync` 派发完自身后递归 `_parent`（父指针只朝根方向，天然无环）；
- `Context(Context? parent)` 构造时把 parent 的 `_events` 传进去，`Fiber` 无需改动；
- **`IEventBus` 公开签名不变**，现有 `tools/*` 接线零修改（这点你的方案已经考虑到，很关键）。

四条回归测试（你文档里已列）：父收子 emit / 父收子 serial 短路 / `tools/*` 回归 / 多层嵌套无环。

### 4.5 不变量成为测试

在你的 `10-testing/strategy.md` 铁律里加一条，并落成测试：

```csharp
[Fact]
public async Task 任何进入模型的消息都能从日志重建()
{
    // 用假的 ILlmRuntime 捕获 StreamAsync 收到的 messages
    // 断言：messages 每一项都能在 sessionStore.Replay(id) 中找到对应事件
}
```

这条测试一旦常绿，**根因 1 永远无法回归**。

### 阶段一改动清单

| 文件 | 动作 | 类型 |
|---|---|---|
| `ForgeSelf.Abstractions/ISessionStore.cs` | `SessionEvent` 改抽象 record + 子类型；新增 `SessionEventMap` | 破坏性 |
| `ForgeSelf.Abstractions/SessionEvents.cs` | 新增：事件 record 层级 | 新增 |
| `ForgeSelf.Api/Services/InMemorySessionStore.cs` | 适配联合类型 + `EnsureKnown` 校验 | 改 |
| `ForgeSelf.Api/Services/PersistentSessionStore.cs` | 新增：XCode/Sqlite 落盘实现 | 新增 |
| `ForgeSelf.Api/Controllers/ChatController.cs` | 写路径改序：先 Append 再派生；`SaveMessage` 降为投影同步 | 改 |
| `Plugins/AIAgent/Controllers/AIChatController.cs` | 同上，去掉末尾双写 | 改 |
| `ForgeSelf.Core/EventBus.cs` | 加 parent 指针 + 冒泡 | 改 |
| `ForgeSelf.Core/Context.cs` | 派生时传父总线 | 改 |
| `ForgeSelf.Core.Tests/EventBusTests.cs` | 补 4 例冒泡/无环 | 改 |
| `ForgeSelf.Api.Tests/`（或 Core.Tests） | 补「可见即已记录」不变量测试 | 新增 |

**门禁**：① `dotnet test` 全绿且无回退（基线 988/988 + Core 12/12 + Abstractions 13/13）；② 不变量测试常绿；③ 手工验证——杀进程重启后 `/session/{id}` 能完整回放出与重启前**逐字一致**的模型历史。

---

## 5. 阶段二：Turn/Step 运行时

> 目标：把 `for (int iteration = 0; iteration < 10; iteration++)` 换成有身份、有状态、可取消、可恢复的状态机。

### 5.1 `IAgent` 句柄（对标 dsh `core/agent`）

现在 `IAgentLoop.RunAsync` 是"一次性异步流"，没有对象身份，所以无从谈 status、cancel、whenIdle。改成两段：

```csharp
public interface IAgent
{
    string SessionId { get; }
    AgentOptions Options { get; }
    IInbox Inbox { get; }
    AgentStatus Status { get; }            // Idle | Running
    IAsyncEnumerable<TurnFrame> RunAsync(CancellationToken ct = default);
    Task CancelAsync(AgentCancelCause cause, bool keepInbox = false);
    Task WhenIdleAsync(CancellationToken ct = default);
}

public interface IAgentRegistry                     // 对标 ctx.agents
{
    Task<IAgent> GetOrCreateAsync(string sessionId, AgentOptions? options = null);
    Task<IAgent?> TryGetAsync(string sessionId);
    Task<bool> DisposeAsync(string sessionId);      // disposer 即能力
}
```

`IAgentLoop` 退居 algorithm詳細（对标 dsh 的 `core/agent-loop` 是"接口的一个实现"）——**插件只依赖 `IAgent`/`IAgentRegistry`，从不依赖具体 loop**。这条正是 dsh "没有特权核心"的关键落点，你现在的 `IAgentLoop` 接口名容易让人直接依赖实现，建议改名或明确划到内部。

### 5.2 turn/step 事件与状态机

`TurnEvent{Phase: string, Payload: string, Sequence}` 要换成 typed stream frame：

```csharp
public abstract record TurnFrame;
public sealed record TurnStarted(string TurnId) : TurnFrame;
public sealed record Preparing(IReadOnlyList<SessionEvent> ClaimedInput) : TurnFrame;
public sealed record StepStarted(string StepId) : TurnFrame;
public sealed record AssistantDelta(string Content) : TurnFrame;        // 对应 UI 流式
public sealed record ToolStarted(string CallId, string ToolName, string ArgsJson) : TurnFrame;
public sealed record ToolCompleted(string CallId, ToolExecutionResult Result) : TurnFrame;
public sealed record StepCompleted(string StepId, string Reason) : TurnFrame;
public sealed record TurnCompleted(TurnEndReason Reason) : TurnFrame;   // Completed | MaxTokens | Aborted | NoInput
public sealed record TurnFailed(string StepId, Exception Error) : TurnFrame;
```

主链路事件序列（照抄 dsh，落到 foreach 里）：

```
turn/start → claim(inbox) → assemble → agent/pre-step(waterfall)
   → step/start → 【Append user/message】→ request/header → agent/request(waterfall)
   → llm/stream → assistant/message | assistant/attempt
   → tool/call* → 工具调度 → tool/result*
   → step/end → 还欠请求？→ 下一 step
→ agent/turn-stopping(serial) → turn/end
```

三条容易漏但很重要的规则：

1. **max-tokens 粘滞**：turn 内任一 step 触顶 → **整个 turn** 记 `MaxTokens`（dsh 原样规则）；
1. **取消期间的原子性**：`agent/request` 与准备调用的异步阶段内被取消，`system/message` 与 `user/message` **都不落盘**；
1. **重试不重复组装**：重试只同步对账同一份已渲染的 assembly，不在 loop 里重跑 `pre-step`。

### 5.3 Inbox 持久化 + 三通道真正生效

现在 `IInbox`（`Followup/Steer/Inject` 三个 void）是死的——agent 循环根本没消费它。改成：

```csharp
public interface IInbox
{
    void Send(InboxMessage message, InboxTarget target, bool wakeup);   // 底层统一
    InboxBatch Claim(string sessionId, bool atTurnBoundary);
    IReadOnlyList<InboxMessage> Peek(string sessionId);
}
public enum InboxTarget { NextTurn, NextStep }
```

- `claim` = **全部 next-step 输入 + turn 边界上的一条 next-turn 消息**；
- 每次变更先 `Append(agent/inbox/spliced)` 再改内存 —— 这条让 inbox 成为**持久投影**，UI 可以在没有活 agent 时照样画出"还有几条待办"；
- `steer` 落到日志才有意义：运行在 next-step 边界消费它，这就是"纠偏"的实现方式。

### 5.4 **不要另起炉灶**——复用你已有的 `AgentRun/AgentStepRun`

这一点我认为是最划算的一招：

`Plugins/AIAgent/Services/RunOrchestratorService.cs` 已经有 **AgentRun/AgentStepRun 双表 + Restart/Cancel/Intervene 状态机**（029 计划驱动模式），`StepRunLoopService.cs` 已经是 `while(true)` + 45s 超时 + `Stuck` 人工介入。

**建议**：不要把 dsh 的 turn/step 当新东西造，而是把 `AgentRun` 升格为 `turn`、`AgentStepRun` 升格为 `step`，让 FreeLoop 与计划驱动两种模式**共用同一个 turn/step 抽象**。这样：

- 你已有的 Restart/Cancel/Intervene 三个运维动作直接变成 turn 级操作，不用重写；
- `Stuck` 有了更准的语义（step 卡住 → turn 停滞 → 人工介入）；
- 避免"两套循环并存"这种典型的双轨技术债。

### 5.5 取消传播要有"原因"

现在 cancel 只是个 `CancellationToken`。dsh 把原因塞进 `AbortSignal.reason` 并分四类。C# 等价：

```csharp
public abstract record AgentCancelCause
{
    public sealed record User : AgentCancelCause;
    public sealed record Parent(string Reason) : AgentCancelCause;
    public sealed record Timeout(string Reason) : AgentCancelCause;
    public sealed record Disposed : AgentCancelCause;
}
```

持久 `turn/end` 只记粗粒度 `{ kind: 'aborted' }`——**谁**取消的走单独事件，不重载终态（这条能防止你的 trajectory 视图被各种取消变体污染）。

### 阶段二改动清单

| 文件 | 动作 |
|---|---|
| `ForgeSelf.Abstractions/IAgentLoop.cs` | 重构为 `IAgent` + `IAgentRegistry` + `TurnFrame` 联合；原 `TurnEvent` 废弃 |
| `ForgeSelf.Abstractions/IInbox.cs` | `Send/Claim/Peek` + `InboxTarget` 枚举 + 持久化语义 |
| `Plugins/AIAgent/Services/ReactLoopAgent.cs` | **新增**：真·turn/step 驱动（替代 `AIAgentService.RunAgentLoopAsync` 的 `for(i<10)`） |
| `Plugins/AIAgent/Services/AIAgentService.cs` | 逐步退化为编排层，循环逻辑外迁 |
| `Plugins/AIAgent/Services/StepRunLoopService.cs` | `AgentRun/AgentStepRun` 语义升格为 turn/step，与 FreeLoop 共用 |
| `ForgeSelf.Api/Services/InMemoryAgentLoop.cs` | 补真实 `Claim` 消费与 turn 边界 |
| `ForgeSelf.Abstractions.Tests/AgentLoopContractTests.cs` | 契约扩写到 turn/step/inbox 行为 |

**门禁**：① 三种输入（`followup`/`steer`/`inject`）各有集成测试，**尤其 `steer` 必须能在运行中回合的下一个 step 边界被消费**；② 取消测试：turn 中途 cancel → 日志里 `turn/end` 存在且 reason=aborted，且**未产生半截 user/message**；③ 计划驱动模式（029）回归通过。

---

## 6. 阶段三：工具管线补全（含一处应修的无效接线）

### 6.1 决策三态（现在只有二态）

`ToolRegistry.cs` 现在是 `var deny = await _events.SerialAsync<ToolCallContext, string?>(...)`——用 `null` 表示放行。这意味着**放行无法携带任何信息**，而且没有 `ask`。改成：

```csharp
public abstract record PreToolDecision
{
    public sealed record Allow : PreToolDecision;
    public sealed record Deny(string Reason) : PreToolDecision;
    public sealed record Ask(string? Reason) : PreToolDecision;   // 只有审批返回 allowed-once 才继续
}
```

`ToolCallContext` 顺带补 `Decision` 字段。审批缺失时 **fail-closed**（降级 deny），别静默放行——你在 IOC\_API 那边的"告警分发失败不得污染持久化"是同一原则。

### 6.2 单调守卫（新增）

```
"你要的最终拒绝， waterfall 给不了 —— 后注册的监听器能推翻前面所有人的决定。"
```

```csharp
public interface IToolGuardRegistry
{
    IDisposable AddGuard(Func<ToolExecution, string?> guard);   // 返回 string = 拒绝；null = 维持现状
}
```

**守卫没有 allow 结果**——这就是"只减不增"的机械保证。配 `ctx.Effect(() => guard)` 注册，插件卸载自动摘除（你的 `Effect` 机制已经现成）。

### 6.3 ⚠️ `tools/execute` / `tools/post-execute` 用了广播 — 建议视为缺陷修掉

实读到的接线：

```csharp
await _events.EmitAsync("tools/execute", ctx);
await _events.EmitAsync("tools/post-execute", ctx);
```

`EmitAsync` 是**广播，拿不到返回值**。后果：

- `tools/execute` 无法包裹工具主体 → **无法加超时（环绕）**、无法做指标；
- `tools/post-execute` 无法改写结果 → `027` 档案里写的"结果改写"实际不成立；
- `WaterfallAsync` 在 `IEventBus.cs` 里定义了却没被工具管道使用。

**改成 `WaterfallAsync`**（这是唯一一处我认为属于"修正性改动"而非"增强"的项）。dsh 里只有 `tools/execute` 这个视图能替换 `exec.Signal` 来施加截止时间，靠的就是 waterfall 的 `next()` 环绕。

### 6.4 model-ordered commit + skipped 闭合 + spill

- 工具调用**按模型返回顺序逐个执行**（不要并发乱序提交，否则日志顺序与模型因果不一致）；
- 作用域失配/未执行的调用，必须**合成一个 `tool/result`（Skipped）**——保证"模型发了 N 个 call，日志里必有 N 个 result"，否则下一轮 `DeriveMessages` 会因缺 tool 消息而让 provider 报错；
- 大结果 spill：超过阈值写 spill 表 / 文件，上下文里只放引用 + 摘要。

### 6.5 `ILlmRuntime` 补 usage 与 tool\_call

现在只有 `{Content, IsFinal}`，导致 `assistant/message` 事件里 `PromptTokens/CompletionTokens/FinishReason` 无从填写，而这三样是成本统计与 `max-tokens` 粘滞判定的输入。建议：

```csharp
public sealed record StreamChunk(string Content, bool IsFinal)
{
    public ToolCallDelta? ToolCall { get; init; }     // 结构化工具调用增量
    public UsageInfo? Usage { get; init; }            // 末片携带
    public string? FinishReason { get; init; }        // stop | length | tool_calls | content_filter
}
```

### 阶段三改动清单

| 文件 | 动作 |
|---|---|
| `ForgeSelf.Api/Services/ToolRegistry.cs` | 三态决策 + 守卫注册表 + execute/post-execute 改 waterfall + model-ordered commit + skipped 合成 |
| `ForgeSelf.Api/Services/ToolCallContext.cs` | 补 Decision / CallId / DurationMs / Signal |
| `ForgeSelf.Core/IToolGuardRegistry.cs` | 新增单调守卫接口（或放 Abstractions） |
| `ForgeSelf.Abstractions/ILlmRuntime.cs` | StreamChunk 补 ToolCall/Usage/FinishReason |
| `ForgeSelf.Api/Plugins/Services/…` | 审批/injection：让 `Ask` 决策真正走到你已有的 IM 网关或前端审批弹窗 |

**门禁**：① **每个注册了守卫/决策监听器的插件，都必须配一条"卸载后解析为 null 且行为回退到默认"的 dispose 测试**（沿用你 027 已有的 dispose 门禁）；② `Ask` 在无审批服务时自动 deny 的测试；③ post-execute 改写结果生效的测试（现在这条**必然是红的**，正好验证 `_events` 接线改对了）。

---

## 7. 明确不要照搬（阻抗匹配：TS vs .NET）

这条比前面任何一条都重要——**盲目平移会在 .NET 上翻车**。

| dsh 特性 | 为什么别照搬 | 建议替代 |
|---|---|---|
| **eager 单例服务 + 共享 store** | JS 单线程无 Scoped 概念；.NET 有 Scoped/DbContext/事务。你 027 待办 1 已经踩到 Scoped 语义近似 | 服务仍走 MS DI 的 Scoped/Singleton 语义；插件间只经由 **Abstractions 契约接口**交互，不透传宿主 Scoped |
| **一切皆插件（连主循环都是）** | 目标正确，但现在就把 loop 做成热插拔，会把 AssemblyLoadContext 卸载、文件锁、类型陈旧等问题一次性引爆（你 035 已经踩过宿主 DI 类型陈旧导致 500 的坑） | **接口先行**：`IAgent`/`IAgentRegistry` 稳定后，loop 自然可换。dsh 也是先有 `agent` 包再有 `agent-loop` 包 |
| **数十家模型 provider 适配器** | 你已有 AIProvider 体系，非瓶颈 | 保持；把 provider 选择纳入 `request/header` 落日志即可 |
| **PTC 模式（模型写 TS 程序批量调用）** | 依赖 TS 运行时；.NET 侧要 Roslyn 脚本沙箱，成本高 | 你有 Roslyn 4.13 与 ScriptRunner 插件，可作为**远期**探索，不入本期 |
| **Creator 模式 / 插件市场 / 3900 插件** | 产品形态，非架构优势 | 你有 PluginStore/版本化布局已在路上（035），按自己节奏 |
| **ACP / SDK / headless 六入口** | 你已经 host+front 分离、有 Webhook/IM 网关，ROI 不同 | 只在确有 CI 驱动需求时补 headless runner |
| **`cordis_*` 运行时自修改** | 最诱人也最危险：.NET 里 ALC 卸载有文件锁、静态引用泄漏 | 你的 `PluginVersionLayout` + 显式版本升级（035）是更稳的实现路径，建议坚持 |

---

## 8. 排期建议

| 批次 | 内容 | 建议颗粒 |
|---|---|---|
| **B1** | SessionEvent 联合化 + SessionEventMap + 单元测试 | 单批可提 |
| **B2** | 持久化 ISessionStore（Sqlite）+ 契约测试复用 | 单批可提 |
| **B3** | EventBus 父子冒泡（027 待办 5 方案 B） | 单批可提 |
| **B4** | 写路径改序（ChatController / AIChatController）+ 不变量测试 | **单批提，风险最高，需真机走查** |
| **B5** | IAgent/IAgentRegistry/TurnFrame + ReactLoopAgent 骨架 | 单批可提 |
| **B6** | Inbox 持久化 + steer 消费接通 | 单批可提 |
| **B7** | 复用 AgentRun/AgentStepRun 统一 turn/step + 029 回归 | 单批可提 |
| **B8** | 工具管线：三态 + 守卫 + waterfall 修正 + skipped + spill | 可分两次提 |
| **B9** | 收官与清理批：旧执行面退役（`ExecuteToolWithResultAsync`/`ExecuteToolWithTimeoutAsync`/`ToolCallContext` 删 + 3 调用方迁移 + grep 守门）+ 前端 tool 事件 FIFO 配对 + `plan:{runId}` 规划过程展示 + 测试基建根治（`FORGESELF_DATA_ROOT`）+ `ConfigUnifier` 降级健壮化 + spill 32 KiB 进配置中心 + 文档同步 | 单批收官（2026-09-29 已落地） |

**B1–B4 是一条完整因果链**，建议作为一轮专项；B4 改动触及真实写路径，**务必按你 spec 036 那套"真机走查"流程验证**。

---

## 9. 用户验证清单

- [x] ✅ 我说的"日志是影子不是真相"，与 `ChatController` 实际认知一致——`_sessionStore?.Append` 的问号恰恰是改造起点；**现已改为 `SessionProjectionService.SyncAsync` 全量重投影直写 XCode 实体，日志成为唯一真相源**。
- [x] ✅ `AgentRun/AgentStepRun` 升格为 turn/step（§5.4）已落地——`ReactLoopAgent` 驱动 turn/step，029 调用方经回归验证无影响。
- [x] ✅ §6.3 我把 `tools/execute`/`tools/post-execute` 用广播判为**无效接线**，已按缺陷修——六闸门改为 `WaterfallAsync` 修正接线，`tools/result` 经 `EmitAsync` 冻结快照。
- [x] ✅ 阶段排期已落 specs 编号（`040-session-event-sourcing` / `041-turn-step-runtime` / `042-tool-pipeline`），三份详细设计均已实施完毕。
- [x] ✅ "暂不做主循环热插拔"（§7）维持——`ReactLoopAgent` 为长生命周期单例，热插拔风险已在 B5 设计期规避。
- [x] ✅ 先从 B1 开工已完成——B1–B9 八批次全部收官（2026-09-29），含 B4 真机走查验证通过。

---

## 附：本次判断的数据来源

| 项目 | 来源 | 可信度 |
|---|---|---|
| 契约层定义（IAgentLoop/IInbox/ISessionStore/ILlmRuntime） | 实读 GitHub main 分支源码 | 高 |
| ToolRegistry 接线、`for(i<10)`、ChatController 双写 | 实读源码（经 subagent，可能有转述误差） | 中高，**落地前本地二次确认** |
| 027 待办清单、.NET↔TS 阻抗问题 | 你的自有文档 | 高 |
| dsh 侧机制细节 | 官方 `docs/architecture.md` + 社区拆解 | 中高（详见上一份《运行全链路》文末标注） |
