/** 外部 MCP 服务器配置（与后端 Models/McpExternalServerConfig.cs 对应）。 */
export interface McpExternalServerConfigDto {
  id: string
  name: string
  enabled: boolean
  transport: 'stdio' | 'streamable-http' | 'http-sse'
  url?: string
  headers?: Record<string, string>
  command?: string
  args?: string[]
  env?: Record<string, string>
}

/** 外部 MCP 服务器状态视图（密钥字段已脱敏）。 */
export interface McpExternalServerStateDto {
  id: string
  name: string
  enabled: boolean
  transport: string
  headersMasked: Record<string, string>
  envMasked: Record<string, string>
  connected: boolean
  toolCount: number
  protocolVersion?: string
  serverInfoName?: string
  lastError?: string
}

/** 外部 MCP 工具（全名 = mcp.<服务器id>.<工具名>）。 */
export interface McpExternalToolDto {
  fullName: string
  serverId: string
  name: string
  description?: string
  inputSchema?: Record<string, unknown>
}

/** 新增 / 更新请求体（PUT 时省略未修改字段）。 */
export interface McpExternalServerUpsertDto {
  id: string
  name: string
  enabled?: boolean
  transport: string
  url?: string
  headers?: Record<string, string>
  command?: string
  args?: string[]
  env?: Record<string, string>
}
