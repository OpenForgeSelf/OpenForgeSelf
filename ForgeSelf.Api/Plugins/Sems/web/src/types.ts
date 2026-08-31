/**
 * sems 界面共享类型。字段与后端 DTO 对应（JSON 序列化后为 camelCase）。
 */

/** 一条项目记录（来自 GET /api/projects，后端共享契约 ProjectRecord）。 */
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