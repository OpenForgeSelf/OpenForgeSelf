# mini-task：复现「MCP 中心·外部 MCP 新增 DeepWiki 后 连接/测试 失败 + 编辑不回显全部值」（输入 · 2026-10-17）

> 级别：轻量（新增 1 个测试文件）。Intent/Spec/Plan/Task 合并本文件；Evidence/Review 单独产出。
> 闸门1：用户指令即明确范围（「先复现一下」），本批**只复现不修复**；修复另立批次。

## Intent
- **Problem**：McpCenter 插件「网关配置 → 外部 MCP 服务器」：① 新增（点「填入 DeepWiki 预设」保存）后点「连接」失败；② 点「测试」失败（现场实测错误：`外部 MCP 返回错误 [-32602]（initialize）: Invalid request parameters`）；③ 点「编辑」没有回显全部值。
- **Why**：三个现象需先钉成可重复证据与确切根因，修复批才有验收基准（红线：结论走可重复测试/现场实测，不用临时脚本）。
- **Expected Outcome**：一条 xUnit 红灯测试锚定 initialize 请求体必须携带必填 `clientInfo`（当前缺失→测试红，修复后转绿）；编辑回显缺陷以 API 实测 + 前端源码行号钉住。
- **Constraints**：不改任何业务代码（McpCenter 后端/前端均不动）；不碰用户运行实例的写端点（:51888 仅 GET 与无副作用的 /test）。
- **Success Criteria**：`dotnet test --filter McpExternalInitializeClientInfoTests` FAIL（红灯=复现成功），失败信息指向 clientInfo 缺失；:51888 现场 /test 返回 400 原文入 05-evidence。

## 适用问题类（problem-class-closure P0 分诊）
- 按台账 tags 表核对（`docs/11-troubleshooting/problem-classes/README.md`）：PC-001/005（跨插件契约/DI）、PC-002（鉴权）、PC-003（插件库）、PC-006/007（脚本/dev-stack）均**不命中**。
- **未命中现成 PC**；候选新类（修复批按 P2 归类后建 PC）：「MCP 协议客户端侧合规缺失（initialize 必填字段/会话头/通知）——第三方服务端严格校验时整链路失败」。与 2026-10-17 stateless 批 07 §建议归档的「SDK 化契约/客户端合规漂移」同族（那次是服务端侧，本次是客户端侧）。

## 根因定位（读码 + 现场实测，修复批按此施工）
### ①②连接/测试失败 —— 根因：客户端 initialize 缺必填 `clientInfo`
- `StreamableHttpMcpTransport.InitializeAsync`（Plugins/McpCenter/Services/McpClient/StreamableHttpMcpTransport.cs:52-55）POST 的 params 仅 `{protocolVersion, capabilities}`，**无 `clientInfo`**。MCP 规范（2025-06-18）`InitializeRequest.params.clientInfo` 必填（`name`+`version`）。
- **现场 A/B 对照实测（2026-10-17，https://mcp.deepwiki.com/mcp）**：不带 clientInfo → `{"error":{"code":-32602,"message":"Invalid request parameters"}}`；带 `clientInfo:{name,version}` → 200 + serverInfo DeepWiki 2.14.3；且带 clientInfo 后**无会话头独立 POST tools/list 正常**（deepwiki 无状态，响应无 mcp-session-id）→ 「每请求独立会话」策略对 deepwiki 不构成第二失败因。
- 运行实例 :51888 复现：`POST /api/mcp-center/servers/deepwiki/test` → HTTP 400，message 逐字 `外部 MCP 返回错误 [-32602]（initialize）: Invalid request parameters`（见 05-evidence）。
- **同类扫查（P3 命中表）**：`StdioMcpTransport.InitializeAsync`（:115-116）与 `LegacySseMcpTransport.InitializeAsync`（:52-56）**同缺 clientInfo**（同根因，属修复批范围）；三处均无 `notifications/initialized` 发送与 `Mcp-Session-Id` 回传——对**有状态**外部服务器会二次失败，属同族次级缺口（修复批一并裁决，见提案）。
### ③ 编辑不回显全部值 —— 根因：编辑表单回填硬编码空值 + 类型丢字段
- 后端 `GET /api/mcp-center/servers` 状态视图**含明文 `url`/`command`/`args`**（McpClientManager.cs:131-134；:51888 实测响应体已证，headers/env 为掩码）。
- 前端 `openEditExternal`（McpCenterView.vue:408-423）把 `url`/`command`/`argsText` **硬编码为 `''`**；`types/external.ts:15-27` 的 `McpExternalServerStateDto` 未声明这三个字段（数据在响应里被类型丢弃）。→ 编辑时地址/命令/参数全空。
- 衍生同根因缺陷：`headersText`/`envText` 回填的是掩码（`****尾4`），保存时 `parseKv` 原样提交 → **掩码覆盖真实密钥**（PUT 合并语义 `dto.Headers ?? baseConfig.Headers` 使掩码串直接落盘）。

## Spec（复现测试规格）
- **Failure Modes**：无现成 PC 命中；候选新类见上节（修复批建 PC 后回填 lens）。
- **Input**：以 `CapturingHandler`（HttpMessageHandler）替身捕获 `StreamableHttpMcpTransport.InitializeAsync` 实际 POST 的请求体。
- **Output（断言）**：请求体 `params.clientInfo` 存在、`clientInfo.name` 非空、`clientInfo.version` 非空——即锚定规范不变量；**当前实现必红**（复现成功形态=红灯）。
- **Error Handling / Boundary**：替身恒回 200 JSON result，排除网络变量；测试结束 CloseAsync 收 GET SSE 后台任务。
- **Acceptance Criteria**：AC1 新测试类编译通过；AC2 `dotnet test --filter McpExternalInitializeClientInfoTests` FAIL 且失败消息含 clientInfo；AC3 :51888 /test 400 原文与 UI 观察逐字一致入 evidence。

## Plan
1. 新建 `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpExternalInitializeClientInfoTests.cs`（单 `[Fact]`，CapturingHandler 模式，不发真实外网请求）。
2. 不碰：`Plugins/McpCenter/**`、`ForgeSelf.Web/**`、宿主任何业务代码。

## Task
- **Objective**：红灯锚定「外部 MCP 客户端 initialize 必须携带 clientInfo」；固化编辑回显缺陷证据链。
- **Scope Allowed**：新增上述 1 个测试文件；本 pilot 目录工件。
- **Scope Forbidden**：修复三缺陷；改插件前后端代码；git 提交（待闸门2）。
- **Acceptance Criteria**：见 Spec AC1-AC3（逐条 checkbox 见 05-evidence）。
- **Verification Commands**：
  - `dotnet build ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj -c Release`
  - `dotnet test ForgeSelf.Api.Tests/ForgeSelf.Api.Tests.csproj -c Release --filter "FullyQualifiedName~McpExternalInitializeClientInfoTests"`（预期 **FAIL=复现成功**）
