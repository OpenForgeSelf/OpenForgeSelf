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

/** 一次工具调用的实时状态（流式期间逐步填充：先 tool_call，后 tool_result）。 */
export interface ToolEvent {
  /** 工具名。 */
  name?: string
  /** 调用参数 JSON。 */
  args?: string
  /** 工具返回结果。 */
  result?: string
  /** 执行是否成功（result 到达后填充）。 */
  success?: boolean
  /** 是否仍在执行中（result 未到达）。 */
  pending?: boolean
}

/** token 用量（对应后端 UnifiedUsage）。 */
export interface ChatUsage {
  promptTokens?: number
  completionTokens?: number
  totalTokens?: number
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
  /** 该条回复触发的工具调用名（非流式返回时携带）。 */
  toolCalls?: string[]
  /** 该条回复触发的工具调用明细（流式实时收集，含参数/结果）。 */
  toolEvents?: ToolEvent[]
  /** token 用量。 */
  usage?: ChatUsage
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

/** AI Agent 工具（来自 GET /api/ai-agent/chat/tools，宿主 IToolRegistry；composer 🔧 按 pluginId 白名单过滤后展示）。 */
export interface AgentTool {
  id?: string
  name?: string
  description?: string
  pluginId?: string
}

/** 技能（来自 GET /api/skills）。 */
export interface SkillItem {
  id?: string
  name?: string
  description?: string
  enabled?: boolean
}

/** 项目自动识别的技能（来自 GET /api/project/skills，后端 ProjectSkillItem）。 */
export interface ProjectSkillItem {
  id?: string
  name?: string
  description?: string
  /** 来源分类：agents（.agents/skills） / commands（.codebuddy/commands）。 */
  source?: string
  /** 相对项目根的 SKILL.md / .md 路径。 */
  path?: string
  isDirectory?: boolean
}

/** Agent 人格画像的五维能力（来自 GET /api/agents 的 AgentDefinition.Personality）。 */
export interface AgentPersonality {
  /** 创造力（0~1）。 */
  creativity?: number
  /** 分析力（0~1）。 */
  analytical?: number
  /** 同理心（0~1）。 */
  empathy?: number
  /** 自信度（0~1）。 */
  confidence?: number
  /** 正式度（0~1）。 */
  formality?: number
  /** 擅长领域。 */
  strengths?: string[]
}

/** Agent 定义（来自 GET /api/agents，后端 AgentDefinition）。 */
export interface AgentDefinition {
  id?: string
  name?: string
  description?: string
  /* avatar 为 emoji 字符，非图标名。 */
  avatar?: string
  personality?: AgentPersonality
  capabilities?: string[]
}

/** 项目目录/文件项（来自 GET /api/project/files，后端 ProjectFileEntry）。 */
export interface ProjectEntry {
  name?: string
  isDirectory?: boolean
  relativePath?: string
  size?: number
  updatedAt?: string
}

/** 正在编辑的项目文件（内容由 GET /api/project/file 读取后填充）。 */
export interface EditingFile {
  /** 相对项目根的文件路径。 */
  path: string
  /** 文件名（展示用）。 */
  name: string
  /** 文件内容。 */
  content: string
}

/** 长期记忆条目（来自 GET /api/ai-agent/chat/memories）。 */
export interface MemoryItem {
  id?: number
  title?: string
  content?: string
  /** fact / preference / project / personal / workflow / skill / other。 */
  type?: string
  /** low / medium / high / critical。 */
  importance?: string
  tags?: string[]
  categoryName?: string
  createdAt?: string
  lastAccessedAt?: string
}
