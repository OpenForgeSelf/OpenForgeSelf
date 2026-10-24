# Specification

> Stage 2｜Task ID: 2026-10-23-mcp-gateway-cors

## Functional Requirements

- FR1：MCP 网关配置页与配置 API 可管理允许跨域访问的 Origin 列表。
- FR2：白名单来源可完成浏览器预检并读取 MCP HTTP 响应。
- FR3：非白名单来源不获得跨域读取授权。
- FR4：OPTIONS 预检不要求 Bearer token；实际 `/mcp` 请求继续执行现有 Bearer 校验。
- FR5：配置经现有配置 API/UI、持久化和网关热重启链路生效。

## Input / Output

- 输入：HTTP `Origin`、预检 `Access-Control-Request-Method/Headers`、用户配置的 Origin 列表（拟字段 `AllowedOrigins`）。
- 白名单响应：精确回显匹配的 Origin，声明 MCP 所需方法/请求头；不启用 Cookie credentials。
- 非白名单响应：不返回允许该 Origin 的 CORS 头。OPTIONS 不进入 JSON-RPC handler。

## Business Rules

- 默认白名单为空；Origin 按 scheme/host/port 精确匹配，不接受路径或通配符。
- 只对 `/mcp` 启用 CORS；`/health` 不在范围内。
- CORS 不替代 Bearer 鉴权；无 token 的现有行为不因本功能改变。
- 方法/请求头限定 MCP 必需集合，拟含 `POST`、`GET`、`OPTIONS` 及 `Authorization`、`Content-Type`、`Accept`、`Mcp-Protocol-Version`、`Mcp-Session-Id`、`Last-Event-ID`；实现前核对 SDK/传输要求。

## Boundary Conditions / Error Handling

- 无 Origin 的非浏览器请求保持兼容。
- 非白名单 Origin 不获得 CORS 许可。
- 无效配置值应拒绝或忽略并可诊断，遵循现有配置容错风格。
- token 配置后，实际请求缺失/错误 token 仍返回 401。

## Compatibility / Non-functional Requirements

- 旧 config.json 缺字段时按空白名单处理；未提交的配置字段保持不变。
- 不新增依赖；来源使用精确匹配；预检不触发工具执行。
- 配置更新走现有网关热重启机制。

## Interaction Design

| 交互点 | 触发 | 结果 | 反馈/边界 |
| --- | --- | --- | --- |
| 编辑来源 | 配置页编辑 Origin 列表并保存 | 保存并重启后新规则生效 | 空列表代表不允许跨域；无效 Origin 给出错误 |

## Acceptance Criteria

- AC1：允许来源 OPTIONS 预检获得成功状态、匹配 Origin、所需方法和请求头。
- AC2：允许来源 POST `/mcp` 响应带正确 CORS 头。
- AC3：非白名单来源不获得 CORS 许可。
- AC4：配置 token 后，允许来源无 token 的实际请求仍 401，正确 token 可继续协议流程。
- AC5：默认值、旧配置兼容、持久化和热重启有自动化覆盖。
- AC6：插件 build、相关后端测试及单条 MCP Center 冒烟 e2e 通过。

## Unknown

| 不确定点 | 影响 | 处理方式 |
| --- | --- | --- |
| 是否允许任意 Origin | 安全边界 | 用户确认由网关配置管理；按 Origin 白名单实现，默认空 |
| 是否覆盖 `/health` | 端点范围 | 用户确认仅对 `/mcp` 开启；`/health` 不覆盖 |
| 需 expose 哪些响应头 | 浏览器 MCP 客户端读取 session/protocol header | 实现前核 SDK/transport 并更新 Plan |
