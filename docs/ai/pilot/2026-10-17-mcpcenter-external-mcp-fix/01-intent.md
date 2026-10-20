# 01-intent：外部 MCP 三缺陷修复批（2026-10-17）

## 用户意图
复现批三缺陷（连接失败 / 测试失败 / 编辑不回显）按已批准的六段方案修复；闸门1 = 用户「开工」（含默认项）。

## 成功判据（可验证）
1. 红灯测试 `McpExternalInitializeClientInfoTests` 转绿（initialize 请求体含 clientInfo{name,version}）。
2. 三传输全部走同一 canonical 构造（源码守卫测试绿：禁止手写 initialize 匿名对象）。
3. Streamable HTTP / Legacy SSE / stdio 握手后均发出 `notifications/initialized`；Streamable HTTP 回传 `Mcp-Session-Id` 与 `Mcp-Protocol-Version`（服务器返回时）。
4. 编辑对话框回显 id/name/transport/url/command/args 全量真值；Headers/Env 掩码未改动则**不提交**该字段（后端保留原密钥）；后端对掩码形态值（`****` 前缀且 ≤8 字符）拒绝保存。
5. 真实 DeepWiki 端点「连接/测试」在 dev-stack 源码前后端上走通（截图为证）。
6. 既有回归不破：`dotnet test` McpCenter 全绿（含 node mock 三传输集成测试）、插件前端 `pnpm run build/check/test` 绿、mcp-center 冒烟 e2e 绿。
7. `pwsh scripts/check-problem-classes.ps1` 绿（新 PC 归档合规）。

## 不做
- 不动接口 `IMcpClientTransport` 签名；不重构 UniversalToolForwarder；不动宿主；不碰运行实例进程。
