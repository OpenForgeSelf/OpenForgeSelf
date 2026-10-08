# 034 - MCP 中心（mcp-center 插件）

> 插件形态：`ForgeSelf.Api/Plugins/McpCenter/`，运行时 id `mcp-center`，当前版本 **2.2.0**。
> 自带界面（`/mcp-center`，双 tab：工具管理 + 网关配置），经宿主远程加载（`frontend.entry = web/dist/index.js`）。
> 前身：`mcp-gateway` v1.0.0（034-MCP 统一网关）更名 + 整合宿主内置「MCP 工具」管理（`api/mcp` + McpService）合并而成。

## 功能定位

对外通过**独立 MCP 端口**暴露本项目全部工具，让任意 MCP 客户端（Claude Desktop / Cursor / 自研客户端等）能直接调用宿主工具注册表中的工具；同时在自带界面内管理**外部 MCP 服务器与工具开关**（原宿主 MCP 工具页功能），并提供**网关地址/端口/令牌的查看与修改**（解决「网关地址在哪看、怎么设 token」）。

**对外只保留一个万能工具 `universal_tool`**：入参 `{tool, parameters}`，内部按名转发调用宿主 `IToolRegistry.ExecuteAsync(ToolExecution)` 六闸门执行面，结果原样透传。整体对外只有一个工具，与「只保留那个万能的工具，通过传参转发调用其他的工具」一致。

## 四大能力

| 能力 | 说明 | 入口 |
|---|---|---|
| ① 对外 MCP 服务端 | 独立端口 JSON-RPC 2.0，仅暴露 `universal_tool`，转发宿主全部工具 | MCP 端口 `18890`（唯一默认；用户可覆盖；e2e 走 worktree 派生的 19000–19899） |
| ② 外部 MCP 服务器/工具管理 | 预置 4 台服务器（ForgeSelf Local/Filesystem/GitHub API/Database，12 个预置工具）+ 工具启用开关 + 测试 | `api/mcp/servers`、`api/mcp/servers/{id}/tools`、`/mcp-center` 工具管理 tab |
| ③ 网关配置 | 查看监听地址/端口/令牌状态（脱敏）、修改并热重启 | `api/mcp-center/config`（GET/PUT）、`/mcp-center` 网关配置 tab |
| ④ 外部 MCP 客户端（v2.1.0） | 按标准 MCP 协议连接外部 MCP 服务器（stdio / Streamable HTTP / HTTP+SSE 三传输），其工具经 `universal_tool` 的 `mcp.<服务器id>.<工具名>` 命名空间统一转发 | `api/mcp-center/servers`（GET/POST/PUT/DELETE + `{id}/connect|disconnect|tools|test`）、`/mcp-center` 网关配置 tab「外部 MCP 服务器」区块 |

> 注：②的「外部服务器/工具」为预置管理数据（内存态），不改变①的对外协议面（对外恒 1 个 `universal_tool`）。④是**真实** MCP 客户端（v2.1.0 新增），外部工具**不进宿主注册表**（决策 D10），统一经 `mcp.` 前缀命名空间转发。

## 为什么用「单工具转发」而不是逐工具注册

| 方案 | 问题 |
|---|---|
| 逐工具注册进 IToolRegistry | 与 AIAgent（031）的 `UniversalTool` 同名工具键冲突；宿主工具集随插件增删而漂移，MCP 端需频繁刷新 |
| **单工具转发（本方案）** | 工具清单恒 1 条、契约稳定；新增宿主工具无需改网关；目标工具不过滤（用户要求可调全部工具） |

安全护栏不因转发而绕开：目标工具仍走宿主分发核既有的 `tools/pre-execute` 拒绝门（含命令工具 `TerminalCommandGuard`）与使用统计。

## 架构

```
外部 MCP 客户端 ──JSON-RPC 2.0 (Streamable HTTP)──> McpGatewayServer（自托管 Kestrel，独立端口）
                                                        │
                                                        ▼
                                              McpJsonRpcHandler
                                              ├─ initialize（协议版本协商，serverInfo.name="ForgeSelf McpCenter"）
                                              ├─ ping
                                              ├─ tools/list（恒 1 条 universal_tool）
                                              └─ tools/call → UniversalToolForwarder
                                                        │
                                                        ▼
                                     IToolRegistry.ExecuteAsync(ToolExecution)（六闸门）
                                                        │（宿主分发核：pre-execute 三态决策 → 单调守卫
                                                        │  → execute waterfall → post-execute waterfall
                                                        │  → finalize 恰好一次 → tools/result 冻结快照 → 使用统计）
                                                        ▼
                                         宿主工具（calculate / 文件 / 命令 / …全部工具）

宿主 Kestrel（51888/7102）
  ├─ /api/mcp/*          → McpController（② 服务器/工具管理，路由前缀 api/mcp 保留宿主契约）
  ├─ /api/mcp-center/*   → McpCenterConfigController（③ 网关配置查看/修改/热重启）
  │                        └─ McpExternalController（④ 外部 MCP 服务器 CRUD/连接/工具/测试）
  ├─ /api/ai-agent/chat/tools → AIAgent 消费的宿主工具清单（Agent 不受 MCP 端口影响，见「与 Agent 的关系」）
  └─ /plugin-view/mcp-center → 插件自带界面（双 tab）
```

