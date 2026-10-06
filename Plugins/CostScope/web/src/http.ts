/**
 * 成本观测插件界面的 HTTP 封装。
 *
 * 与其他插件界面一致：直连后端 HTTP 接口，从 localStorage 读取宿主写入的 token
 * （键名与 ForgeSelf.Web/src/services/request.ts 保持一致）。
 *
 * 后端统一响应信封 ApiResponse<T>：{ code, message, success, data }。
 * 本模块的 request() 负责剥信封——调用方只拿到 data，失败时抛 Error(message)。
 *
 * ⚠️ 接口边界（FR-4.6 / 铁律 14）：本插件**只调自己的端点** `api/cost-scope/*`。
 * 严禁调用宿主观测端点（`api/usage`、`api/usage-stats`、`api/chat-records`）：
 * 成本口径以本插件端点为准，混用会出现「两套数字对不上」，且违反插件自治。
 * 有守卫测试（CostScopeWebAssetTests）静态扫描本目录守住这条线。
 */
const TOKEN_KEY = 'forge_api_token'

/** 后端统一响应信封。 */
interface ApiEnvelope<T> {
  code: number
  message: string
  success: boolean
  data: T
}

/** 成本总览（对应后端 CostOverview；字段为 camelCase）。 */
export interface CostOverview {
  todayCost: number
  monthCost: number
  totalCost: number
  costIsLowerBound: boolean
  promptTokens: number
  completionTokens: number
  totalTokens: number
  turns: number
  failCount: number
  unpricedModels: string[]
  unattributedModels: string[]
  topModels: string[]
  coverage: CoverageInfo
}

/** 覆盖度：让页面能显式说明「哪些没算进来」。 */
export interface CoverageInfo {
  coveredCalls: number
  totalRecords: number
  unpricedCalls: number
  mainChatObservable: boolean
  note: string
}

export interface DimensionItem {
  key: string
  turns: number
  promptTokens: number
  completionTokens: number
  cost: number
  failCount: number
  costIsLowerBound: boolean
}

export interface DailyPoint {
  day: string
  cost: number
  costIsLowerBound: boolean
  promptTokens: number
  completionTokens: number
  turns: number
  failCount: number
}

export interface LatencyPercentiles {
  p50: number
  p95: number
  p99: number
  samples: number
}

export interface LatencyStats {
  firstToken: LatencyPercentiles
  duration: LatencyPercentiles
}

export interface ErrorStats {
  total: number
  failed: number
  unknownStatus: number
  rate: number
  buckets: { category: string; count: number }[]
}

export interface PriceItem {
  model: string
  provider: string
  inputPricePer1M: number
  outputPricePer1M: number
  currency: string
  isEnabled: boolean
}

export interface BudgetItem {
  name: string
  scope: string
  target: string
  limitAmount: number
  currency: string
  period: string
  alertThreshold: number
}

export interface TraceNode {
  kind: 'Llm' | 'Tool'
  label: string
  start: string
  durationMs: number
  model: string | null
  tokens: number | null
  agentRunId: string | null
}

export interface TraceWaterfall {
  agentRunId: string | null
  nodes: TraceNode[]
  linkedTurns: number
  unlinkedTurns: number
  isApproximate: boolean
  correlationNote: string
}

export interface CostSettings {
  defaultWindowDays: number
  defaultPage: number
  defaultSize: number
  maxSize: number
  dimensions: string[]
  currency: string
  coverageNote: string
  correlationNote: string
  unpricedPolicy: string
}

/** 插件端点前缀——**唯一允许调用的路径前缀**。 */
export const COST_SCOPE_PREFIX = '/api/cost-scope'

function authHeaders(): Record<string, string> {
  const token = localStorage.getItem(TOKEN_KEY)
  return token ? { Authorization: `Bearer ${token}` } : {}
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${COST_SCOPE_PREFIX}${path}`, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...authHeaders(), ...(init?.headers ?? {}) },
  })

  // 401 单独提示：端点是类级 [Authorize("ApiKeyPolicy")] 保护的（铁律 17）
  if (res.status === 401) throw new Error('未授权（401）：请先在宿主登录，插件端点受 ApiKeyPolicy 保护')
  if (res.status === 404) throw new Error('端点不存在（404）：成本观测插件可能未启用')

  const body = (await res.json()) as ApiEnvelope<T>
  if (!body.success) {
    // 400 的 message 里带具体原因（如「未知维度 by=x；合法值：…」），直接呈现给用户
    throw new Error(body.message || `请求失败（${res.status}）`)
  }
  return body.data
}

function qs(params: Record<string, string | number | undefined | null>): string {
  const usp = new URLSearchParams()
  for (const [k, v] of Object.entries(params)) {
    if (v !== undefined && v !== null && v !== '') usp.set(k, String(v))
  }
  const s = usp.toString()
  return s ? `?${s}` : ''
}

export const costApi = {
  overview: (p: { from?: string; to?: string; model?: string; style?: string } = {}) =>
    request<CostOverview>(`/overview${qs(p)}`),
  daily: (p: { from?: string; to?: string; model?: string; style?: string } = {}) =>
    request<DailyPoint[]>(`/daily${qs(p)}`),
  breakdown: (p: { by: string; top?: number; from?: string; to?: string } ) =>
    request<DimensionItem[]>(`/breakdown${qs(p)}`),
  latency: (p: { from?: string; to?: string } = {}) => request<LatencyStats>(`/latency${qs(p)}`),
  errors: (p: { from?: string; to?: string } = {}) => request<ErrorStats>(`/errors${qs(p)}`),
  trace: (p: { agentRunId?: string; from?: string; to?: string } = {}) =>
    request<TraceWaterfall>(`/trace${qs(p)}`),
  settings: () => request<CostSettings>('/settings'),

  listPrices: () => request<PriceItem[]>('/prices'),
  createPrice: (body: unknown) => request<PriceItem>('/prices', { method: 'POST', body: JSON.stringify(body) }),
  updatePrice: (body: unknown) => request<PriceItem>('/prices', { method: 'PUT', body: JSON.stringify(body) }),
  deletePrice: (model: string) => request<void>(`/prices/${encodeURIComponent(model)}`, { method: 'DELETE' }),

  listBudgets: () => request<BudgetItem[]>('/budgets'),
  createBudget: (body: unknown) => request<BudgetItem>('/budgets', { method: 'POST', body: JSON.stringify(body) }),
  updateBudget: (body: unknown) => request<BudgetItem>('/budgets', { method: 'PUT', body: JSON.stringify(body) }),
  deleteBudget: (name: string) => request<void>(`/budgets/${encodeURIComponent(name)}`, { method: 'DELETE' }),
  budgetAchievement: (name?: string) =>
    request<unknown[]>('/budgets/achievement' + qs({ name })),

  rebuildSummary: () => request<{ clearedRows: number }>('/summary/rebuild', { method: 'POST' }),
}
