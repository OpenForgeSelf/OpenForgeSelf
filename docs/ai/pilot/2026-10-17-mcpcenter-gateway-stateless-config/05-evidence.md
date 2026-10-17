# 05-evidence：McpCenter 网关 Stateless 会话模式做成配置项（2026-10-17，v2.4.0）

## Changed Files（本批净改动）
| 文件 | 动作 |
| --- | --- |
| `Plugins/McpCenter/Services/McpGatewayConfig.cs` | 加 `Stateless`（默认 true）+ env `FORGESELF_MCP_GATEWAY_STATELESS` 覆盖（"1/true/0/false" 大小写不敏感）+ config.json `stateless` 键（缺键保持默认） |
| `Plugins/McpCenter/Models/McpCenterConfigDto.cs` | 视图 DTO 加 `Stateless`；更新 DTO 加 `bool? Stateless`（不传=不变） |
| `Plugins/McpCenter/Services/McpCenterRuntime.cs` | `GetInfo` 带出；`ApplyUpdateAsync` 快照→应用→落盘→Stop/Start 热重启→失败回滚；`PersistAsync` 写 `stateless` 键 |
| `Plugins/McpCenter/Services/McpGatewayServer.cs` | `WithHttpTransport(o => o.Stateless = _config.Stateless)`；**顺带修 serverInfo 契约回归**：`AddMcpServer(o => o.ServerInfo = {Name=ForgeSelf McpCenter, Version=插件版本, Description=…})`（SDK 化后默认回宿主程序集名+构建号，违 034 契约）；listChanged 覆写无效→撤除留注释 |
| `Plugins/McpCenter/web/src/types/gateway.ts` | 两接口加 `stateless` |
| `Plugins/McpCenter/web/src/McpCenterView.vue` | 网关配置表单加「会话模式」开关（`data-test="stateless-switch"`）+ 常驻说明文案 + 保存提示分态；**走查后补修**：无状态 toast 文案主语笔误「客户端重启宿主后无需重连」→「宿主/网关重启后客户端无需重连」（本批引入文案，dist 重建后实证旧串 0 命中/新串 2 命中） |
| `Plugins/McpCenter/plugin.json` + `McpCenter.csproj` | 2.3.2 → **2.4.0** |
| `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpGatewayConfigTests.cs` | +2 用例（缺键默认 true / 旧文件显式 false 被识别；env 覆盖） |
| `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpCenterRuntimeTests.cs` | +1 用例（PUT stateless=false → 视图/落盘/重载均反映且热重启成功） |
| `ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts` | 修 3 处既有/连带缺陷：①两处 initialize 的 clientInfo 补必填 `version`（否则 SDK 严格反序列化抛 JsonException→HTTP 500，A1 长期红）；②新增 `parseRpcJson`（SDK 应答统一 SSE `event: message`，原 `res.json()` 崩）；③工具测试台用例补「先切网关配置页签」步骤（被 A1 失败长期掩盖）；listChanged 断言放宽为 tools 存在（见 TODO P3） |
| `docs/02-features/034-mcp-center.md` | 契约回写×5 处（配置表会话模式行、PUT 契约 stateless 语义、有状态会话语义 v2.4.0 根治句、API 表 PUT 行、initialize serverInfo/listChanged 实况）+ 本文验证记录节 |
| `TODO.md` / `.forgeself/memory/2026-10-17.md` | 流程工件（待办 P1 + 既有缺陷 P2/P3 登记 + 日记） |

未动：宿主（`ForgeSelf.Api/**` 无改动）、鉴权中间件、universal_tool 面、dsh 面板。

## 验证记录（全部 Verified · 真跑）

### V1/V2 后端门禁
- `dotnet build`（McpCenter + Api）→ **0 错误**。
- `dotnet test --filter "FullyQualifiedName~McpCenter"` → **111 通过 / 0 失败**（含本批新增 3 条）。

### V3 插件前端门禁
- `pnpm run build` → 绿（51 modules，dist/index.js 71.63 kB）；`pnpm run test` → **23/23 绿**。
- `pnpm run check`（vue-tsc）→ 3 错（`McpCenterView.vue:269/285/297` `cfg.url`/`entryId` 不存在于 `DshMcpConfigDto`）——git diff 对照坐实为 **30b076f 重构 types/dsh.ts 遗留的既有缺陷，非本批引入**；已记 TODO P2（line 24），本批不顺手修（§1.3）。

