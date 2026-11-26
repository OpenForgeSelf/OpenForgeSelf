/**
 * sems 工作台 · 状态与数据层
 *
 * 设计约束（承接交接包 replica/architecture.md §「data 层」）：
 * - 页面只调用本文件导出的函数，不直接改数组、不直接 fetch。
 * - 真实数据源是 sems 后端（GET/POST/PUT/DELETE /api/projects、/api/commands、/api/runs、/api/specs）。
 * - 后端不可达时在当前页说明原因，不再切到面向使用的 DEMO 模式或示例项目。
 * - 收藏、向导元数据、配置开关、当前项目等纯 UI 状态落 localStorage（键 sems-workbench-v1），
 *   与后端 ProjectInfo DTO 解耦，不改动任何 C# 契约。
 *
 * 与交接包的唯一有意偏离：插件是宿主远程加载的 Vue 视图，location.hash 归宿主所有，
 * 因此默认用内存路由；加 ?semsHash=1（或预览页）才启用 #sems/... 命名空间 hash 路由。
 */
import { reactive } from 'vue'
import {
  apiGet, apiPost, apiPut, apiDelete, browseDirectories, fetchPluginVersion,
} from '../http'
import type {
  ProjectInfo, ProjectsResp, RunCommandInfo, DirectoryEntry, BrowseResp, RunsResp, RunLogsResp, RunSession, StopRunResp,
} from '../types'
import { filterEngineeringPlugins, parseEngineeringPluginsPayload, type EngineeringPlugin } from '../engineeringPlugins'
import { listSpecs } from '../specApi'
import { isRemoveBlockedByRunningError } from '../confirmOps'
import { sameProjectRoot } from '../registerOps'

/* ------------------------------------------------------------------ *
 * 类型
 * ------------------------------------------------------------------ */

export type WbRoute =
  | '/projects' | '/create' | '/import' | '/create/wizard'
  | '/dashboard' | '/plugins' | '/runs' | '/specs' | '/settings'
  | '/dependencies' | '/configuration' | '/tasks'

/** 旧路径别名，进入时归并到五栏导航。 */
const ROUTE_ALIASES: Record<string, WbRoute> = {
  '/dependencies': '/runs',
  '/tasks': '/runs',
  '/configuration': '/settings',
}

export function canonicalRoute(path: string): WbRoute {
  return (ROUTE_ALIASES[path] ?? path) as WbRoute
}

/** 2 步登记向导写入档案的字段（AC-D1：只登记目录 + 写回真实后端字段，不生成脚手架）。 */
export interface WbWizardMeta {
  type: string
  tags: string
  description: string
}

export interface WbProject {
  id: number
  name: string
  /** 项目根目录（后端 ProjectInfo.Root，唯一键）。 */
  path: string
  type: string
  description: string
  tags: string
  source: string
  isGitRepo: boolean
  pathExists: boolean
  openDate: string
  /** UI 元数据：收藏（localStorage）。 */
  favorite: number
  commands: RunCommandInfo[]
  /** UI 元数据：创建向导的选项快照（localStorage）。 */
  wizardMeta?: WbWizardMeta
}

export interface WbTool {
  name: string
  group: '项目档案' | '运行命令' | '运行会话' | '项目规格'
  desc: string
  official: boolean
}

export interface WbDep {
  id: number
  name: string
  wanted: string
  latest: string
  scope: 'main' | 'dev'
  url?: string | null
}

export interface WbTask {
  id: number
  name: string
  description: string
  status: 'idle' | 'running' | 'done' | 'error'
  script: string
  url?: string | null
}

/* ------------------------------------------------------------------ *
 * 常量：22 个 sems_* 工具（与 SemsPlugin.RegisterToolExtensions 一一对应）
 * 工作台「插件」屏用它们承载原插件的能力清单，不编造新后端能力。
 * ------------------------------------------------------------------ */

export const SEMS_TOOLS: WbTool[] = [
  { name: 'sems_list_projects', group: '项目档案', desc: '列出已登记的项目档案', official: true },
  { name: 'sems_register_project', group: '项目档案', desc: '登记一个已存在的目录为项目', official: true },
  { name: 'sems_update_project', group: '项目档案', desc: '修改项目名称 / 类型 / 描述 / 标签', official: true },
  { name: 'sems_remove_project', group: '项目档案', desc: '移除项目档案（不动磁盘文件）', official: true },
  { name: 'sems_list_commands', group: '运行命令', desc: '列出项目的运行命令', official: true },
  { name: 'sems_add_command', group: '运行命令', desc: '新增运行命令', official: true },
  { name: 'sems_update_command', group: '运行命令', desc: '编辑运行命令', official: true },
  { name: 'sems_delete_command', group: '运行命令', desc: '删除运行命令', official: true },
  { name: 'sems_list_runs', group: '运行会话', desc: '列出运行中的会话', official: true },
  { name: 'sems_check_runs', group: '运行会话', desc: '检测本机属于项目的进程', official: true },
  { name: 'sems_run_command', group: '运行会话', desc: '在项目根目录启动命令', official: true },
  { name: 'sems_stop_command', group: '运行会话', desc: '停止本面板启动的会话', official: true },
  { name: 'sems_stop_run', group: '运行会话', desc: '按 PID 停止会话', official: true },
  { name: 'sems_create_spec', group: '项目规格', desc: '新建规格（v1）', official: true },
  { name: 'sems_add_spec_version', group: '项目规格', desc: '只追加规格新版本', official: true },
  { name: 'sems_get_spec', group: '项目规格', desc: '读取规格版本详情', official: true },
  { name: 'sems_list_specs', group: '项目规格', desc: '列出项目的规格', official: true },
  { name: 'sems_link_spec', group: '项目规格', desc: '登记 commit / PR / Issue 关联', official: true },
  { name: 'sems_list_spec_links', group: '项目规格', desc: '列出规格关联', official: true },
  { name: 'sems_find_spec_by_ref', group: '项目规格', desc: '按标识反查规格版本', official: true },
  { name: 'sems_link_spec_case', group: '项目规格', desc: '登记验收标准用例标识', official: true },
  { name: 'sems_get_spec_coverage', group: '项目规格', desc: '查看验收标准用例覆盖', official: true },
]

