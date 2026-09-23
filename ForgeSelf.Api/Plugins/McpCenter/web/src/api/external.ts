/**
 * 外部 MCP 服务器管理 API（v2.1.0）。
 * 端点：api/mcp-center/servers（GET 列表 / POST 新增 / PUT 更新 / DELETE 删除）
 *      + {id}/connect|disconnect|tools|test。
 * 响应统一 { code, message, data, success } 信封（apiGet/apiPost/apiPut 已解包 data）。
 */
import { apiDelete, apiGet, apiPost, apiPut } from '../http'
import type {
  McpExternalServerConfigDto,
  McpExternalServerStateDto,
  McpExternalServerUpsertDto,
  McpExternalToolDto,
} from '../types/external'

const BASE = '/api/mcp-center/servers'

/** 服务器列表（状态视图，含连接状态 / 工具数 / 脱敏密钥）。 */
export function fetchExternalServers(): Promise<McpExternalServerStateDto[] | undefined> {
  return apiGet<McpExternalServerStateDto[]>(BASE)
}

/** 新增外部服务器（enabled 自动建连）。 */
export function createExternalServer(body: McpExternalServerUpsertDto): Promise<McpExternalServerConfigDto | undefined> {
  return apiPost<McpExternalServerConfigDto>(BASE, body)
}

/** 更新外部服务器（省略字段保留原值；enabled 变化触发重连）。 */
export function updateExternalServer(
  id: string,
  body: Partial<McpExternalServerUpsertDto>
): Promise<McpExternalServerConfigDto | undefined> {
  return apiPut<McpExternalServerConfigDto>(`${BASE}/${id}`, body)
}

/** 删除外部服务器。 */
export function deleteExternalServer(id: string): Promise<void> {
  return apiDelete(`${BASE}/${id}`)
}

/** 手动建立连接。 */
export function connectExternalServer(id: string): Promise<McpExternalServerStateDto | undefined> {
  return apiPost<McpExternalServerStateDto>(`${BASE}/${id}/connect`)
}

/** 断开连接。 */
export function disconnectExternalServer(id: string): Promise<McpExternalServerStateDto | undefined> {
  return apiPost<McpExternalServerStateDto>(`${BASE}/${id}/disconnect`)
}

/** 拉取该服务器的外部工具清单（真实 initialize + tools/list）。 */
export function fetchExternalTools(id: string): Promise<McpExternalToolDto[] | undefined> {
  return apiGet<McpExternalToolDto[]>(`${BASE}/${id}/tools`)
}

/** 连接测试（真实握手：initialize + ping）。 */
export function testExternalServer(id: string): Promise<{ success: boolean; message: string; durationMs: number } | undefined> {
  return apiPost<{ success: boolean; message: string; durationMs: number }>(`${BASE}/${id}/test`)
}
