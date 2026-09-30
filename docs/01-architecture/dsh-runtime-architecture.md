# 01-architecture — dsh 对齐运行时架构图

> 功能编号：040 / 041 / 042（dsh 对齐 B1–B9）
> 状态：已实现（与代码对齐）
> 最后更新：2026-09-30
> 关联：设计真源 [`ai/pilot/dsh-alignment-b2-b9/02-spec.md`](../ai/pilot/dsh-alignment-b2-b9/02-spec.md)；上下文真源与收官报告在该 pilot 目录 00–07。

本文档沉淀 dsh 对齐后的**运行时架构可视化**（三张图），供 `overview.md` §3.2.1、`cordis-kernel.md`、Cordis 内核附录等交叉引用。

---

## 图 1 · 分层架构与会话日志真源

四层（前端 / 宿主 / 内核+契约 / 插件）+ 会话日志唯一真相源带 + 运行时三件套（ReactLoopAgent / 持久 IInbox / ToolRegistry+Guard）。

```svg
<svg viewBox="0 0 720 560" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
  <defs>
    <marker id="arr" viewBox="0 0 8 8" refX="7" refY="4" markerWidth="8" markerHeight="8" markerUnits="userSpaceOnUse" orient="auto"><path d="M1 1 L7 4 L1 7 Z" fill="#52525B"/></marker>
    <marker id="arrb" viewBox="0 0 8 8" refX="7" refY="4" markerWidth="8" markerHeight="8" markerUnits="userSpaceOnUse" orient="auto"><path d="M1 1 L7 4 L1 7 Z" fill="#27D2BF"/></marker>
  </defs>
  <rect x="0" y="0" width="720" height="560" fill="#F7F7F8" rx="12"/>
  <text x="360" y="30" text-anchor="middle" font-family="system-ui,sans-serif" font-size="16" font-weight="600" fill="#171717">dsh 对齐 · 分层架构与会话日志真源</text>
  <text x="360" y="50" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">四层 + 会话日志唯一真相源 + 运行时三件套（B1–B9）</text>

  <rect x="40" y="66" width="640" height="64" rx="10" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)" stroke-dasharray="6 4"/>
  <text x="56" y="88" font-family="system-ui,sans-serif" font-size="14" font-weight="500" fill="#171717">第1层 · 前端 Vue 3 SPA</text>
  <text x="56" y="110" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">ChatPanel 对话 · SessionPanel 会话 · 消费 SSE（content / turn / tool_call / tool_result / done）</text>

  <rect x="40" y="142" width="640" height="64" rx="10" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)" stroke-dasharray="6 4"/>
  <text x="56" y="164" font-family="system-ui,sans-serif" font-size="14" font-weight="500" fill="#171717">第2层 · 宿主 ASP.NET Core（ForgeSelf.Api）</text>
  <text x="56" y="186" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">ChatController（api/chat）· UnifiedAI 网关 · SessionProjectionService（只读投影写入）</text>

  <rect x="40" y="218" width="640" height="64" rx="10" fill="#F2F7FF" stroke="#4B3FE3"/>
  <text x="56" y="240" font-family="system-ui,sans-serif" font-size="14" font-weight="500" fill="#1A1759">第3层 · 内核 ForgeSelf.Core + 契约 ForgeSelf.Abstractions</text>
  <text x="56" y="262" font-family="system-ui,sans-serif" font-size="12" fill="#4B3FE3">IContext / EventBus / Fiber · 能力接缝 ISessionStore / IAgent / IInbox / IToolRegistry</text>

  <rect x="40" y="294" width="640" height="64" rx="10" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)" stroke-dasharray="6 4"/>
  <text x="56" y="316" font-family="system-ui,sans-serif" font-size="14" font-weight="500" fill="#171717">第4层 · 插件层（AIAgent 等）</text>
  <text x="56" y="338" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">ReactLoopAgent（IAgent 实现）· 工具函数经 IToolRegistry 注册分发</text>

  <rect x="40" y="372" width="640" height="74" rx="10" fill="#EAFBF8" stroke="#27D2BF"/>
  <text x="56" y="396" font-family="system-ui,sans-serif" font-size="14" font-weight="600" fill="#0F766E">会话日志唯一真相源 · SessionEvent（append-only）</text>
  <text x="56" y="418" font-family="system-ui,sans-serif" font-size="12" fill="#0F766E">不变量 1 Model-visible means logged：模型可见消息先 Append 再 DeriveMessages（model ⊆ log）；ChatMessage 仅只读投影</text>
  <text x="56" y="438" font-family="system-ui,sans-serif" font-size="12" fill="#0F766E">PersistentSessionStore（XCode · 表 SessionEvent）｜ InMemorySessionStore（测试）</text>

  <text x="40" y="472" font-family="system-ui,sans-serif" font-size="13" font-weight="500" fill="#171717">运行时三件套</text>
  <rect x="40" y="482" width="200" height="56" rx="8" fill="#F2F7FF" stroke="#4B3FE3"/>
  <text x="140" y="508" text-anchor="middle" font-family="system-ui,sans-serif" font-size="13" fill="#1A1759">ReactLoopAgent</text>
  <text x="140" y="528" text-anchor="middle" font-family="system-ui,sans-serif" font-size="11" fill="#4B3FE3">状态机 · turn/step 帧</text>
  <rect x="260" y="482" width="200" height="56" rx="8" fill="#F2F7FF" stroke="#4B3FE3"/>
  <text x="360" y="508" text-anchor="middle" font-family="system-ui,sans-serif" font-size="13" fill="#1A1759">持久 IInbox</text>
  <text x="360" y="528" text-anchor="middle" font-family="system-ui,sans-serif" font-size="11" fill="#4B3FE3">Followup / Steer / Inject</text>
  <rect x="480" y="482" width="200" height="56" rx="8" fill="#F2F7FF" stroke="#4B3FE3"/>
  <text x="580" y="508" text-anchor="middle" font-family="system-ui,sans-serif" font-size="13" fill="#1A1759">ToolRegistry + Guard</text>
  <text x="580" y="528" text-anchor="middle" font-family="system-ui,sans-serif" font-size="11" fill="#4B3FE3">六闸门执行面 · 单调守卫</text>

  <path d="M360 358 L360 372" stroke="#27D2BF" stroke-width="2" fill="none" marker-end="url(#arrb)"/>
  <text x="368" y="350" font-family="system-ui,sans-serif" font-size="11" fill="#0F766E">唯一写路径 Append</text>
</svg>
```

