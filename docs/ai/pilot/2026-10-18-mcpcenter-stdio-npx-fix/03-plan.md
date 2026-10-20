# 03-plan（stdio 外部 MCP npx 三层修复批，2026-10-18）

## 改动分组（canonical fix 优先）

### A. 红灯先行（修复前必须红）

- 新建 `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpStdioWindowsShimTests.cs`：真实 npx server-everything 集成（Windows-only + CI skip）；判据=修复前失败输出与用户报错逐字同款。

### B. 后端·启动层（S1）

- `StdioMcpTransport.cs`：构造函数改走 `ResolveLaunch`（internal，注入 pathDirs/isWindows/fileExists）+ `BuildWrapperArguments`/`ShellWrapperPath`/`QuoteForShell`/`ResolvePathDirs`；wrapperArguments 非 null 走 `psi.Arguments`，null 保持 ArgumentList 原行为。

### C. 后端·帧协商层（S2）

- `StdioMcpTransport.cs`：`StdioFraming` enum + `_configuredFraming`/`_observedFraming`/`_probeResent` + `ReadMessageAsync` 双帧自适应 + `WriteMessageAsync(…, framing)` + `ParseFraming` + RequestAsync auto 探测回退重写 + NotifyAsync 跟随写帧；静态 ReadFrameAsync/WriteFrameAsync 保留（McpFrameTests 依赖）；
- `McpExternalServerConfig.cs`（config.Framing + UpsertDto.Framing）、`McpExternalController.cs`（PUT merge 透传）、`ExternalServersStore.cs`（ValidateAndNormalize 透传——**逐字段拷贝吞新字段**教训点）。

### D. 后端·序列化层（S3，跨传输同类扫查=闸门1 批准的同类修复项）

- `McpClientProtocol.cs` 加 `SerializeRequest`；三传输 RequestAsync 改引用 + `arguments ?? new { }` 同族封堵。

### E. 测试与守卫（S4 + G1/G2/G3）

- `Fixtures/mock-mcp-server.js` 加 `runStdioNdjson()` + `--mode stdio-ndjson`；
- 新建 `McpStdioFramingTests.cs`（auto 命中<5s / auto 回退 / explicit Theory / ParseFraming）；
- `McpClientIntegrationTests.cs` 两处 stdio mock 加 `cfg.Framing="lsp"` 快路径；
- `McpClientProtocolGuardTests.cs` 加 PC-010 段（直测 + 三传输源码扫描守卫）；
- e2e：新「stdio npx 真实世界」零容忍用例 + 既有 stdio mock 用例锁 `framing:'lsp'`。

### F. 归档与文档

- PC-010 建档（编号让位：PC-009 已被并行会话 204 类占用）+ case + 三索引；034 功能文档回写；plugin.json/csproj 2.5.1；发布（release-local -Sign，宿主版本串与并行会话 2.4.9 接续 = 2.4.10）。

## 验证顺序（每步改完即对赌）

1. 红灯测试跑红（复现用户报错）→ 2. S1 修复 → ShimTests 绿 → 3. S2/S3（由中间态超时+探针定位）→ 定向 32/32 → 4. 全量 dotnet test（他批红灯归因登记）→ 5. 插件门禁① build + ② e2e 6/6 + 截图读图 → 6. ③ dev-stack 用户原文配置实测 → 7. PC 归档机器校验 → 8. ④ 发布 → ⑤ 待用户。

## 回滚点

- 每步 git 可回溯（会话起点为干净基线上的独立改动集）；dll 锁绕行 = `-p:BaseOutputPath=.temp\altbuild\`（不污染默认输出）；发布失败不回滚源码、重跑脚本即可（幂等）。
