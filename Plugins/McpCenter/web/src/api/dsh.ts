import { apiGet, apiPost, apiPut, apiDelete } from '../http'
import type { DshMcpConfigDto, DshMcpServerUpsertDto } from '../types/dsh'

/**
 * dsh（DeepSeek Harness）MCP 配置管理 API（v2.3.0，api/mcp-center/dsh）：
 * 列出当前配置 / 新增 / 编辑 / 启停 / 删除 / 去重。全部走宿主管理面鉴权。
 */

function profileQuery(profile?: string): string {
  return profile ? `?profile=${encodeURIComponent(profile)}` : ''
}

/** 列出当前 dsh MCP 配置。 */
export function fetchDshMcpConfig(profile?: string): Promise<DshMcpConfigDto | undefined> {
  return apiGet<DshMcpConfigDto>(`/api/mcp-center/dsh${profileQuery(profile)}`)
}

/** 新增一条 MCP 客户端条目。 */
export function addDshServer(profile: string, body: DshMcpServerUpsertDto): Promise<DshMcpConfigDto | undefined> {
  return apiPost<DshMcpConfigDto>(`/api/mcp-center/dsh/servers${profileQuery(profile)}`, body)
}

/** 编辑一条 MCP 客户端条目（id 不可改）。 */
export function updateDshServer(profile: string, id: string, body: DshMcpServerUpsertDto): Promise<DshMcpConfigDto | undefined> {
  return apiPut<DshMcpConfigDto>(`/api/mcp-center/dsh/servers/${encodeURIComponent(id)}${profileQuery(profile)}`, body)
}

/** 删除一条 MCP 客户端条目（含重复项）。 */
export function deleteDshServer(profile: string, id: string): Promise<DshMcpConfigDto | undefined> {
  return apiDelete<DshMcpConfigDto>(`/api/mcp-center/dsh/servers/${encodeURIComponent(id)}${profileQuery(profile)}`)
}

/** 启用/停用一条 MCP 客户端条目。 */
export function toggleDshServer(profile: string, id: string, enabled: boolean): Promise<DshMcpConfigDto | undefined> {
  const params = new URLSearchParams()
  if (profile) params.append('profile', profile)
  params.append('enabled', enabled ? 'true' : 'false')
  return apiPost<DshMcpConfigDto>(`/api/mcp-center/dsh/servers/${encodeURIComponent(id)}/toggle?${params.toString()}`)
}

/** 一键去重（同一 id 只保留第一条）。 */
export function dedupeDshServers(profile: string): Promise<DshMcpConfigDto | undefined> {
  return apiPost<DshMcpConfigDto>(`/api/mcp-center/dsh/dedupe${profileQuery(profile)}`)
}

/**
 * 快捷写入 ForgeSelf 条目地址（**旧版兼容**，对应控制器 `McpCenterDshController.QuickWrite`
 * = `POST api/mcp-center/dsh`，体 `{profile,serverId,serverName,url}`）。
 * 2026-10-06 补回：CRUD 重构把这层客户端删了，但 `McpCenterView.vue` 仍在调用它，
 * 插件前端 `vite build` 当场红（`"writeDshMcpConfig" is not exported`）⇒ 整条发布链断在
 * `plugin-web/McpCenter`。新界面请优先用上面 5 个 CRUD 函数；此函数只为让旧视图与
 * 后端保留的兼容端点继续对得上，视图迁完后应连同后端 QuickWrite 一起下线。
 */
export function writeDshMcpConfig(body: {
  profile?: string
  serverId?: string
  serverName?: string
  url: string
}): Promise<DshMcpConfigDto | undefined> {
  return apiPost<DshMcpConfigDto>('/api/mcp-center/dsh', body)
}