**④ 外部 MCP 客户端（v2.1.0）**：

```
外部 MCP 服务器（DeepWiki / 任意 MCP 服务器）
  ├─ stdio          → StdioMcpTransport（进程：npx/node/python/python3/uvx/uv/dotnet 白名单）
  ├─ Streamable HTTP → StreamableHttpMcpTransport（单端点 POST /mcp）
  └─ HTTP+SSE       → LegacySseMcpTransport（GET /sse 发现 endpoint → POST /mcp，202 + SSE 流回传）
        │
        ▼
McpClientSession（initialize 协议协商 → tools/list → tools/call，零 SDK 手写客户端）
        │
        ▼
McpClientManager（单例：会话字典 + 错误字典 + TryConnect/Disconnect/CallExternalAsync/GetStates/TestAsync）
        │
        ▼
UniversalToolForwarder（tool 以 "mcp." 前缀 → 按首段点拆 服务器id.工具名 → CallExternalAsync）
        ▲ 外部工具不进宿主注册表（决策 D10）
        │
        └── tools/call universal_tool（对外仍恒 1 个工具）
```

- **传输格式全支持（用户要求「标准 MCP 协议 2.0 所有格式转发」）**：stdio / Streamable HTTP / 旧版 HTTP+SSE 三传输；协议版本协商 `2025-11-25`（**MCP 2.0**）/ `2025-06-18` / `2025-03-26` / `2024-11-05`（客户端与服务端双向，v2.2.0 起支持 2.0）。

- 协议：手写最小 JSON-RPC 2.0 + Streamable HTTP（默认协议版本 2025-06-18，v2.2.0 起支持协商 2025-11-25 = MCP 2.0），**零新增依赖**（无 MCP SDK）。MCP 2.0 的 OAuth / tasks / icons / elicitation 等均为**可选能力**，本网关不声明相应 capabilities 即合规，不实现。
- 传输：`POST /mcp`（JSON 响应）、`GET /mcp`（SSE keep-alive 心跳）、`GET /health`（探活）。
- 生命周期：插件 `IPlugin.Apply` 里自管（铁律 14）：`StartAsync` 幂等启动自托管 Kestrel；`ctx.Effect` 挂停止器，热重载逆序释放先停服务器再卸载 ALC。
- 软依赖：`ctx.Get<IToolRegistry>()`（宿主 seed 晚于 Apply），拿不到时**仅预置数据 + Warn，网关照常启动**（`McpService` 降级、调用转发正常——转发器经 `IContext` 运行期取宿主工具注册表，与 McpService 的软依赖相互独立）。
- 插件服务注册：`ctx.Get<IServiceCollection>()` 注册 `IMcpService`（② 用）与 `McpCenterRuntime`（③ 用）单例 → `PluginServiceRegistry.Mount` 子 provider → 控制器经 `PluginAwareControllerActivator` 解析。

## 配置（三级优先序）

环境变量 → 插件数据根 `config.json` → 内置默认：

| 项 | 环境变量 | config.json 键 | 默认值 |
|---|---|---|---|
| 端口 | `FORGESELF_MCP_GATEWAY_PORT` | `port` / `Port`（大小写不敏感） | `18890` |
| 监听地址 | `FORGESELF_MCP_GATEWAY_HOST` | `listenHost` | `127.0.0.1` |
| Bearer 令牌 | `FORGESELF_MCP_GATEWAY_TOKEN` | `token` | 空（不鉴权） |

> **兼容决策（v2.0.0 更名时保留）**：环境变量前缀**沿用旧名** `FORGESELF_MCP_GATEWAY_*`，不改为 `MCP_CENTER_*`——兼容既有运维/e2e（`playwright.config.ts` 走 env 覆盖 e2e 端口的写法）与已写死该前缀的部署脚本；类名亦保留（`McpGatewayConfig/McpGatewayServer/...`），仅 namespace/日志前缀/`serverInfo.name` 改。config.json 数据目录随更名迁移到 `{宿主数据根}/plugins/mcp-center/`（旧 `mcp-gateway/` 目录保留未删）。

- 插件数据根：`{宿主数据根}/plugins/mcp-center/`（publish 实例 = `~/.forgeself/plugins/mcp-center/`）。
- config.json 缺失时自动生成默认值（幂等）。
- **端口占用约定**（2026-10-04 统一）：**默认端口 = `18890`**（`McpGatewayConfig.DefaultPort`，用户机 `config.json` 未显式设定时即生效）；生产实例、DSH 侧配置全部对齐 `18890`。e2e 走 `playwright.config.ts` 按 worktree 哈希派生的 `19000–19899` 独立段（`FORGESELF_MCP_GATEWAY_PORT` 环境变量），显式避开 `18890`；3 个 spec（`mcp-center.spec.ts` / `design-system-agent.spec.ts` / `sems.spec.ts`）的 env 兜底值为 `18891`（`?? '18891'`），仅在有人绕过 `playwright.config.ts` 直跑单 spec 且未设 env 时才会用到，同样避开生产 `18890`。新机器开箱即为 `18890`，无迁移负担。
- 配置 API `PUT /api/mcp-center/config`：校验端口 1024-65535 → 写 config.json → 停旧服务器 → 启动新服务器热重启；失败回滚旧配置。token 传**空串 = 清除鉴权**，**不传 = 保留**原令牌。

