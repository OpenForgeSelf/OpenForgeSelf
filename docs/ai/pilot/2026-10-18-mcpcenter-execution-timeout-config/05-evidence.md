# Evidence

> 阶段：Stage 7（Execute → Verify）。只记录实际发生的结果，不根据代码推测越界。
> 每条证据标注出处等级：Verified（真实跑过命令/截图）/ Inferred / Unknown。

## Task

PILOT 2026-10-18-mcpcenter-execution-timeout-config（McpCenter v2.5.2 外部 MCP 执行超时可配置）

## Changed Files

- `Plugins/McpCenter/Services/McpGatewayConfig.cs` — ExecutionTimeoutMs 常量/属性/env/config.json 三级优先序 + IsValidExecutionTimeout
- `Plugins/McpCenter/Services/McpClient/StdioMcpTransport.cs` — 构造参数化 requestTimeoutMs（FramingProbeTimeoutMs 常量不动）
- `Plugins/McpCenter/Services/McpClient/StreamableHttpMcpTransport.cs` — 参数化 + **FromSeconds→FromMilliseconds 单位修复** + internal accessor
- `Plugins/McpCenter/Services/McpClient/LegacySseMcpTransport.cs` — 同上（EndpointDiscoverTimeoutMs 常量不动）
- `Plugins/McpCenter/Services/McpClientManager.cs` — 构造注入 config（可选参）、CreateTransport internal 实例方法按配置注入
- `Plugins/McpCenter/McpCenterPlugin.cs` — 同一 config 实例贯穿（Lazy 生效链）
- `Plugins/McpCenter/Models/McpCenterConfigDto.cs` — GET/PUT DTO 加字段
- `Plugins/McpCenter/Services/McpCenterRuntime.cs` — GetInfo/ApplyUpdate（校验+回滚）/Persist
- `Plugins/McpCenter/web/src/types/gateway.ts`、`web/src/McpCenterView.vue` — 「执行超时」秒制输入行 + hint 窄视口守卫（min-width:0 + overflow-wrap:anywhere）
- `Plugins/McpCenter/plugin.json`、`McpCenter.csproj` — 2.5.1 → 2.5.2
- `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpGatewayConfigTimeoutTests.cs` — 新建（13 组）
- `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpCenterRuntimeTests.cs` — 追加 3 用例
- `ForgeSelf.Api.Tests/Plugins/McpCenterTests/Fixtures/mock-mcp-server.js` — `--with-slow` 开关（slow(ms) 延时工具，默认不开）
- `ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts` — v2.5.2 双档用例 + UI 取证腿

## Build

Command:

```bash
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj -v q --nologo   # 宿主级（含插件构建图）
```

Result: **PASS**（exit=0，0 error；仅既有告警）。出处等级：Verified。

```text
插曲：①构建曾被 agent 自己前晚起的 dev-stack（PID 93136，非用户实例）锁 DLL → dev-stack.ps1 -Stop 后恢复；
②Roslyn 编译服务器崩溃一次（GeneratorDriver 栈）→ dotnet build-server shutdown 后恢复。
```

## Unit Test

Command:

```bash
dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj --filter "FullyQualifiedName~McpCenter"
```

Result: **PASS 169/169**（含新增 McpGatewayConfigTimeoutTests + Runtime 3 用例 + PC-010 既有守卫复跑）。出处等级：Verified。

```text
首跑两类红（如实记录）：
1) McpGatewayConfigTimeoutTests 9 例 DirectoryNotFound — MakeTempDir 未建目录（用例先写 config.json 再 Load）→ 显式 CreateDirectory 修复；
2) Runtime 越界拒绝 Theory 4 例 — .Wait() 抛 AggregateException 包裹 → 改 Assert.ThrowsAsync(...).Result 与既有用例同法。
复跑全绿。
```

前端单测：`cd Plugins/McpCenter/web && pnpm run test` → **23/23 绿**（UI 改后复跑仍绿）。Verified。

## Integration Test

Result: N/A（本插件无独立集成测试套件；配置读写闭环由单测 + e2e 双层覆盖）。

## E2E

Command:

```bash
cd ForgeSelf.Web && pnpm exec playwright test e2e/plugins/mcp-center/mcp-center.spec.ts -g "v2.5.2"
```

Result: **PASS（1 passed, 1.2m）**（globalSetup 每次从源码全量重发宿主，测的是新代码）。出处等级：Verified。

```text
证据链（execution-timeout.log，7 行）：
- 基线 executionTimeoutMs=300000
- 往返：PUT 5000 → 响应/GET 均回显 5000
- 5s 档掐断：外部 MCP 请求超时（5s）：tools/call（15s 慢工具，isError=true，零容忍无降级）
- 越界 PUT 999ms → HTTP 400
- 60s 档放行：{"slept":15000,"ok":true}（改配置后 DELETE+POST 重连，Lazy 语义成立）
- UI：执行超时输入框回显 60（=60000ms，毫秒→秒换算正确），hint 可见
首跑红一次：error 封套中文被 \u 转义致 toContain 失配 → 先 JSON.parse 再断言；另一次断言单位写错（600 vs 60），应用行为正确。
```

## Static Analysis

`pnpm run check`（vue-tsc）→ **PASS**（0 错）。出处等级：Verified。

## Screenshots

- `ForgeSelf.Web/screenshots/e2e/mcp-center/execution-timeout-ui.png` — 网关配置 tab「执行超时」行（值 60、hint 完整、v2.5.2 徽标）。已读图核验：对齐栅格一致、无裁切。Verified。
- dev-stack 现场走查（Browser 子代理，视口隐藏致截图不可产出）：功能断言 A–I 全部通过（DOM 几何 + aria + 后端 API 实测）；发现并修复 2 处窄视口 UI 缺陷（hint 溢出被 overflow:hidden 裁切 39px、输入框被挤至 84px）。Verified。

## Known Limitations

- 执行超时对**已建立连接**不生效（需断开重连），UI hint 与 toast 文案均已明示（设计拍板，避免运行中掐断在途请求）。
- 走查在 IDE 内置浏览器窄视口（607px）进行，宽视口下缺陷本不显现；修复为通用 CSS 守卫，无回归风险。
- 走查附带发现（非本批引入，记 TODO 不顺手修）：McpCenterView 使用 `v-loading` 但插件前端未注册 ElLoading 指令（console warn ×5，loading 态不生效）。

## Unresolved Issues

无（门禁①②已绿；④发布按硬闸门等用户授权后进行）。

## AC Coverage

| AC | 验证方式 | 等级 |
|---|---|---|
| AC1 默认 300000 | McpGatewayConfigTimeoutTests + e2e 基线 | Verified |
| AC2 贯穿配置生效 | e2e 60s 档放行 + 5s 档掐断 | Verified |
| AC3 env 覆盖 | McpGatewayConfigTimeoutTests EnvOverridesConfig | Verified |
| AC4 校验拒绝 | 单测 IsValid 边界 + e2e 400 | Verified |
| AC5 单位 bug 修 | FromMilliseconds 单测 | Verified |
| AC6 子超时不变 | PC-010 守卫复跑绿 | Verified |
| AC7 DTO+前端全链 | 单测 + e2e UI 回显 | Verified |
| AC8 单测覆盖 + e2e | 169/169 + e2e PASS | Verified |
