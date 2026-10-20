# ADR-003：外部 MCP 工具「枚举可见 + 调用可达」——修订 D10 的枚举侧，注册表隔离保持

- 状态：Accepted（2026-10-19，闸门1 用户批准「开工」；随 McpCenter v2.6.0 / AIAgent v1.9.0 落地）
- 关联：`docs/02-features/034-mcp-center.md`（F034-R04 / §转发契约）· `docs/ai/pilot/2026-10-19-mcp-external-tools-in-list/`（00–07）· 被修订决策 D10（v2.1.0 外部 MCP 客户端设计，见 034 §四大能力注）
- 背景Link：用户输入 2026-10-19「万能工具要在 list_tools 把 mcp. 开头的也带上，还有所有，不然注册了外部mcp，在工具内外都用不了那添加来干嘛」

## 决策

1. **修订范围只取 D10 的枚举侧**：`list_tools`（McpCenter 注册进宿主注册表的枚举工具函数）在**枚举出口**合并已连接外部 MCP 服务器的工具——条目 `name` 即全名 `mcp.<服务器id>.<工具名>`、`source="mcp-external"`、`pluginId`=服务器 id，`includeSchema:true` 时带目标工具输入 schema。
2. **注册表隔离保持（D10 核心不推翻）**：外部工具**仍不进** 宿主 `IToolRegistry`——宿主工具装配不被外部插件生命周期污染、`/api/ai-agent/chat/tools` 清单与使用统计不受影响、无同名冲突面。合并发生在枚举出口单点（网关与内置 AIAgent 共用同一 `ListToolsToolFunction`，一处改两侧同时生效）。
3. **调用可达两侧同语义**：网关 `UniversalToolForwarder` 原有 `mcp.` 路由保持；内置 AIAgent `universal_tool`（v1.9.0）新增 `mcp.` 前缀路由分支，经跨插件契约 `IMcpExternalGateway`（`ForgeSelf.Abstractions`）转发，与网关同一全名解析真源 `McpExternalToolNames`；McpCenter 缺席时中文引导降级，不落「未知工具」。
4. **只列已连接服务器**（BR-2）：未连接/禁用中的服务器不出现在枚举——与直调前置条件一致，防「列出来却调不了」。
5. **纯增量零破坏**（BR-3）：无外部连接或 McpCenter 停用时，`list_tools` 输出与 v2.5.x 逐字段等价（除新增 `source` 字段）。

## 依据

- 用户拍板（输入 2026-10-19）：注册外部 MCP 却「工具内外都用不了」违背添加初衷——D10 原裁决（v2.1.0）为防注册表污染而**全隐藏**外部工具，实际把「隔离」做成了「不可发现」，发现成本转嫁给界面/管理 API。
- 架构标准姿势（`docs/06-research/001` §5.6 裁决 A/F、architecture-design 铁律 2/4）：跨插件能力 = Abstractions 契约 + 提供方 eager `ctx.Register<T>`（提供即 effect，Fiber 逆序摘除）+ 消费方运行期 `ctx.Get<T>()` 禁缓存；判例 `IWorkflowAIAdvisor`。
- ALC 隔离事实：AIAgent 无法引用 McpCenter 程序集 ⇒ 全名命名/拆分规则必须上移契约层（`McpExternalToolNames` 单点真源），Spec 原定「Manager 持拆分」实施时被迫上移（03-plan 偏差记录）。
- 枚举热路径不变量（CodeReview Major）：契约文档承诺 `ListConnectedTools` 零 IO ⇒ 实现走 `McpClientManager.ConnectedSessions()` 内存快照，禁止经 `GetStates()` 读盘+脱敏。

## 后果

- 正面：外部工具「加进来就能用」——任何 universal_tool 消费方（网关外部客户端、内置数字助手）先 `list_tools` 发现、再按全名直调，无需知道界面/管理 API 旁路；两消费侧看到同一份合并结果（单点杠杆）。
- 负面：`list_tools` 输出随外部连接状态波动（连接即出现、断开即消失），调用方需容忍条目集合动态性；`keyword` 过滤对宿主/外部两侧同时生效（期望值手算易错，单测已锚定）。
- 边界：外部工具依旧不进 `/api/ai-agent/chat/tools`（模型侧原生工具清单）；若未来要求外部工具成为模型一等工具，属重新评估 D10 隔离侧的新决策，不在本 ADR 范围。
- 卸载窗口语义：`ctx.Register<IMcpExternalGateway>` 置于停会话 `ctx.Effect` **之后**（Fiber 逆序回滚 ⇒ 网关先摘、后会话才停），消灭「会话已停、契约尚在」脏窗口（CodeReview Minor 闭环，理由已固化在 `McpCenterPlugin.cs` 注释）。
