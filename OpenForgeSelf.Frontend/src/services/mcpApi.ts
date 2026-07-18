import type { McpServerDto, McpToolDto, McpTestResultDto } from '@/types/mcp'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'

async function unwrap<T>(response: Response): Promise<T> {
  const data = await response.json()
  if (!response.ok) {
    throw new Error(data.message || `请求失败: ${response.status}`)
  }
  return data.data ?? data
}

export const mcpApi = {
  /**
   * 获取服务器列表
   */
  async getServers(): Promise<McpServerDto[]> {
    const response = await fetch(`${API_BASE_URL}/mcp/servers`)
    return unwrap<McpServerDto[]>(response)
  },

  /**
   * 获取工具列表
   */
  async getTools(serverId: string, keyword?: string, category?: string): Promise<McpToolDto[]> {
    const params = new URLSearchParams()
    if (keyword) params.append('keyword', keyword)
    if (category) params.append('category', category)
    const query = params.toString()
    const url = `${API_BASE_URL}/mcp/servers/${serverId}/tools${query ? `?${query}` : ''}`
    const response = await fetch(url)
    return unwrap<McpToolDto[]>(response)
  },

  /**
   * 切换工具状态
   */
  async toggleTool(toolId: string): Promise<McpToolDto> {
    const response = await fetch(`${API_BASE_URL}/mcp/tools/${toolId}/toggle`, {
      method: 'POST'
    })
    return unwrap<McpToolDto>(response)
  },

  /**
   * 测试工具
   */
  async testTool(toolId: string): Promise<McpTestResultDto> {
    const response = await fetch(`${API_BASE_URL}/mcp/tools/${toolId}/test`, {
      method: 'POST'
    })
    return unwrap<McpTestResultDto>(response)
  },

  /**
   * 测试服务器连接
   */
  async testServer(serverId: string): Promise<McpTestResultDto> {
    const response = await fetch(`${API_BASE_URL}/mcp/servers/${serverId}/test`, {
      method: 'POST'
    })
    return unwrap<McpTestResultDto>(response)
  }
}