/* AC-D1：包管理器 / Git / 预设 / 功能勾选等假区域已随 4 步向导一并删除，
   不再导出对应常量；档案字段（type/tags/description）由 2 步向导直接写回后端。 */

/* ------------------------------------------------------------------ *
 * 内置示例数据（后端不可达时的离线回落，字段与 WbProject 一致）
 * ------------------------------------------------------------------ */

function demoCommands(seed: number, root: string): RunCommandInfo[] {
  const base = [
    { name: 'serve', script: 'npm run serve', url: 'http://localhost:8080' },
    { name: 'build', script: 'npm run build', url: null },
    { name: 'lint', script: 'npm run lint', url: null },
    { name: 'inspect', script: 'npm run inspect', url: null },
  ]
  return base.map((c, i) => ({
    id: seed + i,
    projectId: seed,
    name: c.name,
    script: c.script,
    url: c.url,
    sort: i,
    createdAt: '2026-11-08 10:00',
    updatedAt: '2026-11-08 10:00',
  }))
}

function demoProjects(): WbProject[] {
  return [
    {
      id: 9001, name: 'aurora-admin', path: '/workspace/aurora-admin', type: 'frontend',
      description: '示例：管理后台', tags: 'vue,typescript', source: 'demo', isGitRepo: true, pathExists: true,
      openDate: '2026-11-08 10:12', favorite: 1, commands: demoCommands(9001, '/workspace/aurora-admin'),
    },
    {
      id: 9002, name: 'stardust-console', path: '/workspace/stardust-console', type: 'frontend',
      description: '示例：控制台', tags: 'vue', source: 'demo', isGitRepo: false, pathExists: true,
      openDate: '2026-11-07 18:40', favorite: 0, commands: demoCommands(9011, '/workspace/stardust-console'),
    },
    {
      id: 9003, name: 'pulse-tracker', path: '/workspace/pulse-tracker', type: 'tool',
      description: '示例：指标采集工具', tags: 'node', source: 'demo', isGitRepo: true, pathExists: false,
      openDate: '2026-11-06 09:05', favorite: 0, commands: demoCommands(9021, '/workspace/pulse-tracker'),
    },
  ]
}

/* ------------------------------------------------------------------ *
 * 状态
 * ------------------------------------------------------------------ */

const META_KEY = 'sems-workbench-v1'
interface MetaShape {
  favorites: Record<number, number>
  wizardMeta: Record<number, WbWizardMeta>
  currentProjectId: number | null
  viewMode: 'workbench' | 'legacy'
  configs: Record<number, Record<string, boolean | string>>
  /** 项目首页展示模式：列表 / 卡片（v0.2 口径：首页直接改，不做模块注册）。 */
  projectLayout: 'list' | 'card'
}

function readMeta(): MetaShape {
  const empty: MetaShape = {
    favorites: {}, wizardMeta: {}, currentProjectId: null, viewMode: 'workbench', configs: {},
    projectLayout: 'list',
  }
  try {
    const raw = localStorage.getItem(META_KEY)
    if (!raw) return empty
    const o = JSON.parse(raw) as Partial<MetaShape>
    return { ...empty, ...o }
  } catch {
    return empty
  }
}

function writeMeta() {
  try {
    localStorage.setItem(META_KEY, JSON.stringify({
      favorites: meta.favorites,
      wizardMeta: meta.wizardMeta,
      currentProjectId: store.currentProjectId,
      viewMode: meta.viewMode,
      configs: meta.configs,
      projectLayout: meta.projectLayout,
    }))
  } catch { /* 隐私模式下静默降级 */ }
}

/** 配置用 reactive 包装：S09 显示偏好开关切换后，computed 与状态栏立刻反显（修 D-2）。 */
const meta = reactive(readMeta())

/* ------------------------------------------------------------------ *
 * 任务屏支撑（v0.2 口径：以 HTML 原型为准，命令即任务）
 *   - 日志按 commandId 分桶：切任务不丢、实时追加
 *   - 状态机 idle -> running -> done
 *   - 本地运行历史兜底：后端会话消失后记录不跟着消失
 * ------------------------------------------------------------------ */

type TaskStatus = 'idle' | 'running' | 'done'

interface RunHistoryEntry {
  name: string
  script: string
  status: TaskStatus
  startedAt: string
  endedAt?: string
}

const RUN_HISTORY_KEY = 'sems-run-history-v1'
const RUN_LOGS_KEY = 'sems-run-logs-v1'
/** 本地日志留存上限（每命令），避免 localStorage 无限增长。 */
const MAX_LOCAL_LOG_LINES = 2000
const logCursor: Record<number, number> = {}
/**
 * 日志不可用集合：冷态（命令从未在本页启动、或宿主已重启）时后端 /logs 返回设计内 404。
 * 记下后不再对该命令轮询，直到显式启动才解除，避免反复 404 刷控制台（修 B-2）。
 */
const logUnavailable = new Set<number>()

/**
 * 已收敛命令的「尾部补拉」登记（输入4 2026-11-23，PC-024 第4发逃逸面）：
 * 任务进终态后日志缓冲已定，但停止/退出的后端留痕行（如「进程已退出（PID …）」）
 * 可能晚于收敛落盘——允许恰补拉一针，拉完即清；终态任务由此彻底退出常驻轮询面。
 */
const tailPull = new Set<number>()
export function isLogUnavailable(commandId: number): boolean { return logUnavailable.has(commandId) }
function isNotFound(e: unknown): boolean {
  return /\(404\)|\b404\b/.test(e instanceof Error ? e.message : String(e))
}

function readRunHistory(): Record<number, RunHistoryEntry> {
  try {
    const raw = localStorage.getItem(RUN_HISTORY_KEY)
    return raw ? (JSON.parse(raw) as Record<number, RunHistoryEntry>) : {}
  } catch {
    return {}
  }
}

/** 读取本地留存的日志分桶（页面刷新后仍能回看上一次运行的输出）。 */
function readRunLogs(): Record<number, string[]> {
  try {
    const raw = localStorage.getItem(RUN_LOGS_KEY)
    return raw ? (JSON.parse(raw) as Record<number, string[]>) : {}
  } catch {
    return {}
  }
}