### V4 冒烟 e2e（插件层 + 应用层）
- `e2e/plugins/mcp-center/mcp-center.spec.ts` → **4/4 绿**（默认已切无状态：mcpCall 裸 POST 从不带 session，照样全绿 = S1 默认值达成）。
- `e2e/mcp-tools.spec.ts` → **4/4 绿**。
- 排障记录（均为既有缺陷，修复是本批门禁过线的必要条件）：
  1. initialize HTTP 500——TEMP PROBE 中间件抓栈 `JsonException: JSON deserialization for type 'Implementation' was missing required properties including: 'version'`（探针用完即删）；curl 坐实**有状态同样缺 version 即 500** ⇒ e2e spec 客户端合规缺陷，与会话模式无关。
  2. `res.json()` 崩（`Unexpected token 'e', "event: mes"`）——SDK 应答统一 SSE，spec 加 `parseRpcJson` 按 content-type 解析。
  3. 工具测试台 `.external-item` 超时——外部服务器区块在「网关配置」页签而用例未切 tab。

### V5a 协议面运行期 A/B（dev-stack 源码实例：后端 :7301 / 前端 :7399 / 网关 :18895，`FORGESELF_MCP_GATEWAY_PORT=18895` 避开用户 :51888 实例的 18890）
- GET `/api/mcp-center/config` → `stateless: true`（升级后老 config.json 缺键 ⇒ 自动转无状态 = 根治生效）。
- 无状态裸 POST `tools/list`（无 `Mcp-Session-Id` header）→ **200 直接可用**，响应头**不含** `Mcp-Session-Id`。
- PUT `{"stateless": false}` → 视图 `stateless=false`，落盘 `stateless:false`，initialize **签发** session（如 `dDkBlxJYdEGXSikI_XbIXQ`）= 热重启生效。
- PUT `{"stateless": true}` → initialize 响应 `session=[]` 不再签发 = **运行期双向可切换，无需改代码发版（用户拍板目标达成）**。
- env 覆盖：`FORGESELF_MCP_GATEWAY_STATELESS=0` 起网关 → 有状态（三级优先序 S2 成立）。
- `/health` → `{"status":"ok","version":"2.4.0","tools":1,"listen":"http://127.0.0.1:18895"}`；带 version 的 initialize → `serverInfo {"name":"ForgeSelf McpCenter","version":"2.4.0","description":"OpenForgeSelf MCP Center（万能工具网关，支持 MCP 2.0）"}`（契约回归修复有证据）。

### V5b 页面走查（Browser 走查，DOM 核对 7/7 过）
- 标题版本徽标 v2.4.0（与 plugin.json 两头对上）✅；`stateless-switch` 默认「无状态」+ 推荐文案 ✅；切「有状态」文案联动 ✅；保存提示「网关配置已保存并热重启生效（当前：有状态，宿主/网关重启后客户端需重新连接）」✅；刷新后回显有状态（持久化）✅；切回无状态保存 → GET config `stateless:true`（**环境已还原**；当时提示为旧文案「客户端重启宿主后无需重连」，主语笔误即本批后补修的 toast 文案，见 Changed Files 末行）✅；控制台 0 error / 16 warn（v-loading ×6 = 已登记 TODO P2 既有缺陷）。
- **截图缺失（如实标注，不冒充）**：两轮走查截图均失败 `NATIVE_BROWSER_VIEWPORT_UNAVAILABLE`（宿主浏览器窗口最小化，viewport=0×0、`document.hidden=true`；`window.focus/resizeTo`、重新导航均无效，子代理无 OS 级窗口还原能力）。视觉存证以 **e2e 截图 `screenshots/e2e/mcp-center/*.png`（真实宿主渲染）+ 上述 DOM 状态核对 + dist 文案实证** 替代；用户把 Qoder 浏览器窗口还原到前台后可随时补 `screenshots/live-7399/` 走查图。

## Known Limitations / Unresolved
- serverInfo 修复、clientInfo.version、SSE 解析、工具测试台 tab 步骤均属 **SDK 化（2.2.0）以来的既有漂移**，本批因门禁必须过而一并修复，改动全部限定在 McpCenter 插件 + 其 e2e spec。
- listChanged 旗标 SDK 自动推导不可覆写（A/B 坐实）→ 034 按实况改写、断言放宽，登记 TODO P3。
- ~~V6 发布与 git 提交待用户拍板~~ → **V6 已执行（2026-10-17 用户拍板「发布到本地，我自己更新验证」）**：`release-local.ps1 -Version 2.4.7 -Sign`（更新源缺省 ../updates）→ `OpenForgeSelf-2.4.7.2610171917-win-x64.zip` 落盘；产物验真 Verified：签名校验 3/3、`signtool verify /pa /tw` Successfully verified（时间戳）、SHA256 与 SHA256SUMS 逐字一致、L1 成立（versions/<ver>/plugins 可读清单）；包内 McpCenter=2.4.0、TodoTracker=1.1.3（probe-dll-string 坐实旧类 TodoToolProvider ABSENT = 同日 DI 修复批同进包）。git 提交未被要求，不动；plugin-development ⑤运行实例只读复验待用户在 :51888 自行更新后发起。
