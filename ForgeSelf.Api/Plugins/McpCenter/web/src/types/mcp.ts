/** 外部 MCP 服务器 DTO（迁自宿主 types/mcp.ts，对应后端 McpServerDto）。 */
export interface McpServerDto {
  id: string
  name: string
  description: string
  status: 'connected' | 'disconnected' | 'error'
  toolCount: number
}

/** 工具 DTO（对应后端 McpToolDto）。 */
export interface McpToolDto {
  id: string
  name: string
  description: string
  serverId: string
  serverName: string
  category: string
  isEnabled: boolean
}

/** 测试结果 DTO（对应后端 McpTestResultDto）。 */
export interface McpTestResultDto {
  success: boolean
  message: string
  durationMs: number
}
