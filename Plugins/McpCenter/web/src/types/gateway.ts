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
  /** 内置 MCP 服务器是否正在监听。 */
  isRunning: boolean
  /** 会话模式（v2.4.0）：true=无状态（重启不断会话）；false=有状态（握手签发 Mcp-Session-Id）。 */
  stateless: boolean
  /** 插件版本。 */
  version: string
}

/** 网关配置更新（PUT api/mcp-center/config）：token 传空串=清除鉴权、不传=保留原值；各字段不传=不变。 */
export interface McpCenterConfigUpdateDto {
  port?: number
  listenHost?: string
  token?: string
  /** 会话模式（v2.4.0）：不传=不变；保存后热重启立即生效。 */
  stateless?: boolean
}
