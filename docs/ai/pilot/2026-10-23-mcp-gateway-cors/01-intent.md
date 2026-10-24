# Intent

> Stage 1｜Task ID: 2026-10-23-mcp-gateway-cors

## Problem

浏览器客户端从不同 Origin 调用 MCP 网关时，当前网关没有 CORS 处理，浏览器会阻止预检或读取响应。

## Why

用户要求 MCP 网关支持跨域访问，以便浏览器客户端可连接 MCP HTTP 端点。

## Expected Outcome

MCP 网关支持用户允许的来源完成预检和 MCP HTTP 请求；来源不匹配时不授权浏览器读取；原有协议与 Bearer 鉴权语义保持可验证。

## Constraints

- 仅改 `Plugins/McpCenter` 独立网关，不改宿主 API 全局 CORS。
- 不新增依赖，不改 MCP 工具权限或鉴权规则。
- 建议 Origin 白名单可配置，默认空。
- 严守 AI-Native 闸门1；本阶段不改业务代码。

## Success Criteria

- 白名单来源的预检与 POST `/mcp` 响应具备所需 CORS 头。
- 非白名单来源不获得 `Access-Control-Allow-Origin`。
- 配置 token 时，实际 MCP 请求仍受 Bearer 校验。
- 后端测试、插件前端门禁及一条插件冒烟 e2e 按批准计划通过。
