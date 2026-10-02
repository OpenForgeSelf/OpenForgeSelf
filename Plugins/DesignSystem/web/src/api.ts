/**
 * 设计系统后端契约的 TS 镜像（与 `Plugins/DesignSystem/Services/DesignMapper.cs` 的 DTO 一一对应）。
 *
 * 为什么手写而不是 codegen：本插件不引入 OpenAPI 工具链（新增依赖要升级给人），
 * 但**形状漂移必须当场可见**——所以类型集中在这一个文件，`api.ts` 之外不允许出现裸 fetch 路径；
 * 后端加字段时，改这里 + 改 DesignMapper，两处对齐就是评审点。
 */
import { get, post, put, withQuery, getExportText, type ApiError } from './http'

const BASE = '/api/design-system'

/* ------------------------------------------------------------------ */
/* 类型（camelCase = ASP.NET Core 默认序列化）                          */
/* ------------------------------------------------------------------ */

/** 一条变体轴与其档位序（后端 `VariantAxes` 的唯一真源；顺序 = 表的声明序） */
export interface AxisVocabulary {
  axis: string
  values: string[]
}

export interface MetaInfo {
  pluginId: string
  modelVersion: string
  generatorVersion: string
  projectionVersion: string
  tiers: string[]
  tokenTypes: string[]
  lifecycles: string[]
  capabilities: string[]
  exportFormats: string[]
  /** 变体轴清单（size/role/state/…）：界面排状态、做"先选轴再选值"的表单都读它，不许另列词表 */
  variantAxes: AxisVocabulary[]
  /** 尺度档位序：`space` / `radius` / `duration` → 档名序（后端 `ScaleGenerators` 的三张表，前端不许另列） */
  scaleOrders: Record<string, string[]>
  /** primitive 色族序（后端 `ColorFamilies.All`，与生成器逐族产阶同一张表） */
  colorFamilies: string[]
  /** 审计类别展示序（后端 `AuditKinds.All`）：审计板的筛选下拉按它排，前端不许另列 */
  auditKinds: string[]
  /** 十类逻辑实体名（后端 `ExportService.StardustEntities`）：导出台按它列出真实 url，前端不许另列 */
  entities: string[]
  /** 导入格式面（后端 `ImportFormats.All`）：不在清单里的格式，界面不许摆出"能导入"的样子 */
  importFormats: string[]
  /** 单次导入上限（后端 `ImportLimits`）：提示文案读它，前端不抄数字 */
  importLimits: { maxEntries: number; maxBytes: number }
}

/** 导入统计（后端 `ImportCounts`）：每一类都计数，被拒/冲突不许悄悄消失 */
export interface ImportCountsView {
  entries: number
  aliases: number
  literals: number
  composites: number
  rejected: number
  conflicts: number
  duplicates: number
}

/** 导入预览（后端 `DtcgImporter.Parse` 的产物）。差异由后端算，前端只渲染。 */
export interface ImportPlanView {
  format: string
  documentProject: string | null
  counts: ImportCountsView
  willWrite: { path: string; tier: string; type: string; themeId: number; alias: string | null; value: string | null }[]
  rejected: { path: string; reason: string }[]
  conflicts: string[]
}

/** 导入落库结果（整批事务：diagnostics 非空 = 一行都没写） */
export interface ImportResultView {
  created: number
  updated: number
  skippedProtected: number
  conflicts: string[]
  diagnostics: { path: string; status: string; message: string }[]
  rejected: { path: string; reason: string }[]
  counts: ImportCountsView
  auditHint: string
}

/** 逻辑实体明细（裸 JSON，形状与参考物查看器一致） */
export interface EntityView<T = unknown> {
  entity: string
  source: string
  generated: string
  total: number
  data: T[]
}

export interface Project {
  id: number
  code: string
  name: string
  description?: string | null
  kind: string
  version: string
  status: string
  parentProjectId: number
  defaultThemeId: number
  seedText?: string | null
  seedJson?: string | null
  generator?: string | null
  generatorVersion?: string | null
  schemaVersion?: string | null
  projectionVersion?: string | null
  tokenCount: number
  componentCount: number
  publishedAt?: string | null
  createdAt: string
  updatedAt: string
}