## MCP 协议契约

| 方法 | 请求 | 响应要点 |
|---|---|---|
| `initialize` | `{protocolVersion}` | 版本协商：`2025-11-25`（MCP 2.0）/ `2025-06-18` / `2025-03-26` / `2024-11-05`，未知回退 `2025-06-18`；返回 `serverInfo.name="ForgeSelf McpCenter"`（v2.2.0 起含可选 `description`）、`capabilities.tools.listChanged=false` |
| `ping` | — | 空结果 |
| `tools/list` | — | 恒 1 条：`universal_tool`，`inputSchema.required=["tool"]`；description 含发现工具引导（先调 `list_tools` 枚举）、常规能力分类（读写文件/执行命令/搜索/计算/系统监控/工作流）与外部命名空间说明 |
| `tools/call` | `{name:"universal_tool", arguments:{tool, parameters}}` | 转发目标工具；`tool="list_tools"` 枚举全部已注册工具（名称/说明/参数 schema，`parameters:{keyword?, includeSchema?}`）；`result.content[0].type="text"`，文本为宿主 `ToolExecutionResult` 原样透传（`{"success":true,...,"result":...}`）；失败时 `isError=true` 且保留 `error` 字段 |
| 通知/批处理 | — | 支持通知（无响应）、批量数组 |

错误码（JSON-RPC 标准）：

| 场景 | code |
|---|---|
| `tools/call` 的 `name` 不是 `universal_tool` | `-32602`（Invalid params，附中文提示） |
| 未初始化即调用 | `-32002` |
| 请求体超限（1MB） | `-32600` |
| 解析失败 | `-32700` |

## 宿主 HTTP 契约（MCP 中心自带界面与外部调用）

| 端点 | 方法 | 说明 |
|---|---|---|
| `api/mcp/servers` | GET | 外部 MCP 服务器列表（预置 4 台 + ToolRegistry 实时同步，T032 语义） |
| `api/mcp/servers/{serverId}/tools` | GET | 某服务器工具列表（`keyword`/`category` 查询参数） |
| `api/mcp/servers/{serverId}/test` | GET | 服务器连接测试（模拟，connected 判定） |
| `api/mcp/tools/{toolId}/toggle` | POST | 工具启用/禁用切换 |
| `api/mcp/tools/{toolId}/test` | POST | 工具测试（模拟，50-300ms） |
| `api/mcp-center/config` | GET | 网关配置脱敏视图（`port/listenHost/listenUrl/hasToken/tokenMasked/isRunning/version`） |
| `api/mcp-center/config` | PUT | 修改配置并热重启（`{port?, listenHost?, token?}`；token 空串=清鉴权、不传=保留） |

> 控制器位于插件 `Controllers/`（`McpController` 路由 `api/mcp` 保留宿主契约、前端调用零改动；`McpCenterConfigController` 路由 `api/mcp-center/config`；`McpExternalController` 路由 `api/mcp-center/servers`）。
>
> **管理面鉴权（v2.1.0+）**：上述全部管理端点（`api/mcp*` / `api/mcp-center/*`）类级 `[Authorize("ApiKeyPolicy")]`——未带宿主 API 令牌一律 **401**，带 `Authorization: Bearer <宿主令牌>` 才 200。宿主**无全局鉴权中间件**，鉴权逐控制器显式（插件控制器不会自动被保护；本项曾裸 curl 200 已修复，回归 `McpAdminAuthTests`）。对外 MCP 端口（18890）令牌（`config.json` 的 `token`）是**另一层**安全，两者独立。

## 外部 MCP 服务器（v2.1.0 客户端）

### 配置模型与存储

- 服务器清单持久化：`{插件数据根}/external-servers.json`（与 `config.json` 分离），原子写、幂等加载、校验。
- 字段：`id`（kebab-case，命名空间路由用）/ `name` / `transport`（`stdio` | `streamable-http` | `http-sse`）/ `enabled` / `command`+`args`（stdio 用，白名单 `npx/node/python/python3/uvx/uv/dotnet`）/ `url`（http/s 用）/ `headers`（可选，值脱敏存储：整替换 `****`+尾4）。
- 连接测试（`{id}/test`）为**真实** initialize+ping（非 v2.0.0 的模拟 sleep）。

### 管理 API（`api/mcp-center/servers`）

| 端点 | 方法 | 说明 |
|---|---|---|
| `api/mcp-center/servers` | GET | 列表（含状态/连接信息/工具数/协议版本，脱敏） |
| `api/mcp-center/servers` | POST | 新增（校验 + 落盘；enabled=true 自动后台建连） |
| `api/mcp-center/servers/{id}` | PUT | 更新（改传输/端点后自动重连） |
| `api/mcp-center/servers/{id}` | DELETE | 删除（断开 + 落盘） |
| `api/mcp-center/servers/{id}/connect` | POST | 手动连接 |
| `api/mcp-center/servers/{id}/disconnect` | POST | 断开 |
| `api/mcp-center/servers/{id}/tools` | GET | 已拉取的外部工具清单 |
| `api/mcp-center/servers/{id}/test` | POST | 真实连接测试（initialize+ping） |

