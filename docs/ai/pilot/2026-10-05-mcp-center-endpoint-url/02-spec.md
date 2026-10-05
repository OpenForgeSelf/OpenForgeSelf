# Spec

> 阶段：Stage 2｜Task ID：PILOT-052（2026-10-05-mcp-center-endpoint-url）
> 所有接口/文件/行号均取自真实仓库（2026-10-05 实测），不确定点显式标 `Unknown`。

## Functional Requirements

- **FR1** 顶部地址 chip（`.gateway-address-chip`，`McpCenterView.vue:552-553`）显示的文本与 `title` 均为**端点地址**（含 `/mcp`）。
- **FR2** 「MCP 服务地址」卡（`McpCenterView.vue:651-653`）的值改为端点地址，并在该元素上新增检测挂点 `data-mcp-url`（供 e2e 精确取文本）。
- **FR3** 「复制地址」按钮（`:655-660`，处理函数 `copyListenUrl()` `:181-191`）写入剪贴板的字符串 = 端点地址。
- **FR4** 「MCP 服务地址」卡内新增一行说明文案，明确「客户端须使用 `/mcp` 路径（根路径 404）」，避免用户自行去掉路径。
- **FR5** 「运行状态」卡的 `监听 {{ listenHost }}:{{ port }}`（`:673`）保持不变——真实绑定事实仍可见，本任务不隐藏任何既有信息。
- **FR6** 端点地址由后端真源 `listenUrl` **现算**（不另拼 host/port），算式：`trim()` → 去尾部 `/` → 追加 `/mcp`。

## Input

- 后端 `GET /api/mcp-center/config`（带宿主令牌，`[Authorize("ApiKeyPolicy")]`）返回的脱敏视图字段 `listenUrl`（例：`http://127.0.0.1:18890`）、`listenHost`、`port`、`isRunning`。
- 用户操作：打开 `/mcp-center`（读）、点「复制地址」（写剪贴板）。
- e2e 输入：同一端点 `GET /api/mcp-center/config`（`e2e/plugins/mcp-center/mcp-center.spec.ts` 已持有 `AUTH_HEADERS` 与 `BACKEND_URL`）。

## Output

- 界面：`http://127.0.0.1:18890/mcp` 形态的地址（chip + 卡片 + 剪贴板）；绑定地址仅出现在运行状态卡的「监听 …」行。
- 代码：`McpCenterView.vue` 内一个 `computed`（本计划命名 `mcpEndpointUrl`）作为**唯一**地址派生点，三处展示共用它。

## Business Rules

- **BR1 单一真源**：端点地址必须由 `gatewayConfig.listenUrl` 派生；禁止在前端硬编码 host/port/`/mcp` 之外的任何地址（端口改动、`listenHost` 改 `0.0.0.0` 后界面自动跟随）。
- **BR2 绑定地址不参与展示改造**：`McpGatewayConfig.ListenUrl`（`Services/McpGatewayConfig.cs:27`）语义是 Kestrel 绑定串，**不得**因本需求在其后追加 `/mcp`（会直接让 `UseUrls` 绑到非法 URL）。
- **BR3 与既有先例同构**：算法与设计系统插件的 `mcpEndpoint()`（`Plugins/DesignSystem/web/src/delivery/snippets.ts:36-39`）保持一致（去尾斜杠 + `/mcp`），使全仓「同一个 listenUrl 导出的端点」只有一种写法。
- **BR4 未运行/无地址不编造**：`listenUrl` 为空串时显示空态文案，不得拼出 `null/mcp`、`/mcp` 这类假地址。
- **BR5 令牌不进地址**：本任务不涉及令牌，界面**不得**因本次改动把令牌或其掩码写进地址或说明文案（沿用 034 既有安全口径）。
- **BR6 版本同串**：`plugin.json` 的 `Version` 与 `McpCenter.csproj` 的 `Version/AssemblyVersion/FileVersion` 必须同为 `2.2.1`（034 既有约定「版本统一：plugin.json + csproj」）。

## Boundary Conditions

