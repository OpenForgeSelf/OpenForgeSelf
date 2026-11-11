# mini-task：修 MCP C# SDK 外层转义（PC-027 外层残留）

- **日期**：2026-11-09 ｜ **执行**：fix-mcp-sdk-escaping（子代理，Lead 转派）｜ **形态**：轻量（mini-task，≤3 文件档）
- **闸门1 依据**：用户 2026-11-09 18:30 直接指令「修 MCP C# SDK 外层转义……使 MCP 网关响应里的中文原样输出」（原文见 `.forgeself/memory/2026-11-09.md` 输入11）
- **适用问题类**：[PC-027](../../../11-troubleshooting/problem-classes/PC-027-json-nonascii-default-escaping.md)（JSON 写出路径默认 Encoder 转义非 ASCII）——外层第三方依赖面
- **状态**：⏸️ **BLOCKED（调查闭环，按任务预设停止条件停止）**——实证 SDK 无配置缝，修复在不 fork NuGet 的前提下不可达；选项已呈 Lead/用户拍板（见 06-review Final Decision）

## ① 意图（为什么）

输入10 发布验证（17:49）实测：MCP 网关（`POST http://127.0.0.1:18890/mcp` → `tools/call universal_tool` → `read_file`）响应中，**内层工具信封**（ForgeJson.Default）中文已原样，但 **JSON-RPC/SSE 外层**由 ModelContextProtocol C# SDK 自身序列化，仍转义非 ASCII（`\uFF08` 形态）与引号（`\u0022` 形态）。第三方依赖层，ForgeJson G2 守卫不可达。用户点名修复，使中文在 MCP 线路上原样输出。

## ② 规格要求（做什么 / 到什么程度）

1. 定位 SDK 引用与版本（`Plugins/McpCenter/McpCenter.csproj:38` → ModelContextProtocol.AspNetCore 2.2.0）。
2. **先验证缝、后动手**（任务预设指导原话）：核实 `McpJsonUtilities.DefaultOptions` 可变性；若 SDK 冻结 options / 无序列化缝，**禁止 fork NuGet 包**——停止并报告可行选项（含新版 SDK 配置点）。
3. 若缝存在：TDD RED→GREEN 于 SDK 序列化缝（RED 断言外层中文原样先红 → 最小修复 → 绿）。
4. 快档验证：`dotnet build` 0 error + 过滤集全绿（`*> <log>` 重定向判责，exit code 不可信）。
5. 同类扫查（错误响应路径 / /health / 日志落盘）+ PC-027 归档 + 3 索引点 + 05/06 工件。

## ③ 计划（涉及文件）

- 预期修复落点（若缝存在）：`Plugins/McpCenter/Services/McpGatewayServer.cs` 或 SDK 配置点（仅此 1-2 文件）。
- 实际变更（停止决策后）：**零业务代码变更**；仅文档/工件 7 处（本 pilot 3 件 + PC-027 + 新 case + 3 索引点 + TODO + 日记）。
- 探索期探针：SDK 源码快照存 `.temp/tmp/ssewriterext-2.2.0.cs` 等（不入库，结论不依赖——可凭 05-evidence 所列 URL 重新取得）。

## ④ 任务拆分（TDD 说明）

- [x] 日记首行 + TODO ⑤ 标 🔄 署名
- [x] 完整读 ai-native 规范 + problem-class-closure 技能
- [x] 定 SDK 引用与版本（NuGet 缓存盘点 + DLL 元数据探针）
- [x] 读 SDK 源码确认序列化缝（v2.2.0 在用版 + v2.4.0 最新版逐文件比对；deepwiki 交叉验证）
- [x] **缝验证结论：无配置点**（详见 05-evidence E3-E9）→ 触发任务预设停止条件，TDD RED→GREEN **不适用**（修复面在第三方依赖内部且无注入缝，仓内无可写代码承载该断言——若强行落测试则永远红，违反「不写永久红测试」）
- [x] 同类扫查 + PC-027/case/索引归档
- [x] 05-evidence.md + 06-review.md + 汇报 Lead

## ⑤ 验证（命令 + 判据）

- 无代码变更 ⇒ 构建/测试门禁不触发（基线零漂移可由 `git status` 证明：仅新增文档）。
- 调研验证判据 = 05-evidence 证据链（源码行号引用 + DLL 探针 + STJ 语义锚 + 现场抓包对照），全部 Verified 级。
- 同类扫查结论：SSE 成功路径 / 错误路径 / `/health` / 日志落盘四面，见 06-review 五问。

## Failure Modes（预判失效模式与对策）

| # | 失效模式 | 对策 | 实际发生？ |
|---|----------|------|-----------|
| F1 | 假设 DefaultOptions 可写直接动手 → 反射 hack 脆且只达部分路径 | 先 DLL 探针 + 源码验证可变性（任务预设指导） | 避免 ✓ |
| F2 | 只看 SSE 成功路径漏掉错误路径（`Results.Json`）——两路缝不同 | 两路分别取证（成功路径=writer 硬编码；错误路径=DefaultOptions 冻结），均无缝才停 | 已覆盖 ✓ |
| F3 | 误以为升级 SDK 可解 → 引入新依赖风险 | 同时取证最新版 v2.4.0（同形态无缝）再下结论 | 已覆盖 ✓ |
| F4 | fork/patch NuGet 包「顺手解决」 | 用户预设红线：任何结局下禁止 fork——停止条件触发即停 | 遵守 ✓ |
| F5 | 探针脚本被当验证证据（违反 §5.3） | 探针仅探索期取证；结论 = 源码行号 + URL 可复得证据，探针文件不入库 | 遵守 ✓ |

## 无界面交互

本任务无 UI。交互面 = MCP 网关 HTTP 端点 `POST http://127.0.0.1:18890/mcp`（JSON-RPC 2.0，SSE 响应）：请求示例 `{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"universal_tool","arguments":{"tool":"read_file","parameters":{"cwd":"…","path":"…","start_line":1,"end_line":3}}}}`；成功返回 SSE `event: message\ndata: {JSON-RPC result}`；错误经 JSON-RPC error 对象（HTTP 状态码 + JSON 体）。错误提示形态 = JSON-RPC `error.message` 字符串。无界面验收 ⇒ 由 05-evidence 的抓包与源码证据替代。
