# 04 Task — 原子工作单元

> 闸门1：用户 2026-10-08 原话「A 显式打开会话id」⇒ 已批准方向（全量替换 + 有状态会话）。

## T001 引入官方 SDK 依赖

- **Objective**：把协议/传输能力交给官方包
- **Allowed**：`Plugins/McpCenter/McpCenter.csproj`（+1 行 PackageReference）
- **Forbidden**：改其他 csproj；改 `Directory.Packages.props`（若存在）；升级其他包
- **AC**：`dotnet restore` 成功，net10.0 无兼容警告
- **Files**：`Plugins/McpCenter/McpCenter.csproj`
- **Verify**：`dotnet build Plugins/McpCenter/McpCenter.csproj`

## T002 新建 SDK 工具外壳 `McpUniversalTool`

- **Objective**：把 `universal_tool` 以官方 SDK 方式暴露，内部转调既有转发器
- **Allowed**：新建 `Plugins/McpCenter/Services/McpUniversalTool.cs`
- **Forbidden**：修改 `UniversalToolForwarder.cs` 逻辑；注册进宿主 `IToolRegistry`；改 `ToolDefinitionJson`
- **AC**：类非静态；工具名 `universal_tool`；入参 `tool` 必填、`parameters` 可选；转调 `ForwardAsync` 并原样返回文本
- **Files**：`Plugins/McpCenter/Services/McpUniversalTool.cs`（新建）

## T003 网关改用 `MapMcp` + 有状态 + 鉴权中间件

- **Objective**：替换自研传输层，开启会话 id，保留鉴权与 health
- **Allowed**：`Plugins/McpCenter/Services/McpGatewayServer.cs`
- **Forbidden**：改 `McpGatewayConfig` 端口逻辑；删除 `/health`；引入业务侧会话状态
- **AC**：`initialize` 下发 `Mcp-Session-Id`；`/mcp` 需 Bearer（配 token 时）；`/health` 免鉴权 200；启动失败仍降级不抛
- **Files**：`Plugins/McpCenter/Services/McpGatewayServer.cs`

## T004 同步插件装配与受影响测试构造

- **Objective**：消除因构造签名变化导致的编译断裂
- **Allowed**：`Plugins/McpCenter/McpCenterPlugin.cs`、`ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpCenterRuntimeTests.cs`
- **Forbidden**：改 `Controllers/`；改插件前端；改其他插件
- **AC**：宿主与测试工程均编译通过；`McpCenterRuntimeTests` 仍绿
- **Files**：上述 2 个

## T005 标注自研处理器为「不再使用」（保留不删）

- **Objective**：落实 D1，避免误以为仍是链路一环
- **Allowed**：`Plugins/McpCenter/Services/McpJsonRpcHandler.cs`（**仅头注释**）
- **Forbidden**：改其任何逻辑；删 `McpJsonRpcHandlerTests.cs`
- **AC**：仅注释变更，无行为变更
- **Files**：`Plugins/McpCenter/Services/McpJsonRpcHandler.cs`

## T006 新增真实行为用例

- **Objective**：把「会话 id / 单工具 / 版本显式报错 / 鉴权 / health」钉成可回归测试
- **Allowed**：新建 `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpGatewayServerSdkTests.cs`
- **Forbidden**：改造既有 `McpJsonRpcHandlerTests`
- **AC**：覆盖 AC2/AC3/AC5/AC6；失败时先红后绿（反向探针至少 1 条）
- **Files**：上述新建文件

## T007 集成验证（隔离实例，禁碰用户实例）

- **Objective**：用官方 MCP 客户端端到端证明可用
- **Allowed**：隔离端口起独立网关进程；官方 Python SDK 客户端只读请求
- **Forbidden**：停/启/杀 `:18890` 或 `:51888` 用户实例；调改状态端点
- **AC**：握手拿 `Mcp-Session-Id`；`tools/list`=1；`call_tool` 返回真实结果；`2026-07-28` 显式报错
- **Verify**：官方 SDK 客户端脚本输出

## 范围边界（全局）

- **Allowed**：`Plugins/McpCenter/**`（含 csproj）、`ForgeSelf.Api.Tests/Plugins/McpCenterTests/**`
- **Forbidden**：宿主 `ForgeSelf.Api/**`（除必要的插件登记，本次无需改）、其他插件、插件前端 `web/`、e2e、数据库结构、鉴权核心逻辑以外的安全逻辑
