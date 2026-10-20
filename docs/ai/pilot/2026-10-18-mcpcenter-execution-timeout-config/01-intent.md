# 01 Intent — 外部 MCP 执行超时可配置

## Problem
McpCenter 连接外部 MCP 服务器后，转发 `tools/call` / `tools/list` 等请求的超时被写死为 **30 秒**（三个传输各一份 `const RequestTimeoutMs=30000`）。真实世界的慢工具（DeepWiki 代码检索、Playwright 浏览器动作、需要冷启动拉包的 npx 首调）常在 30 秒内回不来，用户只能干等报错，且**无法调整**。

## Why
- 用户 2026-10-18 明确要求「所有 mcp 的执行，默认超时时间改成可配置，默认 5 分钟」。
- 30 秒对交互式/联网型 MCP 工具偏短；而把它写死又让不同网络/工具场景无从适配。
- 顺带纠正一处**超时单位 bug**（HTTP 传输把毫秒当秒），否则把默认提到 5 分钟后该 bug 会以新形态暴露。

## Expected Outcome
- 外部 MCP 请求超时成为**一项网关配置**：默认 **5 分钟（300000ms）**，可在 MCP 中心「网关配置」界面修改、持久化到 `config.json`、支持环境变量覆盖，无需改代码发版。
- 三个传输（stdio / streamable-http / http-sse）的**执行请求超时**统一受该配置驱动。
- 修改后对**新建立的连接**生效（已在用日志/界面提示清楚，不误导为即时改造已挂起的请求）。

## Constraints
- 不改对外协议面（网关对外仍恒 1 个 `universal_tool`）。
- **不破坏 PC-010 的子超时语义**：帧协商探测（5s）、SSE endpoint 发现（15s）是握手/发现阶段固定预算，属"连接建立"而非"执行"，不随本配置变动。
- 不动宿主工具注册表转发路径（`IToolRegistry.ExecuteAsync` 无既有显式超时，本任务不新增——范围仅"外部 MCP 执行"）。
- 遵循既有 Stateless 配置项的同一套贯穿链（config.json + env + GET/PUT DTO + 运行时 + UI），不自造第二套配置模式。
- 不擅自停/启/杀用户运行实例；发布走本地更新源，由用户点更新。

## Success Criteria
1. 全新安装默认执行超时 = 300000ms（5 分钟），界面显示"300 秒"。
2. 界面把超时改成某值并保存 → `config.json` 落该值 → `GET config` 回显该值 → 新连接的外部 MCP 请求按新超时执行（慢响应不再 30s 掐断）。
3. 环境变量 `FORGESELF_MCP_GATEWAY_TIMEOUT_MS` 能覆盖 config.json（e2e/多实例用）。
4. 非法值（≤0 / 超上限）被校验拒绝或回落默认，不产生"永不超时"或 0 超时。
5. HTTP 传输 `_http.Timeout` 单位正确（毫秒→`FromMilliseconds`），不再有"毫秒当秒"。
6. PC-010 既有守卫（帧协商/子超时）全绿不受影响。