---

## 图 2 · 对话运行流程（turn / step）

一条聊天请求如何经 ReactLoopAgent 状态机驱动，落日志、跑 LLM、调用工具六闸门，最终派生只读投影。

```svg
<svg viewBox="0 0 720 760" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
  <defs>
    <marker id="a2" viewBox="0 0 8 8" refX="7" refY="4" markerWidth="8" markerHeight="8" markerUnits="userSpaceOnUse" orient="auto"><path d="M1 1 L7 4 L1 7 Z" fill="#52525B"/></marker>
    <marker id="a2b" viewBox="0 0 8 8" refX="7" refY="4" markerWidth="8" markerHeight="8" markerUnits="userSpaceOnUse" orient="auto"><path d="M1 1 L7 4 L1 7 Z" fill="#27D2BF"/></marker>
  </defs>
  <rect x="0" y="0" width="720" height="760" fill="#F7F7F8" rx="12"/>
  <text x="360" y="28" text-anchor="middle" font-family="system-ui,sans-serif" font-size="16" font-weight="600" fill="#171717">对话运行流程 · turn / step（dsh B5/B7）</text>
  <text x="360" y="48" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">POST → 落日志 → ReactLoopAgent → LLM + 工具六闸门 → 派生只读投影</text>

  <rect x="220" y="64" width="280" height="34" rx="8" fill="#F2F7FF" stroke="#4B3FE3"/>
  <text x="360" y="86" text-anchor="middle" font-family="system-ui,sans-serif" font-size="13" fill="#1A1759">POST /api/chat（或 stream）</text>

  <rect x="220" y="112" width="280" height="34" rx="8" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="360" y="134" text-anchor="middle" font-family="system-ui,sans-serif" font-size="13" fill="#171717">IInbox.Followup（注入续跑/转向/补 inject）</text>

  <rect x="220" y="160" width="280" height="34" rx="8" fill="#EAFBF8" stroke="#27D2BF"/>
  <text x="360" y="182" text-anchor="middle" font-family="system-ui,sans-serif" font-size="13" fill="#0F766E">Append(user/message) 先落会话日志</text>

  <rect x="220" y="208" width="280" height="34" rx="8" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="360" y="230" text-anchor="middle" font-family="system-ui,sans-serif" font-size="13" fill="#171717">GetOrCreateAsync(session)</text>

  <rect x="220" y="256" width="280" height="34" rx="8" fill="#F2F7FF" stroke="#4B3FE3"/>
  <text x="360" y="278" text-anchor="middle" font-family="system-ui,sans-serif" font-size="13" fill="#1A1759">ReactLoopAgent.RunAsync（状态机启动）</text>

  <rect x="220" y="304" width="280" height="34" rx="8" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="360" y="326" text-anchor="middle" font-family="system-ui,sans-serif" font-size="13" fill="#171717">turn/start → claim → pre-step</text>

  <rect x="220" y="352" width="280" height="34" rx="8" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="360" y="374" text-anchor="middle" font-family="system-ui,sans-serif" font-size="13" fill="#171717">step/start → Append(request/header) → agent/request</text>

  <rect x="180" y="400" width="360" height="56" rx="8" fill="#F2F7FF" stroke="#4B3FE3"/>
  <text x="360" y="424" text-anchor="middle" font-family="system-ui,sans-serif" font-size="13" fill="#1A1759">step 主体：LLM 流式 + 工具六闸门</text>
  <text x="360" y="444" text-anchor="middle" font-family="system-ui,sans-serif" font-size="11" fill="#4B3FE3">pre-execute → guard → execute → post-execute → finalize → result</text>

  <path d="M520 428 L600 428 L600 516 L240 516 L240 456" stroke="#52525B" stroke-width="1.5" fill="none" marker-end="url(#a2)"/>
  <text x="610" y="476" font-family="system-ui,sans-serif" font-size="11" fill="#52525B">还欠请求 → 回到 step/start</text>

  <polygon points="360,470 392,500 360,530 328,500" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="360" y="504" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#171717">还欠请求？</text>

  <rect x="220" y="552" width="280" height="34" rx="8" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="360" y="574" text-anchor="middle" font-family="system-ui,sans-serif" font-size="13" fill="#171717">turn/end（Suspended? 挂起待 steer 唤醒）</text>

  <rect x="220" y="600" width="280" height="34" rx="8" fill="#EAFBF8" stroke="#27D2BF"/>
  <text x="360" y="622" text-anchor="middle" font-family="system-ui,sans-serif" font-size="13" fill="#0F766E">DeriveMessages（仅 system/user/assistant/tool）</text>

  <rect x="220" y="648" width="280" height="34" rx="8" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="360" y="670" text-anchor="middle" font-family="system-ui,sans-serif" font-size="13" fill="#171717">SessionProjectionService.SyncAsync</text>

  <rect x="220" y="696" width="280" height="34" rx="8" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="360" y="718" text-anchor="middle" font-family="system-ui,sans-serif" font-size="13" fill="#171717">ChatMessage 只读投影 + UI 实时推送</text>

  <path d="M360 98 L360 112" stroke="#52525B" stroke-width="1.5" fill="none" marker-end="url(#a2)"/>
  <path d="M360 146 L360 160" stroke="#52525B" stroke-width="1.5" fill="none" marker-end="url(#a2)"/>
  <path d="M360 194 L360 208" stroke="#52525B" stroke-width="1.5" fill="none" marker-end="url(#a2)"/>
  <path d="M360 242 L360 256" stroke="#52525B" stroke-width="1.5" fill="none" marker-end="url(#a2)"/>
  <path d="M360 290 L360 304" stroke="#52525B" stroke-width="1.5" fill="none" marker-end="url(#a2)"/>
  <path d="M360 338 L360 352" stroke="#52525B" stroke-width="1.5" fill="none" marker-end="url(#a2)"/>
  <path d="M360 386 L360 400" stroke="#52525B" stroke-width="1.5" fill="none" marker-end="url(#a2)"/>
  <path d="M360 456 L360 470" stroke="#52525B" stroke-width="1.5" fill="none" marker-end="url(#a2)"/>
  <path d="M328 530 L300 540" stroke="#52525B" stroke-width="1.5" fill="none" marker-end="url(#a2)"/>
  <path d="M360 586 L360 600" stroke="#52525B" stroke-width="1.5" fill="none" marker-end="url(#a2)"/>
  <path d="M360 634 L360 648" stroke="#52525B" stroke-width="1.5" fill="none" marker-end="url(#a2)"/>
  <path d="M360 682 L360 696" stroke="#52525B" stroke-width="1.5" fill="none" marker-end="url(#a2)"/>
  <path d="M360 238 L360 304" stroke="#27D2BF" stroke-width="2" stroke-dasharray="5 4" fill="none" marker-end="url(#a2b)"/>
  <text x="368" y="286" font-family="system-ui,sans-serif" font-size="11" fill="#0F766E">唯一写路径</text>
</svg>
```

