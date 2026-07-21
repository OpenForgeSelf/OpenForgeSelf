export type AIProviderType = 'OpenAI' | 'Anthropic' | 'Custom'

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
