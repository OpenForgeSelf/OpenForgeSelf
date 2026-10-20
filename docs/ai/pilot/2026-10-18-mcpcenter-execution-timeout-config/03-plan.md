# 03 Plan — 外部 MCP 执行超时可配置

## Implementation Steps（按真实文件）

### A. 后端配置模型
- `file: Plugins/McpCenter/Services/McpGatewayConfig.cs`
  `reason:` 加 `public const int DefaultExecutionTimeoutMs = 300000;` + `public int ExecutionTimeoutMs { get; set; } = DefaultExecutionTimeoutMs;`；`Load()` 读 env `FORGESELF_MCP_GATEWAY_TIMEOUT_MS`（int.TryParse + 合法域校验，非法回落默认+记警）→ config.json 键 `executionTimeoutMs`（缺键回落默认）；定义合法域常量 `Min=1000`/`Max=3600000` 供校验复用。加一个 `static bool IsValid(int ms)` 助手。

### B. 传输贯穿（执行超时参数化）
- `file: Plugins/McpCenter/Services/McpClient/StdioMcpTransport.cs`
  `reason:` 构造函数增 `int requestTimeoutMs` 参数（或整 `McpGatewayConfig`，倾向仅传超时值、低耦合），存实例字段 `_requestTimeoutMs`；`linked.CancelAfter(_requestTimeoutMs)`；报错文案用 `_requestTimeoutMs/1000`。**`FramingProbeTimeoutMs` 保持常量不动。**
- `file: Plugins/McpCenter/Services/McpClient/StreamableHttpMcpTransport.cs`
  `reason:` 构造增 `requestTimeoutMs`；`_http.Timeout = TimeSpan.FromMilliseconds(_requestTimeoutMs)`（**修 AC-5 单位 bug**）；`linked.CancelAfter(_requestTimeoutMs)`；报错文案用实例值。
- `file: Plugins/McpCenter/Services/McpClient/LegacySseMcpTransport.cs`
  `reason:` 同上 + `_http.Timeout` 单位修；**`EndpointDiscoverTimeoutMs` 保持常量不动**。
- `file: Plugins/McpCenter/Services/McpClientManager.cs`
  `reason:` 构造增 `McpGatewayConfig config` 字段；`CreateTransport` 由 `static` 改实例方法（或加 `int timeoutMs` 参数），按 transport 分派时注入 `config.ExecutionTimeoutMs`；`TryConnectAsync` 调用点相应改。

### C. 配置对外读写
- `file: Plugins/McpCenter/Models/McpCenterConfigDto.cs`
  `reason:` `McpCenterConfigDto` 加 `public int ExecutionTimeoutMs`；`McpCenterConfigUpdateDto` 加 `public int? ExecutionTimeoutMs`。
- `file: Plugins/McpCenter/Services/McpCenterRuntime.cs`
  `reason:` `GetInfo()` 组视图加 `executionTimeoutMs = _config.ExecutionTimeoutMs`；`UpdateAsync` 处理 `update.ExecutionTimeoutMs`（`McpGatewayConfig.IsValid` 校验，非法→抛/返回 400 错误路径与既有风格一致；合法则赋值）；`PersistAsync` 匿名对象加 `executionTimeoutMs`；异常回滚分支恢复 `oldExecutionTimeoutMs`。
- `file: Plugins/McpCenter/Controllers/McpCenterConfigController.cs`（若 PUT 绑定错误需 400）
  `reason:` 视 Runtime 返回的错误语义，确保越界 PUT → `ApiResponse{success:false,code:400}`。（先读控制器确认它是直接透传 Runtime 还是自行 try/catch。）
- `file: Plugins/McpCenter/McpCenterPlugin.cs`
  `reason:` `new McpClientManager(externalStore)` → `new McpClientManager(config, externalStore)`（用同一 `config` 实例，runtime 改后下次建连自然读新值）。

### D. 前端
- `file: Plugins/McpCenter/web/src/types/gateway.ts`
  `reason:` `McpCenterConfigDto` 加 `executionTimeoutMs: number`；`McpCenterConfigUpdateDto` 加 `executionTimeoutMs?: number`。
