import type { AIModel, AIModelGroup, FetchModelsResult, UpdateAIModelRequest } from '@/types/aiModel'

const PROVIDER_BASE = '/api/ai-providers'
const MODEL_BASE = '/api/ai-models'

/**
 * AI 供应商模型记录 API 封装。
 * 所有端点均不返回任何凭证（ApiKey），仅模型元数据（FR-012）。
 */
export const aiModelsApi = {
  /** 触发某供应商上游模型拉取并 upsert 入库（测试连接成功后自动调用） */
  async fetchForProvider(id: number): Promise<FetchModelsResult> {
    const res = await fetch(`${PROVIDER_BASE}/${id}/fetch-models`, { method: 'POST' })
    if (!res.ok) {
      const err = await res.json().catch(() => ({ message: '拉取模型失败' }))
      throw new Error(err.message || `拉取模型失败: ${res.status}`)
    }
    const json = await res.json()
    return json.data as FetchModelsResult
  },

  /** 按供应商分组列出模型（providerId 指定供应商；enabledOnly=true 仅已启用） */
  async list(params?: { providerId?: number; enabledOnly?: boolean }): Promise<AIModelGroup[]> {
    const qs = new URLSearchParams()
    if (params?.providerId != null) qs.set('providerId', String(params.providerId))
    if (params?.enabledOnly) qs.set('enabledOnly', 'true')
    const url = qs.toString() ? `${MODEL_BASE}?${qs.toString()}` : MODEL_BASE
    const res = await fetch(url)
    if (!res.ok) {
      const err = await res.json().catch(() => ({ message: '获取模型列表失败' }))
      throw new Error(err.message || `获取模型列表失败: ${res.status}`)
    }
    const json = await res.json()
    return (json.data?.groups ?? []) as AIModelGroup[]
  },

  /** 启用/禁用指定模型 */
  async toggleEnabled(id: number, enabled: boolean): Promise<AIModel> {
    const res = await fetch(`${MODEL_BASE}/${id}/enabled`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ enabled }),
    })
    if (!res.ok) {
      const err = await res.json().catch(() => ({ message: '切换状态失败' }))
      throw new Error(err.message || `切换状态失败: ${res.status}`)
    }
    const json = await res.json()
    return json.data as AIModel
  },

  /** 编辑模型可编辑字段（alias/capabilities）；ProviderName/UpstreamModelId/ChatModelId 锁定 */
  async update(id: number, req: UpdateAIModelRequest): Promise<AIModel> {
    const res = await fetch(`${MODEL_BASE}/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    })
    if (!res.ok) {
      const err = await res.json().catch(() => ({ message: '更新模型失败' }))
      throw new Error(err.message || `更新模型失败: ${res.status}`)
    }
    const json = await res.json()
    return json.data as AIModel
  },
}
