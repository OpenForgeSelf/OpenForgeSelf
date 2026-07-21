// AI 供应商模型记录类型（按 contracts/model-fetch-and-list.md 的 JSON 形状）。

/** 单条模型记录（仅元数据，绝不含任何凭证） */
export interface AIModel {
  id: number
  providerId: number
  providerName: string
  upstreamModelId: string
  /** 聊天用模型 id，恒为 `提供商:原始模型id` */
  chatModelId: string
  /** 用户别名，未设置时为 null */
  alias: string | null
  /** 能力标签数组，如 ["vision", "stream"] */
  capabilities: string[]
  /** 最大上下文长度（token），0=未设置 */
  maxContext: number
  enabled: boolean
  /** 上游归属方，如 openai，可能为 null */
  owner: string | null
  lastSyncTime: string
  createTime: string
  updateTime: string
}

/** 按供应商分组的模型列表 */
export interface AIModelGroup {
  providerId: number
  providerName: string
  models: AIModel[]
}

/** 拉取模型结果统计（fetch-models 端点返回） */
export interface FetchModelsResult {
  providerId: number
  providerName: string
  fetched: number
  added: number
  updated: number
  kept: number
  models: AIModel[]
}

/** 编辑模型请求（仅 alias/capabilities/maxContext 可编辑） */
export interface UpdateAIModelRequest {
  alias?: string | null
  capabilities?: string[]
  maxContext?: number | null
}
