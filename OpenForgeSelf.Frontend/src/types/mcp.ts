export interface McpServerDto {
  id: string
  name: string
  description: string
  status: 'connected' | 'disconnected' | 'error'
  toolCount: number
}

export interface McpToolDto {
  id: string
  name: string
  description: string
  serverId: string
  serverName: string
  category: string
  isEnabled: boolean
}

export interface McpTestResultDto {
  success: boolean
  message: string
  durationMs: number
}