export interface Theme {
  id: number
  projectId: number
  code: string
  name?: string | null
  modeKind: string
  isDefault: boolean
  baseThemeId: number
  sortOrder: number
  updatedAt: string
}

export interface Token {
  id: number
  projectId: number
  themeId: number
  tier: string
  path: string
  name?: string | null
  type: string
  value?: string | null
  valueJson?: string | null
  aliasPath?: string | null
  group?: string | null
  description?: string | null
  colorHex?: string | null
  oklchL: number
  oklchC: number
  oklchH: number
  alpha: number
  colorSpace?: string | null
  isPrimary: boolean
  contrastOn?: string | null
  contrastRatio: number
  wcagLevel?: string | null
  generator?: string | null
  generatorSeed?: string | null
  generatorVersion?: string | null
  lifecycle?: string | null
  replacedBy?: string | null
  deprecated: boolean
  tags?: string | null
  sortOrder: number
  updatedAt: string
}

/** 解析别名后的有效值视图（界面显示的一律用它，不用原始行） */
export interface EffectiveToken {
  path: string
  tier: string
  type: string
  value: string
  sourcePath: string
  aliasPath?: string | null
  resolved: boolean
  error?: string | null
  colorHex?: string | null
  contrastRatio: number
  wcagLevel?: string | null
  description?: string | null
  /** 复合令牌（shadow/typography/transition）的真源 JSON；界面只展示，不自行换算 */
  valueJson?: string | null
  extensions?: string | null
}

export interface EffectiveView {
  theme?: string | null
  themeId: number
  count: number
  items: EffectiveToken[]
  diagnostics: TokenDiagnostic[]
}

export interface TokenDiagnostic {
  path: string
  status: string
  message: string
}

/** 令牌写入补丁（部分更新：只覆盖传了的字段） */
export interface TokenPatch {
  path: string
  themeId?: number
  tier?: string
  name?: string
  type?: string
  value?: string
  valueJson?: string
  aliasPath?: string
  group?: string
  description?: string
  colorSpace?: string
  alpha?: number
  isPrimary?: boolean
  tags?: string
  lifecycle?: string
  replacedBy?: string
  deprecated?: boolean
  sortOrder?: number
  extensions?: string
  generator?: string
  generatorSeed?: string
  /** 乐观并发：带上读到的 updatedAt，不匹配即冲突（人工修改保护之后的第二道闸） */
  expectUpdatedAt?: string
}

export interface UpsertResult {
  created: number
  updated: number
  skippedProtected?: number
  conflicts?: string[]
}

export interface TokenPage {
  items: Token[]
  total: number
  page: number
  pageSize: number
}

export interface Component {
  id: number
  projectId: number
  code: string
  name?: string | null
  category?: string | null
  interactive: boolean
  description?: string | null
  docJson?: string | null
  guidanceJson?: string | null
  a11yNotes?: string | null
  tokenRefsJson?: string | null
  status?: string | null
  version?: string | null
  sortOrder: number
  updatedAt: string
}

export interface Variant {
  id: number
  componentId: number
  componentCode: string
  code: string
  name?: string | null
  variantJson: string
  state: string
  themeId: number
  tokenRefsJson?: string | null
  cssSnippet?: string | null
  contrastSummary?: string | null
  sortOrder: number
}

export interface Icon {
  id: number
  projectId: number
  code: string
  name?: string | null
  collection: string
  svgBody?: string | null
  strokeWidth: number
  gridPx: number
  viewBox?: string | null
  sizes?: string | null
  tags?: string | null
  usage?: string | null
  license?: string | null
}

export interface Asset {
  id: number
  projectId: number
  code: string
  name?: string | null
  kind: string
  svgBody?: string | null
  fileRef?: string | null
  tokenRefsJson?: string | null
  description?: string | null
  license?: string | null
  sortOrder: number
}

export interface Screen {
  id: number
  projectId: number
  code: string
  title?: string | null
  iconCode?: string | null
  route?: string | null
  themeId: number
  componentIdsJson?: string | null
  description?: string | null
  notes?: string | null
  sortOrder: number
}