---

## 图 3 · 工具六闸门执行面

每个工具调用必经六闸门；守卫三态（Allow/Deny/Ask）+ 单调守卫；结果经 finalize 恰好一次冻结；不变量带保证日志闭合与模型有序。

```svg
<svg viewBox="0 0 720 470" width="100%" height="auto" xmlns="http://www.w3.org/2000/svg">
  <defs>
    <marker id="a3" viewBox="0 0 8 8" refX="7" refY="4" markerWidth="8" markerHeight="8" markerUnits="userSpaceOnUse" orient="auto"><path d="M1 1 L7 4 L1 7 Z" fill="#52525B"/></marker>
    <marker id="a3b" viewBox="0 0 8 8" refX="7" refY="4" markerWidth="8" markerHeight="8" markerUnits="userSpaceOnUse" orient="auto"><path d="M1 1 L7 4 L1 7 Z" fill="#4B3FE3"/></marker>
  </defs>
  <rect x="0" y="0" width="720" height="470" fill="#F7F7F8" rx="12"/>
  <text x="360" y="28" text-anchor="middle" font-family="system-ui,sans-serif" font-size="16" font-weight="600" fill="#171717">工具管线 · 六闸门执行面（dsh B8）</text>
  <text x="360" y="48" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#52525B">IToolRegistry.ExecuteBatchAsync · WaterfallAsync 修正接线 · 单调守卫</text>

  <rect x="40" y="80" width="120" height="48" rx="8" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="100" y="104" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#171717">pre-execute</text>
  <text x="100" y="120" text-anchor="middle" font-family="system-ui,sans-serif" font-size="10" fill="#52525B">三态决策门</text>

  <rect x="180" y="80" width="120" height="48" rx="8" fill="#F2F7FF" stroke="#4B3FE3"/>
  <text x="240" y="104" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#1A1759">guard</text>
  <text x="240" y="120" text-anchor="middle" font-family="system-ui,sans-serif" font-size="10" fill="#4B3FE3">Allow/Deny/Ask</text>

  <rect x="320" y="80" width="120" height="48" rx="8" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="380" y="104" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#171717">execute</text>
  <text x="380" y="120" text-anchor="middle" font-family="system-ui,sans-serif" font-size="10" fill="#52525B">真实执行</text>

  <rect x="460" y="80" width="120" height="48" rx="8" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="520" y="104" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#171717">post-execute</text>
  <text x="520" y="120" text-anchor="middle" font-family="system-ui,sans-serif" font-size="10" fill="#52525B">结果改写/检查</text>

  <rect x="600" y="80" width="80" height="48" rx="8" fill="#F2F7FF" stroke="#4B3FE3"/>
  <text x="640" y="104" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#1A1759">finalize</text>
  <text x="640" y="120" text-anchor="middle" font-family="system-ui,sans-serif" font-size="10" fill="#4B3FE3">恰好一次</text>

  <path d="M160 104 L180 104" stroke="#52525B" stroke-width="1.5" fill="none" marker-end="url(#a3)"/>
  <path d="M300 104 L320 104" stroke="#52525B" stroke-width="1.5" fill="none" marker-end="url(#a3)"/>
  <path d="M440 104 L460 104" stroke="#52525B" stroke-width="1.5" fill="none" marker-end="url(#a3)"/>
  <path d="M580 104 L600 104" stroke="#52525B" stroke-width="1.5" fill="none" marker-end="url(#a3)"/>

  <!-- result 回流 -->
  <rect x="600" y="160" width="80" height="44" rx="8" fill="#EAFBF8" stroke="#27D2BF"/>
  <text x="640" y="186" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#0F766E">result</text>
  <path d="M640 128 L640 160" stroke="#27D2BF" stroke-width="1.5" fill="none" marker-end="url(#a3b)"/>
  <text x="560" y="150" text-anchor="end" font-family="system-ui,sans-serif" font-size="10" fill="#0F766E">冻结快照回流</text>

  <!-- guard Deny 短路 -->
  <path d="M240 128 C240 180, 560 200, 600 162" stroke="#EF4444" stroke-width="1.5" fill="none" marker-end="url(#a3)"/>
  <text x="420" y="200" text-anchor="middle" font-family="system-ui,sans-serif" font-size="11" fill="#EF4444">Deny → 短路到 finalize（fail-closed）</text>

  <!-- 不变式带 -->
  <rect x="40" y="260" width="640" height="150" rx="10" fill="#F2F7FF" stroke="#4B3FE3"/>
  <text x="56" y="286" font-family="system-ui,sans-serif" font-size="14" font-weight="600" fill="#1A1759">不变量带（日志闭合 · 模型有序）</text>
  <rect x="56" y="300" width="280" height="40" rx="8" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="196" y="324" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#171717">fail-closed：守卫默认拒绝</text>
  <rect x="356" y="300" width="300" height="40" rx="8" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="506" y="324" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#171717">Skipped 合成：N call 必有 N result</text>
  <rect x="56" y="352" width="280" height="40" rx="8" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="196" y="376" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#171717">model-ordered commit</text>
  <rect x="356" y="352" width="300" height="40" rx="8" fill="#F7F7F8" stroke="rgba(23,23,23,0.12)"/>
  <text x="506" y="376" text-anchor="middle" font-family="system-ui,sans-serif" font-size="12" fill="#171717">spill &gt; 阈值：超量请求溢出守门</text>

  <text x="40" y="440" font-family="system-ui,sans-serif" font-size="11" fill="#52525B">接线：tools/pre-execute（SerialAsync 三态门）→ tools/execute（WaterfallAsync，可替换 Signal 超时）→ tools/post-execute（WaterfallAsync 改写）→ tools/result（EmitAsync 冻结）。单调守卫 ToolGuardRegistry 杜绝二次放行。</text>
</svg>
```

