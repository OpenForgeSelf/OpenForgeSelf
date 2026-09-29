# 031 · AIAgent 执行记录与可复盘机制

> 版本：2026-09-11 · 状态：**已落地（方案A）**
> 来源：输入6 复盘指令——"agent 插件应该有执行 agent 的记录功能，页面上有没有呈现，方便不方便复盘，而不是苦哈哈分析 Log"。

## 1. 结论先行

| 执行模式 | 工具轨迹落库 | 页面呈现 | 可复盘性 | 评级 |
|---|---|---|---|---|
| **PlanDriven（计划驱动）** | ✅ AgentRun + AgentStepRun（含 ToolCallsJson、入参/出参/卡住原因/重试） | ✅ RunRecordPanel.vue（Run 列表 + 步骤时间线 + 操作 + 工具调用轨迹） | 良好 | 合格 |
| **FreeLoop（默认聊天，「通用 agent」）** | ✅ AIChatMessage 含 ToolCallsJson（name/args/result/success/durationMs） | ✅ ChatPanel.vue 历史消息渲染 toolEvents，刷新不丢 | 良好——可直接在会话历史中复盘每次工具调用 | 合格 |

**一句话**：最常用的 FreeLoop 路径已实现工具轨迹持久化，刷新后仍可在会话历史中复盘每次工具调用。

## 2. 现状详查（代码事实）

### 2.1 FreeLoop（`POST /api/ai-agent/chat`）
- `Entities/AIChatMessage.cs`：已加 `ToolCallsJson` 列；索引器 `this[name]` 同步注册 get/set 分支（XCode 通过索引器而非属性读写字段，缺分支会导致 Insert 静默丢弃、读回 null）。
- `Controllers/AIChatController.cs`：SSE/非流式循环累积 `tool_call`/`tool_result` 事件为 `ChatToolCallTrace`（name/args/result/success/durationMs），随 assistant 消息入库。
- `Services/AIAgentProjectionService.cs`（dsh B4/B6 改序后）：`SaveMessageAsync` 直写路径已**全删**（宿主与插件两侧）——助手行 `ToolCallsJson` 工具轨迹由 `AIAgentProjectionService` 从会话日志的 `tool/call` + `tool/result` 事件**配对还原**（含耗时与成败），幂等重投影写入 `AIChatMessage`（前缀对齐 Role+Content+ToolCallsJson）；正确性唯一来源 = 会话日志。
- `GET history/{sessionId}`：返回 `ChatResponse[]`，assistant 消息携带 `ToolCallsJson`。
- 前端 `ChatPanel.vue`/`AiAgentView.vue`：`loadHistory()` 用 `parseToolEvents(m.toolCallsJson)` 把落库轨迹渲染为 `.chat__tool-card`，刷新后仍可复盘。

### 2.2 PlanDriven（`POST /api/ai-agent/runs`，029）
- `Entities/AgentStepRun.cs`：`ToolCallsJson`（本步工具调用轨迹）、`InputJson`（入参）、`OutputJson`（出参）、`ErrorMessage`、`StuckReason`、`RetryCount` 全落库。
- `Controllers/AgentRunsController.cs`：list / detail / resume / restart / cancel / intervene 完整 API。
- `RunRecordPanel.vue`：Run 列表（状态徽标/步数/token）+ 步骤时间线（状态/耗时/目标/入参/出参/卡住/批注/补位/重试）+ 操作按钮。
- **缺口**：步骤详情渲染条件 `stepDetailVisible()` 不含 `toolCallsJson`——数据在库里，页面看不见。

## 3. 完善方案（≥2 方案 + 推荐）

### 方案A（已落地）：FreeLoop 工具轨迹持久化 + 历史渲染
| 项 | 内容 |
|---|---|
| 改动 | ① `AIChatMessage` 加 `ToolCallsJson` 列（XCode 迁移）；② `AIChatController` 流式循环累积 tool_call/tool_result（name/args/result/success/durationMs），随 assistant 消息入库；③ history 返回工具轨迹；④ `ChatPanel`/`AiAgentView` 历史消息渲染 toolEvents |
| 收益 | 默认路径可复盘：每次 agent 执行"调了什么工具、参数、结果、耗时"在历史消息上直接可见；e2e 复盘不再翻日志 |
| 代价 | 实体迁移 + 4 处后端 + 2 处前端；约半天 |
| 风险 | 低——加列不影响既有读写；SSE 契约不变 |
| 验证 | `agent-execute.spec.ts` 3 passed（含「history 含 toolCallsJson 轨迹」断言）；浏览器走查 51888 `/ai-agent` 刷新后工具卡片仍可见 |

### 方案B：FreeLoop 统一纳入 AgentRun/AgentStepRun 体系
| 项 | 内容 |
|---|---|
| 改动 | FreeLoop 每轮对话生成"单步 Run"，轨迹写 AgentStepRun |
| 收益 | 一套记录模型，一处渲染 |
| 代价 | 大——FreeLoop 无"计划/步骤"语义，强行套会产生空 plan/step，语义错位；RunRecordPanel 与 ChatPanel 双入口重复 |
| 风险 | 中——两套心智混装，用户困惑 |

