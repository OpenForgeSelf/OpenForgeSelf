/**
 * 插件界面共享类型。
 *
 * 字段与后端 DTO 对应（后端为 PascalCase，JSON 序列化后按 ASP.NET Core 默认策略为 camelCase）。
 * 只声明界面真正用到的字段，不追求与后端 DTO 完全一一对应。
 */

/** 可用模型（来自 GET /api/ai-models）。 */
export interface AIModel {
  id?: number
  alias?: string
  upstreamModelId?: string
  chatModelId?: string
  providerId?: number
  providerName?: string
}

/** 聊天消息（对应后端 ChatResponse）。 */
export interface ChatMessage {
  id: number | string
  /** 消息角色：user / assistant。 */
  role: string
  /** 消息正文。 */
  content: string
  /** 创建时间（后端返回 DateTime 字符串）。 */
  createTime?: string
  /** 该条回复触发的工具调用名（后端暂无该字段时为空，界面按空处理）。 */
  toolCalls?: string[]
}

/** MCP 服务器（来自 GET /api/mcp/servers）。 */
export interface McpServer {
  id?: string
  name?: string
  enabled?: boolean
}

/** MCP 工具（来自 GET /api/mcp/servers/{id}/tools）。 */
export interface McpTool {
  name?: string
  description?: string
  /** 所属服务器名，由前端在平铺时补上。 */
  serverName?: string
}

/** 技能（来自 GET /api/skills）。 */
export interface SkillItem {
  id?: string
  name?: string
  description?: string
  enabled?: boolean
}
