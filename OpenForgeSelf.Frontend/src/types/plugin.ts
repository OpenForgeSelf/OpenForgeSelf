/**
 * 插件类型定义
 */

export enum PluginState {
  NotLoaded = 'NotLoaded',
  Loaded = 'Loaded',
  Initialized = 'Initialized',
  Starting = 'Starting',
  Running = 'Running',
  Stopping = 'Stopping',
  Stopped = 'Stopped',
  Destroying = 'Destroying',
  Destroyed = 'Destroyed',
  Error = 'Error'
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