export interface FontFace {
  id: number
  projectId: number
  family: string
  weight: number
  style: string
  fileName?: string | null
  fileRef?: string | null
  display?: string | null
  role?: string | null
  sourceUrl?: string | null
  license?: string | null
}

export interface AuditItem {
  id: number
  kind: string
  rule?: string | null
  severity: string
  targetType: string
  targetPath: string
  pairedPath?: string | null
  expected?: string | null
  actual?: string | null
  ratio: number
  passed: boolean
  suggestion?: string | null
  message?: string | null
  checkedAt: string
}

export interface AuditSummary {
  total: number
  passed: number
  critical: number
  warning: number
  info: number
  blocking: boolean
}

export interface AuditView {
  items: AuditItem[]
  summary: AuditSummary
  blocking: boolean
}

export interface Release {
  id: number
  projectId: number
  version: string
  status: string
  tokensHash: string
  tokenCount: number
  auditSummary?: string | null
  auditPassed: boolean
  sourceReleaseId: number
  releaseNotes?: string | null
  snapshotAvailable: boolean
  createdAt: string
}

export interface ReleaseToken {
  theme: string
  path: string
  tier: string
  type: string
  value: string
  aliasPath?: string | null
  hex?: string | null
  group?: string | null
}

export interface TokenChange {
  theme: string
  path: string
  field: string
  from?: string | null
  to?: string | null
}

/** 快照里的一条非令牌规格（组件 / 变体格子 / 资产 / 起手屏 / 字体登记） */
export interface ReleaseSpec {
  kind: string
  key: string
  fields: Record<string, string | null>
}

/** 规格字段级变更（field 为 null 表示整条新增/删除） */
export interface SpecChange {
  kind: string
  key: string
  field: string | null
  from?: string | null
  to?: string | null
}

export interface ReleaseDiff {
  from: string
  to: string
  added: ReleaseToken[]
  removed: ReleaseToken[]
  changed: TokenChange[]
  missingThemes: string[]
  specsAdded: ReleaseSpec[]
  specsRemoved: ReleaseSpec[]
  specsChanged: SpecChange[]
  /** false = 有一边是 schema 1 的旧快照（没有规格节）：界面必须显示"不可比"而不是"新增 N 条" */
  specsComparable: boolean
  total: number
  isEmpty: boolean
}

export interface GenerateRequest {
  brief?: string | null
  seedColor?: string | null
  hue?: number | null
  accentHueOffset?: number
  chroma?: number | null
  density?: string | null
  typeRatio?: number | null
  typeBasePx?: number | null
  radiusBase?: number | null
  motionScale?: number | null
  brandName?: string | null
  themes?: string[]
  industry?: string | null
}

export interface GenerateResult {
  seed: string
  industry: string
  hue: number
  tokens: number
  /**
   * 本次生成后库里的目录条数（组件规格 / 变体矩阵格子 / 字体 / 页面清单 / 资产）。
   * 由后端种子流程返回，前端不自算；品牌三表按自然键**只补空不覆盖**，所以这些数是"现存"而不是"新增"。
   */
  components?: number
  variants?: number
  fonts?: number
  screens?: number
  assets?: number
  themes: string[]
  notes: string[]
  /** 人工修改保护跳过的行数：>0 时必须显示，否则"生成成功但库里没变"就是一次静默失败 */
  skippedProtected?: number
  conflicts?: string[]
  audit: AuditSummary
}

export interface GeneratePreview {
  seed: string
  industry: string
  hue: number
  notes: string[]
  shared: number
  themes: Record<string, number>
  sample: { path: string; value?: string | null }[]
}

export interface ExportFormats {
  projectionVersion: string
  formats: { name: string; bundle: boolean }[]
}

/* ------------------------------------------------------------------ */
/* M1/M2 向导·展厅·交付契约（03-plan 步骤5；路径只在 api.ts 出现）       */
/* ------------------------------------------------------------------ */

/**
 * 内置风格预设（`GET presets`，REST 只回目录字段）。
 * 预设的 `request` 走 `presets/recommend` 的 `PresetMatch.request` 拿，这里不重复承载。
 */