function writeRunLogs(logs: Record<number, string[]>): void {
  try {
    const trimmed: Record<number, string[]> = {}
    for (const [k, v] of Object.entries(logs)) {
      trimmed[k] = v.length > MAX_LOCAL_LOG_LINES ? v.slice(-MAX_LOCAL_LOG_LINES) : v
    }
    localStorage.setItem(RUN_LOGS_KEY, JSON.stringify(trimmed))
  } catch {
    /* 存储不可用时忽略，不影响主流程 */
  }
}

/** 持久化运行历史（导出供测试与外部显式落盘调用）。 */
export function saveRunHistory(h: Record<number, RunHistoryEntry>): void {
  writeRunHistory(h)
}

function writeRunHistory(h: Record<number, RunHistoryEntry>): void {
  try {
    localStorage.setItem(RUN_HISTORY_KEY, JSON.stringify(h))
  } catch {
    /* 存储不可用时忽略，不影响主流程 */
  }
}

export const store = reactive({
  projects: [] as WbProject[],
  currentProjectId: meta.currentProjectId as number | null,
  mode: 'live' as 'live' | 'demo',
  version: '',
  route: '/projects' as WbRoute,
  loading: true,
  error: '',
  filter: '',
  /** 项目类型筛选（'' = 全部）。 */
  typeFilter: '',
  /** 项目排序维度：recent / name / commands。 */
  projectSort: 'recent' as 'recent' | 'name' | 'commands',
  pluginFilter: '',
  depFilter: '',
  taskFilter: '',
  engineeringPlugins: [] as EngineeringPlugin[],
  engineeringPluginsError: '',
  engineeringPluginsLoading: false,
  specCount: 0,
  renameId: null as number | null,
  moreOpen: false,
  selectedTask: '' as string,
  selectedTaskId: null as number | null,
  logs: [] as string[],
  /** 任务日志分桶：commandId -> 行。切换任务不丢，实时追加。 */
  logsByCommand: readRunLogs() as Record<number, string[]>,
  /** 任务状态机：commandId -> idle / running / done。 */
  taskStatus: {} as Record<number, TaskStatus>,
  /** 本地运行历史：后端不再返回会话时兜底，运行记录不突然消失。 */
  runHistory: readRunHistory() as Record<number, RunHistoryEntry>,
  runningCommandId: null as number | null,
  runningCount: 0,
  runningSessions: [] as RunSession[],
  toasts: [] as { id: number; text: string }[],
  folder: {
    cwd: '' as string,
    parent: null as string | null,
    directories: [] as DirectoryEntry[],
    crumbs: [] as string[],
    loading: false,
    error: '',
  },
  wizard: {
    /** 2 步：0=确认目录，1=填写档案并登记。 */
    step: 0,
    name: '',
    root: '',
    type: '',
    tags: '',
    description: '',
  },
})

export const viewMode = reactive({ value: meta.viewMode })
export function setViewMode(v: 'workbench' | 'legacy') {
  viewMode.value = v
  meta.viewMode = v
  writeMeta()
}

/* 项目首页展示模式：列表 / 卡片。偏好落 localStorage。 */
export const projectLayout = reactive({ value: meta.projectLayout })
export function setProjectLayout(v: 'list' | 'card'): void {
  projectLayout.value = v
  meta.projectLayout = v
  writeMeta()
}

/* ------------------------------------------------------------------ *
 * 通用 UI 动作
 * ------------------------------------------------------------------ */

const USE_HASH = typeof location !== 'undefined' && new URLSearchParams(location.search).has('semsHash')
const HASH_PREFIX = '#sems'

export function currentProject(): WbProject | null {
  return store.projects.find((p) => p.id === store.currentProjectId) ?? null
}

export function routeTab(): 'projects' | 'create' | 'import' {
  const r = store.route
  if (r.startsWith('/create')) return 'create'
  if (r.startsWith('/import')) return 'import'
  return 'projects'
}

export function go(path: WbRoute) {
  store.moreOpen = false
  store.route = canonicalRoute(path)
  if (USE_HASH) location.hash = HASH_PREFIX + store.route
}

export function syncFromHash() {
  if (!USE_HASH) return
  const h = location.hash
  if (!h.startsWith(HASH_PREFIX)) return
  const p = h.slice(HASH_PREFIX.length) as WbRoute
  if (p.startsWith('/')) store.route = canonicalRoute(p)
}

export function toast(text: string) {
  const id = Date.now() + Math.random()
  store.toasts.push({ id, text })
  setTimeout(() => {
    const i = store.toasts.findIndex((t) => t.id === id)
    if (i >= 0) store.toasts.splice(i, 1)
  }, 2600)
}

export function isProjectRoute(r: WbRoute = store.route): boolean {
  const path = canonicalRoute(r)
  return ['/dashboard', '/plugins', '/runs', '/specs', '/settings'].includes(path)
}

/* ------------------------------------------------------------------ *
 * 数据层（页面只调这些函数）
 * ------------------------------------------------------------------ */

function fromDto(p: ProjectInfo): WbProject {
  return {
    id: p.id,
    name: p.name,
    path: p.root,
    type: p.type ?? '',
    description: p.description ?? '',
    tags: p.tags ?? '',
    source: p.source ?? '',
    isGitRepo: !!p.isGitRepo,
    pathExists: !!p.pathExists,
    openDate: p.lastActiveAt || p.updatedAt || p.createdAt || '',
    favorite: meta.favorites[p.id] ?? 0,
    commands: (p.commands ?? []).slice().sort((a, b) => (a.sort ?? 0) - (b.sort ?? 0) || a.id - b.id),
    wizardMeta: meta.wizardMeta[p.id],
  }
}

export async function loadProjects(): Promise<void> {
  store.loading = true
  store.error = ''
  try {
    const r = await apiGet<ProjectsResp>('/api/projects')
    if (!r || !Array.isArray(r.projects)) throw new Error('返回体缺少 projects')
    store.projects = r.projects.map(fromDto)
    store.mode = 'live'
    if (store.currentProjectId && !store.projects.some((p) => p.id === store.currentProjectId)) {
      store.currentProjectId = null
    }
  } catch (e) {
    store.projects = []
    store.mode = 'live'
    store.error = e instanceof Error ? e.message : String(e)
    store.currentProjectId = null
    toast('无法加载项目：' + store.error)
  } finally {
    store.loading = false
  }
  try {
    store.version = await fetchPluginVersion('sems')
  } catch {
    store.version = ''
  }
  await refreshRunningCount()
  const current = currentProject()
  if (current) {
    void loadEngineeringPlugins(current.id)
    void loadSpecCount(current.id)
  }
}

