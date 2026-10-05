# Intent

> 阶段：Stage 1｜Task ID：PILOT-052（2026-10-05-mcp-center-endpoint-url）｜日期：2026-10-05
> 来源：用户 2026-10-05 输入1；对应 2026-09-27 输入16 的历史欠账（`TODO.md:166` 暂缓项）

## Problem

MCP 中心的界面把「放哪里连」显示成 `http://127.0.0.1:18890`（纯 host:port），而该网关**真实的对外端点是 `POST /mcp`**——根路径返回 404（2026-09-27 输入15 实测结论）。用户照着界面把地址复制进 MCP 客户端，**必然连不上**，且现象是「网关坏了」而不是「地址少了一段路径」。

现状（实测，2026-10-05 工作区）：`Plugins/McpCenter/web/src/McpCenterView.vue` 三处地址展示全部是裸 `listenUrl`：

- `:552-553` 顶部 chip；
- `:651-653` 「MCP 服务地址」卡（无 `data-*` 挂点，e2e 无法精确断言）；
- `:181-191` 「复制地址」复制的也是裸 `listenUrl`。

## Why

- 界面是用户唯一被告知「该填什么」的地方；给出一个连不上的地址 = 功能上的错误信息，不只是措辞问题。
- 用户已经在 2026-09-27（输入16）下过这条任务（原话「增加地址，不写路径会让人误会」），当时因转入输入17（发布规范改造）搁置，至今零实施。
- 这件事在本仓库**已有做对的先例**（设计系统插件交付页 `mcpEndpoint()` + `[data-mcp-url]` + e2e 常驻判据），MCP 中心属于**同一事实两套表现**，修它是补一致性，不是新增设计。

## Expected Outcome

用户在 `/mcp-center` 界面看到的「MCP 服务地址」与复制到的地址，都是**可直接粘进 MCP 客户端的完整端点**：

- 顶部 chip = `http://<host>:<port>/mcp`（`title` 同值）；
- 「MCP 服务地址」卡 = 同值，并带一行说明「客户端须使用 `/mcp` 路径」；
- 「复制地址」复制的 = 同值；
- 真实绑定地址（`<host>:<port>`）仍在「运行状态」卡的「监听 …」一行可见，不丢事实。

## Constraints

- **不改后端契约**：`McpGatewayConfig.ListenUrl` 同时是 Kestrel 的真实绑定串（`McpGatewayServer.cs:55`），保持原样；`McpCenterConfigDto` / `GET /api/mcp-center/config` 字段不变。
- 不新增依赖（该插件前端无 vitest，**不为本任务引入测试框架**）；不新增网络请求（端点地址由已有 `listenUrl` 现算）。
- 不改宿主、不改其它插件、不改 `Controllers/`、不改鉴权、不碰 DB。
- 版本号：插件功能可见变化 ⇒ `plugin.json` 与 `McpCenter.csproj` 同串升 `2.2.1`。
- 本轮只做本地工作：**不 commit / 不 push / 不打 tag / 不启停任何宿主进程**（等用户明确指示）。

## Success Criteria（可验证）

1. 插件层 e2e 断言：`/mcp-center` 顶部 chip 文本 **等于** `GET /api/mcp-center/config` 的 `listenUrl` + `/mcp`；「MCP 服务地址」卡（`[data-mcp-url]`）文本同值（判据不许退化成「包含 127.0.0.1」）。
2. `Plugins/McpCenter/web` `pnpm run build` 成功，产物 `dist/index.js` 内可检出 `/mcp` 展示逻辑。
3. 宿主前端 `pnpm run check` 0 error；后端 `dotnet test --filter "FullyQualifiedName~McpCenter"` 全绿（回归）。
4. `docs/02-features/034-mcp-center.md` 的「前端界面」节与新增验证记录与实现一致。
5. 抗误导性：界面任意位置（含复制到剪贴板的字符串）**不再出现**作为"可连接地址"给出的裸 `http://host:port`（绑定地址只以「监听 host:port」形式出现在运行状态卡）。
