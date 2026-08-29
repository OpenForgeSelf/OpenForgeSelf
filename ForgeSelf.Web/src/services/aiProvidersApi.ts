import type { AIProvider, AIProviderRequest, AIProviderTestResult } from '@/types/aiProvider'
import { request } from './request'

const API_BASE = '/api/ai-providers'

/**
 * AI 提供方管理 API 封装（数据库化配置）。
 * 保存（新增/编辑/删除）成功后后端自动重载网关注册表，前端无需刷新即可见变更。
 * 统一走 request.ts：自动携带 Authorization 头、统一错误格式。
 */
export const aiProvidersApi = {
  /** 获取全部提供方 */
  async list(): Promise<AIProvider[]> {
    const json = await request<any>(API_BASE)
    return (json.data || []) as AIProvider[]
  },

  /** 获取指定提供方详情 */
  async getById(id: number): Promise<AIProvider> {
    const json = await request<any>(`${API_BASE}/${id}`)
    return json.data as AIProvider
  },

  /** 新增提供方（apiKey 必填） */
  async create(req: AIProviderRequest): Promise<AIProvider> {
    const json = await request<any>(API_BASE, {
      method: 'POST',
      body: JSON.stringify(req),
    })
    return json.data as AIProvider
  },

  /** 更新提供方（apiKey 留空=保留原密钥） */
  async update(id: number, req: AIProviderRequest): Promise<AIProvider> {
    const json = await request<any>(`${API_BASE}/${id}`, {
      method: 'PUT',
      body: JSON.stringify(req),
    })
    return json.data as AIProvider
  },

  /** 删除提供方 */
  async remove(id: number): Promise<void> {
    await request(`${API_BASE}/${id}`, { method: 'DELETE' })
  },

  /** 测试连接：后端解密 ApiKey 后发一次最小探测请求，返回耗时与结果 */
  async test(id: number): Promise<AIProviderTestResult> {
    const json = await request<any>(`${API_BASE}/${id}/test`, { method: 'POST' })
    return json.data as AIProviderTestResult
  },
}
