import type {
  SuggestionEntity,
  UsagePatternEntity,
  UserProfileSummary,
  UserSkillEntity,
  UserPreferenceEntity,
  UsageEventEntity
} from '../types/planning'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'

interface ApiResponse<T = unknown> {
  data?: T
  message?: string
}

function unwrap<T>(response: Response): Promise<T> {
  return response.json().then((data: ApiResponse<T>) => {
    if (!response.ok) {
      throw new Error(data.message || `请求失败: ${response.status}`)
    }
    return (data.data ?? data) as T
  })
}

export const planningApi = {
  getSuggestions: async (limit = 10): Promise<SuggestionEntity[]> => {
    const response = await fetch(`${API_BASE_URL}/planning/suggestions?limit=${limit}`)
    return unwrap<SuggestionEntity[]>(response)
  },

  getPendingSuggestions: async (limit = 20): Promise<SuggestionEntity[]> => {
    const response = await fetch(`${API_BASE_URL}/planning/suggestions/pending?limit=${limit}`)
    return unwrap<SuggestionEntity[]>(response)
  },

  actionSuggestion: async (id: number): Promise<void> => {
    await fetch(`${API_BASE_URL}/planning/suggestions/${id}/action`, { method: 'POST' })
  },

  dismissSuggestion: async (id: number): Promise<void> => {
    await fetch(`${API_BASE_URL}/planning/suggestions/${id}/dismiss`, { method: 'POST' })
  },

  getPatterns: async (days = 30): Promise<UsagePatternEntity[]> => {
    const response = await fetch(`${API_BASE_URL}/planning/patterns?days=${days}`)
    return unwrap<UsagePatternEntity[]>(response)
  },

  getProfile: async (): Promise<UserProfileSummary> => {
    const response = await fetch(`${API_BASE_URL}/planning/profile`)
    return unwrap<UserProfileSummary>(response)
  },

  getSkills: async (): Promise<UserSkillEntity[]> => {
    const response = await fetch(`${API_BASE_URL}/planning/skills`)
    return unwrap<UserSkillEntity[]>(response)
  },

  getPreferences: async (): Promise<UserPreferenceEntity[]> => {
    const response = await fetch(`${API_BASE_URL}/planning/preferences`)
    return unwrap<UserPreferenceEntity[]>(response)
  },

  recordEvent: async (event: Partial<UsageEventEntity>): Promise<void> => {
    await fetch(`${API_BASE_URL}/planning/event`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(event)
    })
  }
}
