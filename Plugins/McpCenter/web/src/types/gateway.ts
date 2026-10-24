/** 网关配置视图（GET api/mcp-center/config，对应后端 McpCenterConfigDto）。 */
export interface McpCenterConfigDto {
  /** 监听端口。 */
  port: number
  /** 监听地址。 */
  listenHost: string
  /** 完整监听 URL（如 http://127.0.0.1:18890）。 */
  listenUrl: string
  /** 是否已设置令牌（true 时客户端需带 Authorization: Bearer &lt;token&gt;）。 */
  hasToken: boolean
  /** 令牌掩码（••••尾4）；未设置为空串。 */
  tokenMasked: string
  /** 允许浏览器跨域访问 /mcp 的 Origin 白名单，空数组表示不开放跨域。 */
  allowedOrigins: string[]
  /** 内置 MCP 服务器是否正在监听。 */
  isRunning: boolean
  /** 会话模式（v2.4.0）：true=无状态（重启不断会话）；false=有状态（握手签发 Mcp-Session-Id）。 */
  stateless: boolean
  /** 外部 MCP 执行请求超时（毫秒，v2.5.2）：默认 300000（5 分钟）；UI 展示/输入为秒。 */
  executionTimeoutMs: number
  /** 插件版本。 */
  version: string
}

/** 网关配置更新（PUT api/mcp-center/config）：token 传空串=清除鉴权、不传=保留原值；各字段不传=不变。 */
export interface McpCenterConfigUpdateDto {
  port?: number
  listenHost?: string
  token?: string
  /** 跨域来源白名单；空数组清空，不传保持不变。 */
  allowedOrigins?: string[]
  /** 会话模式（v2.4.0）：不传=不变；保存后热重启立即生效。 */
  stateless?: boolean
  /** 外部 MCP 执行超时（毫秒，v2.5.2）：不传=不变；合法 1000~3600000；对保存后新建/重连连接生效。 */
  executionTimeoutMs?: number
}
