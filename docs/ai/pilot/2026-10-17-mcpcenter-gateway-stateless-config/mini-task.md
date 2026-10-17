# mini-task：McpCenter 网关 Stateless 会话模式做成配置项（2026-10-17）

> 轻量通道（合并 Intent/Spec/Plan/Task）；用户已拍板：「把 Stateless 做到 mcp 网关配置里面，改完可重启网关生效，以后不用再改代码发版」。

## Intent

- 网关会话模式当前硬编码 `WithHttpTransport(o => o.Stateless = false)`（`McpGatewayServer.cs:65`），切换要改代码发版。
- 后果实证（2026-10-16/17 两轮）：宿主每次更新/重启清空内存会话表，dsh 等不自动重握手的客户端携旧 `Mcp-Session-Id` 即吃 404/-32001。
- 目标：`Stateless` 进入网关配置链（config.json + GET/PUT API + 前端开关），**PUT 后热重启即生效**（复用现有 Stop→Start 通道），运行期可切换、不再发版。

## Spec（设计拍板）

| # | 决策 | 依据 |
|---|------|------|
| S1 | 配置字段 `Stateless: bool`，**默认 `true`（无状态）** | 用户已被有状态坑两次且同意根治；本网关能力面无任何反向状态需求（034 已证：无 sampling/elicitation/prompts/listChanged）；老 config.json 缺键 ⇒ 升级即自动转无状态=根治生效；想回有状态在页面一键切回 |
| S2 | 三级优先序与现有字段一致：环境变量 `FORGESELF_MCP_GATEWAY_STATELESS`（"true/1/false/0"）→ config.json → 默认 | `McpGatewayConfig.Load` 既有模式；e2e/多实例可强制锁定模式做 A/B |
| S3 | API：GET 视图加 `stateless` 字段；PUT `stateless?: boolean`（不传=不变） | 与 port/token 的部分更新语义一致（`McpCenterConfigUpdateDto` 可空风格） |
| S4 | 生效通道 = 现有 `ApplyUpdateAsync` 热重启（Stop→Start 重建 WebApplication，`WithHttpTransport` 读最新 `_config.Stateless`） | 用户说的「重启网关生效」= 现成的「保存并重启生效」按钮，零新机制 |
| S5 | 前端：网关配置表单加「会话模式」`ElSwitch`（开=无状态推荐；关=有状态握手），常驻说明文案讲清两态差别与代价 | §3.4「禁用/原因可见」；文案锚定实测行为而非泛泛 |
| S6 | 版本 2.3.2 → **2.4.0**（新增配置能力，plugin.json + csproj 同步） | 语义化：新能力非补丁 |
| 边界 | 不改鉴权中间件、不改 universal_tool 面、不动 dsh 面板；`notifications/initialized` 在无状态下的协议兼容交给 SDK | 范围控制 |

## Plan（改动清单）

1. `Plugins/McpCenter/Services/McpGatewayConfig.cs` — 加 `Stateless` 属性（默认 true）+ `EnvStateless` 常量 + `Load` 透传（含 env 覆盖解析 "1/true/0/false"）
2. `Plugins/McpCenter/Models/McpCenterConfigDto.cs` — 视图 DTO 加 `Stateless`；更新 DTO 加 `bool? Stateless`
3. `Plugins/McpCenter/Services/McpCenterRuntime.cs` — `GetInfo` 带出；`ApplyUpdateAsync` 快照/应用/回滚；`PersistAsync` 落盘 `stateless` 键
4. `Plugins/McpCenter/Services/McpGatewayServer.cs` — `o.Stateless = _config.Stateless`；类头注释改「会话模式可配置（默认无状态）」
5. `Plugins/McpCenter/web/src/types/gateway.ts` — 两接口加 `stateless` 字段
6. `Plugins/McpCenter/web/src/McpCenterView.vue` — 表单行「会话模式」开关（`statelessInput`），`loadGatewayConfig` 同步、`saveGatewayConfig` diff 提交；说明文案
7. `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpCenterRuntimeTests.cs` — 新增 1 条：PUT stateless=false → 视图/落盘反映且热重启不炸
8. `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpGatewayConfigTests.cs` — 新增 2 条：默认 true + config.json `"stateless":false` 被识别（大小写不敏感同例）+ env 覆盖
9. `Plugins/McpCenter/plugin.json` + `McpCenter.csproj` — 2.4.0
10. `docs/02-features/034-mcp-center.md` — v2.4.0 节 + config 契约表加 stateless

## 验证（分档）

- V1 `dotnet build Plugins/McpCenter` 0 错；V2 `dotnet test --filter McpCenter` 全绿（含新增 3 条）
- V3 插件前端 `pnpm run build` 绿
- V4 冒烟 e2e（`e2e/plugins/mcp-center/mcp-center.spec.ts` 主链路 + `e2e/mcp-tools.spec.ts`）——**默认切无状态后 e2e 裸 POST 用例应照样绿**（mcpCall 从不带 session）
- V5 dev-stack 现场 A/B：`initialize` 响应头**不再含** `Mcp-Session-Id` ⇒ POST tools/call 不带 header 直接 200；页面开关切回「有状态」保存重启 ⇒ 重新签发 session ⇒ 再切回无状态（证明运行期可切换 = 拍板目标达成）；截图读图
- V6 发布：release-local -Sign -UpdateDir（用户既往要求；如用户说先不发则跳过）

## 回滚

git revert 本批提交即可（无数据库迁移、无宿主改动）；运行态回退不需 revert：页面开关切回「有状态」= 行为回到 2.3.2。