- `file: Plugins/McpCenter/web/src/McpCenterView.vue`
  `reason:` 网关配置 tab，「会话模式」行（:846-855）后加「外部 MCP 执行超时」`ElInputNumber`（min=1 max=3600 step=10，单位秒）+ `form-hint`；`timeoutInputSec` ref；`load` 回填 `cfg.executionTimeoutMs/1000`；`save` 若 `timeoutInputSec*1000 !== cfg.executionTimeoutMs` 则 `update.executionTimeoutMs = timeoutInputSec*1000`；成功 toast 文案含「对新建/重连连接生效」。

### E. 版本
- `file: Plugins/McpCenter/plugin.json` + `McpCenter.csproj`
  `reason:` 版本 2.5.1 → 2.5.2（三连），Description 追加 v2.5.2 执行超时可配置。

## Test Plan
- **单测（`ForgeSelf.Api.Tests/Plugins/McpCenterTests/`）**
  - 新 `McpGatewayConfigTimeoutTests.cs`：默认 300000；env 覆盖；env/config 非法回落；`IsValid` 边界（999/1000/3600000/3600001）。
  - 传输超时注入：`new StreamableHttpMcpTransport(url, headers, timeoutMs)` 后 `_http.Timeout == TimeSpan.FromMilliseconds(timeoutMs)`（internal 可见或经行为断言）；确认 `linked` 用注入值（可用极小 timeout + 慢 mock 触发超时断言文案秒数）。
  - `McpClientManager.CreateTransport` 按 config 值注入（断言三传输都吃到）。
  - **复跑既有 PC-010 守卫**：`McpStdioFramingTests`、`McpStdioWindowsShimTests`、`McpClientIntegrationTests`、`McpClientProtocolGuardTests` 全绿（子超时常量不变）。
- **插件前端**：`cd Plugins/McpCenter/web && pnpm run build` + `pnpm run test`（若有 view 单测则补载入回填/换算）。
- **e2e（`ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts`）**：网关配置 PUT `executionTimeoutMs` 往返（600→GET 回显 600→改回）；`Fixtures/mock-mcp-server.js` 加一个 `slow` 工具（延时 ~35s），配 executionTimeout=10s 时调用超时报错、=60s 时成功——**证明配置真实驱动执行超时**（零容忍断言，无降级 pass）。截图读图：新超时输入框。
- **门禁分档**：后端 `dotnet build` + 定向 `dotnet test --filter McpCenter`；插件前端 build+test；1 条 mcp-center 冒烟 e2e（慢工具那条）；起 dev-stack 现场按 AC 走查；发布本地更新源 -Sign。

## Verification
- Build：`dotnet build`（插件+宿主）0 错（Debug 被并行宿主锁则 `-p:BaseOutputPath=.temp/altbuild/`）。
- Unit：McpCenter 过滤集全绿（新增 + PC-010 既有）。
- E2E：mcp-center 套绿（含超时往返 + 慢工具双档）。
- Other：`check-problem-classes.ps1` 绿（PC-010 不受影响）。

## Rollback
- 纯增量：还原 A-E 六处文件 + 版本回退即回到 2.5.1。改动原子，git 可逐文件 diff 回溯。

## 风险
- 传输构造签名变更波及测试直接构造处——同步改（grep `new StdioMcpTransport(`/`new StreamableHttpMcpTransport(`/`new LegacySseMcpTransport(` 全覆盖）。
- e2e 慢工具用 35s 延时拉长测试时间——用一档够（10s 超时档失败 + 复用既有 connected 断言），控制在可接受范围。

## 实施偏差记录（Implement 阶段回写，2026-10-19）
1. `McpClientManager` 构造按计划是 `(config, store)`，实际改为 `(store, config = null)`（可选参默认新建）：保持既有 18 处测试调用点零改动，插件入口仍传同一 config 实例（Lazy 生效链不变）。
2. `CreateTransport` 从 `public static` 改为 `internal` 实例方法（而非加参数）：需读实例字段 `_gatewayConfig`，internal + InternalsVisibleTo 供单测直接断言注入。
3. Stdio 传输不加 internal accessor（仅 HTTP 两传输加）：stdio 构造即拉起子进程，单测不可行，其超时注入由 `CreateTransport` 层断言覆盖。
4. `Load()` 缺键语义细化：config.json 无 `executionTimeoutMs` 键时反序列化为 0（非类默认值），用 `!= 0` 区分「缺键→隐式默认不记警」与「显式越界→回落记警」，计设计拍板（升级兼容）。
