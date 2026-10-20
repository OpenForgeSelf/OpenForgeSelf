# Spec

> 阶段：Stage 2 ｜ Task ID：PILOT-072 ｜ 日期：2026-10-19
> 架构依据：architecture-design 技能铁律 2/4 + `docs/06-research/001` §5.6 裁决 A/F（Register 提升 root 共享表、随 Fiber 摘除）+ FAQ Q1（插件互通标准答案 = eager 构造 + `ctx.Register<T契约>` + 消费方每次 `ctx.Get` 可降级，判例 `IWorkflowAIAdvisor`）。

## Functional Requirements

- **FR-1 跨插件契约**：`ForgeSelf.Abstractions` 新增 `IMcpExternalGateway`（插件→插件能力供给，宿主设计契约层）：
  - `ListConnectedTools()` → `IReadOnlyList<McpExternalToolInfo>`（同步；仅已连接服务器的工具）
  - `Task<string> InvokeAsync(string fullName, string argumentsJson, CancellationToken ct)`（`fullName` 含 `mcp.` 前缀全名；失败抛异常，由消费者按各自封套呈现）
  - `McpExternalToolInfo(string FullName, string ServerId, string Name, string Description, string InputSchemaJson)`（record，入 Abstractions）
- **FR-2 McpCenter 提供能力**：`McpCenterPlugin.Apply` eager 构造薄适配（包 `McpClientManager`）并 `ctx.Register<IMcpExternalGateway>(adapter)`——卸载/热重载随 Fiber 自动摘除，消费侧自动降级。
- **FR-3 list_tools 合并外部工具**：`ListToolsToolFunction` 输出 = 宿主注册表条目（现状不变）+ 外部工具条目（`name` 用 `FullName`，如 `mcp.deepwiki.ask_wiki_question`；`pluginId` = 服务器 id；`source="mcp-external"`，宿主条目 `source="registry"`）。`keyword` 过滤对 name/description 同规则（OrdinalIgnoreCase Contains）；`includeSchema` 时外部条目反序列化 `InputSchemaJson`，解析失败置 null（与现状同宽松语义）。
- **FR-4 AIAgent 外部路由**：`UniversalTool.ExecuteAsync` 在「软依赖注册表按名分发」**之前**插入分支：`tool` 以 `mcp.` 开头 → `_ctx.Get<IMcpExternalGateway>()`（每次取，禁缓存字段）：
  - 契约 null → `{success:false, error:"外部 MCP 能力不可用：请确认 MCP 中心插件已启用"}`（不落 unknown）；
  - 调用抛异常 → `{success:false, error: ex.Message}`（与网关错误封套同形）;
  - 成功 → 原样返回工具结果文本。
- **FR-5 描述文案同步**：网关 `UniversalToolForwarder.ToolDefinitionJson` 与 AIAgent `UniversalTool.Description` 更新——「发现工具：`list_tools` 现已含已连接外部 MCP 工具（mcp. 前缀全名），可直接按名调用」。

## Input / Output

- 输入：既有两种调用形态不变（`universal_tool {tool, parameters}`；`list_tools {keyword?, includeSchema?}`）。**不新增任何入参**。
- 输出：`list_tools` JSON 仅**增量**（新条目 + `source` 字段），既有字段名/含义不变；外部条目示例 `{"name":"mcp.deepwiki.ask_wiki_question","description":"…","pluginId":"deepwiki","source":"mcp-external"}`。

## Business Rules

- BR-1（D10 修订裁决）：外部工具**枚举可见 + 调用可达**，但仍**不进 `IToolRegistry`**——注册表隔离保住（避免宿主工具装配被外部插件生命周期污染、使用统计误算、同名冲突）；合并发生在枚举出口而非注册表。
- BR-2 只列**已连接**服务器的工具；未连接/连接失败/禁用中的服务器不出现（与直调前置条件一致，防「列出来却调不了」）。
- BR-3 纯增量零破坏：McpCenter 停用 → AIAgent `mcp.` 分支报明确错误；无外部连接 → `list_tools` 结果与现状逐字节等价（除新增 `source` 字段）。

## Boundary Conditions