export interface StylePreset {
  id: string
  name: string
  tagline: string
  tones: string[]
  kinds: string[]
  industries: string[]
  keywords: string[]
}

/** 预设推荐命中（`POST presets/recommend`：`request` 为后端深拷贝，前端可安全改写后交回） */
export interface PresetMatch {
  id: string
  name: string
  tagline: string
  score: number
  reasons: string[]
  request: GenerateRequest
}

/** `POST projects/quick-create` 入参（dryRun=false 落库；与工具 design_create 的 apply 方向相反） */
export interface QuickCreateInput {
  name: string
  code?: string | null
  kind?: string | null
  description?: string | null
  preset?: string | null
  request?: GenerateRequest | null
  dryRun?: boolean
}

/**
 * `POST projects/quick-create` 出参。后端 `applied` 直接透传 `AppliedResult`
 * （Project/Generation/Audit 全量），前端只消费 `project`/`warnings`；`code` 用于创建后回读核对。
 */
export interface QuickCreateResult {
  uiRoute: string
  dryRun: boolean
  project: { code: string; name: string; status: string } | null
  warnings: string[] | null
}

/** `POST generate/preview-css` 入参（GenerationRequest 平铺镜像 + 单个 theme；`themes` 恒被后端忽略） */
export interface PreviewCssInput {
  brief?: string | null
  seedColor?: string | null
  hue?: number | null
  chroma?: number | null
  density?: string | null
  typeRatio?: number | null
  typeBasePx?: number | null
  radiusBase?: number | null
  motionScale?: number | null
  brandName?: string | null
  industry?: string | null
  accentHueOffset?: number | null
  theme?: string | null
}

/** `POST generate/preview-css` 出参（css 与落库导出同源：去注释后逐字相同；theme 缺省回落 light） */
export interface PreviewCssResult {
  theme: string
  css: string
  seed: string
  industry: string
  hue: number
  notes: string[]
}

/** `GET/PUT agent-access`（设计_* 工具的写动作门禁；令牌永不回显） */
export interface AgentAccess {
  allowWrite: boolean
  source: string
  corrupt: boolean
  updatedAt: string
}

/** `GET agent/tools`（与 meta.agentTools / design_guide 同源，供外部枚举） */
export interface AgentTool {
  name: string
  summary: string
  readOnly: boolean
  parameters: Record<string, unknown>
}

/** `GET api/mcp-center/config`（MCP 中心网关视图；token 只给掩码，明文绝不进前端） */
export interface McpConfig {
  port: number
  listenHost: string
  listenUrl: string
  hasToken: boolean
  tokenMasked: string
  isRunning: boolean
  version: string
}

/** `POST projects/{id}/review` 入参（files 与 code 至少其一；Code 缺省按 css 审查） */
export interface ReviewInput {
  files?: { path?: string | null; content?: string | null; language?: string | null }[]
  code?: string | null
  theme?: string | null
  strict?: boolean
  maxFindings?: number
}

/** `POST projects/{id}/review` 出参（summary/findings 均为后端原文，前端只渲染不改写） */
export interface ReviewResult {
  error: string
  summary: string
  findings: string[]
  skipped: number
  truncated: boolean
  notes: string
  theme: string
  themeNote: string
}

/* ------------------------------------------------------------------ */
/* 端点                                                                */
/* ------------------------------------------------------------------ */