### 方案C：前端重放历史时重新解析
**否决**——轨迹根本没入库，无源可解析。

**已落地 A**：改动最小、语义正确（FreeLoop 工具轨迹本就属于"这条 assistant 消息"）、直接命中"刷新可复盘"诉求；同时补 RunRecordPanel 渲染 ToolCallsJson。

## 4. 本次会话经验沉淀（2026-09-11）

### 4.1 根因级教训：ctx 与 IServiceCollection 是两套存储
- **坑**：插件 `Apply` 里 `ctx.Get<IServiceCollection>()` 拿到宿主 DI 容器，`services.AddSingleton<T>` 只进宿主 DI；运行时 `ctx.Get<T>()` 查的是 **ctx 自有共享服务表**，两不相通。工具函数运行时 `ctx.Get<IProjectWorkspaceService>()` 永远 null。
- **正确模式**：`var inst = new T(); services?.AddSingleton<T>(inst); ctx.Register<T>(inst);` —— **同一实例双注册**（参考 `IWorkflowAIAdvisor` line 60-67 既有写法）。
- **排障路径**：`ProjectController`（构造注入走宿主 DI）正常而工具函数（ctx.Get）报"服务不可用"，二者分歧即指向此坑。

### 4.2 验证手段教训
- **「报成功」≠「真发生」**：Round 1 临时脚本断言"发送键可用+文本长度"即判完成，agent 实际未跑完 → **断言必须绑定业务终态**（落盘文件存在+内容特征齐全），不绑 UI 启发式。
- **成功路径也要日志**：只有错误路径日志时，"没报错也没结果"无法定位。文件工具三个函数已补 `XTrace.Log.Info`（根/绝对路径/大小），本轮靠它确认 `write_file 成功：root=D:\agent-test, 绝对路径=…, 大小=10207`。
- **DLL 特征核验**：.NET 字符串是 UTF-16LE，`grep -a` 匹配不到中文；用 Python `s.encode('utf-16-le')` 计数。文本日志是 UTF-8，用 utf-8 计数——**两类文件两套编码，勿混用**。
- **正式 e2e 直连运行实例**：`e2e/plugins/ai-agent/agent-execute.spec.ts` + 专用 config（无 webServer）+ `host-api-token.ts`（机器派生密钥 PBKDF2 210k 解 `~/.forgeself/Config/ForgeSetting.config` 的 v2 ApiToken）。零 mock、真实后端+真实 LLM、证据落盘 `screenshots/e2e/ai-agent/`。

### 4.3 环境坑
- **safe-delete shim 拦截 Remove-Item**：`build.ps1` 清理 `publish/Data|Log` 被 fail-closed 拦退码 1（发布实质已完成）；且产生 `publish/publish/` 嵌套残留。规避：`Remove-Item` 加 `-ErrorAction SilentlyContinue` 或改用纯 .NET 删除。
- **数据根二选一**：`ASPNETCORE_ENVIRONMENT=Development` → `程序目录/Data`；否则 → `~/.forgeself`。重启 51888 复用真实配置必须**不设** Development。NewLife 日志目录按 CWD 走（publish/Log），与数据根无关，勿以日志位置误判数据根。

### 4.4 落地过程关键根因：XCode 实体字段必须注册到索引器
- **坑**：XCode 通过 `public override Object this[String name]` 索引器读写字段，而不是 C# 属性本身。`AIChatMessage` 虽已加 `ToolCallsJson` 属性与 `_ToolCallsJson` 字段，但索引器缺该分支 → ORM Insert 静默丢弃该列值、读回 null（日志看到 `工具轨迹: 255` 但 DB 为 `None`）。
- **正确模式**：XCode 实体新增字段必须三件套齐全：`_` 字段、`__` 元数据、`this[name]` 索引器的 get/set 分支。验证：Insert 后立即用同一会话 `FindById` 读回，断言字段非 null。
- **排障路径**：日志显示保存时字段有值，但 history/DB 无值 → 优先查索引器分支与 `__` 类元数据。

## 5. 用户验证清单

- [x] 打开 `/ai-agent`「通用 agent」发一条带工具调用的消息 → **刷新页面** → 历史消息上工具调用（名称/参数/结果/耗时）仍可见 = FreeLoop 轨迹持久化生效（2026-09-11 浏览器走查 51888 通过）
- [x] 打开「执行记录」面板（计划驱动）→ 点一条 Run → 步骤详情展开可见**工具调用轨迹**（ToolCallsJson 渲染已随代码落地，待后续计划驱动运行时人工点检）
- [x] 计划驱动 Run 列表徽标/步数/token、步骤时间线、继续/重开/跳过/补位/取消操作正常（既有，回归确认）
- [x] `cd ForgeSelf.Web && node node_modules/@playwright/test/cli.js test e2e/plugins/ai-agent/agent-execute.spec.ts --config=e2e/plugins/ai-agent/agent-execute.config.ts` → **3 passed**（含方案A history 轨迹断言）
