/**
 * 技能API服务层 - 封装技能管理相关后端通信
 */

import type {
  SkillItemDto,
  SkillDetailDto,
  CreateSkillDto,
  UpdateSkillDto,
  SkillQueryParams,
  ApiResponse,
} from '@/types/skills'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'

async function unwrap<T>(response: Response): Promise<T> {
  const data: ApiResponse<T> = await response.json()
  if (!response.ok) {
    throw new Error(data.message || `请求失败: ${response.status}`)
  }
  return data.data ?? (data as unknown as T)
}

export const skillsApi = {
  /**
   * 获取技能列表
   */
  async fetchSkills(params?: SkillQueryParams): Promise<SkillItemDto[]> {
    const queryParams = new URLSearchParams()
    if (params?.keyword) queryParams.append('keyword', params.keyword)
    if (params?.category) queryParams.append('category', params.category)
    if (params?.isEnabled !== undefined) queryParams.append('isEnabled', String(params.isEnabled))

    const queryString = queryParams.toString()
    const url = `${API_BASE_URL}/skills${queryString ? `?${queryString}` : ''}`

    const response = await fetch(url)
    if (!response.ok) {
      throw new Error(`获取技能列表失败: ${response.status}`)
    }
    return unwrap<SkillItemDto[]>(response)
  },

  /**
   * 获取技能详情
   */
  async fetchSkillDetail(id: string): Promise<SkillDetailDto> {
    const response = await fetch(`${API_BASE_URL}/skills/${id}`)
    if (!response.ok) {
      throw new Error(`获取技能详情失败: ${response.status}`)
    }
    return unwrap<SkillDetailDto>(response)
  },

  /**
   * 创建技能
   */
  async createSkill(dto: CreateSkillDto): Promise<SkillDetailDto> {
    const response = await fetch(`${API_BASE_URL}/skills`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(dto),
    })
    if (!response.ok) {
      throw new Error(`创建技能失败: ${response.status}`)
    }
    return unwrap<SkillDetailDto>(response)
  },

  /**
   * 更新技能
   */
  async updateSkill(id: string, dto: UpdateSkillDto): Promise<SkillDetailDto> {
    const response = await fetch(`${API_BASE_URL}/skills/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(dto),
    })
    if (!response.ok) {
      throw new Error(`更新技能失败: ${response.status}`)
    }
    return unwrap<SkillDetailDto>(response)
  },

  /**
   * 启用/禁用切换
   */
  async toggleSkill(id: string): Promise<SkillItemDto> {
    const response = await fetch(`${API_BASE_URL}/skills/${id}/toggle`, {
      method: 'POST',
    })
    if (!response.ok) {
      throw new Error(`切换技能状态失败: ${response.status}`)
    }
    return unwrap<SkillItemDto>(response)
  },
}