### 转发契约（对外仍恒 1 个 `universal_tool`）

- `tool` 以 `mcp.` 前缀 → 走外部转发：格式 `mcp.<服务器id>.<工具名>`（id 为 kebab-case），按首段点拆分。
- 坏格式（缺工具名段）提示：「外部工具名格式应为 mcp.<服务器id>.<工具名>（当前: ...；例如 mcp.deepwiki.search）」。
- 未连接：「外部服务器 '...' 未连接（请先在 MCP 中心连接并拉取工具清单）」。
- 外部工具**不进宿主注册表**（决策 D10），不影响 Agent 的 `/api/ai-agent/chat/tools` 清单。
- 协议版本双向协商：客户端 initialize 带 `2025-11-25`（MCP 2.0，v2.2.0 起），服务器回退 `2025-06-18`/`2025-03-26`/`2024-11-05` 时客户端以服务器为准。

## 与 Agent（AIAgent）的关系

- Agent 消费宿主工具走**宿主契约** `IToolRegistry` + `/api/ai-agent/chat/tools`，不经 MCP 端口；MCP 中心任何改动不影响 Agent 工具清单。
- MCP 中心对外是「服务端（暴露宿主工具）+ 客户端（接入外部服务器）」双半身，Agent 只依赖服务端半身的宿主工具部分。
- 实测（v2.0.0→v2.1.0 均验证）：`/api/mcp/servers`、`/api/ai-agent/chat/tools`、`/api/health` 全部 200；18890 `/health` 返回 `{"status":"ok","version":"2.1.0","tools":1}`。

## 使用示例（curl）

```bash
# 探活
curl http://127.0.0.1:18890/health

# 列工具（恒 1 个）
curl -X POST http://127.0.0.1:18890/mcp -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'

# 调用宿主工具 calculate（6*7=42）
curl -X POST http://127.0.0.1:18890/mcp -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"universal_tool","arguments":{"tool":"calculate","parameters":{"expression":"6*7"}}}}'

# 发现工具：枚举全部已注册工具（可按关键字过滤、带 schema）
curl -X POST http://127.0.0.1:18890/mcp -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"universal_tool","arguments":{"tool":"list_tools","parameters":{"keyword":"read","includeSchema":true}}}}'

# 管理端点一律需要宿主令牌（无 token → 401）
curl -H "Authorization: Bearer <宿主令牌>" http://localhost:51888/api/mcp-center/servers

# 查看网关配置（脱敏）
curl http://localhost:51888/api/mcp-center/config

# 修改端口并热重启
curl -X PUT http://localhost:51888/api/mcp-center/config -H 'Content-Type: application/json' \
  -d '{"port":18890}'
```

## 前端界面（/mcp-center）

- 顶部：标题「MCP 中心」+ **版本徽标**（铁律 13，`GET /api/plugin` 解包 `.data` 按 id 过滤）+ **MCP 端点地址 chip**（v2.2.1：显示 `listenUrl + /mcp`，即**可直接粘进 MCP 客户端**的地址）+ 运行状态。
- 左侧：MCP 服务器列表（名称 + 工具数），点击联动右侧工具表。
- 工具管理 tab：搜索框 + 分类筛选（全部/系统/文件/网络/数据/开发）+ 工具表（名称/服务器/描述/状态开关/测试按钮）。
- 网关配置 tab：3 状态卡（**MCP 服务地址 = `listenUrl + /mcp`**（v2.2.1：带 `data-mcp-url` 挂点 + 一行「客户端须使用 /mcp 路径（根路径 404）」说明）+ 复制地址 / 运行状态（仍展示真实绑定 `监听 host:port`）/ 访问令牌状态）+ 修改表单（监听端口 1024-65535 / 监听地址 0.0.0.0 局域网提示 / 访问令牌）+ 「保存并重启生效」（失败自动回滚）+ **外部 MCP 服务器区块（v2.1.0）**：列表（状态 ElTag：已连接/未连接/错误）+ 连接/断开/测试/工具/编辑/删除按钮 + 新增/编辑对话框（ID/名称/传输下拉/命令+参数或 URL/环境变量）+ 外部工具清单对话框。
  - ⚠ **地址口径（v2.2.1 起）**：界面展示/复制的地址一律是**端点** `http://<host>:<port>/mcp`（客户端唯一可用地址）；后端 `McpGatewayConfig.ListenUrl` 仍是 **Kestrel 绑定串**（`http://<host>:<port>`，被 `McpGatewayServer` 的 `UseUrls` 使用），**禁止**在后端给它加路径 —— 端点由前端 `mcpEndpointUrl`（去尾斜杠 + `/mcp`，与设计系统插件 `delivery/snippets.ts` 的 `mcpEndpoint()` 同构）派生。
- 插件前端契约：`export { McpCenterView }`（= plugin.json `frontend.views[0]`）；`vue/vue-router/pinia/element-plus` external；`<ElTabs/ElTabPane/ElSwitch/ElInputNumber/ElInput/ElCheckbox/ElButton/ElSelect/ElOption>` 需宿主 `exposeSharedDeps` 暴露（已在 `ForgeSelf.Web/src/shared/exposeSharedDeps.ts` + `public/shared/element-plus.js` 补齐）。