---

## 关键事实索引（代码真源）

| 概念 | 代码落点 |
|------|----------|
| 会话日志契约 | `ForgeSelf.Abstractions/ISessionStore.cs`（不变量 1 注释）、`SessionEvents.cs`（可辨识联合 record） |
| 持久化实现 | `ForgeSelf.Api/Services/PersistentSessionStore.cs`（XCode 表 `SessionEvent`）／`InMemorySessionStore.cs`（测试） |
| 只读投影 | `ForgeSelf.Api/Services/SessionProjectionService.cs`（`SyncAsync` 幂等全量重投影） |
| 运行时驱动 | AIAgent 插件 `ReactLoopAgent`（实现 `IAgent`，经 `IAgentRegistry` 解析） |
| 收件箱 | `ForgeSelf.Abstractions/IInbox.cs`（`Followup`/`Steer`/`Inject`） |
| 工具执行面 | `ForgeSelf.Abstractions/IToolRegistry.cs` · `ToolPipeline.cs` · `ToolGuardRegistry.cs`（六闸门 + 单调守卫） |
| 不变量测试 | `SessionStoreContractTests.DeriveMessages_OnlyModelVisible` · `ChatControllerInvariantTests.Invariant1_EveryModelVisibleMessage_IsRebuildableFromLog` · `ChatCompletionWritePathTests`（model ⊆ log） |
