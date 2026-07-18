/**
 * 技能管理类型定义
 */

/** 技能列表项 DTO */
export interface SkillItemDto {
  id: string
  name: string
  description: string
  category: string
  isEnabled: boolean
  toolCount: number
  usageCount: number
  createdAt: string
  updatedAt: string
}

/** 技能详情 DTO */
export interface SkillDetailDto extends SkillItemDto {
  toolIds: string[]
  systemPrompt: string
}

/** 创建技能 DTO */
export interface CreateSkillDto {
  name: string
  description?: string
  category?: string
  systemPrompt?: string
  toolIds?: string[]
}

/** 更新技能 DTO */
export interface UpdateSkillDto {
  name: string
  description?: string
  category?: string
  systemPrompt?: string
  toolIds?: string[]
}

/** 技能查询参数 */
export interface SkillQueryParams {
  keyword?: string
  category?: string
  isEnabled?: boolean
}

/** API 统一响应 */
export interface ApiResponse<T> {
  code: number
  message: string
  data: T
}