export async function loadVersion(): Promise<void> {
  try { store.version = await fetchPluginVersion('sems') } catch { /* 徽标缺失不阻断 */ }
}

export function listProjects(): WbProject[] {
  const q = store.filter.trim().toLowerCase()
  const list = store.projects
    .slice()
    .filter((p) => (p.name + ' ' + p.path + ' ' + p.type + ' ' + p.tags).toLowerCase().includes(q))
  // 类型筛选（HTML 原型没有，放大项目列表屏）
  const filtered = store.typeFilter
    ? list.filter((p) => (p.type || '未分类') === store.typeFilter)
    : list
  // 排序：收藏永远置顶，其余按所选维度
  const cmp: Record<string, (a: WbProject, b: WbProject) => number> = {
    recent: (a, b) => String(b.openDate || '').localeCompare(String(a.openDate || '')),
    name: (a, b) => a.name.localeCompare(b.name, 'zh-CN'),
    commands: (a, b) => (b.commands?.length ?? 0) - (a.commands?.length ?? 0),
  }
  const by = cmp[store.projectSort] ?? cmp.recent
  return filtered.sort((a, b) => b.favorite - a.favorite || by(a, b))
}

/** 全部项目类型（用于筛选条）。 */
export function projectTypes(): string[] {
  const set = new Set<string>()
  for (const p of store.projects) set.add(p.type || '未分类')
  return [...set].sort((a, b) => a.localeCompare(b, 'zh-CN'))
}

/**
 * 项目最近一次任务结果（HTML 原型没有，放大项目列表/卡片）。
 * 数据来自本地运行历史，无需额外请求；无历史时返回 undefined。
 */
export function projectLastRun(p: WbProject | null): { name: string; status: TaskStatus; endedAt?: string } | undefined {
  if (!p) return undefined
  let best: { name: string; status: TaskStatus; endedAt?: string; at: string } | undefined
  for (const c of p.commands) {
    const h = store.runHistory[c.id]
    if (!h) continue
    const at = h.endedAt || h.startedAt
    if (!best || at > best.at) best = { name: h.name, status: h.status, endedAt: h.endedAt, at }
  }
  if (!best) return undefined
  return { name: best.name, status: best.status, endedAt: best.endedAt }
}

export function openProject(id: number, route: WbRoute = '/dashboard') {
  store.currentProjectId = id
  writeMeta()
  go(canonicalRoute(route))
  void loadEngineeringPlugins(id)
  void loadSpecCount(id)
}

export function setFavorite(id: number) {
  const p = store.projects.find((x) => x.id === id)
  if (!p) return
  p.favorite = p.favorite ? 0 : 1
  meta.favorites[id] = p.favorite
  writeMeta()
}

export async function renameProject(id: number, name: string): Promise<boolean> {
  const v = name.trim()
  if (!v) return false
  const p = store.projects.find((x) => x.id === id)
  if (!p) return false
  p.name = v
  if (store.mode === 'live') {
    try {
      await apiPut(`/api/projects/${id}`, { name: v })
    } catch (e) {
      toast('重命名写回失败：' + (e instanceof Error ? e.message : String(e)))
      return false
    }
  }
  return true
}

export type RemoveProjectResult =
  | { ok: true }
  | { ok: false; blockedByRunning: boolean; error: string }

export async function removeProject(id: number): Promise<RemoveProjectResult> {
  try {
    await apiDelete(`/api/projects/${id}`)
  } catch (e) {
    const error = e instanceof Error ? e.message : String(e)
    return { ok: false, blockedByRunning: isRemoveBlockedByRunningError(e), error }
  }
  store.projects = store.projects.filter((p) => p.id !== id)
  if (store.currentProjectId === id) store.currentProjectId = null
  delete meta.favorites[id]
  delete meta.wizardMeta[id]
  writeMeta()
  return { ok: true }
}

export function runningSessionsFor(projectId: number): RunSession[] {
  return store.runningSessions.filter((s) => s.projectId === projectId && !s.ended)
}

export function projectHasRunningSessions(p: WbProject): boolean {
  if (runningSessionsFor(p.id).length) return true
  return (p.commands ?? []).some((c) => taskStatusOf(c.id) === 'running')
}

export function runningSessionCountOf(p: WbProject): number {
  const n = runningSessionsFor(p.id).length
  if (n) return n
  return (p.commands ?? []).filter((c) => taskStatusOf(c.id) === 'running').length
}

export function findProjectByRoot(root: string): WbProject | undefined {
  const path = root.trim()
  if (!path) return undefined
  return store.projects.find((p) => sameProjectRoot(p.path, path))
}

export interface RegisterProjectOutcome {
  project: WbProject
  alreadyExisted: boolean
}

