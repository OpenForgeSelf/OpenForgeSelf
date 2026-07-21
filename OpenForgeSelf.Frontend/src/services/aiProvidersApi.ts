import type { AIProvider, AIProviderRequest, AIProviderTestResult } from '@/types/aiProvider'

const API_BASE = '/api/ai-providers'

/**
 * AI 提供方管理 API 封装（数据库化配置）。
 * 保存（新增/编辑/删除）成功后后端自动重载网关注册表，前端无需刷新即可见变更。
 */
export const aiProvidersApi = {
  /** 获取全部提供方 */
  async list(): Promise<AIProvider[]> {
    const res = await fetch(API_BASE)
    if (!res.ok) throw new Error(`获取 AI 提供方列表失败: ${res.status}`)
    const json = await res.json()
    return (json.data || []) as AIProvider[]
  },

  /** 获取指定提供方详情 */
  async getById(id: number): Promise<AIProvider> {
    const res = await fetch(`${API_BASE}/${id}`)
    if (!res.ok) throw new Error(`获取 AI 提供方失败: ${res.status}`)
    const json = await res.json()
    return json.data as AIProvider
  },

  /** 新增提供方（apiKey 必填） */
  async create(req: AIProviderRequest): Promise<AIProvider> {
    const res = await fetch(API_BASE, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    })
    if (!res.ok) {
      const err = await res.json().catch(() => ({ message: '创建失败' }))
      throw new Error(err.message || `创建失败: ${res.status}`)
    }
    const json = await res.json()
    return json.data as AIProvider
  },

  /** 更新提供方（apiKey 留空=保留原密钥） */
  async update(id: number, req: AIProviderRequest): Promise<AIProvider> {
    const res = await fetch(`${API_BASE}/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    })
    if (!res.ok) {
      const err = await res.json().catch(() => ({ message: '更新失败' }))
      throw new Error(err.message || `更新失败: ${res.status}`)
    }
    const json = await res.json()
    return json.data as AIProvider
  },

  /** 删除提供方 */
  async remove(id: number): Promise<void> {
    const res = await fetch(`${API_BASE}/${id}`, { method: 'DELETE' })
    if (!res.ok) {
      const err = await res.json().catch(() => ({ message: '删除失败' }))
      throw new Error(err.message || `删除失败: ${res.status}`)
    }
  },

  /** 测试连接：后端解密 ApiKey 后发一次最小探测请求，返回耗时与结果 */
  async test(id: number): Promise<AIProviderTestResult> {
    const res = await fetch(`${API_BASE}/${id}/test`, { method: 'POST' })
    if (!res.ok) {
      const err = await res.json().catch(() => ({ message: '测试连接失败' }))
      throw new Error(err.message || `测试连接失败: ${res.status}`)
    }
    const json = await res.json()
    return json.data as AIProviderTestResult
  },
}
