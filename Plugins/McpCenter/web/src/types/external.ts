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
  /**
   * 输入 schema 的**原文 JSON 字符串**（后端 McpExternalToolDto.InputSchemaJson）。
   * 不是对象——由 playground/schemaForm.ts 的 parseInputSchema 解析，解析不出就降级。
   */
  inputSchemaJson?: string
}

/** 工具测试台调用请求（POST api/mcp-center/servers/{id}/tools/invoke）。 */
export interface McpToolInvokeRequest {
  /** 外部工具原生名（不是 mcp.<id>.<name> 全名）。 */
  tool: string
  /** 参数 JSON 字符串；为空时后端按 {} 处理。 */
  argumentsJson?: string
}

/** 工具测试台调用结果。 */
export interface McpToolInvokeResult {
  serverId: string
  tool: string
  /** 调用成功（远端未声明 isError）。 */
  ok: boolean
  /** 远端 MCP 声明的 isError。 */
  isError: boolean
  text: string
  rawJson: string
  elapsedMs: number
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