/** 登记目录为项目（真实 POST /api/projects；demo 模式在本地追加）。同一路径已登记时不写回、不新建。 */
export async function registerProject(root: string, name?: string, wizardMeta?: WbWizardMeta): Promise<RegisterProjectOutcome | null> {
  if (!root) return null
  const existed = findProjectByRoot(root)
  if (existed) {
    return { project: existed, alreadyExisted: true }
  }
  if (store.mode === 'live') {
    try {
      const knownIds = new Set(store.projects.map((p) => p.id))
      const resp = await apiPost<{ project: ProjectInfo }>('/api/projects', { root, name: name || undefined })
      if (!resp?.project) throw new Error('后端未返回项目档案')
      const id = resp.project.id
      const alreadyExisted = knownIds.has(id)
      // 2 步向导填的档案字段只在新建时写回，避免再次登记覆盖已有名称与描述
      if (!alreadyExisted) {
        const update: Record<string, string> = {}
        if (wizardMeta?.type?.trim()) update.type = wizardMeta.type.trim()
        if (wizardMeta?.tags?.trim()) update.tags = wizardMeta.tags.trim()
        if (wizardMeta?.description?.trim()) update.description = wizardMeta.description.trim()
        if (Object.keys(update).length) {
          try { await apiPut(`/api/projects/${id}`, update) } catch {
            // BR-1：登记成功但写回失败时可独立重试（S09「编辑项目档案」是现成的重试入口）
            toast('档案字段写回失败，可在「项目设置 → 编辑项目档案」重新填写')
          }
        }
        if (wizardMeta) { meta.wizardMeta[id] = wizardMeta; writeMeta() }
      }
      await loadProjects()
      const project = store.projects.find((p) => p.id === id) ?? fromDto(resp.project)
      return { project, alreadyExisted }
    } catch (e) {
      toast('登记失败：' + (e instanceof Error ? e.message : String(e)))
      return null
    }
  }
  const id = Date.now()
  const p: WbProject = {
    id, name: name || root.split(/[\\/]/).filter(Boolean).pop() || '未命名项目', path: root,
    type: wizardMeta?.type || 'frontend',
    description: wizardMeta?.description ?? '', tags: wizardMeta?.tags ?? '', source: 'demo', isGitRepo: false,
    pathExists: true, openDate: new Date().toLocaleString('zh-CN', { hour12: false }),
    favorite: 0, commands: demoCommands(id, root), wizardMeta,
  }
  if (wizardMeta) { meta.wizardMeta[id] = wizardMeta; writeMeta() }
  store.projects.unshift(p)
  return { project: p, alreadyExisted: false }
}

/** 目录浏览（真实 GET /api/projects/browse）。 */
export async function browse(path?: string): Promise<void> {
  store.folder.loading = true
  store.folder.error = ''
  try {
    const resp = await browseDirectories(path || undefined)
    store.folder.directories = resp.directories ?? []
    store.folder.parent = resp.parent ?? null
    if (resp.path) {
      store.folder.cwd = resp.path
      store.folder.crumbs = resp.path.replace(/\\/g, '/').split('/').filter(Boolean)
    }
  } catch (e) {
    store.folder.error = e instanceof Error ? e.message : String(e)
  } finally {
    store.folder.loading = false
  }
}

/**
 * 以 GET /api/runs 为准同步运行会话。
 * 后端已返回「仍在运行的本页启动会话 + 上次检查中仍存活的检测会话」，
 * 对端没有的会话（含已自行退出的检测进程）一律去掉，不在前端残留。
 */
export function mergeRunningSessions(incoming: RunSession[], _previous: RunSession[]): RunSession[] {
  return incoming.filter((s) => !s.ended)
}

/** 运行中会话数（状态栏与仪表盘 widget）。 */
export async function refreshRunningCount(): Promise<void> {
  if (store.mode !== 'live') {
    store.runningCount = store.runningCommandId ? 1 : 0
    return
  }
  try {
    const r = await apiGet<RunsResp>('/api/runs')
    store.runningSessions = mergeRunningSessions(r?.runs ?? [], store.runningSessions)
    store.runningCount = store.runningSessions.length
  } catch { /* 状态栏统计不阻断主流程 */ }
}

/**
 * 运行页「检查」（F10）：捕获本机属于项目的进程，并入运行会话。
 * 没有已登记命令时也可调用；结果只更新会话，不写入命令档案。
 */
export async function checkRuns(): Promise<void> {
  if (store.mode !== 'live') return
  const r = await apiPost<RunsResp>('/api/runs/check', {})
  store.runningSessions = (r?.runs ?? []).filter((s) => !s.ended)
  store.runningCount = store.runningSessions.length
}

/** 命令行对应的运行中会话（本页启动或检查捕获）；没有则 undefined。 */
export function liveSessionOfCommand(projectId: number, commandId: number): RunSession | undefined {
  return store.runningSessions.find((s) => s.projectId === projectId && s.commandId === commandId && !s.ended)
}

/** 对不上任何已登记命令的运行中进程：单独成行，只能停止，不读日志。 */
export function unmatchedSessionsFor(p: WbProject | null): RunSession[] {
  if (!p) return []
  const ids = new Set(p.commands.map((c) => c.id))
  return runningSessionsFor(p.id).filter((s) => !s.commandId || !ids.has(s.commandId))
}

/** 按本机进程标识停止（检查捕获的进程）；结束后同步运行会话与计数。 */
export async function stopSessionByPid(pid: number): Promise<StopRunResp | undefined> {
  const resp = await apiPost<StopRunResp>(`/api/runs/${pid}/stop`, {})
  if (resp?.outcome !== 'failed') {
    store.runningSessions = store.runningSessions.filter((s) => s.pid !== pid)
    store.runningCount = store.runningSessions.filter((s) => !s.ended).length
  }
  await refreshRunningCount()
  return resp
}

/** 任务日志：按命令取，切换任务不丢。 */
export function taskLogsOf(commandId: number | null): string[] {
  if (commandId == null) return []
  return store.logsByCommand[commandId] ?? []
}

/** 任务状态：本地历史优先，后端会话消失后仍显示已完成。 */
export function taskStatusOf(commandId: number): TaskStatus {
  const live = store.taskStatus[commandId]
  if (live) return live
  // 页面刷新后内存态丢失，从持久化的运行历史恢复（v0.2 缺陷 D2：记录不突然消失）
  const h = store.runHistory[commandId]
  return h ? h.status : 'idle'
}

/** 运行历史条目（含开始/结束时间）。 */
export function taskHistoryOf(commandId: number): RunHistoryEntry | undefined {
  return store.runHistory[commandId]
}

/** 空闲且尚未在本页启动、没有可看会话的命令，不应请求日志。 */
export function hasViewableCommandLogs(commandId: number): boolean {
  const status = taskStatusOf(commandId)
  if (status === 'running' || status === 'done') return true
  if (taskHistoryOf(commandId)) return true
  return store.runningSessions.some((s) => s.commandId === commandId && s.origin === 'Launched' && !s.ended)
}

function bucketOf(commandId: number): string[] {
  return store.logsByCommand[commandId] ?? (store.logsByCommand[commandId] = [])
}

