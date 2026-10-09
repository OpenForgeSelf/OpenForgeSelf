/**
 * dsh（DeepSeek Harness）MCP 配置视图（GET api/mcp-center/dsh，对应后端 DshMcpConfigDto）。
 */

/** 一条 dsh MCP 客户端条目。 */
export interface DshMcpServerDto {
  /** 条目 id（补丁行 id）。 */
  id: string
  /** 暴露给 dsh 的 serverName。 */
  serverName: string
  /** streamable-http / http-sse / stdio。 */
  transport: string
  /** http 系传输的地址。 */
  url: string
  /** stdio 传输的命令。 */
  command: string
  /** stdio 传输的参数。 */
  args: string[]
  /** http 传输的自定义请求头。 */
  headers: Record<string, string>
  /** 是否启用（= !补丁行 disabled）。 */
  enabled: boolean
  /** 所在行号（1 基）。 */
  line: number
}

/** dsh MCP 配置总览。 */
export interface DshMcpConfigDto {
  profile: string
  configPath: string
  configExists: boolean
  /** 默认写入地址（当前网关地址 + /mcp）。 */
  defaultUrl: string
  servers: DshMcpServerDto[]
  /** 重复的条目 id（需要去重）。 */
  duplicateIds: string[]
  lastError?: string | null
}

/** 新增/编辑 dsh MCP 条目。 */
export interface DshMcpServerUpsertDto {
  id?: string
  serverName?: string
  transport?: string
  url?: string
  command?: string
  args?: string
  headers?: Record<string, string>
  enabled?: boolean
}
