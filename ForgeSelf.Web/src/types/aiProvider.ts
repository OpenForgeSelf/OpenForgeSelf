export type AIProviderType = 'OpenAI' | 'OpenAIResponses' | 'Anthropic' | 'Custom'

/** AI 提供方（列表/详情），ApiKey 仅返回掩码 */
export interface AIProvider {
  id: number
  name: string
  providerType: AIProviderType
  endpoint: string
  apiKeyMasked: string
  supportedModels: string[]
  isDefault: boolean
  timeoutSeconds: number
  visionModel: string | null
  enableMultimodal: boolean
  visionPromptTemplate: string | null
  createTime: string
  updateTime: string
}

/**
 * 测试连接请求体。modelId 非空 = 后端用该已登记模型发真实 chat（响应含 ok 判成功）；
 * 省略/null = 仅探连通（兼容旧行为）。
 */
export interface AIProviderTestRequest {
  modelId?: number | null
}

/** 新增/编辑请求。apiKey 留空或省略 = 保留原密钥 */
export interface AIProviderRequest {
  name: string
  providerType: AIProviderType
  endpoint: string
  apiKey?: string
  supportedModels?: string[]
  isDefault: boolean
  timeoutSeconds: number
  visionModel?: string | null
  enableMultimodal: boolean
  visionPromptTemplate?: string | null
}

/** 测试连接结果 */
export interface AIProviderTestResult {
  success: boolean
  latencyMs: number
  message: string
  statusCode: number | null
}
