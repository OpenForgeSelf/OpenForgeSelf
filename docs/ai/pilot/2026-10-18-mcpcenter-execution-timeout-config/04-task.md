---
feature: “McpCenter 外部 MCP 执行超时可配置”
risk: { level: L2, rationale: “传输构造签名变更，影响插件内部” }
expected_files:
  - Plugins/McpCenter/Services/
  - Plugins/McpCenter/web/src/
rollback: “git revert 本批 commit”
---

# 04 Task — 外部 MCP 执行超时可配置

## Allowed Files
McpGatewayConfig.cs, StdioMcpTransport.cs, StreamableHttpMcpTransport.cs, LegacySseMcpTransport.cs, McpClientManager.cs, McpCenterPlugin.cs, McpCenterConfigDto.cs, McpCenterRuntime.cs, gateway.ts, McpCenterView.vue, plugin.json, csproj, test files, e2e spec

## Forbidden
宿主核心服务 / 其他插件 / 数据库 Schema / IToolRegistry 转发路径

> 工作单元清单。闸门1 批准后方可进入 Implement。每个任务标注：目标 / 范围（文件）/ 验收标准 / 验证命令。

## T01 — 配置模型加 ExecutionTimeoutMs
- **Objective**：`McpGatewayConfig` 新增 `ExecutionTimeoutMs`（默认 300000）+ env/config.json 三级加载 + 合法域校验助手。
- **Scope**：`Plugins/McpCenter/Services/McpGatewayConfig.cs`
- **Acceptance**：
  - [ ] `DefaultExecutionTimeoutMs = 300000`、`MinExecutionTimeoutMs = 1000`、`MaxExecutionTimeoutMs = 3600000` 常量存在
  - [ ] `ExecutionTimeoutMs` 属性默认 300000
  - [ ] `Load()` 读 env `FORGESELF_MCP_GATEWAY_TIMEOUT_MS` → config.json `executionTimeoutMs` → 默认；非法回落默认并记警
  - [ ] `static bool IsValid(int ms)` 边界正确
- **Verify**：`dotnet build Plugins/McpCenter`；T06 单测绿

## T02 — 三传输执行超时参数化 + 修单位 bug
- **Objective**：三传输构造接收 `requestTimeoutMs`，`linked.CancelAfter` 与 `_http.Timeout` 用注入值；`FramingProbeTimeoutMs`/`EndpointDiscoverTimeoutMs` 保持常量。
- **Scope**：`StdioMcpTransport.cs` / `StreamableHttpMcpTransport.cs` / `LegacySseMcpTransport.cs`
- **Acceptance**：
  - [ ] 各构造增 `int requestTimeoutMs`（或等价注入），存 `_requestTimeoutMs`
  - [ ] `linked.CancelAfter(_requestTimeoutMs)`；报错文案用 `_requestTimeoutMs/1000`
  - [ ] HTTP/SSE `_http.Timeout = TimeSpan.FromMilliseconds(_requestTimeoutMs)`（修 FromSeconds bug，AC-5）
  - [ ] 子超时常量原样保留（AC-6）
- **Verify**：`dotnet build`；T06 传输单测 + PC-010 守卫复跑绿

## T03 — McpClientManager 注入 config 并贯穿
- **Objective**：`McpClientManager` 持有 `McpGatewayConfig`，`CreateTransport` 建连时注入 `ExecutionTimeoutMs`。
- **Scope**：`Plugins/McpCenter/Services/McpClientManager.cs`、`Plugins/McpCenter/McpCenterPlugin.cs`
- **Acceptance**：
  - [ ] 构造签名 `McpClientManager(McpGatewayConfig config, ExternalServersStore store)`
  - [ ] `CreateTransport` 按 transport 分派注入 `config.ExecutionTimeoutMs`（三路径都吃到）
  - [ ] 插件入口用同一 `config` 实例构造 manager（runtime 改后下次建连读新值）
  - [ ] 全仓 `new XxxTransport(` / `new McpClientManager(` 站点同步（含测试）
- **Verify**：`dotnet build`；`dotnet test --filter McpCenter`