- BC-1 外部条目排序在宿主条目之后，两段内部各按名排序（稳定）。
- BC-2 外部全名与宿主名不会冲突（宿主名不含 `mcp.` 前缀保留段；即便重名，路由以 `mcp.` 前缀判段互斥）。
- BC-3 `GetStates()` 为内存快照，枚举零 IO 零异步；`InputSchemaJson` 为空串时 `includeSchema` 输出空对象。
- BC-4 单服务器枚举异常（理论上不应发生）不拖垮整体枚举——按服务器 try/catch 跳过并记 Warn。

## Error Handling

- `InvokeAsync` 坏格式（非 `mcp.<id>.<tool>` 三段）/未连接/工具不在清单 → 抛异常（消息沿用 `CallExternalAsync` 及既有校验的中文语义），AIAgent 侧转 `{success:false,error}` 封套；网关侧现有封套不变。
- `ListConnectedTools` 内部吞异常保枚举宽松语义（发现入口宁缺勿崩）。

## Failure Modes（新功能，无新缺陷类；相邻 lens 预填）

- FM-1（PC-008 lens：协议合规）：本批不触碰传输与 initialize 协议面——不适用，理由=只消费已连接会话的既有缓存。
- FM-2（同类扫查 lens：改一处查另一处）：消费侧两个（网关 forwarder 描述 + AIAgent universal_tool），FR-5 强制两处文案同改；枚举出口一处（ListToolsToolFunction）天然双端共享。
- FM-3（热重载悬空，裁决 F 已知风险）：McpCenter 卸载后共享表条目摘除 → AIAgent `Get` 得 null → 走 BR-3 降级分支；单测覆盖「契约缺失」用例锚定。
- FM-4（captive dependency）：适配器只包 Manager（均为插件 eager 单例），不持宿主 Scoped 服务——合规。

## Compatibility

- `list_tools` 消费方（外部 MCP 客户端 / AIAgent 转发）：JSON 增量字段，无破坏；既有单测断言（total 计数等）需同步适配新增条目——仅 mock 环境无外部连接时不受影响，集成用例显式断言。
- Abstractions 纯新增接口（无既有契约变更）；宿主 `HostProvidedServiceContracts` 清单**不动**（本契约属插件→插件，FAQ Q6：两来源一表，不进 seed 清单）。
- 风险分级：**中**（公共契约层新增 + 跨插件接缝）→ 全量 Verify（`dotnet build` + 相关域全量 `dotnet test`）。

## Non-functional

- 零新依赖；`list_tools` 增量路径同步零阻塞；错误消息中文可操作。

## Acceptance Criteria

- [ ] AC-1 连接外部 mock 后，`list_tools`（无 keyword）含全部 `mcp.<id>.<tool>` 条目（名称=全名、描述非空、`source="mcp-external"`、`pluginId=<服务器id>`）。
- [ ] AC-2 `keyword` 过滤命中外部条目（按全名或描述）；`includeSchema=true` 时外部条目带其 input schema。
- [ ] AC-3 网关与 AIAgent 两侧 `universal_tool {tool:"list_tools"}` 返回同一合并结果（共用实现有单测证据）。
- [ ] AC-4 AIAgent `universal_tool` 直调 `mcp.<id>.<tool>` 成功返回工具文本；异常时返回 `{success:false,error}`；契约缺失时返回「MCP 中心插件已启用」提示且**不落 unknown tool**。
- [ ] AC-5 无任何外部连接时 `list_tools` 宿主条目与现状一致（既有单测全绿，仅允许可期断言更新）。
- [ ] AC-6 e2e：mock 连接 → 网关 `list_tools` 断言含 `mcp.<id>.echo` → 经 `mcp.<id>.echo` 实调成功。
- [ ] AC-7 `dotnet build` 0 错；`~McpCenter` + `~AIAgent` + `~Abstractions` 域测试全绿；版本 McpCenter 2.5.2→**2.6.0**、AIAgent 1.8.0→**1.9.0**（功能级新增，推荐拍板）。

## 交互设计

**不适用**——本批零用户可见 UI 改动（纯后端枚举/路由；McpCenterView 不动）。唯一「可见面」是 `list_tools` JSON 与工具描述文本，形状变化已在上节 Compatibility 交代。
