export enum UsageEventType {
  Chat = 0,
  ToolCall = 1,
  PluginUse = 2,
  WorkflowRun = 3,
  MemoryAccess = 4,
  AgentUse = 5,
  PageVisit = 6
}

export enum SuggestionType {
  ToolRecommendation = 0,
  WorkflowRecommendation = 1,
  MemoryReminder = 2,
  AgentRecommendation = 3,
  ShortcutSuggestion = 4,
  OptimizationTip = 5,
  LearningRecommendation = 6
}

export enum SuggestionPriority {
  Low = 0,
  Medium = 1,
  High = 2,
  Critical = 3
}

export enum PatternType {
  Temporal = 0,
  Sequential = 1,
  Frequency = 2,
  Contextual = 3,
  Behavioral = 4
}

export interface UsageEventEntity {
  id: number
  userId: string
  eventType: UsageEventType
  eventName: string
  eventData?: string
  category?: string
  tags?: string
  durationMs: number
  success: boolean
  errorMessage?: string
  createdAt: string
}

export interface UsagePatternEntity {
  id: number
  userId: string
  patternType: PatternType
  patternName: string
  patternDescription?: string
  patternData?: string
  confidence: number
  occurrenceCount: number
  firstObservedAt: string
  lastObservedAt: string
  trendScore: number
  isActive: boolean
}

export interface SuggestionEntity {
  id: number
  userId: string
  type: SuggestionType
  title: string
  description: string
  content?: string
  actionUrl?: string
  actionData?: string
  priority: SuggestionPriority
  relevanceScore: number
  sourcePatternId?: string
  isRead: boolean
  isDismissed: boolean
  isActioned: boolean
  readAt?: string
  dismissedAt?: string
  actionedAt?: string
  createdAt: string
  expiresAt: string
}

export interface UserPreferenceEntity {
  id: number
  userId: string
  preferenceKey: string
  preferenceValue?: string
  category?: string
  confidence: number
  updateCount: number
  createdAt: string
  updatedAt: string
  source?: string
}

export interface UserSkillEntity {
  id: number
  userId: string
  skillName: string
  skillCategory?: string
  skillDescription?: string
  proficiencyLevel: number
  usageCount: number
  successRate: number
  averageDurationMs: number
  firstUsedAt: string
  lastUsedAt: string
  trendScore: number
}

export interface UserProfileSummary {
  userId: string
  totalUsageDays: number
  totalActions: number
  topTools: string[]
  topCategories: string[]
  skills: UserSkillEntity[]
  preferences: string[]
  primaryUseTime?: string
  usageStyle?: string
  efficiencyScore: number
  learningRate: number
}
