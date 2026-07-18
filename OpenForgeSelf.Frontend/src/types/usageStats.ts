export interface ToolUsageRankingItem {
  pluginId: string
  toolId: string
  useCount: number
  totalDurationMs: number
}

export interface TopScriptItem {
  scriptId: number
  scriptName: string
  language: string
  usageCount: number
}

export interface TopWorkflowItem {
  workflowId: string
  workflowName: string
  executionCount: number
  successRate: number
}

export interface PersonalLibraryStatsDto {
  scriptCount: number
  codeSnippetCount: number
  workflowCount: number
  favoriteCount: number
  totalUsageCount: number
  totalUsageDurationSeconds: number
  timeRange: string
  topTools: ToolUsageRankingItem[]
  topScripts: TopScriptItem[]
  topWorkflows: TopWorkflowItem[]
  recentUsage: UsageRecord[]
}

export interface UsageRecord {
  id?: string
  pluginId: string
  toolId: string
  actionType: string
  durationMs: number
  timestamp: Date
}

export interface GrowthCurvePoint {
  date: string
  usageCount: number
  newScripts: number
  newSnippets: number
  newWorkflows: number
}

export interface TimeSavedEstimateDto {
  totalTimeSavedMinutes: number
  totalTimeSavedHours: number
  averageTimeSavedPerUse: number
  breakdown: {
    category: string
    count: number
    timeSavedMinutes: number
  }[]
}

export interface RecommendedToolItem {
  pluginId: string
  toolId: string
  toolName: string
  matchScore: number
  matchReason: string
  usageCount: number
}

export interface RecommendedScriptItem {
  scriptId: number
  scriptName: string
  language: string
  matchScore: number
  matchReason: string
  usageCount: number
}

export interface RecommendedSnippetItem {
  snippetId: number
  title: string
  language: string
  matchScore: number
  matchReason: string
  usageCount: number
}

export interface WorkflowRecommendationDto {
  workflowId: string
  workflowName: string
  matchScore: number
  matchReason: string
  usageCount: number
}

export interface ContextualRecommendationDto {
  recommendedTools: RecommendedToolItem[]
  recommendedScripts: RecommendedScriptItem[]
  recommendedSnippets: RecommendedSnippetItem[]
  recommendedWorkflows: WorkflowRecommendationDto[]
}

export interface ContextualRecommendationRequest {
  currentPage?: string
  currentAction?: string
  recentTools?: string[]
  limit?: number
}

export interface SaveAsSuggestionDto {
  shouldSave: boolean
  suggestedType: string
  reason: string
  confidence: number
  suggestedTitle?: string
  suggestedTags: string[]
}

export interface SaveAsSuggestionRequest {
  actionType?: string
  content?: string
  language?: string
  usageFrequency?: number
}