## 验证记录（v2.2.1 · 界面地址补 /mcp 路径）

- 变更：界面三处（顶部 chip / 「MCP 服务地址」卡 / 「复制地址」）从裸 `listenUrl`（`http://host:port`）改为**可直连端点** `listenUrl + /mcp`；卡片增 `data-mcp-url` 挂点与一行「客户端须使用 /mcp 路径（根路径 404）」说明；运行状态卡仍展示真实绑定 `监听 host:port`。**后端零改动**（`ListenUrl` 仍是 Kestrel 绑定串）。版本 2.2.0 → **2.2.1**（plugin.json + csproj 同串）。工件链 `docs/ai/pilot/2026-10-05-mcp-center-endpoint-url/`（00–07）。
- 插件前端构建：`cd Plugins/McpCenter/web && pnpm run build` 成功（`dist/index.js` 49.62 kB / 14.3s）；产物内可检出 `客户端须使用 /mcp 路径（根路径 404）` 与空态 `（未运行，暂无地址）`。
- 插件层 e2e（`mcp-center.spec.ts`，**3 passed / 2.1m**）：新增判据 = chip 文本（含 `title`）**等于** `GET /api/mcp-center/config`.listenUrl + `/mcp`、`[data-mcp-url]` 同值、状态卡仍为 3 张且运行状态卡含「监听 host:port」；**反向腿**：对根地址发 JSON-RPC → **404**，对端点发 → **200** 且 `tools[0]=universal_tool`。实读证据行：`地址展示：config.listenUrl=http://127.0.0.1:19483 → 界面展示=http://127.0.0.1:19483/mcp；根地址 404 / 端点 200`、`host plugin: mcp-center v2.2.1`。
- 后端回归：`dotnet test --filter "FullyQualifiedName~McpCenter"` **96/96 通过**（首次 22 红系本机 `%TEMP%` 下新建目录被拒，把 TEMP 重定向到仓库内 `.temp/` 后全绿 ⇒ 环境问题，非代码）。
- 宿主门禁：`pnpm run check` **0 errors / 81 warnings**（既有基线量级）。
- 视觉：`screenshots/e2e/mcp-center/{gateway-tab,tools-tab}.png` 读图核对（chip 全串不截断、卡值 + 说明行、运行状态卡绑定地址保留）。
- ⚠ 环境依赖（跑本插件 e2e 前必读）：本机设了 `HTTP_PROXY/HTTPS_PROXY=127.0.0.1:10808` 且无 `NO_PROXY` 时，Playwright 对 `localhost` 的 webServer 可用性探测**恒 502 → 120s 超时**（`DEBUG=pw:webserver` 可见）。跑法须带 `NO_PROXY=localhost,127.0.0.1,::1`；详细根因与两份控制实验见 05-evidence。
- 发布与运行实例复验（2026-10-05 追加）：按用户指令做**本地离线整包**（未打 tag）→ `D:\src\my-proj\OpenForgeSelf\updates\OpenForgeSelf-2.7.3.2610051746-win-x64.zip`（106.7 MB；签名 `Valid`、SHA256 与 `SHA256SUMS.txt` **MATCH**、L1/L2/L3 布局不变量通过、包内 mcp-center **v2.2.1**）；用户升级完成后做**只读复验**：`GET /api/plugin` → `mcp-center 2.2.1`（共 18 个插件）、`GET /api/mcp-center/config` → `listenUrl=http://127.0.0.1:18890`（界面即展示 `…:18890/mcp`）、`GET /plugins/mcp-center/web/dist/index.js`（49624 B）sha256 **== 仓库产物**（逐字节一致 ⇒ 跑的就是本批产物）。未做：浏览器截图那一格（以产物逐字节一致作为显式替代判据，见 05-evidence）。

## 验证记录（v2.2.0 · MCP 2.0 协议支持）

- 变更：`McpJsonRpcHandler.SupportedProtocolVersions` 与 `McpClientSession.SupportedProtocolVersions` 白名单加入 `2025-11-25`（MCP 2.0）；默认/回退版本保持 `2025-06-18`（1.x 客户端行为不变）；`initialize` 响应 `serverInfo` 增加可选 `description`（2.0 `Implementation.description`）；版本统一 2.2.0（plugin.json + csproj）。
- 单测（worktree `wt-mcp2`，过滤器 `FullyQualifiedName~McpCenter`）：新增 `Initialize_NewClientVersion2025_11_25_IsNegotiated`（声明 `2025-11-25` → 回显 `2025-11-25`）、`serverInfo.description` 非空断言、客户端声明列表含 `2025-11-25` 断言；未知版本回退 `2025-06-18` 回归通过。
- 插件层 e2e：`mcp-center.spec.ts` 新增 MCP 2.0 声明用例（initialize `2025-11-25` → 回显 `2025-11-25` + description 非空）。
- 已知边界：MCP 2.0 可选能力（OAuth / tasks / icons / elicitation）未实现——本网关 capabilities 不声明即合规；SEP-1303「输入校验错误返回 Tool Execution Error」语义未采纳（保持 `-32602` 现状，见 `docs/ai/pilot/2026-10-01-mcp2-protocol/02-spec.md` D1）。

## 验证记录（v2.1.0）