## T04 — 配置对外读写（DTO + Runtime + Controller）
- **Objective**：GET 回显 + PUT 修改 + 持久化 + 非法拒绝，贯穿 Stateless 同款链。
- **Scope**：`Models/McpCenterConfigDto.cs`、`Services/McpCenterRuntime.cs`、`Controllers/McpCenterConfigController.cs`
- **Acceptance**：
  - [ ] `McpCenterConfigDto.ExecutionTimeoutMs`、`McpCenterConfigUpdateDto.ExecutionTimeoutMs?`
  - [ ] `GetInfo()` 组视图含 `executionTimeoutMs`
  - [ ] `UpdateAsync` 校验（非法→400 中文），合法赋值，回滚分支恢复 `oldExecutionTimeoutMs`
  - [ ] `PersistAsync` 匿名对象加 `executionTimeoutMs`（保存即落盘，AC-2 关键）
  - [ ] 越界 PUT → `ApiResponse{success:false,code:400}`
- **Verify**：`dotnet build`；T06 单测；T08 e2e 往返

## T05 — 前端类型 + 网关配置 UI
- **Objective**：插件前端 types + McpCenterView 网关配置 tab 加「外部 MCP 执行超时」输入（单位秒）。
- **Scope**：`web/src/types/gateway.ts`、`web/src/McpCenterView.vue`
- **Acceptance**：
  - [ ] types 加 `executionTimeoutMs`（读）/ `executionTimeoutMs?`（改）
  - [ ] 会话模式行后加 `ElInputNumber`（min=1 max=3600 step=10）+ `form-hint`
  - [ ] `timeoutInputSec` ref 载入回填 `cfg.executionTimeoutMs/1000`
  - [ ] save 仅变化时组装 `update.executionTimeoutMs = sec*1000`；成功 toast 含「新建/重连连接生效」
  - [ ] 空态/短路沿用既有 `Object.keys(update).length===0`
- **Verify**：`cd Plugins/McpCenter/web && pnpm run build` + `pnpm run test`

## T06 — 后端单测
- **Objective**：覆盖默认/env 覆盖/非法回落/IsValid 边界/传输注入/manager 贯穿。
- **Scope**：`ForgeSelf.Api.Tests/Plugins/McpCenterTests/`（新 `McpGatewayConfigTimeoutTests.cs` + 传输/manager 断言）
- **Acceptance**：
  - [ ] 默认 300000；env 覆盖；env/config 非法回落
  - [ ] IsValid 边界 999/1000/3600000/3600001
  - [ ] 传输 `_http.Timeout == FromMilliseconds(timeoutMs)`；CreateTransport 三路径注入
  - [ ] PC-010 既有守卫复跑绿（帧协商/子超时不受影响）
- **Verify**：`dotnet test --filter McpCenter`

## T07 — 版本三连
- **Objective**：插件版本 2.5.1 → 2.5.2，Description 追加执行超时可配置。
- **Scope**：`Plugins/McpCenter/plugin.json`、`McpCenter.csproj`
- **Verify**：`dotnet build`；发布解包核验 FileVersion

## T08 — e2e（配置往返 + 慢工具双档）
- **Objective**：证明配置真实驱动执行超时（零 mock 真前后端）。
- **Scope**：`ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts`、`Fixtures/mock-mcp-server.js`（加 `slow` 工具延时）
- **Acceptance**：
  - [ ] PUT `executionTimeoutMs` 往返（改→GET 回显→改回）
  - [ ] executionTimeout=10s 调慢工具超时报错；=60s 通过
  - [ ] 截图读图新超时输入框
- **Verify**：`pwsh` 跑 mcp-center e2e 套绿

## T09 — 门禁 + 发布 + 走查（六步闭环）
- **Objective**：按 plugin-development §四 六步全跑。
- **Acceptance**：
  - [ ] ① 门禁（插件前端 build / 后端 build+test）
  - [ ] ② 本插件 1 条冒烟 e2e（慢工具那条）
  - [ ] ③ dev-stack 起源码本地前后端按 AC 走查 + 截图读图 + 清测试数据
  - [ ] ④ 发布本地更新源 `release-local -UpdateDir -Sign`
  - [ ] ⑤ 运行实例只读复验（用户更新后）
  - [ ] ⑥ 更新插件文档（034 真源）+ 记日志
- **Verify**：`check-problem-classes.ps1` 绿

## 依赖顺序
T01 → T02 → T03 → T04 → T05 →（T06 单测）→ T07 版本 → T08 e2e → T09 门禁发布走查。