/** 追加日志到分桶并落盘（刷新页面后仍能回看）。 */
function appendLogLines(commandId: number, lines: string[]): void {
  if (!lines.length) return
  const bucket = bucketOf(commandId)
  bucket.push(...lines)
  if (bucket.length > MAX_LOCAL_LOG_LINES * 2) bucket.splice(0, bucket.length - MAX_LOCAL_LOG_LINES)
  writeRunLogs(store.logsByCommand)
}

function markTaskDone(commandId: number): void {
  store.taskStatus[commandId] = 'done'
  const h = store.runHistory[commandId]
  if (h) {
    h.status = 'done'
    if (!h.endedAt) h.endedAt = new Date().toISOString()
    writeRunHistory(store.runHistory)
  }
  if (store.runningCommandId === commandId) store.runningCommandId = null
  // 终态后允许恰一针补拉（抓后端尾部退出留痕），由 ensureTaskPolling 消费后清登记
  tailPull.add(commandId)
  void refreshRunningCount()
}

/** 增量拉取日志并追加到分桶；冷态 404 只提示一次并停止该命令轮询（修 B-2）。 */
export async function pullTaskLogs(commandId: number): Promise<void> {
  if (store.mode === 'demo') return
  const after = logCursor[commandId] ?? 0
  try {
    const r = await apiGet<RunLogsResp>(`/api/commands/${commandId}/logs?after=${after}`)
    logUnavailable.delete(commandId)
    if (r?.lines?.length) {
      logCursor[commandId] = r.nextIndex ?? after
      const lines = r.lines.map((l) => (l.stream === 'stderr' ? '[err] ' + l.text : l.text))
      appendLogLines(commandId, lines)
    }
    if (r && r.running === false && taskStatusOf(commandId) === 'running') markTaskDone(commandId)
  } catch (e) {
    // 启动瞬态：命令刚 POST run、后端日志缓冲区尚未建立时也可能 404，
    // 运行中命令必须继续轮询，否则实时日志会被永久停掉。
    if (isNotFound(e) && taskStatusOf(commandId) === 'running') {
      // PILOT-087（PC-024 lens）：404+运行中不得无限吞错轮询——向 /api/runs 核对一次。
      // 会话确实不在（宿主/插件重启过、缓冲随之消失）→ 收敛为已结束并留痕一次、登记失效终结轮询；
      // 会话仍在（启动瞬态）→ 维持继续轮询语义。核对失败按「仍在」处理，不误收敛。
      const stillLive = await sessionStillLive(commandId).catch(() => true)
      if (!stillLive) {
        appendLogLines(commandId, ['■ 后端已无该会话（进程已退出或宿主/插件重启过，如进程仍在本机运行可用「检查」重新捕获），状态已同步为已结束'])
        markTaskDone(commandId)
        logUnavailable.add(commandId)
        ensureTaskPolling()
      }
      return
    }
    const bucket = bucketOf(commandId)
    const msg = '! 读取日志失败：' + (e instanceof Error ? e.message : String(e))
    if (!bucket.some((l) => l.startsWith('! 读取日志失败'))) {
      bucket.push(msg)
      writeRunLogs(store.logsByCommand)
    }
    if (isNotFound(e)) {
      logUnavailable.add(commandId)
      ensureTaskPolling()
    }
  }
}

/** 核对该命令是否仍在本面板会话表（Launched）。仅用于 404+运行中的收敛判定。 */
async function sessionStillLive(commandId: number): Promise<boolean> {
  const r = await apiGet<RunsResp>('/api/runs')
  return (r?.runs ?? []).some((s) => s.commandId === commandId && s.origin === 'Launched' && !s.ended)
}

let taskPoll: ReturnType<typeof setInterval> | undefined

/** 选中任务是否仍需要进轮询面：失效登记外，仅「非终态」或「待补一针尾部」入选（输入4：终态不得常驻轮询）。 */
function selectedPollable(): boolean {
  const sel = store.selectedTaskId
  if (sel == null || logUnavailable.has(sel)) return false
  if (taskStatusOf(sel) === 'done') return tailPull.has(sel)
  return hasViewableCommandLogs(sel)
}

/** 轮询运行中任务与「日志可用且未终态」的选中任务，保证实时且不刷 404。 */
function ensureTaskPolling(): void {
  const hasRunning = Object.values(store.taskStatus).some((s) => s === 'running')
  const need = hasRunning || selectedPollable()
  if (!need) {
    if (taskPoll) { clearInterval(taskPoll); taskPoll = undefined }
    return
  }
  if (taskPoll) return
  taskPoll = setInterval(async () => {
    const ids = new Set<number>()
    for (const [k, v] of Object.entries(store.taskStatus)) if (v === 'running') ids.add(Number(k))
    if (selectedPollable()) ids.add(store.selectedTaskId!)
    for (const id of ids) {
      await pullTaskLogs(id)
      if (taskStatusOf(id) === 'done') tailPull.delete(id) // 补拉一针即清，终态任务退出轮询面
    }
    if (ids.size === 0 && taskPoll) { clearInterval(taskPoll); taskPoll = undefined }
  }, 1200)
}

/** 选中任务：切换即拉一次日志（不依赖「先点开日志按钮」）。命令不存在时不请求日志。 */
export async function selectTask(commandId: number): Promise<void> {
  const cmd = currentProject()?.commands.find((c) => c.id === commandId)
  if (!cmd) {
    store.selectedTask = ''
    store.selectedTaskId = null
    return
  }
  store.selectedTask = cmd.name
  store.selectedTaskId = commandId
  // 尚未启动、没有可看会话时不请求日志，避免空闲命令写出 404 失败行
  if (!hasViewableCommandLogs(commandId)) {
    ensureTaskPolling()
    return
  }
  // 冷态已判过 404 的命令直接显示本地留存日志，不再打网络请求（修 B-2）
  if (!logUnavailable.has(commandId) || taskStatusOf(commandId) === 'running') await pullTaskLogs(commandId)
  ensureTaskPolling()
}

export function clearSelectedTask(): void {
  store.selectedTask = ''
  store.selectedTaskId = null
}

/** stopTask 的结果（PILOT-087：每种结果调用方都应给出可见反馈）。 */
export interface StopTaskResult {
  outcome: 'stopped' | 'already-stopped' | 'failed' | 'not-running'
  message: string
}