- 单测：`ForgeSelf.Api.Tests/Plugins/McpCenterTests/` **88/88 绿**（过滤器 `FullyQualifiedName~McpCenter`）——新增 `McpFrameTests`（帧编解码/粘包/UTF-8 字节长度）、`McpExternalConfigTests`、`McpClientSessionTests`（Moq 假传输）、`McpClientIntegrationTests`（真连 node mock 三传输往返 + universal_tool mcp. 转发 + 未连接/坏格式）；旧测试适配转发器新构造签名。
- 插件层 e2e：`ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts` **3 passed**（`test.describe.configure({mode:'serial'})` 串行）——① MCP 端口全链路 + config API；② `/mcp-center` UI 渲染（含外部服务器区块空态）；③ **外部 MCP 服务器接入**：stdio / streamable-http / http-sse 三传输真连 node mock（`Fixtures/mock-mcp-server.js`，工具 echo/add）→ waitConnected（toolCount=2、stdio 协议 2025-06-18）→ `universal_tool` 的 `mcp.<id>.<tool>` 转发断言（add(3,4)=7 / echo 中文 / add(10,32)=42）→ 坏格式错误提示 → finally DELETE 清理 + kill mock。证据：`screenshots/e2e/mcp-center/external-mcp.log`。
- 发布验证（生产 51888 + 18890）：冷启动后 `GET /api/plugin` 含 `mcp-center v2.1.0`；`127.0.0.1:18890/health` → `{"status":"ok","version":"2.1.0","tools":1}`；`/api/mcp-center/config` isRunning=true；**生产端到端**：POST stdio 外部服务器 → connected（toolCount=2）→ 18890 `universal_tool` `mcp.prod-verify2.add(20,22)` → `{"sum":42}` → DELETE 清理。
- 浏览器走查（生产 51888 /mcp-center，`screenshots/e2e/mcp-center/prod-*.png`）：工具管理 tab（v2.1.0 徽标/地址 chip/4 服务器/工具表正常，无破图/溢出）、网关配置 tab（3 状态卡/修改表单/**外部 MCP 服务器区块**：说明 + 新增服务器按钮 + 空态）、新增服务器对话框（ID/名称/传输下拉/命令/参数/环境变量 + 统一工具名提示）打开与取消。
- 宿主门禁：`pnpm run check` 0 errors（~232 warnings 基线）；vitest 476 passed/17 failed（17 均既有漂移，与本次无关）；`dotnet build` 0 error；插件前端 `pnpm run build` 成功。

## 验证记录（v2.0.0）

- 单测：`ForgeSelf.Api.Tests/Plugins/McpCenterTests/` **63/63 绿**（过滤器 `FullyQualifiedName~McpCenter`），含 McpServiceTests(12)、McpCenterRuntimeTests(8)、McpControllerIntegrationTests、McpServiceToolRegistryTests 与网关 3 件套（转发器/处理器/配置）迁移。
- 插件层 e2e：`ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts` **2 passed**——① 宿主清单（含 mcp-center 不含 mcp-gateway）+ MCP 端口全链路（health/initialize serverInfo=ForgeSelf McpCenter/tools/list 恒 1 universal_tool/call calculate(6*7)=42/未知工具 isError/未知 name -32602）+ config GET 脱敏 + PUT 改端口热重启后恢复；② `/mcp-center` UI 渲染（标题/徽标/地址 chip/4 服务器/工具表/双 tab/状态卡 3/表单）。截图：`screenshots/e2e/mcp-center/{tools-tab,gateway-tab,walkthrough-tools-tab}.png`。
- 发布验证（生产 51888 + 端口 18890）：冷启动 `GET /api/plugin` 含 `mcp-center v2.0.0`（name「MCP 中心」）不含 `mcp-gateway`；`127.0.0.1:18890` 全链路通过；`/api/mcp/servers` 4 台、`/api/mcp-center/config` 脱敏正常；浏览器走查双 tab 全项（服务器切换联动/测试按钮/分类筛选/网关配置表单）通过。
- 宿主门禁：`pnpm run check` 0 errors（~232 warnings 基线）；vitest 476 passed/17 failed（17 均既有漂移，与本次无关）；`dotnet build` 0 error。

## 发布与迁移记录（v1.0.0 → v2.0.0）

- 目录更名：`Plugins/McpGateway/` → `Plugins/McpCenter/`（清理 bin/obj）；csproj `AssemblyName=McpCenter`、`RootNamespace`、`<Version>2.0.0</Version>/<AssemblyVersion>/<FileVersion>`（版本徽标数据源）。
- `plugin.json`：`Id=mcp-center`、`Name=MCP 中心`、`Version=2.0.0`、`frontend.views=["McpCenterView"]`、`route=/mcp-center`、`icon=connection`、`entry=web/dist/index.js`。
- 宿主接线：`ForgeSelf.Api.csproj` 全量 `McpGateway→McpCenter`（13 处含 Stage target）；`AppBuilder.cs` 移除宿主 `AddSingleton<IMcpService, McpService>()`（迁入插件）；宿主 mcp-tools 后端（McpController/Services-Mcp/Models-Mcp/web 六件套/e2e-mcp-gateway）移 `.trash/mcp-tools-host-2026-09-22/`。
- 宿主前端清理（六处）：删 `McpToolsView.vue`/`mcpApi.ts`/`types/mcp.ts`/`McpToolsView.test.ts`（移 .trash）；router 删 `/mcp-tools`；`stores/tabs.ts` DEFAULT_TABS 5→4；`data/features.ts` mcp+mcp-gateway 合并为 mcp-center 一条；`tabs.test.ts` 计数同步。
- 发布产物：`publish/plugins/McpCenter/`（McpCenter.dll + plugin.json + web/dist）；旧 `publish/plugins/McpGateway` 归档 `publish/plugins/_backups/mcp-gateway-1.0.0/`；重复 id 遗留目录（小写）归档 `_backups/`。

## 已知边界与踩坑记录

- **全新插件目录不被热重载监听**：宿主 `PluginHotReloadWatcher` 只对**已加载插件**的更新生效；新增插件需「冷启动宿主」（`publish/ForgeSelf.exe --console`）。运行中插件更新 DLL 时文件被 ALC 锁定，同样需重启替换。
- **宿主进程锁导致 build.ps1 发布漏更宿主 DLL（本次实证）**：`publish/ForgeSelf.dll` 被运行中宿主锁定 → `build.ps1` 第 3 步 `Copy-Item -ErrorAction SilentlyContinue` **静默跳过**被锁文件 → publish 里宿主 DLL 保持旧版（仍含已迁走的旧 `ForgeSelf.Api.Controllers.McpController`）→ 与插件 McpController 路由**歧义** → `GET /api/mcp/servers` 500 `AmbiguousMatchException`（无日志，action 未进入）。**教训**：发布含宿主 DLL 变更前先停运行中宿主；若界面 500 且 action 无日志，优先怀疑「路由歧义/控制器残留」，核对 `publish/ForgeSelf.dll` 时间戳与工作区源码是否一致。
- **重复插件 id 目录导致宿主启动崩溃**：`publish/plugins` 同时存在两个同 Id 的目录 → `TopologicalSort` ToDictionary 撞 key → 启动即崩。**教训**：宿主插件目录内不得存在同 id 的多个目录；发布后检查重复 id（`Get-ChildItem plugins -Directory | 读 plugin.json Id | Group-Object`）。
- **转发工具不过滤**：MCP 端可调用宿主全部工具（含命令类），安全依赖宿主分发核既有拒绝门；如对外暴露受限环境，可后续在转发器加白名单配置。
- **宿主 IToolRegistry 软依赖**：`ctx.Get<IToolRegistry>()` 在插件 Apply 期（宿主 seed 晚于 RegisterAllServices）通常为 null → `McpService` 降级仅预置数据 + Warn（界面工具表显示预置 12 工具，不含 ToolRegistry 实时同步真实工具）。网关照常启动、转发调用不受影响（转发器经 IContext 运行期取注册表）。

## 已知边界与踩坑记录（v2.1.0 增补）

- **管理面鉴权回归（2026-09-23）**：`api/mcp` / `api/mcp-center/servers` / `api/mcp-center/config` 三个控制器类级 `[Authorize("ApiKeyPolicy")]`；实测无 token 三个端点全 **401**、带宿主令牌全 200；回归测试 `McpAdminAuthTests`（反射断言）。铁律已写入 `plugin-development`（铁律 17）+ 验收标准 `references/plugin-acceptance.md`（§3.2）。
- **list_tools 枚举工具（2026-09-23）**：新增 `ListToolsToolFunction`（`McpCenterPlugin.ToolExtensions` 属性暴露 → 宿主 ExtensionPointManager 自动注册，热重载自动注销），经 `universal_tool` 转发枚举全部已注册工具（85 个，支持 `keyword` 过滤 + `includeSchema`）；`UniversalToolForwarder.ToolDefinitionJson` description 补全（传参示例/常规能力分类/发现入口/外部命名空间）。**注册方式教训**：插件装配期 `ctx.Get<IToolRegistry>()` 为 null（宿主 seed 晚于 Apply），**勿在 Apply 手动 RegisterTool**——与 AIAgent 同模式走「属性暴露 + ExtensionPointManager 自动发现」（见 `AIChatController.cs:31` 注释）。
- **PS 5.1 `Copy-Item 'dir\*' -Recurse` 通配符 bug（本次实证 + 已修根因）**：`build.ps1` 第 3 步对含子目录的 staging 用 `Copy-Item -Path (Join-Path $stagingDir '*') ... -Recurse -ErrorAction SilentlyContinue`——PS 5.1 下通配符+递归会**静默漏拷文件**（报「未能找到文件」或 exit 0 假成功），导致 publish 里 McpCenter.dll 保持旧版。**已修复**：`build.ps1` 改用 `robocopy $stagingDir $publishDir /E`（exit 0-7 成功）+ `$LASTEXITCODE` 复位 + 末尾 `exit 0`；发布动作一律走 `run-plugin-publish-verify.ps1 -Plugin McpCenter`（正规脚本 + 四重验证：api 版本 == 清单版本 / manifest entry / 静态资源 200 / 入口 DLL 哈希 == staged）。**教训**：发布后核验关键 DLL 哈希，禁止手动 Copy-Item/robocopy 进 publish/。

- **stdio 帧必须字节级读取**：`ReadLine` 按 char 读中文/多字节会错位，须底层 Stream 字节读再 UTF-8 解码（`McpFrameTests` 覆盖粘包/UTF-8 长度）。
- **stdio Args 禁手工加引号**：含空格路径手写引号会被当字面量，`ArgumentList` 自动转义（集成测试传裸路径）。
- **mock SSE 标准行为**：POST 202 空体 + 响应经 SSE 流回传；客户端兼容「POST body 直接回 SSE」变体。
- **e2e fullyParallel 端口竞态（历史实证）**：`playwright.config.ts` `fullyParallel:true` 使同文件 3 个用例并行；首用例 PUT 改端口→改回期间，并行用例连**同一 MCP 端口**（e2e worktree 派生物理端口 19000+ 段）得 `ECONNREFUSED`。**修法**：共享 MCP 端口的用例组必须 `test.describe.configure({ mode: 'serial' })`，且改回后轮询 `/health` 就绪再继续。
- **宿主 shim 缓存戳必须 bump（本次实证）**：`ForgeSelf.Web/index.html` import map 的 `element-plus.js?v=2` 是浏览器缓存键；改了 `public/shared/element-plus.js` 但**不 bump v=** → 生产浏览器仍加载旧 shim → 插件报「The requested module 'element-plus' does not provide an export named 'ElSelect'」。**修法**：改 shim 内容同步 bump index.html 对应 `?v=N`（本次 2→4）+ 重建前端 + 覆盖 publish/wwwroot + **删除 publish/wwwroot 下旧 `.br`/`.gz` 预压缩文件**（StaticFiles 优先回旧压缩内容，删后回退未压缩新文件）。
- **错误消息 JSON 转义**：转发器错误文本以 JSON 序列化（中文 `\uXXXX` 转义），e2e 断言错误提示须 `JSON.parse` 后断言，不能直接 `toContain('未连接')`。
- **host http_get 不证明浏览器模块加载**：`http_get` 拿到的磁盘内容 ≠ 浏览器 module cache 命中的内容（带 `?v=` 的 URL 按缓存键整体缓存）。排查此类问题先看 import map 缓存键。

## v2.3.0 新增：写入 dsh（DeepSeek Harness）MCP 配置

**是什么**：MCP 中心可以把自己（或任意本地聚合网关）的 MCP 地址**一键写进 dsh 的 profile 补丁层**，
让 dsh 作为 MCP 客户端直接接入，不必手工编辑 YAML。

**写入位置**：`%USERPROFILE%\.dsh\profiles\<profile>\cordis.patch.yml`（默认 profile = `desktop`）。
该文件是 dsh 的补丁层（顶层 YAML 数组），MCP 客户端条目形态为：

```yaml
- insert:
    - id: mcp-forgeself
      name: '@deepseek-ai/dsh-mcp-client'
      config:
        serverName: ForgeSelf
        transport: streamable-http
        url: http://127.0.0.1:51888/mcp
```

**写入语义（幂等）**：按条目 `id`（默认 `mcp-forgeself`）做 upsert ——
已存在则**就地改写其 `config.url`**，不存在则**追加一段 `insert` 块**。
只做文本级最小改写（不整篇 YAML 往返），保留用户注释与既有格式；
写入前自动备份 `<文件>.bak`，先写 `.tmp` 再原子替换；从不删除任何数据目录（铁律 10）。

**契约（REST，管理面鉴权：类级 `[Authorize("ApiKeyPolicy")]`，铁律 17）**：

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `api/mcp-center/dsh?profile=&serverId=&serverName=` | 查看状态：目标文件路径/是否存在、条目是否存在、当前 url（不读任何密钥） |
| POST | `api/mcp-center/dsh` | 幂等写入。body = `{ profile?, serverId?, serverName?, url? }`，字段全可省：省略时 profile=`desktop`、id=`mcp-forgeself`、name=`ForgeSelf`、url=当前网关地址 + `/mcp`；`url` 仅支持 http/https 绝对地址，非法返回 400 |

**返回视图 `DshMcpConfigDto`**：
`profile / configPath / configExists / entryId / serverName / transport / url / entryExists / lastError`。

**实现落点**：
- `Services/DshMcpConfigWriter.cs` —— profile 白名单校验 + 路径解析 + 幂等 upsert + `.bak` 备份 + 原子写；
- `Models/DshMcpConfigDto.cs` —— 状态视图 / 写入 DTO；
- `Controllers/McpCenterDshController.cs` —— GET / POST 两个端点；
- 前端：`web/src/api/dsh.ts`、`web/src/types/dsh.ts`，以及 `McpCenterView.vue`「网关配置」页签内的
  「写入 dsh 配置」卡片（profile 输入 + MCP 地址输入 + 写入二次确认 + 状态回显）。

**已知边界**：
- 写入只覆盖 `config.url` 一个字段（`serverName` / `transport` 保持原样）；
- dsh profile 名做白名单校验（禁止路径分隔符与 `..`），非法即 400；
- dsh 侧需重启 / 重载 profile 才会读取新的补丁层；
- 本功能为新增端点，尚未补插件层 e2e（`e2e/plugins/mcp-center/`）与前端 `pnpm run check`（见插件 TODO.md）。
