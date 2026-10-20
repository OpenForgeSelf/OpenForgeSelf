# 00 Repository Understanding — McpCenter 外部 MCP 执行超时可配置（2026-10-18）

## 任务
用户：「所有 mcp 的执行，默认超时时间改成可配置，默认 5 分钟」。

## 仓库现状复核（实测，非推测）
- 技术栈：.NET 10 插件 `Plugins/McpCenter/`（NewLife.XCode 无关，本任务是纯传输层配置）；前端插件自带 `Plugins/McpCenter/web/`（Vue3 + Element Plus）；宿主 e2e 走 `ForgeSelf.Web/e2e/plugins/mcp-center/`。
- 配置三级优先序真源：`McpGatewayConfig.Load(dataDir)`（环境变量 → `config.json` → 内置默认），现有字段 `Port/ListenHost/Token/Stateless`。

## 超时现状（硬编码点 · 全部定位）
三个**外部 MCP 客户端传输**各自 `public const int RequestTimeoutMs = 30000;`（30 秒）：
- `Services/McpClient/StdioMcpTransport.cs:58`（另有 `FramingProbeTimeoutMs=5000` 帧协商探测子超时，:61）
- `Services/McpClient/StreamableHttpMcpTransport.cs:17`
- `Services/McpClient/LegacySseMcpTransport.cs:17`（另有 `EndpointDiscoverTimeoutMs=15000` SSE endpoint 发现子超时，:16）

用法：每个请求 `linked.CancelAfter(RequestTimeoutMs)`；HTTP 传输另设 `_http.Timeout = TimeSpan.FromSeconds(RequestTimeoutMs)`。

### 附带缺陷（本任务顺带修，同根因·超时单位）
`StreamableHttpMcpTransport.cs:41` / `LegacySseMcpTransport.cs:38`：`TimeSpan.FromSeconds(RequestTimeoutMs)` 把**毫秒值 30000 当秒**（=8.3 小时）。当前被更严的 `linked.CancelAfter(30000ms)` 兜住未暴露，但一旦把默认提到 300000ms 并直接套 `FromSeconds`，HttpClient 超时会变成 300000 秒——必须改 `FromMilliseconds`。属同一超时线程的 correctness，纳入本批（非无关重构）。

## 构造/注入链（超时如何贯穿）
`McpCenterPlugin.cs:42` `config = McpGatewayConfig.Load(dataDir)` → 同一 `config` 实例已喂 `McpGatewayServer(:57)` 与 `McpCenterRuntime(:23 构造)`。`McpClientManager(:55)` 目前只接 `ExternalServersStore`，`CreateTransport(cfg)`（`McpClientManager.cs:189`，静态）按 transport 分派 `new StdioMcpTransport(cfg)` / `new LegacySseMcpTransport(url,headers)` / `new StreamableHttpMcpTransport(url,headers)`。
→ 贯穿方案：把 `config` 传入 `McpClientManager`，`CreateTransport` 连接时读 `config.ExecutionTimeoutMs` 注入各传输构造；runtime PUT 改的是同一实例 → 下次建连自动读到新值（lazy，天然生效，无需额外事件）。

## 配置暴露链（Stateless 即模板，逐项对标）
- 后端：`Models/McpCenterConfigDto.cs`（`McpCenterConfigDto` GET 视图 + `McpCenterConfigUpdateDto` PUT，已含 `Stateless`）
- 运行时：`Services/McpCenterRuntime.cs`（`GetInfo()` 组视图 :36-45；`UpdateAsync` 应用+持久化+热重启 :59-105；`PersistAsync` 写 config.json :126-130）
- 前端类型：`web/src/types/gateway.ts`（`McpCenterConfigDto`/`McpCenterConfigUpdateDto` 已含 `stateless`）
- 前端 UI：`web/src/McpCenterView.vue`（网关配置 tab，`statelessInput` ref :166 / load 回填 :189 / save 组装 :225 / 表单 form-row :846-855）
- API：`web/src/api/gateway.ts`（fetchGatewayConfig / updateGatewayConfig）

## 适用问题类（v1.2.0 增补）
查 `docs/11-troubleshooting/problem-classes/README.md` tags（mcp / timeout / 配置透传）：
- **相邻区域：PC-010（stdio 接入层，帧协商/超时探测）** —— 本任务触碰同三个传输文件，其 `FramingProbeTimeoutMs`/超时链为 PC-010 守卫覆盖对象；lens 进 Spec：**改动传输超时常量时，不得破坏 PC-010 的帧协商探测（5s）与 endpoint 发现（15s）等子超时语义**（这些是握手/发现阶段固定预算，不随"执行超时"配置变动）。
- 无直接复发的已知问题类（本任务是新增配置能力，非缺陷修复）。

## 候选低风险落点选择理由
选「全局网关配置单值 + 传输实例注入」而非「每服务器字段」：用户原话"所有 mcp 的执行…默认…可配置"指向一个全局默认；每服务器覆盖会额外触及 `McpExternalServerConfig`/store/表单/ValidateAndNormalize（PC-010 教训：新字段必须透传 ValidateAndNormalize），成本更高。全局单值最小且满足诉求；per-server 作为待拍板可选扩展。