/**
 * 停止任务（PILOT-087，输入1 2026-11-23）：无论结果如何都有可见反馈（toast + 日志留痕），
 * 并按结果收敛状态——已停止/已不在运行 → 已结束；失败 → 保持运行中可重试。
 * 「已不在运行」不算失败：进程可能已自行退出或宿主/插件重启过（会话表在宿主内存）。
 */
export async function stopTask(commandId: number, taskName: string): Promise<StopTaskResult> {
  if (store.mode === 'demo') {
    // 演示模式无后端：按本地状态收敛并同样给出反馈
    if (taskStatusOf(commandId) !== 'running') {
      toast(`「${taskName}」当前没有运行中的进程，无需停止`)
      return { outcome: 'not-running', message: '演示模式没有运行中的进程' }
    }
    appendLogLines(commandId, ['■ 已停止（演示模式）'])
    markTaskDone(commandId)
    toast('已停止：' + taskName)
    return { outcome: 'stopped', message: 'demo' }
  }

  if (taskStatusOf(commandId) !== 'running') {
    // 点得动就一定给说法；未运行时不发任何请求（AC-F09-5）
    toast(`「${taskName}」当前没有运行中的进程，无需停止`)
    return { outcome: 'not-running', message: '当前没有运行中的进程' }
  }

  try {
    const resp = await apiPost<StopRunResp>(`/api/commands/${commandId}/stop`, {})
    if (resp?.outcome === 'already-stopped') {
      appendLogLines(commandId, ['■ 已不在运行（进程可能已自行退出或宿主重启过），状态已同步为已结束'])
      markTaskDone(commandId)
      toast(`「${taskName}」已不在运行，无需停止`)
      return { outcome: 'already-stopped', message: resp.message ?? '' }
    }
    appendLogLines(commandId, ['■ 已停止：' + (resp?.message || '进程树已终止')])
    markTaskDone(commandId)
    toast('已停止：' + taskName)
    return { outcome: 'stopped', message: resp?.message ?? '' }
  } catch (e) {
    const msg = e instanceof Error ? e.message : String(e)
    appendLogLines(commandId, ['✗ 停止失败：' + msg + '（状态保持运行中，可重试）'])
    toast('停止失败：' + msg)
    return { outcome: 'failed', message: msg }
  }
}

/** 启动全部：没有命令时直接说明，不报「启动完成」。已在运行的跳过。 */
export async function startAllTasks(): Promise<void> {
  const cmds = currentProject()?.commands ?? []
  if (cmds.length === 0) {
    toast('没有可启动的命令，请先添加')
    return
  }
  let started = 0
  let failed = 0
  let lastError = ''
  for (const c of cmds) {
    if (taskStatusOf(c.id) === 'running') continue
    try {
      await apiPost(`/api/commands/${c.id}/run`, {})
      // 与详情区「运行任务」共用同一个状态入口，避免两条启动路径不同步（修 B-1）
      markTaskStarted(c.id, c.name)
      started++
    } catch (e) {
      failed++
      lastError = e instanceof Error ? e.message : String(e)
    }
  }
  await refreshRunningCount()
  if (started === 0 && failed > 0) {
    toast('启动失败：' + lastError)
    return
  }
  toast(`启动完成：新起 ${started} 个，其余已在运行或不可启动`)
}

/** 视图卸载时停止日志轮询，避免插件切走后仍在后台写状态。 */
export function disposeWorkbench(): void {
  if (taskPoll) { clearInterval(taskPoll); taskPoll = undefined }
  // 切走视图后重新进入应重新探测日志可用性，不把冷态结论带到下一段会话
  logUnavailable.clear()
  store.runningCommandId = null
}

/**
 * 端口占用：HTML 原型仪表盘的「端口占用」widget。
 * 数据来源是命令的访问地址 url（真实档案字段），解析出端口；不新增后端接口。
 */
export interface WbPort {
  port: number
  url: string
  commandId: number
  commandName: string
  running: boolean
}

export function projectPorts(p: WbProject | null): WbPort[] {
  if (!p) return []
  const out: WbPort[] = []
  for (const c of p.commands) {
    if (!c.url) continue
    const m = /^[a-z]+:\/\/[^/:]*:(\d+)/i.exec(c.url)
    if (!m) continue
    const port = Number(m[1])
    if (!Number.isFinite(port) || port <= 0) continue
    out.push({
      port,
      url: c.url,
      commandId: c.id,
      commandName: c.name,
      running: taskStatusOf(c.id) === 'running',
    })
  }
  return out.sort((a, b) => a.port - b.port)
}

/**
 * 统一登记「命令已启动」：详情区「运行任务」、维护区「启动」、运行面板「启动全部」
 * 三条路径共用同一入口，避免状态机只在一条路径更新（修 B-1）。
 * 本函数只初始化本地状态并起轮询，不发网络请求；调用时机为后端 run 已发起之后。
 */
export function markTaskStarted(commandId: number, taskName?: string): void {
  const p = currentProject()
  const cmd = p?.commands.find((c) => c.id === commandId)
  if (!cmd) return
  const name = taskName || cmd.name
  store.selectedTask = name
  store.selectedTaskId = commandId
  store.taskStatus[commandId] = 'running'
  store.runningCommandId = commandId

  const bucket = bucketOf(commandId)
  bucket.length = 0
  logCursor[commandId] = 0
  bucket.push('$ ' + cmd.script)
  bucket.push('> 正在 ' + (p?.path ?? '') + ' 启动…')
  store.logs = bucket
  writeRunLogs(store.logsByCommand)

  const history: RunHistoryEntry = store.runHistory[commandId] ?? {
    name, script: cmd.script, status: 'running', startedAt: new Date().toISOString(),
  }
  history.name = name
  history.script = cmd.script
  history.status = 'running'
  history.startedAt = new Date().toISOString()
  delete history.endedAt
  store.runHistory[commandId] = history
  writeRunHistory(store.runHistory)

  // 显式启动后解除冷态 404 标记并恢复轮询，日志继续实时追加
  logUnavailable.delete(commandId)
  ensureTaskPolling()
  void refreshRunningCount()
}

/**
 * 启动被拒绝（如目录不可达 409）：进程没有起来，不算一次运行。
 * 日志只留拒绝原因，状态和起止时间回到启动前，也不再去读日志。
 */
