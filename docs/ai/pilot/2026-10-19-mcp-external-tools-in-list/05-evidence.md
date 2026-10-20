# Evidence

> 阶段：Stage 7（Execute → Verify）。只记录实际发生的结果，不根据代码推测越界。
> 每条证据标注出处等级：Verified（真实跑过命令/截图）/ Inferred / Unknown。

## Task

PILOT 2026-10-19-mcp-external-tools-in-list（McpCenter v2.6.0 + AIAgent v1.9.0：外部 MCP 工具进 list_tools 枚举 + 内外均可直调；决策 D10 修订为「枚举可见 + 调用可达，注册表隔离保持」）

## Changed Files

- `ForgeSelf.Abstractions/IMcpExternalGateway.cs` — 新建：跨插件契约 + `McpExternalToolInfo` record + `McpExternalToolNames` 全名命名单一真源（Prefix/BuildFullName/TrySplit）
- `Plugins/McpCenter/Services/McpExternalGatewayAdapter.cs` — 新建：internal 薄适配（ListConnectedTools 走零 IO 会话快照；InvokeAsync 复用 Manager.CallExternalAsync，坏格式/未连接中文错误与网关转发器同源）
- `Plugins/McpCenter/McpCenterPlugin.cs` — `ctx.Register<IMcpExternalGateway>(...)`（置于停止器 Effect 之后，见 03-plan 偏差与 CodeReview Issue 2）
- `Plugins/McpCenter/Services/ListToolsToolFunction.cs` — 合并外部条目（运行期 `ctx.Get`、source/pluginId 语义、keyword/includeSchema 同规则、ParseSchema 抽取、BC-4 降级不崩）+ Description 文案
- `Plugins/McpCenter/Services/UniversalToolForwarder.cs` — 改用 `McpExternalToolNames.TrySplit` + ToolDefinitionJson 文案
- `Plugins/McpCenter/Services/McpClientManager.cs` — `ExternalPrefix` 改契约常量别名、删本地拆分；新增 `ConnectedSessions()`（CodeReview Issue 1）
- `Plugins/McpCenter/Services/McpClient/McpClientSession.cs` — FullName 用 `McpExternalToolNames.BuildFullName`
- `Plugins/AIAgent/Services/ToolFunctions/UniversalTool.cs` — `mcp.` 前缀路由分支（注册表分发之前）+ `ForwardExternalAsync`（每次 Get 禁缓存、null 中文引导、异常封套）+ Description/schema 文案
- 测试：`ForgeSelf.Abstractions.Tests/McpExternalGatewayContractTests.cs`（新）、`ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpExternalGatewayAdapterTests.cs`（新）、`.../ListToolsToolFunctionTests.cs`（扩 6 例）、`ForgeSelf.Api.Tests/Plugins/UniversalToolExternalRouteTests.cs`（新 5 例）
- `ForgeSelf.Web/e2e/plugins/mcp-center/mcp-center.spec.ts` — v2.6.0 用例 + 修存量 `.port-input` strict 冲突（v2.5.2 输入行复用该类，本批首跑全文件才暴露）
- `Plugins/McpCenter/plugin.json` + `McpCenter.csproj`（2.5.2→2.6.0 三处）；`Plugins/AIAgent/plugin.json`（1.8.0→1.9.0）
- 实施偏差记录见 03-plan「实施偏差记录」节（4 条）

## Build

Command:

```bash
dotnet build ForgeSelf.Api/ForgeSelf.Api.csproj -v q --nologo
```

Result: **PASS**（0 error，仅既有告警；CodeReview 修复后复跑仍 0 错）。Verified。

```text
插曲：构建曾被 agent 自起的 dev-stack（PID 86412，非用户实例）锁 ForgeSelf.Abstractions.dll 复制 →
dev-stack.ps1 -Stop 解除；本批域内改动无 CS 错（首跑即证）。
```

## Unit Test

Command:

```bash
dotnet test ForgeSelf.Abstractions.Tests
dotnet test ForgeSelf.Api.Tests --filter "FullyQualifiedName~McpCenter|FullyQualifiedName~UniversalTool"
```

Result: **Abstractions 46/46 绿**（含本批契约守卫 15 断言行）；**Api.Tests 域内 200/200 绿**（含 Adapter 真连 node mock、ListTools 合并 6 新例、AIAgent 路由 5 例；CodeReview 修复后复跑仍 200/200）。Verified。

```text
首跑红一次（如实记录）：ListTools keyword 用例期望值误写 2（keyword=mcp.deepwiki 时宿主 read_file 同被筛，
正确值 1）——测试断言写错，应用行为正确，改断言后绿。
全量 dotnet test（3152）另见 Known Limitations：存量并发互扰 + 存量 BOM 守卫红，与本批无关、定向复跑均过。
```

## Integration Test

Result: 本批新增即含真连集成形态（McpExternalGatewayAdapterTests 真起 node mock stdio 会话：ListConnectedTools 2 条目 + InvokeAsync sum=15）。Verified。

## E2E

Command:

```bash
cd ForgeSelf.Web && pnpm exec playwright test e2e/plugins/mcp-center/mcp-center.spec.ts -g "v2.6.0"
```

