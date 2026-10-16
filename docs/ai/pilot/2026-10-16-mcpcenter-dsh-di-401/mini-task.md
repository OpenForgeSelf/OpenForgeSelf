# mini-task：McpCenter 网关配置 500（DI 漏注册）+ 401（无 Authorization 头）诊断与修复

> 轻量通道（≤3 文件缺陷修复）。日期 2026-10-16 · 来源：用户输入（18:2x）· P1
> 现场证据：截图 `screenshots/mcp-gateway-error-with-token.png` 等 4 张；复现记录见本文件 §验证。

## Intent

用户在 MCP 中心插件点「网关配置」页签报错：
1. `GET /api/mcp-center/dsh` → **500**：`Unable to resolve service for type 'DshMcpConfigService' while attempting to activate 'McpCenterDshController'`（Kestrel 未处理异常，空响应体）。
2. `BearerApiKey was not authenticated. Failure message: Missing or invalid Authorization header`（401）。
3. `[McpService] 宿主 IToolRegistry 不可用（软依赖缺失），仅提供预置服务器/工具数据`（每次查询刷 Warn）。

## Spec（根因，均有文件:行证据）

| # | 现象 | 根因 | 证据 |
|---|------|------|------|
| R1 | dsh 500 | v2.3.0 控制器改依赖新类 `DshMcpConfigService`，但插件入口 DI 注册仍只注册旧类 `DshMcpConfigWriter` ⇒ 插件子 provider 解析失败 | `Plugins/McpCenter/Controllers/McpCenterDshController.cs:22`（构造要 Service）vs `Plugins/McpCenter/McpCenterPlugin.cs:71`（只注册 Writer）；旧类已无任何控制器/服务消费方（全库 grep 仅注册行与类自身） |
| R2 | 401 Missing header | 行为正确非缺陷：`ApiKeyAuthenticationHandler` 仅在**请求完全没带 Authorization 头**时报此文案（token 无效/为空是另外文案，`ForgeSelf.Api/Security/ApiKeyAuthenticationHandler.cs:39-48`）；插件前端 `http.ts:26-31` 只有 localStorage `forge_api_token` 非空才注头 ⇒ 用户当时浏览器无 token（未走 `#token=` 登录入口 / 清了存储 / 换了实例端口 origin）。实测佐证：无 token 时连 `/api/plugin/frontend-manifest` 都 401、插件页空白（Browser 走查第二组实验） |
| R3 | IToolRegistry Warn | `McpCenterPlugin.Apply` 在**装配期**调 `ctx.Get<IToolRegistry>()`（L50），装配期 ctx 解析不到（插件自身注释已言明：`ListToolsToolFunction.cs:14-15`「装配期 Get 不到，运行期才可解析」），拿到 null 后**永久缓存**进 McpService ⇒ 运行期每次同步工具都走降级分支刷 Warn | `Plugins/McpCenter/McpCenterPlugin.cs:50-51` + `Plugins/McpCenter/Services/McpService.cs:103-109` |

## Plan / Task（改动 ≤3 文件）

1. `Plugins/McpCenter/McpCenterPlugin.cs:71`
   `services?.AddSingleton(new DshMcpConfigWriter(config));` → `services?.AddSingleton(new DshMcpConfigService(config));`
2. `Plugins/McpCenter/Services/McpService.cs`
   新增 `Func<IToolRegistry?>` 构造重载，内部统一持 provider；原 `IToolRegistry?` 重载保留（既有 12 处测试构造零改动）。`SyncToolsFromToolRegistry` 改为每次经 provider 惰性解析，解析到 null 才降级（Warn 照旧）。
3. `Plugins/McpCenter/McpCenterPlugin.cs:50-51`
   `new McpService(registry)` → `new McpService(() => ctx.Get<IToolRegistry>())`（删除装配期一次性 Get）。

不改：控制器、前端、`DshMcpConfigWriter.cs` 文件本体（留待后续单独清理，登记 TODO 不顺手删）；鉴权行为（R2 无需代码修复，属使用/入口问题）。

## 验证（DoD）

- `dotnet build ForgeSelf.Api`（含插件引用）0 错误；`dotnet test ForgeSelf.Api.Tests --filter McpCenter`（含既有 McpServiceTests/McpServiceToolRegistryTests/McpControllerIntegrationTests）全绿。
- 现场复验（dev-stack :7301）：带 token `GET /api/mcp-center/dsh?profile=desktop` 由 500 → **200**；宿主日志出现真实工具同步（不再每查刷「IToolRegistry 不可用」Warn）。
- 浏览器点击「网关配置」页签：dsh 区正常显示配置状态（无红条）。
- 401 口径回归：不带 token 直请求仍应 401（鉴权未被削弱）。

## 风险与回滚

单插件两文件小改，回滚 = git revert 提交。风险：McpService 由构造期快照改运行期惰性解析——既有测试均直接注入 mock registry，语义兼容；新路径有 McpServiceToolRegistryTests 覆盖。