| 场景 | 期望 |
| --- | --- |
| `listenUrl = "http://127.0.0.1:18890"` | `http://127.0.0.1:18890/mcp` |
| `listenUrl = "http://127.0.0.1:18890/"`（尾斜杠） | `http://127.0.0.1:18890/mcp`（不出现 `//mcp`） |
| `listenUrl` 前后有空白 | 先 `trim()` 再拼接 |
| `listenUrl = ""`（未运行/未配置） | 卡片显示空态文案（沿用设计系统插件口径：`（未运行，暂无地址）`）；复制按钮保持禁用（既有 `:disabled="!gatewayConfig"` 语义不变） |
| 配置尚未返回（`gatewayConfig === null`） | 卡片保留「加载中...」 |
| `listenHost = 0.0.0.0` | 照直展示 `http://0.0.0.0:<port>/mcp`（**不擅自替换为 localhost**——替换会引入"看起来能连其实连不上/反过来"的新误导，且属本任务范围外；记为已知限制） |
| 地址过长（如局域网 IPv6/长主机名） | chip 既有 `max-width:260px` + `text-overflow:ellipsis`（`:1072-1084`）继续生效，`title` 带全串；卡片 `word-break:break-all`（`:1353`）可换行 |

## Error Handling

- `GET /api/mcp-center/config` 失败：沿用现状（`loadGatewayConfig` 的 `ElMessage.error`），界面停留在「加载中...」，**不显示任何猜测地址**。
- 复制失败（非安全上下文/权限拒绝）：沿用现状提示「复制失败，请手动复制地址」，不静默失败。
- 本任务不新增任何网络请求、不新增错误分支（端点地址是纯字符串派生，无失败态）。

## Compatibility

- **后端**：`McpGatewayConfig` / `McpCenterRuntime` / `McpCenterConfigDto` / `Controllers/*` **零改动**；`GET /api/mcp-center/config` 响应契约不变。
- **其它消费者**：设计系统插件交付页、`sems` e2e 等各自用 `listenUrl` 自行拼 `/mcp`（`design-system-showroom.spec.ts:655-658`、`sems.spec.ts:387-403`），本任务不影响它们。
- **版本兼容**：插件版本 `2.2.0 → 2.2.1`（补丁级，无契约变化）。并行 worktree `wt-mcp-playground`（未提交工作，`plugin.json` 已写 `2.3.0`）与本批合并时版本取高者，需在合并时复跑插件门禁——已登记为风险。
- **既有 e2e 断言**：`mcp-center.spec.ts:228` 现有断言是对 chip 文本 `toContainText('127.0.0.1')`，本任务将其**升级**为端点等值断言（不是放宽）。

## Non-functional Requirements

- 无新增依赖、无新增构建步骤、无新增接口调用。
- 文案用中文、与页面既有措辞一致；样式只用既有 `--el-*` 变量与既有 class，不引入新色值。
- 改动局部化：仅 `McpCenterView.vue` 的 script（一个 computed + 复制函数取数）、template（3 处 + 1 行说明）、必要时 `<style>` 微调（预计不需要，见 03-plan 偏差记录位）。

## Acceptance Criteria

- **AC1** `Plugins/McpCenter/web && pnpm run build` 成功（退出码 0），`dist/index.js` 重新生成。
- **AC2** 插件层 e2e `/mcp-center` UI 用例断言：chip 文本 == `GET /api/mcp-center/config`.listenUrl + `/mcp`；`[data-mcp-url]` 文本 == 同值；并在证据行打印 `listenUrl=<…> 展示=<…>`（同瞬间旁证）。
- **AC3** e2e 内保留「运行状态卡仍显示 `监听 <host>:<port>`」与「状态卡数量 = 3」的既有断言（证明绑定事实未被隐藏、卡片未被增删）。
- **AC4** 端到端真连一次：用界面同源算法得到的端点发 `POST /mcp`（`tools/list`）→ 200 且返回 `universal_tool`；用**根地址**（`http://host:port/`）同法发一次 → 非 200（证明"少了路径连不上"这一前提为真）。
- **AC5** `ForgeSelf.Web && pnpm run check` 0 error；`dotnet test --filter "FullyQualifiedName~McpCenter"` 全绿（回归）。
- **AC6** `plugin.json` 与 `McpCenter.csproj` 版本**同为** `2.2.1`（脚本/命令实读核对，不靠肉眼）；`docs/02-features/034-mcp-center.md` 已补「前端界面」节措辞与 v2.2.1 验证记录。

## Unknowns

- `U1` 剪贴板内容的自动化断言：headless 下读回剪贴板需 `clipboard-read` 权限（`context.grantPermissions`）。**若实测不稳**，则降级为「chip 与 `[data-mcp-url]` 与复制函数共用同一 computed」的静态事实 + 文本等值断言，并把该降级写进 `05-evidence.md` 的 Known Limitations（**不伪造已读回剪贴板的结论**）。
- `U2` 该插件 `web/node_modules` 是否完整（技能 §3.2 记有沙箱内 `pnpm run build` 失败的历史）；实现期若命中，按技能给定的**出树构建兜底**执行，并如实记录。
