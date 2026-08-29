import type {
  PersonalLibraryStatsDto,
  GrowthCurvePoint,
  TimeSavedEstimateDto,
  ContextualRecommendationDto,
  ContextualRecommendationRequest,
  SaveAsSuggestionDto,
  SaveAsSuggestionRequest
} from '@/types/usageStats'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'

export const usageStatsApi = {
  async getPersonalLibraryStats(timeRange: string = '30d'): Promise<PersonalLibraryStatsDto> {
    const response = await fetch(`${API_BASE_URL}/usagestats/personal-library?timeRange=${encodeURIComponent(timeRange)}`)

    if (!response.ok) {
      throw new Error(`获取个人工具库统计失败: ${response.status}`)
    }

    const data = await response.json()
    const result = data.data || data

    return {
      ...result,
      recentUsage: (result.recentUsage || []).map((item: Record<string, unknown>) => ({
        ...item,
        timestamp: new Date(item.timestamp as string)
      }))
    } as PersonalLibraryStatsDto
  },

  async getGrowthCurve(days: number = 30): Promise<GrowthCurvePoint[]> {
    const response = await fetch(`${API_BASE_URL}/usagestats/growth-curve?days=${days}`)

    if (!response.ok) {
      throw new Error(`获取成长曲线失败: ${response.status}`)
    }

    const data = await response.json()
    return data.data || data || []
  },

  async getTimeSavedEstimate(): Promise<TimeSavedEstimateDto> {
    const response = await fetch(`${API_BASE_URL}/usagestats/time-saved`)

    if (!response.ok) {
      throw new Error(`获取节省时间估算失败: ${response.status}`)
    }

    const data = await response.json()
    return data.data || data
  },

  async getContextualRecommendations(request: ContextualRecommendationRequest): Promise<ContextualRecommendationDto> {
    const params = new URLSearchParams()
    if (request.currentPage) {
      params.append('currentPage', request.currentPage)
    }
    if (request.currentAction) {
      params.append('currentAction', request.currentAction)
    }
    if (request.limit !== undefined) {
      params.append('limit', String(request.limit))
    }

    const queryString = params.toString()
    const url = `${API_BASE_URL}/usagestats/recommendations/contextual${queryString ? `?${queryString}` : ''}`

    const response = await fetch(url)

    if (!response.ok) {
      throw new Error(`获取上下文推荐失败: ${response.status}`)
    }

    const data = await response.json()
    return data.data || data
  },

  async getSaveAsSuggestion(request: SaveAsSuggestionRequest): Promise<SaveAsSuggestionDto> {
    const response = await fetch(`${API_BASE_URL}/usagestats/recommendations/save-suggestion`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(request)
    })

    if (!response.ok) {
      throw new Error(`获取保存建议失败: ${response.status}`)
    }

    const data = await response.json()
    return data.data || data
  }
}
