import { apiGet, apiPut } from '../http'
import type { McpCenterConfigDto, McpCenterConfigUpdateDto } from '../types/gateway'

/**
 * MCP 中心网关配置 API（v2.0.0 新增，api/mcp-center/config）：
 * GET 查看监听地址/端口/令牌状态（脱敏）；PUT 修改并热重启内置服务器（失败自动回滚）。
 */

/** 查看网关配置。 */
export function fetchGatewayConfig(): Promise<McpCenterConfigDto | undefined> {
  return apiGet<McpCenterConfigDto>('/api/mcp-center/config')
}

/** 更新网关配置并热重启生效。 */
export function updateGatewayConfig(update: McpCenterConfigUpdateDto): Promise<McpCenterConfigDto | undefined> {
  return apiPut<McpCenterConfigDto>('/api/mcp-center/config', update)
}