function markTaskStartRejected(
  commandId: number, reason: string, prevStatus: TaskStatus | undefined, prevHistory: RunHistoryEntry | undefined,
): void {
  if (prevStatus) store.taskStatus[commandId] = prevStatus
  else delete store.taskStatus[commandId]
  if (prevHistory) store.runHistory[commandId] = prevHistory
  else delete store.runHistory[commandId]
  writeRunHistory(store.runHistory)
  if (store.runningCommandId === commandId) store.runningCommandId = null
  const bucket = bucketOf(commandId)
  bucket.length = 0
  bucket.push('✗ 启动失败：' + reason)
  writeRunLogs(store.logsByCommand)
  logUnavailable.add(commandId)
  ensureTaskPolling()
}

/** 运行任务：真实启动命令 + 增量日志轮询（对齐 HTML 原型：日志区常驻、实时追加）。 */
export async function runTask(commandId: number, taskName: string): Promise<void> {
  const p = currentProject()
  const cmd = p?.commands.find((c) => c.id === commandId)
  if (!cmd) { toast('未找到该运行命令'); return }
  const prevStatus = store.taskStatus[commandId]
  const prevHistory = store.runHistory[commandId] ? { ...store.runHistory[commandId] } : undefined
  markTaskStarted(commandId, taskName)

  try {
    await apiPost(`/api/commands/${commandId}/run`, {})
    void pullTaskLogs(commandId)
    await refreshRunningCount()
  } catch (e) {
    const reason = e instanceof Error ? e.message : String(e)
    markTaskStartRejected(commandId, reason, prevStatus, prevHistory)
    toast('启动失败：' + reason)
  }
}

/* ------------------------------------------------------------------ *
 * 派生视图数据：把 sems 真实实体映射成工作台 5 个模块页的形状
 * ------------------------------------------------------------------ */

export function visibleEngineeringPlugins(): EngineeringPlugin[] {
  return filterEngineeringPlugins(store.engineeringPlugins, store.pluginFilter)
}

export async function loadEngineeringPlugins(projectId?: number): Promise<void> {
  const id = projectId ?? store.currentProjectId
  if (id == null) {
    store.engineeringPlugins = []
    store.engineeringPluginsError = ''
    store.engineeringPluginsLoading = false
    return
  }
  store.engineeringPluginsLoading = true
  try {
    const raw = await apiGet<unknown>(`/api/projects/${id}/engineering-plugins`)
    const parsed = parseEngineeringPluginsPayload(raw)
    store.engineeringPlugins = parsed.plugins
    store.engineeringPluginsError = parsed.error
  } catch (e) {
    store.engineeringPlugins = []
    store.engineeringPluginsError = e instanceof Error ? e.message : String(e)
  } finally {
    store.engineeringPluginsLoading = false
  }
}

export async function loadSpecCount(projectId?: number): Promise<void> {
  const id = projectId ?? store.currentProjectId
  if (id == null) {
    store.specCount = 0
    return
  }
  try {
    const specs = await listSpecs(id)
    store.specCount = Array.isArray(specs) ? specs.length : 0
  } catch {
    store.specCount = 0
  }
}

const MAIN_HINT = /(serve|dev|start|run|preview|build)/i
/** 依赖屏：运行命令按「主命令 / 开发辅助」分组，wanted=脚本，latest=最近更新。 */
export function projectDeps(p: WbProject | null): { main: WbDep[]; dev: WbDep[] } {
  if (!p) return { main: [], dev: [] }
  const q = store.depFilter.trim().toLowerCase()
  const main: WbDep[] = []
  const dev: WbDep[] = []
  for (const c of p.commands) {
    if (!c.name.toLowerCase().includes(q) && !c.script.toLowerCase().includes(q)) continue
    const item: WbDep = {
      id: c.id, name: c.name, wanted: c.script,
      latest: c.updatedAt || c.createdAt || '—', scope: MAIN_HINT.test(c.name) ? 'main' : 'dev', url: c.url,
    }
    ;(item.scope === 'main' ? main : dev).push(item)
  }
  return { main, dev }
}

/** 任务屏：运行命令即任务；状态来自当前轮询 / 会话。 */
export function projectTasks(p: WbProject | null): WbTask[] {
  if (!p) return []
  const q = store.taskFilter.trim().toLowerCase()
  return p.commands
    .filter((c) => c.name.toLowerCase().includes(q) || c.script.toLowerCase().includes(q))
    .map((c) => ({
      id: c.id,
      name: c.name,
      description: c.script,
      script: c.script,
      status: taskStatusOf(c.id),
      url: c.url,
    }))
}

/**
 * 配置屏：只保留真实生效的工作台显示偏好（localStorage），与后端档案字段分离。
 * 原有 autoRefresh / confirmDangerous / defaultCommandPrefix 三项没有任何消费点，
 * 属「写了不生效」的假控件，按 FR-3 不做假能力删除（修 D-2）。
 */
export function projectConfigs(p: WbProject | null): Record<string, boolean | string> {
  if (!p) return {}
  const saved = meta.configs[p.id] ?? {}
  return { showPathBadge: saved.showPathBadge ?? true }
}

export function setProjectConfig(p: WbProject | null, key: string, value: boolean | string) {
  if (!p) return
  meta.configs[p.id] = { ...(meta.configs[p.id] ?? {}), [key]: value }
  writeMeta()
}

/**
 * 反向适配：把工作台项目转成后端 ProjectInfo 形状，
 * 供原 sems 组件（ProjectEditDialog / ProjectCard / CommandList）直接复用，无需改它们。
 */
export function toProjectInfo(p: WbProject | null): ProjectInfo | null {
  if (!p) return null
  return {
    id: p.id,
    root: p.path,
    name: p.name,
    type: p.type,
    description: p.description,
    tags: p.tags,
    source: p.source,
    createdAt: '',
    updatedAt: '',
    lastActiveAt: p.openDate,
    pathExists: p.pathExists,
    isGitRepo: p.isGitRepo,
    commands: p.commands ?? [],
  }
}

export function resetDemo(): void {
  store.projects = demoProjects()
  store.currentProjectId = null
  store.mode = 'demo'
  store.filter = ''
  writeMeta()
  go('/projects')
  toast('已恢复示例数据（仅本地视图）')
}
