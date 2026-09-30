/**
 * sems 界面共享类型。字段与后端 DTO 对应（JSON 序列化后为 camelCase）。
 * 后端契约见 ForgeSelf.Abstractions/IProjectRegistry.cs 与批2 控制器。
 */

/** 一条项目记录（来自 GET /api/projects，后端共享契约 ProjectInfo）。 */
export interface ProjectRecord {
  root?: string
  name?: string
  source?: string
  selectedAt?: string
  lastActivityAt?: string
  /** 根目录当前是否可达（后端计算）。 */
  pathExists?: boolean
  /** 是否为 git 仓库（后端计算）。 */
  isGitRepo?: boolean
}

/** 项目类型预设（design §3.1，也允许自定义文本）。 */
export type ProjectType =
  | 'frontend'
  | 'backend'
  | 'fullstack'
  | 'library'
  | 'tool'
  | 'other'
  | (string & {})

/** 项目档案（后端 ProjectInfo，GET /api/projects 条目）。 */
export interface ProjectInfo {
  id: number
  root: string
  name: string
  type: string
  description: string
  /** 逗号分隔标签（渲染层 split）。 */
  tags: string
  source: string
  createdAt: string
  updatedAt: string
  lastActiveAt: string
  pathExists: boolean
  isGitRepo: boolean
  /** 该项目下的运行命令列表（Get/GetAll 时附带）。 */
  commands: RunCommandInfo[]
}

/** 项目档案编辑请求体（PUT /api/projects/{id}）。 */
export interface ProjectUpdate {
  name?: string
  type?: string
  description?: string
  /** 逗号分隔字符串。 */
  tags?: string
}

/** 运行命令信息（后端 RunCommandInfo）。 */
export interface RunCommandInfo {
  id: number
  projectId: number
  name: string
  script: string
  /** 运行后访问地址（可空）。 */
  url?: string | null
  sort: number
  createdAt: string
  updatedAt: string
}

/** 新增命令请求体（POST /api/projects/{id}/commands）。 */
export interface RunCommandAdd {
  projectId: number
  name: string
  script: string
  url?: string | null
  sort?: number
}

/** 命令编辑请求体（PUT /api/commands/{id}）。 */
export interface RunCommandUpdate {
  name?: string
  script?: string
  url?: string | null
  sort?: number
}

/** 运行会话（GET /api/runs、POST /api/runs/check，后端 RunSession）。 */
export interface RunSession {
  /** Detected 条目无命令时取 0。 */
  commandId: number
  projectId: number
  projectName: string
  /** Detected 条目为可执行文件名。 */
  commandName: string
  pid: number
  /** Detected 条目为 MinValue，UI 判空。 */
  startedAt: string
  /** Launched（本面板启动） / Detected（WMI 检测捕获）。 */
  origin: 'Launched' | 'Detected' | string
}

/** GET /api/projects 响应。 */
export interface ProjectsResp {
  total: number
  projects: ProjectInfo[]
}

/** GET /api/projects/{id}/commands 响应。 */
export interface CommandsResp {
  commands: RunCommandInfo[]
}

/** GET /api/runs、POST /api/runs/check 响应。 */
export interface RunsResp {
  total: number
  runs: RunSession[]
}

/** 目录浏览条目（GET /api/projects/browse，后端 DirectoryEntry）。 */
export interface DirectoryEntry {
  /** 目录名；列举驱动器根时为盘符（如 "C:\\"）。 */
  name: string
  /** 绝对路径。 */
  path: string
}

/** GET /api/projects/browse 响应。 */
export interface BrowseResp {
  /** 当前目录；列举驱动器根时为 null。 */
  path: string | null
  /** 上级目录；已无上级时为 null。 */
  parent: string | null
  directories: DirectoryEntry[]
}

/** POST /api/projects 请求体（插件内部登记项目）。 */
export interface RegisterProjectReq {
  /** 已存在的目录绝对路径。 */
  root: string
  /** 显示名（可选，留空取目录名）。 */
  name?: string | null
}

/** POST /api/projects 响应。 */
export interface RegisterResp {
  project: ProjectInfo
}

/** GET /api/plugin 条目（取自身版本号用，铁律 13）。 */
export interface PluginRow {
  id: string
  version: string
}
