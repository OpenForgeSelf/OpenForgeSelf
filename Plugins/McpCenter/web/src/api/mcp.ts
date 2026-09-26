import { apiGet, apiPost } from '../http'
import type { McpServerDto, McpTestResultDto, McpToolDto } from '../types/mcp'

/**
 * 外部 MCP 服务器与工具管理 API（迁自宿主 services/mcpApi.ts，路由前缀 api/mcp 保留）。
 * HTTP 方法以后端 [Http*] 特性为准（toggle/test 为 POST，server-test 为 GET）。
 */

/** 获取服务器列表（GET /api/mcp/servers）。 */
export function fetchMcpServers(): Promise<McpServerDto[] | undefined> {
  return apiGet<McpServerDto[]>('/api/mcp/servers')
}

/** 获取工具列表（GET /api/mcp/servers/{id}/tools，支持 keyword/category 过滤）。 */
export function fetchMcpTools(
  serverId: string,
  keyword?: string,
  category?: string,
): Promise<McpToolDto[] | undefined> {
  const params = new URLSearchParams()
  if (keyword) params.append('keyword', keyword)
  if (category) params.append('category', category)
  const query = params.toString()
  return apiGet<McpToolDto[]>(`/api/mcp/servers/${encodeURIComponent(serverId)}/tools${query ? `?${query}` : ''}`)
}

/** 切换工具启停（POST /api/mcp/tools/{id}/toggle）。 */
export function toggleMcpTool(toolId: string): Promise<McpToolDto | undefined> {
  return apiPost<McpToolDto>(`/api/mcp/tools/${encodeURIComponent(toolId)}/toggle`)
}

/** 测试工具（POST /api/mcp/tools/{id}/test）。 */
export function testMcpTool(toolId: string): Promise<McpTestResultDto | undefined> {
  return apiPost<McpTestResultDto>(`/api/mcp/tools/${encodeURIComponent(toolId)}/test`)
}

/** 测试服务器连接（GET /api/mcp/servers/{id}/test）。 */
export function testMcpServer(serverId: string): Promise<McpTestResultDto | undefined> {
  return apiGet<McpTestResultDto>(`/api/mcp/servers/${encodeURIComponent(serverId)}/test`)
}
