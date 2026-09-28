/**
 * 插件类型定义
 */

/**
 * 插件状态枚举 —— 必须与后端 `PluginState`（数字枚举）顺序一致：
 * 后端 `PluginState.cs`（Plugins/Abstractions）为默认数字枚举 0-9，
 * JSON 序列化后 `state` 为数字（0=NotLoaded … 9=Error）。
 * 曾误用字符串枚举导致 `state.toLowerCase()` 渲染崩溃（2026-09-22 修复）。
 */
export enum PluginState {
  NotLoaded = 0,
  Loaded = 1,
  Initialized = 2,
  Starting = 3,
  Running = 4,
  Stopping = 5,
  Stopped = 6,
  Destroying = 7,
  Destroyed = 8,
  Error = 9
}

export enum PluginPermission {
  ReadSettings = 'read:settings',
  WriteSettings = 'write:settings',
  ReadData = 'read:data',
  WriteData = 'write:data',
  NetworkAccess = 'network:access',
  FileSystem = 'file:system',
  RegisterMenu = 'register:menu',
  RegisterTool = 'register:tool',
  RegisterRoute = 'register:route'
}

export enum PluginSortBy {
  Name = 'Name',
  InstallCount = 'InstallCount',
  UpdatedAt = 'UpdatedAt',
  Rating = 'Rating'
}

export interface PluginInfo {
  id: string
  name: string
  version: string
  author: string
  description: string
  iconUrl?: string
  state: PluginState
  isEnabled: boolean
  category: string
  tags: string[]
  installCount: number
  rating: number
  updatedAt?: string
  hasUpdate?: boolean
}

export interface PluginDetail extends PluginInfo {
  dependencies: string[]
  permissions: string[]
  extensionPoints: string[]
  usageStats?: PluginUsageStats
  screenshots: string[]
  homepageUrl: string
  repositoryUrl: string
  license: string
  releaseNotes: string
  hasUpdate: boolean
  latestVersion?: string
}

export interface PluginExtension {
  type: 'menu' | 'tool' | 'route' | 'settings'
  count: number
}

export interface PluginUsageStats {
  totalUsage: number
  todayUsage: number
  averageDailyUsage: number
  lastUsedAt?: string
}

export interface PluginMenuItem {
  id: string
  name: string
  icon?: string
  path: string
  order: number
  parentId?: string
  pluginId: string
  children?: PluginMenuItem[]
}

export interface PluginToolFunction {
  id: string
  name: string
  description: string
  pluginId: string
  parametersSchema: Record<string, unknown>
}

export interface PluginCategory {
  name: string
  displayName: string
  count: number
  icon: string
}

export interface PluginSearchResult {
  items: PluginInfo[]
  total: number
  page: number
  pageSize: number
}

export interface PluginSearchParams {
  keyword?: string
  category?: string
  sortBy?: string
  page?: number
  pageSize?: number
}

export interface PluginVersionInfo {
  version: string
  releasedAt?: string
  releaseNotes: string
}

export interface PluginUpdateInfo {
  pluginId: string
  pluginName: string
  currentVersion: string
  latestVersion: string
  hasUpdate: boolean
  /** 更新来源：'backup'（已暂存备份）或 'package'（插件更新源本地包目录，2026-09-28 输入27） */
  source?: string
}

/** 插件更新源配置（2026-09-28，输入27）：localDir 空字符串 = 未配置/停用 */
export interface PluginUpdateSettings {
  localDir: string
}

export interface PluginBackupInfo {
  backupId: string
  version: string
  createdAt: string
  size: number
}

export interface PluginTemplateInfo {
  id: string
  name: string
  description: string
  icon: string
  pluginType: string
  tags?: string[]
  files?: string[]
}

export interface ScaffoldRequest {
  name: string
  id?: string
  description?: string
  author?: string
  version?: string
  pluginType: string
}

export interface PluginListParams {
  keyword?: string
  isEnabled?: boolean
  category?: string
}

export interface PluginSettings {
  [key: string]: unknown
}

/**
 * 插件前端贡献协议（与后端 PluginMetadata.Frontend 对应）。
 * 声明插件希望前端渲染的视图、菜单、路由与图标。
 */
export interface FrontendContributes {
  views: string[]
  menu?: string
  route?: string
  icon?: string
  /**
   * 插件自带界面资源入口的相对路径（如 `frontend/index.js`），对应后端 Frontend.Entry。
   * 为空/undefined 表示插件不自带界面资源，前端走既有主包内组件映射（向后兼容）。
   */
  entry?: string
}

/**
 * 前端插件清单条目（对应 GET /api/plugin/frontend-manifest）。
 */
export interface PluginFrontendManifest {
  id: string
  name: string
  /**
   * 插件版本，用于界面资源 URL 的缓存标识（?v={version}）。
   * 为 010 新增字段，声明为可选以兼容既有清单消费方与测试夹具。
   */
  version?: string
  /**
   * 界面资源缓存标识（内容指纹）：后端基于 web/dist 入口与样式内容计算的短哈希。
   * 内容变化即变化，无需升版本即可让浏览器刷新取到新界面。
   * 前端拼装 JS/CSS 资源 URL 时优先用此值，回退到 {@link version}。
   */
  webVersion?: string
  frontend: FrontendContributes | null
  isEnabled: boolean
}

/**
 * 由清单归一化出的菜单贡献条目（store 的 menus getter 输出）。
 * 供 features.ts 做「内置特性 + 运行期清单补充」合并。
 */
export interface PluginMenuContribution {
  id: string
  name: string
  menu: string
  route: string | null
  icon: string | null
  views: string[]
}