export const api = {
  meta: () => get<MetaInfo>(`${BASE}/meta`),

  listProjects: (status?: string, keyword?: string) => get<Project[]>(withQuery(`${BASE}/projects`, { status, keyword })),
  createProject: (input: { code: string; name: string; description?: string; kind?: string; seedText?: string }) =>
    post<Project>(`${BASE}/projects`, input),
  getProject: (id: number) => get<Project>(`${BASE}/projects/${id}`),
  updateProject: (id: number, patch: Record<string, unknown>) => put<Project>(`${BASE}/projects/${id}`, patch),
  archiveProject: (id: number) => post<Project>(`${BASE}/projects/${id}/archive`, {}),

  listThemes: (id: number) => get<Theme[]>(`${BASE}/projects/${id}/themes`),
  addTheme: (id: number, input: { code: string; name?: string; modeKind?: string; isDefault?: boolean }) =>
    post<Theme>(`${BASE}/projects/${id}/themes`, input),

  listTokens: (id: number, q: { themeId?: number; tier?: string; group?: string; q?: string; page?: number; pageSize?: number } = {}) =>
    get<TokenPage>(withQuery(`${BASE}/projects/${id}/tokens`, q)),
  effective: (id: number, theme?: string) => get<EffectiveView>(withQuery(`${BASE}/projects/${id}/tokens/effective`, { theme })),
  upsertToken: (id: number, patch: TokenPatch) => post<UpsertResult>(`${BASE}/projects/${id}/tokens`, patch),
  upsertTokens: (id: number, items: TokenPatch[], overwrite = false) =>
    post<UpsertResult>(`${BASE}/projects/${id}/tokens/batch`, { items, overwrite }),
  retireToken: (id: number, themeId: number, path: string, replacedBy?: string) =>
    post<{ retired: string }>(withQuery(`${BASE}/projects/${id}/tokens/retire`, { themeId, path, replacedBy }), {}),

  generate: (id: number, req: GenerateRequest, overwrite = false) =>
    post<GenerateResult>(withQuery(`${BASE}/projects/${id}/generate`, { overwrite }), req),
  generatePreview: (req: GenerateRequest) => post<GeneratePreview>(`${BASE}/generate/preview`, req),

  runAudit: (id: number, releaseId = 0) => post<AuditSummary>(withQuery(`${BASE}/projects/${id}/audit`, { releaseId }), {}),
  audit: (id: number, q: { kind?: string; passed?: boolean; releaseId?: number } = {}) =>
    get<AuditView>(withQuery(`${BASE}/projects/${id}/audit`, q)),

  exportText: (id: number, format: string, theme?: string) =>
    getExportText(withQuery(`${BASE}/projects/${id}/export`, { format, theme })),
  exportUrl: (id: number, format: string, theme?: string) => withQuery(`${BASE}/projects/${id}/export`, { format, theme }),
  exportFormats: (id: number) => get<ExportFormats>(`${BASE}/projects/${id}/export/formats`),

  /**
   * 导入预览：把整份 DTCG 文档原样 POST 上去，拿回"将写入 / 冲突 / 被拒"。
   * 差异**不在前端算** —— 前端自己算一遍就等于给同一件事写第二份真相，且必然与后端漂移。
   */
  importPreview: (id: number, doc: unknown, q: { format?: string; theme?: string; overwrite?: boolean } = {}) =>
    post<ImportPlanView>(withQuery(`${BASE}/projects/${id}/import/preview`, { ...q }), doc),
  /** 导入落库（整批事务：失败一行都不写）。成功后界面必须提示"去重跑审计"，本端点不代跑。 */
  import: (id: number, doc: unknown, q: { format?: string; theme?: string; overwrite?: boolean } = {}) =>
    post<ImportResultView>(withQuery(`${BASE}/projects/${id}/import`, { ...q }), doc),

  /** 十类逻辑实体（名字来自 `meta.entities`）：参考物查看器直连的裸 JSON，不走 success/data 信封 */
  entityUrl: (id: number, entity: string, theme?: string) =>
    withQuery(`${BASE}/${id}/${encodeURIComponent(entity)}.json`, { theme }),
  entity: <T = unknown>(id: number, entity: string, theme?: string) =>
    get<EntityView<T>>(withQuery(`${BASE}/${id}/${encodeURIComponent(entity)}.json`, { theme })),

  listComponents: (id: number, category?: string) => get<Component[]>(withQuery(`${BASE}/projects/${id}/components`, { category })),
  saveComponent: (id: number, input: Record<string, unknown>) => post<Component>(`${BASE}/projects/${id}/components`, input),
  listVariants: (id: number, code: string) => get<Variant[]>(`${BASE}/projects/${id}/components/${encodeURIComponent(code)}/variants`),
  saveVariant: (id: number, code: string, input: Record<string, unknown>) =>
    post<Variant>(`${BASE}/projects/${id}/components/${encodeURIComponent(code)}/variants`, input),

  listIcons: (projectId: number, collection?: string, q?: string) =>
    get<Icon[]>(withQuery(`${BASE}/icons`, { projectId, collection, q })),
  saveIcon: (id: number, input: Record<string, unknown>) => post<Icon>(`${BASE}/projects/${id}/icons`, input),
  listAssets: (id: number, kind?: string) => get<Asset[]>(withQuery(`${BASE}/projects/${id}/assets`, { kind })),
  listScreens: (id: number) => get<Screen[]>(`${BASE}/projects/${id}/screens`),
  listFonts: (id: number) => get<FontFace[]>(`${BASE}/projects/${id}/fonts`),
  /** 三张表的写入口（同码 = 覆盖，不是堆行）；后端 upsert，界面必须能登记而不是只能看空表 */
  saveAsset: (id: number, body: { code: string; name?: string | null; kind?: string | null; svgBody?: string | null; fileRef?: string | null; tokenRefsJson?: string | null; description?: string | null; license?: string | null }) =>
    post<Asset>(`${BASE}/projects/${id}/assets`, body),
  saveScreen: (id: number, body: { code: string; title?: string | null; iconCode?: string | null; route?: string | null; componentIdsJson?: string | null; description?: string | null; notes?: string | null; themeId?: number; sortOrder?: number }) =>
    post<Screen>(`${BASE}/projects/${id}/screens`, body),
  saveFont: (id: number, body: { family: string; weight?: number; style?: string; fileName?: string | null; fileRef?: string | null; display?: string | null; role?: string | null; sourceUrl?: string | null; license?: string | null }) =>
    post<FontFace>(`${BASE}/projects/${id}/fonts`, body),

  listReleases: (id: number) => get<Release[]>(`${BASE}/projects/${id}/releases`),
  createRelease: (id: number, version: string, notes?: string, sourceReleaseId = 0) =>
    post<Release>(`${BASE}/projects/${id}/releases`, { version, notes, sourceReleaseId }),
  getRelease: (id: number, releaseId: number) => get<Release>(`${BASE}/projects/${id}/releases/${releaseId}`),
  releaseTokens: (id: number, releaseId: number) =>
    get<{ version: string; generatedAt: string; tokens: ReleaseToken[] }>(`${BASE}/projects/${id}/releases/${releaseId}`),
  releaseDtcg: (id: number, releaseId: number, theme: string) =>
    get<{ version: string; theme: string; content: string }>(
      withQuery(`${BASE}/projects/${id}/releases/${releaseId}`, { format: 'dtcg', theme }),
    ),
  diffReleases: (id: number, from: number, to: number) =>
    get<ReleaseDiff>(withQuery(`${BASE}/projects/${id}/releases/diff`, { from, to })),

  /* ---- M1/M2：向导 · 展厅 · 交付（03-plan 步骤5 新增） ---- */

  listPresets: () => get<StylePreset[]>(`${BASE}/presets`),
  recommendPresets: (input: { brief?: string | null; kind?: string | null; industry?: string | null; tone?: string | null; density?: string | null; brandColor?: string | null; limit?: number }) =>
    post<PresetMatch[]>(`${BASE}/presets/recommend`, input),
  quickCreate: (input: QuickCreateInput) => post<QuickCreateResult>(`${BASE}/projects/quick-create`, input),
  previewCss: (input: PreviewCssInput) => post<PreviewCssResult>(`${BASE}/generate/preview-css`, input),
  getAgentAccess: () => get<AgentAccess>(`${BASE}/agent-access`),
  putAgentAccess: (enabled: boolean) => put<AgentAccess>(`${BASE}/agent-access`, { enabled }),
  listAgentTools: () => get<AgentTool[]>(`${BASE}/agent/tools`),
  reviewCode: (id: number, input: ReviewInput) => post<ReviewResult>(`${BASE}/projects/${id}/review`, input),
  /** MCP 中心网关配置（跨插件只读消费，路径带完整前缀） */
  getMcpConfig: () => get<McpConfig>('/api/mcp-center/config'),
}

export type { ApiError }
