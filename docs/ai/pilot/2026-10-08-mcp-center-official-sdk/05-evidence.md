# 05 Evidence — McpCenter 接入官方 MCP SDK（有状态会话）

> 来源等级标注：**Verified** = 亲自跑过拿到真实输出；**Inferred** = 凭代码推断；**Unknown** = 未验证。三者不混用。

## 1 Changed Files

| 文件 | 动作 | 性质 |
| --- | --- | --- |
| `Plugins/McpCenter/McpCenter.csproj` | 改 | +1 `PackageReference ModelContextProtocol.AspNetCore 2.2.0`（+2 行注释） |
| `Plugins/McpCenter/Services/McpUniversalTool.cs` | **新建** | SDK 工具外壳，转调 `UniversalToolForwarder` |
| `Plugins/McpCenter/Services/McpGatewayServer.cs` | 重写 | `MapMcp` + `Stateless=false` + 鉴权中间件；删自研 POST/GET 处理 |
| `Plugins/McpCenter/McpCenterPlugin.cs` | 改 | 装配同步（去 handler 构造、日志改口径） |
| `Plugins/McpCenter/Services/McpJsonRpcHandler.cs` | 改 | **仅头注释**（保留不删，标注不再参与网关链路） |
| `ForgeSelf.Api.Tests/Plugins/McpCenterTests/McpCenterRuntimeTests.cs` | 改 | 构造签名同步 |
| `docs/ai/pilot/2026-10-08-mcp-center-official-sdk/00~04` | 新建 | 工件 |
| `TODO.md` | 改 | 追加本任务待办 |

## 2 验证记录

### 2.1 编译（Verified）

| 命令 | 结果 |
| --- | --- |
| `dotnet build Plugins/McpCenter/McpCenter.csproj` | **EXIT=1**，共 18 个 CS 错误，**全部集中在 `Plugins/McpCenter/Services/DshMcpConfigWriter.cs`** |
| 我侧文件 | **0 错误**（逐文件核对：错误清单中除 `DshMcpConfigWriter.cs` 外无其他文件） |

错误样例（Verified，原文）：
```text
Services/DshMcpConfigWriter.cs(57,13): error CS0117: “DshMcpConfigDto”未包含“EntryId”的定义
Services/DshMcpConfigWriter.cs(73,55): error CS1061: “DshMcpConfigDto”未包含“EntryId”的定义…
```

### 2.2 尖峰实证（Verified，跑完即删）

尖峰 1（net10.0 最小工程）与尖峰 2（**复刻网关接线**：`CreateSlimBuilder` + DI 注入 + 有状态）均已跑通：

| 项 | 实测读数 |
| --- | --- |
| 包版本（nuget.org `dotnet package search`） | `ModelContextProtocol.AspNetCore` **2.2.0** |
| net10.0 兼容性 | 装包 **0 警告** |
| ① `CreateSlimBuilder` + `AddMcpServer` + `MapMcp` | ✅ 可用 |
| ② `WithTools<T>()` 经 DI 解析带构造依赖的工具实例 | ✅ `tools/list = ['universal_tool']` |
| ③ `Stateless = false` 下发会话 id | ✅ `Mcp-Session-Id=5o-msTR0pWLEzPPxgFRGPA` |
| `call_tool(universal_tool)` | ✅ 返回真实转发串 `{"success":true,"forwarded":{"tool":"list_tools","parameters":{}}}` |
| 声明 `2026-07-28` | ✅ 显式错误 `-32022`（非静默降级） |
| 官方支持版本 | `["2024-11-05","2025-03-26","2025-06-18","2025-11-25"]` —— 与原白名单逐字一致 |

### 2.3 未执行项（**Unknown**，不伪装为通过）

| 项 | 状态 | 阻塞原因 |
| --- | --- | --- |
| T006 新增真实行为用例（`McpGatewayServerSdkTests.cs`） | **未写、未跑** | 插件 DLL 编译不出 |
| `dotnet test --filter McpCenterTests` | **未跑** | 测试工程依赖插件，构建同样被 `DshMcpConfigWriter.cs` 阻断 |
| T007 官方客户端对**真实插件网关**的集成复测 | **未跑** | 同上 |
| 宿主 `dotnet build ForgeSelf.Api` 回归 | **未跑** | 同上 |

## 3 Known Limitations

1. 官方 SDK 响应为 **SSE 帧**（`event: message` / `data: {...}`），原实现返回纯 JSON ⇒ **DSH 补丁层需在宿主升级后复测**（已记 TODO）。
2. 有状态会话由 SDK 管理；业务转发仍无状态（每次 `ForwardAsync` 自包含），未引入业务侧会话依赖。
3. 原 `HandlePostAsync` 的 1 MB 请求体上限改为中间件按 `Content-Length` 判断；**未覆盖无 Content-Length 的分块请求**（行为差异，待验证）。

## 4 Unresolved Issues

1. **【阻断】并行会话未收口**：`Services/DshMcpConfigWriter.cs`（未跟踪，mtime 00:43:40）与 `Models/DshMcpConfigDto.cs`（22:46:20）不匹配 ⇒ 18 个 CS0117/CS1061。多次复核 mtime 未变化。**未修改该文件**（他人作用域）。
2. `McpJsonRpcHandler` + `McpJsonRpcHandlerTests` 保留未删（决策 D1），待链路稳定后下线（已记 TODO）。
3. 提交范围已剔除他人改动：csproj 的版本号 `2.2.1→2.3.0`、`McpCenterPlugin.cs` 的 `AddSingleton(new DshMcpConfigWriter(config))` **均未纳入本次提交**。