Result: **PASS（1 passed, 1.3m）**（globalSetup 每次从源码全量重发宿主，测的是新代码）。Verified。

```text
证据链（screenshots/e2e/mcp-center/list-tools-external.log，4 行）：
- mock http 建连 → list_tools keyword 过滤枚举出 mcp.e2e-listmerge.add/echo（source=mcp-external、pluginId=服务器id、includeSchema 带参数 schema）
- 枚举即可用闭环：全名直调 mcp.e2e-listmerge.add(3,4) → {"sum":7}
- disconnect 后同一枚举外部条目归零（可见性跟随连接态）
UI 渲染用例复跑：首跑红（存量 .port-input strict mode 双元素，v2.5.2 引入类复用、非本批改动）→ 测试侧 .first() 修复 → 1 passed (1.3m)。Verified。
```

## Static Analysis

前端本批无 `Plugins/*/web/` 源码改动（仅 e2e spec，ts 随 playwright 编译执行通过）。主前端 `pnpm run check` 未跑（无前端业务改动，Inferred 不适用）。

## AC 逐项判定

- AC-1 **Verified**（单测 Execute_Merges_External_Tools_With_FullName_And_Source_Marker + e2e 枚举断言）
- AC-2 **Verified**（单测 Keyword/IncludeSchema/BadSchema 3 例 + e2e keyword+includeSchema 腿）
- AC-3 **Verified**（list_tools 是宿主注册表单点，网关与 AIAgent 分发到同一 ListToolsToolFunction；两侧单测同驱动 + 现场走查网关通道实测）
- AC-4 **Verified**（UniversalToolExternalRouteTests 5 例：透传/异常封套/契约缺失中文引导不落 unknown/非前缀零扰动/文案守卫）
- AC-5 **Verified**（Execute_GatewayMissing_Equals_Registry_Only_Before + 既有 200 域内用例全绿）
- AC-6 **Verified**（e2e 1 passed，证据 4 行）
- AC-7 **Verified**（build 0 错 + 域测试全绿 + 版本 2.6.0/1.9.0 落盘并 UI 徽标实测 v2.6.0）

## 现场走查（门禁③，dev-stack 源码实例 :7301/:7399）

- 真连 deepwiki（streamable-http）：connected、toolCount=3。
- 网关通道（POST :18895/mcp，Accept 带 json+event-stream）：`universal_tool {tool:"list_tools", keyword:"mcp.deepwiki", includeSchema:true}` → 3 条 `mcp.deepwiki.*`（source=mcp-external、pluginId=deepwiki、hasSchema=True）+ universal_tool 因描述含关键字同现（宽松过滤语义符合预期）。
- 直调 `mcp.deepwiki.ask_wiki_question(repoName=microsoft/vscode)` → 真实回答原文（转发链确证）。
- UI（Browser 子代理只读走查）：版本徽标 **v2.6.0**、DeepWiki 卡片「已连接 · 3 个工具 · 协议 2025-11-25」、tools 端点 3 工具名齐全。截图因 IDE 浏览器窗口隐藏不可产出（与 v2.5.2 批同环境限制），像素证据改用 e2e `gateway-tab.png` 读图：徽标 v2.6.0 + 网关配置表单（含执行超时行）布局完好。Verified。
- 探针数据清理：deepwiki disconnect + DELETE，GET /servers 归零。

## Known Limitations

- 全量 `dotnet test`（3152）存在**存量**并发互扰（两次跑失败集 15/20 不同，定向复跑全部单过）与 `RepositoryScriptTests` 指 `backfill-feature-ids.ps1` 缺 BOM（存量，TODO 已有 P2 条目）——均与本批改动面零交集，已按同族形态追加登记 flaky 台账（TODO P3 条目），不顺手修。
- 卸载窗口语义（CodeReview Issue 2）已用「注册置于停止器之后」消解；插件热重载瞬间若消费方恰在 Invoke 中途，仍按连接层错误封套返回（非崩溃，可接受）。
- AIAgent 侧 mcp. 直调的端到端证据为单测 + 契约同源论证（AC-3/AC-4）；真实 LLM 会话内自发调用外部工具属运行实例侧行为，留待门禁⑤（用户启用新版本后真实走查）覆盖。

## Unresolved Issues

无（门禁①②③已绿；④发布按硬闸门等用户授权后进行，与 v2.5.2 批合并为 2.6.0 一次发布）。

## AC Coverage

| AC | 验证方式 | 等级 |
|---|---|---|
| AC1 list_tools 含外部条目 | ListToolsToolFunctionTests + e2e v2.6.0 | Verified |
| AC2 keyword 过滤 + includeSchema | ListToolsToolFunctionTests | Verified |
| AC3 网关与 AIAgent 同一结果 | ListToolsToolFunctionTests + UniversalToolExternalRouteTests | Verified |
| AC4 universal_tool 直调 mcp.* | UniversalToolExternalRouteTests | Verified |
| AC5 无外部连接等价现状 | 既有单测全绿 | Verified |
| AC6 e2e v2.6.0 | Playwright e2e PASS | Verified |
| AC7 build 0错 + 版本 bump | dotnet build + 发布核验 | Verified |
