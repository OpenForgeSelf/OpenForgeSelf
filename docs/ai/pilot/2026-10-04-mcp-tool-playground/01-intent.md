# Intent

> 阶段：Stage 1｜只描述「为什么做 / 做什么 / 做到什么程度」，**不提前决定具体代码实现**。
> Task ID：2026-10-04-mcp-tool-playground｜日期：2026-10-04

## Problem

MCP 中心插件已经能连上外部 MCP 服务器并拉取工具清单，但**到了"看工具"这一层就断了**：

1. 界面上「外部服务器工具」弹窗只把 `fullName` + `description` 平铺成两行文本，**`inputSchema` 拉回来了却从未使用**（`McpExternalToolDto.InputSchemaJson` 有值，前端 `types/external.ts` 里声明的 `inputSchema` 字段从未被赋值也从未被读取）。
2. **没有任何"真调用一下试试"的入口**：现有 `POST {id}/test` 只做 `initialize + ping` 握手测试，验证不了"这个工具传这些参数到底返回什么"。要验证工具行为，只能绕到宿主的 `universal_tool`（`mcp.<id>.<name>` 转发名）从外部 MCP 客户端侧去打，链路长且看不到 raw 结果。
3. 结果是：接入一台外部 MCP 服务器后，用户不知道每个工具要传什么参数、哪些必填、默认值是什么，也无法就地确认它是否能正常工作——**可观测性和可调试性都缺一环**。

## Why

- 用户明确要求（输入1）：「完善 mcp 插件，增加测试功能，根据 mcp 工具返回的具体工具列表，参数说明，界面动态可视化，可输入参数发起调用」。
- MCP 生态里"工具能不能调通"是最容易出错、也最难排查的一环（参数类型不对、必填漏了、服务器返回 `isError`）。把它做成界面上的一等公民，能把排障从"猜"变成"看"。
- 插件已经具备全部底层能力（会话、传输、调用、schema 快照），缺的只是**把 schema 渲染成表单 + 把调用结果呈现出来**这一层，属于低风险增量。

## Expected Outcome

在 MCP 中心插件的「外部服务器工具」弹窗内得到一个**工具测试台**：

1. 选中某个工具 → 按它的 `inputSchema` **动态渲染参数表单**（字段名、类型、是否必填、说明、默认值、枚举候选）。
2. 在表单里填参数（或切到 JSON 模式直接编辑）→ 点「调用」→ 看到**调用耗时、成功/失败、文本内容、原始 JSON**。
3. 用 DeepWiki MCP（`https://mcp.deepwiki.com/mcp`）作为内置联调靶子，一键填入预设即可实测。

## Constraints

对照规范 §1 硬性约束逐条：

1. 不修改生产环境 —— 本次只在 `Plugins/McpCenter` 内改动，不碰 `publish/`、不停宿主进程。✅
2. 不修改数据库结构 —— 本插件无 XCode 实体，配置落 `external-servers.json`；本次**不新增持久化数据**。✅
3. 不修改鉴权/权限/支付/安全核心逻辑 —— 新端点落在**已有** `McpExternalController`（类级 `[Authorize("ApiKeyPolicy")]`）内，继承既有鉴权，**不改策略本身**。✅
4. 不新增大规模依赖 —— 前端新增纯 TS/组件，不引第三方表单库；后端不引新 NuGet 包。✅
5. 不进行无关重构 —— 不改 `StreamableHttpMcpTransport` 的会话处理、不改 `CallToolAsync` 现有返回值语义。✅
6. 不修改与本任务无关的文件。✅
7. 不为展示能力扩大范围 —— 不做"工具调用历史持久化""批量调用""资源/提示词（resources/prompts）浏览"等未要求能力。✅
8. 最终必须能跑实际测试或构建命令验证。✅
9. 结论必须基于真实仓库内容。✅
10. 失败不允许伪造成功。✅

额外项目级约束：改完插件需走 `plugin-development` §四 维护闭环（门禁 → 插件层 e2e → 发布 → 走查 → 运行实例只读复验）；发布/提交需用户授权（闸门2/3）。

## Success Criteria

1. 后端存在 `POST api/mcp-center/servers/{id}/tools/invoke`，无宿主令牌访问返回 **401**（继承控制器 `[Authorize("ApiKeyPolicy")]`）。
2. 对一台已连接的外部服务器，传入工具名 + JSON 参数，端点返回 `{ ok, isError, text, rawJson, elapsedMs }`；`elapsedMs > 0`。
3. 前端「外部服务器工具」弹窗中，每个工具可展开，参数表单由 `inputSchema` 动态生成：**必填字段有标记、`enum` 渲染成下拉、有 `default` 则预填、`anyOf` 能解析出可用类型**（DeepWiki `ask_wiki_question.repoName` 即 `anyOf[string, array<string>]`）。
4. 在表单填参后点「调用」，能真实拿到 DeepWiki 返回内容并展示；调用失败时展示错误原因而非静默。
5. 新增单元测试覆盖：schema→表单描述符的纯函数（含 `anyOf`/`required`/`default`/`enum`/嵌套 object/array）、后端 invoke 端点（成功 / 工具不存在 / 未连接 / 鉴权 401）。
6. `dotnet build`、`dotnet test --filter McpCenter`、插件 `web` 构建、宿主 `pnpm run check && pnpm run test` 均实际通过。
7. 插件版本号按项目约定升位并在界面徽标可见（`plugin.json` 2.2.0 → 2.3.0）。
